using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        void ShowAbilityCard(OwnedUnit owned,bool enemy,bool collection,int initialSelection=-1,Action returnToPreview=null)
        {
            CloseModal();inspected=owned;inspectedEnemy=enemy;
            Action close=returnToPreview??CloseModal;
            modal=Panel(CanvasRoot,collection?"Character details":"Inspection shade",new Rect(0,0,540,960),new Color(.01f,.04f,.09f,collection?.65f:.10f));
            modal.gameObject.AddComponent<Button>().onClick.AddListener(()=>close());
            float top=collection?264:0;
            bool battleInfo=!collection&&Match.Phase==MatchPhase.Combat;
            // Four live rows need 94 px with the current font and line spacing.
            // Reserve 112 px plus separation from the skill tiles below.
            float statsOffset=battleInfo?96:16;
            float sheetHeight=556+top+statsOffset;
            // Keep all ability rows and the footer above the existing menu navigation.
            float sheetY=collection?Mathf.Min(110,LobbyFooterTop-12-sheetHeight):Mathf.Min(374,944-sheetHeight);
            var box=BluePanel(modal,"Unit sheet",new Rect(16,sheetY,508,sheetHeight),paper);
            AnimateSheet(box);
            box.gameObject.AddComponent<Button>().transition=Selectable.Transition.None;
            if(collection)
            {
                var stage=BluePanel(box,"Figure stage",new Rect(12,12,484,240),new Color(.07f,.36f,.58f));
                // Public character previews display only the authored idle.
                // Lobby gestures remain available in their own lobby/QA paths.
                Figure(stage,owned.Definition.Id,new Rect(132,8,220,224),true);
            }
            var baseTile=BluePanel(box,"Base ability",new Rect(12,top+12,68,68),brightBlue);
            AbilityIllustration(baseTile,AbilityUiCatalog.Get(owned.Definition.Id+"/basic"),owned.Definition.Id+"/basic",owned.Definition.Id,new Rect(4,4,60,60));
            Label(box,owned.Definition.DisplayName.ToUpperInvariant(),new Rect(92,top+14,324,31),26,Color.black,FontStyle.Bold,TextAnchor.MiddleLeft);
            var stats=MonsterPouch.Gameplay.Units.UnitStats.FromDefinition(owned.Definition,owned,false);
            var info=Label(box,battleInfo?StatsText(owned):BattleInfoText.Summary(stats,GameText.Spanish),
                new Rect(92,top+48,324,battleInfo?112:40),battleInfo?18:17,Color.black,FontStyle.Normal,TextAnchor.UpperLeft);
            info.name=battleInfo?"Battle live stats":"Character base stats";
            if(battleInfo)inspectStats=info;
            GameButton(box,returnToPreview==null?"×":"‹",new Rect(428,top+12,68,68),()=>close(),false,28).name=returnToPreview==null?"CERRAR":"character-details-back";
            top+=statsOffset;
            var detail=BluePanel(box,"Selected ability",new Rect(260,top+176,236,252),new Color(.75f,.90f,1));
            var detailIcon=Panel(detail,"Illustration",new Rect(10,10,56,56),Color.clear,false);
            var title=Label(detail,"",new Rect(76,8,148,60),20,Color.black,FontStyle.Bold,TextAnchor.MiddleLeft);
            var viewport=HitArea(detail,"Description viewport",new Rect(12,80,212,160));
            viewport.gameObject.AddComponent<RectMask2D>();
            var description=Label(viewport,"",new Rect(0,0,212,160),20,Color.black,FontStyle.Normal,TextAnchor.UpperLeft);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=description.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            viewport.GetComponent<Image>().raycastTarget=true;
            var status=Label(box,"",new Rect(12,top+520,484,24),14,Color.black,FontStyle.Normal,TextAnchor.MiddleCenter);
            status.name="Ability purchase status";
            // Unity can materialize an absent serializable optional ability as an
            // empty object. Only real effect identities get a passive tile.
            bool hasPassive=owned.Definition.PassiveAbility!=null&&
                (!string.IsNullOrWhiteSpace(owned.Definition.PassiveAbility.EffectId)||!string.IsNullOrWhiteSpace(owned.Definition.PassiveAbility.Id))&&
                !AbilityBalance.IsRetiredArmorEffect(owned.Definition.PassiveAbility.EffectId??owned.Definition.PassiveAbility.Id);
            bool hasBase=owned.Definition.BaseAbility!=null&&!AbilityBalance.IsRetiredArmorEffect(owned.Definition.BaseAbility.EffectId??owned.Definition.BaseAbility.Id);
            int selected=Mathf.Clamp(initialSelection,-3,2);float lastTap=-10;
            if(selected==-3&&!hasPassive)selected=-1;
            if(selected>=0&&AbilityBalance.IsRetiredArmorEffect(owned.Definition.Tricks[selected]?.EffectId??owned.Definition.Tricks[selected]?.Id))selected=-1;
            if(selected==-1&&!hasBase)selected=-2;
            var skillTiles=new System.Collections.Generic.Dictionary<int,RectTransform>();
            var cards=new RectTransform[3];
            bool permanentCollection=collection&&!enemy&&Match!=null&&Match.Phase==MatchPhase.Menu&&
                guestProfile?.CollectionMode==GuestCollectionMode.StarterInventory;
            GuestAbilityPurchaseResult permanentPreview=null;
            var buy=GameButton(box,"",new Rect(12,top+440,484,68),()=>Purchase(),true,18);
            buy.name="ability-upgrade";
            bool ShowPurchaseState(int index)=>index>=0&&Match!=null&&Match.Phase!=MatchPhase.Menu;
            bool CanPurchase(int index,out int slot)
            {
                slot=-1;
                return index>=0&&!AbilityBalance.IsRetiredArmorEffect(owned.Definition.Tricks[index]?.EffectId??owned.Definition.Tricks[index]?.Id)&&!collection&&!enemy&&Match!=null&&Match.CanPurchaseAbility(owned,index,out slot);
            }
            void RefreshPurchaseStatus(int index,bool canPurchase)
            {
                status.text=GameText.Resolve(collection?"Explora sus habilidades":enemy?"Habilidades del rival":
                    index<0?(GameText.Spanish?"Elige un Trick":"Choose a Trick"):
                    owned.Tricks[index]?(GameText.Spanish?"Trick adquirido":"Trick purchased"):
                    canPurchase?(GameText.Spanish?"Pulsa COMPRAR TRICK":"Tap BUY TRICK"):
                    Match?.PurchaseAbilityUnavailableReason(owned,index)??"Mejora ya adquirida o no disponible.");
            }
            bool InspectPurchase(int index)
            {
                bool can=CanPurchase(index,out _);
                // The eligibility callback runs on refresh, even if the bool remains false.
                // A changed budget/phase must not leave a stale reason on the selected card.
                if(!permanentCollection&&selected==index)RefreshPurchaseStatus(index,can);
                return can;
            }
            void Purchase()
            {
                if(permanentCollection)
                {
                    if(selected>=0)ConfirmPermanentAbility(owned,selected,returnToPreview);
                    return;
                }
                if(!CanPurchase(selected,out int slot))return;
                bool bought;
                if(owned.Definition.IsMonster)bought=Match.UpgradeMonster(selected);
                else
                {
                    bought=Match.BuyTrick(owned.Definition.Id,selected,slot,Match.Player.Offers[slot].Token);
                }
                if(bought)ShowAbilityCard(owned,false,false,selected);
            }
            void Preview(int index)
            {
                var ability=index==-3?owned.Definition.PassiveAbility:index<0?owned.Definition.BaseAbility:owned.Definition.Tricks[index];
                bool basic=index==-2;
                title.text=basic?(AbilityUiCatalog.Title(owned.Definition.Id+"/basic",GameText.Spanish)??(GameText.Spanish?"Ataque básico":"Basic attack")):GameText.AbilityName(ability);
                description.text=basic?BasicAttackDetailText(owned):AbilityDetailText(owned,ability);
                description.rectTransform.sizeDelta=new Vector2(212,Mathf.Max(160,description.preferredHeight+4));
                scroll.verticalNormalizedPosition=1;
                foreach(Transform child in detailIcon){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                if(basic)AbilityIllustration(detailIcon,AbilityUiCatalog.Get(owned.Definition.Id+"/basic"),owned.Definition.Id+"/basic",owned.Definition.Id,new Rect(0,0,56,56));
                else AbilityIllustration(detailIcon,ability,owned.Definition.Id,new Rect(0,0,56,56));
                foreach(var pair in skillTiles)pair.Value.GetComponent<Image>().color=pair.Key==index?new Color(1,.84f,.38f):new Color(.62f,.83f,1);
                for(int j=0;j<3;j++)if(cards[j]!=null)cards[j].GetComponent<Image>().color=
                    j==index?new Color(1,.84f,.38f):owned.Tricks[j]?new Color(.58f,.87f,.72f):new Color(.62f,.83f,1);
                bool locked=owned.Definition.IsMonster&&owned.TrickCount>0&&index>=0&&!owned.Tricks[index];
                bool active=index>=0&&owned.Tricks[index];
                if(collection&&index>=0&&!ObserveEconomyTutorialView("ability"))return;
                if(permanentCollection&&index>=0)permanentPreview=PreviewPermanentAbility(owned.Definition,index);
                else permanentPreview=null;
                var availability=detailIcon.GetComponent<AbilityAvailabilityVisual>()??detailIcon.gameObject.AddComponent<AbilityAvailabilityVisual>();
                availability.Configure(()=>permanentCollection?CanReviewPermanentAbility(permanentPreview):InspectPurchase(index),can=>buy.GetComponent<Button>().interactable=can,
                    ()=>ShowPurchaseState(index));
                if(permanentCollection)
                {
                    SetPermanentAbilityButton(buy,index,permanentPreview);
                    status.text=index<0?(GameText.Spanish?"Habilidad base automática":"Automatic base ability"):
                        PermanentAbilityStatus(permanentPreview?.Status);
                }
                else
                {
                    SetMoonkenButtonCost(buy,active?(GameText.Spanish?"TRICK ACTIVO":"ACTIVE TRICK"):locked?(GameText.Spanish?"BLOQUEADO":"LOCKED"):index<0?(GameText.Spanish?"HABILIDAD":"ABILITY"):collection||enemy?"TRICK":(GameText.Spanish?"COMPRAR TRICK":"BUY TRICK"),
                        index>=0&&!active&&!locked?(int?)ability.Cost:null);
                    RefreshPurchaseStatus(index,CanPurchase(index,out _));
                }
            }
            int trickRow=0;
            for(int i=0;i<3;i++)
            {
                int index=i;var ability=owned.Definition.Tricks[i];if(ability==null||AbilityBalance.IsRetiredArmorEffect(ability.EffectId??ability.Id))continue;
                var tile=BluePanel(box,"ability-"+i,new Rect(12,top+176+trickRow++*88,236,76),new Color(.62f,.83f,1));cards[i]=tile;
                var illustration=AbilityIllustration(tile,ability,owned.Definition.Id,new Rect(8,12,52,52));
                // Only the illustration reflects affordability; labels, costs and the card keep their colors.
                var artwork=illustration.GetComponentsInChildren<Graphic>();
                Label(tile,GameText.AbilityName(ability),new Rect(68,8,114,60),17,Color.black,FontStyle.Bold,TextAnchor.MiddleLeft);
                if(permanentCollection)PermanentAbilityBadge(tile,owned.Definition,ability.Id,new Rect(188,18,40,40));
                else if(owned.Tricks[i]){var check=Label(tile,"✓",new Rect(188,18,40,40),25,blueFrame,FontStyle.Bold);check.font=symbolFont;}
                else MoonkenCost(tile,ability.Cost,new Rect(188,18,40,40));
                tile.gameObject.AddComponent<Button>().onClick.AddListener(()=>{
                    float now=Time.unscaledTime;bool confirm=selected==index&&now-lastTap<=.65f;
                    selected=index;lastTap=now;Preview(index);if(confirm&&!collection)Purchase();
                });
                tile.gameObject.AddComponent<AbilityAvailabilityVisual>().Configure(()=>CanPurchase(index,out _),
                    purchaseStateVisible:()=>ShowPurchaseState(index),artwork:artwork);
            }
            void SkillTile(int index,string caption,TrickDefinition ability,int position,int count)
            {
                float width=(484-(count-1)*8f)/count;
                var tile=BluePanel(box,"skill-slot-"+index,new Rect(12+position*(width+8),top+88,width,76),new Color(.62f,.83f,1));
                skillTiles[index]=tile;
                if(index==-2)AbilityIllustration(tile,AbilityUiCatalog.Get(owned.Definition.Id+"/basic"),owned.Definition.Id+"/basic",owned.Definition.Id,new Rect(6,10,52,52));
                else AbilityIllustration(tile,ability,owned.Definition.Id,new Rect(6,10,52,52));
                Label(tile,caption,new Rect(64,8,width-70,60),16,Color.black,FontStyle.Bold,TextAnchor.MiddleLeft);
                tile.gameObject.AddComponent<Button>().onClick.AddListener(()=>{selected=index;lastTap=-10;Preview(index);});
            }
            int skillCount=1+(hasPassive?1:0)+(hasBase?1:0);
            SkillTile(-2,GameText.Spanish?"Básico":"Basic",null,0,skillCount);
            if(hasPassive)SkillTile(-3,GameText.Spanish?"Pasiva":"Passive",owned.Definition.PassiveAbility,1,skillCount);
            if(hasBase)SkillTile(-1,owned.Definition.BaseAbility?.Trigger==AbilityTrigger.Energy?(GameText.Spanish?"Súper":"Super"):
                (GameText.Spanish?"Pasiva":"Passive"),owned.Definition.BaseAbility,skillCount-1,skillCount);
            baseTile.gameObject.AddComponent<Button>().onClick.AddListener(()=>{selected=-2;lastTap=-10;Preview(-2);});
            baseTile.gameObject.AddComponent<AbilityAvailabilityVisual>().Configure(()=>false,purchaseStateVisible:()=>false);
            Preview(selected);
        }
        Sprite MenuItem(string id)=>Resources.Load<Sprite>("MonsterPouch/UI/MenuItems/"+id);
    }
}

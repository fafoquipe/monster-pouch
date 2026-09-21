using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        void ShowAbilityCard(OwnedUnit owned,bool enemy,bool collection,int initialSelection=-1)
        {
            CloseModal();inspected=owned;inspectedEnemy=enemy;
            modal=Panel(CanvasRoot,collection?"Character details":"Inspection shade",new Rect(0,0,540,960),new Color(.005f,.025f,.08f,.82f));
            var box=BluePanel(modal,"Unit sheet",new Rect(28,54,484,848),paper);
            var stage=Panel(box,"Figure stage",new Rect(8,8,468,280),new Color(.04f,.62f,.83f));
            Figure(stage,owned.Definition.Id,new Rect(108,15,252,245));
            GameButton(box,"×",new Rect(426,12,44,44),CloseModal,false,26);
            Label(box,owned.Definition.DisplayName.ToUpperInvariant(),new Rect(18,294,448,40),29,blueFrame,FontStyle.Bold);
            inspectStats=Label(box,StatsText(owned),new Rect(22,340,440,49),16,blueFrame,FontStyle.Bold);
            var detail=Panel(box,"Selected ability",new Rect(16,406,452,153),new Color(.76f,.88f,.88f));
            var largeIcon=BluePanel(detail,"Ability icon",new Rect(9,14,84,84),brightBlue);
            Image(largeIcon,MenuItem("batalla"),new Rect(7,7,70,70));
            var icon=largeIcon.GetComponentsInChildren<Image>().Last();
            var title=Label(detail,"",new Rect(105,9,336,33),24,blueFrame,FontStyle.Bold,TextAnchor.MiddleLeft);
            var description=Label(detail,"",new Rect(105,46,333,101),20,blueFrame,FontStyle.Normal,TextAnchor.UpperLeft);
            var status=Label(box,"",new Rect(22,737,440,40),16,blueFrame,FontStyle.Normal);
            int selected=initialSelection;float lastTap=-10;
            var cards=new RectTransform[3];
            void Preview(int index)
            {
                var ability=index<0?owned.Definition.BaseAbility:owned.Definition.Tricks[index];
                title.text=ability?.Name ?? "Invocación";
                string trigger=index>=0?"MEJORA":ability?.Trigger==AbilityTrigger.Energy?"SÚPER":ability?.Trigger==AbilityTrigger.EveryAttacks?"ATAQUE":"PASIVA";
                description.text=trigger+" · "+AbilityBalance.Description(ability,owned.Definition);
                icon.sprite=AbilityItem(ability);
                for(int j=0;j<3;j++)if(cards[j]!=null)cards[j].GetComponent<Image>().color=
                    j==index?new Color(.99f,.75f,.16f):owned.Tricks[j]?new Color(.2f,.73f,.51f):brightBlue;
                bool locked=owned.Definition.IsMonster&&owned.TrickCount>0&&index>=0&&!owned.Tricks[index];
                status.text=collection?"Explora sus habilidades":enemy?"Habilidades del rival":locked?"Este héroe ya eligió su mejora":index>=0&&owned.Tricks[index]?"MEJORA ACTIVA":
                    Match.Phase!=MatchPhase.Preparation?"Disponible durante preparación":owned.Definition.IsMonster?"1 mejora por héroe · Doble toque para comprar":"Doble toque para comprar una mejora";
            }
            for(int i=0;i<3;i++)
            {
                int index=i;var ability=owned.Definition.Tricks[i];
                if(ability==null)continue;
                var tile=BluePanel(box,"ability-"+i,new Rect(18+i*153,579,142,102),brightBlue);cards[i]=tile;
                Image(tile,AbilityItem(ability),new Rect(8,12,76,76));
                Image(tile,MoonIcon,new Rect(91,26,30,30));
                Label(tile,owned.Tricks[i]?"✓":ability.Cost.ToString(),new Rect(89,60,39,28),21,Color.white,FontStyle.Bold);
                Label(box,ability.Name,new Rect(16+i*153,686,144,43),16,blueFrame,FontStyle.Bold);
                int offerSlot=-1;
                if(Match.Player!=null)for(int s=0;s<Match.Player.Offers.Count;s++)if(Match.Player.Offers[s]?.UnitId==owned.Definition.Id){offerSlot=s;break;}
                long offerToken=offerSlot<0?0:Match.Player.Offers[offerSlot].Token;
                tile.gameObject.AddComponent<Button>().onClick.AddListener(()=>{
                    float now=Time.unscaledTime;bool confirm=selected==index&&now-lastTap<=.65f;
                    selected=index;lastTap=now;Preview(index);
                    if(!confirm||collection||enemy||Match.Phase!=MatchPhase.Preparation||owned.Tricks[index])return;
                    bool bought=false;
                    if(owned.Definition.IsMonster)bought=Match.UpgradeMonster(index);
                    else if(owned.Copies>0&&offerSlot>=0)bought=Match.BuyTrick(owned.Definition.Id,index,offerSlot,offerToken);
                    else {status.text="Necesitas una copia en el Brief";return;}
                    if(bought)ShowAbilityCard(owned,false,false,index);else status.text=Match.Notice;
                });
            }
            largeIcon.gameObject.AddComponent<Button>().onClick.AddListener(()=>{selected=-1;lastTap=-10;Preview(-1);});
            GameButton(box,collection?"VOLVER A FIGURAS":"CERRAR",new Rect(24,788,436,44),collection?(Action)OpenCollection:CloseModal,false,17);
            Preview(selected);
        }

        Sprite MenuItem(string id)=>Resources.Load<Sprite>("MonsterPouch/UI/MenuItems/"+id);
        Sprite AbilityItem(TrickDefinition ability)
        {
            string id=ability?.EffectId??"";
            if(id.Contains("impact")||id.Contains("burst")||id.Contains("damage"))return MenuItem("batalla");
            if(id.Contains("jump")||id.Contains("flight")||id.Contains("backstep"))return MenuItem("guia");
            if(id.Contains("heal")||id.Contains("revive")||id.Contains("summon")||id.Contains("flower"))return MenuItem("figuras");
            if(id.Contains("income")||id.Contains("steal")||id.Contains("energy")||id.Contains("gift"))return MenuItem("brief");
            if(id.Contains("rhythm")||id.Contains("speed")||id.Contains("stun")||id.Contains("storm"))return MenuItem("sonido");
            if(id.Contains("immunity")||id.Contains("stealth")||id.Contains("health")||id.Contains("protection"))return MenuItem("guia");
            return MenuItem("batalla");
        }
    }
}

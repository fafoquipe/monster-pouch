using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        void ReturnToCharacterPicker(int pickerSlot,bool monsters)
        {
            if(pickerSlot==-2)ShowCollectionPage(monsters);
            else OpenCharacterPicker(pickerSlot);
        }

        void ShowCharacterPreview(UnitDefinition definition,int pickerSlot,bool monsters,int? selectedSlot=null)
        {
            if(Match.Phase!=MatchPhase.Menu||definition==null)return;
            int? destination=pickerSlot==-2?selectedSlot:pickerSlot;
            if(definition.IsMonster)destination=-1;
            if(modal==null||modal.name!="Character selection")ReturnToCharacterPicker(pickerSlot,monsters);
            var grid=modal.Find("Collection panel/Collection viewport/Collection grid") as RectTransform;
            if(grid!=null)grid.parent.GetComponent<CollectionGridWindow>()?.EnsureVisible(definition.Id);
            var card=grid!=null?grid.Find("loadout-"+definition.Id) as RectTransform:null;
            if(card==null)return;
            if(collectionActions!=null){collectionActions.gameObject.SetActive(false);Destroy(collectionActions.gameObject);}
            collectionActions=Panel(grid,"Character actions",new Rect(card.anchoredPosition.x,-card.anchoredPosition.y+124,112,148),new Color(.12f,.14f,.16f));
            var details=Button(collectionActions,GameText.Spanish?"DETALLE":"DETAIL",new Rect(4,4,104,68),()=>
                ShowAbilityCard(new OwnedUnit(definition),false,true,-1,
                    ()=>ShowCharacterPreview(definition,pickerSlot,monsters,destination)),new Color(.20f,.73f,.94f),Color.white,20);
            details.name="character-preview-details";
            var use=Button(collectionActions,GameText.Spanish?"USAR":"USE",new Rect(4,76,104,68),()=>
            {
                if(!destination.HasValue){ShowCharacterDestinationPicker(definition,pickerSlot,monsters);return;}
                if(Match.Phase!=MatchPhase.Menu||!decks.Assign(destination.Value,definition.Id,Match.Config,AvailableWhelpIdsForDeck,AvailableMonsterIdsForDeck))return;
                ApplyDeck();PersistDeck();CloseModal();RenderPage();
                if(pickerSlot==-2)ReturnToCharacterPicker(pickerSlot,monsters);
            },new Color(.26f,.76f,.19f),Color.white,21);
            use.name="character-preview-use";
            var available=definition.IsMonster?AvailableMonsterIdsForDeck:AvailableWhelpIdsForDeck;
            use.GetComponent<Button>().interactable=available==null||available.Contains(definition.Id);
            // Keep both touch targets visible even in the collection's last row.
            float bottom=-collectionActions.anchoredPosition.y+148;
            var scroll=grid.parent.GetComponent<ScrollRect>();scroll.StopMovement();
            float offset=Mathf.Max(grid.anchoredPosition.y,bottom-scroll.viewport.rect.height);
            grid.anchoredPosition=new Vector2(0,Mathf.Clamp(offset,0,grid.rect.height-scroll.viewport.rect.height));
        }

        void ShowCharacterDestinationPicker(UnitDefinition definition,int pickerSlot,bool monsters)
        {
            if(Match.Phase!=MatchPhase.Menu)return;
            CloseModal();
            modal=Panel(CanvasRoot,"Character destination",new Rect(0,0,540,960),new Color(.015f,.055f,.06f,.86f));
            Action back=()=>ShowCharacterPreview(definition,pickerSlot,monsters);
            modal.gameObject.AddComponent<Button>().onClick.AddListener(()=>back());
            var box=BluePanel(modal,"Character destination panel",new Rect(16,150,508,660),paper);
            box.gameObject.AddComponent<Button>().transition=Selectable.Transition.None;
            Label(box,GameText.Spanish?"ELIGE UNA CASILLA":"CHOOSE A SLOT",new Rect(16,22,396,36),28,blueFrame,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(box,"Brief "+(ActiveDeck+1)+" · "+definition.DisplayName,new Rect(16,66,396,28),21,blueFrame,FontStyle.Normal,TextAnchor.MiddleLeft);
            GameButton(box,"‹",new Rect(424,18,68,68),back,false,34).name="character-slots-back";
            for(int slot=0;slot<Match.Config.SelectionLimit;slot++)
            {
                int target=slot;
                string current=decks.Current.Whelps[slot];
                var button=GameButton(box,"",new Rect(slot==6?136:16+(slot%2)*244,116+(slot/2)*120,236,104),
                    ()=>ShowCharacterPreview(definition,pickerSlot,monsters,target));
                button.name="character-slot-"+slot;
                if(!string.IsNullOrEmpty(current))Portrait(button,current,new Rect(5,19,67,70));
                else Label(button,"+",new Rect(9,26,58,58),38,Color.white,FontStyle.Bold);
                Label(button,(GameText.Spanish?"CASILLA ":"SLOT ")+(slot+1),new Rect(78,13,138,25),17,Color.white,FontStyle.Bold,TextAnchor.MiddleLeft);
                Label(button,string.IsNullOrEmpty(current)?(GameText.Spanish?"Libre":"Empty"):
                    Match.Config.Get(current)?.DisplayName??current,new Rect(78,40,138,53),21,Color.white,FontStyle.Normal,TextAnchor.MiddleLeft);
            }
            Label(box,GameText.Spanish?"Después, pulsa Usar para confirmar.":"Then tap Use to confirm.",
                new Rect(16,606,476,31),19,blueFrame,FontStyle.Normal);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        bool collectionMonsters = true;
        string collectionFaction = BriefCollectionFilter.All;
        float collectionScrollOffset;
        RectTransform collectionFactionPopup;

        void SetCollectionCategory(bool monsters)
        {
            if (collectionMonsters != monsters) collectionScrollOffset = 0;
            collectionMonsters = monsters;
        }
        void RetireCollectionPart(Transform part)
        {
            if (part == null) return;
            part.name = "Retired " + part.name; part.gameObject.SetActive(false); Destroy(part.gameObject);
        }
        void CloseCollectionFactions()
        {
            RetireCollectionPart(collectionFactionPopup); collectionFactionPopup = null;
        }
        void RefreshCollectionView(RectTransform box, int slot, bool monsters)
        {
            // Browsing must never call RefreshDeckAvailability/Assign/ApplyDeck/Save.
            CloseCollectionFactions();
            RetireCollectionPart(collectionActions); collectionActions = null;
            RetireCollectionPart(box.Find("Collection filters"));
            RetireCollectionPart(box.Find("Collection viewport"));
            RenderCollectionFilters(box, slot, monsters); PopulateCollection(box, slot, monsters);
        }
        void RenderCollectionFilters(RectTransform box, int slot, bool monsters)
        {
            var row = Panel(box, "Collection filters", new Rect(12,92,484,68), Color.clear, false);
            if (slot == -2)
            {
                GameButton(row, CharacterCatalogLabels.Group(true,GameText.Spanish).ToUpperInvariant(), new Rect(0,0,142,68), () => {
                    SetCollectionCategory(true); RefreshCollectionView(box,slot,true);
                }, monsters,17).name = "MONSTERS";
                GameButton(row, CharacterCatalogLabels.Group(false,GameText.Spanish).ToUpperInvariant(), new Rect(154,0,142,68), () => {
                    SetCollectionCategory(false); RefreshCollectionView(box,slot,false);
                }, !monsters,17).name = "WHELPS";
            }
            else Label(row, GameText.Spanish?"FACCIÓN":"FACTION", new Rect(0,0,296,68),20,blueFrame,FontStyle.Bold,TextAnchor.MiddleLeft);
            var choice = GameButton(row, BriefCollectionFilter.Label(collectionFaction,GameText.Spanish),
                new Rect(308,0,176,68), () => ToggleCollectionFactions(box,slot,monsters), false,19);
            choice.name = "Collection faction filter";
            // Reserve space for the caret even for the longest faction name.
            var caption=choice.GetComponentInChildren<Text>(); SetRect(caption.rectTransform,new Rect(6,6,142,56));
            Label(choice,"▾",new Rect(150,6,20,56),17,CardTableTheme.ButtonText,FontStyle.Bold);
        }
        void ToggleCollectionFactions(RectTransform box, int slot, bool monsters)
        {
            if (collectionFactionPopup != null) { CloseCollectionFactions(); return; }
            collectionFactionPopup = HitArea(box,"Collection faction popup",new Rect(0,84,508,box.rect.height-84));
            collectionFactionPopup.gameObject.AddComponent<Button>().onClick.AddListener(CloseCollectionFactions);
            var choices = new[]{BriefCollectionFilter.All}.Concat(BriefCollectionFilter.FactionIds).ToArray();
            var list = BluePanel(collectionFactionPopup,"Faction choices",new Rect(276,82,220,choices.Length*68+12),paper);
            for (int i=0;i<choices.Length;i++)
            {
                string faction=choices[i];
                GameButton(list,BriefCollectionFilter.Label(faction,GameText.Spanish),new Rect(6,6+i*68,208,68),()=> {
                    if(collectionFaction!=faction)collectionScrollOffset=0;
                    collectionFaction=faction; RefreshCollectionView(box,slot,monsters);
                },collectionFaction==faction,20).name="Collection faction "+(faction==BriefCollectionFilter.All?"all":faction);
            }
        }
        void PopulateFilteredCollection(RectTransform box,int slot,bool monsters)
        {
            var availableSource=monsters?AvailableMonsterIdsForDeck:AvailableWhelpIdsForDeck;
            var available=availableSource==null?null:new HashSet<string>(availableSource,StringComparer.Ordinal);
            var units=BriefCollectionFilter.Select(Match.Config.Units,monsters,collectionFaction);
            var viewport=HitArea(box,"Collection viewport",new Rect(12,172,484,box.rect.height-184));
            viewport.gameObject.AddComponent<RectMask2D>();
            int columns=Mathf.Max(1,Mathf.FloorToInt((viewport.rect.width+12)/124));
            int rows=Mathf.CeilToInt(units.Length/(float)columns);
            var grid=Panel(viewport,"Collection grid",new Rect(0,0,viewport.rect.width,Mathf.Max(viewport.rect.height,rows*136+(slot>=0?234:154))),Color.clear,false);
            var scroll=viewport.gameObject.AddComponent<MenuCollectionScroll>();scroll.Owner=this;
            scroll.viewport=viewport;scroll.content=grid;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            viewport.gameObject.AddComponent<CollectionGridWindow>().Configure(scroll,units.Select(d=>d.Id).ToArray(),columns,collectionScrollOffset,
                parent=> {
                    var card=Panel(parent,"Collection card",new Rect(0,0,112,124),Color.clear,false);
                    card.gameObject.AddComponent<Image>().raycastTarget=true;
                    card.gameObject.AddComponent<Button>();card.SetAsFirstSibling();return card;
                },(card,index)=>BindCollectionCard(card,units[index],slot,monsters,available),ReleaseCollectionCard,
                offset=>collectionScrollOffset=offset);
            if(units.Length==0)
                Label(grid,GameText.Spanish?"Sin personajes.":"No characters.",new Rect(12,28,460,68),22,blueFrame,FontStyle.Bold).name="Collection empty state";
            if(slot>=0&&available==null&&!string.IsNullOrEmpty(decks.Current.Whelps[slot]))
                GameButton(grid,"REMOVE FROM BRIEF",new Rect(90,rows*136+158,304,68),()=>{
                    if(decks.Remove(slot,Match.Config,AvailableWhelpIdsForDeck)){ApplyDeck();PersistDeck();CloseModal();RenderPage();}
                },false,15);
        }
        void ReleaseCollectionCard(RectTransform card)
        {
            card.GetComponent<Button>().onClick.RemoveAllListeners();
            foreach(Transform child in card){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        }
        void BindCollectionCard(RectTransform card,UnitDefinition def,int slot,bool monsters,HashSet<string> available)
        {
            card.name="loadout-"+def.Id;
            bool selected=def.IsMonster?def.Id==chosen:chosenWhelps.Contains(def.Id);
            card.GetComponent<Image>().color=selected?new Color(.02f,.58f,.86f):brightBlue;
            PixelBorder(card,Color.Lerp(card.GetComponent<Image>().color,new Color(.70f,.49f,.22f),.48f),2);
            PixelBorder(card,CardTableTheme.Border,3);
            Panel(card,"Top shine",new Rect(7,5,98,3),CardTableTheme.Shine,false);
            if(selected)PixelBorder(card,gold,3);
            Portrait(card,def.Id,new Rect(6,10,100,104));
            if(selected)Label(card,"✓",new Rect(83,5,23,23),18,gold,FontStyle.Bold);
            if(available!=null&&!available.Contains(def.Id))
            {
                Panel(card,"Locked character shade",new Rect(6,10,100,104),new Color(0,0,0,.55f),false);
                var padlock=Panel(card,"Closed character lock",new Rect(38,34,36,48),Color.clear,false);
                Panel(padlock,"Shackle border",new Rect(4,0,28,28),ink,false);
                Panel(padlock,"Closed shackle",new Rect(7,3,22,22),paper,false);
                Panel(padlock,"Shackle opening",new Rect(12,8,12,17),ink,false);
                var body=Panel(padlock,"Lock body",new Rect(0,18,36,28),paper,false);PixelBorder(body,ink,2);
                Panel(body,"Keyhole",new Rect(15,7,6,6),ink,false);
                Panel(body,"Keyhole stem",new Rect(17,12,2,8),ink,false);
                var locked=Panel(card,def.IsMonster?"Unavailable Monster":"Unavailable Whelp",new Rect(2,98,108,24),new Color(.04f,.07f,.12f,.92f),false);
                Label(locked,GameText.Spanish?"BLOQUEADO":"LOCKED",new Rect(0,0,108,24),11,Color.white,FontStyle.Bold);
            }
            card.GetComponent<Button>().onClick.AddListener(()=>ShowCharacterPreview(def,slot,monsters));
        }
    }
}

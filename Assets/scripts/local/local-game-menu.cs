using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        const string DeckKey="monster-pouch.decks.v1";
        DeckLibrary decks;
        string menuPage="home";
        Sprite homeBackground,deckBackground;
        Color blueFrame=new Color(.03f,.15f,.43f),brightBlue=new Color(.03f,.45f,.87f),paper=new Color(.87f,.97f,1);
        bool ModernMenu=>Match.Config.Units.Any(d=>d!=null&&d.UsesDocumentedRules);
        public int ActiveDeck=>decks?.Active??0;
        public string MenuPage=>menuPage;

        void LoadDecks()
        {
            decks=DeckLibrary.Restore(PlayerPrefs.GetString(DeckKey,""),Match.Config,chosen,chosenWhelps);
            ApplyDeck();
            homeBackground=Resources.Load<Sprite>("MonsterPouch/UI/home-background");
            deckBackground=Resources.Load<Sprite>("MonsterPouch/UI/deck-background");
        }
        void ApplyDeck()
        {
            chosen=decks.Current.Monster;chosenWhelps.Clear();
            foreach(string id in decks.Current.Whelps)if(!string.IsNullOrEmpty(id))chosenWhelps.Add(id);
        }
        void PersistDeck()
        {
            PlayerPrefs.SetString(DeckKey,decks.Serialize());
            PlayerPrefs.SetString("monster-pouch.monster",chosen);
            PlayerPrefs.SetString("monster-pouch.whelps",string.Join(",",decks.Current.Whelps.Where(id=>!string.IsNullOrEmpty(id))));
            PlayerPrefs.Save();
        }
        public void SelectDeck(int index)
        {
            if(Match.Phase!=MatchPhase.Menu||index<0||index>=3)return;
            decks.Active=index;ApplyDeck();PersistDeck();CloseModal();RenderPage();
        }
        public void OpenDecks(){menuPage="decks";CloseModal();RenderPage();}
        public void OpenHome(){menuPage="home";CloseModal();RenderPage();}

        void RenderModernMenu()
        {
            if(decks==null)LoadDecks();
            selectedOfferSlot=-1;lastBriefRound=0;shownPouch=null;briefOpen=briefTarget=0;
            rerollSequence++;rerollBusy=rerollClosing=false;
            if(menuPage=="decks")RenderDeckRoom();else RenderHomeRoom();
            RenderNavigation();
        }
        void Backdrop(Sprite sprite)
        {
            var root=Panel(page,"Menu background",new Rect(0,0,540,960),brightBlue);
            if(sprite!=null){var im=root.GetComponent<Image>();im.sprite=sprite;im.color=Color.white;}
        }
        RectTransform BluePanel(Transform parent,string name,Rect rect,Color? fill=null)
        {
            var panel=Panel(parent,name,rect,fill??blueFrame);PixelBorder(panel,new Color(.05f,.75f,1),3);
            Panel(panel,"Top shine",new Rect(7,5,rect.width-14,3),new Color(.4f,.89f,1,.9f),false);
            return panel;
        }
        RectTransform GameButton(Transform parent,string label,Rect rect,Action action,bool primary=false,int size=20)
        {
            var panel=BluePanel(parent,label,rect,primary?new Color(1,.72f,.05f):brightBlue);
            if(primary)PixelBorder(panel,new Color(1,.94f,.42f),4);
            var b=panel.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>{PlaySound("button");action();});
            var colors=b.colors;colors.pressedColor=new Color(.7f,.8f,1);b.colors=colors;
            var text=Label(panel,label,new Rect(6,6,rect.width-12,rect.height-12),size,primary?new Color(.27f,.10f,.015f):Color.white,FontStyle.Bold);
            if(!primary)text.gameObject.AddComponent<Shadow>().effectDistance=new Vector2(1,-2);
            return panel;
        }
        void Figure(Transform parent,string id,Rect rect,bool groundAligned=true)
        {
            var data=art?.Get(id);if(data?.Portrait==null)return;
            Image(parent,data.Portrait,rect);
            var image=parent.GetChild(parent.childCount-1).GetComponent<Image>();
            if(groundAligned)
            {
                // Keep the original idle portrait resting on the pedestal.
                image.rectTransform.pivot=new Vector2(.5f,0);
                image.rectTransform.anchoredPosition=new Vector2(rect.center.x,-rect.yMax);
            }
        }
        void RenderHomeRoom()
        {
            Backdrop(homeBackground);
            var profile=BluePanel(page,"Player profile",new Rect(14,14,340,64));
            Figure(profile,chosen,new Rect(8,6,52,52),false);
            Label(profile,PlayerPrefs.GetString("monster-pouch.player-name","Player One"),new Rect(72,10,248,25),21,Color.white,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(profile,"BRIEF "+(ActiveDeck+1)+"  ·  "+chosenWhelps.Count+" / 7 WHELPS",new Rect(72,38,248,17),12,cyan,FontStyle.Normal,TextAnchor.MiddleLeft);
            GameButton(page,"AJUSTES",new Rect(367,14,159,64),ShowSettings,false,17);
            Label(page,"MONSTER POUCH",new Rect(36,100,468,42),31,Color.white,FontStyle.Bold).gameObject.AddComponent<Shadow>().effectDistance=new Vector2(2,-3);
            var pick=HitArea(page,"Choose featured Monster",new Rect(128,218,284,260));
            Figure(pick,chosen,new Rect(20,0,244,260));
            pick.gameObject.AddComponent<Button>().onClick.AddListener(()=>OpenCharacterPicker(-1));
            GameButton(page,Match.Config.Get(chosen).DisplayName.ToUpperInvariant(),new Rect(165,497,210,42),()=>OpenCharacterPicker(-1),true,23);
            Label(page,"MONSTER",new Rect(175,545,190,22),13,Color.white,FontStyle.Bold);
            var preview=BluePanel(page,"Current brief",new Rect(26,648,488,92),new Color(.025f,.18f,.5f,.95f));
            Label(preview,"BRIEF "+(ActiveDeck+1),new Rect(12,9,464,18),12,cyan,FontStyle.Bold,TextAnchor.MiddleLeft);
            string[] team=decks.Current.Whelps;
            for(int i=0;i<7;i++)if(!string.IsNullOrEmpty(team[i]))Figure(preview,team[i],new Rect(13+i*66,31,61,53),false);
            preview.gameObject.AddComponent<Button>().onClick.AddListener(OpenDecks);
            GameButton(page,"EDITAR BRIEF",new Rect(26,754,196,70),OpenDecks,false,19);
            var battle=GameButton(page,"¡A BATALLAR!",new Rect(234,754,280,70),StartChosenMatch,true,28);
            battle.name="JUGAR CONTRA BOT";
        }
        void StartChosenMatch()
        {
            SelectedId=null;CloseModal();PersistDeck();Match.StartMatch(chosen,chosenWhelps);
        }
        void RenderDeckRoom()
        {
            Backdrop(deckBackground);
            var name=BluePanel(page,"Deck name",new Rect(170,101,200,35),new Color(.73f,.32f,.015f));
            Label(name,PlayerPrefs.GetString("monster-pouch.player-name","Player One"),new Rect(4,3,192,28),17,Color.white,FontStyle.Bold);
            for(int i=0;i<3;i++){int index=i;GameButton(page,"BRIEF "+(i+1),new Rect(83+i*129,149,116,38),()=>SelectDeck(index),i==ActiveDeck,16);}
            var monster=HitArea(page,"deck-monster",new Rect(162,189,216,122));
            Figure(monster,chosen,new Rect(41,-9,134,128));
            monster.gameObject.AddComponent<Button>().onClick.AddListener(()=>OpenCharacterPicker(-1));
            Label(page,Match.Config.Get(chosen).DisplayName.ToUpperInvariant(),new Rect(164,315,212,26),19,Color.white,FontStyle.Bold);
            for(int i=0;i<7;i++)
            {
                int slot=i;bool upper=i<4;float width=upper?116:145;
                float center=upper?84+i*124:new[]{102f,271f,442f}[i-4];
                float x=center-width*.5f;float y=upper?414:612;
                var hit=HitArea(page,"deck-slot-"+i,new Rect(x,y,width,130));
                string id=decks.Current.Whelps[i];
                if(!string.IsNullOrEmpty(id))
                {
                    Figure(hit,id,new Rect((width-90)/2,-4,90,104));
                    Label(hit,Match.Config.Get(id).DisplayName.ToUpperInvariant(),new Rect(0,105,width,20),14,Color.white,FontStyle.Bold).gameObject.AddComponent<Shadow>();
                }
                else Label(hit,"+",new Rect(0,12,width,77),48,Color.white,FontStyle.Bold);
                hit.gameObject.AddComponent<Button>().onClick.AddListener(()=>OpenCharacterPicker(slot));
            }
            Label(page,chosenWhelps.Count+" / 7 WHELPS",new Rect(126,795,288,23),16,Color.white,FontStyle.Bold);
        }
        void RenderNavigation()
        {
            BluePanel(page,"Navigation",new Rect(0,841,540,119));
            string[] labels={"FIGURAS","BRIEF","JUGAR","GUÍA","SONIDO"};
            string[] symbols={"♦","▣","⚔","?","♫"};
            Action[] actions={() =>OpenCollection(),OpenDecks,OpenHome,ShowHelp,ShowAudio};
            for(int i=0;i<5;i++)
            {
                bool selected=i==1&&menuPage=="decks"||i==2&&menuPage=="home";
                var tab=GameButton(page,"",new Rect(3+i*108,846,102,110),actions[i],selected);
                Label(tab,symbols[i],new Rect(4,10,94,52),38,selected?blueFrame:Color.white).font=symbolFont;
                Label(tab,labels[i],new Rect(2,76,98,23),14,selected?blueFrame:Color.white,FontStyle.Bold);
                tab.name="nav-"+labels[i];
            }
        }
        public void OpenCollection(){OpenCharacterPicker(-2);}
        public void OpenCharacterPicker(int slot)
        {
            CloseModal();modal=Panel(CanvasRoot,"Character selection",new Rect(0,0,540,960),new Color(.005f,.045f,.13f,.8f));
            var box=BluePanel(modal,"Collection panel",new Rect(16,99,508,725),paper);
            var header=GameButton(box,slot==-1?"SELECCIONAR MONSTER":slot>=0?"SELECCIONAR WHELP":"COLECCIÓN",new Rect(10,10,434,52),()=>{},true,22);
            GameButton(box,"×",new Rect(450,10,48,52),CloseModal,false,30);
            bool monsters=slot==-1;
            if(slot==-2)
            {
                GameButton(box,"MONSTERS",new Rect(22,76,220,35),()=>ShowCollectionPage(true),false,15);
                GameButton(box,"WHELPS",new Rect(257,76,229,35),()=>ShowCollectionPage(false),false,15);
            }
            else Label(box,"ELIGE UNA FIGURA PARA TU BRIEF",new Rect(15,76,478,25),15,blueFrame,FontStyle.Bold);
            PopulateCollection(box,slot,monsters);
        }
        void ShowCollectionPage(bool monsters)
        {
            OpenCharacterPicker(-2);
            var box=modal.Find("Collection panel") as RectTransform;
            var old=box.Find("Collection grid");if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            PopulateCollection(box,-2,monsters);
        }
        void PopulateCollection(RectTransform box,int slot,bool monsters)
        {
            var grid=Panel(box,"Collection grid",new Rect(17,123,474,570),Color.clear,false);
            var units=Match.Config.Units.Where(d=>d!=null&&d.IsMonster==monsters).ToArray();
            for(int i=0;i<units.Length;i++)
            {
                var def=units[i];float x=(i%5)*96,y=(i/5)*166;
                bool selected=def.IsMonster?def.Id==chosen:chosenWhelps.Contains(def.Id);
                var card=BluePanel(grid,"loadout-"+def.Id,new Rect(x,y,90,156),selected?new Color(.02f,.58f,.86f):brightBlue);
                if(selected)PixelBorder(card,gold,3);
                Figure(card,def.Id,new Rect(5,9,80,94),false);
                Panel(card,"Name strip",new Rect(3,106,84,47),blueFrame,false);
                Label(card,def.DisplayName.ToUpperInvariant(),new Rect(1,108,88,19),12,Color.white,FontStyle.Bold);
                Label(card,selected?"EN EQUIPO":def.IsMonster?"MONSTER":"WHELP",new Rect(0,132,65,13),8,selected?gold:cyan);
                card.gameObject.AddComponent<Button>().onClick.AddListener(()=>{
                    if(slot==-2){ShowCollectionInfo(def);return;}
                    if(decks.Assign(slot,def.Id,Match.Config)){ApplyDeck();PersistDeck();PlaySound("button");CloseModal();RenderPage();}
                });
                var info=HitArea(card,"info-"+def.Id,new Rect(63,129,25,25));
                Label(info,"i",new Rect(0,0,25,25),17,Color.white,FontStyle.Bold);
                info.gameObject.AddComponent<Button>().onClick.AddListener(()=>ShowCollectionInfo(def));
            }
            if(slot>=0&&!string.IsNullOrEmpty(decks.Current.Whelps[slot]))
                GameButton(grid,"QUITAR DEL BRIEF",new Rect(86,521,304,38),()=>{
                    if(decks.Remove(slot)){ApplyDeck();PersistDeck();CloseModal();RenderPage();}
                },false,15);
        }
        void ShowCollectionInfo(UnitDefinition definition)
        {
            CloseModal();modal=Panel(CanvasRoot,"Character details",new Rect(0,0,540,960),new Color(.005f,.045f,.13f,.9f));
            var box=BluePanel(modal,"Character sheet",new Rect(21,81,498,795),paper);
            Figure(box,definition.Id,new Rect(18,21,112,117),false);
            Label(box,definition.DisplayName.ToUpperInvariant(),new Rect(144,24,336,39),26,blueFrame,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(box,definition.IsMonster?"MONSTER":"WHELP",new Rect(144,64,320,23),13,brightBlue,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(box,"VIDA "+definition.MaxHealth+"    DAÑO "+definition.Damage+"    RANGO "+definition.AttackRange,new Rect(144,95,328,48),13,blueFrame,FontStyle.Normal,TextAnchor.UpperLeft);
            Label(box,definition.BaseAbility.Name.ToUpperInvariant(),new Rect(23,159,450,26),18,brightBlue,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(box,definition.BaseAbility.Description,new Rect(23,194,450,113),16,blueFrame,FontStyle.Normal,TextAnchor.UpperLeft);
            for(int i=0;i<3;i++)
            {
                var trick=definition.Tricks[i];if(trick==null)continue;float y=322+i*121;
                var row=BluePanel(box,"Ability "+i,new Rect(18,y,462,111),new Color(.73f,.90f,1));
                Label(row,(i+1)+" · "+trick.Name,new Rect(14,10,434,24),17,blueFrame,FontStyle.Bold,TextAnchor.MiddleLeft);
                Label(row,trick.Description,new Rect(14,40,434,64),15,blueFrame,FontStyle.Normal,TextAnchor.UpperLeft);
            }
            GameButton(box,"VOLVER A LA COLECCIÓN",new Rect(34,722,430,48),OpenCollection,false,17);
        }
        void ShowSettings()
        {
            CloseModal();modal=Panel(CanvasRoot,"Settings",new Rect(0,0,540,960),new Color(.015f,.06f,.18f,.97f));
            Label(modal,"AJUSTES",new Rect(40,200,460,56),36,Color.white,FontStyle.Bold);
            Label(modal,"NOMBRE DEL JUGADOR",new Rect(65,319,410,25),15,cyan,FontStyle.Bold,TextAnchor.MiddleLeft);
            var field=BluePanel(modal,"Player name",new Rect(65,359,410,58),paper);
            var input=field.gameObject.AddComponent<InputField>();
            var label=Label(field,"",new Rect(14,8,382,42),23,blueFrame,FontStyle.Normal,TextAnchor.MiddleLeft);
            input.textComponent=label;input.characterLimit=20;input.text=PlayerPrefs.GetString("monster-pouch.player-name","Player One");
            GameButton(modal,"GUARDAR",new Rect(65,451,410,54),()=>{
                string value=input.text.Trim();if(value.Length==0)value="Player One";
                PlayerPrefs.SetString("monster-pouch.player-name",value);PlayerPrefs.Save();CloseModal();RenderPage();
            },true);
            GameButton(modal,"SONIDO",new Rect(65,525,410,54),ShowAudio);
            GameButton(modal,"VOLVER",new Rect(65,661,410,54),CloseModal);
        }
    }
}

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
        int collectionPickerSlot=-2;
        RectTransform collectionActions;
        Sprite homeBackground,deckBackground;
        Color blueFrame=new Color(.03f,.15f,.43f),brightBlue=new Color(.03f,.45f,.87f),paper=new Color(.87f,.97f,1);
        bool ModernMenu=>Match.Config.Units.Any(d=>d!=null&&d.UsesDocumentedRules);
        public int ActiveDeck=>decks?.Active??0;
        public string MenuPage=>menuPage;

        void LoadDecks()
        {
            if(!PlayerPrefs.HasKey(LocalAccountPreferences.Key(CardTableTheme.LobbyPreferenceKey)))CardTableTheme.SelectLobby(CardTableTheme.Current);
            LoadDeckStorage();
            ApplyDeck();
            LoadTableArtwork();
        }
        void ApplyDeck()
        {
            chosen=decks.Current.Monster;chosenWhelps.Clear();
            foreach(string id in decks.Current.Whelps)if(!string.IsNullOrEmpty(id))chosenWhelps.Add(id);
        }
        void PersistDeck()
        {
            SaveDeckStorage();
        }
        public void SelectDeck(int index)
        {
            if(Match.Phase!=MatchPhase.Menu||index<0||index>=DeckLibrary.DeckCount)return;
            decks.Active=index;ApplyDeck();PersistDeck();CloseModal();RenderPage();
        }
        public void OpenDecks(){NavigateMenu(1);}
        public void OpenHome(){NavigateMenu(2);}

        void RenderModernMenu()
        {
            if(decks==null)LoadDecks();
            LoadTableArtwork();
            if(RenderStarterSelection())return;
            RefreshDeckAvailability();
            CloseBankContents();
            selectedOfferSlot=-1;lastBriefRound=0;shownPouch=null;briefOpen=briefTarget=0;
            rerollSequence++;rerollBusy=rerollClosing=false;
            if(menuPage=="decks")RenderDeckRoom();else if(menuPage=="shop"||menuPage=="challenges")RenderComingSoon();else RenderHomeRoom();
            RenderNavigation();
            RenderDeckStorageNotice();
        }
        void Backdrop(Sprite sprite)
        {
            // Every menu page keeps the selected Home artwork and its original aspect.
            if(Match.Phase==MatchPhase.Menu&&RenderPaintedLobby())return;
            screenBackdrop.sprite=null;screenBackdrop.color=PixelScenery.MarginColor;
            Vector2 lower=ToCanvas(Vector2.zero),upper=ToCanvas(new Vector2(Screen.width,Screen.height));
            Rect visible=Rect.MinMaxRect(lower.x,upper.y,upper.x,lower.y);
            var root=Panel(page,"Menu background",visible,Color.clear,false);
            root.gameObject.AddComponent<RectMask2D>();
            var plane=Panel(root,"Square scenery",new Rect(-450-visible.x,-240-visible.y,1440,1440),Color.clear,false);
            var image=plane.gameObject.AddComponent<Image>();
            plane.gameObject.AddComponent<SquareSceneryView>().Configure(image,sprite);
        }

        RectTransform BluePanel(Transform parent,string name,Rect rect,Color? fill=null)
        {
            var panel=Panel(parent,name,rect,fill??blueFrame);
            PixelBorder(panel,CardTableTheme.Border,3);
            Panel(panel,"Top shine",new Rect(7,5,rect.width-14,3),CardTableTheme.Shine,false);
            return panel;
        }
        RectTransform GameButton(Transform parent,string label,Rect rect,Action action,bool primary=false,int size=20)
        {
            var panel=Panel(parent,label,rect,Color.clear,false);
            var hit=panel.gameObject.AddComponent<Image>();hit.raycastTarget=true;
            var b=panel.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>{PlaySound("button");action();});
            RetroButtonMotion.Attach(panel,b,primary?CardTableTheme.PrimaryButton:CardTableTheme.Button);
            var text=Label(panel,label,new Rect(6,6,rect.width-12,rect.height-12),size,primary?CardTableTheme.PrimaryButtonText:CardTableTheme.ButtonText,FontStyle.Bold);
            if(!primary)text.gameObject.AddComponent<Shadow>().effectDistance=new Vector2(1,-2);
            return panel;
        }
        void Figure(Transform parent,string id,Rect rect,bool groundAligned=true,bool animate=false,bool previewOnOpen=false,LobbyArtworkContext context=LobbyArtworkContext.Standard,Color? color=null)
        {
            var data=CharacterUiArtwork.GetCharacter(id)?.Visuals??art?.Get(id);
            rect=CharacterMenuFigureLayout.Frame(rect,id,data,groundAligned,parent.name);
            // The shared authored idle renderer is the primary path, even when
            // a character has no profile portrait. Fallback is full-body idle.
            var figure=Panel(parent,"Art",rect,Color.clear,false);
            var image=figure.gameObject.AddComponent<Image>();
            image.sprite=data?.Idle(UnitFacing.South);image.preserveAspect=true;
            image.raycastTarget=false;image.color=color??Color.white;
            image.enabled=image.sprite!=null;
            if(groundAligned)
            {
                // Keep the original idle portrait resting on the pedestal.
                image.rectTransform.pivot=new Vector2(.5f,0);
                image.rectTransform.anchoredPosition=new Vector2(rect.center.x,-rect.yMax);
            }
            bool useLobbyView=Match!=null&&Match.Phase==MatchPhase.Menu;
            if(animate)image.gameObject.AddComponent<LobbyPersonality>().Configure(id,rect.height,LobbyDirector,Match.Config.Get(id)?.IsMonster??false,previewOnOpen,context,useLobbyView);
            else
            {
                var shadow=new GameObject("Contact shadow",typeof(RectTransform));
                var shadowRect=(RectTransform)shadow.transform;shadowRect.SetParent(image.transform,false);
                shadowRect.anchorMin=Vector2.zero;shadowRect.anchorMax=Vector2.one;shadowRect.offsetMin=shadowRect.offsetMax=Vector2.zero;
                shadow.AddComponent<LobbyContactShadow>();
                var child=new GameObject("Original idle",typeof(RectTransform));
                var bounds=(RectTransform)child.transform;bounds.SetParent(image.transform,false);
                bounds.anchorMin=Vector2.zero;bounds.anchorMax=Vector2.one;bounds.offsetMin=bounds.offsetMax=Vector2.zero;
                var drawing=child.AddComponent<LobbyFrameGraphic>();
                drawing.color=color??Color.white;
                if(drawing.Configure(id,false,context,useLobbyView))image.enabled=false;else Destroy(child);
            }
        }
        void RenderHomeRoom()
        {
            if (!RenderPaintedLobby()) Backdrop(homeBackground);
            var characterBounds = MenuRect(new Rect(158,322,224,230),552f/960);
            if (HasLobbyCharacter(chosen) && !RenderLobbyPedestal(new Vector2(characterBounds.center.x,characterBounds.yMax)))
                Pedestal(new Rect(142,545,256,36));
            RenderLobbyHeader();
            var pick=HitArea(page,"Choose featured Monster",MenuRect(new Rect(158,322,224,230),552f/960));
            if(!string.IsNullOrEmpty(chosen))Figure(pick,chosen,new Rect(12,0,200,230),true,true,context:LobbyArtworkContext.BattleHome);
            else Label(pick,GameText.Spanish?"CAMINO DE ERONYA":"ERONYA ROAD",new Rect(12,80,200,80),25,LobbyPaper,FontStyle.Bold);
            pick.gameObject.AddComponent<Button>().onClick.AddListener(OpenProgressionRoad);
            RenderLobbyBattleControls();
        }
        void StartChosenMatch()
        {
            if(!PrepareCurrentDeckForMatch()){RenderPage();return;}
            SelectedId=null;CloseModal();Match.StartMatch(chosen,chosenWhelps,localMap:CardTableTheme.MapId(CardTableTheme.Current));
        }
        void RenderDeckRoom()
        {
            Backdrop(deckBackground);
            DisplayCabinet();
            for(int i=0;i<DeckLibrary.DeckCount;i++){int index=i;GameButton(page,(i+1).ToString(),MenuRect(new Rect(84+i*76,145,68,44),.17f),()=>SelectDeck(index),i==ActiveDeck,22).name="Deck selector "+(i+1);}
            var monster=HitArea(page,"deck-monster",MenuRect(new Rect(162,189,216,122),291f/960));
            if(!string.IsNullOrEmpty(chosen))Figure(monster,chosen,new Rect(41,-3,134,105),true);
            else Label(monster,"+",new Rect(41,-3,134,105),48,Color.white,FontStyle.Bold);
            monster.gameObject.AddComponent<Button>().onClick.AddListener(OpenCollection);
            for(int i=0;i<7;i++)
            {
                int slot=i;bool upper=i<4;float width=upper?116:145;
                float center=upper?84+i*124:new[]{100f,270f,440f}[i-4];
                float x=center-width*.5f;float y=upper?414:612;
                var hit=HitArea(page,"deck-slot-"+i,MenuRect(new Rect(x,y,width,130),(y+98)/960));
                string id=decks.Current.Whelps[i];
                if(!string.IsNullOrEmpty(id))
                {
                    Figure(hit,id,new Rect((width-90)/2,upper?-4:-6,90,104),true);
                }
                else Label(hit,"+",new Rect(0,12,width,77),48,Color.white,FontStyle.Bold);
                hit.gameObject.AddComponent<Button>().onClick.AddListener(()=>OpenCharacterPicker(slot));
            }
            var available=AvailableWhelpIdsForDeck;
            string count=chosenWhelps.Count+" / "+(available==null?Match.Config.SelectionLimit:Mathf.Min(Match.Config.SelectionLimit,available.Count()));
            if(available!=null)count+=GameText.Spanish?" · REQUERIDOS":" · REQUIRED";
            Label(page,count,MenuRect(new Rect(40,795,460,23),1),16,Color.white,FontStyle.Bold).name="Brief required count";
        }
        void RenderNavigation()=>RenderDockedNavigation();
        public void OpenCollection(){ShowCollectionPage(collectionMonsters); }
        public void OpenCharacterPicker(int slot)
        {
            if(Match.Phase!=MatchPhase.Menu||slot < -2||slot>=Match.Config.SelectionLimit)return;
            if(decks==null)LoadDecks();
            RefreshDeckAvailability();
            collectionPickerSlot=slot;
            if(slot!=-2)SetCollectionCategory(slot==-1);
            collectionActions=null;
            CloseModal();modal=Panel(CanvasRoot,"Character selection",new Rect(0,0,540,960),new Color(.005f,.045f,.13f,.8f));
            var box=BluePanel(modal,"Collection panel",MenuRect(new Rect(16,24,508,806+menuExtraHeight),0),paper);
            if(slot==-2)RenderCollectionTabs(box,false);
            else GameButton(box,CharacterCatalogLabels.Group(slot==-1,GameText.Spanish).ToUpperInvariant(),new Rect(12,12,404,68),()=>{},true,22);
            GameButton(box,"×",new Rect(428,12,68,68),CloseModal,false,30);
            bool monsters=slot==-2?collectionMonsters:slot==-1;
            RenderCollectionFilters(box,slot,monsters);
            PopulateCollection(box,slot,monsters);
            RaiseMenuNavigation();
        }
        void ShowCollectionPage(bool monsters)
        {
            SetCollectionCategory(monsters);
            OpenCharacterPicker(-2);
        }
        void PopulateCollection(RectTransform box,int slot,bool monsters)
            =>PopulateFilteredCollection(box,slot,monsters);
        void RenderCollectionTabs(RectTransform box,bool styles)
        {
            GameButton(box,"BRIEF",new Rect(12,12,196,68),()=>ShowCollectionPage(collectionMonsters),!styles,24).name="collection-deck-tab";
            GameButton(box,GameText.Spanish?"ESTILOS":"STYLES",new Rect(220,12,196,68),ShowLobbySkins,styles,24).name="collection-styles-tab";
        }
        void ShowCollectionInfo(UnitDefinition definition) => ShowAbilityCard(new OwnedUnit(definition),false,true);
        void ShowSettings()=>ShowLobbySettings();
    }
}

using System;
using UnityEngine;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Preserve the existing numeric values and saved identities.
    public enum TableStyle { Default=0, GrandCasaHotel=1, CampoReal=2, Naturalis=3, Aquarion=4, Geothar=5, Volkanar=6, CasinoNight=7, Aireola=8 }

    public static class CardTableTheme
    {
        public const string PreferenceKey="monster-pouch.map-skin";
        // Existing preferences remain exact; new players begin with Naturalis.
        public static TableStyle Current=>FromMapId(PlayerPrefs.GetString(LocalAccountPreferences.Key(PreferenceKey),"naturalis"));
        public static string MapId(TableStyle style)
        {
            switch(style)
            {
                case TableStyle.Default:return "classic";
                case TableStyle.GrandCasaHotel:return "lounge";
                case TableStyle.CasinoNight:return "casino-night";
                case TableStyle.Aireola:return "aireola";
                case TableStyle.CampoReal:return "royal";
                case TableStyle.Aquarion:return "aquarion";
                case TableStyle.Geothar:return "geothar";
                case TableStyle.Volkanar:return "volkanar";
                default:return "naturalis";
            }
        }
        public static bool KnownMap(string id)=>id=="classic"||id=="lounge"||id=="casino-night"||id=="royal"||
            id=="naturalis"||id=="aquarion"||id=="geothar"||id=="volkanar"||id=="aireola";
        public static TableStyle FromMapId(string id)
        {
            switch(id)
            {
                case "classic":return TableStyle.Default;
                case "lounge":return TableStyle.GrandCasaHotel;
                case "casino-night":return TableStyle.CasinoNight;
                case "aireola":return TableStyle.Aireola;
                case "royal":return TableStyle.CampoReal;
                case "aquarion":return TableStyle.Aquarion;
                case "geothar":return TableStyle.Geothar;
                case "volkanar":return TableStyle.Volkanar;
                default:return TableStyle.Naturalis;
            }
        }
        public static bool IsNation(TableStyle style)=>style==TableStyle.Naturalis||style==TableStyle.Aquarion||
            style==TableStyle.Geothar||style==TableStyle.Volkanar||style==TableStyle.Aireola;
        public static NationBoardArtwork NationArtwork(TableStyle style)=>
            Resources.Load<NationBoardArtwork>("MonsterPouch/Styles/Nations/"+MapId(style));
        public static Sprite MiniWorld(TableStyle style)=>NationArtwork(style)?.Miniature??
            Resources.Load<Sprite>("MonsterPouch/UI/MiniWorlds/"+MapId(style));
        public static string Name(TableStyle style)=>style==TableStyle.GrandCasaHotel?"GRAND CAS HOTEL":
            style==TableStyle.CasinoNight?(GameText.Spanish?"CASINO NOCHE":"NIGHT CASINO"):
            style==TableStyle.Aireola?"AIREOLA":
            style==TableStyle.CampoReal?(GameText.Spanish?"CAMPO REAL":"ROYAL COURT"):
            style==TableStyle.Naturalis?"NATURALIS":style==TableStyle.Aquarion?"AQUARION":
            style==TableStyle.Geothar?"GEOTHAR":style==TableStyle.Volkanar?"VOLKANAR":
            (GameText.Spanish?"PARQUE DE JUEGOS":"PLAYGROUND");
        public const string LobbyPreferenceKey="monster-pouch.lobby-style";
        public static TableStyle Lobby=>LobbyThemeSelection.BaseStyle(LobbyThemeSelection.Id);
        public static void SelectLobby(TableStyle style)=>LobbyThemeSelection.Select(MapId(style));
        public static bool IsLounge=>Lobby==TableStyle.GrandCasaHotel;
        public static bool IsRoyal=>Lobby==TableStyle.CampoReal;
        public static EnvironmentStyle LobbyDefinition=>EnvironmentStyle.Find(MapId(Lobby));
        public static Color Button=>LobbyDefinition!=null?LobbyDefinition.Button:RetroButtonMotion.Red;
        public static Color PrimaryButton=>LobbyDefinition!=null?LobbyDefinition.PrimaryButton:RetroButtonMotion.Gold;
        public static Color ButtonText=>LobbyDefinition!=null?LobbyDefinition.ButtonText:RetroButtonMotion.Cream;
        public static Color PrimaryButtonText=>LobbyDefinition!=null?LobbyDefinition.PrimaryButtonText:new Color(.18f,.16f,.14f);
        public static Color Border=>LobbyDefinition!=null?LobbyDefinition.Border:IsRoyal?new Color(.84f,.64f,.30f):IsLounge?new Color(.76f,.53f,.22f):new Color(.05f,.75f,1);
        public static Color Shine=>LobbyDefinition!=null?LobbyDefinition.Shine:IsRoyal?new Color(1,.91f,.63f,.8f):IsLounge?new Color(1,.86f,.49f,.7f):new Color(.4f,.89f,1,.9f);
        public static Color Primary=>IsRoyal?new Color(.98f,.76f,.32f):new Color(1,.72f,.05f);
        public static Color PrimaryBorder=>IsRoyal?new Color(1,.93f,.68f):new Color(1,.94f,.42f);
        public static void Select(bool lounge)=>Select(lounge?TableStyle.GrandCasaHotel:TableStyle.Default);
        public static void Select(TableStyle style)
        {
            PlayerPrefs.SetString(LocalAccountPreferences.Key(PreferenceKey),MapId(style));
            PlayerPrefs.Save();
        }
    }

    public sealed partial class LocalGameUI
    {
        TableStyle? renderedArenaStyle,requestedArenaStyle;
        MapIrisTransition mapTransition;
        public bool MapTransitionActive=>mapTransition!=null&&mapTransition.IsRunning;
        public TableStyle DisplayedArenaStyle => Match.IsOnline?CardTableTheme.FromMapId(Match.RoundMap):CardTableTheme.Current;
        void SetArenaStyle(TableStyle style)
        {
            var arena=FindFirstObjectByType<CardTableArena>();
            if(arena!=null)arena.SetTheme(style);
            renderedArenaStyle=style;requestedArenaStyle=style;
        }
        void ApplyRoundMap()
        {
            var style=DisplayedArenaStyle;
            if(renderedArenaStyle==null){SetArenaStyle(style);return;}
            // Local selection preferences intentionally still describe the old visible scene.
            // Rebuilds must not replace its pending transition with that old preference.
            if(MapTransitionActive)
            {if(!Match.IsOnline||requestedArenaStyle==style)return;}
            else if(renderedArenaStyle==style){requestedArenaStyle=style;return;}
            TransitionMap(style,false);
        }
        void TransitionMap(TableStyle style,bool reopenSelector)=>BeginMapTransition(style,reopenSelector,false);
        void BeginMapTransition(TableStyle style,bool reopenSelector,bool saveSelection)
        {
            requestedArenaStyle=style;StopMenuSlide();boardGesture.Cancel();EndDragPreview();
            if(mapTransition==null)mapTransition=MapIrisTransition.Create(transform);
            mapTransition.Play(style,()=>
            {
                if(saveSelection)CardTableTheme.Select(style);
                SetArenaStyle(style);
                if(Match.Phase==MonsterPouch.Gameplay.Match.MatchPhase.Menu)
                {
                    LoadTableArtwork();RenderPage();
                    if(reopenSelector)ShowTableSkins();
                }
            });
        }

        void LoadTableArtwork()
        {
            var style=CardTableTheme.LobbyDefinition;
            var square=CardTableTheme.NationArtwork(CardTableTheme.Lobby)?.LobbyBackground??
                (style!=null?style.LobbyBackground:Resources.Load<Sprite>("MonsterPouch/UI/Scenery/"+CardTableTheme.MapId(CardTableTheme.Lobby)+"-square"));
            homeBackground=Resources.Load<Sprite>("MonsterPouch/UI/home-background");
            if(CardTableTheme.IsLounge)homeBackground=Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/lounge-home")??homeBackground;
            if(CardTableTheme.IsRoyal)homeBackground=Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/royal-home")??homeBackground;
            var original=Resources.Load<Sprite>("MonsterPouch/UI/deck-background-clean")
                ??Resources.Load<Sprite>("MonsterPouch/UI/deck-background-no-whelp-lights")
                ??Resources.Load<Sprite>("MonsterPouch/UI/deck-background");
            deckBackground=CardTableTheme.IsRoyal?Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/royal-deck")??original:CardTableTheme.IsLounge
                ?Resources.Load<Sprite>("MonsterPouch/UI/TableSkins/lounge-deck")??original:original;
            if(square!=null){homeBackground=square;deckBackground=square;}
            blueFrame=CardTableTheme.IsRoyal?new Color(.30f,.13f,.10f):CardTableTheme.IsLounge?new Color(.035f,.19f,.20f):new Color(.03f,.15f,.43f);
            brightBlue=CardTableTheme.IsRoyal?new Color(.56f,.20f,.23f):CardTableTheme.IsLounge?new Color(.035f,.34f,.34f):new Color(.03f,.45f,.87f);
            if(style!=null){blueFrame=style.Panel;brightBlue=style.Accent;}
            paper=style!=null?style.Paper:CardTableTheme.IsRoyal?new Color(1,.96f,.86f):CardTableTheme.IsLounge?new Color(.98f,.95f,.86f):new Color(.87f,.97f,1);
        }

        bool editingLobbyStyle=true;
        static readonly TableStyle[] SelectableStyles={TableStyle.Default,TableStyle.GrandCasaHotel,TableStyle.CampoReal,
            TableStyle.Naturalis,TableStyle.Aquarion,TableStyle.Geothar,TableStyle.Volkanar,TableStyle.CasinoNight,TableStyle.Aireola};
        public void ShowLobbySkins(){editingLobbyStyle=true;ShowTableSkins();}
        public void ShowMapSkins(){editingLobbyStyle=false;ShowTableSkins();}
        public void ShowTableSkins()
        {
            ReloadGuestAvailability();RefreshTrophyBoardAccess();
            CloseModal();
            modal=Panel(CanvasRoot,"Scenario skins",new Rect(0,0,540,960),blueFrame);
            var tabs=BluePanel(modal,"Styles tabs",MenuRect(new Rect(16,24,508,172),0),paper);
            RenderCollectionTabs(tabs,true);
            GameButton(tabs,"×",new Rect(428,12,68,68),OpenDecks,false,30);
            GameButton(tabs,"LOBBY",new Rect(12,92,236,68),ShowLobbySkins,editingLobbyStyle,19).name="styles-lobby-tab";
            GameButton(tabs,GameText.Spanish?"MAPA":"MAP",new Rect(260,92,236,68),ShowMapSkins,!editingLobbyStyle,19).name="styles-map-tab";
            Label(modal,(editingLobbyStyle?(GameText.Spanish?"Tu lobby: ":"Your lobby: "):(GameText.Spanish?"Tu mapa: ":"Your map: "))+
                (editingLobbyStyle?LobbyThemeSelection.Name(LobbyThemeSelection.Id):CardTableTheme.Name(CardTableTheme.Current)),MenuRect(new Rect(20,204,500,26),0),15,paper);
            if(!editingLobbyStyle)
            {
                RenderMapGallery(modal);
                modal.gameObject.AddComponent<StyleSelectorTouchTargets>().Refresh();
                RaiseMenuNavigation();
                return;
            }
            RenderLobbySkinGallery(modal);
            modal.gameObject.AddComponent<StyleSelectorTouchTargets>().Refresh();
            RaiseMenuNavigation();
        }

        public void SelectLobbyStyle(TableStyle style)=>SelectLobbyStyle(CardTableTheme.MapId(style));
        public void SelectLobbyStyle(string id)
        {
            if(Match.Phase!=MonsterPouch.Gameplay.Match.MatchPhase.Menu||!LobbyThemeIdentity.IsKnown(id)||!CardTableTheme.KnownMap(id))return;
            var style=LobbyThemeSelection.BaseStyle(id);
            if(!ReloadGuestAvailability()||!CanSelectTrophyBoard(style))return;
            StopMenuSlide();boardGesture.Cancel();
            editingLobbyStyle=true;
            if(mapTransition==null)mapTransition=MapIrisTransition.Create(transform);
            mapTransition.Play(style,()=>{LobbyThemeSelection.Select(id);LoadTableArtwork();RenderPage();ShowLobbySkins();});
        }

        public void SelectTableSkin(bool lounge)=>SelectTableSkin(lounge?TableStyle.GrandCasaHotel:TableStyle.Default);
        public void SelectTableSkin(TableStyle style)=>SelectTableSkin(style,true);
        void SelectTableSkin(TableStyle style,bool reopenSelector)
        {
            if(Match.Phase!=MonsterPouch.Gameplay.Match.MatchPhase.Menu)return;
            if(!ReloadGuestAvailability()||!CanSelectTrophyBoard(style))return;
            editingLobbyStyle=false;
            if(renderedArenaStyle==style&&!MapTransitionActive)
            {CardTableTheme.Select(style);LoadTableArtwork();RenderPage();if(reopenSelector)ShowTableSkins();return;}
            BeginMapTransition(style,reopenSelector,true);
        }
    }

    // Quiet light variation belongs to the scenery, independent of character gestures.
    // It uses native geometry and never moves the podiums beneath the figures.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyRoomAmbience:MaskableGraphic
    {
        float lastLight;
        protected override void Awake(){base.Awake();raycastTarget=false;}
        void Update()
        {
            float light=.075f+.009f*Mathf.Sin(Time.unscaledTime*.55f);
            if(Mathf.Abs(light-lastLight)<.0003f)return;
            lastLight=light;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Glow(vh,r,new Vector2(40,165),new Vector2(48,72),lastLight);
            Glow(vh,r,new Vector2(500,165),new Vector2(48,72),lastLight);
        }
        static void Glow(VertexHelper vh,Rect r,Vector2 center,Vector2 radius,float alpha)
        {
            int start=vh.currentVertCount;
            var c=new Vector2(r.x+center.x,r.yMax-center.y);
            vh.AddVert(c,new Color(1,.78f,.38f,alpha),Vector2.zero);
            const int segments=32;
            for(int i=0;i<=segments;i++)
            {
                float t=i*Mathf.PI*2/segments;
                vh.AddVert(c+new Vector2(Mathf.Cos(t)*radius.x,Mathf.Sin(t)*radius.y),new Color(1,.78f,.38f,0),Vector2.zero);
                if(i>0)vh.AddTriangle(start,start+i,start+i+1);
            }
        }
    }
}

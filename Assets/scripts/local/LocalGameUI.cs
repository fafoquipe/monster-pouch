using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI : MonoBehaviour
    {
        public Sprite MoonIcon, RerollIcon, BriefArt, BenchArt, ClosedBriefArt;
        [Header("Sell feedback")]
        [SerializeField, Min(.1f)] float sellMoonkenTravelDuration=.58f;
        [SerializeField, Range(0,.15f)] float sellMoonkenStagger=.045f;
        [SerializeField, Min(.05f)] float currencyPunchDuration=.24f;
        [SerializeField, Range(1f,1.4f)] float currencyPunchScale=1.22f;
        public MatchController Match { get; private set; }
        public string SelectedId { get; private set; }
        OwnedUnit selectedOwned;
        public OwnedUnit SelectedOwned => SelectedId==null||Match?.Player==null?null:
            selectedOwned!=null&&selectedOwned.Definition.Id==SelectedId&&Match.Player.Owns(selectedOwned)
                ?selectedOwned:Match.Player.FindOwned(SelectedId,false);
        public Camera GameCamera { get; private set; }
        public RectTransform CanvasRoot { get; private set; }
        UnitArtCatalog art;
        bool ownsArtCatalog;
        ProjectileArtCatalog projectileArt;
        ProjectileVisualRegistry projectileVisuals;
        Font font;
        PhaseCountdownHud phaseCountdown;
        Font headingFont;
        Font symbolFont;
        Sprite briefDisplay, briefBodySprite, briefLidSprite;
        RectTransform briefLid, briefContents;
        CanvasGroup briefContentsGroup;
        Image briefBodyImage, briefLidImage, briefClosedImage;
        float briefOpen, briefTarget;
        int lastBriefRound;
        PouchState shownPouch;
        bool rerollBusy, rerollClosing;
        int rerollSequence;
        int selectedOfferSlot=-1;
        long selectedOfferToken;
        public float BriefOpenAmount => briefOpen;
        public bool BriefReady => Match.Phase==MatchPhase.Preparation && !Match.Paused && !Match.OnlineReady && !rerollBusy && briefOpen>=.999f;
        public int SelectedOfferSlot => selectedOfferSlot;
        RectTransform briefDrop, benchDrop, dragGhost, currencyCounter, currencyIcon;
        float currencyPopStarted=-1,currencyPopInitialScale=1;
        PouchState currencyPopPlayer;
        int currencyPopRound;
        RectTransform page,effects,activeModal;
        RectTransform modal
        {
            get=>activeModal;
            set
            {
                activeModal=value;
                if(phaseCountdown!=null)phaseCountdown.Suppressed=value!=null||MapTransitionActive||(Match!=null&&Match.IsTraining);
            }
        }
        CanvasScaler canvasScaler;
        RectTransform fullCanvas;
        Rect lastSafeArea;
        Vector2Int lastScreen;
        Text clockText, currencyText;
        string chosen = "bugaloo";
        readonly HashSet<string> chosenWhelps = new HashSet<string>(StringComparer.Ordinal);
        public IReadOnlyCollection<string> SelectedWhelps => chosenWhelps;
        OwnedUnit inspected;
        bool inspectedEnemy;
        Text inspectStats;
        readonly BattleInfoText.RowCache inspectionTextCache = new BattleInfoText.RowCache();
        readonly Dictionary<BattleUnit, UnitPresentation> visuals = new Dictionary<BattleUnit, UnitPresentation>();
        readonly Dictionary<BattleUnit, RectTransform> bars = new Dictionary<BattleUnit, RectTransform>();
        readonly Dictionary<BattleUnit,(OwnedUnit owned,UnitVitalsUI vitals,UnitStatusBadges statuses)> barViews = new Dictionary<BattleUnit,(OwnedUnit,UnitVitalsUI,UnitStatusBadges)>();
        RectTransform vitalsRoot;
        readonly List<(RectTransform rect,float screenY,int identity)> vitalsDepth = new List<(RectTransform,float,int)>();
        readonly List<GameObject> highlights = new List<GameObject>();
        readonly PointerGesture boardGesture = new PointerGesture();
        OwnedUnit pressedUnit;
        bool pressedEnemy, rebuild;
        Vector2 lastPointer;
        AudioSource audioSource;
        readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        Color ink = new Color(.045f,.085f,.12f, .96f), gold = new Color(1,.79f,.33f), cyan = new Color(.40f,.91f,.91f);

        void Awake()
        {
            SetupGuestProfile();
            MobileFrameRatePolicy.Initialize();
            originalSleepTimeout=Screen.sleepTimeout;
#if UNITY_ANDROID && !UNITY_EDITOR
            Screen.orientation=ScreenOrientation.Portrait;
            Screen.autorotateToLandscapeLeft=Screen.autorotateToLandscapeRight=false;
            Screen.autorotateToPortraitUpsideDown=false;
            Screen.fullScreenMode=FullScreenMode.FullScreenWindow;
            Screen.fullScreen=true;
#endif
            symbolFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // A simple sans serif keeps small numbers (especially 5/S) legible in the HUD.
            font = Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold")??symbolFont;
            headingFont=Resources.Load<Font>("MonsterPouch/UI/Fonts/LilitaOne-Regular")??font;
            if(font.material.mainTexture!=null)font.material.mainTexture.filterMode=FilterMode.Bilinear;
            // Display the original transparent artwork at usable size, without its empty margin.
            if(BriefArt!=null)
            {
                briefDisplay=Sprite.Create(BriefArt.texture,new Rect(112,306,1024,640),new Vector2(.5f,.5f),100);
                // Two non-destructive slices of the original case share its hinge.
                briefBodySprite=Sprite.Create(BriefArt.texture,new Rect(112,306,1024,458),new Vector2(.5f,.5f),100);
                briefLidSprite=Sprite.Create(BriefArt.texture,new Rect(112,764,1024,182),new Vector2(.5f,.5f),100);
            }
            var sceneBrief=GameObject.Find("briefcase");
            if(sceneBrief!=null && sceneBrief.TryGetComponent<SpriteRenderer>(out var briefRenderer))briefRenderer.enabled=false;
            art = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
            projectileArt = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt");
            Match = GetComponent<MatchController>();
            if (Match == null) Match = gameObject.AddComponent<MatchController>();
            var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            if (config == null) config = MatchConfig.CreateDefault();
            var board = FindFirstObjectByType<BoardManager>();
            var mapper = FindFirstObjectByType<BoardWorldMapper>();
            GameCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            CardTableArena.Apply(board,mapper,GameCamera);
            Match.Configure(config,board,mapper);
            projectileVisuals = new ProjectileVisualRegistry(config.Characters);
            if(art!=null&&config.Characters!=null&&config.Characters.Length>0){art=art.ForCharacters(config.Characters);ownsArtCatalog=true;}
            foreach (var prototype in FindObjectsByType<PrototypeUnitSpawner>(FindObjectsSortMode.None)) prototype.enabled = false;
            chosen = PlayerPrefs.GetString(LocalAccountPreferences.Key("monster-pouch.monster"),"bugaloo");
            if(config.Get(chosen)==null || !config.Get(chosen).IsMonster)chosen="bugaloo";
            string savedTeam=PlayerPrefs.GetString(LocalAccountPreferences.Key("monster-pouch.whelps"),"");
            if(!config.TryResolveWhelpSelection(string.IsNullOrEmpty(savedTeam)?null:savedTeam.Split(','),out var teamIds,out _))
                config.TryResolveWhelpSelection(null,out teamIds,out _);
            foreach(string id in teamIds)chosenWhelps.Add(id);
            BuildCanvas(); BuildSounds();
            Match.Changed += HandlePlacementMatchChanged;
            Match.PlacementExchanged += AnimatePlacementExchange;
            Match.ActorCreated += CreateVisual;
            Match.Displaced += HandleDisplaced;
            Match.Moved += (actor,from,to) => { if (visuals.TryGetValue(actor,out var v)) v.Move(Match.Mapper.GetWorldPosition(from), Match.Mapper.GetWorldPosition(to), actor.BaseStats.MoveInterval); };
            Match.Teleported += (actor,from,to) => { if(visuals.TryGetValue(actor,out var view))view.SnapToCell(); };
            Match.DashStarted += (actor,from,through,to,width,duration) =>
            {
                if (visuals.TryGetValue(actor,out var view))
                    view.Dash(Match.Mapper.GetWorldPosition(from), Match.Mapper.GetWorldPosition(through), Match.Mapper.GetWorldPosition(to), duration);
            };
            Match.FlowerCastStarted += (actor,cell,duration) =>
            {
                if (visuals.TryGetValue(actor,out var view)) view.PrepareFlower(duration);
            };
            Match.Attacked += (actor,target,projectile) =>
            {
                float delay=CombatSimulation.GetImpactDelay(actor,target);
                if (visuals.TryGetValue(actor,out var v)) v.Attack(target.transform.position,projectile,delay);
                if (projectile && !DocumentedBattleEffects.HandlesProjectile(actor)) StartCoroutine(Projectile(actor,target,delay));
            };
            Match.Died += actor => { if (visuals.TryGetValue(actor,out var v)) v.Die(); };
            Match.ProjectileBounced += (actor,origin,target,duration) => StartCoroutine(ReboundProjectile(actor,origin,target,duration));
            Match.Reviving += (actor,duration) => { if(visuals.TryGetValue(actor,out var v))v.BeginRevive(duration); };
            Match.Revived += actor => { if(visuals.TryGetValue(actor,out var v))v.Revive(); StartCoroutine(ImpactEffect(actor,actor,false)); };
            Match.Impacted += (actor,target,damage) => { if(damage>0&&visuals.TryGetValue(target,out var hitView))hitView.Hit(); }; // Contact art has one owner: DocumentedBattleEffects -> shared pool.
            Match.AbilityUsed += (actor,kind) => { if(visuals.TryGetValue(actor,out var abilityView))abilityView.UseAbility(kind); };
            // Lethal contacts use the same settled impact; do not replay a second, enlarged clip.
            Match.MoonkenDeliveryStarted += MoonkensProduced;
            Match.Sound += PlaySound;
            gameObject.AddComponent<DocumentedBattleEffects>().Configure(Match,CanvasRoot,GameCamera,projectileVisuals);
            gameObject.AddComponent<CombatStatusEffects>().Configure(Match,CanvasRoot,GameCamera);
            SetupOnline();
            phaseCountdown=gameObject.AddComponent<PhaseCountdownHud>();
            phaseCountdown.Configure(Match,CanvasRoot,GameCamera,headingFont??font);
            SetupNotices();
            SetupProgression();
            RenderPage();
            LoadingArtworkScreen.RevealReadyView(this);
        }
        void BuildCanvas()
        {
            GameObject go = new GameObject("Monster Pouch UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false); var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            canvasScaler = go.GetComponent<CanvasScaler>(); canvasScaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            canvas.pixelPerfect=false;
            fullCanvas=go.GetComponent<RectTransform>();
            CanvasRoot=Panel(fullCanvas,"Safe portrait",new Rect(0,0,540,960),Color.clear,false);
            CanvasRoot.anchorMin=CanvasRoot.anchorMax=CanvasRoot.pivot=new Vector2(.5f,.5f);
            CanvasRoot.gameObject.AddComponent<MenuSwipeGesture>().Configure(this);
            BuildEdgeToEdgeBackdrop();
            UpdateLayout();
            var boardInput=HitArea(CanvasRoot,"Board input",new Rect(0,0,540,960));
            boardInput.gameObject.AddComponent<BoardPointerGesture>().Configure(this);
            // This stays behind menu controls and survives page replacement. Empty
            // areas and transitions still route drags to the parent MenuSwipeGesture.
            menuSwipeSurface=HitArea(CanvasRoot,"Menu swipe surface",new Rect(0,0,540,960));
            page=Panel(CanvasRoot,"Page",new Rect(0,0,540,960),Color.clear,false);
            effects=Panel(CanvasRoot,"Effects",new Rect(0,0,540,960),Color.clear,false);
            var events=FindFirstObjectByType<EventSystem>();
            if(events==null) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            else if(events.GetComponent<InputSystemUIInputModule>()==null) { var old=events.GetComponent<BaseInputModule>(); if(old!=null) Destroy(old); events.gameObject.AddComponent<InputSystemUIInputModule>(); }
        }
        public void RenderPage()
        {
            ClearEconomyTutorialReturn();
            if(!EconomyTutorialActive)economyTutorialModule=null;
            rebuild=false;
            ApplyRoundMap();
            UpdateScreenAwakePolicy();
            ClearMenuNavigation();
            UpdateEdgeToEdgeVisibility();
            EndDragPreview();
            ResetRoundTransitionReferences();
            foreach(Transform child in page){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            clockText=currencyText=null;currencyCounter=currencyIcon=null;
            foreach(var bar in bars.Values) if(bar!=null) Destroy(bar.gameObject); bars.Clear();barViews.Clear();
            vitalsRoot=null;vitalsDepth.Clear();
            foreach(var pair in visuals.ToList()) if(pair.Key==null) visuals.Remove(pair.Key);
            if(Match.Phase==MatchPhase.RoundResult||Match.Phase==MatchPhase.MatchResult)
                foreach(var v in visuals.Values)if(v!=null)v.Freeze();
            if(Match.Phase==MatchPhase.Menu){CloseBankContents();RenderMenu();} else RenderGame();
            RenderOpponentEchoes();
            AnimatePageEntrance();
            RefreshHighlights();
            RefreshPlacementExchangeVisibility();
        }
        void RenderMenu()
        {
            if(ModernMenu){RenderModernMenu();return;}
            selectedOfferSlot=-1;lastBriefRound=0;shownPouch=null;briefOpen=briefTarget=0;
            rerollSequence++;rerollBusy=rerollClosing=false;
            Panel(page,"Menu shade",new Rect(0,0,540,960),new Color(.025f,.055f,.085f,.88f));
            Label(page,"GRAND CAS HOTEL",new Rect(50,35,440,23),13,cyan);
            Label(page,"MONSTER POUCH",new Rect(25,65,490,63),39,gold,FontStyle.Bold);
            Label(page,"CHOOSE YOUR MONSTER",new Rect(50,140,440,25),15,cyan);
            var monsters=Match.Config.Units.Where(d=>d!=null&&d.IsMonster).ToArray();
            float monsterWidth=(480-12*Mathf.Max(0,monsters.Length-1))/Mathf.Max(1,monsters.Length);
            for(int i=0;i<monsters.Length;i++)
            {
                string id=monsters[i].Id;float x=30+i*(monsterWidth+12);
                var card=Panel(page,id,new Rect(x,183,monsterWidth,200),chosen==id?new Color(.19f,.36f,.37f):ink);
                if(chosen==id)PixelBorder(card,gold,2);
                Portrait(card,id,new Rect(7,12,monsterWidth-14,132));
                Label(card,Match.Config.Get(id).DisplayName,new Rect(3,148,monsterWidth-6,28),17,Color.white,FontStyle.Bold);
                var button=card.gameObject.AddComponent<Button>(); button.onClick.AddListener(()=>{chosen=id;PlayerPrefs.SetString(LocalAccountPreferences.Key("monster-pouch.monster"),id);PlaySound("button");RenderPage();});
                Label(card,chosen==id?"SELECTED":"SELECT",new Rect(3,176,monsterWidth-6,18),11,chosen==id?gold:cyan);
            }
            Label(page,GameText.AbilityName(Match.Config.Get(chosen)?.BaseAbility),new Rect(35,390,470,26),15,gold);
            Label(page,"YOUR WHELPS  ·  "+chosenWhelps.Count+" SELECTED",new Rect(35,430,470,28),18,cyan);
            Label(page,"Tap to add or remove from your team",new Rect(35,463,470,23),13,Color.white);
            var collection=Match.Config.Units.Where(d=>d!=null&&!d.IsMonster&&art?.Get(d.Id)?.Portrait!=null).ToArray();
            for(int i=0;i<collection.Length;i++)
            {
                var definition=collection[i];string id=definition.Id;
                int countInRow=Mathf.Min(4,collection.Length-(i/4)*4);
                float rowLeft=(540-countInRow*116+8)/2f;
                var card=Panel(page,"loadout-"+id,new Rect(rowLeft+(i%4)*116,501+(i/4)*135,108,124),chosenWhelps.Contains(id)?new Color(.14f,.27f,.29f):ink);
                if(chosenWhelps.Contains(id))PixelBorder(card,gold,2);
                Portrait(card,id,new Rect(14,6,80,78));
                Label(card,definition.DisplayName,new Rect(3,84,102,20),15,Color.white);
                Label(card,chosenWhelps.Contains(id)?"IN TEAM":"ADD",new Rect(3,106,102,15),10,chosenWhelps.Contains(id)?gold:cyan);
                card.gameObject.AddComponent<Button>().onClick.AddListener(()=>ToggleWhelp(id));
            }
            Label(page,"Your selected Whelps will appear in the Brief.",new Rect(35,777,470,30),13,Color.white);
            var battle=Button(page,"BATTLE THE BOT",new Rect(60,820,420,56),()=>{if(!PrepareCurrentDeckForMatch()){RenderPage();return;}SelectedId=null;CloseModal();SaveTeam();Match.StartMatch(chosen,chosenWhelps,localMap:CardTableTheme.MapId(CardTableTheme.Current));},gold,ink);
            battle.name="JUGAR CONTRA BOT";
            Button(page,"How to play",new Rect(60,895,198,40),ShowHelp,new Color(.15f,.26f,.30f),Color.white,15);
            Button(page,"Sound",new Rect(282,895,198,40),ShowAudio,new Color(.15f,.26f,.30f),Color.white,15);
        }
        public void ToggleWhelp(string id)
        {
            var next=new HashSet<string>(chosenWhelps,StringComparer.Ordinal);
            if(!next.Remove(id))next.Add(id);
            if(!Match.Config.TryResolveWhelpSelection(next,out var ids,out var reason))Match.Reject(reason);
            else {chosenWhelps.Clear();foreach(string selected in ids)chosenWhelps.Add(selected);SaveTeam();PlaySound("button");}
            RenderPage();
        }
        void SaveTeam(){PlayerPrefs.SetString(LocalAccountPreferences.Key("monster-pouch.whelps"),string.Join(",",chosenWhelps.OrderBy(id=>id,StringComparer.Ordinal)));}
        void RenderGame()
        {
            if(RenderFirstContactTutorial()||RenderGuidedTutorial())return;
            RenderRoundHud();
            RenderBrief();
            foreach(var pair in Match.Actors) CreateBar(pair.Value,pair.Key);
            if(Match.Phase==MatchPhase.RoundResult || Match.Phase==MatchPhase.MatchResult) RenderRoundTransition();
            // Keep this above the result's full-screen hit surface. Draw the icon
            // directly so mobile font atlases cannot drop the pause glyph.
            var pause=Button(page,"",new Rect(253,8,68,68),ShowPause,ink,Color.white);
            pause.name="Battle pause";
            PixelBorder(pause,gold,2);
            Panel(pause,"Pause left bar",new Rect(23,21,8,26),Color.white,false);
            Panel(pause,"Pause right bar",new Rect(37,21,8,26),Color.white,false);
        }
        void RenderBrief()
        {
            bool prep=Match.Phase==MatchPhase.Preparation;
            if(shownPouch!=Match.Player || lastBriefRound!=Match.Round)
            {
                shownPouch=Match.Player;lastBriefRound=Match.Round;briefOpen=0;selectedOfferSlot=-1;
                rerollSequence++;rerollBusy=rerollClosing=false;
            }
            briefTarget=prep&&!rerollClosing?1:0;
            var tray=Panel(page,"Brief tray",new Rect(0,CardTableArena.BriefTop,540,960-CardTableArena.BriefTop),Color.clear,false);
            var body=Panel(tray,"Brief body",new Rect(12,45,350,157),Color.clear,false);
            briefBodyImage=body.gameObject.AddComponent<Image>();briefBodyImage.sprite=briefBodySprite;briefBodyImage.raycastTarget=false;
            briefDrop=HitArea(tray,"Brief drop",new Rect(12,0,350,202));
            briefContents=Panel(tray,"Brief contents",new Rect(0,0,540,202),Color.clear,false);
            briefContentsGroup=briefContents.gameObject.AddComponent<CanvasGroup>();
            for(int slot=0;slot<3;slot++)
            {
                var stored=Match.Player.Owned.Values.FirstOrDefault(o=>o.Location==UnitLocation.Brief && o.BriefSlot==slot);
                var offer=Match.Player.Offers[slot];
                string id=stored?.Definition.Id??offer?.UnitId;
                var card=HitArea(briefContents,"brief-slot-"+slot,new Rect(46+slot*97,57,92,90));
                if(id==null)continue;
                card.name=stored!=null?"brief-"+id:"offer-"+slot;
                if(stored!=null?SelectedId==id && selectedOfferSlot<0:selectedOfferSlot==slot && selectedOfferToken==offer.Token)
                    PixelBorder(card,gold,2);
                Portrait(card,id,new Rect(6,3,80,82));
                if(stored!=null)
                {
                    Label(card,Stars(stored.TrickCount),new Rect(8,-12,76,15),12,gold);
                    AddUnitGesture(card,stored,false);
                }
                else
                {
                    int price=Match.Player.GetOfferCost(slot);
                    var priceTag=Panel(briefContents,"Offer price",new Rect(68+slot*97,150,48,18),ink,false);
                    Image(priceTag,MoonIcon,new Rect(4,3,12,12));
                    Label(priceTag,price<0?"—":price.ToString(),new Rect(18,0,26,18),14,gold,FontStyle.Bold);
                    if(Match.Player.Owned.TryGetValue(id,out var own) && own.TrickCount>0)
                        Label(card,Stars(own.TrickCount),new Rect(8,-12,76,15),12,gold);
                    card.gameObject.AddComponent<BriefOfferGesture>().Configure(this,slot,offer.Token,id);
                }
            }
            briefLid=Panel(tray,"Brief lid",new Rect(12,0,350,45),Color.clear,false);
            briefLid.pivot=new Vector2(.5f,0);briefLid.anchoredPosition=new Vector2(187,-45);
            briefLidImage=briefLid.gameObject.AddComponent<Image>();briefLidImage.sprite=briefLidSprite;briefLidImage.raycastTarget=false;
            var closed=Panel(tray,"Brief closed",new Rect(12,45,350,157),Color.clear,false);
            closed.pivot=new Vector2(.5f,.5f);closed.anchoredPosition=new Vector2(187,-123.5f);
            briefClosedImage=closed.gameObject.AddComponent<Image>();briefClosedImage.sprite=ClosedBriefArt;briefClosedImage.preserveAspect=true;briefClosedImage.raycastTarget=false;
            RenderBank(tray);
            // Income travels here during combat too, so its destination and balance stay visible.
            var resources=Panel(tray,"Battle resources",new Rect(375,54+BankColumnOffset,72,72),Color.clear,false);
            currencyCounter=resources;
            Image(resources,MoonIcon,new Rect(6,2,60,60));
            currencyIcon=resources.Find("Art") as RectTransform;
            if(currencyIcon!=null)currencyIcon.name="Moonkens icon";
            currencyText=Label(resources,Match.Player.Coins.ToString(),new Rect(0,22,72,46),30,Color.white,FontStyle.Bold);
            var coinOutline=currencyText.gameObject.AddComponent<Outline>();coinOutline.effectColor=new Color(.025f,.025f,.09f,1);coinOutline.effectDistance=new Vector2(2,-2);
            ApplySharedBriefLayout(tray);
            if(!prep){ApplyBriefPose();return;}
            int offerVersion=Match.Player.OfferVersion;
            RenderRerollButton(tray,new Rect(453,54+BankColumnOffset,72,72),offerVersion);
            RenderReadyButton(tray);
            ApplyBriefPose();
        }
        void UpdateBriefMotion()
        {
            if(!Match.Paused)briefOpen=Mathf.MoveTowards(briefOpen,briefTarget,Time.unscaledDeltaTime/.32f);
            ApplyBriefPose();
        }
        void ApplyBriefPose()
        {
            if(briefLid==null||briefContentsGroup==null)return;
            // A hinged lid made from the original pixel artwork; the figures sink inside.
            float open=Mathf.SmoothStep(0,1,briefOpen);
            briefLid.sizeDelta=new Vector2(350,Mathf.Lerp(157,45,open));
            briefLid.localRotation=Quaternion.Euler((1-open)*180,0,0);
            // Blend to a separately drawn closed case as the hinge finishes closing.
            // The open artwork is never used as the exterior of the shut suitcase.
            float closed=ClosedBriefArt==null?0:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.4f,open));
            briefClosedImage.color=new Color(1,1,1,closed);
            briefBodyImage.color=briefLidImage.color=new Color(1,1,1,1-closed);
            float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,open));
            briefContentsGroup.alpha=bankVisible?0:reveal;
            briefContentsGroup.blocksRaycasts=BriefReady&&!bankVisible;
            briefContents.anchoredPosition=new Vector2(0,-(1-reveal)*14);
        }
        public void RequestReroll(int version)
        {
            if(!BriefReady)return;
            StartRerollFeedback();
            CloseBankContents();
            if(Match.Player.Rerolls<=0){Match.Reroll(version);return;}
            selectedOfferSlot=-1;SelectedId=null;CancelBoardPointer();
            StartCoroutine(CycleBrief(version,++rerollSequence));
        }
        IEnumerator CycleBrief(int version,int sequence)
        {
            var owner=Match.Player;int round=Match.Round;
            rerollBusy=rerollClosing=true;briefTarget=0;
            while(briefOpen>0 && Match.Player==owner && Match.Round==round && Match.Phase==MatchPhase.Preparation)yield return null;
            if(sequence==rerollSequence && Match.Player==owner && Match.Round==round && Match.Phase==MatchPhase.Preparation)
            {
                while(Match.Paused)yield return null;
                Match.Reroll(version);rerollClosing=false;briefTarget=1;
                while(briefOpen<1 && Match.Player==owner && Match.Round==round && Match.Phase==MatchPhase.Preparation)yield return null;
            }
            if(sequence==rerollSequence){rerollBusy=rerollClosing=false;briefTarget=Match.Phase==MatchPhase.Preparation?1:0;}
        }
        bool CurrentOffer(int slot,long token,out PouchOffer offer)
        {
            offer=Match.Player!=null && slot>=0 && slot<3?Match.Player.Offers[slot]:null;
            return offer!=null && offer.Token==token;
        }
        public void SelectOffer(int slot,long token)
        {
            if(!BriefReady||!CurrentOffer(slot,token,out var offer))return;
            if(Match.Player.Owned.TryGetValue(offer.UnitId,out var owned))
            {
                var offerCard=page.GetComponentsInChildren<RectTransform>().FirstOrDefault(item=>item.name=="offer-"+slot);
                Vector2 from=UpgradePortraitCenter(offerCard,Vector2.zero);
                if(Match.Buy(slot,token))StartCoroutine(UpgradeStar(from,owned));
                selectedOfferSlot=-1;SelectedId=offer.UnitId;selectedOwned=owned;return;
            }
            selectedOfferSlot=slot;selectedOfferToken=token;SelectedId=offer.UnitId;selectedOwned=null;
            rebuild=true;
        }
        public void InspectOffer(int slot,long token)
        {
            if(!BriefReady||!CurrentOffer(slot,token,out var offer))return;
            ShowInspection(Match.Player.Owned.TryGetValue(offer.UnitId,out var owned)?owned:new OwnedUnit(Match.Config.Get(offer.UnitId)),false);
        }
        public void PreviewOffer(int slot,long token,Vector2 screen)
        {
            if(!BriefReady||!CurrentOffer(slot,token,out var offer))return;
            DragPreview(new OwnedUnit(Match.Config.Get(offer.UnitId)),screen);
        }
        public void DropOffer(int slot,long token,Vector2 screen)
        {
            EndDragPreview();
            if(!BriefReady||!CurrentOffer(slot,token,out var offer))return;
            if(Contains(briefDrop,screen))return;
            bool bank=Contains(benchDrop,screen);
            BoardCell cell=null;
            if(!bank && !Match.Mapper.TryGetCell(GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-GameCamera.transform.position.z)),out cell))
            {Match.Reject("Elige una casilla de tu campo o el banco.");return;}
            Match.Player.Owned.TryGetValue(offer.UnitId,out var existing);
            if(Match.BuyAndPlace(slot,token,cell,bank))
            {
                SelectedId=offer.UnitId;selectedOfferSlot=-1;
                selectedOwned=Match.Player.FindOwned(offer.UnitId,bank);
                if(!bank&&existing!=null)StartCoroutine(UpgradeStar(ToCanvas(screen),existing));
            }
        }
        void CreateVisual(BattleUnit actor, OwnedUnit owned)
        {
            var v=actor.gameObject.AddComponent<UnitPresentation>();
            v.Configure(actor,Match.Mapper,art?.Get(owned.Definition.Id)); visuals[actor]=v;
            if(actor.BaseStats.UsesEronyaRules)
                v.BindActionPreparation(()=>Match.Simulation?.CaptureActionPreparation(actor)??default,owned.Definition.BaseAbility?.EffectId);
                v.BindDagataTiming(()=>Match.Simulation?.Elapsed??float.NaN,()=>Match.Simulation?.CaptureDagataJump(actor)??default);
            if(Match.Phase==MatchPhase.Combat)CreateBar(actor,owned);
        }
        void CreateBar(BattleUnit actor,OwnedUnit owned)
        {
            if(bars.TryGetValue(actor,out var existing)&&existing!=null)return;
            if(vitalsRoot==null)
            {
                vitalsRoot=Panel(page,"Unit vitals",new Rect(0,0,540,960),Color.clear,false);
                vitalsRoot.SetAsFirstSibling();
            }
            var bar=Panel(vitalsRoot,"health-"+actor.UnitId,new Rect(0,0,52,28),Color.clear,false);
            var display=bar.gameObject.AddComponent<UnitVitalsUI>();display.Refresh(actor,owned,0,Match.LocalSide);
            var statuses=bar.gameObject.AddComponent<UnitStatusBadges>();
            barViews[actor]=(owned,display,statuses);
            bars[actor]=bar;
        }
        void Update()
        {
            if(lastScreen.x!=Screen.width||lastScreen.y!=Screen.height||lastSafeArea!=Screen.safeArea)UpdateLayout();
            if(rebuild)RenderPage();
            UpdateOpponentEchoes();
            UpdateRoundHud();
            if(phaseCountdown!=null)phaseCountdown.Suppressed=modal!=null||MapTransitionActive||Match.IsTraining;
            UpdateRoundTransition();
            if(inspectStats!=null && inspected!=null)inspectStats.text=GameText.Resolve(StatsText(inspected));
            UpdateBriefMotion();
            UpdateRerollFeedback();
            UpdateCurrencyPop();
            UpdateBankMotion();
            UpdateReadyMotion();
            if(!LoadingArtworkScreen.IsBlocking)UpdateOnlineKeyboard();
            UpdateBoardGesture();
            UpdateGuidedTutorial();
            if(!LoadingArtworkScreen.IsBlocking && !MapTransitionActive && !StarterSelectionOpen && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if(modal!=null){if(Match.Paused)Match.Pause(false);CloseModal();}else if(Match.Phase!=MatchPhase.Menu)ShowPause(); }
        }
        void UpdateLayout()
        {
            lastScreen=new Vector2Int(Screen.width,Screen.height);lastSafeArea=Screen.safeArea;
            Rect safe=lastSafeArea.width>0?lastSafeArea:new Rect(0,0,Screen.width,Screen.height);
            ApplyViewportLayout(safe);
        }
        void LateUpdate()
        {
            UpdateAttackRangePreview();
            UpdateBoardSelectionFeedback();
            UpdateModalScreenShield();
            RaiseMenuNavigation();
            vitalsDepth.Clear();
            foreach(var pair in bars)
            {
                var actor=pair.Key;var bar=pair.Value;if(actor==null||bar==null)continue;
                bool alive=actor.IsAlive && !actor.IsReviving && (Match.Phase==MatchPhase.Preparation || Match.Phase==MatchPhase.Combat);
                bar.gameObject.SetActive(alive);if(!alive)continue;
                Vector3 head=visuals.TryGetValue(actor,out var view)&&view!=null
                    ? view.VitalsHeadWorldPosition : actor.transform.position;
                Vector2 p=ToCanvas(GameCamera.WorldToScreenPoint(head));
                float bottom=0;
                if(barViews.TryGetValue(actor,out var cached))
                {
                    cached.vitals.Refresh(actor,cached.owned,Match.Paused?0:Time.deltaTime,Match.LocalSide);
                    cached.statuses.Refresh(Match.Phase==MatchPhase.Combat?Match.Simulation:null,actor);
                    bottom=cached.vitals.BottomEdge;
                }
                SetRect(bar,new Rect(p.x-26,p.y-3-(28-bottom),52,28));
                vitalsDepth.Add((bar,GameCamera.WorldToScreenPoint(actor.transform.position).y,actor.GetInstanceID()));
            }
            // Only reorder complete HUD blocks inside their own layer; never raise them above modals.
            vitalsDepth.Sort((a,b)=>{int order=b.screenY.CompareTo(a.screenY);return order!=0?order:a.identity.CompareTo(b.identity);});
            for(int i=0;i<vitalsDepth.Count;i++)
                if(vitalsDepth[i].rect.GetSiblingIndex()!=i)vitalsDepth[i].rect.SetSiblingIndex(i);
        }
        public void Select(OwnedUnit owned,bool enemy=false)
        {
            if(TrySelectCombatUnit(owned))return;
            if(Match.Actors.TryGetValue(owned,out var selectedActor)&&selectedActor.IsCombatSummon){ShowInspection(owned,enemy);return;}
            if(enemy){ShowInspection(owned,true);return;}
            selectedOfferSlot=-1;
            CloseBankContents();
            SelectedId=owned.Definition.Id;selectedOwned=owned;PlaySound("button");rebuild=true;
        }
        void AddUnitGesture(RectTransform card,OwnedUnit owned,bool enemy)
        {
            var g=card.gameObject.AddComponent<UnitCardGesture>();g.Configure(this,owned,enemy);
        }
        public void Drop(OwnedUnit owned,Vector2 screen)
        {
            EndDragPreview();
            if(Match.Phase!=MatchPhase.Preparation||Match.Paused||owned==null)return;
            if(!Match.Player.Owns(owned))return;
            SelectedId=owned.Definition.Id;selectedOwned=owned;
            if(Contains(benchDrop,screen)){Match.Store(owned,true);return;}
            if(Contains(briefDrop,screen))
            {
                if(!BriefReady){Match.Reject("Espera a que se abra el Brief.");return;}
                Vector2 soldFrom=ToCanvas(RectTransformUtility.WorldToScreenPoint(null,
                    briefBodyImage.rectTransform.TransformPoint(briefBodyImage.rectTransform.rect.center)));
                int coinsBefore=Match.Player.Coins;
                // The large Brief is the sell destination for both field and bank troops.
                if(Match.Sell(owned))
                {
                    SelectedId=null;CloseBankContents();
                    int refund=Match.Player.Coins-coinsBefore;
                    if(refund>0)StartCoroutine(SellMoonkens(soldFrom,refund));
                }
                return;
            }
            if(Match.Mapper.TryGetCell(GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-GameCamera.transform.position.z)),out var cell)){if(Match.Place(owned,cell))CloseBankContents();}
            rebuild=true;
        }
        static bool Contains(RectTransform target,Vector2 screen)=>target!=null&&RectTransformUtility.RectangleContainsScreenPoint(target,screen,null);
        public void DragPreview(OwnedUnit owned,Vector2 screen)
        {
            if(Match.Phase!=MatchPhase.Preparation||Match.Paused)return;
            if(dragGhost==null)
            {
                dragGhost=Panel(effects,"Dragged Whelp",new Rect(0,0,74,88),Color.clear,false);
                Portrait(dragGhost,owned.Definition.Id,new Rect(5,0,64,76),new Color(1,1,1,.8f));
            }
            PreviewBankDrop(owned,screen);
            PreviewAttackRange(owned,screen);
            var p=ToCanvas(screen);SetRect(dragGhost,new Rect(p.x-37,p.y-70,74,88));
            UpdateBoardSelectionFeedback();
        }
        public void EndDragPreview(){rangeDragUnit=null;rangeDragCell=null;EndBankDrag();if(dragGhost!=null){dragGhost.gameObject.SetActive(false);Destroy(dragGhost.gameObject);}dragGhost=null;UpdateBoardSelectionFeedback();}
        public bool BeginBoardPointer(Vector2 pos)
        {
            if(!UnitHoldAllowed||boardGesture.Active)return false;
            // A new press owns its source; no preview from an earlier gesture survives it.
            EndDragPreview();
            pressedUnit=HitUnit(pos,out pressedEnemy);
            boardGesture.Begin(pos,Time.unscaledTime);lastPointer=pos;
            if(pressedUnit!=null)UpdateUnitHoldProgress(boardGesture,pos);
            return true;
        }
        public void MoveBoardPointer(Vector2 pos)
        {
            lastPointer=pos;
            UpdateBoardGesture();
        }
        void UpdateBoardGesture()
        {
            if(!UnitHoldAllowed){CancelBoardPointer();return;}
            if(!boardGesture.Active){HideUnitHoldProgress(boardGesture);return;}
            var gesture=boardGesture.Move(lastPointer,Time.unscaledTime);
            if(pressedUnit!=null)UpdateUnitHoldProgress(boardGesture,lastPointer);
            if(gesture==GestureResult.Hold && pressedUnit!=null)ShowInspectionFromGuidedTutorialHold(pressedUnit,pressedEnemy);
            else if(boardGesture.Dragging && pressedUnit!=null && !pressedEnemy)DragPreview(pressedUnit,lastPointer);
        }
        public void EndBoardPointer(Vector2 pos)
        {
            if(boardGesture.Active)
            {
                // The last movement and release can arrive during the same frame.
                MoveBoardPointer(pos);
                var result=boardGesture.End();
                if(result==GestureResult.Tap)
                {
                    if(pressedUnit!=null)Select(pressedUnit,pressedEnemy);
                    else if(selectedOfferSlot>=0)DropOffer(selectedOfferSlot,selectedOfferToken,pos);
                    else if(SelectedOwned is OwnedUnit owned)DropWithGuidedTutorialGesture(owned,pos,result);
                }
                else if(result==GestureResult.Drag && pressedUnit!=null&&!pressedEnemy)DropWithGuidedTutorialGesture(pressedUnit,pos,result);
            }
            CancelBoardPointer();
        }
        internal bool BoardPointerActive => boardGesture.Active;
        public void CancelBoardPointer()
        {boardGesture.Cancel();pressedUnit=null;pressedEnemy=false;HideUnitHoldProgress(boardGesture);EndDragPreview();}
        OwnedUnit HitUnit(Vector2 screen,out bool enemy)
        {
            if(Match.Phase==MatchPhase.Preparation)
            {
                // Placement targets belong to cells, even when another figure overlaps
                // them or an exchange temporarily hides their destination renderer.
                var world=GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-GameCamera.transform.position.z));
                if(Match.Mapper.TryGetCell(world,out var cell)&&cell.OccupiedBy!=null)
                    foreach(var pair in Match.Actors)
                        if(ReferenceEquals(pair.Value,cell.OccupiedBy)&&pair.Value.IsAlive&&pair.Value.Side==Match.LocalSide)
                        {enemy=false;return pair.Key;}
                // Keep off-cell figure/echo inspection; never expose a hidden rival formation.
                var visibleOwn = HitVisibleOwnSprite(screen);
                if(visibleOwn != null) { enemy=false; return visibleOwn; }
                var point=ToCanvas(screen);
                foreach(var echo in echoHitRects.Reverse())if(echo.Value.Contains(point))
                {enemy=true;return Match.InspectOpponent(echo.Key);}
            }
            enemy=false;OwnedUnit best=null;float min=45*Screen.height/960f;
            foreach(var pair in Match.Actors)
            {
                if(pair.Value==null||!pair.Value.IsAlive)continue;
                Vector2 point=GameCamera.WorldToScreenPoint(pair.Value.transform.position+Vector3.up*.45f);
                float distance=Vector2.Distance(screen,point);if(distance<min){min=distance;best=pair.Key;enemy=pair.Value.Side!=Match.LocalSide;}
            }
            return best;
        }
        static Vector2 InputPosition()
        {
            if(Touchscreen.current!=null && (Touchscreen.current.primaryTouch.press.isPressed||Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))return Touchscreen.current.primaryTouch.position.ReadValue();
            return Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero;
        }
        public Vector2 ToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRoot,screen,null,out var local);
            return new Vector2(local.x+CanvasRoot.rect.width/2,CanvasRoot.rect.height/2-local.y);
        }
        void RefreshHighlights()
        {
            rangePreviewDirty=true;
            foreach(var h in highlights)if(h!=null){h.SetActive(false);Destroy(h);}highlights.Clear();
            var owned=BoardSelectionFeedbackUnit();
            if(owned==null || owned.IsPositionLocked(Match.Round))return;
            foreach(var cell in Match.Board.GetAllCells())
            {
                if(!Match.Config.IsDeploymentLegal(Match.LocalSide,new Vector2Int(cell.X,cell.Y))||cell.OccupiedBy!=null)continue;
                var go=new GameObject("Legal cell");go.transform.SetParent(transform);go.transform.position=Match.Mapper.GetWorldPosition(cell)+Vector3.back*.05f;
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");line.startColor=line.endColor=new Color(.55f,1,.76f,1);line.startWidth=line.endWidth=.06f;line.positionCount=5;line.useWorldSpace=false;line.sortingOrder=30;
                float x=Mathf.Abs(Match.Mapper.CellSize.x)*.45f,y=Mathf.Abs(Match.Mapper.CellSize.y)*.45f;
                line.SetPositions(new[]{new Vector3(-x,-y),new Vector3(-x,y),new Vector3(x,y),new Vector3(x,-y),new Vector3(-x,-y)});highlights.Add(go);
            }
        }
        public void ShowInspection(OwnedUnit owned,bool enemy)
        {
            if(owned.Definition.UsesDocumentedRules){ShowDocumentedInspection(owned,enemy);return;}
            CloseModal();inspected=owned;inspectedEnemy=enemy;
            bool summoned=Match.Actors.TryGetValue(owned,out var inspectedActor)&&inspectedActor.IsCombatSummon;
            modal=Panel(CanvasRoot,"Inspection shade",new Rect(0,0,540,960),new Color(0,0,0,.55f));
            var box=Panel(modal,"Unit sheet",new Rect(28,158,484,680),ink);
            var summonSprite = summoned && owned.Definition.Id == "dummy"
                ? inspectedActor.GetComponent<UnitPresentation>()?.Renderer?.sprite : null;
            if (summonSprite != null) Image(box,summonSprite,new Rect(15,15,90,94));
            else Portrait(box,owned.Definition.Id,new Rect(15,15,90,94));
            Label(box,owned.Definition.DisplayName+(enemy?" · OPPONENT":""),new Rect(113,26,344,40),27,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
            inspectStats=Label(box,StatsText(owned),new Rect(24,113,436,88),17,Color.white,FontStyle.Normal,TextAnchor.MiddleLeft);
            Label(box,"ABILITY · "+GameText.AbilityName(owned.Definition.BaseAbility),new Rect(24,214,436,30),16,cyan,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(box,GameText.AbilityDescription(owned.Definition.BaseAbility,owned.Definition),new Rect(24,243,436,61),16,Color.white,FontStyle.Normal,TextAnchor.UpperLeft);
            int row=0;
            if(owned.Definition.IsMonster&&!AbilityBalance.IsRetiredArmorEffect(owned.Definition.MonsterUpgrade?.EffectId??owned.Definition.MonsterUpgrade?.Id))
            {
                var trick=owned.Definition.MonsterUpgrade;
                if(!enemy||owned.HasMonsterUpgrade)Label(box,(owned.HasMonsterUpgrade?"◆ ":"")+GameText.AbilityName(trick),new Rect(24,324,436,30),20,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
                if(!enemy||owned.HasMonsterUpgrade)Label(box,GameText.AbilityDescription(trick,owned.Definition),new Rect(24,365,436,103),17,Color.white,FontStyle.Normal,TextAnchor.UpperLeft);
                if(!enemy)Label(box,owned.HasMonsterUpgrade?(GameText.Spanish?"Trick adquirido":"Trick purchased"):"Cost: "+trick.Cost+" Moonkens",new Rect(24,488,436,35),17,cyan);
                if(!enemy && !owned.HasMonsterUpgrade && Match.Phase==MatchPhase.Preparation)
                    Button(box,"TRICK · "+trick.Cost+" MOONKENS",new Rect(94,542,296,45),()=>{Match.UpgradeMonster();ShowInspection(owned,false);},gold,ink,16);
            }
            else if(summoned)
                Label(box,"SUMMONED BY KAYON\n\nThis Dummy fights for this round, then disappears.",new Rect(24,324,436,128),17,cyan,FontStyle.Normal,TextAnchor.UpperLeft);
            else if(!owned.Definition.IsMonster)for(int i=0;i<3;i++)
            {
                if(enemy&&!owned.Tricks[i])continue;
                int index=i;var trick=owned.Definition.Tricks[i];if(trick==null||AbilityBalance.IsRetiredArmorEffect(trick.EffectId??trick.Id))continue;float y=316+row*90;
                Label(box,(owned.Tricks[i]?"★ ":"☆ ")+GameText.AbilityName(trick),new Rect(24,y,318,25),17,owned.Tricks[i]?gold:Color.white,FontStyle.Bold,TextAnchor.MiddleLeft);
                Label(box,GameText.AbilityDescription(trick,owned.Definition),new Rect(24,y+28,418,55),14,new Color(.86f,.92f,.94f),FontStyle.Normal,TextAnchor.UpperLeft);
                if(!enemy&&owned.Copies>0&&!owned.Tricks[i]&&Match.Phase==MatchPhase.Preparation&&Match.Player.Offers.Any(o=>o!=null&&o.UnitId==owned.Definition.Id))
                {
                    var choice=Button(box,"",new Rect(352,y,107,32),()=>{Match.ChooseTrick(owned.Definition.Id,index);ShowInspection(owned,false);},new Color(.22f,.36f,.38f),gold,12);
                    SetMoonkenButtonCost(choice,owned.NextTrick==i?"SELECTED":"",owned.NextTrick==i?null:(int?)trick.Cost);
                }
                row++;
            }
            var close=Button(box,GameText.Close,new Rect(24,614,436,43),CloseModal,new Color(.19f,.31f,.35f),Color.white,17);
            close.name="CERRAR";
        }
        string StatsText(OwnedUnit owned)
        {
            Match.Actors.TryGetValue(owned,out var actor);
            bool spanish=GameText.Spanish;
            if(actor!=null&&Match.Phase!=MatchPhase.Preparation&&Match.Phase!=MatchPhase.Menu)
            {
                if(Match.Simulation!=null)return inspectionTextCache.Format(owned,Match.Simulation.CaptureBattleStats(actor),spanish);
                var current=actor.BaseStats;
                return inspectionTextCache.Rows(owned,actor,actor.CombatResetVersion,actor.PreciseHealth,actor.RoundBaseHealth,current.Attack,actor.RoundBaseDamage,
                    current.AttackInterval>0?1m/(decimal)current.AttackInterval:0,actor.RoundBaseAttacksPerSecond,actor.Energy,current.EnergyMax,spanish);
            }
            var stats=UnitStats.FromDefinition(owned.Definition,owned,true);
            float aps=stats.AttackInterval>0?1f/stats.AttackInterval:0;
            return inspectionTextCache.Rows(owned,null,0,stats.MaxHealth,stats.MaxHealth,stats.Attack,stats.Attack,(decimal)aps,aps,0,stats.EnergyMax,spanish);
        }
        bool BattleMenuOpen=>modal!=null&&(modal.name=="Pause"||modal.name=="Help"||modal.name=="Audio");
        void ShowPause()
        {
            if(Match.Phase==MatchPhase.Menu)return;
            Match.Pause(true);
            CloseModal();modal=Panel(CanvasRoot,"Pause",new Rect(0,0,540,960),new Color(.02f,.055f,.08f,.95f));
            Label(modal,Match.IsOnline?"PARTIDA EN LÍNEA":"PAUSED",new Rect(40,230,460,64),46,gold,FontStyle.Bold);
            if(Match.IsOnline)Label(modal,GameText.Spanish?"La partida continúa mientras abres este menú":"The match continues while this menu is open",new Rect(40,302,460,34),17,cyan);
            var resume=Button(modal,"CONTINUE",new Rect(64,352,412,64),()=>{CloseModal();Match.Pause(false);},gold,ink);
            resume.name="CONTINUAR";
            Button(modal,"How to play",new Rect(64,440,412,64),ShowHelp,new Color(.17f,.29f,.33f),Color.white);
            Button(modal,"Sound",new Rect(64,528,412,64),ShowAudio,new Color(.17f,.29f,.33f),Color.white);
            var back=Button(modal,"BACK TO MENU",new Rect(64,640,412,64),()=>{CloseModal();SelectedId=null;Match.Menu();},new Color(.25f,.16f,.20f),Color.white);
            back.name="VOLVER AL MENÚ";
        }
        void ShowHelp()
        {
            CloseModal();modal=Panel(CanvasRoot,"Help",new Rect(0,0,540,960),new Color(.025f,.065f,.09f,.97f));
            Label(modal,"YOUR FIRST MATCH",new Rect(64,100,412,56),30,gold,FontStyle.Bold);
            Label(modal,"The Brief opens with three available Whelps. Drag one onto the board to buy and place it, or tap it and then tap a tile.\n\nA copy of one of your Whelps buys its next Trick. Hold a figure to inspect it.\n\nStore up to three Whelps in the bank. Storing a field Whelp refunds its investment and keeps its Tricks. Reserving a new Whelp from the Brief is free. Tap the bank to see each deployment cost; drag a Whelp onto the board to pay it. Banked types stay out of the Brief rotation. To sell a troop, drag it into the large Brief. Field troops refund their investment; banked troops give no additional refund.\n\nReroll closes the case and reveals new copies. Press READY or wait 40 seconds to fight. Losing your Monster does not end the round: your Whelps keep fighting until a team has no units left. Win three rounds.",new Rect(64,184,412,518),18,Color.white,FontStyle.Normal,TextAnchor.UpperLeft);
            if(Match.Phase==MatchPhase.Menu)
            {
                var tutorial=Button(modal,GameText.Spanish?"TUTORIAL Y PRÁCTICA":"TUTORIAL AND PRACTICE",new Rect(64,716,412,68),ShowTutorialReviewMenu,ink,Color.white,19);
                tutorial.name="Open tutorial review";
            }
            Button(modal,"GOT IT",new Rect(64,800,412,68),()=>{CloseModal();if(Match.Phase!=MatchPhase.Menu)ShowPause();else OpenHome();},gold,ink);
        }
        void ShowAudio()
        {
            CloseModal();modal=Panel(CanvasRoot,"Audio",new Rect(0,0,540,960),new Color(.025f,.065f,.09f,.97f));
            Label(modal,GameText.Sound,new Rect(40,260,460,60),40,gold,FontStyle.Bold);
            Label(modal,"Volume: "+Mathf.RoundToInt(audioSource.volume*100)+"%",new Rect(40,375,460,40),24,Color.white);
            Button(modal,"−",new Rect(64,448,132,64),()=>SetVolume(audioSource.volume-.1f),new Color(.18f,.3f,.34f),Color.white,34);
            Button(modal,"MUTE",new Rect(204,448,132,64),()=>SetVolume(audioSource.volume>0?0:.7f),new Color(.18f,.3f,.34f),Color.white,17);
            Button(modal,"+",new Rect(344,448,132,64),()=>SetVolume(audioSource.volume+.1f),new Color(.18f,.3f,.34f),Color.white,34);
            Button(modal,GameText.Back,new Rect(64,636,412,64),()=>{CloseModal();if(Match.Phase!=MatchPhase.Menu)ShowPause();else ShowSettings();},gold,ink);
        }
        void SetVolume(float volume){audioSource.volume=Mathf.Clamp01(volume);PlayerPrefs.SetFloat("monster-pouch.volume",audioSource.volume);ShowAudio();}
        public void CloseModal(){if(modal!=null){modal.gameObject.SetActive(false);Destroy(modal.gameObject);}modal=null;inspected=null;inspectStats=null;inspectionTextCache.Clear();CancelBoardPointer();HideAllUnitHoldProgress();}
        void BuildSounds()
        {
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=PlayerPrefs.GetFloat("monster-pouch.volume",.65f);audioSource.playOnAwake=false;
            string[] names={"button","buy","reject","hit","death","result"};float[] freqs={580,880,160,290,110,660};
            for(int i=0;i<names.Length;i++)
            {int n=i==5?17640:4410;var data=new float[n];for(int s=0;s<n;s++){float t=(float)s/44100;float env=Mathf.Pow(1f-(float)s/n,2);data[s]=Mathf.Sin(2*Mathf.PI*(freqs[i]+(i==5?t*660:0))*t)*env*.24f;}var clip=AudioClip.Create(names[i],n,1,44100,false);clip.SetData(data,0);sounds[names[i]]=clip;}
        }
        void PlaySound(string id){if(audioSource!=null&&sounds.TryGetValue(id,out var sound))audioSource.PlayOneShot(sound);}
        void OnDestroy()
        {
            DisposeBoardSelectionFeedback();
            DisposeNotices();
            if(online!=null)online.MatchCompletedVerified-=RecordVerifiedTrophyMatch;
            Screen.sleepTimeout=originalSleepTimeout;
            if(ownsArtCatalog&&art!=null)Destroy(art);
            if(Match!=null)Match.Changed-=UpdateProgression;
            CancelPlacementExchange();
            if(Match!=null)
            {
                Match.Changed -= HandlePlacementMatchChanged;
                Match.PlacementExchanged -= AnimatePlacementExchange;
            }
            if(briefDisplay!=null)Destroy(briefDisplay);
            if(briefBodySprite!=null)Destroy(briefBodySprite);
            if(briefLidSprite!=null)Destroy(briefLidSprite);
            foreach(var clip in sounds.Values)if(clip!=null)Destroy(clip);
        }
        IEnumerator Projectile(BattleUnit actor,BattleUnit target,float impactDelay)
        {
            var style=ProjectileFor(actor);
            if(style==null||style.Flight.Length==0)yield break;
            var simulation=Match.Simulation;
            bool charged=simulation!=null&&simulation.IsNextHitLethal(actor);
            float windup=CombatSimulation.GetAttackWindup(actor),t=0;
            int attackVersion=actor.AttackResetVersion;
            while(t<windup&&Match.Phase==MatchPhase.Combat&&Match.Simulation==simulation&&actor!=null&&actor.AttackResetVersion==attackVersion){t+=Time.deltaTime;yield return null;}
            if(Match.Phase!=MatchPhase.Combat||Match.Simulation!=simulation||actor==null||target==null||actor.AttackResetVersion!=attackVersion)yield break;
            Vector3 from=actor.transform.position+Vector3.up*.5f;
            var sprite=NewEffect("projectile-"+style.UnitId);t=0;float travel=Mathf.Max(.1f,impactDelay-windup);
            while(t<travel&&Match.Phase==MatchPhase.Combat&&Match.Simulation==simulation&&target!=null)
            {
                t+=Time.deltaTime;
                Vector3 end=target.transform.position+Vector3.up*.5f;
                Vector3 position=Vector3.Lerp(from,end,Mathf.Clamp01(t/travel));
                if(style.UnitId=="anuik")position.y+=Mathf.Sin(Mathf.Clamp01(t/travel)*Mathf.PI)*.18f;
                DrawEffect(sprite,style,style.Flight[Mathf.FloorToInt(t*16)%style.Flight.Length],position,charged?1.35f:1);
                SizeProjectile(sprite, actor, charged ? "charged" : "flight");
                if(style.OrientToTarget){var direction=end-from;sprite.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);}
                yield return null;
            }
            if(sprite!=null)Destroy(sprite.gameObject);
        }
        ProjectileArt ProjectileFor(BattleUnit actor)
        {
            if(actor==null)return null;
            foreach(var pair in Match.Actors)
                if(pair.Value==actor)return projectileArt?.Get(pair.Key.Definition.Id);
            return null;
        }
        Image NewEffect(string name)
        {
            var rect=Panel(effects,name,new Rect(0,0,1,1),Color.clear,false);
            rect.pivot=new Vector2(.5f,.5f);
            var sprite=rect.gameObject.AddComponent<Image>();sprite.raycastTarget=false;sprite.preserveAspect=true;
            return sprite;
        }
        void DrawEffect(Image image,ProjectileArt style,Sprite frame,Vector3 position,float scale)
        {
            if(image==null||frame==null)return;
            image.sprite=frame;
            Vector2 point=ToCanvas(GameCamera.WorldToScreenPoint(position));
            image.rectTransform.anchoredPosition=new Vector2(Mathf.Round(point.x),-Mathf.Round(point.y));
            image.rectTransform.sizeDelta=ProjectileFlightRendering.CatalogFrame(frame,style,scale).Size;
        }
        IEnumerator ImpactEffect(BattleUnit actor,BattleUnit target,bool lethal)
        {
            var style=ProjectileFor(actor);
            if(style==null||style.Impact.Length==0||target==null)yield break;
            var simulation=Match.Simulation;
            Vector3 position=target.transform.position+Vector3.up*.55f;
            var effect=NewEffect((lethal?"fatal-":"impact-")+style.UnitId);
            float elapsed=0,duration=lethal?.44f:.32f;
            while(elapsed<duration&&Match.Simulation==simulation&&Match.Phase!=MatchPhase.Menu)
            {
                elapsed+=Time.deltaTime;
                int frame=Mathf.Min(style.Impact.Length-1,Mathf.FloorToInt(elapsed/duration*style.Impact.Length));
                DrawEffect(effect,style,style.Impact[frame],position,lethal?1.8f:1);
                yield return null;
            }
            if(effect!=null)Destroy(effect.gameObject);
        }
        IEnumerator UpgradeStar(Vector2 from,OwnedUnit target)
        {
            var star=Label(effects,"★",new Rect(from.x,from.y,30,30),28,gold);float t=0;
            var player=Match.Player;
            while(t<.6f&&Match.Player==player&&Match.Phase==MatchPhase.Preparation)
            {
                t+=Time.deltaTime;
                Vector2 end=UpgradeStarDestination(target,from);
                Vector2 p=Vector2.Lerp(from,end,Mathf.SmoothStep(0,1,t/.6f));
                SetRect(star.rectTransform,new Rect(p.x-15,p.y-15,30,30));yield return null;
            }
            if(star!=null)Destroy(star.gameObject);
        }
        Vector2 UpgradeStarDestination(OwnedUnit target,Vector2 fallback)
        {
            if(target.Location==UnitLocation.Field&&Match.Actors.TryGetValue(target,out var actor)&&actor!=null)
            {
                Vector3 center=visuals.TryGetValue(actor,out var view)&&view.Renderer!=null?
                    view.Renderer.bounds.center:actor.transform.position+Vector3.up;
                return ToCanvas(GameCamera.WorldToScreenPoint(center));
            }
            return UpgradePortraitCenter(StorageExchangeCard(target,target.Location,target.BriefSlot,false),fallback);
        }
        Vector2 UpgradePortraitCenter(RectTransform card,Vector2 fallback)
        {
            if(card==null)return fallback;
            var portrait=card.GetComponentsInChildren<Image>().FirstOrDefault(item=>item.name=="Art");
            var rect=portrait!=null?portrait.rectTransform:card;
            return ToCanvas(RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
        }
        IEnumerator SellMoonkens(Vector2 from,int amount,MoonkenDelivery delivery=null)
        {
            var owner=Match.Player;int round=Match.Round;
            int count=delivery!=null?Mathf.Clamp(amount,1,7):Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1,amount)*.75f),3,7);
            // Match the HUD artwork throughout the flight, without shrinking at the end.
            Vector2 size=currencyIcon!=null?currencyIcon.rect.size:new Vector2(60,60);
            var chips=new RectTransform[count];
            for(int i=0;i<count;i++)
            {
                chips[i]=Panel(effects,"Sold Moonken",new Rect(from-size*.5f,size),Color.clear,false);
                if(MoonIcon!=null)
                {
                    var image=chips[i].gameObject.AddComponent<Image>();image.sprite=MoonIcon;image.preserveAspect=true;image.raycastTarget=false;
                }
                else Label(chips[i],"●",new Rect(Vector2.zero,size),Mathf.RoundToInt(size.y*.9f),gold,FontStyle.Bold);
            }
            float travel=delivery?.TravelDuration??Mathf.Max(.1f,sellMoonkenTravelDuration),stagger=delivery!=null?0:Mathf.Max(0,sellMoonkenStagger),elapsed=0;
            while(elapsed<travel+stagger*(count-1)&&Match.Player==owner&&
                (delivery!=null?!delivery.Canceled&&Match.Phase!=MatchPhase.Menu:Match.Round==round&&Match.Phase==MatchPhase.Preparation))
            {
                elapsed=delivery!=null?Mathf.Max(0,delivery.Elapsed-delivery.ShineDuration):elapsed+(Match.Paused?0:Time.unscaledDeltaTime);
                Vector2 target=CurrencyTarget();
                for(int i=0;i<count;i++)
                {
                    if(chips[i]==null)continue;
                    float t=Mathf.Clamp01((elapsed-i*stagger)/travel);
                    float eased=1-Mathf.Pow(1-t,3);
                    Vector2 spread=new Vector2((i-(count-1)*.5f)*14,Mathf.Sin(i*2.1f)*8);
                    Vector2 p=Vector2.Lerp(from+spread,target,eased);
                    p.y-=Mathf.Sin(t*Mathf.PI)*(34+i%2*8);
                    SetRect(chips[i],new Rect(p-size*.5f,size));
                    if(t>=1)
                    {
                        chips[i].gameObject.SetActive(false);Destroy(chips[i].gameObject);chips[i]=null;
                        PopCurrencyIcon();
                    }
                }
                yield return null;
            }
            foreach(var chip in chips)if(chip!=null)Destroy(chip.gameObject);
        }
        Vector2 CurrencyTarget()
        {
            RectTransform target=currencyIcon!=null?currencyIcon:currencyCounter;
            if(target==null)return new Vector2(411,CardTableArena.BriefTop+86+BankColumnOffset);
            return ToCanvas(RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center)));
        }
        void PopCurrencyIcon()
        {
            currencyPopInitialScale=currencyIcon!=null?currencyIcon.localScale.x:1;
            currencyPopStarted=Time.unscaledTime;currencyPopPlayer=Match.Player;currencyPopRound=Match.Round;
            UpdateCurrencyPop();
        }
        void UpdateCurrencyPop()
        {
            if(currencyText!=null&&Match.Player!=null)
            {
                string balance=Match.Player.Coins.ToString();
                if(currencyText.text!=balance)currencyText.text=balance;
            }
            if(currencyIcon==null)return;
            bool current=currencyPopStarted>=0&&Match.Player==currencyPopPlayer&&Match.Round==currencyPopRound&&Match.Phase!=MatchPhase.Menu;
            float t=current?Mathf.Clamp01((Time.unscaledTime-currencyPopStarted)/Mathf.Max(.05f,currencyPunchDuration)):1;
            float peak=Mathf.Max(1,currencyPunchScale);
            float scale=t<.25f?Mathf.Lerp(currencyPopInitialScale,peak,Mathf.SmoothStep(0,1,t/.25f)):
                Mathf.Lerp(peak,1,Mathf.SmoothStep(0,1,(t-.25f)/.75f));
            // Scale about the icon's own center; the number and landing point remain fixed.
            currencyIcon.localScale=Vector3.one*scale;
        }
        static string Stars(int n)=>new string('★',Mathf.Clamp(n,0,3));
        public RectTransform Panel(Transform parent,string name,Rect rect,Color color,bool raycast=true)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();SetRect(rt,rect);
            if(color.a>0){var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;if(raycast&&color.a>.8f&&rect.height<800)PixelBorder(rt,Color.Lerp(color,new Color(.70f,.49f,.22f),.48f),2);}return rt;
        }
        RectTransform HitArea(Transform parent,string name,Rect rect)
        {
            var rt=Panel(parent,name,rect,Color.clear,false);
            var hit=rt.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;hit.canvasRenderer.cullTransparentMesh=false;return rt;
        }
        void PixelBorder(RectTransform parent,Color color,int thickness)
        {
            float w=parent.rect.width,h=parent.rect.height;
            // Rectangular strips keep the controls on the same hard pixel grid as the artwork.
            foreach(var edge in new[]{new Rect(2,0,w-4,thickness),new Rect(2,h-thickness,w-4,thickness),new Rect(0,2,thickness,h-4),new Rect(w-thickness,2,thickness,h-4)})
                Panel(parent,"Pixel edge",edge,color,false);
            Panel(parent,"Pixel shade",new Rect(3,h-4,w-6,2),new Color(0,0,0,.28f),false);
        }
        public static void SetRect(RectTransform rt,Rect rect)
        {rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=new Vector2(rect.width,rect.height);}
        Text Label(Transform parent,string text,Rect rect,int size,Color color,FontStyle style=FontStyle.Normal,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var rt=Panel(parent,"Text",rect,Color.clear,false);var label=rt.gameObject.AddComponent<Text>();label.font=style==FontStyle.Bold&&size>=16?headingFont:font;label.fontSize=Mathf.Max(10,size);label.color=color;label.fontStyle=FontStyle.Normal;label.text=GameText.Resolve(text);label.alignment=align;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.lineSpacing=1.12f;label.alignByGeometry=false;return label;
        }
        RectTransform Button(Transform parent,string text,Rect rect,Action action,Color background,Color foreground,int size=20)
        {
            var rt=Panel(parent,text,rect,background);var b=rt.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>{PlaySound("button");action();});var c=b.colors;c.highlightedColor=new Color(1,1,1,.9f);c.pressedColor=new Color(.72f,.8f,.8f);b.colors=c;if(background.a>.1f)RetroButtonMotion.Attach(rt,b,Color.Lerp(background,new Color(.35f,.32f,.29f),.2f));Label(rt,text,new Rect(6,3,rect.width-12,rect.height-6),size,foreground,FontStyle.Bold);return rt;
        }
        void Image(Transform parent,Sprite sprite,Rect rect,Color? color=null)
        {
            if(sprite==null)return;
            var rt=Panel(parent,"Art",rect,Color.clear,false);
            // PreserveAspect uses the RectTransform pivot to distribute spare space.
            // Keep the same rectangle, but fit the visible sprite around its center.
            rt.pivot=new Vector2(.5f,.5f);
            rt.anchoredPosition=new Vector2(rect.x+rect.width*.5f,-rect.y-rect.height*.5f);
            var im=rt.gameObject.AddComponent<Image>();im.sprite=sprite;im.preserveAspect=true;im.raycastTarget=false;im.color=color??Color.white;
            if(sprite.name.StartsWith("anuik-",StringComparison.Ordinal))im.material=Resources.Load<Material>("MonsterPouch/AnuikContourUI");
        }
        // Compatibility name for existing roster/offer slots. Character icons are
        // reserved for PlayerAvatarGraphic in profile customization.
        void Portrait(Transform parent,string id,Rect rect,Color? color=null)
            =>Figure(parent,id,rect,false,color:color);
    }

    // A persistent surface below the other UI captures the entire board gesture.
    // EventSystem preserves the press origin and routes mouse and touch identically.
    public sealed class BoardPointerGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        LocalGameUI ui;int? pointer;
        public void Configure(LocalGameUI owner){ui=owner;}
        public void OnPointerDown(PointerEventData e)
        {
            if(ui==null)return;
            // The owner may have cancelled for a modal/phase/disable without a pointer-up.
            if(!ui.BoardPointerActive)pointer=null;
            if(pointer==null && e.button==PointerEventData.InputButton.Left && ui.BeginBoardPointer(e.position))pointer=e.pointerId;
        }
        void Update()
        {if(ui!=null&&!ui.UnitHoldAllowed&&(pointer!=null||ui.BoardPointerActive))OnDisable();}
        public void OnDrag(PointerEventData e){if(pointer==e.pointerId)ui.MoveBoardPointer(e.position);}
        public void OnPointerUp(PointerEventData e){if(pointer==e.pointerId){ui.EndBoardPointer(e.position);pointer=null;}}
        public void OnEndDrag(PointerEventData e){if(pointer==e.pointerId){ui.CancelBoardPointer();pointer=null;}}
        void OnDisable(){if(ui!=null)ui.CancelBoardPointer();pointer=null;}
        void OnApplicationFocus(bool focused){if(!focused)OnDisable();}
        void OnApplicationPause(bool paused){if(paused)OnDisable();}
    }

    public sealed class BriefOfferGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        LocalGameUI ui;int slot;long token;Vector2 position;int? pointer;
        readonly PointerGesture gesture=new PointerGesture();
        readonly BriefDragSnap briefSnap=new BriefDragSnap();
        public string UnitId { get; private set; }
        public void Configure(LocalGameUI owner,int offerSlot,long offerToken,string id)
        {ui=owner;slot=offerSlot;token=offerToken;UnitId=id;}
        public void OnPointerDown(PointerEventData e)
        {
            if(pointer!=null||ui==null||!ui.UnitHoldAllowed||!ui.BriefReady||e.button!=PointerEventData.InputButton.Left)return;
            briefSnap.Begin(ui,e.position);
            pointer=e.pointerId;position=e.position;gesture.Begin(position,Time.unscaledTime);ui.UpdateUnitHoldProgress(gesture,position);
        }
        public void OnDrag(PointerEventData e)
        {if(pointer!=e.pointerId)return;Advance(e.position);if(gesture.Dragging)ui.PreviewOffer(slot,token,briefSnap.Resolve(ui,position));}
        void Update(){if(gesture.Active)Advance(position);}
        void Advance(Vector2 next)
        {
            if(ui==null||!ui.UnitHoldAllowed||!ui.BriefReady){Cancel();return;}
            position=next;var result=gesture.Move(position,Time.unscaledTime);ui.UpdateUnitHoldProgress(gesture,position);
            if(result==GestureResult.Hold)ui.InspectOffer(slot,token);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(pointer!=e.pointerId)return;
            Advance(e.position);var result=gesture.End();var drop=briefSnap.Resolve(ui,e.position);briefSnap.Cancel();
            pointer=null;ui.HideUnitHoldProgress(gesture);ui.EndDragPreview();
            if(result==GestureResult.Tap)ui.SelectOffer(slot,token);
            else if(result==GestureResult.Drag)ui.DropOffer(slot,token,drop);
        }
        public void OnEndDrag(PointerEventData e){if(pointer==e.pointerId)Cancel();}
        void Cancel()
        {
            bool pending=gesture.Active||pointer!=null;gesture.Cancel();briefSnap.Cancel();pointer=null;
            if(ui!=null){ui.HideUnitHoldProgress(gesture);if(pending)ui.EndDragPreview();}
        }
        void OnDisable(){Cancel();}
        void OnApplicationFocus(bool focused){if(!focused)Cancel();}
        void OnApplicationPause(bool paused){if(paused)Cancel();}
    }

    public sealed class UnitCardGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        LocalGameUI ui;OwnedUnit unit;bool enemy;Vector2 position;int? pointer;readonly PointerGesture gesture=new PointerGesture();
        readonly BriefDragSnap briefSnap=new BriefDragSnap();
        public void Configure(LocalGameUI owner,OwnedUnit owned,bool rival){ui=owner;unit=owned;enemy=rival;}
        public void OnPointerDown(PointerEventData e)
        {
            if(pointer!=null||ui==null||!ui.UnitHoldAllowed||e.button!=PointerEventData.InputButton.Left)return;
            briefSnap.Begin(ui,e.position,!enemy&&unit.Location==UnitLocation.Brief);
            pointer=e.pointerId;position=e.position;gesture.Begin(position,Time.unscaledTime);ui.UpdateUnitHoldProgress(gesture,position);
        }
        public void OnDrag(PointerEventData e)
        {if(pointer!=e.pointerId)return;Advance(e.position);if(gesture.Dragging&&!enemy)ui.DragPreview(unit,briefSnap.Resolve(ui,e.position));}
        void Update(){if(gesture.Active)Advance(position);}
        void Advance(Vector2 next)
        {
            if(ui==null||!ui.UnitHoldAllowed){Cancel();return;}
            position=next;var result=gesture.Move(position,Time.unscaledTime);ui.UpdateUnitHoldProgress(gesture,position);
            if(result==GestureResult.Hold)ui.ShowInspection(unit,enemy);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(pointer!=e.pointerId)return;
            Advance(e.position);var result=gesture.End();bool returned=briefSnap.ReturnedToBrief(ui,e.position);
            var drop=briefSnap.Resolve(ui,e.position);briefSnap.Cancel();pointer=null;ui.HideUnitHoldProgress(gesture);ui.EndDragPreview();
            if(result==GestureResult.Tap)ui.Select(unit,enemy);
            else if(result==GestureResult.Drag&&!enemy&&!returned)ui.DropWithGuidedTutorialGesture(unit,drop,result);
        }
        public void OnEndDrag(PointerEventData e){if(pointer==e.pointerId)Cancel();}
        void Cancel()
        {
            bool pending=gesture.Active||pointer!=null;gesture.Cancel();briefSnap.Cancel();pointer=null;
            if(ui!=null){ui.HideUnitHoldProgress(gesture);if(pending)ui.EndDragPreview();}
        }
        void OnDisable(){Cancel();}
        void OnApplicationFocus(bool focused){if(!focused)Cancel();}
        void OnApplicationPause(bool paused){if(paused)Cancel();}
    }
}

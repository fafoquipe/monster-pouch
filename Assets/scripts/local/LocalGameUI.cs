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
        public MatchController Match { get; private set; }
        public string SelectedId { get; private set; }
        public Camera GameCamera { get; private set; }
        public RectTransform CanvasRoot { get; private set; }
        UnitArtCatalog art;
        ProjectileArtCatalog projectileArt;
        Font font;
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
        public bool BriefReady => Match.Phase==MatchPhase.Preparation && !Match.Paused && !rerollBusy && briefOpen>=.999f;
        public int SelectedOfferSlot => selectedOfferSlot;
        RectTransform briefDrop, benchDrop, dragGhost;
        RectTransform page, modal, effects;
        CanvasScaler canvasScaler;
        RectTransform fullCanvas;
        Rect lastSafeArea;
        Vector2Int lastScreen;
        Text clockText, noticeText;
        string chosen = "bugaloo";
        readonly HashSet<string> chosenWhelps = new HashSet<string>(StringComparer.Ordinal);
        public IReadOnlyCollection<string> SelectedWhelps => chosenWhelps;
        string menuNotice;
        OwnedUnit inspected;
        bool inspectedEnemy;
        Text inspectStats;
        readonly Dictionary<BattleUnit, UnitPresentation> visuals = new Dictionary<BattleUnit, UnitPresentation>();
        readonly Dictionary<BattleUnit, RectTransform> bars = new Dictionary<BattleUnit, RectTransform>();
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
            symbolFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = Resources.Load<Font>("MonsterPouch/PixelFont") ?? symbolFont;
            if(font.material.mainTexture!=null)font.material.mainTexture.filterMode=FilterMode.Point;
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
            Match.Configure(config,board,mapper);
            foreach (var prototype in FindObjectsByType<PrototypeUnitSpawner>(FindObjectsSortMode.None)) prototype.enabled = false;
            chosen = PlayerPrefs.GetString("monster-pouch.monster","bugaloo");
            if(config.Get(chosen)==null || !config.Get(chosen).IsMonster)chosen="bugaloo";
            string savedTeam=PlayerPrefs.GetString("monster-pouch.whelps","");
            if(!config.TryResolveWhelpSelection(string.IsNullOrEmpty(savedTeam)?null:savedTeam.Split(','),out var teamIds,out _))
                config.TryResolveWhelpSelection(null,out teamIds,out _);
            foreach(string id in teamIds)chosenWhelps.Add(id);
            BuildCanvas(); BuildSounds();
            Match.Changed += () => rebuild = true;
            Match.ActorCreated += CreateVisual;
            Match.Moved += (actor,from,to) => { if (visuals.TryGetValue(actor,out var v)) v.Move(Match.Mapper.GetWorldPosition(from), Match.Mapper.GetWorldPosition(to), actor.BaseStats.MoveInterval); };
            Match.Attacked += (actor,target,projectile) =>
            {
                float delay=CombatSimulation.GetImpactDelay(actor,target);
                if (visuals.TryGetValue(actor,out var v)) v.Attack(target.transform.position,projectile,delay);
                if (projectile && !DocumentedBattleEffects.HandlesProjectile(actor)) StartCoroutine(Projectile(actor,target,delay));
            };
            Match.Died += actor => { if (visuals.TryGetValue(actor,out var v)) v.Die(); };
            Match.Reviving += (actor,duration) => { if(visuals.TryGetValue(actor,out var v))v.BeginRevive(duration); };
            Match.Revived += actor => { if(visuals.TryGetValue(actor,out var v))v.Revive(); StartCoroutine(ImpactEffect(actor,actor,false)); };
            Match.Impacted += (actor,target,damage) => { if(damage>0)StartCoroutine(ImpactEffect(actor,target,false)); };
            Match.LethalImpacted += (actor,target) => StartCoroutine(ImpactEffect(actor,target,true));
            Match.Sound += PlaySound;
            gameObject.AddComponent<DocumentedBattleEffects>().Configure(Match,CanvasRoot,GameCamera);
            RenderPage();
        }
        void BuildCanvas()
        {
            GameObject go = new GameObject("Monster Pouch UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false); var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            canvasScaler = go.GetComponent<CanvasScaler>(); canvasScaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            canvas.pixelPerfect=true;
            fullCanvas=go.GetComponent<RectTransform>();
            CanvasRoot=Panel(fullCanvas,"Safe portrait",new Rect(0,0,540,960),Color.clear,false);
            CanvasRoot.anchorMin=CanvasRoot.anchorMax=CanvasRoot.pivot=new Vector2(.5f,.5f);
            UpdateLayout();
            var boardInput=HitArea(CanvasRoot,"Board input",new Rect(0,0,540,960));
            boardInput.gameObject.AddComponent<BoardPointerGesture>().Configure(this);
            page=Panel(CanvasRoot,"Page",new Rect(0,0,540,960),Color.clear,false);
            effects=Panel(CanvasRoot,"Effects",new Rect(0,0,540,960),Color.clear,false);
            var events=FindFirstObjectByType<EventSystem>();
            if(events==null) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            else if(events.GetComponent<InputSystemUIInputModule>()==null) { var old=events.GetComponent<BaseInputModule>(); if(old!=null) Destroy(old); events.gameObject.AddComponent<InputSystemUIInputModule>(); }
        }
        public void RenderPage()
        {
            rebuild=false;
            EndDragPreview();
            foreach(Transform child in page){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            clockText=noticeText=null;
            foreach(var bar in bars.Values) if(bar!=null) Destroy(bar.gameObject); bars.Clear();
            foreach(var pair in visuals.ToList()) if(pair.Key==null) visuals.Remove(pair.Key);
            if(Match.Phase==MatchPhase.RoundResult||Match.Phase==MatchPhase.MatchResult)
                foreach(var v in visuals.Values)if(v!=null)v.Freeze();
            if(Match.Phase==MatchPhase.Menu) RenderMenu(); else RenderGame();
            RefreshHighlights();
        }
        void RenderMenu()
        {
            if(ModernMenu){RenderModernMenu();return;}
            selectedOfferSlot=-1;lastBriefRound=0;shownPouch=null;briefOpen=briefTarget=0;
            rerollSequence++;rerollBusy=rerollClosing=false;
            Panel(page,"Menu shade",new Rect(0,0,540,960),new Color(.025f,.055f,.085f,.88f));
            Label(page,"GRAND CAS HOTEL",new Rect(50,35,440,23),13,cyan);
            Label(page,"MONSTER POUCH",new Rect(25,65,490,63),39,gold,FontStyle.Bold);
            Label(page,"ELIGE TU MONSTER",new Rect(50,140,440,25),15,cyan);
            var monsters=Match.Config.Units.Where(d=>d!=null&&d.IsMonster).ToArray();
            float monsterWidth=(480-12*Mathf.Max(0,monsters.Length-1))/Mathf.Max(1,monsters.Length);
            for(int i=0;i<monsters.Length;i++)
            {
                string id=monsters[i].Id;float x=30+i*(monsterWidth+12);
                var card=Panel(page,id,new Rect(x,183,monsterWidth,200),chosen==id?new Color(.19f,.36f,.37f):ink);
                if(chosen==id)PixelBorder(card,gold,2);
                Portrait(card,id,new Rect(7,12,monsterWidth-14,132));
                Label(card,Match.Config.Get(id).DisplayName,new Rect(3,148,monsterWidth-6,28),17,Color.white,FontStyle.Bold);
                var button=card.gameObject.AddComponent<Button>(); button.onClick.AddListener(()=>{chosen=id;PlayerPrefs.SetString("monster-pouch.monster",id);PlaySound("button");RenderPage();});
                Label(card,chosen==id?"ELEGIDO":"ELEGIR",new Rect(3,176,monsterWidth-6,18),11,chosen==id?gold:cyan);
            }
            Label(page,Match.Config.Get(chosen)?.BaseAbility?.Name??"",new Rect(35,390,470,26),15,gold);
            Label(page,"TUS WHELPS  ·  "+chosenWhelps.Count+" ELEGIDOS",new Rect(35,430,470,28),18,cyan);
            Label(page,"Toca para añadir o retirar del equipo",new Rect(35,463,470,23),13,Color.white);
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
                Label(card,chosenWhelps.Contains(id)?"EN EQUIPO":"AÑADIR",new Rect(3,106,102,15),10,chosenWhelps.Contains(id)?gold:cyan);
                card.gameObject.AddComponent<Button>().onClick.AddListener(()=>ToggleWhelp(id));
            }
            Label(page,menuNotice??"Tus Whelps elegidos aparecerán en el Brief.",new Rect(35,777,470,30),13,menuNotice==null?Color.white:gold);
            Button(page,"JUGAR CONTRA BOT",new Rect(60,820,420,56),()=>{SelectedId=null;CloseModal();SaveTeam();Match.StartMatch(chosen,chosenWhelps);},gold,ink);
            Button(page,"Cómo jugar",new Rect(60,895,198,40),ShowHelp,new Color(.15f,.26f,.30f),Color.white,15);
            Button(page,"Sonido",new Rect(282,895,198,40),ShowAudio,new Color(.15f,.26f,.30f),Color.white,15);
        }
        public void ToggleWhelp(string id)
        {
            var next=new HashSet<string>(chosenWhelps,StringComparer.Ordinal);
            if(!next.Remove(id))next.Add(id);
            if(!Match.Config.TryResolveWhelpSelection(next,out var ids,out var reason))menuNotice=reason;
            else {chosenWhelps.Clear();foreach(string selected in ids)chosenWhelps.Add(selected);menuNotice=null;SaveTeam();PlaySound("button");}
            RenderPage();
        }
        void SaveTeam(){PlayerPrefs.SetString("monster-pouch.whelps",string.Join(",",chosenWhelps.OrderBy(id=>id,StringComparer.Ordinal)));}
        void RenderGame()
        {
            var top=Panel(page,"Round header",new Rect(18,26,284,68),ink);
            Button(page,"Ⅱ",new Rect(320,28,48,48),()=>{Match.Pause(true);ShowPause();},ink,Color.white);
            string phase=Match.Phase==MatchPhase.Preparation?"PREPARACIÓN":Match.Phase==MatchPhase.Combat?"COMBATE":"RESULTADO";
            Label(top,phase,new Rect(12,6,260,28),21,Color.white,FontStyle.Bold);
            var timer=Panel(page,"Clock",new Rect(405,26,117,68),ink);
            clockText=Label(timer,"",new Rect(5,7,107,52),31,cyan,FontStyle.Bold);
            Label(top,$"TÚ {Match.PlayerWins}  —  {Match.BotWins} RIVAL  / 3",new Rect(12,39,260,22),16,Color.white);
            noticeText=Label(page,Match.Notice??"",new Rect(50,102,440,31),13,Color.white);
            noticeText.gameObject.AddComponent<Outline>().effectColor=ink;
            RenderBrief();
            foreach(var pair in Match.Actors) CreateBar(pair.Value,pair.Key);
            if(Match.Phase==MatchPhase.RoundResult || Match.Phase==MatchPhase.MatchResult) RenderResult();
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
            var tray=Panel(page,"Brief tray",new Rect(0,704,540,256),Color.clear,false);
            var body=Panel(tray,"Brief body",new Rect(12,62,350,157),Color.clear,false);
            briefBodyImage=body.gameObject.AddComponent<Image>();briefBodyImage.sprite=briefBodySprite;briefBodyImage.raycastTarget=false;
            briefDrop=HitArea(tray,"Brief drop",new Rect(12,0,350,219));
            briefContents=Panel(tray,"Brief contents",new Rect(0,0,540,219),Color.clear,false);
            briefContentsGroup=briefContents.gameObject.AddComponent<CanvasGroup>();
            for(int slot=0;slot<3;slot++)
            {
                var stored=Match.Player.Owned.Values.FirstOrDefault(o=>o.Location==UnitLocation.Brief && o.BriefSlot==slot);
                var offer=Match.Player.Offers[slot];
                string id=stored?.Definition.Id??offer?.UnitId;
                var card=HitArea(briefContents,"brief-slot-"+slot,new Rect(46+slot*97,74,92,90));
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
                    var priceTag=Panel(briefContents,"Offer price",new Rect(68+slot*97,167,48,18),ink,false);
                    Image(priceTag,MoonIcon,new Rect(4,3,12,12));
                    Label(priceTag,price<0?"—":price.ToString(),new Rect(18,0,26,18),14,gold,FontStyle.Bold);
                    if(Match.Player.Owned.TryGetValue(id,out var own) && own.TrickCount>0)
                        Label(card,Stars(own.TrickCount),new Rect(8,-12,76,15),12,gold);
                    card.gameObject.AddComponent<BriefOfferGesture>().Configure(this,slot,offer.Token,id);
                }
            }
            briefLid=Panel(tray,"Brief lid",new Rect(12,0,350,62),Color.clear,false);
            briefLid.pivot=new Vector2(.5f,0);briefLid.anchoredPosition=new Vector2(187,-62);
            briefLidImage=briefLid.gameObject.AddComponent<Image>();briefLidImage.sprite=briefLidSprite;briefLidImage.raycastTarget=false;
            var closed=Panel(tray,"Brief closed",new Rect(12,62,350,157),Color.clear,false);
            closed.pivot=new Vector2(.5f,.5f);closed.anchoredPosition=new Vector2(187,-140.5f);
            briefClosedImage=closed.gameObject.AddComponent<Image>();briefClosedImage.sprite=ClosedBriefArt;briefClosedImage.preserveAspect=true;briefClosedImage.raycastTarget=false;
            // A separate one-slot case is the bank. It never shares a sell target with the Brief.
            Image(tray,BenchArt,new Rect(374,82,150,112));
            benchDrop=HitArea(tray,"Bench drop",new Rect(374,82,150,112));
            var benched=Match.Player.Owned.Values.FirstOrDefault(o=>o.Location==UnitLocation.Bench);
            if(benched!=null)
            {
                Portrait(benchDrop,benched.Definition.Id,new Rect(48,39,54,46));
                Label(benchDrop,Stars(benched.TrickCount),new Rect(37,30,76,12),10,gold);
                AddUnitGesture(benchDrop,benched,false);
            }
            else benchDrop.gameObject.AddComponent<Button>().onClick.AddListener(()=>{
                if(selectedOfferSlot>=0)DropOffer(selectedOfferSlot,selectedOfferToken,RectTransformUtility.WorldToScreenPoint(null,benchDrop.TransformPoint(benchDrop.rect.center)));
                else if(SelectedId!=null)Match.Store(SelectedId,true);
            });
            if(!prep)
            {
                bool fighting=Match.Phase==MatchPhase.Combat;
                Label(tray,fighting?"COMBATE":"RESULTADO",new Rect(375,207,150,23),14,gold,FontStyle.Bold);
                ApplyBriefPose();return;
            }
            int offerVersion=Match.Player.OfferVersion;
            var resources=Panel(tray,"Battle resources",new Rect(375,-39,150,107),ink);
            Image(resources,MoonIcon,new Rect(18,7,39,39));
            Label(resources,Match.Player.Coins.ToString(),new Rect(69,5,64,42),29,gold,FontStyle.Bold);
            var reroll=Button(tray,"      "+Match.Player.Rerolls,new Rect(380,15,140,48),()=>RequestReroll(offerVersion),new Color(.05f,.10f,.12f),Color.white,26);
            reroll.name="Reroll";
            if(RerollIcon!=null) Image(reroll,RerollIcon,new Rect(14,4,39,39));
            Button(tray,"LISTO",new Rect(375,204,150,50),()=>{CloseModal();Match.Ready();},gold,ink,22);
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
            briefLid.sizeDelta=new Vector2(350,Mathf.Lerp(157,62,open));
            briefLid.localRotation=Quaternion.Euler((1-open)*180,0,0);
            // Blend to a separately drawn closed case as the hinge finishes closing.
            // The open artwork is never used as the exterior of the shut suitcase.
            float closed=ClosedBriefArt==null?0:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.4f,open));
            briefClosedImage.color=new Color(1,1,1,closed);
            briefBodyImage.color=briefLidImage.color=new Color(1,1,1,1-closed);
            float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,open));
            briefContentsGroup.alpha=reveal;
            briefContentsGroup.blocksRaycasts=BriefReady;
            briefContents.anchoredPosition=new Vector2(0,-Mathf.Round((1-reveal)*14));
        }
        public void RequestReroll(int version)
        {
            if(!BriefReady)return;
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
                if(Match.Buy(slot,token))StartCoroutine(UpgradeStar(new Vector2(92+slot*97,793),owned));
                selectedOfferSlot=-1;SelectedId=offer.UnitId;return;
            }
            selectedOfferSlot=slot;selectedOfferToken=token;SelectedId=offer.UnitId;
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
                if(existing!=null)StartCoroutine(UpgradeStar(ToCanvas(screen),existing));
            }
        }
        void RenderResult()
        {
            var box=Panel(page,"Result",new Rect(38,264,464,332),ink);
            bool final=Match.Phase==MatchPhase.MatchResult;
            Label(box,final?(Match.PlayerWins>=3?"¡VICTORIA!":"FIN DE PARTIDA"):"RONDA COMPLETADA",new Rect(22,28,420,50),final?36:26,gold,FontStyle.Bold);
            Label(box,Match.Result,new Rect(22,99,420,76),21,Color.white);
            Label(box,$"{Match.PlayerWins}  —  {Match.BotWins}",new Rect(22,179,420,47),35,cyan,FontStyle.Bold);
            Button(box,final?"REVANCHA":"SIGUIENTE RONDA",new Rect(26,254,268,48),()=>{SelectedId=null;CloseModal();if(final)Match.Rematch();else Match.NextRound();},gold,ink,17);
            Button(box,"MENÚ",new Rect(308,254,130,48),()=>{SelectedId=null;CloseModal();Match.Menu();},new Color(.2f,.29f,.33f),Color.white,16);
        }
        void CreateVisual(BattleUnit actor, OwnedUnit owned)
        {
            var v=actor.gameObject.AddComponent<UnitPresentation>();
            v.Configure(actor,Match.Mapper,art?.Get(owned.Definition.Id)); visuals[actor]=v;
            if(Match.Phase==MatchPhase.Combat)CreateBar(actor,owned);
        }
        void CreateBar(BattleUnit actor,OwnedUnit owned)
        {
            var bar=Panel(page,"health-"+actor.UnitId,new Rect(0,0,58,24),Color.clear,false);
            Panel(bar,"outline",new Rect(1,1,56,8),new Color(.03f,.07f,.09f),false);
            Panel(bar,"health",new Rect(3,3,52,4),actor.Side==BoardSide.Blue?cyan:new Color(1,.44f,.40f),false);
            Label(bar,owned.Definition.IsMonster?(owned.HasMonsterUpgrade?"◆":""):Stars(owned.TrickCount),new Rect(0,10,58,15),11,gold);
            bars[actor]=bar;
        }
        void Update()
        {
            if(lastScreen.x!=Screen.width||lastScreen.y!=Screen.height||lastSafeArea!=Screen.safeArea)UpdateLayout();
            if(rebuild)RenderPage();
            if(clockText!=null)clockText.text=Match.Phase==MatchPhase.Preparation||Match.Phase==MatchPhase.Combat?Mathf.CeilToInt(Match.Remaining)+"s":"";
            if(noticeText!=null)noticeText.text=Match.Notice??"";
            if(inspectStats!=null && inspected!=null)inspectStats.text=StatsText(inspected);
            UpdateBriefMotion();
            UpdateBoardGesture();
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if(modal!=null){if(Match.Paused)Match.Pause(false);CloseModal();}else if(Match.Phase!=MatchPhase.Menu){Match.Pause(true);ShowPause();} }
        }
        void UpdateLayout()
        {
            lastScreen=new Vector2Int(Screen.width,Screen.height);lastSafeArea=Screen.safeArea;
            Rect safe=lastSafeArea.width>0?lastSafeArea:new Rect(0,0,Screen.width,Screen.height);
            float scale=Mathf.Min(safe.width/540f,safe.height/960f);
            canvasScaler.scaleFactor=Mathf.Max(.1f,scale);
            CanvasRoot.anchoredPosition=(safe.center-new Vector2(Screen.width*.5f,Screen.height*.5f))/canvasScaler.scaleFactor;
            CanvasRoot.sizeDelta=new Vector2(540,960);
            float w=540*scale,h=960*scale;
            GameCamera.rect=new Rect((safe.center.x-w*.5f)/Screen.width,(safe.center.y-h*.5f)/Screen.height,w/Screen.width,h/Screen.height);
        }
        void LateUpdate()
        {
            foreach(var pair in bars)
            {
                var actor=pair.Key;var bar=pair.Value;if(actor==null||bar==null)continue;
                bool alive=actor.IsAlive;bar.gameObject.SetActive(alive);if(!alive)continue;
                Vector2 p=ToCanvas(GameCamera.WorldToScreenPoint(actor.transform.position+Vector3.up*1.38f));
                SetRect(bar,new Rect(p.x-29,p.y-12,58,24));
                var hp=bar.Find("health") as RectTransform;
                if(hp!=null) hp.sizeDelta=new Vector2(52*Mathf.Clamp01((float)actor.CurrentHealth/Mathf.Max(1,actor.BaseStats.MaxHealth)),4);
            }
        }
        public void Select(OwnedUnit owned,bool enemy=false)
        {
            if(Match.Actors.TryGetValue(owned,out var selectedActor)&&selectedActor.IsCombatSummon){ShowInspection(owned,enemy);return;}
            if(enemy){ShowInspection(owned,true);return;}
            selectedOfferSlot=-1;
            SelectedId=owned.Definition.Id;PlaySound("button");rebuild=true;
        }
        void AddUnitGesture(RectTransform card,OwnedUnit owned,bool enemy)
        {
            var g=card.gameObject.AddComponent<UnitCardGesture>();g.Configure(this,owned,enemy);
        }
        public void Drop(OwnedUnit owned,Vector2 screen)
        {
            EndDragPreview();
            if(Match.Phase!=MatchPhase.Preparation||Match.Paused||owned==null)return;
            if(!Match.Player.Owned.TryGetValue(owned.Definition.Id,out var current)||!ReferenceEquals(current,owned))return;
            SelectedId=owned.Definition.Id;
            if(Contains(benchDrop,screen)){Match.Store(SelectedId,true);return;}
            if(Contains(briefDrop,screen))
            {
                if(!BriefReady){Match.Reject("Espera a que se abra el Brief.");return;}
                if(owned.Location==UnitLocation.Field){if(Match.Sell(SelectedId))SelectedId=null;}
                else Match.Store(SelectedId,false);
                return;
            }
            if(Match.Mapper.TryGetCell(GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-GameCamera.transform.position.z)),out var cell)) Match.Place(SelectedId,cell);
            else Match.Reject("Elige una casilla iluminada, el bench o el Brief.");
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
            var p=ToCanvas(screen);SetRect(dragGhost,new Rect(p.x-37,p.y-70,74,88));
            if(noticeText!=null && owned.Location==UnitLocation.Field && Contains(briefDrop,screen))
                noticeText.text="VENDER · +"+owned.Paid+" MT";
        }
        public void EndDragPreview(){if(dragGhost!=null){dragGhost.gameObject.SetActive(false);Destroy(dragGhost.gameObject);}dragGhost=null;}
        public bool BeginBoardPointer(Vector2 pos)
        {
            if(Match.Phase==MatchPhase.Menu||Match.Paused||modal!=null)return false;
            pressedUnit=HitUnit(pos,out pressedEnemy);
            boardGesture.Begin(pos,Time.unscaledTime);lastPointer=pos;
            return true;
        }
        public void MoveBoardPointer(Vector2 pos)
        {
            lastPointer=pos;
            UpdateBoardGesture();
        }
        void UpdateBoardGesture()
        {
            if(Match.Phase==MatchPhase.Menu||Match.Paused||modal!=null){boardGesture.Cancel();EndDragPreview();return;}
            if(!boardGesture.Active)return;
            var gesture=boardGesture.Move(lastPointer,Time.unscaledTime);
            if(gesture==GestureResult.Hold && pressedUnit!=null)ShowInspection(pressedUnit,pressedEnemy);
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
                    else if(SelectedId!=null && Match.Player.Owned.TryGetValue(SelectedId,out var owned))Drop(owned,pos);
                }
                else if(result==GestureResult.Drag && pressedUnit!=null&&!pressedEnemy)Drop(pressedUnit,pos);
            }
            EndDragPreview();
        }
        public void CancelBoardPointer(){boardGesture.Cancel();EndDragPreview();}
        OwnedUnit HitUnit(Vector2 screen,out bool enemy)
        {
            enemy=false;OwnedUnit best=null;float min=45*Screen.height/960f;
            foreach(var pair in Match.Actors)
            {
                if(pair.Value==null||!pair.Value.IsAlive)continue;
                Vector2 point=GameCamera.WorldToScreenPoint(pair.Value.transform.position+Vector3.up*.45f);
                float distance=Vector2.Distance(screen,point);if(distance<min){min=distance;best=pair.Key;enemy=pair.Value.Side!=BoardSide.Blue;}
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
            foreach(var h in highlights)if(h!=null)Destroy(h);highlights.Clear();
            if(Match.Phase!=MatchPhase.Preparation||SelectedId==null)return;
            Match.Player.Owned.TryGetValue(SelectedId,out var owned);
            if(owned==null && !CurrentOffer(selectedOfferSlot,selectedOfferToken,out _))return;
            if(owned!=null && owned.IsPositionLocked(Match.Round))return;
            foreach(var cell in Match.Board.GetAllCells())
            {
                if(!Match.Config.IsDeploymentLegal(BoardSide.Blue,new Vector2Int(cell.X,cell.Y))||cell.OccupiedBy!=null)continue;
                var go=new GameObject("Legal cell");go.transform.SetParent(transform);go.transform.position=Match.Mapper.GetWorldPosition(cell)+Vector3.back*.05f;
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");line.startColor=line.endColor=new Color(.55f,1,.76f,.65f);line.startWidth=line.endWidth=.035f;line.positionCount=5;line.useWorldSpace=false;line.sortingOrder=30;
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
            var box=Panel(modal,"Unit sheet",new Rect(28,158,484,648),ink);
            Portrait(box,owned.Definition.Id,new Rect(15,15,90,94));
            Label(box,owned.Definition.DisplayName+(enemy?" · RIVAL":""),new Rect(113,26,344,40),27,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
            inspectStats=Label(box,StatsText(owned),new Rect(24,113,436,58),17,Color.white,FontStyle.Normal,TextAnchor.MiddleLeft);
            Label(box,"HABILIDAD · "+owned.Definition.BaseAbility.Name,new Rect(24,182,436,30),16,cyan,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(box,owned.Definition.BaseAbility.Description,new Rect(24,211,436,61),16,Color.white,FontStyle.Normal,TextAnchor.UpperLeft);
            int row=0;
            if(owned.Definition.IsMonster)
            {
                var trick=owned.Definition.MonsterUpgrade;
                if(!enemy||owned.HasMonsterUpgrade)Label(box,(owned.HasMonsterUpgrade?"◆ ":"")+trick.Name,new Rect(24,292,436,30),20,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
                if(!enemy||owned.HasMonsterUpgrade)Label(box,trick.Description,new Rect(24,333,436,103),17,Color.white,FontStyle.Normal,TextAnchor.UpperLeft);
                if(!enemy)Label(box,owned.HasMonsterUpgrade?"Mejora adquirida":"Coste: "+trick.Cost+" Moon Tokens",new Rect(24,456,436,35),17,cyan);
                if(!enemy && !owned.HasMonsterUpgrade && Match.Phase==MatchPhase.Preparation)
                    Button(box,"MEJORAR · "+trick.Cost+" MT",new Rect(94,510,296,45),()=>{Match.UpgradeMonster();ShowInspection(owned,false);},gold,ink,17);
            }
            else if(summoned)
                Label(box,"INVOCADO POR KAYON\n\nEste Dummy lucha durante esta ronda y desaparece al terminar.",new Rect(24,292,436,128),17,cyan,FontStyle.Normal,TextAnchor.UpperLeft);
            else for(int i=0;i<3;i++)
            {
                if(enemy&&!owned.Tricks[i])continue;
                int index=i;var trick=owned.Definition.Tricks[i];float y=284+row*90;
                Label(box,(owned.Tricks[i]?"★ ":"☆ ")+trick.Name,new Rect(24,y,318,25),17,owned.Tricks[i]?gold:Color.white,FontStyle.Bold,TextAnchor.MiddleLeft);
                Label(box,trick.Description,new Rect(24,y+28,418,55),14,new Color(.78f,.85f,.85f),FontStyle.Normal,TextAnchor.UpperLeft);
                if(!enemy&&owned.Copies>0&&!owned.Tricks[i]&&Match.Phase==MatchPhase.Preparation&&Match.Player.Offers.Any(o=>o!=null&&o.UnitId==owned.Definition.Id))
                    Button(box,owned.NextTrick==i?"ELEGIDO":trick.Cost+" MT",new Rect(352,y,107,27),()=>{Match.ChooseTrick(owned.Definition.Id,index);ShowInspection(owned,false);},new Color(.22f,.36f,.38f),gold,12);
                row++;
            }
            Button(box,"CERRAR",new Rect(24,582,436,43),CloseModal,new Color(.19f,.31f,.35f),Color.white,17);
        }
        string StatsText(OwnedUnit owned)
        {
            Match.Actors.TryGetValue(owned,out var actor);
            var stats=actor!=null && Match.Phase!=MatchPhase.Preparation?actor.BaseStats:UnitStats.FromDefinition(owned.Definition,owned,true);
            int health=actor!=null && Match.Phase!=MatchPhase.Preparation?actor.CurrentHealth:stats.MaxHealth;
            return $"VIDA  {health} / {stats.MaxHealth}     DAÑO  {stats.Attack}\nATAQUES  {1f/stats.AttackInterval:0.0}/s     ALCANCE  {stats.AttackRange}";
        }
        void ShowPause()
        {
            CloseModal();modal=Panel(CanvasRoot,"Pause",new Rect(0,0,540,960),new Color(.02f,.055f,.08f,.95f));
            Label(modal,"PAUSA",new Rect(40,230,460,64),46,gold,FontStyle.Bold);
            Button(modal,"CONTINUAR",new Rect(70,354,400,55),()=>{CloseModal();Match.Pause(false);},gold,ink);
            Button(modal,"Cómo jugar",new Rect(70,432,400,50),ShowHelp,new Color(.17f,.29f,.33f),Color.white);
            Button(modal,"Sonido",new Rect(70,505,400,50),ShowAudio,new Color(.17f,.29f,.33f),Color.white);
            Button(modal,"VOLVER AL MENÚ",new Rect(70,608,400,50),()=>{CloseModal();SelectedId=null;Match.Menu();},new Color(.25f,.16f,.20f),Color.white);
        }
        void ShowHelp()
        {
            CloseModal();modal=Panel(CanvasRoot,"Help",new Rect(0,0,540,960),new Color(.025f,.065f,.09f,.97f));
            Label(modal,"TU PRIMERA PARTIDA",new Rect(34,140,472,55),30,gold,FontStyle.Bold);
            Label(modal,"El Brief se abre con tres Whelps disponibles. Arrastra uno al campo para comprarlo y colocarlo. También puedes tocarlo y luego tocar su casilla.\n\nUna copia de un Whelp propio compra su siguiente Trick. Mantén una figurita para inspeccionarla.\n\nGuarda uno en el mini Brief del costado. Para venderlo, arrástralo del campo al Brief grande: recuperas lo pagado.\n\nEl reroll cierra la caja y revela nuevas copias. Pulsa LISTO o espera 40 segundos para combatir. Si cae el Monster, los Whelps siguen luchando hasta que un equipo quede sin unidades en pie. Gana tres rondas.",new Rect(46,230,448,510),19,Color.white,FontStyle.Normal,TextAnchor.UpperLeft);
            Button(modal,"ENTENDIDO",new Rect(65,802,410,54),()=>{CloseModal();if(Match.Paused)ShowPause();},gold,ink);
        }
        void ShowAudio()
        {
            CloseModal();modal=Panel(CanvasRoot,"Audio",new Rect(0,0,540,960),new Color(.025f,.065f,.09f,.97f));
            Label(modal,"SONIDO",new Rect(40,260,460,60),40,gold,FontStyle.Bold);
            Label(modal,"Volumen: "+Mathf.RoundToInt(audioSource.volume*100)+"%",new Rect(40,375,460,40),24,Color.white);
            Button(modal,"−",new Rect(75,449,110,65),()=>SetVolume(audioSource.volume-.1f),new Color(.18f,.3f,.34f),Color.white,34);
            Button(modal,"SILENCIO",new Rect(202,449,136,65),()=>SetVolume(audioSource.volume>0?0:.7f),new Color(.18f,.3f,.34f),Color.white,17);
            Button(modal,"+",new Rect(355,449,110,65),()=>SetVolume(audioSource.volume+.1f),new Color(.18f,.3f,.34f),Color.white,34);
            Button(modal,"VOLVER",new Rect(65,635,410,54),()=>{CloseModal();if(Match.Paused)ShowPause();},gold,ink);
        }
        void SetVolume(float volume){audioSource.volume=Mathf.Clamp01(volume);PlayerPrefs.SetFloat("monster-pouch.volume",audioSource.volume);ShowAudio();}
        public void CloseModal(){if(modal!=null){modal.gameObject.SetActive(false);Destroy(modal.gameObject);}modal=null;inspected=null;inspectStats=null;boardGesture.Cancel();EndDragPreview();}
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
            while(t<windup&&Match.Phase==MatchPhase.Combat&&Match.Simulation==simulation){t+=Time.deltaTime;yield return null;}
            if(Match.Phase!=MatchPhase.Combat||Match.Simulation!=simulation||actor==null||target==null)yield break;
            Vector3 from=actor.transform.position+Vector3.up*.5f;
            var sprite=NewEffect("projectile-"+style.UnitId);t=0;float travel=Mathf.Max(.1f,impactDelay-windup);
            while(t<travel&&Match.Phase==MatchPhase.Combat&&Match.Simulation==simulation&&target!=null)
            {
                t+=Time.deltaTime;
                Vector3 end=target.transform.position+Vector3.up*.5f;
                Vector3 position=Vector3.Lerp(from,end,Mathf.Clamp01(t/travel));
                if(style.UnitId=="anuik")position.y+=Mathf.Sin(Mathf.Clamp01(t/travel)*Mathf.PI)*.18f;
                DrawEffect(sprite,style,style.Flight[Mathf.FloorToInt(t*16)%style.Flight.Length],position,charged?1.35f:1);
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
            image.rectTransform.sizeDelta=new Vector2(frame.rect.width,frame.rect.height)*(style.DisplaySize*scale/Mathf.Max(1,style.ReferencePixels));
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
            while(t<.6f&&Match.Player==player&&Match.Phase==MatchPhase.Preparation){t+=Time.deltaTime;Vector2 end=Match.Actors.TryGetValue(target,out var actor)?ToCanvas(GameCamera.WorldToScreenPoint(actor.transform.position+Vector3.up)):target.Location==UnitLocation.Bench?new Vector2(449,799):new Vector2(target.Definition.Id=="dummy"?92:189,799);Vector2 p=Vector2.Lerp(from,end,Mathf.SmoothStep(0,1,t/.6f));SetRect(star.rectTransform,new Rect(p.x-15,p.y-15,30,30));yield return null;}if(star!=null)Destroy(star.gameObject);
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
            var rt=Panel(parent,"Text",rect,Color.clear,false);var label=rt.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.color=color;label.fontStyle=FontStyle.Normal;label.text=text;label.alignment=align;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
        RectTransform Button(Transform parent,string text,Rect rect,Action action,Color background,Color foreground,int size=20)
        {
            var rt=Panel(parent,text,rect,background);var b=rt.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>{PlaySound("button");action();});var c=b.colors;c.highlightedColor=new Color(1,1,1,.9f);c.pressedColor=new Color(.72f,.8f,.8f);b.colors=c;Label(rt,text,new Rect(6,3,rect.width-12,rect.height-6),size,foreground,FontStyle.Bold);return rt;
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
        }
        void Portrait(Transform parent,string id,Rect rect,Color? color=null){var unitArt=art?.Get(id);Image(parent,unitArt?.Portrait,rect,color);}
    }

    // A persistent surface below the other UI captures the entire board gesture.
    // EventSystem preserves the press origin and routes mouse and touch identically.
    public sealed class BoardPointerGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        LocalGameUI ui;int? pointer;
        public void Configure(LocalGameUI owner){ui=owner;}
        public void OnPointerDown(PointerEventData e)
        {if(pointer==null && e.button==PointerEventData.InputButton.Left && ui.BeginBoardPointer(e.position))pointer=e.pointerId;}
        public void OnDrag(PointerEventData e){if(pointer==e.pointerId)ui.MoveBoardPointer(e.position);}
        public void OnPointerUp(PointerEventData e){if(pointer==e.pointerId){ui.EndBoardPointer(e.position);pointer=null;}}
        public void OnEndDrag(PointerEventData e){if(pointer==e.pointerId){ui.CancelBoardPointer();pointer=null;}}
        void OnDisable(){if(ui!=null)ui.CancelBoardPointer();pointer=null;}
    }

    public sealed class BriefOfferGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        LocalGameUI ui;int slot;long token;Vector2 position;
        readonly PointerGesture gesture=new PointerGesture();
        public string UnitId { get; private set; }
        public void Configure(LocalGameUI owner,int offerSlot,long offerToken,string id)
        {ui=owner;slot=offerSlot;token=offerToken;UnitId=id;}
        public void OnPointerDown(PointerEventData e)
        {if(ui.BriefReady && e.button==PointerEventData.InputButton.Left){position=e.position;gesture.Begin(e.position,Time.unscaledTime);}}
        public void OnDrag(PointerEventData e)
        {position=e.position;gesture.Move(position,Time.unscaledTime);if(gesture.Dragging)ui.PreviewOffer(slot,token,position);}
        void Update()
        {if(gesture.Active && gesture.Move(position,Time.unscaledTime)==GestureResult.Hold)ui.InspectOffer(slot,token);}
        public void OnPointerUp(PointerEventData e)
        {
            gesture.Move(e.position,Time.unscaledTime);var result=gesture.End();ui.EndDragPreview();
            if(result==GestureResult.Tap)ui.SelectOffer(slot,token);
            else if(result==GestureResult.Drag)ui.DropOffer(slot,token,e.position);
        }
        public void OnEndDrag(PointerEventData e){ui.EndDragPreview();}
    }

    public sealed class UnitCardGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        LocalGameUI ui;OwnedUnit unit;bool enemy;Vector2 position;readonly PointerGesture gesture=new PointerGesture();
        public void Configure(LocalGameUI owner,OwnedUnit owned,bool rival){ui=owner;unit=owned;enemy=rival;}
        public void OnPointerDown(PointerEventData e){position=e.position;gesture.Begin(e.position,Time.unscaledTime);}
        public void OnDrag(PointerEventData e){position=e.position;gesture.Move(e.position,Time.unscaledTime);if(gesture.Dragging&&!enemy)ui.DragPreview(unit,e.position);}
        void Update(){if(gesture.Active&&gesture.Move(position,Time.unscaledTime)==GestureResult.Hold)ui.ShowInspection(unit,enemy);}
        public void OnPointerUp(PointerEventData e){gesture.Move(e.position,Time.unscaledTime);var result=gesture.End();ui.EndDragPreview();if(result==GestureResult.Tap)ui.Select(unit,enemy);else if(result==GestureResult.Drag&&!enemy)ui.Drop(unit,e.position);}
        public void OnEndDrag(PointerEventData e){ui.EndDragPreview();}
    }
}

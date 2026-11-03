using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        OpponentEchoOwner echoOwner;
        readonly System.Collections.Generic.Dictionary<string,Rect> echoHitRects=new System.Collections.Generic.Dictionary<string,Rect>();
        readonly System.Collections.Generic.Dictionary<string,OpponentEchoView> opponentViews=new System.Collections.Generic.Dictionary<string,OpponentEchoView>();
        readonly System.Collections.Generic.Dictionary<string,RectTransform> echoTargets=new System.Collections.Generic.Dictionary<string,RectTransform>();
        MatchPhase entrancePhase = (MatchPhase)(-1);
        string entranceMenu;
        int entranceRound;
        Coroutine pageEntrance;

        void RenderOpponentEchoes()
        {
            if(echoOwner!=null)echoOwner.Clear();
            echoOwner = null;
            echoHitRects.Clear();
            opponentViews.Clear();
            echoTargets.Clear();
            if (Match.Phase != MatchPhase.Preparation) return;
            var root = Panel(page, "Previous opponent formation", new Rect(0,0,540,960),Color.clear,false);
            root.SetAsFirstSibling();
            echoOwner = root.gameObject.AddComponent<OpponentEchoOwner>();
            echoOwner.Configure(Match);
            bool initial=Match.Round==1;
            var echoes=initial&&Match.InitialOpponent!=null?new[]{Match.InitialOpponent}:Match.PreviousOpponent.ToArray();
            foreach (var echo in echoes.GroupBy(e=>e.Position).Select(g=>g.First()).OrderBy(e=>e.Position.y).ThenBy(e=>e.Position.x))
            {
                // Only previously revealed positions are drawn. Inspection may show
                // current upgrades of this identity without revealing its new cell.
                var cell = Match.Board.GetCell(echo.Position.x,echo.Position.y);
                if (cell == null) continue;
                var view=new GameObject("opponent-echo-"+echo.UnitId).AddComponent<OpponentEchoView>();
                view.Configure(echo.UnitId,echo.Position,Match.Mapper.GetWorldPosition(cell),art?.Get(echo.UnitId),!initial,Match.Mapper.CellSize.x);
                echoOwner.Add(view);
                opponentViews[echo.UnitId]=view;
                // This is only the inspection hit target. The real-size sprite is
                // rendered on the board, behind nearer figures using their depth.
                echoTargets[echo.UnitId]=Panel(root,"echo-"+echo.UnitId,new Rect(),Color.clear,false);
            }
            UpdateOpponentEchoes();
        }
        void UpdateOpponentEchoes()
        {
            if(Match.Phase!=MatchPhase.Preparation)
            {
                if(echoOwner!=null)echoOwner.Clear();
                echoHitRects.Clear();
                return;
            }
            foreach(var pair in opponentViews)
            {
                var view=pair.Value;
                if(view==null||view.Renderer==null||!view.gameObject.activeInHierarchy)continue;
                var body=view.Renderer.bounds;
                var bottom=WorldToPortrait(body.min);
                var top=WorldToPortrait(body.max);
                var bounds=Rect.MinMaxRect(Mathf.Min(bottom.x,top.x),Mathf.Min(bottom.y,top.y),Mathf.Max(bottom.x,top.x),Mathf.Max(bottom.y,top.y));
                echoHitRects[pair.Key]=bounds;
                if(echoTargets.TryGetValue(pair.Key,out var target)&&target!=null)SetRect(target,bounds);
            }
        }
        void AnimatePageEntrance()
        {
            // Economy refreshes are immediate: only actual navigation/phase changes animate.
            bool change = entrancePhase!=Match.Phase || entranceRound!=Match.Round || entranceMenu!=menuPage;
            entrancePhase=Match.Phase;entranceRound=Match.Round;entranceMenu=menuPage;
            if(Match.Phase==MatchPhase.Menu)
            {
                if(pageEntrance!=null)StopCoroutine(pageEntrance);pageEntrance=null;
                var group=page.GetComponent<CanvasGroup>();if(group!=null)group.alpha=1;
                return;
            }
            if(!change)return;
            if(pageEntrance!=null)StopCoroutine(pageEntrance);
            pageEntrance=StartCoroutine(PageEntrance());
        }
        IEnumerator PageEntrance()
        {
            var group=page.GetComponent<CanvasGroup>();
            if(group==null)group=page.gameObject.AddComponent<CanvasGroup>();
            float t=0;
            while(t<.25f)
            {
                t+=Time.unscaledDeltaTime;
                group.alpha=Mathf.SmoothStep(.35f,1,t/.25f);
                yield return null;
            }
            group.alpha=1;pageEntrance=null;
        }
        void AnimateSheet(RectTransform sheet)
        {
            var motion=sheet.gameObject.AddComponent<SheetEntrance>();
            motion.enabled=true;
        }
    }

    public sealed class SheetEntrance : MonoBehaviour
    {
        RectTransform rect; CanvasGroup group; Vector2 rest; float age;
        void Awake(){rect=(RectTransform)transform;rest=rect.anchoredPosition;group=gameObject.AddComponent<CanvasGroup>();}
        void Update()
        {
            age+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(age/.24f),ease=1-Mathf.Pow(1-t,3);
            group.alpha=ease;rect.anchoredPosition=rest+Vector2.down*(1-ease)*18;
            if(t>=1){rect.anchoredPosition=rest;enabled=false;}
        }
    }

    public sealed class LobbyPersonality : MonoBehaviour
    {
        Image portrait;Sprite original;
        LobbyAnimationDirector director;LobbyFrameGraphic motion;
        float elapsed,duration;bool configured;
        public string UnitId { get; private set; }
        public int FrameCount => motion!=null?motion.PoseCount:0;
        public int FrameIndex { get; private set; }
        public bool Featured { get; private set; }
        public bool PreviewOnOpen { get; private set; }
        internal float ReadyWait { get; private set; }
        public bool IsGesturing { get; private set; }
        public bool HasContinuousGesture => motion!=null&&motion.HasContinuousGesture;
        public bool HasAuthoredFrames => motion!=null&&motion.HasAuthoredFrames;
        public LobbyFrameGraphic GestureGraphic => motion;
        public float SecondsUntilGesture { get; private set; }
        public float GestureDuration => duration;
        public void Configure(string unitId,float height,LobbyAnimationDirector scheduler,bool featured=false,bool previewOnOpen=false,LobbyArtworkContext context=LobbyArtworkContext.Standard,bool useLobbyView=false)
        {
            UnitId=unitId;Featured=featured;PreviewOnOpen=previewOnOpen;director=scheduler;
            portrait=GetComponent<Image>();original=portrait.sprite;
            var shadow=new GameObject("Contact shadow",typeof(RectTransform));
            var shadowRect=(RectTransform)shadow.transform;shadowRect.SetParent(transform,false);
            shadowRect.anchorMin=Vector2.zero;shadowRect.anchorMax=Vector2.one;shadowRect.offsetMin=shadowRect.offsetMax=Vector2.zero;
            shadow.AddComponent<LobbyContactShadow>();
            var child=new GameObject("Drawn lobby gesture",typeof(RectTransform));
            var rect=(RectTransform)child.transform;rect.SetParent(transform,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            motion=child.AddComponent<LobbyFrameGraphic>();
            if(motion.Configure(unitId,LobbyAnimationDirector.PlaybackEnabled,context,useLobbyView))portrait.enabled=false;
            else {Destroy(child);motion=null;}
            configured=true;director.Register(this);ShowPose(-1);
        }
        public bool PlayGesture()=>director!=null&&director.TryPlay(this);
        internal void BeginGesture(float speed)
        {
            if(!LobbyAnimationDirector.PlaybackEnabled){Rest(0);return;}
            elapsed=0;ReadyWait=0;duration=LobbyGestureTiming.For(UnitId).Duration*speed;
            IsGesturing=true;ShowPose(0);
        }
        internal void Wait(float delta)
        {
            float remaining=SecondsUntilGesture;
            SecondsUntilGesture=Mathf.Max(0,remaining-delta);
            ReadyWait+=Mathf.Max(0,delta-remaining);
        }
        internal void Rest(float delay)
        {
            IsGesturing=false;elapsed=0;ReadyWait=0;SecondsUntilGesture=delay;ShowPose(-1);
        }
        public void Advance(float delta)
        {
            if(!LobbyAnimationDirector.PlaybackEnabled){if(IsGesturing)Rest(0);return;}
            if(!IsGesturing||FrameCount==0||delta<=0||float.IsNaN(delta)||float.IsInfinity(delta))return;
            elapsed+=delta;
            if(elapsed>=duration){IsGesturing=false;ShowPose(-1);return;}
            ShowPose(0);
        }
        void ShowPose(int index)
        {
            if(portrait==null||motion==null)return;
            motion.SampleFrame(duration>0?elapsed/duration:0,IsGesturing&&index>=0);portrait.sprite=motion.CurrentSprite;
            FrameIndex=motion.FrameIndex;
        }
        void OnDisable()
        {
            if(director!=null)director.Unregister(this);
            if(portrait!=null){portrait.sprite=original;portrait.enabled=true;}
        }
        void OnEnable()
        {
            if(!configured)return;
            if(motion!=null)portrait.enabled=false;
            director.Register(this);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public readonly struct LobbyStonePose
    {
        public readonly Vector2 Position;
        public readonly float Alpha, Rotation;
        public LobbyStonePose(Vector2 position,float alpha,float rotation)
        {Position=position;Alpha=alpha;Rotation=rotation;}
    }

    /// <summary>Two persistent props, sampled continuously rather than baked into body poses.</summary>
    public sealed class LobbyJugglingStones : MonoBehaviour
    {
        const float PalmX=.36f, PalmY=.35f, StoneSize=.13f;
        readonly Image[] stones=new Image[2];
        LobbyFrameGraphic body;
        float progress;
        bool active;
        public int RockCount=>stones[0]!=null&&stones[1]!=null?2:0;
        public int VisibleRockCount=>(stones[0]!=null&&stones[0].enabled&&stones[0].color.a>.01f?1:0)+
            (stones[1]!=null&&stones[1].enabled&&stones[1].color.a>.01f?1:0);

        public void Configure(LobbyFrameGraphic graphic)
        {
            body=graphic;
            for(int i=0;i<stones.Length;i++)
            {
                if(stones[i]!=null)continue;
                var go=new GameObject("Juggling stone "+(i+1),typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
                go.transform.SetParent(transform,false);
                var image=go.GetComponent<Image>();image.sprite=LobbyRigAssets.Get("props",i==0?"stone-a":"stone-b");image.raycastTarget=false;
                image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(.5f,.5f);
                image.rectTransform.pivot=new Vector2(.5f,.5f);image.enabled=false;stones[i]=image;
            }
        }

        public void Sample(float normalized,bool gesturing)
        {progress=Mathf.Clamp01(normalized);active=gesturing;Draw();}

        public void Hide(){active=false;Draw();}
        void OnDisable()=>Hide();
        // Layout can change after the director advances; keep prop positions attached
        // to the same foot anchor and scale as the freshly laid-out character.
        void LateUpdate()=>Draw();
        void Draw()
        {
            if(body==null)return;
            float height=body.BodyHeight;
            float poseProgress=LobbyGestureTiming.For("trimol").PoseProgress(progress);
            for(int i=0;i<stones.Length;i++)
            {
                var image=stones[i];if(image==null)continue;
                image.enabled=active&&height>0;
                if(!image.enabled)continue;
                var pose=Evaluate(i,progress);
                image.rectTransform.anchoredPosition=body.StabilizeContinuousPoint(body.FootAnchor+
                    new Vector2(pose.Position.x*body.GestureHorizontalScale,pose.Position.y)*height,poseProgress);
                image.rectTransform.sizeDelta=Vector2.one*height*StoneSize;
                image.rectTransform.localRotation=Quaternion.Euler(0,0,pose.Rotation);
                image.color=new Color(1,1,1,pose.Alpha*body.color.a);
            }
        }

        // Positions are measured in character heights from the foot anchor. The
        // two arcs use different heights, so the stones pass above/below each other.
        public static LobbyStonePose Evaluate(int stoneIndex,float normalized)
        {
            var timing=LobbyGestureTiming.For("trimol");
            float time=Mathf.Clamp01(normalized)*timing.Duration;
            float side=stoneIndex==0?-1:1;
            var stowed=new Vector2(side*.34f,.13f);
            var palm=new Vector2(side*PalmX,PalmY);
            if(time<timing.Reveal)
            {
                float p=Mathf.Clamp01(time/timing.Reveal);
                float alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.45f,p));
                return new LobbyStonePose(Vector2.Lerp(stowed,palm,Mathf.SmoothStep(0,1,p)),alpha,0);
            }
            float main=time-timing.Reveal;
            if(main<timing.Main)
            {
                // Quintic take-off/landing leaves both props near the hands longer.
                // A short stagger keeps the two physical stones clear in mid-air.
                float delay=stoneIndex==0?0:.12f;
                const float flight=.65f, secondThrow=1.30f;
                float arcHeight=stoneIndex==0?.76f:.42f;
                if(main<delay)return new LobbyStonePose(palm,1,0);
                if(main<delay+flight)return Flight(side,(main-delay)/flight,arcHeight,0);
                if(main<delay+secondThrow)return new LobbyStonePose(new Vector2(-side*PalmX,PalmY),1,-side*180);
                if(main<delay+secondThrow+flight)return Flight(-side,(main-delay-secondThrow)/flight,arcHeight,-side*180);
                return new LobbyStonePose(palm,1,0);
            }
            float finish=Mathf.Clamp01((main-timing.Main)/timing.Return);
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.92f,finish));
            return new LobbyStonePose(Vector2.Lerp(palm,stowed,Mathf.SmoothStep(0,1,finish)),fade,0);
        }

        static LobbyStonePose Flight(float startSide,float p,float height,float initialRotation)
        {
            p=LobbyRigMotion.Ease(p);
            return new LobbyStonePose(new Vector2(Mathf.Lerp(startSide*PalmX,-startSide*PalmX,p),
                PalmY+height*Mathf.Sin(Mathf.PI*p)),1,initialRotation-startSide*180*p);
        }

    }
}

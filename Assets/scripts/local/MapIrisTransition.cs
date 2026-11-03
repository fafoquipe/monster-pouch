using System;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Independent of page rebuilds and of the combat clock (including online rounds).
    public sealed class MapIrisTransition : MonoBehaviour
    {
        public const float CloseSeconds=.38f, HoldSeconds=.64f, OpenSeconds=.44f;
        public const float CoveredBeforeSwapSeconds=.12f;
        public const float Duration=CloseSeconds+HoldSeconds+OpenSeconds;
        IrisCurtain curtain;
        Image newWorld;
        Action changeMap;
        float age;
        bool committed;
        int coveredFrame=-1,requestVersion;
        public bool IsRunning {get;private set;}
        public TableStyle TargetStyle {get;private set;}
        public float Progress=>Mathf.Clamp01(age/Duration);
        public bool IsCovered=>IsRunning&&age>=CloseSeconds&&age<CloseSeconds+HoldSeconds;

        public static MapIrisTransition Create(Transform parent)
        {
            var root=new GameObject("Map circular transition",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(540,960);scaler.matchWidthOrHeight=0;
            var view=root.AddComponent<MapIrisTransition>();
            var mask=new GameObject("Circular curtain",typeof(RectTransform),typeof(CanvasRenderer),typeof(IrisCurtain));
            mask.transform.SetParent(root.transform,false);var rect=mask.GetComponent<RectTransform>();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            view.curtain=mask.GetComponent<IrisCurtain>();view.curtain.color=Color.black;
            view.newWorld=World(root.transform,"Destination mini-world");
            root.SetActive(false);return view;
        }
        static Image World(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(260,260);
            var image=go.GetComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        public void Play(TableStyle to,Action apply)=>Play(to,apply,null);
        public void Play(TableStyle to,Action apply,Sprite preview)
        {
            // A later request replaces the pending destination; stale callbacks cannot reopen old pages.
            // A replacement keeps an already closed curtain closed, including callback reentry.
            age=IsRunning?Mathf.Min(age,CloseSeconds):0;
            requestVersion++;coveredFrame=-1;
            changeMap=apply;TargetStyle=to;committed=false;IsRunning=true;
            newWorld.sprite=preview!=null?preview:CardTableTheme.MiniWorld(to);
            gameObject.SetActive(true);Canvas.ForceUpdateCanvases();Paint();
        }
        void Update()=>Advance(Time.unscaledDeltaTime);
        void Advance(float delta)
        {
            if(!IsRunning)return;
            if(!committed)
            {
                age=Mathf.Min(age+Mathf.Max(0,delta),CloseSeconds+CoveredBeforeSwapSeconds);
                if(age>=CloseSeconds&&coveredFrame<0)
                {
                    // Present a fully opaque frame before any preference, redraw or scene mutation.
                    age=CloseSeconds;coveredFrame=Time.frameCount;Paint();Canvas.ForceUpdateCanvases();return;
                }
                Paint();
                if(age<CloseSeconds+CoveredBeforeSwapSeconds||Time.frameCount<=coveredFrame)return;
                int version=requestVersion;
                Canvas.ForceUpdateCanvases();
                if(version!=requestVersion||!IsRunning)return;
                committed=true;var apply=changeMap;changeMap=null;
                try{apply?.Invoke();}catch(Exception e){if(version==requestVersion)Cancel();Debug.LogException(e);}
                // Never consume the opening interval in the swap frame, even after a long hitch.
                return;
            }
            age+=Mathf.Max(0,delta);
            if(age>=Duration){Cancel();return;}
            Paint();
        }
        void Paint()
        {
            float max=curtain.rectTransform.rect.size.magnitude*.5f+4;
            float fraction=age<CloseSeconds?1-Mathf.SmoothStep(0,1,age/CloseSeconds)
                :age<CloseSeconds+HoldSeconds?0:Mathf.SmoothStep(0,1,(age-CloseSeconds-HoldSeconds)/OpenSeconds);
            curtain.Radius=max*fraction;
            float fadeIn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(CloseSeconds*.55f,CloseSeconds,age));
            float fadeOut=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(CloseSeconds+HoldSeconds,CloseSeconds+HoldSeconds+OpenSeconds*.65f,age));
            newWorld.color=new Color(1,1,1,fadeIn*fadeOut);
            newWorld.rectTransform.localScale=Vector3.one*(.94f+.06f*fadeIn);
        }
        public void Cancel(){changeMap=null;IsRunning=false;gameObject.SetActive(false);}
        void OnDisable(){changeMap=null;IsRunning=false;}
    }

    // A small native mesh, no full-screen texture or per-frame allocations.
    public sealed class IrisCurtain : MaskableGraphic
    {
        float radius;
        public float Radius {get=>radius;set{radius=Mathf.Max(0,value);SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect rect=rectTransform.rect;Vector2 center=rect.center;
            float outer=rect.size.magnitude+8;const int segments=128;
            for(int i=0;i<=segments;i++)
            {
                float angle=i*2*Mathf.PI/segments;Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                vh.AddVert(center+direction*radius,color,Vector2.zero);vh.AddVert(center+direction*outer,color,Vector2.zero);
                if(i>0){int p=(i-1)*2;vh.AddTriangle(p,p+1,p+2);vh.AddTriangle(p+1,p+3,p+2);}
            }
        }
    }
}


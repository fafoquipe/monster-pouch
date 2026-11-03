using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // One non-interactive mesh: no texture allocations or individual dash objects.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttackRangeGraphic : MaskableGraphic
    {
        [SerializeField, Min(.1f)] float pathRevealDuration = .65f;
        public float PathProgress { get; private set; }
        public Rect RangeBounds { get; private set; }
        public Vector2 Source { get; private set; }
        public Vector2? Target { get; private set; }
        public Vector2Int SourceCell { get; private set; }
        public Vector2Int? TargetCell { get; private set; }
        static readonly Color Border = new Color32(244,83,88,230);
        static readonly Color Shadow = new Color32(95,31,40,160);

        protected override void OnEnable()
        {
            base.OnEnable();
            PathProgress = 0;
        }

        void Update()
        {
            if (!Target.HasValue || PathProgress >= 1) return;
            PathProgress = Mathf.Min(1,PathProgress+Time.unscaledDeltaTime/Mathf.Max(.1f,pathRevealDuration));
            SetVerticesDirty();
        }

        public void SetPreview(Rect bounds, Vector2 source, Vector2? target, Vector2Int sourceCell,
            Vector2Int? targetCell, bool restartPath = false, bool immediatePath = false)
        {
            if(immediatePath)
            {
                if(PathProgress!=1){PathProgress=1;SetVerticesDirty();}
            }
            else if (restartPath || SourceCell != sourceCell || TargetCell != targetCell)
            {
                PathProgress = 0;
                SetVerticesDirty();
            }
            SourceCell = sourceCell; TargetCell = targetCell;
            if (RangeBounds == bounds && Source == source && Target == target) return;
            RangeBounds = bounds; Source = source; Target = target;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = RangeBounds;
            var a = new Vector2(r.xMin,r.yMin);
            var b = new Vector2(r.xMax,r.yMin);
            var c = new Vector2(r.xMax,r.yMax);
            var d = new Vector2(r.xMin,r.yMax);
            Dash(mesh,a,b,3,8,6); Dash(mesh,b,c,3,8,6);
            Dash(mesh,c,d,3,8,6); Dash(mesh,d,a,3,8,6);
            if (!Target.HasValue) return;
            Vector2 end = Target.Value;
            Vector2 direction = (end-Source).normalized;
            if (Vector2.Distance(Source,end)>24)
                Dash(mesh,Source+direction*10,end-direction*12,5,12,9,PathProgress,true);
            if (PathProgress < 1) return;
            // Stepped, four-point star remains readable over any character artwork.
            Star(mesh,end);
        }

        static void Dash(VertexHelper mesh, Vector2 from, Vector2 to, float width, float dash, float gap,
            float progress = 1, bool fadeIn = false)
        {
            float length = Vector2.Distance(from,to);
            if (length < .01f) return;
            Vector2 direction = (to-from)/length;
            // Equal spacing includes both ends, keeping each corner clearly joined.
            int count = Mathf.Max(1,Mathf.RoundToInt((length+gap)/(dash+gap)));
            float actualGap = count>1 ? Mathf.Max(0,(length-count*dash)/(count-1)) : 0;
            float fadeLength = fadeIn ? Mathf.Max(dash,length*.18f) : 0;
            float revealFront = (length+fadeLength)*Mathf.Clamp01(progress);
            float visibleLength = Mathf.Min(length,revealFront);
            for (int i=0;i<count;i++)
            {
                float start = i*(dash+actualGap);
                if (start >= visibleLength) break;
                var a = from+direction*start;
                var b = from+direction*Mathf.Min(visibleLength,start+dash);
                float opacity = fadeIn ? Mathf.SmoothStep(0,1,Mathf.Clamp01((revealFront-start)/fadeLength)) : 1;
                var shadow = Shadow; shadow.a *= opacity;
                var border = Border; border.a *= opacity;
                Stroke(mesh,a,b,width+2,shadow);
                Stroke(mesh,a,b,width,border);
            }
        }

        static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            int first = mesh.currentVertCount;
            Vertex(mesh,a-normal,tint); Vertex(mesh,a+normal,tint);
            Vertex(mesh,b+normal,tint); Vertex(mesh,b-normal,tint);
            mesh.AddTriangle(first,first+1,first+2); mesh.AddTriangle(first,first+2,first+3);
        }

        static float StarHalfWidth(int radius, float y) => Mathf.Abs(y) >= radius ? 0 :
            Mathf.Max(1,Mathf.Round(radius*Mathf.Pow(1-Mathf.Abs(y)/radius,2)*.5f)*2);

        static void Star(VertexHelper mesh, Vector2 center)
        {
            // Non-overlapping bands keep the entire marker at 50% opacity,
            // including its center, so the targeted character remains visible.
            var rim = new Color32(95,31,40,128);
            var gold = new Color32(255,204,91,128);
            var core = new Color32(255,250,216,128);
            for (int y=-22;y<22;y+=2)
            {
                float outer = StarHalfWidth(22,y+1), middle = StarHalfWidth(18,y+1), inner = StarHalfWidth(10,y+1);
                void Span(float left,float right,Color tint)
                {
                    if (right > left)
                        Stroke(mesh,center+new Vector2(left,y+1),center+new Vector2(right,y+1),2,tint);
                }
                Span(-outer,-middle,rim); Span(middle,outer,rim);
                Span(-middle,-inner,gold); Span(inner,middle,gold);
                Span(-inner,inner,core);
            }
        }

        static void Vertex(VertexHelper mesh, Vector2 point, Color tint) =>
            mesh.AddVert(new Vector3(point.x,-point.y,0),tint,Vector2.zero);
    }
}

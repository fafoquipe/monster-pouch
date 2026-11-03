using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local.Boards
{
    // Small beveled pixel UI platform; all character/world art remains authored artwork.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProgressionStepGraphic : MaskableGraphic
    {
        public Color Surface=new Color32(129,151,112,255), Edge=new Color32(58,77,59,255);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            float x=r.x,y=r.y,w=r.width,h=r.height,b=8;
            Quad(vh,new Vector2(x+b,y),new Vector2(x+w-b,y),new Vector2(x+w,y+h*.42f),new Vector2(x,y+h*.42f),Edge);
            Quad(vh,new Vector2(x,y+h*.42f),new Vector2(x+w,y+h*.42f),new Vector2(x+w-b,y+h),new Vector2(x+b,y+h),Surface);
            Quad(vh,new Vector2(x+b,y+h-3),new Vector2(x+w-b,y+h-3),new Vector2(x+w-b,y+h),new Vector2(x+b,y+h),Color.Lerp(Surface,Color.white,.32f));
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
        {
            int i=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);
            vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}

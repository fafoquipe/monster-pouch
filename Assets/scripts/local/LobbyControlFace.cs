using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyControlFace : MaskableGraphic
    {
        public Color Fill=new Color32(44,61,65,255);
        public Color Edge=new Color32(26,37,43,255);
        public Color Shine=new Color32(100,119,110,255);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            void Box(float x,float y,float w,float h,Color tint)
            {
                if(w<=0||h<=0)return;int n=mesh.currentVertCount;tint*=color;
                mesh.AddVert(new Vector2(x,y),tint,Vector2.zero);mesh.AddVert(new Vector2(x,y+h),tint,Vector2.zero);
                mesh.AddVert(new Vector2(x+w,y+h),tint,Vector2.zero);mesh.AddVert(new Vector2(x+w,y),tint,Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
            }
            void Plate(float inset,Color tint)
            {
                float x=r.xMin+inset,y=r.yMin+inset,w=r.width-2*inset,h=r.height-2*inset;
                Box(x+7,y,w-14,h,tint);Box(x+3,y+3,w-6,h-6,tint);Box(x,y+7,w,h-14,tint);
            }
            Plate(0,Edge);Plate(3,Fill);
            Box(r.xMin+10,r.yMax-6,r.width-20,2,Shine);
            Box(r.xMin+9,r.yMin+4,r.width-18,3,Color.Lerp(Fill,Edge,.45f));
        }
    }
}

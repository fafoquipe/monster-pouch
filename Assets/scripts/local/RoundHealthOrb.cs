using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    /// <summary>Small UI orb; its position, color and scale follow the round timeline.</summary>
    public sealed class RoundHealthOrb : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 center=rectTransform.rect.center;
            float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
            Disc(mesh,center,radius,new Color(0,0,0,color.a));
            Disc(mesh,center,radius-2.5f,color);
            var shade=new Color(color.r*.55f,color.g*.55f,color.b*.55f,color.a);
            Disc(mesh,center+new Vector2(2,-2),radius*.48f,shade);
            var highlight=Color.Lerp(color,Color.white,.65f);highlight.a=color.a;
            Disc(mesh,center+new Vector2(-3,3),radius*.22f,highlight);
        }
        static void Disc(VertexHelper mesh,Vector2 center,float radius,Color tint)
        {
            const int steps=32;
            int start=mesh.currentVertCount;
            mesh.AddVert(center,tint,Vector2.zero);
            for(int i=0;i<=steps;i++)
            {
                float angle=i*Mathf.PI*2/steps;
                mesh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint,Vector2.zero);
            }
            for(int i=0;i<steps;i++)mesh.AddTriangle(start,start+i+1,start+i+2);
        }
    }
}

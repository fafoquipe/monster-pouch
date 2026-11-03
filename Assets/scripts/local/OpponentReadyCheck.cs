using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    // Native geometry avoids missing check glyphs in mobile fonts.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OpponentReadyCheck : MaskableGraphic
    {
        float progress;
        public void SetProgress(float value){raycastTarget=false;progress=Mathf.Clamp01(value);SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(progress<=0)return;
            Vector2 center=rectTransform.rect.center;float size=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height);
            var dark=new Color(.08f,.25f,.22f,progress*.96f);var light=new Color(.47f,.94f,.75f,progress);
            // A stepped seal, then a check drawn from the short stroke to the long stroke.
            Quad(vh,center+new Vector2(-size*.37f,-size*.5f),new Vector2(size*.74f,size),dark);
            Quad(vh,center+new Vector2(-size*.5f,-size*.37f),new Vector2(size,size*.74f),dark);
            Vector2 a=center+new Vector2(-.28f,0)*size,b=center+new Vector2(-.07f,-.20f)*size,c=center+new Vector2(.30f,.25f)*size;
            float t=Mathf.Clamp01(progress*1.6f);
            Stroke(vh,a,Vector2.Lerp(a,b,Mathf.Clamp01(t/.36f)),size*.12f,light);
            if(t>.36f)Stroke(vh,b,Vector2.Lerp(b,c,(t-.36f)/.64f),size*.12f,light);
        }
        static void Quad(VertexHelper v,Vector2 p,Vector2 s,Color color)
        {int i=v.currentVertCount;v.AddVert(p,color,Vector2.zero);v.AddVert(p+new Vector2(0,s.y),color,Vector2.zero);v.AddVert(p+s,color,Vector2.zero);v.AddVert(p+new Vector2(s.x,0),color,Vector2.zero);v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);}
        static void Stroke(VertexHelper v,Vector2 a,Vector2 b,float width,Color color)
        {if((b-a).sqrMagnitude<.0001f)return;Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int i=v.currentVertCount;
            v.AddVert(a-n,color,Vector2.zero);v.AddVert(a+n,color,Vector2.zero);v.AddVert(b+n,color,Vector2.zero);v.AddVert(b-n,color,Vector2.zero);v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);}
    }
}



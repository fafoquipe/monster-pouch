using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // One graphic, subdivided into adjacent one-cell quads, sharing a connected water texture.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MaestroWaveGraphic : MaskableGraphic
    {
        Sprite sprite;
        readonly Vector2[] vertices=new Vector2[64];
        readonly Vector2[] textureUvs=new Vector2[64];
        readonly Color[] colors=new Color[64];
        int count;
        public override Texture mainTexture => sprite!=null?sprite.texture:Texture2D.whiteTexture;
        public void Begin(Sprite art,float alpha)
        {
            if(sprite!=art){sprite=art;SetMaterialDirty();}
            count=0;color=new Color(1,1,1,alpha);raycastTarget=false;
        }
        public void Add(Rect uv,float alpha,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            if(count+4>vertices.Length)return;
            for(int i=count;i<count+4;i++)colors[i]=new Color(1,1,1,alpha);
            Rect r=sprite.textureRect;
            uv=new Rect((r.x+uv.x*r.width)/sprite.texture.width,(r.y+uv.y*r.height)/sprite.texture.height,
                uv.width*r.width/sprite.texture.width,uv.height*r.height/sprite.texture.height);
            vertices[count]=a;textureUvs[count++]=new Vector2(uv.xMin,uv.yMin);
            vertices[count]=b;textureUvs[count++]=new Vector2(uv.xMin,uv.yMax);
            vertices[count]=c;textureUvs[count++]=new Vector2(uv.xMax,uv.yMax);
            vertices[count]=d;textureUvs[count++]=new Vector2(uv.xMax,uv.yMin);
        }
        public void Complete()=>SetVerticesDirty();
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(sprite==null)return;
            for(int i=0;i<count;i++)vh.AddVert(vertices[i],colors[i],textureUvs[i]);
            for(int i=0;i<count;i+=4){vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i+2,i+3,i);}
        }
    }
    public static class MaestroWaveArt
    {
        public const string Resource="MonsterPouch/Eronya/Aquarion/VFX/traveling-wave-views";
        // Rectangles in the original generated 1983x793 PNG. Alpha preserved; no image editing.
        public static readonly Rect Front=new Rect(30f/1983,368f/793,1925f/1983,280f/793);
        public static readonly Rect Rear=new Rect(30f/1983,45f/793,1925f/1983,255f/793);
        public static bool FrontFacing(int forward,bool reverseY)=>forward*(reverseY?-1:1)<0;
        public static Rect Uv(Rect section,int forward,bool reverseX,bool reverseY)
        {
            Rect art=FrontFacing(forward,reverseY)?Front:Rear;
            // Include signed CellSize and the viewer's board rotation; both views stay upright.
            float u=reverseX?1-section.x:section.x,v=reverseY?1-section.y:section.y;
            return new Rect(art.x+u*art.width,art.y+v*art.height,
                section.width*art.width*(reverseX?-1:1),section.height*art.height*(reverseY?-1:1));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    /// <summary>Uses the approved sheets unchanged: the frame and the moving fill have separate UV regions.</summary>
    public sealed class RoundHealthBarArtwork : MonoBehaviour
    {
        public string MapId { get; private set; }
        public string ArtworkId { get; private set; }
        public bool IsLocal { get; private set; }
        public Image Fill { get; private set; }
        public Rect TotalRect { get; private set; }
        public Vector2 FillCenter { get; private set; }
        Image shine;

        sealed class Pieces
        {
            public Texture2D Texture;
            public Rect Frame, Fill, Total;
            public Sprite Left, Right, Top, Bottom, Contents;
        }
        static readonly Dictionary<string, Pieces> Cache = new Dictionary<string, Pieces>();
        static Sprite white;

        public void Configure(string mapId, bool local, string fillName)
        {
            MapId = mapId; ArtworkId = SharedArtworkId(mapId); IsLocal = local;
            var parts = Load(ArtworkId, local);
            var rect = ((RectTransform)transform).rect;
            float scale = rect.width / parts.Frame.width;
            float top = (rect.height - parts.Frame.height * scale) * .5f;
            Rect Place(Rect source) => new Rect((source.x - parts.Frame.x) * scale,
                top + (source.y - parts.Frame.y) * scale, source.width * scale, source.height * scale);
            var frame = parts.Frame; var fill = parts.Fill;
            // Four rectangular regions omit the baked full-health pixels without
            // modifying the original PNG, preserving every approved frame detail.
            Add("Frame left", Place(new Rect(frame.x, frame.y, fill.x-frame.x, frame.height)), parts.Left);
            Add("Frame right", Place(new Rect(fill.xMax, frame.y, frame.xMax-fill.xMax, frame.height)), parts.Right);
            Add("Frame top", Place(new Rect(fill.x, frame.y, fill.width, fill.y-frame.y)), parts.Top);
            Add("Frame bottom", Place(new Rect(fill.x, fill.yMax, fill.width, frame.yMax-fill.yMax)), parts.Bottom);
            var empty = Add("Empty health", Place(fill), null);
            empty.color = ArtworkId=="classic" ? new Color32(17,36,58,255)
                : ArtworkId=="lounge" ? new Color32(23,36,32,255) : new Color32(47,22,26,255);
            Fill = Add(fillName, Place(fill), parts.Contents);
            Fill.type=Image.Type.Filled; Fill.fillMethod=Image.FillMethod.Horizontal; Fill.fillOrigin=0;
            if(white==null) white=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.zero);
            shine=Add("Health arrival shine", Place(fill), white);
            shine.type=Image.Type.Filled; shine.fillMethod=Image.FillMethod.Horizontal; shine.fillOrigin=0;
            TotalRect=Place(parts.Total); FillCenter=Place(fill).center;
            SetValue(0,0); SetPulse(0);
        }

        public void SetValue(float value, float maximum)
        {
            float fraction=maximum>0?Mathf.Clamp01(value/maximum):0;
            Fill.fillAmount=shine.fillAmount=fraction;
        }
        public void SetPulse(float amount) => shine.color=new Color(1,1,1,Mathf.Clamp01(amount)*.16f);

        Image Add(string name, Rect rect, Sprite sprite)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rt=(RectTransform)go.transform; rt.SetParent(transform,false);
            rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(rect.x,-rect.y); rt.sizeDelta=rect.size;
            var image=go.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
            return image;
        }

        // One shared HUD for every map: only scenery changes. Keep MapId for
        // logical ownership and preserve the separate green/red source regions.
        static string SharedArtworkId(string mapId)=>CardTableTheme.KnownMap(mapId)?"classic":mapId;

        static Pieces Load(string mapId, bool local)
        {
            string key=mapId+(local?"-local":"-rival");
            if(Cache.TryGetValue(key,out var cached))return cached;
            var p=new Pieces { Texture=Resources.Load<Texture2D>("MonsterPouch/UI/HealthBars/"+mapId+"-v1") };
            if(p.Texture==null)throw new System.InvalidOperationException("Missing health bar artwork: "+mapId);
            // Source rectangles use top-left coordinates, measured on the original sheets.
            if(mapId=="classic")
            {
                float y=local?140:423;
                p.Frame=new Rect(48,y,1984,220);p.Fill=new Rect(120,y+64,1452,95);
                p.Total=new Rect(1623,y+69,328,86);
            }
            else if(mapId=="lounge")
            {
                float y=local?162:429;
                p.Frame=new Rect(47,y,1984,156);p.Fill=new Rect(155,y+31,1293,93);
                p.Total=new Rect(1501,y+33,399,90);
            }
            else
            {
                float y=local?140:404;
                p.Frame=new Rect(36,y,2008,210);p.Fill=new Rect(173,y+91,1331,87);
                p.Total=new Rect(1550,y+91,346,87);
            }
            var frame=p.Frame;var fill=p.Fill;
            p.Left=Slice(p,new Rect(frame.x,frame.y,fill.x-frame.x,frame.height));
            p.Right=Slice(p,new Rect(fill.xMax,frame.y,frame.xMax-fill.xMax,frame.height));
            p.Top=Slice(p,new Rect(fill.x,frame.y,fill.width,fill.y-frame.y));
            p.Bottom=Slice(p,new Rect(fill.x,fill.yMax,fill.width,frame.yMax-fill.yMax));
            p.Contents=Slice(p,fill);Cache[key]=p;return p;
        }
        static Sprite Slice(Pieces p,Rect topLeft)
        {
            var source=new Rect(topLeft.x,p.Texture.height-topLeft.yMax,topLeft.width,topLeft.height);
            var sprite=Sprite.Create(p.Texture,source,Vector2.zero,100,0,SpriteMeshType.FullRect);
            sprite.name=p.Texture.name+"-"+topLeft;return sprite;
        }
    }
}

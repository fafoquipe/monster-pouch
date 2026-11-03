using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyCabinetBacking : MaskableGraphic
    {
        Sprite sprite;
        public override Texture mainTexture => sprite != null ? sprite.texture : Texture2D.whiteTexture;

        public void Configure(Sprite artwork, Material palette)
        {
            sprite = artwork; material = palette; color = Color.white;
            raycastTarget = false; SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sprite == null) return;
            Rect rect = rectTransform.rect;
            Vector4 uv = DataUtility.GetOuterUV(sprite);
            // One uninterrupted fabric panel: splitting it into five strips produced visible grain seams.
            vh.AddVert(new Vector2(rect.xMin,rect.yMin),color,new Vector2(uv.x,uv.y));
            vh.AddVert(new Vector2(rect.xMin,rect.yMax),color,new Vector2(uv.x,uv.w));
            vh.AddVert(new Vector2(rect.xMax,rect.yMax),color,new Vector2(uv.z,uv.w));
            vh.AddVert(new Vector2(rect.xMax,rect.yMin),color,new Vector2(uv.z,uv.y));
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }
    }
}

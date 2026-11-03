using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class CombatStatusEffects
    {
        readonly Dictionary<Sprite, Bounds> bubbleBodyBounds = new Dictionary<Sprite, Bounds>();
        Bounds BubbleBodyBounds(SpriteRenderer body)
        {
            var sprite = body.sprite;
            if (sprite == null || !sprite.texture.isReadable) return body.bounds;
            if (!bubbleBodyBounds.TryGetValue(sprite, out var local))
            {
                var rect = sprite.rect; int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                var pixels = sprite.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
                int left = width, right = -1, bottom = height, top = -1;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].a >= .5f)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                if (right < left) return body.bounds;
                Vector2 center = (new Vector2((left + right + 1) * .5f, (bottom + top + 1) * .5f) - sprite.pivot) / sprite.pixelsPerUnit;
                local = new Bounds(center, new Vector3(right - left + 1, top - bottom + 1, 0) / sprite.pixelsPerUnit);
                bubbleBodyBounds.Add(sprite, local);
            }
            // Padding in authored animation cells must not lift the bubble above its wearer.
            Vector3 c = local.center;
            if (body.flipX) c.x = -c.x;
            if (body.flipY) c.y = -c.y;
            var world = new Bounds(body.transform.TransformPoint(c), Vector3.zero);
            for (int y = -1; y <= 1; y += 2) for (int x = -1; x <= 1; x += 2)
                world.Encapsulate(body.transform.TransformPoint(c + new Vector3(local.extents.x * x, local.extents.y * y, 0)));
            return world;
        }
    }
}

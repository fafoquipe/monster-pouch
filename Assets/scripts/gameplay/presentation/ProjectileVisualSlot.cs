using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    /// <summary>Presentation metadata on the existing attack/ability asset. Never copied into combat stats.</summary>
    [Serializable]
    public sealed class ProjectileVisualSlot
    {
        public string VariantId = "flight";
        // Identifies the existing presentation renderer, never a combat effect or a replacement sprite.
        // Imported finite-flight clips use "catalog"; procedural/composite producers register their family.
        public string RendererId = "catalog";
        public string NameEnglish = "Projectile", NameSpanish = "Proyectil";
        public bool OverrideSize;
        [Range(0, 100)] public float SizePercent = 50;
        // Informational only. The renderer's existing formula remains authoritative until edited.
        public float LegacyPercent = -1;
        public string LegacyNoteEnglish, LegacyNoteSpanish;
        public ProjectileVisualFrame[] Frames = Array.Empty<ProjectileVisualFrame>();

        public string Name(bool spanish) => spanish ? NameSpanish : NameEnglish;
        public float ClampedPercent => Clamp(SizePercent);
        public static float Clamp(float value) => float.IsNaN(value) ? 0 : Mathf.Clamp(value, 0, 100);
        public ProjectileVisualFrame Frame(Sprite sprite)
        {
            if (Frames != null) foreach (var frame in Frames)
                if (frame != null && frame.Sprite == sprite) return frame;
            return null;
        }
        public Rect ClipRect(Sprite sprite)
        {
            Vector2 envelope = Vector2.zero;
            if (Frames != null) foreach (var frame in Frames)
                if (frame?.Sprite != null)
                    envelope = Vector2.Max(envelope, Vector2.Scale(frame.Sprite.rect.size, frame.VisibleRect.size));
            return new Rect(0, 0, envelope.x / sprite.rect.width, envelope.y / sprite.rect.height);
        }
    }

    [Serializable]
    public sealed class ProjectileVisualFrame
    {
        public Sprite Sprite;
        // Normalized visible bounds, using the common MinimumVisibleAlpha criterion.
        public Rect VisibleRect = new Rect(0, 0, 1, 1);
    }

    /// <summary>Pure sizing math shared by the live image and the inspector preview.</summary>
    public static class ProjectileVisualGeometry
    {
        public const float MaximumTiles = 2f;
        public const byte MinimumVisibleAlpha = 16;
        public static Vector2 Fit(Vector2 rectangle, Vector2 spriteSize, bool preserveAspect)
        {
            if (!preserveAspect || spriteSize.x <= 0 || spriteSize.y <= 0) return rectangle;
            float factor = Mathf.Min(rectangle.x / spriteSize.x, rectangle.y / spriteSize.y);
            return spriteSize * factor;
        }

        public static Vector2 Size(Vector2 legacyRect, Vector2 spriteSize, bool preserveAspect,
            Vector2 localScale, Rect visibleRect, float cellPixels, float percent)
        {
            float target = Mathf.Max(0, cellPixels) * MaximumTiles * ProjectileVisualSlot.Clamp(percent) / 100f;
            if (target == 0) return Vector2.zero;
            Vector2 fitted = Fit(legacyRect, spriteSize, preserveAspect);
            float major = Mathf.Max(Mathf.Abs(fitted.x * localScale.x) * visibleRect.width,
                Mathf.Abs(fitted.y * localScale.y) * visibleRect.height);
            // A fully transparent sprite has no visible dimension. No artificial visible minimum.
            return major > 0 ? legacyRect * (target / major) : Vector2.zero;
        }

        public static Rect AlphaBounds(Color32[] pixels, int width, int height, Rect spriteRect)
        {
            int left = Mathf.Clamp(Mathf.FloorToInt(spriteRect.x), 0, width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(spriteRect.y), 0, height);
            int right = Mathf.Clamp(Mathf.CeilToInt(spriteRect.xMax), left, width);
            int top = Mathf.Clamp(Mathf.CeilToInt(spriteRect.yMax), bottom, height);
            int minX = right, minY = top, maxX = left - 1, maxY = bottom - 1;
            for (int y = bottom; y < top; y++) for (int x = left; x < right; x++)
            {
                if (pixels[y * width + x].a < MinimumVisibleAlpha) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY || spriteRect.width <= 0 || spriteRect.height <= 0)
                return Rect.zero;
            return new Rect((minX - spriteRect.x) / spriteRect.width, (minY - spriteRect.y) / spriteRect.height,
                (maxX - minX + 1) / spriteRect.width, (maxY - minY + 1) / spriteRect.height);
        }
    }
}

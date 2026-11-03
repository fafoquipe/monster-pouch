using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Only the style selector uses this. Padding changes input bounds, never artwork.
    public sealed class StyleSelectorTouchTargets : MonoBehaviour
    {
        public const float MinimumPixels = 44;
        public const float MinimumTextPixels = 12;
        Canvas owner;
        float appliedScale = -1;
        readonly Vector3[] corners = new Vector3[4];
        Text[] labels;
        int[] originalFontSizes;
        Vector2[] originalLabelSizes;

        void LateUpdate()
        {
            if (owner == null) owner = GetComponentInParent<Canvas>();
            if (owner != null && owner.scaleFactor != appliedScale) Refresh();
        }

        public void InvalidateLabelMetrics() { labels = null; }

        public void Refresh()
        {
            owner = GetComponentInParent<Canvas>();
            if (owner == null) return;
            Canvas.ForceUpdateCanvases();
            if (labels == null)
            {
                labels = System.Array.FindAll(GetComponentsInChildren<Text>(), label => label.GetComponentInParent<StyleSelectorTouchTargets>() == this);
                originalFontSizes = new int[labels.Length]; originalLabelSizes = new Vector2[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                { originalFontSizes[i] = labels[i].fontSize; originalLabelSizes[i] = labels[i].rectTransform.sizeDelta; }
            }
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].fontSize = Mathf.Max(originalFontSizes[i], Mathf.CeilToInt(MinimumTextPixels / Mathf.Max(.001f, owner.scaleFactor)));
                var size = originalLabelSizes[i]; size.y = Mathf.Max(size.y, labels[i].preferredHeight);
                labels[i].rectTransform.sizeDelta = size;
            }
            var buttons = System.Array.FindAll(GetComponentsInChildren<Button>(), button => button.GetComponentInParent<StyleSelectorTouchTargets>() == this);
            var bounds = new Rect[buttons.Length];
            for (int i = 0; i < buttons.Length; i++) bounds[i] = ScreenBounds((RectTransform)buttons[i].transform);
            Rect safe = ScreenBounds((RectTransform)transform);
            for (int i = 0; i < buttons.Length; i++)
            {
                var hit = buttons[i].GetComponent<Image>();
                if (hit == null) continue;
                Rect local = hit.rectTransform.rect;
                var scale = new Vector2(bounds[i].width / local.width, bounds[i].height / local.height);
                hit.raycastPadding = CalculatePadding(bounds, i, safe, scale);
            }
            appliedScale = owner.scaleFactor;
        }

        Rect ScreenBounds(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        public static Vector4 CalculatePadding(Rect[] visualBounds, int index, Rect safe, Vector2 pixelsPerUnit)
        {
            if (pixelsPerUnit.x <= 0 || pixelsPerUnit.y <= 0) return Vector4.zero;
            Rect rect = visualBounds[index];
            float left = Mathf.Max(0, rect.xMin - safe.xMin), right = Mathf.Max(0, safe.xMax - rect.xMax);
            float bottom = Mathf.Max(0, rect.yMin - safe.yMin), top = Mathf.Max(0, safe.yMax - rect.yMax);
            for (int i = 0; i < visualBounds.Length; i++)
            {
                if (i == index) continue;
                Rect other = visualBounds[i];
                // Reserve one screen pixel between neighboring targets. Redistribute
                // unused padding to the other side instead of overlapping the next row.
                if (other.yMin < rect.yMax && other.yMax > rect.yMin)
                {
                    if (other.xMax <= rect.xMin) left = Mathf.Min(left, Mathf.Max(0, (rect.xMin - other.xMax - 1) * .5f));
                    if (other.xMin >= rect.xMax) right = Mathf.Min(right, Mathf.Max(0, (other.xMin - rect.xMax - 1) * .5f));
                }
                if (other.xMin < rect.xMax && other.xMax > rect.xMin)
                {
                    if (other.yMax <= rect.yMin) bottom = Mathf.Min(bottom, Mathf.Max(0, (rect.yMin - other.yMax - 1) * .5f));
                    if (other.yMin >= rect.yMax) top = Mathf.Min(top, Mathf.Max(0, (other.yMin - rect.yMax - 1) * .5f));
                }
            }
            Spread(Mathf.Max(0, MinimumPixels - rect.width), left, right, out left, out right);
            Spread(Mathf.Max(0, MinimumPixels - rect.height), bottom, top, out bottom, out top);
            return new Vector4(-left / pixelsPerUnit.x, -bottom / pixelsPerUnit.y, -right / pixelsPerUnit.x, -top / pixelsPerUnit.y);
        }

        static void Spread(float needed, float beforeSpace, float afterSpace, out float before, out float after)
        {
            before = Mathf.Min(needed * .5f, beforeSpace);
            after = Mathf.Min(needed - before, afterSpace);
            before = Mathf.Min(needed - after, beforeSpace);
        }
    }
}

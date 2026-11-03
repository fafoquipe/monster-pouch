using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        UnitHoldProgressRing unitHoldRing;
        PointerGesture visibleUnitHold;

        internal bool UnitHoldAllowed => isActiveAndEnabled && Match != null &&
            !MapTransitionActive && Match.Phase != MatchPhase.Menu && !Match.Paused && modal == null;

        internal void UpdateUnitHoldProgress(PointerGesture gesture, Vector2 screenPosition)
        {
            if (!UnitHoldAllowed || !gesture.ShowHoldFeedback(Time.unscaledTime))
            { HideUnitHoldProgress(gesture); return; }
            if (unitHoldRing == null)
            {
                var root = new GameObject("Unit hold progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnitHoldProgressRing));
                root.transform.SetParent(effects, false);
                unitHoldRing = root.GetComponent<UnitHoldProgressRing>();
                unitHoldRing.raycastTarget = false;
                unitHoldRing.color = new Color(1, 1, 1, .55f);
            }
            visibleUnitHold = gesture;
            // Estimate physical size from reported DPI, then convert screen pixels
            // to this Canvas. This is not a measurement of the touching finger.
            float scale = Mathf.Max(.01f, Mathf.Abs(CanvasRoot.lossyScale.x));
            float diameter = UnitHoldProgressRing.DiameterPixels(Screen.dpi, Mathf.Min(Screen.width, Screen.height)) / scale;
            Vector2 point = ToCanvas(screenPosition);
            SetRect(unitHoldRing.rectTransform, new Rect(point - Vector2.one * diameter * .5f, Vector2.one * diameter));
            unitHoldRing.Track(this, gesture);
            unitHoldRing.Remaining = 1 - gesture.HoldProgress(Time.unscaledTime);
            unitHoldRing.gameObject.SetActive(true);
            unitHoldRing.transform.SetAsLastSibling();
        }

        internal void HideUnitHoldProgress(PointerGesture gesture)
        {
            if (visibleUnitHold != gesture) return;
            HideAllUnitHoldProgress();
        }

        void HideAllUnitHoldProgress()
        {
            visibleUnitHold = null;
            if (unitHoldRing != null) unitHoldRing.gameObject.SetActive(false);
        }
    }

    /// <summary>Non-interactive translucent countdown arc with a clear center; its time belongs to PointerGesture.</summary>
    public sealed class UnitHoldProgressRing : MaskableGraphic
    {
        float remaining;
        LocalGameUI owner;
        PointerGesture gesture;
        public float Remaining
        {
            get => remaining;
            set { remaining = Mathf.Clamp01(value); SetVerticesDirty(); }
        }
        internal void Track(LocalGameUI ui, PointerGesture current) { owner = ui; gesture = current; }

        internal static float DiameterPixels(float dpi, float shortScreenSide)
        {
            float side = Mathf.Max(1, shortScreenSide);
            // 26 mm extends around a typical fingertip. Some devices report zero
            // or implausible DPI; use a stable screen-relative fallback in that case.
            bool validDpi = !float.IsNaN(dpi) && !float.IsInfinity(dpi) && dpi >= 80 && dpi <= 800;
            float pixels = validDpi ? dpi * (26f / 25.4f) : side * .34f;
            return Mathf.Clamp(pixels, Mathf.Min(112, side * .5f), side * .5f);
        }

        void Update()
        {
            if (owner != null && owner.UnitHoldAllowed) return;
            gesture?.Cancel();
            if (owner != null) owner.HideUnitHoldProgress(gesture);
            else gameObject.SetActive(false);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (remaining <= 0) return;
            Rect bounds = GetPixelAdjustedRect();
            float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            // Only the medium-width border counts down; the center stays transparent.
            AddArc(mesh, bounds.center, radius, remaining, color);
        }

        static void AddArc(VertexHelper mesh, Vector2 center, float radius, float fraction, Color tint)
        {
            float inner = radius * .8f;
            int segments = Mathf.Max(1, Mathf.CeilToInt(96 * fraction));
            for (int i = 0; i < segments; i++)
            {
                float first = Mathf.PI * .5f - Mathf.PI * 2 * fraction * i / segments;
                float last = Mathf.PI * .5f - Mathf.PI * 2 * fraction * (i + 1) / segments;
                Vector2 a = new Vector2(Mathf.Cos(first), Mathf.Sin(first));
                Vector2 b = new Vector2(Mathf.Cos(last), Mathf.Sin(last));
                int start = mesh.currentVertCount;
                mesh.AddVert(center + a * radius, tint, Vector2.zero);
                mesh.AddVert(center + b * radius, tint, Vector2.zero);
                mesh.AddVert(center + b * inner, tint, Vector2.zero);
                mesh.AddVert(center + a * inner, tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}

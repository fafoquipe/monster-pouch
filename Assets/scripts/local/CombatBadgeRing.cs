using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    /// <summary>Compact circular status medallion. Fraction is supplied by simulation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatBadgeRing : MaskableGraphic
    {
        Color tint;
        float fraction = 1, pulse;
        bool ready, shimmering, counter, whiteCore;
        public float RemainingFraction => fraction;
        public bool IsReady => ready;
        public bool Shimmers => shimmering;
        public void Set(Color nextTint, float nextFraction, bool nextReady = false, float clock = 0, bool shimmer = false, bool counterStyle = false, bool solidWhiteCore = false)
        {
            raycastTarget = false;
            float nextPulse = nextReady || shimmer ? .5f + .5f * Mathf.Sin(clock * 7) : 0;
            nextFraction = Mathf.Clamp01(nextFraction);
            if (tint == nextTint && fraction == nextFraction && ready == nextReady && pulse == nextPulse && shimmering == shimmer && counter == counterStyle && whiteCore == solidWhiteCore) return;
            tint = nextTint; fraction = nextFraction; ready = nextReady; pulse = nextPulse; shimmering = shimmer; counter = counterStyle; whiteCore = solidWhiteCore;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            if (ready || shimmering)
            {
                Disc(vh, center, radius + 2.4f + pulse, new Color(1, .78f, .2f, .12f + pulse * .13f));
                Disc(vh, center, radius + 1, new Color(1, .83f, .3f, .4f + pulse * .3f));
            }
            Disc(vh, center + Vector2.down * .7f, radius, new Color(.025f, .035f, .065f, .92f));
            Disc(vh, center, radius - .6f, counter ? new Color(.04f, .12f, .19f) : Color.Lerp(new Color(.08f, .12f, .2f), tint, .26f));
            Disc(vh, center, radius - 2.1f, counter ? new Color(.055f, .20f, .30f) : Color.Lerp(new Color(.06f, .1f, .16f), tint, ready ? .73f : .53f));
            Arc(vh, center, radius - .6f, radius - 1.8f, 1, new Color(tint.r, tint.g, tint.b, .38f));
            Arc(vh, center, radius - .6f, radius - 1.8f, fraction,
                counter ? new Color(.95f, .99f, 1) : ready ? new Color(1, .94f, .65f) : Color.Lerp(tint, Color.white, .65f));
            // Damage immunity has the user's exact flat-white center, without a
            // shaded rim or decorative detail competing with the gray countdown.
            if (whiteCore) Disc(vh, center, radius * .43f, Color.white);
        }
        static Vector2 Point(Vector2 c, float r, float f)
        { float a = (90 - f * 360) * Mathf.Deg2Rad; return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
        static void Disc(VertexHelper vh, Vector2 c, float radius, Color color)
        {
            const int steps = 48;
            int start = vh.currentVertCount; vh.AddVert(c, color, Vector2.zero);
            for (int i = 0; i <= steps; i++) vh.AddVert(Point(c, radius, i / (float)steps), color, Vector2.zero);
            for (int i = 0; i < steps; i++) vh.AddTriangle(start, start + i + 1, start + i + 2);
        }
        static void Arc(VertexHelper vh, Vector2 c, float outer, float inner, float fraction, Color color)
        {
            int steps = Mathf.CeilToInt(48 * fraction);
            for (int i = 0; i < steps; i++)
            {
                float from = i / 48f, to = Mathf.Min((i + 1) / 48f, fraction);
                int n = vh.currentVertCount;
                vh.AddVert(Point(c, outer, from), color, Vector2.zero); vh.AddVert(Point(c, outer, to), color, Vector2.zero);
                vh.AddVert(Point(c, inner, to), color, Vector2.zero); vh.AddVert(Point(c, inner, from), color, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}

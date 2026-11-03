using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Freestanding hotel sign; the cabinet and room remain separate artwork.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyCabinetCrest : MaskableGraphic
    {
        public const float DesignWidth = 404, DesignHeight = 128;

        protected override void Awake() { base.Awake(); raycastTarget = false; }

        public void AddLettering()
        {
            var script = Resources.Load<Font>("MonsterPouch/UI/Fonts/GrandHotel-Regular");
            var small = Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold");
            var name = Letter("Hotel name", "Grand Cas", new Rect(32, 37, 340, 66), 64,
                script, new Color32(255, 250, 230, 255));
            var shadow = name.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0, 0, 0, 210);
            shadow.effectDistance = new Vector2(0, -2);
            Letter("Hotel caption", "H O T E L", new Rect(66, 100, 272, 19), 14,
                small, new Color32(240, 222, 182, 255));
        }

        Text Letter(string objectName, string value, Rect bounds, int size, Font font, Color tint)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            rt.sizeDelta = bounds.size;
            var label = go.GetComponent<Text>();
            label.text = value; label.font = font; label.fontSize = size; label.color = tint;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect area = rectTransform.rect;
            Color ink = new Color32(23, 22, 25, 255), edge = new Color32(60, 52, 45, 255);
            Color brass = new Color32(171, 139, 87, 255), rim = new Color32(235, 215, 165, 255);
            Color face = new Color32(38, 39, 38, 255), inner = new Color32(65, 61, 51, 255);
            Color red = new Color32(163, 35, 48, 255), ruby = new Color32(215, 61, 65, 255);
            Color white = new Color32(255, 247, 218, 255);

            Vector2 Point(float x, float y) => new Vector2(
                area.xMin + x / DesignWidth * area.width,
                area.yMax - y / DesignHeight * area.height);
            void Box(float x, float y, float w, float h, Color tint)
            {
                int first = mesh.currentVertCount;
                mesh.AddVert(Point(x, y + h), tint, Vector2.zero);
                mesh.AddVert(Point(x, y), tint, Vector2.zero);
                mesh.AddVert(Point(x + w, y), tint, Vector2.zero);
                mesh.AddVert(Point(x + w, y + h), tint, Vector2.zero);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first, first + 2, first + 3);
            }
            void Fan(Color tint, params Vector2[] vertices)
            {
                int first = mesh.currentVertCount;
                Vector2 center = Vector2.zero;
                foreach (var v in vertices) center += v;
                center /= vertices.Length;
                mesh.AddVert(Point(center.x, center.y), tint, Vector2.zero);
                foreach (var v in vertices) mesh.AddVert(Point(v.x, v.y), tint, Vector2.zero);
                for (int i = 0; i < vertices.Length; i++)
                    mesh.AddTriangle(first, first + i + 1, first + (i + 1) % vertices.Length + 1);
            }
            void Plaque(float inset, float drop, Color tint)
            {
                // Stepped shallow arch, with a beveled bottom edge.
                Vector2 Inset(float x, float y) => new Vector2(
                    202 + (x - 202) * (396 - 2 * inset) / 396,
                    76 + (y - 76) * (100 - 2 * inset) / 100 + drop);
                Fan(tint, Inset(4, 50), Inset(10, 44), Inset(76, 34), Inset(142, 26),
                    Inset(262, 26), Inset(328, 34), Inset(394, 44), Inset(400, 50),
                    Inset(400, 120), Inset(394, 126), Inset(10, 126), Inset(4, 120));
            }

            Plaque(0, 2, ink);
            Plaque(1, 0, edge);
            Plaque(3, 0, brass);
            Plaque(4, 0, rim);
            Plaque(6, 0, ink);
            Plaque(8, 0, inner);
            Plaque(9, 0, face);
            // Symmetric brass hairlines frame the caption.
            Box(28, 109, 27, 1, brass); Box(349, 109, 27, 1, brass);

            // Original red jewel emblem, inset into the arch.
            Fan(ink, new Vector2(202, 0), new Vector2(224, 7), new Vector2(232, 25),
                new Vector2(224, 43), new Vector2(202, 50), new Vector2(180, 43),
                new Vector2(172, 25), new Vector2(180, 7));
            Fan(brass, new Vector2(202, 3), new Vector2(222, 10), new Vector2(228, 25),
                new Vector2(222, 40), new Vector2(202, 47), new Vector2(182, 40),
                new Vector2(176, 25), new Vector2(182, 10));
            Fan(rim, new Vector2(202, 5), new Vector2(221, 12), new Vector2(226, 25),
                new Vector2(221, 39), new Vector2(202, 45), new Vector2(183, 39),
                new Vector2(178, 25), new Vector2(183, 12));
            Fan(red, new Vector2(202, 7), new Vector2(218, 13), new Vector2(224, 25),
                new Vector2(218, 37), new Vector2(202, 43), new Vector2(186, 37),
                new Vector2(180, 25), new Vector2(186, 13));
            Fan(ruby, new Vector2(202, 9), new Vector2(216, 14), new Vector2(220, 24),
                new Vector2(185, 24), new Vector2(189, 15));
            // Four-point diamond light; deliberately distinct from the reference logo.
            Fan(white, new Vector2(202, 12), new Vector2(206, 21), new Vector2(216, 25),
                new Vector2(206, 29), new Vector2(202, 39), new Vector2(198, 29),
                new Vector2(188, 25), new Vector2(198, 21));
            Box(201, 19, 2, 12, new Color32(255, 255, 247, 255));
        }
    }
}

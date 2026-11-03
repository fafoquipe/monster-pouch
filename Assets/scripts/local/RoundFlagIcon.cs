using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    /// <summary>A small, texture-free pixel flag shared by the HUD and round results.</summary>
    [AddComponentMenu("Monster Pouch/UI/Round Flag")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundFlagIcon : MaskableGraphic
    {
        public const int PixelWidth = 14;
        public const int PixelHeight = 18;
        [SerializeField] bool won;

        // O = outline, P/p = staff, C/H/S = cloth, highlight and fold.
        // Every cell remains a hard rectangle, including when the stamp is scaled.
        static readonly string[] Pixels =
        {
            "..............",
            ".OOO..........",
            ".OpOOOOOO.....",
            ".OpPHHHHCOOO..",
            ".OpPCCCCCHHCO.",
            ".OpPCCCCCCCCO.",
            ".OpPSSCCCCCO..",
            ".OpPOOSSCCO...",
            ".OpO..OCCCO...",
            ".OpO...OCCCO..",
            ".OpO....OOOOO.",
            ".OpO..........",
            ".OpO..........",
            ".OpO..........",
            ".OpO..........",
            ".OpO..........",
            "OOOOOO........",
            ".OOOO........."
        };

        public bool Won
        {
            get => won;
            set => SetWon(value);
        }

        public void SetWon(bool value)
        {
            if (won == value) return;
            won = value;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            float cell = Mathf.Min(bounds.width / PixelWidth, bounds.height / PixelHeight);
            if (cell <= 0) return;
            float left = bounds.xMin + (bounds.width - PixelWidth * cell) * .5f;
            float top = bounds.yMax - (bounds.height - PixelHeight * cell) * .5f;
            for (int row = 0; row < PixelHeight; row++)
            {
                // Merge adjacent matching cells without softening the silhouette.
                for (int x = 0; x < PixelWidth;)
                {
                    char token = Pixels[row][x];
                    int end = x + 1;
                    while (end < PixelWidth && Pixels[row][end] == token) end++;
                    if (token != '.')
                    {
                        Color tint = Palette(token) * color;
                        AddQuad(vh, left + x * cell, top - (row + 1) * cell,
                            (end - x) * cell, cell, tint);
                    }
                    x = end;
                }
            }
        }

        Color Palette(char token)
        {
            switch (token)
            {
                case 'O': return new Color32(25, 26, 34, 255);
                case 'P': return won ? new Color32(180, 123, 40, 255) : new Color32(90, 76, 73, 255);
                case 'p': return won ? new Color32(250, 212, 106, 255) : new Color32(142, 124, 119, 255);
                case 'H': return won ? new Color32(255, 234, 145, 255) : new Color32(142, 97, 104, 255);
                case 'S': return won ? new Color32(181, 109, 30, 255) : new Color32(72, 49, 58, 255);
                default: return won ? new Color32(245, 188, 54, 255) : new Color32(104, 64, 77, 255);
            }
        }

        static void AddQuad(VertexHelper vh, float x, float y, float width, float height, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y), tint, Vector2.zero);
            vh.AddVert(new Vector3(x, y + height), tint, Vector2.zero);
            vh.AddVert(new Vector3(x + width, y + height), tint, Vector2.zero);
            vh.AddVert(new Vector3(x + width, y), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}

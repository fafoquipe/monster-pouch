namespace MonsterPouch.Local
{
    // Existing registered art: all-alpha union [47,172,463,454], pivot (256,424),
    // canvas/PPU 512. Fit the whole union, including faint edge alpha, to one cell.
    public readonly struct RootVinesLayout
    {
        public const float NormalizedVisibleWidth = 416f / 512;
        public const float NormalizedVisibleHeight = 282f / 512;
        public const float LeftExtent = 209f / 512, RightExtent = 207f / 512;
        public const float TopExtent = 252f / 512, BottomExtent = 30f / 512;
        public readonly float ScaleX, ScaleY;
        public float WorldWidth => NormalizedVisibleWidth * ScaleX;
        public float WorldHeight => NormalizedVisibleHeight * ScaleY;
        public float AnchorOffsetX => (LeftExtent - RightExtent) * ScaleX * .5f;
        public float AnchorOffsetY => (BottomExtent - TopExtent) * ScaleY * .5f;

        RootVinesLayout(float x, float y) { ScaleX = x; ScaleY = y; }

        // The enclosing vines follow the rectangular board cell, never body size,
        // weapon bounds, animation lift or the HUD/head anchor. Front/back use the
        // same transform; the measured pivot correction centers their combined ink.
        public static bool TryFitCell(float cellWidth, float cellHeight, out RootVinesLayout layout)
        {
            layout = default;
            if (!PositiveFinite(cellWidth) || !PositiveFinite(cellHeight)) return false;
            float x = cellWidth / NormalizedVisibleWidth;
            float y = cellHeight / NormalizedVisibleHeight;
            if (!PositiveFinite(x) || !PositiveFinite(y)) return false;
            layout = new RootVinesLayout(x, y);
            return true;
        }

        // Retain existing callers; valid body calibration no longer shrinks the cell.
        public static bool TryFit(float bodyWidth, float bodyHeight,
            float cellWidth, float cellHeight, out RootVinesLayout layout)
        {
            layout = default;
            return PositiveFinite(bodyWidth) && PositiveFinite(bodyHeight) &&
                TryFitCell(cellWidth, cellHeight, out layout);
        }
        static bool PositiveFinite(float value) => value > 0 &&
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

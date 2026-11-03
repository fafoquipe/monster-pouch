using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    // Zero is deliberately legacy: deserializing old assets never authors a new footprint.
    public enum AbilityAreaShape { Original = 0, Square = 1, Cross = 2, Diamond = 3 }

    [Serializable]
    public sealed class AbilityAreaDefinition
    {
        public AbilityAreaShape Shape;
        public int SquareSize;
        public int DiamondRadius;
        public bool IncludeCenter;
        public int UpWidth = 1, UpDepth;
        public int RightWidth = 1, RightDepth;
        public int DownWidth = 1, DownDepth;
        public int LeftWidth = 1, LeftDepth;
        public bool IsEdited => Shape == AbilityAreaShape.Square || Shape == AbilityAreaShape.Cross || Shape == AbilityAreaShape.Diamond;
        public AbilityAreaDefinition Copy() => (AbilityAreaDefinition)MemberwiseClone();
    }

    public readonly struct AbilityAreaCell : IEquatable<AbilityAreaCell>
    {
        public readonly int X, Y;
        public AbilityAreaCell(int x, int y) { X = x; Y = y; }
        public bool Equals(AbilityAreaCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is AbilityAreaCell cell && Equals(cell);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => X + "," + Y;
    }

    /// <summary>Integer board geometry shared by combat and the inspector. Up is logical +Y;
    /// right is +X. Teams, targets, transforms and camera flips never rotate this geometry.</summary>
    public static class AbilityAreaResolver
    {
        public static bool Contains(AbilityAreaDefinition area, int offsetX, int offsetY) =>
            ContainsOffset(area, offsetX, offsetY);

        private static bool ContainsOffset(AbilityAreaDefinition area, long offsetX, long offsetY)
        {
            if (area == null || !area.IsEdited) return false;
            if (area.Shape == AbilityAreaShape.Square)
            {
                int n = area.SquareSize;
                if (n <= 0) return false;
                // Even squares have their extra row/column at logical up/right.
                int low = -((n - 1) / 2), high = n / 2;
                return offsetX >= low && offsetX <= high && offsetY >= low && offsetY <= high;
            }
            if (area.Shape == AbilityAreaShape.Diamond)
                return area.DiamondRadius >= 0 && Math.Abs(offsetX) + Math.Abs(offsetY) <= area.DiamondRadius;
            if (offsetX == 0 && offsetY == 0) return area.IncludeCenter;
            return Arm(offsetX, offsetY, area.UpWidth, area.UpDepth) ||
                Arm(offsetY, offsetX, area.RightWidth, area.RightDepth) ||
                Arm(offsetX, -offsetY, area.DownWidth, area.DownDepth) ||
                Arm(offsetY, -offsetX, area.LeftWidth, area.LeftDepth);
        }
        static bool Arm(long transverse, long longitudinal, int width, int depth) =>
            width > 0 && (width & 1) == 1 && depth > 0 && longitudinal >= 1 && longitudinal <= depth &&
            transverse >= -(width / 2) && transverse <= width / 2;

        public static AbilityAreaCell[] Resolve(AbilityAreaDefinition area, int originX, int originY, int boardWidth, int boardHeight)
        {
            var result = new List<AbilityAreaCell>();
            if (area == null || !area.IsEdited) return result.ToArray();
            // Iterate actual board cells: bounded work, clipping and unique union by construction.
            for (int y = 0; y < boardHeight; y++)
                for (int x = 0; x < boardWidth; x++)
                    if (ContainsOffset(area, (long)x - originX, (long)y - originY)) result.Add(new AbilityAreaCell(x, y));
            return result.ToArray();
        }
    }
}

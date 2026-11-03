using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // Deployed cells only: reservations and subsequent combat movement cannot change the mark.
    public static class TaurisMarkGeometry
    {
        public const int DefaultRange = 5;

        public static int NormalizeRange(float range)
        {
            if (float.IsNaN(range) || float.IsInfinity(range)) return DefaultRange;
            return Mathf.RoundToInt(Mathf.Clamp(range, 0, BoardManager.Height - 1));
        }

        public static bool Contains(BoardCell origin, BoardCell target, BoardSide side, float range)
        {
            if (origin == null || target == null || origin.X != target.X) return false;
            int forwardDistance = (target.Y - origin.Y) * (side == BoardSide.Blue ? -1 : 1);
            return forwardDistance > 0 && forwardDistance <= NormalizeRange(range);
        }
    }
}

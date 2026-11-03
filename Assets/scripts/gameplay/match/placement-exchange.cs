using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>The two pieces and their original positions before a preparation exchange.</summary>
    public sealed class PlacementExchange
    {
        public OwnedUnit First { get; }
        public OwnedUnit Second { get; }
        public UnitLocation FirstLocation { get; }
        public UnitLocation SecondLocation { get; }
        public Vector2Int FirstCell { get; }
        public Vector2Int SecondCell { get; }
        public int FirstBriefSlot { get; }
        public int SecondBriefSlot { get; }

        internal PlacementExchange(OwnedUnit first, OwnedUnit second)
        {
            First = first; Second = second;
            FirstLocation = first.Location; SecondLocation = second.Location;
            FirstCell = first.Deployment; SecondCell = second.Deployment;
            FirstBriefSlot = first.BriefSlot; SecondBriefSlot = second.BriefSlot;
        }
    }
}

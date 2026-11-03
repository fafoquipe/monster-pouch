using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    /// <summary>Read-only render geometry. Status lifetime and eligibility remain in the simulation.</summary>
    public readonly struct UnitPresentationAnchors
    {
        public readonly BattleUnit Unit;
        public readonly int UnitResetVersion;
        public readonly BoardCell OccupiedCell;
        public readonly Vector3 GroundWorldPosition, FootPivotWorldPosition, HeadWorldPosition;
        public readonly Bounds OccupiedCellWorldBounds;
        public readonly Vector2 ReferenceBodyWorldSize;
        public readonly bool HasCalibratedBodyReference;
        public readonly UnitFacing Facing;
        public readonly int BodySortingLayerId, BodySortingOrder;

        public UnitPresentationAnchors(BattleUnit unit, Vector3 ground, Vector3 foot, Vector3 head,
            Bounds cellBounds, Vector2 referenceSize, bool calibrated, UnitFacing facing,
            int sortingLayerId, int sortingOrder)
        {
            Unit = unit; UnitResetVersion = unit.CombatResetVersion; OccupiedCell = unit.CurrentCell;
            GroundWorldPosition = ground; FootPivotWorldPosition = foot; HeadWorldPosition = head;
            OccupiedCellWorldBounds = cellBounds; ReferenceBodyWorldSize = referenceSize;
            HasCalibratedBodyReference = calibrated; Facing = facing;
            BodySortingLayerId = sortingLayerId; BodySortingOrder = sortingOrder;
        }
    }

    public sealed partial class UnitPresentation
    {
        public bool TryGetPresentationAnchors(out UnitPresentationAnchors anchors)
        {
            anchors = default;
            if (unit == null || art == null || visualRoot == null || body == null || mapper == null ||
                !mapper.TryGetWorldPosition(unit.CurrentCell, out var cellCenter)) return false;
            Vector3 ground = displacementActive ? displacementGroundWorldPosition :
                transform.position - Vector3.up * groundLift;
            Vector3 parentScale = transform.lossyScale;
            // AuthoredWorldWidth is the measured body width, not current weapon/swing bounds.
            float width = art.AuthoredWorldWidth * art.GetInGameScale(Mathf.Abs(mapper.CellSize.x)) * Mathf.Abs(parentScale.x);
            // The calibrated feet-to-head point excludes weapon/particle padding and animation squash.
            float height = art.VitalsHeadAnchor.y * Mathf.Abs(baseScale * parentScale.y);
            bool calibrated = width > 0 && height > 0 && !float.IsNaN(width) && !float.IsInfinity(width) &&
                !float.IsNaN(height) && !float.IsInfinity(height);
            if (!calibrated) { width = 0; height = 0; } // Never substitute attack/weapon bounds.
            var cellBounds = new Bounds(cellCenter, new Vector3(Mathf.Abs(mapper.CellSize.x), Mathf.Abs(mapper.CellSize.y), 0));
            anchors = new UnitPresentationAnchors(unit, ground, visualRoot.position, VitalsHeadWorldPosition,
                cellBounds, new Vector2(width, height), calibrated, facing, body.sortingLayerID, body.sortingOrder);
            return true;
        }
    }
}

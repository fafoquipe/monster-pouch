using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    public sealed partial class UnitPresentation
    {
        private BoardCell reservedMoveOrigin, reservedMoveDestination;
        private int reservedMoveResetVersion;

        private void ClearReservedMovementTracking()
        {
            reservedMoveOrigin = reservedMoveDestination = null;
        }

        private void TrackReservedMovement()
        {
            ClearReservedMovementTracking();
            if (unit == null || flightMotion || pullMotion || dashMotion || unit.ReservedCell == null) return;
            reservedMoveOrigin = unit.CurrentCell;
            reservedMoveDestination = unit.ReservedCell;
            reservedMoveResetVersion = unit.CombatResetVersion;
        }

        private void ValidateReservedMovement()
        {
            if (reservedMoveDestination == null) return;
            if (unit != null && unit.IsAlive && unit.isActiveAndEnabled &&
                unit.CombatResetVersion == reservedMoveResetVersion)
            {
                if (ReferenceEquals(unit.CurrentCell, reservedMoveDestination) &&
                    ReferenceEquals(reservedMoveDestination.OccupiedBy, unit))
                {
                    ClearReservedMovementTracking();
                    if (!moving) SnapToCell();
                    return;
                }
                if (ReferenceEquals(unit.CurrentCell, reservedMoveOrigin) &&
                    ReferenceEquals(unit.ReservedCell, reservedMoveDestination) &&
                    ReferenceEquals(reservedMoveDestination.ReservedBy, unit)) return;
            }
            // Reset, death, disable, teleport or cancelled reservation: stop the old route.
            SnapToCell();
        }

        private void OnDisable()
        {
            ClearDisplacementPresentation();
            ClearBoundActionPresentation(true);
            if (reservedMoveDestination == null) return;
            moving = false;
            groundLift = 0;
            if (unit != null && mapper != null && unit.CurrentCell != null &&
                mapper.TryGetWorldPosition(unit.CurrentCell, out Vector3 position)) transform.position = position;
            if (visualRoot != null) ResetPose();
            // Retain tracking so re-enabling also observes arrival/cancellation while hidden.
        }
    }
}

using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    // A navigation reservation is not occupancy. Keep the last reached cell until
    // the fixed simulation clock reaches arrival, independently of any renderer.
    internal sealed class CombatMovementStep
    {
        public BattleUnit Unit { get; }
        public BoardCell Origin { get; }
        public BoardCell Destination { get; }
        public int ArrivalTick { get; }
        public bool Arrived { get; private set; }
        private readonly int resetVersion;

        public CombatMovementStep(BattleUnit unit, int arrivalTick)
        {
            Unit = unit;
            Origin = unit.CurrentCell;
            Destination = unit.ReservedCell;
            ArrivalTick = arrivalTick;
            resetVersion = unit.CombatResetVersion;
        }

        public bool IsValid(BoardManager board) =>
            Unit != null && Unit.IsAlive && Unit.isActiveAndEnabled && !Unit.IsReviving &&
            Unit.CombatResetVersion == resetVersion && board.IsManagedCell(Origin) &&
            board.IsManagedCell(Destination) && ReferenceEquals(Unit.CurrentCell, Origin) &&
            ReferenceEquals(Origin.OccupiedBy, Unit) && ReferenceEquals(Unit.ReservedCell, Destination) &&
            ReferenceEquals(Destination.ReservedBy, Unit);

        // Returns true when the step has ended, either by arrival or cancellation.
        public bool Advance(BoardManager board, int tick)
        {
            if (!IsValid(board)) { Cancel(board); return true; }
            if (tick < ArrivalTick) return false;
            Arrived = board.ConfirmMove(Unit);
            if (!Arrived) Cancel(board);
            return true;
        }

        public void Cancel(BoardManager board)
        {
            // A teleport or replacement movement may already own a different reservation.
            if (Unit != null && ReferenceEquals(Unit.ReservedCell, Destination))
                board.CancelReservation(Unit);
        }
    }
}

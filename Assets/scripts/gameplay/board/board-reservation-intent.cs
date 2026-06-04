namespace MonsterPouch.Gameplay.Board
{
    public sealed class BoardReservationIntent
    {
        public IBoardUnit Unit { get; }
        public BoardCell OriginCell { get; }
        public BoardCell DestinationCell { get; }

        public BoardReservationIntent(IBoardUnit unit, BoardCell destinationCell)
        {
            Unit = unit;
            OriginCell = unit?.CurrentCell;
            DestinationCell = destinationCell;
        }
    }
}

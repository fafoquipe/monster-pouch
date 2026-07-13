namespace MonsterPouch.Gameplay.Board
{
    public readonly struct BoardMovementResult
    {
        public IBoardUnit Unit { get; }
        public BoardCell TargetCell { get; }
        public BoardCell NextCell { get; }
        public BoardMovementStatus Status { get; }

        public BoardMovementResult(
            IBoardUnit unit,
            BoardCell targetCell,
            BoardCell nextCell,
            BoardMovementStatus status)
        {
            Unit = unit;
            TargetCell = targetCell;
            NextCell = nextCell;
            Status = status;
        }
    }
}

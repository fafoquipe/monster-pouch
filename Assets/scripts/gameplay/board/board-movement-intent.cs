namespace MonsterPouch.Gameplay.Board
{
    public readonly struct BoardMovementIntent
    {
        public IBoardUnit Unit { get; }
        public BoardCell TargetCell { get; }

        public BoardMovementIntent(IBoardUnit unit, BoardCell targetCell)
        {
            Unit = unit;
            TargetCell = targetCell;
        }
    }
}

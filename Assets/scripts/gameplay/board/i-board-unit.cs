namespace MonsterPouch.Gameplay.Board
{
    public interface IBoardUnit
    {
        string UnitId { get; }
        BoardSide Side { get; }
        int IQSpeed { get; }
        BoardCell CurrentCell { get; }
        BoardCell ReservedCell { get; }

        void SetCurrentCell(BoardCell cell);
        void SetReservedCell(BoardCell cell);
        void ClearReservedCell();
        void ClearBoardState();
    }
}

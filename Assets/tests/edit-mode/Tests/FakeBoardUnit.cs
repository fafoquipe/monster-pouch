using MonsterPouch.Gameplay.Board;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    internal sealed class FakeBoardUnit : IBoardUnit
    {
        public string UnitId { get; }
        public BoardSide Side { get; }
        public int IQSpeed { get; }
        public BoardCell CurrentCell { get; private set; }
        public BoardCell ReservedCell { get; private set; }

        public FakeBoardUnit(string unitId, BoardSide side, int iqSpeed)
        {
            UnitId = unitId;
            Side = side;
            IQSpeed = iqSpeed;
        }

        public void SetCurrentCell(BoardCell cell)
        {
            CurrentCell = cell;
        }

        public void SetReservedCell(BoardCell cell)
        {
            ReservedCell = cell;
        }

        public void ClearReservedCell()
        {
            ReservedCell = null;
        }

        public void ClearBoardState()
        {
            CurrentCell = null;
            ReservedCell = null;
        }
    }
}

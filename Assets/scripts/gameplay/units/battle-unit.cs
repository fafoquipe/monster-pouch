using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Units
{
    [DisallowMultipleComponent]
    public abstract class BattleUnit : MonoBehaviour, IBoardUnit
    {
        [SerializeField] private string unitId;
        [SerializeField] private BoardSide side;
        [SerializeField] private UnitStats baseStats = new UnitStats();

        public abstract UnitCategory Category { get; }

        public string UnitId => unitId;
        public BoardSide Side => side;
        public int IQSpeed => baseStats.IQSpeed;
        public UnitStats BaseStats => baseStats;
        public UnitState State { get; private set; } = UnitState.Idle;

        public BoardCell CurrentCell { get; private set; }
        public BoardCell ReservedCell { get; private set; }

        public void ConfigureSide(BoardSide newSide)
        {
            side = newSide;
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

        public void SetState(UnitState state)
        {
            State = state;
        }
    }
}

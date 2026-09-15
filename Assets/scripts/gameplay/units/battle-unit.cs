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
        private BoardManager owningBoard;

        public abstract UnitCategory Category { get; }

        public string UnitId => unitId;
        public BoardSide Side => side;
        public int IQSpeed => baseStats.IQSpeed;
        public UnitStats BaseStats => baseStats;
        public UnitState State { get; private set; } = UnitState.Idle;
        public int CurrentHealth { get; private set; } = 1;
        public bool IsAlive => CurrentHealth > 0 && State != UnitState.Dead;
        public bool IsReviving { get; private set; }
        public bool IsCombatSummon { get; private set; }
        public float Energy { get; private set; }
        public bool IsEnergyLocked { get; internal set; }
        public int Shield { get; private set; }
        public bool IsStunned { get; internal set; }
        public bool IsRooted { get; internal set; }
        public bool IsInvisible { get; internal set; }
        public bool IsSleeping { get; internal set; }
        internal int CombatResetVersion { get; private set; }

        public BoardCell CurrentCell { get; private set; }
        public BoardCell ReservedCell { get; private set; }

        public void Initialize(string id, BoardSide newSide, UnitStats stats, bool isCombatSummon = false)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new System.ArgumentException("A battle unit requires a unique ID.", nameof(id));
            unitId = id;
            side = newSide;
            baseStats = stats ?? new UnitStats();
            IsCombatSummon = isCombatSummon;
            ResetForCombat();
        }

        public void ResetForCombat()
        {
            unchecked { CombatResetVersion++; }
            CurrentHealth = baseStats.MaxHealth;
            State = UnitState.Idle;
            IsReviving = false;
            Energy = 0; Shield = 0; IsEnergyLocked = false;
            IsStunned = IsRooted = IsInvisible = IsSleeping = false;
        }

        internal void BeginReviving()
        {
            CurrentHealth = 0;
            State = UnitState.Dead;
            IsReviving = true;
        }

        internal void CompleteRevival(float healthFraction = -1)
        {
            CurrentHealth = Mathf.Clamp(Mathf.CeilToInt(baseStats.MaxHealth * (healthFraction > 0 ? healthFraction : baseStats.ReviveHealthFraction)), 1, baseStats.MaxHealth);
            State = UnitState.Idle;
            IsReviving = false;
        }

        internal void CancelRevival() { IsReviving = false; }

        public float AddEnergy(float amount)
        {
            if (IsEnergyLocked && amount > 0) return 0;
            float before = Energy;
            Energy = Mathf.Clamp(Energy + amount, 0, Mathf.Max(0, baseStats.EnergyMax));
            return Energy - before;
        }
        internal void ClearEnergy() { Energy = 0; }
        internal void AddShield(int amount) { Shield += Mathf.Max(0, amount); }
        internal int AbsorbShield(int damage)
        {
            int absorbed = Mathf.Min(Shield, Mathf.Max(0, damage)); Shield -= absorbed;
            return Mathf.Max(0, damage - absorbed);
        }

        public int ApplyDamage(int amount)
        {
            int applied = Mathf.Min(CurrentHealth, Mathf.Max(0, amount));
            CurrentHealth -= applied;
            if (CurrentHealth == 0) State = UnitState.Dead;
            return applied;
        }

        public int Heal(int amount)
        {
            if (!IsAlive) return 0;
            int applied = Mathf.Min(baseStats.MaxHealth - CurrentHealth, Mathf.Max(0, amount));
            CurrentHealth += applied;
            return applied;
        }

        internal void BindBoard(BoardManager board) { owningBoard = board; }

        private void OnDestroy()
        {
            if (owningBoard != null) owningBoard.ReleaseUnit(this);
        }

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

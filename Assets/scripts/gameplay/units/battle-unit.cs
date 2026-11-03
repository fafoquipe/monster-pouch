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
        public int RoundBaseHealth { get; private set; }
        public int RoundBaseDamage { get; private set; }
        public float RoundBaseAttacksPerSecond { get; private set; }
        internal float AbilityAnimationDuration { get; private set; } = .65f;
        public UnitState State { get; private set; } = UnitState.Idle;
        public int CurrentHealth { get; private set; } = 1;
        private decimal preciseHealth = 1m;
        public decimal PreciseHealth => baseStats.UsesEronyaRules ? preciseHealth : CurrentHealth;
        internal int CombatTeamSlot { get; set; } = int.MaxValue;
        public bool IsAlive => PreciseHealth > 0m && State != UnitState.Dead;
        internal void SetPreciseCombatHealth(decimal value)
        {
            if (!baseStats.UsesEronyaRules) throw new System.InvalidOperationException("Precise health requires Eronya rules.");
            if (IsRoundFrozen || !IsAlive) return;
            preciseHealth = System.Math.Max(0m,System.Math.Min(baseStats.MaxHealth,value));
            CurrentHealth = MonsterPouch.Gameplay.Match.EronyaMechanics.Display(preciseHealth);
            if (preciseHealth == 0m) { State=UnitState.Dead;ClearFishShield();EndPull();IsTaunted=false;ClearTimedSuperState(); }
        }
        public bool IsReviving { get; private set; }
        public bool IsCombatSummon { get; private set; }
        // Only explicitly created practice actors opt in; normal units retain
        // their exact behavior and combat balance.
        public bool IsPassiveTrainingTarget { get; internal set; }
        internal float TrainingEffectMultiplier { get; set; } = 1f;
        public bool IsRoundFrozen { get; internal set; }
        public float Energy { get; private set; }
        public bool IsEnergyLocked { get; internal set; }
        // Independent of timed Super state: opening recovery must also block gifts and damage energy.
        public bool IsOpeningEnergyBlocked { get; internal set; }
        public bool IsReflecting { get; internal set; }
        public bool IsProtected { get; internal set; }
        sealed class ShieldLayer
        {
            public decimal Amount; public int Capacity, Until, SourceVersion;
            public bool Fish;
            public BattleUnit Source;
        }
        readonly System.Collections.Generic.List<ShieldLayer> shields = new System.Collections.Generic.List<ShieldLayer>();
        public int FishShield => ShieldAmount(true);
        public int FishShieldCapacity { get { int total=0; foreach(var layer in shields)if(layer.Fish)total+=layer.Capacity; return total; } }
        internal int EronyaShieldDisplay;
        internal int LegacyShield => ShieldAmount(false);
        internal bool HasLegacyShield => shields.Exists(layer=>layer.Amount>0m);
        public int Shield => LegacyShield + EronyaShieldDisplay;
        internal int ShieldExpiresAt => shields.Count > 0 ? shields[0].Until : 0;
        internal int FishShieldExpiresAt { get { foreach(var layer in shields)if(layer.Fish)return layer.Until; return 0; } }
        int ShieldAmount(bool fishOnly) { decimal total=0; foreach(var layer in shields)if(!fishOnly||layer.Fish)total+=layer.Amount; return MonsterPouch.Gameplay.Match.EronyaMechanics.Display(total); }
        public bool IsStunned { get; internal set; }
        public bool IsPopowStunned { get; internal set; }
        public bool IsElectrocuted { get; internal set; }
        public string SlowEffectId { get; internal set; }
        public bool IsRooted { get; internal set; }
        public bool IsInvisible { get; internal set; }
        public bool IsSleeping { get; internal set; }
        public bool IsSlowed { get; internal set; }
        public bool IsTaunted { get; internal set; }
        public bool IsFlying => FlightRemaining > 0;
        public float FlightDuration { get; private set; }
        public float FlightRemaining { get; private set; }
        internal void BeginFlight(float duration) { FlightDuration=FlightRemaining=Mathf.Max(.1f,duration); }
        internal void AdvanceFlight(float delta) { FlightRemaining=Mathf.Max(0,FlightRemaining-delta); if(FlightRemaining<.0001f)EndFlight(); }
        internal void EndFlight() { FlightRemaining=0; }
        public bool IsBeingPulled => PullRemaining > 0;
        public float PullDuration { get; private set; }
        public float PullRemaining { get; private set; }
        internal void BeginPull(float duration) { PullDuration=PullRemaining=Mathf.Max(.1f,duration); }
        internal void AdvancePull(float delta) { PullRemaining=Mathf.Max(0,PullRemaining-delta); if(PullRemaining<.0001f)EndPull(); }
        internal void EndPull() { PullRemaining=0; }
        public int CombatResetVersion { get; private set; }
        public int AttackResetVersion { get; private set; }
        internal int HeadbuttImpactVersion { get; set; }
        // Per-action timing, shared by simulation, sprite pose and projectile release.
        // Negative uses the configured preparation; zero resumes an already-running cycle.
        internal float AttackWindupOverride { get; set; } = -1;
        internal string ActionAbilityEffect { get; set; }
        internal void InterruptAttack() { ActionAbilityEffect=null; AttackWindupOverride = -1; unchecked { AttackResetVersion++; } }
        internal void SetAbilityAnimationDuration(float duration)
        {
            AbilityAnimationDuration = float.IsNaN(duration) || float.IsInfinity(duration) ? .65f : Mathf.Max(.1f, duration);
        }

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
            InterruptAttack();
            RoundBaseHealth = baseStats.MaxHealth;
            RoundBaseDamage = baseStats.Attack;
            RoundBaseAttacksPerSecond = baseStats.AttackSpeed;
            CurrentHealth = baseStats.MaxHealth;
            preciseHealth = baseStats.MaxHealth;
            State = UnitState.Idle;
            IsReviving = false;
            IsRoundFrozen = false;
            Energy = 0; shields.Clear(); EronyaShieldDisplay=0; IsEnergyLocked = IsOpeningEnergyBlocked = false;
            EndFlight();
            EndPull();
            IsReflecting = IsProtected = false;
            IsStunned = IsRooted = IsInvisible = IsSleeping = IsSlowed = IsTaunted = false;
            IsPopowStunned=IsElectrocuted=false;SlowEffectId=null;
        }

        internal void BeginReviving()
        {
            EndPull();
            IsTaunted = false;
            ClearTimedSuperState();
            CurrentHealth = 0; preciseHealth=0m;
            State = UnitState.Dead;
            IsReviving = true;
        }

        internal void CompleteRevival(float healthFraction = -1)
        {
            CurrentHealth = Mathf.Clamp(Mathf.CeilToInt(baseStats.MaxHealth * (healthFraction > 0 ? healthFraction : baseStats.ReviveHealthFraction)), 1, baseStats.MaxHealth);
            preciseHealth=CurrentHealth;
            State = UnitState.Idle;
            IsReviving = false;
        }

        internal void CancelRevival() { IsReviving = false; }

        public float AddEnergy(float amount)
        {
            if (IsRoundFrozen) return 0;
            if (amount > 0 && (IsEnergyLocked || IsOpeningEnergyBlocked || !IsAlive)) return 0;
            float before = Energy;
            Energy = Mathf.Clamp(Energy + amount, 0, Mathf.Max(0, baseStats.EnergyMax));
            return Energy - before;
        }
        internal void ClearEnergy() { Energy = 0; }
        internal void ClearTimedSuperState()
        {
            if (IsEnergyLocked) ClearEnergy();
            IsEnergyLocked = IsReflecting = IsProtected = false;
        }
        internal void AddShield(int amount, int until = int.MaxValue)
        {
            if(IsAlive && amount>0)shields.Add(new ShieldLayer { Amount=amount, Capacity=amount, Until=until });
        }
        internal void RefreshFishShield(int amount, BattleUnit source = null, int until = int.MaxValue)
        {
            if(!IsAlive || amount<=0)return;
            // FIFO absorption; each cast keeps its own expiry, even while waiting behind another.
            shields.Add(new ShieldLayer { Amount=amount, Capacity=amount, Until=until, Fish=true,
                Source=source, SourceVersion=source == null ? 0 : source.CombatResetVersion });
        }
        internal void ClearFishShield() => shields.RemoveAll(layer=>layer.Fish);
        internal void ExpireShields(int now) => shields.RemoveAll(layer=>layer.Until<=now);
        internal int AbsorbShield(int damage, System.Action<BattleUnit,int,int> fishAbsorbed = null)
        {
            return MonsterPouch.Gameplay.Match.EronyaMechanics.Display(AbsorbPreciseLegacyShield(damage,(source,version,amount)=>fishAbsorbed?.Invoke(source,version,MonsterPouch.Gameplay.Match.EronyaMechanics.Display(amount))));
        }
        internal decimal AbsorbPreciseLegacyShield(decimal damage,System.Action<BattleUnit,int,decimal> fishAbsorbed=null)
        {
            decimal remaining=System.Math.Max(0m,damage);
            while(remaining>0 && shields.Count>0)
            {
                var layer=shields[0];
                decimal absorbed=System.Math.Min(layer.Amount,remaining);
                layer.Amount-=absorbed; remaining-=absorbed;
                if(layer.Fish && absorbed>0)fishAbsorbed?.Invoke(layer.Source,layer.SourceVersion,absorbed);
                if(layer.Amount==0)shields.RemoveAt(0);
            }
            return remaining;
        }

        public int ApplyDamage(int amount)
        {
            if(IsRoundFrozen || IsFlying)return 0;
            if(baseStats.UsesEronyaRules)
            {
                decimal taken=System.Math.Min(PreciseHealth,System.Math.Max(0,amount));
                SetPreciseCombatHealth(PreciseHealth-taken);
                return MonsterPouch.Gameplay.Match.EronyaMechanics.Display(taken);
            }
            int applied = Mathf.Min(CurrentHealth, Mathf.Max(0, amount));
            CurrentHealth -= applied;
            if (CurrentHealth == 0) { State = UnitState.Dead; ClearFishShield(); EndPull(); IsTaunted = false; ClearTimedSuperState(); }
            return applied;
        }

        public int Heal(int amount)
        {
            if (IsRoundFrozen || !IsAlive) return 0;
            if(baseStats.UsesEronyaRules)
            {
                decimal restored=System.Math.Min(baseStats.MaxHealth-PreciseHealth,System.Math.Max(0,amount));
                SetPreciseCombatHealth(PreciseHealth+restored);
                return MonsterPouch.Gameplay.Match.EronyaMechanics.Display(restored);
            }
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

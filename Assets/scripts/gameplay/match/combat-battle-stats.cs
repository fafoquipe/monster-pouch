using System;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public readonly struct CombatBattleStatsSnapshot
    {
        public readonly BattleUnit Source;
        public readonly int ResetVersion, BaseHealth, BaseDamage;
        public readonly decimal CurrentHealth, CurrentDamage, CurrentAttacksPerSecond;
        public readonly float BaseAttacksPerSecond, Energy, EnergyMax;
        internal CombatBattleStatsSnapshot(BattleUnit unit, decimal damage, decimal attacksPerSecond)
        {
            Source=unit;ResetVersion=unit.CombatResetVersion;
            CurrentHealth=unit.PreciseHealth;BaseHealth=unit.RoundBaseHealth;
            CurrentDamage=damage;BaseDamage=unit.RoundBaseDamage;
            CurrentAttacksPerSecond=attacksPerSecond;BaseAttacksPerSecond=unit.RoundBaseAttacksPerSecond;
            Energy=unit.Energy;EnergyMax=unit.BaseStats.EnergyMax;
        }
    }

    public sealed partial class CombatSimulation
    {
        // Presentation only: no state creation, target selection, clock advance or events.
        public CombatBattleStatsSnapshot CaptureBattleStats(BattleUnit unit)
        {
            if(unit==null)return default;
            var actor=actors.Find(a=>a.Unit==unit&&a.UnitResetVersion==unit.CombatResetVersion);
            decimal damage=unit.BaseStats.Attack, rate=(decimal)unit.BaseStats.AttackSpeed;
            if(actor!=null)
            {
                damage=EronyaOwns(actor)?EronyaRawBasicDamage(actor):BasicDamage(actor);
                // QueueBasicAttack uses this shared multiplier for both rosters.
                rate*=(decimal)AttackSpeed(actor);
            }
            return new CombatBattleStatsSnapshot(unit,damage,rate);
        }
    }
}

using System.Globalization;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Local
{
    // Read-only presentation. Baselines and effective values are owned by combat.
    public static class BattleInfoText
    {
        // One owner-local row cache. Always compare fresh values: tick equality is
        // insufficient for same-tick damage, energy changes, cleansing or reset.
        public sealed class RowCache
        {
            object identity, source;
            int resetVersion, baseHealth, baseDamage;
            decimal health, damage, attacksPerSecond;
            float baseAttacksPerSecond, energy, energyMax;
            bool initialized, spanish;
            string text;
            public string Rows(object identity, object source, int resetVersion,
                decimal health, int baseHealth, decimal damage, int baseDamage,
                decimal attacksPerSecond, float baseAttacksPerSecond, float energy, float energyMax, bool spanish)
            {
                if (initialized && ReferenceEquals(this.identity, identity) && ReferenceEquals(this.source, source) &&
                    this.resetVersion == resetVersion && this.spanish == spanish && this.health == health &&
                    this.baseHealth == baseHealth && this.damage == damage && this.baseDamage == baseDamage &&
                    this.attacksPerSecond == attacksPerSecond && this.baseAttacksPerSecond.Equals(baseAttacksPerSecond) &&
                    this.energy.Equals(energy) && this.energyMax.Equals(energyMax)) return text;
                // Format before committing the cache, so a formatting error never
                // marks a partial value as initialized.
                string next = BattleInfoText.Rows(health, baseHealth, damage, baseDamage,
                    attacksPerSecond, baseAttacksPerSecond, energy, energyMax, spanish);
                this.identity = identity; this.source = source; this.resetVersion = resetVersion;
                this.health = health; this.baseHealth = baseHealth; this.damage = damage; this.baseDamage = baseDamage;
                this.attacksPerSecond = attacksPerSecond; this.baseAttacksPerSecond = baseAttacksPerSecond;
                this.energy = energy; this.energyMax = energyMax; this.spanish = spanish;
                text = next; initialized = true; return text;
            }
            public string Format(object identity, CombatBattleStatsSnapshot value, bool spanish)
                => Rows(identity, value.Source, value.ResetVersion, value.CurrentHealth, value.BaseHealth,
                    value.CurrentDamage, value.BaseDamage, value.CurrentAttacksPerSecond,
                    value.BaseAttacksPerSecond, value.Energy, value.EnergyMax, spanish);
            public void Clear() { identity = source = null; text = null; initialized = false; }
        }

        static string Number(decimal value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        public static string Rows(decimal health,int baseHealth,decimal damage,int baseDamage,
            decimal attacksPerSecond,float baseAttacksPerSecond,float energy,float energyMax,bool spanish)
        {
            string Pair(decimal current,decimal baseline)=>Number(current)+"/"+Number(baseline);
            return (spanish?"Vida: ":"Health: ")+Pair(health,baseHealth)+"\n"+
                (spanish?"Daño: ":"Damage: ")+Pair(damage,baseDamage)+"\n"+
                (spanish?"Ataques por segundo: ":"Attacks per second: ")+Pair(attacksPerSecond,(decimal)baseAttacksPerSecond)+"\n"+
                (spanish?"Energía: ":"Energy: ")+Pair((decimal)energy,(decimal)energyMax);
        }
        public static string Format(CombatBattleStatsSnapshot value,bool spanish)
            =>Rows(value.CurrentHealth,value.BaseHealth,value.CurrentDamage,value.BaseDamage,
                value.CurrentAttacksPerSecond,value.BaseAttacksPerSecond,value.Energy,value.EnergyMax,spanish);
        public static string Summary(UnitStats stats,bool spanish)
        {
            decimal aps=stats.AttackInterval>0?1m/(decimal)stats.AttackInterval:0;
            return (spanish?"Vida: ":"Health: ")+stats.MaxHealth+" · "+(spanish?"Daño: ":"Damage: ")+stats.Attack+"\n"+
                (spanish?"Ataques/s: ":"Attacks/s: ")+Number(aps)+" · "+(spanish?"Energía: ":"Energy: ")+Number((decimal)stats.EnergyMax);
        }
    }
}

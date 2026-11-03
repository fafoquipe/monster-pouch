using System.Linq;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        public event System.Action<BattleUnit, BattleUnit, float> KazuruRingLaunched;

        static int KazuruBurstTicks(BattleUnit unit) => unit != null && unit.BaseStats.HasEffect("kazuru-ring-throw")
            ? Mathf.Max(1, AbilityTiming.Ticks(unit.BaseStats.AbilityValue("kazuru-ring-throw", "burstInterval", .2f))) : 0;

        bool PreparingKazuruBurst(ActorClock actor) => pending.Any(p => p.Source == actor &&
            p.Effect == "kazuru-ring-followup" && !p.RangeValidated && p.ReleaseTick >= tick);

        void QueueKazuruBurst(ActorClock actor, BattleUnit target, int firstDamage)
        {
            QueueImpact(actor, target, firstDamage, "kazuru");
            if (target == null) return;
            int gap = KazuruBurstTicks(actor.Unit);
            int preparation = AbilityTiming.Ticks(GetAttackWindup(actor.Unit));
            int travel = AbilityTiming.Ticks(GetProjectileTravelTime(actor.Unit, target));
            // Snapshot this attack's damage; a buff gained/expired between rings cannot change their ratio.
            pending.Add(new PendingImpact { Source = actor, Target = target,
                Damage = Mathf.Max(0, Mathf.RoundToInt(firstDamage * V(actor, "kazuru-ring-throw", "secondRingPercent", 70) / 100f)),
                Effect = "kazuru-ring-followup", Secondary = true, Projectile = true, RequiresRange = true,
                ReleaseTick = tick + preparation + gap, DueTick = tick + preparation + gap + travel });
        }

        // Exactly one received damage buff. Compare their actual added damage now;
        // innate rewards remain independent. A discarded buff never returns later.
        void ApplyDamageBoost(ActorClock actor, int flat, float multiplier, int until)
        {
            flat=Mathf.Max(0,flat);multiplier=Mathf.Max(1,multiplier);
            if(flat==0&&multiplier<=1)return;
            float basis = actor.Unit.BaseStats.Attack + actor.DamageBonus;
            float strength = flat * actor.DamageMultiplier + basis * (multiplier - 1);
            float previous = actor.ExternalDamageBonus * actor.DamageMultiplier +
                basis * (actor.ExternalDamageMultiplier - 1);
            // A percentage gift is still valid at zero base damage: an innate
            // damage gain later in the round must benefit from the received gift.
            // The same offset preserves comparisons while admitting zero strength.
            if (!EffectWins(1+strength, until, 1+previous, actor.ExternalDamageUntil)) return;
            actor.ExternalDamageBonus = Mathf.Max(0, flat);
            actor.ExternalDamageMultiplier = Mathf.Max(1, multiplier);
            actor.ExternalDamageUntil = until;
        }

        void CastKazuruRings(ActorClock caster)
        {
            var source = caster.Unit;
            if (source == null || !source.IsAlive || source.CurrentCell == null) return;
            int width = Mathf.RoundToInt(V(caster, "kazuru-rising-rings", "width", 3));
            var cells = PokrakBubbleCells(source.CurrentCell.Coordinates, width)
                .Select(p => board.GetCell(p.x, p.y)).Where(c => c != null).ToArray();
            cells = ResolveAbilityArea(caster,"kazuru-rising-rings",source.CurrentCell,cells);
            int until = tick + AbilityTiming.Ticks(V(caster, "kazuru-rising-rings", "duration", 5));
            int bonus = Mathf.Max(0, Mathf.RoundToInt(V(caster, "kazuru-rising-rings", "damage", 15)));
            foreach (var actor in actors)
                if (actor.Unit != null && actor.Unit.IsAlive && actor.Unit.isActiveAndEnabled &&
                    !actor.Unit.IsReviving && actor.Unit.Side == source.Side && cells.Contains(actor.Unit.CurrentCell))
                    ApplyDamageBoost(actor, bonus, 1, until);
            if (Has(caster, "kazuru-renewal"))
            {
                // Recast replaces the schedule; no concurrent healing stacks or extra instant tick.
                caster.KazuruHealAmount = Mathf.Max(0, Mathf.RoundToInt(V(caster, "kazuru-renewal", "heal", 10)));
                caster.KazuruHealTicks = Mathf.Max(0, Mathf.FloorToInt(V(caster, "kazuru-renewal", "duration", 3)));
                caster.KazuruHealNext = tick + AbilityTiming.Ticks(1);
            }
            if (!HasEditedArea(caster,"kazuru-rising-rings")) AreaEffect?.Invoke(source, "kazuru-rising-rings", cells);
            lastChangeTick = tick;
        }

        // Called after reductions and health shields, for impacts, executes, DoT and reflections.
        int ApplyCombatDamage(ActorClock defender, int amount)
        {
            if (defender == null || defender.Unit == null || !defender.Unit.IsAlive ||
                defender.InvulnerableUntil > tick) return 0;
            if (amount >= defender.Unit.CurrentHealth && Has(defender, "kazuru-last-halo") && !defender.KazuruHaloUsed)
            {
                defender.KazuruHaloUsed = true;
                defender.KazuruHaloUntil = tick + AbilityTiming.Ticks(V(defender, "kazuru-last-halo", "duration", 2));
                defender.InvulnerableUntil = Mathf.Max(defender.InvulnerableUntil, defender.KazuruHaloUntil);
                amount = Mathf.Max(0, defender.Unit.CurrentHealth - 1);
                AbilityUsed?.Invoke(defender.Unit, "kazuru-last-halo");
            }
            return defender.Unit.ApplyDamage(amount);
        }

        void TickKazuruEffects()
        {
            foreach (var actor in actors)
            {
                if (actor.Unit == null || !actor.Unit.IsAlive || actor.Unit.IsReviving || !actor.Unit.isActiveAndEnabled)
                { actor.KazuruHealTicks = actor.KazuruHaloUntil = 0; continue; }
                if (actor.KazuruHealTicks > 0 && tick >= actor.KazuruHealNext)
                {
                    actor.KazuruHealTicks--;
                    actor.KazuruHealNext += AbilityTiming.Ticks(1);
                    Heal(actor, actor.KazuruHealAmount);
                }
            }
        }

        public bool KazuruHaloActive(BattleUnit unit) => begun && !Finished && unit != null &&
            unit.IsAlive && Clock(unit) is ActorClock actor && actor.KazuruHaloUntil > tick;
    }
}

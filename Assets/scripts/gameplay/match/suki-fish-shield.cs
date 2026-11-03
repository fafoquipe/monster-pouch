using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        public sealed class FishShieldFlight
        {
            public BattleUnit Source { get; internal set; }
            public BattleUnit Target { get; internal set; }
            public BoardCell Origin { get; internal set; }
            public float StartedAt { get; internal set; }
            public float Duration { get; internal set; }
            public bool Active { get; internal set; } = true;
            public bool Arrived { get; internal set; }
            internal int Due, SourceVersion, TargetVersion, Amount, ShieldTicks;
        }
        readonly List<FishShieldFlight> fishFlights = new List<FishShieldFlight>();
        public event Action<FishShieldFlight> FishShieldLaunched;

        static bool FishRecipient(ActorClock actor) => actor?.Unit != null && actor.Unit.IsAlive &&
            actor.Unit.isActiveAndEnabled && !actor.Unit.IsReviving && actor.Unit.CurrentCell != null;

        void OpenFishShields(ActorClock caster)
        {
            if (!FishRecipient(caster)) return;
            caster.Unit.SetState(UnitState.Attacking);
            ReserveAbilityMotion(caster, "suki-opening-shield");
            AbilityUsed?.Invoke(caster.Unit, "suki-opening-shield");
            LaunchFishShields(caster);
        }

        void LaunchFishShields(ActorClock caster)
        {
            if (!FishRecipient(caster)) return;
            int amount = Mathf.Max(0, Mathf.RoundToInt(V(caster, "suki-fish-shield", "shield", 15) +
                (Has(caster, "suki-extra-shield") ? V(caster, "suki-extra-shield", "shieldBonus", 10) : 0)));
            int duration = AbilityTiming.Ticks(V(caster, "suki-fish-shield", "duration", 5));
            int travel = Mathf.Max(1, AbilityTiming.Ticks(V(caster, "suki-fish-shield", "travelDuration", .7f)));
            int count = Mathf.Clamp(Mathf.RoundToInt(V(caster, "suki-fish-shield", "targets", 3)), 0, actors.Count);
            ApplyFishShield(caster, caster.Unit, amount, duration);
            var origin = caster.Unit.CurrentCell;
            // Stable board distances and IDs give both peers exactly the same recipients.
            var recipients = actors.Where(a => a != caster && FishRecipient(a) && a.Unit.Side == caster.Unit.Side)
                .OrderBy(a => Distance(origin, a.Unit.CurrentCell))
                .ThenBy(a => RingDistance(origin, a.Unit.CurrentCell))
                .ThenBy(a => a.Unit.UnitId, StringComparer.Ordinal).Take(count).ToArray();
            foreach (var recipient in recipients)
            {
                var flight = new FishShieldFlight
                {
                    Source = caster.Unit, Target = recipient.Unit, Origin = origin,
                    StartedAt = Elapsed, Duration = travel * TickDuration, Due = tick + travel,
                    SourceVersion = caster.Unit.CombatResetVersion, TargetVersion = recipient.Unit.CombatResetVersion,
                    Amount = amount, ShieldTicks = duration
                };
                fishFlights.Add(flight);
                FishShieldLaunched?.Invoke(flight);
            }
            if (amount > 0 && duration > 0) lastChangeTick = tick;
        }

        void ApplyFishShield(ActorClock recipient, BattleUnit source, int amount, int duration)
        {
            if (!FishRecipient(recipient) || amount <= 0 || duration <= 0) return;
            recipient.Unit.RefreshFishShield(amount, source, tick + duration);
            recipient.FishShieldUntil = recipient.Unit.FishShieldExpiresAt;
            lastChangeTick = tick;
        }

        void ResolveFishFlights()
        {
            for (int i = fishFlights.Count - 1; i >= 0; i--)
            {
                var flight = fishFlights[i];
                var recipient = Clock(flight.Target);
                bool valid = flight.Source != null && flight.Source.CombatResetVersion == flight.SourceVersion &&
                    FishRecipient(recipient) && flight.Target.CombatResetVersion == flight.TargetVersion &&
                    flight.Target.Side == flight.Source.Side;
                if (valid && tick < flight.Due) continue;
                flight.Active = false;
                if (valid)
                {
                    ApplyFishShield(recipient, flight.Source, flight.Amount, flight.ShieldTicks);
                    flight.Arrived = true;
                }
                fishFlights.RemoveAt(i);
            }
        }

        int AbsorbCombatShield(BattleUnit target, int damage)
        {
            int remaining = target.AbsorbShield(damage, (source,version,absorbed) =>
            {
                // A hit may consume layers from different Sukis; credit each layer's owner.
                if (source != null && source != target && source.IsAlive && source.isActiveAndEnabled &&
                    source.Side == target.Side && source.CombatResetVersion == version && source.BaseStats.HasEffect("suki-shield-energy"))
                    source.AddEnergy(absorbed * source.BaseStats.AbilityValue("suki-shield-energy", "energyPerShieldDamage", 1));
            });
            return remaining;
        }

        void TickFishShields()
        {
            foreach (var actor in actors)
            {
                if (actor.Unit == null) continue;
                actor.Unit.ExpireShields(tick);
                if (!actor.Unit.IsAlive) actor.Unit.ClearFishShield();
                actor.FishShieldUntil = actor.Unit.FishShieldExpiresAt;
            }
        }

        void ClearFishShields()
        {
            foreach (var flight in fishFlights) flight.Active = false;
            fishFlights.Clear();
            foreach (var actor in actors)
            { actor.FishShieldUntil = 0; if (actor.Unit != null) actor.Unit.ClearFishShield(); }
        }
    }
}

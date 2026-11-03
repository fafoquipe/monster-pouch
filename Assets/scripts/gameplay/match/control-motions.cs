using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private sealed class TravellingDash
        {
            public ActorClock Source;
            public Vector2 From, Through, To;
            public int Start, Duration, Version, AttackVersion, LastTick;
            public float Width;
            public readonly HashSet<BattleUnit> Hit = new HashSet<BattleUnit>();
        }
        private sealed class HookPull
        {
            public ActorClock Source, Target;
            public int Due, SourceVersion, TargetVersion, StunUntil, PreviousStun, PreviousMove;
        }
        private readonly List<TravellingDash> travellingDashes = new List<TravellingDash>();
        private readonly List<HookPull> hookPulls = new List<HookPull>();

        private void ClearControlMotions()
        {
            travellingDashes.Clear();
            foreach (HookPull pull in hookPulls.ToList()) FinishHookPull(pull, false);
            foreach (ActorClock actor in actors)
            {
                actor.DashUntilTick = actor.PullUntilTick = 0;
                if (actor.Unit != null) actor.Unit.EndPull();
            }
        }
        private void CancelControlMotions(ActorClock actor)
        {
            travellingDashes.RemoveAll(dash => dash.Source == actor);
            actor.DashUntilTick = 0;
            foreach (HookPull pull in hookPulls.Where(pull => pull.Source == actor || pull.Target == actor).ToList())
                FinishHookPull(pull, false);
        }
        private void ResolveControlMotions()
        {
            foreach (HookPull pull in hookPulls.ToList())
            {
                bool valid = pull.Source.Unit != null && pull.Source.Unit.IsAlive && pull.Source.Unit.isActiveAndEnabled &&
                    pull.Target.Unit != null && pull.Target.Unit.IsAlive && pull.Target.Unit.isActiveAndEnabled &&
                    pull.Source.Unit.CombatResetVersion == pull.SourceVersion && pull.Target.Unit.CombatResetVersion == pull.TargetVersion;
                if (!valid || tick >= pull.Due) FinishHookPull(pull, valid);
            }
            foreach (TravellingDash dash in travellingDashes.ToList())
            {
                BattleUnit unit = dash.Source.Unit;
                if (unit == null || !unit.IsAlive || !unit.isActiveAndEnabled || unit.CombatResetVersion != dash.Version ||
                    unit.AttackResetVersion != dash.AttackVersion)
                { travellingDashes.Remove(dash); dash.Source.DashUntilTick = 0; continue; }
                SweepDashContacts(dash, dash.LastTick, tick);
                dash.LastTick = tick;
                if (tick >= dash.Start + dash.Duration) travellingDashes.Remove(dash);
            }
        }
        private bool StartHookPull(ActorClock source, ActorClock target, BoardCell destination)
        {
            foreach (HookPull previous in hookPulls.Where(pull => pull.Target == target).ToList()) FinishHookPull(previous, false);
            bool moving = destination != target.Unit.CurrentCell;
            int travel = moving ? ToTicks(V(source, "hymay-pull", "pullDuration", .45f)) : 0;
            float post = V(source, "hymay-pull", "postArrivalStun", 1f);
            int after = post > 0 ? ToTicks(post) : 0;
            var pull = new HookPull { Source = source, Target = target, Due = tick + travel,
                SourceVersion = source.Unit.CombatResetVersion, TargetVersion = target.Unit.CombatResetVersion,
                PreviousStun = target.StunUntil, PreviousMove = target.NextMove, StunUntil = tick + travel + after };
            // The board owns the destination immediately; the state keeps the target disabled
            // for the actual visible journey and the configured pause after arrival.
            if (moving)
            {
                target.Unit.BeginPull(travel * TickDuration);
            }
            if (travel + after > 0) Stun(target, travel + after);
            if (moving && !Reposition(target, destination)) { FinishHookPull(pull, false); return false; }
            if (target.StunUntil == pull.StunUntil && pull.PreviousStun < pull.StunUntil)
            {
                // A shorter stun lost the comparison; cancelling this hook cannot revive it.
                if (pull.PreviousMove <= pull.PreviousStun) pull.PreviousMove = 0;
                pull.PreviousStun = 0;
            }
            hookPulls.Add(pull);
            source.PullUntilTick = pull.Due;
            source.NextAttack = Mathf.Max(source.NextAttack, pull.Due);
            source.NextMove = Mathf.Max(source.NextMove, pull.Due);
            if (!moving) FinishHookPull(pull, true);
            return true;
        }
        private void FinishHookPull(HookPull pull, bool arrived)
        {
            hookPulls.Remove(pull);
            pull.Source.PullUntilTick = 0;
            if (pull.Target.Unit != null) pull.Target.Unit.EndPull();
            if (!arrived)
            {
                // Remove only this interrupted pull's control, never a later overlapping stun.
                if (pull.Target.StunUntil == pull.StunUntil)
                {
                    pull.Target.StunUntil = pull.PreviousStun;
                    int ready = Mathf.Max(tick + 1, pull.PreviousStun);
                    if (pull.Source.Unit == null || !pull.Source.Unit.IsAlive)
                        ready = Mathf.Max(ready, tick + ToTicks(GetTargetReactionDelay(pull.Target.Unit)));
                    pull.Target.NextAttack = pull.Target.NextAttack == pull.StunUntil ? ready : Mathf.Max(ready, pull.Target.NextAttack);
                    if (pull.Target.NextMove == pull.StunUntil) pull.Target.NextMove = Mathf.Max(ready, pull.PreviousMove);
                    if (pull.Target.Unit != null) pull.Target.Unit.IsStunned = pull.PreviousStun > tick;
                }
                return;
            }
            if (Has(pull.Source, "hymay-backstep"))
                TryHymayRetreat(pull.Source, N(pull.Source, "hymay-backstep", "distance", 1));
        }
        private bool TryHymayBackflip(ActorClock actor, BoardCell destination)
        {
            if (actor.Unit == null || !actor.Unit.IsAlive || actor.Unit.IsFlying || !Free(destination)) return false;
            int travel = ToTicks(V(actor, "hymay-backstep", "travelDuration", .7f));
            InterruptAttack(actor);
            actor.Unit.BeginFlight(travel * TickDuration);
            if (!Reposition(actor, destination)) { actor.Unit.EndFlight(); return false; }
            int returnTick = tick + travel + AbilityTiming.Ticks(AbilityTiming.BackflipLandingDuration);
            actor.AbilityMotionUntil = Mathf.Max(actor.AbilityMotionUntil, returnTick);
            if (actor.EnergySuperActive) actor.EnergySuperUntil = Mathf.Max(actor.EnergySuperUntil, returnTick);
            actor.NextAttack = actor.NextMove = returnTick;
            actor.Unit.SetState(UnitState.Moving);
            return true;
        }
        private void SweepDashContacts(TravellingDash dash, int fromTick, int toTick)
        {
            float from = Mathf.Clamp01((fromTick - dash.Start) / (float)dash.Duration);
            float to = Mathf.Clamp01((toTick - dash.Start) / (float)dash.Duration);
            float firstLength = Vector2.Distance(dash.From, dash.Through);
            float length = firstLength + Vector2.Distance(dash.Through, dash.To);
            float split = length > .00001f ? firstLength / length : 1;
            Vector2 Path(float progress) => progress <= split
                ? Vector2.Lerp(dash.From, dash.Through, split > 0 ? progress / split : 1)
                : Vector2.Lerp(dash.Through, dash.To, split < 1 ? (progress - split) / (1 - split) : 1);
            Vector2 start = Path(from), end = Path(to);
            float radiusSquared = dash.Width * dash.Width * .25f + .00001f;
            foreach (BattleUnit victim in Enemies(dash.Source))
            {
                if (dash.Hit.Contains(victim)) continue;
                Vector2 position = victim.CurrentCell.Coordinates;
                // Splitting at the corner avoids hitting cells inside an untravelled shortcut.
                bool contact = from < split && to > split
                    ? DistanceToSegmentSquared(position, start, dash.Through) <= radiusSquared ||
                      DistanceToSegmentSquared(position, dash.Through, end) <= radiusSquared
                    : DistanceToSegmentSquared(position, start, end) <= radiusSquared;
                if (!contact) continue;
                dash.Hit.Add(victim);
                float duration = Has(dash.Source, "popow-dash-stun") ? V(dash.Source, "popow-dash-stun", "duration", 2f) : V(dash.Source, "popow-dash", "duration", 1f);
                if (duration > 0) Stun(Clock(victim), ToTicks(duration), "popow");
                if (Has(dash.Source, "popow-dash-charge"))
                    dash.Source.NextBasicBonus += Mathf.RoundToInt(V(dash.Source, "popow-dash-charge", "damage", 3f));
            }
        }
    }
}

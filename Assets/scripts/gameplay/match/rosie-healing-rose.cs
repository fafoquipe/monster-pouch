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
        // Presentation reads the same straight flight and clock as collision. Its sway is visual only.
        public sealed class HealingRoseFlight
        {
            public BattleUnit Source { get; internal set; }
            public BoardCell Origin { get; internal set; }
            public BoardCell Destination { get; internal set; }
            // Destination chooses the heading; End lies beyond the board, not at that ally.
            public Vector2 End { get; internal set; }
            public float Width { get; internal set; }
            public float Duration { get; internal set; }
            public float StartedAt { get; internal set; }
            public bool Active { get; internal set; } = true;
        }
        sealed class HealingRose
        {
            public ActorClock Caster;
            public HealingRoseFlight Flight;
            public Vector2 Start, Direction;
            public float Length, Travelled;
            public int StartTick, TravelTicks, SourceVersion;
            public readonly HashSet<BattleUnit> Reached = new HashSet<BattleUnit>();
        }
        readonly List<HealingRose> healingRoses = new List<HealingRose>();
        public event Action<HealingRoseFlight> HealingRoseLaunched;

        static Vector2 RoseCell(BoardCell cell) => new Vector2(cell.X, cell.Y);
        static bool RoseRecipient(ActorClock actor) => actor.Unit != null && actor.Unit.IsAlive &&
            actor.Unit.isActiveAndEnabled && !actor.Unit.IsReviving && !actor.Unit.IsFlying && actor.Unit.CurrentCell != null;

        void LaunchHealingRose(ActorClock caster)
        {
            var unit = caster.Unit;
            var origin = unit.CurrentCell;
            if (origin == null) return;
            // Select among OTHER allies, otherwise self would always win the nearest selection.
            // A stable tie-break keeps online simulations deterministic.
            bool backRow = origin.Y == (unit.Side == BoardSide.Blue ? BoardManager.Height - 1 : 0);
            var allies = actors.Where(a => a != caster && RoseRecipient(a) && a.Unit.Side == unit.Side);
            var ordered = backRow
                ? allies.OrderBy(a => Distance(origin, a.Unit.CurrentCell)).ThenBy(a => RingDistance(origin, a.Unit.CurrentCell))
                : allies.OrderByDescending(a => Distance(origin, a.Unit.CurrentCell)).ThenByDescending(a => RingDistance(origin, a.Unit.CurrentCell));
            var target = ordered.ThenBy(a => a.Unit.UnitId, StringComparer.Ordinal).FirstOrDefault();
            var destination = target?.Unit.CurrentCell ?? board.GetCell(origin.X, unit.Side == BoardSide.Blue ? 0 : BoardManager.Height - 1);
            Vector2 start = RoseCell(origin), delta = RoseCell(destination) - start;
            Vector2 direction = delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.up * Forward(unit);
            float width = Mathf.Clamp(V(caster, "rosie-healing-rose", "width", 3), 1, BoardManager.Width);
            float length = RoseExitDistance(start, direction, width * .5f);
            var flight = new HealingRoseFlight
            {
                Source = unit, Origin = origin, Destination = destination,
                End = start + direction * length, Width = width,
                Duration = Mathf.Max(TickDuration, ToTicks(length / Mathf.Max(.1f, V(caster, "rosie-healing-rose", "projectileSpeed", 3))) * TickDuration),
                StartedAt = Elapsed
            };
            var rose = new HealingRose
            {
                Caster = caster, Flight = flight, Start = start, Direction = direction,
                Length = length, StartTick = tick, TravelTicks = ToTicks(flight.Duration), SourceVersion = unit.CombatResetVersion
            };
            // Self receives all three purchased effects even with no other ally and at full health.
            rose.Reached.Add(unit);
            ApplyHealingRose(caster, caster);
            healingRoses.Add(rose);
            HealingRoseLaunched?.Invoke(flight);
            SweepHealingRose(rose, 0, 0);
            lastChangeTick = tick;
        }

        static float RoseExitDistance(Vector2 start, Vector2 direction, float radius)
        {
            // Travel until the whole aura leaves the board. No ally limits its range.
            float margin = .5f + radius;
            float x = Mathf.Abs(direction.x) < .0001f ? float.PositiveInfinity :
                ((direction.x > 0 ? BoardManager.Width - 1 + margin : -margin) - start.x) / direction.x;
            float y = Mathf.Abs(direction.y) < .0001f ? float.PositiveInfinity :
                ((direction.y > 0 ? BoardManager.Height - 1 + margin : -margin) - start.y) / direction.y;
            return Mathf.Min(x, y);
        }

        void ResolveHealingRoses()
        {
            for (int i = healingRoses.Count - 1; i >= 0; i--)
            {
                var rose = healingRoses[i];
                if (rose.Flight.Source == null || rose.Flight.Source.CombatResetVersion != rose.SourceVersion)
                { rose.Flight.Active = false; healingRoses.RemoveAt(i); continue; }
                float travelled = rose.Length * Mathf.Clamp01((tick - rose.StartTick) / (float)rose.TravelTicks);
                SweepHealingRose(rose, rose.Travelled, travelled);
                rose.Travelled = travelled;
                if (tick - rose.StartTick >= rose.TravelTicks)
                { rose.Flight.Active = false; healingRoses.RemoveAt(i); }
            }
        }

        void SweepHealingRose(HealingRose rose, float from, float to)
        {
            foreach (var ally in actors)
            {
                if (!RoseRecipient(ally) || ally.Unit.Side != rose.Flight.Source.Side || rose.Reached.Contains(ally.Unit)) continue;
                Vector2 offset = RoseCell(ally.Unit.CurrentCell) - rose.Start;
                float forward = Vector2.Dot(offset, rose.Direction);
                // Sweep the same circular footprint shown by the moving aura.
                // The whole segment is tested, so high editable speeds cannot skip allies.
                Vector2 closest = rose.Direction * Mathf.Clamp(forward, from, to);
                float radius = rose.Flight.Width * .5f;
                if ((offset - closest).sqrMagnitude > radius * radius + .0001f) continue;
                rose.Reached.Add(ally.Unit);
                ApplyHealingRose(rose.Caster, ally);
            }
        }

        void ApplyHealingRose(ActorClock caster, ActorClock recipient)
        {
            float percent = Mathf.Clamp(V(caster, "rosie-healing-rose", "healPercent", 25), 0, 100);
            Heal(recipient, Mathf.RoundToInt(recipient.Unit.BaseStats.MaxHealth * percent / 100));
            if (Has(caster, "rosie-shield"))
                recipient.Unit.AddShield(Mathf.RoundToInt(recipient.Unit.BaseStats.MaxHealth * Mathf.Max(0, V(caster, "rosie-shield", "shieldHealthPercent", 10)) / 100));
            if (Has(caster, "rosie-control-immunity"))
            {
                recipient.RoseControlUntil = Mathf.Max(recipient.RoseControlUntil,
                    tick + AbilityTiming.Ticks(V(caster, "rosie-control-immunity", "duration", 3)));
                if (recipient.RoseControlUntil > tick) ClearRoseControl(recipient);
            }
            if (Has(caster, "rosie-haste"))
            {
                ApplyAttackSpeed(recipient, Mathf.Max(0, V(caster, "rosie-haste", "speedPercent", 30)) / 100,
                    tick + AbilityTiming.Ticks(V(caster, "rosie-haste", "duration", 3)), "rose-haste");
            }
            if (recipient.Unit.CurrentCell != null) AreaEffect?.Invoke(caster.Unit, "rosie-heal", new[] { recipient.Unit.CurrentCell });
        }

        void ClearRoseControl(ActorClock actor)
        {
            var unit = actor.Unit;
            int blockedUntil = actor.StunUntil;
            // Stop incoming hooks only; healing must not cancel this ally's own dash or Super.
            foreach (var pull in hookPulls.Where(p => p.Target == actor).ToList())
                FinishHookPull(pull, false);
            unit.EndPull();
            actor.StunUntil = actor.PopowStunUntil = actor.ElectricStunUntil = actor.RootUntil = 0;
            unit.IsStunned = unit.IsPopowStunned = unit.IsElectrocuted = unit.IsRooted = false;
            gumRootVersions.Remove(unit);
            actor.ChocolateSource = null; actor.ChocolateNext = actor.ChocolateStacks = 0;
            tiles.RemoveAll(tile => tile.Kind == "trap" && tile.Captured == unit);
            ClearTaunt(actor, true);
            bool slowed = actor.AttackSpeedMultiplier < 1;
            actor.AttackSpeedMultiplier = Mathf.Max(1, actor.AttackSpeedMultiplier);
            unit.IsSlowed = false; unit.SlowEffectId = null;
            // A removed stun must not leave its movement lock running invisibly.
            if (blockedUntil > tick && actor.NextMove <= blockedUntil)
                actor.NextMove = Mathf.Max(tick, actor.AbilityMotionUntil, actor.SuperRecoveryUntil,
                    actor.OpeningRecoveryUntil, actor.DashUntilTick, actor.PullUntilTick);
            if (slowed) RetimeRoseAttack(actor);
        }

        void RetimeRoseAttack(ActorClock actor)
        {
            if (actor.LastBasicTick > 0 && actor.NextAttack >= tick && !actor.EnergySuperActive)
                actor.NextAttack = Mathf.Max(tick, actor.LastBasicTick + BasicPeriod(actor), actor.AbilityMotionUntil, actor.StunUntil);
            if (Id(actor) == "sepora" && actor.SuperUntil > tick)
                actor.NextStormPulse = Mathf.Min(actor.NextStormPulse, tick + StormPeriod(actor));
        }
        void TickRoseHaste()
        {
            foreach (var actor in actors)
                if (actor.ExternalSpeedUntil > 0 && actor.ExternalSpeedUntil <= tick)
                { actor.ExternalSpeedUntil = 0; actor.ExternalSpeedBonus = 0; actor.ExternalSpeedId = null; RetimeRoseAttack(actor); }
        }
        void ResetRoseEffects(ActorClock actor)
        {
            actor.RoseControlUntil = 0;
            foreach (var rose in healingRoses.Where(r => r.Caster == actor)) rose.Flight.Active = false;
            healingRoses.RemoveAll(r => r.Caster == actor);
        }
        void ClearHealingRoses()
        {
            foreach (var rose in healingRoses) rose.Flight.Active = false;
            healingRoses.Clear();
            foreach (var actor in actors) { actor.RoseControlUntil = 0; ResetReceivedEffects(actor); }
        }
    }
}

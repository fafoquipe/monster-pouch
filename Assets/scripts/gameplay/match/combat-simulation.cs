using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Authoritative 100 ms combat. Rendering never controls a logical cooldown.</summary>
    public sealed partial class CombatSimulation
    {
        public const float TickDuration = 0.1f;
        private const double ExactTickDuration = 0.1d;
        public const int TimeoutTicks = 400;
        public const int StalemateTicks = 100;

        private sealed class ActorClock
        {
            public BattleUnit Unit;
            public int NextMove = 1;
            public int NextAttack = 1;
            public int Hits;
            public BoardCell LastCell;
            public int RevivesUsed;
            public int ReviveDueTick;
            public int NextSummon;
            public int SummonSequence;
            public BattleUnit Summoner;
            public BattleUnit LockedTarget;
            public int UnitResetVersion;
            public int TargetResetVersion;
            public int Attacks, DamageBonus, NextBasicBonus, StunUntil, RootUntil, InvulnerableUntil, StealthUntil;
            public int SuperUntil, WeakUntil, AntiHealUntil, LastHealTick, NextIncome, GumCycle, LastBasicTick;
            public int ChocolateNext, ChocolateStacks;
            public float SpeedBonus, AttackSpeedMultiplier = 1, DamageMultiplier = 1, Weakness = 1;
            public float AntiHealMultiplier = .4f;
            public bool StealthUsed, SleepStarted, DeathHandled;
            public string SummonKind;
            public BattleUnit LastDamager, ChocolateSource;
            public BattleUnit TauntTarget, TargetBeforeTaunt;
            public BattleUnit FocusTarget;
        }

        private sealed class PendingImpact
        {
            public ActorClock Source;
            public BattleUnit Target;
            public int DueTick;
            public int ReleaseTick;
            public bool Projectile;
            public int Damage;
            public string Effect;
            public bool Super;
            public bool Critical;
        }

        private readonly BoardManager board;
        private readonly List<ActorClock> actors = new List<ActorClock>();
        private readonly List<PendingImpact> pending = new List<PendingImpact>();
        private readonly List<BattleUnit> combatOnlyUnits = new List<BattleUnit>();
        private readonly List<BattleUnit> lockedCandidate = new List<BattleUnit>(1);
        private int tick;
        private int lastChangeTick;
        private double accumulator;
        private bool begun;

        public bool Finished { get; private set; }
        public BoardSide? Winner { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public float Elapsed => tick * TickDuration;
        public int PendingImpactCount => pending.Count;
        public IReadOnlyList<BattleUnit> CombatOnlyUnits => combatOnlyUnits.AsReadOnly();

        public event Action<BattleUnit, BoardCell, BoardCell> Moved;
        public event Action<BattleUnit, BattleUnit, bool> Attacked;
        public event Action<BattleUnit, BattleUnit, int> Impacted;
        public event Action<BattleUnit, int> Healed;
        public event Action<BattleUnit, BattleUnit> CriticalImpacted;
        public event Action<BattleUnit> AttackReset;
        public event Action<BoardCell> HealingArea;
        public event Action<BattleUnit> Died;
        public event Action<BattleUnit, float> Reviving;
        public event Action<BattleUnit> Revived;
        public event Action<BattleUnit, UnitDefinition> Summoned;
        public event Action<BattleUnit, BattleUnit> LethalImpacted;

        public CombatSimulation(BoardManager boardManager)
        {
            board = boardManager ?? throw new ArgumentNullException(nameof(boardManager));
        }

        public void Begin(IReadOnlyList<BattleUnit> units,
            IReadOnlyDictionary<BattleUnit, OwnedUnit> loadouts = null)
        {
            Stop();
            actors.Clear();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var references = new HashSet<BattleUnit>();
            for (int i = 0; units != null && i < units.Count; i++)
            {
                BattleUnit unit = units[i];
                if (unit == null || !references.Add(unit)) continue;
                if (string.IsNullOrWhiteSpace(unit.UnitId) || !identities.Add(unit.UnitId))
                    throw new InvalidOperationException("Combat requires nonempty, unique unit IDs.");
                if (unit.CurrentCell == null || !board.IsManagedCell(unit.CurrentCell) ||
                    !ReferenceEquals(unit.CurrentCell.OccupiedBy, unit))
                    throw new InvalidOperationException("Combat requires a valid occupied cell per unit.");
                if (loadouts != null && loadouts.TryGetValue(unit, out OwnedUnit owned))
                    unit.Initialize(unit.UnitId, unit.Side, UnitStats.FromDefinition(owned.Definition, owned));
                else unit.ResetForCombat();
                board.CancelReservation(unit);
                actors.Add(new ActorClock { Unit = unit, LastCell = unit.CurrentCell, UnitResetVersion = unit.CombatResetVersion,
                    NextSummon = ToTicks(unit.BaseStats.SummonInterval) });
            }
            // Canonical event ordering only. This order is never used to award a reservation.
            actors.Sort((a, b) => string.CompareOrdinal(a.Unit.UnitId, b.Unit.UnitId));
            tick = lastChangeTick = 0;
            accumulator = 0;
            Winner = null;
            Reason = string.Empty;
            Finished = false;
            begun = true;
            BeginDocumentedCombat();
            EvaluateTeams();
        }

        public void Step(float deltaTime)
        {
            if (!begun || Finished || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            accumulator += deltaTime;
            while (accumulator + 0.000001 >= ExactTickDuration && !Finished)
            {
                accumulator -= ExactTickDuration;
                ExecuteTick();
            }
        }

        public void Stop()
        {
            ClearDocumentedCombat();
            pending.Clear();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Unit != null)
                {
                    ClearTimedSuper(actors[i]);
                    actors[i].LockedTarget = null;
                    board.CancelReservation(actors[i].Unit);
                    actors[i].Unit.CancelRevival();
                }
            for (int i = 0; i < combatOnlyUnits.Count; i++)
            {
                BattleUnit unit = combatOnlyUnits[i];
                if (unit == null) continue;
                board.ReleaseUnit(unit);
                unit.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(unit.gameObject);
                else UnityEngine.Object.DestroyImmediate(unit.gameObject);
            }
            combatOnlyUnits.Clear();
            begun = false;
            Finished = true;
            accumulator = 0;
        }

        private void ExecuteTick()
        {
            if (documentedCombat) { ExecuteDocumentedTick(); return; }
            tick++;
            SyncCombatResets();
            CompleteRevivals();
            var live = new List<BattleUnit>();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Unit != null && actors[i].Unit.IsAlive) live.Add(actors[i].Unit);
            var moves = new List<BoardMovementIntent>();
            var movingClocks = new List<ActorClock>();

            for (int i = 0; i < actors.Count; i++)
            {
                ActorClock clock = actors[i];
                BattleUnit unit = clock.Unit;
                if (unit == null || !unit.IsAlive) continue;
                if (tick < clock.NextMove && tick < clock.NextAttack) continue;
                CombatTargetSelection selection = FollowOrAcquireTarget(clock, live);
                if (selection.Status == CombatTargetSelectionStatus.ReadyToAttack)
                {
                    if (tick < clock.NextAttack) continue;
                    bool projectile = unit.BaseStats.AttackRange > 1;
                    int impactDelay = ToTicks(GetImpactDelay(unit, selection.Target));
                    int windup = ToTicks(GetAttackWindup(unit));
                    pending.Add(new PendingImpact
                    {
                        Source = clock, Target = selection.Target,
                        DueTick = tick + impactDelay,
                        ReleaseTick = tick + windup, Projectile = projectile,
                        Damage = unit.BaseStats.Attack
                    });
                    clock.NextAttack = tick + Mathf.Max(windup + 1, ToTicks(unit.BaseStats.AttackInterval));
                    // A melee windup completes before its source may start another step.
                    // Projectiles can remain in flight after the source resumes movement.
                    clock.NextMove = Mathf.Max(clock.NextMove, tick + windup + 1);
                    unit.SetState(UnitState.Attacking);
                    Attacked?.Invoke(unit, selection.Target, projectile);
                }
                else if (selection.Status == CombatTargetSelectionStatus.MoveRequested && tick >= clock.NextMove)
                {
                    moves.Add(new BoardMovementIntent(unit, selection.AttackCell));
                    movingClocks.Add(clock);
                }
                else unit.SetState(UnitState.Idle);
            }

            List<BoardMovementResult> movementResults = BoardMovementResolver.ResolveMovement(board, moves);
            for (int i = 0; i < movementResults.Count; i++)
            {
                ActorClock clock = movingClocks[i];
                clock.NextMove = tick + ToTicks(clock.Unit.BaseStats.MoveInterval);
                if (movementResults[i].Status != BoardMovementStatus.Moved) continue;
                // Logical travel takes the configured duration even when no view exists.
                // Waiting on this clock keeps attacks from firing halfway through a step.
                clock.NextAttack = Mathf.Max(clock.NextAttack, clock.NextMove);
                BoardCell from = clock.LastCell;
                clock.LastCell = clock.Unit.CurrentCell;
                clock.Unit.SetState(UnitState.Moving);
                lastChangeTick = tick;
                Moved?.Invoke(clock.Unit, from, clock.LastCell);
            }

            ResolveImpacts();
            if (EvaluateTeams()) return;
            ResolveSummons();
            if (tick >= TimeoutTicks) Finish(null, "Tiempo de combate agotado");
            else if (tick - lastChangeTick >= StalemateTicks && !HasUsefulPendingImpact() && !HasUsefulPendingAbility())
                Finish(null, "Sin progreso durante 10 segundos");
        }

        private CombatTargetSelection FollowOrAcquireTarget(ActorClock clock, IReadOnlyList<BattleUnit> live)
        {
            if (clock.UnitResetVersion != clock.Unit.CombatResetVersion)
            {
                clock.LockedTarget = null;
                clock.UnitResetVersion = clock.Unit.CombatResetVersion;
            }
            if (!CanKeepTarget(clock)) clock.LockedTarget = null;

            if (clock.LockedTarget != null)
            {
                // Keep opponents in range; explicit taunt/focus abilities may force pursuit.
                lockedCandidate.Clear();
                lockedCandidate.Add(clock.LockedTarget);
                return CombatTargetSelector.SelectTarget(board, clock.Unit, lockedCandidate);
            }

            CombatTargetSelection acquired = CombatTargetSelector.SelectTarget(board, clock.Unit, live);
            clock.LockedTarget = acquired.Target;
            if (clock.LockedTarget != null) clock.TargetResetVersion = clock.LockedTarget.CombatResetVersion;
            return acquired;
        }

        private void SyncCombatResets()
        {
            foreach(var clock in actors)
                if(clock.Unit!=null && clock.UnitResetVersion!=clock.Unit.CombatResetVersion)
                {
                    pending.RemoveAll(p=>p.Source==clock);
                    clock.UnitResetVersion=clock.Unit.CombatResetVersion;
                    clock.LockedTarget=clock.TauntTarget=clock.FocusTarget=null;
                    clock.NextAttack=clock.NextMove=tick;
                    clock.StunUntil=clock.RootUntil=0;
                    ClearTimedSuper(clock);
                    clock.AttackSpeedMultiplier=1;AttackReset?.Invoke(clock.Unit);
                }
        }

        private void InterruptAttack(ActorClock actor)
        {
            actor.Unit.InterruptAttack();
            AttackReset?.Invoke(actor.Unit);
            pending.RemoveAll(p=>p.Source==actor && (!p.Projectile || p.ReleaseTick>tick));
            actor.NextAttack=Mathf.Max(tick+1,actor.StunUntil);
        }

        private static void ClearTimedSuper(ActorClock actor)
        {
            actor.SuperUntil = 0;
            if (actor.Unit != null) actor.Unit.ClearTimedSuperState();
        }

        private bool CanKeepTarget(ActorClock clock)
        {
            BattleUnit target = clock.LockedTarget;
            if (target == null || !target.IsAlive || target.IsReviving ||
                (documentedCombat && IsInvisible(target)) ||
                !clock.Unit.isActiveAndEnabled || !target.isActiveAndEnabled ||
                target.Side == clock.Unit.Side || target.CombatResetVersion != clock.TargetResetVersion ||
                target.CurrentCell == null || !board.IsManagedCell(target.CurrentCell)) return false;
            bool forced = documentedCombat && (clock.TauntTarget == target || clock.FocusTarget == target);
            if (!forced && !CombatTargetSelector.IsInAttackRange(clock.Unit.CurrentCell, target.CurrentCell, clock.Unit.BaseStats.AttackRange)) return false;
            // Occupancy and identity must still belong to this opponent. Anuik's
            // headstand is a deliberate exception: death/revival releases his lock.
            return ReferenceEquals(target.CurrentCell.OccupiedBy, target);
        }

        private void ResolveImpacts()
        {
            var damage = new Dictionary<BattleUnit, int>();
            var healing = new Dictionary<BattleUnit, int>();
            var resolved = new List<PendingImpact>();
            var amounts = new List<int>();
            var lethal = new List<bool>();
            // All due impacts observe life at the start of this resolution, including
            // projectiles whose source was killed after it launched them.
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingImpact impact = pending[i];
                if (impact.DueTick > tick) continue;
                pending.RemoveAt(i);
                if (impact.Target == null || !impact.Target.IsAlive || impact.Source.Unit == null) continue;
                UnitStats source = impact.Source.Unit.BaseStats;
                impact.Source.Hits++;
                // Even a zero-damage hit advances an enabled lethal charge toward a real result.
                if (source.LethalEveryHits > 0) lastChangeTick = tick;
                int bonus = source.BonusEveryHits > 0 && impact.Source.Hits % source.BonusEveryHits == 0
                    ? source.BonusDamage : 0;
                int rawDamage = impact.Damage + bonus;
                bool isLethal = source.LethalEveryHits > 0 && impact.Source.Hits % source.LethalEveryHits == 0;
                int amount = isLethal ? impact.Target.CurrentHealth :
                    rawDamage > 0 ? Mathf.Max(1, rawDamage - impact.Target.BaseStats.Armor) : 0;
                damage.TryGetValue(impact.Target, out int previous);
                damage[impact.Target] = previous + amount;
                if (amount > 0 && source.HealOnHit > 0)
                {
                    healing.TryGetValue(impact.Source.Unit, out int heal);
                    healing[impact.Source.Unit] = heal + source.HealOnHit;
                }
                resolved.Add(impact);
                amounts.Add(amount);
                lethal.Add(isLethal);
            }
            foreach (KeyValuePair<BattleUnit, int> hit in damage)
                if (hit.Key.ApplyDamage(hit.Value) > 0) lastChangeTick = tick;
            foreach (KeyValuePair<BattleUnit, int> heal in healing)
                { int restored=heal.Key.Heal(heal.Value);if(restored>0){lastChangeTick=tick;Healed?.Invoke(heal.Key,restored);} }
            for (int i = 0; i < resolved.Count; i++)
            {
                Impacted?.Invoke(resolved[i].Source.Unit, resolved[i].Target, amounts[i]);
                if (lethal[i]) LethalImpacted?.Invoke(resolved[i].Source.Unit, resolved[i].Target);
            }
            for (int i = 0; i < actors.Count; i++)
            {
                BattleUnit unit = actors[i].Unit;
                if (unit == null || unit.IsAlive || unit.IsReviving || unit.CurrentCell == null) continue;
                actors[i].LockedTarget = null;
                actors[i].LastCell = unit.CurrentCell;
                // Clear every pursuer immediately, including those whose cooldown
                // lasts longer than the revival, so Anuik's return cannot restore it.
                for (int j = 0; j < actors.Count; j++)
                    if (actors[j].LockedTarget == unit) actors[j].LockedTarget = null;
                if (actors[i].RevivesUsed < unit.BaseStats.RevivesPerCombat)
                {
                    actors[i].RevivesUsed++;
                    int duration = ToTicks(unit.BaseStats.ReviveDelay);
                    actors[i].ReviveDueTick = tick + duration;
                    board.CancelReservation(unit);
                    unit.BeginReviving();
                    lastChangeTick = tick;
                    Reviving?.Invoke(unit, duration * TickDuration);
                    continue;
                }
                board.ReleaseUnit(unit);
                Died?.Invoke(unit);
            }
        }

        private bool HasUsefulPendingImpact()
        {
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].Target != null && pending[i].Target.IsAlive &&
                    pending[i].Source.Unit != null &&
                    (pending[i].Damage + pending[i].Source.Unit.BaseStats.BonusDamage > 0 ||
                        pending[i].Source.Unit.BaseStats.LethalEveryHits > 0))
                    return true;
            return false;
        }

        private bool EvaluateTeams()
        {
            bool blueAlive = false, redAlive = false;
            bool awaitingRevival = false;
            for (int i = 0; i < actors.Count; i++)
            {
                BattleUnit unit = actors[i].Unit;
                if (unit == null || (!unit.IsAlive && !unit.IsReviving)) continue;
                awaitingRevival |= unit.IsReviving;
                // Only deployed and combat-only summoned units participate; never economy storage.
                if (unit.Side == BoardSide.Blue) blueAlive = true;
                else redAlive = true;
            }
            if (blueAlive && redAlive) return false;
            // Complete the promised return before displaying a victor still lying at zero HP.
            if (awaitingRevival) return false;
            if (!blueAlive && !redAlive)
                Finish(null, "Eliminación simultánea de ambos equipos");
            else Finish(blueAlive ? BoardSide.Blue : BoardSide.Red, "El equipo rival perdió todas sus unidades");
            return true;
        }

        private void Finish(BoardSide? winner, string reason)
        {
            if (documentedCombat) EndDocumentedCombat();
            Winner = winner;
            Reason = reason;
            Finished = true;
            // Team elimination closes the round after all impacts due in this tick.
            // Future projectiles cannot change a completed result or enter the next round.
            pending.Clear();
            for (int i = 0; i < actors.Count; i++)
            {
                actors[i].LockedTarget = null;
                if (actors[i].Unit == null) continue;
                ClearTimedSuper(actors[i]);
                board.CancelReservation(actors[i].Unit);
                if (actors[i].Unit.IsReviving)
                {
                    // A hard timeout cannot leave a zero-HP non-reviving occupant on the board.
                    board.ReleaseUnit(actors[i].Unit);
                    actors[i].Unit.CancelRevival();
                    Died?.Invoke(actors[i].Unit);
                    continue;
                }
                actors[i].Unit.CancelRevival();
            }
        }

        public bool IsNextHitLethal(BattleUnit unit)
        {
            if (unit == null || unit.BaseStats.LethalEveryHits <= 0) return false;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Unit == unit) return (actors[i].Hits + 1) % unit.BaseStats.LethalEveryHits == 0;
            return false;
        }

        private void CompleteRevivals()
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ActorClock clock = actors[i];
                if (clock.Unit == null || !clock.Unit.IsReviving || tick < clock.ReviveDueTick) continue;
                clock.Unit.CompleteRevival(documentedCombat && clock.RevivesUsed > 1 ? V(clock,"anuik-second-revive","healthPercent",25)/100f : -1);
                clock.LockedTarget = null;
                clock.NextAttack = clock.NextMove = tick + 1;
                lastChangeTick = tick;
                Revived?.Invoke(clock.Unit);
                if (documentedCombat) OnDocumentedRevival(clock);
            }
        }

        private bool HasUsefulPendingAbility()
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ActorClock clock = actors[i];
                if (clock.Unit == null) continue;
                if (clock.Unit.IsReviving) return true;
                if (CanSummon(clock) && FindSummonCell(clock.Unit) != null) return true;
            }
            return false;
        }

        private bool CanSummon(ActorClock clock)
        {
            if (!clock.Unit.IsAlive || clock.Unit.BaseStats.SummonInterval <= 0 ||
                clock.Unit.BaseStats.MaxLivingSummons <= 0) return false;
            int count = 0;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Summoner == clock.Unit && actors[i].Unit != null &&
                    (actors[i].Unit.IsAlive || actors[i].Unit.IsReviving)) count++;
            return count < clock.Unit.BaseStats.MaxLivingSummons;
        }

        private BoardCell FindSummonCell(BattleUnit summoner)
        {
            if (summoner.CurrentCell == null) return null;
            // Adjacent orthogonal spawn, toward the enemy first, then left/right/back.
            int forward = summoner.Side == BoardSide.Blue ? -1 : 1;
            int[] dx = { 0, -1, 1, 0 };
            int[] dy = { forward, 0, 0, -forward };
            for (int i = 0; i < dx.Length; i++)
            {
                BoardCell cell = board.GetCell(summoner.CurrentCell.X + dx[i], summoner.CurrentCell.Y + dy[i]);
                if (cell != null && !cell.IsBlocked && cell.OccupiedBy == null && cell.ReservedBy == null) return cell;
            }
            return null;
        }

        private void ResolveSummons()
        {
            int existingActors = actors.Count;
            for (int i = 0; i < existingActors; i++)
            {
                ActorClock clock = actors[i];
                if (clock.Unit == null || tick < clock.NextSummon || !CanSummon(clock)) continue;
                BoardCell destination = FindSummonCell(clock.Unit);
                if (destination == null) continue; // Retry when a neighbor opens; no missing summon or occupied cell.
                UnitDefinition definition = ExpandedRoster.CreateSummonedDummy(clock.Unit.BaseStats);
                string id;
                do { id = clock.Unit.UnitId + "/summon-" + (++clock.SummonSequence); }
                while (actors.Exists(item => item.Unit != null && item.Unit.UnitId == id));
                var go = new GameObject(id);
                go.transform.SetParent(board.transform, false);
                BattleUnit unit = go.AddComponent<WhelpUnit>();
                unit.Initialize(id, clock.Unit.Side, UnitStats.FromDefinition(definition), true);
                if (!board.TryOccupyCell(unit, destination))
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                    else UnityEngine.Object.DestroyImmediate(go);
                    continue;
                }
                combatOnlyUnits.Add(unit);
                actors.Add(new ActorClock { Unit = unit, LastCell = destination, Summoner = clock.Unit, UnitResetVersion = unit.CombatResetVersion,
                    NextAttack = tick + 1, NextMove = tick + 1, NextSummon = int.MaxValue });
                clock.NextSummon = tick + ToTicks(clock.Unit.BaseStats.SummonInterval);
                lastChangeTick = tick;
                Summoned?.Invoke(unit, definition);
            }
        }

        private static int ToTicks(float seconds)
        {
            return Mathf.Max(1, Mathf.CeilToInt(seconds / TickDuration - 0.00001f));
        }

        public static float GetAttackWindup(BattleUnit actor)
        {
            if (IsInstantRay(actor)) return 0;
            return actor == null ? TickDuration : ToTicks(actor.BaseStats.AttackWindup) * TickDuration;
        }

        public static float GetProjectileTravelTime(BattleUnit actor, BattleUnit target)
        {
            if (actor == null || target == null || IsInstantRay(actor) || actor.BaseStats.AttackRange <= 1 ||
                actor.CurrentCell == null || target.CurrentCell == null) return 0;
            int distance = Mathf.Max(Mathf.Abs(actor.CurrentCell.X - target.CurrentCell.X),
                Mathf.Abs(actor.CurrentCell.Y - target.CurrentCell.Y));
            return ToTicks(distance / actor.BaseStats.ProjectileSpeed) * TickDuration;
        }

        public static float GetImpactDelay(BattleUnit actor, BattleUnit target)
        {
            if (IsInstantRay(actor)) return 0;
            return GetAttackWindup(actor) + GetProjectileTravelTime(actor, target);
        }
        public static bool IsInstantRay(BattleUnit actor) => actor != null && actor.BaseStats.UsesDocumentedRules && actor.BaseStats.DefinitionId == "sepora";
    }
}

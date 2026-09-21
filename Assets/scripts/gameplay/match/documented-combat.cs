using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // PDF mechanics use the same authoritative 100ms clock and captured projectile targets.
    // Numeric choices omitted by the documents are listed in documented-combat-tuning.md.
    public sealed partial class CombatSimulation
    {
        private bool documentedCombat;
        private sealed class Burn { public ActorClock Source; public BattleUnit Target; public int Next, Remaining = 4; }
        private sealed class TileEffect
        {
            public ActorClock Source; public BoardCell Cell; public string Kind;
            public int Until = int.MaxValue; public BattleUnit Captured, InitialOccupant;
        }
        public sealed class CombatGroundEffect
        {
            public BoardCell Cell { get; internal set; }
            public string Kind { get; internal set; }
            public BoardSide Side { get; internal set; }
            public float RemainingSeconds { get; internal set; }
        }
        public IReadOnlyList<CombatGroundEffect> GroundEffects => tiles.Select(t => new CombatGroundEffect { Cell = t.Cell, Kind = t.Kind, Side = t.Source.Unit.Side, RemainingSeconds = t.Until == int.MaxValue ? float.PositiveInfinity : Mathf.Max(0, (t.Until - tick) * TickDuration) }).ToList();
        private readonly List<Burn> burns = new List<Burn>();
        private readonly List<TileEffect> tiles = new List<TileEffect>();
        private sealed class RollingBoulder
        {
            public ActorClock Source; public List<BoardCell> Path; public int Index, NextTick, Damage, StepTicks;
            public readonly HashSet<BattleUnit> Hit = new HashSet<BattleUnit>();
        }
        private readonly List<RollingBoulder> boulders = new List<RollingBoulder>();
        public event Action<BattleUnit, string> AbilityUsed;
        public event Action<BattleUnit, BoardCell, BoardCell, float> BoulderRolled;
        public event Action<BattleUnit, int> PermanentDamageEarned;
        public event Action<BattleUnit> PositionMarked;
        public event Action<BoardSide, int> CurrencyEarned;
        public event Action<BoardSide, int> CurrencyStolen;

        private ActorClock Clock(BattleUnit unit) => actors.Find(a => a.Unit == unit);
        private static float V(ActorClock actor,string effect,string key,float fallback) => actor?.Unit?.BaseStats.AbilityValue(effect,key,fallback) ?? fallback;
        private static int N(ActorClock actor,string effect,string key,int fallback) => Mathf.Max(1,Mathf.RoundToInt(V(actor,effect,key,fallback)));
        private static bool Has(ActorClock actor, string effect) => actor != null && actor.Unit != null && actor.Unit.BaseStats.HasEffect(effect);
        private static string Id(ActorClock actor) => actor.Unit.BaseStats.DefinitionId;
        private static int Forward(BattleUnit unit) => unit.Side == BoardSide.Blue ? -1 : 1;
        private static int Distance(BoardCell a, BoardCell b) => a == null || b == null ? 999 : Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        private static int RingDistance(BoardCell a, BoardCell b) => a == null || b == null ? 999 : Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        private bool IsInvisible(BattleUnit unit) => unit.IsFlying || Clock(unit)?.StealthUntil > tick;
        private bool Immune(ActorClock actor) => Has(actor, "atong-control-immunity") && tick < ToTicks(V(actor, "atong-control-immunity", "duration", 5f));
        private List<BattleUnit> Enemies(ActorClock actor, bool inRange = false)
        {
            return actors.Where(a => a.Unit != null && a.Unit.IsAlive && !a.Unit.IsReviving && a.Unit.isActiveAndEnabled &&
                    a.Unit.Side != actor.Unit.Side && !a.Unit.IsFlying && a.StealthUntil <= tick && a.Unit.CurrentCell != null &&
                    (!inRange || Distance(a.Unit.CurrentCell, actor.Unit.CurrentCell) <= actor.Unit.BaseStats.AttackRange))
                .Select(a => a.Unit).OrderBy(u => RingDistance(u.CurrentCell, actor.Unit.CurrentCell)).ThenBy(u => u.UnitId, StringComparer.Ordinal).ToList();
        }
        private IEnumerable<ActorClock> Nearby(BoardCell center, BoardSide side, int radius = 1) =>
            actors.Where(a => a.Unit != null && a.Unit.IsAlive && a.Unit.Side == side && Distance(center, a.Unit.CurrentCell) <= radius).ToList();
        private void ClearDocumentedCombat() { foreach(var actor in actors)if(actor.Unit!=null)actor.Unit.EndFlight(); documentedCombat = false; burns.Clear(); tiles.Clear(); boulders.Clear(); }
        private void BeginDocumentedCombat()
        {
            documentedCombat = actors.Any(a => a.Unit.BaseStats.UsesDocumentedRules);
            if (!documentedCombat) return;
            foreach (ActorClock actor in actors)
            {
                actor.NextIncome = ToTicks(Has(actor, "blotan-fast-income") ? V(actor, "blotan-fast-income", "interval", 10f) : V(actor, "blotan-income", "interval", 20f));
                if (Has(actor, "flo-full-energy")) actor.Unit.AddEnergy(actor.Unit.BaseStats.EnergyMax);
            }
            // All opening effects observe the deployed formation before any opening movement.
            foreach (ActorClock actor in actors.ToList())
            {
                BoardCell origin = actor.Unit.CurrentCell;
                if (Has(actor, "stein-energy-gift"))
                    foreach (ActorClock ally in Nearby(origin, actor.Unit.Side).Where(a => a.Unit.CurrentCell.Y == origin.Y && Mathf.Abs(a.Unit.CurrentCell.X - origin.X) == 1))
                        ally.Unit.AddEnergy(V(actor, "stein-energy-gift", "energy", 25f));
                if (Has(actor, "tauris-mark"))
                    foreach (BattleUnit enemy in Enemies(actor).Where(u => u.CurrentCell.Y == origin.Y))
                    { actor.Unit.AddEnergy(V(actor, "tauris-mark", "energy", 25f)); PositionMarked?.Invoke(enemy); }
                if (Has(actor, "jazar-front-row"))
                    for (int x = 0; x < BoardManager.Width; x++) tiles.Add(new TileEffect { Source = actor, Cell = board.GetCell(x, actor.Unit.Side == BoardSide.Blue ? 4 : 5), Kind = "radiation", Until = ToTicks(V(actor, "jazar-front-row", "duration", 1f)) });
                if (Has(actor, "trimol-opening-boulder")) QueueBoulder(actor, null);
            }
            foreach (ActorClock actor in actors.ToList())
                if (Has(actor, "aky-opening-flight")) BeginAkyFlight(actor);
            ResolveTileEffects();
        }
        private void EndDocumentedCombat()
        {
            foreach(var actor in actors)if(actor.Unit!=null)actor.Unit.EndFlight();
            foreach (ActorClock actor in actors)
                if (actor.Unit != null && actor.Unit.IsAlive && Has(actor, "blotan-steal")) CurrencyStolen?.Invoke(actor.Unit.Side, N(actor, "blotan-steal", "coins", 2));
        }
        private void ExecuteDocumentedTick()
        {
            tick++;
            SyncCombatResets();
            CompleteRevivals();
            TickStatuses();
            ResolveTileEffects();
            var moves = new List<BoardMovementIntent>();
            var moving = new List<ActorClock>();
            foreach (ActorClock actor in actors.ToList())
            {
                BattleUnit unit = actor.Unit;
                if (unit == null || !unit.IsAlive || unit.IsFlying || actor.StunUntil > tick || actor.SleepStarted || actor.SummonKind == "flower") continue;
                // Flo retries charged summons every tick, independently of targets and cooldowns.
                if (Id(actor) == "flo" && TryEnergyAbility(actor, null)) continue;
                if (tick < actor.NextAttack && tick < actor.NextMove) continue;
                List<BattleUnit> targets = Enemies(actor);
                ForceTaunt(actor, targets);
                CombatTargetSelection selection = FollowOrAcquireTarget(actor, targets);
                if (tick >= actor.NextAttack && TryEnergyAbility(actor, selection.Target)) continue;
                if (selection.Status == CombatTargetSelectionStatus.ReadyToAttack && tick >= actor.NextAttack)
                    QueueBasicAttack(actor, selection.Target);
                else if (selection.Status == CombatTargetSelectionStatus.MoveRequested && tick >= actor.NextMove && actor.RootUntil <= tick)
                { moves.Add(new BoardMovementIntent(unit, selection.AttackCell)); moving.Add(actor); }
                else unit.SetState(UnitState.Idle);
            }
            List<BoardMovementResult> results = BoardMovementResolver.ResolveMovement(board, moves);
            for (int i = 0; i < results.Count; i++)
            {
                ActorClock actor = moving[i]; actor.NextMove = tick + ToTicks(actor.Unit.BaseStats.MoveInterval / (1 + actor.SpeedBonus));
                if (results[i].Status != BoardMovementStatus.Moved) continue;
                actor.NextAttack = Mathf.Max(actor.NextAttack, actor.NextMove);
                BoardCell from = actor.LastCell; actor.LastCell = actor.Unit.CurrentCell;
                actor.Unit.SetState(UnitState.Moving); lastChangeTick = tick; Moved?.Invoke(actor.Unit, from, actor.LastCell);
            }
            ResolveTileEffects();
            ResolveBoulders();
            ResolveDocumentedImpacts();
            ResolveDocumentedDeaths();
            if (EvaluateTeams()) return;
            if (tick >= TimeoutTicks) Finish(null, "Tiempo de combate agotado");
            else if (tick - lastChangeTick >= StalemateTicks && !HasUsefulPendingImpact() &&
                !actors.Any(a => a.Unit.IsReviving || a.Unit.IsFlying || (a.Unit.IsAlive && (a.Unit.BaseStats.EnergyPerSecond > 0 || a.StunUntil > tick || a.StealthUntil > tick))) && burns.Count == 0 && boulders.Count == 0)
                Finish(null, "Sin progreso durante 10 segundos");
        }
        private void ForceTaunt(ActorClock actor, List<BattleUnit> targets)
        {
            if (Immune(actor)) return;
            BattleUnit taunter = targets.FirstOrDefault(u => Has(Clock(u), "bugaloo-reflect") && Clock(u).SuperUntil > tick && Distance(actor.Unit.CurrentCell, u.CurrentCell) <= V(Clock(u), "bugaloo-reflect", "radius", 2f));
            if (taunter != null)
            {
                if (actor.TauntTarget == null) actor.TargetBeforeTaunt = actor.LockedTarget;
                actor.TauntTarget = taunter; SetTarget(actor, taunter);
            }
            else if (actor.TauntTarget != null)
            {
                actor.LockedTarget = actor.TargetBeforeTaunt;
                if (actor.LockedTarget != null) actor.TargetResetVersion = actor.LockedTarget.CombatResetVersion;
                actor.TauntTarget = actor.TargetBeforeTaunt = null;
            }
        }
        private static void SetTarget(ActorClock actor, BattleUnit target)
        { actor.LockedTarget = target; actor.TargetResetVersion = target.CombatResetVersion; }
        private void QueueImpact(ActorClock actor, BattleUnit target, int damage, string effect = null, bool super = false, int delay = -1, bool critical = false)
        {
            if (target == null) return;
            pending.Add(new PendingImpact { Source = actor, Target = target, Damage = damage, Effect = effect, Super = super, Critical = critical,
                ReleaseTick = tick + (IsInstantRay(actor.Unit) ? 0 : ToTicks(GetAttackWindup(actor.Unit))), Projectile = actor.Unit.BaseStats.AttackRange > 1,
                DueTick = tick + (IsInstantRay(actor.Unit) ? 0 : delay < 0 ? ToTicks(GetImpactDelay(actor.Unit, target)) : Mathf.Max(1, delay)) });
            Attacked?.Invoke(actor.Unit, target, actor.Unit.BaseStats.AttackRange > 1);
        }
        private int BasicDamage(ActorClock actor) => Mathf.Max(0, Mathf.RoundToInt((actor.Unit.BaseStats.Attack + actor.DamageBonus) * actor.DamageMultiplier));
        private void QueueBasicAttack(ActorClock actor, BattleUnit target)
        {
            actor.LastBasicTick = tick;
            actor.Attacks++;
            int damage = BasicDamage(actor) + actor.NextBasicBonus; actor.NextBasicBonus = 0;
            string id = Id(actor), effect = id;
            bool critical=id == "atong" && (Has(actor, "atong-always-critical") || actor.Attacks % N(actor, "atong-critical", "every", 2) == 0);
            if (critical)
                damage = Mathf.RoundToInt(damage * (Has(actor, "atong-critical-damage") ? V(actor, "atong-critical-damage", "damagePercent", 200f)/100f : V(actor, "atong-critical", "damagePercent", 150f)/100f));
            if (id == "trimol" && actor.Attacks % N(actor, "trimol-boulder", "every", 4) == 0) QueueBoulder(actor, target);
            else if (id == "stein" || id == "sepora")
            {
                bool stormActive = id == "sepora" && actor.SuperUntil > tick;
                int count = id == "stein" ? (Has(actor, "stein-triple") ? N(actor, "stein-triple", "targets", 3) : N(actor, "stein-stun", "targets", 2)) :
                    stormActive ? (Has(actor, "sepora-multishot") ? N(actor, "sepora-multishot", "superTargets", 5) : N(actor, "sepora-storm", "targets", 3)) : (Has(actor, "sepora-multishot") ? N(actor, "sepora-multishot", "targets", 2) : 1);
                QueueVolley(actor, target, count, damage, effect, stormActive);
            }
            else QueueImpact(actor, target, damage, id == "tsu" && actor.Attacks % N(actor, "tsu-root", "every", 4) == 0 ? "tsu-gum" : effect, critical:critical);
            if (id == "tsu" && actor.Attacks % N(actor, "tsu-root", "every", 4) == 0)
            { AbilityUsed?.Invoke(actor.Unit, "tsu-root"); if (Has(actor, "tsu-colored-gum")) ColoredGum(actor); }
            actor.Unit.AddEnergy(actor.Unit.BaseStats.EnergyPerAttack);
            float rhythm = id == "bugui" ? (Has(actor, "bugui-unlimited-stacks") ? actor.Attacks * V(actor, "bugui-unlimited-stacks", "speedPercent", 2.5f)/100f : Mathf.Min(actor.Attacks, N(actor, "bugui-rhythm", "stacks", 10)) * V(actor, "bugui-rhythm", "speedPercent", 5f)/100f) : 0;
            float storm = id == "sepora" && actor.SuperUntil > tick ? 1 + V(actor, "sepora-storm", "speedPercent", 50f)/100f : 1;
            float speed = (1 + actor.SpeedBonus + rhythm) * actor.AttackSpeedMultiplier * storm;
            actor.NextAttack = tick + Mathf.Max(ToTicks(GetAttackWindup(actor.Unit)) + 1, ToTicks(actor.Unit.BaseStats.AttackInterval / speed));
            actor.NextMove = Mathf.Max(actor.NextMove, tick + ToTicks(GetAttackWindup(actor.Unit)) + 1);
            actor.Unit.SetState(UnitState.Attacking);
        }
        private void QueueVolley(ActorClock actor, BattleUnit primary, int count, int damage, string effect, bool super = false)
        {
            List<BattleUnit> targets = Enemies(actor, true); targets.Remove(primary); targets.Insert(0, primary);
            if (Id(actor) == "sepora") count = Mathf.Min(count, targets.Count);
            for (int i = 0; i < count; i++) QueueImpact(actor, targets[i % targets.Count], damage, effect, super);
        }
        private bool TryEnergyAbility(ActorClock actor, BattleUnit target)
        {
            BattleUnit unit = actor.Unit;
            if (unit.IsEnergyLocked || unit.BaseStats.EnergyMax <= 0 || unit.Energy + .001f < unit.BaseStats.EnergyMax) return false;
            string id = Id(actor);
            if (id == "flo")
            {
                BoardCell front = board.GetCell(unit.CurrentCell.X, unit.CurrentCell.Y + Forward(unit));
                if (!Free(front)) front = board.GetAllCells().Where(Free)
                    .OrderBy(c => RingDistance(unit.CurrentCell,c)).ThenBy(c => Distance(unit.CurrentCell,c))
                    .ThenBy(c => c.Y * -Forward(unit)).ThenBy(c => c.X).FirstOrDefault();
                if (front == null) return false;
                Spawn(actor, front, "flower");
            }
            else if (id == "kayon")
            {
                int spawned = 0, count = Has(actor, "kayon-double-summon") ? N(actor, "kayon-double-summon", "count", 2) : N(actor, "kayon-summon", "count", 1);
                for (int i = 0; i < count; i++) { BoardCell cell = FindSummonCell(unit); if (cell == null) break; Spawn(actor, cell, "dummy"); spawned++; }
                if (spawned == 0) return false;
            }
            else if (id == "bugaloo")
            {
                actor.SuperUntil = tick + ToTicks(V(actor, "bugaloo-reflect", "duration", 3f));
                unit.IsEnergyLocked = true;
                unit.IsReflecting = Has(actor, "bugaloo-reflect");
                unit.IsProtected = Has(actor, "bugaloo-protection");
            }
            else if (target == null || !target.IsAlive) return false;
            else if (id == "popow") Dash(actor);
            else if (id == "sepora")
            {
                if (Distance(unit.CurrentCell, target.CurrentCell) > unit.BaseStats.AttackRange) return false;
                actor.SuperUntil = tick + ToTicks(V(actor, "sepora-storm", "duration", 5f)); unit.IsEnergyLocked = true; actor.LastBasicTick = tick;
                QueueVolley(actor, target, Has(actor, "sepora-multishot") ? N(actor, "sepora-multishot", "superTargets", 5) : N(actor, "sepora-storm", "targets", 3), BasicDamage(actor), "sepora", true);
            }
            else if (id == "tauris" || id == "tokoro" || id == "hymay" || id == "gochan")
            {
                if (Distance(unit.CurrentCell, target.CurrentCell) > unit.BaseStats.AttackRange) return false;
                QueueImpact(actor, target, id == "gochan" ? Mathf.RoundToInt(V(actor, "gochan-seal-burst", "damage", 20f)) : Mathf.RoundToInt(BasicDamage(actor) * (id == "tokoro" ? V(actor, "tokoro-headbutt", "multiplier", 2f) : 1)), id + "-super", true);
            }
            else return false;
            unit.ClearEnergy(); actor.NextAttack = tick + ToTicks(GetAttackWindup(unit)) + 1;
            if (id == "sepora") actor.NextAttack = tick + ToTicks(unit.BaseStats.AttackInterval / ((1 + actor.SpeedBonus) * actor.AttackSpeedMultiplier * (1 + V(actor, "sepora-storm", "speedPercent", 50f)/100f)));
            actor.NextMove = Mathf.Max(actor.NextMove, actor.NextAttack);
            AbilityUsed?.Invoke(unit, unit.BaseStats.DefinitionId + "-super"); lastChangeTick = tick; return true;
        }
        private static bool Free(BoardCell cell) => cell != null && !cell.IsBlocked && cell.OccupiedBy == null && cell.ReservedBy == null;
        private bool Reposition(ActorClock actor, BoardCell cell, bool forced = false)
        {
            if (actor.Unit.CurrentCell == null || !Free(cell) || (forced && (actor.Unit.IsFlying || Immune(actor) || actor.RootUntil > tick))) return false;
            BoardCell from = actor.Unit.CurrentCell;
            if (!board.TryRepositionUnit(actor.Unit, cell)) return false;
            if(forced)InterruptAttack(actor);
            actor.LastCell = cell; actor.NextAttack = Mathf.Max(actor.NextAttack, tick + 1);
            lastChangeTick = tick; Moved?.Invoke(actor.Unit, from, cell); return true;
        }
        private void MoveAlongColumn(ActorClock actor, int amount)
        {
            BoardCell origin = actor.Unit.CurrentCell;
            int end = Mathf.Clamp(origin.Y + amount, 0, BoardManager.Height - 1), direction = amount < 0 ? -1 : 1;
            for (int y = end; y != origin.Y; y -= direction)
                if (Reposition(actor, board.GetCell(origin.X, y))) break;
        }
        private List<BoardCell> Line(BoardCell from, BoardCell to, int maxDistance)
        {
            var result = new List<BoardCell>();
            int dx = to.X - from.X, dy = to.Y - from.Y, steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            if (steps == 0) return result;
            for (int i = 1; i <= maxDistance; i++)
            {
                BoardCell cell = board.GetCell(from.X + Mathf.RoundToInt(dx * i / (float)steps), from.Y + Mathf.RoundToInt(dy * i / (float)steps));
                if (cell == null) break; if (!result.Contains(cell)) result.Add(cell);
            }
            return result;
        }
        private void Dash(ActorClock actor)
        {
            BattleUnit farthest = Enemies(actor).OrderByDescending(u => Distance(actor.Unit.CurrentCell, u.CurrentCell)).ThenBy(u => u.UnitId, StringComparer.Ordinal).FirstOrDefault();
            if (farthest == null) return;
            List<BoardCell> line = Line(actor.Unit.CurrentCell, farthest.CurrentCell, Distance(actor.Unit.CurrentCell, farthest.CurrentCell));
            int hit = 0;
            foreach (BoardCell cell in line)
                if (cell.OccupiedBy is BattleUnit victim && victim.Side != actor.Unit.Side && victim.IsAlive)
                { Stun(Clock(victim), Has(actor, "popow-dash-stun") ? ToTicks(V(actor, "popow-dash-stun", "duration", 2f)) : ToTicks(V(actor, "popow-dash", "duration", 1f))); hit++; }
            if (Has(actor, "popow-dash-charge")) actor.NextBasicBonus += Mathf.RoundToInt(hit * V(actor, "popow-dash-charge", "damage", 3f));
            for (int i = line.Count - 1; i >= 0; i--) if (Reposition(actor, line[i])) break;
        }
        private void QueueBoulder(ActorClock actor, BattleUnit target)
        {
            BoardCell origin = actor.Unit.CurrentCell;
            BoardCell end = target?.CurrentCell ?? board.GetCell(origin.X, actor.Unit.Side == BoardSide.Blue ? 0 : 9);
            if (end == origin) return;
            List<BoardCell> path = Line(origin, end, Has(actor, "trimol-infinite-boulder") ? 20 : actor.Unit.BaseStats.AttackRange);
            int stepTicks=ToTicks(1f/actor.Unit.BaseStats.ProjectileSpeed);
            if (path.Count > 0) BoulderRolled?.Invoke(actor.Unit, origin, path[path.Count-1], path.Count * stepTicks * TickDuration);
            boulders.Add(new RollingBoulder { Source = actor, Path = path, NextTick = tick + stepTicks, StepTicks=stepTicks,
                Damage = Mathf.RoundToInt(BasicDamage(actor) * (Has(actor, "trimol-brutal-boulder") ? V(actor, "trimol-brutal-boulder", "multiplier", 3f) : V(actor, "trimol-boulder", "multiplier", 2f))) });
            AbilityUsed?.Invoke(actor.Unit, "trimol-boulder");
        }
        private void ResolveBoulders()
        {
            foreach (var rock in boulders.ToList())
            {
                if (tick < rock.NextTick) continue;
                if (rock.Index < rock.Path.Count)
                {
                    var enemy = rock.Path[rock.Index++].OccupiedBy as BattleUnit;
                    if (enemy != null && enemy.IsAlive && enemy.Side != rock.Source.Unit.Side && rock.Hit.Add(enemy))
                        pending.Add(new PendingImpact { Source = rock.Source, Target = enemy, Damage = rock.Damage, Effect = "boulder", DueTick = tick });
                }
                rock.NextTick+=rock.StepTicks;
                if (rock.Index >= rock.Path.Count) boulders.Remove(rock);
            }
        }
        public static IReadOnlyList<Vector2Int> SweepCells(Vector2Int source, Vector2Int target)
        {
            Vector2Int[] ring = { new Vector2Int(0,-1), new Vector2Int(1,-1), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1), new Vector2Int(-1,0), new Vector2Int(-1,-1) };
            var direction = new Vector2Int(Math.Sign(target.x-source.x), Math.Sign(target.y-source.y));
            int index = Array.IndexOf(ring, direction);
            if (index < 0) return Array.Empty<Vector2Int>();
            return new[] { source + ring[index], source + ring[(index+7)%8], source + ring[(index+1)%8] };
        }
        private List<BattleUnit> ImpactTargets(PendingImpact impact)
        {
            ActorClock actor = impact.Source; BattleUnit target = impact.Target;
            var result = new List<BattleUnit> { target };
            bool square = impact.Effect == "gochan-super" || (impact.Effect == "gochan" && Has(actor, "gochan-expanded-seal"));
            bool cross = (impact.Effect == "gochan" && !square) || (impact.Effect == "jazar" && Has(actor, "jazar-cross"));
            bool sweep = (impact.Effect == "popow" && Has(actor, "popow-sweep")) || (impact.Effect == "aky" && Has(actor, "aky-sweep")) || (impact.Effect == "tauris-super" && Has(actor, "tauris-sweep"));
            if (square || cross)
                foreach (ActorClock other in actors)
                {
                    BoardCell cell = other.Unit.CurrentCell;
                    if (other.Unit.IsAlive && other.Unit.Side != actor.Unit.Side && Distance(target.CurrentCell, cell) <= 1 &&
                        (square || cell.X == target.CurrentCell.X || cell.Y == target.CurrentCell.Y) && !result.Contains(other.Unit)) result.Add(other.Unit);
                }
            if (sweep && actor.Unit.CurrentCell != null && target.CurrentCell != null)
                foreach (Vector2Int position in SweepCells(actor.Unit.CurrentCell.Coordinates, target.CurrentCell.Coordinates))
                    if (board.GetCell(position.x, position.y)?.OccupiedBy is BattleUnit victim && victim.IsAlive && victim.Side != actor.Unit.Side && !result.Contains(victim)) result.Add(victim);
            return result;
        }
        private void ResolveDocumentedImpacts()
        {
            var damage = new Dictionary<BattleUnit, int>();
            var killers = new Dictionary<BattleUnit, ActorClock>();
            var events = new List<Tuple<PendingImpact, BattleUnit, int, bool>>();
            foreach (PendingImpact impact in pending.Where(p => p.DueTick <= tick).OrderBy(p => p.DueTick).ThenBy(p => p.Source.Unit.UnitId, StringComparer.Ordinal).ToList())
            {
                pending.Remove(impact);
                if (impact.Target == null || !impact.Target.IsAlive || impact.Target.IsReviving || impact.Source.Unit == null) continue;
                foreach (BattleUnit victim in ImpactTargets(impact))
                {
                    ActorClock defender = Clock(victim);
                    if (defender == null || victim.IsFlying || defender.InvulnerableUntil > tick) continue;
                    bool lethal = impact.Effect == "tauris-super" && !Has(impact.Source, "tauris-hunger");
                    int raw = lethal ? victim.CurrentHealth : impact.Effect == "tauris-super" ? Mathf.CeilToInt(victim.CurrentHealth * V(impact.Source, "tauris-hunger", "healthPercent", 50f)/100f) : impact.Damage;
                    int amount = lethal ? victim.CurrentHealth + victim.Shield : Incoming(defender, raw);
                    damage.TryGetValue(victim, out int previous); damage[victim] = previous + amount; killers[victim] = impact.Source;
                    if (Has(defender, "bugaloo-reflect") && defender.SuperUntil > tick && raw > 0 && impact.Source.Unit.IsAlive)
                    {
                        int reflected = Mathf.RoundToInt(raw * (Has(defender, "bugaloo-revenge") ? V(defender, "bugaloo-revenge", "reflectPercent", 150f)/100f : 1));
                        // Reflection never recursively reflects itself; its amount uses unreduced incoming damage.
                        damage.TryGetValue(impact.Source.Unit, out int old); damage[impact.Source.Unit] = old + reflected; killers[impact.Source.Unit] = defender;
                    }
                    ApplyHitEffects(impact, defender);
                    events.Add(Tuple.Create(impact, victim, amount, lethal));
                }
            }
            // Apply all simultaneous HP changes before callbacks and elimination resolution.
            foreach (var hit in damage)
            {
                ActorClock defender = Clock(hit.Key);
                int amount = hit.Key.AbsorbShield(hit.Value);
                if (hit.Key.ApplyDamage(amount) > 0) { lastChangeTick = tick; defender.LastDamager = killers[hit.Key].Unit; hit.Key.AddEnergy(hit.Key.BaseStats.EnergyOnDamage); }
            }
            foreach (var hit in events)
            {
                Impacted?.Invoke(hit.Item1.Source.Unit, hit.Item2, hit.Item3);
                if(hit.Item1.Critical && hit.Item3>0)
                {
                    CriticalImpacted?.Invoke(hit.Item1.Source.Unit,hit.Item2);
                    AbilityUsed?.Invoke(hit.Item1.Source.Unit,"atong-critical");
                }
                if (hit.Item4) LethalImpacted?.Invoke(hit.Item1.Source.Unit, hit.Item2);
            }
            TriggerLowHealthStealth();
        }
        private int Incoming(ActorClock defender, int amount)
        {
            if (amount <= 0) return 0;
            float multiplier = defender.WeakUntil > tick ? defender.Weakness : 1;
            if (Has(defender, "atori-hard-head") && tick < ToTicks(V(defender, "atori-hard-head", "duration", 5f))) multiplier *= Mathf.Clamp01(1 - V(defender, "atori-hard-head", "reduction", 20f)/100f);
            if (Has(defender, "bugaloo-protection") && defender.SuperUntil > tick) multiplier *= Mathf.Clamp01(1 - V(defender, "bugaloo-protection", "reduction", 50f)/100f);
            return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier) - defender.Unit.BaseStats.Armor);
        }
        private void DirectDamage(ActorClock source, ActorClock defender, int amount)
        {
            if (source == null || defender == null || !defender.Unit.IsAlive || defender.Unit.IsFlying || defender.InvulnerableUntil > tick) return;
            int applied = defender.Unit.ApplyDamage(defender.Unit.AbsorbShield(Incoming(defender, amount)));
            if (applied > 0) { defender.LastDamager = source.Unit; lastChangeTick = tick; defender.Unit.AddEnergy(defender.Unit.BaseStats.EnergyOnDamage); }
            Impacted?.Invoke(source.Unit, defender.Unit, applied);
            if (Has(defender, "bugaloo-reflect") && defender.SuperUntil > tick && source.Unit.IsAlive && amount > 0)
            {
                int reflected = Mathf.RoundToInt(amount * (Has(defender, "bugaloo-revenge") ? V(defender, "bugaloo-revenge", "reflectPercent", 150f)/100f : 1));
                int taken = source.InvulnerableUntil > tick ? 0 : source.Unit.ApplyDamage(source.Unit.AbsorbShield(reflected));
                if (taken > 0) { source.LastDamager = defender.Unit; lastChangeTick = tick; }
                Impacted?.Invoke(defender.Unit, source.Unit, taken);
            }
        }
        private void Heal(ActorClock actor, int amount)
        {
            if (actor == null || !actor.Unit.IsAlive) return;
            float multiplier = Has(actor, "bugaloo-healing") ? 1 + V(actor, "bugaloo-healing", "bonusPercent", 50f)/100f : 1;
            if (actor.AntiHealUntil > tick) multiplier *= actor.AntiHealMultiplier;
            int restored=actor.Unit.Heal(Mathf.RoundToInt(amount * multiplier));
            if(restored>0){lastChangeTick=tick;Healed?.Invoke(actor.Unit,restored);}
        }
        private void ApplyHitEffects(PendingImpact impact, ActorClock defender)
        {
            ActorClock source = impact.Source;
            switch (impact.Effect)
            {
                case "stein": Stun(defender, Has(source, "stein-long-stun") ? ToTicks(V(source, "stein-long-stun", "duration", 0.2f)) : ToTicks(V(source, "stein-stun", "duration", 0.1f))); break;
                case "popow": if (Has(source, "popow-sweep")) Stun(defender, ToTicks(V(source, "popow-sweep", "duration", 0.5f))); break;
                case "boulder": Stun(defender, Has(source, "trimol-brutal-boulder") ? ToTicks(V(source, "trimol-brutal-boulder", "duration", 2f)) : ToTicks(V(source, "trimol-boulder", "duration", 1f))); break;
                case "jazar": Radiation(source, defender); break;
                case "gochan":
                case "gochan-super":
                    if (Has(source, "gochan-anti-heal")) { defender.AntiHealUntil = tick + ToTicks(V(source, "gochan-anti-heal", "duration", 4f)); defender.AntiHealMultiplier=Mathf.Clamp01(1-V(source, "gochan-anti-heal", "reduction", 60f)/100f); }
                    if (impact.Super && Has(source, "gochan-burn")) burns.Add(new Burn { Source = source, Target = defender.Unit, Next = tick + 10, Remaining = N(source, "gochan-burn", "duration", 4) });
                    break;
                case "aky":
                    float drained = -defender.Unit.AddEnergy(-V(source, "aky-energy-drain", "energy", 20f));
                    if (Has(source, "aky-drain-heal")) Heal(source, Mathf.RoundToInt(drained * V(source, "aky-drain-heal", "healPercent", 50f)/100f));
                    break;
                case "tsu-gum":
                    if (!Immune(defender))
                    {
                        defender.RootUntil = Mathf.Max(defender.RootUntil, tick + ToTicks(V(source, "tsu-root", "duration", 2f)));
                        if (Has(source, "tsu-chocolate")) { defender.ChocolateNext = tick + 10; defender.ChocolateStacks = 0; defender.ChocolateSource = source.Unit; }
                    }
                    break;
                case "tokoro-super": Stun(defender, ToTicks(V(source, "tokoro-headbutt", "duration", 3f))); Stun(source, ToTicks(V(source, "tokoro-headbutt", "duration", 3f))); break;
                case "hymay-super": Pull(source, defender); break;
            }
        }
        private void Stun(ActorClock actor, int duration)
        {
            if (actor == null || !actor.Unit.IsAlive || actor.Unit.IsFlying || Immune(actor)) return;
            bool starting = actor.StunUntil <= tick;
            actor.StunUntil = Mathf.Max(actor.StunUntil, tick + duration);
            actor.Unit.IsStunned = true;
            // Interrupted windups must start again; already released missiles keep flying.
            InterruptAttack(actor);
            actor.NextMove = Mathf.Max(actor.NextMove, actor.StunUntil);
            if (starting && Has(actor, "tokoro-stun-heal")) { Heal(actor, Mathf.RoundToInt(V(actor, "tokoro-stun-heal", "heal", 8f))); actor.LastHealTick = tick; }
            if (starting && Has(actor, "tokoro-ice-cream")) SpawnCones(actor);
        }
        private void Radiation(ActorClock source, ActorClock target)
        {
            float previous=target.AttackSpeedMultiplier;
            if (!Immune(target)) target.AttackSpeedMultiplier = Mathf.Min(target.AttackSpeedMultiplier, Mathf.Max(.05f, 1-(Has(source, "jazar-intense-radiation") ? V(source, "jazar-intense-radiation", "reduction", 50f) : V(source, "jazar-radiation", "reduction", 25f))/100f));
            target.Unit.IsSlowed=target.AttackSpeedMultiplier<.999f;
            if(target.AttackSpeedMultiplier<previous)AbilityUsed?.Invoke(source.Unit,"jazar-radiation");
        }
        private void Pull(ActorClock source, ActorClock target)
        {
            if (!target.Unit.IsAlive || source.Unit.CurrentCell == null) return;
            BoardCell destination = board.GetCell(source.Unit.CurrentCell.X, source.Unit.CurrentCell.Y + Forward(source.Unit));
            if (Immune(target) || (destination != target.Unit.CurrentCell && !Reposition(target, destination, true))) return;
            if (Has(source, "hymay-weakness")) { target.Weakness = 1 + V(source, "hymay-weakness", "bonusPercent", 25f)/100f; target.WeakUntil = int.MaxValue; }
            if (Has(source, "hymay-focus"))
                foreach (ActorClock ally in actors.Where(a => a.Unit.IsAlive && a.Unit.Side == source.Unit.Side && !Immune(a)))
                { ally.FocusTarget = target.Unit; SetTarget(ally, target.Unit); }
            if (Has(source, "hymay-backstep")) Reposition(source, board.GetCell(source.Unit.CurrentCell.X, source.Unit.CurrentCell.Y - Forward(source.Unit) * N(source, "hymay-backstep", "distance", 1)));
        }
        private void ColoredGum(ActorClock actor)
        {
            ActorClock ally = actors.Where(a => a != actor && a.Unit.IsAlive && a.Unit.Side == actor.Unit.Side)
                .OrderBy(a => Distance(actor.Unit.CurrentCell, a.Unit.CurrentCell)).ThenBy(a => a.Unit.UnitId, StringComparer.Ordinal).FirstOrDefault() ?? actor;
            switch (actor.GumCycle++ % 3)
            {
                case 0: ally.SpeedBonus += V(actor, "tsu-colored-gum", "bonusPercent", 20f)/100f; break;
                case 1: ally.Unit.AddShield(Mathf.CeilToInt(ally.Unit.BaseStats.MaxHealth * V(actor, "tsu-colored-gum", "shieldPercent", 10f)/100f)); break;
                case 2: ally.DamageMultiplier += V(actor, "tsu-colored-gum", "bonusPercent", 20f)/100f; break;
            }
        }
        private void TickStatuses()
        {
            foreach (ActorClock actor in actors.ToList())
            {
                BattleUnit unit = actor.Unit;
                if (unit == null || !unit.IsAlive) continue;
                unit.AdvanceFlight(TickDuration);
                if (unit.IsEnergyLocked && tick >= actor.SuperUntil)
                {
                    ClearTimedSuper(actor);
                    if (Id(actor) == "sepora")
                        actor.NextAttack = Mathf.Max(tick, actor.LastBasicTick + ToTicks(unit.BaseStats.AttackInterval / ((1 + actor.SpeedBonus) * actor.AttackSpeedMultiplier)));
                }
                else unit.AddEnergy(unit.BaseStats.EnergyPerSecond * TickDuration);
                unit.IsStunned = actor.StunUntil > tick; unit.IsRooted = actor.RootUntil > tick; unit.IsInvisible = actor.StealthUntil > tick;
                if (Has(actor, "tokoro-sleep") && tick >= TimeoutTicks - ToTicks(V(actor, "tokoro-sleep", "duration", 10f)))
                {
                    if (!actor.SleepStarted) { actor.SleepStarted = true; unit.IsSleeping = true; actor.LastHealTick = tick; Heal(actor, Mathf.RoundToInt(V(actor, "tokoro-sleep", "heal", 15f))); AbilityUsed?.Invoke(unit, "tokoro-sleep"); }
                    else if (tick - actor.LastHealTick >= 10) { Heal(actor, Mathf.RoundToInt(V(actor, "tokoro-sleep", "heal", 15f))); actor.LastHealTick = tick; }
                }
                else if (actor.StunUntil > tick && tick - actor.LastHealTick >= 10)
                {
                    if (Has(actor, "tokoro-stun-heal")) Heal(actor, Mathf.RoundToInt(V(actor, "tokoro-stun-heal", "heal", 8f)));
                    if (Has(actor, "tokoro-ice-cream")) SpawnCones(actor);
                    actor.LastHealTick = tick;
                }
                if (Has(actor, "blotan-income") && tick >= actor.NextIncome)
                { CurrencyEarned?.Invoke(unit.Side, N(actor, "blotan-income", "coins", 1)); actor.NextIncome = tick + (ToTicks(Has(actor, "blotan-fast-income") ? V(actor, "blotan-fast-income", "interval", 10f) : V(actor, "blotan-income", "interval", 20f))); AbilityUsed?.Invoke(unit, "blotan-income"); }
                if (actor.ChocolateSource != null && actor.RootUntil >= tick && tick >= actor.ChocolateNext)
                { DirectDamage(Clock(actor.ChocolateSource), actor, Mathf.RoundToInt(++actor.ChocolateStacks * V(Clock(actor.ChocolateSource), "tsu-chocolate", "damage", 2f))); actor.ChocolateNext = tick + 10; }
            }
            foreach (Burn burn in burns.ToList())
            {
                if (burn.Target == null || !burn.Target.IsAlive) { burns.Remove(burn); continue; }
                if (tick < burn.Next) continue;
                DirectDamage(burn.Source, Clock(burn.Target), Mathf.RoundToInt(V(burn.Source, "gochan-burn", "damage", 2f))); burn.Next += 10;
                if (--burn.Remaining <= 0) burns.Remove(burn);
            }
            TriggerLowHealthStealth(); ResolveDocumentedDeaths();
        }
        private void TriggerLowHealthStealth()
        {
            foreach (ActorClock actor in actors.ToList())
                if (Has(actor, "sepora-stealth") && !actor.StealthUsed && actor.Unit.IsAlive && actor.Unit.CurrentHealth < actor.Unit.BaseStats.MaxHealth * V(actor, "sepora-stealth", "threshold", 50f)/100f)
                {
                    actor.StealthUsed = true;
                    foreach (ActorClock ally in Nearby(actor.Unit.CurrentCell, actor.Unit.Side))
                    {
                        ally.StealthUntil = tick + ToTicks(V(actor, "sepora-stealth", "duration", 2f)); ally.Unit.IsInvisible = true;
                        foreach (ActorClock enemy in actors) if (enemy.LockedTarget == ally.Unit) enemy.LockedTarget = null;
                    }
                    AbilityUsed?.Invoke(actor.Unit, "sepora-stealth");
                }
        }
        private void SpawnCones(ActorClock actor)
        {
            if (actor.Unit.CurrentCell == null) return;
            foreach (BoardCell cell in board.GetAllCells().Where(c => c != actor.Unit.CurrentCell && Distance(c, actor.Unit.CurrentCell) <= 1))
            {
                tiles.RemoveAll(t => t.Cell == cell && t.Kind == "cone");
                tiles.Add(new TileEffect { Source = actor, Cell = cell, Kind = "cone", InitialOccupant = cell.OccupiedBy as BattleUnit });
            }
        }
        private void ResolveTileEffects()
        {
            foreach (TileEffect tile in tiles.ToList())
            {
                if (tick >= tile.Until || (tile.Captured != null && !tile.Captured.IsAlive)) { tiles.Remove(tile); continue; }
                BattleUnit occupant = tile.Cell.OccupiedBy as BattleUnit;
                if (occupant != tile.InitialOccupant) tile.InitialOccupant = null;
                if (occupant == null || !occupant.IsAlive || occupant.IsFlying) continue;
                ActorClock actor = Clock(occupant);
                if (tile.Kind == "radiation" && occupant.Side != tile.Source.Unit.Side) Radiation(tile.Source, actor);
                else if (tile.Kind == "trap" && occupant.Side != tile.Source.Unit.Side && tile.Captured == null && !Immune(actor))
                { tile.Captured = occupant; actor.RootUntil = int.MaxValue; occupant.IsRooted = true; }
                else if (tile.Kind == "cone")
                {
                    if (occupant == tile.InitialOccupant) continue;
                    if (occupant.Side == tile.Source.Unit.Side) Heal(actor, Mathf.RoundToInt(V(tile.Source, "tokoro-ice-cream", "heal", 10f)));
                    else { actor.Weakness = Mathf.Max(actor.Weakness, 1 + V(tile.Source, "tokoro-ice-cream", "weaknessPercent", 20f)/100f); actor.WeakUntil = Mathf.Max(actor.WeakUntil, tick + ToTicks(V(tile.Source, "tokoro-ice-cream", "duration", 3f))); }
                    tiles.Remove(tile);
                }
            }
        }
        private void Spawn(ActorClock source, BoardCell destination, string kind)
        {
            UnitDefinition definition = kind == "flower" ? new UnitDefinition { Id = "flower", DisplayName = "Flor de Auxilio", MaxHealth = Has(source, "flo-flower-invulnerability") ? N(source, "flo-flower-invulnerability", "health", 1) : N(source, "flo-flower", "health", 20), Damage = 0, AttackRange = 1, AttackInterval = 100, MoveInterval = 100, UsesDocumentedRules = true } : ExpandedRoster.CreateSummonedDummy(source.Unit.BaseStats);
            if (kind == "dummy" && Has(source, "kayon-dummy-health")) definition.MaxHealth += Mathf.RoundToInt(V(source, "kayon-dummy-health", "health", 4f));
            string id = source.Unit.UnitId + "/" + kind + "-" + (++source.SummonSequence);
            var go = new GameObject(id); go.transform.SetParent(board.transform, false);
            BattleUnit unit = go.AddComponent<WhelpUnit>(); unit.Initialize(id, source.Unit.Side, UnitStats.FromDefinition(definition), true);
            if (!board.TryOccupyCell(unit, destination))
            { if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go); return; }
            var actor = new ActorClock { Unit = unit, LastCell = destination, Summoner = source.Unit, SummonKind = kind, UnitResetVersion = unit.CombatResetVersion,
                NextAttack = tick + 1, NextMove = tick + 1, NextSummon = int.MaxValue, InvulnerableUntil = kind == "flower" && Has(source, "flo-flower-invulnerability") ? tick + ToTicks(V(source, "flo-flower-invulnerability", "duration", 1f)) : 0 };
            actors.Add(actor); combatOnlyUnits.Add(unit); lastChangeTick = tick; Summoned?.Invoke(unit, definition);
        }
        private void ResolveDocumentedDeaths()
        {
            foreach (ActorClock actor in actors.ToList())
            {
                BattleUnit unit = actor.Unit;
                if (unit == null || unit.IsAlive || unit.IsReviving || actor.DeathHandled || unit.CurrentCell == null) continue;
                ClearTimedSuper(actor);
                actor.LockedTarget = null; actor.LastCell = unit.CurrentCell;
                foreach (ActorClock enemy in actors) if (enemy.LockedTarget == unit) enemy.LockedTarget = null;
                int revives = Has(actor, "anuik-second-revive") ? 2 : unit.BaseStats.RevivesPerCombat;
                if (actor.RevivesUsed < revives)
                {
                    actor.RevivesUsed++; actor.ReviveDueTick = tick + ToTicks(unit.BaseStats.ReviveDelay);
                    unit.BeginReviving(); board.CancelReservation(unit); lastChangeTick = tick;
                    Reviving?.Invoke(unit, ToTicks(unit.BaseStats.ReviveDelay) * TickDuration); continue;
                }
                actor.DeathHandled = true;
                if (Has(actor, "tsu-death-trap")) tiles.Add(new TileEffect { Source = actor, Cell = unit.CurrentCell, Kind = "trap" });
                ActorClock owner = Clock(actor.Summoner);
                if (actor.SummonKind == "flower") HealingArea?.Invoke(unit.CurrentCell);
                if (actor.SummonKind == "flower")
                    foreach (ActorClock ally in Nearby(unit.CurrentCell, unit.Side)) Heal(ally, Has(owner, "flo-flower-heal") ? Mathf.RoundToInt(V(owner, "flo-flower-heal", "heal", 40f)) : Mathf.RoundToInt(V(owner, "flo-flower", "heal", 20f)));
                if (actor.SummonKind == "dummy" && Has(owner, "kayon-dummy-legacy"))
                    foreach (ActorClock ally in Nearby(unit.CurrentCell, unit.Side)) ally.DamageBonus += Mathf.RoundToInt(V(owner, "kayon-dummy-legacy", "damage", 1f));
                OnKill(Clock(actor.LastDamager));
                board.ReleaseUnit(unit); Died?.Invoke(unit); lastChangeTick = tick;
            }
        }
        private void OnKill(ActorClock killer)
        {
            if (killer == null) return;
            bool rewarded=false;
            int healthBefore=killer.Unit.CurrentHealth;
            float speedBefore=killer.SpeedBonus,damageBefore=killer.DamageMultiplier;
            if (Has(killer, "sepora-kill-power")) { int bonus=Mathf.RoundToInt(V(killer, "sepora-kill-power", "damage", 1f)); killer.DamageBonus+=bonus; PermanentDamageEarned?.Invoke(killer.Unit, bonus); rewarded=bonus>0; }
            if (Has(killer, "bugui-kill-power")) { int bonus=Mathf.RoundToInt(V(killer, "bugui-kill-power", "damage", 1f));killer.DamageBonus+=bonus;rewarded|=bonus>0; }
            if (Has(killer, "atori-hunt-speed")) killer.SpeedBonus += V(killer, "atori-hunt-speed", "speedPercent", 50f)/100f;
            if (Has(killer, "atori-kill-heal")) Heal(killer, Mathf.RoundToInt(V(killer, "atori-kill-heal", "heal", 20f)));
            if (Has(killer, "tauris-hunger")) { Heal(killer, killer.Unit.BaseStats.MaxHealth); killer.DamageMultiplier += V(killer, "tauris-hunger", "damagePercent", 50f)/100f; }
            if(rewarded||killer.Unit.CurrentHealth>healthBefore||killer.SpeedBonus>speedBefore||killer.DamageMultiplier>damageBefore)
                AbilityUsed?.Invoke(killer.Unit,Id(killer)+"-kill");
        }
        private void OnDocumentedRevival(ActorClock actor)
        {
            ClearTimedSuper(actor);
            actor.StunUntil = actor.RootUntil = actor.StealthUntil = 0;
            // The named Siempre de Pie return is the first revival; the optional second return has25%HP.
            if (actor.RevivesUsed != 1) return;
            BoardCell origin = actor.Unit.CurrentCell;
            if (Has(actor, "anuik-revive-jump")) MoveAlongColumn(actor, N(actor, "anuik-revive-jump", "distance", 4) * Forward(actor.Unit));
            if (Has(actor, "anuik-revive-impact"))
                foreach (BattleUnit enemy in Enemies(actor).Where(u => Distance(actor.Unit.CurrentCell, u.CurrentCell) <= 1).ToList())
                {
                    ActorClock victim = Clock(enemy); DirectDamage(actor, victim, Mathf.RoundToInt(V(actor, "anuik-revive-impact", "damage", 10f)));
                    BoardCell cell = enemy.CurrentCell, center = actor.Unit.CurrentCell;
                    Reposition(victim, board.GetCell(cell.X + Math.Sign(cell.X-center.X), cell.Y + Math.Sign(cell.Y-center.Y)), true);
                }
        }
    }
}

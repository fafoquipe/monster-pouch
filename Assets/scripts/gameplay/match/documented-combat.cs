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
            public int Until = int.MaxValue; public BattleUnit Captured;
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
        // Cells are captured by combat at resolution, including empty tiles in the area.
        public event Action<BattleUnit, string, BoardCell[]> AreaEffect;
        public event Action<BattleUnit, BoardCell, BoardCell, float> BoulderRolled;
        // The two lane segments match the visible dash even when a board edge needs a lateral landing.
        public event Action<BattleUnit, BoardCell, BoardCell, BoardCell, float, float> DashStarted;
        public event Action<BattleUnit, BoardCell, float> FlowerCastStarted;
        public event Action<BattleUnit, BattleUnit, BattleUnit, float> ProjectileBounced;
        public event Action<BattleUnit, BattleUnit, float> ReflectedMoonLaunched;
        public event Action<BattleUnit, int> MoonkensProduced;
        public event Action<BattleUnit, int> PermanentDamageEarned;
        public event Action<BattleUnit, int> PermanentHealthEarned;
        public event Action<BattleUnit> PositionMarked;
        public event Action<BoardSide, int> CurrencyEarned;
        public event Action<BoardSide, int> CurrencyStolen;

        private ActorClock Clock(BattleUnit unit)
        {
            for(int i=0;i<actors.Count;i++)if(actors[i].Unit==unit)return actors[i];
            return null;
        }
        private static float V(ActorClock actor,string effect,string key,float fallback) => actor?.Unit?.BaseStats.AbilityValue(effect,key,fallback) ?? fallback;
        private static int N(ActorClock actor,string effect,string key,int fallback) => Mathf.Max(1,Mathf.RoundToInt(V(actor,effect,key,fallback)));
        private static bool Has(ActorClock actor, string effect) => actor != null && actor.Unit != null && actor.Unit.BaseStats.HasEffect(effect);
        private static string Id(ActorClock actor) => actor.Unit.BaseStats.DefinitionId;
        private static int Forward(BattleUnit unit) => unit.Side == BoardSide.Blue ? -1 : 1;
        private static int Distance(BoardCell a, BoardCell b) => a == null || b == null ? 999 : Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        private static int RingDistance(BoardCell a, BoardCell b) => a == null || b == null ? 999 : Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        private bool IsInvisible(BattleUnit unit) => unit.IsFlying || Clock(unit)?.StealthUntil > tick;
        private int ControlImmunityUntil(ActorClock actor) => Mathf.Max(actor.RoseControlUntil,
            Has(actor, "atong-control-immunity") ? ToTicks(V(actor, "atong-control-immunity", "duration", 5f)) : 0);
        private bool Immune(ActorClock actor) => actor != null && tick < ControlImmunityUntil(actor);
        private List<BattleUnit> Enemies(ActorClock actor, bool inRange = false)
        {
            return actors.Where(a => a.Unit != null && a.Unit.IsAlive && !a.Unit.IsReviving && a.Unit.isActiveAndEnabled &&
                    a.Unit.Side != actor.Unit.Side && !a.Unit.IsFlying && a.StealthUntil <= tick && a.Unit.CurrentCell != null &&
                    (!inRange || Distance(a.Unit.CurrentCell, actor.Unit.CurrentCell) <= actor.Unit.BaseStats.AttackRange))
                .Select(a => a.Unit).OrderBy(u => CombatTargetSelector.DistanceSquared(u.CurrentCell, actor.Unit.CurrentCell)).ThenBy(u => u.UnitId, StringComparer.Ordinal).ToList();
        }
        private IEnumerable<ActorClock> Nearby(BoardCell center, BoardSide side, int radius = 1) =>
            actors.Where(a => a.Unit != null && a.Unit.IsAlive && a.Unit.Side == side && Distance(center, a.Unit.CurrentCell) <= radius).ToList();
        private void ClearDocumentedCombat() { ClearEronyaCombat(); ClearMartioFlights(); ClearControlMotions(); ClearTsuGum(); foreach(var actor in actors) { if(actor.Unit!=null)actor.Unit.EndFlight(); ClearTaunt(actor, false); } documentedCombat = false; pokrakBubbles.Clear(); ClearFishShields(); ClearHealingRoses(); burns.Clear(); tiles.Clear(); boulders.Clear(); flyingCones.Clear(); preparedBoulders.Clear(); }
        private void BeginDocumentedCombat()
        {
            documentedCombat = actors.Any(a => a.Unit.BaseStats.UsesDocumentedRules);
            if (!documentedCombat) return;
            foreach (ActorClock actor in actors)
            {
                // Set all opening locks before any ally can grant energy.
                if (Has(actor, "tokoro-sleep"))
                {
                    actor.OpeningRecoveryUntil = Mathf.Max(0, Mathf.CeilToInt(V(actor, "tokoro-sleep", "duration", 3f) / TickDuration - .00001f));
                    actor.SleepStarted = actor.OpeningRecoveryUntil > 0;
                    actor.Unit.IsSleeping = actor.SleepStarted;
                    actor.Unit.IsOpeningEnergyBlocked = actor.SleepStarted && V(actor, "tokoro-sleep", "blockEnergy", 1f) > 0;
                    if (actor.SleepStarted) AbilityUsed?.Invoke(actor.Unit, "tokoro-sleep");
                }
                actor.NextIncome = ToTicks(Has(actor, "blotan-fast-income") ? V(actor, "blotan-fast-income", "interval", 10f) : V(actor, "blotan-income", "interval", 20f));
                if (Has(actor, "flo-full-energy")) actor.Unit.AddEnergy(actor.Unit.BaseStats.EnergyMax);
            }
            // All opening effects observe the deployed formation before any opening movement.
            foreach (ActorClock actor in actors.ToList())
            {
                BoardCell origin = actor.Unit.CurrentCell;
                if (Has(actor, "stein-energy-gift"))
                    foreach (ActorClock ally in Nearby(origin, actor.Unit.Side).Where(a => a.Unit.CurrentCell.Y == origin.Y && Mathf.Abs(a.Unit.CurrentCell.X - origin.X) <= 1))
                        ally.Unit.AddEnergy(V(actor, "stein-energy-gift", "energy", 25f));
                if (Has(actor, "tauris-mark"))
                    foreach (BattleUnit enemy in Enemies(actor).Where(u => TaurisMarkGeometry.Contains(origin,
                        u.CurrentCell, actor.Unit.Side, V(actor, "tauris-mark", "range", TaurisMarkGeometry.DefaultRange))))
                    { actor.Unit.AddEnergy(V(actor, "tauris-mark", "energy", 25f)); PositionMarked?.Invoke(enemy); }
                if (Has(actor, "jazar-front-row"))
                {
                    var contaminatedRow = new List<BoardCell>();
                    int targetRow = actor.Unit.Side == BoardSide.Blue ? 4 : 5;
                    for (int x = 0; x < BoardManager.Width; x++)
                    {
                        BoardCell cell = board.GetCell(x, targetRow);
                        if (cell == null) continue;
                        tiles.Add(new TileEffect { Source = actor, Cell = cell, Kind = "radiation", Until = ToTicks(V(actor, "jazar-front-row", "duration", 3f)) });
                        contaminatedRow.Add(cell);
                    }
                    if (contaminatedRow.Count > 0) AreaEffect?.Invoke(actor.Unit, "jazar-front-row", contaminatedRow.ToArray());
                }
                if (Has(actor, "suki-opening-shield")) OpenFishShields(actor);
                if (Has(actor, "trimol-opening-boulder")) QueueBoulder(actor, null, "trimol-opening-boulder");
            }
            foreach (ActorClock actor in actors.ToList())
                if (Has(actor, "aky-opening-flight")) PrepareOpeningFlight(actor);
            ResolveTileEffects();
        }
        private void EndDocumentedCombat()
        {
            EndEronyaCombat();
            pokrakBubbles.Clear();
            ClearFishShields();
            ClearHealingRoses();
            ClearControlMotions();
            foreach(var actor in actors)
            {
                if(actor.Unit!=null)
                {
                    actor.Unit.EndFlight();
                    actor.Unit.IsOpeningEnergyBlocked = actor.Unit.IsSleeping = false;
                }
                actor.SleepStarted = false;
                actor.StunRetargetPending = false;
                ClearTaunt(actor, false);
            }
            foreach (ActorClock actor in actors)
                if (actor.Unit != null && actor.Unit.IsAlive && Has(actor, "blotan-steal")) CurrencyStolen?.Invoke(actor.Unit.Side, N(actor, "blotan-steal", "coins", 2));
        }
        private void ExecuteDocumentedTick()
        {
            tick++;
            SyncCombatResets();
            TickEronyaCombat();
            CompleteMovementSteps();
            CompleteRevivals();
            // Expiry happens before periodic damage scheduled for this same tick.
            TickFishShields();
            ReleaseDefeatedTargets();
            TickStatuses();
            TickRoseHaste();
            TickPokrakBubbles();
            TickKazuruEffects();
            ResolveControlMotions();
            ResolveTileEffects();
            ResolveFlowerCasts();
            ResolveAbilityPreparations();
            ResolveHealingRoses();
            ResolveFishFlights();
            ResolveFlyingCones();
            TrackTargetRange();
            var moves = new List<BoardMovementIntent>();
            var moving = new List<ActorClock>();
            for(int actorIndex=0,actorCount=actors.Count;actorIndex<actorCount;actorIndex++)
            {
                ActorClock actor=actors[actorIndex];
                BattleUnit unit = actor.Unit;
                if(EronyaOwns(actor)){EronyaAct(actor,moves,moving);continue;}
                AdvanceStorm(actor);
                if(unit!=null&&unit.IsAlive&&!FinishEnergySuper(actor))continue;
                // Opening projectiles, shields, teleports and timed healing above keep advancing.
                // Only ordinary actions wait; a longer individual ability never extends this window.
                if (tick <= openingWindowTicks) continue;
                if (IsTravelling(actor)) continue;
                if (unit == null || !unit.IsAlive || unit.IsPassiveTrainingTarget || unit.IsFlying || unit.IsBeingPulled || actor.DashUntilTick > tick || actor.PullUntilTick > tick || actor.StunUntil > tick || (actor.SleepStarted && V(actor, "tokoro-sleep", "blockActions", 1f) > 0) || actor.SummonKind == "flower" || actor.FlowerCastDueTick > 0 || actor.EnergyCastDue > 0 || actor.AbilityMotionUntil > tick || PreparingBoulder(actor)) continue;
                // Finish the two-ring release before a newly charged Super or another action.
                if (PreparingKazuruBurst(actor)) continue;
                // Decide the action from the energy available before that action starts.
                // A basic attack may fill the bar below, but it cannot turn itself into a Super.
                bool superReadyAtActionStart = IsEnergyAbilityReady(actor);
                // Flo retries charged summons every tick, independently of targets and cooldowns.
                if ((Id(actor) == "flo" || Id(actor) == "rosie" || Id(actor) == "suki" || Id(actor) == "pokrak" || Id(actor) == "kazuru") && tick>=actor.SuperRecoveryUntil && superReadyAtActionStart && TryEnergyAbility(actor, null)) continue;
                if (tick < actor.NextAttack && tick < actor.NextMove) continue;
                List<BattleUnit> targets = Enemies(actor);
                BattleUnit taunter = GetTauntTarget(actor);
                if (taunter != null) SetTarget(actor, taunter);
                CombatTargetSelection selection = FollowOrAcquireTarget(actor, targets);
                if (tick >= actor.NextAttack && superReadyAtActionStart && TryEnergyAbility(actor, selection.Target)) continue;
                if (selection.Status == CombatTargetSelectionStatus.ReadyToAttack && tick >= actor.NextAttack)
                    QueueBasicAttack(actor, selection.Target);
                else if (selection.Status == CombatTargetSelectionStatus.MoveRequested && tick >= actor.NextMove && actor.RootUntil <= tick)
                {
                    actor.ResumeBuguiAttack=actor.ResumeCountedAttack=false;
                    unit.AttackWindupOverride=-1;
                    moves.Add(new BoardMovementIntent(unit, selection.AttackCell)); moving.Add(actor);
                }
                else unit.SetState(UnitState.Idle);
            }
            List<BoardMovementResult> results = BoardMovementResolver.ReserveMovement(board, moves);
            for (int i = 0; i < results.Count; i++)
            {
                ActorClock actor = moving[i]; actor.NextMove = tick + (EronyaOwns(actor)?EronyaMoveTicks(actor):ToTicks(actor.Unit.BaseStats.MoveInterval / (1 + actor.SpeedBonus + actor.ExternalMoveBonus)));
                if (results[i].Status != BoardMovementStatus.Reserved) continue;
                actor.NextAttack = Mathf.Max(actor.NextAttack, actor.NextMove);
                BeginMovementStep(actor);
            }
            ResolveTileEffects();
            ResolveBoulders();
            TickMartioFlights();
            ValidateAttackReleases();
            TickGumGifts();
            ResolveDocumentedImpacts();
            EndTickEronyaCombat();
            ResolveDocumentedDeaths();
            EronyaFlushPackets();
            ResolveDocumentedDeaths();
            if (EvaluateTeams()) return;
            if (tick >= TimeoutTickLimit) ResolveTimeout();
        }
        private void ClearTaunt(ActorClock actor, bool restoreTarget)
        {
            if (restoreTarget && actor.LockedTarget == actor.TauntTarget && actor.TauntTarget != null)
            {
                actor.LockedTarget = actor.TargetBeforeTaunt;
                if (actor.LockedTarget != null) actor.TargetResetVersion = actor.LockedTarget.CombatResetVersion;
            }
            actor.TauntTarget = actor.TargetBeforeTaunt = null;
            actor.TauntUntil = 0;
            if (actor.Unit != null) actor.Unit.IsTaunted = false;
        }
        private void UpdateTaunts()
        {
            foreach (ActorClock actor in actors)
            {
                if (!ReferenceEquals(actor.TauntTarget, null) && GetTauntTarget(actor) == null)
                    ClearTaunt(actor, true);
            }
        }
        private BattleUnit GetTauntTarget(ActorClock actor)
        {
            BattleUnit target = actor.TauntTarget;
            return actor.Unit != null && actor.Unit.IsAlive && tick < actor.TauntUntil && !Immune(actor) &&
                target != null && target.IsAlive && !target.IsReviving && target.isActiveAndEnabled &&
                target.Side != actor.Unit.Side && !IsInvisible(target) &&
                target.CombatResetVersion == actor.TaunterResetVersion &&
                CombatTargetSelector.IsValidBoardPlacement(board, target) ? target : null;
        }
        private BattleUnit PrimaryAttackTarget(ActorClock actor, BattleUnit selected) => GetTauntTarget(actor) ?? selected;
        private void RedirectPreparingAttack(ActorClock actor, BattleUnit taunter)
        {
            // Only uncommitted primary attacks restart. Released projectiles, bounces,
            // ongoing dashes, summoned flowers and timed buffs keep their own lifecycle.
            var preparing = pending.Where(impact => impact.Source == actor && !impact.Secondary &&
                impact.ReleaseTick > tick && impact.Target != taunter).ToList();
            if (preparing.Count == 0) return;
            bool retrySuper = preparing.Any(impact => impact.Super) && !actor.Unit.IsEnergyLocked;
            BoardCell flowerCell = actor.FlowerCastCell;
            int flowerDue = actor.FlowerCastDueTick;
            if (retrySuper) { actor.SuperRecoveryUntil = 0; actor.NextMove = tick + 1; }
            InterruptAttack(actor);
            // Flo may already be planting while an older basic is still preparing.
            // Cancel that basic without cancelling or delaying the independent summon.
            if (flowerDue > tick)
            {
                actor.FlowerCastCell = flowerCell;
                actor.FlowerCastDueTick = flowerDue;
                actor.NextMove = flowerDue + 1;
                actor.NextAttack = Mathf.Max(flowerDue + 1, actor.SuperRecoveryUntil);
                FlowerCastStarted?.Invoke(actor.Unit, flowerCell, (flowerDue - tick) * TickDuration);
            }
            // A cancelled targeted Super did not land or release. Retry it after moving
            // into legal range; AddEnergy clamps the refund to one full energy bar.
            if (retrySuper) actor.Unit.AddEnergy(actor.Unit.BaseStats.EnergyMax);
        }
        private BoardCell[] AreaCells(BoardCell center, float radius, bool cross = false)
        {
            if (center == null) return Array.Empty<BoardCell>();
            return board.GetAllCells().Where(cell => Distance(center, cell) <= radius &&
                (!cross || cell.X == center.X || cell.Y == center.Y)).ToArray();
        }
        private void PulseTaunt(ActorClock source)
        {
            if (!Has(source, "bugaloo-reflect")) return;
            BoardCell[] cells = ResolveAbilityArea(source,"bugaloo-reflect",source.Unit.CurrentCell,
                AreaCells(source.Unit.CurrentCell, V(source, "bugaloo-reflect", "radius", 2f)));
            float duration = V(source, "bugaloo-reflect", "tauntDuration", V(source, "bugaloo-reflect", "duration", 3f));
            if (duration > 0)
                foreach (BattleUnit enemy in Enemies(source).Where(unit => cells.Contains(unit.CurrentCell)))
                {
                    ActorClock victim = Clock(enemy);
                    if(EronyaOwns(victim))
                    {EronyaApplyStatus(source,victim,EronyaStatusKind.Taunt,"bugaloo-taunt",1,ToTicks(duration));continue;}
                    if (Immune(victim)) continue;
                    if (victim.TauntUntil >= tick + ToTicks(duration) && GetTauntTarget(victim) != null) continue;
                    if (victim.TauntTarget == null) victim.TargetBeforeTaunt = victim.LockedTarget;
                    victim.TauntTarget = source.Unit;
                    victim.TaunterResetVersion = source.Unit.CombatResetVersion;
                    victim.TauntUntil = tick + ToTicks(duration);
                    victim.Unit.IsTaunted = true;
                    SetTarget(victim, source.Unit);
                    RedirectPreparingAttack(victim, source.Unit);
                }
            if (!HasEditedArea(source,"bugaloo-reflect")) AreaEffect?.Invoke(source.Unit, "bugaloo-taunt", cells);
        }
        private static void SetTarget(ActorClock actor, BattleUnit target)
        { actor.LockedTarget = target; actor.TargetResetVersion = target.CombatResetVersion; actor.TargetReactionUntil=0;actor.TargetWasInRange=false; }
        private void QueueImpact(ActorClock actor, BattleUnit target, int damage, string effect = null, bool super = false, int delay = -1, bool critical = false, bool secondary = false)
        {
            if (target == null) return;
            int preparation=super&&IsInstantRay(actor.Unit)?0:delay>=0?Mathf.Max(0,delay):AbilityTiming.Ticks(GetAttackWindup(actor.Unit));
            pending.Add(new PendingImpact { Source = actor, Target = target, Damage = damage, Effect = effect, Super = super, Critical = critical, Secondary = secondary, RequiresRange=true,
                PreciseDamage=EronyaOwns(actor)&&!super?(decimal?)EronyaRawBasicDamage(actor):null,
                SourceResetVersion=actor.Unit.CombatResetVersion,TargetResetVersion=target.CombatResetVersion,
                ReleaseTick=tick+preparation,Projectile=actor.Unit.BaseStats.AttackRange>1,
                DueTick=tick+preparation+AbilityTiming.Ticks(GetProjectileTravelTime(actor.Unit,target)) });
            PublishAttackPresentation(actor,target,effect,super);
        }
        private int BasicDamage(ActorClock actor) => Mathf.Max(0, Mathf.RoundToInt((actor.Unit.BaseStats.Attack + actor.DamageBonus + (actor.ExternalDamageUntil > tick ? actor.ExternalDamageBonus : 0)) * (actor.DamageMultiplier + (actor.ExternalDamageUntil > tick ? actor.ExternalDamageMultiplier - 1 : 0))));
        private void QueueBasicAttack(ActorClock actor, BattleUnit target)
        {
            target = PrimaryAttackTarget(actor, target);
            bool resume = Id(actor) == "bugui" && actor.ResumeBuguiAttack;
            bool counted = resume && actor.ResumeCountedAttack;
            actor.ResumeBuguiAttack=actor.ResumeCountedAttack=false;
            actor.Unit.AttackWindupOverride=actor.PrepareFirstBasic?AbilityTiming.Ticks(actor.Unit.BaseStats.AttackWindup)*TickDuration:resume?0:-1;
            actor.PrepareFirstBasic=false;actor.Unit.ActionAbilityEffect=null;
            actor.LastBasicTick = tick;
            if (!counted) actor.Attacks++;
            int damage = BasicDamage(actor) + actor.NextBasicBonus; actor.NextBasicBonus = 0;
            string id = Id(actor), effect = id;
            if(id=="popow" && Has(actor,"popow-sweep") && actor.Attacks % N(actor,"popow-sweep","every",3)==0)effect="popow-hook";
            bool critical=id == "atong" && (Has(actor, "atong-always-critical") || actor.Attacks % N(actor, "atong-critical", "every", 2) == 0);
            if (critical)
                damage = Mathf.RoundToInt(damage * (Has(actor, "atong-critical-damage") ? V(actor, "atong-critical-damage", "damagePercent", 200f)/100f : V(actor, "atong-critical", "damagePercent", 150f)/100f));
            string special=critical?"atong-critical":id=="tsu"&&actor.Attacks%N(actor,"tsu-root","every",4)==0?"tsu-root":null;
            if(special!=null)
            {
                if(AbilityTiming.HasPhases(actor.Unit.BaseStats,special))
                {
                    actor.Unit.ActionAbilityEffect=special;
                    actor.Unit.AttackWindupOverride=AbilityTiming.Cast(actor.Unit.BaseStats,special);
                    ReserveAbilityMotion(actor,special);
                }
                damage=AbilityTiming.Damage(actor.Unit.BaseStats,critical&&Has(actor,"atong-critical-damage")?"atong-critical-damage":special,damage);
            }
            if (id == "trimol" && actor.Attacks % N(actor, "trimol-boulder", "every", 4) == 0) QueueBoulder(actor, target);
            else if (id == "sepora")
            {
                bool stormActive = id == "sepora" && actor.SuperUntil > tick;
                int count = stormActive ? (Has(actor, "sepora-multishot") ? N(actor, "sepora-multishot", "superTargets", 5) : N(actor, "sepora-storm", "targets", 3)) : (Has(actor, "sepora-multishot") ? N(actor, "sepora-multishot", "targets", 2) : 1);
                QueueVolley(actor, target, count, stormActive?AbilityTiming.Damage(actor.Unit.BaseStats,"sepora-storm",damage):damage, effect, stormActive);
            }
            else if (id == "martio") QueueMartioAttack(actor, target, damage);
            else if (id == "kazuru" && Has(actor, "kazuru-ring-throw")) QueueKazuruBurst(actor, target, damage);
            else QueueImpact(actor, target, damage, id == "tsu" && actor.Attacks % N(actor, "tsu-root", "every", 4) == 0 ? "tsu-gum" : effect, critical:critical);
            if (id == "tsu" && actor.Attacks % N(actor, "tsu-root", "every", 4) == 0)
            { AbilityUsed?.Invoke(actor.Unit, "tsu-root"); if (Has(actor, "tsu-colored-gum")) ColoredGum(actor); }
            if (!EronyaOwns(actor) && id != "suki" && id != "pokrak") actor.Unit.AddEnergy(actor.Unit.BaseStats.EnergyPerAttack);
            float speed = AttackSpeed(actor);
            int normalWindup = resume ? ToTicks(actor.Unit.BaseStats.AttackWindup) : ToTicks(GetAttackWindup(actor.Unit));
            int period = Mathf.Max(normalWindup + BasicAttackTiming.TailTicks(actor.Unit), ToTicks(actor.Unit.BaseStats.AttackInterval / speed));
            if(EronyaOwns(actor))
            {
                // Basic preparation/tails have their own cancellable lock. They must
                // never masquerade as an independently committed ability recovery.
                actor.EronyaBasicMoveReadyTick=actor.NextMove;
                actor.EronyaBasicMotionUntil=tick+normalWindup+BasicAttackTiming.TailTicks(actor.Unit);
            }
            // The resumed action releases now; the following full preparation fits
            // inside its normal period so successive releases retain the same cadence.
            int burst = id == "kazuru" ? KazuruBurstTicks(actor.Unit) : 0;
            actor.NextAttack = tick + burst + (resume ? period - normalWindup : period);
            actor.NextMove = Mathf.Max(actor.NextMove, tick + burst + (resume ? 0 : normalWindup) + BasicAttackTiming.TailTicks(actor.Unit));
            actor.NextAttack=Mathf.Max(actor.NextAttack,actor.AbilityMotionUntil,actor.EronyaBasicMotionUntil);
            actor.NextMove=Mathf.Max(actor.NextMove,actor.AbilityMotionUntil,actor.EronyaBasicMotionUntil);
            actor.Unit.SetState(UnitState.Attacking);
        }
        private void QueueVolley(ActorClock actor, BattleUnit primary, int count, int damage, string effect, bool super = false)
        {
            primary = PrimaryAttackTarget(actor, primary);
            List<BattleUnit> targets = Enemies(actor, true); targets.Remove(primary); targets.Insert(0, primary);
            if (Id(actor) == "sepora") count = Mathf.Min(count, targets.Count);
            for (int i = 0; i < count; i++) QueueImpact(actor, targets[i % targets.Count], damage, effect, super, secondary: i > 0);
        }
        private static bool IsEnergyAbilityReady(ActorClock actor)
        {
            BattleUnit unit = actor?.Unit;
            return unit != null && !unit.IsEnergyLocked && unit.BaseStats.EnergyMax > 0 &&
                unit.Energy + .001f >= unit.BaseStats.EnergyMax;
        }
        // Storm pulses are Super effects: they do not count as basics, gain energy,
        // or consume the first-hit preparation reserved for after the Super.
        void AdvanceStorm(ActorClock actor)
        {
            if(Id(actor)!="sepora"||actor.Unit==null||!actor.Unit.IsAlive||actor.SuperUntil<=tick||
                actor.EnergyCastDue>0||actor.NextStormPulse>tick||actor.StunUntil>tick||actor.Unit.IsFlying||actor.Unit.IsBeingPulled)return;
            var selection=FollowOrAcquireTarget(actor,Enemies(actor));
            if(selection.Status!=CombatTargetSelectionStatus.ReadyToAttack)return;
            actor.Unit.AttackWindupOverride=-1;
            QueueVolley(actor,selection.Target,Has(actor,"sepora-multishot")?N(actor,"sepora-multishot","superTargets",5):N(actor,"sepora-storm","targets",3),
                AbilityTiming.Damage(actor.Unit.BaseStats,"sepora-storm",BasicDamage(actor)),"sepora",true);
            actor.NextStormPulse=tick+StormPeriod(actor);
        }
        int StormPeriod(ActorClock actor)=>ToTicks(actor.Unit.BaseStats.AttackInterval/AttackSpeed(actor));
        private bool TryEnergyAbility(ActorClock actor, BattleUnit target, bool releasing = false)
        {
            BattleUnit unit = actor.Unit;
            if(actor.EnergySuperActive&&!releasing)return false;
            if (!IsEnergyAbilityReady(actor)) return false;
            target = PrimaryAttackTarget(actor, target);
            string id = Id(actor);
            string ability = AbilityTiming.Effect(id+"-super");
            int cast = AbilityTiming.Ticks(AbilityTiming.Cast(unit.BaseStats,ability));
            if (!releasing && cast>0 && (id=="sepora"||id=="bugaloo"||id=="kayon"||id=="popow"||id=="rosie"||id=="suki"||id=="pokrak"||id=="kazuru"))
            {
                if(id=="sepora"&&!CanReach(actor,target))return false;
                if(id=="kayon"&&FindSummonCell(unit)==null)return false;
                if(id=="popow"&&Enemies(actor).Count==0)return false;
                actor.EnergyCastDue=tick+cast;actor.EnergyCastTarget=target;
                actor.EnergyCastTargetVersion=target==null?0:target.CombatResetVersion;
                unit.ClearEnergy();unit.IsEnergyLocked=true;unit.SetState(UnitState.Attacking);
                ReserveAbilityMotion(actor,ability);ReserveSuperRecovery(actor);AbilityUsed?.Invoke(unit,id+"-super");
                return true;
            }
            if (id == "flo")
            {
                BoardCell front = FindFlowerCell(unit);
                if (front == null) return false;
                int castTicks = cast;
                actor.FlowerCastCell = front;
                actor.FlowerCastDueTick = tick + castTicks;
                actor.NextAttack = actor.NextMove = actor.FlowerCastDueTick + 1;
                unit.SetState(UnitState.Attacking);
                unit.ClearEnergy();
                ReserveSuperRecovery(actor);
                AbilityUsed?.Invoke(unit, "flo-super");
                FlowerCastStarted?.Invoke(unit, front, castTicks * TickDuration);
                ReserveAbilityMotion(actor,ability);
                if(castTicks==0)ResolveFlowerCasts();
                lastChangeTick = tick;
                return true;
            }
            else if (id == "suki") LaunchFishShields(actor);
            else if (id == "pokrak") CastPokrakBubbles(actor);
            else if (id == "kazuru") CastKazuruRings(actor);
            else if (id == "rosie") LaunchHealingRose(actor);
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
                PulseTaunt(actor);
            }
            else if (id == "popow") { if (!Dash(actor)) return false; }
            else if (target == null || !target.IsAlive) return false;
            else if (id == "sepora")
            {
                if (Distance(unit.CurrentCell, target.CurrentCell) > unit.BaseStats.AttackRange) return false;
                actor.SuperUntil = tick + ToTicks(V(actor, "sepora-storm", "duration", 5f)); unit.IsEnergyLocked = true; actor.LastBasicTick = tick;
                actor.NextStormPulse=tick+StormPeriod(actor);
                QueueVolley(actor, target, Has(actor, "sepora-multishot") ? N(actor, "sepora-multishot", "superTargets", 5) : N(actor, "sepora-storm", "targets", 3), AbilityTiming.Damage(unit.BaseStats,ability,BasicDamage(actor)), "sepora", true);
            }
            else if (id == "stein")
            {
                if(Distance(unit.CurrentCell,target.CurrentCell)>unit.BaseStats.AttackRange)return false;
                var targets=new[]{target}.Concat(Enemies(actor,true)).Distinct()
                    .Take(Has(actor,"stein-triple")?N(actor,"stein-triple","targets",4):N(actor,"stein-stun","targets",3)).ToArray();
                if(targets.Length==0)return false;
                int castTicks=cast;
                foreach(var victim in targets)
                    pending.Add(new PendingImpact { Source=actor, Target=victim, Damage=AbilityTiming.Damage(unit.BaseStats,ability,BasicDamage(actor)), Effect="stein-super",
                        Super=true, Projectile=false, RequiresRange=true, ReleaseTick=tick+castTicks, DueTick=tick+castTicks, Secondary=victim!=targets[0] });
                actor.NextAttack=actor.NextMove=tick+castTicks+1;
                unit.SetState(UnitState.Attacking);
            }
            else if (id == "tauris" || id == "tokoro" || id == "hymay" || id == "gochan")
            {
                if (Distance(unit.CurrentCell, target.CurrentCell) > unit.BaseStats.AttackRange) return false;
                int castTicks = cast;
                unit.AttackWindupOverride=cast*TickDuration;
                QueueImpact(actor, target, id == "gochan" ? Mathf.RoundToInt(V(actor, "gochan-seal-burst", "damage", 20f)) : AbilityTiming.Damage(unit.BaseStats,ability,BasicDamage(actor)), id + "-super", true, delay: castTicks);
                actor.NextAttack = actor.NextMove = tick + castTicks + 1;
            }
            else return false;
            unit.ClearEnergy(); actor.NextAttack = Mathf.Max(actor.NextAttack, tick + ToTicks(GetAttackWindup(unit)) + 1);
            if (id == "sepora") actor.NextAttack = tick + StormPeriod(actor);
            ReserveSuperRecovery(actor);
            actor.NextMove = Mathf.Max(actor.NextMove, actor.NextAttack);
            if(!releasing) { ReserveAbilityMotion(actor,ability); AbilityUsed?.Invoke(unit, id+"-super"); }
            actor.NextMove=Mathf.Max(actor.NextMove,actor.AbilityMotionUntil);
            actor.NextAttack=Mathf.Max(actor.NextAttack,actor.AbilityMotionUntil);
            lastChangeTick = tick; return true;
        }
        private static bool Free(BoardCell cell) => cell != null && !cell.IsBlocked && cell.OccupiedBy == null && cell.ReservedBy == null;
        private BoardCell FindFlowerCell(BattleUnit unit)
        {
            if (unit?.CurrentCell == null) return null;
            BoardCell front = board.GetCell(unit.CurrentCell.X, unit.CurrentCell.Y + Forward(unit));
            return Free(front) ? front : board.GetAllCells().Where(Free)
                .OrderBy(c => RingDistance(unit.CurrentCell, c)).ThenBy(c => Distance(unit.CurrentCell, c))
                .ThenBy(c => c.Y * -Forward(unit)).ThenBy(c => c.X).FirstOrDefault();
        }
        private static void CancelFlowerCast(ActorClock actor)
        {
            actor.FlowerCastDueTick = 0;
            actor.FlowerCastCell = null;
        }
        private void ResolveFlowerCasts()
        {
            foreach (ActorClock actor in actors.ToList())
            {
                if (actor.FlowerCastDueTick == 0) continue;
                if (actor.Unit == null || !actor.Unit.IsAlive || !actor.Unit.isActiveAndEnabled || actor.StunUntil > tick)
                { CancelFlowerCast(actor); continue; }
                if (tick < actor.FlowerCastDueTick) continue;
                // Another unit may have stepped onto the planned cell during extraction.
                BoardCell destination = Free(actor.FlowerCastCell) ? actor.FlowerCastCell : FindFlowerCell(actor.Unit);
                if (destination == null) continue;
                CancelFlowerCast(actor);
                Spawn(actor, destination, "flower");
                actor.NextMove = Mathf.Max(tick+1,actor.AbilityMotionUntil);actor.NextAttack = Mathf.Max(actor.NextMove,actor.SuperRecoveryUntil);
                actor.Unit.SetState(UnitState.Idle);
            }
        }
        private bool Reposition(ActorClock actor, BoardCell cell, bool forced = false)
        {
            if(forced&&EronyaOwns(actor)&&Id(actor)=="arboreo")return false;
            if (actor.Unit.CurrentCell == null || !Free(cell) || (forced && (actor.Unit.IsFlying || Immune(actor) || actor.RootUntil > tick))) return false;
            BoardCell from = actor.Unit.CurrentCell;
            if (!board.TryRepositionUnit(actor.Unit, cell)) return false;
            CancelMovementStep(actor);
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
        private bool Dash(ActorClock actor)
        {
            BattleUnit farthest = GetTauntTarget(actor) ?? Enemies(actor).OrderByDescending(u => Distance(actor.Unit.CurrentCell, u.CurrentCell)).ThenBy(u => u.UnitId, StringComparer.Ordinal).FirstOrDefault();
            if (farthest == null) return false;
            BoardCell origin = actor.Unit.CurrentCell, target = farthest.CurrentCell;
            BoardCell landing = FindDashLanding(origin, target);
            float width = Mathf.Max(.1f, V(actor, "popow-dash", "width", 1f));
            int travelTicks = ToTicks(V(actor, "popow-dash", "travelDuration", .38f));
            actor.DashUntilTick = tick + travelTicks;
            var dash = new TravellingDash { Source = actor, From = origin.Coordinates, Through = target.Coordinates,
                To = landing.Coordinates, Start = tick, LastTick = tick, Duration = travelTicks,
                Version = actor.Unit.CombatResetVersion, Width = width };
            travellingDashes.Add(dash);
            DashStarted?.Invoke(actor.Unit, origin, target, landing, width, travelTicks * TickDuration);
            SweepDashContacts(dash, tick, tick);
            Reposition(actor, landing);
            // Match presentation: a later attack interruption also ends the moving aura.
            // Capture after the initial movement so departure cannot cancel its own dash.
            dash.AttackVersion = actor.Unit.AttackResetVersion;
            SetTarget(actor, farthest);
            actor.NextAttack = actor.NextMove = tick + travelTicks + 1;
            return true;
        }
        private BoardCell FindDashLanding(BoardCell origin, BoardCell target)
        {
            int dx = target.X - origin.X, dy = target.Y - origin.Y;
            int distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            // Continue through the selected opponent, rather than stopping on its near side.
            for (int step = distance + 1; step <= distance + BoardManager.Height; step++)
            {
                BoardCell candidate = board.GetCell(origin.X + Mathf.RoundToInt(dx * step / (float)distance), origin.Y + Mathf.RoundToInt(dy * step / (float)distance));
                if (candidate == null) break;
                if (Free(candidate)) return candidate;
            }
            // At a board edge, prefer the closest cell on the far half-plane, then the side.
            // A full board safely returns to the vacated origin after crossing the target.
            return board.GetAllCells().Where(Free)
                .OrderBy(c => (c.X - target.X) * dx + (c.Y - target.Y) * dy < 0 ? 1 : 0)
                .ThenBy(c => RingDistance(c, target))
                .ThenBy(c => Mathf.Abs((c.X - target.X) * dy - (c.Y - target.Y) * dx))
                .ThenBy(c => c.Y).ThenBy(c => c.X).FirstOrDefault() ?? origin;
        }
        private static float DistanceToSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float t = delta.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(point - from, delta) / delta.sqrMagnitude) : 0;
            return (point - (from + delta * t)).sqrMagnitude;
        }
        private void QueueBoulder(ActorClock actor, BattleUnit target, string timing="trimol-boulder", bool released=false)
        {
            if(!released)
            {
                ReserveAbilityMotion(actor,timing);AbilityUsed?.Invoke(actor.Unit,timing);
                int delay=AbilityTiming.Ticks(AbilityTiming.Cast(actor.Unit.BaseStats,timing));
                if(delay>0){preparedBoulders.Add(new PreparedBoulder{Source=actor,Target=target,Due=tick+delay,Reset=actor.Unit.AttackResetVersion,Timing=timing});return;}
            }
            BoardCell origin = actor.Unit.CurrentCell;
            BoardCell end = target?.CurrentCell ?? board.GetCell(origin.X, actor.Unit.Side == BoardSide.Blue ? 0 : 9);
            if (end == origin) return;
            List<BoardCell> path = Line(origin, end, Has(actor, "trimol-infinite-boulder") ? 20 : actor.Unit.BaseStats.AttackRange);
            int stepTicks=ToTicks(1f/actor.Unit.BaseStats.ProjectileSpeed);
            if (path.Count > 0) BoulderRolled?.Invoke(actor.Unit, origin, path[path.Count-1], path.Count * stepTicks * TickDuration);
            boulders.Add(new RollingBoulder { Source = actor, Path = path, NextTick = tick + stepTicks, StepTicks=stepTicks,
                Damage = AbilityTiming.Damage(actor.Unit.BaseStats,Has(actor,"trimol-brutal-boulder")?"trimol-brutal-boulder":"trimol-boulder",Mathf.RoundToInt(BasicDamage(actor) * (Has(actor, "trimol-brutal-boulder") ? V(actor, "trimol-brutal-boulder", "multiplier", 3f) : V(actor, "trimol-boulder", "multiplier", 2f)))) });
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
        private BoardCell[] ImpactAreaCells(PendingImpact impact, out string kind)
        {
            ActorClock actor = impact.Source; BattleUnit target = impact.Target;
            bool square = impact.Effect == "gochan-super" || (impact.Effect == "gochan" && Has(actor, "gochan-expanded-seal"));
            bool cross = (impact.Effect == "gochan" && !square) || (impact.Effect == "jazar" && Has(actor, "jazar-cross"));
            bool sweep = (impact.Effect == "popow-hook") || (impact.Effect == "aky" && Has(actor, "aky-sweep")) || (impact.Effect == "tauris-super" && Has(actor, "tauris-sweep"));
            kind = square ? "gochan-square" : cross ? (impact.Effect == "gochan" ? "gochan-cross" : "jazar-cross") :
                sweep ? (impact.Effect == "popow-hook" ? "popow-sweep" : impact.Effect == "tauris-super" ? "tauris-sweep" : impact.Effect + "-sweep") : null;
            if (square || cross)
            {
                string areaEffect = impact.Effect == "gochan-super" ? "gochan-seal-burst" :
                    impact.Effect == "jazar" ? "jazar-cross" : square ? "gochan-expanded-seal" : null;
                return ResolveAbilityArea(actor,areaEffect,target.CurrentCell,AreaCells(target.CurrentCell,1,cross));
            }
            if (sweep && actor.Unit.CurrentCell != null && target.CurrentCell != null)
                return SweepCells(actor.Unit.CurrentCell.Coordinates, target.CurrentCell.Coordinates)
                    .Select(position => board.GetCell(position.x, position.y)).Where(cell => cell != null)
                    .Concat(new[] { target.CurrentCell }).Distinct().ToArray();
            return Array.Empty<BoardCell>();
        }
        private List<BattleUnit> ImpactTargets(PendingImpact impact, BoardCell[] cells)
        {
            var result = new List<BattleUnit>();
            // The seal super is wholly an area. Basic upgrade splash retains the separate basic hit.
            if (impact.Effect != "gochan-super" || !HasEditedArea(impact.Source,"gochan-seal-burst") || cells.Contains(impact.Target.CurrentCell))
                result.Add(impact.Target);
            foreach (ActorClock other in actors)
                if (other.Unit.IsAlive && other.Unit.Side != impact.Source.Unit.Side &&
                    cells.Contains(other.Unit.CurrentCell) && !result.Contains(other.Unit)) result.Add(other.Unit);
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
                if (impact.Missed || impact.Target == null || !impact.Target.IsAlive || impact.Target.IsReviving || impact.Source.Unit == null) continue;
                if(impact.PreciseDamage.HasValue&&(impact.Source.Unit.CombatResetVersion!=impact.SourceResetVersion||
                    impact.Target.CombatResetVersion!=impact.TargetResetVersion))continue;
                BoardCell[] cells = ImpactAreaCells(impact, out string areaKind);
                bool edited = HasEditedArea(impact.Source, impact.Effect=="gochan-super"?"gochan-seal-burst":
                    impact.Effect=="jazar"?"jazar-cross":impact.Effect=="gochan"&&Has(impact.Source,"gochan-expanded-seal")?"gochan-expanded-seal":null);
                if (!edited && areaKind != null && cells.Length > 0) AreaEffect?.Invoke(impact.Source.Unit, areaKind, cells);
                foreach (BattleUnit victim in ImpactTargets(impact, cells))
                {
                    ActorClock defender = Clock(victim);
                    if (defender == null || victim.IsFlying || defender.InvulnerableUntil > tick) continue;
                    if(eronyaCombat&&EronyaRouteLegacyImpact(impact,defender))continue;
                    bool lethal = impact.Effect == "tauris-super" && !Has(impact.Source, "tauris-hunger");
                    int raw = lethal ? victim.CurrentHealth : impact.Effect == "tauris-super" ? Mathf.CeilToInt(victim.CurrentHealth * V(impact.Source, "tauris-hunger", "healthPercent", 50f)/100f) : impact.Damage;
                    int amount = lethal ? victim.CurrentHealth + victim.Shield : impact.Effect == "bugaloo-reflected-moon" ? ReflectedIncoming(defender, raw) : Incoming(defender, raw);
                    damage.TryGetValue(victim, out int previous); damage[victim] = previous + amount; killers[victim] = impact.Source;
                    // Reflected projectiles cannot trigger another reflection.
                    if (impact.Effect != "bugaloo-reflected-moon") QueueReflectedMoon(defender, impact.Source, raw);
                    ApplyHitEffects(impact, defender);
                    events.Add(Tuple.Create(impact, victim, amount, lethal));
                }
                if (impact.Effect == "anuik" && Has(impact.Source, "anuik-bounce"))
                    QueueAnuikBounce(impact);
            }
            // Apply all simultaneous HP changes before callbacks and elimination resolution.
            foreach (var hit in damage)
            {
                ActorClock defender = Clock(hit.Key);
                int shieldBefore = hit.Key.Shield;
                int amount = AbsorbCombatShield(hit.Key, hit.Value);
                int applied = ApplyCombatDamage(defender, amount);
                if (applied > 0 || hit.Key.Shield < shieldBefore) { lastChangeTick = tick; defender.LastDamager = killers[hit.Key].Unit; }
                if (applied > 0 && Id(defender) != "suki" && Id(defender) != "pokrak") hit.Key.AddEnergy(hit.Key.BaseStats.EnergyOnDamage);
            }
            foreach (var hit in events)
            {
                if (hit.Item3 > 0)
                {
                    if (hit.Item2.BaseStats.DefinitionId == "suki" || hit.Item2.BaseStats.DefinitionId == "pokrak") hit.Item2.AddEnergy(hit.Item2.BaseStats.EnergyOnDamage);
                    if ((Id(hit.Item1.Source) == "suki" || Id(hit.Item1.Source) == "pokrak") && !hit.Item1.Super && !hit.Item1.Secondary)
                        hit.Item1.Source.Unit.AddEnergy(hit.Item1.Source.Unit.BaseStats.EnergyPerAttack);
                }
                PublishImpactPresentation(hit.Item1.Source.Unit, hit.Item2, hit.Item3, hit.Item1.Effect, hit.Item1.Super);
                if(hit.Item1.Critical && hit.Item3>0)
                {
                    CriticalImpacted?.Invoke(hit.Item1.Source.Unit,hit.Item2);
                    AbilityUsed?.Invoke(hit.Item1.Source.Unit,"atong-critical");
                }
                if (hit.Item4) LethalImpacted?.Invoke(hit.Item1.Source.Unit, hit.Item2);
            }
            TriggerLowHealthStealth();
        }

        private void QueueAnuikBounce(PendingImpact original)
        {
            BattleUnit first = original.Target;
            if (first == null || first.CurrentCell == null) return;
            float range = V(original.Source, "anuik-bounce", "bounceRange", 3f);
            BattleUnit next = Enemies(original.Source)
                .Where(candidate => candidate != first && candidate.CurrentCell != null && Distance(first.CurrentCell, candidate.CurrentCell) <= range)
                .OrderBy(candidate => CombatTargetSelector.DistanceSquared(first.CurrentCell, candidate.CurrentCell))
                .ThenBy(candidate => candidate.UnitId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (next == null) return;

            float multiplier = Mathf.Max(0, V(original.Source, "anuik-bounce", "bounceDamageMultiplier", .5f));
            int damage = Mathf.Max(0, Mathf.RoundToInt(original.Damage * multiplier));
            int distance = Mathf.Max(1, Distance(first.CurrentCell, next.CurrentCell));
            int travel = Mathf.Max(1, ToTicks(distance / Mathf.Max(.1f, original.Source.Unit.BaseStats.ProjectileSpeed)));
            // The distinct effect key is the recursion guard: a bounced heart can never spawn another bounce.
            // The heart leaves the first victim immediately; it does not start a second
            // Anuik windup and cannot be redirected by a later taunt or interruption.
            pending.Add(new PendingImpact { Source = original.Source, Target = next, Damage = damage,
                Effect = "anuik-bounce-hit", Secondary = true, Projectile = true, ReleaseTick = tick, DueTick = tick + travel });
            ProjectileBounced?.Invoke(original.Source.Unit, first, next, travel * TickDuration);
        }
        private int Incoming(ActorClock defender, int amount) =>
            (int)IncomingWithDefense(defender, amount, ExternalReduction(defender));
        // Shared damage path: approved percentage reductions remain; armor is retired.
        private decimal IncomingWithDefense(ActorClock defender, decimal amount, float reduction)
        {
            if (amount <= 0) return 0;
            float multiplier = defender.WeakUntil > tick ? defender.Weakness : 1;
            multiplier *= 1f - reduction;
            if (Has(defender, "atori-hard-head") && tick < ToTicks(V(defender, "atori-hard-head", "duration", 5f))) multiplier *= Mathf.Clamp01(1 - V(defender, "atori-hard-head", "reduction", 20f)/100f);
            if (Has(defender, "bugaloo-protection") && defender.SuperUntil > tick) multiplier *= Mathf.Clamp01(1 - V(defender, "bugaloo-protection", "reduction", 50f)/100f);
            return Math.Max(1m, Mathf.RoundToInt((float)amount * multiplier));
        }
        private void DirectDamage(ActorClock source, ActorClock defender, int amount)
        {
            if(eronyaCombat&&EronyaRouteDirectDamage(source,defender,amount))return;
            if (source == null || defender == null || !defender.Unit.IsAlive || defender.Unit.IsFlying || defender.InvulnerableUntil > tick) return;
            int shieldBefore = defender.Unit.Shield;
            int applied = ApplyCombatDamage(defender, AbsorbCombatShield(defender.Unit, Incoming(defender, amount)));
            bool absorbed = defender.Unit.Shield < shieldBefore;
            if (applied > 0 || absorbed) { defender.LastDamager = source.Unit; lastChangeTick = tick; }
            if (applied > 0 || absorbed && (Id(defender) == "suki" || Id(defender) == "pokrak")) defender.Unit.AddEnergy(defender.Unit.BaseStats.EnergyOnDamage);
            PublishImpactPresentation(source.Unit, defender.Unit, applied, "legacy-direct", periodic:true);
            QueueReflectedMoon(defender, source, amount);
        }
        // Capture unreduced incoming damage at launch, then resolve it on the combat clock.
        // Travel has a readable minimum even when the attacker is in the adjacent cell.
        private void QueueReflectedMoon(ActorClock reflector, ActorClock attacker, int incoming)
        {
            if (!Has(reflector, "bugaloo-reflect") || reflector.SuperUntil <= tick || incoming <= 0 ||
                attacker?.Unit == null || !attacker.Unit.IsAlive) return;
            int reflected = Mathf.RoundToInt(incoming * (Has(reflector, "bugaloo-revenge") ?
                V(reflector, "bugaloo-revenge", "reflectPercent", 150f) / 100f : reflector.Unit.TrainingEffectMultiplier));
            int travel = ToTicks(Mathf.Clamp(.35f + Distance(reflector.Unit.CurrentCell, attacker.Unit.CurrentCell) * .08f, .4f, .9f) / reflector.Unit.TrainingEffectMultiplier);
            pending.Add(new PendingImpact { Source = reflector, Target = attacker.Unit, Damage = reflected,
                Effect = "bugaloo-reflected-moon", Secondary = true, Projectile = true,
                ReleaseTick = tick, DueTick = tick + travel });
            ReflectedMoonLaunched?.Invoke(reflector.Unit, attacker.Unit, travel * TickDuration);
        }
        private int ReflectedIncoming(ActorClock defender, int amount) =>
            ReflectedIncomingWithReduction(amount, ExternalReduction(defender));
        private int ReflectedIncomingWithReduction(int amount, float reduction) => amount <= 0 ? 0 :
            Mathf.Max(1, Mathf.RoundToInt(amount * (1f - reduction)));

        private void Heal(ActorClock actor, int amount)
        {
            if (actor == null || !actor.Unit.IsAlive) return;
            float multiplier = Has(actor, "bugaloo-healing") ? 1 + V(actor, "bugaloo-healing", "bonusPercent", 50f)/100f : 1;
            if(eronyaCombat){EronyaHeal(actor,actor,Mathf.RoundToInt(amount*multiplier),"legacy-heal");return;}
            if (actor.AntiHealUntil > tick) multiplier *= actor.AntiHealMultiplier;
            int restored=actor.Unit.Heal(Mathf.RoundToInt(amount * multiplier));
            if(restored>0){lastChangeTick=tick;Healed?.Invoke(actor.Unit,restored);}
        }
        private void ApplyHitEffects(PendingImpact impact, ActorClock defender)
        {
            ActorClock source = impact.Source;
            switch (impact.Effect)
            {
                case "stein-super":
                    Stun(defender,ToTicks(V(source,"stein-stun","duration",2f)+(Has(source,"stein-long-stun")?V(source,"stein-long-stun","extraDuration",1.5f):0)), "electric");
                    if(defender.Unit.CurrentCell!=null)AreaEffect?.Invoke(source.Unit,"stein-lightning",new[]{defender.Unit.CurrentCell});
                    break;
                case "popow-hook": Stun(defender, ToTicks(V(source, "popow-sweep", "duration", 2f)), "popow"); break;
                case "boulder": Stun(defender, Has(source, "trimol-brutal-boulder") ? ToTicks(V(source, "trimol-brutal-boulder", "duration", 2f)) : ToTicks(V(source, "trimol-boulder", "duration", 1f))); break;
                case "jazar": Radiation(source, defender); break;
                case "gochan":
                case "gochan-super":
                    if (Has(source, "gochan-anti-heal")) ApplyAntiHeal(defender, V(source, "gochan-anti-heal", "reduction", 60f)/100f, tick + ToTicks(V(source, "gochan-anti-heal", "duration", 4f)));
                    // Gochan's authored marks explicitly stack, each with its own lifespan.
                    if (impact.Super && Has(source, "gochan-burn")) burns.Add(new Burn { Source = source, Target = defender.Unit, Next = tick + 10, Remaining = N(source, "gochan-burn", "duration", 4) });
                    break;
                case "aky":
                    float drained = -defender.Unit.AddEnergy(-V(source, "aky-energy-drain", "energy", 20f));
                    if (Has(source, "aky-drain-heal")) Heal(source, Mathf.RoundToInt(drained * V(source, "aky-drain-heal", "healPercent", 50f)/100f));
                    break;
                case "tsu-gum":
                    if (!Immune(defender))
                    {
                        float rootDuration = V(source, "tsu-root", "duration", 2f);
                        if (Has(source, "tsu-death-trap")) rootDuration += V(source, "tsu-death-trap", "extraDuration", 3f);
                        if(EronyaOwns(defender))
                        {
                            if(!EronyaApplyStatus(source,defender,EronyaStatusKind.Root,"tsu-root",1,ToTicks(rootDuration)))break;
                            MarkGumRoot(defender,defender.RootUntil);
                            defender.ChocolateSource=Has(source,"tsu-chocolate")?source.Unit:null;
                            defender.ChocolateNext=tick+10;defender.ChocolateStacks=0;break;
                        }
                        int until = statusRules.Apply("root",tick,defender.RootUntil,ToTicks(rootDuration));
                        if (until <= defender.RootUntil) break;
                        defender.RootUntil = until;
                        MarkGumRoot(defender, until);
                        defender.ChocolateSource = Has(source, "tsu-chocolate") ? source.Unit : null;
                        defender.ChocolateNext = tick + 10; defender.ChocolateStacks = 0;
                    }
                    break;
                case "tokoro-super": source.Unit.HeadbuttImpactVersion++; Stun(defender, ToTicks(V(source, "tokoro-headbutt", "duration", 3f))); Stun(source, ToTicks(V(source, "tokoro-headbutt", "duration", 3f))); break;
                case "hymay-super": Pull(source, defender); break;
            }
        }
        private void Stun(ActorClock actor, int duration, string visual = "impact")
        {
            if(EronyaOwns(actor)){EronyaApplyStatus(actor,actor,EronyaStatusKind.Stun,"legacy-stun",1,duration);return;}
            if (duration <= 0 || actor == null || !actor.Unit.IsAlive || actor.Unit.IsFlying || Immune(actor)) return;
            // A valid incoming stun still triggers Tokoro's existing on-hit reaction.
            // Its duration/visual need not replace a longer control already in progress.
            if (Has(actor, "tokoro-ice-cream")) SpawnCones(actor);
            bool starting = actor.StunUntil <= tick;
            int until = statusRules.Apply("stun",tick,actor.StunUntil,duration);
            if (until <= actor.StunUntil) return;
            actor.StunUntil = until;
            actor.StunRetargetPending = true;
            actor.Unit.IsStunned = true;
            actor.PopowStunUntil=visual=="popow"?until:0;
            actor.ElectricStunUntil=visual=="electric"?until:0;
            actor.Unit.IsPopowStunned=actor.PopowStunUntil>tick;
            actor.Unit.IsElectrocuted=actor.ElectricStunUntil>tick;
            // Interrupted windups must start again; already released missiles keep flying.
            InterruptAttack(actor);
            actor.NextMove = Mathf.Max(actor.NextMove, actor.StunUntil);
            if (starting && Has(actor, "tokoro-stun-heal")) { Heal(actor, Mathf.RoundToInt(V(actor, "tokoro-stun-heal", "heal", 8f))); actor.LastHealTick = tick; }
        }
        private void Radiation(ActorClock source, ActorClock target)
        {
            float previous=target.AttackSpeedMultiplier;
            if (!Immune(target)) target.AttackSpeedMultiplier = Mathf.Min(target.AttackSpeedMultiplier, Mathf.Max(.05f, 1-(Has(source, "jazar-intense-radiation") ? V(source, "jazar-intense-radiation", "reduction", 50f) : V(source, "jazar-radiation", "reduction", 25f))/100f));
            target.Unit.IsSlowed=target.AttackSpeedMultiplier<.999f;
            if(target.Unit.IsSlowed)target.Unit.SlowEffectId="jazar-radiation";
            if(target.AttackSpeedMultiplier<previous)AbilityUsed?.Invoke(source.Unit,"jazar-radiation");
        }
        private void Pull(ActorClock source, ActorClock target)
        {
            if (!source.Unit.IsAlive || !target.Unit.IsAlive || source.Unit.CurrentCell == null ||
                target.Unit.IsFlying || Immune(target) || target.RootUntil > tick) return;
            BoardCell origin = source.Unit.CurrentCell;
            BoardCell destination = board.GetCell(origin.X, origin.Y + Forward(source.Unit));
            bool pulled = destination != null && (ReferenceEquals(destination, target.Unit.CurrentCell) || Free(destination)) &&
                StartHookPull(source, target, destination);
            if (pulled)
            {
                if (Has(source, "hymay-weakness")) ApplyWeakness(target, 1 + V(source, "hymay-weakness", "bonusPercent", 25f)/100f, int.MaxValue);
                if (Has(source, "hymay-focus"))
                {
                    var focusCells = ResolveAbilityArea(source,"hymay-focus",origin,
                        board.GetAllCells().Where(c=>InHymayFocusArea(source,origin,c)));
                    foreach (ActorClock ally in actors.Where(a => a.Unit != null && a.Unit.IsAlive && a.Unit.Side == source.Unit.Side &&
                        !Immune(a) && focusCells.Contains(a.Unit.CurrentCell)))
                    { ally.FocusTarget = target.Unit; SetTarget(ally, target.Unit); }
                }
            }
        }
        private static bool InHymayFocusArea(ActorClock source, BoardCell origin, BoardCell cell)
        {
            if (origin == null || cell == null) return false;
            int width = Mathf.Clamp(N(source, "hymay-focus", "width", 2), 1, BoardManager.Width);
            int height = Mathf.Clamp(N(source, "hymay-focus", "height", 2), 1, BoardManager.Height);
            int forward = Forward(source.Unit);
            int x = (cell.X - origin.X) * -forward, y = (cell.Y - origin.Y) * forward;
            // Exact cell counts, not a radius. Even sizes extend forward and to the
            // player's right; rotate the same footprint for the opposing side.
            return x >= -(width - 1) / 2 && x <= width / 2 &&
                y >= -(height - 1) / 2 && y <= height / 2;
        }
        private bool TryHymayRetreat(ActorClock source, int normalDistance)
        {
            BoardCell origin = source?.Unit?.CurrentCell;
            if (origin == null) return false;
            int backward = -Forward(source.Unit);

            // Preferred distance first, then every shorter distance.
            for (int distance = Mathf.Max(1, normalDistance); distance >= 1; distance--)
                if (TryHymayRetreatAtDistance(source, origin, backward, distance)) return true;

            // If the normal space is full, keep searching farther back until the board ends.
            for (int distance = Mathf.Max(1, normalDistance) + 1; ; distance++)
            {
                int y = origin.Y + backward * distance;
                if (y < 0 || y >= BoardManager.Height) return false;
                if (TryHymayRetreatAtDistance(source, origin, backward, distance)) return true;
            }
        }
        private bool TryHymayRetreatAtDistance(ActorClock source, BoardCell origin, int backward, int distance)
        {
            int y = origin.Y + backward * distance;
            if (y < 0 || y >= BoardManager.Height) return false;
            // Centre has priority; the two immediately adjacent lateral cells follow.
            if (TryHymayBackflip(source, board.GetCell(origin.X, y))) return true;
            if (TryHymayBackflip(source, board.GetCell(origin.X - 1, y))) return true;
            return TryHymayBackflip(source, board.GetCell(origin.X + 1, y));
        }
        private void TickStatuses()
        {
            for(int actorIndex=0,actorCount=actors.Count;actorIndex<actorCount;actorIndex++)
            {
                ActorClock actor=actors[actorIndex];
                BattleUnit unit = actor.Unit;
                if (unit == null || !unit.IsAlive) continue;
                TickOpeningRecovery(actor);
                unit.AdvanceFlight(TickDuration);
                unit.AdvancePull(TickDuration);
                if (unit.IsEnergyLocked && actor.EnergyCastDue==0 && tick >= actor.SuperUntil)
                {
                    ClearTimedSuper(actor);
                    if (Id(actor) == "sepora")
                        actor.NextAttack = Mathf.Max(tick, actor.LastBasicTick + ToTicks(unit.BaseStats.AttackInterval / AttackSpeed(actor)));
                }
                else if(!EronyaOwns(actor))unit.AddEnergy(unit.BaseStats.EnergyPerSecond * TickDuration);
                unit.IsStunned = actor.StunUntil > tick;
                unit.IsPopowStunned=unit.IsStunned&&actor.PopowStunUntil>tick;
                unit.IsElectrocuted=unit.IsStunned&&actor.ElectricStunUntil>tick;
                unit.IsRooted = actor.RootUntil > tick; unit.IsInvisible = actor.StealthUntil > tick;
                if (actor.StunUntil > tick && tick - actor.LastHealTick >= 10)
                {
                    if (Has(actor, "tokoro-stun-heal")) Heal(actor, Mathf.RoundToInt(V(actor, "tokoro-stun-heal", "heal", 8f)));
                    actor.LastHealTick = tick;
                }
                if (Has(actor, "blotan-income") && tick >= actor.NextIncome)
                {
                    int earned = N(actor, "blotan-income", "coins", 1);
                    CurrencyEarned?.Invoke(unit.Side, earned);
                    MoonkensProduced?.Invoke(unit, earned);
                    actor.NextIncome = tick + ToTicks(Has(actor, "blotan-fast-income") ? V(actor, "blotan-fast-income", "interval", 10f) : V(actor, "blotan-income", "interval", 20f));
                    // Escape already interrupts the basic. Extraction without that
                    // Trick must also cancel preparation, preserving released projectiles.
                    if (Has(actor, "blotan-health")) BlotanEscape(actor);
                    else InterruptAttack(actor);
                    ReserveAbilityMotion(actor, "blotan-income");
                    ReserveSuperRecovery(actor);
                    actor.EnergySuperUntil = Mathf.Max(actor.EnergySuperUntil, actor.StealthUntil);
                    unit.SetState(UnitState.Attacking);
                    AbilityUsed?.Invoke(unit, "blotan-income");
                }
                if (actor.ChocolateSource != null && actor.RootUntil >= tick && tick >= actor.ChocolateNext)
                {
                    // Emit the visual at the actual periodic hit, including a lethal tick.
                    if (actor.Unit.CurrentCell != null)
                        AreaEffect?.Invoke(actor.ChocolateSource, "tsu-chocolate", new[] { actor.Unit.CurrentCell });
                    DirectDamage(Clock(actor.ChocolateSource), actor, Mathf.RoundToInt(++actor.ChocolateStacks * V(Clock(actor.ChocolateSource), "tsu-chocolate", "damage", 2f)));
                    actor.ChocolateNext = tick + ToTicks(1f);
                }
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
        private void TickOpeningRecovery(ActorClock actor)
        {
            if (actor.OpeningRecoveryUntil <= 0) return;
            // Accumulate fractional HP before rounding so editable rates do not lose healing per tick.
            int elapsedTicks = Mathf.Min(tick, actor.OpeningRecoveryUntil);
            int scheduled = Mathf.FloorToInt(elapsedTicks * TickDuration * V(actor, "tokoro-sleep", "heal", 60f) + .0001f);
            int amount = Mathf.Max(0, scheduled - actor.OpeningHealingScheduled);
            actor.OpeningHealingScheduled = scheduled;
            if (amount > 0) Heal(actor, amount);
            actor.SleepStarted = tick < actor.OpeningRecoveryUntil;
            actor.Unit.IsSleeping = actor.SleepStarted;
            actor.Unit.IsOpeningEnergyBlocked = actor.SleepStarted && V(actor, "tokoro-sleep", "blockEnergy", 1f) > 0;
        }
        private void BlotanEscape(ActorClock actor)
        {
            var unit = actor.Unit;
            // The old ID preserves already purchased second-slot upgrades.
            actor.StealthUntil = statusRules.Apply("stealth",tick,actor.StealthUntil,ToTicks(V(actor,"blotan-health","duration",.5f)));
            unit.IsInvisible = actor.StealthUntil>tick;
            InterruptAttack(actor);
            actor.NextMove = Mathf.Max(actor.NextMove,actor.StealthUntil);
            actor.NextAttack = Mathf.Max(actor.NextAttack,actor.StealthUntil);
            foreach (var enemy in actors)
                if (enemy.LockedTarget == unit) enemy.LockedTarget = null;
            var origin=unit.CurrentCell;
            if(origin==null)return;
            int radiusX=Mathf.Clamp(Mathf.RoundToInt(V(actor,"blotan-health","radiusX",2)),0,BoardManager.Width-1);
            int radiusY=Mathf.Clamp(Mathf.RoundToInt(V(actor,"blotan-health","radiusY",2)),0,BoardManager.Height-1);
            var choices = board.GetAllCells().Where(c => c != origin && Free(c) &&
                Mathf.Abs(c.X-origin.X)<=radiusX && Mathf.Abs(c.Y-origin.Y)<=radiusY).ToList();
            if (choices.Count == 0) return; // Still conceal and pay income on a full board.
            var from = unit.CurrentCell;
            var destination = choices[combatRandom.Next(choices.Count)];
            if (!board.TryRepositionUnit(unit, destination)) return;
            CancelMovementStep(actor);
            actor.LastCell = destination;
            lastChangeTick = tick;
            Teleported?.Invoke(unit, from, destination);
        }

        private void TriggerLowHealthStealth()
        {
            foreach (ActorClock actor in actors.ToList())
                if (Has(actor, "sepora-stealth") && !actor.StealthUsed && actor.Unit.IsAlive && actor.Unit.CurrentHealth < actor.Unit.BaseStats.MaxHealth * V(actor, "sepora-stealth", "threshold", 50f)/100f)
                {
                    actor.StealthUsed = true;
                    foreach (ActorClock ally in LegacyAbilityNeighbors(actor,"sepora-stealth",actor.Unit.CurrentCell,actor.Unit.Side))
                    {
                        ally.StealthUntil = statusRules.Apply("stealth",tick,ally.StealthUntil,ToTicks(V(actor, "sepora-stealth", "duration", 2f))); ally.Unit.IsInvisible = true;
                        foreach (ActorClock enemy in actors) if (enemy.LockedTarget == ally.Unit) enemy.LockedTarget = null;
                    }
                    AbilityUsed?.Invoke(actor.Unit, "sepora-stealth");
                }
        }
        private void SpawnCones(ActorClock actor)
        {
            if (actor.Unit.CurrentCell == null) return;
            var origin=actor.Unit.CurrentCell;
            var cells=board.GetAllCells().Where(c=>c!=origin && !c.IsBlocked && Distance(c,origin)<=1).ToArray();
            cells = ResolveAbilityArea(actor,"tokoro-ice-cream",origin,cells,origin,c=>!c.IsBlocked);
            int travel=AbilityTiming.Ticks(V(actor,"tokoro-ice-cream","travelDuration",.4f));
            if(travel==0){LandCones(actor,cells);return;}
            flyingCones.Add(new FlyingCones{Source=actor,Cells=cells,Due=tick+travel});
            ConesLaunched?.Invoke(actor.Unit,origin,cells,travel*TickDuration);
        }

        private bool TryConsumeCone(TileEffect cone)
        {
            BattleUnit occupant = cone.Cell.OccupiedBy as BattleUnit;
            if (occupant == null || !occupant.IsAlive || occupant.IsFlying) return false;
            ActorClock actor = Clock(occupant);
            if (actor == null) return false;
            if (occupant.Side == cone.Source.Unit.Side)
                Heal(actor, Mathf.RoundToInt(V(cone.Source, "tokoro-ice-cream", "heal", 10f)));
            else
            {
                ApplyWeakness(actor, 1 + V(cone.Source, "tokoro-ice-cream", "weaknessPercent", 20f) / 100f,
                    tick + ToTicks(V(cone.Source, "tokoro-ice-cream", "duration", 3f)));
            }
            return true;
        }
        private void ResolveTileEffects()
        {
            foreach (TileEffect tile in tiles.ToList())
            {
                if (tick >= tile.Until || (tile.Captured != null && !tile.Captured.IsAlive)) { tiles.Remove(tile); continue; }
                BattleUnit occupant = tile.Cell.OccupiedBy as BattleUnit;
                if (occupant == null || !occupant.IsAlive || occupant.IsFlying) continue;
                ActorClock actor = Clock(occupant);
                if (tile.Kind == "radiation" && occupant.Side != tile.Source.Unit.Side) Radiation(tile.Source, actor);
                else if (tile.Kind == "trap" && occupant.Side != tile.Source.Unit.Side && tile.Captured == null && !Immune(actor))
                { tile.Captured = occupant; actor.RootUntil = int.MaxValue; occupant.IsRooted = true; }
                else if (tile.Kind == "cone" && TryConsumeCone(tile)) tiles.Remove(tile);
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
                CancelFlowerCast(actor);
                actor.LockedTarget = null; actor.LastCell = unit.CurrentCell;
                actor.StunRetargetPending = false;
                int revives = Has(actor, "anuik-second-revive") ? 2 : unit.BaseStats.RevivesPerCombat;
                if (actor.RevivesUsed < revives)
                {
                    actor.RevivesUsed++; actor.ReviveDueTick = tick + ToTicks(unit.BaseStats.ReviveDelay);
                    unit.BeginReviving(); board.CancelReservation(unit); lastChangeTick = tick;
                    Reviving?.Invoke(unit, ToTicks(unit.BaseStats.ReviveDelay) * TickDuration); continue;
                }
                actor.DeathTick = tick;
                actor.DeathHandled = true;
                EronyaDied(actor);
                ActorClock owner = Clock(actor.Summoner);
                if (actor.SummonKind == "flower" && !HasEditedArea(owner,"flo-flower")) HealingArea?.Invoke(unit.CurrentCell);
                if (actor.SummonKind == "flower")
                    foreach (ActorClock ally in LegacyAbilityNeighbors(owner,"flo-flower",unit.CurrentCell,unit.Side)) Heal(ally, Has(owner, "flo-flower-heal") ? Mathf.RoundToInt(V(owner, "flo-flower-heal", "heal", 40f)) : Mathf.RoundToInt(V(owner, "flo-flower", "heal", 20f)));
                if (actor.SummonKind == "dummy" && Has(owner, "kayon-dummy-legacy"))
                    foreach (ActorClock ally in LegacyAbilityNeighbors(owner,"kayon-dummy-legacy",unit.CurrentCell,unit.Side)) ally.DamageBonus += Mathf.RoundToInt(V(owner, "kayon-dummy-legacy", "damage", 1f));
                OnKill(Clock(actor.LastDamager));
                board.ReleaseUnit(unit); Died?.Invoke(unit); lastChangeTick = tick;
            }
            ReleaseDefeatedTargets();
            UpdateTaunts();
        }
        private void OnKill(ActorClock killer)
        {
            if (killer == null) return;
            bool rewarded=false;
            int healthBefore=killer.Unit.CurrentHealth;
            float speedBefore=killer.SpeedBonus,damageBefore=killer.DamageMultiplier;
            if (Has(killer, "sepora-kill-power") && killer.SeporaStacksThisRound < Mathf.Max(0,Mathf.RoundToInt(V(killer,"sepora-kill-power","maxPerRound",3))))
            {
                int bonus=Mathf.Max(0,Mathf.RoundToInt(V(killer,"sepora-kill-power","damage",1)));
                if(bonus>0){killer.SeporaStacksThisRound++;killer.DamageBonus+=bonus;PermanentDamageEarned?.Invoke(killer.Unit,bonus);rewarded=true;}
            }
            if (Has(killer, "bugui-kill-power")) { int bonus=Mathf.RoundToInt(V(killer, "bugui-kill-power", "damage", 1f));killer.DamageBonus+=bonus;rewarded|=bonus>0; }
            if (Has(killer, "atori-hunt-speed")) killer.SpeedBonus += V(killer, "atori-hunt-speed", "speedPercent", 50f)/100f;
            if (Has(killer, "atori-kill-heal")) Heal(killer, Mathf.RoundToInt(V(killer, "atori-kill-heal", "heal", 20f)));
            if (Has(killer, "tauris-hunger"))
            {
                int health = Mathf.Max(0, Mathf.RoundToInt(V(killer, "tauris-hunger", "healthPerKill", 20)));
                if (health > 0)
                {
                    // Increase the cap before healing, including a kill made at full health.
                    killer.Unit.BaseStats.AddMaxHealth(health);
                    PermanentHealthEarned?.Invoke(killer.Unit, health);
                    Heal(killer, health);
                    rewarded = true;
                }
                killer.DamageMultiplier += V(killer, "tauris-hunger", "damagePercent", 50f)/100f;
            }
            if(rewarded||killer.Unit.CurrentHealth>healthBefore||killer.SpeedBonus>speedBefore||killer.DamageMultiplier>damageBefore)
                AbilityUsed?.Invoke(killer.Unit,Id(killer)+"-kill");
        }
        private void OnDocumentedRevival(ActorClock actor)
        {
            ClearTimedSuper(actor);
            actor.StunUntil = actor.RootUntil = actor.StealthUntil = actor.PopowStunUntil = actor.ElectricStunUntil = 0;
            actor.Unit.IsPopowStunned=actor.Unit.IsElectrocuted=false;
            // The named Siempre de Pie return is the first revival; the optional second return has25%HP.
            if (actor.RevivesUsed != 1) return;
            if (Has(actor, "anuik-revive-impact"))
            {
                BoardCell[] cells = ResolveAbilityArea(actor,"anuik-revive-impact",actor.Unit.CurrentCell,AreaCells(actor.Unit.CurrentCell,1));
                if (!HasEditedArea(actor,"anuik-revive-impact")) AreaEffect?.Invoke(actor.Unit, "anuik-revive-impact", cells);
                foreach (BattleUnit enemy in Enemies(actor).Where(u => cells.Contains(u.CurrentCell)).ToList())
                {
                    ActorClock victim = Clock(enemy); DirectDamage(actor, victim, Mathf.RoundToInt(V(actor, "anuik-revive-impact", "damage", 10f)));
                    BoardCell cell = enemy.CurrentCell, center = actor.Unit.CurrentCell;
                    Reposition(victim, board.GetCell(cell.X + Math.Sign(cell.X-center.X), cell.Y + Math.Sign(cell.Y-center.Y)), true);
                    float stun = V(actor, "anuik-revive-impact", "stunDuration", 3f);
                    if (stun > 0) Stun(victim, ToTicks(stun));
                }
            }
        }
    }
}

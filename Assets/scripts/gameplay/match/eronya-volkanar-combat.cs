using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    // Staging only. Shared helpers and simultaneous settlement belong to core contracts 1.0-1.11.
    public sealed partial class CombatSimulation
    {
        private sealed class EronyaVolkanarState
        {
            public int ResetVersion, ConquestCount;
            public bool OpeningAttempted, HiddenBladeReady;
            public bool JumpActive, JumpLanded;
            public BoardCell JumpFrom, JumpTo;
            public ActorClock JumpTarget;
            public int JumpTargetVersion;
            public DagataJumpTiming JumpTiming;
        }
        private readonly Dictionary<ActorClock, EronyaVolkanarState> EronyaVolkanarStates =
            new Dictionary<ActorClock, EronyaVolkanarState>();
        private bool EronyaVolkanarOwns(ActorClock actor)
        {
            if (actor == null || actor.Unit == null) return false;
            string id = Id(actor);
            return id == "serya" || id == "legionario" || id == "dagata" || id == "canonero-del-orvek";
        }
        private EronyaVolkanarState EronyaVolkanarStateFor(ActorClock actor)
        {
            EronyaVolkanarState state;
            if (!EronyaVolkanarStates.TryGetValue(actor, out state) || state.ResetVersion != actor.Unit.CombatResetVersion)
            {
                state = new EronyaVolkanarState { ResetVersion = actor.Unit.CombatResetVersion };
                EronyaVolkanarStates[actor] = state;
            }
            return state;
        }
        private int EronyaVolkanarInt(ActorClock actor, string effect, string key, int fallback) =>
            Math.Max(0, (int)Math.Round(V(actor, effect, key, fallback), MidpointRounding.AwayFromZero));
        private int EronyaVolkanarTicks(ActorClock actor, string effect, string key, float seconds) =>
            Math.Max(0, (int)Math.Round(V(actor, effect, key, seconds) * 10, MidpointRounding.AwayFromZero));
        private bool EronyaVolkanarLegal(ActorClock actor, ActorClock target, int range) =>
            actor != null && actor.Unit != null && actor.Unit.IsAlive && actor.Unit.CurrentCell != null &&
            target != null && target.Unit != null && target.Unit.IsAlive && target.Unit.CurrentCell != null &&
            actor.Unit.Side != target.Unit.Side && EronyaDistance(actor.Unit.CurrentCell, target.Unit.CurrentCell) <= range;
        // Serya's primary Super target uses the same square reach as her basic.
        // Leave secondary radii, opening jumps and the other Volkanar abilities unchanged.
        private bool EronyaVolkanarSuperLegal(ActorClock actor, ActorClock target, int range) =>
            Id(actor) == "serya"
                ? EronyaVolkanarLegal(actor, target, int.MaxValue) &&
                    CombatTargetSelector.IsInAttackRange(actor.Unit.CurrentCell, target.Unit.CurrentCell, range) &&
                    EronyaCanReach(actor, target)
                : EronyaVolkanarLegal(actor, target, range);
        // Equal distance/health retains EronyaEnemies canonical team-slot ordering.
        // Contract1.6 guarantees this order; verify its connected implementation during integration.
        private ActorClock EronyaVolkanarNearest(ActorClock actor, int range)
        {
            ActorClock chosen = null;
            int distance = int.MaxValue;
            decimal health = decimal.MaxValue;
            foreach (var enemy in EronyaEnemies(actor, Id(actor) == "serya" ? int.MaxValue : range))
            {
                if (!EronyaVolkanarSuperLegal(actor, enemy, range)) continue;
                int d = EronyaDistance(actor.Unit.CurrentCell, enemy.Unit.CurrentCell);
                decimal hp = EronyaHealthFraction(enemy);
                if (d > distance || d == distance && hp >= health) continue;
                chosen = enemy; distance = d; health = hp;
            }
            return chosen;
        }
        private string EronyaVolkanarActiveId(ActorClock actor)
        {
            switch (Id(actor))
            {
                case "serya": return "serya-avance-de-la-llama";
                case "legionario": return "legionario-tajo-de-forja";
                case "dagata": return "dagata-doble-ascua";
                case "canonero-del-orvek": return "canonero-del-orvek-bomba-de-crater";
                default: return null;
            }
        }
        private void EronyaVolkanarBegin(ActorClock actor)
        {
            if (!EronyaVolkanarOwns(actor) || !actor.Unit.IsAlive) return;
            var state = EronyaVolkanarStateFor(actor);
            if (state.OpeningAttempted) return;
            state.OpeningAttempted = true;
            if (Id(actor) == "legionario")
            {
                const string effect = "legionario-paso-ardiente";
                int duration = EronyaHasTrick(actor,1) ? EronyaVolkanarTicks(actor, "legionario-segunda-marcha", "duration", 8) :
                    EronyaVolkanarTicks(actor, effect, "duration", 5);
                EronyaApplyStatus(actor, actor, EronyaStatusKind.AttackSpeed, effect,
                    V(actor, effect, "attackSpeed", 15) / 100f, duration);
            }
            else if (Id(actor) == "dagata") EronyaVolkanarOpeningJump(actor, state);
        }
        private void EronyaVolkanarOpeningJump(ActorClock actor, EronyaVolkanarState state)
        {
            var origin = actor.Unit.CurrentCell;
            if (origin == null) return;
            ActorClock target = null;
            int farthest = -1;
            decimal health = decimal.MaxValue;
            foreach (var enemy in EronyaEnemies(actor, BoardManager.Width + BoardManager.Height))
            {
                if (!EronyaVolkanarLegal(actor, enemy, BoardManager.Width + BoardManager.Height)) continue;
                int distance = EronyaDistance(origin, enemy.Unit.CurrentCell);
                decimal fraction = EronyaHealthFraction(enemy);
                if (distance < farthest || distance == farthest && fraction >= health) continue;
                target = enemy; farthest = distance; health = fraction;
            }
            if (target == null) return;
            var center = target.Unit.CurrentCell;
            var candidates = new List<BoardCell>();
            int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 };
            for (int i = 0; i < 4; i++)
            {
                var cell = board.GetCell(center.X + dx[i], center.Y + dy[i]);
                if (cell != null && EronyaIsFree(cell)) candidates.Add(cell);
            }
            candidates.Sort((a, b) =>
            {
                int distance = EronyaDistance(origin, a).CompareTo(EronyaDistance(origin, b));
                if (distance != 0) return distance;
                int row = actor.Unit.Side == BoardSide.Blue ? a.Y.CompareTo(b.Y) : b.Y.CompareTo(a.Y);
                return row != 0 ? row : actor.Unit.Side == BoardSide.Blue ? a.X.CompareTo(b.X) : b.X.CompareTo(a.X);
            });
            if (candidates.Count == 0) return;
            BeginDagataJump(actor, state, target, candidates[0]);
        }
        private void EronyaVolkanarTick(ActorClock actor)
        {
            if (Id(actor) == "dagata" && EronyaVolkanarStates.TryGetValue(actor, out var jumpState))
                AdvanceDagataJump(actor, jumpState);
            if (!EronyaVolkanarOwns(actor) || Id(actor) != "serya" || !actor.Unit.IsAlive) return;
            const string effect = "serya-combustion";
            var target = EronyaBasicTarget(actor);
            if (EronyaHasTrick(actor,0) && target != null && target.Unit != null && target.Unit.IsAlive &&
                target.Unit.Side != actor.Unit.Side && EronyaHasStatus(target, EronyaStatusKind.Burn))
                EronyaApplyPermanentStatus(actor, actor, EronyaStatusKind.AttackSpeed, effect, V(actor, effect, "attackSpeed", 15) / 100f);
            else EronyaRemoveEffect(actor, effect);
        }
        private bool EronyaVolkanarTryPlanCast(ActorClock actor, out EronyaCastPlan plan)
        {
            plan = null;
            if (!EronyaVolkanarOwns(actor) || !actor.Unit.IsAlive || actor.Unit.CurrentCell == null) return false;
            bool cannon = Id(actor) == "canonero-del-orvek";
            int range = EronyaVolkanarInt(actor, EronyaVolkanarActiveId(actor), "range", cannon ? 8 : 1);
            var target = cannon ? null : EronyaBasicTarget(actor);
            if (!EronyaVolkanarSuperLegal(actor, target, range)) target = EronyaVolkanarNearest(actor, range);
            if (target == null) return false;
            plan = new EronyaCastPlan { EffectId = EronyaVolkanarActiveId(actor), Target = target.Unit };
            return true;
        }
        private void EronyaVolkanarBurn(ActorClock actor, ActorClock target, string effect, string damageKey,
            int damage, string durationKey, float seconds)
        {
            EronyaPeriodic(actor, target, EronyaStatusKind.Burn, effect, EronyaVolkanarInt(actor, effect, damageKey, damage),
                EronyaVolkanarTicks(actor, effect, durationKey, seconds), 10);
        }
        private void EronyaVolkanarResolveCast(ActorClock actor, EronyaCastPlan plan)
        {
            if (!EronyaVolkanarOwns(actor) || plan == null || plan.EffectId != EronyaVolkanarActiveId(actor)) return;
            string id = Id(actor), effect = plan.EffectId;
            int range = EronyaVolkanarInt(actor, effect, "range", id == "canonero-del-orvek" ? 8 : 1);
            var target = id == "canonero-del-orvek" ? EronyaVolkanarNearest(actor, range) : plan.Target == null ? null : Clock(plan.Target);
            if (!EronyaVolkanarSuperLegal(actor, target, range)) return;
            if(id=="canonero-del-orvek")
            {EronyaQueueProjectile(actor,target,effect,()=>EronyaVolkanarImpactBomb(actor,target,effect));return;}

            var cells = new List<BoardCell>();
            if (id == "serya")
            {
                string damageEffect = EronyaHasTrick(actor,1) ? "serya-frente-abrasador" : effect;
                EronyaDealDamage(actor, target, V(actor, damageEffect, "damage", EronyaHasTrick(actor,1) ? 24 : 18), effect);
                EronyaVolkanarBurn(actor, target, effect, "burnDamage", 2, "burnDuration", 3);
                cells.Add(target.Unit.CurrentCell);
                foreach (var enemy in SelectAbilityArea(actor,effect,actor.Unit.CurrentCell,target.Unit.Side,
                    ()=>EronyaEnemies(actor,EronyaVolkanarInt(actor,effect,"radius",2)),visibleOnly:true))
                {
                    if (enemy == target) continue;
                    EronyaDealDamage(actor, enemy, V(actor, damageEffect, "secondaryDamage", EronyaHasTrick(actor,1) ? 13 : 9), effect);
                    EronyaVolkanarBurn(actor, enemy, effect, "burnDamage", 2, "burnDuration", 3);
                    cells.Add(enemy.Unit.CurrentCell);
                }
            }
            else if (id == "legionario")
            {
                bool wasBurning = EronyaBeforeImpactStatus(target, EronyaStatusKind.Burn);
                string damageEffect = EronyaHasTrick(actor,0) ? "legionario-acero-vivo" : effect;
                EronyaDealDamage(actor, target, V(actor, damageEffect, "damage", EronyaHasTrick(actor,0) ? 17 : 13), effect);
                EronyaVolkanarBurn(actor, target, effect, "burnDamage", 2, "burnDuration", 3);
                if (EronyaHasTrick(actor,2) && wasBurning)
                    EronyaHeal(actor, actor, EronyaVolkanarInt(actor, "legionario-fuego-interior", "heal", 6), "legionario-fuego-interior");
                cells.Add(target.Unit.CurrentCell);
            }
            else if (id == "dagata")
            {
                bool third = EronyaHasTrick(actor,2) && EronyaHealthFraction(target) < (decimal)V(actor, "dagata-caza-de-brasas", "healthThreshold", 50) / 100m;
                int hits = EronyaVolkanarInt(actor, effect, "hitCount", 2);
                for (int i = 0; i < hits; i++)
                    EronyaDealDamage(actor, target, V(actor, effect, "damagePerHit", 10), effect);
                if (third) EronyaDealDamage(actor, target, V(actor, "dagata-caza-de-brasas", "bonusDamage", 9), "dagata-caza-de-brasas");
                cells.Add(target.Unit.CurrentCell);
            }
            EronyaEmitAbility(actor, effect, cells.ToArray());
        }
        private void EronyaVolkanarImpactBomb(ActorClock actor,ActorClock target,string effect)
        {
            var cells=new List<BoardCell>();
                var center = target.Unit.CurrentCell;
                int radius = EronyaVolkanarInt(actor, effect, "radius", 1);
                string damageEffect = EronyaHasTrick(actor,0) ? "canonero-del-orvek-carga-fundida" : effect;
                int duration = EronyaHasTrick(actor,1) ? EronyaVolkanarTicks(actor, "canonero-del-orvek-ascuas-persistentes", "duration", 5) :
                    EronyaVolkanarTicks(actor, effect, "burnDuration", 3);
                foreach (var enemy in SelectAbilityArea(actor,effect,center,target.Unit.Side,
                    ()=>EronyaInRadius(center,target.Unit.Side,radius)))
                {
                    if (IsInvisible(enemy.Unit)) continue; // Original radius is already enforced by the legacy selector.
                    EronyaDealDamage(actor, enemy, V(actor, damageEffect, "damage", EronyaHasTrick(actor,0) ? 20 : 15), effect);
                    EronyaPeriodic(actor, enemy, EronyaStatusKind.Burn, effect,
                        EronyaVolkanarInt(actor, effect, "burnDamage", 1), duration, 10);
                    if (EronyaHasTrick(actor,2))
                        EronyaApplyStatus(actor, enemy, EronyaStatusKind.AntiHeal, "canonero-del-orvek-horno-abierto",
                            V(actor, "canonero-del-orvek-horno-abierto", "healingReduction", 30) / 100f,
                            EronyaVolkanarTicks(actor, "canonero-del-orvek-horno-abierto", "duration", 4));
                    cells.Add(enemy.Unit.CurrentCell);
                }
            if (!HasEditedArea(actor,effect)) AreaEffect?.Invoke(actor.Unit,effect,cells.ToArray());
        }

        private void EronyaVolkanarBasicImpact(ActorClock actor, EronyaHit hit)
        {
            if (!EronyaVolkanarOwns(actor) || hit == null || !hit.Landed || hit.Origin != EronyaDamageOrigin.Basic ||
                hit.Target == null || hit.Target.Unit == null || hit.SourceResetVersion != actor.Unit.CombatResetVersion ||
                hit.TargetResetVersion != hit.Target.Unit.CombatResetVersion) return;
            string id = Id(actor);
            if (id == "serya") EronyaVolkanarBurn(actor, hit.Target, "serya-impetu", "damage", 1, "duration", 3);
            else if (id == "canonero-del-orvek") EronyaVolkanarBurn(actor, hit.Target, "canonero-del-orvek-escoria", "damage", 2, "duration", 3);
            else if (id == "dagata")
            {
                var state = EronyaVolkanarStateFor(actor);
                if (!state.HiddenBladeReady || !EronyaHasTrick(actor,0)) return;
                state.HiddenBladeReady = false;
                EronyaDealDamage(actor, hit.Target, V(actor, "dagata-hoja-oculta", "bonusDamage", 7), "dagata-hoja-oculta");
            }
        }
        private void EronyaVolkanarKilled(ActorClock killer, ActorClock victim, EronyaHit finalHit)
        {
            if (!EronyaVolkanarOwns(killer) || Id(killer) != "serya" || !killer.Unit.IsAlive || !EronyaHasTrick(killer,2) ||
                finalHit == null || finalHit.Source != killer || finalHit.SourceResetVersion != killer.Unit.CombatResetVersion) return;
            const string effect = "serya-conquista";
            var state = EronyaVolkanarStateFor(killer);
            if (state.ConquestCount >= EronyaVolkanarInt(killer, effect, "maxActivations", 2)) return;
            state.ConquestCount++;
            EronyaHeal(killer, killer, EronyaVolkanarInt(killer, effect, "heal", 14), effect);
            EronyaApplyStatus(killer, killer, EronyaStatusKind.AttackSpeed, effect,
                V(killer, effect, "attackSpeed", 20) / 100f, EronyaVolkanarTicks(killer, effect, "duration", 4));
            EronyaEmitAbility(killer, effect, new[] { killer.Unit.CurrentCell });
        }
        private void EronyaVolkanarEndTick(ActorClock actor) { }
        private void EronyaVolkanarBeforeOutgoing(ActorClock source, EronyaDamage damage) { }
        private void EronyaVolkanarBeforeDamage(ActorClock defender, EronyaDamage damage) { }
        private void EronyaVolkanarAfterDamage(ActorClock defender, EronyaHit hit) { }
        private void EronyaVolkanarAfterOwnDamage(ActorClock source, EronyaHit hit) { }
        private void EronyaVolkanarAfterOwnImpact(ActorClock source, EronyaHit hit) { }
        private void EronyaVolkanarShieldEnded(ActorClock owner, EronyaShieldEvent ended) { }
        private void EronyaVolkanarStatusAttempt(ActorClock target, ActorClock source, EronyaStatusKind kind, string effectId) { }
        private void EronyaVolkanarMovementStarted(ActorClock actor) { }
        private void EronyaVolkanarMoved(ActorClock actor, BoardCell from, BoardCell to, bool forced) { CancelDagataJumpOnMovement(actor,to); }
        private void EronyaVolkanarInterrupt(ActorClock actor, EronyaInterruptReason reason) { CancelDagataJump(actor); }
        private void EronyaVolkanarDied(ActorClock actor) { CancelDagataJump(actor); EronyaVolkanarStates.Remove(actor); }
        private void EronyaVolkanarEnd(ActorClock actor) { CancelDagataJump(actor); EronyaVolkanarStates.Remove(actor); }
        private void EronyaVolkanarClear() { foreach(var pair in EronyaVolkanarStates)CancelDagataJump(pair.Key,pair.Value); EronyaVolkanarStates.Clear(); }
    }
}

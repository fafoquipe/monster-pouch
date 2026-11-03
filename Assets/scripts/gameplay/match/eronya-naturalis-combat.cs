using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;

namespace MonsterPouch.Gameplay.Match
{
    // Contracts through 1.9. All vital/status changes pass through the authoritative core.
    public sealed partial class CombatSimulation
    {
        private sealed class EronyaNaturalisState
        {
            public int ResetVersion;
            public int StationarySince;
            public bool StationaryReductionAttempted;
            public int LandedBasics;
            public int AncestralHeartUses;
        }

        private readonly Dictionary<ActorClock, EronyaNaturalisState> EronyaNaturalisStates =
            new Dictionary<ActorClock, EronyaNaturalisState>();

        private EronyaNaturalisState EronyaNaturalisStateFor(ActorClock actor)
        {
            if (!EronyaNaturalisStates.TryGetValue(actor, out var state) ||
                state.ResetVersion != actor.Unit.CombatResetVersion)
            {
                state = new EronyaNaturalisState
                {
                    ResetVersion = actor.Unit.CombatResetVersion,
                    StationarySince = EronyaNowTick
                };
                EronyaNaturalisStates[actor] = state;
            }
            return state;
        }

        private int EronyaNaturalisTicks(ActorClock actor, string effect, string key, float fallback) =>
            (int)Math.Round(V(actor, effect, key, fallback) * 10f, MidpointRounding.AwayFromZero);

        private int EronyaNaturalisInt(ActorClock actor, string effect, string key, int fallback) =>
            (int)Math.Round(V(actor, effect, key, fallback), MidpointRounding.AwayFromZero);

        private bool EronyaNaturalisAlive(ActorClock actor) =>
            actor != null && actor.Unit != null && actor.Unit.IsAlive && EronyaHealth(actor) > 0m;

        private void EronyaNaturalisBegin(ActorClock actor)
        {
            EronyaNaturalisStates.Remove(actor);
            EronyaNaturalisStateFor(actor);
        }

        private void EronyaNaturalisTick(ActorClock actor)
        {
            if(!EronyaNaturalisAlive(actor)||Id(actor)!="guarda-veterano")return;
            if(EronyaIsMoving(actor)){EronyaNaturalisMovementStarted(actor);return;}
            var state=EronyaNaturalisStateFor(actor);
            const string effect="guarda-veterano-dialogo-con-la-raiz";
            if(state.StationaryReductionAttempted||EronyaNowTick-state.StationarySince<
                EronyaNaturalisTicks(actor,effect,"stationaryDuration",2))return;
            state.StationaryReductionAttempted=true;
            EronyaApplyPermanentStatus(actor,actor,EronyaStatusKind.DamageReduction,effect,
                V(actor,effect,"reduction",10)/100f,CombatStatusTimingKind.Conditional);
        }

        private void EronyaNaturalisEndTick(ActorClock actor)
        {
            if (!EronyaNaturalisAlive(actor) || Id(actor) != "arboreo" || !EronyaHasTrick(actor,2)) return;
            var state = EronyaNaturalisStateFor(actor);
            const string effect = "arboreo-corazon-ancestral";
            decimal threshold = (decimal)V(actor, effect, "thresholdPercent", 30f) / 100m;
            if (state.AncestralHeartUses >= EronyaNaturalisInt(actor, effect, "limitPerRound", 1) || EronyaHealthFraction(actor) >= threshold) return;
            state.AncestralHeartUses++;
            var allies = SelectAbilityArea(actor,effect,actor.Unit.CurrentCell,actor.Unit.Side,
                ()=>EronyaAllies(actor,1,false),includeSource:false,excluded:actor.Unit.CurrentCell);
            EronyaHeal(actor, actor, EronyaNaturalisInt(actor, effect, "heal", 22), effect);
            foreach (var ally in allies)
                EronyaHeal(actor, ally, EronyaNaturalisInt(actor, effect, "allyHeal", 10), effect);
            var cells = new List<BoardCell> { actor.Unit.CurrentCell };
            foreach (var ally in allies) cells.Add(ally.Unit.CurrentCell);
            EronyaEmitAbility(actor, effect, cells.ToArray());
        }

        private ActorClock EronyaNaturalisEnemy(ActorClock actor, int range, EronyaCastPlan plan = null)
        {
            var enemies = EronyaEnemies(actor, range);
            if (plan != null && plan.Target != null)
                foreach (var enemy in enemies) if (enemy.Unit == plan.Target) return enemy;
            var basic = EronyaBasicTarget(actor);
            if (basic != null && enemies.Contains(basic)) return basic;
            return enemies.Count > 0 ? enemies[0] : null;
        }

        private bool EronyaNaturalisTryPlanCast(ActorClock actor, out EronyaCastPlan plan)
        {
            plan = null;
            if (!EronyaNaturalisAlive(actor)) return false;
            ActorClock target;
            string effect;
            switch (Id(actor))
            {
                case "cael":
                    effect = "cael-torbellino-de-hojas";
                    target = EronyaNaturalisEnemy(actor, EronyaNaturalisInt(actor, effect, "range", 3));
                    break;
                case "guarda-veterano":
                    effect = "guarda-veterano-liana-vinculante";
                    target = EronyaNaturalisEnemy(actor, EronyaNaturalisInt(actor, effect, "range", 2));
                    break;
                case "arboreo":
                    effect = "arboreo-abrazo-del-bosque";
                    target = EronyaNaturalisArboreoPlanTarget(actor);
                    break;
                case "antidota":
                    effect = "antidota-brote-vital";
                    target = EronyaMostInjured(actor, EronyaNaturalisInt(actor, effect, "range", 3));
                    if (target != null && EronyaHealthFraction(target) >= 1m) target = null;
                    break;
                default: return false;
            }
            if (target == null) return false;
            plan = new EronyaCastPlan { EffectId = effect, Target = target.Unit };
            return true;
        }

        private ActorClock EronyaNaturalisArboreoPlanTarget(ActorClock actor)
        {
            const string active = "arboreo-abrazo-del-bosque";
            if (!HasEditedArea(actor,active))
                return EronyaNaturalisEnemy(actor,EronyaNaturalisInt(actor,active,"radius",1));
            // Planning/revalidation is read-only: no telegraph, damage, shield or basic lock mutation.
            var targets = SelectAbilityArea(actor,active,actor.Unit.CurrentCell,
                actor.Unit.Side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue,
                ()=>Array.Empty<ActorClock>(),visibleOnly:true,emitEffect:false);
            var basic = EronyaBasicTarget(actor);
            if (basic != null && targets.Contains(basic)) return basic;
            if (targets.Count > 0) return targets[0];
            return EronyaNaturalisArboreoShieldOnlyTarget(actor);
        }

        private ActorClock EronyaNaturalisArboreoShieldOnlyTarget(ActorClock actor)
        {
            const string shield = "arboreo-corteza-renovada";
            if (!EronyaHasTrick(actor,0) || EronyaNaturalisInt(actor,shield,"shield",18) <= 0 ||
                EronyaNaturalisTicks(actor,shield,"duration",4f) <= 0) return null;
            // Keep an already-legal local activation for the purchased self shield when editing
            // removes its offensive recipients. No enemy / no useful shield => no empty cast.
            return EronyaNaturalisEnemy(actor,EronyaNaturalisInt(actor,"arboreo-abrazo-del-bosque","radius",1));
        }

        private void EronyaNaturalisResolveCast(ActorClock actor, EronyaCastPlan plan)
        {
            if (!EronyaNaturalisAlive(actor) || plan == null) return;
            switch (Id(actor))
            {
                case "cael": EronyaNaturalisCaelCast(actor, plan); break;
                case "guarda-veterano": EronyaNaturalisGuardaCast(actor, plan); break;
                case "arboreo": EronyaNaturalisArboreoCast(actor); break;
                case "antidota": EronyaNaturalisAntidotaCast(actor); break;
            }
        }

        private void EronyaNaturalisCaelCast(ActorClock actor, EronyaCastPlan plan)
        {
            const string active = "cael-torbellino-de-hojas";
            const string last = "cael-mandato-de-raiz-madre";

            var target = EronyaNaturalisEnemy(actor, EronyaNaturalisInt(actor, active, "range", 3), plan);
            if (target == null) return;
            var affected = SelectAbilityArea(actor,active,target.Unit.CurrentCell,target.Unit.Side,()=>
                EronyaInRadius(target.Unit.CurrentCell,target.Unit.Side,EronyaNaturalisInt(actor,active,"radius",1)));
            var cells = new List<BoardCell>();
            foreach (var enemy in affected)
            {
                bool primary = enemy == target;
                float amount = V(actor, EronyaHasTrick(actor,2) ? last : active,
                    primary ? "damage" : "secondaryDamage", primary ? (EronyaHasTrick(actor,2) ? 25 : 19) : (EronyaHasTrick(actor,2) ? 15 : 11));
                EronyaDealDamage(actor, enemy, amount, active);
                if (EronyaHasTrick(actor,0))
                    EronyaApplyStatus(actor, enemy, EronyaStatusKind.Slow, "cael-hojas-tenaces",
                        V(actor, "cael-hojas-tenaces", "slowPercent", 20f) / 100f,
                        EronyaNaturalisTicks(actor, "cael-hojas-tenaces", "duration", 3f));
                if (primary && EronyaHasTrick(actor,2))
                    EronyaApplyStatus(actor, enemy, EronyaStatusKind.Root, last, 1f,
                        EronyaNaturalisTicks(actor, last, "rootDuration", 1.2f));
                cells.Add(enemy.Unit.CurrentCell);
            }
            if (EronyaHasTrick(actor,1))
            {
                const string heal = "cael-savia-dorada";
                var other = EronyaMostInjured(actor, EronyaNaturalisInt(actor, heal, "range", 2), false);
                EronyaHeal(actor, actor, EronyaNaturalisInt(actor, heal, "heal", 10), heal);
                if (other != null) EronyaHeal(actor, other, EronyaNaturalisInt(actor, heal, "allyHeal", 7), heal);
            }
            EronyaEmitAbility(actor, active, cells.ToArray());
        }

        private void EronyaNaturalisGuardaCast(ActorClock actor, EronyaCastPlan plan)
        {
            const string active = "guarda-veterano-liana-vinculante";
            const string last = "guarda-veterano-zarza-despierta";

            var target = EronyaNaturalisEnemy(actor, EronyaNaturalisInt(actor, active, "range", 2), plan);
            if (target == null) return;
            EronyaDealDamage(actor, target, V(actor, EronyaHasTrick(actor,2) ? last : active, "damage", EronyaHasTrick(actor,2) ? 13 : 8),
                active);
            string root = EronyaHasTrick(actor,0) ? "guarda-veterano-vinculo-profundo" : active;
            EronyaApplyStatus(actor, target, EronyaStatusKind.Root, root, 1f,
                EronyaNaturalisTicks(actor, root, "rootDuration", EronyaHasTrick(actor,0) ? 2.5f : 1.5f));
            if (EronyaHasTrick(actor,1))
            {
                const string shield = "guarda-veterano-corteza-compartida";
                var ally = EronyaMostInjured(actor, EronyaNaturalisInt(actor, shield, "range", 3));
                if (ally != null) EronyaShield(actor, ally, shield, EronyaNaturalisInt(actor, shield, "shield", 9),
                    EronyaNaturalisTicks(actor, shield, "duration", 4f));
            }
            if (EronyaHasTrick(actor,2))
                EronyaPeriodic(actor, target, EronyaStatusKind.Poison, last,
                    EronyaNaturalisInt(actor, last, "poisonDamage", 2),
                    EronyaNaturalisTicks(actor, last, "poisonDuration", 3f), 10);
            EronyaEmitAbility(actor, active, new[] { target.Unit.CurrentCell });
        }

        private void EronyaNaturalisArboreoCast(ActorClock actor)
        {
            const string active = "arboreo-abrazo-del-bosque";
            var targets = SelectAbilityArea(actor,active,actor.Unit.CurrentCell,
                actor.Unit.Side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue,
                ()=>EronyaEnemies(actor,EronyaNaturalisInt(actor,active,"radius",1)),visibleOnly:true);
            if (targets.Count == 0 && (!HasEditedArea(actor,active) || EronyaNaturalisArboreoShieldOnlyTarget(actor) == null)) return;

            var cells = new List<BoardCell>();
            foreach (var enemy in targets)
            {
                EronyaDealDamage(actor, enemy, V(actor, active, "damage", 11f), active);
                if (EronyaCanReach(enemy, actor))
                    EronyaApplyStatus(actor, enemy, EronyaStatusKind.Taunt, active, 1f,
                        EronyaNaturalisTicks(actor, active, "tauntDuration", 2f));
                if (EronyaHasTrick(actor,1))
                    EronyaApplyStatus(actor, enemy, EronyaStatusKind.Root, "arboreo-bosque-cerrado", 1f,
                        EronyaNaturalisTicks(actor, "arboreo-bosque-cerrado", "rootDuration", 1.5f));
                cells.Add(enemy.Unit.CurrentCell);
            }
            if (EronyaHasTrick(actor,0))
                EronyaShield(actor, actor, "arboreo-corteza-renovada",
                    EronyaNaturalisInt(actor, "arboreo-corteza-renovada", "shield", 18),
                    EronyaNaturalisTicks(actor, "arboreo-corteza-renovada", "duration", 4f));
            EronyaEmitAbility(actor, active, cells.ToArray());
        }

        private void EronyaNaturalisAntidotaCast(ActorClock actor)
        {
            const string active = "antidota-brote-vital";
            var primary = EronyaMostInjured(actor, EronyaNaturalisInt(actor, active, "range", 3));
            if (primary == null || EronyaHealthFraction(primary) >= 1m) return;

            // Snapshot both recipients before queuing either healing delta.
            var secondary = EronyaHasTrick(actor,2) ? EronyaMostInjured(primary,
                EronyaNaturalisInt(actor, "antidota-floracion", "radius", 1), false) : null;
            string heal = EronyaHasTrick(actor,0) ? "antidota-raiz-nutritiva" : active;
            EronyaHeal(actor, primary, EronyaNaturalisInt(actor, heal, "heal", EronyaHasTrick(actor,0) ? 22 : 17), active);
            EronyaPeriodic(actor, primary, EronyaStatusKind.Regeneration, active,
                EronyaNaturalisInt(actor, active, "regen", 3),
                EronyaNaturalisTicks(actor, EronyaHasTrick(actor,1) ? "antidota-ciclo-fertil" : active, "duration", EronyaHasTrick(actor,1) ? 5f : 3f), 10);
            var cells = new List<BoardCell> { primary.Unit.CurrentCell };
            if (secondary != null)
            {
                EronyaHeal(actor, secondary, EronyaNaturalisInt(actor, "antidota-floracion", "heal", 10), "antidota-floracion");
                cells.Add(secondary.Unit.CurrentCell);
            }
            EronyaEmitAbility(actor, active, cells.ToArray());
        }

        private void EronyaNaturalisBasicImpact(ActorClock actor, EronyaHit hit)
        {
            if (!EronyaNaturalisAlive(actor) || hit == null || !hit.Landed || hit.Origin != EronyaDamageOrigin.Basic) return;
            string id = Id(actor);
            if (id != "cael" && id != "antidota") return;
            var state = EronyaNaturalisStateFor(actor);
            string effect = id == "cael" ? "cael-voluntad-del-guardian" : "antidota-savia-compartida";
            int period = Math.Max(1, EronyaNaturalisInt(actor, effect, "everyAttacks", 3));
            state.LandedBasics = (state.LandedBasics + 1) % period;
            if (state.LandedBasics != 0) return;
            if (id == "cael")
            {
                if (EronyaNaturalisAlive(hit.Target))
                    EronyaApplyStatus(actor, hit.Target, EronyaStatusKind.Root, effect, 1f,
                        EronyaNaturalisTicks(actor, effect, "rootDuration", .8f));
            }
            else
            {
                var ally = EronyaMostInjured(actor, EronyaNaturalisInt(actor, effect, "range", 4));
                if (ally == null) return;
                // Core snapshots effective antiheal when Heal is queued. Cleanse removes one,
                // poison before burn, and must not retroactively change this queued healing.
                EronyaHeal(actor, ally, EronyaNaturalisInt(actor, effect, "heal", 4), effect);
                EronyaCleanse(ally, true);
                EronyaEmitAbility(actor, effect, new[] { ally.Unit.CurrentCell });
            }
        }

        // Read-only projection for central CollectPassiveProgress; do not create state while drawing.
        private int EronyaNaturalisPassiveProgress(ActorClock actor) =>
            actor != null && actor.Unit != null && EronyaNaturalisStates.TryGetValue(actor, out var state) &&
            state.ResetVersion == actor.Unit.CombatResetVersion ? state.LandedBasics : 0;

        private void EronyaNaturalisStatusAttempt(ActorClock target, ActorClock source, EronyaStatusKind kind, string effectId)
        {
            if (!EronyaNaturalisAlive(target) || Id(target) != "arboreo" || kind != EronyaStatusKind.Root) return;
            const string effect = "arboreo-raices-profundas";
            EronyaApplyStatus(target, target, EronyaStatusKind.AttackSpeed, effect,
                V(target, effect, "speedPercent", 15f) / 100f,
                EronyaNaturalisTicks(target, effect, "duration", 4f));
        }

        private void EronyaNaturalisMovementStarted(ActorClock actor)
        {
            if(Id(actor)!="guarda-veterano")return;
            var state=EronyaNaturalisStateFor(actor);
            state.StationarySince=EronyaNowTick;state.StationaryReductionAttempted=false;
            EronyaRemoveEffect(actor,"guarda-veterano-dialogo-con-la-raiz");
        }

        private void EronyaNaturalisMoved(ActorClock actor, BoardCell from, BoardCell to, bool forced) =>
            EronyaNaturalisMovementStarted(actor);
        private void EronyaNaturalisBeforeDamage(ActorClock defender, EronyaDamage damage) { }
        private void EronyaNaturalisAfterDamage(ActorClock defender, EronyaHit hit) { }
        private void EronyaNaturalisBeforeOutgoing(ActorClock source, EronyaDamage damage) { }
        private void EronyaNaturalisAfterOwnDamage(ActorClock source, EronyaHit hit) { }
        private void EronyaNaturalisAfterOwnImpact(ActorClock source, EronyaHit hit)
        {
            if (hit.Settled && hit.ValidContact && hit.Origin == EronyaDamageOrigin.Basic &&
                hit.Target?.Unit != null && hit.Target.Unit.CombatResetVersion == hit.TargetResetVersion)
                AreaEffect?.Invoke(source.Unit, "naturalis-basic-contact", new[] { hit.Target.Unit.CurrentCell });
        }
        private void EronyaNaturalisKilled(ActorClock killer, ActorClock victim, EronyaHit finalHit) { }
        private void EronyaNaturalisShieldEnded(ActorClock owner, EronyaShieldEvent ended) { }
        private void EronyaNaturalisInterrupt(ActorClock actor, EronyaInterruptReason reason) { }
        private void EronyaNaturalisDied(ActorClock actor) => EronyaNaturalisStates.Remove(actor);
        private void EronyaNaturalisEnd(ActorClock actor) => EronyaNaturalisStates.Remove(actor);
        private void EronyaNaturalisClear() => EronyaNaturalisStates.Clear();
    }
}

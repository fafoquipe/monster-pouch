using System;
using System.Linq;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    // Faction implementation for core contract 1.8. Shared settlement and status arbitration stay in core.
    public sealed partial class CombatSimulation
    {
        private sealed class EronyaGeotharState
        {
            public int ResetVersion, StationarySince;
            public bool StationaryApplied, EmergencyUsed, FirstDamageSpent;
        }
        private readonly Dictionary<ActorClock, EronyaGeotharState> EronyaGeotharStates =
            new Dictionary<ActorClock, EronyaGeotharState>();
        private readonly HashSet<long> EronyaGeotharEndedShields = new HashSet<long>();

        private bool EronyaGeotharOwns(ActorClock actor)
        {
            if (actor?.Unit == null) return false;
            string id = Id(actor);
            return id == "korran" || id == "golem-tectonico" || id == "la-minera" || id == "protectora-del-mausoleo";
        }
        private bool EronyaGeotharAlive(ActorClock actor) => actor?.Unit != null &&
            actor.UnitResetVersion == actor.Unit.CombatResetVersion && EronyaHealth(actor) > 0m;
        private EronyaGeotharState EronyaGeotharStateFor(ActorClock actor)
        {
            if (!EronyaGeotharStates.TryGetValue(actor, out var state) || state.ResetVersion != actor.Unit.CombatResetVersion)
            {
                state = new EronyaGeotharState { ResetVersion = actor.Unit.CombatResetVersion, StationarySince = EronyaNowTick };
                EronyaGeotharStates[actor] = state;
            }
            return state;
        }
        private int EronyaGeotharInt(ActorClock actor, string effect, string key, int fallback) =>
            (int)Math.Round(V(actor, effect, key, fallback), MidpointRounding.AwayFromZero);
        private int EronyaGeotharTicks(ActorClock actor, string effect, string key, float seconds) =>
            (int)Math.Round(V(actor, effect, key, seconds) * 10f, MidpointRounding.AwayFromZero);
        private bool EronyaGeotharEnemy(ActorClock actor, ActorClock target, int range) =>
            EronyaGeotharAlive(target) && actor.Unit.Side != target.Unit.Side &&
            actor.Unit.CurrentCell != null && target.Unit.CurrentCell != null &&
            EronyaDistance(actor.Unit.CurrentCell, target.Unit.CurrentCell) <= range;
        // These Supers act on the basic target. A legal diagonal basic must not
        // strand a full energy bar behind a second, Manhattan-only range gate.
        // Keep the edited Super range and all area/ally radii independent.
        private bool EronyaGeotharMeleeSuperTarget(ActorClock actor, ActorClock target, int range) =>
            EronyaGeotharEnemy(actor, target, int.MaxValue) &&
            CombatTargetSelector.IsInAttackRange(actor.Unit.CurrentCell, target.Unit.CurrentCell, range) &&
            EronyaCanReach(actor, target);
        private string EronyaGeotharStationaryEffect(ActorClock actor) => Id(actor) == "korran" ?
            "korran-juramento-de-permanencia" : "golem-tectonico-puerta-cerrada";

        private void EronyaGeotharBegin(ActorClock actor)
        {
            if (!EronyaGeotharOwns(actor)) return;
            EronyaGeotharStates[actor] = new EronyaGeotharState
            { ResetVersion = actor.Unit.CombatResetVersion, StationarySince = EronyaNowTick };
            if (Id(actor) == "la-minera")
            {
                const string effect = "la-minera-tunel-subterraneo";
                var destination = EronyaTunnelDestination(actor, EronyaGeotharInt(actor, effect, "distance", 5));
                if (destination != null) EronyaReposition(actor, destination, false, CombatDisplacementKind.Tunnel,
                    EronyaGeotharTicks(actor, effect, "duration", 0));
            }
            else if(Id(actor)=="protectora-del-mausoleo")
            {
                const string effect="protectora-del-mausoleo-memoria-de-la-roca";
                const string shared="protectora-del-mausoleo-memoria-compartida";
                bool upgraded=EronyaHasTrick(actor,1);
                var allies=EronyaAllies(actor,upgraded?EronyaGeotharInt(actor,shared,"range",4):int.MaxValue,false);
                int count=upgraded?EronyaGeotharInt(actor,shared,"targetCount",2):1;
                for(int i=0;i<Math.Min(count,allies.Count);i++)
                    EronyaShield(actor,allies[i],effect,EronyaGeotharInt(actor,effect,"shield",10),
                        EronyaGeotharTicks(actor,effect,"duration",4));
            }
        }
        private void EronyaGeotharResetStationary(ActorClock actor)
        {
            if (!EronyaGeotharOwns(actor) || (Id(actor) != "korran" && Id(actor) != "golem-tectonico")) return;
            var state = EronyaGeotharStateFor(actor);
            state.StationarySince = EronyaNowTick;
            state.StationaryApplied = false;
            EronyaRemoveEffect(actor, EronyaGeotharStationaryEffect(actor));
        }
        private void EronyaGeotharTick(ActorClock actor)
        {
            if (!EronyaGeotharOwns(actor) || !EronyaGeotharAlive(actor)) return;
            if (Id(actor) != "korran" && Id(actor) != "golem-tectonico") return;
            if (EronyaIsMoving(actor)) { EronyaGeotharResetStationary(actor); return; }
            var state = EronyaGeotharStateFor(actor);
            if (state.StationaryApplied) return;
            string effect = EronyaGeotharStationaryEffect(actor);
            int elapsed = EronyaNowTick - state.StationarySince;
            int required = EronyaGeotharTicks(actor, effect, "stationarySeconds", 2);
            if (Id(actor) == "korran" ? elapsed < required : elapsed <= required) return;
            // An application that loses arbitration is discarded, never retried every tick.
            state.StationaryApplied = true;
            EronyaApplyPermanentStatus(actor, actor, EronyaStatusKind.DamageReduction, effect,
                V(actor, effect, "damageReductionPercent", 15) / 100f);
        }
        private void EronyaGeotharEndTick(ActorClock actor)
        {
            const string effect = "korran-el-hogar-resiste";
            if (!EronyaGeotharOwns(actor) || !EronyaGeotharAlive(actor) || Id(actor) != "korran" || !Has(actor, effect)) return;
            var state = EronyaGeotharStateFor(actor);
            if (state.EmergencyUsed || EronyaHealthFraction(actor) >= (decimal)V(actor, effect, "healthThresholdPercent", 35) / 100m) return;
            state.EmergencyUsed = true;
            EronyaShield(actor, actor, effect, EronyaGeotharInt(actor, effect, "shield", 25), EronyaGeotharTicks(actor, effect, "duration", 6));
            EronyaCleanse(actor);
            EronyaEmitAbility(actor, effect, new[] { actor.Unit.CurrentCell });
        }
        private bool EronyaGeotharTryPlanCast(ActorClock actor, out EronyaCastPlan plan)
        {
            plan = null;
            if (!EronyaGeotharOwns(actor) || !EronyaGeotharAlive(actor) || actor.Unit.CurrentCell == null) return false;
            string id = Id(actor), effect;
            ActorClock target;
            if (id == "korran") { effect = "korran-bastion-imperial"; target = actor; }
            else if (id == "protectora-del-mausoleo")
            {
                effect = "protectora-del-mausoleo-sello-ancestral";
                target = EronyaMostInjured(actor, EronyaGeotharInt(actor, effect, "range", 4));
            }
            else
            {
                effect = id == "la-minera" ? "la-minera-fractura" : "golem-tectonico-guardia-de-piedra";
                target = EronyaSelectBasicTarget(actor);
                if (!EronyaGeotharMeleeSuperTarget(actor, target, EronyaGeotharInt(actor, effect, "range", 1))) return false;
            }
            if (target == null) return false;
            plan = new EronyaCastPlan { EffectId = effect, Target = target.Unit };
            return true;
        }
        private void EronyaGeotharResolveCast(ActorClock actor, EronyaCastPlan plan)
        {
            if (!EronyaGeotharOwns(actor) || !EronyaGeotharAlive(actor) || plan == null) return;
            string effect = plan.EffectId;
            var target = Clock(plan.Target);
            switch (effect)
            {
                case "korran-bastion-imperial":
                    EronyaShield(actor, actor, effect, EronyaGeotharInt(actor, effect, "shield", 22), EronyaGeotharTicks(actor, effect, "duration", 5));
                    string mandate = "korran-mandato-firme";
                    int tauntTicks = Has(actor, mandate) ? EronyaGeotharTicks(actor, mandate, "tauntDuration", 3) : EronyaGeotharTicks(actor, effect, "tauntDuration", 1.5f);
                    foreach (var enemy in SelectAbilityArea(actor,effect,actor.Unit.CurrentCell,
                        actor.Unit.Side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue,()=>EronyaKorranTauntTargets(actor),visibleOnly:true)
                        .Where(enemy=>EronyaCanReach(enemy,actor)))
                        EronyaApplyStatus(actor, enemy, EronyaStatusKind.Taunt, effect, 1, tauntTicks);
                    const string root = "korran-raiz-de-piedra";
                    if (Has(actor, root)) foreach (var ally in SelectAbilityArea(actor,root,actor.Unit.CurrentCell,actor.Unit.Side,
                        ()=>EronyaAllies(actor,1,false),includeSource:false,excluded:actor.Unit.CurrentCell))
                        EronyaShield(actor, ally, root, EronyaGeotharInt(actor, root, "allyShield", 8), EronyaGeotharTicks(actor, root, "duration", 5));
                    break;
                case "golem-tectonico-guardia-de-piedra":
                    if (!EronyaGeotharMeleeSuperTarget(actor, target, EronyaGeotharInt(actor, effect, "range", 1))) return;
                    const string reinforced = "golem-tectonico-escudo-reforzado";
                    int capacity = Has(actor, reinforced) ? EronyaGeotharInt(actor, reinforced, "shield", 30) : EronyaGeotharInt(actor, effect, "shield", 22);
                    EronyaShield(actor, actor, effect, capacity, EronyaGeotharTicks(actor, effect, "duration", 4));
                    if (EronyaCanReach(target, actor)) EronyaApplyStatus(actor, target, EronyaStatusKind.Taunt, effect, 1, EronyaGeotharTicks(actor, effect, "tauntDuration", 1.5f));
                    const string shelter = "golem-tectonico-amparo";
                    if (Has(actor, shelter))
                    {
                        var ally = EronyaMostInjured(actor, 1, false);
                        if (ally != null) EronyaShield(actor, ally, shelter, EronyaGeotharInt(actor, shelter, "allyShield", 8), EronyaGeotharTicks(actor, shelter, "duration", 4));
                    }
                    break;
                case "la-minera-fractura":
                    if (!EronyaGeotharMeleeSuperTarget(actor, target, EronyaGeotharInt(actor, effect, "range", 1))) return;
                    // One primary packet; the shield-breaking hit keeps its pre-absorption bonus.
                    float fractureDamage=V(actor,effect,"damage",17);
                    if(EronyaHasTemporaryShield(target))fractureDamage+=V(actor,effect,"shieldBonusDamage",9);
                    EronyaDealDamage(actor,target,fractureDamage,effect);
                    const string vein = "la-minera-veta-expuesta";
                    if (Has(actor, vein)) EronyaDrainMana(target, V(actor, vein, "manaDrain", 10));
                    const string collapse = "la-minera-derrumbe";
                    if (Has(actor, collapse)) foreach (var secondary in SelectAbilityArea(actor,collapse,target.Unit.CurrentCell,target.Unit.Side,
                        ()=>EronyaInRadius(target.Unit.CurrentCell,target.Unit.Side,EronyaGeotharInt(actor,collapse,"radius",1)),excluded:target.Unit.CurrentCell))
                        if (secondary != target) EronyaDealDamage(actor, secondary, V(actor, collapse, "secondaryDamage", 8), collapse);
                    break;
                case "protectora-del-mausoleo-sello-ancestral":
                    if (!EronyaGeotharAlive(target) || actor.Unit.Side != target.Unit.Side || target.Unit.CurrentCell == null ||
                        EronyaDistance(actor.Unit.CurrentCell, target.Unit.CurrentCell) > EronyaGeotharInt(actor, effect, "range", 4)) return;
                    EronyaShield(actor, target, effect, EronyaGeotharInt(actor, effect, "shield", 19), EronyaGeotharTicks(actor, effect, "duration", 5));
                    const string name = "protectora-del-mausoleo-nombre-protegido";
                    if (Has(actor, name)) EronyaHeal(actor, target, EronyaGeotharInt(actor, name, "heal", 12), name);
                    break;
                default: return;
            }
            EronyaEmitAbility(actor, effect, new[] { target?.Unit.CurrentCell ?? actor.Unit.CurrentCell });
        }
        private void EronyaGeotharAfterOwnDamage(ActorClock source, EronyaHit hit)
        {
            if (!EronyaGeotharOwns(source) || Id(source) != "la-minera" || hit == null ||
                hit.PreciseHealthDamage + hit.PreciseShieldDamage <= 0m) return;
            var state = EronyaGeotharStateFor(source);
            if (state.FirstDamageSpent) return;
            state.FirstDamageSpent = true;
            if (EronyaGeotharAlive(source) && EronyaGeotharEnemy(source, hit.Target, int.MaxValue))
                EronyaApplyStatus(source,hit.Target,EronyaStatusKind.Taunt,"la-minera-tunel-subterraneo",1,
                    EronyaGeotharTicks(source,"la-minera-tunel-subterraneo","tauntDuration",2));
        }
        private void EronyaGeotharKilled(ActorClock killer, ActorClock victim, EronyaHit finalHit)
        {
            const string effect = "la-minera-recuperacion-de-veta";
            if (!EronyaGeotharOwns(killer) || !EronyaGeotharAlive(killer) || Id(killer) != "la-minera" || !Has(killer, effect)) return;
            EronyaPeriodic(killer, killer, EronyaStatusKind.Regeneration, effect, EronyaGeotharInt(killer, effect, "healPerSecond", 4),
                EronyaGeotharTicks(killer, effect, "duration", 3), 10);
        }
        private void EronyaGeotharShieldEnded(ActorClock owner, EronyaShieldEvent ended)
        {
            if (!EronyaGeotharOwns(owner) || ended == null || ended.Source != owner ||
                (ended.Reason != EronyaShieldEndReason.Consumed && ended.Reason != EronyaShieldEndReason.Expired) ||
                !EronyaGeotharEndedShields.Add(ended.InstanceId)) return;
            if (ended.EffectId == "korran-bastion-imperial" && ended.Target == owner && ended.Reason == EronyaShieldEndReason.Consumed)
            {
                const string mandate = "korran-mandato-firme";
                float damage = Has(owner, mandate) ? V(owner, mandate, "breakDamage", 18) : V(owner, ended.EffectId, "breakDamage", 12);
                foreach (var enemy in SelectAbilityArea(owner,ended.EffectId,owner.Unit.CurrentCell,
                    owner.Unit.Side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue,()=>EronyaEnemies(owner,1),visibleOnly:true))
                    EronyaDealDamage(owner, enemy, damage, ended.EffectId);
                // Area-only feedback: rupture must not replay a cast or consume mana.
                if (!HasEditedArea(owner,ended.EffectId) && owner.Unit.CurrentCell != null) AreaEffect?.Invoke(owner.Unit,
                    "korran-bastion-imperial:rupture", new[] { owner.Unit.CurrentCell });
            }
            else if (ended.EffectId == "golem-tectonico-guardia-de-piedra" && ended.Target == owner && EronyaGeotharAlive(owner))
            {
                const string effect = "golem-tectonico-relevo-de-guardia";
                if (Has(owner, effect)) EronyaHeal(owner, owner, EronyaGeotharInt(owner, effect, "heal", 12), effect);
            }
            else if (ended.EffectId == "protectora-del-mausoleo-sello-ancestral" && ended.Reason == EronyaShieldEndReason.Consumed && EronyaGeotharAlive(ended.Target))
            {
                const string effect = "protectora-del-mausoleo-ultima-inscripcion";
                if (Has(owner, effect)) EronyaApplyStatus(owner, ended.Target, EronyaStatusKind.AttackSpeed, effect,
                    V(owner, effect, "attackSpeedPercent", 20) / 100f, EronyaGeotharTicks(owner, effect, "duration", 4));
            }
        }
        private void EronyaGeotharMovementStarted(ActorClock actor) => EronyaGeotharResetStationary(actor);
        private void EronyaGeotharMoved(ActorClock actor, BoardCell from, BoardCell to, bool forced) => EronyaGeotharResetStationary(actor);
        private void EronyaGeotharBasicImpact(ActorClock actor, EronyaHit hit) { }
        private void EronyaGeotharAfterOwnImpact(ActorClock actor, EronyaHit hit) { }
        private void EronyaGeotharBeforeDamage(ActorClock defender, EronyaDamage damage) { }
        private void EronyaGeotharAfterDamage(ActorClock defender, EronyaHit hit) { }
        private void EronyaGeotharBeforeOutgoing(ActorClock source, EronyaDamage damage) { }
        private void EronyaGeotharStatusAttempt(ActorClock target, ActorClock source, EronyaStatusKind kind, string effectId) { }
        private void EronyaGeotharInterrupt(ActorClock actor, EronyaInterruptReason reason) { }
        // Do not remove source-owned Sello on caster death.
        private void EronyaGeotharDied(ActorClock actor) { }
        private void EronyaGeotharEnd(ActorClock actor) { if (actor != null) EronyaGeotharStates.Remove(actor); }
        private void EronyaGeotharClear() { EronyaGeotharStates.Clear(); EronyaGeotharEndedShields.Clear(); }
    }
}

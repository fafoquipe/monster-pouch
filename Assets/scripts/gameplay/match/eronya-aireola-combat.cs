using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;

namespace MonsterPouch.Gameplay.Match
{
    // Faction-owned staging. All combat mutations go through the central API.
    // Contract 1.8. Post-damage effects run only on the settled valid primary packet.
    public sealed partial class CombatSimulation
    {
        private const string EronyaAireolaWinds = "alveyr-escudo-de-los-vientos";
        private const string EronyaAireolaDiscipline = "alveyr-viento-disciplinado";
        private const string EronyaAireolaCloudStep = "alveyr-paso-de-nube";
        private const string EronyaAireolaSphere = "zephalontropo-esfera-comprimida";
        private const string EronyaAireolaExpanded = "zephalontropo-carga-expandida";
        private const string EronyaAireolaShock = "zephalontropo-onda-de-choque";
        private const string EronyaAireolaTempo = "alboguera-compas-ascendente";
        private const string EronyaAireolaProtection = "alboguera-armonia-protectora";
        private const string EronyaAireolaSustain = "alboguera-final-sostenido";
        private const string EronyaAireolaGale = "palista-golpe-de-vendaval";

        private sealed class EronyaAireolaActorState
        {
            public int ResetVersion;
            public int WindsUntil;
            public decimal ReceivedDamage;
            public decimal ManaGranted;
        }
        private readonly Dictionary<ActorClock, EronyaAireolaActorState> EronyaAireolaStates =
            new Dictionary<ActorClock, EronyaAireolaActorState>();
        private readonly HashSet<long> EronyaAireolaProcessedImpacts = new HashSet<long>();

        private static bool EronyaAireolaOwns(ActorClock actor)
        {
            string id = Id(actor);
            return id == "alveyr" || id == "zephalontropo" || id == "alboguera" || id == "palista";
        }

        private EronyaAireolaActorState EronyaAireolaState(ActorClock actor)
        {
            if (actor?.Unit == null) return null;
            return EronyaAireolaStates.TryGetValue(actor, out var value) &&
                value.ResetVersion == actor.Unit.CombatResetVersion ? value : null;
        }

        private static int EronyaAireolaInteger(ActorClock actor, string effect, string key, float fallback) =>
            Math.Max(0, (int)Math.Round(V(actor, effect, key, fallback), MidpointRounding.AwayFromZero));
        private static int EronyaAireolaTicks(ActorClock actor, string effect, string key, float fallback) =>
            Math.Max(0, (int)Math.Round(V(actor, effect, key, fallback) * 10d, MidpointRounding.AwayFromZero));

        private void EronyaAireolaBegin(ActorClock actor)
        {
            if (!EronyaAireolaOwns(actor)) return;
            EronyaAireolaStates[actor] = new EronyaAireolaActorState { ResetVersion = actor.Unit.CombatResetVersion };
            if (Id(actor) == "alveyr")
            {
                const string effect = "alveyr-anillo-al-cielo";
                var origin = actor.Unit.CurrentCell;
                var target = EronyaEnemies(actor, int.MaxValue).FirstOrDefault(enemy =>
                    enemy.Unit.CurrentCell.X == origin.X &&
                    (enemy.Unit.CurrentCell.Y - origin.Y) * Forward(actor.Unit) > 0);
                if (target == null) return;
                EronyaSetDamageSuppressed(target, effect,
                    EronyaNowTick + EronyaAireolaTicks(actor, effect, "duration", 3));
                EronyaEmitAbility(actor, effect, new[] { target.Unit.CurrentCell });
            }
            else if (Id(actor) == "alboguera")
            {
                const string effect = "alboguera-afinacion";
                var allies = SelectAbilityArea(actor,effect,actor.Unit.CurrentCell,actor.Unit.Side,
                    ()=>EronyaAllies(actor,1,false),includeSource:false,excluded:actor.Unit.CurrentCell);
                foreach (var ally in allies) EronyaAddMana(ally, V(actor, effect, "mana", 10));
                EronyaEmitAbility(actor, effect, allies.Select(a => a.Unit.CurrentCell).ToArray());
            }
            // Palista's stun immunity is enforced by core before control side effects.
        }

        private bool EronyaAireolaWindsActive(ActorClock actor)
        {
            var state = EronyaAireolaState(actor);
            return state != null && state.WindsUntil > EronyaNowTick;
        }

        // Read-only projection of the existing authoritative status slot. No UI timer or extra HUD badge.
        public bool TryCaptureAireolaWinds(MonsterPouch.Gameplay.Units.BattleUnit unit, out CombatStatusSnapshot snapshot)
        {
            snapshot = default;
            var actor = unit == null ? null : Clock(unit);
            if (!begun || Finished || actor == null || !unit.IsAlive || unit.IsReviving || !EronyaAireolaWindsActive(actor)) return false;
            var slot = EronyaState(actor).AttackBlocked;
            if (!slot.Active(tick) || slot.EffectId != EronyaAireolaWinds) return false;
            snapshot = new CombatStatusSnapshot("attack-blocked", slot.EffectId, slot.SourceId, slot.InstanceId,
                unit.CombatResetVersion, slot.AppliedTick, slot.Until, 0, (float)slot.Magnitude, tick, slot.ApplicationRevision);
            return true;
        }

        private void EronyaAireolaFinishWinds(ActorClock actor)
        {
            var state = EronyaAireolaState(actor);
            if (state == null || state.WindsUntil == 0 || state.WindsUntil > EronyaNowTick) return;
            state.WindsUntil = 0;
            state.ReceivedDamage = state.ManaGranted = 0m;
            if (actor.Unit.IsAlive && Has(actor, EronyaAireolaCloudStep))
                EronyaApplyStatus(actor, actor, EronyaStatusKind.AttackSpeed, EronyaAireolaCloudStep,
                    V(actor, EronyaAireolaCloudStep, "attackSpeed", .2f),
                    EronyaAireolaTicks(actor, EronyaAireolaCloudStep, "duration", 3));
        }
        private void EronyaAireolaTick(ActorClock actor) { EronyaAireolaFinishWinds(actor); }
        private void EronyaAireolaEndTick(ActorClock actor) { EronyaAireolaFinishWinds(actor); }

        private bool EronyaAireolaTryPlanCast(ActorClock actor, out EronyaCastPlan plan)
        {
            plan = null;
            if (actor?.Unit == null || !actor.Unit.IsAlive) return false;
            ActorClock target;
            switch (Id(actor))
            {
                case "alveyr":
                    if (EronyaAireolaWindsActive(actor)) return false;
                    plan = new EronyaCastPlan { EffectId = EronyaAireolaWinds, Target = actor.Unit };
                    return true;
                case "zephalontropo":
                    target = EronyaEnemies(actor, EronyaAireolaInteger(actor, EronyaAireolaSphere, "range", 4)).FirstOrDefault();
                    if (target != null) plan = new EronyaCastPlan { EffectId = EronyaAireolaSphere, Target = target.Unit };
                    break;
                case "alboguera":
                    target = EronyaHighestBasicDamageAlly(actor, EronyaAireolaInteger(actor, EronyaAireolaTempo, "range", 4));
                    if (target != null) plan = new EronyaCastPlan { EffectId = EronyaAireolaTempo, Target = target.Unit };
                    break;
                case "palista":
                    var basic = EronyaSelectBasicAction(actor);
                    target = basic.Status == CombatTargetSelectionStatus.ReadyToAttack ? Clock(basic.Target) : null;
                    if (target != null) plan = new EronyaCastPlan { EffectId = EronyaAireolaGale, Target = target.Unit };
                    break;
            }
            return plan != null;
        }

        private void EronyaAireolaResolveCast(ActorClock actor, EronyaCastPlan plan)
        {
            if (plan == null || actor?.Unit == null || !actor.Unit.IsAlive) return;
            switch (Id(actor))
            {
                case "alveyr": EronyaAireolaReleaseWinds(actor); break;
                case "zephalontropo": EronyaAireolaReleaseSphere(actor, Clock(plan.Target)); break;
                case "alboguera": EronyaAireolaReleaseTempo(actor, Clock(plan.Target)); break;
                case "palista": EronyaAireolaReleaseGale(actor, Clock(plan.Target)); break;
            }
        }

        private void EronyaAireolaReleaseWinds(ActorClock actor)
        {
            EronyaAireolaFinishWinds(actor);
            var state = EronyaAireolaState(actor);
            if (state == null || EronyaAireolaWindsActive(actor)) return;
            int duration = EronyaAireolaTicks(actor, EronyaAireolaWinds, "duration", 5);
            state.WindsUntil = EronyaNowTick + duration;
            state.ReceivedDamage = state.ManaGranted = 0m;
            EronyaSetAttackBlocked(actor, EronyaAireolaWinds, state.WindsUntil);
            var reachable = EronyaEnemies(actor, int.MaxValue).Where(e => EronyaCanReach(e, actor)).ToList();
            foreach (var enemy in reachable)
                EronyaApplyStatus(actor, enemy, EronyaStatusKind.Taunt, EronyaAireolaWinds, 1, duration);
            if (Has(actor, "alveyr-circulo-de-calma"))
                foreach (var ally in SelectAbilityArea(actor,"alveyr-circulo-de-calma",actor.Unit.CurrentCell,actor.Unit.Side,
                    ()=>EronyaAllies(actor,1,false),includeSource:false,excluded:actor.Unit.CurrentCell)) EronyaCleanse(ally);
            EronyaEmitAbility(actor, EronyaAireolaWinds,
                reachable.Select(e => e.Unit.CurrentCell).Concat(new[] { actor.Unit.CurrentCell }).ToArray());
        }

        private void EronyaAireolaReleaseSphere(ActorClock actor, ActorClock primary)
        {
            if (primary == null || !primary.Unit.IsAlive) return;
            EronyaQueueProjectile(actor,primary,EronyaAireolaSphere,()=>EronyaAireolaImpactSphere(actor,primary));
        }
        private void EronyaAireolaImpactSphere(ActorClock actor,ActorClock primary)
        {
            int radius = EronyaAireolaInteger(actor, EronyaAireolaSphere, "radius", 1);
            var affected = SelectAbilityArea(actor,EronyaAireolaSphere,primary.Unit.CurrentCell,primary.Unit.Side,
                ()=>EronyaInRadius(primary.Unit.CurrentCell,primary.Unit.Side,radius));
            var cells = affected.Select(t => t.Unit.CurrentCell).ToArray();
            string damageEffect = Has(actor, EronyaAireolaExpanded) ? EronyaAireolaExpanded : EronyaAireolaSphere;
            EronyaDealDamage(actor, primary, V(actor, damageEffect, "damage", damageEffect == EronyaAireolaExpanded ? 23 : 18),
                EronyaAireolaSphere);
            foreach (var secondary in affected.Where(t => t != primary))
                EronyaDealDamage(actor, secondary, V(actor, damageEffect, "secondaryDamage", damageEffect == EronyaAireolaExpanded ? 12 : 9),
                    EronyaAireolaSphere);
            // Primary remains packet0; sample the existing area at actual arrival before hit reactions push it.
            if (!HasEditedArea(actor,EronyaAireolaSphere)) AreaEffect?.Invoke(actor.Unit,EronyaAireolaSphere,cells);
        }

        private void EronyaAireolaReleaseTempo(ActorClock actor, ActorClock primary)
        {
            if (primary == null || !primary.Unit.IsAlive) return;
            bool sustained = Has(actor, EronyaAireolaSustain);
            int duration = EronyaAireolaTicks(actor, sustained ? EronyaAireolaSustain : EronyaAireolaTempo,
                "duration", sustained ? 6 : 4);
            var affected = SelectAbilityArea(actor,EronyaAireolaTempo,primary.Unit.CurrentCell,actor.Unit.Side,()=>
                EronyaInRadius(primary.Unit.CurrentCell,actor.Unit.Side,EronyaAireolaInteger(actor,EronyaAireolaTempo,"radius",1)));
            foreach (var ally in affected)
            {
                float haste = V(actor, EronyaAireolaTempo, "attackSpeed", .2f);
                if (Has(actor, "alboguera-tempo-vivo"))
                    haste = V(actor, "alboguera-tempo-vivo", ally == primary ? "primaryAttackSpeed" : "secondaryAttackSpeed", ally == primary ? .3f : .2f);
                EronyaApplyStatus(actor, ally, EronyaStatusKind.AttackSpeed, EronyaAireolaTempo, haste, duration);
                if (Has(actor, EronyaAireolaProtection))
                    EronyaShield(actor, ally, EronyaAireolaProtection,
                        EronyaAireolaInteger(actor, EronyaAireolaProtection, "shield", 7),
                        sustained ? duration : EronyaAireolaTicks(actor, EronyaAireolaProtection, "duration", 4));
            }
            EronyaEmitAbility(actor, EronyaAireolaTempo, affected.Select(a => a.Unit.CurrentCell).ToArray());
        }

        private void EronyaAireolaReleaseGale(ActorClock actor, ActorClock target)
        {
            if (target == null || !target.Unit.IsAlive) return;
            const string firm = "palista-palma-firme";
            EronyaDealDamage(actor, target, V(actor, Has(actor, firm) ? firm : EronyaAireolaGale, "damage", Has(actor, firm) ? 16 : 12),
                EronyaAireolaGale);
            EronyaEmitAbility(actor, EronyaAireolaGale, new[] { target.Unit.CurrentCell });
        }

        private void EronyaAireolaBeforeOutgoing(ActorClock source, EronyaDamage damage)
        {
            if(Id(source)!="zephalontropo"||damage.Origin!=EronyaDamageOrigin.Basic||
                !EronyaHasTemporaryShield(damage.Target))return;
            bool improved=EronyaHasTrick(source,0);
            damage.Amount+=V(source,improved?"zephalontropo-camara-de-precision":"zephalontropo-presion-calibrada",
                "shieldBonusDamage",improved?6:4);
        }

        private void EronyaAireolaBeforeDamage(ActorClock defender, EronyaDamage damage)
        {
            if (Id(defender) != "alveyr" || !EronyaAireolaWindsActive(defender)) return;
            bool improved = Has(defender, EronyaAireolaDiscipline) && EronyaHealthFraction(defender) <
                (decimal)V(defender, EronyaAireolaDiscipline, "healthThreshold", .2f);
            damage.Reduction = V(defender, improved ? EronyaAireolaDiscipline : EronyaAireolaWinds, "reduction", improved ? .5f : .4f);
            damage.ReductionEffectId = EronyaAireolaWinds;
        }

        private void EronyaAireolaAfterDamage(ActorClock defender, EronyaHit hit)
        {
            if (Id(defender) != "alveyr" || !EronyaAireolaWindsActive(defender)) return;
            if (hit.CanReflect && hit.ReductionEffectId == EronyaAireolaWinds && hit.PreciseOwnReductionPrevented > 0m && hit.Source != null)
                EronyaDealDamage(defender, hit.Source, (float)hit.PreciseOwnReductionPrevented,
                    EronyaAireolaWinds, EronyaDamageOrigin.Reflection, 0, false);
            if (!defender.Unit.IsAlive) return;
            var state = EronyaAireolaState(defender);
            decimal threshold = (decimal)V(defender, EronyaAireolaWinds, "receivedDamageThreshold", 100);
            decimal grant = (decimal)V(defender, EronyaAireolaWinds, "manaPerThreshold", 10);
            decimal cap = (decimal)V(defender, EronyaAireolaWinds, "manaPerCastCap", 30);
            if (threshold <= 0m || grant <= 0m || cap <= state.ManaGranted) return;
            state.ReceivedDamage += hit.PreciseHealthDamage + hit.PreciseShieldDamage;
            decimal available = Math.Min(cap, decimal.Floor(state.ReceivedDamage / threshold) * grant) - state.ManaGranted;
            if (available <= 0m) return;
            state.ManaGranted += available;
            EronyaAddMana(defender, (float)available);
        }

        private void EronyaAireolaBasicImpact(ActorClock actor, EronyaHit hit) { }
        private void EronyaAireolaAfterOwnDamage(ActorClock source, EronyaHit hit) { }
        private void EronyaAireolaAfterOwnImpact(ActorClock source, EronyaHit hit)
        {
            if (hit.Settled && hit.ValidContact && hit.Origin == EronyaDamageOrigin.Basic &&
                hit.Target?.Unit != null && hit.Target.Unit.CombatResetVersion == hit.TargetResetVersion)
            {
                // Contact only: includes fully prevented hits, excludes ability/periodic/reflection packets.
                AreaEffect?.Invoke(source.Unit, "aireola-basic-contact", new[] { hit.Target.Unit.CurrentCell });
                return;
            }
            if (!hit.Settled || !hit.ValidContact || hit.Origin != EronyaDamageOrigin.Ability || hit.PacketIndex != 0) return;
            if (hit.EffectId != EronyaAireolaSphere && hit.EffectId != EronyaAireolaGale) return;
            if (!EronyaAireolaProcessedImpacts.Add(hit.EventId)) return;
            var target = hit.Target;
            if (Id(source) == "zephalontropo" && hit.EffectId == EronyaAireolaSphere)
            {
                if (!Has(source, EronyaAireolaShock) || target?.Unit == null || !target.Unit.IsAlive) return;
                EronyaPush(source, target, EronyaAireolaInteger(source, EronyaAireolaShock, "pushCells", 1));
                EronyaApplyStatus(source, target, EronyaStatusKind.Stun, EronyaAireolaShock, 1,
                    EronyaAireolaTicks(source, EronyaAireolaShock, "stunDuration", .7f));
            }
            else if (Id(source) == "palista" && hit.EffectId == EronyaAireolaGale)
            {
                if (source.Unit.IsAlive && Has(source, "palista-paso-ligero"))
                    EronyaApplyStatus(source, source, EronyaStatusKind.AttackSpeed, "palista-paso-ligero",
                        V(source, "palista-paso-ligero", "attackSpeed", .2f), EronyaAireolaTicks(source, "palista-paso-ligero", "duration", 3));
                if (target?.Unit == null || !target.Unit.IsAlive) return;
                EronyaPush(source, target, EronyaAireolaInteger(source, EronyaAireolaGale, "pushCells", 1));
                if (Has(source, "palista-aire-cortante"))
                    EronyaApplyStatus(source, target, EronyaStatusKind.Slow, "palista-aire-cortante",
                        V(source, "palista-aire-cortante", "slow", .25f), EronyaAireolaTicks(source, "palista-aire-cortante", "duration", 3));
            }
        }
        private void EronyaAireolaKilled(ActorClock killer, ActorClock victim, EronyaHit hit) { }
        private void EronyaAireolaStatusAttempt(ActorClock target, ActorClock source, EronyaStatusKind kind, string effectId) { }
        private void EronyaAireolaShieldEnded(ActorClock owner, EronyaShieldEvent ended) { }
        private void EronyaAireolaMovementStarted(ActorClock actor) { }
        private void EronyaAireolaMoved(ActorClock actor, BoardCell from, BoardCell to, bool forced) { }
        private void EronyaAireolaInterrupt(ActorClock actor, EronyaInterruptReason reason)
        {
            if (reason == EronyaInterruptReason.Death || reason == EronyaInterruptReason.Reset || reason == EronyaInterruptReason.End)
                EronyaAireolaStates.Remove(actor);
            // A post-release winds effect is not an active cast preparation. Stun does not recast/refund it.
        }
        private void EronyaAireolaDied(ActorClock actor) { EronyaAireolaStates.Remove(actor); }
        private void EronyaAireolaEnd(ActorClock actor) { EronyaAireolaStates.Remove(actor); }
        private void EronyaAireolaClear() { EronyaAireolaStates.Clear(); EronyaAireolaProcessedImpacts.Clear(); }
    }
}

using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    // Staging module for core contract 1.6. No legacy helpers/stacking paths.
    public sealed partial class CombatSimulation
    {
        private sealed class EronyaAquarionState
        {
            public int ResetVersion, Basics, FirmIceAttacks, DeepAnchorAttacks;
            public readonly Dictionary<long, bool> FirmIceBlocks = new Dictionary<long, bool>();
            public readonly Dictionary<long, bool> DeepAnchorBlocks = new Dictionary<long, bool>();
        }
        private readonly Dictionary<ActorClock, EronyaAquarionState> EronyaAquarionStates =
            new Dictionary<ActorClock, EronyaAquarionState>();

        private bool EronyaAquarionOwns(ActorClock actor)
        {
            if (actor == null || actor.Unit == null) return false;
            string id = Id(actor);
            return id == "drayven" || id == "titanica" || id == "jabalinero" || id == "maestro-de-las-mareas";
        }
        private EronyaAquarionState EronyaAquarionStateFor(ActorClock actor)
        {
            if (!EronyaAquarionStates.TryGetValue(actor, out var state) ||
                state.ResetVersion != actor.Unit.CombatResetVersion)
            {
                state = new EronyaAquarionState { ResetVersion = actor.Unit.CombatResetVersion };
                EronyaAquarionStates[actor] = state;
            }
            return state;
        }
        private int EronyaAquarionInt(ActorClock actor, string effect, string key, int fallback) =>
            (int)Math.Round(V(actor, effect, key, fallback), MidpointRounding.AwayFromZero);
        private int EronyaAquarionTicks(ActorClock actor, string effect, string key, float seconds) =>
            (int)Math.Round(V(actor, effect, key, seconds) * 10, MidpointRounding.AwayFromZero);
        private int EronyaAquarionEvery(ActorClock actor, string effect, string key, int fallback) =>
            Math.Max(1, EronyaAquarionInt(actor, effect, key, fallback));
        private string EronyaAquarionActive(ActorClock actor) =>
            Id(actor) == "drayven" ? "drayven-marea-del-norte" : Id(actor) == "titanica" ? "titanica-ancla-glacial" :
            Id(actor) == "jabalinero" ? "jabalinero-red-de-cienaga" : "maestro-de-las-mareas-oleaje";
        private int EronyaAquarionRange(ActorClock actor) =>
            Math.Max(0, EronyaAquarionInt(actor, EronyaAquarionActive(actor), "range", Id(actor) == "titanica" ? 1 : 3));
        private bool EronyaAquarionLegal(ActorClock actor, ActorClock target, int range) =>
            target != null && target.Unit != null && target.Unit.IsAlive &&
            actor.Unit.Side != target.Unit.Side && actor.Unit.CurrentCell != null && target.Unit.CurrentCell != null &&
            EronyaEnemies(actor, range).Contains(target);
        private ActorClock EronyaAquarionTarget(ActorClock actor, int range)
        {
            var basic = EronyaBasicTarget(actor);
            if (EronyaAquarionLegal(actor, basic, range)) return basic;
            var enemies = EronyaEnemies(actor, range);
            return enemies.Count == 0 ? null : enemies[0];
        }
        private void EronyaAquarionSlow(ActorClock actor, ActorClock target, string effect, float percent, float seconds)
        {
            EronyaApplyStatus(actor, target, EronyaStatusKind.Slow, effect,
                V(actor, effect, "slowPercent", percent) / 100f,
                EronyaAquarionTicks(actor, effect, "duration", seconds));
        }
        private void EronyaAquarionPoison(ActorClock actor, ActorClock target)
        {
            const string passive = "jabalinero-punta-toxica";
            string strength = EronyaHasTrick(actor,0) ? "jabalinero-veneno-maduro" : passive;
            int damage = EronyaAquarionInt(actor, strength, "damagePerSecond", EronyaHasTrick(actor,0) ? 3 : 2);
            // Core owns poison's anti-heal bucket and single winning source/expiry.
            EronyaPeriodic(actor, target, EronyaStatusKind.Poison, passive, damage,
                EronyaAquarionTicks(actor, passive, "duration", 3), 10);
        }
        private void EronyaAquarionBegin(ActorClock actor)
        {
            if (!EronyaAquarionOwns(actor)) return;
            EronyaAquarionStates[actor] = new EronyaAquarionState { ResetVersion = actor.Unit.CombatResetVersion };
            if (Id(actor) == "maestro-de-las-mareas")
                EronyaAddMana(actor, V(actor, "maestro-de-las-mareas-reflejo-lunar", "startingMana", 15));
        }
        private void EronyaAquarionBasicImpact(ActorClock actor, EronyaHit hit)
        {
            if (!EronyaAquarionOwns(actor) || hit == null || !hit.Landed || hit.Origin != EronyaDamageOrigin.Basic || hit.Target == null) return;
            var state = EronyaAquarionStateFor(actor);
            state.Basics++;
            string id = Id(actor);
            if (id == "titanica") EronyaAquarionSlow(actor, hit.Target, "titanica-hielo-firme", 15, 1.5f);
            else if (id == "drayven" && state.Basics % EronyaAquarionEvery(actor, "drayven-dominio-de-las-mareas", "everyHits", 3) == 0)
            {
                const string effect = "drayven-dominio-de-las-mareas";
                EronyaDealDamage(actor, hit.Target, V(actor, effect, "damage", 4), effect);
                EronyaAquarionSlow(actor, hit.Target, effect, 20, 2);
            }
            else if (id == "jabalinero" && state.Basics % EronyaAquarionEvery(actor, "jabalinero-punta-toxica", "everyHits", 3) == 0)
                EronyaAquarionPoison(actor, hit.Target);
        }
        private void EronyaAquarionBeforeDamage(ActorClock defender, EronyaDamage damage)
        {
            if (defender == null || Id(defender) != "titanica" || damage == null || !damage.IsAttack || damage.Amount <= 0 ||
                (damage.Origin != EronyaDamageOrigin.Basic && damage.Origin != EronyaDamageOrigin.Ability) ||
                damage.TargetResetVersion != defender.Unit.CombatResetVersion) return;
            var state = EronyaAquarionStateFor(defender);
            // Both authored effects now observe general attack contacts. Their
            // serialized parameter keys and per-attack deduplication stay intact.
            if (!state.FirmIceBlocks.TryGetValue(damage.AttackId, out bool block))
            {
                block = ++state.FirmIceAttacks % EronyaAquarionEvery(defender,
                    "titanica-hielo-firme", "magicEvery", 4) == 0;
                state.FirmIceBlocks.Add(damage.AttackId, block);
            }
            if (EronyaHasTrick(defender,1))
            {
                if (!state.DeepAnchorBlocks.TryGetValue(damage.AttackId, out bool additional))
                {
                    additional = ++state.DeepAnchorAttacks % EronyaAquarionEvery(defender,
                        "titanica-ancla-profunda", "physicalEvery", 5) == 0;
                    state.DeepAnchorBlocks.Add(damage.AttackId, additional);
                }
                block |= additional;
            }
            if (block) damage.Cancel = true;
        }
        private MaestroWaveFlight EronyaAquarionWaveFlight(ActorClock actor)
        {
            const string effect = MaestroWaveFlight.EffectId;
            return new MaestroWaveFlight(actor.Unit.CurrentCell.X, actor.Unit.CurrentCell.Y,
                Forward(actor.Unit), EronyaAquarionInt(actor, effect, "width", 3),
                EronyaAquarionInt(actor, effect, "length", MaestroWaveFlight.DefaultLength),
                V(actor, effect, "waveSpeed", MaestroWaveFlight.DefaultSpeed),
                (x, y) => board.GetCell(x, y) == null || board.GetCell(x, y).IsBlocked);
        }
        // Corridor geometry is explicitly agreed in core1.3; no targeting fallback sideways.
        private ActorClock EronyaAquarionWaveTarget(ActorClock actor)
        {
            BoardCell origin = actor.Unit.CurrentCell;
            if (origin == null) return null;
            var wave = EronyaAquarionWaveFlight(actor);
            int forward = Forward(actor.Unit), nearestRowDistance = int.MaxValue;
            ActorClock chosen = null;
            foreach (var enemy in EronyaEnemies(actor, BoardManager.Width + BoardManager.Height))
            {
                var cell = enemy.Unit.CurrentCell;
                if (cell == null || !wave.ContainsPath(cell.X, cell.Y)) continue;
                int ahead = (cell.Y - origin.Y) * forward;
                if (ahead <= 0 || ahead > Math.Max(1, EronyaAquarionInt(actor, "maestro-de-las-mareas-oleaje", "length", MaestroWaveFlight.DefaultLength)) || ahead >= nearestRowDistance) continue;
                nearestRowDistance = ahead; chosen = enemy;
            }
            return chosen;
        }
        private bool EronyaAquarionTryPlanCast(ActorClock actor, out EronyaCastPlan plan)
        {
            plan = null;
            if (!EronyaAquarionOwns(actor) || !actor.Unit.IsAlive || actor.Unit.CurrentCell == null) return false;
            string id = Id(actor);
            ActorClock target = id == "maestro-de-las-mareas" ? EronyaAquarionWaveTarget(actor) :
                EronyaAquarionTarget(actor, EronyaAquarionRange(actor));
            if (target == null) return false;
            string effect = EronyaAquarionActive(actor);
            plan = new EronyaCastPlan { EffectId = effect, Target = target.Unit };
            return true;
        }
        private void EronyaAquarionResolveCast(ActorClock actor, EronyaCastPlan plan)
        {
            if (!EronyaAquarionOwns(actor) || plan == null || !actor.Unit.IsAlive) return;

            string id = Id(actor), effect = plan.EffectId;
            if (id == "maestro-de-las-mareas") { EronyaAquarionResolveWave(actor); return; }
            var target = plan.Target == null ? null : Clock(plan.Target);
            if (!EronyaAquarionLegal(actor, target, EronyaAquarionRange(actor))) return;
            if (id == "drayven")
            {
                var cells = EronyaLine(actor.Unit.CurrentCell, target.Unit.CurrentCell,
                    EronyaAquarionInt(actor, effect, "length", 3));
                var enemies = EronyaEnemies(actor, BoardManager.Width + BoardManager.Height);
                bool first = true;
                foreach (var cell in cells) foreach (var enemy in enemies)
                {
                    if (enemy.Unit.CurrentCell != cell) continue;
                    EronyaDealDamage(actor, enemy, V(actor, EronyaHasTrick(actor,1) ? "drayven-mar-obediente" : effect,
                        "damage", EronyaHasTrick(actor,1) ? 22 : 16), effect);
                    EronyaAquarionSlow(actor, enemy, effect, 25, 3);
                    if (first && EronyaHasTrick(actor,0)) EronyaApplyStatus(actor, enemy, EronyaStatusKind.Stun,
                        "drayven-escarcha-del-puerto", 1, EronyaAquarionTicks(actor, "drayven-escarcha-del-puerto", "stunDuration", .6f));
                    first = false;
                }
                if (EronyaHasTrick(actor,2)) foreach (var ally in SelectAbilityArea(actor,"drayven-juramento-de-skeldris",
                    actor.Unit.CurrentCell,actor.Unit.Side,()=>EronyaAllies(actor,1))) EronyaShield(actor, ally,
                    "drayven-juramento-de-skeldris", EronyaAquarionInt(actor, "drayven-juramento-de-skeldris", "shield", 10),
                    EronyaAquarionTicks(actor, "drayven-juramento-de-skeldris", "duration", 4));
                EronyaEmitAbility(actor, effect, cells);
            }
            else if (id == "titanica")
            {
                EronyaDealDamage(actor, target, V(actor, effect, "damage", 10), effect);
                EronyaApplyStatus(actor, target, EronyaStatusKind.Root, effect, 1,
                    EronyaAquarionTicks(actor, EronyaHasTrick(actor,1) ? "titanica-ancla-profunda" : effect, "rootDuration", EronyaHasTrick(actor,1) ? 3 : 2));
                if (EronyaHasTrick(actor,0)) EronyaShield(actor, actor, "titanica-hielo-grueso",
                    EronyaAquarionInt(actor, "titanica-hielo-grueso", "shield", 10), EronyaAquarionTicks(actor, "titanica-hielo-grueso", "duration", 4));
                var cells = new List<BoardCell> { target.Unit.CurrentCell };
                if (EronyaHasTrick(actor,2)) foreach (var enemy in SelectAbilityArea(actor,"titanica-frente-helado",
                    actor.Unit.CurrentCell,target.Unit.Side,()=>EronyaEnemies(actor,1),visibleOnly:true))
                {
                    if (enemy == target) continue;
                    const string secondary = "titanica-frente-helado";
                    EronyaDealDamage(actor, enemy, V(actor, secondary, "damage", 7), secondary);
                    EronyaAquarionSlow(actor, enemy, secondary, 25, 3); cells.Add(enemy.Unit.CurrentCell);
                }
                EronyaEmitAbility(actor, effect, cells.ToArray());
            }
            else if (id == "jabalinero")
            {
                EronyaQueueProjectile(actor,target,effect,()=>
                {
                EronyaDealDamage(actor, target, V(actor, effect, "damage", 7), effect);
                EronyaApplyStatus(actor, target, EronyaStatusKind.Root, effect, 1,
                    EronyaAquarionTicks(actor, EronyaHasTrick(actor,1) ? "jabalinero-red-lastrada" : effect, "rootDuration", EronyaHasTrick(actor,1) ? 2 : 1.5f));
                if (EronyaHasTrick(actor,2)) EronyaAquarionPoison(actor, target);
                AreaEffect?.Invoke(actor.Unit,effect,new[]{target.Unit.CurrentCell});
                });
            }
        }
        private void EronyaAquarionResolveWave(ActorClock actor)
        {
            var target = EronyaAquarionWaveTarget(actor);
            if (target == null) return; // Core revalidates/refunds before successful release.
            const string effect = "maestro-de-las-mareas-oleaje";
            var wave = EronyaAquarionWaveFlight(actor);
            float damage = V(actor, EronyaHasTrick(actor,0) ? "maestro-de-las-mareas-luz-sobre-el-agua" : effect,
                "damage", EronyaHasTrick(actor,0) ? 10 : 8);
            int stun = EronyaAquarionTicks(actor, effect, "stunDuration", .5f);
            int heal = EronyaHasTrick(actor,2) ? EronyaAquarionInt(actor, "maestro-de-las-mareas-marea-compartida", "heal", 8) : 0;
            var side = actor.Unit.Side;
            var hit = new HashSet<MonsterPouch.Gameplay.Units.BattleUnit>();
            var healed = new HashSet<MonsterPouch.Gameplay.Units.BattleUnit>();
            var origin = actor.Unit.CurrentCell;
            EronyaQueueWave(actor, target, wave, (from, to) =>
            {
                // Popow-style swept contact: traverse units, once each; no target pursuit or bounce.
                foreach (var other in EronyaOrder(actors, origin))
                {
                    if (!EronyaValidActor(other) || other.Unit.IsFlying || other == actor) continue;
                    var cell = other.Unit.CurrentCell;
                    if (!wave.ContainsSweep(cell.X, cell.Y, from, to)) continue;
                    if (other.Unit.Side != side)
                    {
                        if (IsInvisible(other.Unit) || !hit.Add(other.Unit)) continue;
                        EronyaDealDamage(actor, other, damage, effect);
                        EronyaApplyStatus(actor, other, EronyaStatusKind.Stun, effect, 1, stun);
                    }
                    else if (heal > 0 && healed.Add(other.Unit))
                        EronyaHeal(actor, other, heal, "maestro-de-las-mareas-marea-compartida");
                }
            });
            if (EronyaHasTrick(actor,1)) foreach (var ally in SelectAbilityArea(actor,"maestro-de-las-mareas-manto-lunar",
                actor.Unit.CurrentCell,actor.Unit.Side,()=>EronyaAllies(actor,1)))
            {
                const string shield = "maestro-de-las-mareas-manto-lunar";
                bool self = ally == actor;
                EronyaShield(actor, ally, shield, EronyaAquarionInt(actor, shield, self ? "selfShield" : "allyShield", self ? 10 : 8),
                    EronyaAquarionTicks(actor, shield, "duration", 4));
            }
            // The release snapshot owns exactly one traveling crest; no path-wide AreaEffect.
        }
        private void EronyaAquarionTick(ActorClock actor) { }
        private void EronyaAquarionEndTick(ActorClock actor) { }
        private void EronyaAquarionBeforeOutgoing(ActorClock source, EronyaDamage damage) { }
        private void EronyaAquarionMovementStarted(ActorClock actor) { }
        private void EronyaAquarionAfterOwnDamage(ActorClock source, EronyaHit hit) { }
        private void EronyaAquarionAfterDamage(ActorClock defender, EronyaHit hit) { }
        private void EronyaAquarionKilled(ActorClock killer, ActorClock victim, EronyaHit finalHit) { }
        private void EronyaAquarionStatusAttempt(ActorClock target, ActorClock source, EronyaStatusKind kind, string effectId) { }
        private void EronyaAquarionShieldEnded(ActorClock owner, EronyaShieldEvent ended) { }
        private void EronyaAquarionMoved(ActorClock actor, BoardCell from, BoardCell to, bool forced) { }
        private void EronyaAquarionInterrupt(ActorClock actor, EronyaInterruptReason reason) { }
        private void EronyaAquarionDied(ActorClock actor) { EronyaAquarionStates.Remove(actor); }
        private void EronyaAquarionEnd(ActorClock actor) { EronyaAquarionStates.Remove(actor); }
        private void EronyaAquarionClear() { EronyaAquarionStates.Clear(); }
    }
}

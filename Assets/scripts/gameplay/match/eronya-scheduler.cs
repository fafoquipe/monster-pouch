using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private bool eronyaCombat;
        private readonly Dictionary<ActorClock,EronyaHit> eronyaLastHits=new Dictionary<ActorClock,EronyaHit>();
        private void EronyaSyncMana(ActorClock actor)
        {
            var mana=EronyaState(actor).Mana;if(mana==null)return;
            actor.Unit.ClearEnergy();actor.Unit.AddEnergy((float)mana.Value);
        }
        private void BeginEronyaCombat()
        {
            eronyaCombat=actors.Any(EronyaOwns);if(!eronyaCombat)return;
            EronyaCaptureRound();
            // Bind everyone before any opening passive touches another actor.
            foreach(var actor in actors.Where(EronyaOwns))EronyaBindModule(actor);
            foreach(var actor in actors.Where(EronyaOwns))EronyaState(actor).Module?.Begin?.Invoke();
            EronyaFlushPackets();
        }
        private void EronyaResetActor(ActorClock actor)
        {
            eronyaProjectiles.RemoveAll(p=>p.Source==actor||p.Target==actor);
            if(!eronyaStates.TryGetValue(actor,out var old))return;
            foreach(var recipient in eronyaStates.Values)
            {
                foreach(var slot in recipient.Statuses.Values)
                    if(slot.SourceId==actor.Unit.UnitId&&slot.SourceVersion==old.ResetVersion)slot.Clear();
                recipient.Shields.RemoveSource(actor.Unit.UnitId,old.ResetVersion,EronyaLedgerEndReason.Reset);
            }
            eronyaPeriodic.RemoveSource(actor.Unit.UnitId,old.ResetVersion);
            EronyaClearActor(actor,old,EronyaInterruptReason.Reset,EronyaLedgerEndReason.Reset);
            old.Module?.End?.Invoke();eronyaStates.Remove(actor);eronyaLastHits.Remove(actor);
            if(EronyaOwns(actor)){EronyaBindModule(actor);EronyaState(actor).Module?.Begin?.Invoke();}
        }
        private void EronyaClearActor(ActorClock actor,EronyaActorState state,EronyaInterruptReason reason,EronyaLedgerEndReason shieldReason)
        {
            state.Module?.Interrupt?.Invoke(reason);state.Cast.Reset();state.Attack.Reset();state.Plan=null;
            state.Control.Reset();state.AttackBlocked.Clear();state.DamageSuppressed.Clear();
            foreach(var slot in state.Statuses.Values)slot.Clear();
            eronyaPeriodic.RemoveTarget(actor.Unit.UnitId,state.ResetVersion);
            state.Shields.Clear(shieldReason);state.Shields.DrainEnded();state.Active=false;state.Revision++;
            actor.Unit.EronyaShieldDisplay=0;
        }
        private void EndEronyaCombat()
        {
            foreach(var pair in eronyaStates.ToArray())
            {EronyaClearActor(pair.Key,pair.Value,EronyaInterruptReason.End,EronyaLedgerEndReason.End);pair.Value.Module?.End?.Invoke();}
            eronyaProjectiles.Clear();eronyaPeriodic.Reset();eronyaDamageQueue.Clear();eronyaHealQueue.Clear();eronyaLastHits.Clear();
            eronyaAttackContext=null;eronyaAttackContexts.Clear();
        }
        private void ClearEronyaCombat()
        {
            EndEronyaCombat();foreach(var state in eronyaStates.Values)state.Module?.Clear?.Invoke();
            eronyaStates.Clear();eronyaCombat=false;eronyaEventSequence=eronyaAttackSequence=eronyaInstanceSequence=0;
            eronyaInitialHealth.Clear();eronyaHealthDamage.Clear();
        }
        private void TickEronyaCombat()
        {
            if(!eronyaCombat)return;
            CancelInvalidDagataJumps();
            foreach(var actor in actors.ToArray())
            {
                if(!EronyaValidActor(actor))continue;
                if(!eronyaStates.TryGetValue(actor,out var state))continue;
                state.Control.Expire(tick);
                foreach(var slot in state.Statuses.Values)slot.Expire(tick);
                state.AttackBlocked.Expire(tick);state.DamageSuppressed.Expire(tick);
                actor.Unit.IsSlowed=actor.AttackSpeedMultiplier<.999f||EronyaMagnitude(actor,EronyaStatusKind.Slow)>0m;
                if(!actor.Unit.IsSlowed)actor.Unit.SlowEffectId=null;
                if(state.Mana!=null)
                {
                    // Import grants/drains made by the legacy ally/enemy APIs once.
                    state.Mana.Add((decimal)actor.Unit.Energy-state.Mana.Value);
                    state.Mana.PassiveTick();EronyaSyncMana(actor);
                }
                state.Module?.Tick?.Invoke();
            }
            foreach(var pulse in eronyaPeriodic.PulsesAt(tick))
            {
                var effect=pulse.Effect;
                var source=actors.Find(a=>a.Unit.UnitId==effect.SourceId&&a.Unit.CombatResetVersion==effect.SourceVersion);
                var target=actors.Find(a=>a.Unit.UnitId==effect.TargetId&&a.Unit.CombatResetVersion==effect.TargetVersion);
                if(source==null||!EronyaValidActor(target))continue;
                if(effect.Kind==EronyaPeriodicKind.Regeneration)EronyaHeal(source,target,EronyaMechanics.Display(effect.Amount),effect.EffectId);
                else EronyaDealDamage(source,target,(float)effect.Amount,effect.EffectId,EronyaDamageOrigin.Periodic);
            }
        }
        private void EronyaAct(ActorClock actor,List<BoardMovementIntent> moves,List<ActorClock> moving)
        {
            if(!EronyaValidActor(actor)||actor.Unit.IsPassiveTrainingTarget)return;
            var state=EronyaState(actor);
            if(actor.StunUntil>tick||actor.Unit.IsFlying||actor.Unit.IsBeingPulled||IsTravelling(actor))return;
            if(state.Cast.Preparing)
            {
                if(state.Cast.Ready(tick,actor.Unit.CombatResetVersion))EronyaReleaseCast(actor,state);
                return;
            }
            if(actor.AbilityMotionUntil>tick||actor.EronyaBasicMotionUntil>tick||
                pending.Any(p=>p.Source==actor&&p.RequiresRange&&p.ReleaseTick>tick))return;
            if(tick<=openingWindowTicks)return;
            if(state.Mana!=null&&state.Mana.Value>=state.Mana.Capacity&&tick>=state.Cast.RetryTick)
            {
                var plan=state.Module?.Plan?.Invoke();
                if(plan!=null&&state.Cast.Begin(state.Mana,tick,actor.Unit.CombatResetVersion,true,false,
                    AbilityTiming.Ticks(AbilityTiming.Cast(actor.Unit.BaseStats,plan.EffectId))))
                {
                    plan.SourceResetVersion=actor.Unit.CombatResetVersion;plan.TargetResetVersion=plan.Target?.CombatResetVersion??0;
                    plan.ReleaseTick=state.Cast.DueTick;plan.AttackId=++eronyaAttackSequence;state.Plan=plan;
                    state.Attack.Reset();EronyaSyncMana(actor);actor.Unit.SetState(UnitState.Attacking);
                    if(state.Cast.Ready(tick,actor.Unit.CombatResetVersion))EronyaReleaseCast(actor,state);
                    return;
                }
            }
            if(state.AttackBlocked.Active(tick))return;
            var selection=EronyaSelectBasicAction(actor);
            var target=selection.Target==null?null:Clock(selection.Target);
            if(target==null){actor.LockedTarget=null;actor.TargetWasInRange=false;actor.Unit.SetState(UnitState.Idle);return;}
            EronyaSetBasicTarget(actor,target);
            if(selection.Status==CombatTargetSelectionStatus.ReadyToAttack)
            {
                actor.TargetWasInRange=true;
                if(tick>=actor.NextAttack)QueueBasicAttack(actor,target.Unit);
                return;
            }
            if(actor.RootUntil>tick||tick<actor.NextMove)return;
            // The same legacy selector supplies the legal attack cell and first path step.
            if(selection.Status==CombatTargetSelectionStatus.MoveRequested&&selection.NextCell!=null)
            {moves.Add(new BoardMovementIntent(actor.Unit,selection.NextCell));moving.Add(actor);}
        }
        private void EronyaReleaseCast(ActorClock actor,EronyaActorState state)
        {
            // Preserve each ability's legal target/area and refund semantics at release.
            var legal=state.Module?.Plan?.Invoke();
            if(legal==null){state.Cast.LostTarget(state.Mana,tick);state.Plan=null;EronyaSyncMana(actor);return;}
            legal.AttackId=state.Plan.AttackId;legal.SourceResetVersion=actor.Unit.CombatResetVersion;
            legal.TargetResetVersion=legal.Target?.CombatResetVersion??0;legal.ReleaseTick=tick;
            state.Cast.Released();state.Plan=null;state.Attack.Reset();
            EronyaWithAttack(actor,legal.AttackId,()=>state.Module?.Cast?.Invoke(legal));
            int recovery=AbilityTiming.Ticks(AbilityTiming.Hold(actor.Unit.BaseStats,legal.EffectId)+
                AbilityTiming.Recovery(actor.Unit.BaseStats,legal.EffectId));
            actor.AbilityMotionUntil=Math.Max(actor.AbilityMotionUntil,tick+recovery);
            actor.NextAttack=actor.NextMove=Math.Max(tick,actor.AbilityMotionUntil);
            actor.Unit.SetState(recovery>0?UnitState.Attacking:UnitState.Idle);
        }
        private int EronyaMoveTicks(ActorClock actor)
        {
            decimal speed=EronyaMechanics.MoveSpeed(1m/(decimal)actor.Unit.BaseStats.MoveInterval,
                (decimal)(actor.SpeedBonus+actor.ExternalMoveBonus),EronyaMagnitude(actor,EronyaStatusKind.Slow));
            return Math.Max(1,(int)Math.Ceiling(1m/speed/EronyaMechanics.TickSeconds));
        }
        private void EronyaDrainShieldEvents()
        {
            foreach(var pair in eronyaStates.ToArray())
            {
                foreach(var ended in pair.Value.Shields.DrainEnded())
                {
                    var layer=ended.Shield;
                    var source=actors.Find(a=>a.Unit.UnitId==layer.SourceId&&a.Unit.CombatResetVersion==layer.SourceVersion);
                    if(source==null||pair.Key.Unit.CombatResetVersion!=layer.TargetVersion)continue;
                    EronyaState(source).Module?.ShieldEnded?.Invoke(new EronyaShieldEvent {Source=source,Target=pair.Key,
                        EffectId=layer.EffectId,InstanceId=layer.InstanceId,Reason=(EronyaShieldEndReason)ended.Reason});
                }
                pair.Key.Unit.EronyaShieldDisplay=EronyaMechanics.Display(pair.Value.Shields.Total);
            }
        }
        private void EronyaFlushPackets()
        {
            EronyaDrainShieldEvents();
            int waves=0;
            while(eronyaDamageQueue.Count>0||eronyaHealQueue.Count>0)
            {
                if(++waves>128)throw new InvalidOperationException("Non-terminating Eronya damage reaction chain.");
                EronyaSettlePackets();EronyaDrainShieldEvents();
            }
        }
        private void EndTickEronyaCombat()
        {
            if(!eronyaCombat)return;
            ResolveEronyaProjectileImpacts();
            EronyaFlushPackets();
            foreach(var actor in actors.ToArray())if(EronyaValidActor(actor)&&eronyaStates.TryGetValue(actor,out var state))state.Module?.EndTick?.Invoke();
            EronyaFlushPackets();
            foreach(var state in eronyaStates.Values)state.Shields.ExpireAfterResolution(tick);
            eronyaPeriodic.ExpireAfterPulses(tick);EronyaFlushPackets();
            eronyaAttackContexts.Clear();
        }
        private void EronyaDied(ActorClock actor)
        {
            if(eronyaLastHits.TryGetValue(actor,out var hit)&&hit.Source.Unit.CombatResetVersion==hit.SourceResetVersion)
                EronyaState(hit.Source).Module?.Killed?.Invoke(actor,hit);
            if(eronyaStates.TryGetValue(actor,out var state))
            {state.Module?.Died?.Invoke();EronyaClearActor(actor,state,EronyaInterruptReason.Death,EronyaLedgerEndReason.Death);}
            eronyaLastHits.Remove(actor);
        }
    }
}

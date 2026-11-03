using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private bool EronyaHasStatus(ActorClock actor,EronyaStatusKind kind)
        {
            if(actor?.Unit==null)return false;
            if(EronyaPeriodicType(kind,out var periodic))return (kind==EronyaStatusKind.Burn&&burns.Any(b=>b.Target==actor.Unit))||
                eronyaPeriodic.Effects.Any(e=>e.TargetId==actor.Unit.UnitId&&e.TargetVersion==actor.Unit.CombatResetVersion&&e.Kind==periodic&&e.Until>=tick);
            return EronyaMagnitude(actor,kind)>0m;
        }
        private bool EronyaBeforeImpactStatus(ActorClock actor,EronyaStatusKind kind) => EronyaHasStatus(actor,kind);
        private static bool EronyaPeriodicType(EronyaStatusKind kind,out EronyaPeriodicKind periodic)
        {
            periodic=kind==EronyaStatusKind.Poison?EronyaPeriodicKind.Poison:kind==EronyaStatusKind.Regeneration?EronyaPeriodicKind.Regeneration:EronyaPeriodicKind.Burn;
            return kind==EronyaStatusKind.Burn||kind==EronyaStatusKind.Poison||kind==EronyaStatusKind.Regeneration;
        }
        private bool EronyaApplyPermanentStatus(ActorClock source,ActorClock target,EronyaStatusKind kind,string effectId,float magnitude,
            CombatStatusTimingKind timingKind=CombatStatusTimingKind.Conditional) =>
            EronyaApplyStatusCore(source,target,kind,effectId,magnitude,int.MaxValue,timingKind);
        private bool EronyaApplyStatus(ActorClock source,ActorClock target,EronyaStatusKind kind,string effectId,float magnitude,
            int durationTicks,EronyaStackRule stackRule=EronyaStackRule.RefreshStrongest)
        {
            if(stackRule==EronyaStackRule.Independent)throw new InvalidOperationException("Eronya effects cannot stack.");
            return EronyaApplyStatusCore(source,target,kind,effectId,magnitude,durationTicks,
                durationTicks==int.MaxValue?CombatStatusTimingKind.Conditional:CombatStatusTimingKind.Timed);
        }
        private bool EronyaApplyStatusCore(ActorClock source,ActorClock target,EronyaStatusKind kind,string effectId,float magnitude,
            int durationTicks,CombatStatusTimingKind timingKind)
        {
            if(!EronyaValidActor(target)||source?.Unit==null||durationTicks<=0||magnitude<=0||float.IsNaN(magnitude)||float.IsInfinity(magnitude))return false;
            if(kind==EronyaStatusKind.Armor||kind==EronyaStatusKind.ArmorReduction)return false;
            if(EronyaPeriodicType(kind,out _))throw new InvalidOperationException("Periodic status requires its pulse API.");
            var state=EronyaState(target);state.Control.Expire(tick);state.Module?.StatusAttempt?.Invoke(source,kind,effectId);
            bool control=kind==EronyaStatusKind.Root||kind==EronyaStatusKind.Stun||kind==EronyaStatusKind.Taunt;
            if(control&&(Immune(target)||target.Unit.IsFlying))return false;
            if(EronyaOwns(target)&&Id(target)=="arboreo"&&(kind==EronyaStatusKind.Root||kind==EronyaStatusKind.Stun))return false;
            if(kind==EronyaStatusKind.Stun&&((EronyaOwns(target)&&Id(target)=="palista")||state.Control.StunImmuneUntil>tick))return false;
            if(kind==EronyaStatusKind.Taunt&&!EronyaCanReach(target,source))return false;
            int until=durationTicks==int.MaxValue?int.MaxValue:checked(tick+durationTicks);
            if(control)
            {
                int existing=kind==EronyaStatusKind.Root?target.RootUntil:kind==EronyaStatusKind.Stun?target.StunUntil:target.TauntUntil;
                magnitude=1;until=statusRules.Apply(kind.ToString().ToLowerInvariant(),tick,Math.Max(existing,EronyaSlot(target,kind).Until),durationTicks);
            }
            if(!EronyaAcceptAgainstLegacy(target,kind,(decimal)magnitude,until))return false;
            if(!EronyaSlot(target,kind).Apply(effectId,source.Unit.UnitId,source.Unit.CombatResetVersion,(decimal)magnitude,tick,until,++eronyaInstanceSequence))return false;
            EronyaRemoveLegacyLoser(target,kind);
            state.Timing[kind]=timingKind;state.Revision++;
            if(kind==EronyaStatusKind.Root)
            {target.RootUntil=until;target.Unit.IsRooted=true;state.Control.ApplyRoot(tick,durationTicks,false);CancelMovementStep(target);board.CancelReservation(target.Unit);}
            if(kind==EronyaStatusKind.Stun)
            {
                if(!EronyaOwns(target))Stun(target,durationTicks);
                target.StunUntil=until;target.StunRetargetPending=true;target.Unit.IsStunned=true;state.Control.ApplyStun(tick,durationTicks,false);
                CancelMovementStep(target);board.CancelReservation(target.Unit);
                if(state.Mana!=null)state.Cast.Interrupt(state.Mana,tick,target.Unit.IsAlive,until);
                state.Plan=null;state.Attack.Reset();state.Module?.Interrupt?.Invoke(EronyaInterruptReason.Stun);if(EronyaOwns(target))InterruptAttack(target);
                if(state.Mana!=null)EronyaSyncMana(target);
            }
            if(kind==EronyaStatusKind.Taunt)
            {
                if(target.TauntTarget==null)target.TargetBeforeTaunt=target.LockedTarget;
                target.TauntTarget=source.Unit;target.TaunterResetVersion=source.Unit.CombatResetVersion;target.TauntUntil=until;
                target.Unit.IsTaunted=true;SetTarget(target,source.Unit);
                RedirectPreparingAttack(target,source.Unit);
            }
            if(kind==EronyaStatusKind.Slow){target.Unit.IsSlowed=true;target.Unit.SlowEffectId=effectId;}
            return true;
        }
        private void EronyaPeriodic(ActorClock source,ActorClock target,EronyaStatusKind kind,string effectId,int amount,
            int durationTicks,int intervalTicks,EronyaStackRule stackRule=EronyaStackRule.RefreshStrongest)
        {
            if(!EronyaValidActor(target)||source?.Unit==null||!EronyaPeriodicType(kind,out var periodic))return;
            if(stackRule==EronyaStackRule.Independent)throw new InvalidOperationException("Eronya effects cannot stack.");
            var before=eronyaPeriodic.Effects.FirstOrDefault(e=>e.TargetId==target.Unit.UnitId&&e.TargetVersion==target.Unit.CombatResetVersion&&e.Kind==periodic);
            long revision=before?.ApplicationRevision??0;
            var result=eronyaPeriodic.Apply(source.Unit.UnitId,source.Unit.CombatResetVersion,target.Unit.UnitId,target.Unit.CombatResetVersion,
                effectId,periodic,amount,tick,durationTicks,intervalTicks,
                poisonAntiHeal:periodic==EronyaPeriodicKind.Poison?
                    EronyaMechanics.Clamp((decimal)V(source,effectId,"antiHealPercent",30)/100m,0m,1m):0m);
            if(result.ApplicationRevision!=revision)EronyaState(target).Revision++;
        }
        private decimal EronyaAntiHeal(ActorClock actor) => Math.Max(actor.AntiHealUntil>tick?1m-(decimal)actor.AntiHealMultiplier:0m,
            Math.Max(EronyaMagnitude(actor,EronyaStatusKind.AntiHeal),eronyaPeriodic.PoisonAntiHeal(actor.Unit.UnitId,actor.Unit.CombatResetVersion,tick)));
        private void EronyaCleanse(ActorClock target,bool onlyPoisonOrBurn=false)
        {
            if(!EronyaValidActor(target))return;
            var state=EronyaState(target);int count=eronyaPeriodic.Effects.Count;
            if(onlyPoisonOrBurn)
            {
                var kind=EronyaHasStatus(target,EronyaStatusKind.Poison)?EronyaPeriodicKind.Poison:EronyaPeriodicKind.Burn;
                eronyaPeriodic.RemoveKind(target.Unit.UnitId,target.Unit.CombatResetVersion,kind);
                if(kind==EronyaPeriodicKind.Burn)burns.RemoveAll(b=>b.Target==target.Unit);
            }
            else
            {
                eronyaPeriodic.Cleanse(target.Unit.UnitId,target.Unit.CombatResetVersion);
                foreach(var kind in new[]{EronyaStatusKind.Stun,EronyaStatusKind.Root,EronyaStatusKind.Slow,EronyaStatusKind.Taunt})
                    if(state.Statuses.TryGetValue(kind,out var slot)&&slot.EffectId!=null){slot.Clear();state.Revision++;}
                state.Control.Cleanse(tick);target.RootUntil=target.StunUntil=0;target.Unit.IsRooted=target.Unit.IsStunned=target.Unit.IsSlowed=false;
                target.AttackSpeedMultiplier=1;target.ChocolateSource=null;target.ChocolateNext=target.ChocolateStacks=0;
                burns.RemoveAll(b=>b.Target==target.Unit);
                target.Unit.SlowEffectId=null;ClearTaunt(target,true);
            }
            if(count!=eronyaPeriodic.Effects.Count)state.Revision++;
        }
        private void EronyaRemoveEffect(ActorClock target,string effectId)
        {
            if(target?.Unit==null||!eronyaStates.TryGetValue(target,out var state))return;
            foreach(var pair in state.Statuses)if(pair.Value.EffectId==effectId){pair.Value.Clear();state.Revision++;}
            foreach(var slot in new[]{state.AttackBlocked,state.DamageSuppressed})if(slot.EffectId==effectId){slot.Clear();state.Revision++;}
            int count=eronyaPeriodic.Effects.Count;eronyaPeriodic.RemoveEffect(target.Unit.UnitId,target.Unit.CombatResetVersion,effectId);
            if(count!=eronyaPeriodic.Effects.Count)state.Revision++;
            decimal shield=state.Shields.Total;state.Shields.RemoveEffect(effectId,EronyaLedgerEndReason.Cleared);
            if(shield!=state.Shields.Total)state.Revision++;
        }
        private static string EronyaStatusId(EronyaStatusKind kind)
        {
            switch(kind){case EronyaStatusKind.AttackSpeed:return "haste";case EronyaStatusKind.AntiHeal:return "anti-heal";
                case EronyaStatusKind.Armor:return "armor-up";case EronyaStatusKind.ArmorReduction:return "armor-down";
                case EronyaStatusKind.DamageReduction:return "damage-reduction";case EronyaStatusKind.Damage:return "attack-up";
                case EronyaStatusKind.Root:return "root";case EronyaStatusKind.Stun:return "stun";
                case EronyaStatusKind.Slow:return "slow";case EronyaStatusKind.Burn:return "burn";
                case EronyaStatusKind.Poison:return "poison";case EronyaStatusKind.Regeneration:return "regeneration";
                case EronyaStatusKind.Taunt:return "taunt";
                default:return kind.ToString().ToLowerInvariant();}
        }
        public void CollectStatusSnapshots(BattleUnit unit,List<CombatStatusSnapshot> output)
        {output.Clear();output.AddRange(CaptureStatusFrame(unit).Statuses);}
        public CombatStatusFrame CaptureStatusFrame(BattleUnit unit)
        {
            var actor=unit==null?null:Clock(unit);EronyaActorState state=null;
            if(actor!=null)eronyaStates.TryGetValue(actor,out state);
            bool active=begun&&!Finished&&unit!=null&&unit.IsAlive&&!unit.IsReviving&&
                (state==null||state.Active&&state.ResetVersion==unit.CombatResetVersion);
            var values=statusPresentationScratch;values.Clear();
            if(active&&state!=null)
            {
                void Add(string id,EronyaEffectSlot slot,CombatStatusTimingKind timing=CombatStatusTimingKind.Timed)
                {if(slot.Active(tick))values.Add(new CombatStatusSnapshot(id,slot.EffectId,slot.SourceId,slot.InstanceId,unit.CombatResetVersion,
                    slot.AppliedTick,slot.Until,0,(float)slot.Magnitude,tick,slot.ApplicationRevision,timing));}
                foreach(var pair in state.Statuses)
                    if(pair.Key!=EronyaStatusKind.Armor&&pair.Key!=EronyaStatusKind.ArmorReduction)
                        Add(EronyaStatusId(pair.Key),pair.Value,state.Timing.TryGetValue(pair.Key,out var timing)?timing:CombatStatusTimingKind.Timed);
                Add("damage-suppressed",state.DamageSuppressed);
                bool innate=EronyaOwns(actor)&&(Id(actor)=="arboreo"||Id(actor)=="palista");
                if(innate||state.Control.StunImmuneUntil>tick)
                {
                    string effect=innate?(Id(actor)=="arboreo"?"arboreo-raices-profundas":"palista-respiracion-templaria"):"post-stun-immunity";
                    int applied=innate?state.CreatedTick:state.Control.StunImmuneUntil-EronyaMechanics.StunImmunityTicks;
                    values.Add(new CombatStatusSnapshot("stun-immunity",effect,unit.UnitId,state.Identity,unit.CombatResetVersion,
                        applied,innate?int.MaxValue:state.Control.StunImmuneUntil,0,1,tick,innate?1:applied+1,
                        innate?CombatStatusTimingKind.Round:CombatStatusTimingKind.Timed));
                }
                foreach(var effect in eronyaPeriodic.Effects)
                    if(effect.TargetId==unit.UnitId&&effect.TargetVersion==unit.CombatResetVersion&&effect.Until>=tick)
                        values.Add(new CombatStatusSnapshot(PeriodicPresentationId(effect.Kind),effect.EffectId,effect.SourceId,effect.InstanceId,
                            unit.CombatResetVersion,effect.AppliedTick,effect.Until,effect.Next,(float)effect.Amount,tick,effect.ApplicationRevision));
                foreach(var layer in state.Shields.Layers)
                    values.Add(new CombatStatusSnapshot("shield",layer.EffectId,layer.SourceId,layer.InstanceId,unit.CombatResetVersion,
                        layer.AppliedTick,layer.Until,0,(float)layer.Amount,tick,layer.ApplicationRevision));
                EronyaPeriodicLedger.Effect poison=null;
                foreach(var candidate in eronyaPeriodic.Effects)
                    if(candidate.TargetId==unit.UnitId&&candidate.TargetVersion==unit.CombatResetVersion&&candidate.Kind==EronyaPeriodicKind.Poison&&candidate.Until>=tick)
                    {poison=candidate;break;}
                bool poisonWins=poison!=null&&poison.AntiHeal>0&&(EronyaMagnitude(actor,EronyaStatusKind.AntiHeal)<poison.AntiHeal||
                    EronyaMagnitude(actor,EronyaStatusKind.AntiHeal)==poison.AntiHeal&&
                    (!state.Statuses.TryGetValue(EronyaStatusKind.AntiHeal,out var antiHeal)||poison.Until>antiHeal.Until));
                if(poisonWins)
                {
                    values.RemoveAll(s=>s.Id=="anti-heal");
                    values.Add(new CombatStatusSnapshot("anti-heal",poison.EffectId,poison.SourceId,poison.InstanceId,unit.CombatResetVersion,
                        poison.AppliedTick,poison.Until,poison.Next,(float)poison.AntiHeal,tick,poison.ApplicationRevision));
                }
            }
            // Re-read all live values, even during pause: same-tick cleanse, replacement,
            // death and reset remain observable. Only unchanged immutable output is reused.
            StatusPresentationCache.StableSort(values);
            int resetVersion=unit==null?0:unit.CombatResetVersion;
            if(state==null)return StatusPresentationCache.Empty(resetVersion,tick,active);
            return state.PresentationCache.Capture(resetVersion,tick,ref state.Revision,active,values);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private sealed class EronyaAttackContext
        {public ActorClock Source;public int SourceVersion,NextPacket;public long AttackId;}
        private sealed class EronyaQueuedDamage
        {public EronyaHit Hit;public decimal Amount,LegacyShieldDamage;public EronyaVitalLedger.Receipt Receipt;}
        private sealed class EronyaQueuedHeal
        {public ActorClock Source,Target;public int TargetVersion;public decimal Amount;}
        private EronyaAttackContext eronyaAttackContext;
        private readonly Dictionary<long,EronyaAttackContext> eronyaAttackContexts=new Dictionary<long,EronyaAttackContext>();
        private readonly List<EronyaQueuedDamage> eronyaDamageQueue=new List<EronyaQueuedDamage>();
        private readonly List<EronyaQueuedHeal> eronyaHealQueue=new List<EronyaQueuedHeal>();
        private void EronyaWithAttack(ActorClock source,long attackId,Action action)
        {
            if(!eronyaAttackContexts.TryGetValue(attackId,out var current))
                eronyaAttackContexts[attackId]=current=new EronyaAttackContext {Source=source,SourceVersion=source.Unit.CombatResetVersion,AttackId=attackId};
            var previous=eronyaAttackContext;eronyaAttackContext=current;
            try{action();}finally{eronyaAttackContext=previous;}
        }
        private EronyaHit EronyaDealDamage(ActorClock source,ActorClock target,float amount,
            string effectId,EronyaDamageOrigin origin=EronyaDamageOrigin.Ability,float armorPenetration=0,bool canReflect=true,bool execute=false)
        {
            var hit=new EronyaHit {Source=source,Target=target,EffectId=effectId,Origin=origin};
            if(!EronyaValidActor(target)||source?.Unit==null||source.Unit.CombatResetVersion!=source.UnitResetVersion||
                source.Unit.Side==target.Unit.Side||target.Unit.IsFlying||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return hit;
            bool attack=origin==EronyaDamageOrigin.Basic||origin==EronyaDamageOrigin.Ability;
            EronyaAttackContext context=attack&&eronyaAttackContext?.Source==source&&eronyaAttackContext.SourceVersion==source.Unit.CombatResetVersion?eronyaAttackContext:null;
            long attackId=context?.AttackId??++eronyaAttackSequence;
            if(attack&&context==null)eronyaAttackContexts[attackId]=context=new EronyaAttackContext {Source=source,SourceVersion=source.Unit.CombatResetVersion,AttackId=attackId};
            var request=new EronyaDamage {Source=source,Target=target,EffectId=effectId,Origin=origin,Amount=amount,
                CanReflect=canReflect&&origin!=EronyaDamageOrigin.Reflection,IsAttack=attack,
                AttackId=attackId,EventId=++eronyaEventSequence,PacketIndex=context==null?0:context.NextPacket++,
                SourceResetVersion=source.Unit.CombatResetVersion,TargetResetVersion=target.Unit.CombatResetVersion};
            var defender=EronyaState(target);var attacker=EronyaState(source);
            attacker.Module?.BeforeOutgoing?.Invoke(request);defender.Module?.BeforeDamage?.Invoke(request);
            hit.EventId=request.EventId;hit.AttackId=attackId;hit.PacketIndex=request.PacketIndex;hit.IsAttack=attack;
            hit.SourceResetVersion=request.SourceResetVersion;hit.TargetResetVersion=request.TargetResetVersion;
            hit.CanReflect=request.CanReflect;hit.ValidContact=true;
            decimal raw=Math.Max(0m,(decimal)request.Amount);
            decimal reduction=EronyaMagnitude(target,EronyaStatusKind.DamageReduction);
            string reductionId=defender.Statuses.TryGetValue(EronyaStatusKind.DamageReduction,out var reductionSlot)&&reductionSlot.Active(tick)?reductionSlot.EffectId:null;
            decimal requested=EronyaMechanics.Clamp((decimal)request.Reduction,0m,EronyaMechanics.ReductionCap);
            if(requested>reduction){reduction=requested;reductionId=request.ReductionEffectId;}
            reduction=EronyaMechanics.Clamp(reduction,0m,EronyaMechanics.ReductionCap);
            decimal external=(decimal)ExternalReduction(target);
            if(external>reduction){reduction=external;reductionId=target.DamageReductionId;}
            reduction=execute?0m:reduction;
            // The origin retains reflection safeguards; saved physical/magical flags
            // no longer choose armor, reductions, packet routing or block counters.
            bool reflected=origin==EronyaDamageOrigin.Reflection;
            decimal beforeReduction=execute?raw:reflected?ReflectedIncomingWithReduction(EronyaMechanics.Display(raw),0):
                IncomingWithDefense(target,raw,0);
            decimal reduced=execute?raw:reflected?ReflectedIncomingWithReduction(EronyaMechanics.Display(raw),(float)reduction):
                IncomingWithDefense(target,raw,(float)reduction);
            bool cancelled=request.Cancel||attacker.DamageSuppressed.Active(tick)||target.InvulnerableUntil>tick;
            hit.ReductionEffectId=reductionId;
            hit.PreciseOwnReductionPrevented=cancelled?0m:beforeReduction-reduced;
            hit.PrecisePreventedDamage=cancelled?beforeReduction:beforeReduction-reduced;
            hit.PreventedDamage=EronyaMechanics.Display(hit.PrecisePreventedDamage);
            hit.Landed=!cancelled&&reduced>0m;
            eronyaDamageQueue.Add(new EronyaQueuedDamage {Hit=hit,Amount=cancelled?0m:reduced});
            if(origin==EronyaDamageOrigin.Basic&&hit.Landed&&attacker.LandedBasics.Add(attackId))
            {
                if(attacker.Mana!=null){attacker.Mana.CreditBasic(attackId,true,true);source.Unit.ClearEnergy();source.Unit.AddEnergy((float)attacker.Mana.Value);}
                EronyaWithAttack(source,attackId,()=>attacker.Module?.BasicImpact?.Invoke(hit));
            }
            return hit;
        }
        private int EronyaHeal(ActorClock source,ActorClock target,int amount,string effectId)
        {
            if(!EronyaValidActor(target)||amount<=0)return 0;
            decimal effective=EronyaMechanics.HealAmount(amount,EronyaAntiHeal(target));
            eronyaHealQueue.Add(new EronyaQueuedHeal {Source=source,Target=target,TargetVersion=target.Unit.CombatResetVersion,Amount=effective});
            return EronyaMechanics.Display(effective);
        }
        private long EronyaShield(ActorClock source,ActorClock target,string effectId,int amount,int durationTicks)
        {
            if(!EronyaValidActor(target)||source?.Unit==null||amount<=0||durationTicks<=0)return 0;
            var state=EronyaState(target);var before=state.Shields.Layers.FirstOrDefault();long revision=before?.ApplicationRevision??0;
            var layer=state.Shields.Grant(source.Unit.UnitId,source.Unit.CombatResetVersion,target.Unit.CombatResetVersion,effectId,
                amount,target.Unit.BaseStats.MaxHealth,tick,durationTicks);
            if(!ReferenceEquals(layer,before))layer.InstanceId=++eronyaInstanceSequence;
            if(!ReferenceEquals(layer,before)||layer.ApplicationRevision!=revision)state.Revision++;
            return layer.InstanceId;
        }
        private void EronyaSettlePackets()
        {
            var vitals=new Dictionary<ActorClock,EronyaVitalLedger>();
            EronyaVitalLedger Get(ActorClock actor)
            {if(!vitals.TryGetValue(actor,out var ledger))vitals[actor]=ledger=new EronyaVitalLedger(EronyaMaxHealth(actor),EronyaHealth(actor),EronyaState(actor).Shields);return ledger;}
            var packets=eronyaDamageQueue.ToArray();eronyaDamageQueue.Clear();
            var heals=eronyaHealQueue.ToArray();eronyaHealQueue.Clear();
            foreach(var packet in packets)
                if(packet.Hit.Target.Unit.CombatResetVersion==packet.Hit.TargetResetVersion&&EronyaValidActor(packet.Hit.Target))
                {
                    decimal remaining=EronyaAbsorbLegacyShield(packet.Hit.Target.Unit,packet.Amount);
                    packet.LegacyShieldDamage=packet.Amount-remaining;
                    packet.Receipt=Get(packet.Hit.Target).Damage(packet.Hit.EventId,remaining);
                }
            foreach(var heal in heals)
                if(heal.Target.Unit.CombatResetVersion==heal.TargetVersion&&EronyaValidActor(heal.Target))Get(heal.Target).Heal(heal.Amount);
            foreach(var pair in vitals)
            {
                decimal before=EronyaHealth(pair.Key),shieldBefore=EronyaState(pair.Key).Shields.Total;pair.Value.Commit();
                if(EronyaOwns(pair.Key))pair.Key.Unit.SetPreciseCombatHealth(pair.Value.Health);
                else if(pair.Value.Health<before)
                {
                    int applied=ApplyCombatDamage(pair.Key,EronyaMechanics.Display(before-pair.Value.Health));
                    // Kazuru's last halo can reduce the settled net health loss.
                    decimal prevented=Math.Max(0m,before-pair.Value.Health-applied);
                    foreach(var packet in packets.Where(p=>p.Hit.Target==pair.Key&&p.Receipt!=null).OrderByDescending(p=>p.Hit.EventId))
                    {decimal cut=Math.Min(prevented,packet.Receipt.HealthDamage);packet.Receipt.HealthDamage-=cut;prevented-=cut;}
                }
                else if(pair.Value.Health>before)pair.Key.Unit.Heal(EronyaMechanics.Display(pair.Value.Health-before));
                if(pair.Value.Health!=before||shieldBefore!=EronyaState(pair.Key).Shields.Total){lastChangeTick=tick;EronyaState(pair.Key).Revision++;}
                if(EronyaHealth(pair.Key)>before)Healed?.Invoke(pair.Key.Unit,EronyaMechanics.Display(EronyaHealth(pair.Key)-before));
            }
            // Match Bugrum's existing legacy settlement: one grant per recipient
            // for simultaneous attack packets; each direct periodic application is separate.
            foreach(var group in packets.Where(p=>p.Receipt!=null&&p.Receipt.HealthDamage>0m)
                .GroupBy(p=>p.Hit.Target))
            {
                if(Id(group.Key)=="suki"||Id(group.Key)=="pokrak")continue;
                int credits=group.Count(p=>p.Hit.Origin==EronyaDamageOrigin.Periodic)+
                    (group.Any(p=>p.Hit.Origin!=EronyaDamageOrigin.Periodic)?1:0);
                for(int i=0;i<credits;i++)CreditReceivedDamageEnergy(group.Key);
            }
            foreach(var packet in packets.OrderBy(p=>p.Hit.EventId))
            {
                var hit=packet.Hit;if(packet.Receipt==null){hit.ValidContact=false;continue;}
                hit.PreciseHealthDamage=packet.Receipt.HealthDamage;hit.PreciseShieldDamage=packet.Receipt.ShieldDamage+packet.LegacyShieldDamage;
                hit.HealthDamage=EronyaMechanics.Display(hit.PreciseHealthDamage);hit.ShieldDamage=EronyaMechanics.Display(hit.PreciseShieldDamage);hit.Settled=true;
                // Capture before settled callbacks can displace the recipient. Presentation only.
                var presentation=CaptureImpactPresentation(hit.Source.Unit,hit.Target.Unit,hit.EffectId,
                    hit.Origin==EronyaDamageOrigin.Ability,hit.Origin==EronyaDamageOrigin.Periodic,
                    hit.PreciseHealthDamage+hit.PreciseShieldDamage>0m,hit.Origin==EronyaDamageOrigin.Basic);
                WithImpactPresentation(presentation,()=>
                {
                EronyaRecordDamage(hit);
                EronyaState(hit.Target).Module?.AfterDamage?.Invoke(hit);
                if(hit.Source.Unit.CombatResetVersion==hit.SourceResetVersion)
                {
                    var module=EronyaState(hit.Source).Module;
                    if(hit.PreciseHealthDamage+hit.PreciseShieldDamage>0m)
                    {
                        hit.Target.LastDamager=hit.Source.Unit;eronyaLastHits[hit.Target]=hit;module?.AfterOwnDamage?.Invoke(hit);
                        if(Id(hit.Target)=="suki"||Id(hit.Target)=="pokrak")
                            CreditReceivedDamageEnergy(hit.Target);
                    }
                    module?.AfterOwnImpact?.Invoke(hit);
                }
                Impacted?.Invoke(hit.Source.Unit,hit.Target.Unit,hit.HealthDamage+hit.ShieldDamage);
                });
            }
        }
        private void CreditReceivedDamageEnergy(ActorClock actor)
        {
            // BattleUnit owns alive/locked/opening/frozen/capacity semantics.
            actor.Unit.AddEnergy(actor.Unit.BaseStats.EnergyOnDamage);
            if(eronyaStates.TryGetValue(actor,out var state)&&state.ResetVersion==actor.Unit.CombatResetVersion&&state.Mana!=null)
                state.Mana.Add((decimal)actor.Unit.Energy-state.Mana.Value);
        }
    }
}

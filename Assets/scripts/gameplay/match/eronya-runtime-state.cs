using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private enum EronyaDamageOrigin { Basic, Ability, Periodic, Reflection }
        private enum EronyaInterruptReason { Stun, Death, Reset, End, InvalidTarget }
        private enum EronyaShieldEndReason { Consumed, Expired, Replaced, Cleared, Death, Reset, End }
        private enum EronyaStatusKind { Root,Stun,Slow,Burn,Poison,Regeneration,Taunt,AntiHeal,AttackSpeed,DamageReduction,Armor,Damage,ArmorReduction }
        private enum EronyaStackRule { RefreshStrongest,Independent,ReplaceSameSource }
        private sealed class EronyaCastPlan
        {
            public string EffectId;
            public BattleUnit Target;
            public BoardCell[] Cells;
            public int SourceResetVersion,TargetResetVersion,ReleaseTick;
            public long AttackId;
        }
        private sealed class EronyaDamage
        {
            public ActorClock Source,Target;
            public string EffectId,ReductionEffectId;
            public EronyaDamageOrigin Origin;
            public long EventId,AttackId;
            public int PacketIndex,SourceResetVersion,TargetResetVersion;
            public float Amount,Reduction;
            public bool Cancel,CanReflect,IsAttack;
        }
        private sealed class EronyaHit
        {
            public ActorClock Source,Target;
            public string EffectId,ReductionEffectId;
            public EronyaDamageOrigin Origin;
            public long EventId,AttackId;
            public int PacketIndex,SourceResetVersion,TargetResetVersion;
            public bool Landed,IsAttack,CanReflect,ValidContact,Settled;
            public int HealthDamage,ShieldDamage,PreventedDamage;
            public decimal PreciseHealthDamage,PreciseShieldDamage,PrecisePreventedDamage,PreciseOwnReductionPrevented;
        }
        private sealed class EronyaShieldEvent
        {
            public ActorClock Source,Target;
            public string EffectId;
            public long InstanceId;
            public EronyaShieldEndReason Reason;
        }
        private sealed class EronyaModuleCallbacks
        {
            public Action Begin,Tick,EndTick,MovementStarted,Died,End,Clear;
            public Func<EronyaCastPlan> Plan;
            public Action<EronyaCastPlan> Cast;
            public Action<EronyaDamage> BeforeOutgoing,BeforeDamage;
            public Action<EronyaHit> BasicImpact,AfterDamage,AfterOwnDamage,AfterOwnImpact;
            public Action<ActorClock,EronyaHit> Killed;
            public Action<EronyaShieldEvent> ShieldEnded;
            public Action<ActorClock,EronyaStatusKind,string> StatusAttempt;
            public Action<BoardCell,BoardCell,bool> Moved;
            public Action<EronyaInterruptReason> Interrupt;
        }
        private sealed class EronyaActorState
        {
            public int ResetVersion;
            public int CreatedTick;
            public long Identity;
            public bool Active=true;
            public long Revision;
            public readonly StatusPresentationCache PresentationCache=new StatusPresentationCache();
            public readonly EronyaAttackClock Attack=new EronyaAttackClock();
            public readonly EronyaCastClock Cast=new EronyaCastClock();
            public readonly EronyaControlClock Control=new EronyaControlClock();
            public readonly EronyaShieldLedger Shields=new EronyaShieldLedger();
            public readonly Dictionary<EronyaStatusKind,EronyaEffectSlot> Statuses=new Dictionary<EronyaStatusKind,EronyaEffectSlot>();
            public readonly Dictionary<EronyaStatusKind,CombatStatusTimingKind> Timing=new Dictionary<EronyaStatusKind,CombatStatusTimingKind>();
            public readonly EronyaEffectSlot AttackBlocked=new EronyaEffectSlot(),DamageSuppressed=new EronyaEffectSlot();
            public readonly HashSet<long> LandedBasics=new HashSet<long>();
            public EronyaCastPlan Plan;
            public EronyaModuleCallbacks Module;
            public EronyaManaLedger Mana;
        }
        private readonly Dictionary<ActorClock,EronyaActorState> eronyaStates=new Dictionary<ActorClock,EronyaActorState>();
        private readonly EronyaPeriodicLedger eronyaPeriodic=new EronyaPeriodicLedger();
        private long eronyaEventSequence,eronyaAttackSequence,eronyaInstanceSequence;
        private int EronyaNowTick => tick;
        private bool EronyaHasTrick(ActorClock actor,int index) => actor?.Unit!=null&&actor.Unit.BaseStats.HasTrick(index);
        private bool EronyaOwns(ActorClock actor) => actor?.Unit?.BaseStats.UsesEronyaRules==true;
        private EronyaActorState EronyaState(ActorClock actor)
        {
            if(actor?.Unit==null)throw new ArgumentNullException(nameof(actor));
            if(!eronyaStates.TryGetValue(actor,out var state)||state.ResetVersion!=actor.Unit.CombatResetVersion)
            {
                long previous=state?.Revision??0;
                state=new EronyaActorState {ResetVersion=actor.Unit.CombatResetVersion,Revision=previous+1,CreatedTick=tick,Identity=++eronyaInstanceSequence};
                if(EronyaOwns(actor))state.Mana=new EronyaManaLedger((decimal)actor.Unit.BaseStats.EnergyMax,
                    (decimal)actor.Unit.BaseStats.EnergyPerAttack,(decimal)actor.Unit.BaseStats.EnergyPerSecond);
                eronyaStates[actor]=state;
            }
            return state;
        }
        private EronyaEffectSlot EronyaSlot(ActorClock actor,EronyaStatusKind kind)
        {
            var state=EronyaState(actor);
            if(!state.Statuses.TryGetValue(kind,out var slot))state.Statuses[kind]=slot=new EronyaEffectSlot();
            return slot;
        }
        private decimal EronyaMagnitude(ActorClock actor,EronyaStatusKind kind) =>
            actor!=null&&eronyaStates.TryGetValue(actor,out var state)&&state.ResetVersion==actor.Unit.CombatResetVersion&&
            state.Statuses.TryGetValue(kind,out var slot)&&slot.Active(tick)?slot.Magnitude:0m;
        private ActorClock EronyaBasicTarget(ActorClock actor) => actor?.LockedTarget==null?null:Clock(actor.LockedTarget);
        private void EronyaSetBasicTarget(ActorClock actor,ActorClock target)
        {
            if(!EronyaValidActor(actor)||!EronyaValidActor(target)||actor.Unit.Side==target.Unit.Side)return;
            SetTarget(actor,target.Unit);
        }
        private bool EronyaIsMoving(ActorClock actor) => actor!=null&&IsTravelling(actor);
        private void EronyaAddMana(ActorClock actor,float amount)
        {
            if(!EronyaValidActor(actor)||amount<0||float.IsNaN(amount)||float.IsInfinity(amount))return;
            var state=EronyaState(actor);
            if(state.Mana==null){actor.Unit.AddEnergy(amount);return;}
            state.Mana.Add((decimal)amount);actor.Unit.ClearEnergy();actor.Unit.AddEnergy((float)state.Mana.Value);
        }
        private void EronyaDrainMana(ActorClock actor,float amount)
        {
            if(!EronyaValidActor(actor)||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return;
            var state=EronyaState(actor);
            if(state.Mana==null){actor.Unit.AddEnergy(-amount);return;}
            state.Mana.Add(-(decimal)amount);actor.Unit.ClearEnergy();actor.Unit.AddEnergy((float)state.Mana.Value);
        }
        private void EronyaSetAttackBlocked(ActorClock actor,string effectId,int untilTick)
        {
            var state=EronyaState(actor);
            if(state.AttackBlocked.Apply(effectId,actor.Unit.UnitId,actor.Unit.CombatResetVersion,1m,tick,untilTick,++eronyaInstanceSequence))state.Revision++;
        }
        private void EronyaSetDamageSuppressed(ActorClock actor,string effectId,int untilTick)
        {
            var state=EronyaState(actor);
            if(state.DamageSuppressed.Apply(effectId,actor.Unit.UnitId,actor.Unit.CombatResetVersion,1m,tick,untilTick,++eronyaInstanceSequence))state.Revision++;
        }
        private void EronyaEmitAbility(ActorClock source,string effectId,BoardCell[] cells)
        { if(source?.Unit==null)return;AbilityUsed?.Invoke(source.Unit,effectId);if(!HasEditedArea(source,effectId)&&cells!=null&&cells.Length>0)AreaEffect?.Invoke(source.Unit,effectId,cells); }
    }
}

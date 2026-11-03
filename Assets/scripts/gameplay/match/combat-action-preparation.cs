using System;
using System.Linq;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Gameplay.Board;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Read-only preparation pose. Contact/release still comes from the existing combat events.</summary>
    public readonly struct CombatActionPreparation
    {
        public readonly BattleUnit Source, Target;
        public readonly int ResetVersion;
        public readonly bool IsSuper;
        public readonly float Progress;
        public CombatActionPreparation(BattleUnit source, BattleUnit target, bool super, float progress)
        { Source=source;Target=target;ResetVersion=source.CombatResetVersion;IsSuper=super;Progress=progress; }
    }

    public sealed partial class CombatSimulation
    {
        // Never creates state, selects a new target, advances a clock or emits a hit.
        public CombatActionPreparation CaptureActionPreparation(BattleUnit unit)
        {
            if(!begun||Finished||unit==null||!unit.BaseStats.UsesEronyaRules||!unit.IsAlive||
                !unit.isActiveAndEnabled||unit.IsRoundFrozen||unit.IsStunned||unit.IsFlying||unit.IsBeingPulled)return default;
            ActorClock actor=null;
            foreach(var candidate in actors)if(candidate.Unit==unit){actor=candidate;break;}
            if(actor==null||!EronyaValidActor(actor)||!eronyaStates.TryGetValue(actor,out var state)||state.ResetVersion!=unit.CombatResetVersion||
                !state.Active||IsTravelling(actor)||actor.StunUntil>tick)return default;
            if(state.Cast.Preparing&&state.Plan!=null)
            {
                var target=state.Plan.Target;
                if(target!=null&&(!target.IsAlive||target.CombatResetVersion!=state.Plan.TargetResetVersion))return default;
                float progress=state.Cast.PreparationTicks<=0?1:1f-(state.Cast.DueTick-tick)/(float)state.Cast.PreparationTicks;
                return new CombatActionPreparation(unit,target,true,Math.Max(0,Math.Min(.99999f,progress)));
            }
            var impact=pending.FirstOrDefault(p=>p.Source==actor&&p.PreciseDamage.HasValue&&!p.Super&&
                !p.Missed&&p.ReleaseTick>tick&&p.SourceResetVersion==unit.CombatResetVersion);
            var enemy=impact?.Target;
            if(tick<=openingWindowTicks||state.AttackBlocked.Active(tick)||enemy==null||!enemy.IsAlive||
                enemy.CombatResetVersion!=impact.TargetResetVersion||IsInvisible(enemy)||
                !CombatTargetSelector.IsInAttackRange(unit.CurrentCell,enemy.CurrentCell,unit.BaseStats.AttackRange))return default;
            int windup=impact.ReleaseTick-actor.LastBasicTick;
            if(windup<=0)return default;
            return new CombatActionPreparation(unit,enemy,false,Math.Max(0,Math.Min(.99999f,(tick-actor.LastBasicTick)/(float)windup)));

        }
    }
}

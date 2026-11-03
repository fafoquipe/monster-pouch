using System;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public readonly struct DagataJumpSnapshot
    {
        public readonly BattleUnit Unit;
        public readonly BoardCell From, To;
        public readonly int ResetVersion, Tick;
        public readonly DagataJumpTiming Timing;
        public bool Active=>Unit!=null;
        public DagataJumpSnapshot(BattleUnit unit,BoardCell from,BoardCell to,int tick,DagataJumpTiming timing)
        { Unit=unit; From=from; To=to; ResetVersion=unit.CombatResetVersion; Tick=tick; Timing=timing; }
    }

    public sealed partial class CombatSimulation
    {
        const string DagataOpeningEffect="dagata-entrada-entre-humo";
        int DagataPhaseTicks(ActorClock actor,string key,float fallback)=>
            AbilityTiming.Ticks(Math.Max(0,V(actor,DagataOpeningEffect,key,fallback)));

        void BeginDagataJump(ActorClock actor,EronyaVolkanarState state,ActorClock target,BoardCell destination)
        {
            if(!EronyaValidActor(actor)||actor.Unit.IsStunned||actor.Unit.IsRooted||actor.Unit.IsBeingPulled||
                actor.Unit.IsFlying||actor.StunUntil>tick||actor.RootUntil>tick||!board.TryReserveCell(actor.Unit,destination))return;
            state.JumpFrom=actor.Unit.CurrentCell;state.JumpTo=destination;state.JumpTarget=target;
            state.JumpTargetVersion=target.Unit.CombatResetVersion;state.JumpLanded=false;state.JumpActive=true;
            state.JumpTiming=new DagataJumpTiming(tick,DagataPhaseTicks(actor,"castDuration",.2f),
                DagataPhaseTicks(actor,"travelDuration",.4f),DagataPhaseTicks(actor,"holdDuration",.1f),
                DagataPhaseTicks(actor,"recoveryDuration",.2f));
            actor.AbilityMotionUntil=Math.Max(actor.AbilityMotionUntil,state.JumpTiming.End);
            actor.NextAttack=Math.Max(actor.NextAttack,state.JumpTiming.End);
            actor.NextMove=Math.Max(actor.NextMove,state.JumpTiming.End);
            actor.Unit.SetState(UnitState.Moving);
            AdvanceDagataJump(actor,state); // Explicit zero phases complete immediately, once.
        }

        void CancelDagataJump(ActorClock actor,EronyaVolkanarState state)
        {
            if(!state.JumpActive)return;
            if(actor.Unit!=null&&ReferenceEquals(actor.Unit.ReservedCell,state.JumpTo))board.CancelReservation(actor.Unit);
            int end=state.JumpTiming.End;
            if(actor.AbilityMotionUntil==end)actor.AbilityMotionUntil=tick;
            if(actor.NextAttack==end)actor.NextAttack=tick;
            if(actor.NextMove==end)actor.NextMove=tick;
            state.JumpActive=false;
            if(!state.JumpLanded)state.HiddenBladeReady=false;
            if(actor.Unit!=null&&actor.Unit.IsAlive)actor.Unit.SetState(UnitState.Idle);
        }

        void CancelDagataJump(ActorClock actor)
        {
            if(actor!=null&&EronyaVolkanarStates.TryGetValue(actor,out var state))CancelDagataJump(actor,state);
        }

        // Disabled/dead actors do not receive Module.Tick. Release only this jump's reservation.
        void CancelInvalidDagataJumps()
        {
            foreach(var pair in EronyaVolkanarStates)
                if(pair.Value.JumpActive&&!EronyaValidActor(pair.Key))CancelDagataJump(pair.Key,pair.Value);
        }
        void CancelDagataJumpOnMovement(ActorClock actor,BoardCell to)
        {
            if(EronyaVolkanarStates.TryGetValue(actor,out var state)&&state.JumpActive&&
                (!state.JumpLanded||!ReferenceEquals(to,state.JumpTo)))CancelDagataJump(actor,state);
        }

        void AdvanceDagataJump(ActorClock actor,EronyaVolkanarState state)
        {
            if(!state.JumpActive)return;
            bool invalid=!EronyaValidActor(actor)||state.ResetVersion!=actor.Unit.CombatResetVersion||
                actor.Unit.IsStunned||actor.Unit.IsRooted||actor.Unit.IsBeingPulled||actor.Unit.IsFlying||
                actor.StunUntil>tick||actor.RootUntil>tick;
            if(!state.JumpLanded)invalid|=!ReferenceEquals(actor.Unit.CurrentCell,state.JumpFrom)||
                !ReferenceEquals(actor.Unit.ReservedCell,state.JumpTo)||!ReferenceEquals(state.JumpTo?.ReservedBy,actor.Unit)||
                !EronyaValidActor(state.JumpTarget)||state.JumpTarget.Unit.CombatResetVersion!=state.JumpTargetVersion;
            else invalid|=!ReferenceEquals(actor.Unit.CurrentCell,state.JumpTo);
            if(invalid){CancelDagataJump(actor,state);return;}
            if(!state.JumpLanded&&tick>=state.JumpTiming.Land)
            {
                if(!board.ConfirmMove(actor.Unit)){CancelDagataJump(actor,state);return;}
                state.JumpLanded=true;actor.LastCell=state.JumpTo;lastChangeTick=tick;
                EronyaSetBasicTarget(actor,state.JumpTarget);
                state.HiddenBladeReady=EronyaHasTrick(actor,0);
                if(EronyaHasTrick(actor,1))
                    EronyaShield(actor,actor,"dagata-humo-protector",EronyaVolkanarInt(actor,"dagata-humo-protector","shield",10),
                        EronyaVolkanarTicks(actor,"dagata-humo-protector","duration",3));
                EronyaState(actor).Module?.Moved?.Invoke(state.JumpFrom,state.JumpTo,false);
                EronyaPublishDisplacement(new CombatDisplacementSnapshot(actor.Unit,state.JumpFrom,state.JumpTo,
                    CombatDisplacementKind.OpeningJump,++eronyaEventSequence,state.JumpTiming.Start,tick,false));
                EronyaEmitAbility(actor,DagataOpeningEffect,new[]{state.JumpFrom,state.JumpTo});
            }
            if(state.JumpTiming.Finished(tick))CancelDagataJump(actor,state);
        }

        // Never advances the jump, grants a shield or changes a reservation.
        public DagataJumpSnapshot CaptureDagataJump(BattleUnit unit)
        {
            if(!begun||Finished||unit==null||!unit.IsAlive||unit.IsRoundFrozen||!unit.isActiveAndEnabled)return default;
            foreach(var pair in EronyaVolkanarStates)
                if(pair.Key.Unit==unit&&pair.Value.JumpActive&&pair.Value.ResetVersion==unit.CombatResetVersion)
                {
                    var state=pair.Value;
                    if(unit.IsStunned||unit.IsRooted||unit.IsBeingPulled||unit.IsFlying)return default;
                    if(!ReferenceEquals(unit.CurrentCell,state.JumpLanded?state.JumpTo:state.JumpFrom)||
                        !state.JumpLanded&&!ReferenceEquals(unit.ReservedCell,state.JumpTo))return default;
                    return new DagataJumpSnapshot(unit,state.JumpFrom,state.JumpTo,tick,state.JumpTiming);
                }
            return default;
        }
    }
}

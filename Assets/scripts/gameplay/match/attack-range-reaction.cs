using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
 public sealed partial class CombatSimulation
 {
  bool CanReach(ActorClock actor,MonsterPouch.Gameplay.Units.BattleUnit target)=>
   actor.Unit!=null&&target!=null&&target.IsAlive&&!target.IsFlying&&
   CombatTargetSelector.IsValidBoardPlacement(board,target)&&
   (!documentedCombat||!IsInvisible(target))&&
   CombatTargetSelector.IsInAttackRange(actor.Unit.CurrentCell,target.CurrentCell,actor.Unit.BaseStats.AttackRange);

  // Recognition runs every simulation tick, independently of attack/move cooldowns.
  // A windup can finish before IQ notices the escape, but it cannot deal remote damage.
  void TrackTargetRange()
  {
   foreach(var actor in actors)
   {
    if(actor.Unit==null||!actor.Unit.IsAlive){actor.StunRetargetPending=false;continue;}
    if(actor.StunRetargetPending&&actor.StunUntil<=tick)
    {
     // Consume the last stun once, including an early cleanse. The normal
     // decision below reacquires with current policy/reach and active taunt.
     // Stun already cancelled preparation; released projectiles stay bound.
     actor.StunRetargetPending=false;
     actor.LockedTarget=actor.TargetBeforeTaunt=null;
     actor.TargetResetVersion=actor.TargetReactionUntil=0;actor.TargetWasInRange=false;
    }
    if(actor.LockedTarget==null)continue;
    if(actor.TargetReactionUntil>0)
    {
     if(tick<actor.TargetReactionUntil)continue;
     actor.TargetReactionUntil=0;actor.TargetWasInRange=false;
     if(CanReach(actor,actor.LockedTarget)){actor.TargetWasInRange=true;continue;}
     bool preparing=pending.Exists(p=>p.Source==actor&&p.RequiresRange&&p.ReleaseTick>tick);
     if(preparing)
     {
      pending.RemoveAll(p=>p.Source==actor&&p.RequiresRange&&p.ReleaseTick>tick);
      CancelEronyaBasicMotion(actor);
      actor.Unit.InterruptAttack();AttackReset?.Invoke(actor.Unit);
      actor.NextAttack=Mathf.Max(tick,actor.SuperRecoveryUntil,actor.StunUntil);
      if(EronyaOwns(actor))actor.NextAttack=Mathf.Max(actor.NextAttack,actor.AbilityMotionUntil);
     }
     actor.LockedTarget=null;
    }
    else if(actor.TargetWasInRange&&!CanReach(actor,actor.LockedTarget))
    {
     // Forced targets remain selected, but still cannot be hit out of range.
     if(documentedCombat&&(actor.TauntTarget==actor.LockedTarget||actor.FocusTarget==actor.LockedTarget))continue;
     actor.TargetReactionUntil=tick+ToTicks(GetTargetReactionDelay(actor.Unit));
    }
   }
  }
  void CancelEronyaBasicMotion(ActorClock actor)
  {
   if(!EronyaOwns(actor)||actor.EronyaBasicMotionUntil<=0)return;
   int until=actor.EronyaBasicMotionUntil;
   actor.EronyaBasicMotionUntil=0;
   // Release only the move deadline imposed by this basic, preserving an older
   // move cooldown and any later independent ability/control commitment.
   if(actor.NextMove<=until)
    actor.NextMove=Mathf.Max(tick,actor.EronyaBasicMoveReadyTick,actor.AbilityMotionUntil,
     actor.SuperRecoveryUntil,actor.StunUntil);
   actor.EronyaBasicMoveReadyTick=0;
  }
  void ValidateAttackReleases()
  {
   foreach(var impact in pending)
   {
    if(!impact.RequiresRange||impact.RangeValidated||impact.ReleaseTick>tick)continue;
    impact.RangeValidated=true;impact.Missed=!CanReach(impact.Source,impact.Target);
    if(impact.Effect=="martio-release")
    {
     if(!impact.Missed)LaunchMartio(impact);
     impact.Missed=true; // This reservation launches a volley; it never deals target-locked damage.
    }
    if(impact.Effect=="kazuru-ring-followup")
    {
     impact.Missed |= impact.Source.Unit==null || !impact.Source.Unit.IsAlive || !impact.Source.Unit.isActiveAndEnabled;
     if(!impact.Missed)KazuruRingLaunched?.Invoke(impact.Source.Unit,impact.Target,(impact.DueTick-tick)*TickDuration);
    }
   }
  }
  int BasicPeriod(ActorClock actor)
  {
   return Mathf.Max(ToTicks(GetAttackWindup(actor.Unit))+BasicAttackTiming.TailTicks(actor.Unit),ToTicks(actor.Unit.BaseStats.AttackInterval/AttackSpeed(actor))) + KazuruBurstTicks(actor.Unit);
  }
  void ReserveSuperRecovery(ActorClock actor)
  {
   actor.LastBasicTick=tick;
   actor.EnergySuperActive=true;
   actor.EnergySuperUntil=Mathf.Max(actor.EnergySuperUntil,actor.AbilityMotionUntil);
   actor.ResumeBuguiAttack=actor.ResumeCountedAttack=false;
   actor.Unit.AttackWindupOverride=-1;
   // Recovery belongs to the Super's own phases, not the basic attack interval.
   actor.SuperRecoveryUntil=Mathf.Max(tick+1,actor.AbilityMotionUntil,actor.SuperUntil);
  }
  bool FinishEnergySuper(ActorClock actor)
  {
   if(!actor.EnergySuperActive)return true;
   if(actor.EnergyCastDue>0||actor.FlowerCastDueTick>0||actor.SuperUntil>tick||actor.EnergySuperUntil>tick||
      actor.AbilityMotionUntil>tick||actor.DashUntilTick>tick||actor.PullUntilTick>tick||
      actor.Unit.IsFlying||actor.Unit.IsBeingPulled||actor.StunUntil>tick||
      pending.Exists(p=>p.Source==actor&&p.Super&&p.ReleaseTick>tick))return false;
   actor.EnergySuperActive=false;
   actor.EnergySuperUntil=0;
   actor.PrepareFirstBasic=true;
   actor.SuperRecoveryUntil=tick;
   actor.NextAttack=tick;
   actor.ResumeBuguiAttack=actor.ResumeCountedAttack=false;
   actor.Unit.AttackWindupOverride=-1;
   actor.Unit.SetState(MonsterPouch.Gameplay.Units.UnitState.Idle);
   // QueueBasicAttack supplies one complete first-hit preparation from this idle
   // boundary. Do not add another windup-sized pause before that preparation.
   return true;
  }
 }
}

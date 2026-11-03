using System;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private bool EronyaReposition(ActorClock actor,BoardCell destination,bool forced) =>
            EronyaReposition(actor,destination,forced,forced?CombatDisplacementKind.ForcedPush:CombatDisplacementKind.Teleport);
        private bool EronyaReposition(ActorClock actor,BoardCell destination,bool forced,CombatDisplacementKind kind,int travelTicks=0)
        {
            if(!EronyaValidActor(actor)||!EronyaIsFree(destination)||actor.Unit.CurrentCell==destination)return false;
            if(forced&&EronyaOwns(actor)&&Id(actor)=="arboreo")return false;
            if(!forced&&(actor.RootUntil>tick||actor.StunUntil>tick||actor.Unit.IsRooted||actor.Unit.IsStunned))return false;
            var origin=actor.Unit.CurrentCell;
            if(!board.TryRepositionUnit(actor.Unit,destination))return false;
            CancelMovementStep(actor);actor.LastCell=destination;actor.Unit.SetState(UnitState.Idle);lastChangeTick=tick;
            // Optional real tunnel interval shares the existing legacy non-interacting travel lifecycle.
            int duration=kind==CombatDisplacementKind.Tunnel?Math.Max(0,travelTicks):0;
            if(duration>0)
            {
                actor.Unit.BeginFlight(duration*TickDuration);
                actor.NextAttack=actor.NextMove=tick+duration;
                actor.LockedTarget=null;
                foreach(var enemy in actors)if(enemy.LockedTarget==actor.Unit)enemy.LockedTarget=null;
                actor.Unit.SetState(UnitState.Moving);
            }
            EronyaState(actor).Module?.Moved?.Invoke(origin,destination,forced);
            EronyaPublishDisplacement(new CombatDisplacementSnapshot(actor.Unit,origin,destination,kind,++eronyaEventSequence,tick,tick+duration,duration==0));
            return true;
        }
        private bool EronyaPush(ActorClock source,ActorClock target,int cells)
        {
            if(!EronyaValidActor(source)||!EronyaValidActor(target)||cells<=0||Immune(target)||target.Unit.IsFlying||
                EronyaOwns(target)&&Id(target)=="arboreo")return false;
            var from=target.Unit.CurrentCell;var destination=from;var origin=source.Unit.CurrentCell;
            for(int step=0;step<cells;step++)
            {
                int dx=destination.X-origin.X,dy=destination.Y-origin.Y;
                int sx=0,sy=0;if(Math.Abs(dy)>=Math.Abs(dx))sy=Math.Sign(dy);else sx=Math.Sign(dx);
                if(sx==0&&sy==0)break;
                var next=board.GetCell(destination.X+sx,destination.Y+sy);
                if(!EronyaIsFree(next))break;destination=next;
            }
            return destination!=from&&EronyaReposition(target,destination,true,CombatDisplacementKind.ForcedPush);
        }
    }
}

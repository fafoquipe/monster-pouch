using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private void BeginAkyFlight(ActorClock actor)
        {
            BoardCell origin=actor.Unit.CurrentCell;
            int end=actor.Unit.Side==BoardSide.Blue?0:BoardManager.Height-1;
            int direction=Forward(actor.Unit);
            for(int y=end;y!=origin.Y;y-=direction)
            {
                var destination=board.GetCell(origin.X,y);
                if(!Free(destination))continue;
                int duration=ToTicks(V(actor,"aky-opening-flight","duration",1.5f));
                InterruptAttack(actor);
                actor.Unit.BeginFlight(duration*TickDuration);
                // As with walking, occupancy commits before the visual motion. The landing
                // cell stays reserved by this occupant, who cannot interact until landing.
                if(!Reposition(actor,destination)){actor.Unit.EndFlight();return;}
                actor.LockedTarget=null;
                foreach(var enemy in actors)if(enemy.LockedTarget==actor.Unit)enemy.LockedTarget=null;
                actor.NextAttack=actor.NextMove=tick+duration;
                actor.Unit.SetState(MonsterPouch.Gameplay.Units.UnitState.Moving);
                AbilityUsed?.Invoke(actor.Unit,"aky-opening-flight");
                return;
            }
        }
    }
}

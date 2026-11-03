using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private bool IsTravelling(ActorClock actor) =>
            actor.MovementStep != null && actor.MovementStep.IsValid(board);

        private void BeginMovementStep(ActorClock actor)
        {
            var step = new CombatMovementStep(actor.Unit, actor.NextMove);
            actor.MovementStep = step;
            if(eronyaStates.TryGetValue(actor,out var state))state.Module?.MovementStarted?.Invoke();
            actor.Unit.SetState(UnitState.Moving);
            lastChangeTick = tick;
            // Presentation still starts at departure, with the same route and duration.
            Moved?.Invoke(actor.Unit, step.Origin, step.Destination);
        }

        private void CompleteMovementSteps()
        {
            foreach (ActorClock actor in actors)
            {
                CombatMovementStep step = actor.MovementStep;
                if (step == null || !step.Advance(board, tick)) continue;
                actor.MovementStep = null;
                if (step.Arrived) { actor.LastCell = step.Destination; lastChangeTick = tick;
                    if(eronyaStates.TryGetValue(actor,out var state))state.Module?.Moved?.Invoke(step.Origin,step.Destination,false); }
                if (actor.Unit != null && actor.Unit.IsAlive && actor.Unit.State == UnitState.Moving)
                    actor.Unit.SetState(UnitState.Idle);
            }
        }

        private void CancelMovementStep(ActorClock actor)
        {
            actor.MovementStep?.Cancel(board);
            actor.MovementStep = null;
        }
    }
}

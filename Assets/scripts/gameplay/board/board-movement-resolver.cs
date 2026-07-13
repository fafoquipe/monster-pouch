using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Board
{
    public static class BoardMovementResolver
    {
        public static List<BoardMovementResult> ResolveMovement(
            BoardManager boardManager,
            IReadOnlyList<BoardMovementIntent> movementIntents)
        {
            if (movementIntents == null)
                return new List<BoardMovementResult>();

            if (boardManager == null)
                return new List<BoardMovementResult>();

            int count = movementIntents.Count;
            var results = new BoardMovementResult[count];
            var reservationIntents = new List<BoardReservationIntent>();
            var intentToReservationIndex = new Dictionary<int, int>();
            var seenUnits = new HashSet<IBoardUnit>();

            for (int i = 0; i < count; i++)
            {
                BoardMovementIntent intent = movementIntents[i];
                IBoardUnit unit = intent.Unit;
                BoardCell targetCell = intent.TargetCell;

                if (!IsValidIntent(boardManager, unit, targetCell))
                {
                    results[i] = new BoardMovementResult(unit, targetCell, null, BoardMovementStatus.Invalid);
                    continue;
                }

                if (seenUnits.Contains(unit))
                {
                    results[i] = new BoardMovementResult(unit, targetCell, null, BoardMovementStatus.Invalid);
                    continue;
                }

                seenUnits.Add(unit);

                if (ReferenceEquals(unit.CurrentCell, targetCell))
                {
                    results[i] = new BoardMovementResult(unit, targetCell, null, BoardMovementStatus.AlreadyAtTarget);
                    continue;
                }

                var path = new List<BoardCell>();
                bool pathFound = BoardPathfinder.TryFindPath(boardManager, unit, unit.CurrentCell, targetCell, path);

                if (!pathFound)
                {
                    results[i] = new BoardMovementResult(unit, targetCell, null, BoardMovementStatus.NoPath);
                    continue;
                }

                if (path.Count == 0)
                {
                    results[i] = new BoardMovementResult(unit, targetCell, null, BoardMovementStatus.AlreadyAtTarget);
                    continue;
                }

                BoardCell nextCell = path[0];
                var reservationIntent = new BoardReservationIntent(unit, nextCell);
                intentToReservationIndex[i] = reservationIntents.Count;
                reservationIntents.Add(reservationIntent);
            }

            if (reservationIntents.Count > 0)
            {
                List<BoardReservationResult> reservationResults =
                    BoardReservationResolver.Resolve(boardManager, reservationIntents);

                var reservationOutcomeByUnit = new Dictionary<IBoardUnit, BoardReservationOutcome>();

                for (int i = 0; i < reservationResults.Count; i++)
                {
                    BoardReservationResult rr = reservationResults[i];
                    reservationOutcomeByUnit[rr.Intent.Unit] = rr.Outcome;
                }

                foreach (var kvp in intentToReservationIndex)
                {
                    int originalIndex = kvp.Key;
                    BoardMovementIntent originalIntent = movementIntents[originalIndex];
                    IBoardUnit unit = originalIntent.Unit;
                    BoardCell targetCell = originalIntent.TargetCell;
                    BoardReservationIntent reservationIntent = reservationIntents[kvp.Value];
                    BoardCell nextCell = reservationIntent.DestinationCell;

                    BoardReservationOutcome outcome = reservationOutcomeByUnit[unit];

                    if (outcome == BoardReservationOutcome.Reserved)
                    {
                        bool moveConfirmed = boardManager.ConfirmMove(unit);

                        if (moveConfirmed)
                        {
                            results[originalIndex] = new BoardMovementResult(
                                unit, targetCell, nextCell, BoardMovementStatus.Moved);
                        }
                        else
                        {
                            results[originalIndex] = new BoardMovementResult(
                                unit, targetCell, nextCell, BoardMovementStatus.MoveFailed);
                        }
                    }
                    else
                    {
                        results[originalIndex] = new BoardMovementResult(
                            unit, targetCell, nextCell, BoardMovementStatus.ReservationRejected);
                    }
                }
            }

            var finalResults = new List<BoardMovementResult>(count);

            for (int i = 0; i < count; i++)
            {
                finalResults.Add(results[i]);
            }

            return finalResults;
        }

        private static bool IsValidIntent(BoardManager boardManager, IBoardUnit unit, BoardCell targetCell)
        {
            if (unit == null)
                return false;

            if (targetCell == null)
                return false;

            if (unit.CurrentCell == null)
                return false;

            if (!boardManager.IsManagedCell(unit.CurrentCell))
                return false;

            if (!boardManager.IsManagedCell(targetCell))
                return false;

            if (!ReferenceEquals(unit.CurrentCell.OccupiedBy, unit))
                return false;

            return true;
        }
    }
}

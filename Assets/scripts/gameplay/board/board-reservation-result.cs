namespace MonsterPouch.Gameplay.Board
{
    public sealed class BoardReservationResult
    {
        public BoardReservationIntent Intent { get; }
        public BoardReservationOutcome Outcome { get; }

        public BoardReservationResult(BoardReservationIntent intent, BoardReservationOutcome outcome)
        {
            Intent = intent;
            Outcome = outcome;
        }
    }
}

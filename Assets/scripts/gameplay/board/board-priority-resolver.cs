namespace MonsterPouch.Gameplay.Board
{
    public static class BoardPriorityResolver
    {
        public static BoardPriorityResult CompareCellValue(BoardCell firstCell, BoardCell secondCell)
        {
            if (firstCell == null || secondCell == null)
                return BoardPriorityResult.Unresolved;

            if (firstCell.Value > secondCell.Value)
                return BoardPriorityResult.First;

            if (secondCell.Value > firstCell.Value)
                return BoardPriorityResult.Second;

            return BoardPriorityResult.Unresolved;
        }

        public static BoardPriorityResult CompareQuadrants(
            BoardQuadrant firstQuadrant,
            BoardQuadrant secondQuadrant)
        {
            if (firstQuadrant == secondQuadrant)
                return BoardPriorityResult.Unresolved;

            if (QuadrantBeats(firstQuadrant, secondQuadrant))
                return BoardPriorityResult.First;

            if (QuadrantBeats(secondQuadrant, firstQuadrant))
                return BoardPriorityResult.Second;

            return BoardPriorityResult.Unresolved;
        }

        public static bool QuadrantBeats(BoardQuadrant attacker, BoardQuadrant defender)
        {
            if (attacker == defender)
                return false;

            return (attacker == BoardQuadrant.A && defender == BoardQuadrant.D)
                || (attacker == BoardQuadrant.B && defender == BoardQuadrant.A)
                || (attacker == BoardQuadrant.B && defender == BoardQuadrant.C)
                || (attacker == BoardQuadrant.C && defender == BoardQuadrant.A)
                || (attacker == BoardQuadrant.C && defender == BoardQuadrant.D)
                || (attacker == BoardQuadrant.D && defender == BoardQuadrant.B);
        }

        public static BoardPriorityResult CompareTerritorialAdvantage(
            BoardSide territorySide,
            BoardSide firstUnitSide,
            BoardSide secondUnitSide)
        {
            if (firstUnitSide == secondUnitSide)
                return BoardPriorityResult.Unresolved;

            if (firstUnitSide == territorySide && secondUnitSide != territorySide)
                return BoardPriorityResult.First;

            if (secondUnitSide == territorySide && firstUnitSide != territorySide)
                return BoardPriorityResult.Second;

            return BoardPriorityResult.Unresolved;
        }

        public static BoardPriorityResult CompareSharedTerritorialAdvantage(
            BoardCell firstCell,
            BoardSide firstUnitSide,
            BoardCell secondCell,
            BoardSide secondUnitSide)
        {
            if (firstCell == null || secondCell == null)
                return BoardPriorityResult.Unresolved;

            if (firstCell.Side != secondCell.Side)
                return BoardPriorityResult.Unresolved;

            return CompareTerritorialAdvantage(
                firstCell.Side,
                firstUnitSide,
                secondUnitSide);
        }

        public static BoardPriorityResult ComparePositionalPriority(
            BoardCell firstCell,
            BoardSide firstUnitSide,
            BoardCell secondCell,
            BoardSide secondUnitSide)
        {
            if (firstCell == null || secondCell == null)
                return BoardPriorityResult.Unresolved;

            BoardPriorityResult result = CompareCellValue(firstCell, secondCell);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            result = CompareQuadrants(
                firstCell.Quadrant,
                secondCell.Quadrant);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            result = CompareSharedTerritorialAdvantage(
                firstCell,
                firstUnitSide,
                secondCell,
                secondUnitSide);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            return BoardPriorityResult.Unresolved;
        }

        public static BoardPriorityResult CompareReservationPriority(
            BoardCell destinationCell,
            BoardCell firstCurrentCell,
            BoardSide firstUnitSide,
            BoardCell secondCurrentCell,
            BoardSide secondUnitSide)
        {
            if (destinationCell == null || firstCurrentCell == null || secondCurrentCell == null)
                return BoardPriorityResult.Unresolved;

            BoardPriorityResult result = CompareCellValue(firstCurrentCell, secondCurrentCell);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            result = CompareQuadrants(
                firstCurrentCell.Quadrant,
                secondCurrentCell.Quadrant);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            result = CompareTerritorialAdvantage(
                destinationCell.Side,
                firstUnitSide,
                secondUnitSide);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            return BoardPriorityResult.Unresolved;
        }

        public static BoardPriorityResult CompareFinalSimultaneousDeath(
            BoardCell firstCell,
            BoardSide firstUnitSide,
            BoardCell secondCell,
            BoardSide secondUnitSide)
        {
            if (firstCell == null || secondCell == null)
                return BoardPriorityResult.Unresolved;

            BoardPriorityResult result = CompareCellValue(firstCell, secondCell);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            result = CompareQuadrants(
                firstCell.Quadrant,
                secondCell.Quadrant);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            result = CompareSharedTerritorialAdvantage(
                firstCell,
                firstUnitSide,
                secondCell,
                secondUnitSide);
            if (result != BoardPriorityResult.Unresolved)
                return result;

            return BoardPriorityResult.Unresolved;
        }
    }
}

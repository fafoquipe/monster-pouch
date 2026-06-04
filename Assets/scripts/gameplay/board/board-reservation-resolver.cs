using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Board
{
    public static class BoardReservationResolver
    {
        public static List<BoardReservationResult> Resolve(
            BoardManager boardManager,
            IReadOnlyList<BoardReservationIntent> intents)
        {
            if (intents == null)
                return new List<BoardReservationResult>();

            if (boardManager == null)
            {
                var allInvalid = new List<BoardReservationResult>(intents.Count);
                for (int i = 0; i < intents.Count; i++)
                    allInvalid.Add(new BoardReservationResult(intents[i], BoardReservationOutcome.Invalid));
                return allInvalid;
            }

            int count = intents.Count;
            BoardReservationResult[] results = new BoardReservationResult[count];
            bool[] isDuplicate = new bool[count];

            for (int i = 0; i < count; i++)
            {
                if (intents[i]?.Unit == null)
                    continue;

                for (int j = i + 1; j < count; j++)
                {
                    if (intents[j]?.Unit == null)
                        continue;

                    if (ReferenceEquals(intents[i].Unit, intents[j].Unit))
                    {
                        isDuplicate[i] = true;
                        isDuplicate[j] = true;
                    }
                }
            }

            bool[] isValid = new bool[count];

            for (int i = 0; i < count; i++)
            {
                if (isDuplicate[i])
                {
                    results[i] = new BoardReservationResult(intents[i], BoardReservationOutcome.Invalid);
                    continue;
                }

                if (IsValidIntent(boardManager, intents[i]))
                {
                    isValid[i] = true;
                }
                else
                {
                    results[i] = new BoardReservationResult(intents[i], BoardReservationOutcome.Invalid);
                }
            }

            var groups = new Dictionary<BoardCell, List<int>>();

            for (int i = 0; i < count; i++)
            {
                if (!isValid[i])
                    continue;

                BoardCell dest = intents[i].DestinationCell;

                if (!groups.TryGetValue(dest, out var list))
                {
                    list = new List<int>();
                    groups[dest] = list;
                }

                list.Add(i);
            }

            foreach (var kvp in groups)
            {
                ResolveGroup(boardManager, intents, kvp.Value, results);
            }

            for (int i = 0; i < count; i++)
            {
                if (results[i] == null)
                    results[i] = new BoardReservationResult(intents[i], BoardReservationOutcome.Invalid);
            }

            return new List<BoardReservationResult>(results);
        }

        private static bool IsValidIntent(BoardManager boardManager, BoardReservationIntent intent)
        {
            if (intent == null)
                return false;

            if (intent.Unit == null)
                return false;

            if (intent.OriginCell == null)
                return false;

            if (intent.DestinationCell == null)
                return false;

            if (!boardManager.IsManagedCell(intent.OriginCell))
                return false;

            if (!boardManager.IsManagedCell(intent.DestinationCell))
                return false;

            if (!ReferenceEquals(intent.Unit.CurrentCell, intent.OriginCell))
                return false;

            if (!ReferenceEquals(intent.OriginCell.OccupiedBy, intent.Unit))
                return false;

            if (!boardManager.CanReserveCell(intent.Unit, intent.DestinationCell))
                return false;

            return true;
        }

        private static void ResolveGroup(
            BoardManager boardManager,
            IReadOnlyList<BoardReservationIntent> allIntents,
            List<int> groupIndices,
            BoardReservationResult[] results)
        {
            BoardCell destinationCell = allIntents[groupIndices[0]].DestinationCell;

            if (groupIndices.Count == 1)
            {
                int idx = groupIndices[0];
                var intent = allIntents[idx];
                bool success = boardManager.TryReserveCell(intent.Unit, destinationCell);
                results[idx] = new BoardReservationResult(
                    intent,
                    success ? BoardReservationOutcome.Reserved : BoardReservationOutcome.Invalid);
                return;
            }

            var groupOutcomes = new Dictionary<int, BoardReservationOutcome>();
            List<int> survivors = new List<int>(groupIndices);

            survivors = FilterByHighestIQSpeed(allIntents, survivors, groupOutcomes);
            if (survivors.Count == 1)
            {
                ApplyWinner(boardManager, allIntents, survivors[0], groupIndices, groupOutcomes, results);
                return;
            }

            survivors = FilterByHighestCellValue(allIntents, survivors, groupOutcomes);
            if (survivors.Count == 1)
            {
                ApplyWinner(boardManager, allIntents, survivors[0], groupIndices, groupOutcomes, results);
                return;
            }

            int? dominantGlobalIdx = TryGetUniqueQuadrantDominant(allIntents, survivors);

            if (dominantGlobalIdx.HasValue)
            {
                int winnerIdx = dominantGlobalIdx.Value;

                foreach (int idx in survivors)
                {
                    if (idx != winnerIdx)
                        groupOutcomes[idx] = BoardReservationOutcome.Rejected;
                }

                ApplyWinner(boardManager, allIntents, winnerIdx, groupIndices, groupOutcomes, results);
                return;
            }

            survivors = FilterByTerritory(allIntents, destinationCell, survivors, groupOutcomes);

            if (survivors.Count == 1)
            {
                ApplyWinner(boardManager, allIntents, survivors[0], groupIndices, groupOutcomes, results);
                return;
            }

            foreach (int idx in survivors)
                groupOutcomes[idx] = BoardReservationOutcome.Unresolved;

            WriteGroupResults(allIntents, groupIndices, groupOutcomes, results);
        }

        private static List<int> FilterByHighestIQSpeed(
            IReadOnlyList<BoardReservationIntent> allIntents,
            List<int> survivors,
            Dictionary<int, BoardReservationOutcome> groupOutcomes)
        {
            int maxIQ = int.MinValue;

            foreach (int idx in survivors)
            {
                int iq = allIntents[idx].Unit.IQSpeed;
                if (iq > maxIQ)
                    maxIQ = iq;
            }

            var result = new List<int>(survivors.Count);

            foreach (int idx in survivors)
            {
                if (allIntents[idx].Unit.IQSpeed == maxIQ)
                {
                    result.Add(idx);
                }
                else
                {
                    groupOutcomes[idx] = BoardReservationOutcome.Rejected;
                }
            }

            return result;
        }

        private static List<int> FilterByHighestCellValue(
            IReadOnlyList<BoardReservationIntent> allIntents,
            List<int> survivors,
            Dictionary<int, BoardReservationOutcome> groupOutcomes)
        {
            int maxValue = int.MinValue;

            foreach (int idx in survivors)
            {
                int val = allIntents[idx].OriginCell.Value;
                if (val > maxValue)
                    maxValue = val;
            }

            var result = new List<int>(survivors.Count);

            foreach (int idx in survivors)
            {
                if (allIntents[idx].OriginCell.Value == maxValue)
                {
                    result.Add(idx);
                }
                else
                {
                    groupOutcomes[idx] = BoardReservationOutcome.Rejected;
                }
            }

            return result;
        }

        private static int? TryGetUniqueQuadrantDominant(
            IReadOnlyList<BoardReservationIntent> allIntents,
            List<int> survivors)
        {
            int? dominantGlobalIdx = null;

            for (int i = 0; i < survivors.Count; i++)
            {
                int candidateIdx = survivors[i];
                BoardQuadrant candidateQuadrant = allIntents[candidateIdx].OriginCell.Quadrant;
                bool beatsAll = true;

                for (int j = 0; j < survivors.Count; j++)
                {
                    if (i == j)
                        continue;

                    BoardQuadrant otherQuadrant = allIntents[survivors[j]].OriginCell.Quadrant;

                    if (!BoardPriorityResolver.QuadrantBeats(candidateQuadrant, otherQuadrant))
                    {
                        beatsAll = false;
                        break;
                    }
                }

                if (beatsAll)
                {
                    if (dominantGlobalIdx.HasValue)
                        return null;

                    dominantGlobalIdx = candidateIdx;
                }
            }

            return dominantGlobalIdx;
        }

        private static List<int> FilterByTerritory(
            IReadOnlyList<BoardReservationIntent> allIntents,
            BoardCell destinationCell,
            List<int> survivors,
            Dictionary<int, BoardReservationOutcome> groupOutcomes)
        {
            BoardSide territorySide = destinationCell.Side;
            var matching = new List<int>(survivors.Count);

            foreach (int idx in survivors)
            {
                if (allIntents[idx].Unit.Side == territorySide)
                    matching.Add(idx);
            }

            if (matching.Count == 0)
                return survivors;

            foreach (int idx in survivors)
            {
                if (allIntents[idx].Unit.Side != territorySide)
                    groupOutcomes[idx] = BoardReservationOutcome.Rejected;
            }

            return matching;
        }

        private static void ApplyWinner(
            BoardManager boardManager,
            IReadOnlyList<BoardReservationIntent> allIntents,
            int winnerGlobalIdx,
            List<int> groupIndices,
            Dictionary<int, BoardReservationOutcome> groupOutcomes,
            BoardReservationResult[] results)
        {
            var winnerIntent = allIntents[winnerGlobalIdx];
            bool success = boardManager.TryReserveCell(winnerIntent.Unit, winnerIntent.DestinationCell);

            groupOutcomes[winnerGlobalIdx] = success
                ? BoardReservationOutcome.Reserved
                : BoardReservationOutcome.Invalid;

            foreach (int idx in groupIndices)
            {
                if (!groupOutcomes.ContainsKey(idx))
                    groupOutcomes[idx] = BoardReservationOutcome.Rejected;
            }

            WriteGroupResults(allIntents, groupIndices, groupOutcomes, results);
        }

        private static void WriteGroupResults(
            IReadOnlyList<BoardReservationIntent> allIntents,
            List<int> groupIndices,
            Dictionary<int, BoardReservationOutcome> groupOutcomes,
            BoardReservationResult[] results)
        {
            foreach (int idx in groupIndices)
            {
                if (results[idx] != null)
                    continue;

                results[idx] = new BoardReservationResult(
                    allIntents[idx],
                    groupOutcomes.TryGetValue(idx, out var outcome) ? outcome : BoardReservationOutcome.Rejected);
            }
        }
    }
}

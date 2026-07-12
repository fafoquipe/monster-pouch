using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Board
{
    public static class BoardPathfinder
    {
        private static readonly int[] DirX = { 0, 1, 0, -1 };
        private static readonly int[] DirY = { 1, 0, -1, 0 };

        public static bool TryFindPath(
            BoardManager boardManager,
            IBoardUnit unit,
            BoardCell startCell,
            BoardCell targetCell,
            List<BoardCell> path)
        {
            if (path == null)
                return false;

            path.Clear();

            if (boardManager == null)
                return false;

            if (unit == null)
                return false;

            if (startCell == null)
                return false;

            if (targetCell == null)
                return false;

            if (!IsManagedCell(boardManager, startCell))
                return false;

            if (!IsManagedCell(boardManager, targetCell))
                return false;

            if (!ReferenceEquals(unit.CurrentCell, startCell))
                return false;

            if (!ReferenceEquals(startCell.OccupiedBy, unit))
                return false;

            if (ReferenceEquals(startCell, targetCell))
                return true;

            if (!IsTraversable(targetCell, unit))
                return false;

            var openSet = new List<BoardCell> { startCell };
            var closedSet = new HashSet<BoardCell>();
            var cameFrom = new Dictionary<BoardCell, BoardCell>();
            var gScore = new Dictionary<BoardCell, int> { { startCell, 0 } };

            while (openSet.Count > 0)
            {
                int bestIndex = GetBestOpenSetIndex(openSet, gScore, boardManager, targetCell);
                BoardCell current = openSet[bestIndex];

                if (ReferenceEquals(current, targetCell))
                {
                    ReconstructPath(cameFrom, current, path);
                    return true;
                }

                openSet.RemoveAt(bestIndex);
                closedSet.Add(current);

                for (int i = 0; i < 4; i++)
                {
                    int nx = current.X + DirX[i];
                    int ny = current.Y + DirY[i];
                    BoardCell neighbor = boardManager.GetCell(nx, ny);

                    if (neighbor == null)
                        continue;

                    if (closedSet.Contains(neighbor))
                        continue;

                    if (!IsTraversable(neighbor, unit))
                        continue;

                    int tentativeG = gScore[current] + 1;

                    if (!gScore.TryGetValue(neighbor, out int existingG) || tentativeG < existingG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;

                        if (!openSet.Contains(neighbor))
                            openSet.Add(neighbor);
                    }
                }
            }

            return false;
        }

        private static bool IsManagedCell(BoardManager boardManager, BoardCell cell)
        {
            BoardCell managed = boardManager.GetCell(cell.X, cell.Y);
            return ReferenceEquals(managed, cell);
        }

        private static bool IsTraversable(BoardCell cell, IBoardUnit unit)
        {
            if (cell.IsBlocked)
                return false;

            if (cell.OccupiedBy != null && !ReferenceEquals(cell.OccupiedBy, unit))
                return false;

            if (cell.ReservedBy != null && !ReferenceEquals(cell.ReservedBy, unit))
                return false;

            return true;
        }

        private static int GetManhattanDistance(BoardCell a, BoardCell b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return (dx < 0 ? -dx : dx) + (dy < 0 ? -dy : dy);
        }

        private static int GetBestOpenSetIndex(
            List<BoardCell> openSet,
            Dictionary<BoardCell, int> gScore,
            BoardManager boardManager,
            BoardCell targetCell)
        {
            int bestIndex = 0;
            BoardCell bestCell = openSet[0];
            int bestH = GetManhattanDistance(bestCell, targetCell);
            int bestF = gScore[bestCell] + bestH;
            int bestValue = boardManager.GetCell(bestCell.X, bestCell.Y).Value;

            for (int i = 1; i < openSet.Count; i++)
            {
                BoardCell cell = openSet[i];
                int h = GetManhattanDistance(cell, targetCell);
                int g = gScore[cell];
                int f = g + h;
                int cellValue = boardManager.GetCell(cell.X, cell.Y).Value;

                if (f < bestF)
                {
                    bestIndex = i;
                    bestCell = cell;
                    bestF = f;
                    bestH = h;
                    bestValue = cellValue;
                }
                else if (f == bestF)
                {
                    if (h < bestH)
                    {
                        bestIndex = i;
                        bestCell = cell;
                        bestF = f;
                        bestH = h;
                        bestValue = cellValue;
                    }
                    else if (h == bestH)
                    {
                        if (cellValue > bestValue)
                        {
                            bestIndex = i;
                            bestCell = cell;
                            bestF = f;
                            bestH = h;
                            bestValue = cellValue;
                        }
                        else if (cellValue == bestValue)
                        {
                            if (cell.Y < bestCell.Y)
                            {
                                bestIndex = i;
                                bestCell = cell;
                                bestF = f;
                                bestH = h;
                                bestValue = cellValue;
                            }
                            else if (cell.Y == bestCell.Y && cell.X < bestCell.X)
                            {
                                bestIndex = i;
                                bestCell = cell;
                                bestF = f;
                                bestH = h;
                                bestValue = cellValue;
                            }
                        }
                    }
                }
            }

            return bestIndex;
        }

        private static void ReconstructPath(
            Dictionary<BoardCell, BoardCell> cameFrom,
            BoardCell current,
            List<BoardCell> path)
        {
            while (cameFrom.ContainsKey(current))
            {
                BoardCell previous = cameFrom[current];
                path.Add(current);
                current = previous;
            }

            path.Reverse();
        }
    }
}

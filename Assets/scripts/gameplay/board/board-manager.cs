using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Gameplay.Board
{
    public sealed class BoardManager : MonoBehaviour
    {
        public const int Width = 6;
        public const int Height = 10;

        private static readonly int[,] CellValues =
        {
            { 7, 6, 5, 5, 6, 7 },
            { 6, 5, 4, 4, 5, 6 },
            { 5, 4, 3, 3, 4, 5 },
            { 4, 3, 2, 2, 3, 4 },
            { 3, 2, 1, 1, 2, 3 },
            { 3, 2, 1, 1, 2, 3 },
            { 4, 3, 2, 2, 3, 4 },
            { 5, 4, 3, 3, 4, 5 },
            { 6, 5, 4, 4, 5, 6 },
            { 7, 6, 5, 5, 6, 7 },
        };

        [SerializeField] private Vector2Int redMonsterDefaultSpawn = new Vector2Int(3, 4);
        [SerializeField] private Vector2Int blueMonsterDefaultSpawn = new Vector2Int(2, 5);

        private BoardCell[,] cells;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            BuildBoard();
        }

        public void BuildBoard()
        {
            cells = new BoardCell[Width, Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int value = GetConfiguredValue(x, y);
                    BoardQuadrant quadrant = GetQuadrant(x, y);
                    BoardSide side = GetSide(x, y);
                    cells[x, y] = new BoardCell(x, y, value, quadrant, side);
                }
            }

            IsInitialized = true;
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public BoardCell GetCell(int x, int y)
        {
            if (!IsInside(x, y))
                return null;

            return cells[x, y];
        }

        public bool TryGetCell(int x, int y, out BoardCell cell)
        {
            cell = GetCell(x, y);
            return cell != null;
        }

        public int GetCellValue(int x, int y)
        {
            BoardCell cell = GetCell(x, y);

            if (cell == null)
                return -1;

            return cell.Value;
        }

        public BoardQuadrant GetQuadrant(int x, int y)
        {
            bool isTopHalf = y < 5;
            bool isLeftHalf = x < 3;

            if (isTopHalf && isLeftHalf) return BoardQuadrant.A;
            if (isTopHalf && !isLeftHalf) return BoardQuadrant.B;
            if (!isTopHalf && isLeftHalf) return BoardQuadrant.C;

            return BoardQuadrant.D;
        }

        public BoardSide GetSide(int x, int y)
        {
            if (y < 5)
                return BoardSide.Red;

            return BoardSide.Blue;
        }

        public BoardCell GetDefaultMonsterSpawnCell(BoardSide side)
        {
            Vector2Int spawnCoords = side == BoardSide.Red
                ? redMonsterDefaultSpawn
                : blueMonsterDefaultSpawn;

            if (!IsInside(spawnCoords.x, spawnCoords.y))
            {
                Debug.LogWarning(
                    $"Default spawn coordinates ({spawnCoords.x}, {spawnCoords.y}) " +
                    $"for {side} side are outside the board. Returning null.");
                return null;
            }

            return GetCell(spawnCoords.x, spawnCoords.y);
        }

        public List<BoardCell> GetAllCells()
        {
            var result = new List<BoardCell>(Width * Height);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    result.Add(cells[x, y]);
                }
            }

            return result;
        }

        public List<BoardCell> GetCellsByValueAndQuadrant(int value, BoardQuadrant quadrant)
        {
            var result = new List<BoardCell>();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    BoardCell cell = cells[x, y];

                    if (cell.MatchesValueAndQuadrant(value, quadrant))
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        public bool TryOccupyCell(IBoardUnit unit, int x, int y)
        {
            return TryOccupyCell(unit, GetCell(x, y));
        }

        public bool TryOccupyCell(IBoardUnit unit, BoardCell cell)
        {
            if (unit == null || cell == null || !IsManagedCell(cell))
                return false;

            if (unit.CurrentCell != null && ReferenceEquals(unit.CurrentCell, cell) && ReferenceEquals(cell.OccupiedBy, unit))
                return true;

            if (unit.CurrentCell != null)
                return false;

            if (unit.ReservedCell != null)
                return false;

            if (!cell.TryOccupy(unit))
                return false;

            unit.SetCurrentCell(cell);
            return true;
        }

        public bool TryReserveCell(IBoardUnit unit, int x, int y)
        {
            return TryReserveCell(unit, GetCell(x, y));
        }

        public bool TryReserveCell(IBoardUnit unit, BoardCell destinationCell)
        {
            if (!CanReserveCell(unit, destinationCell))
                return false;

            if (unit.ReservedCell != null && ReferenceEquals(unit.ReservedCell, destinationCell) && ReferenceEquals(destinationCell.ReservedBy, unit))
                return true;

            if (!destinationCell.TryReserve(unit))
                return false;

            unit.SetReservedCell(destinationCell);
            return true;
        }

        public bool CancelReservation(IBoardUnit unit)
        {
            if (unit == null)
                return false;

            BoardCell reservedCell = unit.ReservedCell;

            if (reservedCell == null)
                return false;

            if (!IsManagedCell(reservedCell))
                return false;

            if (!reservedCell.ClearReservation(unit))
                return false;

            unit.ClearReservedCell();
            return true;
        }

        public bool ConfirmMove(IBoardUnit unit)
        {
            if (unit == null)
                return false;

            BoardCell originCell = unit.CurrentCell;
            BoardCell destinationCell = unit.ReservedCell;

            if (originCell == null || destinationCell == null)
                return false;

            if (!IsManagedCell(originCell) || !IsManagedCell(destinationCell))
                return false;

            if (!ReferenceEquals(originCell.OccupiedBy, unit))
                return false;

            if (!ReferenceEquals(destinationCell.ReservedBy, unit))
                return false;

            if (!destinationCell.CanBeOccupiedBy(unit))
                return false;

            if (!originCell.ClearOccupant(unit))
                return false;

            if (!destinationCell.ClearReservation(unit))
                return false;

            if (!destinationCell.TryOccupy(unit))
                return false;

            unit.SetCurrentCell(destinationCell);
            unit.ClearReservedCell();
            return true;
        }

        public bool ReleaseUnit(IBoardUnit unit)
        {
            if (unit == null)
                return false;

            bool hasChanged = false;

            if (unit.ReservedCell != null && IsManagedCell(unit.ReservedCell) && ReferenceEquals(unit.ReservedCell.ReservedBy, unit))
            {
                unit.ReservedCell.ClearReservation(unit);
                hasChanged = true;
            }

            if (unit.CurrentCell != null && IsManagedCell(unit.CurrentCell) && ReferenceEquals(unit.CurrentCell.OccupiedBy, unit))
            {
                unit.CurrentCell.ClearOccupant(unit);
                hasChanged = true;
            }

            unit.ClearBoardState();
            return hasChanged;
        }

        public bool TrySetCellBlocked(int x, int y, bool isBlocked)
        {
            BoardCell cell = GetCell(x, y);

            if (cell == null)
                return false;

            return cell.TrySetBlocked(isBlocked);
        }

        internal bool CanReserveCell(IBoardUnit unit, BoardCell destinationCell)
        {
            if (unit == null)
                return false;

            if (destinationCell == null || !IsManagedCell(destinationCell))
                return false;

            if (unit.CurrentCell == null || !IsManagedCell(unit.CurrentCell))
                return false;

            if (!ReferenceEquals(unit.CurrentCell.OccupiedBy, unit))
                return false;

            if (ReferenceEquals(destinationCell, unit.CurrentCell))
                return false;

            if (unit.ReservedCell != null && ReferenceEquals(unit.ReservedCell, destinationCell) && ReferenceEquals(destinationCell.ReservedBy, unit))
                return true;

            if (unit.ReservedCell != null)
                return false;

            return destinationCell.CanBeReservedBy(unit);
        }

        internal bool IsManagedCell(BoardCell cell)
        {
            if (cell == null)
                return false;

            BoardCell managedCell = GetCell(cell.X, cell.Y);
            return ReferenceEquals(managedCell, cell);
        }

        private int GetConfiguredValue(int x, int y)
        {
            return CellValues[y, x];
        }
    }
}

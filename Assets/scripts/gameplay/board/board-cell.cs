using UnityEngine;

namespace MonsterPouch.Gameplay.Board
{
    [System.Serializable]
    public sealed class BoardCell
    {
        public int X { get; }
        public int Y { get; }
        public Vector2Int Coordinates => new Vector2Int(X, Y);
        public int Value { get; }
        public BoardQuadrant Quadrant { get; }
        public BoardSide Side { get; }

        public IBoardUnit OccupiedBy { get; private set; }
        public IBoardUnit ReservedBy { get; private set; }
        public bool IsBlocked { get; private set; }

        public bool IsOccupied => OccupiedBy != null;
        public bool IsReserved => ReservedBy != null;

        public BoardCell(int x, int y, int value, BoardQuadrant quadrant, BoardSide side)
        {
            X = x;
            Y = y;
            Value = value;
            Quadrant = quadrant;
            Side = side;
        }

        public bool MatchesCoordinates(int x, int y)
        {
            return X == x && Y == y;
        }

        public bool MatchesValueAndQuadrant(int value, BoardQuadrant quadrant)
        {
            return Value == value && Quadrant == quadrant;
        }

        internal bool CanBeOccupiedBy(IBoardUnit unit)
        {
            if (unit == null)
                return false;

            if (IsBlocked)
                return false;

            if (OccupiedBy != null && !ReferenceEquals(OccupiedBy, unit))
                return false;

            if (ReservedBy != null && !ReferenceEquals(ReservedBy, unit))
                return false;

            return true;
        }

        internal bool CanBeReservedBy(IBoardUnit unit)
        {
            if (unit == null)
                return false;

            if (IsBlocked)
                return false;

            if (IsOccupied)
                return false;

            if (ReservedBy != null && !ReferenceEquals(ReservedBy, unit))
                return false;

            return true;
        }

        internal bool TryOccupy(IBoardUnit unit)
        {
            if (!CanBeOccupiedBy(unit))
                return false;

            OccupiedBy = unit;
            return true;
        }

        internal bool TryReserve(IBoardUnit unit)
        {
            if (!CanBeReservedBy(unit))
                return false;

            ReservedBy = unit;
            return true;
        }

        internal bool ClearOccupant(IBoardUnit unit)
        {
            if (unit == null)
                return false;

            if (!ReferenceEquals(OccupiedBy, unit))
                return false;

            OccupiedBy = null;
            return true;
        }

        internal bool ClearReservation(IBoardUnit unit)
        {
            if (unit == null)
                return false;

            if (!ReferenceEquals(ReservedBy, unit))
                return false;

            ReservedBy = null;
            return true;
        }

        internal bool TrySetBlocked(bool isBlocked)
        {
            if (isBlocked && (IsOccupied || IsReserved))
                return false;

            IsBlocked = isBlocked;
            return true;
        }

        public override string ToString()
        {
            return $"Cell ({X},{Y}) | Value {Value} | Quadrant {Quadrant} | Side {Side}";
        }
    }
}

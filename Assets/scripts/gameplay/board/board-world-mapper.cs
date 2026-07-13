using UnityEngine;

namespace MonsterPouch.Gameplay.Board
{
    public sealed class BoardWorldMapper : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private Transform boardOrigin;
        [SerializeField] private Vector2 cellSize = new Vector2(0.26f, 0.18f);
        [SerializeField] private Vector2 boardOffset = Vector2.zero;

        public BoardManager BoardManager => boardManager;
        public Vector2 CellSize => cellSize;
        public Vector2 BoardOffset => boardOffset;

        public void Configure(BoardManager newBoardManager, Transform newBoardOrigin, Vector2 newCellSize, Vector2 newBoardOffset)
        {
            boardManager = newBoardManager;
            boardOrigin = newBoardOrigin;
            cellSize = newCellSize;
            boardOffset = newBoardOffset;
        }

        public Vector3 GetWorldPosition(BoardCell cell)
        {
            if (TryGetWorldPosition(cell, out Vector3 worldPosition))
                return worldPosition;

            return Vector3.zero;
        }

        public bool TryGetWorldPosition(BoardCell cell, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            if (cell == null)
                return false;

            if (boardManager == null)
                return false;

            if (!boardManager.IsManagedCell(cell))
                return false;

            Vector3 origin = boardOrigin != null
                ? boardOrigin.position
                : transform.position;

            worldPosition = new Vector3(
                origin.x + boardOffset.x + cell.X * cellSize.x,
                origin.y + boardOffset.y + cell.Y * cellSize.y,
                origin.z);

            return true;
        }
    }
}

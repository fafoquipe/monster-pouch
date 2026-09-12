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

        public bool TryGetCell(Vector3 worldPosition, out BoardCell cell)
        {
            cell = null;
            if (boardManager == null || Mathf.Abs(cellSize.x) < 0.0001f || Mathf.Abs(cellSize.y) < 0.0001f) return false;
            Vector3 origin = boardOrigin != null ? boardOrigin.position : transform.position;
            float x = (worldPosition.x - origin.x - boardOffset.x) / cellSize.x;
            float y = (worldPosition.y - origin.y - boardOffset.y) / cellSize.y;
            if (x < -.5f || x >= BoardManager.Width - .5f || y < -.5f || y >= BoardManager.Height - .5f) return false;
            cell = boardManager.GetCell(Mathf.FloorToInt(x + .5f), Mathf.FloorToInt(y + .5f));
            return cell != null;
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

using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Per-pointer preview state only. The original buy/place call still commits on release.
    internal sealed class BriefDragSnap
    {
        Vector2 previous;
        bool fromBrief, crossedTop;

        internal void Begin(LocalGameUI ui, Vector2 screen, bool eligible = true)
        { previous = screen; fromBrief = eligible && ui.ContainsBriefPoint(screen); crossedTop = false; }

        internal Vector2 Resolve(LocalGameUI ui, Vector2 screen)
        {
            if (!fromBrief) return screen;
            if (ui.ContainsBriefPoint(screen)) crossedTop = false;
            else if (!crossedTop && ui.CrossesBriefTop(previous, screen)) crossedTop = true;
            previous = screen;
            return crossedTop && ui.TryGetBriefGapDestination(screen, out var destination) ? destination : screen;
        }

        internal bool ReturnedToBrief(LocalGameUI ui, Vector2 screen) => fromBrief && ui.ContainsBriefPoint(screen);
        internal void Cancel() { fromBrief = crossedTop = false; }
    }

    public sealed partial class LocalGameUI
    {
        internal bool ContainsBriefPoint(Vector2 screen) => Contains(briefDrop, screen);

        internal bool CrossesBriefTop(Vector2 from, Vector2 to)
        {
            if (briefDrop == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(briefDrop, from, null, out var a) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(briefDrop, to, null, out var b)) return false;
            Rect bounds = briefDrop.rect;
            if (a.y > bounds.yMax || b.y <= bounds.yMax) return false;
            float x = Mathf.Lerp(a.x, b.x, (bounds.yMax - a.y) / (b.y - a.y));
            return x >= bounds.xMin && x <= bounds.xMax;
        }

        internal bool TryGetBriefGapDestination(Vector2 screen, out Vector2 destination)
        {
            destination = screen;
            if (!BriefReady || briefDrop == null || GameCamera == null || Contains(benchDrop, screen)) return false;
            var world = GameCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -GameCamera.transform.position.z));
            // Once the finger reaches any actual board cell, ordinary targeting owns it.
            if (Match.Mapper.TryGetCell(world, out _)) return false;
            Vector2 top = RectTransformUtility.WorldToScreenPoint(null,
                briefDrop.TransformPoint(new Vector2(briefDrop.rect.center.x, briefDrop.rect.yMax)));
            if (screen.y <= top.y) return false;

            // Choose the rear row from the rendered geometry before filtering occupancy.
            // A full rear row must not silently select a forward row or an enemy cell.
            int rearRow = -1;
            float closestRow = float.PositiveInfinity;
            for (int y = 0; y < BoardManager.Height; y++)
            {
                float rowY = 0; int count = 0;
                for (int x = 0; x < BoardManager.Width; x++)
                {
                    var cell = Match.Board.GetCell(x, y);
                    if (!Match.Config.IsDeploymentLegal(Match.LocalSide, cell.Coordinates)) continue;
                    rowY += GameCamera.WorldToScreenPoint(Match.Mapper.GetWorldPosition(cell)).y; count++;
                }
                if (count == 0) continue;
                float distance = rowY / count - top.y;
                if (distance > 0 && distance < closestRow) { closestRow = distance; rearRow = y; }
            }
            if (rearRow < 0) return false;

            BoardCell target = null;
            float closestX = float.PositiveInfinity;
            float nearEdge = float.PositiveInfinity;
            Vector3 half = new Vector3(Mathf.Abs(Match.Mapper.CellSize.x), Mathf.Abs(Match.Mapper.CellSize.y), 0) * .5f;
            for (int x = 0; x < BoardManager.Width; x++)
            {
                var cell = Match.Board.GetCell(x, rearRow);
                if (!Match.Config.IsDeploymentLegal(Match.LocalSide, cell.Coordinates)) continue;
                Vector3 center = Match.Mapper.GetWorldPosition(cell);
                IncludeBriefCellCorner(center + new Vector3(-half.x, -half.y), ref nearEdge);
                IncludeBriefCellCorner(center + new Vector3(-half.x, half.y), ref nearEdge);
                IncludeBriefCellCorner(center + new Vector3(half.x, -half.y), ref nearEdge);
                IncludeBriefCellCorner(center + new Vector3(half.x, half.y), ref nearEdge);
                if (cell.IsBlocked || cell.IsOccupied || cell.IsReserved) continue;
                Vector2 point = GameCamera.WorldToScreenPoint(center);
                float distance = Mathf.Abs(point.x - screen.x);
                if (distance < closestX) { closestX = distance; target = cell; }
            }
            // The top crossing is bounded by the Brief, which may be wider than the board.
            // Nearest free X naturally clamps to a rear cell outside the board width.
            if (target == null || screen.y >= nearEdge) return false;
            destination = GameCamera.WorldToScreenPoint(Match.Mapper.GetWorldPosition(target));
            return true;
        }

        void IncludeBriefCellCorner(Vector3 corner, ref float nearEdge)
        {
            Vector2 edge = GameCamera.WorldToScreenPoint(corner);
            nearEdge = Mathf.Min(nearEdge, edge.y);
        }
    }
}

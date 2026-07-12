using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardPathfinderTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("pathfinder-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(boardObject);
        }

        private FakeBoardUnit CreateAndOccupy(
            string unitId, BoardSide side, int iqSpeed, int x, int y)
        {
            FakeBoardUnit unit = new FakeBoardUnit(unitId, side, iqSpeed);
            BoardCell cell = boardManager.GetCell(x, y);
            Assert.IsTrue(boardManager.TryOccupyCell(unit, cell));
            return unit;
        }

        private static void AssertOrthogonalPath(
            BoardCell startCell, IReadOnlyList<BoardCell> path)
        {
            BoardCell previous = startCell;

            for (int i = 0; i < path.Count; i++)
            {
                BoardCell current = path[i];
                int dx = current.X - previous.X;
                int dy = current.Y - previous.Y;
                int absDx = dx < 0 ? -dx : dx;
                int absDy = dy < 0 ? -dy : dy;

                Assert.AreEqual(1, absDx + absDy,
                    $"Step {i}: distance from ({previous.X},{previous.Y}) to ({current.X},{current.Y}) is not 1");

                Assert.AreEqual(0, absDx * absDy,
                    $"Step {i}: diagonal move from ({previous.X},{previous.Y}) to ({current.X},{current.Y})");

                previous = current;
            }
        }

        [Test]
        public void TryFindPath_StraightVerticalPath_ReturnsExpectedCells()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 2, 2);
            BoardCell startCell = boardManager.GetCell(2, 2);
            BoardCell targetCell = boardManager.GetCell(2, 5);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.AreEqual(3, path.Count);
            Assert.IsTrue(path[0].MatchesCoordinates(2, 3));
            Assert.IsTrue(path[1].MatchesCoordinates(2, 4));
            Assert.IsTrue(path[2].MatchesCoordinates(2, 5));
            AssertOrthogonalPath(startCell, path);
        }

        [Test]
        public void TryFindPath_StraightHorizontalPath_ReturnsExpectedCells()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 1, 4);
            BoardCell startCell = boardManager.GetCell(1, 4);
            BoardCell targetCell = boardManager.GetCell(4, 4);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.AreEqual(3, path.Count);
            Assert.IsTrue(path[0].MatchesCoordinates(2, 4));
            Assert.IsTrue(path[1].MatchesCoordinates(3, 4));
            Assert.IsTrue(path[2].MatchesCoordinates(4, 4));
            AssertOrthogonalPath(startCell, path);
        }

        [Test]
        public void TryFindPath_StartEqualsTarget_ReturnsTrueWithEmptyPath()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 3, 4);
            BoardCell startCell = boardManager.GetCell(3, 4);
            BoardCell targetCell = boardManager.GetCell(3, 4);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindPath_RejectsNullArguments()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 1);
            var path = new List<BoardCell>();

            Assert.IsFalse(BoardPathfinder.TryFindPath(null, unit, startCell, targetCell, path));
            Assert.AreEqual(0, path.Count);

            Assert.IsFalse(BoardPathfinder.TryFindPath(boardManager, null, startCell, targetCell, path));
            Assert.AreEqual(0, path.Count);

            Assert.IsFalse(BoardPathfinder.TryFindPath(boardManager, unit, null, targetCell, path));
            Assert.AreEqual(0, path.Count);

            Assert.IsFalse(BoardPathfinder.TryFindPath(boardManager, unit, startCell, null, path));
            Assert.AreEqual(0, path.Count);

            Assert.IsFalse(BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, null));
        }

        [Test]
        public void TryFindPath_RejectsExternalStartCell()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell externalCell = new BoardCell(2, 2, 1, BoardQuadrant.A, BoardSide.Red);
            BoardCell targetCell = boardManager.GetCell(0, 1);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, externalCell, targetCell, path);

            Assert.IsFalse(result);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindPath_RejectsExternalTargetCell()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell externalCell = new BoardCell(2, 2, 1, BoardQuadrant.A, BoardSide.Red);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, externalCell, path);

            Assert.IsFalse(result);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindPath_RejectsBlockedTarget()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 1);
            Assert.IsTrue(boardManager.TrySetCellBlocked(0, 1, true));
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsFalse(result);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindPath_RoutesAroundBlockedCell()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 2, 2);
            BoardCell startCell = boardManager.GetCell(2, 2);
            BoardCell targetCell = boardManager.GetCell(2, 5);
            Assert.IsTrue(boardManager.TrySetCellBlocked(2, 3, true));
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.IsFalse(path.Exists(c => c.MatchesCoordinates(2, 3)));
            Assert.IsTrue(path[path.Count - 1].MatchesCoordinates(2, 5));
            Assert.IsTrue(path.Count > 3);
            AssertOrthogonalPath(startCell, path);
        }

        [Test]
        public void TryFindPath_ReturnsFalseWhenWallBlocksAllRoutes()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 2, 2);
            BoardCell startCell = boardManager.GetCell(2, 2);
            BoardCell targetCell = boardManager.GetCell(2, 7);

            for (int x = 0; x < BoardManager.Width; x++)
                Assert.IsTrue(boardManager.TrySetCellBlocked(x, 5, true));

            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsFalse(result);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindPath_AvoidsOccupiedCell()
        {
            FakeBoardUnit unitA = CreateAndOccupy("unitA", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("unitB", BoardSide.Red, 1, 0, 1);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 2);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unitA, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.IsFalse(path.Exists(c => c.MatchesCoordinates(0, 1)));
            Assert.IsTrue(path[path.Count - 1].MatchesCoordinates(0, 2));
            AssertOrthogonalPath(startCell, path);
        }

        [Test]
        public void TryFindPath_RejectsOccupiedTargetByOtherUnit()
        {
            FakeBoardUnit unitA = CreateAndOccupy("unitA", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("unitB", BoardSide.Red, 1, 0, 2);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 2);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unitA, startCell, targetCell, path);

            Assert.IsFalse(result);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindPath_AvoidsReservedCellByOtherUnit()
        {
            FakeBoardUnit unitA = CreateAndOccupy("unitA", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("unitB", BoardSide.Red, 1, 5, 5);
            BoardCell reservedCell = boardManager.GetCell(0, 1);
            Assert.IsTrue(boardManager.TryReserveCell(unitB, reservedCell));
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 2);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unitA, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.IsFalse(path.Exists(c => c.MatchesCoordinates(0, 1)));
            Assert.IsTrue(path[path.Count - 1].MatchesCoordinates(0, 2));
            AssertOrthogonalPath(startCell, path);
        }

        [Test]
        public void TryFindPath_AllowsCellReservedBySameUnit()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 1);
            Assert.IsTrue(boardManager.TryReserveCell(unit, targetCell));
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.AreEqual(1, path.Count);
            Assert.AreSame(targetCell, path[0]);
        }

        [Test]
        public void TryFindPath_RejectsReservedTargetByOtherUnit()
        {
            FakeBoardUnit unitA = CreateAndOccupy("unitA", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("unitB", BoardSide.Red, 1, 5, 5);
            BoardCell targetCell = boardManager.GetCell(0, 1);
            Assert.IsTrue(boardManager.TryReserveCell(unitB, targetCell));
            BoardCell startCell = boardManager.GetCell(0, 0);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unitA, startCell, targetCell, path);

            Assert.IsFalse(result);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindPath_DoesNotUseDiagonalShortcut()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(1, 1);
            var path = new List<BoardCell>();

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsTrue(result);
            Assert.AreEqual(2, path.Count);
            AssertOrthogonalPath(startCell, path);
            Assert.IsFalse(path.Exists(c => c.MatchesCoordinates(0, 0)));
        }

        [Test]
        public void TryFindPath_IsDeterministicForSameBoardState()
        {
            FakeBoardUnit unit1 = CreateAndOccupy("unit1", BoardSide.Red, 1, 0, 0);
            BoardCell startCell1 = boardManager.GetCell(0, 0);
            BoardCell targetCell1 = boardManager.GetCell(5, 9);
            var path1 = new List<BoardCell>();

            bool result1 = BoardPathfinder.TryFindPath(boardManager, unit1, startCell1, targetCell1, path1);

            boardManager.ReleaseUnit(unit1);

            FakeBoardUnit unit2 = CreateAndOccupy("unit2", BoardSide.Red, 1, 0, 0);
            BoardCell startCell2 = boardManager.GetCell(0, 0);
            BoardCell targetCell2 = boardManager.GetCell(5, 9);
            var path2 = new List<BoardCell>();

            bool result2 = BoardPathfinder.TryFindPath(boardManager, unit2, startCell2, targetCell2, path2);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.AreEqual(path1.Count, path2.Count);

            for (int i = 0; i < path1.Count; i++)
            {
                Assert.IsTrue(path1[i].MatchesCoordinates(path2[i].X, path2[i].Y),
                    $"Path mismatch at index {i}: ({path1[i].X},{path1[i].Y}) vs ({path2[i].X},{path2[i].Y})");
            }
        }

        [Test]
        public void TryFindPath_ClearsExistingPathBeforeSearch()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell startCell = boardManager.GetCell(0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 1);
            Assert.IsTrue(boardManager.TrySetCellBlocked(0, 1, true));
            var path = new List<BoardCell> { boardManager.GetCell(3, 3) };

            bool result = BoardPathfinder.TryFindPath(boardManager, unit, startCell, targetCell, path);

            Assert.IsFalse(result);
            Assert.AreEqual(0, path.Count);
        }
    }
}

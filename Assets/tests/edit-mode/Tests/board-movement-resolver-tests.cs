using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardMovementResolverTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("movement-resolver-tests");
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

        [Test]
        public void ResolveMovement_ReturnsEmpty_WhenBoardManagerIsNull()
        {
            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(null, null)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(null, intents);

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ResolveMovement_ReturnsEmpty_WhenIntentsAreNull()
        {
            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, null);

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ResolveMovement_MovesUnitOneCellTowardTarget()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 3);
            BoardCell oldCell = boardManager.GetCell(0, 0);
            BoardCell newCell = boardManager.GetCell(0, 1);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, targetCell)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.Moved, results[0].Status);
            Assert.AreSame(newCell, results[0].NextCell);
            Assert.AreSame(newCell, unit.CurrentCell);
            Assert.IsFalse(oldCell.IsOccupied);
            Assert.IsTrue(newCell.IsOccupied);
            Assert.AreSame(unit, newCell.OccupiedBy);
            Assert.IsNull(unit.ReservedCell);
        }

        [Test]
        public void ResolveMovement_DoesNotMove_WhenAlreadyAtTarget()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 2, 2);
            BoardCell targetCell = boardManager.GetCell(2, 2);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, targetCell)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.AlreadyAtTarget, results[0].Status);
            Assert.IsNull(results[0].NextCell);
            Assert.AreSame(boardManager.GetCell(2, 2), unit.CurrentCell);
        }

        [Test]
        public void ResolveMovement_ReturnsNoPath_WhenTargetBlocked()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);
            BoardCell targetCell = boardManager.GetCell(0, 1);
            Assert.IsTrue(boardManager.TrySetCellBlocked(0, 1, true));

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, targetCell)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.NoPath, results[0].Status);
            Assert.AreSame(boardManager.GetCell(0, 0), unit.CurrentCell);
        }

        [Test]
        public void ResolveMovement_ReturnsNoPath_WhenWallBlocksRoute()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 2, 2);
            BoardCell targetCell = boardManager.GetCell(2, 7);

            for (int x = 0; x < BoardManager.Width; x++)
                Assert.IsTrue(boardManager.TrySetCellBlocked(x, 5, true));

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, targetCell)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.NoPath, results[0].Status);
            Assert.AreSame(boardManager.GetCell(2, 2), unit.CurrentCell);
        }

        [Test]
        public void ResolveMovement_UsesPathfinderToRouteAroundBlockedCell()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 2, 2);
            BoardCell targetCell = boardManager.GetCell(2, 5);
            BoardCell startCell = boardManager.GetCell(2, 2);
            Assert.IsTrue(boardManager.TrySetCellBlocked(2, 3, true));

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, targetCell)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.Moved, results[0].Status);
            Assert.IsFalse(unit.CurrentCell.MatchesCoordinates(2, 3));
            Assert.AreSame(unit, unit.CurrentCell.OccupiedBy);
            Assert.AreSame(startCell, boardManager.GetCell(2, 2));
            Assert.IsFalse(boardManager.GetCell(2, 2).IsOccupied);
        }

        [Test]
        public void ResolveMovement_ResolvesConflict_IQSpeedWins()
        {
            FakeBoardUnit unitSlow = CreateAndOccupy("slow", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitFast = CreateAndOccupy("fast", BoardSide.Red, 5, 2, 0);
            BoardCell destination = boardManager.GetCell(1, 0);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unitSlow, destination),
                new BoardMovementIntent(unitFast, destination)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(2, results.Count);

            BoardMovementResult slowResult = results[0];
            BoardMovementResult fastResult = results[1];

            Assert.AreEqual(BoardMovementStatus.Moved, fastResult.Status);
            Assert.AreEqual(BoardMovementStatus.ReservationRejected, slowResult.Status);
            Assert.AreSame(destination, unitFast.CurrentCell);
            Assert.AreSame(boardManager.GetCell(0, 0), unitSlow.CurrentCell);
            Assert.AreSame(unitFast, destination.OccupiedBy);
        }

        [Test]
        public void ResolveMovement_RejectsDuplicateUnitIntent()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, boardManager.GetCell(0, 1)),
                new BoardMovementIntent(unit, boardManager.GetCell(1, 0))
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(BoardMovementStatus.Moved, results[0].Status);
            Assert.AreEqual(BoardMovementStatus.Invalid, results[1].Status);
            Assert.AreSame(boardManager.GetCell(0, 1), unit.CurrentCell);
        }

        [Test]
        public void ResolveMovement_ReturnsInvalid_WhenUnitIsNull()
        {
            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(null, boardManager.GetCell(0, 1))
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.Invalid, results[0].Status);
        }

        [Test]
        public void ResolveMovement_ReturnsInvalid_WhenTargetIsNull()
        {
            FakeBoardUnit unit = CreateAndOccupy("unit", BoardSide.Red, 1, 0, 0);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, null)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.Invalid, results[0].Status);
        }

        [Test]
        public void ResolveMovement_ReturnsInvalid_WhenUnitHasNoCurrentCell()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, boardManager.GetCell(0, 1))
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.Invalid, results[0].Status);
        }

        [Test]
        public void ResolveMovement_ReturnsInvalid_WhenCurrentCellNotManaged()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);
            BoardCell externalCell = new BoardCell(0, 0, 1, BoardQuadrant.A, BoardSide.Red);
            unit.SetCurrentCell(externalCell);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unit, boardManager.GetCell(0, 1))
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardMovementStatus.Invalid, results[0].Status);
        }

        [Test]
        public void ResolveMovement_ReturnsResultsInInputOrder()
        {
            FakeBoardUnit unitA = CreateAndOccupy("a", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("b", BoardSide.Red, 1, 1, 1);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(null, boardManager.GetCell(0, 0)),
                new BoardMovementIntent(unitA, boardManager.GetCell(0, 2)),
                new BoardMovementIntent(unitB, boardManager.GetCell(1, 1))
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(BoardMovementStatus.Invalid, results[0].Status);
            Assert.AreEqual(BoardMovementStatus.Moved, results[1].Status);
            Assert.AreEqual(BoardMovementStatus.AlreadyAtTarget, results[2].Status);
        }

        [Test]
        public void ResolveMovement_ResolvesConflict_CellValueWins()
        {
            FakeBoardUnit unitA = CreateAndOccupy("a", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("b", BoardSide.Red, 1, 2, 0);
            BoardCell destination = boardManager.GetCell(1, 0);

            var intents = new List<BoardMovementIntent>
            {
                new BoardMovementIntent(unitA, destination),
                new BoardMovementIntent(unitB, destination)
            };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(BoardMovementStatus.Moved, results[0].Status);
            Assert.AreEqual(BoardMovementStatus.ReservationRejected, results[1].Status);
            Assert.AreSame(destination, unitA.CurrentCell);
            Assert.AreSame(boardManager.GetCell(2, 0), unitB.CurrentCell);
            Assert.AreSame(unitA, destination.OccupiedBy);
        }
    }
}

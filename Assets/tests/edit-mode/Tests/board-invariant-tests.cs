using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardInvariantTests
    {
        private GameObject go;
        private BoardManager board;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("board-invariants");
            board = go.AddComponent<BoardManager>();
            board.BuildBoard();
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(go); }

        [Test]
        public void Rebuild_ReleasesOccupancyReservationsAndUnitReferences()
        {
            var unit = new FakeBoardUnit("unit", BoardSide.Blue, 1);
            BoardCell origin = board.GetCell(2, 4), destination = board.GetCell(2, 5);
            board.TryOccupyCell(unit, origin);
            board.TryReserveCell(unit, destination);
            board.BuildBoard();
            Assert.IsNull(unit.CurrentCell);
            Assert.IsNull(unit.ReservedCell);
            Assert.IsNull(origin.OccupiedBy);
            Assert.IsNull(destination.ReservedBy);
            Assert.IsTrue(board.TryOccupyCell(unit, 2, 4));
        }

        [Test]
        public void InvalidReposition_LeavesOriginAndReservationIntact()
        {
            var unit = new FakeBoardUnit("unit", BoardSide.Blue, 1);
            var blocker = new FakeBoardUnit("blocker", BoardSide.Red, 1);
            board.TryOccupyCell(unit, 2, 4);
            board.TryOccupyCell(blocker, 3, 4);
            board.TryReserveCell(unit, 2, 5);
            Assert.IsFalse(board.TryRepositionUnit(unit, blocker.CurrentCell));
            Assert.AreSame(unit, board.GetCell(2, 4).OccupiedBy);
            Assert.AreSame(unit, board.GetCell(2, 5).ReservedBy);
        }

        [Test]
        public void SwappingOccupiedCells_IsRejectedForBothUnits()
        {
            var first = new FakeBoardUnit("first", BoardSide.Blue, 99);
            var second = new FakeBoardUnit("second", BoardSide.Red, 1);
            board.TryOccupyCell(first, 2, 4);
            board.TryOccupyCell(second, 3, 4);
            var results = BoardMovementResolver.ResolveMovement(board, new[]
            {
                new BoardMovementIntent(first, second.CurrentCell),
                new BoardMovementIntent(second, first.CurrentCell)
            });
            Assert.AreEqual(BoardMovementStatus.NoPath, results[0].Status);
            Assert.AreEqual(BoardMovementStatus.NoPath, results[1].Status);
            Assert.AreSame(first, board.GetCell(2, 4).OccupiedBy);
            Assert.AreSame(second, board.GetCell(3, 4).OccupiedBy);
            Assert.IsNull(first.ReservedCell);
            Assert.IsNull(second.ReservedCell);
        }

        [Test]
        public void DuplicateMovementCommands_DoNotBecomeOrderDependent()
        {
            for (int reversed = 0; reversed < 2; reversed++)
            {
                board.BuildBoard();
                var unit = new FakeBoardUnit("unit", BoardSide.Blue, 1);
                board.TryOccupyCell(unit, 2, 4);
                var a = new BoardMovementIntent(unit, board.GetCell(2, 5));
                var b = new BoardMovementIntent(unit, board.GetCell(3, 4));
                var results = BoardMovementResolver.ResolveMovement(board, reversed == 0 ? new[] { a, b } : new[] { b, a });
                Assert.AreEqual(BoardMovementStatus.Invalid, results[0].Status);
                Assert.AreEqual(BoardMovementStatus.Invalid, results[1].Status);
                Assert.AreSame(unit, board.GetCell(2, 4).OccupiedBy);
                Assert.IsNull(unit.ReservedCell);
            }
        }

        [Test]
        public void ThreeQuadrantCycle_AllSixPermutationsRemainUnresolved()
        {
            int[][] permutations =
            {
                new[] {0,1,2}, new[] {0,2,1}, new[] {1,0,2},
                new[] {1,2,0}, new[] {2,0,1}, new[] {2,1,0}
            };
            foreach (int[] order in permutations)
            {
                board.BuildBoard();
                var units = new[]
                {
                    new FakeBoardUnit("A", BoardSide.Blue, 1),
                    new FakeBoardUnit("D", BoardSide.Blue, 1),
                    new FakeBoardUnit("B", BoardSide.Blue, 1)
                };
                board.TryOccupyCell(units[0], 2, 4);
                board.TryOccupyCell(units[1], 3, 5);
                board.TryOccupyCell(units[2], 3, 4);
                var intents = new List<BoardReservationIntent>();
                foreach (int index in order) intents.Add(new BoardReservationIntent(units[index], board.GetCell(2, 5)));
                List<BoardReservationResult> results = BoardReservationResolver.Resolve(board, intents);
                foreach (BoardReservationResult result in results) Assert.AreEqual(BoardReservationOutcome.Unresolved, result.Outcome);
                Assert.IsNull(board.GetCell(2, 5).ReservedBy);
                foreach (FakeBoardUnit unit in units) Assert.IsNull(unit.ReservedCell);
            }
        }
    }
}

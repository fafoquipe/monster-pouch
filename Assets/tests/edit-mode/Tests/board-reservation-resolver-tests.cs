using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardReservationResolverTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("resolver-tests");
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
        public void Resolve_SingleValidIntent_ReservesDestination()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 1, 2, 4);
            BoardCell destination = boardManager.GetCell(2, 5);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardReservationOutcome.Reserved, results[0].Outcome);
            Assert.AreSame(unitA, destination.ReservedBy);
        }

        [Test]
        public void Resolve_HigherIQSpeedWins()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 2, 2, 4);
            FakeBoardUnit unitB = CreateAndOccupy("B", BoardSide.Red, 1, 3, 4);
            BoardCell destination = boardManager.GetCell(2, 5);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, destination),
                new BoardReservationIntent(unitB, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Reserved, results[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Rejected, results[1].Outcome);
            Assert.AreSame(unitA, destination.ReservedBy);
        }

        [Test]
        public void Resolve_HigherOriginValueWinsWhenIQTies()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("B", BoardSide.Red, 1, 3, 5);
            BoardCell destination = boardManager.GetCell(0, 5);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, destination),
                new BoardReservationIntent(unitB, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Reserved, results[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Rejected, results[1].Outcome);
            Assert.AreSame(unitA, destination.ReservedBy);
        }

        [Test]
        public void Resolve_UniqueDominantQuadrantWinsWhenValueAndIQTie()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Blue, 1, 2, 4);
            FakeBoardUnit unitB = CreateAndOccupy("B", BoardSide.Blue, 1, 3, 5);
            BoardCell destination = boardManager.GetCell(2, 5);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, destination),
                new BoardReservationIntent(unitB, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Reserved, results[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Rejected, results[1].Outcome);
            Assert.AreSame(unitA, destination.ReservedBy);
        }

        [Test]
        public void Resolve_DestinationTerritoryBreaksTie()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 1, 2, 0);
            FakeBoardUnit unitB = CreateAndOccupy("B", BoardSide.Blue, 1, 0, 2);
            BoardCell destination = boardManager.GetCell(0, 5);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, destination),
                new BoardReservationIntent(unitB, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Rejected, results[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Reserved, results[1].Outcome);
            Assert.AreSame(unitB, destination.ReservedBy);
        }

        [Test]
        public void Resolve_FullTieReturnsUnresolvedAndLeavesDestinationFree()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 1, 2, 0);
            FakeBoardUnit unitB = CreateAndOccupy("B", BoardSide.Red, 1, 0, 2);
            BoardCell destination = boardManager.GetCell(0, 0);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, destination),
                new BoardReservationIntent(unitB, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Unresolved, results[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Unresolved, results[1].Outcome);
            Assert.IsNull(destination.ReservedBy);
        }

        [Test]
        public void Resolve_ThreeUnitQuadrantCycleReturnsUnresolved()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Blue, 1, 2, 4);
            FakeBoardUnit unitD = CreateAndOccupy("D", BoardSide.Blue, 1, 3, 5);
            FakeBoardUnit unitB = CreateAndOccupy("B", BoardSide.Blue, 1, 3, 4);
            BoardCell destination = boardManager.GetCell(2, 5);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, destination),
                new BoardReservationIntent(unitD, destination),
                new BoardReservationIntent(unitB, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Unresolved, results[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Unresolved, results[1].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Unresolved, results[2].Outcome);
            Assert.IsNull(destination.ReservedBy);
        }

        [Test]
        public void Resolve_DuplicateUnitIntentsMarksBothInvalid()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 1, 0, 0);
            BoardCell dest1 = boardManager.GetCell(0, 1);
            BoardCell dest2 = boardManager.GetCell(1, 1);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, dest1),
                new BoardReservationIntent(unitA, dest2)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Invalid, results[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Invalid, results[1].Outcome);
            Assert.IsNull(dest1.ReservedBy);
            Assert.IsNull(dest2.ReservedBy);
        }

        [Test]
        public void Resolve_ExternalDestinationReturnsInvalid()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 1, 0, 0);
            BoardCell externalCell = new BoardCell(3, 4, 1, BoardQuadrant.B, BoardSide.Red);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitA, externalCell)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Invalid, results[0].Outcome);
        }

        [Test]
        public void Resolve_PreviouslyReservedDestinationReturnsInvalid()
        {
            FakeBoardUnit unitA = CreateAndOccupy("A", BoardSide.Red, 1, 0, 0);
            FakeBoardUnit unitB = CreateAndOccupy("B", BoardSide.Blue, 1, 5, 9);
            BoardCell destination = boardManager.GetCell(0, 1);

            Assert.IsTrue(boardManager.TryReserveCell(unitA, destination));

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unitB, destination)
            };

            var results = BoardReservationResolver.Resolve(boardManager, intents);

            Assert.AreEqual(BoardReservationOutcome.Invalid, results[0].Outcome);
            Assert.AreSame(unitA, destination.ReservedBy);
        }

        [Test]
        public void Resolve_NullIntentListReturnsEmpty()
        {
            var results = BoardReservationResolver.Resolve(boardManager, null);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Resolve_NullBoardManagerMarksIntentInvalid()
        {
            FakeBoardUnit unit = CreateAndOccupy("A", BoardSide.Red, 1, 0, 0);
            BoardCell dest = boardManager.GetCell(0, 1);

            var intents = new List<BoardReservationIntent>
            {
                new BoardReservationIntent(unit, dest)
            };

            var results = BoardReservationResolver.Resolve(null, intents);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BoardReservationOutcome.Invalid, results[0].Outcome);
        }

        [Test]
        public void Resolve_InputOrderDoesNotChangeWinner()
        {
            BoardReservationOutcome RunResolve(int firstIQ, int secondIQ)
            {
                var go = new GameObject("order-test");
                var bm = go.AddComponent<BoardManager>();
                bm.BuildBoard();

                var unitFirst = new FakeBoardUnit("first", BoardSide.Red, firstIQ);
                var unitSecond = new FakeBoardUnit("second", BoardSide.Red, secondIQ);
                BoardCell origin1 = bm.GetCell(0, 0);
                BoardCell origin2 = bm.GetCell(5, 9);
                Assert.IsTrue(bm.TryOccupyCell(unitFirst, origin1));
                Assert.IsTrue(bm.TryOccupyCell(unitSecond, origin2));

                BoardCell dest = bm.GetCell(0, 1);
                var intents = new List<BoardReservationIntent>
                {
                    new BoardReservationIntent(unitFirst, dest),
                    new BoardReservationIntent(unitSecond, dest)
                };

                var results = BoardReservationResolver.Resolve(bm, intents);
                Object.DestroyImmediate(go);
                return results[0].Outcome;
            }

            Assert.AreEqual(BoardReservationOutcome.Rejected, RunResolve(1, 2));
            Assert.AreEqual(BoardReservationOutcome.Reserved, RunResolve(2, 1));
        }
    }
}

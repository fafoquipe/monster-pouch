using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardManagerTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("board-manager-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(boardObject);
        }

        [Test]
        public void BuildBoard_CreatesSixtyCells()
        {
            Assert.AreEqual(60, boardManager.GetAllCells().Count);
        }

        [Test]
        public void GetCell_ReturnsExpectedCenterCellData()
        {
            BoardCell cell = boardManager.GetCell(3, 4);

            Assert.NotNull(cell);
            Assert.AreEqual(1, cell.Value);
            Assert.AreEqual(BoardQuadrant.B, cell.Quadrant);
            Assert.AreEqual(BoardSide.Red, cell.Side);
        }

        [Test]
        public void TryOccupyCell_AssignsUnitToFreeCell()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit-red", BoardSide.Red, 1);
            BoardCell cell = boardManager.GetCell(3, 4);

            bool result = boardManager.TryOccupyCell(unit, cell);

            Assert.IsTrue(result);
            Assert.AreSame(unit, cell.OccupiedBy);
            Assert.AreSame(cell, unit.CurrentCell);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(6, 0)]
        [TestCase(0, 10)]
        public void IsInside_ReturnsFalseForCoordinatesOutsideBoard(int x, int y)
        {
            Assert.IsFalse(boardManager.IsInside(x, y));
        }

        [Test]
        public void GetCell_ReturnsNullForCoordinatesOutsideBoard()
        {
            Assert.IsNull(boardManager.GetCell(-1, 0));
            Assert.IsNull(boardManager.GetCell(6, 0));
            Assert.IsNull(boardManager.GetCell(0, 10));
        }

        [Test]
        public void GetDefaultMonsterSpawnCell_ReturnsConfiguredDefaults()
        {
            BoardCell redSpawn = boardManager.GetDefaultMonsterSpawnCell(BoardSide.Red);
            BoardCell blueSpawn = boardManager.GetDefaultMonsterSpawnCell(BoardSide.Blue);

            Assert.NotNull(redSpawn);
            Assert.NotNull(blueSpawn);
            Assert.AreEqual(new Vector2Int(3, 4), redSpawn.Coordinates);
            Assert.AreEqual(new Vector2Int(2, 5), blueSpawn.Coordinates);
        }

        [Test]
        public void GetCellsByValueAndQuadrant_ReturnsAllRepeatedStrategicLabels()
        {
            List<BoardCell> cells = boardManager.GetCellsByValueAndQuadrant(3, BoardQuadrant.C);

            Assert.AreEqual(3, cells.Count);
            Assert.IsTrue(cells.Exists(c => c.MatchesCoordinates(0, 5)));
            Assert.IsTrue(cells.Exists(c => c.MatchesCoordinates(1, 6)));
            Assert.IsTrue(cells.Exists(c => c.MatchesCoordinates(2, 7)));
        }

        [Test]
        public void TryOccupyCell_RejectsSecondCellForAlreadyPlacedUnit()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);
            BoardCell cellA = boardManager.GetCell(0, 0);
            BoardCell cellB = boardManager.GetCell(5, 9);

            Assert.IsTrue(boardManager.TryOccupyCell(unit, cellA));
            bool secondResult = boardManager.TryOccupyCell(unit, cellB);

            Assert.IsFalse(secondResult);
            Assert.AreSame(unit, cellA.OccupiedBy);
            Assert.IsNull(cellB.OccupiedBy);
        }

        [Test]
        public void TryReserveCell_ReservesFreeDestination()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);
            BoardCell origin = boardManager.GetCell(0, 0);
            BoardCell destination = boardManager.GetCell(0, 1);

            Assert.IsTrue(boardManager.TryOccupyCell(unit, origin));
            bool result = boardManager.TryReserveCell(unit, destination);

            Assert.IsTrue(result);
            Assert.AreSame(unit, destination.ReservedBy);
            Assert.AreSame(destination, unit.ReservedCell);
        }

        [Test]
        public void TryReserveCell_RejectsOccupiedDestination()
        {
            FakeBoardUnit unitA = new FakeBoardUnit("unitA", BoardSide.Red, 1);
            FakeBoardUnit unitB = new FakeBoardUnit("unitB", BoardSide.Blue, 1);
            BoardCell originA = boardManager.GetCell(0, 0);
            BoardCell destination = boardManager.GetCell(5, 9);

            Assert.IsTrue(boardManager.TryOccupyCell(unitA, originA));
            Assert.IsTrue(boardManager.TryOccupyCell(unitB, destination));

            bool result = boardManager.TryReserveCell(unitA, destination);
            Assert.IsFalse(result);
        }

        [Test]
        public void TryReserveCell_RejectsDestinationReservedByDifferentUnit()
        {
            FakeBoardUnit unitA = new FakeBoardUnit("unitA", BoardSide.Red, 1);
            FakeBoardUnit unitB = new FakeBoardUnit("unitB", BoardSide.Blue, 1);
            BoardCell originA = boardManager.GetCell(0, 0);
            BoardCell originB = boardManager.GetCell(5, 9);
            BoardCell destination = boardManager.GetCell(0, 1);

            Assert.IsTrue(boardManager.TryOccupyCell(unitA, originA));
            Assert.IsTrue(boardManager.TryOccupyCell(unitB, originB));
            Assert.IsTrue(boardManager.TryReserveCell(unitA, destination));

            bool secondResult = boardManager.TryReserveCell(unitB, destination);

            Assert.IsFalse(secondResult);
            Assert.AreSame(unitA, destination.ReservedBy);
        }

        [Test]
        public void CancelReservation_ClearsCellAndUnitState()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);
            BoardCell origin = boardManager.GetCell(0, 0);
            BoardCell destination = boardManager.GetCell(0, 1);

            Assert.IsTrue(boardManager.TryOccupyCell(unit, origin));
            Assert.IsTrue(boardManager.TryReserveCell(unit, destination));

            bool result = boardManager.CancelReservation(unit);

            Assert.IsTrue(result);
            Assert.IsNull(destination.ReservedBy);
            Assert.IsNull(unit.ReservedCell);
        }

        [Test]
        public void ConfirmMove_TransfersOccupancyAndClearsReservation()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);
            BoardCell origin = boardManager.GetCell(0, 0);
            BoardCell destination = boardManager.GetCell(0, 1);

            Assert.IsTrue(boardManager.TryOccupyCell(unit, origin));
            Assert.IsTrue(boardManager.TryReserveCell(unit, destination));

            bool result = boardManager.ConfirmMove(unit);

            Assert.IsTrue(result);
            Assert.IsNull(origin.OccupiedBy);
            Assert.IsNull(destination.ReservedBy);
            Assert.AreSame(unit, destination.OccupiedBy);
            Assert.AreSame(destination, unit.CurrentCell);
            Assert.IsNull(unit.ReservedCell);
        }

        [Test]
        public void ReleaseUnit_ClearsOccupancyAndReservation()
        {
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);
            BoardCell origin = boardManager.GetCell(0, 0);
            BoardCell destination = boardManager.GetCell(0, 1);

            Assert.IsTrue(boardManager.TryOccupyCell(unit, origin));
            Assert.IsTrue(boardManager.TryReserveCell(unit, destination));

            bool result = boardManager.ReleaseUnit(unit);

            Assert.IsTrue(result);
            Assert.IsNull(origin.OccupiedBy);
            Assert.IsNull(destination.ReservedBy);
            Assert.IsNull(unit.CurrentCell);
            Assert.IsNull(unit.ReservedCell);
        }

        [Test]
        public void TrySetCellBlocked_BlocksFreeCellAndRejectsOccupation()
        {
            BoardCell cell = boardManager.GetCell(3, 4);
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);

            bool blockResult = boardManager.TrySetCellBlocked(3, 4, true);

            Assert.IsTrue(blockResult);
            Assert.IsTrue(cell.IsBlocked);

            bool occupyResult = boardManager.TryOccupyCell(unit, cell);
            Assert.IsFalse(occupyResult);
        }

        [Test]
        public void TrySetCellBlocked_RejectsOccupiedCell()
        {
            BoardCell cell = boardManager.GetCell(3, 4);
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);

            Assert.IsTrue(boardManager.TryOccupyCell(unit, cell));

            bool blockResult = boardManager.TrySetCellBlocked(3, 4, true);

            Assert.IsFalse(blockResult);
            Assert.IsFalse(cell.IsBlocked);
        }

        [Test]
        public void TryOccupyCell_RejectsExternalCell()
        {
            BoardCell externalCell = new BoardCell(3, 4, 1, BoardQuadrant.B, BoardSide.Red);
            FakeBoardUnit unit = new FakeBoardUnit("unit", BoardSide.Red, 1);

            bool result = boardManager.TryOccupyCell(unit, externalCell);

            Assert.IsFalse(result);
        }
    }
}

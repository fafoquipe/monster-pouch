using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class PrototypeUnitSpawnerTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private GameObject mapperObject;
        private BoardWorldMapper mapper;
        private GameObject spawnerObject;
        private PrototypeUnitSpawner spawner;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("spawner-board-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();

            mapperObject = new GameObject("spawner-mapper");
            mapper = mapperObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(boardManager, null, Vector2.one * 0.26f, Vector2.zero);

            spawnerObject = new GameObject("spawner-tests");
            spawner = spawnerObject.AddComponent<PrototypeUnitSpawner>();
            spawner.Configure(boardManager, mapper, spawnerObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(spawnerObject);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(mapperObject);
        }

        [Test]
        public void SpawnPrototypes_Creates4Units()
        {
            spawner.SpawnPrototypes();

            Assert.IsNotNull(spawner.BlueMonster);
            Assert.IsNotNull(spawner.RedMonster);
            Assert.IsNotNull(spawner.DummyWhelp);
            Assert.IsNotNull(spawner.BuguiWhelp);
        }

        [Test]
        public void SpawnPrototypes_OccupiesBlueMonsterCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(2, 8);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.BlueMonster, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_OccupiesRedMonsterCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(3, 1);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.RedMonster, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_OccupiesDummyWhelpCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(1, 7);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.DummyWhelp, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_OccupiesBuguiWhelpCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(4, 2);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.BuguiWhelp, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_PlacesUnitsUnderSpawnerRoot()
        {
            spawner.SpawnPrototypes();

            int childCount = spawnerObject.transform.childCount;
            Assert.AreEqual(4, childCount);
        }

        [Test]
        public void MoveBlueMonsterOneTickTowardTestCell_MovesBlueMonsterAndUpdatesView()
        {
            spawner.SpawnPrototypes();

            BoardCell oldCell = spawner.BlueMonster.CurrentCell;
            Assert.IsNotNull(oldCell);

            spawner.MoveBlueMonsterOneTickTowardTestCell();

            BoardCell newCell = spawner.BlueMonster.CurrentCell;
            Assert.IsNotNull(newCell);
            Assert.AreNotEqual(oldCell, newCell);
            Assert.IsFalse(oldCell.IsOccupied);
            Assert.AreEqual(spawner.BlueMonster, newCell.OccupiedBy);
        }
    }
}

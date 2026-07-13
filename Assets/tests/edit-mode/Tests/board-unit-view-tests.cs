using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardUnitViewTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private GameObject mapperObject;
        private BoardWorldMapper mapper;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("view-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();

            mapperObject = new GameObject("mapper-for-view");
            mapper = mapperObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(boardManager, null, Vector2.one * 0.26f, Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(mapperObject);
        }

        [Test]
        public void SnapToCurrentCell_MovesTransformToCellPosition()
        {
            GameObject unitObject = new GameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, mapper);

            BoardCell cell = boardManager.GetCell(2, 3);
            bool occupied = boardManager.TryOccupyCell(unit, cell);
            Assert.IsTrue(occupied);

            view.SnapToCurrentCell();

            Vector3 expected = mapper.GetWorldPosition(cell);
            Assert.AreEqual(expected, unitObject.transform.position);
        }

        [Test]
        public void SnapToCurrentCell_DoesNothing_WhenUnitIsNull()
        {
            GameObject viewObject = new GameObject("view-only");
            BoardUnitView view = viewObject.AddComponent<BoardUnitView>();

            view.Configure(null, mapper);

            Vector3 before = viewObject.transform.position;
            view.SnapToCurrentCell();
            Vector3 after = viewObject.transform.position;

            Assert.AreEqual(before, after);
        }

        [Test]
        public void SnapToCurrentCell_DoesNothing_WhenMapperIsNull()
        {
            GameObject unitObject = new GameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, null);

            BoardCell cell = boardManager.GetCell(0, 0);
            boardManager.TryOccupyCell(unit, cell);

            Vector3 before = unitObject.transform.position;
            view.SnapToCurrentCell();
            Vector3 after = unitObject.transform.position;

            Assert.AreEqual(before, after);
        }

        [Test]
        public void SnapToCurrentCell_DoesNothing_WhenCurrentCellIsNull()
        {
            GameObject unitObject = new GameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, mapper);

            Vector3 before = unitObject.transform.position;
            view.SnapToCurrentCell();
            Vector3 after = unitObject.transform.position;

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Configure_AssignsUnitAndMapper()
        {
            GameObject unitObject = new GameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, mapper);

            Assert.AreEqual(unit, view.Unit);
            Assert.AreEqual(mapper, view.WorldMapper);
        }
    }
}

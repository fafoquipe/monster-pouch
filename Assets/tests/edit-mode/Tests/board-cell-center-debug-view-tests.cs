using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardCellCenterDebugViewTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private GameObject mapperObject;
        private BoardWorldMapper mapper;
        private GameObject viewerObject;
        private BoardCellCenterDebugView viewer;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("debug-view-board-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();

            mapperObject = new GameObject("debug-view-mapper-tests");
            mapper = mapperObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(boardManager, null, Vector2.one * 0.26f, Vector2.zero);

            viewerObject = new GameObject("debug-view-tests");
            viewer = viewerObject.AddComponent<BoardCellCenterDebugView>();
            viewer.Configure(boardManager, mapper);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewerObject);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(mapperObject);
        }

        [Test]
        public void DisabledByDefault_DoesNotCreateRoot()
        {
            Assert.IsFalse(viewer.ShowMarkers);

            viewer.BuildMarkers();

            Assert.IsNull(viewerObject.transform.Find("board-cell-centers-debug"));
            Assert.AreEqual(0, viewerObject.transform.childCount);
        }

        [Test]
        public void BuildMarkers_CreatesExactly60Markers()
        {
            viewer.SetShowMarkers(true);

            Transform root = viewerObject.transform.Find("board-cell-centers-debug");
            Assert.IsNotNull(root);
            Assert.AreEqual(60, root.childCount);
        }

        [Test]
        public void BuildMarkers_CreatesExpectedRoot()
        {
            viewer.SetShowMarkers(true);

            Transform root = viewerObject.transform.Find("board-cell-centers-debug");
            Assert.IsNotNull(root);
            Assert.AreEqual("board-cell-centers-debug", root.name);
        }

        [Test]
        public void BuildMarkers_UsesMapperPositions()
        {
            viewer.SetShowMarkers(true);

            Transform root = viewerObject.transform.Find("board-cell-centers-debug");

            BoardCell cell00 = boardManager.GetCell(0, 0);
            Vector3 expected00 = mapper.GetWorldPosition(cell00);
            Transform marker00 = root.Find("cell-center-x0-y0");
            Assert.IsNotNull(marker00);
            Assert.AreEqual(expected00, marker00.position);

            BoardCell cell59 = boardManager.GetCell(5, 9);
            Vector3 expected59 = mapper.GetWorldPosition(cell59);
            Transform marker59 = root.Find("cell-center-x5-y9");
            Assert.IsNotNull(marker59);
            Assert.AreEqual(expected59, marker59.position);
        }

        [Test]
        public void BuildMarkers_CalledTwice_DoesNotDuplicateRoot()
        {
            viewer.SetShowMarkers(true);
            Transform rootFirst = viewerObject.transform.Find("board-cell-centers-debug");
            int childCountFirst = rootFirst.childCount;

            viewer.SetShowMarkers(true);
            Transform rootSecond = viewerObject.transform.Find("board-cell-centers-debug");

            Assert.IsNotNull(rootSecond);
            Assert.AreEqual(60, rootSecond.childCount);
            Assert.AreEqual(1, viewerObject.transform.childCount);
            Assert.AreEqual(60, childCountFirst);

            int rootCount = 0;
            foreach (Transform child in viewerObject.transform)
            {
                if (child.name == "board-cell-centers-debug")
                    rootCount++;
            }

            Assert.AreEqual(1, rootCount);
        }

        [Test]
        public void DisablingMarkers_RemovesOnlyDebugRoot()
        {
            GameObject preservedChild = new GameObject("preserved-child");
            preservedChild.transform.SetParent(viewerObject.transform, false);

            viewer.SetShowMarkers(true);
            viewer.SetShowMarkers(false);

            Assert.IsNull(viewerObject.transform.Find("board-cell-centers-debug"));
            Assert.IsNotNull(viewerObject.transform.Find("preserved-child"));
            Assert.AreEqual(1, viewerObject.transform.childCount);
        }
    }
}

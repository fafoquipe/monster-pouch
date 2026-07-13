using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardWorldMapperTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private GameObject mapperObject;
        private BoardWorldMapper mapper;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("board-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();

            mapperObject = new GameObject("mapper-tests");
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
        public void TryGetWorldPosition_ReturnsTrue_ForManagedCell()
        {
            BoardCell cell = boardManager.GetCell(2, 3);
            bool result = mapper.TryGetWorldPosition(cell, out Vector3 position);

            Assert.IsTrue(result);
            Assert.AreEqual(mapper.GetWorldPosition(cell), position);
        }

        [Test]
        public void TryGetWorldPosition_ReturnsFalse_ForNullCell()
        {
            bool result = mapper.TryGetWorldPosition(null, out Vector3 position);

            Assert.IsFalse(result);
            Assert.AreEqual(Vector3.zero, position);
        }

        [Test]
        public void TryGetWorldPosition_ReturnsFalse_WhenBoardManagerIsNull()
        {
            var freshMapperObject = new GameObject("fresh-mapper");
            BoardWorldMapper freshMapper = freshMapperObject.AddComponent<BoardWorldMapper>();
            BoardCell cell = boardManager.GetCell(0, 0);

            bool result = freshMapper.TryGetWorldPosition(cell, out Vector3 position);

            Assert.IsFalse(result);
            Assert.AreEqual(Vector3.zero, position);

            Object.DestroyImmediate(freshMapperObject);
        }

        [Test]
        public void GetWorldPosition_ReturnsZero_ForNullCell()
        {
            Vector3 result = mapper.GetWorldPosition(null);

            Assert.AreEqual(Vector3.zero, result);
        }

        [Test]
        public void GetWorldPosition_ReturnsSameAsTryGetWorldPosition_ForValidCell()
        {
            BoardCell cell = boardManager.GetCell(2, 3);

            Vector3 getResult = mapper.GetWorldPosition(cell);
            bool tryResult = mapper.TryGetWorldPosition(cell, out Vector3 tryPosition);

            Assert.IsTrue(tryResult);
            Assert.AreEqual(tryPosition, getResult);
        }

        [Test]
        public void TryGetWorldPosition_PositionsAreConsistentAcrossGrid()
        {
            for (int y = 0; y < BoardManager.Height; y++)
            {
                for (int x = 0; x < BoardManager.Width; x++)
                {
                    BoardCell cell = boardManager.GetCell(x, y);
                    bool result = mapper.TryGetWorldPosition(cell, out Vector3 position);

                    Assert.IsTrue(result, $"Failed for cell ({x},{y})");
                }
            }
        }
    }
}

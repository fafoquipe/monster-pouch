using MonsterPouch.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardPriorityResolverTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("priority-resolver-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(boardObject);
        }

        [Test]
        public void CompareCellValue_HigherValueWins()
        {
            BoardCell first = boardManager.GetCell(0, 0);
            BoardCell second = boardManager.GetCell(2, 4);

            BoardPriorityResult result = BoardPriorityResolver.CompareCellValue(first, second);

            Assert.AreEqual(BoardPriorityResult.First, result);
        }

        [Test]
        public void CompareCellValue_TieReturnsUnresolved()
        {
            BoardCell first = boardManager.GetCell(2, 4);
            BoardCell second = boardManager.GetCell(3, 4);

            BoardPriorityResult result = BoardPriorityResolver.CompareCellValue(first, second);

            Assert.AreEqual(BoardPriorityResult.Unresolved, result);
        }

        [TestCase(BoardQuadrant.A, BoardQuadrant.D, true)]
        [TestCase(BoardQuadrant.B, BoardQuadrant.A, true)]
        [TestCase(BoardQuadrant.B, BoardQuadrant.C, true)]
        [TestCase(BoardQuadrant.C, BoardQuadrant.A, true)]
        [TestCase(BoardQuadrant.C, BoardQuadrant.D, true)]
        [TestCase(BoardQuadrant.D, BoardQuadrant.B, true)]
        [TestCase(BoardQuadrant.A, BoardQuadrant.A, false)]
        [TestCase(BoardQuadrant.A, BoardQuadrant.B, false)]
        [TestCase(BoardQuadrant.D, BoardQuadrant.C, false)]
        public void QuadrantBeats_ReturnsExpectedHierarchy(
            BoardQuadrant attacker, BoardQuadrant defender, bool expected)
        {
            Assert.AreEqual(expected, BoardPriorityResolver.QuadrantBeats(attacker, defender));
        }

        [TestCase(BoardQuadrant.A, BoardQuadrant.D, BoardPriorityResult.First)]
        [TestCase(BoardQuadrant.D, BoardQuadrant.A, BoardPriorityResult.Second)]
        [TestCase(BoardQuadrant.B, BoardQuadrant.C, BoardPriorityResult.First)]
        [TestCase(BoardQuadrant.C, BoardQuadrant.B, BoardPriorityResult.Second)]
        [TestCase(BoardQuadrant.A, BoardQuadrant.A, BoardPriorityResult.Unresolved)]
        public void CompareQuadrants_ReturnsExpectedResults(
            BoardQuadrant first, BoardQuadrant second, BoardPriorityResult expected)
        {
            Assert.AreEqual(expected, BoardPriorityResolver.CompareQuadrants(first, second));
        }

        [Test]
        public void CompareTerritorialAdvantage_FavorsMatchingSide()
        {
            BoardPriorityResult redResult = BoardPriorityResolver.CompareTerritorialAdvantage(
                BoardSide.Red, BoardSide.Red, BoardSide.Blue);

            BoardPriorityResult blueResult = BoardPriorityResolver.CompareTerritorialAdvantage(
                BoardSide.Blue, BoardSide.Red, BoardSide.Blue);

            Assert.AreEqual(BoardPriorityResult.First, redResult);
            Assert.AreEqual(BoardPriorityResult.Second, blueResult);
        }

        [Test]
        public void CompareSharedTerritorialAdvantage_ReturnsUnresolvedAcrossDifferentTerritories()
        {
            BoardCell firstCell = boardManager.GetCell(2, 4);
            BoardCell secondCell = boardManager.GetCell(2, 5);

            BoardPriorityResult result = BoardPriorityResolver.CompareSharedTerritorialAdvantage(
                firstCell, BoardSide.Red, secondCell, BoardSide.Blue);

            Assert.AreEqual(BoardPriorityResult.Unresolved, result);
        }

        [Test]
        public void ComparePositionalPriority_UsesCellValueBeforeQuadrant()
        {
            BoardCell first = boardManager.GetCell(0, 0);
            BoardCell second = boardManager.GetCell(3, 5);

            BoardPriorityResult result = BoardPriorityResolver.ComparePositionalPriority(
                first, BoardSide.Red, second, BoardSide.Red);

            Assert.AreEqual(BoardPriorityResult.First, result);
        }

        [Test]
        public void ComparePositionalPriority_UsesQuadrantWhenValuesTie()
        {
            BoardCell first = boardManager.GetCell(2, 4);
            BoardCell second = boardManager.GetCell(3, 5);

            BoardPriorityResult result = BoardPriorityResolver.ComparePositionalPriority(
                first, BoardSide.Red, second, BoardSide.Blue);

            Assert.AreEqual(BoardPriorityResult.First, result);
        }

        [Test]
        public void CompareFinalSimultaneousDeath_UsesTerritoryAfterPreviousTies()
        {
            BoardCell first = boardManager.GetCell(5, 5);
            BoardCell second = boardManager.GetCell(4, 6);

            BoardPriorityResult result = BoardPriorityResolver.CompareFinalSimultaneousDeath(
                first, BoardSide.Red, second, BoardSide.Blue);

            Assert.AreEqual(BoardPriorityResult.Second, result);
        }

        [Test]
        public void ComparePositionalPriority_ReturnsUnresolvedForNullCell()
        {
            BoardCell valid = boardManager.GetCell(0, 0);

            BoardPriorityResult result = BoardPriorityResolver.ComparePositionalPriority(
                null, BoardSide.Red, valid, BoardSide.Blue);

            Assert.AreEqual(BoardPriorityResult.Unresolved, result);
        }
    }
}

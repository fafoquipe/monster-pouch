using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class DeploymentMaskTests
    {
        private MatchConfig config;
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            config = MatchConfig.CreateDefault();
            config.RoundIncome = new[] { 100 };
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Time.timeScale = 1;
        }

        [TestCase(BoardSide.Blue)]
        [TestCase(BoardSide.Red)]
        public void DefaultMask_CoversExactlyItsThirtyCellsWithoutNeutralRowsOrEnemyCells(BoardSide side)
        {
            Vector2Int[] mask = side == BoardSide.Blue ? config.BlueDeployment : config.RedDeployment;
            Assert.AreEqual(30, mask.Length);
            Assert.AreEqual(30, new HashSet<Vector2Int>(mask).Count);
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 6; x++)
            {
                var cell = new Vector2Int(x, y);
                bool expected = side == BoardSide.Blue ? y >= 5 : y <= 4;
                Assert.AreEqual(expected, config.IsDeploymentLegal(side, cell), side + " at " + cell);
                Assert.AreNotEqual(config.IsDeploymentLegal(BoardSide.Blue, cell),
                    config.IsDeploymentLegal(BoardSide.Red, cell), "Every board cell belongs to exactly one deployment half.");
            }
            foreach (var outside in new[] { new Vector2Int(-1, 5), new Vector2Int(6, 5),
                new Vector2Int(0, -1), new Vector2Int(0, 10) })
                Assert.IsFalse(config.IsDeploymentLegal(side, outside));
        }

        [TestCase(BoardSide.Blue, true)]
        [TestCase(BoardSide.Red, true)]
        [TestCase(BoardSide.Blue, false)]
        [TestCase(BoardSide.Red, false)]
        public void Pouch_AllThirtyDestinationsAcceptPurchaseOrPlacement_EnemyHalfCannotConsumeTheOffer(
            BoardSide side, bool buyAndPlace)
        {
            int firstRow = side == BoardSide.Blue ? 5 : 0;
            for (int y = firstRow; y < firstRow + 5; y++)
            for (int x = 0; x < 6; x++)
            {
                var cell = new Vector2Int(x, y);
                var enemy = new Vector2Int(x, 9 - y);
                var pouch = new PouchState(config, 811, "bugaloo", side, new[] { "dummy" });
                Assert.IsTrue(pouch.BeginPreparation(1));
                var monsterCell = new Vector2Int(x == 0 ? 5 : 0, side == BoardSide.Blue ? 9 : 0);
                Assert.IsTrue(pouch.TrySetLocation("bugaloo", UnitLocation.Field, monsterCell, out string reason), reason);
                PouchOffer offer = pouch.Offers[0];
                int coins = pouch.Coins, pool = pouch.PoolCount, version = pouch.OfferVersion;
                int cost = pouch.GetOfferCost(0);

                Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, UnitLocation.Field, enemy, out _));
                Assert.AreSame(offer, pouch.Offers[0]);
                Assert.AreEqual(coins, pouch.Coins);
                Assert.AreEqual(pool, pouch.PoolCount);
                Assert.AreEqual(version, pouch.OfferVersion);
                Assert.AreEqual(4, pouch.RemainingCopies("dummy"));
                Assert.AreEqual(1, pouch.Owned.Count);

                if (buyAndPlace)
                    Assert.IsTrue(pouch.TryBuyAndPlace(0, offer.Token, UnitLocation.Field, cell, out reason), side + " " + cell + ": " + reason);
                else
                {
                    Assert.IsTrue(pouch.TryBuy(0, offer.Token, out reason), reason);
                    Assert.IsFalse(pouch.TrySetLocation("dummy", UnitLocation.Field, enemy, out _));
                    Assert.AreEqual(UnitLocation.Brief, pouch.Owned["dummy"].Location);
                    Assert.IsTrue(pouch.TrySetLocation("dummy", UnitLocation.Field, cell, out reason), side + " " + cell + ": " + reason);
                }
                Assert.AreEqual(cell, pouch.Owned["dummy"].Deployment);
                Assert.AreEqual(UnitLocation.Field, pouch.Owned["dummy"].Location);
                Assert.AreEqual(1, pouch.Owned["dummy"].Copies);
                Assert.AreEqual(coins - cost, pouch.Coins);
                Assert.AreEqual(3, pouch.RemainingCopies("dummy"));
            }
        }

        [TestCase(BoardSide.Blue, FormationPreference.Front, 5)]
        [TestCase(BoardSide.Blue, FormationPreference.Middle, 7)]
        [TestCase(BoardSide.Blue, FormationPreference.Back, 9)]
        [TestCase(BoardSide.Red, FormationPreference.Front, 4)]
        [TestCase(BoardSide.Red, FormationPreference.Middle, 2)]
        [TestCase(BoardSide.Red, FormationPreference.Back, 0)]
        public void Formations_UseTheFullHalfAndPreferItsNewFrontMiddleAndBack(
            BoardSide side, FormationPreference preference, int preferredRow)
        {
            List<Vector2Int> cells = config.FormationCells(new UnitDefinition { Formation = preference }, side);
            Assert.AreEqual(30, cells.Count);
            Assert.AreEqual(30, cells.Distinct().Count());
            Assert.IsTrue(cells.Take(6).All(cell => cell.y == preferredRow));
            Assert.IsTrue(cells.All(cell => config.IsDeploymentLegal(side, cell)));
        }

        [Test]
        public void Controller_NewFrontRowSupportsBuyingMovingAndBankReturn_AndBotUsesItsFifthRow()
        {
            root = new GameObject("deployment-controller-test");
            var board = root.AddComponent<BoardManager>();
            var mapper = root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, root.transform, new Vector2(1, -1), Vector2.zero);
            var match = root.AddComponent<MatchController>();
            match.Configure(config, board, mapper);
            Assert.IsTrue(match.StartMatch("bugaloo", new[] { "dummy" }, 811));
            Assert.AreEqual(5, match.Player.Monster.Deployment.y);
            Assert.AreEqual(4, match.Bot.Monster.Deployment.y);
            Assert.AreEqual(4, match.Bot.Owned["dummy"].Deployment.y, "The bot must use the newly legal front row.");
            Assert.IsTrue(match.Place("bugaloo", board.GetCell(5, 9)));

            PouchOffer offer = match.Player.Offers[0];
            int coins = match.Player.Coins, cost = match.Player.GetOfferCost(0);
            for (int x = 0; x < 6; x++)
            {
                Assert.IsFalse(match.BuyAndPlace(0, offer.Token, board.GetCell(x, 4)));
                Assert.AreSame(offer, match.Player.Offers[0]);
                Assert.AreEqual(coins, match.Player.Coins);
            }
            Assert.IsTrue(match.BuyAndPlace(0, offer.Token, board.GetCell(0, 5)));
            OwnedUnit owned = match.Player.Owned["dummy"];
            BattleUnit actor = match.Actors[owned];
            for (int x = 0; x < 6; x++)
            {
                BoardCell destination = board.GetCell(x, 5);
                Assert.IsTrue(match.Place("dummy", destination), "New front-row column " + x);
                Assert.AreSame(actor, match.Actors[owned]);
                Assert.AreSame(actor, destination.OccupiedBy);
                Assert.AreSame(destination, actor.CurrentCell);
                Assert.AreEqual(mapper.GetWorldPosition(destination), actor.transform.position);
                Assert.IsFalse(match.Place("dummy", board.GetCell(x, 4)));
                Assert.AreSame(destination, actor.CurrentCell);
            }
            Assert.IsTrue(match.Store("dummy", true));
            Assert.IsFalse(match.Actors.ContainsKey(owned));
            Assert.IsTrue(match.Place("dummy", board.GetCell(3, 5)));
            Assert.AreEqual(new Vector2Int(3, 5), owned.Deployment);
            Assert.AreEqual(1, match.Actors.Values.Count(unit => unit.Side == BoardSide.Blue && unit.Category == UnitCategory.Whelp));
            Assert.AreEqual(coins - cost, match.Player.Coins);
            Assert.AreEqual(1, owned.Copies);
        }
    }
}

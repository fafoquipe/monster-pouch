using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class PlacementExchangeTests
    {
        GameObject root;
        MatchConfig config;
        BoardManager board;
        MatchController match;
        BoardWorldMapper mapper;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("placement-exchange-tests");
            config = MatchConfig.CreateDefault();
            config.RoundIncome = new[] { 100 };
            board = root.AddComponent<BoardManager>();
            mapper = root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, root.transform, new Vector2(.26f, -.18f), Vector2.zero);
            match = root.AddComponent<MatchController>();
            match.Configure(config, board, mapper);
            match.StartMatch("bugaloo", 16);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Time.timeScale = 1;
        }

        [Test]
        public void FieldPiecesExchangeAtomicallyKeepActorsAndPublishOldEndpointsBeforeChanged()
        {
            var first = match.Player.Monster;
            var second = BuyNew();
            Assert.IsTrue(match.Place(second.Definition.Id, FreeCell()));
            var firstActor = match.Actors[first]; var secondActor = match.Actors[second];
            var firstCell = firstActor.CurrentCell; var secondCell = secondActor.CurrentCell;
            int exchanged = 0, changed = 0;
            match.PlacementExchanged += exchange =>
            {
                exchanged++;
                Assert.AreEqual(0, changed);
                Assert.AreSame(first, exchange.First); Assert.AreSame(second, exchange.Second);
                Assert.AreEqual(UnitLocation.Field, exchange.FirstLocation);
                Assert.AreEqual(UnitLocation.Field, exchange.SecondLocation);
                Assert.AreEqual(firstCell.Coordinates, exchange.FirstCell);
                Assert.AreEqual(secondCell.Coordinates, exchange.SecondCell);
                Assert.AreSame(secondActor, firstCell.OccupiedBy);
                Assert.AreSame(firstActor, secondCell.OccupiedBy);
                Assert.AreEqual(secondCell.Coordinates, first.Deployment);
                Assert.AreEqual(firstCell.Coordinates, second.Deployment);
            };
            match.Changed += () => changed++;

            Assert.IsTrue(match.Place(first.Definition.Id, secondCell));

            Assert.AreEqual(1, exchanged); Assert.AreEqual(1, changed);
            Assert.AreSame(firstActor, match.Actors[first]); Assert.AreSame(secondActor, match.Actors[second]);
            Assert.AreEqual(mapper.GetWorldPosition(secondCell), firstActor.transform.position);
            Assert.AreEqual(mapper.GetWorldPosition(firstCell), secondActor.transform.position);
            AssertOccupancy();
        }

        [TestCase(UnitLocation.Brief)]
        [TestCase(UnitLocation.Bench)]
        public void StoredWhelpCannotExchangeWithOccupiedFieldAndKeepsItsStoragePlace(UnitLocation source)
        {
            var old = BuyNew(); Assert.IsTrue(match.Place(old.Definition.Id, FreeCell()));
            var incoming = BuyNew();
            if (source == UnitLocation.Bench) Assert.IsTrue(match.Store(incoming.Definition.Id, true));
            int oldSlot = incoming.BriefSlot;
            var cell = match.Actors[old].CurrentCell;
            int coins = match.Player.Coins;
            PlacementExchange result = null;
            match.PlacementExchanged += exchange => result = exchange;

            Assert.IsFalse(match.Player.TryExchange(incoming.Definition.Id, old.Definition.Id, out var directExchange, out _));
            Assert.IsNull(directExchange);
            Assert.IsFalse(match.Place(incoming.Definition.Id, cell));

            Assert.AreEqual(source, incoming.Location);
            Assert.AreEqual(oldSlot, incoming.BriefSlot);
            Assert.AreEqual(UnitLocation.Field, old.Location);
            Assert.IsFalse(match.Actors.ContainsKey(incoming));
            Assert.AreSame(match.Actors[old], cell.OccupiedBy);
            Assert.AreEqual(coins, match.Player.Coins);
            Assert.IsNull(result);
            AssertOccupancy();
        }

        [Test]
        public void NewPurchaseOntoOccupiedFieldRejectsWithoutBuyingOrExchanging()
        {
            var old = BuyNew(); Assert.IsTrue(match.Place(old.Definition.Id, FreeCell()));
            int slot = NewOffer(); var offer = match.Player.Offers[slot];
            int price = match.Player.GetOfferCost(slot), coins = match.Player.Coins;
            int remaining = match.Player.RemainingCopies(offer.UnitId);
            int version = match.Player.OfferVersion;
            var cell = match.Actors[old].CurrentCell;
            PlacementExchange result = null;
            match.PlacementExchanged += exchange => result = exchange;

            Assert.IsFalse(match.BuyAndPlace(slot, offer.Token, cell));

            Assert.IsNull(result);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.AreEqual(UnitLocation.Field, old.Location);
            Assert.AreSame(match.Actors[old], cell.OccupiedBy);
            Assert.AreSame(offer, match.Player.Offers[slot]);
            Assert.AreEqual(coins, match.Player.Coins);
            Assert.AreEqual(version, match.Player.OfferVersion);
            Assert.AreEqual(remaining, match.Player.RemainingCopies(offer.UnitId));
            Assert.IsTrue(match.BuyAndPlace(slot, offer.Token, FreeCell()), "A rejected purchase retains its usable offer.");
            Assert.IsNull(result, "Purchasing onto an empty tile is not a two-piece exchange.");
            Assert.AreEqual(coins - price, match.Player.Coins);
            AssertOccupancy();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EitherLockedPieceRejectsExchangeWithoutMovingOrNotifying(bool lockIncoming)
        {
            var first = match.Player.Monster;
            var second = BuyNew(); Assert.IsTrue(match.Place(second.Definition.Id, FreeCell()));
            (lockIncoming ? first : second).MarkedPositionRound = 1;
            match.Player.EndPreparation(); Assert.IsTrue(match.Player.BeginPreparation(2));
            var firstCell = first.Deployment; var secondCell = second.Deployment;
            int exchanged = 0;
            match.PlacementExchanged += _ => exchanged++;

            Assert.IsFalse(match.Place(first.Definition.Id, board.GetCell(secondCell.x, secondCell.y)));

            Assert.AreEqual(0, exchanged);
            Assert.AreEqual(firstCell, first.Deployment); Assert.AreEqual(secondCell, second.Deployment);
            AssertOccupancy();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReservationOnEitherPieceRejectsBothHalves(bool reserveIncoming)
        {
            var first = match.Player.Monster;
            var second = BuyNew(); Assert.IsTrue(match.Place(second.Definition.Id, FreeCell()));
            var firstCell = first.Deployment; var secondCell = second.Deployment;
            var reservedActor = match.Actors[reserveIncoming ? first : second];
            var reservation = FreeCell(); Assert.IsTrue(board.TryReserveCell(reservedActor, reservation));

            Assert.IsFalse(match.Place(first.Definition.Id, board.GetCell(secondCell.x, secondCell.y)));

            Assert.AreEqual(firstCell, first.Deployment); Assert.AreEqual(secondCell, second.Deployment);
            Assert.AreSame(reservedActor, reservation.ReservedBy);
            AssertOccupancy();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MonsterUpgradeLockCannotBeBypassedFromEitherDirection(bool dragMonster)
        {
            var hero = match.Player.Monster;
            var whelp = BuyNew(); Assert.IsTrue(match.Place(whelp.Definition.Id, FreeCell()));
            hero.UpgradePurchasedRound = 1;
            match.Player.EndPreparation(); Assert.IsTrue(match.Player.BeginPreparation(2));
            var heroCell = hero.Deployment; var whelpCell = whelp.Deployment;
            var incoming = dragMonster ? hero : whelp;
            var destination = dragMonster ? whelpCell : heroCell;

            Assert.IsFalse(match.Place(incoming.Definition.Id, board.GetCell(destination.x, destination.y)));

            Assert.AreEqual(heroCell, hero.Deployment); Assert.AreEqual(whelpCell, whelp.Deployment);
            AssertOccupancy();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HeroCannotBeDisplacedIntoBriefOrBench(bool bench)
        {
            var incoming = BuyNew();
            if (bench) Assert.IsTrue(match.Store(incoming.Definition.Id, true));
            int slot = incoming.BriefSlot, coins = match.Player.Coins;
            var destination = match.Actors[match.Player.Monster].CurrentCell;

            Assert.IsFalse(match.Place(incoming.Definition.Id, destination));

            Assert.AreEqual(bench ? UnitLocation.Bench : UnitLocation.Brief, incoming.Location);
            Assert.AreEqual(slot, incoming.BriefSlot); Assert.AreEqual(coins, match.Player.Coins);
            Assert.AreEqual(UnitLocation.Field, match.Player.Monster.Location);
            AssertOccupancy();
        }

        [Test]
        public void PurchaseOntoLockedWhelpLeavesCurrencyOfferCopiesAndBothLocationsUntouched()
        {
            var old = BuyNew(); Assert.IsTrue(match.Place(old.Definition.Id, FreeCell()));
            old.MarkedPositionRound = 1;
            match.Player.EndPreparation(); Assert.IsTrue(match.Player.BeginPreparation(2));
            int slot = NewOffer(); var offer = match.Player.Offers[slot];
            int coins = match.Player.Coins, version = match.Player.OfferVersion;
            int remaining = match.Player.RemainingCopies(offer.UnitId);
            var destination = match.Actors[old].CurrentCell;

            Assert.IsFalse(match.BuyAndPlace(slot, offer.Token, destination));

            Assert.AreSame(offer, match.Player.Offers[slot]);
            Assert.AreEqual(coins, match.Player.Coins); Assert.AreEqual(version, match.Player.OfferVersion);
            Assert.AreEqual(remaining, match.Player.RemainingCopies(offer.UnitId));
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.AreEqual(UnitLocation.Field, old.Location);
            AssertOccupancy();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CombatAndPauseRejectPlacementExchange(bool pause)
        {
            var incoming = BuyNew(); Assert.IsTrue(match.Place(incoming.Definition.Id, FreeCell()));
            var first = match.Player.Monster; var oldFirst = first.Deployment; var oldIncoming = incoming.Deployment;
            if (pause) match.Pause(true); else match.Ready();
            int exchanged = 0; match.PlacementExchanged += _ => exchanged++;

            Assert.IsFalse(match.Place(first.Definition.Id, board.GetCell(oldIncoming.x, oldIncoming.y)));

            Assert.AreEqual(oldFirst, first.Deployment); Assert.AreEqual(oldIncoming, incoming.Deployment);
            Assert.AreEqual(0, exchanged);
        }

        [Test]
        public void InvalidOrEnemyDestinationDoesNotMoveOwnPiece()
        {
            var first = match.Player.Monster; var source = first.Deployment;
            var enemyCell = board.GetCell(2, 2);
            var foreignCell = new BoardCell(2, 6, 1, BoardQuadrant.C, BoardSide.Blue);
            Assert.IsFalse(match.Place(first.Definition.Id, enemyCell));
            Assert.IsFalse(match.Place(first.Definition.Id, foreignCell));
            Assert.AreEqual(source, first.Deployment);
            AssertOccupancy();
        }

        [Test]
        public void FieldWhelpJoinsAnotherBenchedUnitWithoutExchangingOrLosingCopies()
        {
            var first = BuyNew(); Assert.IsTrue(match.Place(first.Definition.Id, FreeCell()));
            var second = BuyNew(); Assert.IsTrue(match.Store(second.Definition.Id, true));
            var field = first.Deployment;
            int copies = match.Player.RemainingCopies(first.Definition.Id);
            var offers = match.Player.Offers.ToArray(); int version = match.Player.OfferVersion;
            int exchanged = 0; match.PlacementExchanged += _ => exchanged++;

            Assert.IsTrue(match.Store(first.Definition.Id, true));
            Assert.IsFalse(match.Player.TryExchange(first.Definition.Id, second.Definition.Id, out _, out _));

            Assert.AreEqual(UnitLocation.Bench, first.Location); Assert.AreEqual(field, first.Deployment);
            Assert.AreEqual(UnitLocation.Bench, second.Location);
            Assert.AreEqual(copies, match.Player.RemainingCopies(first.Definition.Id));
            Assert.IsTrue(match.Player.Offers.All(o=>o==null||o.UnitId!=first.Definition.Id));
            Assert.GreaterOrEqual(match.Player.OfferVersion,version); Assert.AreEqual(0, exchanged);
            AssertOccupancy();
        }

        OwnedUnit BuyNew()
        {
            int slot = NewOffer(); var offer = match.Player.Offers[slot];
            Assert.IsTrue(match.Buy(slot, offer.Token));
            return match.Player.Owned[offer.UnitId];
        }

        int NewOffer()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                for (int slot = 0; slot < match.Player.Offers.Count; slot++)
                    if (match.Player.Offers[slot] != null && !match.Player.Owned.ContainsKey(match.Player.Offers[slot].UnitId)) return slot;
                Assert.IsTrue(match.Reroll(match.Player.OfferVersion), "Need an unowned Whelp offer for this test.");
            }
            Assert.Fail("No unowned Whelp offer."); return -1;
        }

        BoardCell FreeCell() => board.GetAllCells().First(c => c.Side == BoardSide.Blue && c.OccupiedBy == null && c.ReservedBy == null);

        void AssertOccupancy()
        {
            foreach (var owned in match.Player.Owned.Values)
            {
                if (owned.Location != UnitLocation.Field) { Assert.IsFalse(match.Actors.ContainsKey(owned)); continue; }
                var actor = match.Actors[owned];
                Assert.AreEqual(owned.Deployment, actor.CurrentCell.Coordinates);
                Assert.AreSame(actor, actor.CurrentCell.OccupiedBy);
            }
            Assert.AreEqual(match.Actors.Count, board.GetAllCells().Count(c => c.OccupiedBy != null));
        }
    }
}

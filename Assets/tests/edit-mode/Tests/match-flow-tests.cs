using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    /// <summary>Real controller, economy, board and combat integration without scene or rendering dependencies.</summary>
    public sealed class MatchFlowTests
    {
        private GameObject root;
        private MatchConfig config;
        private BoardManager board;
        private BoardWorldMapper mapper;
        private MatchController match;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("match-flow-tests");
            config = MatchConfig.CreateDefault();
            board = root.AddComponent<BoardManager>();
            board.BuildBoard();
            mapper = root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, root.transform, new Vector2(0.26f, -0.18f), new Vector2(-0.65f, 0.81f));
            match = root.AddComponent<MatchController>();
            match.Configure(config, board, mapper);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Time.timeScale = 1;
        }

        [TestCase("bugaloo", "popow")]
        [TestCase("popow", "bugaloo")]
        public void StartMatch_BothChoicesCreateExactlyOneMonsterPerSide_WithUniqueIdsAndValidOccupancy(
            string selected, string opponent)
        {
            match.StartMatch(selected, 731);

            Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            Assert.AreEqual(1, match.Round);
            Assert.AreEqual(selected, match.Player.Monster.Definition.Id);
            Assert.AreEqual(opponent, match.Bot.Monster.Definition.Id);
            Assert.AreEqual(BoardSide.Blue, match.Player.Side);
            Assert.AreEqual(BoardSide.Red, match.Bot.Side);
            Assert.AreEqual(2, match.Actors.Values.Count(u => u.Category == UnitCategory.Monster));
            Assert.AreEqual(1, match.Actors.Values.Count(u => u.Category == UnitCategory.Monster && u.Side == BoardSide.Blue));
            Assert.AreEqual(1, match.Actors.Values.Count(u => u.Category == UnitCategory.Monster && u.Side == BoardSide.Red));
            Assert.AreEqual(60, board.GetAllCells().Count);
            Assert.AreEqual(6, match.Player.Coins);
            Assert.AreEqual(4, match.Player.Rerolls);
            AssertActorInvariants();
        }

        [Test]
        public void BuyPlaceBenchAndSell_KeepEconomyRosterAndBoardInAgreement()
        {
            config.RoundIncome = new[] { 100 };
            match.StartMatch("bugaloo", 16);
            PouchOffer offer = match.Player.Offers[0];
            int price = match.Player.GetOfferCost(0);
            int balance = match.Player.Coins;
            Assert.IsTrue(match.Buy(0, offer.Token));
            Assert.IsFalse(match.Buy(0, offer.Token));
            Assert.AreEqual(balance - price, match.Player.Coins);
            OwnedUnit owned = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(UnitLocation.Brief, owned.Location);
            Assert.IsFalse(match.Actors.ContainsKey(owned));

            BoardCell placement = FreePlayerCell();
            Assert.IsTrue(match.Place(offer.UnitId, placement));
            Assert.IsTrue(match.Actors.TryGetValue(owned, out BattleUnit actor));
            Assert.AreSame(actor, placement.OccupiedBy);
            Assert.AreSame(placement, actor.CurrentCell);
            Assert.AreEqual(mapper.GetWorldPosition(placement), actor.transform.position);

            Assert.IsTrue(match.Store(offer.UnitId, true));
            Assert.AreEqual(UnitLocation.Bench, owned.Location);
            Assert.IsNull(placement.OccupiedBy);
            Assert.IsFalse(match.Actors.ContainsKey(owned));
            Assert.AreEqual(1, owned.Copies);
            Assert.IsTrue(match.Place(offer.UnitId, placement));
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            AssertActorInvariants();

            Assert.IsTrue(match.Sell(offer.UnitId));
            Assert.AreEqual(balance, match.Player.Coins);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.IsFalse(match.Actors.ContainsKey(owned));
            Assert.IsNull(placement.OccupiedBy);
            Assert.IsFalse(match.Sell(offer.UnitId));
            Assert.AreEqual(balance, match.Player.Coins);
            AssertActorInvariants();
        }

        [Test]
        public void BuyAndPlace_NewWhelpCreatesOneActor_AndSpentTokenCannotChargeAgain()
        {
            StartPurchaseMatch();
            PouchOffer offer = match.Player.Offers[0];
            int price = match.Player.GetOfferCost(0);
            int balance = match.Player.Coins;
            int pool = match.Player.PoolCount;
            int actorCount = match.Actors.Count;
            int created = 0;
            match.ActorCreated += (createdActor, createdOwned) => { if (createdActor.Side == BoardSide.Blue) created++; };
            BoardCell destination = FreePlayerCell();

            Assert.IsTrue(match.BuyAndPlace(0, offer.Token, destination));

            OwnedUnit whelp = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(UnitLocation.Field, whelp.Location);
            Assert.AreEqual(-1, whelp.BriefSlot);
            Assert.AreEqual(destination.Coordinates, whelp.Deployment);
            Assert.AreEqual(1, whelp.Copies);
            Assert.AreEqual(price, whelp.Paid);
            Assert.AreEqual(balance - price, match.Player.Coins);
            Assert.AreEqual(pool, match.Player.PoolCount);
            Assert.AreEqual(3, match.Player.RemainingCopies(offer.UnitId));
            Assert.IsNull(match.Player.Offers[0]);
            Assert.IsTrue(match.Actors.TryGetValue(whelp, out BattleUnit actor));
            Assert.AreSame(actor, destination.OccupiedBy);
            Assert.AreSame(destination, actor.CurrentCell);
            Assert.AreEqual(mapper.GetWorldPosition(destination), actor.transform.position);
            Assert.AreEqual(actorCount + 1, match.Actors.Count);
            Assert.AreEqual(1, created);

            AssertPurchaseRejectedWithoutChanges(0, offer.Token, FreePlayerCell());
            Assert.AreSame(actor, match.Actors[whelp]);
            Assert.AreEqual(1, created);
            AssertActorInvariants();
        }

        [TestCase("null")]
        [TestCase("foreign")]
        [TestCase("blocked")]
        [TestCase("reserved")]
        [TestCase("occupied")]
        public void BuyAndPlace_UnavailableCellRejectsBeforeChargingOrConsumingOffer(string unavailable)
        {
            StartPurchaseMatch();
            PouchOffer offer = match.Player.Offers[0];
            BoardCell destination = FreePlayerCell();
            BattleUnit monster = match.Actors[match.Player.Monster];
            switch (unavailable)
            {
                case "null":
                    destination = null;
                    break;
                case "foreign":
                    destination = new BoardCell(destination.X, destination.Y, destination.Value, destination.Quadrant, destination.Side);
                    break;
                case "blocked":
                    Assert.IsTrue(board.TrySetCellBlocked(destination.X, destination.Y, true));
                    break;
                case "reserved":
                    Assert.IsTrue(board.TryReserveCell(monster, destination));
                    break;
                case "occupied":
                    destination = monster.CurrentCell;
                    break;
            }

            AssertPurchaseRejectedWithoutChanges(0, offer.Token, destination);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            if (unavailable == "foreign")
            {
                Assert.IsNull(destination.OccupiedBy);
                Assert.IsNull(destination.ReservedBy);
            }
            AssertActorInvariants();
        }

        [Test]
        public void BuyAndPlace_CopyUpgradesOnOwnOccupiedCell_AndRejectsAnotherActorCell()
        {
            StartPurchaseMatch();
            PouchOffer first = match.Player.Offers[0];
            BoardCell destination = FreePlayerCell();
            Assert.IsTrue(match.BuyAndPlace(0, first.Token, destination));
            OwnedUnit whelp = match.Player.Owned[first.UnitId];
            BattleUnit actor = match.Actors[whelp];
            Assert.IsTrue(match.ChooseTrick(first.UnitId, 2));
            PouchOffer copy = match.Player.Offers[1];
            int price = match.Player.GetOfferCost(1);
            int balance = match.Player.Coins;
            int paid = whelp.Paid;
            int actorCount = match.Actors.Count;
            int created = 0;
            match.ActorCreated += (newActor, owned) => created++;

            AssertPurchaseRejectedWithoutChanges(1, copy.Token, match.Actors[match.Player.Monster].CurrentCell);
            Assert.IsTrue(match.BuyAndPlace(1, copy.Token, destination));

            Assert.AreSame(whelp, match.Player.Owned[first.UnitId]);
            Assert.AreSame(actor, match.Actors[whelp]);
            Assert.AreSame(actor, destination.OccupiedBy);
            Assert.AreSame(destination, actor.CurrentCell);
            Assert.AreEqual(UnitLocation.Field, whelp.Location);
            Assert.AreEqual(-1, whelp.BriefSlot);
            Assert.AreEqual(destination.Coordinates, whelp.Deployment);
            Assert.AreEqual(2, whelp.Copies);
            CollectionAssert.AreEqual(new[] { false, false, true }, whelp.Tricks);
            Assert.AreEqual(-1, whelp.NextTrick);
            Assert.AreEqual(paid + price, whelp.Paid);
            Assert.AreEqual(balance - price, match.Player.Coins);
            Assert.AreEqual(2, match.Player.RemainingCopies(first.UnitId));
            Assert.IsNull(match.Player.Offers[1]);
            Assert.AreEqual(actorCount, match.Actors.Count);
            Assert.AreEqual(0, created);
            AssertActorInvariants();
        }

        [Test]
        public void BuyAndPlace_BenchAcceptsNullCell_AndCopiesKeepUnitOffTheBoard()
        {
            StartPurchaseMatch();
            PouchOffer offer = match.Player.Offers[0];
            int price = match.Player.GetOfferCost(0);
            int balance = match.Player.Coins;
            int actorCount = match.Actors.Count;
            int created = 0;
            match.ActorCreated += (actor, owned) => created++;

            Assert.IsTrue(match.BuyAndPlace(0, offer.Token, null, true));

            OwnedUnit whelp = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(UnitLocation.Bench, whelp.Location);
            Assert.AreEqual(-1, whelp.BriefSlot);
            Assert.AreEqual(1, whelp.Copies);
            Assert.AreEqual(price, whelp.Paid);
            Assert.AreEqual(balance - price, match.Player.Coins);
            Assert.IsNull(match.Player.Offers[0]);
            Assert.IsFalse(match.Actors.ContainsKey(whelp));
            AssertPurchaseRejectedWithoutChanges(0, offer.Token, null, true);

            PouchOffer copy = match.Player.Offers[1];
            int copyPrice = match.Player.GetOfferCost(1);
            Assert.IsTrue(match.BuyAndPlace(1, copy.Token, null, true));
            Assert.AreSame(whelp, match.Player.Owned[offer.UnitId]);
            Assert.AreEqual(UnitLocation.Bench, whelp.Location);
            Assert.AreEqual(-1, whelp.BriefSlot);
            Assert.AreEqual(2, whelp.Copies);
            Assert.AreEqual(1, whelp.TrickCount);
            Assert.AreEqual(balance - price - copyPrice, match.Player.Coins);
            Assert.IsFalse(match.Actors.ContainsKey(whelp));
            Assert.AreEqual(actorCount, match.Actors.Count);
            Assert.AreEqual(0, created);
            AssertActorInvariants();
        }

        [Test]
        public void BuyAndPlace_BenchOccupiedByAnotherTypeRejectsWithoutBuying()
        {
            StartPurchaseMatch(false);
            PouchOffer first = match.Player.Offers[0];
            Assert.IsTrue(match.BuyAndPlace(0, first.Token, null, true));
            OwnedUnit benched = match.Player.Owned[first.UnitId];
            int otherSlot = Enumerable.Range(0, match.Player.Offers.Count)
                .Where(slot => match.Player.Offers[slot] != null && match.Player.Offers[slot].UnitId != first.UnitId)
                .DefaultIfEmpty(-1).First();
            if (otherSlot < 0)
            {
                // If all three initial offers match, consume those copies. A refill must then contain the other type.
                for (int slot = 1; slot < match.Player.Offers.Count; slot++)
                    Assert.IsTrue(match.BuyAndPlace(slot, match.Player.Offers[slot].Token, null, true));
                Assert.IsTrue(match.Reroll(match.Player.OfferVersion));
                otherSlot = Enumerable.Range(0, match.Player.Offers.Count)
                    .First(slot => match.Player.Offers[slot] != null && match.Player.Offers[slot].UnitId != first.UnitId);
            }
            PouchOffer other = match.Player.Offers[otherSlot];

            AssertPurchaseRejectedWithoutChanges(otherSlot, other.Token, null, true);

            Assert.IsFalse(match.Player.Owned.ContainsKey(other.UnitId));
            Assert.AreSame(benched, match.Player.Owned.Values.Single(unit => unit.Location == UnitLocation.Bench));
            Assert.IsFalse(match.Actors.ContainsKey(benched));
            Assert.IsTrue(match.BuyAndPlace(otherSlot, other.Token, FreePlayerCell()), "The rejected offer remains purchasable on the field.");
            Assert.AreEqual(UnitLocation.Field, match.Player.Owned[other.UnitId].Location);
            Assert.AreEqual(UnitLocation.Bench, benched.Location);
            AssertActorInvariants();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BuyAndPlace_PauseAndCombatRejectFieldAndBenchPurchases(bool pause)
        {
            StartPurchaseMatch();
            PouchOffer offer = match.Player.Offers[0];
            BoardCell destination = FreePlayerCell();
            if (pause) match.Pause(true);
            else match.Ready();

            AssertPurchaseRejectedWithoutChanges(0, offer.Token, destination);
            AssertPurchaseRejectedWithoutChanges(0, offer.Token, null, true);

            Assert.AreEqual(pause ? MatchPhase.Preparation : MatchPhase.Combat, match.Phase);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            AssertActorInvariants();
        }

        [Test]
        public void Place_BlockedAndReservedDestinationsRejectWithoutPartialRosterChanges()
        {
            match.StartMatch("bugaloo", 16);
            PouchOffer offer = match.Player.Offers[0];
            Assert.IsTrue(match.Buy(0, offer.Token));
            OwnedUnit whelp = match.Player.Owned[offer.UnitId];
            OwnedUnit monster = match.Player.Monster;
            BattleUnit monsterActor = match.Actors[monster];
            Vector2Int previousMonsterCell = monster.Deployment;
            BoardCell destination = FreePlayerCell();
            Assert.IsTrue(board.TrySetCellBlocked(destination.X, destination.Y, true));

            Assert.IsFalse(match.Place(offer.UnitId, destination));
            Assert.AreEqual(UnitLocation.Brief, whelp.Location);
            Assert.IsFalse(match.Actors.ContainsKey(whelp));
            Assert.IsFalse(match.Place(monster.Definition.Id, destination));
            Assert.AreEqual(previousMonsterCell, monster.Deployment);
            Assert.AreEqual(previousMonsterCell, monsterActor.CurrentCell.Coordinates);
            Assert.IsTrue(board.TrySetCellBlocked(destination.X, destination.Y, false));

            Assert.IsTrue(board.TryReserveCell(monsterActor, destination));
            Assert.IsFalse(match.Place(offer.UnitId, destination));
            Assert.AreEqual(UnitLocation.Brief, whelp.Location);
            Assert.IsFalse(match.Actors.ContainsKey(whelp));
            Assert.AreSame(monsterActor, destination.ReservedBy);
            Assert.IsTrue(board.CancelReservation(monsterActor));
            AssertActorInvariants();
        }

        [Test]
        public void Place_ForeignCellRejectsEvenWhenItsCoordinatesAreLegal()
        {
            match.StartMatch("popow", 16);
            PouchOffer offer = match.Player.Offers[0];
            Assert.IsTrue(match.Buy(0, offer.Token));
            BoardCell destination = FreePlayerCell();
            var foreign = new BoardCell(destination.X, destination.Y, destination.Value, destination.Quadrant, destination.Side);

            Assert.IsFalse(match.Place(offer.UnitId, foreign));
            Assert.AreEqual(UnitLocation.Brief, match.Player.Owned[offer.UnitId].Location);
            Assert.IsNull(destination.OccupiedBy);
            Assert.IsNull(foreign.OccupiedBy);
        }

        [Test]
        public void PreparationTimeout_StartsCombatOnce_AndRejectsFurtherEconomyCommands()
        {
            match.StartMatch("popow", 71);
            PouchOffer offer = match.Player.Offers[0];
            int balance = match.Player.Coins;
            match.Advance(39.9f);
            Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            match.Advance(0.11f);
            Assert.AreEqual(MatchPhase.Combat, match.Phase);
            Assert.IsFalse(match.Player.IsPreparation);
            Assert.IsFalse(match.Bot.IsPreparation);
            CombatSimulation simulation = match.Simulation;
            match.Ready();
            Assert.AreSame(simulation, match.Simulation);
            Assert.IsFalse(match.Buy(0, offer.Token));
            Assert.IsFalse(match.Reroll(match.Player.OfferVersion));
            Assert.IsFalse(match.UpgradeMonster());
            Assert.IsFalse(match.Place(match.Player.Monster.Definition.Id, FreePlayerCell()));
            Assert.AreEqual(balance, match.Player.Coins);
            match.Advance(0.1f);
            Assert.That(match.Simulation.Elapsed, Is.EqualTo(0.1f).Within(0.00001f));
            AssertActorInvariants();
        }

        [Test]
        public void Pause_FreezesPreparationAndCombatClocks_AndResumesExistingSimulation()
        {
            match.StartMatch("bugaloo", 14);
            match.Advance(12.3f);
            float preparationRemaining = match.Remaining;
            match.Pause(true);
            match.Advance(100);
            match.Ready();
            Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            Assert.AreEqual(preparationRemaining, match.Remaining);
            Assert.AreEqual(0, Time.timeScale);

            match.Pause(false);
            match.Ready();
            match.Advance(0.3f);
            CombatSimulation simulation = match.Simulation;
            float elapsed = simulation.Elapsed;
            float combatRemaining = match.Remaining;
            var cells = match.Actors.Values.ToDictionary(actor => actor, actor => actor.CurrentCell);
            var health = match.Actors.Values.ToDictionary(actor => actor, actor => actor.CurrentHealth);
            match.Pause(true);
            match.Advance(100);
            Assert.AreEqual(MatchPhase.Combat, match.Phase);
            Assert.AreEqual(elapsed, simulation.Elapsed);
            Assert.AreEqual(combatRemaining, match.Remaining);
            foreach (BattleUnit actor in cells.Keys)
            {
                Assert.AreSame(cells[actor], actor.CurrentCell);
                Assert.AreEqual(health[actor], actor.CurrentHealth);
            }
            match.Pause(false);
            match.Advance(0.1f);
            Assert.AreSame(simulation, match.Simulation);
            Assert.That(simulation.Elapsed, Is.EqualTo(elapsed + 0.1f).Within(0.00001f));
            Assert.AreEqual(1, Time.timeScale);
        }

        [TestCase("bugaloo", 20260911)]
        [TestCase("popow", 20260911)]
        public void FullBotSeries_ReachesThreeWins_AndRoundResultsDoNotAdvanceByThemselves(string selected, int seed)
        {
            match.StartMatch(selected, seed);
            // Test safety ceiling, not a rule of the game. A stalled deterministic series must fail visibly.
            for (int attempts = 0; attempts < 100 && match.Phase != MatchPhase.MatchResult; attempts++)
            {
                Assert.AreEqual(MatchPhase.Preparation, match.Phase);
                match.RunBot(match.Player);
                AssertActorInvariants();
                match.Ready();
                FinishCurrentCombat();
                Assert.IsTrue(match.Phase == MatchPhase.RoundResult || match.Phase == MatchPhase.MatchResult);
                AssertActorInvariants();
                if (match.Phase == MatchPhase.RoundResult)
                {
                    int previousRound = match.Round;
                    int totalWins = match.PlayerWins + match.BotWins;
                    match.Advance(100);
                    Assert.AreEqual(previousRound, match.Round);
                    Assert.AreEqual(totalWins, match.PlayerWins + match.BotWins);
                    match.NextRound();
                    Assert.AreEqual(previousRound + 1, match.Round);
                }
            }
            Assert.AreEqual(MatchPhase.MatchResult, match.Phase, "The deterministic series exceeded 100 rounds without reaching three wins.");
            Assert.IsTrue(match.PlayerWins == 3 || match.BotWins == 3);
            Assert.Less(Mathf.Min(match.PlayerWins, match.BotWins), 3);
            int finalRound = match.Round;
            match.NextRound();
            Assert.AreEqual(finalRound, match.Round);
            Assert.AreEqual(MatchPhase.MatchResult, match.Phase);
        }

        [Test]
        public void RepeatedDraws_AdvanceRoundsWithoutPoints_AndDoNotImposeSevenRoundLimit()
        {
            config.RoundIncome = new[] { 0 };
            foreach (UnitDefinition definition in config.Units.Where(d => d.IsMonster))
            {
                definition.Damage = 0;
                definition.AttackRange = 10;
                definition.BaseAbility = new TrickDefinition();
            }
            match.StartMatch("bugaloo", 23);
            for (int expectedRound = 1; expectedRound <= 9; expectedRound++)
            {
                Assert.AreEqual(expectedRound, match.Round);
                match.Ready();
                FinishCurrentCombat();
                Assert.AreEqual(MatchPhase.RoundResult, match.Phase);
                Assert.IsNull(match.Simulation.Winner);
                Assert.AreEqual(0, match.PlayerWins);
                Assert.AreEqual(0, match.BotWins);
                match.NextRound();
                Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            }
            Assert.AreEqual(10, match.Round);
        }

        [Test]
        public void Rematch_ResetsRosterEconomyStatsClockAndReservations_AndRejectsOldOffers()
        {
            config.RoundIncome = new[] { 100 };
            match.StartMatch("popow", 31);
            PouchState previousPlayer = match.Player;
            PouchOffer staleOffer = previousPlayer.Offers[0];
            int staleVersion = previousPlayer.OfferVersion;
            match.RunBot(previousPlayer);
            Assert.IsTrue(previousPlayer.Monster.HasMonsterUpgrade);
            Assert.Greater(previousPlayer.Owned.Count, 1);
            BattleUnit previousMonster = match.Actors[previousPlayer.Monster];
            previousMonster.ApplyDamage(10);
            BoardCell reserved = FreePlayerCell();
            Assert.IsTrue(board.TryReserveCell(previousMonster, reserved));
            List<BoardCell> previousCells = board.GetAllCells();
            match.Pause(true);

            match.Rematch();

            Assert.AreNotSame(previousPlayer, match.Player);
            Assert.AreEqual("popow", match.Player.Monster.Definition.Id);
            Assert.AreEqual(1, match.Round);
            Assert.AreEqual(0, match.PlayerWins);
            Assert.AreEqual(0, match.BotWins);
            Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            Assert.AreEqual(40, match.Remaining);
            Assert.AreEqual(100, match.Player.Coins);
            Assert.AreEqual(4, match.Player.Rerolls);
            Assert.AreEqual(1, match.Player.Owned.Count);
            Assert.IsFalse(match.Player.Monster.HasMonsterUpgrade);
            Assert.IsFalse(match.Paused);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsNull(match.Simulation);
            foreach (BoardCell cell in previousCells)
            {
                Assert.IsNull(cell.OccupiedBy);
                Assert.IsNull(cell.ReservedBy);
            }
            BattleUnit newMonster = match.Actors[match.Player.Monster];
            Assert.AreEqual(newMonster.BaseStats.MaxHealth, newMonster.CurrentHealth);
            Assert.IsFalse(match.Buy(0, staleOffer.Token));
            Assert.IsFalse(match.Reroll(staleVersion));
            Assert.AreEqual(100, match.Player.Coins);
            Assert.AreEqual(4, match.Player.Rerolls);
            AssertActorInvariants();
        }

        [Test]
        public void Menu_ReleasesUnitsAndReservations_AndCanStartOtherMonsterWithoutDuplicates()
        {
            match.StartMatch("bugaloo", 21);
            BattleUnit monster = match.Actors[match.Player.Monster];
            BoardCell reserved = FreePlayerCell();
            Assert.IsTrue(board.TryReserveCell(monster, reserved));
            List<BoardCell> oldCells = board.GetAllCells();
            match.Ready();
            match.Advance(0.1f);
            match.Menu();

            Assert.AreEqual(MatchPhase.Menu, match.Phase);
            Assert.IsNull(match.Player);
            Assert.IsNull(match.Bot);
            Assert.IsNull(match.Simulation);
            Assert.AreEqual(0, match.Actors.Count);
            Assert.AreEqual(0, match.Round);
            foreach (BoardCell cell in oldCells.Concat(board.GetAllCells()))
            {
                Assert.IsNull(cell.OccupiedBy);
                Assert.IsNull(cell.ReservedBy);
            }
            match.StartMatch("popow", 21);
            Assert.AreEqual("popow", match.Player.Monster.Definition.Id);
            Assert.AreEqual(2, match.Actors.Values.Count(u => u.Category == UnitCategory.Monster));
            AssertActorInvariants();
        }

        private void StartPurchaseMatch(bool onlyDummy = true)
        {
            config.RoundIncome = new[] { 100 };
            if (onlyDummy) config.Units = new[] { config.Get("bugaloo"), config.Get("popow"), config.Get("dummy") };
            match.StartMatch("bugaloo", 16);
        }

        private void AssertPurchaseRejectedWithoutChanges(int slot, long token, BoardCell destination, bool bench = false)
        {
            int coins = match.Player.Coins;
            int rerolls = match.Player.Rerolls;
            int version = match.Player.OfferVersion;
            int pool = match.Player.PoolCount;
            PouchOffer[] offers = match.Player.Offers.ToArray();
            var remaining = config.Units.Where(unit => !unit.IsMonster)
                .ToDictionary(unit => unit.Id, unit => match.Player.RemainingCopies(unit.Id));
            var roster = match.Player.Owned.ToDictionary(pair => pair.Key, pair => new
            {
                Unit = pair.Value, pair.Value.Copies, pair.Value.Paid, pair.Value.Location,
                pair.Value.BriefSlot, pair.Value.Deployment, pair.Value.NextTrick,
                Tricks = pair.Value.Tricks.ToArray()
            });
            var actors = match.Actors.ToDictionary(pair => pair.Key, pair => pair.Value);
            var cells = board.GetAllCells().ToDictionary(cell => cell, cell => new { cell.OccupiedBy, cell.ReservedBy });

            Assert.IsFalse(match.BuyAndPlace(slot, token, destination, bench));

            Assert.AreEqual(coins, match.Player.Coins);
            Assert.AreEqual(rerolls, match.Player.Rerolls);
            Assert.AreEqual(version, match.Player.OfferVersion);
            Assert.AreEqual(pool, match.Player.PoolCount);
            for (int i = 0; i < offers.Length; i++)
            {
                Assert.AreSame(offers[i], match.Player.Offers[i]);
                if (offers[i] != null) Assert.AreEqual(offers[i].Token, match.Player.Offers[i].Token);
            }
            foreach (var pair in remaining) Assert.AreEqual(pair.Value, match.Player.RemainingCopies(pair.Key));
            Assert.AreEqual(roster.Count, match.Player.Owned.Count);
            foreach (var pair in roster)
            {
                Assert.IsTrue(match.Player.Owned.TryGetValue(pair.Key, out OwnedUnit unit));
                Assert.AreSame(pair.Value.Unit, unit);
                Assert.AreEqual(pair.Value.Copies, unit.Copies);
                Assert.AreEqual(pair.Value.Paid, unit.Paid);
                Assert.AreEqual(pair.Value.Location, unit.Location);
                Assert.AreEqual(pair.Value.BriefSlot, unit.BriefSlot);
                Assert.AreEqual(pair.Value.Deployment, unit.Deployment);
                Assert.AreEqual(pair.Value.NextTrick, unit.NextTrick);
                CollectionAssert.AreEqual(pair.Value.Tricks, unit.Tricks);
            }
            Assert.AreEqual(actors.Count, match.Actors.Count);
            foreach (var pair in actors)
            {
                Assert.IsTrue(match.Actors.TryGetValue(pair.Key, out BattleUnit actor));
                Assert.AreSame(pair.Value, actor);
            }
            foreach (var pair in cells)
            {
                Assert.AreSame(pair.Value.OccupiedBy, pair.Key.OccupiedBy);
                Assert.AreSame(pair.Value.ReservedBy, pair.Key.ReservedBy);
            }
        }

        private BoardCell FreePlayerCell()
        {
            return config.FormationCells(match.Player.Monster.Definition, match.Player.Side)
                .Select(cell => board.GetCell(cell.x, cell.y))
                .First(cell => !cell.IsBlocked && !cell.IsOccupied && !cell.IsReserved);
        }

        private void FinishCurrentCombat()
        {
            for (int tick = 0; tick < 405 && match.Phase == MatchPhase.Combat; tick++) match.Advance(0.1f);
            Assert.AreNotEqual(MatchPhase.Combat, match.Phase, "Combat did not finish within the configured 40 seconds.");
        }

        private void AssertActorInvariants()
        {
            var ids = new HashSet<string>();
            var occupied = new HashSet<BoardCell>();
            foreach (KeyValuePair<OwnedUnit, BattleUnit> pair in match.Actors)
            {
                BattleUnit actor = pair.Value;
                Assert.IsNotNull(actor);
                Assert.IsFalse(string.IsNullOrWhiteSpace(actor.UnitId));
                Assert.IsTrue(ids.Add(actor.UnitId), "A unit ID appeared twice: " + actor.UnitId);
                BoardSide expectedSide = match.Player.Owned.Values.Contains(pair.Key) ? BoardSide.Blue : BoardSide.Red;
                Assert.AreEqual(expectedSide, actor.Side);
                Assert.AreEqual(UnitLocation.Field, pair.Key.Location);
                if (!actor.IsAlive)
                {
                    Assert.IsNull(actor.CurrentCell);
                    Assert.IsNull(actor.ReservedCell);
                    continue;
                }
                Assert.IsNotNull(actor.CurrentCell);
                Assert.IsTrue(occupied.Add(actor.CurrentCell), "Two actors occupy the same cell.");
                Assert.AreSame(actor, actor.CurrentCell.OccupiedBy);
            }
            Assert.AreEqual(occupied.Count, board.GetAllCells().Count(cell => cell.IsOccupied));
        }
    }

    public sealed class PointerGestureFlowTests
    {
        [Test]
        public void Tap_WithSmallMovement_CommitsOnlyOnce()
        {
            var gesture = new PointerGesture();
            gesture.Begin(new Vector2(100, 100), 0);
            Assert.AreEqual(GestureResult.None, gesture.Move(new Vector2(104, 103), 0.2f));
            Assert.AreEqual(GestureResult.Tap, gesture.End());
            Assert.AreEqual(GestureResult.None, gesture.End());
            Assert.AreEqual(GestureResult.None, gesture.Move(new Vector2(100, 100), 1));
        }

        [Test]
        public void Hold_DoesNotBecomeDragOrTap_WhenPointerMovesOrReleasesAfterInspection()
        {
            var gesture = new PointerGesture();
            gesture.Begin(Vector2.zero, 3);
            Assert.AreEqual(GestureResult.Hold, gesture.Move(new Vector2(1, 2), 3.51f));
            Assert.AreEqual(GestureResult.None, gesture.Move(new Vector2(100, 100), 4));
            Assert.AreEqual(GestureResult.None, gesture.Move(Vector2.zero, 5));
            Assert.AreEqual(GestureResult.None, gesture.End());
            Assert.AreEqual(GestureResult.None, gesture.End());
        }

        [Test]
        public void Drag_RemainsDragAfterReturningToOrigin_AndNeverOpensInspection()
        {
            var gesture = new PointerGesture();
            gesture.Begin(Vector2.zero, 0);
            Assert.AreEqual(GestureResult.None, gesture.Move(new Vector2(13, 0), 0.1f));
            Assert.AreEqual(GestureResult.None, gesture.Move(Vector2.zero, 1));
            Assert.AreEqual(GestureResult.Drag, gesture.End());
            Assert.AreEqual(GestureResult.None, gesture.End());
        }

        [Test]
        public void Cancel_OnPhaseChangeOrLostPointer_DropsPendingGestureAndAllowsFreshTap()
        {
            var gesture = new PointerGesture();
            gesture.Begin(Vector2.zero, 0);
            gesture.Move(new Vector2(40, 0), 0.2f);
            gesture.Cancel();
            Assert.AreEqual(GestureResult.None, gesture.End());
            Assert.AreEqual(GestureResult.None, gesture.Move(Vector2.zero, 1));
            gesture.Begin(Vector2.zero, 2);
            Assert.AreEqual(GestureResult.Tap, gesture.End());
            gesture.Begin(Vector2.zero, 3);
            Assert.AreEqual(GestureResult.Hold, gesture.Move(Vector2.zero, 3.6f));
            gesture.Cancel();
            Assert.AreEqual(GestureResult.None, gesture.End());
            gesture.Begin(Vector2.zero, 4);
            Assert.AreEqual(GestureResult.Tap, gesture.End());
        }
    }

    public sealed class BoardMapperInputFlowTests
    {
        private GameObject root;
        private BoardManager board;
        private BoardWorldMapper mapper;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("mapper-input-flow-tests");
            root.transform.position = new Vector3(7.1f, -2.8f, 3);
            board = root.AddComponent<BoardManager>();
            board.BuildBoard();
            mapper = root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, root.transform, new Vector2(0.26f, -0.18f), new Vector2(-0.65f, 0.81f));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void AllSixtyCells_RoundTripThroughVisibleCentersAndInteriorCorners_WithInvertedY()
        {
            foreach (BoardCell cell in board.GetAllCells())
            {
                Vector3 center = mapper.GetWorldPosition(cell);
                Assert.IsTrue(mapper.TryGetCell(center, out BoardCell selected));
                Assert.AreSame(cell, selected);
                foreach (float x in new[] { -0.49f, 0.49f })
                    foreach (float y in new[] { -0.49f, 0.49f })
                    {
                        Vector3 inside = center + new Vector3(mapper.CellSize.x * x, mapper.CellSize.y * y, 0);
                        Assert.IsTrue(mapper.TryGetCell(inside, out selected));
                        Assert.AreSame(cell, selected, "Wrong cell selected near a visible interior corner.");
                    }
            }
            Assert.Greater(mapper.GetWorldPosition(board.GetCell(0, 0)).y, mapper.GetWorldPosition(board.GetCell(0, 9)).y);
        }

        [Test]
        public void OutsideVisibleBounds_IsRejected_OnAllFourEdges()
        {
            Vector3 first = mapper.GetWorldPosition(board.GetCell(0, 0));
            Vector3 last = mapper.GetWorldPosition(board.GetCell(5, 9));
            foreach (Vector3 outside in new[]
            {
                first + new Vector3(-0.51f * mapper.CellSize.x, 0, 0),
                first + new Vector3(0, -0.51f * mapper.CellSize.y, 0),
                last + new Vector3(0.51f * mapper.CellSize.x, 0, 0),
                last + new Vector3(0, 0.51f * mapper.CellSize.y, 0)
            })
            {
                Assert.IsFalse(mapper.TryGetCell(outside, out BoardCell selected));
                Assert.IsNull(selected);
            }
        }

        [Test]
        public void CrossingVisualCellBoundary_SelectsNeighborAlongTheCorrectLogicalAxis()
        {
            Vector3 center = mapper.GetWorldPosition(board.GetCell(2, 4));
            Assert.IsTrue(mapper.TryGetCell(center + new Vector3(mapper.CellSize.x * 0.51f, 0, 0), out BoardCell right));
            Assert.AreSame(board.GetCell(3, 4), right);
            Assert.IsTrue(mapper.TryGetCell(center + new Vector3(0, mapper.CellSize.y * 0.51f, 0), out BoardCell below));
            Assert.AreSame(board.GetCell(2, 5), below);
            mapper.Configure(board, root.transform, new Vector2(0, -0.18f), Vector2.zero);
            Assert.IsFalse(mapper.TryGetCell(center, out _), "A degenerate grid must not divide by zero.");
        }
    }
}

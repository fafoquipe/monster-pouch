using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TeamSelectionTests
    {
        private MatchConfig config;
        private GameObject scene;

        [SetUp]
        public void SetUp()
        {
            config = MatchConfig.CreateDefault();
            config.RoundIncome = new[] { 100 };
        }

        [TearDown]
        public void TearDown()
        {
            if (scene != null) Object.DestroyImmediate(scene);
            Object.DestroyImmediate(config);
            Time.timeScale = 1;
        }

        [Test]
        public void SelectedWhelp_IsTheOnlySourceAcrossPurchasesSalesRerollsAndRounds()
        {
            var pouch = new PouchState(config, 62, "bugaloo", BoardSide.Blue, new[] { "bugui" });
            Assert.IsTrue(pouch.BeginPreparation(1));
            AssertOnlySelectedOffers(pouch, "bugui");
            Assert.AreEqual(0, pouch.RemainingCopies("dummy"));
            Assert.AreEqual(4, pouch.RemainingCopies("bugui"));
            for (int pass = 0; pass < 2; pass++)
            {
                for (int slot = 0; slot < pouch.Offers.Count; slot++)
                {
                    PouchOffer offer = pouch.Offers[slot];
                    if (offer == null) continue;
                    Assert.IsTrue(pouch.TryBuyAndPlace(slot, offer.Token, UnitLocation.Bench, Vector2Int.zero, out string reason), reason);
                }
                if (pouch.RemainingCopies("bugui") > 0)
                    Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
            }
            Assert.AreEqual(4, pouch.Owned["bugui"].Copies);
            Assert.AreEqual(3, pouch.Owned["bugui"].TrickCount);
            Assert.AreEqual(0, pouch.RemainingCopies("bugui"));
            Assert.IsFalse(pouch.Owned.ContainsKey("dummy"));
            Assert.IsTrue(pouch.TrySell("bugui", out _));
            Assert.AreEqual(100, pouch.Coins);
            Assert.AreEqual(4, pouch.RemainingCopies("bugui"));
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
            AssertOnlySelectedOffers(pouch, "bugui");
            pouch.EndPreparation();
            Assert.IsTrue(pouch.BeginPreparation(2));
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
            AssertOnlySelectedOffers(pouch, "bugui");
            Assert.AreEqual(0, pouch.RemainingCopies("dummy"));
        }

        [Test]
        public void Selection_DeduplicatesWithoutExtraCopies_AndDetachesFromCallerList()
        {
            var requested = new List<string> { "bugui", "dummy", "bugui", "dummy" };
            var first = new PouchState(config, 14, "bugaloo", BoardSide.Blue, requested);
            var second = new PouchState(config, 14, "bugaloo", BoardSide.Blue, new[] { "dummy", "bugui" });
            requested.Clear();
            requested.Add("unknown");
            CollectionAssert.AreEqual(new[] { "dummy", "bugui" }, first.SelectedWhelpIds);
            Assert.AreEqual(4, first.RemainingCopies("dummy"));
            Assert.AreEqual(4, first.RemainingCopies("bugui"));
            Assert.AreEqual(8, first.PoolCount);
            Assert.IsTrue(first.BeginPreparation(1));
            Assert.IsTrue(second.BeginPreparation(1));
            for (int slot = 0; slot < first.Offers.Count; slot++)
            {
                Assert.AreEqual(second.Offers[slot].UnitId, first.Offers[slot].UnitId,
                    "The same selected set and seed should not depend on selection click order.");
                Assert.AreNotEqual(second.Offers[slot].Token, first.Offers[slot].Token);
            }
        }

        [TestCase("empty")]
        [TestCase("unknown")]
        [TestCase("monster")]
        [TestCase("null-entry")]
        [TestCase("too-many")]
        public void InvalidSelection_IsRejectedWithoutReturningAPartialTeam(string invalid)
        {
            string[] requested;
            switch (invalid)
            {
                case "empty": requested = Array.Empty<string>(); break;
                case "unknown": requested = new[] { "dummy", "unknown" }; break;
                case "monster": requested = new[] { "bugaloo" }; break;
                case "null-entry": requested = new[] { "bugui", null }; break;
                default:
                    config.TeamWhelpLimit = 1;
                    requested = new[] { "dummy", "bugui" };
                    break;
            }
            Assert.IsFalse(config.TryResolveWhelpSelection(requested, out string[] ids, out string reason));
            Assert.IsEmpty(ids);
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason));
            Assert.Throws<ArgumentException>(() => new PouchState(config, 6, "bugaloo", BoardSide.Blue, requested));
        }

        [Test]
        public void RepeatedIds_AreOneChoiceEvenWhenTeamLimitIsOne()
        {
            config.TeamWhelpLimit = 1;
            Assert.IsTrue(config.TryResolveWhelpSelection(new[] { "bugui", "bugui", "bugui" }, out string[] ids, out string reason), reason);
            CollectionAssert.AreEqual(new[] { "bugui" }, ids);
            var pouch = new PouchState(config, 6, "popow", BoardSide.Red, ids);
            Assert.AreEqual(4, pouch.PoolCount);
            Assert.AreEqual(0, pouch.RemainingCopies("dummy"));
        }

        [Test]
        public void LargeCollection_AllowsChoosingSevenIncludingLaterEntries_ButRejectsEight()
        {
            var units = new List<UnitDefinition> { config.Get("bugaloo"), config.Get("popow") };
            for (int i = 0; i < 9; i++)
                units.Add(new UnitDefinition
                {
                    Id = "candidate-" + i,
                    Tricks = new[] { new TrickDefinition(), new TrickDefinition(), new TrickDefinition() }
                });
            config.Units = units.ToArray();
            string[] selected = Enumerable.Range(2, 7).Select(i => "candidate-" + i).ToArray();
            var pouch = new PouchState(config, 12, "bugaloo", BoardSide.Blue, selected);
            CollectionAssert.AreEqual(selected, pouch.SelectedWhelpIds);
            Assert.AreEqual(28, pouch.PoolCount);
            Assert.AreEqual(0, pouch.RemainingCopies("candidate-0"));
            Assert.AreEqual(4, pouch.RemainingCopies("candidate-8"));
            Assert.IsFalse(config.TryResolveWhelpSelection(Enumerable.Range(0, 8).Select(i => "candidate-" + i), out _, out _));
            Assert.IsTrue(config.TryResolveWhelpSelection(null, out string[] defaults, out _));
            CollectionAssert.AreEqual(Enumerable.Range(0, 7).Select(i => "candidate-" + i).ToArray(), defaults);
        }

        [Test]
        public void Match_SelectionAndRematchKeepTheTeam_WhileLegacyStartUsesDefaults()
        {
            MatchController match = CreateMatch();
            var selected = new List<string> { "bugui", "bugui" };
            Assert.IsTrue(match.StartMatch("popow", selected, 67));
            PouchState oldPlayer = match.Player;
            PouchOffer stale = oldPlayer.Offers[0];
            selected.Clear();
            selected.Add("dummy");
            AssertOnlySelectedOffers(match.Player, "bugui");
            CollectionAssert.AreEqual(new[] { "bugui" }, match.Bot.SelectedWhelpIds);
            Assert.AreEqual("bugaloo", match.Bot.Monster.Definition.Id);
            Assert.AreEqual(0, match.Bot.RemainingCopies("dummy"));
            Assert.IsTrue(match.Bot.Owned.Values.All(unit => unit.Definition.IsMonster || unit.Definition.Id == "bugui"));
            Assert.IsTrue(match.BuyAndPlace(0, stale.Token, null, true));
            match.Rematch();
            Assert.AreNotSame(oldPlayer, match.Player);
            Assert.AreEqual("popow", match.Player.Monster.Definition.Id);
            Assert.AreEqual(1, match.Round);
            Assert.AreEqual(100, match.Player.Coins);
            Assert.AreEqual(1, match.Player.Owned.Count);
            Assert.AreEqual(4, match.Player.RemainingCopies("bugui"));
            AssertOnlySelectedOffers(match.Player, "bugui");
            Assert.IsFalse(match.BuyAndPlace(0, stale.Token, null, true));
            match.StartMatch("bugaloo", 67);
            CollectionAssert.AreEqual(new[] { "dummy", "bugui" }, match.Player.SelectedWhelpIds);
            Assert.AreEqual(4, match.Player.RemainingCopies("dummy"));
            Assert.AreEqual(4, match.Player.RemainingCopies("bugui"));
        }

        [Test]
        public void InvalidTeam_DoesNotDestroyOrStopTheCurrentCombat()
        {
            MatchController match = CreateMatch();
            Assert.IsTrue(match.StartMatch("bugaloo", new[] { "dummy" }, 21));
            match.Ready();
            match.Advance(0.1f);
            PouchState player = match.Player;
            PouchState bot = match.Bot;
            CombatSimulation combat = match.Simulation;
            float elapsed = combat.Elapsed;
            float remaining = match.Remaining;
            var actors = match.Actors.ToDictionary(pair => pair.Key, pair => pair.Value);
            var cells = match.Board.GetAllCells().Select(cell => cell.OccupiedBy).ToArray();
            Assert.IsFalse(match.StartMatch("popow", Array.Empty<string>(), 22));
            Assert.IsFalse(match.StartMatch("popow", new[] { "unknown" }, 22));
            Assert.AreSame(player, match.Player);
            Assert.AreSame(bot, match.Bot);
            Assert.AreSame(combat, match.Simulation);
            Assert.AreEqual(MatchPhase.Combat, match.Phase);
            Assert.AreEqual(remaining, match.Remaining);
            Assert.AreEqual(actors.Count, match.Actors.Count);
            foreach (var pair in actors) Assert.AreSame(pair.Value, match.Actors[pair.Key]);
            CollectionAssert.AreEqual(cells, match.Board.GetAllCells().Select(cell => cell.OccupiedBy).ToArray());
            match.Advance(0.1f);
            Assert.Greater(combat.Elapsed, elapsed, "A rejected menu choice stopped the running simulation.");
        }

        private MatchController CreateMatch()
        {
            scene = new GameObject("team-selection-tests");
            var board = scene.AddComponent<BoardManager>();
            board.BuildBoard();
            var mapper = scene.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, scene.transform, new Vector2(0.26f, -0.18f), new Vector2(-0.65f, 0.81f));
            var match = scene.AddComponent<MatchController>();
            match.Configure(config, board, mapper);
            return match;
        }

        private static void AssertOnlySelectedOffers(PouchState pouch, string id)
        {
            CollectionAssert.AreEqual(new[] { id }, pouch.SelectedWhelpIds);
            Assert.IsTrue(pouch.Offers.Any(offer => offer != null));
            foreach (PouchOffer offer in pouch.Offers)
                if (offer != null) Assert.AreEqual(id, offer.UnitId);
        }
    }
}

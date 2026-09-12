using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class PouchStateTests
    {
        private MatchConfig config;

        [SetUp]
        public void SetUp()
        {
            config = MatchConfig.CreateDefault();
            config.RoundIncome = new[] { 100, 4, 3, 2, 1 };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        [Test]
        public void Buy_IsAtomic_StoredBriefSlotStaysReservedUntilDeployment()
        {
            PouchState pouch = CreatePouch();
            PouchOffer offer = pouch.Offers[0];
            PouchOffer preserved = pouch.Offers[1];
            int price = pouch.GetOfferCost(0);

            Assert.IsTrue(pouch.TryBuy(0, offer.Token, out string reason), reason);
            Assert.IsNull(pouch.Offers[0]);
            Assert.AreSame(preserved, pouch.Offers[1]);
            Assert.AreEqual(100 - price, pouch.Coins);
            Assert.AreEqual(1, pouch.Owned[offer.UnitId].Copies);
            Assert.AreEqual(0, pouch.Owned[offer.UnitId].BriefSlot);
            Assert.IsFalse(pouch.TryBuy(0, offer.Token, out _));
            Assert.AreEqual(100 - price, pouch.Coins);
            Assert.AreEqual(1, pouch.Owned[offer.UnitId].Copies);

            pouch.EndPreparation();
            Assert.IsTrue(pouch.BeginPreparation(2));
            Assert.IsNull(pouch.Offers[0], "A round refill must not hide a paid Whelp stored in the Brief.");
            Assert.AreEqual(0, pouch.Owned[offer.UnitId].BriefSlot);
            Assert.AreSame(preserved, pouch.Offers[1]);
            Assert.AreEqual(104 - price, pouch.Coins);
            Assert.AreEqual(8, pouch.Rerolls);
            Assert.IsTrue(pouch.TrySetLocation(offer.UnitId, UnitLocation.Bench, Vector2Int.zero, out reason), reason);
            Assert.AreEqual(-1, pouch.Owned[offer.UnitId].BriefSlot);
            Assert.IsNull(pouch.Offers[0], "Deployment does not refill the vacated offer slot immediately.");
            pouch.EndPreparation();
            Assert.IsTrue(pouch.BeginPreparation(3));
            Assert.IsNotNull(pouch.Offers[0]);
            Assert.AreNotEqual(offer.Token, pouch.Offers[0].Token);
        }

        [Test]
        public void Reroll_ConservesFiniteCopies_AndRejectsDuplicateVersion()
        {
            PouchState pouch = CreatePouch();
            var oldTokens = new HashSet<long>();
            foreach (PouchOffer offer in pouch.Offers) oldTokens.Add(offer.Token);
            int version = pouch.OfferVersion;
            Assert.IsTrue(pouch.TryReroll(version, out string reason), reason);
            Assert.AreEqual(3, pouch.Rerolls);
            Assert.AreEqual(4, pouch.RemainingCopies("dummy"));
            Assert.AreEqual(4, pouch.RemainingCopies("bugui"));
            foreach (PouchOffer offer in pouch.Offers) Assert.IsFalse(oldTokens.Contains(offer.Token));
            PouchOffer current = pouch.Offers[0];
            Assert.IsFalse(pouch.TryReroll(version, out _));
            Assert.AreEqual(3, pouch.Rerolls);
            Assert.AreSame(current, pouch.Offers[0]);
            Assert.AreEqual(100, pouch.Coins);
        }

        [TestCase(UnitLocation.Field)]
        [TestCase(UnitLocation.Bench)]
        public void BuyAndPlace_NewWhelpCommitsMoneyCopyAndDestinationOnce(UnitLocation destination)
        {
            UseOnlyDummy();
            PouchState pouch = CreatePouch();
            PouchOffer offer = pouch.Offers[0];
            var cell = new Vector2Int(1, 7);
            int price = pouch.GetOfferCost(0);
            Assert.IsTrue(pouch.TryBuyAndPlace(0, offer.Token, destination, cell, out string reason), reason);
            OwnedUnit unit = pouch.Owned[offer.UnitId];
            Assert.AreEqual(destination, unit.Location);
            Assert.AreEqual(-1, unit.BriefSlot);
            if (destination == UnitLocation.Field) Assert.AreEqual(cell, unit.Deployment);
            Assert.AreEqual(1, unit.Copies);
            Assert.AreEqual(price, unit.Paid);
            Assert.AreEqual(100 - price, pouch.Coins);
            Assert.AreEqual(3, pouch.RemainingCopies(offer.UnitId));
            Assert.IsNull(pouch.Offers[0]);
            int version = pouch.OfferVersion;
            Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, destination, new Vector2Int(5, 7), out _));
            Assert.AreEqual(version, pouch.OfferVersion);
            Assert.AreEqual(100 - price, pouch.Coins);
            Assert.AreEqual(1, unit.Copies);
            Assert.AreEqual(destination, unit.Location);
            if (destination == UnitLocation.Field) Assert.AreEqual(cell, unit.Deployment);
        }

        [Test]
        public void BuyAndPlace_InvalidDestinationStaleTokenAndInsufficientFundsLeavePurchaseUntouched()
        {
            UseOnlyDummy();
            PouchState pouch = CreatePouch();
            PouchOffer offer = pouch.Offers[0];
            int version = pouch.OfferVersion;
            int pool = pouch.PoolCount;
            Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, UnitLocation.Field, pouch.Monster.Deployment, out _));
            Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, UnitLocation.Field, new Vector2Int(1, 4), out _));
            Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, UnitLocation.Brief, Vector2Int.zero, out _));
            Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, (UnitLocation)99, Vector2Int.zero, out _));
            Assert.IsFalse(pouch.TryBuyAndPlace(0, -1, UnitLocation.Bench, Vector2Int.zero, out _));
            config.Get("dummy").BaseCost = 101;
            Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, UnitLocation.Field, new Vector2Int(1, 7), out _));
            Assert.IsFalse(pouch.TryBuyAndPlace(0, offer.Token, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreSame(offer, pouch.Offers[0]);
            Assert.AreEqual(version, pouch.OfferVersion);
            Assert.AreEqual(pool, pouch.PoolCount);
            Assert.AreEqual(100, pouch.Coins);
            Assert.AreEqual(4, pouch.Rerolls);
            Assert.AreEqual(4, pouch.RemainingCopies("dummy"));
            Assert.AreEqual(1, pouch.Owned.Count);
        }

        [TestCase(UnitLocation.Brief)]
        [TestCase(UnitLocation.Bench)]
        [TestCase(UnitLocation.Field)]
        public void BuyAndPlace_RepeatCopyKeepsExistingLocationAndBriefSlot(UnitLocation originalLocation)
        {
            UseOnlyDummy();
            PouchState pouch = CreatePouch();
            Buy(pouch, 0);
            OwnedUnit unit = pouch.Owned["dummy"];
            if (originalLocation != UnitLocation.Brief)
                Assert.IsTrue(pouch.TrySetLocation("dummy", originalLocation, new Vector2Int(1, 7), out _));
            Assert.IsTrue(pouch.TrySelectTrick("dummy", 2, out _));
            Vector2Int originalCell = unit.Deployment;
            int originalSlot = unit.BriefSlot;
            int coins = pouch.Coins;
            int paid = unit.Paid;
            int price = pouch.GetOfferCost(1);
            PouchOffer offer = pouch.Offers[1];
            Assert.IsFalse(pouch.TryBuyAndPlace(1, offer.Token, UnitLocation.Field, pouch.Monster.Deployment, out _));
            Assert.AreEqual(2, unit.NextTrick);
            Assert.AreSame(offer, pouch.Offers[1]);
            Assert.AreEqual(coins, pouch.Coins);
            Assert.AreEqual(1, unit.Copies);
            Assert.IsTrue(pouch.TryBuyAndPlace(1, offer.Token, UnitLocation.Field, new Vector2Int(5, 7), out string reason), reason);
            Assert.AreEqual(originalLocation, unit.Location);
            Assert.AreEqual(originalCell, unit.Deployment);
            Assert.AreEqual(originalSlot, unit.BriefSlot);
            Assert.AreEqual(2, unit.Copies);
            CollectionAssert.AreEqual(new[] { false, false, true }, unit.Tricks);
            Assert.AreEqual(-1, unit.NextTrick);
            Assert.AreEqual(coins - price, pouch.Coins);
            Assert.AreEqual(paid + price, unit.Paid);
            Assert.AreEqual(2, pouch.RemainingCopies("dummy"));
        }

        [Test]
        public void StoredBriefUnit_ReservesItsSlotAcrossRerollAndRounds_AndFullBriefRejectsReturn()
        {
            UseOnlyDummy();
            PouchState pouch = CreatePouch();
            Assert.IsTrue(pouch.TryBuyAndPlace(0, pouch.Offers[0].Token, UnitLocation.Bench, Vector2Int.zero, out _));
            OwnedUnit unit = pouch.Owned["dummy"];
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
            foreach (PouchOffer offer in pouch.Offers) Assert.IsNotNull(offer);
            int coins = pouch.Coins;
            int version = pouch.OfferVersion;
            Assert.IsFalse(pouch.TrySetLocation("dummy", UnitLocation.Brief, Vector2Int.zero, out _));
            Assert.AreEqual(UnitLocation.Bench, unit.Location);
            Assert.AreEqual(-1, unit.BriefSlot);
            Assert.AreEqual(coins, pouch.Coins);
            Assert.AreEqual(version, pouch.OfferVersion);

            // A duplicate frees an offer slot without moving the banked unit.
            Buy(pouch, 1);
            Assert.AreEqual(UnitLocation.Bench, unit.Location);
            Assert.IsTrue(pouch.TrySetLocation("dummy", UnitLocation.Brief, Vector2Int.zero, out string reason), reason);
            Assert.AreEqual(1, unit.BriefSlot);
            coins = pouch.Coins;
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out reason), reason);
            Assert.IsNull(pouch.Offers[1]);
            Assert.IsNotNull(pouch.Offers[0]);
            Assert.IsNotNull(pouch.Offers[2]);
            Assert.AreEqual(1, unit.BriefSlot);
            Assert.AreEqual(2, unit.Copies);
            Assert.AreEqual(2, pouch.RemainingCopies("dummy"));
            Assert.AreEqual(coins, pouch.Coins);
            pouch.EndPreparation();
            Assert.IsTrue(pouch.BeginPreparation(2));
            Assert.IsNull(pouch.Offers[1]);
            Assert.AreEqual(1, unit.BriefSlot);
            Assert.IsTrue(pouch.TrySetLocation("dummy", UnitLocation.Field, new Vector2Int(1, 7), out reason), reason);
            Assert.AreEqual(-1, unit.BriefSlot);
            Assert.IsNull(pouch.Offers[1], "Leaving storage does not create another offer immediately.");
        }

        [Test]
        public void FourCopies_GrantBaseAndThreeTricks_InSelectedThenMissingOrder()
        {
            UseOnlyDummy();
            UnitDefinition dummy = config.Get("dummy");
            dummy.Tricks[0].Cost = 2;
            dummy.Tricks[1].Cost = 7;
            dummy.Tricks[2].Cost = 11;
            PouchState pouch = CreatePouch();
            Buy(pouch, 0);
            OwnedUnit owned = pouch.Owned["dummy"];
            Assert.AreEqual(0, owned.TrickCount);
            Assert.IsTrue(pouch.TrySelectTrick("dummy", 2, out string reason), reason);
            Assert.AreEqual(11, pouch.GetOfferCost(1));
            Buy(pouch, 1);
            CollectionAssert.AreEqual(new[] { false, false, true }, owned.Tricks);
            Assert.AreEqual(-1, owned.NextTrick);
            Buy(pouch, 2);
            CollectionAssert.AreEqual(new[] { true, false, true }, owned.Tricks);
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out reason), reason);
            Assert.IsNull(pouch.Offers[0]);
            Assert.IsNull(pouch.Offers[2]);
            Assert.AreEqual(7, pouch.GetOfferCost(1));
            Buy(pouch, 1);
            Assert.AreEqual(0, owned.BriefSlot, "Repeat copies preserve the stored unit's original Brief slot.");
            CollectionAssert.AreEqual(new[] { true, true, true }, owned.Tricks);
            Assert.AreEqual(4, owned.Copies);
            Assert.AreEqual(22, owned.Paid);
            Assert.AreEqual(78, pouch.Coins);
            Assert.AreEqual(0, pouch.RemainingCopies("dummy"));
            int rerolls = pouch.Rerolls;
            Assert.IsFalse(pouch.TryReroll(pouch.OfferVersion, out _));
            Assert.AreEqual(rerolls, pouch.Rerolls);
            Assert.AreEqual(2, pouch.Owned.Count, "There is one Monster and one Whelp, not one soldier per copy.");
        }

        [Test]
        public void FullPool_ContainsExactlyEightPurchasableCopies_AndNoPhantomRefills()
        {
            PouchState pouch = CreatePouch();
            BuyAll(pouch);
            Assert.AreEqual(4, pouch.Owned["dummy"].Copies);
            Assert.AreEqual(4, pouch.Owned["bugui"].Copies);
            Assert.AreEqual(3, pouch.Owned["dummy"].TrickCount);
            Assert.AreEqual(3, pouch.Owned["bugui"].TrickCount);
            Assert.AreEqual(0, pouch.PoolCount);
            foreach (PouchOffer offer in pouch.Offers) Assert.IsNull(offer);
            pouch.EndPreparation();
            Assert.IsTrue(pouch.BeginPreparation(2));
            foreach (PouchOffer offer in pouch.Offers) Assert.IsNull(offer);
        }

        [Test]
        public void Selling_RefundsExactlyPaidPrices_ReturnsCopies_AndNeverRefillsImmediately()
        {
            UseOnlyDummy();
            PouchState pouch = CreatePouch();
            Buy(pouch, 0);
            Buy(pouch, 1);
            int copies = pouch.Owned["dummy"].Copies;
            int paid = pouch.Owned["dummy"].Paid;
            int coins = pouch.Coins;
            int pool = pouch.PoolCount;
            Assert.IsTrue(pouch.TrySell("dummy", out string reason), reason);
            Assert.AreEqual(coins + paid, pouch.Coins);
            Assert.AreEqual(pool + copies, pouch.PoolCount);
            Assert.AreEqual(4, pouch.RemainingCopies("dummy"));
            Assert.IsFalse(pouch.Owned.ContainsKey("dummy"));
            Assert.IsNull(pouch.Offers[0]);
            Assert.IsFalse(pouch.TrySell("dummy", out _));
            Assert.AreEqual(coins + paid, pouch.Coins);
            Assert.IsFalse(pouch.TrySell("bugaloo", out _));
            Assert.AreEqual(1, pouch.Owned.Count);
        }

        [Test]
        public void Selling_CopyReturnCanBeDisabled_WithoutChangingRefund()
        {
            UseOnlyDummy();
            config.ReturnSoldCopies = false;
            PouchState pouch = CreatePouch();
            Buy(pouch, 0);
            Assert.IsTrue(pouch.TrySell("dummy", out _));
            Assert.AreEqual(100, pouch.Coins);
            Assert.AreEqual(3, pouch.RemainingCopies("dummy"));
        }

        [Test]
        public void InsufficientFunds_LeavesOfferMoneyCopiesAndChosenTrickUntouched()
        {
            UseOnlyDummy();
            config.RoundIncome = new[] { 2 };
            PouchState pouch = CreatePouch();
            Buy(pouch, 0);
            Assert.IsTrue(pouch.TrySelectTrick("dummy", 2, out _));
            PouchOffer offer = pouch.Offers[1];
            int version = pouch.OfferVersion;
            Assert.IsFalse(pouch.TryBuy(1, offer.Token, out _));
            Assert.IsFalse(pouch.TryBuyAndPlace(1, offer.Token, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreSame(offer, pouch.Offers[1]);
            Assert.AreEqual(0, pouch.Coins);
            Assert.AreEqual(version, pouch.OfferVersion);
            Assert.AreEqual(1, pouch.Owned["dummy"].Copies);
            Assert.AreEqual(2, pouch.Owned["dummy"].NextTrick);
            Assert.AreEqual(0, pouch.Owned["dummy"].TrickCount);
        }

        [Test]
        public void Combat_RejectsEveryPreparationMutation_WithoutPartialChanges()
        {
            UseOnlyDummy();
            PouchState pouch = CreatePouch();
            Buy(pouch, 0);
            int coins = pouch.Coins;
            int version = pouch.OfferVersion;
            PouchOffer offer = pouch.Offers[1];
            pouch.EndPreparation();
            Assert.IsFalse(pouch.TryBuy(1, offer.Token, out _));
            Assert.IsFalse(pouch.TryBuyAndPlace(1, offer.Token, UnitLocation.Field, new Vector2Int(1, 7), out _));
            Assert.IsFalse(pouch.TryReroll(version, out _));
            Assert.IsFalse(pouch.TrySell("dummy", out _));
            Assert.IsFalse(pouch.TrySelectTrick("dummy", 0, out _));
            Assert.IsFalse(pouch.TryUpgradeMonster(out _));
            Assert.IsFalse(pouch.TrySetLocation("dummy", UnitLocation.Field, new Vector2Int(1, 6), out _));
            Assert.AreEqual(coins, pouch.Coins);
            Assert.AreEqual(version, pouch.OfferVersion);
            Assert.AreEqual(4, pouch.Rerolls);
            Assert.AreSame(offer, pouch.Offers[1]);
            Assert.AreEqual(1, pouch.Owned["dummy"].Copies);
            Assert.AreEqual(UnitLocation.Brief, pouch.Owned["dummy"].Location);
            Assert.IsFalse(pouch.Monster.HasMonsterUpgrade);
        }

        [Test]
        public void MonsterUpgrade_AppliesForNextCombat_LocksPositionStartingFollowingRound()
        {
            PouchState pouch = CreatePouch();
            Assert.IsTrue(pouch.TryUpgradeMonster(out string reason), reason);
            Assert.IsTrue(pouch.Monster.IsUpgradeActive(1));
            Assert.IsFalse(pouch.Monster.IsUpgradeActive(0));
            Assert.IsFalse(pouch.Monster.IsPositionLocked(1));
            Assert.IsTrue(pouch.TrySetLocation("bugaloo", UnitLocation.Field, new Vector2Int(1, 7), out reason), reason);
            int coins = pouch.Coins;
            Assert.IsFalse(pouch.TryUpgradeMonster(out _));
            Assert.AreEqual(coins, pouch.Coins);
            pouch.EndPreparation();
            Assert.IsTrue(pouch.BeginPreparation(2));
            Assert.IsTrue(pouch.Monster.IsPositionLocked(2));
            Assert.IsFalse(pouch.TrySetLocation("bugaloo", UnitLocation.Field, new Vector2Int(2, 7), out _));
            Assert.AreEqual(new Vector2Int(1, 7), pouch.Monster.Deployment);
            Assert.IsFalse(pouch.TrySetLocation("bugaloo", UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.IsFalse(pouch.TrySell("bugaloo", out _));
        }

        [Test]
        public void Placement_RejectsOccupiedCellsAndFullBench_KeepsProgressionAcrossLocations()
        {
            PouchState pouch = CreatePouch();
            BuyAll(pouch);
            Assert.IsFalse(pouch.TrySetLocation("dummy", UnitLocation.Field, pouch.Monster.Deployment, out _));
            Assert.IsFalse(pouch.TrySetLocation("dummy", UnitLocation.Field, new Vector2Int(1, 4), out _));
            Assert.IsTrue(pouch.TrySetLocation("dummy", UnitLocation.Field, new Vector2Int(1, 6), out _));
            Assert.IsTrue(pouch.TrySetLocation("bugui", UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.IsFalse(pouch.TrySetLocation("dummy", UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreEqual(UnitLocation.Field, pouch.Owned["dummy"].Location);
            Assert.AreEqual(new Vector2Int(1, 6), pouch.Owned["dummy"].Deployment);
            Assert.IsTrue(pouch.TrySetLocation("bugui", UnitLocation.Brief, Vector2Int.zero, out _));
            Assert.IsTrue(pouch.TrySetLocation("dummy", UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.IsTrue(pouch.TrySetLocation("dummy", UnitLocation.Brief, Vector2Int.zero, out _));
            Assert.AreEqual(4, pouch.Owned["dummy"].Copies);
            Assert.AreEqual(3, pouch.Owned["dummy"].TrickCount);
        }

        [Test]
        public void Rounds_RejectDuplicateOrSkippedStarts_AccumulateRerollsAndRepeatLastIncome()
        {
            config.RoundIncome = new[] { 6, 4, 3, 2, 1 };
            PouchState pouch = CreatePouch();
            Assert.IsFalse(pouch.BeginPreparation(1));
            Assert.IsFalse(pouch.BeginPreparation(2), "A new round cannot start before the current preparation ends.");
            Assert.AreEqual(6, pouch.Coins);
            Assert.AreEqual(4, pouch.Rerolls);
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
            for (int round = 2; round <= 6; round++)
            {
                pouch.EndPreparation();
                Assert.IsFalse(pouch.BeginPreparation(round + 1));
                Assert.IsTrue(pouch.BeginPreparation(round));
            }
            Assert.AreEqual(17, pouch.Coins);
            Assert.AreEqual(23, pouch.Rerolls);
        }

        [Test]
        public void SeededPouch_IsReproducible_AndInvalidCommandsDoNotConsumeRandomness()
        {
            PouchState first = CreatePouch(548);
            PouchState second = CreatePouch(548);
            Assert.IsFalse(first.TryReroll(first.OfferVersion - 1, out _));
            for (int pass = 0; pass < 4; pass++)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    Assert.AreEqual(first.Offers[slot].UnitId, second.Offers[slot].UnitId);
                    Assert.AreNotEqual(first.Offers[slot].Token, second.Offers[slot].Token,
                        "Command tokens belong to one pouch even when the seeded offer sequence is identical.");
                }
                Assert.IsTrue(first.TryReroll(first.OfferVersion, out _));
                Assert.IsTrue(second.TryReroll(second.OfferVersion, out _));
            }
            Assert.IsFalse(first.TryReroll(first.OfferVersion, out _));
        }

        [Test]
        public void NewMatch_ResetsCopiesTricksUpgradeMoneyAndRerolls_WithoutMutatingConfig()
        {
            PouchState previous = CreatePouch(12);
            BuyAll(previous);
            Assert.IsTrue(previous.TryUpgradeMonster(out _));
            PouchState fresh = CreatePouch(12);
            Assert.AreEqual(1, fresh.Owned.Count);
            Assert.AreEqual(100, fresh.Coins);
            Assert.AreEqual(4, fresh.Rerolls);
            Assert.IsFalse(fresh.Monster.HasMonsterUpgrade);
            Assert.AreEqual(4, fresh.RemainingCopies("dummy"));
            Assert.AreEqual(4, fresh.RemainingCopies("bugui"));
            Assert.AreEqual(110, config.Get("bugaloo").MaxHealth);
            Assert.AreEqual(45, config.Get("dummy").MaxHealth);
        }

        [Test]
        public void NewMatch_RejectsOfferTokenFromPreviousMatch_EvenWithSameSeed()
        {
            PouchState previous = CreatePouch(12);
            PouchOffer stale = previous.Offers[0];
            PouchState fresh = CreatePouch(12);
            Assert.AreEqual(stale.UnitId, fresh.Offers[0].UnitId);
            Assert.IsFalse(fresh.TryBuy(0, stale.Token, out _));
            Assert.IsFalse(fresh.TryReroll(previous.OfferVersion, out _));
            Assert.AreEqual(100, fresh.Coins);
            Assert.AreEqual(4, fresh.Rerolls);
            Assert.AreEqual(1, fresh.Owned.Count);
            Assert.IsNotNull(fresh.Offers[0]);
        }

        [Test]
        public void Placement_UsesEditableMask_InsteadOfAssumingPlayerHalf()
        {
            config.BlueDeployment = new[] { new Vector2Int(2, 2), new Vector2Int(4, 4) };
            UseOnlyDummy();
            PouchState pouch = CreatePouch();
            Buy(pouch, 0);
            Assert.AreEqual(new Vector2Int(2, 2), pouch.Monster.Deployment);
            Assert.IsTrue(pouch.TrySetLocation("dummy", UnitLocation.Field, new Vector2Int(4, 4), out _));
            Assert.IsFalse(pouch.TrySetLocation("dummy", UnitLocation.Field, new Vector2Int(1, 7), out _));
        }

        [Test]
        public void FutureCatalog_SupportsSevenTypes_FiveFieldWhelpsAndOneBench()
        {
            // Synthetic definitions verify capacity without adding invented characters to the shipped catalog.
            var definitions = new List<UnitDefinition> { config.Get("bugaloo"), config.Get("popow") };
            for (int i = 0; i < 7; i++)
                definitions.Add(new UnitDefinition
                {
                    Id = "test-whelp-" + i, BaseCost = 0,
                    Tricks = new[] { new TrickDefinition(), new TrickDefinition(), new TrickDefinition() }
                });
            config.Units = definitions.ToArray();
            PouchState pouch = CreatePouch();
            for (int pass = 0; pass < 7 * MatchConfig.CopiesPerWhelp && pouch.Owned.Count < 8; pass++)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    if (pouch.Offers[slot] == null) continue;
                    string id = pouch.Offers[slot].UnitId;
                    Buy(pouch, slot);
                    for (int i = 0; i < 5; i++)
                        if (id == "test-whelp-" + i)
                            Assert.IsTrue(pouch.TrySetLocation(id, UnitLocation.Field, new Vector2Int(i, 7), out _));
                }
                if (pouch.Owned.Count == 8) break;
                if (pouch.Rerolls > 0) Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
                else
                {
                    pouch.EndPreparation();
                    Assert.IsTrue(pouch.BeginPreparation(pouch.Round + 1));
                }
            }
            Assert.AreEqual(8, pouch.Owned.Count);
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(pouch.TrySetLocation("test-whelp-" + i, UnitLocation.Field, new Vector2Int(i, 7), out _));
            Assert.IsFalse(pouch.TrySetLocation("test-whelp-5", UnitLocation.Field, new Vector2Int(5, 7), out _));
            Assert.AreEqual(UnitLocation.Brief, pouch.Owned["test-whelp-5"].Location);
            Assert.IsTrue(pouch.TrySetLocation("test-whelp-5", UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.IsFalse(pouch.TrySetLocation("test-whelp-6", UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreEqual(UnitLocation.Brief, pouch.Owned["test-whelp-6"].Location);
        }

        private PouchState CreatePouch(int seed = 112)
        {
            var result = new PouchState(config, seed, "bugaloo", BoardSide.Blue);
            Assert.IsTrue(result.BeginPreparation(1));
            return result;
        }

        private void UseOnlyDummy()
        {
            config.Units = new[] { config.Get("bugaloo"), config.Get("popow"), config.Get("dummy") };
        }

        private static void Buy(PouchState pouch, int slot)
        {
            Assert.IsNotNull(pouch.Offers[slot]);
            Assert.IsTrue(pouch.TryBuy(slot, pouch.Offers[slot].Token, out string reason), reason);
        }

        private static void BuyAll(PouchState pouch)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                for (int slot = 0; slot < pouch.Offers.Count; slot++)
                    if (pouch.Offers[slot] != null)
                    {
                        string id = pouch.Offers[slot].UnitId;
                        Buy(pouch, slot);
                        if (pouch.Owned[id].Location == UnitLocation.Brief)
                            Assert.IsTrue(pouch.TrySetLocation(id, UnitLocation.Field,
                                new Vector2Int(id == "dummy" ? 1 : 2, 7), out string placementReason), placementReason);
                    }
                if (pouch.PoolCount == 0) return;
                Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out string reason), reason);
            }
            Assert.Fail("The finite eight-copy pool did not finish in three packages.");
        }
    }
}

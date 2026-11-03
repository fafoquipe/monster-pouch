using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BankEconomyTests
    {
        MatchConfig config;
        PouchState pouch;
        static readonly Vector2Int Cell = new Vector2Int(1, 7);

        [SetUp] public void SetUp()
        {
            config = MatchConfig.CreateDefault();
            var whelp = JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(config.Get("dummy")));
            whelp.Id = "bank-test"; whelp.BaseCost = 3;
            whelp.Tricks[0].Cost = 4; whelp.Tricks[1].Cost = 2; whelp.Tricks[2].Cost = 5;
            config.Units = new[] { config.Get("bugaloo"), whelp };
            config.RoundIncome = new[] { 10, 0 };
            pouch = new PouchState(config, 9, "bugaloo", BoardSide.Blue);
            Assert.IsTrue(pouch.BeginPreparation(1));
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(config);

        OwnedUnit BuyField()
        {
            Assert.IsTrue(pouch.TryBuyAndPlace(0, pouch.Offers[0].Token, UnitLocation.Field, Cell, out var reason), reason);
            return pouch.Owned["bank-test"];
        }

        void Store() => Assert.IsTrue(pouch.TrySetLocation("bank-test", UnitLocation.Bench, Vector2Int.zero, out var reason), reason);
        void Deploy() => Assert.IsTrue(pouch.TrySetLocation("bank-test", UnitLocation.Field, Cell, out var reason), reason);

        [Test] public void PurchasedUnitRefundsAndRepaysActualBaseOncePerTransition()
        {
            var unit = BuyField();
            Assert.AreEqual(7, pouch.Coins);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                Store();
                Assert.AreEqual(10, pouch.Coins); Assert.AreEqual(3, unit.Paid);
                Assert.AreEqual(3, unit.BankRefunded); Assert.AreEqual(3, unit.BankRedeployCost);
                Assert.AreEqual(0, unit.SaleRefund);
                Store(); Assert.AreEqual(10, pouch.Coins, "Repeated store must not issue another refund.");
                Deploy();
                Assert.AreEqual(7, pouch.Coins); Assert.AreEqual(3, unit.Paid);
                Assert.AreEqual(0, unit.BankRefunded); Assert.AreEqual(0, unit.BankRedeployCost);
                Assert.AreEqual(3, unit.SaleRefund);
                Deploy(); Assert.AreEqual(7, pouch.Coins, "Repeated placement must not charge twice.");
                Assert.AreSame(unit, pouch.Owned["bank-test"]);
            }
        }

        [Test] public void ImprovedUnitRefundsAndRepaysActualInvestmentWithoutLosingItsTricks()
        {
            var unit = BuyField();
            Assert.IsTrue(pouch.TryBuy(1, pouch.Offers[1].Token, out _));
            Assert.AreEqual(3, pouch.Coins); Assert.AreEqual(7, unit.Paid);
            int remaining = pouch.RemainingCopies("bank-test");
            var tricks = unit.Tricks.ToArray();
            Store();
            Assert.AreEqual(10, pouch.Coins); Assert.AreEqual(7, unit.BankRedeployCost);
            Assert.AreEqual(0, unit.SaleRefund);
            config.Get("bank-test").BaseCost = 20;
            config.Get("bank-test").Tricks[0].Cost = 30;
            Deploy();
            Assert.AreEqual(3, pouch.Coins); Assert.AreEqual(7, unit.Paid);
            Assert.AreEqual(2, unit.Copies); Assert.AreEqual(remaining, pouch.RemainingCopies("bank-test"));
            CollectionAssert.AreEqual(tricks, unit.Tricks);
        }

        [TestCase(false)] [TestCase(true)]
        public void SaleFromBankNeverRefundsTheSameInvestmentTwice(bool improved)
        {
            var unit = BuyField();
            if (improved) Assert.IsTrue(pouch.TryBuy(1, pouch.Offers[1].Token, out _));
            Store(); int coins = pouch.Coins;
            Assert.IsTrue(pouch.TrySell("bank-test", out _));
            Assert.AreEqual(coins, pouch.Coins); Assert.IsFalse(pouch.Owned.ContainsKey("bank-test"));
            Assert.IsFalse(pouch.TrySell("bank-test", out _)); Assert.AreEqual(coins, pouch.Coins);
        }

        [Test] public void ReservationNeedsNoCoinsAndChargesItsCapturedBaseOnlyOnValidDeployment()
        {
            pouch.AddCombatCoins(-pouch.Coins);
            long token = pouch.Offers[0].Token;
            Assert.IsTrue(pouch.TryBuyAndPlace(0, token, UnitLocation.Bench, Vector2Int.zero, out var reason), reason);
            var unit = pouch.Owned["bank-test"];
            Assert.AreEqual(0, pouch.Coins); Assert.AreEqual(0, unit.Paid);
            Assert.AreEqual(3, unit.BankReservedCost); Assert.AreEqual(0, unit.BankRefunded);
            Assert.AreEqual(3, unit.BankRedeployCost); Assert.AreEqual(0, unit.SaleRefund);
            Assert.AreEqual(1, unit.Copies); Assert.AreEqual(3, pouch.RemainingCopies("bank-test"));
            Assert.IsTrue(pouch.Offers.All(offer => offer == null));
            Assert.IsFalse(pouch.TryBuyAndPlace(0, token, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.IsFalse(pouch.TrySetLocation("bank-test", UnitLocation.Field, Cell, out _));
            Assert.AreEqual(UnitLocation.Bench, unit.Location); Assert.AreEqual(0, unit.Paid);
            Assert.AreEqual(3, unit.BankReservedCost); Assert.AreEqual(0, pouch.Coins);
            config.Get("bank-test").BaseCost = 8;
            pouch.AddCombatCoins(3); Deploy();
            Assert.AreEqual(0, pouch.Coins); Assert.AreEqual(3, unit.Paid);
            Assert.AreEqual(0, unit.BankReservedCost); Assert.AreEqual(0, unit.BankRefunded);
            Store(); Assert.AreEqual(3, pouch.Coins); Assert.AreEqual(3, unit.BankRedeployCost);
            Deploy(); Assert.AreEqual(0, pouch.Coins); Assert.AreEqual(3, unit.Paid);
        }

        [Test] public void SellingUnpaidReservationReturnsNoMoneyAndRestoresItsCopy()
        {
            int coins = pouch.Coins;
            Assert.IsTrue(pouch.TryBuyAndPlace(0, pouch.Offers[0].Token, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.IsTrue(pouch.TrySell("bank-test", out _));
            Assert.AreEqual(coins, pouch.Coins); Assert.AreEqual(4, pouch.RemainingCopies("bank-test"));
        }

        [TestCase(false)] [TestCase(true)]
        public void FailedWithdrawalKeepsMoneyLocationAndBankLedgerUntouched(bool reserved)
        {
            OwnedUnit unit;
            if (reserved)
            {
                Assert.IsTrue(pouch.TryBuyAndPlace(0, pouch.Offers[0].Token, UnitLocation.Bench, Vector2Int.zero, out _));
                unit = pouch.Owned["bank-test"];
            }
            else { unit = BuyField(); Store(); }
            int refunded = unit.BankRefunded, reserve = unit.BankReservedCost, paid = unit.Paid, coins = pouch.Coins;
            int version = pouch.OfferVersion, copies = pouch.RemainingCopies("bank-test");
            foreach (var invalid in new[] { pouch.Monster.Deployment, new Vector2Int(-1, 7), new Vector2Int(1, 1) })
            {
                Assert.IsFalse(pouch.TrySetLocation("bank-test", UnitLocation.Field, invalid, out _));
                Assert.AreEqual(UnitLocation.Bench, unit.Location); Assert.AreEqual(coins, pouch.Coins);
                Assert.AreEqual(refunded, unit.BankRefunded); Assert.AreEqual(reserve, unit.BankReservedCost);
                Assert.AreEqual(paid, unit.Paid); Assert.AreEqual(version, pouch.OfferVersion);
                Assert.AreEqual(copies, pouch.RemainingCopies("bank-test"));
            }
            pouch.AddCombatCoins(2 - pouch.Coins);
            Assert.IsFalse(pouch.TrySetLocation("bank-test", UnitLocation.Field, Cell, out _));
            Assert.AreEqual(2, pouch.Coins); Assert.AreEqual(UnitLocation.Bench, unit.Location);
            Assert.AreEqual(refunded, unit.BankRefunded); Assert.AreEqual(reserve, unit.BankReservedCost);
            pouch.EndPreparation();
            Assert.IsFalse(pouch.TrySetLocation("bank-test", UnitLocation.Field, Cell, out _));
            Assert.IsFalse(pouch.TrySell("bank-test", out _)); Assert.AreEqual(2, pouch.Coins);
        }

        [Test] public void FreeBaseOnlyRefundsImprovementsActuallyPurchased()
        {
            config.Get("bank-test").BaseCost = 0;
            var unit = BuyField(); Store(); Deploy();
            Assert.AreEqual(10, pouch.Coins); Assert.AreEqual(0, unit.Paid);
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
            Assert.IsTrue(pouch.TryBuy(0, pouch.Offers[0].Token, out _));
            Assert.AreEqual(6, pouch.Coins); Assert.AreEqual(4, unit.Paid);
            Store(); Assert.AreEqual(10, pouch.Coins); Assert.AreEqual(4, unit.BankRedeployCost);
            Deploy(); Assert.AreEqual(6, pouch.Coins); Assert.AreEqual(4, unit.Paid);
        }

        OwnedUnit ReserveDuplicate()
        {
            int slot = Enumerable.Range(0, pouch.Offers.Count).First(i => pouch.Offers[i] != null);
            Assert.IsTrue(pouch.TryBuyAndPlace(slot, pouch.Offers[slot].Token, UnitLocation.Bench, Vector2Int.zero, out var reason), reason);
            return pouch.BankReserves["bank-test"];
        }

        void AssertFourCopies()
        {
            Assert.AreEqual(4, pouch.CountOwnedCopies("bank-test") + pouch.RemainingCopies("bank-test"));
        }

        [Test] public void DuplicateBankReservationIsFreeKeepsFieldUnitAndSuppressesOffers()
        {
            var field = BuyField(); pouch.AddCombatCoins(-pouch.Coins);
            long oldToken = pouch.Offers[1].Token;
            var reserve = ReserveDuplicate();
            Assert.AreSame(field, pouch.Owned["bank-test"]); Assert.AreEqual(UnitLocation.Field, field.Location);
            Assert.AreEqual(Cell, field.Deployment); Assert.AreEqual(0, pouch.Coins); Assert.AreEqual(3, field.Paid);
            Assert.AreEqual(1, field.Copies); Assert.AreEqual(0, field.TrickCount);
            Assert.AreEqual(1, reserve.Copies); Assert.AreEqual(0, reserve.Paid); Assert.AreEqual(3, reserve.BankRedeployCost);
            Assert.AreSame(reserve, pouch.FindOwned("bank-test", true)); Assert.IsTrue(pouch.Owns(reserve));
            Assert.AreEqual(3, pouch.AllOwned.Count()); Assert.AreEqual(1, pouch.BankedUnits.Count());
            Assert.IsTrue(pouch.Offers.All(offer => offer == null)); AssertFourCopies();
            Assert.IsFalse(pouch.TryBuyAndPlace(1, oldToken, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreEqual(0, pouch.Coins); AssertFourCopies();
        }

        [TestCase(false)] [TestCase(true)]
        public void FieldToDuplicateBankRefundsFieldAndReservesAllFinalTricksAtCurrentPrices(bool improved)
        {
            var field = BuyField();
            if (improved) Assert.IsTrue(pouch.TryBuyTrick("bank-test", 2, 1, pouch.Offers[1].Token, out _));
            field.PersistentDamageBonus = 7; field.PersistentHealthBonus = 11; field.MarkedPositionRound = 1;
            Assert.IsTrue(pouch.TrySelectTrick("bank-test", 1, out _));
            var reserve = ReserveDuplicate();
            config.Get("bank-test").BaseCost = 2;
            config.Get("bank-test").Tricks[0].Cost = 3;
            int expectedCost = improved ? 10 : 5;
            Assert.IsTrue(pouch.TrySetLocation(field, UnitLocation.Bench, Vector2Int.zero, out var reason), reason);
            Assert.AreEqual(10, pouch.Coins); Assert.AreSame(reserve, pouch.Owned["bank-test"]);
            Assert.IsEmpty(pouch.BankReserves); Assert.IsFalse(pouch.Owns(field)); Assert.IsTrue(pouch.Owns(reserve));
            Assert.AreEqual(improved ? 3 : 2, reserve.Copies); Assert.IsTrue(reserve.Tricks[0]);
            Assert.AreEqual(improved, reserve.Tricks[2]); Assert.IsFalse(reserve.Tricks[1]); Assert.AreEqual(1, reserve.NextTrick);
            Assert.AreEqual(7, reserve.PersistentDamageBonus); Assert.AreEqual(11, reserve.PersistentHealthBonus);
            Assert.AreEqual(1, reserve.MarkedPositionRound); Assert.AreEqual(0, reserve.Paid);
            Assert.AreEqual(0, reserve.BankRefunded); Assert.AreEqual(expectedCost, reserve.BankReservedCost);
            Assert.AreEqual(0, reserve.SaleRefund); AssertFourCopies();
            Assert.IsFalse(pouch.TrySetLocation(field, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.IsTrue(pouch.TrySetLocation(reserve, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreEqual(10, pouch.Coins);
            Assert.IsTrue(pouch.TrySetLocation(reserve, UnitLocation.Field, Cell, out reason), reason);
            Assert.AreEqual(10 - expectedCost, pouch.Coins); Assert.AreEqual(expectedCost, reserve.Paid);
            Assert.AreEqual(expectedCost, reserve.SaleRefund); Assert.AreEqual(0, reserve.BankRedeployCost);
            Store(); Assert.AreEqual(10, pouch.Coins);
            Assert.IsTrue(pouch.TrySell(reserve, out _)); Assert.AreEqual(10, pouch.Coins); AssertFourCopies();
        }

        [Test] public void BankToExistingFieldChargesOnlyNextNumericalTrickAndNeverMovesFieldUnit()
        {
            pouch.AddCombatCoins(10);
            var field = BuyField();
            Assert.IsTrue(pouch.TryBuyTrick("bank-test", 2, 1, pouch.Offers[1].Token, out _));
            Assert.IsTrue(pouch.TrySelectTrick("bank-test", 1, out _));
            field.PersistentDamageBonus = 7; field.PersistentHealthBonus = 11;
            var reserve = ReserveDuplicate(); int coins = pouch.Coins;
            Assert.AreEqual(4, pouch.GetDeploymentCost(reserve));
            Assert.IsTrue(pouch.TrySetLocation(reserve, UnitLocation.Field, pouch.Monster.Deployment, out var reason), reason);
            Assert.AreSame(field, pouch.Owned["bank-test"]); Assert.IsEmpty(pouch.BankReserves);
            Assert.IsFalse(pouch.Owns(reserve)); Assert.AreEqual(Cell, field.Deployment);
            Assert.AreEqual(coins - 4, pouch.Coins); Assert.AreEqual(12, field.Paid);
            Assert.AreEqual(3, field.Copies); CollectionAssert.AreEqual(new[] { true, false, true }, field.Tricks);
            Assert.AreEqual(1, field.NextTrick); Assert.AreEqual(7, field.PersistentDamageBonus);
            Assert.AreEqual(11, field.PersistentHealthBonus); AssertFourCopies();
            Assert.IsFalse(pouch.TrySetLocation(reserve, UnitLocation.Field, Cell, out _));
            Assert.AreEqual(coins - 4, pouch.Coins);
        }

        [Test] public void UnaffordableFusionLeavesBothObjectsAndLedgerUntouched()
        {
            var field = BuyField(); var reserve = ReserveDuplicate(); pouch.AddCombatCoins(3 - pouch.Coins);
            int version = pouch.OfferVersion;
            Assert.IsFalse(pouch.TrySetLocation(reserve, UnitLocation.Field, Cell, out _));
            Assert.AreSame(field, pouch.Owned["bank-test"]); Assert.AreSame(reserve, pouch.BankReserves["bank-test"]);
            Assert.AreEqual(3, pouch.Coins); Assert.AreEqual(1, field.Copies); Assert.AreEqual(1, reserve.Copies);
            Assert.AreEqual(0, field.TrickCount); Assert.AreEqual(3, field.Paid); Assert.AreEqual(0, reserve.Paid);
            Assert.AreEqual(3, reserve.BankReservedCost); Assert.AreEqual(version, pouch.OfferVersion); AssertFourCopies();
        }

        [TestCase(false)] [TestCase(true)]
        public void SellingOneOfTwoSameTypeObjectsPreservesTheOtherAndItsPool(bool sellField)
        {
            var field = BuyField(); var reserve = ReserveDuplicate();
            Assert.IsTrue(pouch.TrySell(sellField ? field : reserve, out var reason), reason);
            Assert.AreEqual(sellField ? 10 : 7, pouch.Coins);
            Assert.AreSame(sellField ? reserve : field, pouch.Owned["bank-test"]); Assert.IsEmpty(pouch.BankReserves);
            Assert.IsFalse(pouch.TrySell(sellField ? field : reserve, out _)); AssertFourCopies();
            if (sellField)
            {
                Assert.AreEqual(UnitLocation.Bench, reserve.Location); Assert.AreEqual(3, reserve.BankRedeployCost);
                Assert.IsTrue(pouch.TrySell(reserve, out _)); Assert.AreEqual(10, pouch.Coins); AssertFourCopies();
            }
        }

        [Test] public void RepeatedReserveAndFuseCannotExceedFourCopiesOrCreateMoney()
        {
            pouch.AddCombatCoins(20); var field = BuyField();
            for (int i = 0; i < 3; i++)
            {
                if (pouch.Offers.All(o => o == null)) Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
                var reserve = ReserveDuplicate();
                Assert.IsTrue(pouch.TrySetLocation(reserve, UnitLocation.Field, Cell, out var reason), reason);
                Assert.AreEqual(i + 2, field.Copies); AssertFourCopies();
            }
            Assert.AreEqual(4, field.Copies); Assert.AreEqual(3, field.TrickCount); Assert.AreEqual(14, field.Paid);
            Assert.AreEqual(16, pouch.Coins); Assert.AreEqual(0, pouch.RemainingCopies("bank-test"));
            Assert.IsFalse(pouch.TryReroll(pouch.OfferVersion, out _));
            Store(); Assert.AreEqual(30, pouch.Coins); Deploy(); Assert.AreEqual(16, pouch.Coins);
        }

        [Test] public void FullBankRejectsStorageAndReservationWithoutChangingCoinsOffersOrDebt()
        {
            var template = config.Get("bank-test");
            var whelps = Enumerable.Range(0, 7).Select(i =>
            {
                var definition = JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(template));
                definition.Id = "capacity-" + i; return definition;
            }).ToArray();
            config.Units = new[] { config.Get("bugaloo") }.Concat(whelps).ToArray();
            pouch = new PouchState(config, 19, "bugaloo", BoardSide.Blue);
            Assert.IsTrue(pouch.BeginPreparation(1));
            string fieldId = pouch.Offers[0].UnitId;
            Assert.IsTrue(pouch.TryBuyAndPlace(0, pouch.Offers[0].Token, UnitLocation.Field, Cell, out _));
            var field = pouch.Owned[fieldId];
            foreach (var definition in whelps.Where(w => w.Id != fieldId).Take(3))
            {
                var bank = pouch.AddTrainingWhelp(definition.Id);
                Assert.IsTrue(pouch.TrySetLocation(bank, UnitLocation.Bench, Vector2Int.zero, out _));
            }
            if (pouch.Offers.All(o => o == null)) Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out _));
            int slot = Enumerable.Range(0, pouch.Offers.Count).First(i => pouch.Offers[i] != null);
            var offer = pouch.Offers[slot]; int coins = pouch.Coins, version = pouch.OfferVersion;
            int copies = pouch.RemainingCopies(offer.UnitId);
            Assert.IsFalse(pouch.TrySetLocation(field, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreEqual(UnitLocation.Field, field.Location); Assert.AreEqual(3, field.Paid);
            Assert.AreEqual(0, field.BankRefunded); Assert.AreEqual(0, field.BankReservedCost);
            Assert.IsFalse(pouch.TryBuyAndPlace(slot, offer.Token, UnitLocation.Bench, Vector2Int.zero, out _));
            Assert.AreSame(offer, pouch.Offers[slot]); Assert.AreEqual(coins, pouch.Coins);
            Assert.AreEqual(version, pouch.OfferVersion); Assert.AreEqual(copies, pouch.RemainingCopies(offer.UnitId));
            Assert.AreEqual(3, pouch.BankedUnits.Count()); Assert.IsEmpty(pouch.BankReserves);
        }

        [Test] public void BankLedgerSurvivesRoundsWithoutRepeatedRefunds()
        {
            var unit = BuyField(); Store();
            for (int round = 2; round <= 4; round++)
            {
                pouch.EndPreparation(); Assert.IsTrue(pouch.BeginPreparation(round));
                Assert.AreEqual(10, pouch.Coins); Assert.AreEqual(3, unit.BankRefunded);
                Assert.AreEqual(3, unit.BankRedeployCost); Assert.AreEqual(0, unit.SaleRefund);
                Assert.IsTrue(pouch.Offers.All(offer => offer == null));
            }
            Deploy(); Assert.AreEqual(7, pouch.Coins);
        }
    }
}

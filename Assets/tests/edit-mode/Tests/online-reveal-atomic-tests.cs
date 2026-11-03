using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

public sealed class OnlineRevealAtomicTests
{
    MatchConfig config;
    MatchController subject, control;
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp] public void Setup()
    {
        config = ScriptableObject.CreateInstance<MatchConfig>();
        config.Units = DocumentedRoster.CreateAll(); config.RoundIncome = new[] { 200 };
        subject = Create("atomic-reveal"); control = Create("clean-reveal");
        Start(new[] { "tsu" });
    }
    MatchController Create(string name)
    {
        var go = new GameObject(name); var board = go.AddComponent<BoardManager>(); board.BuildBoard();
        var mapper = go.AddComponent<BoardWorldMapper>(); mapper.Configure(board, go.transform, new Vector2(1, -1), Vector2.zero);
        var match = go.AddComponent<MatchController>(); match.Configure(config, board, mapper); return match;
    }
    void Start(string[] team)
    {
        foreach (var match in new[] { subject, control })
        {
            match.Menu();
            Assert.IsTrue(match.StartOnlineMatch(BoardSide.Blue, "bugaloo", team, "sepora", team, 417));
        }
    }
    [TearDown] public void Cleanup()
    {
        foreach (var match in new[] { subject, control })
            if (match != null) { match.Menu(); UnityEngine.Object.DestroyImmediate(match.gameObject); }
        UnityEngine.Object.DestroyImmediate(config);
    }
    static bool Reveal(MatchController match, params PreparationCommand[] commands)
    {
        Assert.IsTrue(match.SetReady(true)); match.SetOpponentReady();
        return match.RevealOpponent(commands, match.Round);
    }
    static PreparationCommand Invalid() => new PreparationCommand { kind = "invalid-command" };
    static PreparationCommand Place(OwnedUnit unit, bool bankSource, Vector2Int cell) => new PreparationCommand
    { kind = "place", unit = unit.Definition.Id, sourceBench = bankSource, x = cell.x, y = cell.y };
    Vector2Int FreeCell(PouchState pouch, OwnedUnit unit, Vector2Int? exclude = null) =>
        config.FormationCells(unit.Definition, pouch.Side).First(cell => (!exclude.HasValue || cell != exclude.Value) &&
            pouch.AllOwned.All(other => other.Location != UnitLocation.Field || other.Deployment != cell));
    OwnedUnit Field(PouchState pouch)
    {
        var definition = config.Get("tsu");
        var cell = FreeCell(pouch, new OwnedUnit(definition));
        Assert.IsTrue(pouch.TryBuyAndPlace(0, pouch.Offers[0].Token, UnitLocation.Field, cell, out var reason), reason);
        var unit = pouch.Owned["tsu"];
        unit.PersistentDamageBonus = 7; unit.PersistentHealthBonus = 9; unit.MarkedPositionRound = 1;
        return unit;
    }
    OwnedUnit Reserve(PouchState pouch, bool improved)
    {
        if (improved) Assert.IsTrue(pouch.TryBuyTrick("tsu", 2, 1, pouch.Offers[1].Token, out var reason), reason);
        int slot = improved ? 2 : 1;
        Assert.IsTrue(pouch.TryBuyAndPlace(slot, pouch.Offers[slot].Token, UnitLocation.Bench, Vector2Int.zero, out var why), why);
        return pouch.FindOwned("tsu", true);
    }

    [TestCase("buy")][TestCase("buy-place")][TestCase("reroll")][TestCase("upgrade")]
    [TestCase("choose")][TestCase("trick")][TestCase("place")][TestCase("place-exchange")][TestCase("store")][TestCase("sell")]
    public void InvalidTailLeavesEveryCommandKindAndExistingViewsUnchanged(string kind)
    {
        var pouch = subject.Bot; var unit = Field(pouch); var before = new Observation(pouch);
        var destination = kind == "place-exchange" ? pouch.Monster.Deployment : FreeCell(pouch, unit);
        var command = new PreparationCommand { kind = kind == "place-exchange" ? "place" : kind, unit = "tsu", slot = 1, index = 2,
            bench = true, x = destination.x, y = destination.y };
        Assert.IsFalse(Reveal(subject, command, Invalid())); before.AssertUnchanged(pouch);
        Assert.IsFalse(subject.StartOnlineCombat(1), "A rejected journal must not open the combat barrier.");
        Assert.IsTrue(Reveal(subject, command), "The rejected prefix must remain legal on retry: " + kind);
        Assert.AreSame(before.OwnedView, pouch.Owned); Assert.AreSame(before.BankView, pouch.BankReserves);
        Assert.AreSame(before.OfferView, pouch.Offers); Assert.AreSame(before.Monster, pouch.Monster);
    }

    [Test] public void RejectedRerollPreservesRandomSequenceAndFutureRoundsAgainstACleanPeer()
    {
        Start(new[] { "blotan", "tsu", "stein" }); var before = new Observation(subject.Bot);
        Assert.IsFalse(Reveal(subject, new PreparationCommand { kind = "reroll" },
            new PreparationCommand { kind = "buy", slot = 0 }, Invalid()));
        before.AssertUnchanged(subject.Bot);
        Assert.IsTrue(Reveal(subject, new PreparationCommand { kind = "reroll" }));
        Assert.IsTrue(Reveal(control, new PreparationCommand { kind = "reroll" }));
        Assert.AreEqual(control.OnlineDigest(), subject.OnlineDigest());
        EquivalentOffers();
        for (int round = 2; round <= 5; round++)
        {
            foreach (var pouch in new[] { subject.Bot, control.Bot })
            {
                pouch.EndPreparation(); Assert.IsTrue(pouch.BeginPreparation(round));
                Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out var reason), reason);
            }
            EquivalentOffers(); Assert.AreEqual(control.Bot.Coins, subject.Bot.Coins);
            Assert.AreEqual(control.Bot.Rerolls, subject.Bot.Rerolls);
        }
    }
    void EquivalentOffers()
    {
        CollectionAssert.AreEqual(control.Bot.Offers.Select(o => o?.UnitId), subject.Bot.Offers.Select(o => o?.UnitId));
        CollectionAssert.AreEqual(Available(control.Bot), Available(subject.Bot));
        foreach (string id in subject.Bot.SelectedWhelpIds)
            Assert.AreEqual(control.Bot.RemainingCopies(id), subject.Bot.RemainingCopies(id));
    }

    [TestCase(false)][TestCase(true)]
    public void FusedBankJournalCommitsOnlyOnceAndPreservesTheCorrectSurvivor(bool intoBank)
    {
        var pouch = subject.Bot; var field = Field(pouch); var reserve = Reserve(pouch, true);
        var before = new Observation(pouch); int balance = pouch.Coins, paid = field.Paid;
        int nextPrice = Mathf.Max(0, field.Definition.Tricks[0].Cost);
        var command = intoBank ? new PreparationCommand { kind = "store", unit = "tsu", bench = true } :
            Place(reserve, true, pouch.Monster.Deployment);
        Assert.IsFalse(Reveal(subject, command, Invalid())); before.AssertUnchanged(pouch);
        Assert.IsTrue(Reveal(subject, command));
        var survivor = intoBank ? reserve : field; var removed = intoBank ? field : reserve;
        Assert.AreSame(survivor, pouch.Owned["tsu"]); Assert.IsFalse(pouch.Owns(removed));
        Assert.IsEmpty(pouch.BankReserves); Assert.AreEqual(3, survivor.Copies);
        CollectionAssert.AreEqual(new[] { true, false, true }, survivor.Tricks);
        Assert.AreEqual(7, survivor.PersistentDamageBonus); Assert.AreEqual(9, survivor.PersistentHealthBonus);
        Assert.AreEqual(1, survivor.MarkedPositionRound);
        Assert.AreEqual(intoBank ? balance + paid : balance - nextPrice, pouch.Coins);
        if (intoBank)
        {
            int fullCost = Mathf.Max(0, field.Definition.BaseCost) + nextPrice + Mathf.Max(0, field.Definition.Tricks[2].Cost);
            Assert.AreEqual(fullCost, reserve.BankRedeployCost); Assert.AreEqual(0, reserve.Paid);
        }
        else Assert.AreEqual(paid + nextPrice, field.Paid);
        string digest = subject.OnlineDigest();
        Assert.IsFalse(subject.RevealOpponent(new[] { command }, 1)); Assert.AreEqual(digest, subject.OnlineDigest());
        Assert.IsTrue(subject.StartOnlineCombat(1));
        Assert.AreEqual(!intoBank, subject.Actors.ContainsKey(survivor));
        Assert.IsFalse(subject.Actors.ContainsKey(removed));
    }

    [TestCase(false)][TestCase(true)]
    public void SellingTheChosenInstancePreservesOrPromotesTheOtherAfterAtomicCommit(bool sourceBench)
    {
        var pouch = subject.Bot; var field = Field(pouch); var reserve = Reserve(pouch, false);
        var before = new Observation(pouch); int balance = pouch.Coins, refund = field.SaleRefund;
        var command = new PreparationCommand { kind = "sell", unit = "tsu", sourceBench = sourceBench };
        Assert.IsFalse(Reveal(subject, command, Invalid())); before.AssertUnchanged(pouch);
        Assert.IsTrue(Reveal(subject, command));
        Assert.AreSame(sourceBench ? field : reserve, pouch.Owned["tsu"]);
        Assert.IsFalse(pouch.Owns(sourceBench ? reserve : field)); Assert.IsEmpty(pouch.BankReserves);
        Assert.AreEqual(balance + (sourceBench ? 0 : refund), pouch.Coins);
    }

    [Test] public void RedeploymentRetainsOriginalBankIdentityAndChargesOnlyAfterTheEntireJournalPasses()
    {
        var pouch = subject.Bot;
        Assert.IsTrue(pouch.TryBuyAndPlace(0, pouch.Offers[0].Token, UnitLocation.Bench, Vector2Int.zero, out var reason), reason);
        var reserve = pouch.FindOwned("tsu", true); var before = new Observation(pouch);
        int balance = pouch.Coins, cost = reserve.BankRedeployCost;
        var command = Place(reserve, true, FreeCell(pouch, reserve));
        Assert.IsFalse(Reveal(subject, command, Invalid())); before.AssertUnchanged(pouch);
        Assert.IsTrue(Reveal(subject, command)); Assert.AreSame(reserve, pouch.Owned["tsu"]);
        Assert.AreEqual(UnitLocation.Field, reserve.Location); Assert.AreEqual(balance - cost, pouch.Coins);
        Assert.AreEqual(cost, reserve.Paid); Assert.AreEqual(0, reserve.BankRedeployCost);
    }

    [Test] public void SellingThenBuyingTheSameTypeKeepsTheNewInstanceDistinctFromTheSoldObject()
    {
        var pouch = subject.Bot; var sold = Field(pouch); var before = new Observation(pouch);
        int balance = pouch.Coins, refund = sold.SaleRefund, cost = Mathf.Max(0, sold.Definition.BaseCost);
        var sell = new PreparationCommand { kind = "sell", unit = "tsu" };
        var buy = new PreparationCommand { kind = "buy-place", slot = 1, x = sold.Deployment.x, y = sold.Deployment.y };
        Assert.IsFalse(Reveal(subject, sell, buy, Invalid())); before.AssertUnchanged(pouch);
        Assert.IsTrue(Reveal(subject, sell, buy));
        var replacement = pouch.Owned["tsu"];
        Assert.AreNotSame(sold, replacement); Assert.IsFalse(pouch.Owns(sold));
        Assert.AreEqual(1, replacement.Copies); Assert.AreEqual(cost, replacement.Paid);
        Assert.AreEqual(0, replacement.PersistentDamageBonus); Assert.AreEqual(0, replacement.PersistentHealthBonus);
        Assert.AreEqual(balance + refund - cost, pouch.Coins);
    }

    static string[] Available(PouchState pouch) =>
        ((List<string>)typeof(PouchState).GetField("available", Hidden).GetValue(pouch)).ToArray();
    static string UnitState(OwnedUnit unit) => string.Join("|", unit.Copies, unit.NextTrick, unit.Paid,
        unit.BankRefunded, unit.BankReservedCost, unit.Location, unit.BriefSlot, unit.Deployment,
        unit.UpgradePurchasedRound, unit.PersistentDamageBonus, unit.PersistentHealthBonus, unit.MarkedPositionRound,
        string.Join(",", unit.Tricks));
    sealed class Observation
    {
        public readonly object OwnedView, BankView, OfferView;
        public readonly OwnedUnit Monster;
        readonly PouchState owner;
        readonly int coins, rerolls, version, round;
        readonly bool preparing;
        readonly string[] available;
        readonly KeyValuePair<string, OwnedUnit>[] primary, bank;
        readonly PouchOffer[] offers;
        readonly Dictionary<OwnedUnit, string> states;
        public Observation(PouchState pouch)
        {
            owner = pouch; OwnedView = pouch.Owned; BankView = pouch.BankReserves; OfferView = pouch.Offers; Monster = pouch.Monster;
            coins = pouch.Coins; rerolls = pouch.Rerolls; version = pouch.OfferVersion; round = pouch.Round; preparing = pouch.IsPreparation;
            available = Available(pouch); primary = pouch.Owned.ToArray(); bank = pouch.BankReserves.ToArray(); offers = pouch.Offers.ToArray();
            states = pouch.AllOwned.ToDictionary(unit => unit, UnitState);
        }
        public void AssertUnchanged(PouchState pouch)
        {
            Assert.AreSame(owner, pouch); Assert.AreSame(OwnedView, pouch.Owned); Assert.AreSame(BankView, pouch.BankReserves);
            Assert.AreSame(OfferView, pouch.Offers); Assert.AreSame(Monster, pouch.Monster);
            Assert.AreEqual(coins, pouch.Coins); Assert.AreEqual(rerolls, pouch.Rerolls); Assert.AreEqual(version, pouch.OfferVersion);
            Assert.AreEqual(round, pouch.Round); Assert.AreEqual(preparing, pouch.IsPreparation);
            CollectionAssert.AreEqual(available, Available(pouch)); CollectionAssert.AreEqual(primary, pouch.Owned.ToArray());
            CollectionAssert.AreEqual(bank, pouch.BankReserves.ToArray());
            for (int i = 0; i < offers.Length; i++) Assert.AreSame(offers[i], pouch.Offers[i], "Offer/token " + i);
            foreach (var pair in states) Assert.AreEqual(pair.Value, UnitState(pair.Key), pair.Key.Definition.Id);
        }
    }
}

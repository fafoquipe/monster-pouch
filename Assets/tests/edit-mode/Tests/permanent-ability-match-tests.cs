using System;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PermanentAbilityMatchTests
{
    MatchConfig config;
    MatchController host, guest;
    const string Whelp = "blotan";
    UnitDefinition Definition => config.Get(Whelp);
    [SetUp] public void Setup()
    {
        config = ScriptableObject.CreateInstance<MatchConfig>();
        config.Units = DocumentedRoster.CreateAll(); config.RoundIncome = new[] { 100 };
    }
    [TearDown] public void Cleanup()
    {
        foreach (var match in new[] { host, guest }) if (match != null) { match.Menu(); Object.DestroyImmediate(match.gameObject); }
        Object.DestroyImmediate(config);
    }
    AbilityAccessEntry Entry(string id, params int[] indexes) => new AbilityAccessEntry {
        characterId = id, abilityIds = indexes.Select(i => config.Get(id).Tricks[i].Id).ToArray()
    };
    AbilityAccessSnapshot Access(params int[] indexes) => AbilityAccessSnapshot.Create(config, new[] { Entry(Whelp, indexes) });
    PouchState Pouch(AbilityAccessSnapshot access)
    {
        var pouch = new PouchState(config, 71, "bugaloo", BoardSide.Blue, new[] { Whelp }, access);
        Assert.IsTrue(pouch.BeginPreparation(1)); return pouch;
    }
    int Offer(PouchState pouch)
    {
        int slot = Enumerable.Range(0, pouch.Offers.Count).Where(i => pouch.Offers[i] != null).DefaultIfEmpty(-1).First();
        if (slot < 0)
        {
            Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion, out string reason), reason);
            slot = Enumerable.Range(0, pouch.Offers.Count).First(i => pouch.Offers[i] != null);
        }
        return slot;
    }
    Vector2Int Cell(PouchState pouch) => config.FormationCells(Definition, pouch.Side).First(c => c != pouch.Monster.Deployment);
    OwnedUnit BuyField(PouchState pouch)
    {
        int slot = Offer(pouch);
        Assert.IsTrue(pouch.TryBuyAndPlace(slot, pouch.Offers[slot].Token, UnitLocation.Field, Cell(pouch), out string reason), reason);
        return pouch.Owned[Whelp];
    }
    OwnedUnit Reserve(PouchState pouch)
    {
        int slot = Offer(pouch);
        Assert.IsTrue(pouch.TryBuyAndPlace(slot, pouch.Offers[slot].Token, UnitLocation.Bench, Vector2Int.zero, out string reason), reason);
        return pouch.FindOwned(Whelp, true);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void CopyPoolIsBasePlusOwnedAbilitiesAndNeverCreatesLockedTricks(int count)
    {
        var indexes = new[] { 2, 0, 1 }.Take(count).ToArray();
        var pouch = Pouch(Access(indexes));
        Assert.AreEqual(1 + count, pouch.RemainingCopies(Whelp));
        Assert.AreEqual(1 + count, pouch.CopyLimit(Whelp));
        var unit = BuyField(pouch);
        for (int i = 0; i < count; i++)
        {
            int slot = Offer(pouch); Assert.IsTrue(pouch.TryBuy(slot, pouch.Offers[slot].Token, out string reason), reason);
            Assert.AreEqual(1 + count, pouch.CountOwnedCopies(Whelp) + pouch.RemainingCopies(Whelp));
        }
        Assert.AreEqual(1 + count, unit.Copies); Assert.AreEqual(count, unit.TrickCount);
        for (int i = 0; i < 3; i++) Assert.AreEqual(indexes.Contains(i), unit.Tricks[i]);
        Assert.AreEqual(-1, unit.PendingTrickIndex); Assert.AreEqual(0, pouch.RemainingCopies(Whelp));
        Assert.IsFalse(pouch.TryReroll(pouch.OfferVersion, out _));
    }

    [Test] public void ExplicitLockedPurchaseAndSelectionRejectWithoutSpendingOrConsumingOffer()
    {
        var pouch = Pouch(Access(2)); var unit = BuyField(pouch); int slot = Offer(pouch);
        var offer = pouch.Offers[slot]; int coins = pouch.Coins;
        Assert.AreEqual(2, unit.PendingTrickIndex);
        Assert.IsFalse(pouch.TrySelectTrick(Whelp, 0, out _));
        Assert.IsFalse(pouch.CanBuyTrick(Whelp, 0, slot, offer.Token, out _));
        Assert.IsFalse(pouch.TryBuyTrick(Whelp, 0, slot, offer.Token, out _));
        Assert.AreSame(offer, pouch.Offers[slot]); Assert.AreEqual(coins, pouch.Coins); Assert.AreEqual(1, unit.Copies);
        Assert.IsTrue(pouch.TryBuyTrick(Whelp, 2, slot, offer.Token, out _));
        Assert.IsTrue(unit.Tricks[2]); Assert.AreEqual(coins - Definition.Tricks[2].Cost, pouch.Coins);
    }

    [TestCase(true)] [TestCase(false)]
    public void BankFusionUsesFirstUnlockedMissingAbilityAndKeepsCopyAndRefundAccounting(bool intoBank)
    {
        var pouch = Pouch(Access(2)); var field = BuyField(pouch); var reserve = Reserve(pouch);
        Assert.AreEqual(2, pouch.CountOwnedCopies(Whelp)); Assert.AreEqual(0, pouch.RemainingCopies(Whelp));
        int balance = pouch.Coins; int paid = field.Paid;
        Assert.AreEqual(Definition.Tricks[2].Cost, pouch.GetDeploymentCost(reserve));
        Assert.IsTrue(pouch.TrySetLocation(intoBank ? field : reserve, intoBank ? UnitLocation.Bench : UnitLocation.Field,
            field.Deployment, out string reason), reason);
        var survivor = pouch.Owned[Whelp]; Assert.AreSame(intoBank ? reserve : field, survivor);
        Assert.AreEqual(2, survivor.Copies); CollectionAssert.AreEqual(new[] { false, false, true }, survivor.Tricks);
        Assert.IsEmpty(pouch.BankReserves); Assert.AreEqual(intoBank ? balance + paid : balance - Definition.Tricks[2].Cost, pouch.Coins);
        if (intoBank)
        {
            Assert.AreEqual(Definition.BaseCost + Definition.Tricks[2].Cost, survivor.BankRedeployCost);
            int beforeSale = pouch.Coins; Assert.IsTrue(pouch.TrySell(survivor, out _));
            Assert.AreEqual(beforeSale, pouch.Coins); Assert.AreEqual(2, pouch.RemainingCopies(Whelp));
        }
    }

    [Test] public void BaseOnlyUnitCanUseBankWithoutManufacturingACopyOrUpgrade()
    {
        var pouch = Pouch(Access()); var unit = BuyField(pouch); int paid = unit.Paid, afterBuy = pouch.Coins;
        Assert.IsTrue(pouch.TrySetLocation(unit, UnitLocation.Bench, Vector2Int.zero, out _));
        Assert.AreEqual(afterBuy + paid, pouch.Coins); Assert.AreEqual(paid, unit.BankRedeployCost);
        Assert.IsTrue(pouch.TrySetLocation(unit, UnitLocation.Field, Cell(pouch), out _));
        Assert.AreEqual(afterBuy, pouch.Coins); Assert.AreEqual(paid, unit.Paid);
        Assert.AreEqual(1, unit.Copies); Assert.AreEqual(0, unit.TrickCount); Assert.AreEqual(0, pouch.RemainingCopies(Whelp));
    }

    [Test] public void MonsterKeepsItsBaseAndCanOnlyPurchaseAnOwnedUpgrade()
    {
        var restricted = Pouch(Access());
        Assert.IsNotNull(restricted.Monster.Definition.BaseAbility);
        Assert.IsFalse(restricted.TryUpgradeMonster(out _));
        for (int i = 0; i < 3; i++) Assert.IsFalse(restricted.CanUpgradeMonster(i));
        var access = AbilityAccessSnapshot.Create(config, new[] { Entry("bugaloo", 2) });
        var allowed = Pouch(access); Assert.IsFalse(allowed.TryUpgradeMonster(0, out _));
        Assert.IsTrue(allowed.CanUpgradeMonster(2)); Assert.IsTrue(allowed.TryUpgradeMonster(out _));
        CollectionAssert.AreEqual(new[] { false, false, true }, allowed.Monster.Tricks);
    }

    [Test] public void SnapshotDefensivelyCopiesInputsAndWireDataAndRejectsInvalidCanonicalPayloads()
    {
        var entry = Entry(Whelp, 2); var snapshot = AbilityAccessSnapshot.Create(config, new[] { entry });
        entry.abilityIds[0] = Definition.Tricks[0].Id;
        var wire = snapshot.ToData(); wire.entries[0].abilityIds[0] = Definition.Tricks[1].Id;
        Assert.IsTrue(snapshot.Allows(Definition, 2)); Assert.IsFalse(snapshot.Allows(Definition, 0));
        Assert.AreEqual(snapshot.Canonical, AbilityAccessSnapshot.FromData(config, snapshot.ToData()).Canonical);
        Assert.Throws<ArgumentException>(() => AbilityAccessSnapshot.FromData(config, null));
        Assert.Throws<ArgumentException>(() => AbilityAccessSnapshot.Create(config, new[] { Entry(Whelp, 2, 2) }));
        Assert.Throws<ArgumentException>(() => AbilityAccessSnapshot.Create(config, new[] { new AbilityAccessEntry { characterId = Whelp, abilityIds = new[] { "unknown" } } }));
        Assert.Throws<ArgumentException>(() => AbilityAccessSnapshot.FromData(config, new AbilityAccessData { unrestricted = true, entries = new[] { Entry(Whelp, 2) } }));
        var sorted = AbilityAccessSnapshot.Create(config, new[] { Entry(Whelp, 0, 2) }).ToData();
        Array.Reverse(sorted.entries[0].abilityIds);
        Assert.Throws<ArgumentException>(() => AbilityAccessSnapshot.FromData(config, sorted));
    }

    [Test] public void PrivateJournalRetainsPolicyAndRejectsAForbiddenTailAtomically()
    {
        var pouch = Pouch(Access(2)); var field = BuyField(pouch); int slot = Offer(pouch);
        var offer = pouch.Offers[slot]; int before = pouch.Coins;
        Assert.IsFalse(pouch.TryApplyPreparationJournal(candidate => {
            Assert.AreSame(pouch.AbilityAccess, candidate.AbilityAccess);
            if (!candidate.TryBuyTrick(Whelp, 2, slot, offer.Token, out _)) return false;
            return candidate.TrySelectTrick(Whelp, 0, out _);
        }));
        Assert.AreEqual(before, pouch.Coins); Assert.AreSame(field, pouch.Owned[Whelp]); Assert.AreEqual(1, field.Copies);
        Assert.AreSame(offer, pouch.Offers[slot]);
        Assert.IsTrue(pouch.TryApplyPreparationJournal(candidate => candidate.TryBuyTrick(Whelp, 2, slot, offer.Token, out _)));
        Assert.AreSame(field, pouch.Owned[Whelp]); Assert.IsTrue(field.Tricks[2]); Assert.IsFalse(field.Tricks[0]);
    }

    MatchController Create(string name)
    {
        var go = new GameObject(name); var board = go.AddComponent<BoardManager>(); board.BuildBoard();
        var mapper = go.AddComponent<BoardWorldMapper>(); mapper.Configure(board, go.transform, new Vector2(1, -1), Vector2.zero);
        var match = go.AddComponent<MatchController>(); match.Configure(config, board, mapper); return match;
    }
    [Test] public void NormalLocalMatchUsesFrozenAccountPolicyWhileBotAndTrainingKeepExistingAccess()
    {
        host = Create("policy-local"); Assert.IsTrue(host.SetLocalAbilityAccess(Access()));
        Assert.IsTrue(host.StartMatch("bugaloo", new[] { Whelp }, 71));
        Assert.AreEqual(1, host.Player.CopyLimit(Whelp)); Assert.IsTrue(host.Bot.AbilityAccess.IsUnrestricted);
        Assert.IsFalse(host.SetLocalAbilityAccess(AbilityAccessSnapshot.Unrestricted)); Assert.AreEqual(1, host.Player.CopyLimit(Whelp));
        host.Menu();
        Assert.IsTrue(host.StartTraining("bugaloo", new[] { Whelp }, TrainingLesson.PreparationEconomy));
        Assert.IsTrue(host.Player.AbilityAccess.IsUnrestricted);
        Assert.AreEqual(4, host.Player.CopyLimit(Whelp));
        var practice = BuyField(host.Player); int offer = Offer(host.Player);
        Assert.IsTrue(host.Player.TryBuyTrick(Whelp, 0, offer, host.Player.Offers[offer].Token, out _));
        Assert.IsTrue(practice.Tricks[0]);
        host.Menu(); Assert.IsTrue(host.StartMatch("bugaloo", new[] { Whelp }, 72));
        Assert.AreEqual(1, host.Player.CopyLimit(Whelp));
        Assert.IsFalse(host.Player.AbilityAccess.Allows(Definition, 0));
    }

    [Test] public void BothPeersReplayDifferentAccountPoliciesAndRejectLockedRemoteCommands()
    {
        host = Create("policy-host"); guest = Create("policy-guest");
        var blue = Access(); var red = Access(2);
        Assert.IsTrue(host.StartOnlineMatch(BoardSide.Blue, "bugaloo", new[] { Whelp }, "sepora", new[] { Whelp }, 71,
            localAccess: blue, rivalAccess: red));
        Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Red, "sepora", new[] { Whelp }, "bugaloo", new[] { Whelp }, 71,
            localAccess: red, rivalAccess: blue));
        foreach (var match in new[] { host, guest })
        {
            int slot = Offer(match.Player); var cell = Cell(match.Player);
            Assert.IsTrue(match.BuyAndPlace(slot, match.Player.Offers[slot].Token, match.Board.GetCell(cell.x, cell.y)), match.Notice);
        }
        int upgradeSlot = Offer(guest.Player);
        Assert.IsTrue(guest.BuyTrick(Whelp, 2, upgradeSlot, guest.Player.Offers[upgradeSlot].Token));
        host.Ready(); guest.Ready(); host.SetOpponentReady(); guest.SetOpponentReady();
        string before = host.OnlineDigest();
        var invalid = guest.PreparationCommands.Concat(new[] { new PreparationCommand { kind = "choose", unit = Whelp, index = 0 } }).ToArray();
        Assert.IsFalse(host.RevealOpponent(invalid, 1)); Assert.AreEqual(before, host.OnlineDigest());
        Assert.IsTrue(host.RevealOpponent(guest.PreparationCommands, 1));
        Assert.IsTrue(guest.RevealOpponent(host.PreparationCommands, 1));
        Assert.AreEqual(host.OnlineDigest(), guest.OnlineDigest());
        Assert.AreEqual(1, host.Player.CountOwnedCopies(Whelp)); Assert.AreEqual(2, host.Bot.CountOwnedCopies(Whelp));
    }

    [Test] public void DigestIncludesAbilityIdsEvenWhenPoolSizeAndRosterAreEqual()
    {
        host = Create("digest-first"); guest = Create("digest-second");
        Assert.IsTrue(host.StartOnlineMatch(BoardSide.Blue, "bugaloo", new[] { Whelp }, "sepora", new[] { Whelp }, 71,
            localAccess: Access(0), rivalAccess: Access()));
        Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Blue, "bugaloo", new[] { Whelp }, "sepora", new[] { Whelp }, 71,
            localAccess: Access(2), rivalAccess: Access()));
        Assert.AreEqual(host.Player.PoolCount, guest.Player.PoolCount);
        Assert.AreNotEqual(host.OnlineDigest(), guest.OnlineDigest());
    }
}

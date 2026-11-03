using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class ApprovedVendingEconomyTests
{
    static readonly string[] CurrentIds = {
        "aky", "anuik", "atong", "atori", "blotan", "bugaloo", "bugui", "flo", "gochan", "hymay", "jazar",
        "kayon", "kazuru", "pokrak", "popow", "rosie", "sepora", "stein", "suki", "tauris", "tokoro", "trimol", "tsu"
    };
    static readonly double[] ApprovedChances = { .01, .01, .01, .01, .01, .02, .04, .08, .20, 1 };
    MatchConfig characters;
    ApprovedPlayerMachineEconomy economy;
    string directory;

    [SetUp] public void Setup()
    {
        characters = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        economy = Resources.Load<ApprovedPlayerMachineEconomy>("MonsterPouch/ApprovedVendingEconomy");
        Assert.IsNotNull(economy, "The approved shared economics resource must be imported.");
    }

    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(characters);
        if (directory == null) return;
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchApprovedVendingTests")) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }

    [TestCaseSource(nameof(CurrentIds))]
    public void ApprovedEconomicsUseCurrentCharacterAndAbilityIdsWithoutArtBindings(string id)
    {
        // This test-only identity deliberately does not assign a production machine or artwork.
        var machine = Create(id);
        Assert.DoesNotThrow(() => machine.Validate(characters));
        Assert.AreEqual(id, machine.Definition.CharacterId);
        Assert.AreEqual("test-approved-" + id, machine.Definition.MachineId);
        Assert.AreEqual(5, machine.Cost.Amount);
        Assert.AreEqual(PermanentCurrency.Saturncoins, machine.Cost.Currency);
        Assert.AreEqual(14, machine.Definition.Rewards.Length);
        CollectionAssert.AreEquivalent(characters.Get(id).Tricks.Select(t => t.Id).ToArray(),
            machine.Definition.Rewards.Where(r => r.Kind == MachineRewardKind.Ability).Select(r => r.AbilityId).ToArray());
        CollectionAssert.AreEqual(ApprovedChances, machine.Policy.CharacterChanceByDraw);
        Assert.AreEqual(13, machine.Policy.RewardWeights.Length);
        Assert.IsTrue(machine.Policy.RewardWeights.All(w => w.Weight == 1));
        CollectionAssert.AreEquivalent(new[] {
            "Earthcoins:10", "Earthcoins:20", "Earthcoins:30", "Earthcoins:50",
            "Suncoins:10", "Suncoins:20", "Suncoins:30", "Suncoins:50",
            "Earthcoins:10;Suncoins:10", "Earthcoins:20;Suncoins:20"
        }, machine.Packages.Select(p => string.Join(";", p.Amounts.OrderBy(a => a.Currency).Select(a => a.Currency + ":" + a.Amount))).ToArray());
    }

    [Test] public void MissingArtworkAndEmptyEconomyCannotActivateACatalogue()
    {
        CollectionAssert.AreEquivalent(CurrentIds, characters.Units.Select(u => u.Id).ToArray());
        var catalogue = ScriptableObject.CreateInstance<PlayerMachineCatalogue>();
        var unconfigured = ScriptableObject.CreateInstance<ApprovedPlayerMachineEconomy>();
        try
        {
            catalogue.Machines = new[] { new PlayerMachineEntry { Configuration = Create("flo") } };
            Assert.Throws<ArgumentException>(() => catalogue.Validate(characters), "Economics do not approve missing artwork.");
            Assert.Throws<ArgumentException>(() => unconfigured.Create(characters, "unapproved", "flo"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalogue);
            UnityEngine.Object.DestroyImmediate(unconfigured);
        }
    }

    [Test] public void ApprovedLiveCatalogueHasAllTwentyThreeExplicitBindingsAndMatchingEconomics()
    {
        var catalogue = Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue");
        Assert.IsNotNull(catalogue);
        Assert.DoesNotThrow(() => catalogue.Validate(characters));
        Assert.AreEqual(23, catalogue.Machines.Length);
        CollectionAssert.AreEquivalent(CurrentIds, catalogue.Machines.Select(m => m.Configuration.Definition.CharacterId).ToArray());
        foreach (var entry in catalogue.Machines)
        {
            string id = entry.Configuration.Definition.CharacterId;
            // These are the explicit proposedMachineId bindings approved from the artwork manifest.
            Assert.AreEqual("character-" + id, entry.Configuration.Definition.MachineId);
            Assert.AreSame(Resources.Load<Sprite>("MonsterPouch/Vending/Artwork/" + id), entry.Artwork);
            Assert.AreEqual(FilterMode.Point, entry.Artwork.texture.filterMode);
            Assert.AreEqual(1, entry.Artwork.texture.mipmapCount);
            Assert.AreEqual(economy.Create(characters, entry.Configuration.Definition.MachineId, id).Signature(),
                entry.Configuration.Signature(), id);
        }
        Assert.AreEqual(23, catalogue.Machines.Select(m => m.Artwork.GetInstanceID()).Distinct().Count());
    }

    [Test] public void CreatingConfigurationsDoesNotShareMutableEconomicsOrInferBindings()
    {
        var first = Create("flo");
        string signature = first.Signature();
        first.Cost.Amount = 999;
        first.Policy.CharacterChanceByDraw[0] = 1;
        first.Policy.RewardWeights[0].Weight = 99;
        first.Packages[0].Amounts[0].Amount = 999;
        first.Definition.Rewards[1].AbilityId = "different-ability";
        Assert.AreEqual(signature, Create("flo").Signature());
        Assert.Throws<ArgumentException>(() => economy.Create(characters, null, "flo"));
        Assert.Throws<ArgumentException>(() => economy.Create(characters, "machine-with-visible-name", "Floru"));
        Assert.Throws<ArgumentException>(() => economy.Create(characters, "machine-with-unknown-id", "unknown"));
        Assert.Throws<ArgumentException>(() => economy.Create(null, "machine-without-catalogue", "flo"));
    }

    [Test] public void TenDrawCurveAndFollowingAbilityGuaranteeUseExactApprovedProbabilities()
    {
        var machine = Create("flo");
        var context = MachineDrawEngine.Prepare(machine.Definition, machine.Policy);
        var state = context.CreateState();
        var ownership = new MachineOwnership();
        string characterReward = machine.Definition.Rewards.Single(r => r.Kind == MachineRewardKind.Character).RewardId;
        for (int i = 0; i < 10; i++)
        {
            var preview = context.Preview(state, ownership);
            Assert.AreEqual(MachineStatus.Ready, preview.Status, "draw " + (i + 1));
            Assert.AreEqual(ApprovedChances[i], preview.Outcomes.Single(o => o.RewardId == characterReward).Probability, 1e-12);
            Assert.AreEqual(3, preview.Rewards.Count(r => r.Status == MachineRewardStatus.Locked));
            foreach (var outcome in preview.Outcomes.Where(o => o.RewardId != characterReward))
                Assert.AreEqual((1 - ApprovedChances[i]) / (10 - i), outcome.Probability, 1e-12);
            var result = context.TryDraw(state, ownership, Request(machine, "pity-" + i, state.Revision), .999999);
            Assert.AreEqual(MachineStatus.Drawn, result.Status, result.Reason);
            Assert.AreEqual(i == 9 ? MachineRewardKind.Character : MachineRewardKind.Resource,
                machine.Definition.Rewards.Single(r => r.RewardId == result.Receipt.RewardId).Kind);
            if (i == 9) Assert.AreEqual(MachineGuarantee.CharacterAtTen, result.Receipt.Guarantee);
            state = result.State;
        }
        var next = context.Preview(state, ownership);
        Assert.IsTrue(next.FirstAbilityGuaranteed);
        Assert.AreEqual(3, next.Outcomes.Length);
        Assert.IsTrue(next.Outcomes.All(o => Math.Abs(o.Probability - 1d / 3) < 1e-12));
        var ability = context.TryDraw(state, ownership, Request(machine, "guaranteed-skill", state.Revision), .5);
        Assert.AreEqual(MachineStatus.Drawn, ability.Status, ability.Reason);
        Assert.AreEqual(MachineGuarantee.FirstAbility, ability.Receipt.Guarantee);
        Assert.AreEqual(MachineRewardKind.Ability, machine.Definition.Rewards.Single(r => r.RewardId == ability.Receipt.RewardId).Kind);
    }

    [Test] public void AfterEarlyCharacterAndGuaranteedAbilityAllRemainingPrizesHaveEqualWeight()
    {
        var machine = Create("flo");
        var context = MachineDrawEngine.Prepare(machine.Definition, machine.Policy);
        var ownership = new MachineOwnership();
        var character = context.TryDraw(context.CreateState(), ownership, Request(machine, "early-character", 0), 0);
        Assert.AreEqual(MachineStatus.Drawn, character.Status, character.Reason);
        Assert.AreEqual(MachineRewardKind.Character, machine.Definition.Rewards.Single(r => r.RewardId == character.Receipt.RewardId).Kind);
        var ability = context.TryDraw(character.State, ownership, Request(machine, "first-ability", 1), .5);
        Assert.AreEqual(MachineGuarantee.FirstAbility, ability.Receipt.Guarantee);
        var next = context.Preview(ability.State, ownership);
        Assert.IsFalse(next.FirstAbilityGuaranteed);
        Assert.AreEqual(12, next.Outcomes.Length);
        Assert.IsTrue(next.Outcomes.All(o => Math.Abs(o.Probability - 1d / 12) < 1e-12));
        Assert.AreEqual(2, next.Outcomes.Count(o => machine.Definition.Rewards.Single(r => r.RewardId == o.RewardId).Kind == MachineRewardKind.Ability));
    }

    [Test] public void ApprovedMachineCompletesAcrossReloadsForSeventySaturnkensAndBothResourceTotals()
    {
        var store = NewStore();
        var machine = ActiveMachine("flo");
        // The new-account welcome supplies20; this isolated fixture earns the remaining50.
        Assert.AreEqual(GuestVendingStatus.Committed, store.CreditFreeCurrency("test-earned", "test-only", PermanentCurrency.Saturncoins, 50).Status);
        string primary = Path.Combine(directory, "guest-profile.json");
        string beforePreview = File.ReadAllText(primary);
        Assert.AreEqual(GuestVendingStatus.Ready, store.PreviewMachine(characters, machine).Status);
        Assert.AreEqual(beforePreview, File.ReadAllText(primary));
        Assert.IsFalse(store.Reload().OwnsCharacter("flo"));
        Assert.IsEmpty(store.Reload().VendingMachines);

        for (int i = 0; i < 14; i++)
        {
            store = new LocalGuestProfileStore(directory);
            machine = ActiveMachine("flo");
            var draw = store.DrawMachineCore(characters, machine, Request(machine, "approved-draw-" + i, i), () => .999999);
            Assert.AreEqual(GuestVendingStatus.Committed, draw.Status, "draw " + (i + 1) + ": " + draw.Reason);
            var saved = new LocalGuestProfileStore(directory).Reload();
            Assert.AreEqual(70 - (i + 1) * 5, saved.FreeBalance(PermanentCurrency.Saturncoins));
            Assert.AreEqual(i + 1, saved.VendingMachines.Single().State.DrawCount);
            if (i == 9) Assert.AreEqual(MachineGuarantee.CharacterAtTen, draw.Receipt.Draw.Guarantee);
            if (i == 10) Assert.AreEqual(MachineGuarantee.FirstAbility, draw.Receipt.Draw.Guarantee);
        }
        var complete = store.Reload();
        Assert.IsTrue(complete.OwnsCharacter("flo"));
        Assert.AreEqual(3, complete.GetOwnedAbilityIds("flo").Count);
        Assert.AreEqual(140, complete.FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(140, complete.FreeBalance(PermanentCurrency.Suncoins));
        Assert.AreEqual(14, complete.VendingMachines.Single().State.ClaimedRewardIds.Distinct().Count());
        string beforeRetry = File.ReadAllText(primary);
        Assert.AreEqual(GuestVendingStatus.Complete,
            store.DrawMachineCore(characters, machine, Request(machine, "complete-machine", 14), () => 0).Status);
        Assert.AreEqual(GuestVendingStatus.AlreadyApplied,
            store.DrawMachineCore(characters, machine, Request(machine, "approved-draw-0", 0), () => 0).Status);
        Assert.AreEqual(beforeRetry, File.ReadAllText(primary));
    }

    [Test] public void FourSaturnkensCannotDrawAndNeitherPreviewNorConstructionGivesFreeCredit()
    {
        var store = NewStore(historical: true);
        var machine = ActiveMachine("flo");
        Assert.AreEqual(0, store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(GuestVendingStatus.Committed,
            store.CreditFreeCurrency("test-four", "test-only", PermanentCurrency.Saturncoins, 4).Status);
        string primary = Path.Combine(directory, "guest-profile.json");
        string before = File.ReadAllText(primary);
        bool sampled = false;
        var result = store.DrawMachineCore(characters, machine, Request(machine, "cannot-pay", 0), () => { sampled = true; return 0; });
        Assert.AreEqual(GuestVendingStatus.InsufficientFunds, result.Status);
        Assert.IsFalse(sampled);
        Assert.AreEqual(GuestVendingStatus.Ready, store.PreviewMachine(characters, ActiveMachine("flo")).Status);
        Assert.AreEqual(before, File.ReadAllText(primary));
        Assert.IsEmpty(store.Reload().VendingMachines);
        Assert.IsFalse(store.Reload().OwnsCharacter("flo"));
    }

    [Test] public void NewAccountCanPreviewAllApprovedMachinesWithoutDrawingAndOwnedStartersKeepTheirGuarantee()
    {
        var store = NewStore();
        var profile = store.Reload();
        Assert.AreEqual(4, profile.GetOwnedCharacterIds().Count);
        string primary = Path.Combine(directory, "guest-profile.json");
        string before = File.ReadAllText(primary);
        var catalogue = Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue");
        foreach (var entry in catalogue.Machines)
        {
            var preview = store.PreviewMachine(characters, entry.Configuration);
            string id = entry.Configuration.Definition.CharacterId;
            Assert.AreEqual(GuestVendingStatus.Ready, preview.Status, id + ": " + preview.Reason);
            bool owned = profile.OwnsCharacter(id);
            Assert.AreEqual(owned, preview.Preview.FirstAbilityGuaranteed, id);
            Assert.AreEqual(owned ? 0 : 3, preview.Preview.Rewards.Count(r => r.Status == MachineRewardStatus.Locked), id);
            Assert.AreEqual(owned ? 1 : 0, preview.Preview.Rewards.Count(r => r.Status == MachineRewardStatus.Claimed), id);
        }
        Assert.AreEqual(before, File.ReadAllText(primary));
        Assert.IsEmpty(store.Reload().VendingMachines);
        Assert.AreEqual(1, store.Reload().WalletEvents.Count);
        Assert.AreEqual(20, store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
    }

    PlayerMachineConfiguration Create(string id) => economy.Create(characters, "test-approved-" + id, id);
    static PlayerMachineConfiguration ActiveMachine(string id) =>
        Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue").Machines
            .Single(m => m.Configuration.Definition.CharacterId == id).Configuration.Snapshot();
    static MachineDrawRequest Request(PlayerMachineConfiguration machine, string id, long revision) => new MachineDrawRequest {
        RequestId = id, MachineId = machine.Definition.MachineId,
        CatalogVersion = machine.Definition.CatalogVersion, ExpectedRevision = revision
    };
    LocalGuestProfileStore NewStore(bool historical = false)
    {
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchApprovedVendingTests", Guid.NewGuid().ToString("N"));
        if (historical) return GuestHistoricalProfileFixture.CreateVersionSeven(directory, characters);
        var store = new LocalGuestProfileStore(directory);
        store.OpenOrCreate(false);
        store.ChooseStarter(characters, "bugaloo");
        return store;
    }
}

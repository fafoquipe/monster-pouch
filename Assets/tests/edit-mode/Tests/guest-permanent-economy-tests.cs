using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class GuestPermanentEconomyTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig characters;
    string Primary => Path.Combine(directory, "guest-profile.json");
    string Backup => Path.Combine(directory, "guest-profile.backup.json");
    string Ability(int index = 0) => characters.Get("atori").Tricks[index].Id;
    [Serializable] sealed class Envelope { public int Version = 1; public string Payload, Sha256; }

    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchPermanentEconomyTests", Guid.NewGuid().ToString("N"));
        characters = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        store = new LocalGuestProfileStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(characters);
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchPermanentEconomyTests")) + Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(directory).StartsWith(root, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    void Create() { store.OpenOrCreate(false); store.ChooseStarter(characters, "bugaloo"); }
    void Fund(long earth = 100, long sun = 100)
    {
        if (earth > 0) store.CreditFreeCurrency("earth-earned", "test-only", PermanentCurrency.Earthcoins, earth);
        if (sun > 0) store.CreditFreeCurrency("sun-earned", "test-only", PermanentCurrency.Suncoins, sun);
    }
    GuestAbilityPurchaseResult Buy(string request = "buy-one", int index = 0) => store.PurchaseAbility(characters, request, "atori", Ability(index));

    [Test] public void NewIdentityReceivesTwentyBeforeChoiceAndEveryReopenPreservesOneReceipt()
    {
        var created = store.OpenOrCreate(false);
        Assert.AreEqual(20, created.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(GuestOnboardingStage.ChooseStarter, created.OnboardingStage);
        var receipt = created.WalletEvents.Single();
        Assert.AreEqual("welcome:" + created.ProfileId, receipt.Id);
        Assert.AreEqual(GuestPermanentEconomy.WelcomeApprovalId, receipt.SourceId);
        Assert.AreEqual(1, receipt.PermanentPolicyVersion); Assert.AreEqual(0, created.Revision);
        string before = File.ReadAllText(Primary);
        Assert.AreEqual(created.ProfileId, new LocalGuestProfileStore(directory).OpenOrCreate(true).ProfileId);
        Assert.AreEqual(before, File.ReadAllText(Primary));
        store.ChooseStarter(characters, "popow"); store.CompleteTutorialModule(GuestTutorialModules.FirstContact);
        var restored = new LocalGuestProfileStore(directory).OpenOrCreate(false);
        Assert.AreEqual(20, restored.FreeBalance(PermanentCurrency.Saturncoins)); Assert.AreEqual(1, restored.WalletEvents.Count);
        Assert.IsEmpty(restored.AbilityUnlocks);
        foreach (var character in characters.Units)
        { Assert.IsNotNull(character.BaseAbility); Assert.IsEmpty(restored.GetOwnedAbilityIds(character.Id)); }
    }

    [Test] public void HistoricalUnchosenIdentityAndLegacyReceiveNoWelcomeEvenWhenCallerSaysNew()
    {
        store = GuestHistoricalProfileFixture.CreateVersionSeven(directory, characters, null);
        string before = File.ReadAllText(Primary); var old = store.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfile.CurrentVersion, old.Version); Assert.IsEmpty(old.WalletEvents); Assert.AreEqual(before, File.ReadAllText(Primary));
        store.ChooseStarter(characters, "bugaloo"); Assert.IsEmpty(store.Reload().WalletEvents);
        var legacy = new LocalGuestProfileStore(Path.Combine(directory, "legacy"));
        var profile = legacy.OpenOrCreate(true); Assert.IsEmpty(profile.WalletEvents);
        Assert.AreEqual(profile.ProfileId, legacy.OpenOrCreate(false).ProfileId); Assert.IsEmpty(legacy.Reload().WalletEvents);
    }

    [Test] public void CreationLockFailureLeavesNoPartialIdentityAndRetryCreatesExactlyOneWelcome()
    {
        Directory.CreateDirectory(directory);
        using (new FileStream(Path.Combine(directory, "guest-profile.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => store.OpenOrCreate(false));
        Assert.IsFalse(File.Exists(Primary));
        Assert.AreEqual(20, store.OpenOrCreate(false).FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(1, store.Reload().WalletEvents.Count);
    }

    [Test] public void TwentyWelcomeCreditsFundFourRealApprovedDrawsAndNoFifth()
    {
        Create(); var machine = Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue").Machines
            .Single(m => m.Configuration.Definition.CharacterId == "flo").Configuration;
        for (int i = 0; i < 4; i++)
        {
            var draw = store.DrawMachineCore(characters, machine, new MachineDrawRequest {
                RequestId = "welcome-draw-" + i, MachineId = machine.Definition.MachineId,
                CatalogVersion = machine.Definition.CatalogVersion, ExpectedRevision = i
            }, () => .99999);
            Assert.AreEqual(GuestVendingStatus.Committed, draw.Status);
            Assert.AreEqual(20 - 5 * (i + 1), store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        }
        Assert.AreEqual(GuestVendingStatus.InsufficientFunds, store.DrawMachineCore(characters, machine, new MachineDrawRequest {
            RequestId = "fifth", MachineId = machine.Definition.MachineId, CatalogVersion = machine.Definition.CatalogVersion, ExpectedRevision = 4
        }, () => { Assert.Fail("Insufficient funds must not sample."); return 0; }).Status);
    }

    [TestCase(100, 99)] [TestCase(99, 100)] [TestCase(0, 100)] [TestCase(100, 0)]
    public void BothCurrenciesAreRequiredWithoutPartialDebit(long earth, long sun)
    {
        Create(); Fund(earth, sun); string before = File.ReadAllText(Primary);
        Assert.AreEqual(GuestAbilityPurchaseStatus.InsufficientFunds, store.PreviewAbilityPurchase(characters, "atori", Ability()).Status);
        Assert.AreEqual(GuestAbilityPurchaseStatus.InsufficientFunds, Buy().Status);
        Assert.AreEqual(before, File.ReadAllText(Primary)); Assert.IsEmpty(store.Reload().AbilityUnlocks);
    }

    [Test] public void PurchaseCommitsBothDebitsAndStableAbilityOnceAndPreservesBaseAndCombatPrices()
    {
        Create(); Fund(); var definition = characters.Get("atori"); string beforeDefinition = JsonUtility.ToJson(definition);
        string previewBytes = File.ReadAllText(Primary);
        Assert.AreEqual(GuestAbilityPurchaseStatus.Ready, store.PreviewAbilityPurchase(characters, "atori", Ability()).Status);
        Assert.AreEqual(previewBytes, File.ReadAllText(Primary));
        var result = Buy(); Assert.AreEqual(GuestAbilityPurchaseStatus.Purchased, result.Status);
        Assert.AreEqual(5, (int)result.UnlockReceipt.Source); Assert.AreEqual(2, (int)result.WalletReceipt.Kind);
        Assert.AreEqual(result.WalletReceipt.Id, result.UnlockReceipt.ReceiptId);
        Assert.AreEqual(result.WalletReceipt.TimestampUtc, result.UnlockReceipt.TimestampUtc);
        Assert.AreEqual(1, result.WalletReceipt.PermanentPolicyVersion);
        Assert.AreEqual(0, result.Profile.FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(0, result.Profile.FreeBalance(PermanentCurrency.Suncoins));
        Assert.AreEqual(20, result.Profile.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(beforeDefinition, JsonUtility.ToJson(definition));
        string committed = File.ReadAllText(Primary);
        var retry = new LocalGuestProfileStore(directory).PurchaseAbility(characters, "buy-one", "atori", Ability());
        Assert.AreEqual(GuestAbilityPurchaseStatus.AlreadyApplied, retry.Status);
        Assert.AreEqual(JsonUtility.ToJson(result.WalletReceipt), JsonUtility.ToJson(retry.WalletReceipt));
        Assert.AreEqual(GuestAbilityPurchaseStatus.Conflict, Buy(index: 1).Status);
        Assert.AreEqual(GuestAbilityPurchaseStatus.AlreadyOwned, Buy("other-request").Status);
        Assert.AreEqual(committed, File.ReadAllText(Primary));
    }

    [Test] public void GiftBetweenPreviewAndConfirmWinsWithoutAChargeAndPurchasedAbilityLeavesMachinePool()
    {
        Create(); Fund(); Assert.AreEqual(GuestAbilityPurchaseStatus.Ready, store.PreviewAbilityPurchase(characters, "atori", Ability()).Status);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,
            store.UnlockAbility(characters, "approved-gift", "atori", Ability(), GuestUnlockSource.Tutorial));
        string before = File.ReadAllText(Primary); var owned = Buy();
        Assert.AreEqual(GuestAbilityPurchaseStatus.AlreadyOwned, owned.Status); Assert.IsNotNull(owned.Profile);
        Assert.IsNull(owned.WalletReceipt); Assert.AreEqual("approved-gift", owned.UnlockReceipt.ReceiptId);
        Assert.AreEqual(before, File.ReadAllText(Primary));
        Assert.AreEqual(GuestAbilityPurchaseStatus.Purchased, Buy("purchase-second", 1).Status);
        var machine = Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue").Machines
            .Single(m => m.Configuration.Definition.CharacterId == "atori").Configuration;
        var preview = store.PreviewMachine(characters, machine);
        Assert.AreEqual(3, preview.Preview.Rewards.Count(r => r.Status == MachineRewardStatus.Claimed));
        Assert.IsTrue(preview.Preview.FirstAbilityGuaranteed); Assert.IsEmpty(store.Reload().VendingMachines);
    }

    [Test, Platform("Win")] public void FailedSavePublishesNeitherDebitNorAbilityAndSameRequestCanRetry()
    {
        Create(); Fund(); string before = File.ReadAllText(Primary), backup = File.ReadAllText(Backup);
        using (new FileStream(Primary, FileMode.Open, FileAccess.Read, FileShare.Read)) Assert.Throws<IOException>(() => Buy());
        Assert.AreEqual(before, File.ReadAllText(Primary)); Assert.AreEqual(backup, File.ReadAllText(Backup));
        Assert.IsEmpty(store.Reload().AbilityUnlocks); Assert.AreEqual(100, store.Reload().FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(GuestAbilityPurchaseStatus.Purchased, Buy().Status);
    }

    [Test] public void StaleStoreSeesPriorPurchaseAndCannotSpendTheSameFundsAgain()
    {
        Create(); Fund(); var stale = new LocalGuestProfileStore(directory); stale.Reload(); Buy();
        string before = File.ReadAllText(Primary);
        Assert.AreEqual(GuestAbilityPurchaseStatus.InsufficientFunds, stale.PurchaseAbility(characters, "second", "atori", Ability(1)).Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
    }

    [Test] public void ReservedIdsSourcesAndPurchaseSourceCannotBeUsedAsFreeGrants()
    {
        Create(); Fund(); string before = File.ReadAllText(Primary);
        foreach (string id in new[] { "welcome:fake", "ability-buy:fake" })
        {
            Assert.AreEqual(GuestVendingStatus.InvalidRequest, store.CreditFreeCurrency(id, "test", PermanentCurrency.Earthcoins, 100).Status);
            Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest, store.UnlockAbility(characters, id, "atori", Ability(), GuestUnlockSource.Tutorial));
        }
        foreach (string source in new[] { GuestPermanentEconomy.WelcomeApprovalId, GuestPermanentEconomy.AbilityPurchaseApprovalId })
            Assert.AreEqual(GuestVendingStatus.InvalidRequest, store.CreditFreeCurrency("fake", source, PermanentCurrency.Saturncoins, 20).Status);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,
            store.UnlockAbility(characters, "fake", "atori", Ability(), GuestUnlockSource.Purchase));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,
            store.UnlockCharacter(characters, "fake", "flo", GuestUnlockSource.Purchase));
        Assert.AreEqual(GuestAbilityPurchaseStatus.Conflict, Buy("earth-earned").Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
    }

    [Test] public void InvalidTargetBaseAbilityLockedCharacterAndRequestBoundaryCannotDebit()
    {
        Create(); Fund(); string before = File.ReadAllText(Primary);
        Assert.AreEqual(GuestAbilityPurchaseStatus.InvalidRequest, store.PurchaseAbility(characters, "base", "atori", characters.Get("atori").BaseAbility.Id).Status);
        Assert.AreEqual(GuestAbilityPurchaseStatus.CharacterLocked, store.PurchaseAbility(characters, "locked", "flo", characters.Get("flo").Tricks[0].Id).Status);
        Assert.AreEqual(GuestAbilityPurchaseStatus.InvalidRequest, Buy(new string('r', 109)).Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
        Assert.AreEqual(GuestAbilityPurchaseStatus.Purchased, Buy(new string('r', 108)).Status);
        Assert.AreEqual(120, store.Reload().WalletEvents.Last().Id.Length);
    }

    [Test] public void MissingStarterLegacyAndRecoveryDoNotAllowPurchasesOrAnotherWelcome()
    {
        Assert.AreEqual(GuestAbilityPurchaseStatus.MissingProfile, Buy().Status);
        store.OpenOrCreate(false); Assert.AreEqual(GuestAbilityPurchaseStatus.StarterRequired, Buy().Status);
        store.ChooseStarter(characters, "bugaloo"); Fund(); Buy(); File.WriteAllText(Primary, "broken");
        Assert.AreEqual(GuestAbilityPurchaseStatus.RecoveryRequired, Buy().Status);
        var recovered = store.OpenOrCreate(false); Assert.IsTrue(store.NeedsRecovery);
        Assert.AreEqual(20, recovered.FreeBalance(PermanentCurrency.Saturncoins)); Assert.AreEqual("broken", File.ReadAllText(Primary));
        var legacy = new LocalGuestProfileStore(Path.Combine(directory, "legacy")); legacy.OpenOrCreate(true);
        Assert.AreEqual(GuestAbilityPurchaseStatus.LegacyUnavailable, legacy.PurchaseAbility(characters, "legacy", "atori", Ability()).Status);
    }

    [TestCase("amount")] [TestCase("policy")] [TestCase("orphan-wallet")] [TestCase("orphan-unlock")]
    [TestCase("timestamp")] [TestCase("welcome-identity")] [TestCase("welcome-amount")]
    public void TamperedPermanentReceiptOrMissingHalfRequiresRecovery(string mutation)
    {
        Create(); Fund(); Buy(); var profile = store.Reload();
        var purchase = profile.WalletEvents.Last();
        switch (mutation)
        {
            case "amount": purchase.Amounts[0].Amount = -99; break;
            case "policy": purchase.PermanentPolicyVersion = 2; break;
            case "orphan-wallet": profile.WalletEvents.Remove(purchase); break;
            case "orphan-unlock": profile.AbilityUnlocks.Clear(); break;
            case "timestamp": purchase.TimestampUtc = DateTime.UtcNow.AddDays(1).ToString("O"); break;
            case "welcome-identity": profile.WalletEvents[0].Id = "welcome:" + Guid.NewGuid().ToString("N"); break;
            case "welcome-amount": profile.WalletEvents[0].Amounts[0].Amount = 21; break;
        }
        string damaged = Encode(JsonUtility.ToJson(profile)); File.WriteAllText(Primary, damaged);
        Assert.AreEqual(GuestAbilityPurchaseStatus.RecoveryRequired, Buy().Status);
        Assert.AreEqual(damaged, File.ReadAllText(Primary));
    }

    [Test] public void VersionSevenMigrationPreservesExistingReceiptsWithoutWelcomeAndRejectsFutureTransactions()
    {
        store = GuestHistoricalProfileFixture.CreateVersionSeven(directory, characters); Fund();
        store.UnlockAbility(characters, "older-gift", "atori", Ability(2), GuestUnlockSource.Mastery);
        var old = JObject.Parse(JsonUtility.ToJson(store.Reload())); old["Version"] = 7; old.Remove("TrophyLadder");
        string historical = Encode(old.ToString()); File.WriteAllText(Primary, historical);
        var migrated = store.Reload(); Assert.AreEqual(LocalGuestProfile.CurrentVersion, migrated.Version); Assert.AreEqual(0, migrated.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsTrue(migrated.OwnsAbility("atori", Ability(2))); Assert.AreEqual(historical, File.ReadAllText(Primary));
        Buy(); Assert.AreEqual(historical, File.ReadAllText(Backup));
        var smuggled = store.Reload(); smuggled.Version = 7;
        Assert.Throws<InvalidDataException>(() => smuggled.UpgradeSchema());
    }

    [Test] public void JsonUtilityRoundTripsAcceptEmptyInlineMachineObjectsButRejectTheirRealContent()
    {
        // Existing v7 credits may deserialize null inline classes as empty DTOs.
        store = GuestHistoricalProfileFixture.CreateVersionSeven(directory, characters); Fund();
        var historical = store.Reload(); historical.Version = 7;
        foreach (var entry in historical.WalletEvents)
        { entry.Draw = new MachineDrawReceipt(); entry.OwnershipBefore = new MachineOwnership(); }
        File.WriteAllText(Primary, Encode(GuestHistoricalProfileFixture.WithoutTrophies(JsonUtility.ToJson(historical))));
        Assert.AreEqual(100, store.Reload().FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(GuestAbilityPurchaseStatus.Purchased, Buy().Status);
        var current = JsonUtility.FromJson<LocalGuestProfile>(JsonUtility.ToJson(store.Reload()));
        foreach (var entry in current.WalletEvents)
        { entry.Draw = new MachineDrawReceipt(); entry.OwnershipBefore = new MachineOwnership(); }
        Assert.DoesNotThrow(() => current.Validate());
        File.WriteAllText(Primary, Encode(JsonUtility.ToJson(current)));
        Assert.AreEqual(GuestAbilityPurchaseStatus.AlreadyApplied, Buy().Status);
        current.WalletEvents.Last().Draw.RewardId = "unexpected-real-machine-data";
        Assert.Throws<InvalidDataException>(() => current.Validate());
        current.WalletEvents.Last().Draw.RewardId = null;
        current.WalletEvents[0].OwnershipBefore.CharacterOwned = true;
        Assert.Throws<InvalidDataException>(() => current.Validate());
    }

    static string Encode(string payload)
    {
        using (var hash = SHA256.Create()) return JsonUtility.ToJson(new Envelope {
            Payload = payload, Sha256 = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "")
        });
    }
}

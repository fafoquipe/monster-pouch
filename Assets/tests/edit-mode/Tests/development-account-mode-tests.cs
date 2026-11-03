using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class DevelopmentAccountModeTests
{
    string directory;
    MatchConfig catalogue;
    DevelopmentAccountStore accounts;
    static string FixtureRoot =>
#if ERONYA_OFFLINE_HARNESS
        Environment.GetEnvironmentVariable("MP_DEV_ACCOUNT_TEST_ROOT") ?? throw new InvalidOperationException("Missing isolated test root.");
#else
        Path.Combine(Path.GetTempPath(), "MPDev");
#endif
    string Pointer => Path.Combine(directory, DevelopmentAccountStore.FolderName, "active.json");
    string ProfilePath => Path.Combine(accounts.AccountDirectory, "GuestProfile", "guest-profile.json");
    LocalGuestProfileStore GuestStore => new LocalGuestProfileStore(Path.Combine(accounts.AccountDirectory, "GuestProfile"));

    [SetUp] public void Setup()
    {
        directory = Path.Combine(FixtureRoot, Guid.NewGuid().ToString("N").Substring(0, 12));
        Directory.CreateDirectory(directory);
#if ERONYA_OFFLINE_HARNESS
        catalogue = new MatchConfig { Units = GuestProgressionCatalogue.Worlds.SelectMany(w => w.CharacterIds).Select(id => new UnitDefinition {
            Id = id, IsMonster = new[] { "anuik", "bugaloo", "cael", "kazuru", "tauris", "drayven", "korran", "serya", "alveyr", "popow", "sepora" }.Contains(id),
            Tricks = Enumerable.Range(1, 3).Select(i => new TrickDefinition { Id = id + "-" + i }).ToArray() }).ToArray() };
#else
        catalogue = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
#endif
        accounts = new DevelopmentAccountStore(directory);
    }
    [TearDown] public void Cleanup()
    {
#if !ERONYA_OFFLINE_HARNESS
        UnityEngine.Object.DestroyImmediate(catalogue);
#endif
        string allowed = Path.GetFullPath(FixtureRoot) + Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(directory).StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test] public void DefaultDevelopmentOwnsAll44AndAbilitiesWithoutWalletOrCombatChanges()
    {
        var normal = new LocalGuestProfileStore(Path.Combine(directory, "NormalFixture")).OpenOrCreate(false);
        string catalogueBefore = JsonUtility.ToJson(catalogue);
        var profile = accounts.Open(catalogue);
        Assert.AreEqual(44, catalogue.Units.Count(u => u != null));
        CollectionAssert.AreEquivalent(catalogue.Units.Select(u => u.Id), profile.GetOwnedCharacterIds());
        foreach (var unit in catalogue.Units)
            CollectionAssert.AreEquivalent(unit.Tricks.Select(a => a.Id), profile.GetOwnedAbilityIds(unit.Id));
        Assert.AreEqual(DevelopmentAccountMode.AllContent, profile.DevelopmentMode);
        Assert.IsFalse(profile.OnboardingRequired);
        Assert.IsNull(profile.NextTutorialModuleId);
        Assert.IsEmpty(profile.CompletedTutorialModules, "Access is not a forged tutorial receipt.");
        Assert.AreEqual(0, profile.Trophies);
        foreach (PermanentCurrency currency in Enum.GetValues(typeof(PermanentCurrency)))
            Assert.AreEqual(normal.FreeBalance(currency), profile.FreeBalance(currency), currency.ToString());
        Assert.AreEqual(normal.WalletEvents.Count, profile.WalletEvents.Count);
        Assert.AreEqual(catalogueBefore, JsonUtility.ToJson(catalogue));
        string saved = File.ReadAllText(ProfilePath);
        accounts.Open(catalogue);
        Assert.AreEqual(saved, File.ReadAllText(ProfilePath), "Restart/grant is idempotent.");
    }

    [Test] public void FreshResetUsesRealStarterTutorialZeroProgressAndPersistsAcrossRestart()
    {
        accounts.Open(catalogue);
        string previousId = accounts.Profile.ProfileId;
        var profile = accounts.StartFresh(accounts.GenerationId, Guid.NewGuid().ToString("N"), catalogue);
        Assert.AreNotEqual(previousId, profile.ProfileId);
        Assert.AreEqual(DevelopmentAccountMode.Fresh, profile.DevelopmentMode);
        Assert.AreEqual(GuestOnboardingStage.ChooseStarter, profile.OnboardingStage);
        Assert.IsTrue(profile.OnboardingRequired);
        Assert.IsNull(profile.Starter);
        Assert.IsEmpty(profile.GetOwnedCharacterIds());
        Assert.IsEmpty(profile.AbilityUnlocks);
        Assert.IsEmpty(profile.CompletedTutorialModules);
        Assert.IsEmpty(profile.ProgressionClaims);
        Assert.IsEmpty(profile.ProgressionPouches);
        Assert.IsEmpty(profile.Pouches);
        Assert.IsEmpty(profile.TutorialPouches);
        Assert.AreEqual(0, profile.Trophies);
        Assert.AreEqual(GuestPermanentEconomy.WelcomeSaturnkens, profile.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(0, profile.FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(0, profile.FreeBalance(PermanentCurrency.Suncoins));
        Assert.IsFalse(Directory.Exists(Path.Combine(accounts.AccountDirectory, "PlayerProgress")));
        Assert.IsFalse(Directory.Exists(Path.Combine(accounts.AccountDirectory, "PlayerDecks")));
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected, GuestStore.ChooseStarter(catalogue, "popow"));
        var restarted = new DevelopmentAccountStore(directory);
        var after = restarted.Open(catalogue);
        Assert.AreEqual(profile.ProfileId, after.ProfileId);
        Assert.AreEqual(DevelopmentAccountMode.Fresh, after.DevelopmentMode);
        Assert.AreEqual(GuestTutorialModules.FirstContact, after.NextTutorialModuleId);
        CollectionAssert.AreEquivalent(new[] { "popow" }.Concat(StarterRoster.Whelps), after.GetOwnedCharacterIds());
        Assert.AreEqual(GuestOnboardingStage.Tutorial, after.OnboardingStage);
        Assert.IsFalse(AndroidDeveloperContent.IsUnlocked(after));
        string saved = File.ReadAllText(ProfilePath);
        restarted.Open(catalogue);
        Assert.AreEqual(saved, File.ReadAllText(ProfilePath));
    }

    [Test] public void ResetPublishesVerifiedBackupAndPreservesRealAndPreviousGenerations()
    {
        string real = Path.Combine(directory, "GuestProfile", "guest-profile.json");
        Directory.CreateDirectory(Path.GetDirectoryName(real));
        File.WriteAllText(real, "do-not-import-or-touch-real-profile");
        accounts.Open(catalogue);
        string oldDirectory = accounts.AccountDirectory, oldProfile = File.ReadAllText(ProfilePath);
        foreach (string relative in new[] { "PlayerDecks/decks.json", "PlayerProgress/progress.json", "DailyGifts/example.json" })
        {
            string path = Path.Combine(oldDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "backup fixture " + relative);
        }
        accounts.StartFresh(accounts.GenerationId, Guid.NewGuid().ToString("N"), catalogue);
        Assert.AreEqual("do-not-import-or-touch-real-profile", File.ReadAllText(real));
        Assert.AreEqual(oldProfile, File.ReadAllText(Path.Combine(oldDirectory, "GuestProfile", "guest-profile.json")));
        Assert.AreEqual(oldProfile, File.ReadAllText(Path.Combine(accounts.LastBackupDirectory, "GuestProfile", "guest-profile.json")));
        foreach (string relative in new[] { "PlayerDecks/decks.json", "PlayerProgress/progress.json", "DailyGifts/example.json" })
            Assert.AreEqual(File.ReadAllText(Path.Combine(oldDirectory, relative)), File.ReadAllText(Path.Combine(accounts.LastBackupDirectory, relative)));
        Assert.IsTrue(File.Exists(Path.Combine(accounts.LastBackupDirectory, "backup-manifest.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory, DevelopmentAccountStore.FolderName, "active.backup.json")));
        Assert.IsEmpty(Directory.GetFiles(accounts.LastBackupDirectory, "*.lock", SearchOption.AllDirectories));
    }

    [Test] public void ResetRetryIsIdempotentEvenAfterStarterSelectionAndRejectsStaleRequests()
    {
        accounts.Open(catalogue);
        string oldGeneration = accounts.GenerationId, request = Guid.NewGuid().ToString("N");
        var fresh = accounts.StartFresh(oldGeneration, request, catalogue);
        GuestStore.ChooseStarter(catalogue, "bugaloo");
        string bytes = File.ReadAllText(ProfilePath), pointer = File.ReadAllText(Pointer);
        var repeated = accounts.StartFresh(oldGeneration, request, catalogue);
        Assert.AreEqual(fresh.ProfileId, repeated.ProfileId);
        Assert.AreEqual(bytes, File.ReadAllText(ProfilePath));
        Assert.AreEqual(pointer, File.ReadAllText(Pointer));
        Assert.Throws<InvalidOperationException>(() => accounts.StartFresh(oldGeneration, Guid.NewGuid().ToString("N"), catalogue));
        accounts.StartFresh(accounts.GenerationId, Guid.NewGuid().ToString("N"), catalogue);
        pointer = File.ReadAllText(Pointer);
        Assert.Throws<InvalidDataException>(() => accounts.StartFresh(accounts.GenerationId, request, catalogue));
        Assert.AreEqual(pointer, File.ReadAllText(Pointer));
    }

    [Test] public void UnlockAllAfterFreshRetainsIdentityWalletAndProgressAndPersists()
    {
        accounts.Open(catalogue);
        accounts.StartFresh(accounts.GenerationId, Guid.NewGuid().ToString("N"), catalogue);
        GuestStore.ChooseStarter(catalogue, "popow");
        GuestStore.CompleteTutorialModule(GuestTutorialModules.FirstContact);
        var before = GuestStore.Reload();
        var after = accounts.UnlockAll(accounts.GenerationId, catalogue);
        Assert.AreEqual(before.ProfileId, after.ProfileId);
        Assert.AreEqual("popow", after.Starter.Deck.Monster);
        CollectionAssert.AreEqual(before.CompletedTutorialModules, after.CompletedTutorialModules);
        Assert.AreEqual(JsonUtility.ToJson(before.TrophyLadder), JsonUtility.ToJson(after.TrophyLadder));
        CollectionAssert.AreEqual(before.WalletEvents.Select(e => JsonUtility.ToJson(e)), after.WalletEvents.Select(e => JsonUtility.ToJson(e)));
        Assert.IsFalse(after.OnboardingRequired);
        Assert.IsTrue(AndroidDeveloperContent.IsUnlocked(new DevelopmentAccountStore(directory).Open(catalogue)));
    }

    [Test] public void NormalProfilesKeepOnboardingAndFreshPurchasesKeepTheirActualCosts()
    {
        var normal = new LocalGuestProfileStore(Path.Combine(directory, "NormalFixture"));
        Assert.IsTrue(normal.OpenOrCreate(false).OnboardingRequired);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected, normal.ChooseStarter(catalogue, "bugaloo"));
        Assert.AreEqual(GuestTutorialModules.FirstContact, normal.Reload().NextTutorialModuleId);
        accounts.Open(catalogue);
        accounts.StartFresh(accounts.GenerationId, Guid.NewGuid().ToString("N"), catalogue);
        GuestStore.ChooseStarter(catalogue, "bugaloo");
        string ability = catalogue.Get("bugaloo").Tricks[0].Id;
        string before = File.ReadAllText(ProfilePath);
        Assert.AreEqual(GuestAbilityPurchaseStatus.InsufficientFunds, GuestStore.PurchaseAbility(catalogue, "dev-no-free-purchase", "bugaloo", ability).Status);
        Assert.AreEqual(before, File.ReadAllText(ProfilePath));
        accounts.UnlockAll(accounts.GenerationId, catalogue);
        before = File.ReadAllText(ProfilePath);
        Assert.AreEqual(GuestAbilityPurchaseStatus.AlreadyOwned, GuestStore.PurchaseAbility(catalogue, "dev-owned-no-charge", "bugaloo", ability).Status);
        Assert.AreEqual(before, File.ReadAllText(ProfilePath));
    }

    [Test] public void BusyBackupFailsBeforeCommitAndRetryCanSucceed()
    {
        accounts.Open(catalogue);
        string pointer = File.ReadAllText(Pointer), generation = accounts.GenerationId, request = Guid.NewGuid().ToString("N");
        string lockFile = Path.Combine(accounts.AccountDirectory, "PlayerDecks", "decks.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockFile));
        using (var held = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => accounts.StartFresh(generation, request, catalogue));
        Assert.AreEqual(pointer, File.ReadAllText(Pointer));
        Assert.IsFalse(Directory.Exists(Path.Combine(directory, DevelopmentAccountStore.FolderName, "Backups", request)));
        accounts.StartFresh(generation, request, catalogue);
        Assert.AreEqual(request, accounts.GenerationId);
    }

    [Test] public void CorruptPointerOrProfileNeverCreatesReplacementAndPathTraversalIsRejected()
    {
        accounts.Open(catalogue);
        string pointer = File.ReadAllText(Pointer);
        File.WriteAllText(Pointer, "{}");
        Assert.Throws<InvalidDataException>(() => new DevelopmentAccountStore(directory).Open(catalogue));
        Assert.AreEqual("{}", File.ReadAllText(Pointer));
        File.WriteAllText(Pointer, pointer);
        File.WriteAllText(ProfilePath, "{}");
        Assert.Throws<InvalidDataException>(() => accounts.StartFresh(accounts.GenerationId, Guid.NewGuid().ToString("N"), catalogue));
        Assert.AreEqual(pointer, File.ReadAllText(Pointer));
        Assert.AreEqual("{}", File.ReadAllText(ProfilePath));
        Assert.Throws<ArgumentException>(() => accounts.StartFresh(accounts.GenerationId, "../GuestProfile", catalogue));
    }

    [Test] public void FailedPointerCommitKeepsOldAccountAndWillNotReuseABackupAfterNewProgress()
    {
        accounts.Open(catalogue);
        string generation = accounts.GenerationId, request = Guid.NewGuid().ToString("N"), pointer = File.ReadAllText(Pointer);
        using (var held = new FileStream(Pointer, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => accounts.StartFresh(generation, request, catalogue));
        Assert.AreEqual(pointer, File.ReadAllText(Pointer));
        Assert.AreEqual(generation, accounts.GenerationId);
        Assert.AreEqual(GuestTrophyResultStatus.Applied, GuestStore.RecordTrophyResult(new GuestTrophyResultRequest {
            MatchId = Guid.NewGuid().ToString("N"), OpponentId = "fixture", SourceModeId = "online", VerifiedDigest = new string('A', 64), PlayerWins = 3, OpponentWins = 0 }).Status);
        Assert.Throws<InvalidDataException>(() => accounts.StartFresh(generation, request, catalogue));
        Assert.AreEqual(pointer, File.ReadAllText(Pointer));
        string latest = File.ReadAllText(ProfilePath);
        accounts.StartFresh(generation, Guid.NewGuid().ToString("N"), catalogue);
        Assert.AreEqual(latest, File.ReadAllText(Path.Combine(accounts.LastBackupDirectory, "GuestProfile", "guest-profile.json")));
    }
}

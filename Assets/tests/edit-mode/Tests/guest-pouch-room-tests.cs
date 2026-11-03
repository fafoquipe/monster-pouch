using System;
using System.Collections.Generic;
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

public sealed class GuestPouchRoomTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig characters;
    GuestPouchRoomConfiguration configuration;
    DateTime now;
    string Primary => Path.Combine(directory, "guest-profile.json");
    string Backup => Path.Combine(directory, "guest-profile.backup.json");
    long InitialSaturnkens;
    GuestWalletEvent[] PouchWalletEvents => store.Reload().WalletEvents.Where(e => e.Id.StartsWith("pouch-open:", StringComparison.Ordinal)).ToArray();
    [Serializable] sealed class DailyTable { public string Id; public GuestPouchResourceReward[] Rewards; }
    [Serializable] sealed class Envelope { public int Version = 1; public string Payload, Sha256; }

    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchPouchRoomTests", Guid.NewGuid().ToString("N"));
        now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        characters = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var daily = JsonUtility.FromJson<DailyTable>(Resources.Load<TextAsset>("MonsterPouch/UI/Lobby/Header/DailyGifts").text);
        configuration = new GuestPouchRoomConfiguration {
            ApprovalId = "approved-pouch-room-2026-10-24", ResourceTableId = daily.Id, Resources = daily.Rewards
        };
        store = new LocalGuestProfileStore(directory);
        store.OpenOrCreate(false); store.ChooseStarter(characters, "bugaloo");
        InitialSaturnkens = store.Reload().FreeBalance(PermanentCurrency.Saturncoins); Assert.AreEqual(20, InitialSaturnkens);
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(characters);
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchPouchRoomTests")) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(directory); Assert.IsTrue(target.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }

    [Test] public void ReadingInitializesOnlyTodaysCycleAndRepeatedReadsDoNotGrantOrWrite()
    {
        var ready = Read();
        Assert.AreEqual(GuestPouchRoomStatus.Ready, ready.Status);
        Assert.AreEqual("2026-10-24", ready.Snapshot.Day);
        Assert.AreEqual(5, ready.Snapshot.Slots.Length);
        Assert.AreEqual(0, ready.Snapshot.EarnedCount);
        Assert.IsTrue(ready.Snapshot.Slots.All(s => s.Kind == GuestPouchKind.Empty && !s.CanOpen));
        string before = File.ReadAllText(Primary);
        Read(); Read();
        Assert.AreEqual(before, File.ReadAllText(Primary));
        Assert.IsEmpty(PouchWalletEvents); Assert.IsEmpty(store.Reload().PouchVictories);
        Assert.AreEqual(9, configuration.Resources.Length);
        Assert.AreEqual(10000, configuration.Resources.Sum(r => r.Chances));
    }

    [TestCase(7499, GuestPouchKind.Blue)]
    [TestCase(7500, GuestPouchKind.Character)]
    public void FirstThreeAreBlueAndBothRemainingSlotsUseExactSeventyFiveTwentyFiveBoundary(int kindRoll, GuestPouchKind expected)
    {
        for (int i = 0; i < 3; i++)
        {
            var win = Win("blue-" + i, 9999);
            Assert.AreEqual(GuestPouchKind.Blue, win.Snapshot.Slots[i].Kind);
        }
        for (int i = 3; i < 5; i++)
        {
            var win = expected == GuestPouchKind.Blue ? Win("last-" + i, kindRoll, 0) : Win("last-" + i, kindRoll, 0, 50, 0);
            Assert.AreEqual(expected, win.Snapshot.Slots[i].Kind);
            Assert.AreEqual(i + 1, win.Snapshot.EarnedCount);
        }
    }

    [Test] public void RepeatingCyclesAccumulateBeyondFiveWithoutOpeningAndResumeAfterReload()
    {
        for (int i = 0; i < 16; i++)
        {
            bool special = i % 5 >= 3;
            var win = special ? Win("win-" + i, 7500, 0, 9999, 9500) : Win("win-" + i, 9500);
            Assert.AreEqual(special ? GuestPouchKind.Character : GuestPouchKind.Blue, win.Snapshot.Slots[i].Kind);
            Assert.AreEqual(i + 1, win.Snapshot.EarnedCount);
            Assert.AreEqual(i, win.Profile.Pouches.Last().SlotIndex);
            if (i == 7) store = new LocalGuestProfileStore(directory);
        }
        var snapshot = Read().Snapshot;
        Assert.AreEqual(20, snapshot.Slots.Length); Assert.AreEqual(16, snapshot.Slots.Count(s => s.CanOpen));
        Assert.AreEqual(4, snapshot.Slots.Count(s => s.Kind == GuestPouchKind.Empty));
        Assert.IsTrue(store.Reload().PouchVictories.All(r => r.Status == GuestPouchRoomStatus.Granted));
        foreach (var slot in snapshot.Slots.Where(s => s.CanOpen).Reverse())
            Assert.AreEqual(GuestPouchRoomStatus.Opened, Open(slot.PouchId, "open-" + slot.Index).Status);
        Assert.AreEqual(16, store.Reload().PouchOpenings.Count);
        Assert.AreEqual(InitialSaturnkens + 80, store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsTrue(Read().Snapshot.Slots.Where(s => s.Kind != GuestPouchKind.Empty).All(s => s.Opened && !s.CanOpen));
        Assert.AreEqual(17, Win("after-all-opened", 0).Snapshot.EarnedCount);
        var original = Request("win-5");
        now = now.AddDays(1);
        var repeated = store.RecordPouchVictoryCore(characters, configuration, original, now, _ => throw new Exception("No retry draw."));
        Assert.AreEqual(GuestPouchRoomStatus.AlreadyApplied, repeated.Status);
        Assert.AreEqual(0, repeated.Snapshot.EarnedCount);
        Assert.AreEqual(1, Win("new-day-win", 0).Snapshot.EarnedCount);
    }

    [Test] public void HistoricalOpeningsAndCappedVictoriesArePreservedButUnopenedPouchesUseTheNewRule()
    {
        // Recreate the old persisted contract: a roll of 100 lost at the old 50/10000 threshold.
        string openedId = CharacterPouch(3000, 9500);
        Assert.AreEqual(GuestPouchRewardKind.Resources, Open(openedId, "historical-open").OpenReceipt.RewardKind);
        int flo = Array.IndexOf(Eligible(), "flo"); Assert.GreaterOrEqual(flo, 0);
        string unopenedId = Win("historical-unopened", 7500, flo, 2999, 0).VictoryReceipt.PouchId;
        var payload = JObject.Parse(JsonUtility.ToJson(store.Reload()));
        ((JArray)payload["Pouches"]).OfType<JObject>().Single(p => (string)p["Id"] == openedId)["CharacterRoll"] = 100;
        foreach (JObject receipt in (JArray)payload["PouchOpenings"]) receipt.Remove("RewardRulesVersion");
        ((JArray)payload["PouchVictories"]).Add(JObject.FromObject(new GuestPouchVictoryReceipt {
            MatchId = "historical-cap", SourceModeId = "bot", CompletedUtc = now.ToString("O"), RecordedUtc = now.ToString("O"),
            Day = "2026-10-24", Status = GuestPouchRoomStatus.DailyLimitReached
        }));
        string historical = Encode(payload.ToString()); File.WriteAllText(Primary, historical);
        store = new LocalGuestProfileStore(directory);
        Assert.AreEqual(GuestPouchRoomStatus.Ready, Read().Status);
        Assert.AreEqual(historical, File.ReadAllText(Primary), "Reading must not reroll, grant, or rewrite old receipts.");
        Assert.AreEqual(0, store.Reload().PouchOpenings.Single().RewardRulesVersion);
        var replay = Open(openedId, "retry-historical");
        Assert.AreEqual(GuestPouchRoomStatus.AlreadyApplied, replay.Status);
        Assert.AreEqual(GuestPouchRewardKind.Resources, replay.OpenReceipt.RewardKind);
        Assert.AreEqual(InitialSaturnkens + 5, store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(GuestPouchRoomStatus.AlreadyApplied, store.RecordPouchVictory(characters, configuration, Request("historical-cap"), now).Status);
        Assert.AreEqual(5, store.Reload().Pouches.Count, "Old capped wins do not receive retroactive pouches.");
        Assert.AreEqual(6, Win("new-sixth", 0).Snapshot.EarnedCount);
        var prize = Open(unopenedId, "new-rule-open");
        Assert.AreEqual(GuestPouchRewardKind.Character, prize.OpenReceipt.RewardKind);
        Assert.AreEqual(1, prize.OpenReceipt.RewardRulesVersion);
        Assert.AreEqual(2999, store.Reload().Pouches.Single(p => p.Id == unopenedId).CharacterRoll);
        Assert.AreEqual(100, store.Reload().Pouches.Single(p => p.Id == openedId).CharacterRoll);
        Assert.IsTrue(store.Reload().OwnsCharacter("flo"));
        Assert.AreEqual(1, PouchWalletEvents.Length); Assert.AreEqual(2, store.Reload().PouchOpenings.Count);
    }

    [TestCase(7499, GuestPouchKind.Blue)] [TestCase(7500, GuestPouchKind.Character)]
    public void EveryFourthAndFifthPositionRepeatsTheSameKindBoundary(int roll, GuestPouchKind expected)
    {
        for (int i = 0; i < 15; i++)
        {
            var result = i % 5 < 3 ? Win("cycle-" + i, 0) : expected == GuestPouchKind.Blue ?
                Win("cycle-" + i, roll, 0) : Win("cycle-" + i, roll, 0, 9999, 0);
            Assert.AreEqual(i % 5 < 3 ? GuestPouchKind.Blue : expected, result.Snapshot.Slots[i].Kind);
        }
    }

    [Test] public void UnknownOpeningRulesRequireRecoveryInsteadOfReinterpretingACommittedReward()
    {
        string id = Win("invalid-rule", 0).VictoryReceipt.PouchId; Open(id, "invalid-rule-open");
        var payload = JObject.Parse(JsonUtility.ToJson(store.Reload()));
        payload["PouchOpenings"][0]["RewardRulesVersion"] = 2;
        string damaged = Encode(payload.ToString()); File.WriteAllText(Primary, damaged);
        Assert.AreEqual(GuestPouchRoomStatus.RecoveryRequired, Read().Status);
        Assert.AreEqual(damaged, File.ReadAllText(Primary));
    }

    [TestCase(false)] [TestCase(true)]
    public void CharacterSelectionCanReachEitherEndOfTheCurrentUnownedRosterAndKeepsItsId(bool last)
    {
        for (int i = 0; i < 3; i++) Win("before-" + i, 0);
        var expected = Eligible();
        int index = last ? expected.Length - 1 : 0;
        var samples = new Queue<int>(new[] { 7500, index, 50, 0 });
        var maxima = new List<int>();
        var result = store.RecordPouchVictoryCore(characters, configuration, Request("target"), now,
            max => { maxima.Add(max); return samples.Dequeue(); });
        Assert.AreEqual(GuestPouchRoomStatus.Granted, result.Status);
        CollectionAssert.AreEqual(new[] { 10000, expected.Length, 10000, 10000 }, maxima);
        Assert.AreEqual(expected[index], result.Snapshot.Slots[3].CharacterId);
        Assert.IsFalse(store.Reload().OwnsCharacter(expected[index]));
        var reloaded = new LocalGuestProfileStore(directory).ReadPouchRoom(characters, configuration, now);
        Assert.AreEqual(expected[index], reloaded.Snapshot.Slots[3].CharacterId);
        Assert.AreEqual(result.Snapshot.Slots[3].PouchId, reloaded.Snapshot.Slots[3].PouchId);
    }

    [TestCase(2999, GuestPouchRewardKind.Character)]
    [TestCase(3000, GuestPouchRewardKind.Resources)]
    public void CharacterOpeningHasExactThirtyPercentBoundaryAndUsesSameDailyResourceTable(int characterRoll, GuestPouchRewardKind expected)
    {
        string id = CharacterPouch(characterRoll, 9500);
        var opened = Open(id, "open-target");
        Assert.AreEqual(GuestPouchRoomStatus.Opened, opened.Status);
        Assert.AreEqual(expected, opened.OpenReceipt.RewardKind);
        Assert.AreEqual(expected == GuestPouchRewardKind.Character, store.Reload().OwnsCharacter("flo"));
        Assert.AreEqual(InitialSaturnkens + (expected == GuestPouchRewardKind.Resources ? 5 : 0), store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(0, store.Reload().GetOwnedAbilityIds("flo").Count);
        if (expected == GuestPouchRewardKind.Character)
        {
            var machine = Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue").Machines.Single(m => m.Configuration.Definition.CharacterId == "flo");
            Assert.IsTrue(store.PreviewMachine(characters, machine.Configuration).Preview.FirstAbilityGuaranteed);
            Assert.AreEqual("flo", opened.OpenReceipt.CharacterId);
        }
    }

    [Test] public void CharacterObtainedBeforeOpeningFallsBackToResourcesWithoutAnotherUnlock()
    {
        string id = CharacterPouch(0, 9500);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,
            store.UnlockCharacter(characters, "other-channel", "flo", GuestUnlockSource.NormalPouch));
        var opened = Open(id, "after-external");
        Assert.AreEqual(GuestPouchRewardKind.Resources, opened.OpenReceipt.RewardKind);
        Assert.IsTrue(opened.OpenReceipt.CharacterOwnedBefore);
        Assert.AreEqual(1, store.Reload().CharacterUnlocks.Count(r => r.CharacterId == "flo"));
        Assert.AreEqual(InitialSaturnkens + 5, store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
    }

    [Test] public void CompleteCollectionTurnsACharacterSlotIntoBlueResourcesWithoutChoosingInvalidTarget()
    {
        foreach (string id in Eligible())
            Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,
                store.UnlockCharacter(characters, "complete-" + id, id, GuestUnlockSource.NormalPouch));
        for (int i = 0; i < 3; i++) Win("blue-" + i, 0);
        var fourth = Win("complete-collection", 7500, 9500);
        Assert.AreEqual(GuestPouchKind.Blue, fourth.Snapshot.Slots[3].Kind);
        Assert.IsTrue(string.IsNullOrEmpty(fourth.Snapshot.Slots[3].CharacterId));
        Assert.AreEqual(GuestPouchRewardKind.Resources, Open(fourth.VictoryReceipt.PouchId, "complete-open").OpenReceipt.RewardKind);
        Assert.AreEqual(InitialSaturnkens + 5, store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
    }

    [Test] public void VictoryIsIdempotentAndRequiresACompleteWinningMatchWithConsistentIdentity()
    {
        Win("same-match", 0);
        string before = File.ReadAllText(Primary);
        var repeated = store.RecordPouchVictoryCore(characters, configuration, Request("same-match"), now, _ => throw new Exception("No retry roll."));
        Assert.AreEqual(GuestPouchRoomStatus.AlreadyApplied, repeated.Status);
        var changed = Request("same-match"); changed.SourceModeId = "online";
        Assert.AreEqual(GuestPouchRoomStatus.Conflict, store.RecordPouchVictory(characters, configuration, changed, now).Status);
        changed = Request("round-only"); changed.Completed = false;
        Assert.AreEqual(GuestPouchRoomStatus.Ineligible, store.RecordPouchVictory(characters, configuration, changed, now).Status);
        changed = Request("defeat"); changed.Won = false;
        Assert.AreEqual(GuestPouchRoomStatus.Ineligible, store.RecordPouchVictory(characters, configuration, changed, now).Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
    }

    [Test] public void TomorrowDiscardsUnopenedPouchesAndClockRollbackCannotRestoreThem()
    {
        string old = Win("yesterday", 0).VictoryReceipt.PouchId;
        now = now.AddDays(1); var today = Read();
        Assert.AreEqual(0, today.Snapshot.EarnedCount);
        Assert.AreEqual(GuestPouchRoomStatus.Expired, Open(old, "expired-open").Status);
        string before = File.ReadAllText(Primary);
        Assert.AreEqual(GuestPouchRoomStatus.ClockRollback, store.ReadPouchRoom(characters, configuration, now.AddDays(-1)).Status);
        Assert.AreEqual(GuestPouchRoomStatus.ClockRollback, store.OpenPouch(characters, configuration, old, "clock-open", now.AddDays(-1)).Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
        Assert.IsEmpty(store.Reload().PouchOpenings); Assert.IsEmpty(PouchWalletEvents);
        Assert.AreEqual(1, Win("today", 0).Snapshot.EarnedCount);
    }

    [Test] public void DelayedVictoryNeverTransfersAnOpportunityIntoANewDay()
    {
        var yesterday = Request("delayed"); now = now.AddDays(1);
        var late = store.RecordPouchVictoryCore(characters, configuration, yesterday, now, _ => throw new Exception("No expired victory roll."));
        Assert.AreEqual(GuestPouchRoomStatus.Expired, late.Status);
        Assert.AreEqual(0, late.Snapshot.EarnedCount);
        Assert.IsEmpty(store.Reload().Pouches);
        Assert.AreEqual(GuestPouchRoomStatus.AlreadyApplied, store.RecordPouchVictory(characters, configuration, yesterday, now).Status);
    }

    [Test] public void OpeningReplayReturnsOriginalPrizeAndRequestCannotTargetAnotherPouch()
    {
        string first = Win("first", 9500).VictoryReceipt.PouchId;
        string second = Win("second", 0).VictoryReceipt.PouchId;
        var reward = Open(first, "request"); string before = File.ReadAllText(Primary);
        store = new LocalGuestProfileStore(directory);
        Assert.AreEqual(GuestPouchRoomStatus.AlreadyApplied, Open(first, "request").Status);
        var newRequest = Open(first, "another-request");
        Assert.AreEqual(GuestPouchRoomStatus.AlreadyApplied, newRequest.Status);
        Assert.AreEqual(JsonUtility.ToJson(reward.OpenReceipt), JsonUtility.ToJson(newRequest.OpenReceipt));
        Assert.AreEqual(GuestPouchRoomStatus.Conflict, Open(second, "request").Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
        Assert.AreEqual(InitialSaturnkens + 5, store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(1, store.Reload().PouchOpenings.Count);
    }

    [TestCase(false)] [TestCase(true)] [Platform("Win")]
    public void FailedOpeningReplaceRollsBackConsumptionAndPrizeAndReloadCannotReroll(bool character)
    {
        string id = CharacterPouch(character ? 0 : 3000, 9500);
        string before = File.ReadAllText(Primary), backup = File.ReadAllText(Backup);
        using (new FileStream(Primary, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => Open(id, "failed-request"));
        Assert.AreEqual(before, File.ReadAllText(Primary)); Assert.AreEqual(backup, File.ReadAllText(Backup));
        store = new LocalGuestProfileStore(directory);
        Assert.IsEmpty(store.Reload().PouchOpenings); Assert.IsEmpty(PouchWalletEvents);
        Assert.IsFalse(store.Reload().OwnsCharacter("flo"));
        var retry = Open(id, "new-request-after-reload");
        Assert.AreEqual(GuestPouchRoomStatus.Opened, retry.Status);
        Assert.AreEqual(character ? GuestPouchRewardKind.Character : GuestPouchRewardKind.Resources, retry.OpenReceipt.RewardKind);
        Assert.AreEqual(character, store.Reload().OwnsCharacter("flo"));
        Assert.AreEqual(InitialSaturnkens + (character ? 0 : 5), store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Test, Platform("Win")] public void FailedAcquisitionOrDayResetDoesNotPublishInventoryOrDiscardBeforeCommit()
    {
        string before = File.ReadAllText(Primary);
        using (new FileStream(Primary, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => Win("retry-win", 0));
        Assert.AreEqual(before, File.ReadAllText(Primary)); Assert.IsEmpty(store.Reload().Pouches);
        string id = Win("retry-win", 0).VictoryReceipt.PouchId;
        before = File.ReadAllText(Primary); now = now.AddDays(1);
        using (new FileStream(Primary, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => Read());
        Assert.AreEqual(before, File.ReadAllText(Primary));
        Assert.AreEqual("2026-10-24", store.Reload().PouchRoomDay);
        Read(); Assert.AreEqual(GuestPouchRoomStatus.Expired, Open(id, "too-late").Status);
    }

    [Test] public void AcquiredPouchRetainsItsResourceSnapshotAfterConfigurationChanges()
    {
        string id = Win("snapshot", 0).VictoryReceipt.PouchId;
        configuration.Resources[0].Amount = 999;
        var result = Open(id, "snapshot-open");
        Assert.AreEqual(GuestPouchRoomStatus.Opened, result.Status);
        Assert.AreEqual(10, result.OpenReceipt.Amounts.Single().Amount);
        Assert.AreEqual(10, store.Reload().FreeBalance(PermanentCurrency.Earthcoins));
    }

    [Test] public void OpeningRequestLengthReservesRoomForItsWalletPrefix()
    {
        string id = Win("length", 0).VictoryReceipt.PouchId;
        string before = File.ReadAllText(Primary);
        Assert.AreEqual(GuestPouchRoomStatus.InvalidRequest, Open(id, new string('x', 110)).Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
        Assert.AreEqual(GuestPouchRoomStatus.Opened, Open(id, new string('x', 109)).Status);
        Assert.AreEqual(120, PouchWalletEvents.Single().Id.Length);
    }

    [Test] public void InvalidConfigurationAndRandomSampleCannotConsumeAnOpportunity()
    {
        string before = File.ReadAllText(Primary);
        Assert.AreEqual(GuestPouchRoomStatus.NeedsConfiguration, store.ReadPouchRoom(characters, null, now).Status);
        Assert.AreEqual(GuestPouchRoomStatus.InvalidRequest,
            store.RecordPouchVictoryCore(characters, configuration, Request("invalid-roll"), now, _ => 10000).Status);
        var future = Request("future"); future.CompletedUtc = now.AddDays(1);
        Assert.AreEqual(GuestPouchRoomStatus.InvalidRequest, store.RecordPouchVictory(characters, configuration, future, now).Status);
        Assert.AreEqual(before, File.ReadAllText(Primary));
    }

    [Test] public void VersionSixMigratesWithoutGiftsAndPreservesWalletCharacterAndMachineReceipts()
    {
        store = GuestHistoricalProfileFixture.CreateVersionSeven(directory, characters);
        store.CreditFreeCurrency("old-earned", "test-only", PermanentCurrency.Saturncoins, 10);
        var machine = Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue").Machines.Single(m => m.Configuration.Definition.CharacterId == "flo").Configuration;
        Assert.AreEqual(GuestVendingStatus.Committed, store.DrawMachineCore(characters, machine, new MachineDrawRequest {
            RequestId = "old-machine", MachineId = machine.Definition.MachineId, CatalogVersion = machine.Definition.CatalogVersion, ExpectedRevision = 0
        }, () => 0).Status);
        var profile = store.Reload(); var payload = JObject.Parse(JsonUtility.ToJson(profile)); payload["Version"] = 6; payload.Remove("TrophyLadder");
        foreach (string field in new[] { "PouchRoomDay", "PouchVictories", "Pouches", "PouchOpenings" }) payload.Remove(field);
        string old = Encode(payload.ToString()); File.WriteAllText(Primary, old);
        var migrated = store.Reload();
        Assert.AreEqual(LocalGuestProfile.CurrentVersion, migrated.Version); Assert.AreEqual(profile.ProfileId, migrated.ProfileId);
        Assert.AreEqual(profile.Revision, migrated.Revision); Assert.AreEqual(5, migrated.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsTrue(migrated.OwnsCharacter("flo")); Assert.AreEqual(1, migrated.VendingMachines.Single().State.DrawCount);
        Assert.IsEmpty(migrated.Pouches); Assert.IsEmpty(migrated.PouchVictories); Assert.IsEmpty(migrated.PouchOpenings);
        Assert.AreEqual(old, File.ReadAllText(Primary));
        Read(); Assert.AreEqual(old, File.ReadAllText(Backup)); Assert.IsEmpty(store.Reload().Pouches);
    }

    [TestCase("PouchVictories")] [TestCase("Pouches")] [TestCase("PouchOpenings")] [TestCase("PouchRoomDay")]
    public void MissingCurrentSchemaRecordsRequireRecoveryRatherThanSilentlyLosingInventory(string field)
    {
        Win("persisted", 0);
        var payload = JObject.Parse(JsonUtility.ToJson(store.Reload())); payload.Remove(field);
        string damaged = Encode(payload.ToString()); File.WriteAllText(Primary, damaged);
        Assert.AreEqual(GuestPouchRoomStatus.RecoveryRequired, Read().Status);
        Assert.AreEqual(damaged, File.ReadAllText(Primary));
    }

    [Test] public void LegacyProfileCannotGainPouchesAndCorruptPrimaryCannotReplayAnOlderGeneration()
    {
        Win("primary", 0); File.WriteAllText(Primary, "corrupt");
        Assert.AreEqual(GuestPouchRoomStatus.RecoveryRequired, Read().Status);
        Assert.AreEqual(GuestPouchRoomStatus.RecoveryRequired, store.RecordPouchVictory(characters, configuration, Request("recover"), now).Status);
        Assert.AreEqual("corrupt", File.ReadAllText(Primary));
        string legacyDirectory = Path.Combine(directory, "legacy"); var legacy = new LocalGuestProfileStore(legacyDirectory);
        legacy.OpenOrCreate(true); string legacyPath = Path.Combine(legacyDirectory, "guest-profile.json"), before = File.ReadAllText(legacyPath);
        Assert.AreEqual(GuestPouchRoomStatus.LegacyUnavailable, legacy.ReadPouchRoom(characters, configuration, now).Status);
        Assert.AreEqual(GuestPouchRoomStatus.LegacyUnavailable, legacy.RecordPouchVictory(characters, configuration, Request("legacy"), now).Status);
        Assert.AreEqual(before, File.ReadAllText(legacyPath));
    }

    GuestPouchRoomResult Read() => store.ReadPouchRoom(characters, configuration, now);
    GuestPouchRoomResult Open(string pouch, string request) => store.OpenPouch(characters, configuration, pouch, request, now);
    GuestPouchVictoryRequest Request(string id) => new GuestPouchVictoryRequest {
        MatchId = id, SourceModeId = "bot", Completed = true, Won = true, CompletedUtc = now
    };
    GuestPouchRoomResult Win(string id, params int[] rolls)
    {
        var queue = new Queue<int>(rolls);
        var result = store.RecordPouchVictoryCore(characters, configuration, Request(id), now, max => {
            Assert.IsNotEmpty(queue, "Unexpected random sample."); int value = queue.Dequeue(); Assert.Less(value, max); return value;
        });
        Assert.AreEqual(GuestPouchRoomStatus.Granted, result.Status, result.Reason); Assert.IsEmpty(queue);
        return result;
    }
    string[] Eligible()
    {
        var profile = store.Reload();
        return characters.Units.Where(u => !profile.OwnsCharacter(u.Id)).Select(u => u.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }
    string CharacterPouch(int characterRoll, int resourceRoll)
    {
        for (int i = 0; i < 3; i++) Win("prefix-" + i, 0);
        int index = Array.IndexOf(Eligible(), "flo"); Assert.GreaterOrEqual(index, 0);
        var result = Win("target-pouch", 7500, index, characterRoll, resourceRoll);
        Assert.AreEqual("flo", result.Snapshot.Slots[3].CharacterId);
        return result.VictoryReceipt.PouchId;
    }
    static string Encode(string payload)
    {
        using (var hash = SHA256.Create()) return JsonUtility.ToJson(new Envelope {
            Payload = payload, Sha256 = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "")
        });
    }
}

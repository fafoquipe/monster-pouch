using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class LocalInventoryTests
    {
        string directory;
        LocalQAProfileStore store;
        EconomyDevelopmentConfig settings;
        MatchConfig catalogue;
        UnitDefinition first, second;
        string Primary => Path.Combine(directory, "qa-profile.json");

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "MonsterPouchInventoryTests", Guid.NewGuid().ToString("N"));
            settings = ScriptableObject.CreateInstance<EconomyDevelopmentConfig>();
            settings.EnableLocalQA = true;
            var asset = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            Assert.IsNotNull(asset);
            catalogue = asset.CreateRuntimeConfig();
            first = catalogue.Get("atori");
            second = catalogue.Get("anuik");
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            store = new LocalQAProfileStore(directory);
            store.OpenOrCreate("unchanged legacy progress");
        }

        [TearDown] public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(catalogue);
            string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchInventoryTests")) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(directory);
            Assert.IsTrue(target.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }

        LocalQAProfileStore.InventoryResult Grant(string receipt, string characterId, string abilityId = null,
            InventoryRewardSource source = InventoryRewardSource.InternalQA) =>
            store.GrantInventory(settings, catalogue, receipt, source, characterId, abilityId);

        [Test] public void ActualCatalogueIdsPersistAndDisplayRenamesDoNotChangeOwnership()
        {
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied, Grant("character", first.Id));
            foreach (var ability in first.Tricks)
                Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied, Grant(ability.Id, first.Id, ability.Id));
            first.DisplayName = "A renamed display label";
            foreach (var ability in first.Tricks) ability.Name = "Another display label";
            var loaded = new LocalQAProfileStore(directory).OpenOrCreate();
            Assert.IsTrue(loaded.OwnsCharacter(first.Id));
            Assert.AreEqual(3, loaded.OwnedAbilities.Count);
            foreach (var ability in first.Tricks) Assert.IsTrue(loaded.OwnsAbility(first.Id, ability.Id));
            Assert.AreEqual(4, loaded.InventoryReceipts.Count);
            Assert.AreEqual("unchanged legacy progress", loaded.LegacyProgressJson);
            Assert.IsEmpty(loaded.Transactions);
            Assert.IsEmpty(loaded.Balances);
        }

        [Test] public void CrossSourceOwnershipAndReceiptRetriesNeverGrantAnAbilityTwice()
        {
            Grant("character", first.Id);
            var ability = first.Tricks[0];
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied,
                Grant("mastery-receipt", first.Id, ability.Id, InventoryRewardSource.Mastery));
            string before = File.ReadAllText(Primary);
            foreach (var source in new[] { InventoryRewardSource.Machine, InventoryRewardSource.NormalPouch, InventoryRewardSource.Tutorial })
                Assert.AreEqual(LocalQAProfileStore.InventoryResult.AlreadyOwned,
                    Grant("another-source:" + source, first.Id, ability.Id, source));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.AlreadyApplied,
                Grant("mastery-receipt", first.Id, ability.Id, InventoryRewardSource.Mastery));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Conflict,
                Grant("mastery-receipt", first.Id, first.Tricks[1].Id, InventoryRewardSource.Mastery));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Conflict,
                Grant("mastery-receipt", first.Id, ability.Id, InventoryRewardSource.Machine));
            Assert.AreEqual(before, File.ReadAllText(Primary));
        }

        [Test] public void StarterCharactersAreAlreadyOwnedWithoutAnotherReceiptOrCharge()
        {
            Assert.AreEqual(LocalQAProfileStore.Result.Applied, store.ChooseStarter(settings, catalogue, "bugaloo"));
            var before = store.OpenOrCreate();
            Assert.AreEqual(4, before.OwnedCharacterIds.Count);
            string bytes = File.ReadAllText(Primary);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.AlreadyOwned,
                Grant("machine-character", first.Id, null, InventoryRewardSource.Machine));
            Assert.AreEqual(bytes, File.ReadAllText(Primary));
            Assert.IsEmpty(store.OpenOrCreate().InventoryReceipts);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied,
                Grant("mastery-ability", first.Id, first.Tricks[0].Id, InventoryRewardSource.Mastery));
        }

        [Test] public void LockedCharacterAndInvalidRewardsDoNotChangeAnySavedRecords()
        {
            string bytes = File.ReadAllText(Primary);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.CharacterLocked, Grant("locked", first.Id, first.Tricks[0].Id));
            foreach (string invalid in new[] { first.BaseAbility.Id, second.Tricks[0].Id, "missing-upgrade", " " })
                Assert.AreEqual(LocalQAProfileStore.InventoryResult.InvalidReward, Grant("invalid", first.Id, invalid));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.InvalidReward, Grant("bad-character", "missing-character"));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.InvalidReward, Grant(" ", first.Id));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.InvalidReward,
                Grant("source", first.Id, null, (InventoryRewardSource)99));
            Assert.AreEqual(bytes, File.ReadAllText(Primary));
        }

        [Test] public void DisabledQaDoesNotGrantKnownRewards()
        {
            settings.EnableLocalQA = false;
            string bytes = File.ReadAllText(Primary);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Disabled, Grant("disabled", first.Id));
            Assert.AreEqual(bytes, File.ReadAllText(Primary));
        }

        [Test] public void TwoStoreInstancesReloadBeforeGrantingAndKeepBothInventories()
        {
            var stale = new LocalQAProfileStore(directory);
            stale.OpenOrCreate();
            Grant("first", first.Id);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied,
                stale.GrantInventory(settings, catalogue, "second", InventoryRewardSource.InternalQA, second.Id));
            Grant("first-skill", first.Id, first.Tricks[0].Id);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied,
                stale.GrantInventory(settings, catalogue, "second-skill", InventoryRewardSource.Mastery, second.Id, second.Tricks[0].Id));
            var loaded = store.OpenOrCreate();
            CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, loaded.OwnedCharacterIds);
            Assert.AreEqual(2, loaded.OwnedAbilities.Count);
            Assert.AreEqual(4, loaded.InventoryReceipts.Count);
        }

        [Test] public void CorruptedPrimaryCannotGrantFromAnOlderReadableBackup()
        {
            Grant("character", first.Id);
            File.WriteAllText(Primary, "corrupted inventory");
            Assert.IsEmpty(store.OpenOrCreate().OwnedCharacterIds);
            Assert.IsTrue(store.NeedsRecovery);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.RecoveryRequired, Grant("character", first.Id));
            Assert.AreEqual("corrupted inventory", File.ReadAllText(Primary));
        }

        [Test] public void ExclusiveWriterFailureLeavesTheGrantRetryableExactlyOnce()
        {
            using (new FileStream(Path.Combine(directory, "qa-profile.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Throws<IOException>(() => Grant("character", first.Id));
            Assert.IsEmpty(store.OpenOrCreate().OwnedCharacterIds);
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied, Grant("character", first.Id));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.AlreadyApplied, Grant("character", first.Id));
            Assert.AreEqual(1, store.OpenOrCreate().InventoryReceipts.Count);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void OlderSchemasMigrateWithoutRewritingUntilAValidGrant(int version)
        {
            store.GrantWelcome(settings);
            var original = store.OpenOrCreate();
            var legacy = new LegacyProfile {
                Version = version, ProfileId = original.ProfileId, Environment = original.Environment,
                Revision = original.Revision, LegacyProgressJson = original.LegacyProgressJson,
                OwnedCharacterIds = new List<string> { first.Id }, Balances = original.Balances,
                Transactions = original.Transactions
            };
            string bytes = Encode(JsonUtility.ToJson(legacy));
            File.WriteAllText(Primary, bytes);
            var restored = new LocalQAProfileStore(directory).OpenOrCreate();
            Assert.AreEqual(LocalEconomyProfile.CurrentVersion, restored.Version);
            Assert.IsTrue(restored.OwnsCharacter(first.Id));
            Assert.IsEmpty(restored.OwnedAbilities);
            Assert.IsEmpty(restored.InventoryReceipts);
            Assert.AreEqual(1, restored.Balance(PermanentCurrency.Saturncoins, CreditOrigin.InternalQA));
            Assert.AreEqual(bytes, File.ReadAllText(Primary));
            Assert.AreEqual(LocalQAProfileStore.InventoryResult.Applied, Grant("ability", first.Id, first.Tricks[0].Id));
            Assert.AreEqual(bytes, File.ReadAllText(Path.Combine(directory, "qa-profile.backup.json")));
            Assert.AreEqual(original.ProfileId, store.OpenOrCreate().ProfileId);
            Assert.AreEqual(LocalQAProfileStore.Result.AlreadyApplied, store.GrantWelcome(settings));
        }

        [Test] public void InvalidCurrentInventoryAndFutureSchemasAreNotSilentlyMigrated()
        {
            var profile = store.OpenOrCreate();
            profile.OwnedAbilities = null;
            Assert.Throws<InvalidDataException>(() => profile.UpgradeSchema());
            profile.OwnedAbilities = new List<OwnedAbility>();
            profile.Version = LocalEconomyProfile.CurrentVersion + 1;
            string bytes = Encode(JsonUtility.ToJson(profile));
            File.WriteAllText(Primary, bytes);
            Assert.Throws<NotSupportedException>(() => store.OpenOrCreate());
            Assert.AreEqual(bytes, File.ReadAllText(Primary));
        }

        [Test] public void OwnedAbilitiesRequireTheirCharacterAndOneMatchingReceipt()
        {
            Grant("character", first.Id);
            Grant("ability", first.Id, first.Tricks[0].Id);
            var profile = store.OpenOrCreate();
            profile.OwnedCharacterIds.Clear();
            Assert.Throws<InvalidDataException>(() => profile.Validate());
            profile = store.OpenOrCreate();
            profile.InventoryReceipts.RemoveAll(r => !string.IsNullOrEmpty(r.AbilityId));
            Assert.Throws<InvalidDataException>(() => profile.Validate());
            profile = store.OpenOrCreate();
            profile.OwnedAbilities.Add(profile.OwnedAbilities[0]);
            Assert.Throws<InvalidDataException>(() => profile.Validate());
        }

        [Serializable] sealed class Envelope
        {
            public int Version = 1;
            public string Payload, Sha256;
        }
        [Serializable] sealed class LegacyProfile
        {
            public int Version;
            public string ProfileId, LegacyProgressJson;
            public ProfileEnvironment Environment;
            public long Revision;
            public List<string> OwnedCharacterIds = new List<string>();
            public List<string> CompletedTutorialSteps = new List<string>();
            public List<CurrencyBalance> Balances = new List<CurrencyBalance>();
            public List<EconomicTransaction> Transactions = new List<EconomicTransaction>();
            public List<StarterSelection> StarterSelections = new List<StarterSelection>();
        }
        static string Encode(string payload)
        {
            string checksum;
            using (var hash = SHA256.Create())
                checksum = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "");
            return JsonUtility.ToJson(new Envelope { Payload = payload, Sha256 = checksum });
        }
    }
}

using System;
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
    public sealed class LocalProfileTests
    {
        string directory;
        LocalQAProfileStore store;
        EconomyDevelopmentConfig config;
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "MonsterPouchProfileTests", Guid.NewGuid().ToString("N"));
            store = new LocalQAProfileStore(directory);
            config = ScriptableObject.CreateInstance<EconomyDevelopmentConfig>();
            config.EnableLocalQA = true;
        }
        [TearDown] public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(config);
            string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchProfileTests")) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(directory);
            Assert.IsTrue(target.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
        [Test] public void WelcomeAndLegacyProgressSurviveRestartWithoutAnotherGrant()
        {
            const string legacy = "{\"Level\":17,\"Experience\":89,\"Unlocks\":[\"original\"]}";
            var first = store.OpenOrCreate(legacy);
            Assert.AreEqual(legacy, first.LegacyProgressJson);
            Assert.AreEqual(LocalQAProfileStore.Result.Applied, store.GrantWelcome(config));
            var reopened = new LocalQAProfileStore(directory);
            var restored = reopened.OpenOrCreate("a different legacy snapshot");
            Assert.AreEqual(first.ProfileId, restored.ProfileId);
            Assert.AreEqual(legacy, restored.LegacyProgressJson);
            Assert.AreEqual(1, restored.Balance(PermanentCurrency.Saturncoins, CreditOrigin.InternalQA));
            config.starterSaturncoins = 10;
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(LocalQAProfileStore.Result.AlreadyApplied, reopened.GrantWelcome(config));
            Assert.AreEqual(1, reopened.OpenOrCreate().Transactions.Count);
            Assert.AreEqual(0, restored.Balance(PermanentCurrency.Saturncoins, CreditOrigin.FreePlayer));
            Assert.AreEqual(0, restored.Balance(PermanentCurrency.Saturncoins, CreditOrigin.Purchased));
        }
        [Test] public void DuplicateSpendsAreIdempotentAndConflictingRetriesAreRejected()
        {
            config.starterSaturncoins = 3; store.OpenOrCreate(); store.GrantWelcome(config);
            Assert.AreEqual(LocalQAProfileStore.Result.Applied, store.Spend(config, "draw-1", PermanentCurrency.Saturncoins, 1));
            Assert.AreEqual(LocalQAProfileStore.Result.AlreadyApplied, new LocalQAProfileStore(directory).Spend(config, "draw-1", PermanentCurrency.Saturncoins, 1));
            Assert.AreEqual(LocalQAProfileStore.Result.Conflict, store.Spend(config, "draw-1", PermanentCurrency.Saturncoins, 2));
            Assert.AreEqual(LocalQAProfileStore.Result.Conflict, store.Spend(config, "welcome:v1", PermanentCurrency.Saturncoins, 1));
            Assert.AreEqual(2, store.OpenOrCreate().Balance(PermanentCurrency.Saturncoins, CreditOrigin.InternalQA));
        }
        [Test] public void OverspendingAndOtherCurrenciesLeaveEveryRecordUnchanged()
        {
            store.OpenOrCreate(); store.GrantWelcome(config);
            string before = File.ReadAllText(Path.Combine(directory, "qa-profile.json"));
            Assert.AreEqual(LocalQAProfileStore.Result.InsufficientFunds, store.Spend(config, "too-much", PermanentCurrency.Saturncoins, 2));
            Assert.AreEqual(LocalQAProfileStore.Result.InsufficientFunds, store.Spend(config, "earth", PermanentCurrency.Earthcoins, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Spend(config, "zero", PermanentCurrency.Saturncoins, 0));
            Assert.Throws<ArgumentException>(() => store.Spend(config, "", PermanentCurrency.Saturncoins, 1));
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(directory, "qa-profile.json")));
        }
        [Test] public void DisabledEconomyCannotGrantOrSpend()
        {
            store.OpenOrCreate(); config.EnableLocalQA = false;
            Assert.AreEqual(LocalQAProfileStore.Result.Disabled, store.GrantWelcome(config));
            Assert.AreEqual(LocalQAProfileStore.Result.Disabled, store.Spend(config, "op", PermanentCurrency.Saturncoins, 1));
            Assert.IsEmpty(store.OpenOrCreate().Transactions);
        }
        [Test] public void CorruptPrimaryExposesBackupReadOnlyAndCannotReplayLostReceipts()
        {
            store.OpenOrCreate(); store.GrantWelcome(config);
            File.WriteAllText(Path.Combine(directory, "qa-profile.json"), "damaged save");
            var recovered = store.OpenOrCreate();
            Assert.IsTrue(store.NeedsRecovery);
            Assert.AreEqual(0, recovered.Transactions.Count);
            Assert.AreEqual(LocalQAProfileStore.Result.RecoveryRequired, store.GrantWelcome(config));
            Assert.AreEqual(LocalQAProfileStore.Result.RecoveryRequired, store.Spend(config, "replay", PermanentCurrency.Saturncoins, 1));
            Assert.AreEqual("damaged save", File.ReadAllText(Path.Combine(directory, "qa-profile.json")));
            Assert.IsTrue(File.Exists(Path.Combine(directory, "qa-profile.backup.json")));
        }
        [Test] public void ChecksumDamageExposesBackupWithoutGrantingWelcomeAgain()
        {
            store.OpenOrCreate();store.GrantWelcome(config);
            string primary=Path.Combine(directory,"qa-profile.json");
            string backupPath=Path.Combine(directory,"qa-profile.backup.json");
            var envelope=JsonUtility.FromJson<TestEnvelope>(File.ReadAllText(primary));
            envelope.Sha256="wrong";
            string damaged=JsonUtility.ToJson(envelope);File.WriteAllText(primary,damaged);
            string backup=File.ReadAllText(backupPath);
            var reopened=new LocalQAProfileStore(directory);
            Assert.IsEmpty(reopened.OpenOrCreate().Transactions);
            Assert.IsTrue(reopened.NeedsRecovery);
            Assert.AreEqual(LocalQAProfileStore.Result.RecoveryRequired,reopened.GrantWelcome(config));
            Assert.AreEqual(damaged,File.ReadAllText(primary));
            Assert.AreEqual(backup,File.ReadAllText(backupPath));
        }
        [Test] public void UnreadableSaveWithoutBackupNeverCreatesAnotherIdentity()
        {
            store.OpenOrCreate();
            File.WriteAllText(Path.Combine(directory, "qa-profile.json"), "{}");
            var error = Assert.Catch<Exception>(() => store.OpenOrCreate());
            Assert.That(error, Is.InstanceOf<InvalidDataException>().Or.InstanceOf<NotSupportedException>());
            Assert.AreEqual("{}", File.ReadAllText(Path.Combine(directory, "qa-profile.json")));
        }
        [Test] public void FutureSchemaNeverFallsBackToOlderBackupOrOverwritesFiles()
        {
            store.OpenOrCreate(); store.GrantWelcome(config);
            const string future = "{\"Version\":2,\"Payload\":\"future schema\"}";
            File.WriteAllText(Path.Combine(directory, "qa-profile.json"), future);
            Assert.Throws<NotSupportedException>(() => store.OpenOrCreate());
            Assert.AreEqual(future, File.ReadAllText(Path.Combine(directory, "qa-profile.json")));
        }
        [Test] public void IndependentStoreInstancesObserveLatestBalanceAndNeverOverwriteIt()
        {
            store.OpenOrCreate(); store.GrantWelcome(config);
            var other = new LocalQAProfileStore(directory);
            var stale = other.OpenOrCreate();
            Assert.AreEqual(LocalQAProfileStore.Result.Applied, store.Spend(config, "first", PermanentCurrency.Saturncoins, 1));
            stale.Balances.Single().Amount = 100000;
            Assert.AreEqual(LocalQAProfileStore.Result.InsufficientFunds, other.Spend(config, "second", PermanentCurrency.Saturncoins, 1));
            Assert.AreEqual(0, other.OpenOrCreate().Balance(PermanentCurrency.Saturncoins, CreditOrigin.InternalQA));
        }
        [Test] public void ExistingWriterLockPreventsAnyPartialMutation()
        {
            store.OpenOrCreate();
            string before = File.ReadAllText(Path.Combine(directory, "qa-profile.json"));
            using (new FileStream(Path.Combine(directory, "qa-profile.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Throws<IOException>(() => store.GrantWelcome(config));
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(directory, "qa-profile.json")));
            Assert.AreEqual(LocalQAProfileStore.Result.Applied, store.GrantWelcome(config));
        }
        [Test] public void SchemaRejectsProductionCreditsInsideAQAProfile()
        {
            var profile = store.OpenOrCreate();
            profile.Balances.Add(new CurrencyBalance { Currency = PermanentCurrency.Saturncoins, Origin = CreditOrigin.Purchased });
            Assert.Throws<InvalidDataException>(() => profile.Validate());
            Assert.IsEmpty(store.OpenOrCreate().Balances);
            CollectionAssert.AreEqual(new[] { "Earthcoins", "Suncoins", "Saturncoins" }, Enum.GetNames(typeof(PermanentCurrency)));
        }
        [TestCase("bugaloo")] [TestCase("popow")]
        public void StarterChoiceSurvivesRestartAndGrantsOnlyTheChosenMonsterAndThreeWhelps(string choice)
        {
            var catalogue=MatchConfig.CreateDocumented();
            try
            {
                store.OpenOrCreate("legacy unchanged");
                Assert.AreEqual(LocalQAProfileStore.Result.Applied,store.ChooseStarter(config,catalogue,choice));
                var reopened=new LocalQAProfileStore(directory);
                var profile=reopened.OpenOrCreate();
                Assert.AreEqual(choice,profile.Starter.Deck.Monster);
                CollectionAssert.AreEqual(new[]{"atori","bugui","blotan"},profile.Starter.Deck.Whelps);
                CollectionAssert.AreEquivalent(new[]{choice}.Concat(StarterRoster.Whelps),profile.OwnedCharacterIds);
                Assert.AreEqual(4,profile.OwnedCharacterIds.Count);
                Assert.AreEqual("legacy unchanged",profile.LegacyProgressJson);
                Assert.IsEmpty(profile.Transactions);Assert.IsEmpty(profile.Balances);
                string before=File.ReadAllText(Path.Combine(directory,"qa-profile.json"));
                Assert.AreEqual(LocalQAProfileStore.Result.AlreadyApplied,reopened.ChooseStarter(config,catalogue,choice));
                string other=choice=="bugaloo"?"popow":"bugaloo";
                Assert.AreEqual(LocalQAProfileStore.Result.Conflict,reopened.ChooseStarter(config,catalogue,other));
                Assert.AreEqual(before,File.ReadAllText(Path.Combine(directory,"qa-profile.json")));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalogue); }
        }
        [Test] public void MissingCatalogueInvalidChoiceAndDisabledQAHaveNoPartialUnlocks()
        {
            var catalogue=MatchConfig.CreateDocumented();
            try
            {
                store.OpenOrCreate();
                string before=File.ReadAllText(Path.Combine(directory,"qa-profile.json"));
                Assert.AreEqual(LocalQAProfileStore.Result.InvalidStarter,store.ChooseStarter(config,catalogue,"anuik"));
                catalogue.Units=catalogue.Units.Where(u=>u.Id!="bugui").ToArray();
                Assert.AreEqual(LocalQAProfileStore.Result.InvalidCatalogue,store.ChooseStarter(config,catalogue,"bugaloo"));
                config.EnableLocalQA=false;
                Assert.AreEqual(LocalQAProfileStore.Result.Disabled,store.ChooseStarter(config,catalogue,"popow"));
                Assert.AreEqual(before,File.ReadAllText(Path.Combine(directory,"qa-profile.json")));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalogue); }
        }
        [Test] public void V1MigrationPreservesOriginalBytesAndDoesNotInventAStarter()
        {
            var original=store.OpenOrCreate("old progress");
            original.Version=1;
            string payload=JsonUtility.ToJson(original).Replace(",\"StarterSelections\":[]",""),hash;
            using(var sha=SHA256.Create())
                hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
            string encoded=JsonUtility.ToJson(new TestEnvelope{Payload=payload,Sha256=hash});
            File.WriteAllText(Path.Combine(directory,"qa-profile.json"),encoded);
            var restored=store.OpenOrCreate();
            Assert.AreEqual(LocalEconomyProfile.CurrentVersion,restored.Version);
            Assert.AreEqual(original.ProfileId,restored.ProfileId);
            Assert.AreEqual("old progress",restored.LegacyProgressJson);Assert.IsNull(restored.Starter);
            Assert.AreEqual(encoded,File.ReadAllText(Path.Combine(directory,"qa-profile.json")));
            var catalogue=MatchConfig.CreateDocumented();
            try { Assert.AreEqual(LocalQAProfileStore.Result.Applied,store.ChooseStarter(config,catalogue,"popow")); }
            finally { UnityEngine.Object.DestroyImmediate(catalogue); }
            Assert.AreEqual(encoded,File.ReadAllText(Path.Combine(directory,"qa-profile.backup.json")));
            Assert.AreEqual("popow",store.OpenOrCreate().Starter.Deck.Monster);
        }
        [Test] public void CorruptStarterSaveCannotGrantTheOtherMonsterFromAnOlderBackup()
        {
            var catalogue=MatchConfig.CreateDocumented();
            try
            {
                store.OpenOrCreate();store.ChooseStarter(config,catalogue,"bugaloo");
                File.WriteAllText(Path.Combine(directory,"qa-profile.json"),"corrupt");
                Assert.AreEqual(LocalQAProfileStore.Result.RecoveryRequired,store.ChooseStarter(config,catalogue,"popow"));
                Assert.AreEqual("corrupt",File.ReadAllText(Path.Combine(directory,"qa-profile.json")));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalogue); }
        }
        [Serializable] sealed class TestEnvelope
        {
            public int Version=1;
            public string Payload,Sha256;
        }
        [Test] public void ShippedCatalogueContainsBothStarterChoicesAndAllThreeWhelps()
        {
            var asset=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            Assert.IsNotNull(asset);
            var catalogue=asset.CreateRuntimeConfig();
            try
            {
                foreach(var id in StarterRoster.Monsters)
                {
                    Assert.IsTrue(StarterRoster.TryBuild(id,catalogue,out var deck),id);
                    Assert.AreEqual(3,deck.Whelps.Length);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(catalogue); }
        }
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Progression;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class LocalProgressStoreTests
    {
        [Serializable] sealed class Envelope { public int Version; public string Payload,Sha256; }
        [Serializable] sealed class Record { public int Version; public long Revision; public string ProgressJson,OriginalLegacyJson; }
        string directory;
        LocalProgressStore store;
        ProgressionConfig config;
        string Primary=>Path.Combine(directory,"progress.json");
        string Backup=>Path.Combine(directory,"progress.backup.json");
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"MonsterPouchProgressTests",Guid.NewGuid().ToString("N"));
            store=new LocalProgressStore(directory);config=ScriptableObject.CreateInstance<ProgressionConfig>();
            config.ExperiencePerMatch=3;config.ExperiencePerWin=2;config.ExperiencePerLevel=new[]{100};
        }
        [TearDown] public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(config);
            string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchProgressTests"))+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(directory);
            Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
            if(Directory.Exists(target))Directory.Delete(target,true);
        }
        [Test] public void ImportsLegacyOnceAndRetainsItsExactJsonAlongsideProgress()
        {
            var previous=new PlayerProgress{Level=17,Experience=89};
            previous.Unlocks.AddRange(new[]{"anuik","lobby.test"});previous.CompletedMatches.Add("old-match");
            string legacy=previous.Serialize();
            Assert.AreEqual(legacy,store.LoadOrImport(legacy).Serialize());
            string imported=File.ReadAllText(Primary);
            var envelope=JsonUtility.FromJson<Envelope>(imported);
            Assert.AreEqual(legacy,JsonUtility.FromJson<Record>(envelope.Payload).OriginalLegacyJson);
            Assert.AreEqual(legacy,new LocalProgressStore(directory).LoadOrImport("damaged stale PlayerPrefs").Serialize());
            Assert.AreEqual(imported,File.ReadAllText(Primary));
            Assert.AreEqual(LocalProgressStore.AwardResult.AlreadyApplied,store.Award(config,"old-match",true,out _));
            Assert.AreEqual(imported,File.ReadAllText(Primary));
        }
        [Test] public void MoreThan128CommittedMatchesCannotBeAwardedAgainAfterRestart()
        {
            store.LoadOrImport("");
            for(int i=0;i<260;i++)
            {
                if(i%40==0)store=new LocalProgressStore(directory);
                Assert.AreEqual(LocalProgressStore.AwardResult.Applied,store.Award(config,"match-"+i,i%2==0,out _));
            }
            string before=File.ReadAllText(Primary);
            var reopened=new LocalProgressStore(directory);
            foreach(int i in new[]{0,127,128,131,132,259})
                Assert.AreEqual(LocalProgressStore.AwardResult.AlreadyApplied,reopened.Award(config,"match-"+i,i%2!=0,out _));
            Assert.AreEqual(before,File.ReadAllText(Primary));
            Assert.AreEqual(260,reopened.LoadOrImport("").CompletedMatches.Count);
            Assert.AreEqual(0,Directory.GetFiles(directory,"*.tmp").Length);
        }
        [Test] public void StaleReadersAndDuplicateCallbacksDoNotLoseNewerProgress()
        {
            var stale=store.LoadOrImport("");var other=new LocalProgressStore(directory);other.LoadOrImport("");
            Assert.AreEqual(LocalProgressStore.AwardResult.Applied,store.Award(config,"first",true,out var first));
            Assert.AreEqual(LocalProgressStore.AwardResult.Applied,other.Award(config,"second",false,out var second));
            Assert.AreEqual(8,second.Experience);Assert.IsEmpty(stale.CompletedMatches);
            first.Experience=999;first.CompletedMatches.Clear();
            Assert.AreEqual(LocalProgressStore.AwardResult.AlreadyApplied,store.Award(config,"first",false,out var current));
            Assert.AreEqual(8,current.Experience);
            CollectionAssert.AreEqual(new[]{"first","second"},current.CompletedMatches);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDamagedPrimaryUsesBackupReadOnlyWithoutRepeatingLostReceipts(bool missing)
        {
            store.LoadOrImport("");store.Award(config,"first",true,out _);store.Award(config,"latest",true,out _);
            string originalBackup=File.ReadAllText(Backup);
            if(missing)File.Delete(Primary);else File.WriteAllText(Primary,"broken primary");
            var recovered=new LocalProgressStore(directory);
            Assert.AreEqual(1,recovered.LoadOrImport(new PlayerProgress().Serialize()).CompletedMatches.Count);
            Assert.IsTrue(recovered.NeedsRecovery);
            Assert.AreEqual(LocalProgressStore.AwardResult.RecoveryRequired,recovered.Award(config,"latest",true,out _));
            Assert.AreEqual(LocalProgressStore.AwardResult.RecoveryRequired,recovered.Award(config,"new",true,out _));
            Assert.AreEqual(originalBackup,File.ReadAllText(Backup));
            if(missing)Assert.IsFalse(File.Exists(Primary));else Assert.AreEqual("broken primary",File.ReadAllText(Primary));
        }
        [Test] public void ChecksumDamageExposesBackupWithoutOverwritingTheDamagedPrimary()
        {
            store.LoadOrImport("");store.Award(config,"committed",true,out _);
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary));
            envelope.Sha256="wrong";
            string damaged=JsonUtility.ToJson(envelope);File.WriteAllText(Primary,damaged);
            string backup=File.ReadAllText(Backup);
            var reopened=new LocalProgressStore(directory);
            Assert.IsEmpty(reopened.LoadOrImport("").CompletedMatches);
            Assert.IsTrue(reopened.NeedsRecovery);
            Assert.AreEqual(LocalProgressStore.AwardResult.RecoveryRequired,reopened.Award(config,"committed",true,out _));
            Assert.AreEqual(damaged,File.ReadAllText(Primary));
            Assert.AreEqual(backup,File.ReadAllText(Backup));
        }
        [TestCase("{}")]
        [TestCase("broken json")]
        [TestCase("{\"Version\":1,\"Level\":0,\"Experience\":0,\"Unlocks\":[],\"CompletedMatches\":[]}")]
        [TestCase("{\"Version\":1,\"Level\":1,\"Experience\":0,\"Unlocks\":[],\"CompletedMatches\":[\"same\",\"same\"]}")]
        public void InvalidLegacyCannotBeSilentlyReplacedByAnEmptyProfile(string legacy)
        {
            Assert.Catch<Exception>(()=>store.LoadOrImport(legacy));
            Assert.IsFalse(File.Exists(Primary));Assert.IsFalse(File.Exists(Backup));
        }
        [TestCase("envelope")]
        [TestCase("record")]
        [TestCase("progress")]
        public void FutureVersionsArePreservedAndNeverDowngradedToBackup(string level)
        {
            store.LoadOrImport("");store.Award(config,"first",true,out _);
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary));
            var record=JsonUtility.FromJson<Record>(envelope.Payload);
            if(level=="envelope")envelope.Version=2;
            else
            {
                if(level=="record")record.Version=2;
                else{var progress=PlayerProgress.Restore(record.ProgressJson);progress.Version=2;record.ProgressJson=progress.Serialize();}
                envelope.Payload=JsonUtility.ToJson(record);envelope.Sha256=Hash(envelope.Payload);
            }
            string future=JsonUtility.ToJson(envelope);File.WriteAllText(Primary,future);
            string backup=File.ReadAllText(Backup);
            Assert.Throws<NotSupportedException>(()=>new LocalProgressStore(directory).LoadOrImport(""));
            Assert.Throws<NotSupportedException>(()=>store.Award(config,"new",true,out _));
            Assert.AreEqual(future,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        }
        [Test] public void InterruptedTemporaryFileCannotBecomeAnAwardOrReplaceCommittedData()
        {
            store.LoadOrImport("");store.Award(config,"committed",false,out _);
            string before=File.ReadAllText(Primary);
            File.WriteAllText(Path.Combine(directory,"progress.interrupted.tmp"),"unfinished write");
            var reopened=new LocalProgressStore(directory).LoadOrImport("");
            CollectionAssert.AreEqual(new[]{"committed"},reopened.CompletedMatches);
            Assert.AreEqual(before,File.ReadAllText(Primary));
        }
        [Test] public void CompetingWriterLeavesFilesUnchangedAndRetryAwardsOnlyOnce()
        {
            store.LoadOrImport("");string before=File.ReadAllText(Primary);
            using(var writer=new FileStream(Path.Combine(directory,"progress.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                Assert.Throws<IOException>(()=>store.Award(config,"blocked",true,out _));
                Assert.AreEqual(before,File.ReadAllText(Primary));
            }
            Assert.AreEqual(LocalProgressStore.AwardResult.Applied,store.Award(config,"blocked",true,out var committed));
            Assert.AreEqual(5,committed.Experience);
            Assert.AreEqual(LocalProgressStore.AwardResult.AlreadyApplied,store.Award(config,"blocked",true,out _));
        }
        static string Hash(string value)
        {
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
        }
    }
}

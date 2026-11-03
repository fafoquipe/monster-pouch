using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class GuestOnboardingV3Tests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    string Backup=>Path.Combine(directory,"guest-profile.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchOnboardingV3Tests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchOnboardingV3Tests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [TestCase(GuestOnboardingStage.None,false,false)]
    [TestCase(GuestOnboardingStage.None,true,false)]
    [TestCase(GuestOnboardingStage.ChooseStarter,false,false)]
    [TestCase(GuestOnboardingStage.Tutorial,true,false)]
    [TestCase(GuestOnboardingStage.Tutorial,true,true)]
    public void V2MigrationPreservesIdentityStageAndReceiptsWithoutWriting(GuestOnboardingStage stage,bool starter,bool firstContact)
    {
        var original=MakeV2(stage,starter,firstContact);
        string before=Write(original),backup=File.Exists(Backup)?File.ReadAllText(Backup):null;
        var migrated=new LocalGuestProfileStore(directory).OpenOrCreate(true);
        Assert.AreEqual(LocalGuestProfile.CurrentVersion,migrated.Version);Assert.AreEqual(original.ProfileId,migrated.ProfileId);
        Assert.AreEqual(original.Revision,migrated.Revision);Assert.AreEqual(stage,migrated.OnboardingStage);
        Assert.AreEqual(original.HasLegacyAccess,migrated.HasLegacyAccess);
        CollectionAssert.AreEqual(original.CompletedTutorialModules,migrated.CompletedTutorialModules);
        Assert.AreEqual(starter,migrated.Starter!=null);
        if(starter)Assert.AreEqual(JsonUtility.ToJson(original.Starter),JsonUtility.ToJson(migrated.Starter));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        Assert.AreEqual(backup,File.Exists(Backup)?File.ReadAllText(Backup):null);
    }
    [Test] public void NewObjectiveCommitsCurrentSchemaWhileBackingUpTheUnmodifiedV2Receipt()
    {
        var original=MakeV2(GuestOnboardingStage.Tutorial,true,true);
        string before=Write(original);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.Positioning));
        Assert.AreEqual(before,File.ReadAllText(Backup));
        var reloaded=new LocalGuestProfileStore(directory).OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfile.CurrentVersion,reloaded.Version);Assert.AreEqual(original.ProfileId,reloaded.ProfileId);
        Assert.AreEqual(original.Revision+1,reloaded.Revision);
        Assert.AreEqual(GuestTutorialModules.AttacksHealth,reloaded.NextTutorialModuleId);
        Assert.AreEqual(JsonUtility.ToJson(original.Starter),JsonUtility.ToJson(reloaded.Starter));
        var payload=JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary)).Payload;
        Assert.AreEqual(LocalGuestProfile.CurrentVersion,JsonUtility.FromJson<LocalGuestProfile>(payload).Version);
    }
    [Test] public void ForgedV2HistoryCannotBeLaunderedByNewlyImplementedModules()
    {
        var invalid=MakeV2(GuestOnboardingStage.Tutorial,true,true);
        invalid.CompletedTutorialModules.Add(GuestTutorialModules.Positioning);
        string before=Write(invalid);if(File.Exists(Backup))File.Delete(Backup);
        Assert.Throws<InvalidDataException>(()=>new LocalGuestProfileStore(directory).OpenOrCreate(false));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(2,invalid.Version);
        Assert.Throws<InvalidDataException>(()=>invalid.UpgradeSchema());Assert.AreEqual(2,invalid.Version);
    }
    [Test] public void ConcurrentReloadsCannotDoubleRecordANewlyEnabledObjective()
    {
        Write(MakeV2(GuestOnboardingStage.Tutorial,true,true));
        var other=new LocalGuestProfileStore(directory);other.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.Positioning));
        string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.AlreadyCompleted,other.CompleteTutorialModule(GuestTutorialModules.Positioning));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        CollectionAssert.AreEqual(GuestTutorialModules.OrderedIds.Take(2),other.OpenOrCreate(false).CompletedTutorialModules);
    }
    [Test] public void FailedNewObjectiveWritePreservesV2ForRetry()
    {
        var original=MakeV2(GuestOnboardingStage.Tutorial,true,true);string before=Write(original);
        using(new FileStream(Path.Combine(directory,"guest-profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>store.CompleteTutorialModule(GuestTutorialModules.Positioning));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        Assert.AreEqual(GuestTutorialModules.Positioning,new LocalGuestProfileStore(directory).OpenOrCreate(false).NextTutorialModuleId);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.Positioning));
    }
    [Test] public void FutureSchemaDoesNotFallBackToAnOlderOnboardingGeneration()
    {
        var future=MakeV2(GuestOnboardingStage.Tutorial,true,true);future.Version=LocalGuestProfile.CurrentVersion+1;
        string before=Write(future),backup=File.ReadAllText(Backup);
        Assert.Throws<NotSupportedException>(()=>store.OpenOrCreate(false));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    LocalGuestProfile MakeV2(GuestOnboardingStage stage,bool starter,bool firstContact)
    {
        store=GuestHistoricalProfileFixture.CreateVersionSeven(directory,catalogue,null);
        if(starter)store.ChooseStarter(catalogue,"popow");
        if(firstContact)store.CompleteTutorialModule(GuestTutorialModules.FirstContact);
        var profile=store.OpenOrCreate(false);profile.Version=2;profile.OnboardingStage=stage;
        if(profile.Starter!=null)
        {
            profile.Starter.ReceiptId=StarterRoster.LegacyReceiptId;
            profile.Starter.Deck.Whelps=StarterRoster.LegacyWhelps.ToArray();
        }
        return profile;
    }
    string Write(LocalGuestProfile profile)
    {
        string payload=profile.Version<10?GuestHistoricalProfileFixture.WithoutTrophies(JsonUtility.ToJson(profile)):JsonUtility.ToJson(profile),checksum;
        using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        string encoded=JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=checksum});
        File.WriteAllText(Primary,encoded);return encoded;
    }
    [Serializable] sealed class Envelope {public int Version=1;public string Payload,Sha256;}
}

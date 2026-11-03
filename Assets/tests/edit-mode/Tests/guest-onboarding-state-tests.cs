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

public sealed class GuestOnboardingStateTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    string Backup=>Path.Combine(directory,"guest-profile.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchOnboardingStateTests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchOnboardingStateTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void OnlyAnIdentityCreatedNowWithoutLegacyDataNeedsAStarter(bool legacy)
    {
        var profile=store.OpenOrCreate(legacy);
        Assert.AreEqual(legacy?GuestOnboardingStage.None:GuestOnboardingStage.ChooseStarter,profile.OnboardingStage);
        Assert.AreEqual(!legacy,profile.OnboardingRequired);Assert.IsNull(profile.NextTutorialModuleId);
        string before=File.ReadAllText(Primary);
        var reloaded=new LocalGuestProfileStore(directory).OpenOrCreate(!legacy);
        Assert.AreEqual(profile.ProfileId,reloaded.ProfileId);Assert.AreEqual(profile.OnboardingStage,reloaded.OnboardingStage);
        Assert.AreEqual(before,File.ReadAllText(Primary));
        if(legacy)
        {
            Assert.AreEqual(LocalGuestProfileStore.SelectionResult.NotRequired,store.ChooseStarter(catalogue,"popow"));
            Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsNull(store.OpenOrCreate(false).Starter);
        }
    }
    [TestCase(false)]
    [TestCase(true)]
    public void ExistingV1IdentityNeverGetsAnIntroBecauseItsStarterOrStageWasAbsent(bool hadStarter)
    {
        var initial=store.OpenOrCreate(false);
        if(hadStarter)store.ChooseStarter(catalogue,"popow");
        var current=store.OpenOrCreate(false);
        if(current.Starter!=null)
        {
            current.Starter.ReceiptId=StarterRoster.LegacyReceiptId;
            current.Starter.Deck.Whelps=StarterRoster.LegacyWhelps.ToArray();
        }
        string legacy=Encode(JsonUtility.ToJson(new V1Profile{
            ProfileId=initial.ProfileId,Revision=current.Revision,HasLegacyAccess=false,
            StarterSelections=current.StarterSelections
        }));
        File.WriteAllText(Primary,legacy);
        var restored=new LocalGuestProfileStore(directory).OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfile.CurrentVersion,restored.Version);Assert.AreEqual(initial.ProfileId,restored.ProfileId);
        Assert.AreEqual(GuestOnboardingStage.None,restored.OnboardingStage);Assert.IsFalse(restored.OnboardingRequired);
        Assert.IsEmpty(restored.CompletedTutorialModules);Assert.AreEqual(hadStarter,restored.Starter!=null);
        if(hadStarter)Assert.AreEqual("popow",restored.Starter.Deck.Monster);
        Assert.AreEqual(hadStarter?LocalGuestProfileStore.SelectionResult.AlreadySelected:LocalGuestProfileStore.SelectionResult.NotRequired,
            store.ChooseStarter(catalogue,"popow"));
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.NotInTutorial,store.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        Assert.AreEqual(legacy,File.ReadAllText(Primary),"Reading/migrating or reopening an old account must not rewrite it or start onboarding.");
    }
    [TestCase("bugaloo")]
    [TestCase("popow")]
    public void SelectingOnceEntersTutorialAndReopeningNeverReturnsToSelection(string monster)
    {
        string id=store.OpenOrCreate(false).ProfileId;
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,monster));
        var profile=new LocalGuestProfileStore(directory).OpenOrCreate(true);
        Assert.AreEqual(id,profile.ProfileId);Assert.AreEqual(GuestOnboardingStage.Tutorial,profile.OnboardingStage);
        Assert.IsTrue(profile.OnboardingRequired);Assert.AreEqual(GuestTutorialModules.FirstContact,profile.NextTutorialModuleId);
        Assert.AreEqual(monster,profile.Starter.Deck.Monster);Assert.AreEqual(1,profile.Revision);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        string completed=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.AlreadySelected,store.ChooseStarter(catalogue,monster));
        Assert.AreEqual(completed,File.ReadAllText(Primary));
        Assert.AreEqual("positioning",store.OpenOrCreate(false).NextTutorialModuleId);
    }
    [Test] public void ModuleReceiptsAreSequentialIdempotentAndCannotCompleteUnimplementedContent()
    {
        store.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.NotInTutorial,store.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        store.ChooseStarter(catalogue,"bugaloo");string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.InvalidStep,store.CompleteTutorialModule("invented-module"));
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.OutOfOrder,store.CompleteTutorialModule("positioning"));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        foreach(string module in new[]{GuestTutorialModules.FirstContact,"positioning","attacks-health","simple-combat","preparation-economy","tactical-bank"})
            Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(module));
        string completed=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var restarted=new LocalGuestProfileStore(directory);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.AlreadyCompleted,restarted.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.ObjectivePending,restarted.CompleteTutorialModule("progression-collection"));
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.OutOfOrder,restarted.CompleteTutorialModule("first-bo5"));
        var profile=restarted.OpenOrCreate(false);
        CollectionAssert.AreEqual(new[]{GuestTutorialModules.FirstContact,"positioning","attacks-health","simple-combat","preparation-economy","tactical-bank"},profile.CompletedTutorialModules);
        Assert.AreEqual(GuestOnboardingStage.Tutorial,profile.OnboardingStage);Assert.IsTrue(profile.OnboardingRequired);
        Assert.AreEqual(7,profile.Revision);Assert.AreEqual(completed,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void TwoStoreInstancesCannotCompleteTheSameModuleTwice()
    {
        store.OpenOrCreate(false);store.ChooseStarter(catalogue,"popow");
        var other=new LocalGuestProfileStore(directory);other.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.AlreadyCompleted,other.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        Assert.AreEqual(1,other.OpenOrCreate(false).CompletedTutorialModules.Count);Assert.AreEqual(2,other.OpenOrCreate(false).Revision);
    }
    [Test] public void FailedModuleWriteCanRetryWithoutLosingTheStarterOrInventingCompletion()
    {
        store.OpenOrCreate(false);store.ChooseStarter(catalogue,"popow");string before=File.ReadAllText(Primary);
        using(new FileStream(Path.Combine(directory,"guest-profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>store.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(store.OpenOrCreate(false).CompletedTutorialModules);
        var restarted=new LocalGuestProfileStore(directory);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,restarted.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        Assert.AreEqual("popow",restarted.OpenOrCreate(false).Starter.Deck.Monster);
    }
    [Test] public void RecoveryCannotReplayACompletionMissingFromTheBackup()
    {
        store.OpenOrCreate(false);store.ChooseStarter(catalogue,"popow");store.CompleteTutorialModule(GuestTutorialModules.FirstContact);
        File.WriteAllText(Primary,"damaged onboarding");string backup=File.ReadAllText(Backup);
        var restored=store.OpenOrCreate(false);Assert.IsTrue(store.NeedsRecovery);Assert.IsEmpty(restored.CompletedTutorialModules);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.RecoveryRequired,store.CompleteTutorialModule(GuestTutorialModules.FirstContact));
        Assert.AreEqual("damaged onboarding",File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void CurrentSchemaRejectsMissingStateAndOutOfOrderModuleHistory()
    {
        var profile=store.OpenOrCreate(false);
        profile.OnboardingStage=(GuestOnboardingStage)(-1);Assert.Throws<InvalidDataException>(()=>profile.Validate());
        profile=store.OpenOrCreate(false);profile.CompletedTutorialModules=null;Assert.Throws<InvalidDataException>(()=>profile.Validate());
        store.ChooseStarter(catalogue,"bugaloo");profile=store.OpenOrCreate(false);
        profile.CompletedTutorialModules.Add("positioning");Assert.Throws<InvalidDataException>(()=>profile.Validate());
        profile=store.OpenOrCreate(false);profile.OnboardingStage=GuestOnboardingStage.Complete;
        Assert.Throws<InvalidDataException>(()=>profile.Validate(),"Six implemented modules cannot complete the ten-module tutorial.");
    }
    [Serializable] sealed class V1Profile
    {
        public int Version=1;public string ProfileKind="local-guest",ProfileId;public long Revision;
        public bool HasLegacyAccess;public List<StarterSelection> StarterSelections=new List<StarterSelection>();
    }
    [Serializable] sealed class Envelope{public int Version=1;public string Payload,Sha256;}
    static string Encode(string payload)
    {
        string checksum;using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        return JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=checksum});
    }
}

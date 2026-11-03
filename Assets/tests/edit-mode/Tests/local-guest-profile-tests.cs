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

public sealed class LocalGuestProfileTests
{
    [Serializable] sealed class Envelope { public int Version; public string Payload,Sha256; }
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    string Backup=>Path.Combine(directory,"guest-profile.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchGuestTests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);catalogue=MatchConfig.CreateDocumented();
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchGuestTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void IdentityAndLegacyMarkerSurviveRestartWithoutAutomaticallySelectingACharacter(bool legacy)
    {
        var profile=store.OpenOrCreate(legacy);string before=File.ReadAllText(Primary);
        Assert.IsTrue(Guid.TryParseExact(profile.ProfileId,"N",out _));
        Assert.IsNull(profile.Starter);Assert.AreEqual(0,profile.Revision);
        var restored=new LocalGuestProfileStore(directory).OpenOrCreate(!legacy);
        Assert.AreEqual(profile.ProfileId,restored.ProfileId);Assert.AreEqual(legacy,restored.HasLegacyAccess);
        Assert.IsEmpty(restored.StarterSelections);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("bugaloo")]
    [TestCase("popow")]
    public void StarterReceiptPersistsOnlyTheSelectedMonsterAndThreeApprovedWhelps(string choice)
    {
        var initial=store.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,choice));
        var reopened=new LocalGuestProfileStore(directory);var profile=reopened.OpenOrCreate(true);
        Assert.AreEqual(initial.ProfileId,profile.ProfileId);Assert.IsFalse(profile.HasLegacyAccess);
        Assert.AreEqual(1,profile.Revision);Assert.AreEqual(1,profile.StarterSelections.Count);
        Assert.AreEqual(StarterRoster.CurrentReceiptId,profile.Starter.ReceiptId);Assert.AreEqual(choice,profile.Starter.Deck.Monster);
        CollectionAssert.AreEqual(new[]{"atori","bugui","blotan"},profile.Starter.Deck.Whelps);
        string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.AlreadySelected,reopened.ChooseStarter(catalogue,choice));
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Conflict,reopened.ChooseStarter(catalogue,choice=="bugaloo"?"popow":"bugaloo"));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void ExistingReadersAndCallerMutationsCannotReplaceAnotherSessionsIdentityOrChoice()
    {
        var stale=store.OpenOrCreate(false);
        var other=new LocalGuestProfileStore(directory);var current=other.OpenOrCreate(false);
        other.ChooseStarter(catalogue,"popow");
        stale.ProfileId=Guid.NewGuid().ToString("N");stale.HasLegacyAccess=false;stale.StarterSelections.Clear();
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Conflict,store.ChooseStarter(catalogue,"bugaloo"));
        var restored=store.OpenOrCreate(false);
        Assert.AreEqual(current.ProfileId,restored.ProfileId);Assert.IsFalse(restored.HasLegacyAccess);
        Assert.AreEqual("popow",restored.Starter.Deck.Monster);
    }
    [Test] public void InvalidChoiceOrIncompleteCatalogueCannotPartiallySelectAStarter()
    {
        store.OpenOrCreate(false);string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.InvalidStarter,store.ChooseStarter(catalogue,"anuik"));
        catalogue.Units=catalogue.Units.Where(u=>u.Id!="bugui").ToArray();
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.InvalidCatalogue,store.ChooseStarter(catalogue,"bugaloo"));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsNull(store.OpenOrCreate(false).Starter);
    }
    [TestCase("missing")]
    [TestCase("json")]
    [TestCase("checksum")]
    public void DamagedPrimaryExposesBackupWithoutAllowingAnotherStarterOrIdentity(string damage)
    {
        string identity=store.OpenOrCreate(false).ProfileId;store.ChooseStarter(catalogue,"bugaloo");
        if(damage=="missing")File.Delete(Primary);
        else if(damage=="json")File.WriteAllText(Primary,"corrupt");
        else
        {
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary));envelope.Sha256="wrong";
            File.WriteAllText(Primary,JsonUtility.ToJson(envelope));
        }
        string before=File.Exists(Primary)?File.ReadAllText(Primary):null,backup=File.ReadAllText(Backup);
        var recovered=store.OpenOrCreate(true);
        Assert.AreEqual(identity,recovered.ProfileId);Assert.IsTrue(store.NeedsRecovery);Assert.IsNull(recovered.Starter);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.RecoveryRequired,store.ChooseStarter(catalogue,"popow"));
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.RecoveryRequired,store.ChooseStarter(catalogue,"bugaloo"));
        Assert.AreEqual(before,File.Exists(Primary)?File.ReadAllText(Primary):null);
        Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void DamagedIdentityWithoutBackupCannotBeReplacedByANewGuest()
    {
        store.OpenOrCreate(false);File.WriteAllText(Primary,"{}");
        Assert.Throws<InvalidDataException>(()=>store.OpenOrCreate(false));
        Assert.AreEqual("{}",File.ReadAllText(Primary));Assert.IsFalse(File.Exists(Backup));
    }
    [Test] public void ForeignProfilePayloadCannotBecomeAGuestEvenWithAValidChecksum()
    {
        store.OpenOrCreate(false);
        var foreign=LocalEconomyProfile.CreateQA("legacy QA data");
        foreign.Version=1;
        string payload=JsonUtility.ToJson(foreign);
        string encoded=JsonUtility.ToJson(new Envelope{Version=1,Payload=payload,Sha256=Hash(payload)});
        File.WriteAllText(Primary,encoded);
        Assert.Throws<InvalidDataException>(()=>store.OpenOrCreate(false));
        Assert.Throws<InvalidDataException>(()=>store.ChooseStarter(catalogue,"bugaloo"));
        Assert.AreEqual(encoded,File.ReadAllText(Primary));
    }
    [TestCase("envelope")]
    [TestCase("profile")]
    public void FutureVersionIsPreservedInsteadOfDowngradingToBackup(string level)
    {
        store.OpenOrCreate(false);store.ChooseStarter(catalogue,"bugaloo");
        var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary));
        if(level=="envelope")envelope.Version=2;
        else
        {
            var profile=JsonUtility.FromJson<LocalGuestProfile>(envelope.Payload);profile.Version=LocalGuestProfile.CurrentVersion+1;
            envelope.Payload=JsonUtility.ToJson(profile);envelope.Sha256=Hash(envelope.Payload);
        }
        string future=JsonUtility.ToJson(envelope);File.WriteAllText(Primary,future);string backup=File.ReadAllText(Backup);
        Assert.Throws<NotSupportedException>(()=>store.OpenOrCreate(false));
        Assert.Throws<NotSupportedException>(()=>store.ChooseStarter(catalogue,"popow"));
        Assert.AreEqual(future,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void CompetingWriterCanBeRetriedWithoutReadingAnInterruptedTemporary()
    {
        var initial=store.OpenOrCreate(false);string before=File.ReadAllText(Primary);
        File.WriteAllText(Path.Combine(directory,"guest-profile.interrupted.tmp"),"partial");
        using(new FileStream(Path.Combine(directory,"guest-profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            Assert.Throws<IOException>(()=>store.ChooseStarter(catalogue,"popow"));
            Assert.AreEqual(before,File.ReadAllText(Primary));
        }
        Assert.AreEqual(initial.ProfileId,store.OpenOrCreate(false).ProfileId);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,"popow"));
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.AlreadySelected,store.ChooseStarter(catalogue,"popow"));
    }
    [Test] public void BootstrapCapturesLegacyBeforeNewStoresAreCreatedAndIgnoresQAAndSettings()
    {
        var preferences=new HashSet<string>(StringComparer.Ordinal);
        var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string root=Path.GetFullPath(directory);
        files.Add(Path.Combine(root,"RoadmapQA","qa-profile.json"));
        files.Add(Path.Combine(root,"PlayerProgress","progress.lock"));
        files.Add(Path.Combine(root,"PlayerDecks","decks.interrupted.tmp"));
        preferences.Add("monster-pouch.language");preferences.Add("monster-pouch.volume");
        bool captured=GuestProfileBootstrap.CaptureExistingPlayerData(root,preferences.Contains,files.Contains);
        Assert.IsFalse(captured);
        files.Add(Path.Combine(root,"PlayerProgress","progress.json"));
        Assert.IsTrue(GuestProfileBootstrap.CaptureExistingPlayerData(root,preferences.Contains,files.Contains));
        Assert.IsFalse(store.OpenOrCreate(captured).HasLegacyAccess,"Use the captured pre-initialization value, not newly created XP/deck files.");
        files.Clear();preferences.Add("monster-pouch.decks.v1");
        Assert.IsTrue(GuestProfileBootstrap.CaptureExistingPlayerData(root,preferences.Contains,files.Contains));
    }
    [TestCase("PlayerProgress","progress.json")]
    [TestCase("PlayerProgress","progress.backup.json")]
    [TestCase("PlayerDecks","decks.json")]
    [TestCase("PlayerDecks","decks.backup.json")]
    public void BootstrapRecognizesOnlyCommittedPlayerFilesIncludingRecoveryCopies(string folder,string filename)
    {
        string expected=Path.Combine(Path.GetFullPath(directory),folder,filename);
        Assert.IsTrue(GuestProfileBootstrap.CaptureExistingPlayerData(directory,_=>false,path=>path==expected));
    }
    static string Hash(string value)
    {
        using(var hash=SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
    }
}

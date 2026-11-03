using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class LocalDeckStarterTests
{
    string directory,profileId;
    MatchConfig catalogue;
    LocalDeckStore store;
    string Primary=>Path.Combine(directory,"decks.json");
    string Backup=>Path.Combine(directory,"decks.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchDeckStarterTests",Guid.NewGuid().ToString("N"));
        profileId=Guid.NewGuid().ToString("N");
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        store=new LocalDeckStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchDeckStarterTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    DeckLibrary Load(string json="")=>store.LoadOrImport(json,catalogue,"anuik",new[]{"stein","kayon"});
    StarterSelection Selection(string monster="popow")
    {
        Assert.IsTrue(StarterRoster.TryBuild(monster,catalogue,out var deck));
        return new StarterSelection{ReceiptId=StarterRoster.CurrentReceiptId,TimestampUtc=DateTime.UtcNow.ToString("O"),Deck=deck};
    }
    [TestCase("bugaloo")]
    [TestCase("popow")]
    public void FirstApplicationChangesOnlyDeckZeroAndKeepsTheOtherSlots(string monster)
    {
        var initial=DeckLibrary.Restore(null,catalogue,"anuik",new[]{"stein","kayon"});
        initial.Active=1;initial.Assign(-1,"sepora",catalogue);
        initial.Active=2;initial.Assign(-1,"tauris",catalogue);
        string one=JsonUtility.ToJson(initial.Decks[1]),two=JsonUtility.ToJson(initial.Decks[2]);
        Load(initial.Serialize());string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Applied,store.ApplyStarterSelection(profileId,Selection(monster),0,catalogue));
        var result=Load();Assert.AreEqual(0,result.Active);Assert.AreEqual(monster,result.Current.Monster);
        CollectionAssert.AreEqual(StarterRoster.Whelps,result.Current.Whelps.Where(id=>!string.IsNullOrEmpty(id)));
        Assert.AreEqual(7,result.Current.Whelps.Length);Assert.IsTrue(store.HasConfirmedInventoryRoster(profileId));
        Assert.AreEqual(one,JsonUtility.ToJson(result.Decks[1]));Assert.AreEqual(two,JsonUtility.ToJson(result.Decks[2]));
        Assert.AreEqual(1,store.Revision);Assert.AreEqual(before,File.ReadAllText(Backup));
        var record=ReadRecord();Assert.AreEqual(LocalDeckStore.CurrentVersion,record.Version);
        Assert.AreEqual(1,record.StarterApplications.Length);Assert.AreEqual(profileId,record.StarterApplications[0].ProfileId);
        Assert.AreEqual(monster,record.StarterApplications[0].MonsterId);
    }
    [Test] public void RetryAfterPersonalizingAndRestartDoesNotRestoreTheStarterDeck()
    {
        Load();var selection=Selection();store.ApplyStarterSelection(profileId,selection,0,catalogue);
        var custom=Load();custom.Assign(-1,"anuik",catalogue);custom.Assign(0,"stein",catalogue);custom.Active=2;
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(custom,store.Revision,catalogue));
        string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var restarted=new LocalDeckStore(directory);
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.AlreadyApplied,restarted.ApplyStarterSelection(profileId,selection,0,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        var loaded=Load();Assert.AreEqual(2,loaded.Active);Assert.AreEqual("anuik",loaded.Decks[0].Monster);
        Assert.AreEqual("stein",loaded.Decks[0].Whelps[0]);
    }
    [Test] public void AnotherProfileOrAnotherSelectedMonsterCannotReuseTheApplicationMarker()
    {
        Load();var selection=Selection();store.ApplyStarterSelection(profileId,selection,0,catalogue);
        string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Conflict,store.ApplyStarterSelection(Guid.NewGuid().ToString("N"),selection,1,catalogue));
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Conflict,store.ApplyStarterSelection(profileId,Selection("bugaloo"),1,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void ACustomizedDeckOrWrongExpectedRevisionCannotBeOverwrittenOnFirstApplication()
    {
        var library=Load();var selection=Selection();string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Conflict,store.ApplyStarterSelection(profileId,selection,1,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        library.Assign(-1,"sepora",catalogue);store.Save(library,0,catalogue);before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Conflict,store.ApplyStarterSelection(profileId,selection,store.Revision,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(ReadRecord().StarterApplications);
    }
    [Test] public void PersistedGuestSelectionCanRetryADeckFailureAfterRestartWithoutChangingItsMonster()
    {
        var guest=new LocalGuestProfileStore(Path.Combine(directory,"guest"));
        var profile=guest.OpenOrCreate(false);profileId=profile.ProfileId;
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,guest.ChooseStarter(catalogue,"popow"));
        profile=guest.OpenOrCreate(false);Load();string before=File.ReadAllText(Primary);
        using(new FileStream(Path.Combine(directory,"decks.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>store.ApplyStarterSelection(profileId,profile.Starter,0,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(ReadRecord().StarterApplications);
        var reopenedGuest=new LocalGuestProfileStore(Path.Combine(directory,"guest"));
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Conflict,reopenedGuest.ChooseStarter(catalogue,"bugaloo"));
        var restarted=new LocalDeckStore(directory);profile=reopenedGuest.OpenOrCreate(false);
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Applied,restarted.ApplyStarterSelection(profileId,profile.Starter,0,catalogue));
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.AlreadyApplied,restarted.ApplyStarterSelection(profileId,profile.Starter,0,catalogue));
        Assert.AreEqual("popow",Load().Current.Monster);Assert.AreEqual(1,ReadRecord().StarterApplications.Length);
    }
    [Test] public void CorruptedCommittedApplicationCannotReplayFromItsBackup()
    {
        Load();var selection=Selection();store.ApplyStarterSelection(profileId,selection,0,catalogue);
        string backup=File.ReadAllText(Backup);File.WriteAllText(Primary,"damaged starter application");
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.RecoveryRequired,store.ApplyStarterSelection(profileId,selection,0,catalogue));
        Assert.AreEqual("damaged starter application",File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void MissingApprovedCharacterLeavesBothDeckAndMarkerUnchanged()
    {
        Load();var selection=Selection();string before=File.ReadAllText(Primary);
        catalogue.Units=catalogue.Units.Where(u=>u.Id!="atori").ToArray();
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.UnavailableCharacters,store.ApplyStarterSelection(profileId,selection,0,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(ReadRecord().StarterApplications);
    }
    [TestCase("profile")]
    [TestCase("receipt")]
    [TestCase("whelps")]
    public void InvalidSelectionDoesNotChangeTheDeck(string invalid)
    {
        Load();var selection=Selection();string before=File.ReadAllText(Primary);
        if(invalid=="profile")profileId="not-a-profile";
        if(invalid=="receipt")selection.ReceiptId="invented-receipt";
        if(invalid=="whelps")selection.Deck.Whelps[0]="stein";
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.InvalidSelection,store.ApplyStarterSelection(profileId,selection,0,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void V1MigrationPreservesBytesUntilTheAtomicStarterApplication()
    {
        var library=Load();var current=ReadRecord();
        string original=Encode(JsonUtility.ToJson(new LegacyRecord{
            Version=1,Revision=0,DecksJson=library.Serialize(),OriginalLegacyJson=current.OriginalLegacyJson,
            OriginalLegacyMonster=current.OriginalLegacyMonster,OriginalLegacyWhelps=current.OriginalLegacyWhelps
        }));
        File.WriteAllText(Primary,original);
        Assert.AreEqual(library.Serialize(),Load().Serialize());Assert.AreEqual(original,File.ReadAllText(Primary));
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Applied,store.ApplyStarterSelection(profileId,Selection(),0,catalogue));
        Assert.AreEqual(original,File.ReadAllText(Backup));Assert.AreEqual(LocalDeckStore.CurrentVersion,ReadRecord().Version);
        Assert.AreEqual(1,ReadRecord().StarterApplications.Length);
    }
    [Serializable] sealed class Envelope{public int Version=1;public string Payload,Sha256;}
    [Serializable] sealed class ApplicationRecord{public string ProfileId,ReceiptId,MonsterId;public string[] WhelpIds;public long Revision;}
    [Serializable] sealed class Record
    {
        public int Version;public long Revision;public string DecksJson,OriginalLegacyJson,OriginalLegacyMonster;
        public string[] OriginalLegacyWhelps;public ApplicationRecord[] StarterApplications;
    }
    [Serializable] sealed class LegacyRecord
    {
        public int Version;public long Revision;public string DecksJson,OriginalLegacyJson,OriginalLegacyMonster;
        public string[] OriginalLegacyWhelps;
    }
    Record ReadRecord()=>JsonUtility.FromJson<Record>(JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary)).Payload);
    static string Encode(string payload)
    {
        string checksum;using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        return JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=checksum});
    }
}

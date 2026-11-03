using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class DeckAvailableRosterTests
{
    MatchConfig config;
    string directory,profileId;
    LocalDeckStore store;
    string Primary=>Path.Combine(directory,"decks.json");
    [SetUp] public void Setup()
    {
        config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchAvailableRosterTests",Guid.NewGuid().ToString("N"));
        profileId=Guid.NewGuid().ToString("N");store=new LocalDeckStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(config);
        string expected=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchAvailableRosterTests"))+Path.DirectorySeparatorChar;
        string actual=Path.GetFullPath(directory);Assert.IsTrue(actual.StartsWith(expected,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(actual))Directory.Delete(actual,true);
    }
    DeckLibrary Library()=>DeckLibrary.Restore(null,config,"bugaloo",StarterRoster.Whelps);
    DeckLibrary Load()=>store.LoadOrImport(null,config,"bugaloo",StarterRoster.Whelps);
    static readonly string[] Extra={"flo","gochan","trimol","atong","hymay"};

    [Test] public void ThreeStartersUseSevenSlotsAndNoUnapprovedFill()
    {
        var library=Library();Assert.IsFalse(library.NormalizeAvailableWhelps(config,StarterRoster.Whelps));
        foreach(var deck in library.Decks.Where(d=>!d.IsEmpty))
        {Assert.AreEqual(7,deck.Whelps.Length);CollectionAssert.AreEqual(StarterRoster.Whelps,deck.Whelps.Where(id=>id!=null));}
        Assert.IsTrue(library.CanPlayAll(config,StarterRoster.Whelps,out _));
        Assert.IsFalse(library.Assign(3,"flo",config,StarterRoster.Whelps));
        Assert.IsFalse(library.Remove(0,config,StarterRoster.Whelps));
    }
    [Test] public void EachUnlockFillsEmptySlotsStablyUntilSevenWithoutReplacingCustomSlots()
    {
        var library=Library();Assert.IsTrue(library.Assign(6,"atori",config));
        var available=StarterRoster.Whelps.ToList();
        for(int i=0;i<Extra.Length;i++)
        {
            var before=library.Current.Whelps.ToArray();available.Add(Extra[i]);
            Assert.AreEqual(i<4,library.NormalizeAvailableWhelps(config,available));
            for(int slot=0;slot<7;slot++)if(before[slot]!=null)Assert.AreEqual(before[slot],library.Current.Whelps[slot]);
            Assert.AreEqual(Math.Min(7,available.Count),library.Current.Whelps.Count(id=>id!=null));
            Assert.AreEqual(library.Current.Whelps.Count(id=>id!=null),library.Current.Whelps.Where(id=>id!=null).Distinct().Count());
            Assert.IsTrue(library.CanPlayAll(config,available,out _));
            Assert.IsFalse(library.Remove(1,config,available));
        }
        Assert.IsFalse(library.Current.Whelps.Contains(Extra[4]));
        Assert.IsTrue(library.Assign(2,Extra[4],config,available));
        Assert.AreEqual(Extra[4],library.Current.Whelps[2]);Assert.IsTrue(library.CanPlayCurrent(config,available,out _));
    }
    [Test] public void BadInventoryEntriesNeverBecomeCharactersAndOrderIsDeterministic()
    {
        var library=Library();var available=StarterRoster.Whelps.Concat(new[]{"flo","flo",null,"","missing","bugaloo"});
        Assert.IsTrue(library.NormalizeAvailableWhelps(config,available));
        CollectionAssert.AreEqual(StarterRoster.Whelps.Concat(new[]{"flo"}),library.Current.Whelps.Where(id=>id!=null));
        Assert.IsFalse(library.NormalizeAvailableWhelps(config,available));
        Assert.IsFalse(library.Assign(3,"hymay",config,available));
    }
    [Test] public void APartialOrUnavailableDeckCannotEnterBeforeNormalization()
    {
        var library=Library();var available=StarterRoster.Whelps.Concat(new[]{"flo","gochan","trimol","atong"}).ToArray();
        Assert.IsFalse(library.CanPlayCurrent(config,available,out _));
        library.NormalizeAvailableWhelps(config,available);Assert.IsTrue(library.CanPlayCurrent(config,available,out _));
        library.Current.Whelps[0]="hymay";Assert.IsFalse(library.CanPlayCurrent(config,available,out _));
        library.Current.Whelps[0]=library.Current.Whelps[1];Assert.IsFalse(library.CanPlayCurrent(config,available,out _));
        Assert.IsFalse(library.CanPlayCurrent(config,Array.Empty<string>(),out _));
        library.NormalizeAvailableWhelps(config,available);Assert.IsTrue(library.CanPlayCurrent(config,available,out _));
    }
    [Test] public void LegacyAccessIsExplicitlyUnrestrictedWithoutClaimingInventory()
    {
        var library=DeckLibrary.Restore(null,config,"sepora",new[]{"hymay","kayon"});string before=library.Serialize();
        Assert.IsFalse(library.NormalizeAvailableWhelps(config,null));Assert.AreEqual(before,library.Serialize());
        Assert.IsTrue(library.Assign(1,"stein",config,null));Assert.IsTrue(library.Remove(0,config,null));
        Assert.IsTrue(library.CanPlayCurrent(config,null,out _));
    }
    [Test] public void SaveRequiresAllAvailableWhelpsAndPersistsExplicitConfirmationAtomically()
    {
        var library=Load();string original=File.ReadAllText(Primary);
        var available=StarterRoster.Whelps.Concat(new[]{"flo"}).ToArray();
        Assert.AreEqual(LocalDeckStore.SaveResult.IncompleteRoster,store.Save(library,0,config,available,profileId));
        Assert.AreEqual(original,File.ReadAllText(Primary));Assert.IsFalse(store.HasConfirmedInventoryRoster(profileId));
        library.NormalizeAvailableWhelps(config,available);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,0,config,available,profileId));
        Assert.IsTrue(store.HasConfirmedInventoryRoster(profileId));string committed=File.ReadAllText(Primary);
        var restarted=new LocalDeckStore(directory);var restored=restarted.LoadOrImport(null,config,"bugaloo",null);
        Assert.IsTrue(restarted.HasConfirmedInventoryRoster(profileId));Assert.AreEqual(library.Serialize(),restored.Serialize());
        Assert.AreEqual(LocalDeckStore.SaveResult.Unchanged,restarted.Save(restored,0,config,available,profileId));
        Assert.AreEqual(committed,File.ReadAllText(Primary));
    }
    [Test] public void ConcurrentUnapprovedChoiceRemainsOnDiskUntilExplicitLegalRepair()
    {
        var initial=Load();long revision=store.Revision;
        var other=new LocalDeckStore(directory);var modified=other.LoadOrImport(null,config,"bugaloo",null);
        modified.Assign(0,"hymay",config);Assert.AreEqual(LocalDeckStore.SaveResult.Saved,other.Save(modified,revision,config));
        string concurrent=File.ReadAllText(Primary);
        Assert.AreEqual(LocalDeckStore.SaveResult.Conflict,store.Save(initial,revision,config,StarterRoster.Whelps,profileId));
        Assert.AreEqual(concurrent,File.ReadAllText(Primary));
        var view=Load();Assert.AreEqual("hymay",view.Current.Whelps[0]);
        Assert.IsFalse(view.CanPlayCurrent(config,StarterRoster.Whelps,out _));Assert.IsFalse(store.HasConfirmedInventoryRoster(profileId));
        view.NormalizeAvailableWhelps(config,StarterRoster.Whelps);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(view,store.Revision,config,StarterRoster.Whelps,profileId));
        Assert.IsTrue(Load().CanPlayAll(config,StarterRoster.Whelps,out _));
    }
    [Test] public void HistoricalSevenStarterReceiptLoadsByteForByteAndCannotReplayThreeOverIt()
    {
        Load();var legacy=new StarterSelection{ReceiptId=StarterRoster.LegacyReceiptId,TimestampUtc=DateTime.UtcNow.ToString("O"),
            Deck=new SavedDeck{Monster="popow",Whelps=StarterRoster.LegacyWhelps.ToArray()}};
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Applied,store.ApplyStarterSelection(profileId,legacy,0,config));
        var record=JsonUtility.FromJson<Record>(JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary)).Payload);
        record.Version=2;record.InventoryProfileId=null;
        string historic=Encode(JsonUtility.ToJson(record));File.WriteAllText(Primary,historic);
        CollectionAssert.AreEqual(StarterRoster.LegacyWhelps,Load().Current.Whelps);Assert.AreEqual(historic,File.ReadAllText(Primary));
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.AlreadyApplied,store.ApplyStarterSelection(profileId,legacy,0,config));
        Assert.IsTrue(StarterRoster.TryBuild("popow",config,out var starter));
        Assert.AreEqual(LocalDeckStore.StarterApplyResult.Conflict,store.ApplyStarterSelection(profileId,new StarterSelection{
            ReceiptId=StarterRoster.CurrentReceiptId,TimestampUtc=DateTime.UtcNow.ToString("O"),Deck=starter},1,config));
        Assert.AreEqual(historic,File.ReadAllText(Primary));
    }
    [Serializable] sealed class Envelope{public int Version=1;public string Payload,Sha256;}
    [Serializable] sealed class ApplicationRecord{public string ProfileId,ReceiptId,MonsterId;public string[] WhelpIds;public long Revision;}
    [Serializable] sealed class Record
    {
        public int Version;public long Revision;public string DecksJson,OriginalLegacyJson,OriginalLegacyMonster,InventoryProfileId;
        public string[] OriginalLegacyWhelps;public ApplicationRecord[] StarterApplications;
    }
    static string Encode(string payload)
    {
        string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        return JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=hash});
    }
}

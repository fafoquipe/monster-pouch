using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

public sealed class LocalDeckStoreTests
{
    [Serializable] sealed class Envelope { public int Version; public string Payload,Sha256; }
    [Serializable] sealed class Record { public int Version; public long Revision; public string DecksJson,OriginalLegacyJson,OriginalLegacyMonster; public string[] OriginalLegacyWhelps; }
    string directory;
    MatchConfig catalogue;
    LocalDeckStore store;
    string Primary=>Path.Combine(directory,"decks.json");
    string Backup=>Path.Combine(directory,"decks.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchDeckTests",Guid.NewGuid().ToString("N"));
        catalogue=MatchConfig.CreateDocumented();store=new LocalDeckStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchDeckTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    DeckLibrary Existing()
    {
        var library=DeckLibrary.Restore(null,catalogue,"anuik",new[]{"stein","kayon"});
        library.Assign(0,"flo",catalogue);
        library.Active=1;library.Assign(-1,"popow",catalogue);
        library.Active=2;library.Assign(-1,"bugaloo",catalogue);
        library.Active=0;return library;
    }
    DeckLibrary Load(string legacy=null)=>store.LoadOrImport(legacy??"",catalogue,"anuik",new[]{"stein","kayon"});
    [Test] public void ImportsAllThreeDecksAndExactLegacyOnceWithoutRenamingIds()
    {
        string legacy=Existing().Serialize();
        var loaded=Load(legacy);
        Assert.AreEqual(legacy,loaded.Serialize());
        var record=JsonUtility.FromJson<Record>(JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary)).Payload);
        Assert.AreEqual(legacy,record.OriginalLegacyJson);
        Assert.AreEqual("anuik",record.OriginalLegacyMonster);
        CollectionAssert.AreEqual(new[]{"stein","kayon"},record.OriginalLegacyWhelps);
        loaded.Active=2;Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(loaded,store.Revision,catalogue));
        string before=File.ReadAllText(Primary);
        var reopened=new LocalDeckStore(directory).LoadOrImport("damaged stale legacy",catalogue,"popow",null);
        Assert.AreEqual(2,reopened.Active);Assert.AreEqual("bugaloo",reopened.Current.Monster);
        Assert.AreEqual("anuik",reopened.Decks[0].Monster);Assert.AreEqual("flo",reopened.Decks[0].Whelps[0]);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void RepeatedSaveDoesNotRotateBackupsAndStaleChoiceCannotOverwriteAnotherSession()
    {
        var first=Load(Existing().Serialize());long firstRevision=store.Revision;
        var other=new LocalDeckStore(directory);
        var stale=other.LoadOrImport("",catalogue,"anuik",null);long staleRevision=other.Revision;
        first.Active=1;
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(first,firstRevision,catalogue));
        string current=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        Assert.AreEqual(LocalDeckStore.SaveResult.Unchanged,store.Save(first,firstRevision,catalogue));
        stale.Active=2;
        Assert.AreEqual(LocalDeckStore.SaveResult.Conflict,other.Save(stale,staleRevision,catalogue));
        Assert.AreEqual(current,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        Assert.AreEqual(1,Load().Active);
    }
    [TestCase("missing")]
    [TestCase("json")]
    [TestCase("checksum")]
    public void MissingOrCorruptPrimaryOnlyShowsBackupAndNeverOverwritesIt(string damage)
    {
        var library=Load(Existing().Serialize());library.Active=1;store.Save(library,store.Revision,catalogue);
        if(damage=="missing")File.Delete(Primary);
        else if(damage=="json")File.WriteAllText(Primary,"corrupt");
        else
        {
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary));envelope.Sha256="wrong";
            File.WriteAllText(Primary,JsonUtility.ToJson(envelope));
        }
        string damaged=File.Exists(Primary)?File.ReadAllText(Primary):null,backup=File.ReadAllText(Backup);
        var recovered=Load();Assert.AreEqual(0,recovered.Active);Assert.IsTrue(store.NeedsRecovery);
        recovered.Active=2;
        Assert.AreEqual(LocalDeckStore.SaveResult.RecoveryRequired,store.Save(recovered,store.Revision,catalogue));
        Assert.AreEqual(backup,File.ReadAllText(Backup));
        Assert.AreEqual(damaged,File.Exists(Primary)?File.ReadAllText(Primary):null);
    }
    [TestCase("{}")]
    [TestCase("broken json")]
    [TestCase("{\"Active\":0,\"Decks\":[]}")]
    public void InvalidLegacyDoesNotCreateAReplacementSave(string legacy)
    {
        Assert.Catch<Exception>(()=>Load(legacy));
        Assert.IsFalse(File.Exists(Primary));Assert.IsFalse(File.Exists(Backup));
    }
    [Test] public void TemporarilyMissingCharacterCannotBeRemovedFromSavedDecks()
    {
        string legacy=Existing().Serialize();Load(legacy);
        string before=File.ReadAllText(Primary);
        var all=catalogue.Units;catalogue.Units=all.Where(u=>u.Id!="flo").ToArray();
        var display=Load();Assert.IsTrue(store.NeedsCatalogue);Assert.IsNull(display.Decks[0].Whelps[0]);
        display.Active=1;
        Assert.AreEqual(LocalDeckStore.SaveResult.UnavailableCharacters,store.Save(display,store.Revision,catalogue));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        catalogue.Units=all;
        Assert.AreEqual(legacy,Load().Serialize());Assert.IsFalse(store.NeedsCatalogue);
    }
    [TestCase("envelope")]
    [TestCase("record")]
    public void FutureVersionsCannotFallBackOrOverwriteData(string level)
    {
        var library=Load();library.Active=1;store.Save(library,store.Revision,catalogue);
        var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(Primary));
        if(level=="envelope")envelope.Version=2;
        else
        {
            var record=JsonUtility.FromJson<Record>(envelope.Payload);record.Version=LocalDeckStore.CurrentVersion+1;
            envelope.Payload=JsonUtility.ToJson(record);envelope.Sha256=Hash(envelope.Payload);
        }
        string future=JsonUtility.ToJson(envelope);File.WriteAllText(Primary,future);string backup=File.ReadAllText(Backup);
        Assert.Throws<NotSupportedException>(()=>Load());
        Assert.Throws<NotSupportedException>(()=>store.Save(library,store.Revision,catalogue));
        Assert.AreEqual(future,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void CompetingWriterAndInterruptedTemporaryFileCannotReplaceSavedChoices()
    {
        var library=Load(Existing().Serialize());library.Active=2;
        long revision=store.Revision;string before=File.ReadAllText(Primary);
        File.WriteAllText(Path.Combine(directory,"decks.interrupted.tmp"),"unfinished");
        using(new FileStream(Path.Combine(directory,"decks.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            Assert.Throws<IOException>(()=>store.Save(library,revision,catalogue));
            Assert.AreEqual(before,File.ReadAllText(Primary));
        }
        Assert.AreEqual(0,Load().Active);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,revision,catalogue));
        Assert.AreEqual(2,Load().Active);
    }
    static string Hash(string value)
    {
        using(var hash=SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
    }
}

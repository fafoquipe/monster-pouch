using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

public sealed class FiveDeckMigrationTests
{
    MatchConfig config;
    string directory;
    [SetUp] public void Setup()
    {
        config=MatchConfig.CreateDocumented();
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchFiveDeckTests",Guid.NewGuid().ToString("N"));
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(config);
        var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchFiveDeckTests"))+Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(directory).StartsWith(allowed,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    [Test] public void LegacyThreeDecksRemainExactAndTwoBlanksSurviveSelectionAndReload()
    {
        var old=DeckLibrary.Restore(null,config,"anuik",new[]{"stein","kayon"});
        old.Decks=old.Decks.Take(3).ToArray();old.Active=1;old.Assign(-1,"sepora",config);
        old.Active=2;old.Assign(-1,"bugaloo",config);old.Assign(0,"flo",config);
        string legacy=old.Serialize();string[] originals=old.Decks.Select(d=>JsonUtility.ToJson(d)).ToArray();
        var store=new LocalDeckStore(directory);
        var expanded=store.LoadOrImport(legacy,config,"anuik",new[]{"stein","kayon"});
        Assert.AreEqual(5,expanded.Decks.Length);Assert.AreEqual(2,expanded.Active);
        CollectionAssert.AreEqual(originals,expanded.Decks.Take(3).Select(d=>JsonUtility.ToJson(d)));
        Assert.IsTrue(expanded.Decks.Skip(3).All(d=>d.IsEmpty));
        string primary=File.ReadAllText(Path.Combine(directory,"decks.json"));
        store.LoadOrImport("stale",config,"anuik",null);
        Assert.AreEqual(primary,File.ReadAllText(Path.Combine(directory,"decks.json")),"Read does not rewrite a legacy payload.");
        expanded.Active=4;
        Assert.IsFalse(expanded.CanPlayCurrent(config,null,out _),"A blank selection cannot start combat.");
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(expanded,store.Revision,config));
        var reopened=new LocalDeckStore(directory).LoadOrImport("",config,"anuik",null);
        Assert.AreEqual(4,reopened.Active);Assert.IsTrue(reopened.Current.IsEmpty);
        CollectionAssert.AreEqual(originals,reopened.Decks.Take(3).Select(d=>JsonUtility.ToJson(d)));
        Assert.IsTrue(reopened.Assign(-1,"sepora",config));
        Assert.IsFalse(reopened.Current.IsEmpty);Assert.IsTrue(reopened.Decks[3].IsEmpty);
        CollectionAssert.AreEqual(originals,reopened.Decks.Take(3).Select(d=>JsonUtility.ToJson(d)));
    }
    [Test] public void InventoryNormalizationNeverPopulatesUntouchedBlankDecks()
    {
        var library=DeckLibrary.Restore(null,config,"anuik",new[]{"stein","kayon"});
        library.NormalizeAvailableWhelps(config,new[]{"stein","kayon","flo"});
        library.NormalizeAvailableMonsters(config,new[]{"anuik"});
        Assert.IsTrue(library.Decks.Skip(3).All(d=>d.IsEmpty));
        Assert.AreEqual(7,library.Decks[4].Whelps.Length,"Team slots remain seven, independent of five decks.");
        library.Active=4;Assert.IsFalse(library.CanPlayCurrent(config,new[]{"stein","kayon","flo"},out _,new[]{"anuik"}));
    }
}

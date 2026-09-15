using System.Linq;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

public class DeckLibraryTests
{
    MatchConfig config;
    [SetUp] public void Setup(){config=MatchConfig.CreateDocumented();}
    [TearDown] public void Cleanup(){Object.DestroyImmediate(config);}
    [Test] public void ThreeDecksKeepIndependentSelectionsAcrossSerialization()
    {
        var library=DeckLibrary.Restore(null,config,"anuik",new[]{"stein","kayon"});
        library.Assign(0,"flo",config);library.Active=1;library.Assign(-1,"sepora",config);
        var restored=DeckLibrary.Restore(library.Serialize(),config,"bugaloo",null);
        Assert.AreEqual(1,restored.Active);Assert.AreEqual("sepora",restored.Current.Monster);
        Assert.AreEqual("stein",restored.Current.Whelps[0]);
        restored.Active=0;Assert.AreEqual("flo",restored.Current.Whelps[0]);Assert.AreEqual("anuik",restored.Current.Monster);
    }
    [Test] public void DuplicateSlotSelectionSwapsAndNeverDuplicatesAFigure()
    {
        var library=DeckLibrary.Restore(null,config,"anuik",new[]{"stein","kayon"});
        Assert.IsTrue(library.Assign(1,"stein",config));
        Assert.AreEqual("kayon",library.Current.Whelps[0]);Assert.AreEqual("stein",library.Current.Whelps[1]);
        Assert.IsFalse(library.Assign(2,"tauris",config));Assert.IsFalse(library.Assign(-1,"stein",config));
    }
    [Test] public void DamagedOrOutdatedSaveIsRepairedToAPlayableDeck()
    {
        var library=DeckLibrary.Restore("{\"Active\":20,\"Decks\":[{\"Monster\":\"missing\",\"Whelps\":[\"dummy\",\"dummy\"]}]}",config,"missing",null);
        Assert.AreEqual(2,library.Active);
        foreach(var deck in library.Decks)
        {
            Assert.IsTrue(config.Get(deck.Monster).IsMonster);
            Assert.IsTrue(config.TryResolveWhelpSelection(deck.Whelps.Where(id=>id!=null),out _,out _));
        }
        var repaired=DeckLibrary.Restore("broken json",config,"anuik",new[]{"stein"});
        Assert.IsFalse(repaired.Remove(0));
    }
}

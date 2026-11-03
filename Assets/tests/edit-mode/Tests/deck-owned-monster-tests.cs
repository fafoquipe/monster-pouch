using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class DeckOwnedMonsterTests
{
    MatchConfig config;
    string directory,profileId;
    LocalDeckStore store;
    string Primary=>Path.Combine(directory,"decks.json");
    [SetUp] public void Setup()
    {
        config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchOwnedMonsterTests",Guid.NewGuid().ToString("N"));
        profileId=Guid.NewGuid().ToString("N");store=new LocalDeckStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(config);
        string expected=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchOwnedMonsterTests"))+Path.DirectorySeparatorChar;
        string actual=Path.GetFullPath(directory);Assert.IsTrue(actual.StartsWith(expected,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(actual))Directory.Delete(actual,true);
    }
    DeckLibrary Library(string monster="bugaloo")=>DeckLibrary.Restore(null,config,monster,StarterRoster.Whelps);
    DeckLibrary Load()=>store.LoadOrImport(null,config,"bugaloo",StarterRoster.Whelps);

    [TestCase("bugaloo","popow")]
    [TestCase("popow","bugaloo")]
    public void OnlyTheChosenStarterCanBeAssignedOrPlayedUntilAnotherMonsterIsOwned(string owned,string locked)
    {
        var library=Library(owned);var monsters=new[]{owned};string before=library.Serialize();
        Assert.IsFalse(library.Assign(-1,locked,config,StarterRoster.Whelps,monsters));
        Assert.IsFalse(library.Assign(-1,"sepora",config,StarterRoster.Whelps,monsters));
        Assert.AreEqual(before,library.Serialize());Assert.IsTrue(library.CanPlayCurrent(config,StarterRoster.Whelps,out _,monsters));
        library.Current.Monster=locked;
        Assert.IsFalse(library.CanPlayCurrent(config,StarterRoster.Whelps,out _,monsters));
    }
    [Test] public void ConfirmedRosterNormalizationReplacesOnlyUnownedDefaultsAndPreservesWhelpSlots()
    {
        var library=Library("popow");library.Decks[1].Monster="bugaloo";library.Decks[2].Monster="sepora";
        string[] whelps=library.Decks.Select(deck=>string.Join("|",deck.Whelps)).ToArray();
        Assert.IsTrue(library.NormalizeAvailableMonsters(config,new[]{"popow"}));
        Assert.IsTrue(library.Decks.Take(3).All(deck=>deck.Monster=="popow"));
        Assert.IsTrue(library.Decks.Skip(3).All(deck=>deck.IsEmpty));
        CollectionAssert.AreEqual(whelps,library.Decks.Select(deck=>string.Join("|",deck.Whelps)));
        Assert.IsFalse(library.NormalizeAvailableMonsters(config,new[]{"popow"}));
    }
    [Test] public void UnlockingAnotherMonsterDoesNotAutoSelectItOrReplaceValidDecks()
    {
        var library=Library();var owned=new[]{"bugaloo","popow"};string before=library.Serialize();
        Assert.IsFalse(library.NormalizeAvailableMonsters(config,owned));Assert.AreEqual(before,library.Serialize());
        Assert.IsTrue(library.Assign(-1,"popow",config,StarterRoster.Whelps,owned));
        Assert.AreEqual("popow",library.Current.Monster);Assert.AreEqual("bugaloo",library.Decks[1].Monster);
        Assert.IsFalse(library.NormalizeAvailableMonsters(config,owned));
        Assert.IsTrue(library.CanPlayAll(config,StarterRoster.Whelps,out _,owned));
    }
    [Test] public void AConflictingUnownedMonsterRemainsOnDiskUntilExplicitLegalRepair()
    {
        var library=Load();library.Current.Monster="sepora";
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,store.Revision,config));
        string original=File.ReadAllText(Primary);long revision=store.Revision;
        Assert.AreEqual(LocalDeckStore.SaveResult.IncompleteRoster,
            store.Save(library,revision,config,StarterRoster.Whelps,profileId,new[]{"bugaloo"}));
        Assert.AreEqual(original,File.ReadAllText(Primary));Assert.IsFalse(store.HasConfirmedInventoryRoster(profileId));
        Assert.IsFalse(library.CanPlayCurrent(config,StarterRoster.Whelps,out _,new[]{"bugaloo"}));
        library.NormalizeAvailableMonsters(config,new[]{"bugaloo"});
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,revision,config,StarterRoster.Whelps,profileId,new[]{"bugaloo"}));
        Assert.IsTrue(store.HasConfirmedInventoryRoster(profileId));Assert.AreEqual("bugaloo",Load().Current.Monster);
    }
    [Test] public void SavingChecksAllThreeDecksWhilePlayChecksOnlyTheCurrentOne()
    {
        var library=Load();library.Decks[1].Monster="popow";string before=File.ReadAllText(Primary);
        Assert.IsTrue(library.CanPlayCurrent(config,StarterRoster.Whelps,out _,new[]{"bugaloo"}));
        Assert.IsFalse(library.CanPlayAll(config,StarterRoster.Whelps,out _,new[]{"bugaloo"}));
        Assert.AreEqual(LocalDeckStore.SaveResult.IncompleteRoster,
            store.Save(library,store.Revision,config,StarterRoster.Whelps,profileId,new[]{"bugaloo"}));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void MissingOrInvalidOwnedMonsterIdsCannotEnableASelectionOrEraseTheSavedOne()
    {
        var library=Library();string before=library.Serialize();var invalid=new[]{"atori","missing",null,""};
        Assert.IsEmpty(DeckLibrary.AvailableMonsters(config,invalid));
        Assert.IsFalse(library.NormalizeAvailableMonsters(config,invalid));Assert.AreEqual(before,library.Serialize());
        Assert.IsFalse(library.Assign(-1,"bugaloo",config,StarterRoster.Whelps,invalid));
        Assert.IsFalse(library.CanPlayCurrent(config,StarterRoster.Whelps,out _,invalid));
        Assert.IsFalse(library.CanPlayCurrent(config,StarterRoster.Whelps,out _,Array.Empty<string>()));
    }
    [Test] public void LegacyDecksRetainEveryExistingMonsterChoiceWithoutCreatingOwnership()
    {
        var library=Library("anuik");library.Decks[2].Monster="sepora";string before=library.Serialize();
        Assert.IsFalse(library.NormalizeAvailableMonsters(config,null));Assert.AreEqual(before,library.Serialize());
        Assert.IsTrue(library.CanPlayAll(config,null,out _,null));
        Assert.IsTrue(library.Assign(-1,"tauris",config,null,null));Assert.AreEqual("tauris",library.Current.Monster);
    }
}

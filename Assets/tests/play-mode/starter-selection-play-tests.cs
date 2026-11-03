using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class StarterSelectionPlayTests
{
    [Serializable] sealed class Envelope { public int Version; public string Payload,Sha256; }
    [Serializable] sealed class GuestV3
    {
        public int Version=3;
        public string ProfileKind="local-guest",ProfileId;
        public long Revision;
        public bool HasLegacyAccess;
        public StarterSelection[] StarterSelections=Array.Empty<StarterSelection>();
        public GuestOnboardingStage OnboardingStage=GuestOnboardingStage.ChooseStarter;
        public string[] CompletedTutorialModules=Array.Empty<string>();
    }
    static readonly string[] OverrideNames={"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"};
    static readonly PropertyInfo[] Overrides=OverrideNames.Select(name=>typeof(LocalGameUI).GetProperty(name,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    static readonly FieldInfo DeckField=typeof(LocalGameUI).GetField("decks",BindingFlags.Instance|BindingFlags.NonPublic);
    readonly string[] keys={"monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;
    string[] previousDirectories,saved;
    bool[] existed;
    LocalGameUI ui;
    string GuestDirectory=>Path.Combine(root,"GuestProfile");
    string DeckDirectory=>Path.Combine(root,"PlayerDecks");
    string GuestPrimary=>Path.Combine(GuestDirectory,"guest-profile.json");
    string DeckPrimary=>Path.Combine(DeckDirectory,"decks.json");
    DeckLibrary Decks=>(DeckLibrary)DeckField.GetValue(ui);
    LocalGuestProfile ReadGuest()=>new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);

    [SetUp] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchStarterPlayTests",Guid.NewGuid().ToString("N"));
        previousDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,GuestDirectory);
        Overrides[1].SetValue(null,Path.Combine(root,"PlayerProgress"));
        Overrides[2].SetValue(null,DeckDirectory);
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);
    }
    IEnumerator LoadScene()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();}
        yield return null;
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,previousDirectories[i]);
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchStarterPlayTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(root);
        Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [UnityTest] public IEnumerator ChoosingPopowPersistsAcrossReloadAndDoesNotResetLaterDeckEdits()
    {
        yield return LoadScene();
        Assert.IsTrue(SelectorOpen);Assert.IsNull(ReadGuest().Starter);
        Assert.IsFalse(ButtonNamed("Confirm starter selection").interactable,"A new player must explicitly choose a Monster.");
        yield return ConfirmChoice("popow");
        AssertApplied("popow");
        string identity=ReadGuest().ProfileId,receipt=File.ReadAllText(GuestPrimary);
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);AssertApplied("popow");Assert.AreEqual(identity,ReadGuest().ProfileId);
        Assert.IsTrue(ui.Match.IsTraining,"A new account proceeds directly into its tutorial.");
        ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;
        ui.OpenCharacterPicker(0);yield return null;
        ButtonNamed("loadout-bugui").onClick.Invoke();yield return null;
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual("bugui",Decks.Current.Whelps[0]);
        string customized=File.ReadAllText(DeckPrimary);
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.AreEqual("bugui",Decks.Current.Whelps[0]);
        Assert.AreEqual(customized,File.ReadAllText(DeckPrimary));
        Assert.AreEqual(receipt,File.ReadAllText(GuestPrimary),"Editing a deck must not rewrite the initial choice receipt.");
    }
    [UnityTest] public IEnumerator ChoosingBugalooShowsThreeCenteredWhelpsWithCurrentNamesAndStartsTheirTutorial()
    {
        yield return LoadScene();
        Assert.IsFalse(ButtonNamed("Confirm starter selection").interactable);
        var panel=ui.CanvasRoot.Find("Starter selection/Starter panel") as RectTransform;
        var slots=panel.GetComponentsInChildren<RectTransform>().Where(rect=>rect.name.StartsWith("Starter Whelp ",StringComparison.Ordinal)).ToArray();
        CollectionAssert.AreEqual(new[]{"Starter Whelp atori","Starter Whelp bugui","Starter Whelp blotan"},slots.Select(rect=>rect.name));
        float left=panel.InverseTransformPoint(slots[0].TransformPoint(slots[0].rect.min)).x;
        float right=panel.InverseTransformPoint(slots[2].TransformPoint(slots[2].rect.max)).x;
        Assert.AreEqual(panel.rect.center.x,(left+right)*.5f,.01f,"The initial three-Whelp row must stay centered.");
        Assert.IsTrue(panel.GetComponentsInChildren<Text>().Any(text=>text.text=="Blotun"));
        Assert.IsFalse(panel.GetComponentsInChildren<Text>().Any(text=>text.text.Contains("SIETE")||text.text.Contains("SEVEN")));
        yield return Capture("starter-selection.png");
        yield return ConfirmChoice("bugaloo");
        AssertApplied("bugaloo");Assert.IsTrue(ui.Match.IsTraining,"The newly created account must enter training, not the lobby.");
        Assert.AreEqual(1,ReadGuest().StarterSelections.Count);
        Assert.IsFalse(keys.Any(PlayerPrefs.HasKey),"The initial choice must not overwrite migration source preferences.");
    }
    [UnityTest] public IEnumerator NewCollectionAllowsDetailsButRejectsUnavailableWhelpsAndRemovingRequiredSlots()
    {
        yield return LoadScene();yield return ConfirmChoice("bugaloo");
        ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;
        ui.OpenDecks();yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Single(text=>text.name=="Brief required count").text.StartsWith("3 / 3",StringComparison.Ordinal));
        string before=File.ReadAllText(DeckPrimary);
        ui.OpenCharacterPicker(0);yield return null;
        var unavailable=ButtonNamed("loadout-hymay");
        Assert.IsNotNull(unavailable.transform.Find("Unavailable Whelp"));
        AssertLockedPresentation(unavailable,true);
        yield return Capture("collection-locked-whelps.png");
        unavailable.onClick.Invoke();yield return null;
        Assert.IsTrue(ButtonNamed("character-preview-details").interactable);
        Assert.IsFalse(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary),"Even a queued callback must reject an unavailable Whelp.");
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(button=>button.name=="REMOVE FROM BRIEF"));
        AssertApplied("bugaloo");
    }
    [UnityTest] public IEnumerator UnlockReceiptsFillAllBriefsToSevenThenPreserveSelectionAndAllowAnOwnedReplacement()
    {
        yield return LoadScene();yield return ConfirmChoice("bugaloo");
        ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;
        var store=new LocalGuestProfileStore(GuestDirectory);
        var extra=ui.Match.Config.Units.Where(unit=>unit!=null&&!unit.IsMonster&&!StarterRoster.Whelps.Contains(unit.Id))
            .Select(unit=>unit.Id).Distinct().Take(5).ToArray();
        Assert.AreEqual(5,extra.Length);
        ui.OpenCharacterPicker(0);yield return null;
        AssertLockedPresentation(ButtonNamed("loadout-"+extra[4]),true);
        ui.CloseModal();
        string[] previous=Decks.Current.Whelps.ToArray();string fullDeck=null;
        for(int i=0;i<extra.Length;i++)
        {
            // These explicit receipts are confined to this fixture's temporary
            // profile; opening a collection never grants a real reward.
            Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,
                store.UnlockCharacter(ui.Match.Config,"play-unlock-"+i,extra[i],GuestUnlockSource.Tutorial));
            ui.OpenDecks();yield return null;yield return null;
            int required=Mathf.Min(7,StarterRoster.Whelps.Count+i+1);
            Assert.IsTrue(Decks.Decks.Skip(3).All(deck=>deck.IsEmpty),"New blank decks are never auto-filled.");
            foreach(var deck in Decks.Decks.Where(deck=>!deck.IsEmpty))
            {
                Assert.AreEqual(required,deck.Whelps.Count(id=>!string.IsNullOrEmpty(id)));
                Assert.AreEqual(required,deck.Whelps.Where(id=>!string.IsNullOrEmpty(id)).Distinct().Count());
            }
            for(int slot=0;slot<previous.Length;slot++)if(!string.IsNullOrEmpty(previous[slot]))
                Assert.AreEqual(previous[slot],Decks.Current.Whelps[slot],"Auto-fill preserves occupied slots.");
            if(i<4)Assert.Contains(extra[i],Decks.Current.Whelps);
            else
            {
                Assert.IsFalse(Decks.Current.Whelps.Contains(extra[i]),"The eighth available Whelp must not replace a chosen slot automatically.");
                Assert.AreEqual(fullDeck,File.ReadAllText(DeckPrimary));
            }
            previous=Decks.Current.Whelps.ToArray();
            if(required==7)fullDeck=File.ReadAllText(DeckPrimary);
        }
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Single(text=>text.name=="Brief required count").text.StartsWith("7 / 7",StringComparison.Ordinal));
        ui.OpenCharacterPicker(3);yield return null;
        AssertLockedPresentation(ButtonNamed("loadout-"+extra[4]),false);
        ButtonNamed("loadout-"+extra[4]).onClick.Invoke();yield return null;
        Assert.IsTrue(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual(extra[4],Decks.Current.Whelps[3]);Assert.AreEqual(7,Decks.Current.Whelps.Count(id=>!string.IsNullOrEmpty(id)));
        for(int slot=0;slot<previous.Length;slot++)if(slot!=3)Assert.AreEqual(previous[slot],Decks.Current.Whelps[slot]);
        string customized=File.ReadAllText(DeckPrimary),guest=File.ReadAllText(GuestPrimary);
        yield return LoadScene();
        Assert.AreEqual(customized,File.ReadAllText(DeckPrimary));Assert.AreEqual(guest,File.ReadAllText(GuestPrimary));
        Assert.AreEqual(extra[4],Decks.Current.Whelps[3]);
    }
    [UnityTest] public IEnumerator ChoosingBugrumLocksOtherMonstersUntilAnExplicitUnlockWithoutAutoSelectingThem()
    {
        yield return LoadScene();yield return ConfirmChoice("bugaloo");yield return ExitFirstContact();
        string beforeDeck=File.ReadAllText(DeckPrimary),beforeGuest=File.ReadAllText(GuestPrimary);
        ui.OpenCharacterPicker(-1);yield return null;
        var popow=ButtonNamed("loadout-popow");Assert.IsNotNull(popow.transform.Find("Unavailable Monster"));
        AssertLockedPresentation(popow,true);
        yield return Capture("collection-locked-monsters.png");
        popow.onClick.Invoke();yield return null;
        Assert.IsTrue(ButtonNamed("character-preview-details").interactable);
        Assert.IsFalse(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual("bugaloo",Decks.Current.Monster);Assert.IsFalse(ReadGuest().OwnsCharacter("popow"));
        Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));Assert.AreEqual(beforeGuest,File.ReadAllText(GuestPrimary));
        Assert.IsFalse(ui.GetComponent<OnlineRoom>().LocalTeamValidator("popow",ui.SelectedWhelps));
        var store=new LocalGuestProfileStore(GuestDirectory);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,
            store.UnlockCharacter(ui.Match.Config,"play-unlock-other-monster","popow",GuestUnlockSource.Tutorial));
        string unlocked=File.ReadAllText(GuestPrimary);
        ui.CloseModal();ui.OpenDecks();yield return null;yield return null;
        Assert.AreEqual("bugaloo",Decks.Current.Monster,"An unlocked Monster must not replace the chosen one automatically.");
        Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));Assert.AreEqual(3,ui.SelectedWhelps.Count);
        CollectionAssert.AreEqual(StarterRoster.Whelps,ReadGuest().GetAvailableWhelpIds(ui.Match.Config));
        ui.OpenCharacterPicker(-1);yield return null;
        popow=ButtonNamed("loadout-popow");Assert.IsNull(popow.transform.Find("Unavailable Monster"));
        AssertLockedPresentation(popow,false);
        popow.onClick.Invoke();yield return null;Assert.IsTrue(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual("popow",Decks.Current.Monster);Assert.AreEqual(3,ui.SelectedWhelps.Count);
        string customized=File.ReadAllText(DeckPrimary);
        yield return LoadScene();
        Assert.AreEqual("popow",Decks.Current.Monster);Assert.AreEqual("bugaloo",ReadGuest().Starter.Deck.Monster);
        Assert.AreEqual(customized,File.ReadAllText(DeckPrimary));Assert.AreEqual(unlocked,File.ReadAllText(GuestPrimary));
        yield return ExitFirstContact();
        Assert.IsTrue(ui.GetComponent<OnlineRoom>().LocalTeamValidator("popow",ui.SelectedWhelps));
        yield return AttemptBot();Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
        Assert.AreEqual("popow",ui.Match.Player.Monster.Definition.Id);Assert.IsFalse(ui.Match.IsTraining);
    }
    [UnityTest] public IEnumerator ConflictingUnownedMonsterCannotEnterBotOrRoomAndItsSavedDeckIsPreserved()
    {
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var guestStore=new LocalGuestProfileStore(GuestDirectory);guestStore.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,guestStore.ChooseStarter(catalogue,"bugaloo"));
        var store=new LocalDeckStore(DeckDirectory);
        var library=store.LoadOrImport(null,catalogue,"bugaloo",StarterRoster.Whelps);library.Assign(-1,"popow",catalogue);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,store.Revision,catalogue));
        string before=File.ReadAllText(DeckPrimary);UnityEngine.Object.Destroy(catalogue);
        yield return LoadScene();yield return ExitFirstContact();
        Assert.AreEqual("popow",Decks.Current.Monster);Assert.IsFalse(ReadGuest().OwnsCharacter("popow"));
        Assert.IsFalse(ui.GetComponent<OnlineRoom>().LocalTeamValidator("popow",ui.SelectedWhelps));
        yield return AttemptBot();Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        ui.OpenCharacterPicker(-1);yield return null;ButtonNamed("loadout-bugaloo").onClick.Invoke();yield return null;
        Assert.IsTrue(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual("bugaloo",Decks.Current.Monster);Assert.IsFalse(ReadGuest().OwnsCharacter("popow"));
        yield return AttemptBot();Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
        Assert.AreEqual("bugaloo",ui.Match.Player.Monster.Definition.Id);
    }
    [UnityTest] public IEnumerator LegacyMonsterSelectionRemainsAvailableWithoutCreatingOwnershipReceipts()
    {
        new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(true);
        yield return LoadScene();Assert.IsFalse(SelectorOpen);Assert.IsFalse(ui.Match.IsTraining);
        string beforeGuest=File.ReadAllText(GuestPrimary);
        ui.OpenCharacterPicker(-1);yield return null;
        var popow=ButtonNamed("loadout-popow");Assert.IsNull(popow.transform.Find("Unavailable Monster"));
        AssertLockedPresentation(popow,false);
        popow.onClick.Invoke();yield return null;Assert.IsTrue(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual("popow",Decks.Current.Monster);Assert.IsFalse(ReadGuest().OwnsCharacter("popow"));
        Assert.AreEqual(beforeGuest,File.ReadAllText(GuestPrimary));
        Assert.IsTrue(ui.GetComponent<OnlineRoom>().LocalTeamValidator("popow",ui.SelectedWhelps));
        yield return AttemptBot();Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
        Assert.AreEqual("popow",ui.Match.Player.Monster.Definition.Id);Assert.AreEqual(beforeGuest,File.ReadAllText(GuestPrimary));
    }
    [UnityTest] public IEnumerator FailedDeckWriteRetainsTheCommittedChoiceAndRetriesWithoutChoosingAnotherMonster()
    {
        yield return LoadScene();
        string beforeDeck=File.ReadAllText(DeckPrimary),beforeView=Decks.Serialize(),committedReceipt;
        using(new FileStream(Path.Combine(DeckDirectory,"decks.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            yield return ConfirmChoice("popow");
            Assert.IsTrue(SelectorOpen);Assert.AreEqual("popow",ReadGuest().Starter.Deck.Monster);
            committedReceipt=File.ReadAllText(GuestPrimary);
            Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));Assert.AreEqual(beforeView,Decks.Serialize());
            Assert.IsNotNull(ButtonNamed("Retry starter selection"));
            // A queued callback for the other button cannot replace the receipt.
            var other=ui.CanvasRoot.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Starter choice bugaloo");
            if(other!=null)other.onClick.Invoke();
            yield return null;
            Assert.AreEqual(committedReceipt,File.ReadAllText(GuestPrimary));
        }
        ButtonNamed("Retry starter selection").onClick.Invoke();yield return null;yield return null;
        AssertApplied("popow");Assert.AreEqual(committedReceipt,File.ReadAllText(GuestPrimary));
        yield return LoadScene();
        AssertApplied("popow");Assert.IsFalse(SelectorOpen);
    }
    [UnityTest] public IEnumerator LegacyAndUnreadableGuestsKeepTheirDecksWithoutOpeningTheStarterSelector()
    {
        var profile=new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(true);
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var library=DeckLibrary.Restore(null,catalogue,"anuik",new[]{"stein","kayon"});
        library.Active=2;library.Assign(-1,"sepora",catalogue);
        string legacy=library.Serialize();UnityEngine.Object.Destroy(catalogue);
        PlayerPrefs.SetString("monster-pouch.decks.v1",legacy);
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.AreEqual(legacy,Decks.Serialize());
        Assert.IsNull(ReadGuest().Starter);Assert.AreEqual(profile.ProfileId,ReadGuest().ProfileId);
        string before=File.ReadAllText(DeckPrimary);
        File.WriteAllText(GuestPrimary,"corrupt guest identity");
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.AreEqual(legacy,Decks.Serialize());
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        Assert.AreEqual("corrupt guest identity",File.ReadAllText(GuestPrimary));
        Assert.AreEqual(legacy,PlayerPrefs.GetString("monster-pouch.decks.v1"));
    }
    [UnityTest] public IEnumerator ExistingV3GuestWithCustomizedDecksKeepsThemWithoutOpeningTheStarterSelector()
    {
        var profile=new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);
        WriteGuestPayload(JsonUtility.ToJson(new GuestV3{ProfileId=profile.ProfileId,Revision=profile.Revision}));
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var store=new LocalDeckStore(DeckDirectory);
        var library=store.LoadOrImport(null,catalogue,"anuik",new[]{"stein","kayon"});
        library.Active=2;library.Assign(-1,"sepora",catalogue);library.Assign(0,"hymay",catalogue);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,store.Revision,catalogue));
        Assert.Greater(store.Revision,0);
        string expected=library.Serialize(),beforeDeck=File.ReadAllText(DeckPrimary),beforeGuest=File.ReadAllText(GuestPrimary);
        UnityEngine.Object.Destroy(catalogue);
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.AreEqual(expected,Decks.Serialize());
        Assert.AreEqual(profile.ProfileId,ReadGuest().ProfileId);Assert.IsFalse(ReadGuest().HasLegacyAccess);
        Assert.AreEqual(GuestCollectionMode.Legacy,ReadGuest().CollectionMode);
        Assert.IsNull(ReadGuest().Starter);
        Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));Assert.AreEqual(beforeGuest,File.ReadAllText(GuestPrimary));
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.AreEqual(expected,Decks.Serialize());
        Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));Assert.AreEqual(beforeGuest,File.ReadAllText(GuestPrimary));
    }
    [UnityTest] public IEnumerator NewV4GuestWithExistingDeckMustChooseAndCannotInferOwnershipOrSkipThroughBattle()
    {
        var profile=new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var store=new LocalDeckStore(DeckDirectory);
        var library=store.LoadOrImport(null,catalogue,"anuik",new[]{"stein","kayon"});
        library.Active=2;library.Assign(-1,"sepora",catalogue);library.Assign(0,"hymay",catalogue);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,store.Revision,catalogue));
        string expected=library.Serialize(),beforeDeck=File.ReadAllText(DeckPrimary),beforeGuest=File.ReadAllText(GuestPrimary);
        UnityEngine.Object.Destroy(catalogue);
        yield return LoadScene();
        Assert.IsTrue(SelectorOpen);Assert.AreEqual(expected,Decks.Serialize());
        Assert.AreEqual(GuestCollectionMode.StarterInventory,ReadGuest().CollectionMode);
        Assert.IsNull(ReadGuest().Starter);Assert.IsEmpty(ReadGuest().GetOwnedCharacterIds());
        ui.ShowBattleModes();yield return null;
        ui.CanvasRoot.GetComponentsInChildren<Button>().Single(button=>button.GetComponentsInChildren<Text>().Any(text=>text.text=="BOT")).onClick.Invoke();
        yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);Assert.IsTrue(SelectorOpen);
        Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));Assert.AreEqual(beforeGuest,File.ReadAllText(GuestPrimary));
        yield return LoadScene();
        Assert.IsTrue(SelectorOpen);Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));
        yield return ConfirmChoice("popow");
        Assert.IsFalse(SelectorOpen);Assert.IsTrue(ui.Match.IsTraining);
        Assert.AreEqual(profile.ProfileId,ReadGuest().ProfileId);Assert.AreEqual("popow",ReadGuest().Starter.Deck.Monster);
        Assert.AreEqual(expected,Decks.Serialize());Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));
        CollectionAssert.AreEqual(StarterRoster.Whelps,ReadGuest().GetAvailableWhelpIds(ui.Match.Config));
        Assert.IsFalse(ReadGuest().OwnsCharacter("hymay"),"A preserved conflicting deck is not an ownership grant.");
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.AreEqual(expected,Decks.Serialize());Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));
    }
    [UnityTest] public IEnumerator ConcurrentDeckEditBeforeConfirmationPreservesTheLatestDeckAndLeavesTheMenuUsable()
    {
        yield return LoadScene();
        Assert.IsTrue(SelectorOpen);
        ButtonNamed("Starter choice popow").onClick.Invoke();yield return null;
        var otherStore=new LocalDeckStore(DeckDirectory);
        var concurrent=otherStore.LoadOrImport(null,ui.Match.Config,"anuik",null);
        concurrent.Active=2;concurrent.Assign(-1,"sepora",ui.Match.Config);concurrent.Assign(0,"hymay",ui.Match.Config);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,otherStore.Save(concurrent,otherStore.Revision,ui.Match.Config));
        string expected=concurrent.Serialize(),before=File.ReadAllText(DeckPrimary);
        ButtonNamed("Confirm starter selection").onClick.Invoke();yield return null;yield return null;
        Assert.IsFalse(SelectorOpen);Assert.AreEqual(expected,Decks.Serialize());
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));Assert.AreEqual("popow",ReadGuest().Starter.Deck.Monster);
        Assert.IsTrue(ui.Match.IsTraining);ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="Deck storage notice"));
        ui.OpenCharacterPicker(0);yield return null;
        Assert.IsNotNull(ButtonNamed("loadout-hymay"),"The conflict must leave the existing deck menu usable.");
        ui.CloseModal();yield return null;
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.AreEqual(expected,Decks.Serialize());
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
    }
    [UnityTest] public IEnumerator SevenAvailableWhelpsRejectAConflictingSixUnitTeamAndALockedSeventhWithoutWriting()
    {
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var guestStore=new LocalGuestProfileStore(GuestDirectory);guestStore.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,guestStore.ChooseStarter(catalogue,"bugaloo"));
        string[] extra={"flo","gochan","trimol","atong"};
        for(int i=0;i<extra.Length;i++)Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,
            guestStore.UnlockCharacter(catalogue,"required-seven-"+i,extra[i],GuestUnlockSource.Tutorial));
        var store=new LocalDeckStore(DeckDirectory);
        var library=store.LoadOrImport(null,catalogue,"bugaloo",StarterRoster.Whelps);
        string[] six=StarterRoster.Whelps.Concat(extra.Take(3)).ToArray();
        library.Decks[0].Whelps=new string[7];Array.Copy(six,library.Decks[0].Whelps,six.Length);
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,store.Revision,catalogue));
        string before=File.ReadAllText(DeckPrimary);
        yield return LoadScene();yield return ExitFirstContact();
        Assert.AreEqual(7,ReadGuest().GetAvailableWhelpIds(ui.Match.Config).Count);
        Assert.IsFalse(ui.GetComponent<OnlineRoom>().LocalTeamValidator("bugaloo",six),"Host/Join must use the same minimum-roster validation without contacting a service.");
        Assert.AreEqual(6,ui.SelectedWhelps.Count);yield return AttemptBot();
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        library=store.LoadOrImport(null,catalogue,"bugaloo",StarterRoster.Whelps);
        library.Current.Whelps[6]="hymay";
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,store.Revision,catalogue));
        before=File.ReadAllText(DeckPrimary);
        yield return LoadScene();yield return ExitFirstContact();
        Assert.AreEqual(7,ui.SelectedWhelps.Count);Assert.IsFalse(ReadGuest().OwnsCharacter("hymay"));
        Assert.IsFalse(ui.GetComponent<OnlineRoom>().LocalTeamValidator("bugaloo",ui.SelectedWhelps));
        yield return AttemptBot();Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));UnityEngine.Object.Destroy(catalogue);
    }
    [UnityTest] public IEnumerator AConflictingLegalCurrentTeamStartsWithoutConfirmingOrRewritingTheOtherDecks()
    {
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var guestStore=new LocalGuestProfileStore(GuestDirectory);var guest=guestStore.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,guestStore.ChooseStarter(catalogue,"bugaloo"));
        var store=new LocalDeckStore(DeckDirectory);
        var library=store.LoadOrImport(null,catalogue,"bugaloo",StarterRoster.Whelps);
        library.Decks[1].Whelps=new string[7];library.Decks[1].Whelps[0]="hymay";
        Assert.AreEqual(LocalDeckStore.SaveResult.Saved,store.Save(library,store.Revision,catalogue));
        string before=File.ReadAllText(DeckPrimary);UnityEngine.Object.Destroy(catalogue);
        yield return LoadScene();yield return ExitFirstContact();yield return AttemptBot();
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);Assert.IsFalse(ui.Match.IsTraining);
        CollectionAssert.AreEquivalent(StarterRoster.Whelps,ui.Match.Player.SelectedWhelpIds);
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        var reloaded=new LocalDeckStore(DeckDirectory);reloaded.LoadOrImport(null,ui.Match.Config,"bugaloo",StarterRoster.Whelps);
        Assert.IsFalse(reloaded.HasConfirmedInventoryRoster(guest.ProfileId),"Entering a legal match must not silently confirm conflicting saved decks.");
    }
    [UnityTest] public IEnumerator UnreadableGuestIdentityCannotUseTheCatalogueOrStartAndIsNeverRecreated()
    {
        yield return LoadScene();yield return ConfirmChoice("bugaloo");yield return ExitFirstContact();
        string before=File.ReadAllText(DeckPrimary),backup=Path.Combine(GuestDirectory,"guest-profile.backup.json");
        File.WriteAllText(GuestPrimary,"unreadable primary identity");
        File.WriteAllText(backup,"unreadable backup identity");
        yield return LoadScene();Assert.IsFalse(SelectorOpen);yield return AttemptBot();
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);Assert.IsFalse(ui.Match.IsTraining);
        Assert.IsFalse(ui.GetComponent<OnlineRoom>().LocalTeamValidator("bugaloo",StarterRoster.Whelps));
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        ui.OpenCharacterPicker(0);yield return null;ButtonNamed("loadout-hymay").onClick.Invoke();yield return null;
        Assert.IsFalse(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        ui.OpenCharacterPicker(-1);yield return null;ButtonNamed("loadout-popow").onClick.Invoke();yield return null;
        Assert.IsFalse(ButtonNamed("character-preview-use").interactable);
        ButtonNamed("character-preview-use").onClick.Invoke();yield return null;
        Assert.AreEqual(before,File.ReadAllText(DeckPrimary));
        Assert.AreEqual("unreadable primary identity",File.ReadAllText(GuestPrimary));
        Assert.AreEqual("unreadable backup identity",File.ReadAllText(backup));
    }
    [UnityTest] public IEnumerator OpeningAnExistingV1AccountWithoutAStarterDoesNotStartOnboarding()
    {
        var profile=new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);
        profile.Version=1;
        string payload=JsonUtility.ToJson(profile),checksum;
        using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        string original=JsonUtility.ToJson(new Envelope{Version=1,Payload=payload,Sha256=checksum});
        File.WriteAllText(GuestPrimary,original);
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.IsFalse(ui.Match.IsTraining);
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
        Assert.AreEqual(GuestOnboardingStage.None,ReadGuest().OnboardingStage);
        Assert.AreEqual(profile.ProfileId,ReadGuest().ProfileId);
        Assert.AreEqual(original,File.ReadAllText(GuestPrimary));
        yield return LoadScene();
        Assert.IsFalse(SelectorOpen);Assert.IsFalse(ui.Match.IsTraining);
        Assert.AreEqual(original,File.ReadAllText(GuestPrimary));
    }
    IEnumerator ConfirmChoice(string id)
    {
        ButtonNamed("Starter choice "+id).onClick.Invoke();yield return null;
        ButtonNamed("Confirm starter selection").onClick.Invoke();yield return null;yield return null;
    }
    IEnumerator ExitFirstContact()
    {
        Assert.IsTrue(ui.Match.IsTraining);
        ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;yield return null;
    }
    IEnumerator AttemptBot()
    {
        ui.ShowBattleModes();yield return null;
        ui.CanvasRoot.GetComponentsInChildren<Button>().Single(button=>button.GetComponentsInChildren<Text>().Any(text=>text.text=="BOT")).onClick.Invoke();
        yield return null;yield return null;
    }
    void WriteGuestPayload(string payload)
    {
        string checksum;
        using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        File.WriteAllText(GuestPrimary,JsonUtility.ToJson(new Envelope{Version=1,Payload=payload,Sha256=checksum}));
    }
    void AssertApplied(string id)
    {
        Assert.IsFalse(SelectorOpen);
        Assert.AreEqual(id,ReadGuest().Starter.Deck.Monster);
        Assert.AreEqual(id,Decks.Current.Monster);Assert.AreEqual(0,Decks.Active);
        CollectionAssert.AreEqual(StarterRoster.Whelps,Decks.Current.Whelps.Where(whelp=>!string.IsNullOrEmpty(whelp)));
    }
    bool SelectorOpen=>ui.CanvasRoot.Find("Starter selection")!=null;
    IEnumerator Capture(string filename)
    {
        string capture=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/roadmap-v1.2",filename));
        Directory.CreateDirectory(Path.GetDirectoryName(capture));
        ScreenCapture.CaptureScreenshot(capture);yield return null;yield return null;
    }
    void AssertLockedPresentation(Button card,bool locked)
    {
        var shade=card.transform.Find("Locked character shade");
        var padlock=card.transform.Find("Closed character lock");
        if(!locked){Assert.IsNull(shade);Assert.IsNull(padlock);return;}
        Assert.IsNotNull(shade);Assert.IsNotNull(padlock);Assert.IsTrue(padlock.gameObject.activeInHierarchy);
        var color=shade.GetComponent<Image>().color;
        Assert.GreaterOrEqual(color.a,.5f);Assert.LessOrEqual(Mathf.Max(color.r,color.g,color.b),.05f);
        Assert.IsNotNull(padlock.Find("Closed shackle"));Assert.IsNotNull(padlock.Find("Lock body/Keyhole"));
    }
    Button ButtonNamed(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name==name);
}

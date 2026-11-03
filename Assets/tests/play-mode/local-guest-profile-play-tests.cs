using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class LocalGuestProfilePlayTests
{
    static readonly string[] OverrideNames={"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"};
    static readonly PropertyInfo[] Overrides=OverrideNames.Select(name=>typeof(LocalGameUI).GetProperty(name,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    static readonly FieldInfo ProfileField=typeof(LocalGameUI).GetField("guestProfile",BindingFlags.Instance|BindingFlags.NonPublic);
    static readonly FieldInfo ErrorField=typeof(LocalGameUI).GetField("guestProfileError",BindingFlags.Instance|BindingFlags.NonPublic);
    readonly string[] keys={"monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;
    string[] previousDirectories,saved;
    bool[] existed;
    LocalGameUI ui;
    string GuestDirectory=>Path.Combine(root,"GuestProfile");
    string Primary=>Path.Combine(GuestDirectory,"guest-profile.json");
    LocalGuestProfile Profile=>(LocalGuestProfile)ProfileField.GetValue(ui);

    [SetUp] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchGuestPlayTests",Guid.NewGuid().ToString("N"));
        previousDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,GuestDirectory);
        Overrides[1].SetValue(null,Path.Combine(root,"PlayerProgress"));
        Overrides[2].SetValue(null,Path.Combine(root,"PlayerDecks"));
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
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchGuestPlayTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(root);
        Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [UnityTest] public IEnumerator StartupCapturesLegacyBeforeCreatingSavesAndRetainsTheSameGuestOnReload()
    {
        yield return LoadScene();
        Assert.IsNotNull(Profile);Assert.IsFalse(Profile.HasLegacyAccess);Assert.IsNull(Profile.Starter);
        string identity=Profile.ProfileId,original=File.ReadAllText(Primary);
        Assert.IsTrue(File.Exists(Path.Combine(root,"PlayerProgress","progress.json")));
        Assert.IsTrue(File.Exists(Path.Combine(root,"PlayerDecks","decks.json")));
        yield return LoadScene();
        Assert.AreEqual(identity,Profile.ProfileId);Assert.IsFalse(Profile.HasLegacyAccess);
        Assert.AreEqual(original,File.ReadAllText(Primary));Assert.IsNull(ErrorField.GetValue(ui));
        string decks=File.ReadAllText(Path.Combine(root,"PlayerDecks","decks.json"));
        // A different empty guest directory with pre-existing game data simulates
        // upgrading an older installation that did not have a guest profile yet.
        Overrides[0].SetValue(null,Path.Combine(root,"MigratedGuest"));
        yield return LoadScene();
        Assert.IsTrue(Profile.HasLegacyAccess);Assert.IsNull(Profile.Starter);
        Assert.AreEqual(decks,File.ReadAllText(Path.Combine(root,"PlayerDecks","decks.json")));
        Assert.AreEqual(original,File.ReadAllText(Primary));
    }
    [UnityTest] public IEnumerator CorruptGuestDoesNotRegenerateIdentityOrPreventExistingLocalPlay()
    {
        new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);
        File.WriteAllText(Primary,"corrupt guest identity");
        yield return LoadScene();
        Assert.IsNull(Profile);Assert.AreEqual("load",ErrorField.GetValue(ui));
        Assert.AreEqual("corrupt guest identity",File.ReadAllText(Primary));
        Assert.IsTrue(ui.Match.StartMatch("popow",new[]{"atori"},20261020));
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);ui.Match.Menu();
        yield return LoadScene();
        Assert.IsNull(Profile);Assert.AreEqual("load",ErrorField.GetValue(ui));
        Assert.AreEqual("corrupt guest identity",File.ReadAllText(Primary));
        Assert.IsFalse(File.Exists(Path.Combine(GuestDirectory,"guest-profile.backup.json")));
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
    }
}

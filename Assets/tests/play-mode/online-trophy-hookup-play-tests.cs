using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class OnlineTrophyHookupPlayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Digest = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(n => typeof(LocalGameUI).GetProperty(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    readonly string[] keys = {
        "monster-pouch.progress.v1", "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps",
        CardTableTheme.PreferenceKey, CardTableTheme.LobbyPreferenceKey
    };
    string root;
    string[] previousDirectories, previousValues;
    bool[] hadKeys;
    LocalGameUI ui;
    Scene loadedScene;
    string GuestDirectory => Path.Combine(root, "Guest");
    string Primary => Path.Combine(GuestDirectory, "guest-profile.json");
    LocalGuestProfile Profile => (LocalGuestProfile)typeof(LocalGameUI).GetField("guestProfile", Private).GetValue(ui);
    OnlineRoom Room => (OnlineRoom)typeof(LocalGameUI).GetField("online", Private).GetValue(ui);

    [UnitySetUp] public IEnumerator Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "MonsterPouchOnlineTrophyPlayTests", Guid.NewGuid().ToString("N"));
        previousDirectories = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null, GuestDirectory);
        Overrides[1].SetValue(null, Path.Combine(root, "Progress"));
        Overrides[2].SetValue(null, Path.Combine(root, "Decks"));
        previousValues = keys.Select(k => PlayerPrefs.GetString(k)).ToArray();
        hadKeys = keys.Select(k => PlayerPrefs.HasKey(k)).ToArray();
        foreach (string key in keys) PlayerPrefs.DeleteKey(key);
        yield return LoadScene();
        Assert.IsFalse(Profile.HasLegacyAccess);
        Assert.IsNull(Profile.Starter, "The fixture begins with a fresh isolated account.");
        Assert.AreEqual(0L, Profile.Trophies);
    }

    IEnumerator LoadScene()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        Assert.IsNotNull(ui); loadedScene = ui.gameObject.scene;
        ui.Match.enabled = false; Room.enabled = false; ui.CloseModal();
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (ui != null) { ui.CloseModal(); ui.Match.Menu(); }
        // Destroy all scene subscribers while the temporary save overrides still apply.
        if (loadedScene.IsValid() && loadedScene.isLoaded)
        {
            var empty = SceneManager.CreateScene("Online trophy cleanup " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, previousDirectories[i]);
        for (int i = 0; i < keys.Length; i++)
        { if (hadKeys[i]) PlayerPrefs.SetString(keys[i], previousValues[i]); else PlayerPrefs.DeleteKey(keys[i]); }
        PlayerPrefs.Save();
        string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchOnlineTrophyPlayTests")) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(root);
        Assert.IsTrue(target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }

    [UnityTest] public IEnumerator BotAndUnconfirmedOnlineResultsKeepZeroThenVerifiedWinPersistsThirtyOnce()
    {
        Assert.IsTrue(ui.Match.StartMatch("bugaloo", new[] { "atori" }, 251025));
        FinalScore(3, 0); UpdateProgress();
        Assert.AreEqual(0L, Profile.Trophies, "A normal bot victory must not earn online trophies.");
        ui.Match.Menu(); StartOnline(); FinalScore(3, 1);
        UpdateProgress();
        Assert.AreEqual(0L, Profile.Trophies, "MatchResult alone must not count as peer confirmation.");
        Field(Room, "peerDigest", null); Compare();
        Assert.AreEqual(0L, Profile.Trophies);
        Field(Room, "peerDigest", Digest); Compare();
        Assert.AreEqual(30L, Profile.Trophies);
        string saved = File.ReadAllText(Primary);
        Compare(); UpdateProgress(); ui.RetryTrophyResults();
        Assert.AreEqual(30L, Profile.Trophies);
        Assert.AreEqual(saved, File.ReadAllText(Primary), "Duplicate callbacks must not rewrite or double-award the receipt.");
        Assert.IsFalse(ui.TrophySavePending);
        string identity = Profile.ProfileId;
        yield return LoadScene();
        Assert.AreEqual(identity, Profile.ProfileId); Assert.AreEqual(30L, Profile.Trophies);
    }

    [UnityTest] public IEnumerator FailedTrophyWriteSurvivesMenuAndRetriesExactlyOnce()
    {
        StartOnline(); FinalScore(3, 2);
        string before = File.ReadAllText(Primary);
        using (var blocker = new FileStream(Path.Combine(GuestDirectory, "guest-profile.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Compare();
            Assert.IsTrue(ui.TrophySavePending);
            Assert.AreEqual(0L, Profile.Trophies);
            Assert.AreEqual(before, File.ReadAllText(Primary));
            ui.Match.Menu();
            Assert.IsTrue(ui.TrophySavePending, "Leaving the match must retain the failed confirmed receipt.");
        }
        ui.RetryTrophyResults();
        Assert.IsFalse(ui.TrophySavePending); Assert.AreEqual(30L, Profile.Trophies);
        string saved = File.ReadAllText(Primary);
        ui.RetryTrophyResults(); UpdateProgress();
        Assert.AreEqual(saved, File.ReadAllText(Primary));
        Assert.AreEqual(30L, new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false).Trophies);
        yield return null;
    }

    [UnityTest] public IEnumerator TrainingAndDigestMismatchCannotAwardTrophies()
    {
        StartOnline(); FinalScore(3, 0); Property(ui.Match, "IsTraining", true);
        Compare(); Assert.AreEqual(0L, Profile.Trophies);
        Property(ui.Match, "IsTraining", false);
        Field(Room, "peerDigest", new string('B', 64)); Compare();
        Assert.AreEqual(0L, Profile.Trophies); Assert.IsFalse(ui.TrophySavePending);
        Assert.AreEqual(0L, new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false).Trophies);
        yield return null;
    }

    void StartOnline()
    {
        // This transport-free fixture starts the controller directly. It does not
        // call OnlineRoom.Connect or its account/starter-dependent ability provider.
        Assert.IsTrue(ui.Match.StartOnlineMatch(BoardSide.Blue, "bugaloo", new[] { "atori" }, "popow", new[] { "atori" }, 251025));
        Field(Room, "sharedMatchId", Guid.NewGuid().ToString("N")); Field(Room, "peer", 42UL);
        Field(Room, "localDigest", Digest); Field(Room, "peerDigest", Digest);
    }
    void FinalScore(int local, int rival)
    {
        Property(ui.Match, "Phase", MatchPhase.MatchResult);
        Property(ui.Match, "PlayerWins", local); Property(ui.Match, "BotWins", rival);
    }
    void Compare() => typeof(OnlineRoom).GetMethod("CompareResults", Private).Invoke(Room, null);
    void UpdateProgress() => typeof(LocalGameUI).GetMethod("UpdateProgression", Private).Invoke(ui, null);
    static void Field(object owner, string name, object value)
    { var field = owner.GetType().GetField(name, Private); Assert.IsNotNull(field, name); field.SetValue(owner, value); }
    static void Property(object owner, string name, object value)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, name); property.GetSetMethod(true).Invoke(owner, new[] { value });
    }
}

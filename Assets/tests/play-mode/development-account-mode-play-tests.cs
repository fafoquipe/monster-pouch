using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class DevelopmentAccountModePlayTests
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "DeckDirectoryOverride", "ProgressionDirectoryOverride", "DevelopmentPersistentDirectoryOverride" }
        .Select(n => typeof(LocalGameUI).GetProperty(n, BindingFlags.NonPublic | BindingFlags.Static)).ToArray();
    static readonly string[] AccountKeys = { "monster-pouch.monster", "monster-pouch.whelps", CardTableTheme.PreferenceKey, CardTableTheme.LobbyPreferenceKey };
    string root;
    string[] oldOverrides, oldAccountValues;
    bool[] oldAccountPresence;
    bool spanish, hadLanguage;
    string oldLanguage, oldScope;
    readonly HashSet<string> scopedKeys = new HashSet<string>();
    LocalGameUI ui;
    DevelopmentAccountStore Accounts => new DevelopmentAccountStore(root);

    [UnitySetUp] public IEnumerator Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "MPDevUI", Guid.NewGuid().ToString("N").Substring(0, 12));
        oldOverrides = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        foreach (var property in Overrides) property.SetValue(null, null);
        Overrides[3].SetValue(null, root);
        oldScope = (string)typeof(LocalAccountPreferences).GetField("DevelopmentScope", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        oldAccountValues = AccountKeys.Select(k => PlayerPrefs.GetString(k)).ToArray();
        oldAccountPresence = AccountKeys.Select(PlayerPrefs.HasKey).ToArray();
        spanish = GameText.Spanish; hadLanguage = PlayerPrefs.HasKey("monster-pouch.language"); oldLanguage = PlayerPrefs.GetString("monster-pouch.language");
        yield return Load();
    }

    IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return WaitForUi();
    }
    IEnumerator WaitForUi()
    {
        yield return null; yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.IsNotNull(ui); ui.Match.enabled = false;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (LoadingArtworkScreen.IsBlocking && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        foreach (string key in AccountKeys) scopedKeys.Add(LocalAccountPreferences.Key(key));
    }
    Button Button(string name) => ui.CanvasRoot.GetComponentsInChildren<Button>().Last(b => b.name == name && b.gameObject.activeInHierarchy);
    void Settings() => typeof(LocalGameUI).GetMethod("ShowLobbySettings", Private).Invoke(ui, null);
    bool Has(string name) => ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t => t.name == name && t.gameObject.activeInHierarchy);
    LocalGuestProfile ReadProfile() => Accounts.Open(ui.Match.Config);

    [UnityTest] public IEnumerator DevelopmentDefaultShowsAllBoardsAndOneHomeDailyCasesRouteInBothLanguages()
    {
        var profile = ReadProfile();
        Assert.AreEqual(44, profile.GetOwnedCharacterIds().Count);
        foreach (TableStyle style in Enum.GetValues(typeof(TableStyle))) Assert.IsTrue(ui.CanSelectTrophyBoard(style), style.ToString());
        foreach (bool es in new[] { true, false })
        {
            GameText.SetLanguage(es); ui.RenderPage(); yield return null;
            Assert.AreEqual("DAILY CASES", Button("Lobby cases").GetComponentInChildren<Text>().text);
            Button("Lobby cases").onClick.Invoke(); yield return null;
            Assert.IsTrue(Has("Pouch room panel")); Button("Close pouch room").onClick.Invoke(); yield return null;
            Button("Lobby settings").onClick.Invoke(); yield return null;
            Assert.IsFalse(Has("Pouch room"), "Quick settings must not duplicate Cases inventory.");
            Button("Open settings").onClick.Invoke(); yield return null;
            Assert.AreEqual(es ? "COMENZAR DESDE CERO" : "START FROM ZERO", Button("Development start fresh").GetComponentInChildren<Text>().text);
            Assert.AreEqual(es ? "DESBLOQUEAR TODO" : "UNLOCK ALL", Button("Development unlock all").GetComponentInChildren<Text>().text);
            yield return Capture(es ? "development-settings-es" : "development-settings-en");
            ui.CloseModal();
        }
        for (int i = 0; i < AccountKeys.Length; i++)
        { Assert.AreEqual(oldAccountPresence[i], PlayerPrefs.HasKey(AccountKeys[i])); Assert.AreEqual(oldAccountValues[i], PlayerPrefs.GetString(AccountKeys[i])); }
    }

    [UnityTest] public IEnumerator ConfirmationCancelAndDirtyGateDoNotChangeTheAccount()
    {
        string identity = ReadProfile().ProfileId;
        Settings(); Button("Development start fresh").onClick.Invoke(); yield return null;
        Assert.IsTrue(Button("Confirm development account change").interactable);
        yield return Capture("development-reset-confirmation");
        Button("Cancel development account change").onClick.Invoke(); yield return null;
        Assert.AreEqual(identity, ReadProfile().ProfileId);
        typeof(LocalGameUI).GetField("pendingDeckJson", Private).SetValue(ui, "pending-fixture");
        Button("Development start fresh").onClick.Invoke(); yield return null;
        Assert.IsFalse(Button("Confirm development account change").interactable);
        yield return Capture("development-pending-save-gate");
        Assert.AreEqual(identity, ReadProfile().ProfileId);
        typeof(LocalGameUI).GetField("pendingDeckJson", Private).SetValue(ui, null);
    }

    [UnityTest] public IEnumerator ConfirmedFreshResetRestartsWithStarterAndStaysFreshUntilExplicitUnlockAll()
    {
        string before = ReadProfile().ProfileId;
        Settings(); Button("Development start fresh").onClick.Invoke();
        Button("Confirm development account change").onClick.Invoke(); yield return WaitForUi();
        var fresh = ReadProfile();
        Assert.AreNotEqual(before, fresh.ProfileId); Assert.AreEqual(DevelopmentAccountMode.Fresh, fresh.DevelopmentMode);
        Assert.IsTrue(Has("Starter selection")); Assert.IsEmpty(fresh.GetOwnedCharacterIds());
        Assert.AreEqual(0, fresh.Trophies); Assert.IsFalse(ui.CanSelectTrophyBoard(TableStyle.CampoReal));
        yield return Load();
        Assert.AreEqual(fresh.ProfileId, ReadProfile().ProfileId); Assert.IsTrue(Has("Starter selection"));
        Button("Starter development unlock all").onClick.Invoke(); yield return null;
        Assert.IsTrue(Button("Confirm development account change").interactable);
        Button("Confirm development account change").onClick.Invoke(); yield return WaitForUi();
        Assert.AreEqual(fresh.ProfileId, ReadProfile().ProfileId); Assert.AreEqual(44, ReadProfile().GetOwnedCharacterIds().Count);
        Assert.IsFalse(Has("Starter selection")); Assert.IsTrue(ui.CanSelectTrophyBoard(TableStyle.CampoReal));
    }

    IEnumerator Capture(string name)
    {
        string folder = Environment.GetEnvironmentVariable("MONSTER_POUCH_DEV_ACCOUNT_CAPTURES");
        if (string.IsNullOrWhiteSpace(folder)) yield break;
        Directory.CreateDirectory(folder); string path = Path.Combine(folder, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        for (int i = 0; i < 30 && !File.Exists(path); i++) yield return null;
        Assert.IsTrue(File.Exists(path));
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (ui != null) { ui.CloseModal(); ui.gameObject.SetActive(false); Object.Destroy(ui.gameObject); }
        yield return null;
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, oldOverrides[i]);
        typeof(LocalAccountPreferences).GetField("DevelopmentScope", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, oldScope);
        foreach (string key in scopedKeys) if (!AccountKeys.Contains(key)) PlayerPrefs.DeleteKey(key);
        GameText.SetLanguage(spanish);
        if (hadLanguage) PlayerPrefs.SetString("monster-pouch.language", oldLanguage); else PlayerPrefs.DeleteKey("monster-pouch.language");
        PlayerPrefs.Save();
        string allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MPDevUI")) + Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Editor-only visual fixture: actual combat, actors, board and HUD. No account or PlayerPrefs writes.
public static class TaurukMarkPreview
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static LocalGameUI owner;
    static MatchConfig originalConfig, fixtureConfig;
    static AbilityAccessSnapshot originalAccess;
    static Coroutine recording;
    static Action progressionHandler;
    static bool originalUIEnabled, originalMatchEnabled;
    static float originalTimeScale, originalCaptureDelta;
    static int originalGameViewSize;
    static EditorWindow gameView;
    static string status = "idle";
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/screenshots"));

    public static string Status() => status;
    public static string Combat() => Begin(false);
    public static string Preparation()
    {
        if (owner != null && status == "combat-ready")
        { status = "recording-preparation"; recording = owner.StartCoroutine(AdvancePreparation()); return status; }
        return Begin(true);
    }
    static string Begin(bool preparation)
    {
        if (owner != null) Restore();
        var ui = Object.FindFirstObjectByType<LocalGameUI>();
        if (!Application.isPlaying || ui == null) throw new InvalidOperationException("Enter Play Mode first.");
        if (ui.Match.IsOnline || ui.Match.Phase != MatchPhase.Menu)
            throw new InvalidOperationException("Return to the offline lobby before this isolated preview.");
        owner = ui; originalConfig = ui.Match.Config;
        originalUIEnabled = ui.enabled; originalMatchEnabled = ui.Match.enabled;
        originalTimeScale = Time.timeScale; originalCaptureDelta = Time.captureDeltaTime;
        originalAccess = (AbilityAccessSnapshot)typeof(MatchController).GetField("localAbilityAccess", Hidden).GetValue(ui.Match);
        gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        originalGameViewSize = (int)gameView.GetType().GetProperty("selectedSizeIndex", Any).GetValue(gameView);
        progressionHandler = (Action)Delegate.CreateDelegate(typeof(Action), ui,
            typeof(LocalGameUI).GetMethod("UpdateProgression", Hidden));
        // Disconnect disk-backed rewards, including pending receipt retries, while this fixture runs.
        ui.Match.Changed -= progressionHandler;
        ui.enabled = false; ui.Match.enabled = false;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        try
        {
            fixtureConfig = Object.Instantiate(originalConfig); fixtureConfig.name = "Tauruk mark visual fixture (memory only)";
            fixtureConfig.hideFlags = HideFlags.DontSave;
            fixtureConfig.Characters = Array.Empty<CharacterDefinition>();
            fixtureConfig.Units = originalConfig.Units.Where(u => u.Id == "tauris" || u.Id == "atori" || u.Id == "stein")
                .Select(u => JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(u))).ToArray();
            fixtureConfig.RoundIncome = new[] { 0, 0, 0, 0, 0 };
            foreach (var unit in fixtureConfig.Units)
            {
                // Keep authored art, size, HP and mark parameters; hold positions for the photograph.
                unit.Damage = 0; unit.AttackInterval = unit.MoveInterval = 1000;
                unit.EnergyPerAttack = unit.EnergyOnDamage = unit.EnergyPerSecond = 0;
                unit.EnergyMax = 10000; unit.RevivesPerCombat = 0;
            }
            typeof(MatchController).GetProperty("Config").SetValue(ui.Match, fixtureConfig);
            typeof(MatchController).GetField("localAbilityAccess", Hidden).SetValue(ui.Match, AbilityAccessSnapshot.Unrestricted);
            ui.CloseModal();
            MonsterPouchLayoutPreview.Resize("393x852");
            Time.timeScale = 1; Time.captureDeltaTime = 1f / 60f;
            if (!ui.Match.StartMatch("tauris", new[] { "atori", "stein" }, 102525))
                throw new InvalidOperationException(ui.Match.Notice);
            typeof(MatchController).GetMethod("ClearActors", Hidden).Invoke(ui.Match, null);
            SetupRoster(ui.Match.Player, "atori", new Vector2Int(2, 5), new Vector2Int(4, 6));
            SetupRoster(ui.Match.Bot, "stein", new Vector2Int(2, 4), new Vector2Int(4, 3));
            typeof(MatchController).GetMethod("SyncActors", Hidden).Invoke(ui.Match, null);
            ui.Match.Ready(); Require(ui.Match.Phase == MatchPhase.Combat, "Combat did not start.");
            Require(ui.Match.Player.Monster.MarkedPositionRound == 1 && ui.Match.Bot.Monster.MarkedPositionRound == 1,
                "Both real opening effects must mark the enemy in their column.");
            Require(ui.Match.Player.Owned["atori"].MarkedPositionRound == 0 && ui.Match.Bot.Owned["stein"].MarkedPositionRound == 0,
                "The off-column examples must remain unmarked.");
            status = "recording-combat";
            recording = ui.StartCoroutine(AdvanceCombat(preparation));
            return status;
        }
        catch { Restore(); throw; }
    }
    static void SetupRoster(PouchState pouch, string outside, Vector2Int monsterCell, Vector2Int outsideCell)
    {
        var roster = (Dictionary<string, OwnedUnit>)typeof(PouchState).GetField("owned", Hidden).GetValue(pouch);
        foreach (var key in roster.Keys.Where(id => id != pouch.Monster.Definition.Id).ToArray()) roster.Remove(key);
        Set(pouch.Monster, "Deployment", monsterCell); Set(pouch.Monster, "UpgradePurchasedRound", 0);
        pouch.Monster.Tricks[0] = true;
        var offColumn = new OwnedUnit(fixtureConfig.Get(outside));
        Set(offColumn, "Copies", 1); Set(offColumn, "Location", UnitLocation.Field); Set(offColumn, "Deployment", outsideCell);
        roster.Add(outside, offColumn);
    }
    static void Set(OwnedUnit unit, string property, object value) => typeof(OwnedUnit).GetProperty(property).SetValue(unit, value);
    static IEnumerator AdvanceCombat(bool preparation)
    {
        yield return null; yield return null;
        typeof(LocalGameUI).GetMethod("UpdateLayout", Hidden).Invoke(owner, null);
        owner.RenderPage();
        while (owner.Match.Simulation.Elapsed < 2)
        {
            owner.Match.Advance(Time.deltaTime);
            typeof(LocalGameUI).GetMethod("LateUpdate", Hidden).Invoke(owner, null);
            yield return null;
        }
        Time.timeScale = 0;
        yield return Capture("tauruk-mark-combat-393x852");
        status = "combat-ready"; recording = null;
        if (preparation) { status = "recording-preparation"; yield return AdvancePreparation(); }
    }
    static IEnumerator AdvancePreparation()
    {
        Time.timeScale = 1;
        foreach (var actor in owner.Match.Actors.Values.Where(a => a.Side != owner.Match.LocalSide)) actor.ApplyDamage(99999);
        owner.Match.Advance(.1f);
        Require(owner.Match.Phase == MatchPhase.RoundResult, "Only one round may finish; no BO5 rewards.");
        owner.Match.NextRound(); owner.RenderPage();
        Require(owner.Match.Phase == MatchPhase.Preparation && owner.Match.Round == 2, "Expected the next preparation.");
        var marked = owner.Match.Player.Monster;
        Require(marked.MarkedPositionRound == 1 && marked.IsPositionLocked(2), "Marked unit must remain locked.");
        Require(!owner.Match.Player.TrySetLocation(marked, UnitLocation.Field, new Vector2Int(3, 5), out _),
            "Repositioning a marked unit must fail.");
        yield return null; yield return null;
        Time.timeScale = 0;
        yield return Capture("tauruk-mark-preparation-393x852");
        status = "preparation-ready"; recording = null;
    }
    static IEnumerator Capture(string name)
    {
        typeof(LocalGameUI).GetMethod("LateUpdate", Hidden).Invoke(owner, null);
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        Require(Screen.width == 393 && Screen.height == 852, "Game view must render at 393×852.");
        int expected = owner.Match.Phase == MatchPhase.Combat ? 2 : 1;
        var visible = owner.CanvasRoot.GetComponentsInChildren<UnitPositionMarkUI>().Where(m => m.Visible).ToArray();
        Require(visible.Length == expected, "Expected " + expected + " visible real position marks, got " + visible.Length + ".");
        if (owner.Match.Phase == MatchPhase.Preparation)
            Require(owner.Match.Actors.Values.All(a => a.Side == owner.Match.LocalSide), "Preparation cannot reveal rival actors.");
        Directory.CreateDirectory(Folder);
        var frame = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(Path.Combine(Folder, name + ".png"), frame.EncodeToPNG()); }
        finally { Object.Destroy(frame); }
        File.WriteAllText(Path.Combine(Folder, name + ".txt"), MonsterPouchLayoutPreview.Measure() +
            "\nVisible marks=" + visible.Length + "; account storage disconnected; authored mark energy/range unchanged.");
    }
    static void Require(bool condition, string message)
    {
        if (condition) return;
        status = "failed: " + message;
        throw new InvalidOperationException(message + " Call TaurukMarkPreview.Restore().");
    }
    public static string Restore()
    {
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        var ui = owner; owner = null;
        if (ui != null)
        {
            if (recording != null) ui.StopCoroutine(recording);
            recording = null;
            ui.Match.Menu();
            typeof(MatchController).GetProperty("Config").SetValue(ui.Match, originalConfig);
            typeof(MatchController).GetField("localAbilityAccess", Hidden).SetValue(ui.Match, originalAccess);
            ui.RenderPage();
            ui.Match.Changed += progressionHandler;
            ui.enabled = originalUIEnabled; ui.Match.enabled = originalMatchEnabled;
            Time.timeScale = originalTimeScale; Time.captureDeltaTime = originalCaptureDelta;
        }
        if (fixtureConfig != null) Object.DestroyImmediate(fixtureConfig);
        fixtureConfig = null; originalConfig = null; progressionHandler = null;
        if (gameView != null) gameView.GetType().GetProperty("selectedSizeIndex", Any).SetValue(gameView, originalGameViewSize);
        gameView = null; status = "restored"; return status;
    }
    static void PlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Restore(); }
}

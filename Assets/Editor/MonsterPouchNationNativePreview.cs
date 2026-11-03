using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Editor-only, explicit opt-in. This uses the playable scene and its native renderers.
// No scene/config asset is saved. Every account store points to a disposable QA directory.
[InitializeOnLoad]
public static class MonsterPouchNationNativePreview
{
    const string StateKey = "MonsterPouch.NationNativePreview.Active";
    const string RestoreKey = "MonsterPouch.NationNativePreview.Restoring";
    const string ScenePath = "Assets/Scenes/main-scene.unity";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly string[] StoreProperties = { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" };
    static readonly string[] StoreFolders = { "Guest", "Progress", "Decks" };
    static readonly string[] StringKeys = {
        "monster-pouch.language", "monster-pouch.player-name", CardTableTheme.PreferenceKey, CardTableTheme.LobbyPreferenceKey,
        "monster-pouch.decks.v1", "monster-pouch.progress.v1", "monster-pouch.monster", "monster-pouch.whelps"
    };
    static readonly string[] FloatKeys = { "monster-pouch.volume", "monster-pouch.music-volume" };
    static string BackupPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterPouchNationPreview-backup.json"));
    static string TempParent => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchNativePreview"));
    static bool Active => SessionState.GetBool(StateKey, false);
    static bool Restoring => SessionState.GetBool(RestoreKey, false);
    static bool ready;

    [Serializable] sealed class Preference
    {
        public string Key, Text;
        public bool Existed, IsFloat;
        public float Number;
    }
    [Serializable] sealed class Snapshot
    {
        public int Version = 1;
        public string QaRoot, ProfileId, StartScene;
        public Preference[] Preferences;
        public string[] Overrides;
        public int GameViewIndex, EnterPlayOptions, TargetFrameRate, SleepTimeout;
        public bool EnterPlayOptionsEnabled, Spanish;
        public float TimeScale;
    }

    static MonsterPouchNationNativePreview()
    {
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorApplication.update += InitializeReadyScene;
        EditorApplication.quitting += BeforeQuit;
        // Domain reload continuation only: never enters Play Mode or starts a preview.
        if (Active && !Restoring) InstallOverrides(ReadSnapshot());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeSceneLoad()
    {
        if (Active && !Restoring) InstallOverrides(ReadSnapshot());
    }

    /// <summary>Call from Edit Mode; argument example: 393x852. No automatic activation.</summary>
    public static string Begin(string dimensions)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Finish the current play/compile session before Begin.");
        if (Active || File.Exists(BackupPath))
            throw new InvalidOperationException("A preview backup exists. Call Restore before beginning another preview.");
        if (StoreProperties.Any(name => StoreProperty(name).GetValue(null) != null))
            throw new InvalidOperationException("Another isolated session owns the profile overrides.");
        ValidateDimensions(dimensions);
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (scene == null) throw new FileNotFoundException("The playable scene is missing.", ScenePath);
        var snapshot = new Snapshot {
            QaRoot = Path.Combine(TempParent, Guid.NewGuid().ToString("N")),
            Preferences = StringKeys.Select(key => new Preference { Key = key, Existed = PlayerPrefs.HasKey(key), Text = PlayerPrefs.GetString(key) })
                .Concat(FloatKeys.Select(key => new Preference { Key = key, Existed = PlayerPrefs.HasKey(key), IsFloat = true, Number = PlayerPrefs.GetFloat(key) })).ToArray(),
            Overrides = StoreProperties.Select(name => (string)StoreProperty(name).GetValue(null)).ToArray(),
            StartScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
            GameViewIndex = GameViewIndex(), EnterPlayOptions = (int)EditorSettings.enterPlayModeOptions,
            EnterPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled,
            Spanish = GameText.Spanish, TimeScale = Time.timeScale,
            TargetFrameRate = Application.targetFrameRate, SleepTimeout = Screen.sleepTimeout
        };
        WriteSnapshot(snapshot); // Durable before the first preference changes, including editor restart recovery.
        SessionState.SetBool(StateKey, true); SessionState.SetBool(RestoreKey, false);
        try
        {
            Directory.CreateDirectory(snapshot.QaRoot);
            InstallOverrides(snapshot);
            foreach (string key in StringKeys) PlayerPrefs.DeleteKey(key); // Never import real legacy decks or XP.
            PlayerPrefs.SetString("monster-pouch.player-name", "REVISIÓN QA");
            PlayerPrefs.SetString("monster-pouch.monster", "bugaloo");
            PlayerPrefs.SetFloat("monster-pouch.volume", 0); PlayerPrefs.SetFloat("monster-pouch.music-volume", 0);
            GameText.SetLanguage(true);
            CardTableTheme.Select(TableStyle.Naturalis); CardTableTheme.SelectLobby(TableStyle.Naturalis);
            var store = new LocalGuestProfileStore(Path.Combine(snapshot.QaRoot, StoreFolders[0]));
            snapshot.ProfileId = store.OpenOrCreate(true).ProfileId;
            WriteSnapshot(snapshot);
            // Synthetic receipts belong ONLY to the newly-created QA identity. Native access gates stay active.
            // This is fixture seeding, not peer verification and never a production result/currency grant.
            for (int index = 0; index < 20; index++)
            {
                var result = store.RecordTrophyResult(new GuestTrophyResultRequest {
                    MatchId = Guid.NewGuid().ToString("N"), OpponentId = "native-preview-fixture", SourceModeId = "online",
                    VerifiedDigest = new string('A', 64), PlayerWins = 3, OpponentWins = 0
                });
                if (result.Status != GuestTrophyResultStatus.Applied) throw new InvalidOperationException("QA access seed failed: " + result.Status);
            }
            Time.timeScale = 1;
            // Force a fresh scene/domain even when the user's fast-enter options skip them; restore later.
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = scene;
            MonsterPouchLayoutPreview.Resize(dimensions);
            ready = false; EditorApplication.isPlaying = true;
            return "Native QA preview requested; use Status until ready. Temporary data: " + snapshot.QaRoot;
        }
        catch
        {
            RestoreImmediately(snapshot, true);
            throw;
        }
    }

    public static string Status()
    {
        if (!Active) return File.Exists(BackupPath) ? "Inactive; recovery backup exists. Call Restore." : "Inactive; preferences restored.";
        if (Restoring) return "Restoring; waiting for Edit Mode.";
        if (!ready || !Application.isPlaying) return "Preparing isolated native scene.";
        var ui = RequireUI();
        return "Ready; QA only; screen=" + Screen.width + "x" + Screen.height + "; map=" + CardTableTheme.MapId(ui.DisplayedArenaStyle) +
            "; lobby=" + CardTableTheme.MapId(CardTableTheme.Lobby) + "; phase=" + ui.Match.Phase +
            "; transitionRunning=" + ui.MapTransitionActive + "; transitionPaintHeld=" + TransitionPaintHeld(ui) + "; fixtureTrophies=" + ui.AccountTrophies +
            "; loading=" + LoadingArtworkScreen.IsBlocking + "; root=" + ReadSnapshot().QaRoot;
    }

    public static string Resize(string dimensions)
    {
        RequireUI(); ValidateDimensions(dimensions);
        return MonsterPouchLayoutPreview.Resize(dimensions);
    }
    public static string Lobby(string nation) => ShowLobby(ParseNation(nation), "bugaloo");
    public static string KazuruLobby(string nation) => ShowLobby(ParseNation(nation), "kazuru");
    public static string Preparation(string nation) => ShowBattle(ParseNation(nation), "bugaloo", false);
    public static string Battle(string nation) => ShowBattle(ParseNation(nation), "bugaloo", true);
    public static string Kazuru(string nation) => ShowBattle(ParseNation(nation), "kazuru", true);

    public static string Step(string seconds)
    {
        var ui = RequireUI(); var match = ui.Match;
        RequireInitialCombat(match);
        if (!float.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out float step) ||
            float.IsNaN(step) || float.IsInfinity(step) || step <= 0 || step > .1f || match.Simulation.Elapsed + step > 5.00001f)
            throw new ArgumentException("Use 0 < seconds <= 0.1, with at most 5 seconds in this initial combat.");
        if (match.Actors.Values.Any(actor => actor.GetComponent<UnitPresentation>()?.enabled == false))
            throw new InvalidOperationException("Restart Kazuru/Battle before stepping after a held HopSample.");
        match.enabled = false;
        match.Advance(step); // The only clock advance; never advances RoundResult or starts another round.
        return "Native simulation=" + match.Simulation.Elapsed.ToString("0.00", CultureInfo.InvariantCulture) +
            "; phase=" + match.Phase + "; presentation continues its normal frame interpolation.";
    }

    public static string HopSample(string sample)
    {
        var ui = RequireUI(); var match = ui.Match;
        RequireInitialCombat(match);
        if (match.Simulation.Elapsed > .00001f)
            throw new InvalidOperationException("Restart Kazuru first; HopSample reserves a presentation-only route before simulation advances.");
        float fraction;
        switch (sample)
        {
            case "idle": fraction = 0; break;
            case "lift": fraction = .61f; break;
            case "land": fraction = .895f; break;
            case "arrived": fraction = 1; break;
            default: throw new ArgumentException("Use idle, lift, land or arrived.");
        }
        var owned = match.Player?.Monster;
        if (owned?.Definition.Id != "kazuru") throw new InvalidOperationException("Call Kazuru(nation) before HopSample.");
        match.Actors.TryGetValue(owned, out var actor);
        var view = actor != null ? actor.GetComponent<UnitPresentation>() : null;
        if (view == null || !actor.IsAlive || actor.CurrentCell == null)
            throw new InvalidOperationException("Call Kazuru(nation) before HopSample.");
        var origin = actor.CurrentCell;
        var destination = actor.ReservedCell;
        if (destination == null)
        {
            foreach (var offset in new[] { new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(1, 1), new Vector2Int(-1, 1) })
            {
                var candidate = match.Board.GetCell(origin.X + offset.x, origin.Y + offset.y);
                if (candidate != null && match.Board.TryReserveCell(actor, candidate)) { destination = candidate; break; }
            }
        }
        if (destination == null || Math.Abs(destination.X - origin.X) != 1 || Math.Abs(destination.Y - origin.Y) != 1)
            throw new InvalidOperationException("No legal adjacent diagonal is available for the native presentation sample.");
        // Disable only this view's frame clock, then sample its real authored Move/AdvancePresentation path.
        // Reservation remains at its real origin: no teleport, combat stat mutation or simulation arrival is invented.
        view.enabled = false; view.SnapToCell();
        view.Move(match.Mapper.GetWorldPosition(origin), match.Mapper.GetWorldPosition(destination), actor.BaseStats.MoveInterval);
        typeof(UnitPresentation).GetMethod("AdvancePresentation", Any).Invoke(view, new object[] { actor.BaseStats.MoveInterval * fraction });
        Canvas.ForceUpdateCanvases();
        return "Native Kazuru hop sample=" + sample + "; fraction=" + fraction.ToString("0.000", CultureInfo.InvariantCulture) +
            "; origin=" + origin.Coordinates + "; reserved=" + destination.Coordinates + "; simulation=0; presentation held.";
    }
    static void RequireInitialCombat(MatchController match)
    {
        if (match.Phase != MatchPhase.Combat || match.IsOnline || match.IsTraining || match.Round != 1 ||
            match.PlayerWins != 0 || match.BotWins != 0 || match.WinsToWin != 3 || match.Simulation == null || match.Simulation.Finished)
            throw new InvalidOperationException("Only an unfinished first-round QA combat at 0:0 can be sampled; never a BO5 result.");
    }

    public static string Transition(string nation)
    {
        var style = ParseNation(nation);
        ShowLobby(style, "bugaloo");
        var ui = RequireUI();
        Invoke(ui, "TransitionMap", style, false);
        var transition = (MapIrisTransition)typeof(LocalGameUI).GetField("mapTransition", Any).GetValue(ui);
        transition.enabled = false;
        // OnDisable deliberately stops playback and discards its pending callback. The target is already applied.
        // This is a frozen sample of the real native Paint, not evidence of a running/completed transition.
        typeof(MapIrisTransition).GetField("age", Any).SetValue(transition, MapIrisTransition.CloseSeconds + MapIrisTransition.HoldSeconds * .5f);
        typeof(MapIrisTransition).GetMethod("Paint", Any).Invoke(transition, null);
        DisableInput(ui); Canvas.ForceUpdateCanvases();
        return "Native transition paint held (visual sample; playback stopped): " + nation + "; 260x260 miniature on its real canvas.";
    }

    public static string Capture(string name)
    {
        var ui = RequireUI();
        if (LoadingArtworkScreen.IsBlocking) throw new InvalidOperationException("Wait until the native loading overlay has closed.");
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
            throw new ArgumentException("Capture names may contain letters, digits, hyphens and underscores only.");
        RefreshLayout(ui);
        ui.StartCoroutine(CaptureNative(ui, name));
        return "Capture queued: " + Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/screenshots/" + name + ".png"));
    }
    static IEnumerator CaptureNative(LocalGameUI ui, string name)
    {
        yield return null; yield return null;
        RefreshLayout(ui);
        MonsterPouchPlayInspection.Capture(name); // Native screen capture includes every overlay canvas, including the iris.
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/screenshots"));
        Directory.CreateDirectory(directory);
        var arena = Object.FindFirstObjectByType<NationArenaScenery>();
        string evidence = Status() + "\n" + MonsterPouchLayoutPreview.Measure() +
            "\nnativeNationTiles=" + (arena != null ? arena.TileCount.ToString() : "none") +
            "\nQA fixture only. Capture is queued; inspect the resulting PNG before visual approval.";
        File.WriteAllText(Path.Combine(directory, name + ".txt"), evidence);
    }

    public static string Restore()
    {
        if (!File.Exists(BackupPath))
        {
            if (Active) throw new InvalidOperationException("Preview backup missing; cannot safely reconstruct prior preferences.");
            return "No preview backup; nothing to restore.";
        }
        var snapshot = ReadSnapshot();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (!Active) throw new InvalidOperationException("Exit the unrelated play session before recovery.");
            SessionState.SetBool(RestoreKey, true);
            StopPreviewObjects(); RestoreImmediately(snapshot, false);
            EditorApplication.isPlaying = false;
            return "Preferences restored; leaving QA Play Mode. Final restoration runs on EnteredEditMode.";
        }
        RestoreImmediately(snapshot, true);
        return "Original preferences, stores, Play settings and Game view restored. QA files retained at " + snapshot.QaRoot;
    }

    static void PlayModeChanged(PlayModeStateChange state)
    {
        if (!Active) return;
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SessionState.SetBool(RestoreKey, true);
            StopPreviewObjects(); RestoreImmediately(ReadSnapshot(), false);
        }
        else if (state == PlayModeStateChange.EnteredEditMode) RestoreImmediately(ReadSnapshot(), true);
    }
    static void BeforeQuit()
    {
        if (!Active) return;
        StopPreviewObjects(); RestoreImmediately(ReadSnapshot(), false); // Keep the backup until normal Edit Mode cleanup/recovery.
    }
    static void InitializeReadyScene()
    {
        if (!Active || Restoring || !EditorApplication.isPlaying || ready) return;
        var ui = Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null || ui.Match == null) return;
        try
        {
            ValidateIdentity(ui, ReadSnapshot());
            ui.Match.enabled = false;
            // Keep LocalGameUI Update/LateUpdate alive: they position the real vitals/HUD and animate the native view.
            // Only the authoritative match clock is frozen; all account writes remain isolated to QA stores.
            DisableInput(ui); ready = true;
        }
        catch (Exception error)
        {
            Debug.LogException(error); Restore();
        }
    }
    static string ShowLobby(TableStyle style, string monster)
    {
        var ui = RequireUI(); ValidateMonster(ui, monster); CancelTransition(ui);
        ui.CloseModal(); ui.Match.Menu(); ui.Match.enabled = false;
        if (!ui.CanSelectTrophyBoard(style)) throw new InvalidOperationException("Temporary QA profile cannot select this map.");
        CardTableTheme.Select(style); CardTableTheme.SelectLobby(style);
        typeof(LocalGameUI).GetField("chosen", Any).SetValue(ui, monster);
        Invoke(ui, "SetArenaStyle", style); Invoke(ui, "LoadTableArtwork");
        ui.OpenHome(); Invoke(ui, "StopMenuSlide"); ui.RenderPage(); RefreshLayout(ui); DisableInput(ui);
        return "Native QA lobby: " + CardTableTheme.MapId(style) + "; hero=" + monster;
    }
    static string ShowBattle(TableStyle style, string monster, bool combat)
    {
        ShowLobby(style, monster);
        var ui = RequireUI();
        if (!ui.Match.StartMatch(monster, new[] { "rosie", "suki", "pokrak" }, 20261025, CardTableTheme.MapId(style), CardTableTheme.MapId(style)))
            throw new InvalidOperationException(ui.Match.Notice);
        ui.Match.enabled = false;
        if (combat) ui.Match.Ready();
        ui.RenderPage(); RefreshLayout(ui); DisableInput(ui);
        if (ui.Match.Phase != (combat ? MatchPhase.Combat : MatchPhase.Preparation))
            throw new InvalidOperationException("Native preview did not reach its requested phase.");
        return "Native QA " + ui.Match.Phase + ": " + CardTableTheme.MapId(style) + "; hero=" + monster + "; simulation does not advance.";
    }
    static void CancelTransition(LocalGameUI ui)
    {
        var transition = (MapIrisTransition)typeof(LocalGameUI).GetField("mapTransition", Any).GetValue(ui);
        if (transition == null) return;
        transition.Cancel(); transition.enabled = true;
    }
    static bool TransitionPaintHeld(LocalGameUI ui)
    {
        var transition = (MapIrisTransition)typeof(LocalGameUI).GetField("mapTransition", Any).GetValue(ui);
        return transition != null && !transition.enabled && transition.gameObject.activeSelf;
    }
    static LocalGameUI RequireUI()
    {
        if (!Active || Restoring || !Application.isPlaying || !ready) throw new InvalidOperationException("Call Begin from Edit Mode and wait for Status=Ready.");
        var ui = Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null) throw new InvalidOperationException("Native QA scene is unavailable.");
        ValidateIdentity(ui, ReadSnapshot()); return ui;
    }
    static void ValidateIdentity(LocalGameUI ui, Snapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        for (int index = 0; index < StoreProperties.Length; index++)
            if (!SamePath((string)StoreProperty(StoreProperties[index]).GetValue(null), Path.Combine(snapshot.QaRoot, StoreFolders[index])))
                throw new InvalidOperationException("A profile override escaped the QA session.");
        var profile = (LocalGuestProfile)typeof(LocalGameUI).GetField("guestProfile", Any).GetValue(ui);
        if (profile == null || profile.ProfileId != snapshot.ProfileId) throw new InvalidOperationException("Refusing to preview a different account.");
    }
    static void ValidateMonster(LocalGameUI ui, string monster)
    {
        if (ui.Match.Config.Get(monster)?.IsMonster != true) throw new InvalidOperationException("The authored monster is missing: " + monster);
    }
    static TableStyle ParseNation(string value)
    {
        if (value != "naturalis" && value != "aquarion" && value != "geothar" && value != "volkanar")
            throw new ArgumentException("Use naturalis, aquarion, geothar or volkanar.");
        var style = CardTableTheme.FromMapId(value);
        var artwork = CardTableTheme.NationArtwork(style);
        if (artwork == null || artwork.Miniature == null || artwork.LobbyBackground == null)
            throw new InvalidOperationException("Incomplete native artwork: " + value);
        return style;
    }
    static void ValidateDimensions(string value)
    {
        var parts = (value ?? "").Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height) ||
            width < 200 || height < 200 || width > 3840 || height > 3840)
            throw new ArgumentException("Use a pixel size such as 393x852 or 1280x720 (200–3840 per axis).");
    }
    static void DisableInput(LocalGameUI ui)
    {
        foreach (var raycaster in ui.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
    }
    static void RefreshLayout(LocalGameUI ui)
    {
        Invoke(ui, "UpdateLayout"); Canvas.ForceUpdateCanvases();
    }
    static void Invoke(LocalGameUI ui, string method, params object[] args) => typeof(LocalGameUI).GetMethod(method, Any).Invoke(ui, args);
    static PropertyInfo StoreProperty(string name) => typeof(LocalGameUI).GetProperty(name, Any) ?? throw new MissingMemberException(typeof(LocalGameUI).FullName, name);
    static bool SamePath(string a, string b) => !string.IsNullOrEmpty(a) && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    static void InstallOverrides(Snapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        for (int index = 0; index < StoreProperties.Length; index++) StoreProperty(StoreProperties[index]).SetValue(null, Path.Combine(snapshot.QaRoot, StoreFolders[index]));
    }
    static void StopPreviewObjects()
    {
        ready = false;
        foreach (var ui in Object.FindObjectsByType<LocalGameUI>(FindObjectsSortMode.None))
        {
            ui.StopAllCoroutines();
            if (ui.Match != null) ui.Match.enabled = false;
            ui.gameObject.SetActive(false); // Stop writers/input before restoring global preferences; no result/menu callbacks.
        }
    }
    static int GameViewIndex()
    {
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        return (int)type.GetProperty("selectedSizeIndex", Any).GetValue(EditorWindow.GetWindow(type));
    }
    static void RestoreImmediately(Snapshot snapshot, bool finish)
    {
        ValidateSnapshot(snapshot);
        ready = false;
        // The runtime language flag is restored first; then restore the key's exact existence/value.
        GameText.SetLanguage(snapshot.Spanish);
        foreach (var preference in snapshot.Preferences)
        {
            if (!preference.Existed) PlayerPrefs.DeleteKey(preference.Key);
            else if (preference.IsFloat) PlayerPrefs.SetFloat(preference.Key, preference.Number);
            else PlayerPrefs.SetString(preference.Key, preference.Text);
        }
        if (!string.IsNullOrEmpty(snapshot.ProfileId))
        {
            PlayerPrefs.DeleteKey(LobbyAvatarSelection.PreferenceKey(snapshot.ProfileId));
            PlayerPrefs.DeleteKey("monster-pouch.lobby.mode." + snapshot.ProfileId);
        }
        PlayerPrefs.Save();
        // JsonUtility may round-trip null strings as empty strings. A missing override must remain null,
        // otherwise the runtime's ?? persistentDataPath fallback would be bypassed after review.
        for (int index = 0; index < StoreProperties.Length; index++)
            StoreProperty(StoreProperties[index]).SetValue(null, string.IsNullOrEmpty(snapshot.Overrides[index]) ? null : snapshot.Overrides[index]);
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(snapshot.StartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(snapshot.StartScene);
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)snapshot.EnterPlayOptions;
        EditorSettings.enterPlayModeOptionsEnabled = snapshot.EnterPlayOptionsEnabled;
        Time.timeScale = snapshot.TimeScale; Application.targetFrameRate = snapshot.TargetFrameRate; Screen.sleepTimeout = snapshot.SleepTimeout;
        var viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", Any).SetValue(view, snapshot.GameViewIndex); view.Repaint();
        if (!finish) return;
        SessionState.EraseBool(StateKey); SessionState.EraseBool(RestoreKey);
        File.Delete(BackupPath); // Only this known recovery file; QA data is retained, never recursively removed.
    }
    static Snapshot ReadSnapshot()
    {
        var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(BackupPath));
        ValidateSnapshot(snapshot); return snapshot;
    }
    static void WriteSnapshot(Snapshot snapshot)
    {
        ValidateSnapshot(snapshot); Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
        string temporary = BackupPath + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(snapshot, true));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            // Same-directory atomic replacement: the original valid snapshot survives an interrupted rewrite.
            if (File.Exists(BackupPath)) File.Replace(temporary, BackupPath, null);
            else File.Move(temporary, BackupPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    static void ValidateSnapshot(Snapshot snapshot)
    {
        if (snapshot == null || snapshot.Version != 1 || snapshot.Preferences == null || snapshot.Overrides?.Length != 3 ||
            string.IsNullOrEmpty(snapshot.QaRoot) || !SamePath(Path.GetDirectoryName(Path.GetFullPath(snapshot.QaRoot)), TempParent) ||
            !Guid.TryParseExact(Path.GetFileName(snapshot.QaRoot), "N", out _) ||
            snapshot.Preferences.Length != StringKeys.Length + FloatKeys.Length ||
            !snapshot.Preferences.Select(p => p.Key).SequenceEqual(StringKeys.Concat(FloatKeys)) ||
            snapshot.Preferences.Any(p => p.IsFloat != FloatKeys.Contains(p.Key)) ||
            (!string.IsNullOrEmpty(snapshot.ProfileId) && !Guid.TryParseExact(snapshot.ProfileId, "N", out _)))
            throw new InvalidDataException("Invalid native preview recovery snapshot; nothing was restored.");
    }
}

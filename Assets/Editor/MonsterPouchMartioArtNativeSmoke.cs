using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit Run only. No InitializeOnLoad, automatic session, asset save or simulation advance.
public static class MonsterPouchMartioArtNativeSmoke
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const string ArtRoot = "Assets/art/units/martio/animation-rework-20261026/";
    static readonly string[] Directions = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };
    static readonly Vector2Int[] BoardDirections = {
        new Vector2Int(0,-1), new Vector2Int(1,-1), new Vector2Int(1,0), new Vector2Int(1,1),
        new Vector2Int(0,1), new Vector2Int(-1,1), new Vector2Int(-1,0), new Vector2Int(-1,-1) };
    static readonly MethodInfo Advance = typeof(UnitPresentation).GetMethod("AdvancePresentation", Any);
    static LocalGameUI ui;
    static GameObject root;
    static MonsterUnit actor;
    static UnitPresentation view;
    static UnitArt art;
    static MartioVisualAssets bundle;
    static BoardCell origin;
    static Coroutine running;
    static string directory;
    static Report report;

    [Serializable] sealed class Sample
    {
        public string Name, SpritePath, Facing, Png;
        public Vector3 BodyWorld, FootWorld, HeadWorld, GroundWorld;
        public Vector2 ReferenceBodyWorldSize;
        public int Width, Height;
    }
    [Serializable] sealed class Report
    {
        public string Status = "running", Error, StartedUtc, FinishedUtc, Session, CurrentStep;
        public string Scope = "Native framebuffer and real-art presentation samples only; no combat/fan simulation or visual approval.";
        public bool Passed, QaActorRemoved;
        public bool ProductionAssetsWritten = false;
        public int UniqueSprites, Views;
        public float InGameSizePercent, AttackContactDelay;
        public List<Sample> Samples = new List<Sample>();
    }

    public static string Run()
    {
        if (root != null || running != null) throw new InvalidOperationException("Smoke already active; call Status or Cancel.");
        string session = MonsterPouchNationNativePreview.Status(); // Validates the existing isolated profile identity.
        if (!session.StartsWith("Ready; QA only;", StringComparison.Ordinal))
            throw new InvalidOperationException("Use the existing NationNativePreview session in a visible frozen battle first.");
        ui = Object.FindFirstObjectByType<LocalGameUI>();
        RequireSession();
        art = ((UnitArtCatalog)typeof(LocalGameUI).GetField("art", Any).GetValue(ui))?.Get("martio");
        bundle = Resources.Load<MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
        var definition = ui.Match.Config.Get("martio");
        if (art == null || bundle == null || definition == null || Advance == null)
            throw new InvalidOperationException("Installed Martio runtime art/definition/helper is missing.");
        origin = FindEmptyRoute();
        directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/martio-art-native/" +
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        Directory.CreateDirectory(directory);
        report = new Report { StartedUtc = DateTime.UtcNow.ToString("o"), Session = session, InGameSizePercent = art.InGameSizePercent };
        try
        {
            ValidateReferences();
            root = new GameObject("QA-Martio-Art-Smoke-" + Guid.NewGuid().ToString("N")) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(ui.Match.transform, false);
            actor = root.AddComponent<MonsterUnit>();
            actor.Initialize("qa-martio-art-only", BoardSide.Blue, UnitStats.FromDefinition(definition, null, false));
            // Anchor reference only: no Board occupancy/reservation or Match.Actors registration is changed.
            actor.SetCurrentCell(origin);
            view = root.AddComponent<UnitPresentation>();
            view.enabled = false;
            Reset(4);
            AssemblyReloadEvents.beforeAssemblyReload += CancelBeforeReload;
            EditorApplication.playModeStateChanged += OnPlayMode;
            running = ui.StartCoroutine(Drive());
            if (report.Status != "running") running = null;
            return "Explicit native Martio art smoke started; keep Game view rendering. Evidence: " + directory;
        }
        catch (Exception error)
        {
            report.Error = error.ToString(); Finish(); throw;
        }
    }

    public static string Status() => report == null ? "Not run." : JsonUtility.ToJson(report, true);
    public static string Cancel()
    {
        if (report != null && report.Status == "running") report.Error = "Cancelled explicitly; no PASS.";
        if (running != null && ui != null) ui.StopCoroutine(running);
        Finish();
        return "Only the smoke actor was removed; the existing QA session remains open.";
    }
    static void CancelBeforeReload() => Cancel();
    static void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Cancel(); }

    static IEnumerator Drive()
    {
        var steps = Samples();
        try
        {
            while (true)
            {
                object wait = null;
                bool next;
                try { next = steps.MoveNext(); if (next) wait = steps.Current; }
                catch (Exception error) { report.Error = error.ToString(); Debug.LogException(error); break; }
                if (!next) { report.Passed = true; break; }
                yield return wait;
            }
        }
        finally { (steps as IDisposable)?.Dispose(); Finish(); }
    }

    static IEnumerator Samples()
    {
        for (int d = 0; d < 8; d++)
        {
            Reset(d); report.CurrentStep = "idle-" + Directions[d];
            yield return null; yield return new WaitForEndOfFrame();
            Capture(report.CurrentStep, art.Animation((UnitFacing)d).Idle[0]);
        }
        foreach (var offset in new[] { new Vector2Int(1, 0), new Vector2Int(1, -1) })
        {
            Reset(2);
            var destination = ui.Match.Board.GetCell(origin.X + offset.x, origin.Y + offset.y);
            float duration = actor.BaseStats.MoveInterval, previous = 0;
            view.Move(root.transform.position, ui.Match.Mapper.GetWorldPosition(destination), duration);
            var clip = art.Animation(view.Facing);
            var fractions = new[] { .215f, .61f, .895f };
            for (int phase = 0; phase < fractions.Length; phase++)
            {
                Step(duration * (fractions[phase] - previous)); previous = fractions[phase];
                report.CurrentStep = "hop-" + (offset.y == 0 ? "cardinal-" : "diagonal-") + phase;
                yield return null; yield return new WaitForEndOfFrame();
                Capture(report.CurrentStep, clip.HopLeft[phase]);
            }
            Step(duration * (1.001f - previous));
            Check(!view.IsMoving, "Hop must finish.");
        }
        Reset(2); view.Hit(); Step(0); report.CurrentStep = "hurt-rest-e";
        yield return null; yield return new WaitForEndOfFrame();
        Capture(report.CurrentStep, bundle.HurtDirections[2]);
        Step(Mathf.Max(.05f, art.HitFlashDuration) + .01f);
        Check(art.Animation(UnitFacing.East).Idle.Contains(view.Renderer.sprite), "Hurt must return to Idle.");
        foreach (int direction in new[] { 2, 1 })
        {
            Reset(direction);
            Vector2Int offset = BoardDirections[direction];
            Vector3 target = ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(origin.X + offset.x, origin.Y + offset.y));
            view.Attack(target, true);
            var clip = art.Animation(view.Facing);
            float delay = view.AttackContactDelay;
            report.AttackContactDelay = delay;
            Check(delay > 0 && clip.AttackContactFrame == 2, "Current native contact delay/frame are invalid.");
            Step(delay * .25f); view.Hit(); Step(0);
            Check(view.IsAttacking && !view.AttackContactReached, "Hit interrupted the attack anticipation.");
            report.CurrentStep = "hurt-during-attack-" + Directions[direction];
            yield return null; yield return new WaitForEndOfFrame();
            Capture(report.CurrentStep, clip.Attack[0]);
            Step(delay * .75f);
            Check(view.IsAttacking && view.AttackContactReached, "Contact was not reached at the runtime delay.");
            report.CurrentStep = "attack-contact2-" + Directions[direction];
            yield return null; yield return new WaitForEndOfFrame();
            Capture(report.CurrentStep, clip.Attack[2]);
        }
        Reset(2); view.Die();
        float deathDuration = (float)typeof(UnitPresentation).GetField("DeathDuration", Any).GetRawConstantValue();
        float elapsed = 0;
        for (int frame = 0; frame < 4; frame++)
        {
            float time = deathDuration * (frame + .1f) / 4;
            Step(time - elapsed); elapsed = time;
            report.CurrentStep = "death-e-" + frame;
            yield return null; yield return new WaitForEndOfFrame();
            Capture(report.CurrentStep, art.Animation(UnitFacing.East).Death[frame]);
        }
        Step(deathDuration - elapsed + .01f);
        Check(view.DeathFinished, "East defeat did not finish.");
    }

    static void ValidateReferences()
    {
        Check(art.TravelMotion == UnitTravelMotion.WholeBodyHop && art.WholeBodyHopUsesPoses, "WholeBodyHop opt-in missing.");
        Check(art.DirectionalAnimations?.Length == 8 && bundle.HurtDirections?.Length == 8, "Eight views required.");
        var unique = new HashSet<Sprite>();
        for (int d = 0; d < 8; d++)
        {
            var clip = art.Animation((UnitFacing)d);
            Check(clip != null && !clip.FlipX && !clip.MoveFlipX && clip.AttackContactFrame == 2, "Invalid facing/contact.");
            Check(clip.Idle?.Length == 4 && clip.Idle[3] == clip.Idle[1], "Idle loop must be 0,1,2,1.");
            Check(clip.HopLeft?.Length == 3 && clip.HopRight?.Length == 3 && clip.Attack?.Length == 4 && clip.Death?.Length == 4, "Incomplete action references.");
            for (int i = 0; i < 3; i++) { Reference(clip.Idle[i], d, "idle", i, unique); Reference(clip.HopLeft[i], d, "hop", i, unique); Check(clip.HopRight[i] == clip.HopLeft[i], "Whole-body triplets disagree."); }
            for (int i = 0; i < 4; i++) { Reference(clip.Attack[i], d, "attack", i, unique); Reference(clip.Death[i], d, "death", i, unique); }
            Reference(bundle.HurtDirections[d], d, "hurt", 0, unique);
        }
        Check(unique.Count == 120, "Expected 120 distinct approved sprites.");
        report.UniqueSprites = unique.Count; report.Views = 8;
    }
    static void Reference(Sprite sprite, int direction, string action, int index, HashSet<Sprite> unique)
    {
        string d = Directions[direction], expected = ArtRoot + d + "/martio-" + d + "-" + action + "-" + index + ".png";
        Check(sprite != null && AssetDatabase.GetAssetPath(sprite) == expected, "Missing/wrong reference: " + expected);
        Check(sprite.rect.size == new Vector2(240, 240) && Mathf.Approximately(sprite.pixelsPerUnit, 160), "Invalid imported geometry: " + expected);
        unique.Add(sprite);
    }
    static BoardCell FindEmptyRoute()
    {
        for (int y = 5; y < 9; y++) for (int x = 1; x < 5; x++)
        {
            var cells = new[] { ui.Match.Board.GetCell(x, y), ui.Match.Board.GetCell(x + 1, y), ui.Match.Board.GetCell(x + 1, y - 1) };
            if (cells.All(c => c != null && !c.IsBlocked && c.OccupiedBy == null && c.ReservedBy == null)) return cells[0];
        }
        throw new InvalidOperationException("No empty visible route; prepare another frozen QA battle explicitly.");
    }
    static void Reset(int direction)
    {
        RequireSession(); root.transform.position = ui.Match.Mapper.GetWorldPosition(origin);
        view.Configure(actor, ui.Match.Mapper, art); view.enabled = false;
        view.Face(BoardDirections[direction]); Step(0);
    }
    static void Step(float seconds) => Advance.Invoke(view, new object[] { seconds });
    static void Capture(string name, Sprite expected)
    {
        RequireSession();
        Check(view.Renderer.sprite == expected && !view.Renderer.flipX, "Wrong rendered native pose: " + name);
        Check(view.TryGetPresentationAnchors(out var anchors) && anchors.HasCalibratedBodyReference &&
            anchors.ReferenceBodyWorldSize.x > 0 && anchors.ReferenceBodyWorldSize.y > 0 &&
            anchors.HeadWorldPosition.y > anchors.FootPivotWorldPosition.y, "Invalid native presentation anchors: " + name);
        Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            Check(frame != null && frame.width == Screen.width && frame.height == Screen.height, "Native framebuffer missing.");
            string path = Path.Combine(directory, name + ".png");
            File.WriteAllBytes(path, frame.EncodeToPNG());
            report.Samples.Add(new Sample { Name = name, SpritePath = AssetDatabase.GetAssetPath(expected), Facing = view.Facing.ToString(),
                Png = path, BodyWorld = root.transform.position, FootWorld = anchors.FootPivotWorldPosition,
                HeadWorld = anchors.HeadWorldPosition, GroundWorld = anchors.GroundWorldPosition,
                ReferenceBodyWorldSize = anchors.ReferenceBodyWorldSize, Width = frame.width, Height = frame.height });
        }
        finally { if (frame != null) Object.DestroyImmediate(frame); }
    }
    static void RequireSession()
    {
        if (!Application.isPlaying || EditorApplication.isCompiling || ui == null || ui.Match == null || ui.Match.enabled ||
            ui.Match.IsOnline || ui.Match.IsTraining || ui.Match.Phase != MatchPhase.Combat || ui.MapTransitionActive || LoadingArtworkScreen.IsBlocking ||
            !MonsterPouchNationNativePreview.Status().StartsWith("Ready; QA only;", StringComparison.Ordinal))
            throw new InvalidOperationException("Requires the existing isolated, frozen, visible QA combat. No session will be started or changed.");
    }
    static void Check(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    static void Finish()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= CancelBeforeReload;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        if (root != null) Object.DestroyImmediate(root); // Only the object created by this explicit Run.
        root = null; actor = null; view = null; running = null;
        if (report == null || report.Status != "running") return;
        report.QaActorRemoved = true; report.FinishedUtc = DateTime.UtcNow.ToString("o");
        report.Status = report.Passed && string.IsNullOrEmpty(report.Error) ? "passed-native-samples-awaiting-visual-review" : "failed-or-cancelled";
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true));
    }
}

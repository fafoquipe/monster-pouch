using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Local;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit editor QA only. Reuses the existing isolated, recoverable preview session.
// Captures the actual game framebuffer; never changes scene assets or account rewards.
public static class EronyaMapReview
{
    const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly string[] Maps = { "naturalis", "aquarion", "geothar", "volkanar", "royal", "classic", "lounge", "casino-night", "aireola" };
    static readonly string[] Controls = { "Brief tray", "Brief drop", "Bench drop", "Enemy round HUD", "Player round HUD", "Round label", "Clock", "Battle pause", "LISTO", "Reroll" };
    static string state = "Idle";
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/eronya-maps"));

    [Serializable] sealed class Control { public string name; public Rect rect; public Vector3 scale; }
    [Serializable] sealed class Evidence
    {
        public string map, phase, fixture, nativeSession, activeArtwork;
        public int width, height, tileCount;
        public Rect safeArea, boardWorld, boardPortrait, cameraRect;
        public Vector3 cameraPosition;
        public float cameraSize;
        public Vector2 cellSize, boardOffset;
        public Vector3[] cellCenters;
        public Control[] controls;
        public bool allCellsMapBack, decorativeApronRejectsInput, usesPortraitArtwork, usesContinuousArtwork;
    }

    public static string Status() => state;
    public static string Begin(string dimensions) => MonsterPouchNationNativePreview.Begin(dimensions);
    public static string Restore() => MonsterPouchNationNativePreview.Restore();

    public static string CaptureSet(string prefix) => StartCapture(prefix, false);
    public static string CaptureBaseline(string prefix) => StartCapture(prefix, true);

    static string StartCapture(string prefix, bool baseline)
    {
        if (state.StartsWith("Running", StringComparison.Ordinal)) throw new InvalidOperationException("A capture set is already running.");
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
            throw new ArgumentException("Use letters, digits and hyphens for the evidence prefix.");
        if (!MonsterPouchNationNativePreview.Status().StartsWith("Ready", StringComparison.Ordinal))
            throw new InvalidOperationException("Begin the isolated preview and wait until its Status is Ready.");
        var ui = Object.FindFirstObjectByType<LocalGameUI>();
        state = "Running " + prefix;
        ui.StartCoroutine(CaptureSequence(ui, prefix, baseline));
        return state;
    }

    static IEnumerator CaptureSequence(LocalGameUI ui, string prefix, bool baseline)
    {
        Directory.CreateDirectory(Root);
        int captured = 0;
        foreach (string map in baseline ? new[] { "naturalis" } : Maps)
        foreach (bool combat in new[] { false, true })
        {
            if (combat && map != "naturalis" && map != "casino-night") continue;
            string phase = combat ? "combat" : "preparation";
            string name = prefix + "-" + map + "-" + Screen.width + "x" + Screen.height + "-" + phase;
            try
            {
                // ShowBattle validates the disposable identity before each scene change.
                typeof(MonsterPouchNationNativePreview).GetMethod("ShowBattle", Hidden).Invoke(null,
                    new object[] { CardTableTheme.FromMapId(map), "bugaloo", combat });
            }
            catch (Exception error) { Fail(name, error); yield break; }
            yield return null;
            yield return new WaitForSecondsRealtime(.45f);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Texture2D frame = null;
            try
            {
                frame = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(Root, name + ".png"), frame.EncodeToPNG());
                File.WriteAllText(Path.Combine(Root, name + ".json"), JsonUtility.ToJson(Measure(ui, map, phase), true));
                if (!combat) SaveCorners(frame, ui, name);
                captured++;
                state = "Running " + prefix + "; captured=" + name;
                File.WriteAllText(Path.Combine(Root, prefix + "-status.txt"), state);
            }
            catch (Exception error) { Fail(name, error); yield break; }
            finally { if (frame != null) Object.Destroy(frame); }
        }
        state = "Complete " + prefix + "; " + captured + " native game captures; isolated QA fixture, simulation held at initial state.";
        File.WriteAllText(Path.Combine(Root, prefix + "-status.txt"), state);
    }

    static Evidence Measure(LocalGameUI ui, string map, string phase)
    {
        var arena = ui.Match.Mapper.GetComponent<CardTableArena>();
        var cells = ui.Match.Board.GetAllCells().OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
        var centers = cells.Select(c => ui.Match.Mapper.GetWorldPosition(c)).ToArray();
        var camera = ui.GameCamera;
        var scenery = arena.GetComponent<NationArenaScenery>();
        var roots = ui.CanvasRoot.GetComponentsInChildren<RectTransform>(true);
        return new Evidence {
            map = map, phase = phase, fixture = "Disposable local QA identity; native gameplay renderer; no simulated screenshot or reward claim.",
            nativeSession = MonsterPouchNationNativePreview.Status(), width = Screen.width, height = Screen.height,
            safeArea = Screen.safeArea, boardWorld = arena.BoardWorldBounds, boardPortrait = arena.BoardPortraitBounds,
            cameraRect = camera.rect, cameraPosition = camera.transform.position, cameraSize = camera.orthographicSize,
            cellSize = ui.Match.Mapper.CellSize, boardOffset = ui.Match.Mapper.BoardOffset, cellCenters = centers,
            tileCount = scenery?.TileCount ?? 0,
            activeArtwork = scenery != null && scenery.IsVisible ? scenery.ActiveCombatArtwork?.name ?? "none" : "original-scene",
            usesPortraitArtwork = scenery != null && scenery.IsVisible && scenery.UsesPortraitArtwork,
            usesContinuousArtwork = scenery != null && scenery.IsVisible && scenery.UsesContinuousArtwork,
            allCellsMapBack = cells.All(c => ui.Match.Mapper.TryGetCell(ui.Match.Mapper.GetWorldPosition(c), out var mapped) && mapped == c),
            decorativeApronRejectsInput = ApronRejectsInput(ui.Match.Mapper, arena.BoardWorldBounds),
            controls = roots.Where(r => Controls.Contains(r.name)).Select(r => new Control { name = r.name, rect = ScreenRect(r), scale = r.lossyScale }).ToArray()
        };
    }
    static bool ApronRejectsInput(BoardWorldMapper mapper, Rect bounds)
    {
        float epsilon = Mathf.Min(Mathf.Abs(mapper.CellSize.x), Mathf.Abs(mapper.CellSize.y)) * .01f;
        for (int x = 0; x < BoardManager.Width; x++)
        {
            float center = bounds.xMin + (x + .5f) * bounds.width / BoardManager.Width;
            if (mapper.TryGetCell(new Vector3(center, bounds.yMin - epsilon), out _) ||
                mapper.TryGetCell(new Vector3(center, bounds.yMax + epsilon), out _)) return false;
        }
        for (int y = 0; y < BoardManager.Height; y++)
        {
            float center = bounds.yMin + (y + .5f) * bounds.height / BoardManager.Height;
            if (mapper.TryGetCell(new Vector3(bounds.xMin - epsilon, center), out _) ||
                mapper.TryGetCell(new Vector3(bounds.xMax + epsilon, center), out _)) return false;
        }
        return true;
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
    static void SaveCorners(Texture2D frame, LocalGameUI ui, string name)
    {
        // Literal crops of this same native framebuffer, without retouching or resampling.
        var grid = ui.Match.Mapper.GetComponent<CardTableArena>().BoardWorldBounds;
        var positions = new[] { new Vector2(grid.xMin,grid.yMax),new Vector2(grid.xMax,grid.yMax),
            new Vector2(grid.xMin,grid.yMin),new Vector2(grid.xMax,grid.yMin) };
        var labels = new[] { "top-left", "top-right", "bottom-left", "bottom-right" };
        string folder = Path.Combine(Root,"corners"); Directory.CreateDirectory(folder);
        for(int i=0;i<positions.Length;i++)
        {
            Vector3 point = ui.GameCamera.WorldToScreenPoint(positions[i]);
            int radius = Mathf.RoundToInt(72f * ui.GameCamera.pixelRect.height / 960f);
            int x = Mathf.Clamp(Mathf.RoundToInt(point.x)-radius,0,frame.width-1);
            int y = Mathf.Clamp(Mathf.RoundToInt(point.y)-radius,0,frame.height-1);
            int width = Mathf.Min(radius*2,frame.width-x), height = Mathf.Min(radius*2,frame.height-y);
            var crop = new Texture2D(width,height,TextureFormat.RGBA32,false);
            try { crop.SetPixels(frame.GetPixels(x,y,width,height)); crop.Apply();
                File.WriteAllBytes(Path.Combine(folder,name+"-"+labels[i]+".png"),crop.EncodeToPNG()); }
            finally { Object.Destroy(crop); }
        }
    }
    static void Fail(string name, Exception error)
    {
        state = "Failed " + name + ": " + error;
        Directory.CreateDirectory(Root); File.WriteAllText(Path.Combine(Root, "failure.txt"), state);
        Debug.LogException(error);
    }
}

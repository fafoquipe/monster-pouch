using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Imports new authored combat sheets without replacing portraits or existing combat clips.</summary>
public static class MonsterPouchDirectionalSetup
{
    const string CatalogPath = "Assets/Resources/MonsterPouch/UnitArt.asset";
    const string Folder = "Assets/art/units/directional-v1/";
    const string ReportPath = "Logs/local-validation/directional-report.json";
    static readonly string[] Full = { "sepora", "flo", "jazar", "blotan", "gochan", "trimol", "tsu", "atong", "tokoro", "aky", "hymay", "bugaloo", "popow" };
    static readonly string[] Specials = { "atori", "bugui", "kayon", "stein", "tauris" };
    static readonly int[] FacingRows = { 4, 3, 2, 1, 0, 7, 6, 5 };
    static readonly HashSet<string> MirrorNorthWest = new HashSet<string> { "sepora", "flo", "jazar", "tsu", "atong", "tokoro", "bugaloo", "blotan", "gochan", "bugui" };
    static readonly List<SheetReport> ImportedSheets = new List<SheetReport>();

    [Serializable]
    public sealed class Layout
    {
        public string id;
        public int columns;
        // Optional arrays use runtime order N, NE, E, SE, S, SW, W, NW.
        public int[] rowForFacing;
        public int[] rowEdgesTop;
        public int[] mirroredFacings;
        public int[] idle;
        public int[] move;
        public int[] attack;
        public int[] special;
        public int[] death;
        public int contactFrame = 1;
    }
    [Serializable] public sealed class LayoutFile { public Layout[] layouts; }
    [Serializable] public sealed class Report { public string generatedUtc; public bool valid; public string[] errors; public UnitReport[] units; public SheetReport[] sheets; }
    [Serializable] public sealed class SheetReport
    {
        public string path;
        public int width, height, frames;
        public float pixelsPerUnit, minimumWidth, maximumWidth, minimumHeight, maximumHeight;
        public string[] edgeTouchedFrames, extremeAspectFrames;
    }
    [Serializable] public sealed class UnitReport
    {
        public string id, portrait, source;
        public bool preservedTemporalClips;
        public float worldWidth, referencePixelWidth, southVisibleWorldWidth, southWorldHeight;
        public DirectionReport[] directions;
    }
    [Serializable] public sealed class DirectionReport
    {
        public string direction, idleSource, specialSource;
        public int idle, move, attack, death, revive, special;
        public bool flipX, specialFlipX;
        public float idlePpu, specialPpu;
    }
    sealed class Prepared
    {
        public UnitArt art;
        public UnitDirectionalAnimation[] clips;
        public Sprite[][] specials;
        public bool[] specialFlips;
        public string source;
    }

    public static string Apply()
    {
        RequireEditMode();
        ImportedSheets.Clear();
        var catalog = LoadCatalog();
        var layouts = ReadLayouts();
        foreach (string id in Full.Concat(Specials).Concat(new[] { "anuik" }))
            if (catalog.Get(id) == null || catalog.Get(id).Portrait == null)
                throw new InvalidOperationException("Missing existing character or portrait: " + id);
        foreach (string id in Full) RequireFile(Folder + id + "-atlas.png");
        foreach (string id in Specials) RequireFile(Folder + id + "-special.png");

        // Import and validate all sheets before changing the catalog. A bad sheet cannot leave a partial roster.
        var prepared = new List<Prepared>();
        foreach (string id in Full) prepared.Add(PrepareFull(catalog.Get(id), ResolveLayout(id, false, layouts)));
        foreach (string id in Specials) prepared.Add(PrepareSpecial(catalog.Get(id), ResolveLayout(id, true, layouts)));
        UnitArt anuik = catalog.Get("anuik");
        if (anuik.DirectionalAnimations == null || anuik.DirectionalAnimations.Length != 8 ||
            anuik.DirectionalAnimations.Any(c => c == null || !ValidFrames(c.Revive, 1)))
            throw new InvalidOperationException("Anuik requires the existing eight revival clips.");

        foreach (Prepared entry in prepared)
        {
            UnitArt art = entry.art;
            if (entry.clips != null)
            {
                art.DirectionalAnimations = entry.clips;
                art.IdleDirections = entry.clips.Select(c => c.Idle[0]).ToArray();
                art.AttackDirections = entry.clips.Select(c => c.Attack[c.AttackContactFrame]).ToArray();
                art.WalkRigs = new UnitSpriteRig[8];
                art.ProvisionalDirectionalProjection = false;
            }
            else
            {
                for (int d = 0; d < 8; d++)
                {
                    art.DirectionalAnimations[d].Special = entry.specials[d];
                    art.DirectionalAnimations[d].SpecialFlipX = entry.specialFlips[d];
                    art.DirectionalAnimations[d].SpecialDuration = .65f;
                }
            }
            string note = "Directional-v1: " + entry.source + "; portrait and presentation size preserved.";
            if (string.IsNullOrEmpty(art.SourceNotes) || !art.SourceNotes.Contains(note)) art.SourceNotes += "\n" + note;
        }
        foreach (UnitDirectionalAnimation clip in anuik.DirectionalAnimations)
        {
            clip.Special = clip.Revive;
            clip.SpecialFlipX = clip.FlipX;
            clip.SpecialDuration = 1.2f;
        }
        Report report = MakeReport(catalog);
        WriteReport(report);
        if (!report.valid) throw new InvalidOperationException(string.Join("\n", report.errors));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return "19 characters validated: eight directions, idle/move/attack/death/special clips. Original portraits preserved. " + Path.GetFullPath(ReportPath);
    }

    static Prepared PrepareFull(UnitArt art, Layout layout)
    {
        string path = Folder + art.Id + "-atlas.png";
        Sprite[,] frames = Import(path, 100, layout, art.Id);
        float ppu = VisibleWidth(frames[0, layout.idle[0]]) * art.Portrait.pixelsPerUnit / Mathf.Max(1, art.ReferencePixelWidth);
        frames = CalibratePixelsPerUnit(path, frames, ppu);
        InspectSheet(path, frames, ppu);
        var clips = new UnitDirectionalAnimation[8];
        for (int d = 0; d < 8; d++)
        {
            int row = layout.rowForFacing[d];
            bool flip = layout.mirroredFacings.Contains(d);
            clips[d] = new UnitDirectionalAnimation
            {
                Idle = Sequence(frames, row, layout.idle), Move = Sequence(frames, row, layout.move),
                Attack = Sequence(frames, row, layout.attack), Death = Sequence(frames, row, layout.death),
                Special = Sequence(frames, row, layout.special), SpecialDuration = .65f,
                IdleFramesPerSecond = 2.5f, MoveFramesPerSecond = 6f,
                AttackContactFrame = layout.contactFrame, FlipX = flip, SpecialFlipX = flip
            };
        }
        return new Prepared { art = art, clips = clips, source = path };
    }

    static Prepared PrepareSpecial(UnitArt art, Layout layout)
    {
        if (art.DirectionalAnimations == null || art.DirectionalAnimations.Length != 8 || art.DirectionalAnimations.Any(c => c == null || !ValidFrames(c.Idle, 1)))
            throw new InvalidOperationException("Existing combat clips must be retained for " + art.Id);
        string path = Folder + art.Id + "-special.png";
        Sprite[,] frames = Import(path, 100, layout, art.Id + "-special");
        // Match the pre-existing standing height; arms spread during a cast must not shrink the body.
        float height = art.Idle(UnitFacing.South).bounds.size.y;
        float ppu = frames[0, layout.special[0]].rect.height / Mathf.Max(.01f, height);
        frames = CalibratePixelsPerUnit(path, frames, ppu);
        InspectSheet(path, frames, ppu);
        return new Prepared
        {
            art = art, source = path,
            specials = Enumerable.Range(0, 8).Select(d => Sequence(frames, layout.rowForFacing[d], layout.special)).ToArray(),
            specialFlips = Enumerable.Range(0, 8).Select(d => layout.mirroredFacings.Contains(d)).ToArray()
        };
    }

    static Sprite[,] Import(string path, float ppu, Layout layout, string prefix) =>
        MonsterPouchSpriteAnimationImporter.ImportGrid(path, ppu, layout.columns, 8, prefix + "-directional-v1", adaptive: true, rowEdgesTop: layout.rowEdgesTop);

    static Sprite[,] CalibratePixelsPerUnit(string path, Sprite[,] frames, float ppu)
    {
        if (Mathf.Approximately(frames[0, 0].pixelsPerUnit, ppu)) return frames;
        int rows = frames.GetLength(0), columns = frames.GetLength(1);
        var names = new string[rows, columns];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < columns; c++) names[r, c] = frames[r, c].name;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.spritePixelsPerUnit = ppu;
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < columns; c++) frames[r, c] = sprites[names[r, c]];
        return frames;
    }
    static Sprite[] Sequence(Sprite[,] frames, int row, int[] columns) => columns.Select(c => frames[row, c]).ToArray();
    static float VisibleWidth(Sprite sprite) => Mathf.Max(1, sprite.rect.width - 4);
    static bool ValidFrames(Sprite[] frames, int minimum) => frames != null && frames.Length >= minimum && frames.All(s => s != null);

    static Layout[] ReadLayouts()
    {
        const string path = Folder + "layouts.json";
        if (!File.Exists(path)) return Array.Empty<Layout>();
        var file = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path));
        return file?.layouts ?? Array.Empty<Layout>();
    }

    static Layout ResolveLayout(string id, bool specialOnly, Layout[] overrides)
    {
        bool extended = id == "sepora" || id == "tsu";
        bool extendedSpecial = id == "trimol" || id == "gochan";
        var layout = new Layout
        {
            id = id, columns = specialOnly ? id == "bugui" ? 5 : 4 : extended || extendedSpecial ? 11 : 10,
            rowForFacing = (int[])FacingRows.Clone(), mirroredFacings = Array.Empty<int>(),
            // User's exact Bugui sheet has narrow gaps before each next row's overhead sparks.
            rowEdgesTop = specialOnly && id == "bugui" ? new[] { 0, 226, 396, 569, 744, 920, 1098, 1276, 1536 } : null,
            idle = new[] { 0, 1 }, move = new[] { 2, 3 },
            attack = extended ? new[] { 4, 5, 6 } : new[] { 4, 5 },
            special = specialOnly ? id == "bugui" ? new[] { 0, 1, 2, 3, 4 } : new[] { 0, 1, 2, 3 } : extended ? new[] { 7, 8 } : extendedSpecial ? new[] { 6, 7, 8 } : new[] { 6, 7 },
            death = extended || extendedSpecial ? new[] { 9, 10 } : new[] { 8, 9 }
        };
        if (MirrorNorthWest.Contains(id))
        {
            layout.rowForFacing[7] = 3;
            layout.mirroredFacings = new[] { 7 };
        }
        Layout change = overrides.FirstOrDefault(x => x != null && x.id == id);
        if (change != null)
        {
            if (change.columns > 0) layout.columns = change.columns;
            if (change.rowForFacing != null) layout.rowForFacing = change.rowForFacing;
            if (change.rowEdgesTop != null) layout.rowEdgesTop = change.rowEdgesTop;
            if (change.mirroredFacings != null) layout.mirroredFacings = change.mirroredFacings;
            if (change.idle != null) layout.idle = change.idle;
            if (change.move != null) layout.move = change.move;
            if (change.attack != null) layout.attack = change.attack;
            if (change.special != null) layout.special = change.special;
            if (change.death != null) layout.death = change.death;
            layout.contactFrame = change.contactFrame;
        }
        if (layout.rowForFacing.Length != 8 || layout.rowForFacing.Any(r => r < 0 || r >= 8) || layout.mirroredFacings.Any(d => d < 0 || d >= 8))
            throw new InvalidDataException("Invalid direction mapping for " + id);
        int[][] groups = specialOnly ? new[] { layout.special } : new[] { layout.idle, layout.move, layout.attack, layout.special, layout.death };
        if (groups.Any(g => g.Length == 0 || g.Any(c => c < 0 || c >= layout.columns)) || (!specialOnly && (layout.contactFrame < 0 || layout.contactFrame >= layout.attack.Length)))
            throw new InvalidDataException("Invalid frame mapping for " + id);
        return layout;
    }

    public static string Validate()
    {
        Report report = MakeReport(LoadCatalog());
        WriteReport(report);
        if (!report.valid) throw new InvalidOperationException(string.Join("\n", report.errors));
        return "Directional roster valid: " + report.units.Length + " characters, 152 direction clips. " + Path.GetFullPath(ReportPath);
    }

    static Report MakeReport(UnitArtCatalog catalog)
    {
        var errors = new List<string>();
        var units = new List<UnitReport>();
        foreach (string id in Full.Concat(Specials).Concat(new[] { "anuik" }))
        {
            UnitArt art = catalog.Get(id);
            if (art == null) { errors.Add(id + ": missing art"); continue; }
            if (art.Portrait == null) errors.Add(id + ": missing portrait");
            var directions = new List<DirectionReport>();
            for (int d = 0; d < 8; d++)
            {
                UnitDirectionalAnimation clip = art.Animation((UnitFacing)d);
                if (clip == null) { errors.Add(id + ": missing " + (UnitFacing)d); continue; }
                if (!ValidFrames(clip.Idle, 2) || !ValidFrames(clip.Move, 2) || !ValidFrames(clip.Attack, 2) || !ValidFrames(clip.Death, 2) || !ValidFrames(clip.Special, 1))
                    errors.Add(id + "/" + (UnitFacing)d + ": incomplete authored clips");
                if (id == "anuik" && !ValidFrames(clip.Revive, 1)) errors.Add("Anuik revival missing: " + (UnitFacing)d);
                directions.Add(new DirectionReport
                {
                    direction = ((UnitFacing)d).ToString(), idle = clip.Idle?.Length ?? 0, move = clip.Move?.Length ?? 0,
                    attack = clip.Attack?.Length ?? 0, death = clip.Death?.Length ?? 0, revive = clip.Revive?.Length ?? 0, special = clip.Special?.Length ?? 0,
                    flipX = clip.FlipX, specialFlipX = clip.SpecialFlipX,
                    idleSource = ValidFrames(clip.Idle, 1) ? AssetDatabase.GetAssetPath(clip.Idle[0]) + ":" + clip.Idle[0].name : "",
                    specialSource = ValidFrames(clip.Special, 1) ? AssetDatabase.GetAssetPath(clip.Special[0]) + ":" + clip.Special[0].name : "",
                    idlePpu = ValidFrames(clip.Idle, 1) ? clip.Idle[0].pixelsPerUnit : 0,
                    specialPpu = ValidFrames(clip.Special, 1) ? clip.Special[0].pixelsPerUnit : 0
                });
            }
            Sprite south = art.Idle(UnitFacing.South);
            float scale = art.Portrait == null ? 1 : 1.2f * art.WorldWidth * art.Portrait.pixelsPerUnit / Mathf.Max(1, art.ReferencePixelWidth);
            units.Add(new UnitReport
            {
                id = id, portrait = art.Portrait == null ? "" : AssetDatabase.GetAssetPath(art.Portrait),
                source = Full.Contains(id) ? Folder + id + "-atlas.png" : id == "anuik" ? "Existing authored revival" : Folder + id + "-special.png",
                preservedTemporalClips = !Full.Contains(id), worldWidth = art.WorldWidth, referencePixelWidth = art.ReferencePixelWidth,
                southVisibleWorldWidth = south == null ? 0 : VisibleWidth(south) / south.pixelsPerUnit * scale,
                southWorldHeight = south == null ? 0 : south.bounds.size.y * scale, directions = directions.ToArray()
            });
        }
        // Keep import diagnostics when validation is called after an editor domain reload.
        SheetReport[] sheets = ImportedSheets.ToArray();
        if (sheets.Length == 0 && File.Exists(ReportPath))
            sheets = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath))?.sheets ?? Array.Empty<SheetReport>();
        return new Report { generatedUtc = DateTime.UtcNow.ToString("O"), valid = errors.Count == 0, errors = errors.ToArray(), units = units.ToArray(), sheets = sheets };
    }

    static void InspectSheet(string path, Sprite[,] frames, float ppu)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new InvalidDataException(path);
            Color32[] pixels = texture.GetPixels32();
            var touches = new List<string>();
            var aspects = new List<string>();
            var sprites = frames.Cast<Sprite>().ToArray();
            foreach (Sprite sprite in sprites)
            {
                Rect r = sprite.rect;
                int x0 = Mathf.RoundToInt(r.xMin), x1 = Mathf.RoundToInt(r.xMax) - 1;
                int y0 = Mathf.RoundToInt(r.yMin), y1 = Mathf.RoundToInt(r.yMax) - 1;
                bool edge = false;
                for (int x = x0; x <= x1 && !edge; x++) edge = pixels[y0 * texture.width + x].a > 128 || pixels[y1 * texture.width + x].a > 128;
                for (int y = y0; y <= y1 && !edge; y++) edge = pixels[y * texture.width + x0].a > 128 || pixels[y * texture.width + x1].a > 128;
                if (edge) touches.Add(sprite.name);
                float aspect = r.width / Mathf.Max(1, r.height);
                if (aspect < .18f || aspect > 5.5f) aspects.Add(sprite.name);
            }
            ImportedSheets.Add(new SheetReport
            {
                path = path, width = texture.width, height = texture.height, frames = sprites.Length, pixelsPerUnit = ppu,
                minimumWidth = sprites.Min(s => s.rect.width), maximumWidth = sprites.Max(s => s.rect.width),
                minimumHeight = sprites.Min(s => s.rect.height), maximumHeight = sprites.Max(s => s.rect.height),
                edgeTouchedFrames = touches.ToArray(), extremeAspectFrames = aspects.ToArray()
            });
            if (touches.Count > 0 || aspects.Count > 0)
                Debug.LogWarning(path + ": inspect " + touches.Count + " slices touching an opaque edge and " + aspects.Count + " extreme aspect ratios.");
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    public static string Gallery()
    {
        RequireEditMode();
        Validate();
        UnitArtCatalog catalog = LoadCatalog();
        string[] ids = Full.Concat(Specials).Concat(new[] { "anuik" }).ToArray();
        Directory.CreateDirectory("Logs/local-validation");
        RenderGallery(catalog, ids, false, "Logs/local-validation/directional-idle-gallery.png");
        RenderGallery(catalog, ids, true, "Logs/local-validation/directional-special-gallery.png");
        return "Wrote idle and special galleries to " + Path.GetFullPath("Logs/local-validation") + ". Rows: " + string.Join(", ", ids) + "; columns N, NE, E, SE, S, SW, W, NW.";
    }

    static void RenderGallery(UnitArtCatalog catalog, string[] ids, bool special, string path)
    {
        const float cell = 1.3f, left = 1.8f, header = .75f;
        float width = left + 8 * cell, height = header + ids.Length * cell;
        Scene scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Directional gallery") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(root, scene);
        RenderTexture target = null;
        Texture2D output = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Gallery camera");
            cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = height * .5f;
            camera.transform.position = new Vector3(width * .5f, -height * .5f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .09f, .15f, 1);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = new RenderTexture(Mathf.CeilToInt(width * 100), Mathf.CeilToInt(height * 100), 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            for (int d = 0; d < 8; d++)
                GalleryText(root.transform, ((UnitFacing)d).ToString(), new Vector3(left + (d + .5f) * cell, -header * .5f, -1), TextAnchor.MiddleCenter);
            for (int i = 0; i < ids.Length; i++)
            {
                float y = -header - (i + .5f) * cell;
                GalleryText(root.transform, ids[i], new Vector3(.12f, y, -1), TextAnchor.MiddleLeft);
                UnitArt art = catalog.Get(ids[i]);
                for (int d = 0; d < 8; d++)
                {
                    UnitDirectionalAnimation clip = art.Animation((UnitFacing)d);
                    Sprite sprite = special ? clip.Special[clip.Special.Length / 2] : clip.Idle[0];
                    bool flip = special ? clip.SpecialFlipX : clip.FlipX;
                    var item = new GameObject(ids[i] + " " + (UnitFacing)d);
                    item.transform.SetParent(root.transform);
                    var renderer = item.AddComponent<SpriteRenderer>();
                    renderer.sharedMaterial = Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");
                    renderer.sprite = sprite;
                    renderer.flipX = flip;
                    float scale = (cell - .16f) / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                    item.transform.localScale = Vector3.one * scale;
                    Vector3 center = sprite.bounds.center;
                    if (flip) center.x = -center.x;
                    item.transform.position = new Vector3(left + (d + .5f) * cell, y, 0) - center * scale;
                }
            }
            camera.Render();
            RenderTexture.active = target;
            output = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            output.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            output.Apply();
            File.WriteAllBytes(path, output.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (output != null) UnityEngine.Object.DestroyImmediate(output);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void GalleryText(Transform parent, string value, Vector3 position, TextAnchor anchor)
    {
        var item = new GameObject(value);
        item.transform.SetParent(parent);
        item.transform.position = position;
        var text = item.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        item.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.text = value;
        text.anchor = anchor;
        text.fontSize = 48;
        text.characterSize = .05f;
        text.color = Color.white;
    }

    static void WriteReport(Report report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
    }
    static UnitArtCatalog LoadCatalog() => AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath) ?? throw new InvalidOperationException("Missing UnitArt catalog.");
    static void RequireFile(string path) { if (!File.Exists(path)) throw new FileNotFoundException("Missing animation sheet.", path); }
    static void RequireEditMode() { if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first."); }

    public static string BuildWindows()
    {
        RequireEditMode();
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException("Wait for a successful editor compilation before building Windows.");
        Validate();
        const string path = "Builds/Windows-Directions/Monster Pouch.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/main-scene.unity" }, locationPathName = path,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        string result = report.summary.result + "; errors=" + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings + "; path=" + Path.GetFullPath(path);
        Directory.CreateDirectory("Logs/local-validation");
        File.WriteAllText("Logs/local-validation/directional-build.txt", result);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(result);
        return result;
    }
}

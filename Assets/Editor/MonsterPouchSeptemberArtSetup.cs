using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the September 14 source designs without replacing approved battle animation clips.</summary>
public static class MonsterPouchSeptemberArtSetup
{
    private const string CatalogPath = "Assets/resources/MonsterPouch/UnitArt.asset";
    private const string ArtRoot = "Assets/art/units/user-sept14/";
    private enum Background { Alpha, Black, DarkNeutral, White }

    private sealed class Design
    {
        public readonly string Id;
        public readonly Background Background;
        public readonly float Width;
        public Design(string id, Background background, float width)
        { Id = id; Background = background; Width = width; }
    }

    private static readonly Design[] Designs =
    {
        new Design("anuik", Background.Black, 1.18f),
        new Design("bugaloo", Background.Alpha, 1.05f),
        new Design("popow", Background.Alpha, 1.05f),
        new Design("sepora", Background.DarkNeutral, 1.0f),
        new Design("tauris", Background.Alpha, .75f),
        new Design("atori", Background.Alpha, .88f),
        new Design("blotan", Background.Alpha, .85f),
        new Design("bugui", Background.Alpha, .72f),
        new Design("flo", Background.White, .78f),
        new Design("gochan", Background.Alpha, .87f),
        new Design("jazar", Background.Alpha, .88f),
        new Design("kayon", Background.Black, .8f),
        new Design("stein", Background.Black, .86f),
        new Design("trimol", Background.Alpha, .82f),
        new Design("tsu", Background.Alpha, .68f),
        new Design("atong", Background.White, .72f),
        new Design("tokoro", Background.White, .9f),
        new Design("aky", Background.Alpha, .82f),
        new Design("hymay", Background.White, .73f)
    };

    [MenuItem("Monster Pouch/Art/Import September 14 character designs")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before importing the September character art.");
        var catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Existing UnitArt catalog is required.");
        // Validate the complete source set before changing the catalog.
        foreach (Design design in Designs)
            if (!File.Exists(SourcePath(design.Id))) throw new FileNotFoundException("Missing September design", SourcePath(design.Id));

        var entries = new List<UnitArt>(catalog.Units ?? Array.Empty<UnitArt>());
        var report = new StringBuilder("# September 14 character design import\n\nOriginal source files are preserved. " +
            "Only border-connected background pixels are removed in RGBA derivatives. Sprite rectangles and GUID references " +
            "are assigned through the Unity Editor API.\n\n" +
            "Existing battle animations, reference scale and foot anchors are retained. Newly added characters use the " +
            "existing runtime breathing, walking bob, attack anticipation/contact and death-collapse animation of their " +
            "supplied frontal sprite; these are not authored directional animation sheets.\n\n" +
            "| Id | Battle animation | Visible size | Portrait PPU |\n| --- | --- | --- | --- |\n");

        foreach (Design design in Designs)
        {
            UnitArt art = entries.FirstOrDefault(candidate => candidate != null && candidate.Id == design.Id);
            bool hasBattleArt = art != null && (art.Portrait != null || (art.IdleDirections != null && art.IdleDirections.Any(sprite => sprite != null)));
            float ppu = hasBattleArt && art.Portrait != null ? art.Portrait.pixelsPerUnit : 100f;
            string derivative = ArtRoot + design.Id + "/" + design.Id + "-portrait-rgba.png";
            PreparePortrait(SourcePath(design.Id), derivative, design.Background);
            Sprite sprite = MonsterPouchSpriteAnimationImporter.ImportGrid(derivative, ppu, 1, 1, design.Id + "-sept14")[0, 0];

            if (art == null)
            {
                art = new UnitArt { Id = design.Id };
                entries.Add(art);
            }
            art.Portrait = sprite;
            if (!hasBattleArt)
            {
                art.WorldWidth = design.Width;
                art.ReferencePixelWidth = Mathf.Max(1, sprite.rect.width - 4);
                art.FootPivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                art.IdleDirections = Enumerable.Repeat(sprite, 8).ToArray();
                art.AttackDirections = Enumerable.Repeat(sprite, 8).ToArray();
                art.WalkRigs = new UnitSpriteRig[8];
                // Empty temporal arrays intentionally allow UnitPresentation's procedural animation fallback.
                art.DirectionalAnimations = new UnitDirectionalAnimation[8];
                art.ProvisionalDirectionalProjection = false;
            }
            string note = "September 14, 2026: exact supplied design from pictures/gogos-minis; immutable source in " +
                SourcePath(design.Id) + ". " + (hasBattleArt ? "Approved combat animation and board scale preserved." :
                "Frontal sprite animated procedurally by UnitPresentation; no authored additional views are claimed.");
            if (art.SourceNotes == null || !art.SourceNotes.Contains("September 14, 2026:"))
                art.SourceNotes = string.IsNullOrEmpty(art.SourceNotes) ? note : art.SourceNotes + "\n" + note;
            report.AppendLine($"| {design.Id} | {(hasBattleArt ? "preserved" : "procedural frontal sprite")} | {sprite.rect.width} x {sprite.rect.height} | {sprite.pixelsPerUnit} |");
        }
        catalog.Units = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        ConfigureIcon("Assets/art/ui/resources/ui-moon-icon.png");
        ConfigureIcon("Assets/art/ui/resources/ui-reroll-icon.png");
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/september-character-art-import.md", report.ToString(), new UTF8Encoding(false));
        Debug.Log("September character art imported: 19 user designs plus the existing Dummy, approved animation clips preserved.");
    }

    private static string SourcePath(string id) => ArtRoot + id + "/" + id + "-source.png";

    private static void ConfigureIcon(string path)
    {
        if (!File.Exists(path)) return;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();
    }

    private static void PreparePortrait(string source, string derivative, Background background)
    {
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(derivative), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The original design must not be overwritten.");
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(source), false))
                throw new InvalidDataException("Cannot read character image: " + source);
            Color32[] pixels = texture.GetPixels32();
            if (background != Background.Alpha)
            {
                int width = texture.width, height = texture.height;
                var visited = new bool[pixels.Length];
                var queue = new Queue<int>();
                void Enqueue(int index)
                {
                    if (visited[index]) return;
                    visited[index] = true;
                    if (IsBackground(pixels[index], background)) queue.Enqueue(index);
                }
                for (int x = 0; x < width; x++) { Enqueue(x); Enqueue((height - 1) * width + x); }
                for (int y = 0; y < height; y++) { Enqueue(y * width); Enqueue(y * width + width - 1); }
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    Color32 pixel = pixels[index]; pixel.a = 0; pixels[index] = pixel;
                    int x = index % width, y = index / width;
                    if (x > 0) Enqueue(index - 1);
                    if (x < width - 1) Enqueue(index + 1);
                    if (y > 0) Enqueue(index - width);
                    if (y < height - 1) Enqueue(index + width);
                }
            }
            if (!pixels.Any(pixel => pixel.a < 128))
                throw new InvalidDataException("No transparent background after import: " + source);
            var rgba = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                rgba.SetPixels32(pixels); rgba.Apply(false, false);
                byte[] png = rgba.EncodeToPNG();
                Directory.CreateDirectory(Path.GetDirectoryName(derivative));
                if (!File.Exists(derivative) || !File.ReadAllBytes(derivative).SequenceEqual(png))
                    File.WriteAllBytes(derivative, png);
            }
            finally { UnityEngine.Object.DestroyImmediate(rgba); }
            AssetDatabase.ImportAsset(derivative, ImportAssetOptions.ForceSynchronousImport);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static bool IsBackground(Color32 pixel, Background background)
    {
        if (pixel.a == 0) return true;
        int maximum = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
        int minimum = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
        switch (background)
        {
            case Background.Black: return maximum <= 26;
            // Sepora's backdrop is a neutral dark gradient. Its saturated plum outline stays opaque.
            case Background.DarkNeutral: return maximum <= 64 && maximum - minimum <= 20;
            // White-backed designs all have a continuous dark silhouette; internal white face details stay intact.
            case Background.White: return minimum >= 218 && maximum - minimum <= 30;
            default: return false;
        }
    }
}

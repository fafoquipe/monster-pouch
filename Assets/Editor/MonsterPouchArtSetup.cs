using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Explicit, repeatable import. Does not run automatically or touch existing prototype GUIDs.</summary>
public static class MonsterPouchArtSetup
{
    private const string LocalRoot = "Assets/art/units/local";
    private const string ResourceRoot = "Assets/Resources/MonsterPouch";
    private static readonly string[] Directions = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
    private static readonly int[] AttackSheetCells = { 1, 7, 2, 5, 0, 4, 3, 6 };

    [MenuItem("Monster Pouch/Setup local unit art")]
    public static void Setup()
    {
        string sourceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../pictures/gogos-minis"));
        if (!Directory.Exists(sourceRoot))
        {
            // An already imported project is portable; the external source folder is needed only for first import.
            if (!Directory.Exists(LocalRoot)) throw new DirectoryNotFoundException("Original unit art was not found at " + sourceRoot);
        }
        EnsureFolder(LocalRoot);
        EnsureFolder(ResourceRoot);
        string catalogPath = ResourceRoot + "/UnitArt.asset";
        UnitArtCatalog catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<UnitArtCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        var result = new List<UnitArt>();
        foreach (string id in new[] { "bugaloo", "popow", "dummy", "bugui" })
        {
            UnitArt art = catalog.Get(id) ?? NewArt(id);
            // A separately installed authored Dummy atlas supersedes only this prototype fallback.
            if (id == "dummy" && art.DirectionalAnimations != null && art.DirectionalAnimations.Any(clip => clip != null && clip.Idle != null && clip.Idle.Length > 0))
            {
                result.Add(art);
                continue;
            }
            art.IdleDirections = new Sprite[8];
            art.AttackDirections = new Sprite[8];
            art.WalkRigs = new UnitSpriteRig[8];
            EnsureFolder(LocalRoot + "/" + id);
            if (id == "dummy")
            {
                string destination = LocalRoot + "/dummy/dummy-reference.png";
                CopyIfMissing("Assets/art/units/prototypes/dummy-whelp-prototype.png", destination);
                ImportIdle(destination, "dummy", 580, out Sprite pose, out UnitSpriteRig rig, out Vector2 pivot, out Sprite portrait, true);
                for (int d = 0; d < 8; d++)
                {
                    art.IdleDirections[d] = pose;
                    art.AttackDirections[d] = pose;
                    art.WalkRigs[d] = rig;
                }
                art.FootPivot = pivot;
                art.Portrait = portrait;
                art.ProvisionalDirectionalProjection = true;
                art.SourceNotes = "Original Dummy prototype silhouette retained. Shared source with articulated feet/body and provisional directional width projection; no authored eight-view Dummy sheet exists in the supplied project.";
            }
            else
            {
                for (int d = 0; d < 8; d++)
                {
                    string filename = (id == "popow" ? "" : id + "-") + Directions[d] + ".png";
                    string original = Path.Combine(sourceRoot, id, id + "-animations", id + "-idle", filename);
                    string destination = LocalRoot + "/" + id + "/" + id + "-" + Directions[d] + ".png";
                    CopyIfMissing(original, destination);
                    ImportIdle(destination, id + "-" + Directions[d], 150, out Sprite pose, out UnitSpriteRig rig, out Vector2 pivot, out Sprite portrait, d == 4);
                    art.IdleDirections[d] = pose;
                    art.WalkRigs[d] = rig;
                    if (d == 4)
                    {
                        art.FootPivot = pivot;
                        art.Portrait = portrait;
                    }
                }
                if (id == "popow" || id == "bugaloo")
                {
                    string destination = LocalRoot + "/" + id + "/" + id + "-attack-directions.png";
                    CopyIfMissing(Path.Combine(sourceRoot, id, id + "-atack.png"), destination);
                    art.AttackDirections = ImportAttacks(destination, id, id == "popow" ? 260 : 336);
                }
                else Array.Copy(art.IdleDirections, art.AttackDirections, 8);
                art.SourceNotes = "Eight named 256 x 256 source directions from pictures/gogos-minis. PNG bytes preserved. Idle breathing and articulated movement are provisional. " +
                    (id == "bugui" ? "Attack uses the body/feet rig pending authored attack poses." : "One separate authored attack view per direction is combined with anticipation/contact/recovery; the 4 x 2 sheet is not played as eight temporal frames.");
            }
            result.Add(art);
        }
        // Additional catalog entries (e.g. Atori) are installed by their own explicit art setup.
        if (catalog.Units != null)
            result.AddRange(catalog.Units.Where(existing => existing != null && !result.Any(entry => string.Equals(entry.Id, existing.Id, StringComparison.OrdinalIgnoreCase))));
        catalog.Units = result.ToArray();
        EditorUtility.SetDirty(catalog);
        EnsureSpriteMaterial();
        AssetDatabase.SaveAssets();
        WriteImportReport(catalog);
        Debug.Log("Monster Pouch: local unit art configured. Original textures and their GUIDs were preserved.");
    }

    private static UnitArt NewArt(string id)
    {
        return new UnitArt
        {
            Id = id,
            WorldWidth = id == "dummy" ? .78f : id == "bugui" ? .72f : 1.05f,
            ReferencePixelWidth = id == "dummy" ? 580 : id == "bugui" ? 91 : id == "popow" ? 154 : 142
        };
    }

    private static void ImportIdle(string path, string prefix, float ppu, out Sprite pose, out UnitSpriteRig rig, out Vector2 pivot,
        out Sprite portrait, bool includePortrait)
    {
        Texture2D texture = ReadSource(path);
        try
        {
            RectInt full = new RectInt(0, 0, texture.width, texture.height);
            RectInt visible = GetAlphaBounds(texture, full);
            Vector2 anchor = FindFootAnchor(texture, visible);
            pivot = new Vector2(anchor.x / texture.width, anchor.y / texture.height);
            // Padding stays consistent in the full pose; articulated regions have an explicit common origin.
            int cutY = visible.yMin + Mathf.Max(2, Mathf.RoundToInt(visible.height * .2f));
            int midX = Mathf.RoundToInt(anchor.x);
            Rect bodyRect = new Rect(visible.xMin, cutY - 1, visible.width, visible.yMax - cutY + 1);
            Rect leftRect = new Rect(visible.xMin, visible.yMin, Mathf.Max(1, midX - visible.xMin), cutY - visible.yMin + 1);
            Rect rightRect = new Rect(midX, visible.yMin, Mathf.Max(1, visible.xMax - midX), cutY - visible.yMin + 1);
            var rects = new List<SpriteRect>
            {
                NewRect(prefix + "-pose", new Rect(0, 0, texture.width, texture.height), pivot),
                NewRect(prefix + "-body", bodyRect, new Vector2(.5f, 0)),
                NewRect(prefix + "-left-foot", leftRect, new Vector2(.5f, 1)),
                NewRect(prefix + "-right-foot", rightRect, new Vector2(.5f, 1))
            };
            if (includePortrait)
            {
                // UI-only crop. Keep the complete gameplay pose canvas, its foot pivot, and every rig region unchanged.
                // A small border retains outline antialiasing while discarding the large transparent source margins.
                int padding = Mathf.Max(2, Mathf.RoundToInt(visible.height * .005f));
                int left = Mathf.Max(0, visible.xMin - padding);
                int bottom = Mathf.Max(0, visible.yMin - padding);
                int right = Mathf.Min(texture.width, visible.xMax + padding);
                int top = Mathf.Min(texture.height, visible.yMax + padding);
                rects.Add(NewRect(prefix + "-portrait", new Rect(left, bottom, right - left, top - bottom), Vector2.one * .5f));
            }
            Dictionary<string, Sprite> sprites = ApplySlices(path, ppu, rects);
            pose = sprites[prefix + "-pose"];
            portrait = includePortrait ? sprites[prefix + "-portrait"] : pose;
            rig = new UnitSpriteRig
            {
                Body = sprites[prefix + "-body"], LeftFoot = sprites[prefix + "-left-foot"], RightFoot = sprites[prefix + "-right-foot"],
                BodyOffset = (new Vector2(bodyRect.center.x, bodyRect.yMin) - anchor) / ppu,
                LeftFootOffset = (new Vector2(leftRect.center.x, leftRect.yMax) - anchor) / ppu,
                RightFootOffset = (new Vector2(rightRect.center.x, rightRect.yMax) - anchor) / ppu
            };
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Sprite[] ImportAttacks(string path, string id, float ppu)
    {
        Texture2D texture = ReadSource(path);
        try
        {
            if (texture.width != 1448 || texture.height != 1086)
                throw new InvalidDataException("Expected the verified 1448 x 1086 directional sheet: " + path);
            var rects = new List<SpriteRect>();
            for (int d = 0; d < 8; d++)
            {
                int cell = AttackSheetCells[d];
                RectInt region = new RectInt(cell % 4 * 362, (1 - cell / 4) * 543, 362, 543);
                RectInt visible = GetAlphaBounds(texture, region);
                Vector2 foot = FindFootAnchor(texture, visible);
                Vector2 pivot = new Vector2((foot.x - region.x) / region.width, (foot.y - region.y) / region.height);
                rects.Add(NewRect(id + "-attack-" + Directions[d], new Rect(region.x, region.y, region.width, region.height), pivot));
            }
            Dictionary<string, Sprite> sprites = ApplySlices(path, ppu, rects);
            return Directions.Select(direction => sprites[id + "-attack-" + direction]).ToArray();
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Dictionary<string, Sprite> ApplySlices(string path, float ppu, List<SpriteRect> requested)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.isReadable = false;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        textureSettings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect);
        foreach (SpriteRect rect in requested)
        {
            if (previous.TryGetValue(rect.name, out SpriteRect old)) rect.spriteID = old.spriteID;
            else rect.spriteID = GUID.Generate();
        }
        provider.SetSpriteRects(requested.ToArray());
        ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null) names.SetNameFileIdPairs(requested.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name, sprite => sprite);
    }

    private static SpriteRect NewRect(string name, Rect rect, Vector2 pivot)
    {
        return new SpriteRect { name = name, rect = rect, pivot = pivot, alignment = SpriteAlignment.Custom };
    }

    private static Texture2D ReadSource(string path)
    {
        // Decode solely to inspect alpha and choose sprite rectangles. No pixel is edited or re-encoded.
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidDataException("Could not decode unit PNG: " + path);
        }
        return texture;
    }

    private static RectInt GetAlphaBounds(Texture2D texture, RectInt region)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = region.xMax, minY = region.yMax, maxX = region.xMin, maxY = region.yMin;
        bool found = false;
        for (int y = region.yMin; y < region.yMax; y++)
        for (int x = region.xMin; x < region.xMax; x++)
        {
            if (pixels[y * texture.width + x].a <= 128) continue;
            minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x + 1); maxY = Mathf.Max(maxY, y + 1);
            found = true;
        }
        if (!found) throw new InvalidDataException("A source direction is empty.");
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    private static Vector2 FindFootAnchor(Texture2D texture, RectInt visible)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = visible.xMax, maxX = visible.xMin;
        // Include both feet in staggered perspectives; a one-pixel band can anchor to only the lower foot.
        int band = Mathf.Max(3, Mathf.RoundToInt(visible.height * .14f));
        for (int y = visible.yMin; y < Mathf.Min(visible.yMax, visible.yMin + band); y++)
        for (int x = visible.xMin; x < visible.xMax; x++)
        {
            if (pixels[y * texture.width + x].a <= 128) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x + 1);
        }
        return new Vector2((minX + maxX) * .5f, visible.yMin + 1);
    }

    private static void CopyIfMissing(string source, string destination)
    {
        if (File.Exists(destination)) return;
        if (!File.Exists(source)) throw new FileNotFoundException("Missing original unit art", source);
        File.Copy(source, destination, false);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void EnsureSpriteMaterial()
    {
        string path = ResourceRoot + "/UnitSpriteMaterial.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Sprites/Default shader is unavailable.");
        AssetDatabase.CreateAsset(new Material(shader) { name = "Monster Pouch unlit sprites" }, path);
    }

    private static void WriteImportReport(UnitArtCatalog catalog)
    {
        var report = new StringBuilder("# Imported unit sprite references\n\nGenerated by MonsterPouchArtSetup.Setup after a successful import. This records assets, not a claim of visual gameplay verification.\n\n");
        report.AppendLine("| Unit | Direction | State | Sprite | Asset | Rect (pixels) | Foot pivot in sprite pixels | PPU |");
        report.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (UnitArt art in catalog.Units)
        for (int d = 0; d < 8; d++)
        {
            foreach (bool attack in new[] { false, true })
            {
                Sprite sprite = attack ? art.AttackDirections[d] : art.IdleDirections[d];
                if (sprite == null) continue;
                report.AppendLine($"| {art.Id} | {Directions[d]} | {(attack ? "Attack contact" : "Idle / Move rig reference")} | {sprite.name} | {AssetDatabase.GetAssetPath(sprite)} | {sprite.rect} | {sprite.pivot} | {sprite.pixelsPerUnit} |");
            }
        }
        report.AppendLine("\nOriginal profiles use runtime articulation. Profiles with DirectionalAnimations use authored temporal sprite sequences; separately installed Dummy/Atori art is preserved. Details: local-unit-art.md and dummy-animation-art.md.");
        report.AppendLine("\n## UI-only portraits\n\nPortraits use an alpha-bounds crop with a small outline margin. Gameplay pose rectangles, feet pivots, and rig references retain their original dimensions. PNG bytes are unchanged.\n");
        report.AppendLine("| Unit | Portrait subasset | Pixel rectangle | Source |\n| --- | --- | --- | --- |");
        foreach (UnitArt art in catalog.Units)
            if (art.Portrait != null)
                report.AppendLine($"| {art.Id} | {art.Portrait.name} | {art.Portrait.rect} | {AssetDatabase.GetAssetPath(art.Portrait)} |");
        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/unit-art-import-report.md", report.ToString(), new UTF8Encoding(false));
    }
}

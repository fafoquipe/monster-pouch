using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Technical atlas import: retains generated source bytes, alpha-keys a new derivative and slices using Unity APIs.</summary>
public static class MonsterPouchSpriteAnimationImporter
{
    public static string PrepareChromaSource(string sourcePath, string rgbaPath, Color32 key, byte tolerance = 48)
    {
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(rgbaPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The RGBA derivative must have a new path; the generated source is immutable.");
        Texture2D texture = ReadSource(sourcePath);
        try
        {
            Color32[] pixels = texture.GetPixels32();
            int removed = 0;
            foreach (Color32 pixel in pixels)
                if (pixel.a < 128) removed++;
            bool isMagenta = key.r > 240 && key.g < 16 && key.b > 240;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                bool close = Mathf.Abs(p.r - key.r) <= tolerance && Mathf.Abs(p.g - key.g) <= tolerance && Mathf.Abs(p.b - key.b) <= tolerance;
                // Magenta contamination at a dark outline still has a distinctive R+B excess.
                // Require nearly equal R/B so warm wood/brown pixels remain intact for UI atlases.
                bool fringe = isMagenta && Mathf.Abs(p.r - p.b) <= 12 && Mathf.Min(p.r, p.b) - p.g > Mathf.Max(16, tolerance / 2);
                if (!close && !fringe) continue;
                pixels[i] = new Color32(p.r, p.g, p.b, 0);
                removed++;
            }
            if (removed == 0) throw new InvalidDataException("No transparent or chroma-key pixels found in " + sourcePath);
            // LoadImage can change an RGB source texture to RGB24, so encode through a new explicit RGBA32 texture.
            byte[] encoded;
            var rgba = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                rgba.SetPixels32(pixels);
                rgba.Apply(false, false);
                encoded = rgba.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(rgba); }
            Directory.CreateDirectory(Path.GetDirectoryName(rgbaPath));
            if (!File.Exists(rgbaPath) || !File.ReadAllBytes(rgbaPath).SequenceEqual(encoded)) File.WriteAllBytes(rgbaPath, encoded);
            AssetDatabase.ImportAsset(rgbaPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Sprite alpha derivative: {rgbaPath}; source preserved: {sourcePath}; transparent/keyed pixels: {removed}.");
            return rgbaPath;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    /// <summary>Returns [top-to-bottom row, left-to-right column]. Dimensions need not divide evenly.</summary>
    public static Sprite[,] ImportGrid(string path, float ppu, int columns, int rows, string prefix)
    {
        if (columns < 1 || rows < 1 || ppu <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        Texture2D texture = ReadSource(path);
        try
        {
            Color32[] pixels = texture.GetPixels32();
            if (!pixels.Any(p => p.a < 128)) throw new InvalidDataException("Atlas must contain actual transparent alpha: " + path);
            // Generated atlases may have unequal outer margins. Real transparent gutters, not canvas division,
            // determine the cell boundaries when their projected count matches the requested layout.
            int[] columnEdges = FindGridEdges(pixels, texture.width, texture.height, columns, true);
            int[] rowEdges = FindGridEdges(pixels, texture.width, texture.height, rows, false);
            var rects = new List<SpriteRect>();
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                int x0 = columnEdges[col];
                int x1 = columnEdges[col + 1];
                int y0 = rowEdges[rows - row - 1];
                int y1 = rowEdges[rows - row];
                RectInt cell = new RectInt(x0, y0, x1 - x0, y1 - y0);
                RectInt visible = AlphaBounds(pixels, texture.width, cell);
                Vector2 foot = FootAnchor(pixels, texture.width, visible);
                int padding = 2;
                Rect rect = Rect.MinMaxRect(Mathf.Max(x0, visible.xMin - padding), Mathf.Max(y0, visible.yMin - padding),
                    Mathf.Min(x1, visible.xMax + padding), Mathf.Min(y1, visible.yMax + padding));
                Vector2 pivot = new Vector2((foot.x - rect.xMin) / rect.width, (foot.y - rect.yMin) / rect.height);
                rects.Add(new SpriteRect { name = FrameName(prefix, row, col), rect = rect, pivot = pivot, alignment = SpriteAlignment.Custom });
            }
            Dictionary<string, Sprite> sprites = ApplySlices(path, ppu, rects);
            var result = new Sprite[rows, columns];
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++) result[row, col] = sprites[FrameName(prefix, row, col)];
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    public static string FrameName(string prefix, int row, int col) => $"{prefix}-r{row:D2}-f{col:D2}";

    private static int[] FindGridEdges(Color32[] pixels, int width, int height, int count, bool horizontal)
    {
        int length = horizontal ? width : height;
        int perpendicular = horizontal ? height : width;
        var occupancy = new int[length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (pixels[y * width + x].a > 128) occupancy[horizontal ? x : y]++;
        // Ignore isolated chroma-edge specks for finding gutters only; source/sprite pixels are not erased.
        int minimumCoverage = Mathf.Max(2, Mathf.RoundToInt(perpendicular * .004f));
        int maximumInternalGap = Mathf.Max(2, length / (count * 30));
        var starts = new List<int>();
        var ends = new List<int>();
        int start = -1, last = -1;
        for (int i = 0; i < length; i++)
        {
            if (occupancy[i] < minimumCoverage) continue;
            if (start < 0) start = i;
            else if (i - last > maximumInternalGap + 1)
            {
                starts.Add(start); ends.Add(last + 1); start = i;
            }
            last = i;
        }
        if (start >= 0) { starts.Add(start); ends.Add(last + 1); }
        var result = new int[count + 1];
        result[count] = length;
        if (starts.Count == count)
        {
            for (int i = 1; i < count; i++) result[i] = (ends[i - 1] + starts[i]) / 2;
        }
        else
        {
            Debug.LogWarning($"Atlas {(horizontal ? "columns" : "rows")}: expected {count} alpha bands, found {starts.Count}; using equal cells. Inspect the slice report before approval.");
            for (int i = 1; i < count; i++) result[i] = Mathf.RoundToInt(i * length / (float)count);
        }
        return result;
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
        importer.maxTextureSize = 4096;
        importer.isReadable = false;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect);
        foreach (SpriteRect rect in requested)
            rect.spriteID = previous.TryGetValue(rect.name, out SpriteRect old) ? old.spriteID : GUID.Generate();
        // An additional portrait or manually curated slice retains its existing subasset identity.
        var merged = requested.Concat(previous.Values.Where(rect => requested.All(item => item.name != rect.name))).ToArray();
        provider.SetSpriteRects(merged);
        ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null) names.SetNameFileIdPairs(merged.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name, sprite => sprite);
    }

    private static Texture2D ReadSource(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false)) return texture;
        UnityEngine.Object.DestroyImmediate(texture);
        throw new InvalidDataException("Could not decode sprite atlas: " + path);
    }

    private static RectInt AlphaBounds(Color32[] pixels, int width, RectInt region)
    {
        int x0 = region.xMax, x1 = region.xMin, y0 = region.yMax, y1 = region.yMin;
        for (int y = region.yMin; y < region.yMax; y++)
        for (int x = region.xMin; x < region.xMax; x++)
        {
            if (pixels[y * width + x].a <= 128) continue;
            x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x + 1);
            y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y + 1);
        }
        if (x1 <= x0 || y1 <= y0) throw new InvalidDataException("Empty animation cell " + region);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    private static Vector2 FootAnchor(Color32[] pixels, int width, RectInt bounds)
    {
        int x0 = bounds.xMax, x1 = bounds.xMin;
        int band = Mathf.Max(3, Mathf.RoundToInt(bounds.height * .14f));
        for (int y = bounds.yMin; y < Mathf.Min(bounds.yMax, bounds.yMin + band); y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            if (pixels[y * width + x].a <= 128) continue;
            x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x + 1);
        }
        return new Vector2((x0 + x1) * .5f, bounds.yMin + 1);
    }
}

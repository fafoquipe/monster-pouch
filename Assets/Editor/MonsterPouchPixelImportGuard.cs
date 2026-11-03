using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Removes residual key colour from imported texture memory only. PNGs, sprite
/// rectangles, pivots and GUIDs remain the authored assets. Original-alpha art is
/// deliberately absent from the chroma allowlist, including purple projectiles.
/// </summary>
public sealed class MonsterPouchPixelImportGuard : AssetPostprocessor
{
    sealed class KeyedAsset
    {
        public readonly string Path, Source;
        public readonly bool BrownBrief;
        public KeyedAsset(string path, string source, bool brownBrief = false)
        { Path = path; Source = source; BrownBrief = brownBrief; }
    }

    static readonly KeyedAsset[] Keyed =
    {
        new KeyedAsset("Assets/art/ui/brief/brief-closed.png", "Assets/art/ui/brief/brief-closed-source.png", true),
        new KeyedAsset("Assets/art/units/generated/anuik-approved/anuik-animation-rgba-v2.png", "Assets/art/units/generated/anuik-approved/anuik-animation-source-v2.png", true),
        new KeyedAsset("Assets/art/units/generated/anuik-approved/anuik-revive-rgba-v1.png", "Assets/art/units/generated/anuik-approved/anuik-revive-source-v1.png", true),
        new KeyedAsset("Assets/art/units/generated/atori/atori-animation-rgba.png", "Assets/art/units/generated/atori/atori-animation-source.png", true),
        new KeyedAsset("Assets/art/units/generated/bugui/bugui-animation-rgba-v2.png", "Assets/art/units/generated/bugui/bugui-animation-source-v2.png"),
        new KeyedAsset("Assets/art/units/generated/kayon-approved/kayon-animation-rgba-v1.png", "Assets/art/units/generated/kayon-approved/kayon-animation-source-v1.png"),
        new KeyedAsset("Assets/art/units/generated/kayon-approved/kayon-portrait-rgba-v1.png", "Assets/art/units/generated/kayon-approved/kayon-portrait-source-v1.png", true),
        new KeyedAsset("Assets/art/units/generated/stein-approved/stein-animation-rgba-v1.png", "Assets/art/units/generated/stein-approved/stein-animation-source-v1.png"),
        new KeyedAsset("Assets/art/units/generated/stein-approved/stein-portrait-rgba-v1.png", "Assets/art/units/generated/stein-approved/stein-portrait-source-v1.png", true),
        new KeyedAsset("Assets/art/units/generated/tauris-approved/tauris-animation-rgba-v3.png", "Assets/art/units/generated/tauris-approved/tauris-animation-source-v3.png", true),
        new KeyedAsset("Assets/art/units/local/dummy/dummy-animation-v1.png", "Assets/art/units/local/dummy/dummy-animation-source-v1.png", true)
    };

    // Actual exceptions found in the runtime dependency audit. Other runtime
    // textures already use these settings and do not need another import.
    static readonly string[] SettingsExceptions =
    {
        "Assets/art/units/prototypes/bugaloo-hero-prototype.png",
        "Assets/art/units/prototypes/bugui-whelp-prototype.png",
        "Assets/art/units/prototypes/dummy-whelp-prototype.png",
        "Assets/art/units/prototypes/popow-hero-prototype.png",
        "Assets/Resources/MonsterPouch/UI/home-background.png",
        "Assets/Resources/MonsterPouch/UI/MenuItems/batalla.png",
        "Assets/Resources/MonsterPouch/UI/MenuItems/brief.png",
        "Assets/Resources/MonsterPouch/UI/MenuItems/figuras.png",
        "Assets/Resources/MonsterPouch/UI/MenuItems/guia.png",
        "Assets/Resources/MonsterPouch/UI/MenuItems/sonido.png"
    };
    static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps", "tvOS" };
    static readonly Dictionary<string, CleanupStats> LastImported = new Dictionary<string, CleanupStats>(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    public sealed class CleanupStats
    {
        public int Changed, Unmixed, Removed, PreservedPurple, Candidates;
    }

    [Serializable]
    public sealed class ImportAuditReport
    {
        public int ImportedAssets, ChangedPixels, VerifiedPngFiles;
        public string Report, ReportPath;
    }

    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!IsTarget(assetPath)) return;
        var importer = (TextureImporter)assetImporter;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        int required = SourceSize(assetPath);
        importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, required);
        foreach (string platform in Platforms)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            if (!settings.overridden) continue;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            settings.format = TextureImporterFormat.RGBA32;
            settings.maxTextureSize = Mathf.Max(settings.maxTextureSize, required);
            importer.SetPlatformTextureSettings(settings);
        }
    }

    void OnPostprocessTexture(Texture2D texture)
    {
        KeyedAsset entry = Keyed.FirstOrDefault(item => string.Equals(item.Path, assetPath, StringComparison.OrdinalIgnoreCase));
        if (entry == null || !HasKeySource(entry, texture.width, texture.height)) return;
        Color32[] pixels = texture.GetPixels32();
        CleanupStats stats = CleanFringe(pixels, texture.width, texture.height, entry.BrownBrief, EffectPurpleOnly(entry), CyanEffects(entry));
        if (stats.Changed > 0)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }
        LastImported[assetPath] = stats;
    }

    static bool IsTarget(string path) => Keyed.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)) ||
        SettingsExceptions.Any(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
    static bool EffectPurpleOnly(KeyedAsset entry) => entry.Path.Contains("/stein-approved/stein-animation-") || entry.Path.Contains("/kayon-approved/kayon-animation-");
    static bool CyanEffects(KeyedAsset entry) => entry.Path.Contains("/stein-approved/stein-animation-");

    static int SourceSize(string path)
    {
        using (var input = File.OpenRead(path))
        {
            var header = new byte[24];
            if (input.Read(header, 0, header.Length) != header.Length) return 2048;
            int width = BigEndian(header, 16), height = BigEndian(header, 20);
            return Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(width, height)), 32, 16384);
        }
    }
    static int BigEndian(byte[] data, int i) => data[i] << 24 | data[i + 1] << 16 | data[i + 2] << 8 | data[i + 3];

    static bool HasKeySource(KeyedAsset entry, int width, int height)
    {
        if (!File.Exists(entry.Source)) return false;
        var source = ReadPng(entry.Source);
        try
        {
            if (source.width != width || source.height != height) return false;
            Color32[] pixels = source.GetPixels32();
            int clear = 0, key = 0;
            foreach (Color32 p in pixels)
            {
                if (p.a < 128) clear++;
                if (p.r >= 207 && p.b >= 207 && p.g <= 48) key++;
            }
            // Sources already carrying transparency must never enter a colour-key
            // pass. The matching opaque magenta source proves this derivative's origin.
            return clear == 0 && key > pixels.Length / 20;
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }

    /// <summary>
    /// Pure pixel pass. The historical brownBriefOnly argument also enables the
    /// audited no-violet palette for Anuik, Atori, Tauris, Dummy and two portraits.
    /// Mixed artwork keeps regional glyph/lightning/purple-paint protection.
    /// </summary>
    public static CleanupStats CleanFringe(Color32[] pixels, int width, int height, bool brownBriefOnly = false,
        bool effectsOnlyPalette = false, bool preserveCyanEffects = true)
    {
        if (pixels == null || width <= 0 || height <= 0 || pixels.Length != width * height)
            throw new ArgumentException("Pixel dimensions do not match the texture.");
        var stats = new CleanupStats();
        var original = (Color32[])pixels.Clone();
        byte[] distance = ExteriorDistance(original, width, height);
        byte[] regions = brownBriefOnly ? null : RegionalProtection(original, distance, width, height);
        bool noVioletBody = brownBriefOnly || effectsOnlyPalette;
        int band = noVioletBody ? 4 : 1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            Color32 p = original[index];
            if (p.a == 0 || distance[index] == 0 || distance[index] > band || !Magenta(p, brownBriefOnly ? 16 : 24)) continue;
            stats.Candidates++;
            if (!brownBriefOnly)
            {
                // Tiny disconnected keyed specks next to a substantial body are
                // matte, not body paint. Distant glyph strokes remain independent.
                bool cyan = preserveCyanEffects && HasCyanEffect(original, width, height, x, y);
                if (regions[index] == 2 && !cyan)
                {
                    pixels[index] = new Color32(0, 0, 0, 0); stats.Removed++; stats.Changed++; continue;
                }
                if (regions[index] == 1 || cyan || (!effectsOnlyPalette && HasMatchingInterior(original, distance, width, height, x, y, p)))
                { stats.PreservedPurple++; continue; }
            }
            if (FindInterior(original, distance, width, height, x, y, p, noVioletBody ? 6 : 4,
                noVioletBody, out Color32 foreground, out float keyAmount, out float residual) && residual <= 20)
            {
                // Undo the key mixture, retaining the estimated foreground coverage.
                // Replacing the contaminated RGB also prevents future filtered bleed.
                byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(p.a * (1 - keyAmount)), 0, 255);
                pixels[index] = new Color32(foreground.r, foreground.g, foreground.b, alpha < 8 ? (byte)0 : alpha);
                stats.Unmixed++;
                stats.Changed++;
            }
            else if (noVioletBody || (!HasPurpleInterior(original, distance, width, height, x, y) &&
                HasNeutralCore(original, distance, width, height, x, y)))
            {
                // The no-violet palettes have no authored purple at this silhouette.
                // In mixed sheets, only a locally non-purple body can use this fallback.
                pixels[index] = new Color32(0, 0, 0, 0);
                stats.Removed++;
                stats.Changed++;
            }
        }
        return stats;
    }

    static bool Magenta(Color32 p, int excess) => Mathf.Min(p.r, p.b) - p.g > excess && Mathf.Abs(p.r - p.b) <= 100;

    // 1 protects a detached thin glyph; 2 identifies a tiny matte speck near a body.
    static byte[] RegionalProtection(Color32[] pixels, byte[] distance, int width, int height)
    {
        var result = new byte[pixels.Length];
        var seen = new bool[pixels.Length];
        var queue = new int[pixels.Length];
        for (int seed = 0; seed < pixels.Length; seed++)
        {
            if (seen[seed] || pixels[seed].a == 0) continue;
            int head = 0, tail = 1; queue[0] = seed; seen[seed] = true;
            bool core = false;
            while (head < tail)
            {
                int p = queue[head++], x = p % width, y = p / width;
                if (distance[p] >= 3) core = true;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int qx = x + dx, qy = y + dy;
                    if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
                    int q = qy * width + qx;
                    if (!seen[q] && pixels[q].a > 0) { seen[q] = true; queue[tail++] = q; }
                }
            }
            if (core) continue;
            bool nearCore = false;
            int radius = tail <= 5 ? 5 : 2;
            for (int i = 0; i < tail && !nearCore; i++)
            {
                int x = queue[i] % width, y = queue[i] / width;
                for (int dy = -radius; dy <= radius && !nearCore; dy++) for (int dx = -radius; dx <= radius; dx++)
                {
                    int qx = x + dx, qy = y + dy;
                    if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
                    int q = qy * width + qx;
                    if (pixels[q].a >= 192 && distance[q] >= 3) { nearCore = true; break; }
                }
            }
            byte protection = tail <= 5 && nearCore ? (byte)2 : tail >= 6 && !nearCore ? (byte)1 : (byte)0;
            if (protection != 0) for (int i = 0; i < tail; i++) result[queue[i]] = protection;
        }
        return result;
    }

    static bool HasMatchingInterior(Color32[] pixels, byte[] distance, int width, int height, int x, int y, Color32 color)
    {
        int support = 0;
        for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
        {
            int qx = x + dx, qy = y + dy;
            if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
            int q = qy * width + qx; Color32 p = pixels[q];
            if (distance[q] >= 2 && p.a >= 192 && Mathf.Abs(p.r - color.r) <= 18 &&
                Mathf.Abs(p.g - color.g) <= 18 && Mathf.Abs(p.b - color.b) <= 18 && ++support >= 2) return true;
        }
        return false;
    }

    static bool HasCyanEffect(Color32[] pixels, int width, int height, int x, int y)
    {
        for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
        {
            int qx = x + dx, qy = y + dy;
            if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
            Color32 p = pixels[qy * width + qx];
            if (p.a >= 128 && p.g >= 115 && p.b >= 150 && p.b - p.r > 40) return true;
        }
        return false;
    }

    static bool HasNeutralCore(Color32[] pixels, byte[] distance, int width, int height, int x, int y)
    {
        int count = 0;
        for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
        {
            int qx = x + dx, qy = y + dy;
            if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
            int q = qy * width + qx;
            if (distance[q] >= 2 && pixels[q].a >= 192 && !Magenta(pixels[q], 16) && ++count >= 3) return true;
        }
        return false;
    }

    static byte[] ExteriorDistance(Color32[] pixels, int width, int height)
    {
        var distance = new byte[pixels.Length];
        for (int i = 0; i < distance.Length; i++) distance[i] = 255;
        var queue = new int[pixels.Length];
        int head = 0, tail = 0;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int p = y * width + x;
            if (pixels[p].a == 0 && (x == 0 || y == 0 || x == width - 1 || y == height - 1))
            { distance[p] = 0; queue[tail++] = p; }
        }
        while (head < tail)
        {
            int p = queue[head++], x = p % width, y = p / width;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int qx = x + dx, qy = y + dy;
                if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
                int q = qy * width + qx;
                if (distance[q] == 255 && pixels[q].a == 0) { distance[q] = 0; queue[tail++] = q; }
            }
        }
        // A second bounded breadth-first pass records how far a visible pixel is
        // from exterior alpha; enclosed transparent decoration cannot seed it.
        head = 0;
        while (head < tail)
        {
            int p = queue[head++];
            if (distance[p] >= 4) continue;
            int x = p % width, y = p / width;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int qx = x + dx, qy = y + dy;
                if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
                int q = qy * width + qx;
                if (distance[q] != 255) continue;
                distance[q] = (byte)(distance[p] + 1);
                queue[tail++] = q;
            }
        }
        return distance;
    }

    static bool HasPurpleInterior(Color32[] pixels, byte[] distance, int width, int height, int x, int y)
    {
        int support = 0;
        for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
        {
            int qx = x + dx, qy = y + dy;
            if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
            int q = qy * width + qx;
            if (distance[q] >= 3 && pixels[q].a >= 192 && Magenta(pixels[q], 24) && ++support >= 3) return true;
        }
        return false;
    }

    static bool FindInterior(Color32[] pixels, byte[] distance, int width, int height, int x, int y, Color32 pixel, int radius,
        bool noVioletPalette, out Color32 foreground, out float keyAmount, out float residual)
    {
        foreground = new Color32(); keyAmount = 0; residual = float.PositiveInfinity;
        float bestScore = float.PositiveInfinity;
        for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
        {
            int qx = x + dx, qy = y + dy;
            if (qx < 0 || qy < 0 || qx >= width || qy >= height) continue;
            int q = qy * width + qx;
            Color32 candidate = pixels[q];
            if (distance[q] < 2 || candidate.a < 250 || (noVioletPalette && Mathf.Min(candidate.r, candidate.b) - candidate.g > 16)) continue;
            // Adding a zero-green key cannot increase green. This rejects ordinary
            // purple shading which merely happens to lie near the fitted RGB line.
            if (pixel.g > candidate.g + 5) continue;
            var direction = new Vector3(255 - candidate.r, -candidate.g, 255 - candidate.b);
            if (direction.sqrMagnitude < 1) continue;
            var delta = new Vector3(pixel.r - candidate.r, pixel.g - candidate.g, pixel.b - candidate.b);
            float amount = Vector3.Dot(delta, direction) / direction.sqrMagnitude;
            if (amount < .07f || amount > .98f) continue;
            float error = (delta - direction * amount).magnitude / Mathf.Sqrt(3);
            float score = error + .04f * Mathf.Sqrt(dx * dx + dy * dy);
            if (score >= bestScore) continue;
            bestScore = score; foreground = candidate; keyAmount = amount; residual = error;
        }
        return !float.IsPositiveInfinity(residual);
    }

    public static ImportAuditReport Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before applying texture imports.");
        string[] targets = Keyed.Select(item => item.Path).Concat(SettingsExceptions).Distinct().Where(File.Exists).ToArray();
        string[] pngs = targets.Concat(Keyed.Select(item => item.Source)).Where(File.Exists).Distinct().ToArray();
        var hashes = pngs.ToDictionary(path => path, HashFile);
        int imported = 0;
        LastImported.Clear();
        foreach (string path in targets)
        {
            // Every chroma derivative must rebuild its import cache once. Settings-
            // only textures are skipped after their first successful normalization.
            if (!Keyed.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)) && SettingsCorrect(path)) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            imported++;
        }
        foreach (var pair in hashes)
            if (HashFile(pair.Key) != pair.Value) throw new InvalidDataException("PNG bytes changed unexpectedly: " + pair.Key);
        ImportAuditReport report = Audit();
        report.ImportedAssets = imported;
        report.VerifiedPngFiles = hashes.Count;
        report.ChangedPixels = LastImported.Values.Sum(stats => stats.Changed);
        report.ReportPath = "Library/MonsterPouch/pixel-import-audit.md";
        report.Report = "Imported assets: " + imported + "; changed imported pixels: " + report.ChangedPixels +
            "; PNG hashes unchanged: " + hashes.Count + ".\n\n" + report.Report;
        Directory.CreateDirectory(Path.GetDirectoryName(report.ReportPath));
        File.WriteAllText(report.ReportPath, report.Report, new UTF8Encoding(false));
        Debug.Log(report.Report);
        return report;
    }

    public static ImportAuditReport Audit()
    {
        var report = new StringBuilder("# Imported pixel edge audit\n\n");
        report.AppendLine("Source preview is computed from unchanged PNG bytes. Imported readback inspects the actual texture cache, including non-readable textures. Candidate counts are not a licence to erase authored purple.\n");
        report.AppendLine("| Runtime texture | Verified key source | Preview changed | Purple protected | Imported exterior magenta | Pixel settings |\n|---|---|---:|---:|---:|---|");
        foreach (KeyedAsset entry in Keyed)
        {
            if (!File.Exists(entry.Path)) continue;
            var raw = ReadPng(entry.Path);
            try
            {
                bool keyed = HasKeySource(entry, raw.width, raw.height);
                CleanupStats preview = keyed ? CleanFringe(raw.GetPixels32(), raw.width, raw.height, entry.BrownBrief, EffectPurpleOnly(entry), CyanEffects(entry)) : new CleanupStats();
                Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Path);
                string edgeCount = "unavailable";
                if (imported != null)
                {
                    Color32[] readback = ReadImportedPixels(imported);
                    if (readback != null) edgeCount = CountExteriorMagenta(readback, imported.width, imported.height).ToString();
                }
                report.AppendLine("| `" + entry.Path + "` | " + keyed + " | " + preview.Changed + " | " + preview.PreservedPurple + " | " + edgeCount + " | " + SettingsCorrect(entry.Path) + " |");
            }
            finally { UnityEngine.Object.DestroyImmediate(raw); }
        }
        report.AppendLine("\nSettings-only assets:");
        foreach (string path in SettingsExceptions.Where(File.Exists)) report.AppendLine("- `" + path + "`: " + SettingsCorrect(path));
        return new ImportAuditReport { Report = report.ToString() };
    }

    static int CountExteriorMagenta(Color32[] pixels, int width, int height)
    {
        byte[] distance = ExteriorDistance(pixels, width, height);
        int count = 0;
        for (int i = 0; i < pixels.Length; i++) if (distance[i] == 1 && pixels[i].a > 0 && Magenta(pixels[i], 24)) count++;
        return count;
    }

    static bool SettingsCorrect(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression ||
            importer.npotScale != TextureImporterNPOTScale.None || importer.maxTextureSize < SourceSize(path)) return false;
        foreach (string platform in Platforms)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            if (settings.overridden && (settings.textureCompression != TextureImporterCompression.Uncompressed ||
                settings.crunchedCompression || settings.maxTextureSize < SourceSize(path))) return false;
        }
        return true;
    }

    static Color32[] ReadImportedPixels(Texture2D texture)
    {
        if (texture.isReadable) return texture.GetPixels32();
        if (SystemInfo.supportsAsyncGPUReadback)
        {
            var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32);
            request.WaitForCompletion();
            if (!request.hasError) return request.GetData<Color32>().ToArray();
        }
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, temporary);
            RenderTexture.active = temporary;
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply(false, false);
            return copy.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }

    static Texture2D ReadPng(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false)) return texture;
        UnityEngine.Object.DestroyImmediate(texture);
        throw new InvalidDataException("Cannot decode PNG: " + path);
    }
    static string HashFile(string path)
    {
        using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create())
            return Convert.ToBase64String(hash.ComputeHash(stream));
    }
}

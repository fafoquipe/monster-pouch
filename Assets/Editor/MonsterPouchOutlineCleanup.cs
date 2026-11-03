using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Removes only the bright exterior halo that sits outside an existing dark pixel outline.</summary>
public static class MonsterPouchOutlineCleanup
{
    public static int RemoveWhiteExteriorHalo(string assetPath, int passes = 2)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            throw new FileNotFoundException("Missing sprite sheet for outline cleanup.", assetPath);

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(assetPath), false))
                throw new InvalidDataException("Unity could not decode " + assetPath);
            int width = texture.width, height = texture.height, removed = 0;
            Color32[] pixels = texture.GetPixels32();
            for (int pass = 0; pass < Mathf.Max(1, passes); pass++)
            {
                Color32[] before = (Color32[])pixels.Clone();
                bool changed = false;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (!NearWhite(before[index]) || !TouchesTransparency(before, width, height, x, y) ||
                        !HasNearbyDarkOutline(before, width, height, x, y, 3)) continue;
                    pixels[index].a = 0;
                    removed++;
                    changed = true;
                }
                if (!changed) break;
            }
            if (removed == 0) return 0;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return removed;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    static bool NearWhite(Color32 pixel) => pixel.a > 24 && pixel.r >= 238 && pixel.g >= 238 && pixel.b >= 238;
    static bool Dark(Color32 pixel) => pixel.a > 96 && pixel.r <= 90 && pixel.g <= 100 && pixel.b <= 115;

    static bool TouchesTransparency(Color32[] pixels, int width, int height, int x, int y)
    {
        for (int oy = -1; oy <= 1; oy++)
        for (int ox = -1; ox <= 1; ox++)
        {
            if (ox == 0 && oy == 0) continue;
            int px = x + ox, py = y + oy;
            if (px < 0 || py < 0 || px >= width || py >= height || pixels[py * width + px].a <= 24) return true;
        }
        return false;
    }

    static bool HasNearbyDarkOutline(Color32[] pixels, int width, int height, int x, int y, int radius)
    {
        for (int oy = -radius; oy <= radius; oy++)
        for (int ox = -radius; ox <= radius; ox++)
        {
            int px = x + ox, py = y + oy;
            if (px < 0 || py < 0 || px >= width || py >= height) continue;
            if (Dark(pixels[py * width + px])) return true;
        }
        return false;
    }
}

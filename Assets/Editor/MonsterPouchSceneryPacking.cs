using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

// Explicit asset-only packing: source pixels, sprite IDs and authored layouts remain intact.
public static class MonsterPouchSceneryPacking
{
    public static readonly string[] Sheets = {
        "brief-classic-cabinet-v2", "brief-lounge-cabinet-v2", "brief-royal-cabinet-v2",
        "casino-accessories-props", "royal-props", "furniture-props"
    };
    public const string Folder = "Assets/art/ambientations/modular/";

    [MenuItem("Monster Pouch/Art/Pack modular scenery for Android")]
    public static string Apply()
    {
        if (Application.isPlaying || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Finish Play Mode and the current build first.");
        int count = 0;
        foreach (string sheet in Sheets)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Folder + sheet + ".png").OfType<Sprite>().ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("No slices for " + sheet);
            string path = Folder + sheet + ".spriteatlasv2";
            var atlas = new SpriteAtlasAsset();
            atlas.Add(sprites.Cast<UnityEngine.Object>().ToArray());
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (importer == null) throw new InvalidOperationException("Missing atlas importer for " + sheet);
            var packing = importer.packingSettings;
            packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
            importer.packingSettings = packing;
            var texture = importer.textureSettings;
            texture.filterMode = FilterMode.Point; texture.generateMipMaps = false;
            texture.readable = false; texture.sRGB = true;
            importer.textureSettings = texture; importer.includeInBuild = true;
            var defaults = importer.GetPlatformSettings("DefaultTexturePlatform");
            defaults.name = "DefaultTexturePlatform"; defaults.maxTextureSize = 2048;
            defaults.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformSettings(defaults);
            var android = importer.GetPlatformSettings("Android");
            android.name = "Android"; android.overridden = true; android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ETC2_RGBA8;
            android.textureCompression = TextureImporterCompression.CompressedHQ;
            android.compressionQuality = 100;
            importer.SetPlatformSettings(android); importer.SaveAndReimport(); count += sprites.Length;
        }
        AssetDatabase.SaveAssets();
        return "Packed " + count + " scenery slices in " + Sheets.Length + " atlases; layouts and source images preserved.";
    }
}

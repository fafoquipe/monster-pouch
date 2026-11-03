using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class MonsterPouchFloralArt
{
    public const string RosePath = "Assets/resources/MonsterPouch/Effects/Floral/rosie-open-rose-v1.png";
    public const string FlowerPath = "Assets/resources/MonsterPouch/Effects/Floral/flo-healing-bloom-v1.png";

    [MenuItem("Monster Pouch/Art/Apply blooming rose and Flo flower")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply floral art outside Play Mode.");
        var rose = Import(RosePath, new Vector2(.5f, .5f));
        var flower = Import(FlowerPath, new Vector2(.5f, .01f));
        var catalog = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt");
        var projectile = catalog.Get("rosie") ?? throw new InvalidDataException("Missing Rosie projectile entry.");
        projectile.Flight = new[] { rose };
        projectile.ReferencePixels = Mathf.Max(rose.rect.width, rose.rect.height);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        if (Resources.LoadAll<Sprite>("MonsterPouch/Effects/Floral/flo-healing-bloom-v1").Single() != flower)
            throw new InvalidDataException("Flo's planted flower cannot be loaded.");
        Debug.Log("FLORAL ART READY: Rosie's basic and healing rose use " + AssetDatabase.GetAssetPath(rose) +
            "; Flo's planting and summoned flower use " + AssetDatabase.GetAssetPath(flower) +
            ". Both sprites are transparent, point-filtered and tightly framed.");
    }

    static Sprite Import(string path, Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 256;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var pixels = texture.GetPixels32();
        int left = texture.width, bottom = texture.height, right = -1, top = -1;
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            if (pixels[y * texture.width + x].a > 32)
            { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y); }
        if (right < left || top < bottom) throw new InvalidDataException("Empty floral sprite: " + path);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        string name = Path.GetFileNameWithoutExtension(path);
        var previous = provider.GetSpriteRects().FirstOrDefault(s => s.name == name);
        var rect = new SpriteRect { name = name, rect = new Rect(left, bottom, right - left + 1, top - bottom + 1),
            alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = previous?.spriteID ?? GUID.Generate() };
        provider.SetSpriteRects(new[] { rect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name, rect.spriteID) });
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
    }
}

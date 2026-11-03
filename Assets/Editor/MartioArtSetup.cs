using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Explicit menu only: no automatic import hook and no modification of shared catalogs.
public static class MartioArtSetup
{
    const string Root = "Assets/art/units/martio/";
    const string BundlePath = "Assets/resources/MonsterPouch/Martio/MartioVisualAssets.asset";

    [Serializable] public sealed class SliceFile { public int Version; public Atlas[] Atlases; }
    [Serializable] public sealed class Atlas
    {
        public string Path, Prefix, Sha256;
        public int Width, Height, Columns, Rows;
        public Frame[] Frames;
    }
    [Serializable] public sealed class Frame
    {
        public string Name;
        public int Row, Column, X, Y, Width, Height;
        public float PivotX, PivotY;
    }

    [MenuItem("Monster Pouch/Martio/Prepare isolated visual assets")]
    public static void Build()
    {
        var spec = JsonUtility.FromJson<SliceFile>(File.ReadAllText(Root + "martio-slices.json"));
        if (spec == null || spec.Version != 1 || spec.Atlases == null || spec.Atlases.Length != 5)
            throw new InvalidDataException("Unsupported Martio slice manifest.");
        // Check every source before changing any import settings.
        foreach (var atlas in spec.Atlases)
        {
            if (!atlas.Path.StartsWith(Root, StringComparison.Ordinal) || atlas.Path.Contains(".."))
                throw new InvalidDataException("Asset path escapes the Martio folder.");
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(atlas.Path))).Replace("-", "").ToLowerInvariant();
                if (hash != atlas.Sha256) throw new InvalidDataException("Source changed: " + atlas.Path);
            }
            if (atlas.Frames.Length != atlas.Columns * atlas.Rows) throw new InvalidDataException("Incomplete atlas.");
            foreach (var f in atlas.Frames)
                if (f.X < 0 || f.Y < 0 || f.Width <= 0 || f.Height <= 0 || f.X + f.Width > atlas.Width || f.Y + f.Height > atlas.Height)
                    throw new InvalidDataException("Invalid slice: " + f.Name);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(BundlePath));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var imported = new Dictionary<string, Sprite[,]>();
        foreach (var atlas in spec.Atlases) imported.Add(atlas.Prefix, Import(atlas));
        var asset = AssetDatabase.LoadAssetAtPath<MartioVisualAssets>(BundlePath);
        if (asset == null) { asset = ScriptableObject.CreateInstance<MartioVisualAssets>(); AssetDatabase.CreateAsset(asset, BundlePath); }
        else Undo.RecordObject(asset, "Prepare Martio visuals");
        asset.PortraitLeft = imported["portrait-left"][0, 0];
        asset.PortraitRight = imported["portrait-right"][0, 0];
        var body = imported["body"];
        asset.Body = new UnitArt {
            Id = "martio", Portrait = asset.PortraitRight, AuthoredBodyMotion = true,
            TravelMotion = UnitTravelMotion.AlternatingHop, WorldWidth = 1f, ReferencePixelWidth = 150,
            SourceNotes = "User references preserved. Five authored views; west views mirror east. Requires Martio celestial prop composition. Staging art only."
        };
        asset.HurtDirections = new Sprite[8];
        int[] rows = { 4, 3, 2, 1, 0, 1, 2, 3 };
        for (int d = 0; d < 8; d++)
        {
            int r = rows[d];
            var clip = new UnitDirectionalAnimation {
                Idle = new[] { body[r, 0], body[r, 1] },
                Move = new[] { body[r, 0], body[r, 2], body[r, 3] },
                HopLeft = new[] { body[r, 0], body[r, 2], body[r, 3] },
                HopRight = new[] { body[r, 1], body[r, 2], body[r, 3] },
                Attack = new[] { body[r, 4], body[r, 5], body[r, 6], body[r, 7] },
                Death = new[] { body[r, 8], body[r, 9] },
                IdleFramesPerSecond = 3, MoveFramesPerSecond = 6, AttackContactFrame = 2,
                FlipX = d >= 5, AttackLungeDistance = .025f, AttackImpactSquash = .035f
            };
            asset.Body.DirectionalAnimations[d] = clip;
            asset.Body.IdleDirections[d] = body[r, 0];
            asset.Body.AttackDirections[d] = body[r, 6];
            asset.HurtDirections[d] = body[r, 8];
        }
        var fx = imported["effects"];
        asset.Mars = new ProjectileArt { UnitId = "martio-mars", Flight = Row(fx, 0, 0, 4), Impact = Row(fx, 0, 4, 4), ReferencePixels = 222, DisplaySize = 46, OrientToTarget = false };
        asset.Moon = new ProjectileArt { UnitId = "martio-moon", Flight = Row(fx, 1, 0, 4), Impact = Row(fx, 1, 4, 4), ReferencePixels = 222, DisplaySize = 32, OrientToTarget = false };
        asset.MarsShockwave = Row(fx, 2, 0, 8);
        asset.MoonShockwave = Row(fx, 3, 0, 8);
        asset.UpgradeIcons = Row(imported["upgrades"], 0, 0, 3);
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        Debug.Log("Martio visual bundle prepared. Shared catalogs and combat remain unchanged: " + BundlePath);
    }

    static Sprite[] Row(Sprite[,] source, int row, int first, int length)
        => Enumerable.Range(first, length).Select(c => source[row, c]).ToArray();

    static Sprite[,] Import(Atlas atlas)
    {
        AssetDatabase.ImportAsset(atlas.Path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlas.Path);
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 160;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = atlas.Prefix != "upgrades";
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.isReadable = false;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect.spriteID);
        var rects = atlas.Frames.Select(f => new SpriteRect {
            name = f.Name, rect = new Rect(f.X, atlas.Height - f.Y - f.Height, f.Width, f.Height),
            alignment = SpriteAlignment.Custom, pivot = new Vector2(f.PivotX, f.PivotY),
            spriteID = previous.TryGetValue(f.Name, out var id) ? id : GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        names?.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        // A newly imported sheet can still expose the pre-slice artifact in this editor update.
        AssetDatabase.ImportAsset(atlas.Path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(atlas.Path).OfType<Sprite>().ToDictionary(s => s.name);
        var result = new Sprite[atlas.Rows, atlas.Columns];
        foreach (var f in atlas.Frames) {
            if (!sprites.TryGetValue(f.Name, out var sprite))
                throw new InvalidDataException("Missing sprite " + f.Name + " in " + atlas.Path + "; imported: " + string.Join(",", sprites.Keys));
            result[f.Row, f.Column] = sprite;
        }
        return result;
    }
}

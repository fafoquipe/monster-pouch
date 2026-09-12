using System;
using System.IO;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

public static class MonsterPouchProjectileSetup
{
    public const string SourcePath = "Assets/art/effects/roster/projectiles-source-v1.png";
    public const string AtlasPath = "Assets/art/effects/roster/projectiles-rgba-v1.png";
    public static void Setup()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException(SourcePath);
        // The selected generated output already has real alpha. Retain it, including the violet effects.
        File.Copy(SourcePath, AtlasPath, true);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        Sprite[,] sprites = MonsterPouchSpriteAnimationImporter.ImportGrid(AtlasPath, 128, 8, 4, "projectiles");
        const string path = "Assets/Resources/MonsterPouch/ProjectileArt.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<ProjectileArtCatalog>(path);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<ProjectileArtCatalog>(); AssetDatabase.CreateAsset(catalog, path); }
        var ids = new[] { "anuik", "tauris", "stein", "bugui" };
        catalog.Projectiles = new ProjectileArt[4];
        for (int row = 0; row < 4; row++)
        {
            var effect = new ProjectileArt { UnitId = ids[row], Flight = new Sprite[4], Impact = new Sprite[4], DisplaySize = row == 1 ? 60 : 46, OrientToTarget = row != 0 };
            float largest = 1;
            for (int col = 0; col < 8; col++)
            {
                var sprite = sprites[row, col];
                if (sprite == null) throw new InvalidOperationException("Empty effect cell: " + row + "," + col);
                if (col < 4) effect.Flight[col] = sprite; else effect.Impact[col - 4] = sprite;
                largest = Mathf.Max(largest, sprite.rect.width, sprite.rect.height);
            }
            effect.ReferencePixels = largest;
            catalog.Projectiles[row] = effect;
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Monster Pouch: corazones, mordiscos, rayos y magia Bugui importados: 32 fotogramas.");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Applies Kayon's requested ranged coin attack without replacing other characters or effects.</summary>
public static class MonsterPouchKayonCoinsSetup
{
    public const string Source = "Assets/art/effects/kayon/coins-source-v1.png";
    public const string Atlas = "Assets/art/effects/kayon/coins-rgba-v1.png";

    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before applying Kayon's coin attack.");
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/Resources/MonsterPouch/MatchConfig.asset");
        var catalog = AssetDatabase.LoadAssetAtPath<ProjectileArtCatalog>("Assets/Resources/MonsterPouch/ProjectileArt.asset");
        if (config == null || config.Get("kayon") == null || catalog == null)
            throw new InvalidOperationException("Existing Kayon and projectile catalog required.");
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool hasAlpha;
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Source))) throw new InvalidDataException(Source);
            hasAlpha = texture.GetPixels32().Any(pixel => pixel.a < 128);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        if (hasAlpha)
        {
            File.Copy(Source, Atlas, true);
            AssetDatabase.ImportAsset(Atlas, ImportAssetOptions.ForceSynchronousImport);
        }
        else MonsterPouchSpriteAnimationImporter.PrepareChromaSource(Source, Atlas, new Color32(255, 0, 255, 255), 48);
        Sprite[,] sprites = MonsterPouchSpriteAnimationImporter.ImportGrid(Atlas, 128, 4, 2, "kayon-coins");
        var effect = new ProjectileArt { UnitId = "kayon", Flight = new Sprite[4], Impact = new Sprite[4],
            DisplaySize = 34, ReferencePixels = 1, OrientToTarget = false };
        for (int col = 0; col < 4; col++)
        {
            effect.Flight[col] = sprites[0, col];
            effect.Impact[col] = sprites[1, col];
            foreach (Sprite sprite in new[] { sprites[0, col], sprites[1, col] })
                effect.ReferencePixels = Mathf.Max(effect.ReferencePixels, sprite.rect.width, sprite.rect.height);
        }
        var effects = new List<ProjectileArt>(catalog.Projectiles ?? Array.Empty<ProjectileArt>());
        int index = effects.FindIndex(item => item != null && item.UnitId == "kayon");
        if (index < 0) effects.Add(effect); else effects[index] = effect;
        catalog.Projectiles = effects.ToArray();
        UnitDefinition defaults = ExpandedRoster.CreateKayon();
        config.Get("kayon").AttackRange = defaults.AttackRange;
        config.Get("kayon").BaseAbility.Description = defaults.BaseAbility.Description;
        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Kayon: monedas de oro, alcance " + defaults.AttackRange + ", giro4/impacto4. Vida, daño e invocaciones conservados.");
    }
}

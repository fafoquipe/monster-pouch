using System;
using System.Collections.Generic;
using System.IO;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Additive, repeatable import for Kayon, Stein, and the authored Bugui casting animations.</summary>
public static class MonsterPouchWhelpsArtSetup
{
    public const string KayonSourcePath = "Assets/art/units/generated/kayon/kayon-animation-source-v1.png";
    public const string KayonAtlasPath = "Assets/art/units/generated/kayon/kayon-animation-rgba-v1.png";
    public const string SteinSourcePath = "Assets/art/units/generated/stein/stein-animation-source-v1.png";
    public const string SteinAtlasPath = "Assets/art/units/generated/stein/stein-animation-rgba-v1.png";
    public const string BuguiSourcePath = "Assets/art/units/generated/bugui/bugui-animation-source-v2.png";
    public const string BuguiAtlasPath = "Assets/art/units/generated/bugui/bugui-animation-rgba-v2.png";
    private const string ConfigPath = "Assets/Resources/MonsterPouch/MatchConfig.asset";
    private const string CatalogPath = "Assets/Resources/MonsterPouch/UnitArt.asset";

    [MenuItem("Monster Pouch/Art/Import Kayon Stein and Bugui animations")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Sal del modo Play antes de importar el arte.");
        foreach (string source in new[] { KayonSourcePath, SteinSourcePath, BuguiSourcePath })
            if (!File.Exists(source)) throw new FileNotFoundException("Falta una hoja fuente de Whelps.", source);

        MatchConfig config = AssetDatabase.LoadAssetAtPath<MatchConfig>(ConfigPath);
        UnitArtCatalog catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        if (config == null || catalog == null)
            throw new InvalidOperationException("Crea primero MatchConfig y UnitArt con la configuración local.");

        // All sheets are decoded and sliced before replacing catalog entries.
        // The generated source bytes and all original Bugui views remain untouched.
        Sprite[,] kayon = Import(KayonSourcePath, KayonAtlasPath, "kayon");
        Sprite[,] stein = Import(SteinSourcePath, SteinAtlasPath, "stein");
        Sprite[,] bugui = Import(BuguiSourcePath, BuguiAtlasPath, "bugui-v2");
        UnitArt previousBugui = catalog.Get("bugui");
        float buguiWidth = previousBugui != null && previousBugui.WorldWidth > 0 ? previousBugui.WorldWidth : .72f;

        var definitions = new List<UnitDefinition>(config.Units ?? Array.Empty<UnitDefinition>());
        ReplaceDefinition(definitions, ExpandedRoster.CreateKayon());
        ReplaceDefinition(definitions, ExpandedRoster.CreateStein());

        var artwork = new List<UnitArt>(catalog.Units ?? Array.Empty<UnitArt>());
        ReplaceArt(artwork, CreateArt("kayon", kayon, .80f,
            "Kayon #76: photographed MegaTrip album palette and unpainted toy sculpt; squat navy body, cyan eyes."));
        ReplaceArt(artwork, CreateArt("stein", stein, .86f,
            "Stein #03: photographed MegaTrip album; cream scientist, white hair, facial formula and large grin. Electrical casting poses."));
        ReplaceArt(artwork, CreateArt("bugui", bugui, buguiWidth,
            "Bugui: original project front/profile/back sprites preserved as identity references. New orb windup, release and followthrough poses."));

        config.Units = definitions.ToArray();
        catalog.Units = artwork.ToArray();
        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Monster Pouch: Kayon y Stein añadidos; Bugui animado. 180 poses importadas, cinco vistas por unidad y tres reflejadas. Originales conservados.");
    }

    private static Sprite[,] Import(string source, string atlas, string prefix)
    {
        MonsterPouchSpriteAnimationImporter.PrepareChromaSource(source, atlas, new Color32(255, 0, 255, 255), 48);
        return MonsterPouchSpriteAnimationImporter.ImportGrid(atlas, 100f, 12, 5, prefix);
    }

    private static void ReplaceDefinition(List<UnitDefinition> items, UnitDefinition replacement)
    {
        int index = items.FindIndex(item => item != null && string.Equals(item.Id, replacement.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) items.Add(replacement);
        else items[index] = replacement;
    }

    private static void ReplaceArt(List<UnitArt> items, UnitArt replacement)
    {
        int index = items.FindIndex(item => item != null && string.Equals(item.Id, replacement.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) items.Add(replacement);
        else items[index] = replacement;
    }

    private static UnitArt CreateArt(string id, Sprite[,] frames, float width, string identity)
    {
        // Source rows: S, SE, E, NE, N. Opposite diagonals/profile intentionally share mirrored clips.
        int[] rowForFacing = { 4, 3, 2, 1, 0, 1, 2, 3 };
        var art = new UnitArt
        {
            Id = id, WorldWidth = width, Portrait = frames[0, 0],
            ReferencePixelWidth = Mathf.Max(1f, frames[0, 0].rect.width),
            IdleDirections = new Sprite[8], AttackDirections = new Sprite[8],
            WalkRigs = new UnitSpriteRig[8], DirectionalAnimations = new UnitDirectionalAnimation[8],
            ProvisionalDirectionalProjection = false,
            SourceNotes = identity + " Generated pixel-art interpretation: 60 poses, S/SE/E/NE/N; SW/W/NW intentionally mirrored. " +
                "Each view: idle2, move4, attack3, death3. Source is immutable magenta; RGBA derivative and pivots use the shared importer. " +
                "Reference sources and exact built-in imagegen prompts: docs/whelps-expanded-art.md and docs/whelps-generation-prompts.md. " +
                "Animation staging is an original game interpretation, not extracted toy animation."
        };
        art.FootPivot = new Vector2(art.Portrait.pivot.x / art.Portrait.rect.width, art.Portrait.pivot.y / art.Portrait.rect.height);
        for (int direction = 0; direction < 8; direction++)
        {
            int row = rowForFacing[direction];
            art.IdleDirections[direction] = frames[row, 0];
            art.AttackDirections[direction] = frames[row, 7];
            art.DirectionalAnimations[direction] = new UnitDirectionalAnimation
            {
                Idle = Slice(frames, row, 0, 2), Move = Slice(frames, row, 2, 4),
                Attack = Slice(frames, row, 6, 3), Death = Slice(frames, row, 9, 3),
                IdleFramesPerSecond = 2.5f, MoveFramesPerSecond = 10f,
                AttackContactFrame = 1, FlipX = direction >= 5
            };
        }
        return art;
    }

    private static Sprite[] Slice(Sprite[,] frames, int row, int start, int count)
    {
        var result = new Sprite[count];
        for (int frame = 0; frame < count; frame++) result[frame] = frames[row, start + frame];
        return result;
    }
}

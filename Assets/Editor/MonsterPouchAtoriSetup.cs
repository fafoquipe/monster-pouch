using System;
using System.Collections.Generic;
using System.IO;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Additive, repeatable import for the generated MegaTrip Atori interpretation.</summary>
public static class MonsterPouchAtoriSetup
{
    public const string SourcePath = "Assets/art/units/generated/atori/atori-animation-source.png";
    public const string AtlasPath = "Assets/art/units/generated/atori/atori-animation-rgba.png";
    private const string ConfigPath = "Assets/Resources/MonsterPouch/MatchConfig.asset";
    private const string CatalogPath = "Assets/Resources/MonsterPouch/UnitArt.asset";

    [MenuItem("Monster Pouch/Art/Import Atori animations and definition")]
    public static void Setup()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Falta la hoja fuente de Atori.", SourcePath);
        MatchConfig config = AssetDatabase.LoadAssetAtPath<MatchConfig>(ConfigPath);
        UnitArtCatalog catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        if (config == null || catalog == null)
            throw new InvalidOperationException("Crea primero MatchConfig y UnitArt con la configuración local del proyecto.");

        // Preserve the image-generator source. Keying happens only in the derived import texture.
        MonsterPouchSpriteAnimationImporter.PrepareChromaSource(SourcePath, AtlasPath, new Color32(255, 0, 255, 255), 48);
        Sprite[,] frames = MonsterPouchSpriteAnimationImporter.ImportGrid(AtlasPath, 100f, 12, 5, "atori");
        UnitArt art = CreateArt(frames);

        // Only the entry owned by this setup is replaced. All configured units and user choices remain.
        var definitions = new List<UnitDefinition>(config.Units ?? Array.Empty<UnitDefinition>());
        int definitionIndex = definitions.FindIndex(unit => unit != null && unit.Id == "atori");
        if (definitionIndex < 0) definitions.Add(CreateDefinition());
        else definitions[definitionIndex] = CreateDefinition();
        config.Units = definitions.ToArray();

        var artwork = new List<UnitArt>(catalog.Units ?? Array.Empty<UnitArt>());
        int artIndex = artwork.FindIndex(unit => unit != null && string.Equals(unit.Id, "atori", StringComparison.OrdinalIgnoreCase));
        if (artIndex < 0) artwork.Add(art);
        else artwork[artIndex] = art;
        catalog.Units = artwork.ToArray();
        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Monster Pouch: Atori añadido; 60 poses, 5 vistas dibujadas y 3 vistas reflejadas, 4 acciones por dirección. Balance provisional propio.");
    }

    public static UnitDefinition CreateDefinition()
    {
        // Original game balance, not statistics or rules from the Gogo's toy collection.
        return new UnitDefinition
        {
            Id = "atori", DisplayName = "Atori", IsMonster = false,
            MaxHealth = 36, Damage = 6, AttackInterval = 1.05f, AttackWindup = .3f,
            AttackRange = 1, MoveInterval = .4f, IQSpeed = 7, BaseCost = 3,
            Formation = FormationPreference.Middle, TargetPolicy = TargetPolicy.LowestHealth,
            BaseAbility = new TrickDefinition
            {
                Id = "atori-precise-bump", Name = "Cabezazo certero",
                Description = "Busca un rival alcanzable con poca vida. Cada tercer impacto suma 4 de daño.",
                BonusEveryHits = 3, BonusDamage = 4
            },
            Tricks = new[]
            {
                new TrickDefinition { Id = "atori-hard-head", Name = "Cabeza dura", Description = "Suma 1 de armadura. Cada golpe recibido inflige al menos 1 de daño.", Cost = 2, Armor = 1 },
                new TrickDefinition { Id = "atori-momentum", Name = "Impulso", Description = "+2 de daño en cada impacto.", Cost = 2, DamageBonus = 2 },
                new TrickDefinition { Id = "atori-endurance", Name = "Aguante", Description = "+16 de vida máxima al comenzar el combate.", Cost = 3, HealthBonus = 16 }
            }
        };
    }

    private static UnitArt CreateArt(Sprite[,] frames)
    {
        // Sheet rows are S, SE, E, NE, N. Atori has a bilaterally symmetric silhouette.
        int[] rowForFacing = { 4, 3, 2, 1, 0, 1, 2, 3 };
        var art = new UnitArt
        {
            Id = "atori", WorldWidth = .88f, Portrait = frames[0, 0],
            ReferencePixelWidth = Mathf.Max(1f, frames[0, 0].rect.width),
            IdleDirections = new Sprite[8], AttackDirections = new Sprite[8],
            WalkRigs = new UnitSpriteRig[8], DirectionalAnimations = new UnitDirectionalAnimation[8],
            ProvisionalDirectionalProjection = false,
            SourceNotes = "Generated pixel-art interpretation of Atori #01, MegaTrip, referenced from photographed album artwork. " +
                "60 poses: S/SE/E/NE/N, with SW/W/NW intentionally mirrored; each has idle2, move4, attack3 and death3. " +
                "The immutable source uses magenta; a separate RGBA atlas removes the import key. " +
                "Character references and generation prompts: docs/atori-megatrip.md. All combat statistics are provisional Monster Pouch design."
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
                IdleFramesPerSecond = 2.5f, MoveFramesPerSecond = 10f, AttackContactFrame = 1,
                FlipX = direction >= 5
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

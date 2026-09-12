using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Additive import of Anuik and Tauris. Runtime rules come from ExpandedRoster, not the art source.</summary>
public static class MonsterPouchMonstersArtSetup
{
    public const string AnuikSource = "Assets/art/units/generated/anuik/anuik-animation-source-v2.png";
    public const string AnuikAtlas = "Assets/art/units/generated/anuik/anuik-animation-rgba-v2.png";
    public const string ReviveSource = "Assets/art/units/generated/anuik/anuik-revive-source-v1.png";
    public const string ReviveAtlas = "Assets/art/units/generated/anuik/anuik-revive-rgba-v1.png";
    public const string TaurisSource = "Assets/art/units/generated/tauris/tauris-animation-source-v1.png";
    public const string TaurisAtlas = "Assets/art/units/generated/tauris/tauris-animation-rgba-v1.png";
    private const string CatalogPath = "Assets/Resources/MonsterPouch/UnitArt.asset";
    private const string ConfigPath = "Assets/Resources/MonsterPouch/MatchConfig.asset";
    private static readonly int[] Rows = { 4, 3, 2, 1, 0, 1, 2, 3 };
    private static readonly Color32 Chroma = new Color32(255, 0, 255, 255);
    private const float Ppu = 100f;

    [MenuItem("Monster Pouch/Art/Import Anuik and Tauris")]
    public static void Setup()
    {
        UnitArtCatalog catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        MatchConfig config = AssetDatabase.LoadAssetAtPath<MatchConfig>(ConfigPath);
        if (catalog == null || config == null)
            throw new InvalidOperationException("Create the local UnitArt and MatchConfig assets before importing the new Monsters.");

        Sprite[,] anuikFrames = Import(AnuikSource, AnuikAtlas, Ppu, 12, 5, "anuik-v2");
        Sprite[,] reviveFrames = Import(ReviveSource, ReviveAtlas, Ppu, 5, 5, "anuik-revive-v1");
        // Match the standing ear span across the two differently sized generated atlases. This changes import scale only.
        float revivePpu = Ppu * VisibleWidth(reviveFrames[0, 4]) / VisibleWidth(anuikFrames[0, 0]);
        reviveFrames = MonsterPouchSpriteAnimationImporter.ImportGrid(ReviveAtlas, revivePpu, 5, 5, "anuik-revive-v1");
        Sprite[,] taurisFrames = Import(TaurisSource, TaurisAtlas, Ppu, 14, 5, "tauris-v1");

        UnitArt anuik = BuildArt("anuik", anuikFrames, 1.18f, new[] { 6, 7, 8 }, new[] { 9, 10, 11 });
        UnitArt tauris = BuildArt("tauris", taurisFrames, 1f, new[] { 6, 7, 8, 9 }, new[] { 10, 11, 12, 13 });
        for (int d = 0; d < 8; d++)
        {
            // Hold the two actual upside-down drawings in the middle; transition through curled/crouched poses.
            anuik.DirectionalAnimations[d].Revive = Sequence(reviveFrames, Rows[d], 0, 1, 2, 2, 2, 3, 4);
        }
        anuik.SourceNotes = "Pixel-art interpretation of Anuik MegaTrip #02 based on photographed album artwork: broad peach-orange ears/head, closed happy eyes, gray heart shirt. Five source views plus three intentional mirrored views. Idle2/Move4/Attack3/Death3; separate 25-pose revive sheet includes real headstands, not rotated idle art. Source/chroma/PPU audit and exact prompts: docs/anuik-tauris-art.md.";
        tauris.SourceNotes = "Pixel-art interpretation of Tauris MegaTrip #36 from photographed album silhouette and actual figure collection. Knucklebone-shaped head, two eyes, broad teeth and short side-gesturing arm; blue-gray game palette. Five source views plus three mirrored views. Idle2/Move4/Attack4/Death4 across 70 poses. Exact sources/prompts: docs/anuik-tauris-art.md.";

        var artwork = new List<UnitArt>(catalog.Units ?? Array.Empty<UnitArt>());
        ReplaceArt(artwork, anuik);
        ReplaceArt(artwork, tauris);
        catalog.Units = artwork.ToArray();
        var definitions = new List<UnitDefinition>(config.Units ?? Array.Empty<UnitDefinition>());
        ReplaceDefinition(definitions, ExpandedRoster.CreateAnuik());
        ReplaceDefinition(definitions, ExpandedRoster.CreateTauris());
        config.Units = definitions.ToArray();
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        WriteReport(anuikFrames, reviveFrames, taurisFrames, revivePpu);
        Debug.Log("Monster Pouch: Anuik and Tauris imported additively, including Anuik's authored headstand revival. Source images and existing catalog/material GUIDs preserved.");
    }

    private static Sprite[,] Import(string source, string atlas, float ppu, int columns, int rows, string prefix)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("Missing generated Monster source", source);
        MonsterPouchSpriteAnimationImporter.PrepareChromaSource(source, atlas, Chroma, 48);
        return MonsterPouchSpriteAnimationImporter.ImportGrid(atlas, ppu, columns, rows, prefix);
    }

    private static UnitArt BuildArt(string id, Sprite[,] frames, float width, int[] attack, int[] death)
    {
        var art = new UnitArt
        {
            Id = id, WorldWidth = width, Portrait = frames[0, 0], ReferencePixelWidth = VisibleWidth(frames[0, 0]),
            IdleDirections = new Sprite[8], AttackDirections = new Sprite[8], WalkRigs = new UnitSpriteRig[8],
            DirectionalAnimations = new UnitDirectionalAnimation[8], ProvisionalDirectionalProjection = false
        };
        art.FootPivot = new Vector2(art.Portrait.pivot.x / art.Portrait.rect.width, art.Portrait.pivot.y / art.Portrait.rect.height);
        for (int d = 0; d < 8; d++)
        {
            int row = Rows[d];
            art.IdleDirections[d] = frames[row, 0];
            art.AttackDirections[d] = frames[row, attack[1]];
            art.DirectionalAnimations[d] = new UnitDirectionalAnimation
            {
                Idle = Sequence(frames, row, 0, 1), Move = Sequence(frames, row, 2, 3, 4, 5),
                Attack = Sequence(frames, row, attack), Death = Sequence(frames, row, death),
                IdleFramesPerSecond = 2.5f, MoveFramesPerSecond = 10f, AttackContactFrame = 1,
                // Opposite views intentionally mirror a bilateral design, not Popow's authored handed glove poses.
                FlipX = d >= 5
            };
        }
        return art;
    }

    private static float VisibleWidth(Sprite sprite) => Mathf.Max(1, sprite.rect.width - 4);

    private static Sprite[] Sequence(Sprite[,] frames, int row, params int[] columns)
    {
        var result = new Sprite[columns.Length];
        for (int i = 0; i < columns.Length; i++) result[i] = frames[row, columns[i]];
        return result;
    }

    private static void ReplaceArt(List<UnitArt> list, UnitArt entry)
    {
        int index = list.FindIndex(item => item != null && string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) list.Add(entry); else list[index] = entry;
    }

    private static void ReplaceDefinition(List<UnitDefinition> list, UnitDefinition entry)
    {
        int index = list.FindIndex(item => item != null && string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) list.Add(entry); else list[index] = entry;
    }

    private static void WriteReport(Sprite[,] anuik, Sprite[,] revive, Sprite[,] tauris, float revivePpu)
    {
        var report = new StringBuilder("# Anuik and Tauris import audit\n\nGenerated by MonsterPouchMonstersArtSetup.Setup. Asset import evidence, not a claim of rendered gameplay or combat-test approval.\n\n");
        report.AppendLine("Rows: S, SE, E, NE, N. Runtime clockwise views: N, NE, E, SE, S, SW, W, NW; the last three intentionally mirror their opposite source views.\n");
        report.AppendLine($"Anuik main: Idle0,1 / Move2,3,4,5 / Attack6,7,8 (contact index1) / Death9,10,11. Revive source0,1,2,2,2,3,4, with actual headstands in source columns1 and2. Revival PPU: {revivePpu:F3}; standing ear width matches the main atlas.\n");
        report.AppendLine("Tauris: Idle0,1 / Move2,3,4,5 / Attack6,7,8,9 (contact index1) / Death10,11,12,13. The generator supplied14 columns; all are assigned.\n");
        AppendAtlas(report, AnuikAtlas, anuik);
        AppendAtlas(report, ReviveAtlas, revive);
        AppendAtlas(report, TaurisAtlas, tauris);
        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/monsters-art-import-report.md", report.ToString(), new UTF8Encoding(false));
    }

    private static void AppendAtlas(StringBuilder report, string path, Sprite[,] frames)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false);
            int clear = 0, opaque = 0, partial = 0;
            foreach (Color32 pixel in texture.GetPixels32())
                if (pixel.a == 0) clear++; else if (pixel.a == 255) opaque++; else partial++;
            report.AppendLine($"## {path}\n\n{texture.width}x{texture.height}; actual alpha0: {clear}, alpha255: {opaque}, partial: {partial}.\n");
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        report.AppendLine("| Row | Column | Sprite | Pixel rectangle | Foot/contact pivot in pixels | PPU |\n| --- | --- | --- | --- | --- | --- |");
        for (int row = 0; row < frames.GetLength(0); row++)
        for (int col = 0; col < frames.GetLength(1); col++)
        {
            Sprite sprite = frames[row, col];
            report.AppendLine($"| {row} | {col} | {sprite.name} | {sprite.rect} | {sprite.pivot} | {sprite.pixelsPerUnit:F3} |");
        }
        report.AppendLine();
    }
}

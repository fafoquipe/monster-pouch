using System;
using System.IO;
using System.Linq;
using System.Text;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Updates only Stein and Kayon from the designs supplied by the user.</summary>
public static class MonsterPouchUserWhelpsArtSetup
{
    private const string CatalogPath = "Assets/Resources/MonsterPouch/UnitArt.asset";
    private static readonly int[] Rows = { 4, 3, 2, 1, 0, 1, 2, 3 };

    [MenuItem("Monster Pouch/Art/Use the user's Stein and Kayon designs")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing art.");
        UnitArtCatalog catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        if (catalog == null || catalog.Get("stein") == null || catalog.Get("kayon") == null)
            throw new InvalidOperationException("The existing Stein and Kayon catalog entries are required.");
        var report = new StringBuilder("# Stein y Kayon: referencias del usuario\n\nImportación mediante Unity Editor API. Fuentes conservadas; fondo magenta convertido a alfa en archivos derivados.\n\n");
        Install(catalog.Get("stein"), 15, new[] { 2, 3, 4, 5 }, new[] { 6, 7, 8, 9, 10 }, new[] { 11, 12, 13, 14 }, report);
        Install(catalog.Get("kayon"), 16, new[] { 2, 3, 4, 5, 6 }, new[] { 7, 8, 9, 10, 11 }, new[] { 12, 13, 14, 15 }, report);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        File.WriteAllText("docs/user-whelps-import-report.md", report.ToString(), new UTF8Encoding(false));
        Debug.Log("Stein y Kayon: retratos del usuario y 155 poses importadas; escalas del tablero conservadas.");
    }

    private static void Install(UnitArt art, int columns, int[] move, int[] attack, int[] death, StringBuilder report)
    {
        string folder = "Assets/art/units/generated/" + art.Id + "-approved/";
        string atlas = folder + art.Id + "-animation-rgba-v1.png";
        string portraitAtlas = folder + art.Id + "-portrait-rgba-v1.png";
        MonsterPouchSpriteAnimationImporter.PrepareChromaSource(folder + art.Id + "-animation-source-v1.png", atlas, new Color32(255, 0, 255, 255), 48);
        MonsterPouchSpriteAnimationImporter.PrepareChromaSource(folder + art.Id + "-portrait-source-v1.png", portraitAtlas, new Color32(255, 0, 255, 255), 48);
        Sprite[,] frames = MonsterPouchSpriteAnimationImporter.ImportGrid(atlas, 100, columns, 5, art.Id + "-approved");
        Sprite[,] portrait = MonsterPouchSpriteAnimationImporter.ImportGrid(portraitAtlas, 100, 1, 1, art.Id + "-approved-portrait");
        art.Portrait = portrait[0, 0];
        art.ReferencePixelWidth = Mathf.Max(1, frames[0, 0].rect.width - 4);
        art.IdleDirections = new Sprite[8];
        art.AttackDirections = new Sprite[8];
        art.WalkRigs = new UnitSpriteRig[8];
        art.DirectionalAnimations = new UnitDirectionalAnimation[8];
        art.ProvisionalDirectionalProjection = false;
        for (int d = 0; d < 8; d++)
        {
            int row = Rows[d];
            art.IdleDirections[d] = frames[row, 0];
            art.AttackDirections[d] = frames[row, attack[1]];
            art.DirectionalAnimations[d] = new UnitDirectionalAnimation
            {
                Idle = Sequence(frames, row, new[] { 0, 1 }), Move = Sequence(frames, row, move),
                Attack = Sequence(frames, row, attack), Death = Sequence(frames, row, death),
                IdleFramesPerSecond = 2.5f, MoveFramesPerSecond = 10, AttackContactFrame = 1, FlipX = d >= 5
            };
        }
        Sprite standing = frames[0, 0];
        art.FootPivot = new Vector2(standing.pivot.x / standing.rect.width, standing.pivot.y / standing.rect.height);
        art.SourceNotes = "User supplied and identified design, September 12 2026. " +
            (art.Id == "stein" ? "Peach body, white hair, formula face, huge grin and chest heart; cyan lightning casting. " :
                "Royal blue round head, two white arrow/circle marks, tiny central eyes, curved smile and sack of golden stones. ") +
            (columns * 5) + " generated poses in five views with three intentional mirrored directions. Original portraits and exact built-in imagegen prompts preserved: docs/user-whelps-designs.md. Existing combat stats and world width retained.";
        report.AppendLine($"## {art.Id}\n\n{columns * 5} poses ({columns} × 5), width {art.WorldWidth}, reference pixels {art.ReferencePixelWidth}. Idle0–1; Move {string.Join(",", move)}; Attack {string.Join(",", attack)}; Death {string.Join(",", death)}. Contact index1. PPU100, Point, no compression or mipmaps.\n");
        report.AppendLine("| Row | Frame | Rect | Pivot |\n| --- | --- | --- | --- |");
        for (int row = 0; row < 5; row++)
        for (int col = 0; col < columns; col++)
            report.AppendLine($"| {row} | {col} | {frames[row, col].rect} | {frames[row, col].pivot} |");
        report.AppendLine();
    }

    private static Sprite[] Sequence(Sprite[,] frames, int row, int[] columns) => columns.Select(col => frames[row, col]).ToArray();
}

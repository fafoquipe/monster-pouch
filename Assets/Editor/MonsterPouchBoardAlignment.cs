using System;
using System.IO;
using System.Linq;
using System.Text;
using MonsterPouch.Gameplay.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit calibration to the existing art, without modifying its pixels or logical board data.</summary>
public static class MonsterPouchBoardAlignment
{
    private const string BoardTexturePath = "Assets/art/ambientations/casino/green-path-np.png";
    // Measured checkerboard cell centers in the original 1086 x 1448 PNG, from top left.
    // These describe geometry only. Cell values, quadrants, and territories stay in BoardManager.
    private static readonly float[] ColumnCenters = { 209f, 340.25f, 473.5f, 607.25f, 740.75f, 873.75f };
    private static readonly float[] RowCenters = { 137.25f, 268.25f, 402f, 536.5f, 671.75f, 806.5f, 940.75f, 1074.5f, 1207.25f, 1337.25f };

    [MenuItem("Monster Pouch/Align board cell centers to existing art")]
    public static string Calibrate()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before saving board alignment.");
        FindComponents(out BoardManager board, out BoardWorldMapper mapper, out SpriteRenderer surface);
        Fit(ColumnCenters, out float firstX, out float stepX);
        Fit(RowCenters, out float firstY, out float stepY);
        Vector3 first = PixelToWorld(surface, new Vector2(firstX, firstY));
        Vector3 across = PixelToWorld(surface, new Vector2(firstX + stepX, firstY)) - first;
        Vector3 down = PixelToWorld(surface, new Vector2(firstX, firstY + stepY)) - first;
        if (Mathf.Abs(across.y) > .00001f || Mathf.Abs(down.x) > .00001f)
            throw new InvalidOperationException("The current mapper expects an axis-aligned board.");
        Vector2 beforeSize = mapper.CellSize;
        Vector2 beforeOffset = mapper.BoardOffset;
        float beforeError = MaximumError(mapper, board, surface);
        Vector2 size = new Vector2(across.x, down.y);
        Vector2 offset = first - surface.transform.position;
        Undo.RecordObject(mapper, "Align Monster Pouch cells");
        mapper.Configure(board, surface.transform, size, offset);
        EditorUtility.SetDirty(mapper);
        EditorSceneManager.MarkSceneDirty(mapper.gameObject.scene);
        EditorSceneManager.SaveScene(mapper.gameObject.scene);
        string report = BuildReport(board, mapper, surface, beforeSize, beforeOffset, beforeError);
        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/board-alignment.md", report, new UTF8Encoding(false));
        Debug.Log(report);
        return report;
    }

    public static string Measure()
    {
        FindComponents(out BoardManager board, out BoardWorldMapper mapper, out SpriteRenderer surface);
        return BuildReport(board, mapper, surface, mapper.CellSize, mapper.BoardOffset, MaximumError(mapper, board, surface));
    }

    private static void FindComponents(out BoardManager board, out BoardWorldMapper mapper, out SpriteRenderer surface)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/main-scene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/main-scene.unity before calibrating.");
        board = UnityEngine.Object.FindObjectsByType<BoardManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(b => b.gameObject.scene == scene);
        mapper = UnityEngine.Object.FindObjectsByType<BoardWorldMapper>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(m => m.gameObject.scene == scene);
        surface = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(s => s.gameObject.scene == scene && s.name == "table-surface");
        if (surface.sprite == null || AssetDatabase.GetAssetPath(surface.sprite) != BoardTexturePath ||
            surface.sprite.texture.width != 1086 || surface.sprite.texture.height != 1448)
            throw new InvalidOperationException("Board source differs from the inspected 1086 x 1448 green-path-np.png.");
        if (!board.IsInitialized) board.BuildBoard();
    }

    private static Vector3 PixelToWorld(SpriteRenderer surface, Vector2 topLeftPixel)
    {
        Sprite sprite = surface.sprite;
        Vector2 texturePoint = new Vector2(topLeftPixel.x, sprite.texture.height - topLeftPixel.y);
        Vector2 local = (texturePoint - sprite.rect.position - sprite.pivot) / sprite.pixelsPerUnit;
        return surface.transform.TransformPoint(new Vector3(local.x, local.y, 0));
    }

    private static void Fit(float[] centers, out float first, out float step)
    {
        float meanIndex = (centers.Length - 1) * .5f;
        float meanCenter = centers.Average();
        float covariance = 0, variance = 0;
        for (int i = 0; i < centers.Length; i++)
        {
            covariance += (i - meanIndex) * (centers[i] - meanCenter);
            variance += (i - meanIndex) * (i - meanIndex);
        }
        step = covariance / variance;
        first = meanCenter - meanIndex * step;
    }

    private static float MaximumError(BoardWorldMapper mapper, BoardManager board, SpriteRenderer surface)
    {
        float maximum = 0;
        for (int y = 0; y < RowCenters.Length; y++)
        for (int x = 0; x < ColumnCenters.Length; x++)
        {
            Vector3 measured = PixelToWorld(surface, new Vector2(ColumnCenters[x], RowCenters[y]));
            maximum = Mathf.Max(maximum, Vector2.Distance(measured, mapper.GetWorldPosition(board.GetCell(x, y))));
        }
        return maximum;
    }

    private static string BuildReport(BoardManager board, BoardWorldMapper mapper, SpriteRenderer surface,
        Vector2 beforeSize, Vector2 beforeOffset, float beforeError)
    {
        var report = new StringBuilder("# Board alignment to the existing artwork\n\n");
        report.AppendLine("Measured from green-path-np.png (1086 x 1448). A least-squares regular grid fits the six drawn columns and ten drawn rows. This changes only the mapper, preserving sprite foot pivots, animations, board values, occupancy, Brief, and artwork.\n");
        report.AppendLine($"Previous cell size: {beforeSize.ToString("F7")}; offset: {beforeOffset.ToString("F7")}.");
        report.AppendLine($"Current cell size: {mapper.CellSize.ToString("F7")}; offset: {mapper.BoardOffset.ToString("F7")}.");
        report.AppendLine($"Maximum center error before: {beforeError:F6} world units; after: {MaximumError(mapper, board, surface):F6} world units.");
        report.AppendLine($"Cell (0,0): {mapper.GetWorldPosition(board.GetCell(0, 0)).ToString("F6")}; cell (5,9): {mapper.GetWorldPosition(board.GetCell(5, 9)).ToString("F6")}.\n");
        report.AppendLine("The source's hand-drawn boundaries vary slightly: remaining residual is approximately one screen pixel in the 540 x 960 composition. Unit roots and their ground footprint use these cell centers; their bodies extend above them. Rendering validation should inspect preparation with no result dialog and compare the four corners and central cells. This report records measurements, not a completed screenshot check.\n");
        report.AppendLine("Repeat in the Editor with Monster Pouch → Align board cell centers to existing art, or invoke MonsterPouchBoardAlignment.Calibrate(). It is safe to rerun and leaves texture GUIDs and subassets intact.");
        return report.ToString();
    }
}

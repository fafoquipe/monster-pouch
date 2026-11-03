using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class NationArenaSceneryPlayTests
{
    Scene scene;
    GameObject root;
    BoardManager board;
    BoardWorldMapper mapper;
    CardTableArena arena;
    Camera camera;
    Sprite frameSprite;
    NationArenaScenery scenery;

    [UnitySetUp] public IEnumerator Setup()
    {
        // Isolated cosmetic fixture: no LocalGameUI, profile, PlayerPrefs writes or match start.
        scene = SceneManager.CreateScene("Nation scenery fixture");
        root = new GameObject("Nation fixture"); SceneManager.MoveGameObjectToScene(root, scene);
        board = root.AddComponent<BoardManager>(); mapper = root.AddComponent<BoardWorldMapper>();
        mapper.Configure(board, root.transform, new Vector2(1.4f, -1.2f), new Vector2(-3.5f, 5.4f));
        var cameraObject = new GameObject("Nation fixture camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.transform.SetParent(root.transform); camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 12.2f; camera.aspect = 540f / 960;
        camera.transform.position = new Vector3(0, 0, -10); camera.enabled = false;
        var frame = new GameObject("table-frame", typeof(SpriteRenderer)); SceneManager.MoveGameObjectToScene(frame, scene);
        frame.transform.SetParent(root.transform); frame.transform.localScale = new Vector3(10.4f, 14, 1);
        frameSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
        frame.GetComponent<SpriteRenderer>().sprite = frameSprite;
        arena = root.AddComponent<CardTableArena>();
        SetField("board", board); SetField("mapper", mapper); SetField("arenaCamera", camera); SetField("applied", true);
        scenery = root.AddComponent<NationArenaScenery>();
        yield return null;
    }
    void SetField(string name, object value) => typeof(CardTableArena).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena, value);
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (root != null) Object.Destroy(root);
        if (frameSprite != null) Object.Destroy(frameSprite);
        yield return null;
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest] public IEnumerator NineOriginalEnvironmentsKeepRealCellCentersAndNeverInterceptBoardInput()
    {
        Vector2 cellSize = mapper.CellSize, offset = mapper.BoardOffset;
        Vector3 cameraPosition = camera.transform.position; float size = camera.orthographicSize;
        string preference = PlayerPrefs.GetString(CardTableTheme.PreferenceKey, "");
        foreach (string id in new[] { "naturalis", "aquarion", "geothar", "volkanar", "royal", "casino-night", "lounge", "classic", "aireola" })
        {
            var art = Resources.Load<NationBoardArtwork>("MonsterPouch/Styles/Nations/" + id);
            Assert.IsNotNull(art, id); Assert.AreEqual(id, art.NationId);
            Assert.IsNotNull(art.Miniature); Assert.IsNotNull(art.LobbyBackground);
            Assert.IsNotNull(art.CombatPanorama);
            Assert.IsNotNull(art.CombatPortrait, id + " needs a composition that retains its environment on phones.");
            Assert.AreNotSame(art.CombatPanorama.texture, art.Miniature.texture, "The supplied miniature is distinct from combat artwork.");
            Assert.AreSame(art.CombatPanorama.texture, art.LobbyBackground.texture, "The lobby crop must not duplicate the bitmap.");
            scenery.Configure(arena, camera, art);
            if (id == "naturalis") Assert.IsFalse(scenery.IsVisible, "Configure must not activate a map on its own.");
            scenery.SetVisible(true); yield return null;
            Assert.AreEqual(60, scenery.TileCount);
            Assert.IsTrue(scenery.UsesContinuousArtwork);
            Assert.IsEmpty(scenery.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(scenery.GetComponentsInChildren<Collider2D>());
            Assert.IsEmpty(scenery.GetComponentsInChildren<UnityEngine.UI.Graphic>());
            Assert.AreEqual(2, scenery.GetComponentsInChildren<MeshRenderer>().Length);
            foreach (bool flipped in new[] { false, true })
            {
                mapper.Flipped = flipped;
                AssertOpaqueGrid(scenery, arena, mapper);
                AssertPaintedApronIsOutsidePlay(id);
            }
            mapper.Flipped = false;
            var environment = scenery.GetComponentsInChildren<MeshFilter>().Single(r => r.name == "Nation continuous environment");
            Assert.IsTrue(scenery.UsesPaintedGrid);
            Assert.AreEqual(0, art.GridLine.a, "Do not draw a second grid over the original.");
            Assert.IsFalse(scenery.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name.StartsWith("Nation grid ")));
            AssertPaintedIntersectionsMatchLogicalCorners(environment, art);
            var active = scenery.ActiveCombatArtwork;
            Assert.AreSame(Screen.width < Screen.height ? art.CombatPortrait : art.CombatPanorama, active);
            Assert.AreEqual(cellSize, mapper.CellSize); Assert.AreEqual(offset, mapper.BoardOffset);
            AssertPaintedApronIsOutsidePlay(id);
            Assert.AreEqual(cameraPosition, camera.transform.position); Assert.AreEqual(size, camera.orthographicSize);
            Assert.AreEqual(preference, PlayerPrefs.GetString(CardTableTheme.PreferenceKey, ""));
        }
        Assert.AreNotSame(Resources.Load<NationBoardArtwork>("MonsterPouch/Styles/Nations/aireola").CombatPanorama,
            Resources.Load<NationBoardArtwork>("MonsterPouch/Styles/Nations/royal").CombatPanorama);
    }

    void AssertPaintedIntersectionsMatchLogicalCorners(MeshFilter environment, NationBoardArtwork art)
    {
        var mesh = environment.sharedMesh; var vertices = mesh.vertices; var uv = mesh.uv;
        Assert.AreEqual(77, art.PaintedGrid.Length);
        Assert.AreEqual(165, vertices.Length, "The full original is one connected mesh, including its border.");
        var sprite = scenery.ActiveCombatArtwork; var source = sprite.textureRect;
        var logical = arena.BoardPortraitBounds;
        for (int row = 0; row <= 10; row++) for (int column = 0; column <= 6; column++)
        {
            int index = (row + 2) * 11 + column + 2;
            var actual = arena.PortraitPoint(environment.transform.TransformPoint(vertices[index]));
            var expected = new Vector2(logical.xMin + column * logical.width / 6, logical.yMin + row * logical.height / 10);
            Assert.Less(Vector2.Distance(expected, actual), .0001f, art.NationId + " painted crossing " + column + "," + row);
            var pixel = new Vector2((uv[index].x * sprite.texture.width - source.x) / source.width,
                (source.yMax - uv[index].y * sprite.texture.height) / source.height);
            Assert.Less(Vector2.Distance(art.PaintedGrid[row * 7 + column], pixel), .000001f);
        }
        AssertOpaqueGrid(scenery, arena, mapper);
    }

    public static void AssertOpaqueGrid(NationArenaScenery scenery, CardTableArena arena, BoardWorldMapper mapper)
    {
        var pads = scenery.GetComponentsInChildren<MeshFilter>().Where(r => r.name == "Nation uniform playable cells").ToArray();
        Assert.AreEqual(1, pads.Length, "One opaque mesh for all playable cells.");
        Assert.IsFalse(scenery.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name.StartsWith("Nation tile ")));
        var pad = pads[0]; var mesh = pad.sharedMesh; var vertices = mesh.vertices;
        Assert.AreEqual(60 * 16, vertices.Length); Assert.AreEqual(60 * 54, mesh.triangles.Length);
        Assert.IsTrue(mesh.colors.All(c => c.a == 1), "No baked checkerboard may show through.");
        Assert.AreEqual(18, pad.GetComponent<MeshRenderer>().sortingOrder);
        Assert.AreSame(Texture2D.whiteTexture, pad.GetComponent<MeshRenderer>().sharedMaterial.mainTexture);
        var bounds = arena.BoardWorldBounds;
        var mappedCells = new System.Collections.Generic.HashSet<BoardCell>();
        for (int tile = 0; tile < 60; tile++)
        {
            var world = vertices.Skip(tile * 16).Take(16).Select(pad.transform.TransformPoint).ToArray();
            float left = world.Min(p => p.x), right = world.Max(p => p.x);
            float bottom = world.Min(p => p.y), top = world.Max(p => p.y);
            Assert.That(left, Is.GreaterThanOrEqualTo(bounds.xMin - .00001f));
            Assert.That(right, Is.LessThanOrEqualTo(bounds.xMax + .00001f));
            Assert.That(bottom, Is.GreaterThanOrEqualTo(bounds.yMin - .00001f));
            Assert.That(top, Is.LessThanOrEqualTo(bounds.yMax + .00001f));
            Assert.That(right - left, Is.EqualTo(Mathf.Abs(mapper.CellSize.x)).Within(.00001f));
            Assert.That(top - bottom, Is.EqualTo(Mathf.Abs(mapper.CellSize.y)).Within(.00001f));
            var center = new Vector3((left + right) * .5f, (top + bottom) * .5f);
            Assert.IsTrue(mapper.TryGetCell(center, out var cell)); Assert.IsTrue(mappedCells.Add(cell));
            Assert.Less(Vector3.Distance(center, mapper.GetWorldPosition(cell)), .00001f);
            float area = 0; var triangles = mesh.triangles;
            for (int i = tile * 54; i < (tile + 1) * 54; i += 3)
            {
                var a = pad.transform.TransformPoint(vertices[triangles[i]]);
                var b = pad.transform.TransformPoint(vertices[triangles[i + 1]]);
                var c = pad.transform.TransformPoint(vertices[triangles[i + 2]]);
                float signed = Vector3.Cross(b - a, c - a).z;
                Assert.Greater(signed, 0, "No folded or degenerate tile triangles.");
                area += signed * .5f;
            }
            Assert.That(area, Is.EqualTo((right - left) * (top - bottom)).Within(.00001f));
            if (tile % 6 < 5)
                for (int row = 0; row < 4; row++)
                    Assert.AreEqual(vertices[tile * 16 + row * 4 + 3], vertices[(tile + 1) * 16 + row * 4], "Shared column edge has no gap.");
            if (tile / 6 < 9)
                for (int column = 0; column < 4; column++)
                    Assert.AreEqual(vertices[tile * 16 + 12 + column], vertices[(tile + 6) * 16 + column], "Shared row edge has no gap.");
        }
        Assert.AreEqual(60, mappedCells.Count);
        Assert.That(pad.GetComponent<MeshRenderer>().bounds.min.x, Is.EqualTo(bounds.xMin).Within(.00001f));
        Assert.That(pad.GetComponent<MeshRenderer>().bounds.max.x, Is.EqualTo(bounds.xMax).Within(.00001f));
        Assert.That(pad.GetComponent<MeshRenderer>().bounds.min.y, Is.EqualTo(bounds.yMin).Within(.00001f));
        Assert.That(pad.GetComponent<MeshRenderer>().bounds.max.y, Is.EqualTo(bounds.yMax).Within(.00001f));
    }

    void AssertPaintedApronIsOutsidePlay(string id)
    {
        // The continuous painting extends past the grid. Those extra strips never
        // become partial rows/columns or selectable cells, including near corners.
        var bounds = arena.BoardWorldBounds;
        float inset = Mathf.Min(Mathf.Abs(mapper.CellSize.x), Mathf.Abs(mapper.CellSize.y)) * .01f;
        for (int column = 0; column < BoardManager.Width; column++)
        {
            float x = bounds.xMin + (column + .5f) * bounds.width / BoardManager.Width;
            Assert.IsTrue(mapper.TryGetCell(new Vector3(x, bounds.yMin + inset), out _), id + " bottom playable edge");
            Assert.IsTrue(mapper.TryGetCell(new Vector3(x, bounds.yMax - inset), out _), id + " top playable edge");
            Assert.IsFalse(mapper.TryGetCell(new Vector3(x, bounds.yMin - inset), out _), id + " bottom decorative apron");
            Assert.IsFalse(mapper.TryGetCell(new Vector3(x, bounds.yMax + inset), out _), id + " top decorative apron");
        }
        for (int row = 0; row < BoardManager.Height; row++)
        {
            float y = bounds.yMin + (row + .5f) * bounds.height / BoardManager.Height;
            Assert.IsTrue(mapper.TryGetCell(new Vector3(bounds.xMin + inset, y), out _), id + " left playable edge");
            Assert.IsTrue(mapper.TryGetCell(new Vector3(bounds.xMax - inset, y), out _), id + " right playable edge");
            Assert.IsFalse(mapper.TryGetCell(new Vector3(bounds.xMin - inset, y), out _), id + " left decorative apron");
            Assert.IsFalse(mapper.TryGetCell(new Vector3(bounds.xMax + inset, y), out _), id + " right decorative apron");
        }
    }

    [UnityTest] public IEnumerator ReconfigureAndCameraViewportChangesPreserveTheRegisteredComposition()
    {
        var art = Resources.Load<NationBoardArtwork>("MonsterPouch/Styles/Nations/naturalis");
        scenery.Configure(arena, camera, art); scenery.SetVisible(true);
        var originalTiles = scenery.GetComponentsInChildren<MeshFilter>().Select(r => r.GetInstanceID()).ToArray();
        var originalGrid = arena.BoardPortraitBounds;
        scenery.Configure(arena, camera, art);
        CollectionAssert.AreEqual(originalTiles, scenery.GetComponentsInChildren<MeshFilter>().Select(r => r.GetInstanceID()).ToArray());
        var originalMesh = scenery.GetComponentsInChildren<MeshFilter>().Single(r => r.name == "Nation continuous environment").sharedMesh;
        var originalVertices = originalMesh.vertices;
        camera.aspect = 16f / 9; yield return null;
        Assert.AreEqual(originalGrid, arena.BoardPortraitBounds); Assert.AreEqual(60, scenery.TileCount);
        var mesh = scenery.GetComponentsInChildren<MeshFilter>().Single(r => r.name == "Nation continuous environment").sharedMesh;
        var active = scenery.ActiveCombatArtwork;
        var source = active.textureRect; var tex = active.texture;
        foreach (Vector2 uv in mesh.uv)
        {
            Assert.That(uv.x, Is.InRange(source.xMin / tex.width, source.xMax / tex.width));
            Assert.That(uv.y, Is.InRange(source.yMin / tex.height, source.yMax / tex.height));
        }
        Assert.Greater(mesh.bounds.size.x, arena.BoardWorldBounds.width);
        Assert.Greater(mesh.bounds.size.y, arena.BoardWorldBounds.height);
        Assert.AreSame(Screen.width < Screen.height ? art.CombatPortrait : art.CombatPanorama, active,
            "The desktop game viewport also has portrait dimensions; only the display selects the composition.");
        CollectionAssert.AreEqual(originalVertices, mesh.vertices, "Widening the camera must not move or distort painted corners.");
        camera.aspect = 32f / 9; yield return null;
        CollectionAssert.AreEqual(originalVertices, mesh.vertices, "Ultrawide uses outer atmosphere instead of enlarging the courtyard.");
        foreach(var viewport in new[]{new Rect(.341796875f,0,.31640625f,1),new Rect(0,.09f,1,.82f),new Rect(.42f,0,.16f,1)})
        {
            camera.rect=viewport;yield return null;
            CollectionAssert.AreEqual(originalVertices,mesh.vertices,"Viewport changes must not stretch ANY painted vertex, including the outside strips.");
            AssertPaintedIntersectionsMatchLogicalCorners(scenery.GetComponentsInChildren<MeshFilter>().Single(r => r.name == "Nation continuous environment"),art);
            Assert.AreEqual(originalGrid,arena.BoardPortraitBounds);
        }
        scenery.SetVisible(false); Assert.IsFalse(scenery.IsVisible);
        scenery.Configure(arena, camera, art); Assert.IsFalse(scenery.IsVisible);
        scenery.SetVisible(true); Assert.IsTrue(scenery.IsVisible);
    }
}

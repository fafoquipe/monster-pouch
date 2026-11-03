using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

// A canvas-only editor preview. No scene is created or changed, and no game or profile callback is called.
public sealed class NationBoardArtworkPreview : EditorWindow
{
    public const string DefaultAssetPath = "Assets/resources/MonsterPouch/Styles/Nations/naturalis.asset";
    const float PortraitWidth = CardTableArena.PortraitWidth;
    const float PortraitHeight = CardTableArena.PortraitHeight;
    [SerializeField] NationBoardArtwork artwork;
    [SerializeField] Rect boardBounds, frameBounds;
    [SerializeField] bool hasBounds, exactLiveBounds, landscape, outlines;
    [SerializeField] int previewMode;
    Vector2 scroll;

    [MenuItem("Monster Pouch/Art review/Naturalis board preview")]
    public static void Open()
    {
        var window = GetWindow<NationBoardArtworkPreview>("Naturalis · revisión");
        window.minSize = new Vector2(380, 460);
        window.Show();
    }

    void OnEnable()
    {
        if (artwork == null) artwork = AssetDatabase.LoadAssetAtPath<NationBoardArtwork>(DefaultAssetPath);
        if (!hasBounds) ResetReferenceBounds();
    }

    // Read-only: does not apply a theme, initialize an arena, or alter the camera.
    public static bool TryReadCurrentFrame(out Rect board, out Rect frame)
    {
        board = frame = default;
        foreach (var arena in Object.FindObjectsByType<CardTableArena>(FindObjectsSortMode.None))
        {
            if (!arena.IsApplied || !arena.gameObject.scene.IsValid()) continue;
            var candidateBoard = arena.BoardPortraitBounds;
            var candidateFrame = arena.FramePortraitBounds;
            if (!ValidBounds(candidateBoard, candidateFrame)) continue;
            board = candidateBoard; frame = candidateFrame;
            return true;
        }
        return false;
    }

    public static bool ValidBounds(Rect board, Rect frame) =>
        Finite(board.x) && Finite(board.y) && Finite(board.width) && Finite(board.height) &&
        Finite(frame.x) && Finite(frame.y) && Finite(frame.width) && Finite(frame.height) &&
        board.width > 0 && board.height > 0 && frame.xMin < board.xMin && frame.yMin < board.yMin &&
        frame.xMax > board.xMax && frame.yMax > board.yMax;
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    void ResetReferenceBounds()
    {
        // Source: main-scene mapper cellSize (1.2774138, -1.1745076), board-origin Y=1.3350009,
        // boardOffset.Y=5.160671; CardTableArena uses 12.2 orthographic size, camera Y=.55,
        // and horizontal expansion 1.10. The frame uses the documented (50,84)-(394,558)
        // / inner (96,126)-(350,517) reference ratios. It is a reference, not a live measurement.
        float unit = 24.4f / PortraitHeight;
        float width = 1.2774138f * CardTableArena.HorizontalExpansion * BoardManager.Width / unit;
        float height = 1.1745076f * BoardManager.Height / unit;
        float centerY = PortraitHeight * .5f - (1.3350009f + 5.160671f - 1.1745076f * (BoardManager.Height - 1) * .5f - .55f) / unit;
        boardBounds = new Rect((PortraitWidth - width) * .5f, centerY - height * .5f, width, height);
        float sx = width / 254f, sy = height / 391f;
        frameBounds = Rect.MinMaxRect(boardBounds.xMin - 46f * sx, boardBounds.yMin - 42f * sy,
            boardBounds.xMax + 44f * sx, boardBounds.yMax + 41f * sy);
        hasBounds = true; exactLiveBounds = false;
    }

    void OnGUI()
    {
        artwork = (NationBoardArtwork)EditorGUILayout.ObjectField("Arte de revisión", artwork, typeof(NationBoardArtwork), false);
        previewMode = GUILayout.Toolbar(previewMode, new[] { "Tablero", "Minimapa", "Lobby" });
        if (previewMode != 0) { DrawIllustration(); return; }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copiar encuadre actual"))
        {
            if (TryReadCurrentFrame(out var board, out var frame))
            { boardBounds = board; frameBounds = frame; hasBounds = exactLiveBounds = true; }
            else ShowNotification(new GUIContent("No hay un CardTableArena aplicado. No se ha iniciado ni modificado ninguna escena."));
        }
        if (GUILayout.Button("Restablecer referencia")) ResetReferenceBounds();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        landscape = GUILayout.Toggle(landscape, "PC panorámico", "Button");
        outlines = GUILayout.Toggle(outlines, "Ver límites y celdas", "Button");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(exactLiveBounds ?
            "Encuadre copiado de la arena actual. Vista de arte aislada: no activa mapa, cuenta, copas, personajes ni red." :
            "Encuadre de referencia derivado de la escena. Copia el encuadre actual antes de aprobar alineación final. Esta ventana no inicia el juego.",
            exactLiveBounds ? MessageType.Info : MessageType.Warning);
        if (artwork == null)
        {
            EditorGUILayout.HelpBox("Asigna NaturalisBoard.asset para revisar las piezas. El tablero se construye con 60 casillas; el fondo no debe llevar una rejilla pintada.", MessageType.Info);
            return;
        }
        if (!ValidBounds(boardBounds, frameBounds)) { EditorGUILayout.HelpBox("Límites de tablero o marco inválidos.", MessageType.Error); return; }
        if (artwork.TileTexture == null || artwork.HorizontalFrame == null || artwork.VerticalFrame == null)
        {
            EditorGUILayout.HelpBox("Piezas de combate pendientes. Puedes revisar el minimapa y el lobby en sus pestañas.", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField($"{BoardManager.Width} × {BoardManager.Height} · tablero {boardBounds.width:F2} × {boardBounds.height:F2} · marco {frameBounds.width:F2} × {frameBounds.height:F2}", EditorStyles.miniLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        float designWidth = landscape ? PortraitHeight * 16f / 9f : PortraitWidth;
        float availableWidth = Mathf.Max(100, position.width - 30);
        float scale = Mathf.Min(1, availableWidth / designWidth);
        Rect canvas = GUILayoutUtility.GetRect(designWidth * scale, PortraitHeight * scale, GUILayout.ExpandWidth(false));
        if (Event.current.type == EventType.Repaint) DrawCanvas(canvas, designWidth, scale);
        EditorGUILayout.EndScrollView();
    }

    void DrawIllustration()
    {
        EditorGUILayout.HelpBox("Vista de arte aislada. No modifica escenas, selección de mapa ni cuenta.", MessageType.Info);
        if (artwork == null) { EditorGUILayout.HelpBox("Selecciona un asset de nación.", MessageType.Info); return; }
        Sprite sprite = previewMode == 1 ? artwork.Miniature : artwork.LobbyBackground;
        if (sprite == null) { EditorGUILayout.HelpBox("Esta imagen está pendiente.", MessageType.Info); return; }
        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(artwork.DisplayName) ? artwork.name : artwork.DisplayName, EditorStyles.boldLabel);
        float width = Mathf.Min(sprite.rect.width, Mathf.Max(100, position.width - 30));
        float height = width * sprite.rect.height / sprite.rect.width;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Rect canvas = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
        if (Event.current.type == EventType.Repaint)
        {
            GUI.BeginClip(canvas);
            const float tile = 18;
            for (int row = 0; row * tile < canvas.height; row++)
                for (int column = 0; column * tile < canvas.width; column++)
                    EditorGUI.DrawRect(new Rect(column * tile, row * tile, tile, tile),
                        (row + column) % 2 == 0 ? new Color(.24f, .26f, .27f) : new Color(.29f, .31f, .32f));
            DrawSprite(new Rect(0, 0, canvas.width, canvas.height), sprite, Color.white);
            GUI.EndClip();
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawCanvas(Rect canvas, float designWidth, float scale)
    {
        GUI.BeginClip(canvas);
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        // Same vertical scale in both formats. Wide displays reveal additional side scenery.
        float offset = (designWidth - PortraitWidth) * .5f;
        Rect grid = Shift(boardBounds, offset), frame = Shift(frameBounds, offset);
        Rect world = new Rect(0, 0, designWidth, PortraitHeight);
        EditorGUI.DrawRect(world, new Color(.20f, .30f, .24f));
        DrawTiled(world, artwork.FloorTexture, Mathf.Max(16, artwork.FloorTileWidth), artwork.FloorTint);
        if (artwork.Backdrop != null)
        {
            float width = Mathf.Max(16, artwork.BackdropWidth);
            float height = width * artwork.Backdrop.rect.height / artwork.Backdrop.rect.width;
            DrawSprite(new Rect(frame.center.x - width * .5f, frame.yMin - height, width, height), artwork.Backdrop, Color.white);
        }
        EditorGUI.DrawRect(new Rect(frame.x + 5, frame.y + 7, frame.width, frame.height), new Color(0, 0, 0, .18f));
        var top = Rect.MinMaxRect(frame.xMin, frame.yMin, frame.xMax, grid.yMin);
        var bottom = Rect.MinMaxRect(frame.xMin, grid.yMax, frame.xMax, frame.yMax);
        var left = Rect.MinMaxRect(frame.xMin, grid.yMin, grid.xMin, grid.yMax);
        var right = Rect.MinMaxRect(grid.xMax, grid.yMin, frame.xMax, grid.yMax);
        DrawSprite(top, artwork.HorizontalFrame, artwork.FrameTint);
        DrawSprite(bottom, artwork.HorizontalFrame, artwork.FrameTint);
        DrawSprite(left, artwork.VerticalFrame, artwork.FrameTint);
        DrawSprite(right, artwork.VerticalFrame, artwork.FrameTint);
        for (int row = 0; row < BoardManager.Height; row++)
            for (int column = 0; column < BoardManager.Width; column++)
            {
                Rect cell = new Rect(grid.x + column * grid.width / BoardManager.Width, grid.y + row * grid.height / BoardManager.Height,
                    grid.width / BoardManager.Width, grid.height / BoardManager.Height);
                DrawSprite(cell, artwork.TileTexture, (row + column) % 2 == 0 ? artwork.Light : artwork.Dark);
                if (outlines) Outline(cell, new Color(1, 1, 1, .32f), .65f);
            }
        if (artwork.FlowerCluster != null)
        {
            float width = Mathf.Max(8, artwork.FlowerWidth), height = width * artwork.FlowerCluster.rect.height / artwork.FlowerCluster.rect.width;
            DrawSprite(new Rect(frame.xMin - width - 8, frame.yMin - height * .3f, width, height), artwork.FlowerCluster, Color.white);
            DrawSprite(new Rect(frame.xMax + 8, frame.yMax - height, width, height), artwork.FlowerCluster, Color.white);
        }
        if (outlines) { Outline(grid, Color.cyan, 2); Outline(frame, Color.yellow, 2); }
        GUI.matrix = oldMatrix;
        GUI.EndClip();
    }

    static Rect Shift(Rect value, float x) => new Rect(value.x + x, value.y, value.width, value.height);
    static void DrawSprite(Rect destination, Sprite sprite, Color tint)
    {
        if (sprite == null) { EditorGUI.DrawRect(destination, tint); return; }
        var old = GUI.color; GUI.color = tint;
        Rect source = sprite.textureRect;
        var uv = new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height, source.width / sprite.texture.width, source.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(destination, sprite.texture, uv, true);
        GUI.color = old;
    }
    static void DrawTiled(Rect destination, Sprite sprite, float width, Color tint)
    {
        if (sprite == null) return;
        float height = width * sprite.rect.height / sprite.rect.width;
        // Each tile uses its own atlas UVs, so an atlas never repeats neighboring pieces.
        for (float y = destination.y; y < destination.yMax; y += height)
            for (float x = destination.x; x < destination.xMax; x += width)
                DrawSprite(new Rect(x, y, width, height), sprite, tint);
    }
    static void Outline(Rect bounds, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(bounds.x, bounds.y, bounds.width, thickness), color);
        EditorGUI.DrawRect(new Rect(bounds.x, bounds.yMax - thickness, bounds.width, thickness), color);
        EditorGUI.DrawRect(new Rect(bounds.x, bounds.y, thickness, bounds.height), color);
        EditorGUI.DrawRect(new Rect(bounds.xMax - thickness, bounds.y, thickness, bounds.height), color);
    }
}

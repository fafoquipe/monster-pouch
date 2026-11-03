using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Cosmetic only. All cells use the active arena's existing mapper and camera framing.
    public sealed class NationArenaScenery : MonoBehaviour
    {
        CardTableArena arena;
        Camera view;
        NationBoardArtwork artwork;
        GameObject drawingRoot, floorObject;
        Mesh floorMesh, cellMesh;
        Sprite solid;
        readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();
        float lastAspect, lastDisplayAspect;
        Rect lastViewport;
        public bool IsVisible => drawingRoot != null && drawingRoot.activeSelf;
        public Rect TilePortraitBounds { get; private set; }
        public Rect FramePortraitBounds { get; private set; }
        public NationBoardArtwork Artwork => artwork;
        public int TileCount { get; private set; }
        public bool UsesContinuousArtwork => artwork != null && artwork.CombatPanorama != null;
        public Sprite ActiveCombatArtwork { get; private set; }
        public bool UsesPortraitArtwork => artwork != null && artwork.CombatPortrait != null && lastDisplayAspect < 1 && ActiveCombatArtwork == artwork.CombatPortrait;
        public bool UsesPaintedGrid => artwork != null && artwork.PaintedGrid != null && artwork.PaintedGrid.Length > 0;

        public void Configure(CardTableArena owner, Camera camera, NationBoardArtwork data)
        {
            if (owner == null || camera == null || data == null) throw new ArgumentNullException("Nation scenery requires an existing arena, camera and artwork.");
            var grid = owner.BoardPortraitBounds;
            var frame = owner.FramePortraitBounds;
            if (!owner.IsApplied || grid.width <= 0 || grid.height <= 0 ||
                frame.xMin >= grid.xMin || frame.yMin >= grid.yMin || frame.xMax <= grid.xMax || frame.yMax <= grid.yMax)
                throw new InvalidOperationException("Apply the shared arena layout before configuring nation scenery.");
            if (data.CombatPanorama == null && (data.FloorTexture == null || data.TileTexture == null || data.HorizontalFrame == null || data.VerticalFrame == null))
                throw new InvalidOperationException("Nation combat artwork is incomplete: " + data.NationId);
            if (data.CombatPanorama != null && (data.PlayableArea.width <= 0 || data.PlayableArea.height <= 0 ||
                data.PlayableArea.xMin < 0 || data.PlayableArea.yMin < 0 || data.PlayableArea.xMax > 1 || data.PlayableArea.yMax > 1))
                throw new InvalidOperationException("Nation panorama courtyard is outside its image: " + data.NationId);
            if (data.CombatPortrait != null && (data.PortraitPlayableArea.width <= 0 || data.PortraitPlayableArea.height <= 0 ||
                data.PortraitPlayableArea.xMin < 0 || data.PortraitPlayableArea.yMin < 0 || data.PortraitPlayableArea.xMax > 1 || data.PortraitPlayableArea.yMax > 1))
                throw new InvalidOperationException("Nation portrait courtyard is outside its image: " + data.NationId);
            ValidatePaintedGrid(data);
            if (arena == owner && view == camera && artwork == data && drawingRoot != null &&
                TilePortraitBounds == grid && FramePortraitBounds == frame) return;
            bool visible = IsVisible;
            Clear();
            arena = owner; view = camera; artwork = data;
            TilePortraitBounds = grid; FramePortraitBounds = frame;
            drawingRoot = new GameObject(data.DisplayName + " combat scenery");
            drawingRoot.transform.SetParent(transform, false);
            drawingRoot.SetActive(false);
            solid = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), 100);
            BuildFloor();
            if (data.CombatPanorama != null)
            {
                // Panoramic monitors beyond the authored 16:9 reveal this quiet atmosphere;
                // never enlarge the scene or move the board to fill an arbitrary display.
                Draw("Nation outer atmosphere", solid, new Rect(-4500, -2200, 9540, 5360), data.FloorTint, 16);
                BuildCellOverlays();
                drawingRoot.SetActive(visible);
                return;
            }
            ShadowOutsideBoard(new Rect(frame.x + 5, frame.y + 7, frame.width, frame.height), .23f, 8);
            Draw("Nation top rail", data.HorizontalFrame, Rect.MinMaxRect(frame.xMin, frame.yMin, frame.xMax, grid.yMin), data.FrameTint, 12);
            Draw("Nation bottom rail", data.HorizontalFrame, Rect.MinMaxRect(frame.xMin, grid.yMax, frame.xMax, frame.yMax), data.FrameTint, 12);
            Draw("Nation left rail", data.VerticalFrame, Rect.MinMaxRect(frame.xMin, grid.yMin, grid.xMin, grid.yMax), data.FrameTint, 12);
            Draw("Nation right rail", data.VerticalFrame, Rect.MinMaxRect(grid.xMax, grid.yMin, frame.xMax, grid.yMax), data.FrameTint, 12);
            Draw("Nation board backing", solid, grid, data.Dark, 17);
            for (int row = 0; row < BoardManager.Height; row++)
                for (int column = 0; column < BoardManager.Width; column++)
                {
                    Rect cell = new Rect(grid.x + column * grid.width / BoardManager.Width, grid.y + row * grid.height / BoardManager.Height,
                        grid.width / BoardManager.Width, grid.height / BoardManager.Height);
                    Draw("Nation tile " + column + " " + row, data.TileTexture, cell, (row + column) % 2 == 0 ? data.Light : data.Dark, 18);
                    TileCount++;
                }
            if (data.Backdrop != null)
            {
                float width = Mathf.Max(16, data.BackdropWidth);
                float height = width * data.Backdrop.rect.height / data.Backdrop.rect.width;
                Rect rect = new Rect(frame.center.x - width * .5f, frame.yMin - height, width, height);
                ShadowOutsideBoard(new Rect(rect.x + 4, rect.yMax - 5, rect.width, 12), .20f, 3);
                Draw("Nation backdrop", data.Backdrop, rect, Color.white, 5);
            }
            if (data.FlowerCluster != null)
            {
                float width = Mathf.Max(8, data.FlowerWidth), height = width * data.FlowerCluster.rect.height / data.FlowerCluster.rect.width;
                var a = new Rect(frame.xMin - width - 8, frame.yMin - height * .3f, width, height);
                var b = new Rect(frame.xMax + 8, frame.yMax - height, width, height);
                ShadowOutsideBoard(new Rect(a.x + 3, a.yMax - 3, width, 8), .18f, 3);
                ShadowOutsideBoard(new Rect(b.x + 3, b.yMax - 3, width, 8), .18f, 3);
                Draw("Nation left decoration", data.FlowerCluster, a, Color.white, 5);
                Draw("Nation right decoration", data.FlowerCluster, b, Color.white, 5);
            }
            drawingRoot.SetActive(visible);
        }

        public void SetVisible(bool visible) { if (drawingRoot != null) drawingRoot.SetActive(visible); }
        void LateUpdate()
        {
            if (drawingRoot != null && view != null &&
                (!Mathf.Approximately(lastAspect, view.aspect) || !Mathf.Approximately(lastDisplayAspect, DisplayAspect) || lastViewport != view.rect)) BuildFloor();
        }

        void BuildFloor()
        {
            lastViewport = view.rect;
            if (artwork.CombatPanorama != null) { BuildPanorama(); return; }
            if (floorObject == null)
            {
                floorObject = new GameObject("Nation repeating floor", typeof(MeshFilter), typeof(MeshRenderer));
                floorObject.transform.SetParent(drawingRoot.transform, false);
                floorMesh = new Mesh { name = "Nation atlas-safe repeating floor" };
                floorObject.GetComponent<MeshFilter>().sharedMesh = floorMesh;
                var renderer = floorObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = MaterialFor(artwork.FloorTexture.texture); renderer.sortingOrder = 2;
            }
            lastAspect = view.aspect;
            lastDisplayAspect = DisplayAspect;
            float width = Mathf.Max(16, artwork.FloorTileWidth);
            float height = width * artwork.FloorTexture.rect.height / artwork.FloorTexture.rect.width;
            float visibleWidth = Mathf.Max(CardTableArena.PortraitWidth, CardTableArena.PortraitHeight * Mathf.Max(.1f, view.aspect));
            float minX = Mathf.Floor((270 - visibleWidth * .5f - width) / width) * width;
            float maxX = 270 + visibleWidth * .5f + width;
            const float minY = -160, maxY = CardTableArena.PortraitHeight + 160;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>(); var colors = new List<Color>();
            Rect source = artwork.FloorTexture.textureRect;
            float tw = artwork.FloorTexture.texture.width, th = artwork.FloorTexture.texture.height;
            Vector2 lower = new Vector2((source.xMin + .5f) / tw, (source.yMin + .5f) / th);
            Vector2 upper = new Vector2((source.xMax - .5f) / tw, (source.yMax - .5f) / th);
            for (float y = minY; y < maxY; y += height)
                for (float x = minX; x < maxX; x += width)
                {
                    int start = vertices.Count;
                    vertices.Add(floorObject.transform.InverseTransformPoint(Point(new Vector2(x, y + height))));
                    vertices.Add(floorObject.transform.InverseTransformPoint(Point(new Vector2(x + width, y + height))));
                    vertices.Add(floorObject.transform.InverseTransformPoint(Point(new Vector2(x + width, y))));
                    vertices.Add(floorObject.transform.InverseTransformPoint(Point(new Vector2(x, y))));
                    uv.Add(lower); uv.Add(new Vector2(upper.x, lower.y)); uv.Add(upper); uv.Add(new Vector2(lower.x, upper.y));
                    for (int i = 0; i < 4; i++) colors.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? artwork.FloorTint.linear : artwork.FloorTint);
                    indices.Add(start); indices.Add(start + 1); indices.Add(start + 2); indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
                }
            floorMesh.Clear(); floorMesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            floorMesh.SetVertices(vertices); floorMesh.SetUVs(0, uv); floorMesh.SetColors(colors); floorMesh.SetTriangles(indices, 0); floorMesh.RecalculateBounds();
        }

        void BuildCellOverlays()
        {
            if (UsesPaintedGrid) { BuildOpaquePaintedCells(); return; }
            Rect grid = TilePortraitBounds;
            float width = grid.width / BoardManager.Width, height = grid.height / BoardManager.Height;
            for (int row = 0; row < BoardManager.Height; row++)
                for (int column = 0; column < BoardManager.Width; column++)
                {
                    var cell = new Rect(grid.x + column * width, grid.y + row * height, width, height);
                    Draw("Nation tile " + column + " " + row, solid, cell,
                        (row + column) % 2 == 0 ? artwork.Light : artwork.Dark, 18);
                    TileCount++;
                }
            float line = artwork.GridLineWidth;
            if (artwork.GridLine.a <= 0) return; // The supplied originals already contain their cell boundaries.
            for (int column = 0; column <= BoardManager.Width; column++)
                Draw("Nation grid column " + column, solid,
                    new Rect(grid.x + column * width - line * .5f, grid.y, line, grid.height), artwork.GridLine, 19);
            for (int row = 0; row <= BoardManager.Height; row++)
                Draw("Nation grid row " + row, solid,
                    new Rect(grid.x, grid.y + row * height - line * .5f, grid.width, line), artwork.GridLine, 19);
        }

        void BuildOpaquePaintedCells()
        {
            // World bounds come from the real mapper's first/last centers plus half cells.
            // Stay in world space so camera pan/zoom/aspect fit cannot slide this pad.
            // Reversing both axes of the even 6x10 grid preserves checker parity.
            Rect grid = arena.BoardWorldBounds;
            int columns = BoardManager.Width, rows = BoardManager.Height;
            float width = grid.width / columns, height = grid.height / rows;
            float inset = Mathf.Min(width, height) * .008f;
            var item = new GameObject("Nation uniform playable cells", typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(drawingRoot.transform, false);
            cellMesh = new Mesh { name = "Opaque mapper-aligned playable cells" };
            item.GetComponent<MeshFilter>().sharedMesh = cellMesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(Texture2D.whiteTexture);
            renderer.sortingOrder = 18; // Above the painting (17), below gameplay.
            var vertices = new Vector3[columns * rows * 16];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[columns * rows * 54];
            int next = 0;
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    int start = (row * columns + column) * 16;
                    // Evaluate shared edges identically; never accumulate widths or leave gaps.
                    float left = grid.xMin + column * width;
                    float right = column == columns - 1 ? grid.xMax : grid.xMin + (column + 1) * width;
                    float top = grid.yMax - row * height;
                    float bottom = row == rows - 1 ? grid.yMin : grid.yMax - (row + 1) * height;
                    Color tint = (row + column) % 2 == 0 ? artwork.Light : artwork.Dark;
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            int index = start + y * 4 + x;
                            float px = x == 0 ? left : x == 1 ? left + inset : x == 2 ? right - inset : right;
                            float py = y == 0 ? top : y == 1 ? top - inset : y == 2 ? bottom + inset : bottom;
                            vertices[index] = item.transform.InverseTransformPoint(new Vector3(px, py, 0));
                            uv[index] = new Vector2(.5f, .5f);
                            // Restrained relief, baked into vertex colors: one mesh, no tile objects.
                            float shade = x == 0 || x == 3 || y == 0 || y == 3 ? .88f : y == 1 ? 1.012f : .988f;
                            Color color = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1);
                            colors[index] = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
                        }
                    for (int y = 0; y < 3; y++)
                        for (int x = 0; x < 3; x++)
                        {
                            int a = start + y * 4 + x;
                            triangles[next++] = a; triangles[next++] = a + 4; triangles[next++] = a + 5;
                            triangles[next++] = a; triangles[next++] = a + 5; triangles[next++] = a + 1;
                        }
                }
            cellMesh.vertices = vertices; cellMesh.uv = uv; cellMesh.colors = colors;
            cellMesh.triangles = triangles; cellMesh.RecalculateBounds();
            TileCount = columns * rows;
        }

        void BuildPanorama()
        {
            if (floorObject == null)
            {
                floorObject = new GameObject("Nation continuous environment", typeof(MeshFilter), typeof(MeshRenderer));
                floorObject.transform.SetParent(drawingRoot.transform, false);
                floorMesh = new Mesh { name = "Board anchored undistorted panorama" };
                floorObject.GetComponent<MeshFilter>().sharedMesh = floorMesh;
                floorObject.GetComponent<MeshRenderer>().sortingOrder = 17;
            }
            lastAspect = view.aspect;
            lastDisplayAspect = DisplayAspect;
            // The game camera remains portrait on desktop too. Select by the physical display,
            // not by that camera viewport; a resize changes only the painted environment.
            bool portrait = lastDisplayAspect < 1 && artwork.CombatPortrait != null;
            var sprite = portrait ? artwork.CombatPortrait : artwork.CombatPanorama;
            ActiveCombatArtwork = sprite;
            floorObject.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(sprite.texture);
            var source = sprite.textureRect;
            if (UsesPaintedGrid) { BuildRegisteredArtwork(sprite, source); return; }
            Rect board = TilePortraitBounds, authored = portrait ? artwork.PortraitPlayableArea : artwork.PlayableArea;
            // Both compositions register their courtyard to the existing board. A uniform
            // scale preserves the artwork and never changes cells, actors or the camera.
            float scale = Mathf.Max(board.width / (authored.width * source.width), board.height / (authored.height * source.height));
            float left = board.center.x - authored.center.x * source.width * scale;
            float top = board.center.y - authored.center.y * source.height * scale;
            float right = left + source.width * scale, bottom = top + source.height * scale;
            var vertices = new[] {
                floorObject.transform.InverseTransformPoint(Point(new Vector2(left, bottom))),
                floorObject.transform.InverseTransformPoint(Point(new Vector2(right, bottom))),
                floorObject.transform.InverseTransformPoint(Point(new Vector2(right, top))),
                floorObject.transform.InverseTransformPoint(Point(new Vector2(left, top))) };
            float u0 = (source.xMin + .5f) / sprite.texture.width, u1 = (source.xMax - .5f) / sprite.texture.width;
            float v0 = (source.yMin + .5f) / sprite.texture.height, v1 = (source.yMax - .5f) / sprite.texture.height;
            floorMesh.Clear(); floorMesh.vertices = vertices;
            floorMesh.uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            floorMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            floorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; floorMesh.RecalculateBounds();
        }

        static void ValidatePaintedGrid(NationBoardArtwork data)
        {
            var grid = data.PaintedGrid;
            if (grid == null || grid.Length == 0) return;
            int columns = BoardManager.Width + 1, rows = BoardManager.Height + 1;
            if (grid.Length != columns * rows || data.CombatPanorama == null ||
                data.CombatPortrait != null && data.CombatPortrait != data.CombatPanorama)
                throw new InvalidOperationException("Painted grid needs one original image and every cell intersection: " + data.NationId);
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    var point = grid[row * columns + column];
                    if (!(point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1) ||
                        column > 0 && point.x <= grid[row * columns + column - 1].x ||
                        row > 0 && point.y <= grid[(row - 1) * columns + column].y)
                        throw new InvalidOperationException("Painted grid is folded or outside its original: " + data.NationId);
                }
        }

        void BuildRegisteredArtwork(Sprite sprite, Rect source)
        {
            // Shared mesh vertices join the whole painting, frame and all four corners.
            // Each painted intersection maps to the corresponding EXISTING cell corner.
            // Only texture coordinates/composition change; actors, input and HUD are untouched.
            int columns = BoardManager.Width + 1, rows = BoardManager.Height + 1;
            int stride = columns + 4, totalRows = rows + 4;
            var grid = artwork.PaintedGrid;
            Rect board = TilePortraitBounds, authored = artwork.PlayableArea;
            float scaleX = board.width / authored.width, scaleY = board.height / authored.height;
            float left = board.xMin - authored.xMin * scaleX, right = board.xMax + (1 - authored.xMax) * scaleX;
            float top = board.yMin - authored.yMin * scaleY, bottom = board.yMax + (1 - authored.yMax) * scaleY;
            // Keep the entire registered painting independent of viewport dimensions.
            // Spare space uses the outer atmosphere instead of stretching distant scenery.
            // Local registration to the existing 77 board anchors is still necessary.
            const float apron = .08f;
            var vertices = new Vector3[stride * totalRows];
            var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length];
            for (int row = 0; row < totalRows; row++)
                for (int column = 0; column < stride; column++)
                {
                    int r = Mathf.Clamp(row - 2, 0, rows - 1), c = Mathf.Clamp(column - 2, 0, columns - 1);
                    Vector2 sample = grid[r * columns + c];
                    float x = board.xMin + c * board.width / BoardManager.Width;
                    float y = board.yMin + r * board.height / BoardManager.Height;
                    if (column == 0) { sample.x = 0; x = left; }
                    else if (column == 1) { sample.x = authored.xMin - apron; x = board.xMin - apron * scaleX; }
                    else if (column == stride - 2) { sample.x = authored.xMax + apron; x = board.xMax + apron * scaleX; }
                    else if (column == stride - 1) { sample.x = 1; x = right; }
                    if (row == 0) { sample.y = 0; y = top; }
                    else if (row == 1) { sample.y = authored.yMin - apron; y = board.yMin - apron * scaleY; }
                    else if (row == totalRows - 2) { sample.y = authored.yMax + apron; y = board.yMax + apron * scaleY; }
                    else if (row == totalRows - 1) { sample.y = 1; y = bottom; }
                    int index = row * stride + column;
                    vertices[index] = floorObject.transform.InverseTransformPoint(Point(new Vector2(x, y)));
                    // Pixel-edge coordinates: do not inset each cell separately and create seams.
                    uv[index] = new Vector2((source.x + sample.x * source.width) / sprite.texture.width,
                        (source.yMax - sample.y * source.height) / sprite.texture.height);
                    colors[index] = Color.white;
                }
            var triangles = new int[(stride - 1) * (totalRows - 1) * 6]; int next = 0;
            for (int row = 0; row < totalRows - 1; row++)
                for (int column = 0; column < stride - 1; column++)
                {
                    int a = row * stride + column, b = a + 1, c = a + stride, d = c + 1;
                    triangles[next++] = a; triangles[next++] = c; triangles[next++] = d;
                    triangles[next++] = a; triangles[next++] = d; triangles[next++] = b;
                }
            floorMesh.Clear(); floorMesh.vertices = vertices; floorMesh.uv = uv; floorMesh.colors = colors;
            floorMesh.triangles = triangles; floorMesh.RecalculateBounds();
        }

        void ShadowOutsideBoard(Rect rect, float alpha, int order)
        {
            Rect grid = TilePortraitBounds;
            if (!rect.Overlaps(grid)) { Draw("Nation contact shadow", solid, rect, new Color(0, 0, 0, alpha), order); return; }
            // Four non-overlapping strips retain a contact shadow without darkening any playable cell.
            ShadowPart(Rect.MinMaxRect(rect.xMin, rect.yMin, rect.xMax, Mathf.Min(rect.yMax, grid.yMin)), alpha, order);
            ShadowPart(Rect.MinMaxRect(rect.xMin, Mathf.Max(rect.yMin, grid.yMax), rect.xMax, rect.yMax), alpha, order);
            float top = Mathf.Max(rect.yMin, grid.yMin), bottom = Mathf.Min(rect.yMax, grid.yMax);
            ShadowPart(Rect.MinMaxRect(rect.xMin, top, Mathf.Min(rect.xMax, grid.xMin), bottom), alpha, order);
            ShadowPart(Rect.MinMaxRect(Mathf.Max(rect.xMin, grid.xMax), top, rect.xMax, bottom), alpha, order);
        }
        void ShadowPart(Rect rect, float alpha, int order) { if (rect.width > 0 && rect.height > 0) Draw("Nation contact shadow", solid, rect, new Color(0, 0, 0, alpha), order); }
        SpriteRenderer Draw(string name, Sprite sprite, Rect rect, Color tint, int order)
        {
            var item = new GameObject(name, typeof(SpriteRenderer)); item.transform.SetParent(drawingRoot.transform, false);
            item.transform.localScale = new Vector3(rect.width * Unit / sprite.bounds.size.x, rect.height * Unit / sprite.bounds.size.y, 1);
            item.transform.position = Point(rect.center) - item.transform.TransformVector(sprite.bounds.center);
            var renderer = item.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = tint; renderer.sortingOrder = order;
            renderer.sharedMaterial = MaterialFor(sprite.texture); return renderer;
        }
        Material MaterialFor(Texture texture)
        {
            if (materials.TryGetValue(texture, out var existing)) return existing;
            var material = PixelScenery.CreateMaterial();
            if (material == null) throw new InvalidOperationException("The existing scenery shader is missing.");
            material.name = "Nation scenery " + texture.name; material.mainTexture = texture;
            material.SetVector("_CropRect", new Vector4(0, 0, 1, 1)); material.SetFloat("_PixelSize", 1);
            material.SetFloat("_Saturation", 1); material.SetFloat("_Brightness", 1); material.SetFloat("_Contrast", 1);
            materials.Add(texture, material); return material;
        }
        float Unit => view.orthographicSize * 2 / CardTableArena.PortraitHeight;
        static float DisplayAspect => Screen.width / (float)Mathf.Max(1, Screen.height);
        Vector3 Point(Vector2 portrait) => new Vector3(view.transform.position.x + (portrait.x - 270) * Unit, view.transform.position.y + (480 - portrait.y) * Unit, 0);
        static void Dispose(UnityEngine.Object item) { if (item == null) return; if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        void Clear()
        {
            if (drawingRoot != null) drawingRoot.SetActive(false);
            Dispose(drawingRoot); Dispose(floorMesh); Dispose(cellMesh); Dispose(solid);
            foreach (var material in materials.Values) Dispose(material);
            materials.Clear(); drawingRoot = floorObject = null; floorMesh = cellMesh = null; solid = null; TileCount = 0; ActiveCombatArtwork = null;
        }
        void OnDestroy() => Clear();
    }
}

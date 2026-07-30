using UnityEngine;

namespace MonsterPouch.Gameplay.Board
{
    public sealed class BoardCellCenterDebugView : MonoBehaviour
    {
        private const string DebugRootName = "board-cell-centers-debug";

        [SerializeField] private BoardManager boardManager;
        [SerializeField] private BoardWorldMapper worldMapper;
        [SerializeField] private bool showMarkers;
        [SerializeField] private float markerWorldSize = 0.10f;
        [SerializeField] private int markerSortingOrder = 19;
        [SerializeField] private Color markerColor = new Color(1f, 1f, 1f, 0.65f);

        private Sprite markerSprite;
        private Texture2D markerTexture;

        public bool ShowMarkers => showMarkers;

        public void Configure(BoardManager newBoardManager, BoardWorldMapper newWorldMapper)
        {
            boardManager = newBoardManager;
            worldMapper = newWorldMapper;
        }

        private void Start()
        {
            if (boardManager == null)
                boardManager = Object.FindFirstObjectByType<BoardManager>();

            if (worldMapper == null)
                worldMapper = Object.FindFirstObjectByType<BoardWorldMapper>();

            ApplyMarkerVisibility();
        }

        public void SetShowMarkers(bool shouldShowMarkers)
        {
            showMarkers = shouldShowMarkers;
            ApplyMarkerVisibility();
        }

        [ContextMenu("Rebuild Cell Center Markers")]
        public void BuildMarkers()
        {
            ClearMarkersInternal();

            if (!showMarkers)
                return;

            if (boardManager == null)
            {
                Debug.LogWarning("BoardCellCenterDebugView: BoardManager is null.");
                return;
            }

            if (worldMapper == null)
            {
                Debug.LogWarning("BoardCellCenterDebugView: WorldMapper is null.");
                return;
            }

            GameObject root = new GameObject(DebugRootName);
            root.transform.SetParent(transform, false);

            markerSprite = CreateCircleSprite(out markerTexture);

            for (int y = 0; y < BoardManager.Height; y++)
            {
                for (int x = 0; x < BoardManager.Width; x++)
                {
                    BoardCell cell = boardManager.GetCell(x, y);

                    if (cell == null)
                        continue;

                    Vector3 position = worldMapper.GetWorldPosition(cell);

                    GameObject marker = new GameObject($"cell-center-x{x}-y{y}");
                    marker.transform.SetParent(root.transform, false);
                    marker.transform.position = position;

                    float scale =
                        markerWorldSize /
                        markerSprite.rect.width *
                        markerSprite.pixelsPerUnit;
                    marker.transform.localScale = new Vector3(scale, scale, 1f);

                    SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
                    sr.sprite = markerSprite;
                    sr.color = markerColor;
                    sr.sortingOrder = markerSortingOrder;
                }
            }
        }

        [ContextMenu("Clear Cell Center Markers")]
        public void ClearMarkers()
        {
            ClearMarkersInternal();
        }

        private void OnDisable()
        {
            ClearMarkersInternal();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                ApplyMarkerVisibility();
        }

        private void ApplyMarkerVisibility()
        {
            if (showMarkers)
                BuildMarkers();
            else
                ClearMarkersInternal();
        }

        private void ClearMarkersInternal()
        {
            Transform existing = transform.Find(DebugRootName);

            if (existing != null)
                DestroyImmediate(existing.gameObject);

            if (markerSprite != null)
            {
                DestroyImmediate(markerSprite);
                markerSprite = null;
            }

            if (markerTexture != null)
            {
                DestroyImmediate(markerTexture);
                markerTexture = null;
            }
        }

        private static Sprite CreateCircleSprite(out Texture2D texture)
        {
            int size = 8;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color transparent = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = transparent;

            float center = (size - 1) * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                        pixels[y * size + x] = Color.white;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // The pool contains only viewport-sized rows. Textures belong exclusively to this
    // gallery's small imports, never to LobbyArtwork or the live menu behind it.
    public sealed class LobbySkinScrollView : MonoBehaviour
    {
        public const float RowHeight = 164, RowStride = 176, PreviewTop = 22, PreviewHeight = 132;
        readonly List<LobbySkinCell> cells = new List<LobbySkinCell>();
        ScrollRect scroll;
        Func<Transform, LobbySkinCell> create;
        Action<LobbySkinCell, int> bind;
        Action<float> remember;
        int count;
        Coroutine loader;
        bool loading, subscribed;
        float viewportHeight;
        float canvasScale = -1;
        public int PooledCellCount => cells.Count;
        public int ActiveCellCount { get; private set; }
        public int ResidentPreviewCount { get; private set; }
        public int PeakResidentPreviews { get; private set; }
        public int PreviewLoads { get; private set; }
        public int PreviewReleases { get; private set; }
        public bool HasPendingWork => loading;

        public void Configure(ScrollRect target, int itemCount, float initialOffset,
            Func<Transform, LobbySkinCell> factory, Action<LobbySkinCell, int> binder, Action<float> saveOffset)
        {
            scroll = target; count = itemCount; create = factory; bind = binder; remember = saveOffset;
            scroll.content.sizeDelta = new Vector2(scroll.viewport.rect.width,
                Mathf.Max(scroll.viewport.rect.height, Mathf.Max(0, count * RowStride - (RowStride - RowHeight))));
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(initialOffset, 0,
                Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)));
            Subscribe(); RefreshVisible();
        }

        void OnEnable() { if (scroll != null) { Subscribe(); RefreshVisible(); } }
        void Subscribe()
        {
            if (subscribed) return;
            scroll.onValueChanged.AddListener(OnScroll); subscribed = true;
        }
        void OnScroll(Vector2 value) => RefreshVisible();
        void LateUpdate()
        {
            if (scroll != null && viewportHeight != scroll.viewport.rect.height) RefreshVisible();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvasScale != canvas.scaleFactor)
            {
                canvasScale = canvas.scaleFactor;
                foreach (var cell in cells) if (cell.Index >= 0) { bind(cell, cell.Index); cell.TouchTargets.Refresh(); }
            }
        }

        public static bool Intersects(float rowTop, float height, float offset, float visibleHeight) =>
            height > 0 && visibleHeight > 0 && rowTop < offset + visibleHeight && rowTop + height > offset;

        void RefreshVisible()
        {
            if (!isActiveAndEnabled || scroll == null) return;
            viewportHeight = scroll.viewport.rect.height;
            float offset = Mathf.Clamp(scroll.content.anchoredPosition.y, 0,
                Mathf.Max(0, scroll.content.rect.height - viewportHeight));
            remember?.Invoke(offset);
            int capacity = Mathf.Min(count, Mathf.CeilToInt((viewportHeight + RowHeight) / RowStride));
            while (cells.Count < capacity) cells.Add(create(scroll.content));
            // Release before rebinding, including art that is clipped although its row is visible.
            foreach (var cell in cells)
            {
                bool visible = cell.Index >= 0 && Intersects(cell.Index * RowStride, RowHeight, offset, viewportHeight);
                cell.PreviewVisible = visible && Intersects(cell.Index * RowStride + PreviewTop, PreviewHeight, offset, viewportHeight);
                if (!cell.PreviewVisible) Release(cell);
                if (!visible) { cell.Index = -1; cell.Root.gameObject.SetActive(false); }
            }
            ActiveCellCount = 0;
            int first = Mathf.Max(0, Mathf.FloorToInt(offset / RowStride));
            for (int index = first; index < count && index * RowStride < offset + viewportHeight; index++)
            {
                if (!Intersects(index * RowStride, RowHeight, offset, viewportHeight)) continue;
                LobbySkinCell cell = cells.Find(c => c.Index == index);
                if (cell == null)
                {
                    cell = cells.Find(c => c.Index < 0);
                    cell.Index = index; cell.Attempted = false;
                    LocalGameUI.SetRect(cell.Root, new Rect(0, index * RowStride, scroll.viewport.rect.width, RowHeight));
                    bind(cell, index); cell.Root.gameObject.SetActive(true);
                    cell.TouchTargets.Refresh();
                }
                cell.PreviewVisible = Intersects(index * RowStride + PreviewTop, PreviewHeight, offset, viewportHeight);
                ActiveCellCount++;
            }
            if (!loading && cells.Exists(NeedsPreview))
            {
                loading = true;
                loader = StartCoroutine(LoadVisible());
            }
        }

        static bool NeedsPreview(LobbySkinCell cell) => cell.Index >= 0 && cell.PreviewVisible && !cell.Attempted;
        IEnumerator LoadVisible()
        {
            // Coalesce fast scroll events and load at most one small sprite per frame.
            // No ResourceRequest callback can outlive a closed/disabled selector.
            yield return null;
            while (isActiveAndEnabled)
            {
                var cell = cells.Find(NeedsPreview);
                if (cell == null) break;
                cell.Attempted = true;
                var sprite = Resources.Load<Sprite>(LobbySkinPreviewCatalog.ResourcePath(cell.Id));
                cell.Art.sprite = sprite; cell.Art.enabled = sprite != null;
                if (sprite != null)
                {
                    PreviewLoads++; ResidentPreviewCount++;
                    PeakResidentPreviews = Mathf.Max(PeakResidentPreviews, ResidentPreviewCount);
                }
                yield return null;
            }
            loading = false; loader = null;
        }

        void Release(LobbySkinCell cell)
        {
            var sprite = cell.Art.sprite;
            cell.Art.sprite = null; cell.Art.enabled = false; cell.Attempted = false;
            if (sprite == null) return;
            var texture = sprite.texture;
            Resources.UnloadAsset(sprite);
            if (texture != null) Resources.UnloadAsset(texture);
            ResidentPreviewCount--; PreviewReleases++;
        }
        void OnDisable()
        {
            if (scroll != null && subscribed) scroll.onValueChanged.RemoveListener(OnScroll);
            subscribed = false;
            if (loader != null) StopCoroutine(loader);
            loader = null; loading = false;
            foreach (var cell in cells) { Release(cell); cell.Index = -1; cell.Root.gameObject.SetActive(false); }
            ActiveCellCount = 0;
        }
        void OnDestroy() { create = null; bind = null; remember = null; cells.Clear(); }
    }

    public sealed class LobbySkinCell
    {
        public int Index = -1;
        public string Id;
        public bool PreviewVisible, Attempted;
        public RectTransform Root, Use;
        public Image Art, Threshold;
        public Text Title, Caption;
        public Image[] Swatches;
        public Button Button;
        public StyleSelectorTouchTargets TouchTargets;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Reuses the existing MenuCollectionScroll gesture routing. Only visible cards
    // have figures; recycled shells release their old figure before being rebound.
    public sealed class CollectionGridWindow : MonoBehaviour
    {
        sealed class Cell { public int Index = -1; public RectTransform Root; }
        readonly List<Cell> cells = new List<Cell>();
        ScrollRect scroll;
        string[] ids;
        int columns;
        float viewportHeight;
        float columnStride=124, rowStride=136, cellWidth=112, cellHeight=124;
        Func<Transform, RectTransform> create;
        Action<RectTransform, int> bind;
        Action<RectTransform> release;
        Action<float> remember;
        bool subscribed;
        public int ItemCount => ids?.Length ?? 0;
        public int PooledCellCount => cells.Count;
        public int ActiveCellCount => cells.Count(c => c.Index >= 0);
        public IEnumerable<string> ItemIds => ids ?? Array.Empty<string>();

        public void Configure(ScrollRect target, string[] itemIds, int columnCount, float initialOffset,
            Func<Transform, RectTransform> factory, Action<RectTransform, int> binder,
            Action<RectTransform> releaser, Action<float> saveOffset,
            float columnStride=124, float rowStride=136, float cellWidth=112, float cellHeight=124)
        {
            if (target==null || columnCount<=0 || !Positive(columnStride) || !Positive(rowStride) || !Positive(cellWidth) || !Positive(cellHeight))
                throw new ArgumentOutOfRangeException(nameof(columnCount),"A positive finite grid layout is required.");
            this.columnStride=columnStride;this.rowStride=rowStride;this.cellWidth=cellWidth;this.cellHeight=cellHeight;
            scroll = target; ids = itemIds; columns = columnCount;
            create = factory; bind = binder; release = releaser; remember = saveOffset;
            scroll.content.anchoredPosition = new Vector2(0, ClampOffset(initialOffset));
            Subscribe(); RefreshVisible();
        }
        static bool Positive(float value)=>value>0&&!float.IsNaN(value)&&!float.IsInfinity(value);
        float ClampOffset(float value) => Mathf.Clamp(value, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
        void Subscribe() { if (!subscribed) { scroll.onValueChanged.AddListener(OnScroll); subscribed = true; } }
        void OnScroll(Vector2 value) => RefreshVisible();
        void OnEnable() { if (scroll != null) { Subscribe(); RefreshVisible(); } }
        void LateUpdate() { if (scroll != null && viewportHeight != scroll.viewport.rect.height) RefreshVisible(); }

        // Details/destination screens return to the same filtered list. Do not reset
        // a filter or pick a substitute when the requested character is absent.
        public bool EnsureVisible(string id)
        {
            int index = Array.IndexOf(ids ?? Array.Empty<string>(), id);
            if (index < 0) return false;
            float top = index / columns * rowStride, offset = scroll.content.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (top + cellHeight > offset + scroll.viewport.rect.height) offset = top + cellHeight - scroll.viewport.rect.height;
            scroll.StopMovement(); scroll.content.anchoredPosition = new Vector2(0, ClampOffset(offset));
            RefreshVisible(); return true;
        }

        void RefreshVisible()
        {
            if (!isActiveAndEnabled || scroll == null) return;
            viewportHeight = scroll.viewport.rect.height;
            float offset = ClampOffset(scroll.content.anchoredPosition.y);
            remember?.Invoke(offset);
            var visible = new HashSet<int>(BriefCollectionFilter.VisibleIndices(ItemCount, columns, offset, viewportHeight, rowStride, cellHeight));
            foreach (var cell in cells)
                if (cell.Index >= 0 && !visible.Contains(cell.Index))
                { cell.Root.gameObject.SetActive(false); release(cell.Root); cell.Index = -1; cell.Root.name = "Recycled collection card"; }
            foreach (int index in visible.OrderBy(i => i))
            {
                if (cells.Any(c => c.Index == index)) continue;
                var cell = cells.Find(c => c.Index < 0);
                if (cell == null) { cell = new Cell { Root = create(scroll.content) }; cells.Add(cell); }
                cell.Index = index;
                LocalGameUI.SetRect(cell.Root, new Rect(index % columns * columnStride, index / columns * rowStride, cellWidth, cellHeight));
                bind(cell.Root, index); cell.Root.gameObject.SetActive(true);
            }
        }
        void OnDisable()
        {
            if (scroll != null && subscribed) scroll.onValueChanged.RemoveListener(OnScroll);
            subscribed = false;
        }
    }
}

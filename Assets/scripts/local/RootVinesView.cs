using UnityEngine;

namespace MonsterPouch.Local
{
    // A view only. Owner calls Present from a current core snapshot, including while
    // paused. No Update, independent clock, collider, locomotion or combat mutation.
    [DisallowMultipleComponent]
    public sealed class RootVinesView : MonoBehaviour
    {
        SpriteRenderer back, front;
        Sprite[] backFrames, frontFrames;
        int generation = int.MinValue, highestGrowthFrame = -1;
        bool shown;
        const float GrowSeconds = .24f;

        public bool IsShown => shown;
        public bool AssetsReady => Ready();

        public void Configure(Sprite[] behind, Sprite[] inFront)
        {
            Hide();
            backFrames = behind; frontFrames = inFront;
            if (back == null) back = CreateLayer("Root vines - back");
            if (front == null) front = CreateLayer("Root vines - front");
        }

        SpriteRenderer CreateLayer(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            return renderer;
        }

        // Same radial art for all eight facings. Width/height are supplied from body
        // reference geometry and cell limits, never live weapon/attack sprite bounds.
        // normalizedVisibleSize is measured from the delivered sprite manifest.
        public void Present(bool coreHasRoot, int unitResetVersion, float coreAgeSeconds,
            Vector3 groundAnchor, Vector2 permittedWorldSize, Vector2 normalizedVisibleSize,
            int sortingLayerId, int bodySortingOrder)
        {
            if (generation != unitResetVersion) { Hide(); generation = unitResetVersion; }
            if (!isActiveAndEnabled || !coreHasRoot || !Ready() || !Finite(groundAnchor.x) ||
                !Finite(groundAnchor.y) || !Finite(groundAnchor.z) || !Finite(permittedWorldSize.x) ||
                !Finite(permittedWorldSize.y) || permittedWorldSize.x <= 0 ||
                permittedWorldSize.y <= 0 || !Finite(normalizedVisibleSize.x) ||
                !Finite(normalizedVisibleSize.y) || normalizedVisibleSize.x <= 0 ||
                normalizedVisibleSize.y <= 0) { Hide(); return; }

            float age = Finite(coreAgeSeconds) ? Mathf.Max(0, coreAgeSeconds) : 0;
            int frame = Mathf.Min(backFrames.Length - 1,
                Mathf.FloorToInt(age / GrowSeconds * backFrames.Length));
            // An accepted refresh updates this same enclosure; never regrow/shrink
            // an already mature vine merely because the core interval changed.
            highestGrowthFrame = Mathf.Max(highestGrowthFrame, frame);
            var scale = new Vector3(permittedWorldSize.x / normalizedVisibleSize.x,
                permittedWorldSize.y / normalizedVisibleSize.y, 1);
            Draw(back, backFrames[highestGrowthFrame], groundAnchor, scale,
                sortingLayerId, bodySortingOrder - 1);
            Draw(front, frontFrames[highestGrowthFrame], groundAnchor, scale,
                sortingLayerId, bodySortingOrder + 1);
            shown = true;
        }

        bool Ready()
        {
            if (back == null || front == null || backFrames == null || frontFrames == null ||
                backFrames.Length == 0 || backFrames.Length != frontFrames.Length) return false;
            for (int i = 0; i < backFrames.Length; i++)
                if (backFrames[i] == null || frontFrames[i] == null) return false;
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void Draw(SpriteRenderer renderer, Sprite sprite, Vector3 anchor,
            Vector3 localScale, int layer, int order)
        {
            renderer.sprite = sprite;
            renderer.transform.position = anchor;
            renderer.transform.rotation = Quaternion.identity;
            // Own component is mounted on an unscaled status presentation root.
            renderer.transform.localScale = localScale;
            renderer.sortingLayerID = layer; renderer.sortingOrder = order;
            renderer.enabled = true;
        }

        public void Hide()
        {
            if (back != null) back.enabled = false;
            if (front != null) front.enabled = false;
            shown = false; highestGrowthFrame = -1;
        }

        void OnDisable() { Hide(); }
    }
}

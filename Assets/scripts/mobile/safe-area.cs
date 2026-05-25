using UnityEngine;

namespace MonsterPouch.Mobile
{
    public sealed class SafeArea : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            float canvasWidth = canvas.pixelRect.width;
            float canvasHeight = canvas.pixelRect.height;

            anchorMin.x /= canvasWidth;
            anchorMin.y /= canvasHeight;
            anchorMax.x /= canvasWidth;
            anchorMax.y /= canvasHeight;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }
    }
}

using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Presentation only: the existing scene chooses lobby, starter selection or tutorial.
    public sealed class LoadingArtworkScreen : MonoBehaviour
    {
        public const string ResourcePath = "MonsterPouch/UI/Branding/loading-screen-supplied";
        public const float MinimumDisplaySeconds = 3f;
        public const float FadeSeconds = .65f;
        const float MaximumPresentationStep = .1f;
        const string MainScenePath = "Assets/Scenes/main-scene.unity";
        static LoadingArtworkScreen current;
        CanvasGroup group;
        RectTransform artwork;
        Image branding;
        Sprite suppliedArtwork;
        LocalGameUI owner;
        bool tracksOwner, completeRequested, presentedFirstFrame;
        int sceneHandle, readyFrame;
        float visibleAge, fadeAge;
        EventSystem inputEvents;
        bool savedNavigation;
        CanvasGroup ownerInput;
        bool addedOwnerInput, savedInteractable, savedRaycasts;
        Vector2Int lastScreen;
        Rect lastSafeArea;

        public static LoadingArtworkScreen Current => current;
        public static bool IsBlocking => current != null && current.isActiveAndEnabled;
        public bool IsFading => completeRequested;
        public float Opacity => group != null ? group.alpha : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            // Also release a surviving object when Enter Play Mode skips domain/scene reload.
            if (current != null) current.Dismiss();
            current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeFirstScene()
        {
            // An Editor session may start in a tooling/test scene, where no game startup is occurring.
            if (Application.isEditor && SceneManager.GetActiveScene().path != MainScenePath) return;
            Show();
        }

        public static LoadingArtworkScreen Show()
        {
            if (current != null && current.isActiveAndEnabled) return current;
            var sprite = Resources.Load<Sprite>(ResourcePath);
            if (sprite == null)
            {
                Debug.LogError("The supplied loading artwork is missing: " + ResourcePath);
                return null;
            }
            var root = new GameObject("Loading artwork overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            canvas.pixelPerfect = true;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var screen = root.AddComponent<LoadingArtworkScreen>();
            current = screen;
            screen.group = root.GetComponent<CanvasGroup>();
            screen.group.alpha = 1;
            screen.group.blocksRaycasts = true;
            screen.group.interactable = true;
            screen.suppliedArtwork = sprite;
            var background = MakeImage(root.transform, "Loading artwork background");
            background.color = Color.black;
            background.raycastTarget = true;
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            var image = MakeImage(root.transform, "Loading artwork image");
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            screen.artwork = image.rectTransform;
            screen.artwork.anchorMin = screen.artwork.anchorMax = Vector2.zero;
            screen.artwork.pivot = Vector2.zero;
            // The previous background baked in the title. Reuse the existing transparent
            // logo as a separate safe-area layer when replacing that background.
            var logo = SuppliedBranding.Logo;
            if (logo != null)
            {
                screen.branding = MakeImage(root.transform, "Loading artwork logo");
                screen.branding.sprite = logo;
                screen.branding.preserveAspect = true;
                screen.branding.raycastTarget = false;
                screen.branding.rectTransform.anchorMin = screen.branding.rectTransform.anchorMax = Vector2.zero;
                screen.branding.rectTransform.pivot = Vector2.zero;
            }
            screen.Layout();
            screen.BlockNavigation();
            SceneManager.sceneUnloaded += screen.SceneUnloaded;
            return screen;
        }

        public static void RevealReadyView(LocalGameUI readyOwner)
        {
            if (readyOwner == null) return;
            var screen = current;
            if (screen == null) return;
            screen.owner = readyOwner;
            screen.tracksOwner = true;
            screen.sceneHandle = readyOwner.gameObject.scene.handle;
            screen.BlockOwnerInput();
            screen.CompleteLoading();
        }

        public void CompleteLoading()
        {
            if (completeRequested) return;
            completeRequested = true;
            readyFrame = Time.frameCount;
            fadeAge = 0;
        }

        static Image MakeImage(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Image>();
        }

        // Backgrounds cover the physical screen, including notches. Only UI uses the safe area.
        public static Rect CoverArtwork(Vector2 screen, Vector2 source)
        {
            var fit = LobbyArtwork.Frame(new Rect(0, 0, Mathf.Max(1, screen.x), Mathf.Max(1, screen.y)), source);
            // The two main faces sit slightly right of center in the supplied artwork.
            // Move the focus to 55% only where horizontal overflow permits it; never expose an edge.
            fit.x -= Mathf.Min(fit.width * .05f, Mathf.Max(0, fit.width - Mathf.Max(1, screen.x)) * .5f);
            return fit;
        }

        // UGUI rounds the rect before preserveAspect. Give it integer bounds that
        // still contain the ideal frame after uniform aspect fitting (pivot stays zero).
        static Rect AlignArtworkToPixels(Rect fit, Vector2 source)
        {
            var position = new Vector2(Mathf.Floor(fit.xMin), Mathf.Floor(fit.yMin));
            float aspect = source.x / source.y;
            float height = Mathf.Ceil(Mathf.Max(fit.yMax - position.y, (fit.xMax - position.x) / aspect));
            return new Rect(position, new Vector2(Mathf.Ceil(height * aspect), height));
        }

        void Layout()
        {
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            Rect fit = AlignArtworkToPixels(CoverArtwork(lastScreen, suppliedArtwork.rect.size), suppliedArtwork.rect.size);
            artwork.anchoredPosition = fit.position;
            artwork.sizeDelta = fit.size;
            if (branding != null)
            {
                Rect safe = PortraitViewport.NormalizeSafeArea(lastScreen, lastSafeArea);
                float shortSide = Mathf.Min(safe.width, safe.height);
                float width = shortSide * .3f;
                branding.rectTransform.sizeDelta = new Vector2(width, width * branding.sprite.rect.height / branding.sprite.rect.width);
                branding.rectTransform.anchoredPosition = new Vector2(safe.center.x - width * .5f, safe.yMin + shortSide * .04f);
            }
        }

        void LateUpdate()
        {
            if (group == null) return;
            if (tracksOwner && (owner == null || !owner.isActiveAndEnabled)) { Dismiss(); return; }
            BlockNavigation();
            if (lastScreen.x != Screen.width || lastScreen.y != Screen.height || lastSafeArea != Screen.safeArea) Layout();
            // The first delta can include scene loading before this Canvas was ever drawn.
            // Keep that first frame fully opaque, then count only presentation time.
            if (!presentedFirstFrame) { presentedFirstFrame = true; return; }
            // Loading stalls and resuming the app must not swallow the hold or the fade.
            float step = Mathf.Min(Time.unscaledDeltaTime, MaximumPresentationStep);
            if (visibleAge < MinimumDisplaySeconds) { visibleAge += step; return; }
            // Let the destination finish its first layout pass underneath the opaque artwork.
            if (!completeRequested || Time.frameCount <= readyFrame + 1) return;
            fadeAge += step;
            group.alpha = 1 - Mathf.SmoothStep(0, 1, fadeAge / FadeSeconds);
            if (fadeAge >= FadeSeconds) Dismiss();
        }

        void BlockOwnerInput()
        {
            if (ownerInput != null || owner == null || owner.CanvasRoot == null) return;
            ownerInput = owner.CanvasRoot.GetComponent<CanvasGroup>();
            addedOwnerInput = ownerInput == null;
            if (addedOwnerInput) ownerInput = owner.CanvasRoot.gameObject.AddComponent<CanvasGroup>();
            savedInteractable = ownerInput.interactable;
            savedRaycasts = ownerInput.blocksRaycasts;
            ownerInput.interactable = false;
            ownerInput.blocksRaycasts = false;
        }

        void BlockNavigation()
        {
            if (inputEvents == EventSystem.current) return;
            if (inputEvents != null) inputEvents.sendNavigationEvents = savedNavigation;
            inputEvents = EventSystem.current;
            if (inputEvents == null) return;
            savedNavigation = inputEvents.sendNavigationEvents;
            inputEvents.sendNavigationEvents = false;
        }

        void ReleaseInput()
        {
            if (inputEvents != null) inputEvents.sendNavigationEvents = savedNavigation;
            inputEvents = null;
            if (ownerInput != null)
            {
                ownerInput.interactable = savedInteractable;
                ownerInput.blocksRaycasts = savedRaycasts;
                if (addedOwnerInput) Destroy(ownerInput);
            }
            ownerInput = null;
        }

        void SceneUnloaded(Scene scene) { if (tracksOwner && scene.handle == sceneHandle) Dismiss(); }

        public void Dismiss()
        {
            if (group != null) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
            ReleaseInput();
            if (current == this) current = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void OnDisable()
        {
            if (group != null) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
            ReleaseInput();
            SceneManager.sceneUnloaded -= SceneUnloaded;
            if (current == this) current = null;
            // Disabling just this behaviour must not leave its still-enabled Canvas over the game.
            if (gameObject.activeSelf) gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void OnDestroy() { ReleaseInput(); SceneManager.sceneUnloaded -= SceneUnloaded; if (current == this) current = null; }
    }
}

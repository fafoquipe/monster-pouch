using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Temporary QA only. Explicit Begin/Capture/Restore; never runs on load.
public static class LobbyArtworkNativeQa
{
    const string Root = "C:/Users/HP VICTUS/OneDrive/Documentos/ChatGPT/monsters-pouch/eronya-lobbies-20261029/separated-nine/evidence/native";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static LocalGameUI UI => Object.FindFirstObjectByType<LocalGameUI>();
    static readonly string[] Styles = { "lounge", "casino-night", "volkanar", "geothar", "naturalis",
        "aireola", "aquarion", "classic", "royal" };

    public static string Begin()
    {
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Preserve the unsaved scene; do not start QA.");
        return MonsterPouchNationNativePreview.Begin("393x852");
    }
    public static string Status() => MonsterPouchNationNativePreview.Status();
    public static string Restore() => MonsterPouchNationNativePreview.Restore();
    public static string Capture(string phase)
    {
        if (phase != "before" && phase != "after" && phase != "aspect") throw new ArgumentException("before, after or aspect");
        if (!Status().StartsWith("Ready;")) throw new InvalidOperationException("Isolated preview is not ready.");
        string folder = Path.Combine(Root, phase);
        if (Directory.Exists(folder)) throw new InvalidOperationException("Keep existing evidence; use a reviewed new evidence folder.");
        Directory.CreateDirectory(folder);
        UI.StartCoroutine(Guard(Run(folder, phase != "before", phase == "aspect"), folder));
        return "Native lobby capture started: " + folder;
    }
    // A focused before/after pass for the coverage correction. Uses the existing isolated account.
    public static string CaptureCoverage(string phase)
    {
        if (phase != "before" && phase != "after") throw new ArgumentException("before or after");
        if (!Status().StartsWith("Ready;")) throw new InvalidOperationException("Isolated preview is not ready.");
        string folder = Path.Combine("C:/Users/HP VICTUS/OneDrive/Documentos/ChatGPT/monsters-pouch/eronya-lobbies-20261029/coverage-correction/evidence/native", phase);
        if (Directory.Exists(folder)) throw new InvalidOperationException("Preserve previous coverage evidence.");
        Directory.CreateDirectory(folder);
        UI.StartCoroutine(Guard(RunCoverage(folder, phase == "after"), folder));
        return "Native coverage capture started: " + folder;
    }
    static IEnumerator RunCoverage(string folder, bool validate)
    {
        while (LoadingArtworkScreen.IsBlocking) yield return null;
        foreach (var size in new[] { new Vector2Int(393, 852), new Vector2Int(768, 1024), new Vector2Int(1280, 720), new Vector2Int(3440, 1440) })
        {
            string dimensions = size.x + "x" + size.y;
            MonsterPouchNationNativePreview.Resize(dimensions);
            float deadline = Time.realtimeSinceStartup + 10;
            while ((Screen.width != size.x || Screen.height != size.y) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Screen.width == size.x && Screen.height == size.y, "GameView did not resize.");
            Show("casino-night", true); yield return Settle();
            Rect shared = Bounds(UI.CanvasRoot.GetComponentsInChildren<Image>().Single(i => i.name == "Painted lobby artwork").rectTransform);
            foreach (int tab in new[] { 2, 1, 0, 3, 4 })
            {
                UI.CloseModal(); UI.NavigateMenu(tab); yield return Settle();
                if (validate) ValidateArtwork("casino-night", tab == 2 || tab == 3);
                Rect frame = Bounds(UI.CanvasRoot.GetComponentsInChildren<Image>().Single(i => i.name == "Painted lobby artwork").rectTransform);
                Check(Vector2.Distance(shared.position, frame.position) < 1 && Vector2.Distance(shared.size, frame.size) < 1, "Menu changed background framing.");
                yield return Shot(folder, dimensions + "-menu-" + tab);
            }
        }
    }
    static IEnumerator Guard(IEnumerator work, string folder)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error) { File.WriteAllText(Path.Combine(folder, "ERROR.txt"), error.ToString()); Debug.LogException(error); yield break; }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.WriteAllText(Path.Combine(folder, "COMPLETE.txt"), Status());
    }
    static IEnumerator Settle(float seconds = .6f)
    { yield return new WaitForSecondsRealtime(seconds); yield return null; Canvas.ForceUpdateCanvases(); }
    static void Show(string id, bool after)
    {
        // A missing additive map can only use the old base in BEFORE evidence.
        Check(!after || CardTableTheme.KnownMap(id), "Maps identity dependency is not installed: " + id);
        var style = CardTableTheme.KnownMap(id) ? CardTableTheme.FromMapId(id) : TableStyle.CampoReal;
        typeof(MonsterPouchNationNativePreview).GetMethod("ShowLobby", Any).Invoke(null, new object[] { style, "bugaloo" });
        if (after) { LobbyThemeSelection.Select(id); UI.RenderPage(); }
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static Rect Bounds(RectTransform rect)
    {
        var c = new Vector3[4]; rect.GetWorldCorners(c);
        return Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
    }
    static IEnumerator Run(string folder, bool after, bool aspect)
    {
        while (LoadingArtworkScreen.IsBlocking) yield return null;
        var sizes = aspect ? new[] { new Vector2Int(393, 852), new Vector2Int(1280, 720), new Vector2Int(3440, 1440) }
            : new[] { new Vector2Int(393, 852), new Vector2Int(1280, 720) };
        foreach (var size in sizes)
        {
            string dimensions = size.x + "x" + size.y;
            MonsterPouchNationNativePreview.Resize(dimensions);
            float deadline = Time.realtimeSinceStartup + 10;
            while ((Screen.width != size.x || Screen.height != size.y) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Screen.width == size.x && Screen.height == size.y, "GameView did not resize.");
            foreach (var id in Styles)
            {
                Show(id, after); yield return Settle();
                if (after) ValidateArtwork(id);
                yield return Shot(folder, dimensions + "-" + id);
                if (after)
                {
                    UI.OpenDecks(); yield return Settle(); ValidateArtwork(id, false);
                    yield return Shot(folder, dimensions + "-" + id + "-brief");
                }
            }
            if (after)
            {
                // Latest instruction also replaces the classic Home, keeping its cabinet.
                Show("classic", true); yield return Settle(); ValidateArtwork("classic");
                Show("naturalis", true); yield return Settle();
                foreach (var raycaster in UI.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = true;
                var pick = UI.CanvasRoot.GetComponentsInChildren<Button>().Single(b => b.name == "Choose featured Monster");
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Bounds((RectTransform)pick.transform).center }, hits);
                Check(hits.Any(h => h.gameObject.transform.IsChildOf(pick.transform) || h.gameObject == pick.gameObject), "Artwork blocks character selection.");
                pick.onClick.Invoke(); yield return Settle();
                Check(UI.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Character selection"), "Picker does not open.");
                UI.CloseModal(); UI.OpenDecks(); yield return Settle();
                ValidateArtwork("naturalis", false);
                if (aspect) yield return Shot(folder, dimensions + "-menu-decks");
                ValidateCabinetPedestals();
                if (aspect)
                {
                    foreach (int tab in new[] { 0, 3, 4 })
                    {
                        UI.NavigateMenu(tab); yield return Settle(); ValidateArtwork("naturalis", false);
                        yield return Shot(folder, dimensions + "-menu-" + tab);
                    }
                }
                UI.CloseModal(); UI.OpenHome(); yield return Settle();
                string savedMap = PlayerPrefs.GetString(CardTableTheme.PreferenceKey);
                foreach (string selected in new[] { "aireola", "royal" })
                {
                    // Reflection permits the same helper to compile for the dormant BEFORE import.
                    typeof(LocalGameUI).GetMethod("SelectLobbyStyle", new[] { typeof(string) }).Invoke(UI, new object[] { selected });
                    yield return Settle(MapIrisTransition.Duration + .2f);
                    Check(LobbyThemeSelection.Id == selected && PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey) == selected, "Lobby selection did not persist.");
                    Check(PlayerPrefs.GetString(CardTableTheme.PreferenceKey) == savedMap, "Lobby changed the map preference.");
                    UI.CloseModal(); UI.OpenHome(); yield return Settle(); ValidateArtwork(selected);
                    yield return Shot(folder, dimensions + "-selector-return-" + selected);
                }
            }
        }
    }
    static void ValidateArtwork(string id, bool home = true)
    {
        var images = UI.CanvasRoot.GetComponentsInChildren<Image>();
        var image = images.Single(i => i.name == "Painted lobby artwork");
        var art = LobbyArtwork.Find(id);
        Check(art != null && art.Background == image.sprite, "Wrong original for " + id);
        Check(!image.raycastTarget && image.color == Color.white && image.preserveAspect, "Original tint, aspect or interaction changed.");
        Check(image.transform.parent.GetComponent<Image>().color == Color.black, "Spare image space must stay black.");
        Check(!images.Any(i => i.name == "Home wooden pedestal" || i.name == "Figure platform"), "Double pedestal.");
        Rect frame = Bounds(image.rectTransform);
        Check(Mathf.Abs(frame.width / frame.height - image.sprite.rect.width / image.sprite.rect.height) < .001f, "Artwork was stretched.");
        Rect visible = Bounds((RectTransform)image.transform.parent);
        Check(Mathf.Abs(visible.xMin) < 1 && Mathf.Abs(visible.yMin) < 1 &&
            Mathf.Abs(visible.xMax - Screen.width) < 1 && Mathf.Abs(visible.yMax - Screen.height) < 1, "Background must include the full physical display.");
        const float coverAspect = 21f / 9f; // Keep this helper importable for the BEFORE pass against the old runtime.
        float coveredWidth = Mathf.Min(visible.width, visible.height * coverAspect);
        Check(frame.xMin <= visible.center.x - coveredWidth * .5f + 1 && frame.xMax >= visible.center.x + coveredWidth * .5f - 1 &&
            frame.yMin <= visible.yMin + 1 && frame.yMax >= visible.yMax - 1, "Background leaves a normal-screen gap.");
        Check(Vector2.Distance(frame.center, visible.center) < 1, "Cover must remain centered.");
        if (visible.width / visible.height > coverAspect)
            Check(Mathf.Abs(frame.width - coveredWidth) < 1, "Only the excess width beyond 21:9 may form side margins.");
        var pick = UI.CanvasRoot.GetComponentsInChildren<RectTransform>().SingleOrDefault(r => r.name == "Choose featured Monster");
        var pedestal = images.SingleOrDefault(i => i.name == "Home character pedestal");
        bool hasHero = pick != null && pick.GetComponentInChildren<LobbyPersonality>() != null;
        Check(hasHero == (pedestal != null), "Only a visible character may have a Home pedestal.");
        if (UI.MenuPage == "decks") ValidateCabinetPedestals();
        if (!hasHero) return;
        Check(pedestal.sprite == art.Pedestal && pedestal.preserveAspect && !pedestal.raycastTarget, "Wrong or interactive pedestal.");
        Rect hero = Bounds(pick), platform = Bounds(pedestal.rectTransform);
        Check(Mathf.Abs(platform.width / platform.height - art.Pedestal.rect.width / art.Pedestal.rect.height) < .001f, "Pedestal was stretched.");
        Vector2 foot = new Vector2(platform.xMin + platform.width * art.PedestalFootAnchor.x, platform.yMax - platform.height * art.PedestalFootAnchor.y);
        Check(Vector2.Distance(foot, new Vector2(hero.center.x, hero.yMin)) < 1f, "Pedestal and character feet separated.");
    }
    static void ValidateCabinetPedestals()
    {
        var images = UI.CanvasRoot.GetComponentsInChildren<Image>();
        foreach (var slot in UI.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r => r.name == "deck-monster" || r.name.StartsWith("deck-slot-")))
        {
            string name = slot.name == "deck-monster" ? "Monster wooden pedestal" : "Whelp wooden pedestal " + slot.name.Substring(10);
            Check((slot.GetComponentInChildren<LobbyPersonality>() != null) == images.Any(i => i.name == name), "Orphan or missing cabinet pedestal: " + name);
        }
    }
    [Serializable] sealed class Control { public string name; public Rect bounds; public bool enabled; }
    [Serializable] sealed class Evidence { public int width, height; public string lobby, map, status, menuPage; public Rect artworkBounds; public float sourceAspect, renderedAspect; public bool preserveAspect; public Control[] controls; }
    static IEnumerator Shot(string folder, string name)
    {
        var controls = UI.CanvasRoot.GetComponentsInChildren<Button>().Select(b => new Control { name = b.name, bounds = Bounds((RectTransform)b.transform), enabled = b.interactable }).ToArray();
        File.WriteAllText(Path.Combine(folder, name + ".json"), JsonUtility.ToJson(new Evidence { width = Screen.width, height = Screen.height,
            lobby = LobbyThemeSelection.Id, map = CardTableTheme.MapId(CardTableTheme.Current), status = Status(), controls = controls }, true));
        var drawing = UI.CanvasRoot.GetComponentsInChildren<Image>().SingleOrDefault(i => i.name == "Painted lobby artwork");
        if (drawing != null)
        {
            Rect bounds = Bounds(drawing.rectTransform);
            File.WriteAllText(Path.Combine(folder, name + ".json"), JsonUtility.ToJson(new Evidence {
                width = Screen.width, height = Screen.height, lobby = LobbyThemeSelection.Id,
                map = CardTableTheme.MapId(CardTableTheme.Current), status = Status(), menuPage = UI.MenuPage,
                artworkBounds = bounds, sourceAspect = drawing.sprite.rect.width / drawing.sprite.rect.height,
                renderedAspect = bounds.width / bounds.height, preserveAspect = drawing.preserveAspect, controls = controls }, true));
        }
        string path = Path.Combine(folder, name + ".png"); ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.realtimeSinceStartup + 10;
        do { yield return null; if (File.Exists(path) && new FileInfo(path).Length > 24) break; } while (Time.realtimeSinceStartup < deadline);
        Check(File.Exists(path), "Screenshot missing: " + path);
    }
}

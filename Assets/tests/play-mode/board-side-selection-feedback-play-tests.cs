using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BoardSideSelectionFeedbackPlayTests
{
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(name => typeof(LocalGameUI).GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    LocalGameUI ui;
    InteractionCaptureGameView captureView;
    string directory, previousLobbyStyle;
    string[] previousDirectories;
    Dictionary<string, string> savedFiles;
    bool hadLobbyStyle, savedPreferences;
    int previousSleep;
    MatchController Match => ui.Match;
    SpriteRenderer RivalSurface => ui.GetComponentsInChildren<SpriteRenderer>(true).SingleOrDefault(r => r.name == "Rival placement surface");
    LineRenderer[] Lines => ui.GetComponentsInChildren<LineRenderer>().Where(r => r.name == "Legal cell" && r.enabled).ToArray();

    [UnitySetUp] public IEnumerator Setup()
    {
        previousSleep = Screen.sleepTimeout;
        hadLobbyStyle = PlayerPrefs.HasKey(CardTableTheme.LobbyPreferenceKey);
        previousLobbyStyle = PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey);
        savedPreferences = true;
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchBoardSideFeedbackTests", Guid.NewGuid().ToString("N"));
        previousDirectories = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, Path.Combine(directory, "store-" + i));
        new LocalGuestProfileStore(Path.Combine(directory, "store-0")).OpenOrCreate(true);
        captureView = new InteractionCaptureGameView();
        yield return captureView.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.IsNotNull(ui);
        Match.enabled = false;
        float deadline = Time.unscaledTime + 8;
        while (LoadingArtworkScreen.IsBlocking && Time.unscaledTime < deadline) yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        ui.CloseModal();
        yield return null; yield return null;
        Assert.AreEqual(MatchPhase.Menu, Match.Phase);
        savedFiles = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            AssertSavedFiles();
            if (ui != null) { ui.enabled = true; ui.CloseModal(); Match.Menu(); }
            yield return null;
        }
        finally
        {
            try
            {
                if (previousDirectories != null)
                    for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, previousDirectories[i]);
                if (savedPreferences)
                {
                    if (hadLobbyStyle) PlayerPrefs.SetString(CardTableTheme.LobbyPreferenceKey, previousLobbyStyle);
                    else PlayerPrefs.DeleteKey(CardTableTheme.LobbyPreferenceKey);
                    PlayerPrefs.Save();
                }
                Screen.sleepTimeout = previousSleep;
                if (directory != null)
                {
                    string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchBoardSideFeedbackTests")) + Path.DirectorySeparatorChar;
                    string target = Path.GetFullPath(directory);
                    Assert.IsTrue(target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                }
            }
            finally { captureView?.RestoreSelection(); }
        }
        if (captureView != null) yield return captureView.WaitForRestoration();
    }

    [UnityTest] public IEnumerator UnselectedOfferDragShowsOutlinesAndRivalFilterWithoutBuyingOrRecreatingThemOnMovement()
    {
        // Capture the actual initial lobby before beginning this fixture's match.
        yield return Capture("initial-lobby");
        yield return StartLocal();
        Assert.IsNull(ui.SelectedId); Assert.AreEqual(-1, ui.SelectedOfferSlot);
        AssertHidden();
        var gesture = ui.CanvasRoot.GetComponentsInChildren<BriefOfferGesture>().First(g => g.UnitId == "rosie");
        var pointer = Pointer(Center((RectTransform)gesture.transform), 81);
        string economy = Economy();
        gesture.OnPointerDown(pointer);
        pointer.position = ScreenPoint(Match.Board.GetCell(1, 7));
        gesture.OnDrag(pointer);
        AssertFeedback();
        Assert.IsNull(ui.SelectedId, "A drag preview does not turn into a tap selection.");
        Assert.IsNotNull(Find("Dragged Whelp"));
        int rendererId = RivalSurface.GetInstanceID();
        int[] lineIds = Lines.Select(line => line.GetInstanceID()).OrderBy(id => id).ToArray();
        foreach (var coordinate in new[] { new Vector2Int(2, 7), new Vector2Int(3, 8), new Vector2Int(4, 6) })
        {
            pointer.position = ScreenPoint(Match.Board.GetCell(coordinate.x, coordinate.y));
            gesture.OnDrag(pointer);
            yield return null;
            AssertFeedback();
            Assert.AreEqual(rendererId, RivalSurface.GetInstanceID());
            CollectionAssert.AreEqual(lineIds, Lines.Select(line => line.GetInstanceID()).OrderBy(id => id).ToArray(),
                "Pointer movement must reuse the existing contour renderers.");
            Assert.AreEqual(economy, Economy());
        }
        yield return Capture("blue-offer-drag");
        gesture.OnEndDrag(pointer);
        gesture.OnPointerUp(pointer);
        AssertHidden();
        Assert.IsNull(Find("Dragged Whelp"));
        yield return null; yield return null;
        AssertHidden(); Assert.AreEqual(economy, Economy());
        Assert.IsNull(ui.SelectedId); Assert.IsFalse(Match.Player.Owned.ContainsKey("rosie"));
    }

    [UnityTest] public IEnumerator FieldSelectionAndBankDragShareFeedbackAndCancellationKeepsUnitsAndEconomyIntact()
    {
        yield return StartLocal();
        var monster = Match.Player.Monster;
        var actor = Match.Actors[monster];
        var origin = actor.CurrentCell;
        Vector3 position = actor.transform.position;
        ui.Select(monster);
        yield return null; yield return null;
        AssertFeedback();
        string economy = Economy();
        var boardGesture = ui.CanvasRoot.GetComponentInChildren<BoardPointerGesture>();
        var pointer = Pointer(ui.GameCamera.WorldToScreenPoint(actor.transform.position + Vector3.up * .45f), 82);
        boardGesture.OnPointerDown(pointer);
        pointer.position = ScreenPoint(Match.Board.GetCell(0, 8));
        boardGesture.OnDrag(pointer);
        Assert.IsNotNull(Find("Dragged Whelp")); AssertFeedback();
        boardGesture.OnEndDrag(pointer); boardGesture.OnPointerUp(pointer);
        Assert.IsNull(Find("Dragged Whelp"));
        yield return null;
        AssertFeedback(); // Explicit field selection remains after cancelling its drag.
        Assert.AreEqual(monster.Definition.Id, ui.SelectedId);
        Assert.AreEqual(economy, Economy());
        Assert.AreSame(origin, actor.CurrentCell); Assert.AreSame(actor, origin.OccupiedBy);
        Assert.AreEqual(origin.Coordinates, monster.Deployment); Assert.AreEqual(position, actor.transform.position);

        var offer = Match.Player.Offers.First(o => o != null && o.UnitId == "rosie");
        int slot = Match.Player.Offers.ToList().IndexOf(offer);
        ui.DropOffer(slot, offer.Token, Center(Find("Bench drop")));
        yield return null; yield return null;
        var stored = Match.Player.Owned["rosie"];
        Assert.AreEqual(UnitLocation.Bench, stored.Location);
        ui.ToggleBankContents(); yield return null;
        var bankGesture = Find("bank-unit-rosie").GetComponent<UnitCardGesture>();
        pointer = Pointer(Center((RectTransform)bankGesture.transform), 83);
        economy = Economy();
        bankGesture.OnPointerDown(pointer);
        pointer.position = ScreenPoint(Match.Board.GetCell(1, 7));
        bankGesture.OnDrag(pointer);
        AssertFeedback(); Assert.IsNotNull(Find("Dragged Whelp"));
        Assert.AreEqual(0, Find("Bank contents").GetComponent<CanvasGroup>().alpha);
        bankGesture.OnEndDrag(pointer); bankGesture.OnPointerUp(pointer);
        yield return null; yield return null;
        Assert.IsNull(Find("Dragged Whelp"));
        AssertFeedback();
        Assert.AreEqual(1, Find("Bank contents").GetComponent<CanvasGroup>().alpha);
        Assert.IsTrue(Find("Bank contents").GetComponent<CanvasGroup>().blocksRaycasts);
        Assert.AreEqual(economy, Economy());
        Assert.AreSame(stored, Match.Player.Owned["rosie"]); Assert.AreEqual(UnitLocation.Bench, stored.Location);
        Assert.IsFalse(Match.Actors.ContainsKey(stored));
    }

    [UnityTest] public IEnumerator RedLocalSideKeepsOwnOutlinesBelowRivalFilterAndHidesFeedbackForModalReadyAndMenu()
    {
        Assert.IsTrue(Match.StartOnlineMatch(BoardSide.Red, "popow", new[] { "rosie" }, "bugaloo", new[] { "atori" }, 921));
        yield return WaitForPreparation();
        Assert.AreEqual(BoardSide.Red, Match.LocalSide); Assert.IsTrue(Match.Mapper.Flipped);
        ui.Select(Match.Player.Monster);
        yield return null; yield return null;
        AssertFeedback();
        float ownY = Match.Board.GetAllCells().Where(c => c.Side == Match.LocalSide).Average(c => ScreenPoint(c).y);
        float rivalY = Match.Board.GetAllCells().Where(c => c.Side == Match.RivalSide).Average(c => ScreenPoint(c).y);
        Assert.Less(ownY, rivalY, "The rotated local half stays below the rival in screen coordinates.");
        Assert.Greater(ui.GameCamera.WorldToScreenPoint(RivalSurface.bounds.center).y, ownY);
        string economy = Economy();
        yield return Capture("red-rotated-selection");

        ui.ShowInspection(Match.Player.Monster, false);
        yield return null; yield return null;
        AssertHidden();
        ui.CloseModal();
        yield return null; yield return null;
        AssertFeedback();
        Match.LockPreparation(true);
        yield return null; yield return null;
        AssertHidden();
        Match.LockPreparation(false);
        yield return null; yield return null;
        AssertFeedback();
        ui.enabled = false;
        AssertHidden();
        ui.enabled = true;
        yield return null; yield return null;
        AssertFeedback();
        Assert.IsTrue(Match.SetReady(true));
        yield return null; yield return null;
        AssertHidden();
        Assert.IsTrue(Match.SetReady(false));
        yield return null; yield return null;
        AssertFeedback(); Assert.AreEqual(economy, Economy());
        AssertSavedFiles(); savedFiles = null;
        Match.Menu();
        yield return null; yield return null;
        AssertHidden();
    }

    void AssertSavedFiles()
    {
        if (savedFiles != null)
            foreach (var file in savedFiles)
                Assert.AreEqual(file.Value, File.ReadAllText(file.Key), "Selection feedback must not change saved identity, decks or progression.");
    }

    IEnumerator StartLocal()
    {
        Assert.IsTrue(Match.StartMatch("bugaloo", new[] { "rosie" }, 712));
        yield return WaitForPreparation();
    }
    IEnumerator WaitForPreparation()
    {
        yield return null; yield return null;
        float deadline = Time.unscaledTime + 5;
        while ((!ui.BriefReady || ui.MapTransitionActive) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(ui.BriefReady); Assert.IsFalse(ui.MapTransitionActive);
        yield return null; yield return null;
    }
    void AssertFeedback()
    {
        Assert.IsFalse(ui.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.name == "Own placement surface"),
            "The local half has green outlines only, with no filled surface.");
        var surface = RivalSurface;
        Assert.IsNotNull(surface); Assert.IsTrue(surface.gameObject.activeInHierarchy); Assert.IsTrue(surface.enabled);
        Assert.IsNotNull(surface.sprite); Assert.IsNotNull(surface.sharedMaterial);
        Assert.That(surface.color.r, Is.GreaterThan(surface.color.g).And.GreaterThan(surface.color.b));
        Assert.That(surface.color.a, Is.GreaterThan(0).And.LessThan(1));
        Assert.IsNull(surface.GetComponent<Collider>()); Assert.IsNull(surface.GetComponent<Collider2D>());
        Assert.IsNull(surface.GetComponent<Graphic>(), "The board filter must not intercept UI gestures.");
        var rival = Match.Board.GetAllCells().Where(c => c.Side == Match.RivalSide).Select(c => Match.Mapper.GetWorldPosition(c)).ToArray();
        Vector2 half = new Vector2(Mathf.Abs(Match.Mapper.CellSize.x), Mathf.Abs(Match.Mapper.CellSize.y)) * .5f;
        Assert.That(surface.bounds.min.x, Is.EqualTo(rival.Min(p => p.x) - half.x).Within(.001f));
        Assert.That(surface.bounds.max.x, Is.EqualTo(rival.Max(p => p.x) + half.x).Within(.001f));
        Assert.That(surface.bounds.min.y, Is.EqualTo(rival.Min(p => p.y) - half.y).Within(.001f));
        Assert.That(surface.bounds.max.y, Is.EqualTo(rival.Max(p => p.y) + half.y).Within(.001f));
        foreach (var cell in Match.Board.GetAllCells())
        {
            Vector3 point = Match.Mapper.GetWorldPosition(cell);
            bool covered = point.x > surface.bounds.min.x && point.x < surface.bounds.max.x &&
                point.y > surface.bounds.min.y && point.y < surface.bounds.max.y;
            Assert.AreEqual(cell.Side == Match.RivalSide, covered, "Only the rival's cells receive the red filter: " + cell.Coordinates);
        }
        var expected = Match.Board.GetAllCells().Where(c => Match.Config.IsDeploymentLegal(Match.LocalSide, c.Coordinates) && c.OccupiedBy == null).ToArray();
        var lines = Lines;
        Assert.IsNotEmpty(lines); Assert.AreEqual(expected.Length, lines.Length);
        foreach (var line in lines)
        {
            Assert.IsTrue(expected.Any(c => Vector2.Distance(Match.Mapper.GetWorldPosition(c), line.transform.position) < .001f));
            Assert.That(line.startColor.g, Is.GreaterThan(line.startColor.r).And.GreaterThan(line.startColor.b));
            Assert.Greater(line.sortingOrder, surface.sortingOrder);
            Assert.AreEqual(5, line.positionCount); Assert.AreEqual(line.GetPosition(0), line.GetPosition(4));
        }
    }
    void AssertHidden()
    {
        Assert.IsTrue(RivalSurface == null || !RivalSurface.gameObject.activeInHierarchy);
        Assert.IsEmpty(Lines);
        Assert.IsFalse(ui.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.name == "Own placement surface"));
    }
    string Economy()
    {
        var p = Match.Player;
        return p.Coins + "/" + p.OfferVersion + "/" + p.PoolCount + "/" + p.Rerolls + ":" +
            string.Join(";", p.Offers.Select(o => o == null ? "empty" : o.UnitId + "/" + o.Token)) + ":" +
            string.Join(";", p.Owned.OrderBy(pair => pair.Key).Select(pair => pair.Key + "/" + pair.Value.Copies + "/" +
                pair.Value.Paid + "/" + pair.Value.Location + "/" + pair.Value.Deployment + "/" + pair.Value.BriefSlot + "/" +
                pair.Value.NextTrick + "/" + string.Join(",", pair.Value.Tricks)));
    }
    static IEnumerator Capture(string name)
    {
        string folder = Path.GetFullPath(Path.Combine("Logs", "roadmap-v1.2", "board-side-feedback-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.unscaledTime + 15;
        while ((!File.Exists(path) || new FileInfo(path).Length <= 24) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(File.Exists(path), "Native screenshot missing: " + path);
        byte[] bytes = File.ReadAllBytes(path); Assert.Greater(bytes.Length, 24);
        Assert.AreEqual(137, bytes[0]); Assert.AreEqual(80, bytes[1]); Assert.AreEqual(78, bytes[2]); Assert.AreEqual(71, bytes[3]);
        Assert.AreEqual(540, (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]);
        Assert.AreEqual(960, (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
        TestContext.WriteLine("Native board-side feedback capture: " + path);
    }
    Vector2 ScreenPoint(BoardCell cell) => ui.GameCamera.WorldToScreenPoint(Match.Mapper.GetWorldPosition(cell));
    RectTransform Find(string name) => ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == name);
    static PointerEventData Pointer(Vector2 point, int id) => new PointerEventData(EventSystem.current)
        { position = point, pointerId = id, button = PointerEventData.InputButton.Left };
    static Vector2 Center(RectTransform rect)
    { Assert.IsNotNull(rect); Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)); }
}

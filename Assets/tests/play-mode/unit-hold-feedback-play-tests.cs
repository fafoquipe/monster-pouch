using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class UnitHoldFeedbackPlayTests
{
    static readonly string[] DirectoryProperties =
        { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" };
    static readonly PropertyInfo[] Overrides = DirectoryProperties.Select(name =>
        typeof(LocalGameUI).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Static)).ToArray();
    static readonly FieldInfo Inspected = typeof(LocalGameUI).GetField("inspected", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo InspectedEnemy = typeof(LocalGameUI).GetField("inspectedEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo PressedUnit = typeof(LocalGameUI).GetField("pressedUnit", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo DragUnit = typeof(LocalGameUI).GetField("rangeDragUnit", BindingFlags.Instance | BindingFlags.NonPublic);
    LocalGameUI ui;
    InteractionCaptureGameView captureView;
    string root;
    string[] previousDirectories;
    Dictionary<string, string> savedFiles;
    int previousSleep;
    UnitHoldProgressRing Ring => ui.CanvasRoot.GetComponentInChildren<UnitHoldProgressRing>(true);
    RectTransform Sheet => Optional("Inspection shade");
    BoardPointerGesture Board => ui.CanvasRoot.GetComponentInChildren<BoardPointerGesture>();

    [UnitySetUp] public IEnumerator Load()
    {
        previousSleep = Screen.sleepTimeout;
        root = Path.Combine(Path.GetTempPath(), "MonsterPouchHoldFeedbackTests", Guid.NewGuid().ToString("N"));
        previousDirectories = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        string[] folders = { "GuestProfile", "PlayerProgress", "PlayerDecks" };
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, Path.Combine(root, folders[i]));
        // A separate legacy test identity avoids onboarding without altering the user's identity or preferences.
        new LocalGuestProfileStore(Path.Combine(root, "GuestProfile")).OpenOrCreate(true);
        captureView = new InteractionCaptureGameView();
        yield return captureView.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>();
        Assert.IsNotNull(ui);
        ui.Match.enabled = false;
        ui.CloseModal();
        Assert.IsTrue(ui.Match.StartMatch("bugaloo", new[] { "rosie" }, 712));
        yield return null; yield return null;
        float deadline = Time.unscaledTime + 4;
        while ((!ui.BriefReady || ui.MapTransitionActive) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(ui.BriefReady);
        Assert.IsFalse(ui.MapTransitionActive);
        Assert.IsNull(Sheet);
        savedFiles = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            try
            {
                if (savedFiles != null)
                    foreach (var file in savedFiles) Assert.AreEqual(file.Value, File.ReadAllText(file.Key), "Gestures must not change saved progression, decks or identity.");
                if (ui != null)
                {
                    ui.enabled = true;
                    ui.CloseModal();
                    ui.Match.Menu();
                }
            }
            finally
            {
                if (previousDirectories != null)
                    for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, previousDirectories[i]);
                Screen.sleepTimeout = previousSleep;
                string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchHoldFeedbackTests")) + Path.DirectorySeparatorChar;
                if (root != null)
                {
                    string target = Path.GetFullPath(root);
                    Assert.IsTrue(target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                }
            }
            yield return null;
        }
        finally { captureView?.RestoreSelection(); }
        if (captureView != null) yield return captureView.WaitForRestoration();
    }

    [UnityTest] public IEnumerator FieldHoldCountsDownOnceAndCancelsOnReleaseForeignPointerEndDragFocusAndDisable()
    {
        var unit = ui.Match.Player.Monster;
        var presentation = ui.Match.Actors[unit].GetComponent<UnitPresentation>();
        presentation.Freeze();
        Vector2 point = ui.GameCamera.WorldToScreenPoint(ui.Match.Actors[unit].transform.position + Vector3.up * .45f);
        var pointer = Pointer(point, 11);
        Board.OnPointerDown(pointer);
        AssertHidden();

        Board.OnPointerUp(Pointer(point, 12));
        AssertHidden();
        Board.OnPointerUp(pointer);
        AssertHidden();
        Assert.AreEqual(unit.Definition.Id, ui.SelectedId, "A short press preserves ordinary field selection.");
        Assert.IsNull(Sheet);

        Board.OnPointerDown(pointer);
        AssertHidden();
        yield return new WaitForSecondsRealtime(PointerGesture.FeedbackDelay-.1f);
        AssertHidden();Assert.IsNull(Sheet);
        yield return new WaitForSecondsRealtime(.16f);
        AssertFullRing(point);AssertVisibleMesh();
        Assert.That(Ring.Remaining, Is.GreaterThan(0).And.LessThan(1));
        Assert.IsNull(Sheet);
        yield return CaptureFieldHoldProgress();
        yield return WaitForSheet();
        Assert.AreSame(unit, Inspected.GetValue(ui));
        yield return AssertSingleInspection(() => Board.OnPointerUp(pointer));

        foreach (string cancel in new[] { "end-drag", "focus", "pause", "disable", "owner-disable" })
        {
            Board.OnPointerDown(pointer);
            AssertHidden();
            if (cancel == "end-drag") Board.OnEndDrag(pointer);
            else if (cancel == "focus") Board.SendMessage("OnApplicationFocus", false);
            else if (cancel == "pause") Board.SendMessage("OnApplicationPause", true);
            else if (cancel == "disable") { Board.enabled = false; Board.enabled = true; }
            else { ui.enabled = false; yield return null; ui.enabled = true; }
            AssertHidden();
            Board.OnPointerUp(pointer);
            yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.05f);
            Assert.IsNull(Sheet, "Cancelled " + cancel + " must not inspect later.");
        }
    }

    [UnityTest] public IEnumerator VisibleOpponentEchoAndCombatEnemyUseTheSameCountdownAndInspectTheCorrectUnit()
    {
        var snapshot = ui.Match.InitialOpponent;
        var echo = Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None).Single(e => e.UnitId == snapshot.UnitId);
        var pointer = Pointer(ui.GameCamera.WorldToScreenPoint(echo.Renderer.bounds.center), 21);
        Board.OnPointerDown(pointer);
        AssertHidden();
        yield return WaitForSheet();
        Assert.AreEqual(snapshot.UnitId, ((OwnedUnit)Inspected.GetValue(ui)).Definition.Id);
        Assert.IsTrue((bool)InspectedEnemy.GetValue(ui));
        Assert.AreNotSame(ui.Match.Bot.Monster, Inspected.GetValue(ui), "Preparation inspection must use the revealed snapshot.");
        Board.OnPointerUp(pointer); ui.CloseModal();
        yield return null;

        ui.Match.Ready();
        yield return null; yield return null;
        Assert.AreEqual(MatchPhase.Combat, ui.Match.Phase);
        var enemy = ui.Match.Bot.Monster;
        var actor = ui.Match.Actors[enemy];
        actor.GetComponent<UnitPresentation>().Freeze();
        pointer = Pointer(ui.GameCamera.WorldToScreenPoint(actor.transform.position + Vector3.up * .45f), 22);
        Board.OnPointerDown(pointer);
        AssertHidden();
        yield return WaitForSheet();
        Assert.AreSame(enemy, Inspected.GetValue(ui));
        Assert.IsTrue((bool)InspectedEnemy.GetValue(ui));
        yield return AssertSingleInspection(() => Board.OnPointerUp(pointer));
    }

    [UnityTest] public IEnumerator BriefHoldDoesNotBuyAndDragOrCancelledPointerCannotOpenInspectionOrSpend()
    {
        var gesture = ui.CanvasRoot.GetComponentsInChildren<BriefOfferGesture>().First(g => g.UnitId == "rosie");
        var pointer = Pointer(Center((RectTransform)gesture.transform), 31);
        int coins = ui.Match.Player.Coins, count = ui.Match.Player.Owned.Count;
        long[] offers = ui.Match.Player.Offers.Where(o => o != null).Select(o => o.Token).ToArray();
        gesture.OnPointerDown(pointer);
        AssertHidden();
        gesture.OnPointerDown(Pointer(pointer.position + Vector2.right * 40, 32));
        gesture.OnDrag(Pointer(pointer.position + Vector2.right * 50, 32));
        gesture.OnPointerUp(Pointer(pointer.position, 32));
        AssertHidden();
        yield return WaitForSheet();
        Assert.AreEqual("rosie", ((OwnedUnit)Inspected.GetValue(ui)).Definition.Id);
        yield return AssertSingleInspection(() => gesture.OnPointerUp(pointer));
        AssertEconomyUnchanged(coins, count, offers);

        pointer.position = Center((RectTransform)gesture.transform);
        gesture.OnPointerDown(pointer);
        pointer.position += new Vector2(80, 0);
        gesture.OnDrag(pointer);
        AssertHidden();
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.05f);
        Assert.IsNull(Sheet);
        gesture.OnEndDrag(pointer);
        gesture.OnPointerUp(pointer);
        AssertEconomyUnchanged(coins, count, offers);
        foreach (string cancel in new[] { "focus", "pause", "disable" })
        {
            pointer.position = Center((RectTransform)gesture.transform);
            gesture.OnPointerDown(pointer);
            AssertHidden();
            if (cancel == "disable") { gesture.enabled = false; gesture.enabled = true; }
            else gesture.SendMessage(cancel == "focus" ? "OnApplicationFocus" : "OnApplicationPause", cancel == "pause");
            AssertHidden();
            gesture.OnPointerUp(pointer);
            yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.05f);
            Assert.IsNull(Sheet);
            AssertEconomyUnchanged(coins, count, offers);
        }
    }

    [UnityTest] public IEnumerator BankHoldAndCancelledDragPreserveEconomyWhileCompletedDragPaysExactlyOnce()
    {
        var offer = ui.Match.Player.Offers.First(o => o != null && o.UnitId == "rosie");
        int slot = ui.Match.Player.Offers.ToList().IndexOf(offer);
        ui.DropOffer(slot, offer.Token, Center(Find("Bench drop")));
        yield return null; yield return null;
        var unit = ui.Match.Player.Owned["rosie"];
        Assert.AreEqual(UnitLocation.Bench, unit.Location);
        int coins = ui.Match.Player.Coins, copies = unit.Copies, paid = unit.Paid;
        ui.ToggleBankContents(); yield return null;
        var gesture = Find("bank-unit-rosie").GetComponent<UnitCardGesture>();
        var pointer = Pointer(Center((RectTransform)gesture.transform), 41);
        gesture.OnPointerDown(pointer);
        AssertHidden();
        gesture.OnPointerUp(Pointer(pointer.position, 42));
        AssertHidden();
        yield return WaitForSheet();
        Assert.AreSame(unit, Inspected.GetValue(ui));
        yield return AssertSingleInspection(() => gesture.OnPointerUp(pointer));
        AssertOwnedUnchanged(unit, coins, copies, paid, UnitLocation.Bench);

        var cell = ui.Match.Board.GetCell(1, 7);
        Vector2 destination = ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell));
        gesture.OnPointerDown(pointer);
        pointer.position = destination; gesture.OnDrag(pointer);
        AssertHidden();
        gesture.OnEndDrag(pointer);
        gesture.OnPointerUp(pointer);
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.05f);
        Assert.IsNull(Sheet);
        AssertOwnedUnchanged(unit, coins, copies, paid, UnitLocation.Bench);

        pointer.position = Center((RectTransform)gesture.transform);
        gesture.OnPointerDown(pointer);
        gesture.SendMessage("OnApplicationFocus", false);
        AssertHidden();
        gesture.OnPointerUp(pointer);
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.05f);
        Assert.IsNull(Sheet);
        AssertOwnedUnchanged(unit, coins, copies, paid, UnitLocation.Bench);
        int deploymentCost=ui.Match.Player.GetDeploymentCost(unit);
        int committedPaid=paid+unit.BankReservedCost;
        gesture.OnPointerDown(pointer);
        gesture.enabled = false; gesture.enabled = true;
        AssertHidden();
        gesture.OnPointerUp(pointer);
        AssertOwnedUnchanged(unit, coins, copies, paid, UnitLocation.Bench);
        gesture.OnPointerDown(pointer);
        pointer.position = destination; gesture.OnDrag(pointer);
        AssertHidden();
        gesture.OnPointerUp(pointer);
        gesture.OnEndDrag(pointer);
        gesture.OnPointerUp(pointer);
        AssertOwnedUnchanged(unit, coins-deploymentCost, copies, committedPaid, UnitLocation.Field);
        yield return null; yield return null;
        AssertOwnedUnchanged(unit, coins-deploymentCost, copies, committedPaid, UnitLocation.Field);
        Assert.AreEqual(cell.Coordinates, unit.Deployment);
        Assert.IsTrue(ui.Match.Actors.ContainsKey(unit));
        int refund=unit.SaleRefund;
        ui.Drop(unit, Center(Find("Bench drop")));
        yield return null;
        AssertOwnedUnchanged(unit, coins-deploymentCost+refund, copies, committedPaid, UnitLocation.Bench);
    }

    [UnityTest] public IEnumerator OccupiedCellOverridesPriorSelectionAndOverlappingArtForBlueSideSwaps()
    { yield return VerifyCellSelectionAndSwap(); }

    [UnityTest] public IEnumerator OccupiedCellOverridesPriorSelectionAndOverlappingArtForFlippedRedSideSwaps()
    {
        Assert.IsTrue(ui.Match.StartOnlineMatch(BoardSide.Red, "bugaloo", new[] { "rosie" }, "popow", new[] { "atori" }, 712));
        yield return null; yield return null;
        float deadline = Time.unscaledTime + 5;
        while ((!ui.BriefReady || ui.MapTransitionActive) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(ui.BriefReady); Assert.IsFalse(ui.MapTransitionActive); Assert.IsTrue(ui.Match.Mapper.Flipped);
        yield return VerifyCellSelectionAndSwap();
    }

    IEnumerator VerifyCellSelectionAndSwap()
    {
        var first = ui.Match.Player.Monster;
        var origin = ui.Match.Actors[first].CurrentCell;
        var destination = FreeCell();
        var second = DeployRosie(destination);
        ui.RenderPage(); yield return null;
        ui.Select(first);
        var firstActor = ui.Match.Actors[first];
        var secondActor = ui.Match.Actors[second];
        firstActor.GetComponent<UnitPresentation>().Freeze();
        secondActor.GetComponent<UnitPresentation>().Freeze();
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        var overlapping = new GameObject("Selection test oversized figure").AddComponent<SpriteRenderer>();
        overlapping.transform.SetParent(firstActor.transform, false);
        overlapping.transform.position = ui.Match.Mapper.GetWorldPosition(destination);
        overlapping.transform.localScale = new Vector3(Mathf.Abs(ui.Match.Mapper.CellSize.x) * 4, Mathf.Abs(ui.Match.Mapper.CellSize.y) * 4, 1);
        overlapping.sprite = sprite; overlapping.sortingOrder = 32760;
        try
        {
            var hitArt = typeof(LocalGameUI).GetMethod("HitVisibleOwnSprite", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 center = ui.Match.Mapper.GetWorldPosition(destination);
            foreach (var offset in new[] { Vector2.zero, new Vector2(-.42f, -.42f), new Vector2(.42f, -.42f), new Vector2(-.42f, .42f), new Vector2(.42f, .42f) })
            {
                Vector2 point = ui.GameCamera.WorldToScreenPoint(center + new Vector3(offset.x * ui.Match.Mapper.CellSize.x, offset.y * ui.Match.Mapper.CellSize.y, 0));
                Assert.AreSame(first, hitArt.Invoke(ui, new object[] { point }), "The regression must actually overlap the prior unit's art.");
                ui.Select(first);
                var tap = Pointer(point, 61);
                Board.OnPointerDown(tap);
                Assert.AreSame(second, PressedUnit.GetValue(ui), "The occupied cell owns every press, including its corners.");
                Board.OnPointerUp(tap);
                Assert.AreSame(second, ui.SelectedOwned);
                Assert.IsNull(PressedUnit.GetValue(ui));
            }
            int coins = ui.Match.Player.Coins;
            ui.Select(first);
            var drag = Pointer(CellPoint(destination), 62);
            Board.OnPointerDown(drag);
            drag.position = CellPoint(origin); Board.OnDrag(drag);
            Assert.AreSame(second, DragUnit.GetValue(ui), "A new press must drag its own cell's unit, not the prior selection.");
            Board.OnPointerUp(drag); Board.OnEndDrag(drag); Board.OnPointerUp(drag);
            Assert.AreEqual(origin.Coordinates, second.Deployment);
            Assert.AreEqual(destination.Coordinates, first.Deployment);
            Assert.AreSame(secondActor, origin.OccupiedBy); Assert.AreSame(firstActor, destination.OccupiedBy);
            Assert.IsTrue(ui.IsPlacementExchanging);
            Assert.IsTrue(firstActor.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff));

            // A second press uses the committed cell while the visual exchange is still in flight.
            drag = Pointer(CellPoint(destination), 63);
            Board.OnPointerDown(drag);
            Assert.AreSame(first, PressedUnit.GetValue(ui));
            drag.position = CellPoint(origin); Board.OnDrag(drag); Board.OnPointerUp(drag);
            Assert.AreEqual(origin.Coordinates, first.Deployment);
            Assert.AreEqual(destination.Coordinates, second.Deployment);
            Assert.AreSame(firstActor, origin.OccupiedBy); Assert.AreSame(secondActor, destination.OccupiedBy);
            Assert.AreEqual(coins, ui.Match.Player.Coins); Assert.IsNull(Sheet);
            Assert.IsNull(PressedUnit.GetValue(ui)); Assert.IsNull(DragUnit.GetValue(ui));
        }
        finally { Object.Destroy(overlapping.gameObject); Object.Destroy(sprite); }
        yield return null;
    }

    [UnityTest] public IEnumerator OwnerCancellationReleasesCaptureAndForeignPointerCannotReplaceOrDropIt()
    {
        var first = ui.Match.Player.Monster;
        var origin = ui.Match.Actors[first].CurrentCell;
        var destination = FreeCell();
        var second = DeployRosie(destination);
        ui.RenderPage(); yield return null;
        ui.Select(first);
        var oldPointer = Pointer(CellPoint(origin), 71);
        var nextPointer = Pointer(CellPoint(destination), 72);
        Board.OnPointerDown(oldPointer);
        Assert.IsFalse(ui.BeginBoardPointer(nextPointer.position), "The active press cannot be replaced through the UI entry point.");
        Board.OnPointerDown(nextPointer); Board.OnDrag(nextPointer); Board.OnPointerUp(nextPointer);
        Assert.AreSame(first, PressedUnit.GetValue(ui));
        ui.CancelBoardPointer(); // Owner cancellation does not deliver pointer-up to the surface.
        Assert.IsNull(PressedUnit.GetValue(ui)); Assert.IsNull(DragUnit.GetValue(ui));
        Board.OnPointerDown(nextPointer);
        Assert.AreSame(second, PressedUnit.GetValue(ui), "A fresh press can start after an internally cancelled pointer.");
        oldPointer.position = CellPoint(destination);
        Board.OnDrag(oldPointer); Board.OnPointerUp(oldPointer); Board.OnEndDrag(oldPointer);
        Assert.AreSame(second, PressedUnit.GetValue(ui));
        Board.OnPointerUp(nextPointer);
        Assert.AreSame(second, ui.SelectedOwned);
        Assert.AreEqual(origin.Coordinates, first.Deployment); Assert.AreEqual(destination.Coordinates, second.Deployment);

        Board.OnPointerDown(nextPointer);
        ui.ShowInspection(first, false); ui.CloseModal();
        Assert.IsNull(PressedUnit.GetValue(ui));
        nextPointer = Pointer(CellPoint(destination), 73);
        Board.OnPointerDown(nextPointer); Board.OnPointerUp(nextPointer);
        Assert.AreSame(second, ui.SelectedOwned); Assert.IsNull(Sheet); AssertHidden();

        // The owner can be disabled before the delayed feedback disk even exists.
        oldPointer = Pointer(CellPoint(origin), 74); Board.OnPointerDown(oldPointer);
        ui.enabled = false; yield return null; ui.enabled = true;
        Assert.IsNull(PressedUnit.GetValue(ui));
        nextPointer = Pointer(CellPoint(destination), 75); Board.OnPointerDown(nextPointer);
        Board.OnPointerUp(oldPointer); Assert.AreSame(second, PressedUnit.GetValue(ui));
        Board.OnPointerUp(nextPointer); Assert.AreSame(second, ui.SelectedOwned);
    }

    [Test] public void CountdownDiameterUsesPhysicalEstimateAndBoundsInvalidDpiFallback()
    {
        float physical = Diameter(400, 1080) / 400 * 25.4f;
        Assert.That(physical, Is.InRange(25.9f, 26.1f));
        foreach (float dpi in new[] { 0f, -1f, 20f, 10000f, float.NaN, float.PositiveInfinity })
            Assert.That(Diameter(dpi, 1080), Is.InRange(300f, 540f), "Invalid DPI needs a useful stable fallback.");
        Assert.That(Diameter(800, 320), Is.LessThanOrEqualTo(160));
        Assert.That(Diameter(96, 540), Is.GreaterThanOrEqualTo(112));
        Assert.That(Diameter(0, 120), Is.GreaterThan(0).And.LessThanOrEqualTo(60));
    }

    static float Diameter(float dpi, float side) => (float)typeof(UnitHoldProgressRing)
        .GetMethod("DiameterPixels", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { dpi, side });
    BoardCell FreeCell() => ui.Match.Board.GetAllCells().First(cell => cell.OccupiedBy == null &&
        ui.Match.Config.IsDeploymentLegal(ui.Match.LocalSide, cell.Coordinates));
    Vector2 CellPoint(BoardCell cell) => ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell));
    OwnedUnit DeployRosie(BoardCell cell)
    {
        var offer = ui.Match.Player.Offers.First(o => o != null && o.UnitId == "rosie");
        int slot = ui.Match.Player.Offers.ToList().IndexOf(offer);
        Assert.IsTrue(ui.Match.BuyAndPlace(slot, offer.Token, cell));
        return ui.Match.Player.Owned["rosie"];
    }

    static IEnumerator CaptureFieldHoldProgress()
    {
        string folder = Path.GetFullPath(Path.Combine("Logs", "roadmap-v1.2",
            "unit-hold-feedback-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "field-hold-progress.png");
        // Native capture includes the overlay Canvas. A regular frame wait also
        // works in the hidden batch GameView, where WaitForEndOfFrame can stall.
        ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.unscaledTime + 15;
        while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.unscaledTime < deadline)
            yield return null;
        Assert.IsTrue(File.Exists(path), "The native mid-hold screenshot must be saved.");
        Assert.Greater(new FileInfo(path).Length, 0);
        TestContext.WriteLine("Native hold feedback capture: " + path);
        // Inspection may open while the screenshot is written; callers only
        // wait for that result and must not assert the elapsed hold duration.
    }

    IEnumerator WaitForSheet()
    {
        float deadline = Time.unscaledTime + 2;
        while (Sheet == null && Time.unscaledTime < deadline) yield return null;
        Assert.IsNotNull(Sheet, "A stationary press must open inspection after the feedback delay and countdown.");
        AssertHidden();
    }

    IEnumerator AssertSingleInspection(Action release)
    {
        int id = Sheet.GetInstanceID();
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.05f);
        Assert.AreEqual(id, Sheet.GetInstanceID(), "Continuing the press must not recreate inspection.");
        ui.CloseModal();
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.05f);
        Assert.IsNull(Sheet, "Closing inspection while still pressed must not reopen it.");
        release();
        yield return null;
        Assert.IsNull(Sheet, "Release after a completed hold must not become another action.");
        AssertHidden();
    }

    void AssertFullRing(Vector2 point)
    {
        Assert.IsNotNull(Ring);
        Assert.IsTrue(Ring.gameObject.activeInHierarchy);
        Assert.That(Ring.Remaining, Is.GreaterThan(0).And.LessThanOrEqualTo(1));
        Assert.IsFalse(Ring.raycastTarget, "Feedback must not intercept pointer events.");
        Assert.That(Ring.color.r, Is.EqualTo(1));
        Assert.That(Ring.color.g, Is.EqualTo(1));
        Assert.That(Ring.color.b, Is.EqualTo(1));
        Assert.That(Ring.color.a, Is.GreaterThan(0).And.LessThan(1));
        Assert.Less(Vector2.Distance(point, Center(Ring.rectTransform)), 1.1f);
        Assert.That(Ring.rectTransform.rect.width * ui.CanvasRoot.lossyScale.x,
            Is.EqualTo(Diameter(Screen.dpi, Mathf.Min(Screen.width, Screen.height))).Within(1.1f));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(Pointer(point, 100), hits);
        Assert.IsFalse(hits.Any(hit => hit.gameObject == Ring.gameObject));
    }
    void AssertHidden() { Assert.IsTrue(Ring == null || !Ring.gameObject.activeSelf); }
    void AssertVisibleMesh()
    {
        Canvas.ForceUpdateCanvases();
        var mesh = Ring.canvasRenderer.GetMesh();
        Assert.Greater(mesh.vertexCount, 0, "The visible countdown must submit actual geometry.");
        Assert.Greater(mesh.triangles.Length, 0);
        Assert.IsTrue(mesh.colors.All(c => c.r > .99f && c.g > .99f && c.b > .99f && c.a > 0 && c.a < 1));
        float radius = Mathf.Min(Ring.rectTransform.rect.width, Ring.rectTransform.rect.height) * .5f;
        Assert.IsTrue(mesh.vertices.All(v => Vector2.Distance(v, Ring.rectTransform.rect.center) >= radius * .79f),
            "The countdown draws only its border and leaves the fingertip center transparent.");
        Assert.IsTrue(mesh.vertices.Any(v => Vector2.Distance(v, Ring.rectTransform.rect.center) < radius * .9f),
            "The arc keeps a medium-width border.");
        Assert.Less(mesh.colors.Max(c => c.a) - mesh.colors.Min(c => c.a), .01f,
            "There must be no second filled disk or pie layer underneath the arc.");
    }
    void AssertEconomyUnchanged(int coins, int count, long[] offers)
    {
        Assert.AreEqual(coins, ui.Match.Player.Coins);
        Assert.AreEqual(count, ui.Match.Player.Owned.Count);
        CollectionAssert.AreEqual(offers, ui.Match.Player.Offers.Where(o => o != null).Select(o => o.Token));
    }
    void AssertOwnedUnchanged(OwnedUnit unit, int coins, int copies, int paid, UnitLocation location)
    {
        Assert.AreSame(unit, ui.Match.Player.Owned[unit.Definition.Id]);
        Assert.AreEqual(coins, ui.Match.Player.Coins);
        Assert.AreEqual(copies, unit.Copies);
        Assert.AreEqual(paid, unit.Paid);
        Assert.AreEqual(location, unit.Location);
    }
    static PointerEventData Pointer(Vector2 point, int id) => new PointerEventData(EventSystem.current)
        { position = point, pressPosition = point, pointerId = id, button = PointerEventData.InputButton.Left };
    RectTransform Optional(string name) => ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == name);
    RectTransform Find(string name) { var result = Optional(name); Assert.IsNotNull(result, name); return result; }
    static Vector2 Center(RectTransform rect)
    { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)); }
}

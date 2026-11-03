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
using Object = UnityEngine.Object;

public sealed class BriefDragSnapPlayTests
{
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(name => typeof(LocalGameUI).GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    static readonly FieldInfo PreviewCell = typeof(LocalGameUI).GetField("rangeDragCell", BindingFlags.Instance | BindingFlags.NonPublic);
    readonly List<Blocker> blockers = new List<Blocker>();
    LocalGameUI ui;
    InteractionCaptureGameView capture;
    string directory;
    string[] oldDirectories;
    int oldSleep;

    [UnitySetUp] public IEnumerator Setup()
    {
        oldSleep = Screen.sleepTimeout;
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchBriefSnapTests", Guid.NewGuid().ToString("N"));
        oldDirectories = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, Path.Combine(directory, "store-" + i));
        new LocalGuestProfileStore(Path.Combine(directory, "store-0")).OpenOrCreate(true);
        capture = new InteractionCaptureGameView(); yield return capture.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.IsNotNull(ui);
        ui.Match.enabled = false; ui.CloseModal();
        Assert.IsTrue(ui.Match.StartMatch("bugaloo", new[] { "rosie" }, 712));
        ui.Match.Player.AddCombatCoins(100);
        yield return Ready();
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            if (ui != null)
            {
                foreach (var blocker in blockers) ui.Match.Board.ReleaseUnit(blocker);
                ui.CloseModal(); ui.Match.Menu();
            }
            yield return null;
        }
        finally
        {
            for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, oldDirectories[i]);
            Screen.sleepTimeout = oldSleep;
            capture?.RestoreSelection();
            string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchBriefSnapTests")) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(directory);
            Assert.IsTrue(target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
        if (capture != null) yield return capture.WaitForRestoration();
    }

    [UnityTest] public IEnumerator BlueUpperEdgeSnapsToOnlyFreeRearCellAndGapReleaseBuysExactlyOnce()
    { yield return VerifyOnlyFreeRearCell(); }

    [UnityTest] public IEnumerator FlippedRedUpperEdgeSnapsToOnlyFreeOwnRearCellAndGapReleaseBuysExactlyOnce()
    {
        Assert.IsTrue(ui.Match.StartOnlineMatch(BoardSide.Red, "bugaloo", new[] { "rosie" }, "popow", new[] { "atori" }, 712));
        ui.Match.Player.AddCombatCoins(100); yield return Ready();
        Assert.IsTrue(ui.Match.Mapper.Flipped);
        yield return VerifyOnlyFreeRearCell();
    }

    IEnumerator VerifyOnlyFreeRearCell()
    {
        var rear = RearRow();
        Assert.AreEqual(ui.Match.LocalSide == BoardSide.Blue ? 9 : 0, rear[0].Y);
        var target = rear.Last(cell => !cell.IsOccupied);
        FillRearExcept(target);
        var gesture = Offer(out int slot, out var pointer);
        var token = ui.Match.Player.Offers[slot];
        int before = ui.Match.Player.Coins, price = ui.Match.Player.GetOfferCost(slot);
        gesture.OnPointerDown(pointer); pointer.position = GapPoint(pointer.position.x); gesture.OnDrag(pointer);
        Assert.AreSame(target, PreviewCell.GetValue(ui), "Crossing the top jumps the preview across the empty gap.");
        Assert.IsFalse(ui.Match.Player.Owned.ContainsKey("rosie"));
        Assert.AreEqual(before, ui.Match.Player.Coins); Assert.AreSame(token, ui.Match.Player.Offers[slot]);
        gesture.OnPointerUp(pointer);
        var unit = ui.Match.Player.Owned["rosie"];
        Assert.AreEqual(UnitLocation.Field, unit.Location); Assert.AreEqual(target.Coordinates, unit.Deployment);
        Assert.AreSame(ui.Match.Actors[unit], target.OccupiedBy);
        Assert.IsTrue(ui.Match.Config.IsDeploymentLegal(ui.Match.LocalSide, unit.Deployment));
        Assert.AreEqual(before - price, ui.Match.Player.Coins); Assert.IsFalse(ui.IsPlacementExchanging);
        foreach (var blocker in blockers) Assert.AreSame(blocker, blocker.CurrentCell.OccupiedBy);
        string after = State();
        gesture.OnPointerUp(pointer); gesture.OnEndDrag(pointer); gesture.OnPointerUp(pointer);
        ui.DropOffer(slot, token.Token, CellPoint(target));
        Assert.AreEqual(after, State(), "Duplicate releases and a spent offer cannot charge twice.");
        yield return null;
    }

    [UnityTest] public IEnumerator FullRearRowKeepsGapDropInvalidWithoutChoosingForwardOrEnemyCells()
    {
        FillRearExcept(null);
        var gesture = Offer(out _, out var pointer); string before = State();
        gesture.OnPointerDown(pointer); pointer.position = GapPoint(pointer.position.x); gesture.OnDrag(pointer);
        Assert.IsNull(PreviewCell.GetValue(ui)); Assert.AreEqual(before, State());
        gesture.OnPointerUp(pointer);
        Assert.AreEqual(before, State()); Assert.IsFalse(ui.Match.Player.Owned.ContainsKey("rosie"));
        Assert.IsFalse(ui.IsPlacementExchanging);
        yield return null;
    }

    [UnityTest] public IEnumerator ReturningToBriefOrCancellingDisarmsSnapAndDoesNotConsumeOffer()
    {
        var gesture = Offer(out _, out var pointer); Vector2 origin = pointer.position; string before = State();
        gesture.OnPointerDown(pointer); pointer.position = GapPoint(origin.x); gesture.OnDrag(pointer);
        Assert.IsNotNull(PreviewCell.GetValue(ui));
        pointer.position = origin; gesture.OnDrag(pointer); Assert.IsNull(PreviewCell.GetValue(ui));
        gesture.OnPointerUp(pointer); Assert.AreEqual(before, State());
        gesture.OnPointerDown(pointer); pointer.position = GapPoint(origin.x); gesture.OnDrag(pointer);
        Assert.IsNotNull(PreviewCell.GetValue(ui));
        gesture.OnEndDrag(pointer); gesture.OnPointerUp(pointer); Assert.AreEqual(before, State());
        Assert.IsNull(PreviewCell.GetValue(ui));

        // A lateral exit never arms the upper-edge shortcut, even if it later enters the gap.
        pointer.position = origin; gesture.OnPointerDown(pointer);
        var brief = Named("Brief drop");
        Vector2 right = RectTransformUtility.WorldToScreenPoint(null, brief.TransformPoint(new Vector2(brief.rect.xMax, brief.rect.center.y)));
        pointer.position = right + Vector2.right * 20; gesture.OnDrag(pointer);
        pointer.position = new Vector2(pointer.position.x, GapPoint(origin.x).y); gesture.OnDrag(pointer);
        pointer.position = GapPoint(origin.x); gesture.OnDrag(pointer);
        Assert.IsNull(PreviewCell.GetValue(ui)); gesture.OnPointerUp(pointer); Assert.AreEqual(before, State());
        yield return null;
    }

    [UnityTest] public IEnumerator SnappedPreviewTracksFingerXThenNormalBoardTargetTakesOver()
    {
        var gesture = Offer(out _, out var pointer);
        gesture.OnPointerDown(pointer); pointer.position = GapPoint(pointer.position.x); gesture.OnDrag(pointer);
        var free = RearRow().Where(cell => !cell.IsOccupied).OrderBy(cell => CellPoint(cell).x).ToArray();
        foreach (var cell in new[] { free.First(), free.Last() })
        {
            pointer.position = GapPoint(CellPoint(cell).x); gesture.OnDrag(pointer);
            Assert.AreSame(cell, PreviewCell.GetValue(ui), "The same drag can choose a different free rear column.");
        }
        var normal = ui.Match.Board.GetAllCells().First(cell => !cell.IsOccupied && cell.Y != free[0].Y &&
            ui.Match.Config.IsDeploymentLegal(ui.Match.LocalSide, cell.Coordinates));
        pointer.position = CellPoint(normal); gesture.OnDrag(pointer);
        Assert.AreSame(normal, PreviewCell.GetValue(ui));
        gesture.OnPointerUp(pointer);
        Assert.AreEqual(normal.Coordinates, ui.Match.Player.Owned["rosie"].Deployment);
        yield return null;
    }

    [UnityTest] public IEnumerator ReturningStoredBriefUnitCancelsWithoutSellingAndAnotherDragCanSnapIt()
    {
        Offer(out int slot, out _);
        Assert.IsTrue(ui.Match.Buy(slot, ui.Match.Player.Offers[slot].Token));
        ui.RenderPage(); yield return Ready();
        var unit = ui.Match.Player.Owned["rosie"]; Assert.AreEqual(UnitLocation.Brief, unit.Location);
        var card = Named("brief-rosie"); var gesture = card.GetComponent<UnitCardGesture>();
        var pointer = Pointer(Center(card)); var origin = pointer.position; string before = State();
        gesture.OnPointerDown(pointer); pointer.position = GapPoint(origin.x); gesture.OnDrag(pointer);
        Assert.IsNotNull(PreviewCell.GetValue(ui));
        pointer.position = origin; gesture.OnDrag(pointer); gesture.OnPointerUp(pointer);
        Assert.AreEqual(before, State()); Assert.IsTrue(ui.Match.Player.Owns(unit));
        gesture.OnPointerDown(pointer); pointer.position = GapPoint(origin.x); gesture.OnDrag(pointer);
        var destination = (BoardCell)PreviewCell.GetValue(ui); Assert.IsNotNull(destination);
        int coins = ui.Match.Player.Coins;
        gesture.OnPointerUp(pointer);
        Assert.AreEqual(UnitLocation.Field, unit.Location); Assert.AreEqual(destination.Coordinates, unit.Deployment);
        Assert.AreEqual(coins, ui.Match.Player.Coins, "The already-paid Brief unit is not purchased a second time.");
        yield return null;
    }

    [UnityTest] public IEnumerator BlueGapOutsideBoardWidthClampsToNearestFreeRearCell()
    { yield return VerifyGapOutsideBoardWidth(); }

    [UnityTest] public IEnumerator FlippedRedGapOutsideBoardWidthClampsToNearestFreeOwnRearCell()
    {
        Assert.IsTrue(ui.Match.StartOnlineMatch(BoardSide.Red, "bugaloo", new[] { "rosie" }, "popow", new[] { "atori" }, 712));
        ui.Match.Player.AddCombatCoins(100); yield return Ready();
        Assert.IsTrue(ui.Match.Mapper.Flipped);
        yield return VerifyGapOutsideBoardWidth();
    }

    IEnumerator VerifyGapOutsideBoardWidth()
    {
        var rear = RearRow();
        var free = rear.Where(cell => !cell.IsBlocked && !cell.IsOccupied && !cell.IsReserved)
            .OrderBy(cell => CellPoint(cell).x).ToArray();
        Assert.IsNotEmpty(free);
        float left = float.PositiveInfinity, right = float.NegativeInfinity;
        var half = new Vector3(Mathf.Abs(ui.Match.Mapper.CellSize.x), Mathf.Abs(ui.Match.Mapper.CellSize.y), 0) * .5f;
        foreach (var cell in rear)
            foreach (float dx in new[] { -half.x, half.x })
                foreach (float dy in new[] { -half.y, half.y })
                {
                    float x = ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell) + new Vector3(dx, dy)).x;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                }
        float margin = Mathf.Max(1, (right - left) * .02f);
        var gesture = Offer(out int slot, out var pointer);
        string before = State(); int coins = ui.Match.Player.Coins, price = ui.Match.Player.GetOfferCost(slot);
        var brief = Named("Brief drop");
        float briefLeft = RectTransformUtility.WorldToScreenPoint(null,
            brief.TransformPoint(new Vector2(brief.rect.xMin, brief.rect.center.y))).x;
        float briefRight = RectTransformUtility.WorldToScreenPoint(null,
            brief.TransformPoint(new Vector2(brief.rect.xMax, brief.rect.center.y))).x;
        Assert.IsTrue(briefLeft < left || briefRight > right, "This regression needs the Brief to extend beyond a board edge.");
        float exitX = briefLeft < left ? (briefLeft + left) * .5f : (right + briefRight) * .5f;
        gesture.OnPointerDown(pointer);
        // Move within the Brief, then cross its top outside the board width. No fixed screen coordinates.
        pointer.position = new Vector2(exitX, pointer.position.y);
        Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(brief, pointer.position));
        gesture.OnDrag(pointer);
        pointer.position = GapPoint(exitX); gesture.OnDrag(pointer);
        Assert.AreSame(free.OrderBy(cell => Mathf.Abs(CellPoint(cell).x - exitX)).First(), PreviewCell.GetValue(ui),
            "A valid top crossing outside the board width must arm the snap immediately.");
        pointer.position = GapPoint(left - margin); gesture.OnDrag(pointer);
        Assert.AreSame(free.First(), PreviewCell.GetValue(ui), "Outside the left board edge still selects the nearest free own rear cell.");
        Assert.AreEqual(before, State(), "A snapped preview never purchases the offer.");
        pointer.position = GapPoint(right + margin); gesture.OnDrag(pointer);
        Assert.AreSame(free.Last(), PreviewCell.GetValue(ui), "Outside the right board edge still selects the nearest free own rear cell.");
        Assert.AreEqual(before, State());
        gesture.OnPointerUp(pointer);
        var unit = ui.Match.Player.Owned["rosie"];
        Assert.AreEqual(UnitLocation.Field, unit.Location); Assert.AreEqual(free.Last().Coordinates, unit.Deployment);
        Assert.IsTrue(ui.Match.Config.IsDeploymentLegal(ui.Match.LocalSide, unit.Deployment));
        Assert.AreEqual(coins - price, ui.Match.Player.Coins);
        string after = State(); gesture.OnPointerUp(pointer); gesture.OnEndDrag(pointer);
        Assert.AreEqual(after, State(), "The outside-width gap release commits only once.");
        yield return null;
    }

    IEnumerator Ready()
    {
        yield return null; yield return null;
        float deadline = Time.unscaledTime + 8;
        while ((!ui.BriefReady || ui.MapTransitionActive || LoadingArtworkScreen.IsBlocking) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(ui.BriefReady); Assert.IsFalse(ui.MapTransitionActive); Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        Canvas.ForceUpdateCanvases();
    }
    BriefOfferGesture Offer(out int slot, out PointerEventData pointer)
    {
        slot = ui.Match.Player.Offers.ToList().FindIndex(offer => offer != null && offer.UnitId == "rosie");
        Assert.GreaterOrEqual(slot, 0);
        var card = Named("offer-" + slot); pointer = Pointer(Center(card)); return card.GetComponent<BriefOfferGesture>();
    }
    BoardCell[] RearRow() => ui.Match.Board.GetAllCells().Where(cell => ui.Match.Config.IsDeploymentLegal(ui.Match.LocalSide, cell.Coordinates))
        .GroupBy(cell => cell.Y).OrderBy(row => row.Average(cell => CellPoint(cell).y)).First().ToArray();
    void FillRearExcept(BoardCell available)
    {
        foreach (var cell in RearRow())
        {
            if (cell == available || cell.IsOccupied) continue;
            var blocker = new Blocker(ui.Match.LocalSide); Assert.IsTrue(ui.Match.Board.TryOccupyCell(blocker, cell)); blockers.Add(blocker);
        }
    }
    Vector2 GapPoint(float x)
    {
        var brief = Named("Brief drop");
        float top = RectTransformUtility.WorldToScreenPoint(null, brief.TransformPoint(new Vector2(brief.rect.center.x, brief.rect.yMax))).y;
        float bottom = RearRow().Min(cell => ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell) +
            Vector3.down * Mathf.Abs(ui.Match.Mapper.CellSize.y) * .5f).y);
        Assert.Greater(bottom, top, "The regression needs the actual gap between Brief and board.");
        var point = new Vector2(x, (top + bottom) * .5f);
        Assert.IsFalse(RectTransformUtility.RectangleContainsScreenPoint(brief, point));
        Assert.IsFalse(ui.Match.Mapper.TryGetCell(ui.GameCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, -ui.GameCamera.transform.position.z)), out _));
        return point;
    }
    string State() => ui.Match.Player.Coins + ":" + ui.Match.Player.OfferVersion + ":" +
        string.Join(";", ui.Match.Player.Offers.Select(o => o == null ? "empty" : o.UnitId + "/" + o.Token)) + ":" +
        string.Join(";", ui.Match.Player.Owned.Values.Select(u => u.Definition.Id + "/" + u.Copies + "/" + u.Paid + "/" + u.Location + "/" + u.Deployment));
    Vector2 CellPoint(BoardCell cell) => ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell));
    RectTransform Named(string name) => ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == name);
    static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
    static PointerEventData Pointer(Vector2 point) => new PointerEventData(EventSystem.current)
        { pointerId = 91, button = PointerEventData.InputButton.Left, position = point, pressPosition = point };
    sealed class Blocker : IBoardUnit
    {
        public string UnitId => "brief-snap-test-blocker";
        public BoardSide Side { get; }
        public int IQSpeed => 0;
        public BoardCell CurrentCell { get; private set; }
        public BoardCell ReservedCell { get; private set; }
        public Blocker(BoardSide side) { Side = side; }
        public void SetCurrentCell(BoardCell cell) { CurrentCell = cell; }
        public void SetReservedCell(BoardCell cell) { ReservedCell = cell; }
        public void ClearReservedCell() { ReservedCell = null; }
        public void ClearBoardState() { CurrentCell = ReservedCell = null; }
    }
}

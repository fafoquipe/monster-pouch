using System;
using System.Collections;
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

public sealed class ValidCellFeedbackPlayTests
{
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(name => typeof(LocalGameUI).GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    LocalGameUI ui;
    InteractionCaptureGameView captureView;
    string directory;
    string[] previousDirectories;
    int rejected, rejectSounds;
    MatchController Match => ui.Match;

    [UnitySetUp] public IEnumerator Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchValidCellFeedbackTests", Guid.NewGuid().ToString("N"));
        previousDirectories = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, Path.Combine(directory, "store-" + i));
        new LocalGuestProfileStore(Path.Combine(directory, "store-0")).OpenOrCreate(true);
        captureView = new InteractionCaptureGameView();
        yield return captureView.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.IsNotNull(ui);
        Match.enabled = false; ui.CloseModal();
        Assert.IsTrue(Match.StartMatch("bugaloo", new[] { "rosie" }, 712));
        float deadline = Time.unscaledTime + 4;
        while ((!ui.BriefReady || ui.MapTransitionActive) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(ui.BriefReady); Assert.IsFalse(ui.MapTransitionActive);
        yield return null; yield return null;
        Match.ActionRejected += Rejected;
        Match.Sound += Sound;
        rejected = rejectSounds = 0;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            if (ui != null)
            {
                Match.ActionRejected -= Rejected; Match.Sound -= Sound;
                ui.CloseModal(); Match.Menu();
            }
            yield return null;
        }
        finally
        {
            try
            {
                if (previousDirectories != null)
                    for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, previousDirectories[i]);
                if (directory != null)
                {
                    string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchValidCellFeedbackTests")) + Path.DirectorySeparatorChar;
                    string target = Path.GetFullPath(directory);
                    Assert.IsTrue(target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                }
            }
            finally { captureView?.RestoreSelection(); }
        }
        if (captureView != null) yield return captureView.WaitForRestoration();
    }

    [UnityTest] public IEnumerator FieldDropOutsideReturnsToItsOriginSilentlyAndKeepsOnlyTheExistingLegalHighlights()
    {
        var unit = Match.Player.Monster;
        var actor = Match.Actors[unit];
        var origin = unit.Deployment;
        var cell = actor.CurrentCell;
        Vector3 position = actor.transform.position;
        int coins = Match.Player.Coins;
        string notice = Match.Notice;
        ui.Select(unit);
        yield return null; yield return null;
        AssertLegalHighlightSet();

        var destination = Match.Board.GetCell(0, 8);
        ui.DragPreview(unit, ScreenPoint(destination));
        Assert.IsNotNull(Find("Dragged Whelp"));
        Assert.AreEqual(origin, unit.Deployment, "Hovering previews a move without moving the unit.");
        yield return CaptureDragHighlights();
        ui.Drop(unit, new Vector2(-100, -100));
        Assert.IsNull(Find("Dragged Whelp"), "An invalid release immediately hides the preview.");
        yield return null; yield return null;
        Assert.AreEqual(0, rejected); Assert.AreEqual(0, rejectSounds);
        Assert.AreEqual(notice, Match.Notice);
        Assert.IsNull(ui.CanvasRoot.GetComponentInChildren<TransientNoticeView>());
        Assert.AreEqual(origin, unit.Deployment);
        Assert.AreSame(actor, Match.Actors[unit]); Assert.AreSame(cell, actor.CurrentCell);
        Assert.AreSame(actor, cell.OccupiedBy); Assert.AreEqual(position, actor.transform.position);
        Assert.AreEqual(coins, Match.Player.Coins);
        AssertLegalHighlightSet();

        // This request removes only the generic off-board message. Actual rule
        // failures still reach the existing central feedback and simulation.
        ui.Drop(unit, ScreenPoint(Match.Board.GetCell(0, 0)));
        Assert.AreEqual(1, rejected); Assert.AreEqual(1, rejectSounds);
        Assert.AreEqual(origin, unit.Deployment); Assert.AreSame(actor, cell.OccupiedBy);
    }

    [UnityTest] public IEnumerator BankDropOutsideKeepsItsUnitAndCoinsWhilePurchaseErrorsAndValidDeploymentStillWork()
    {
        var offer = Match.Player.Offers.First(o => o != null && o.UnitId == "rosie");
        int slot = Match.Player.Offers.ToList().IndexOf(offer);
        int initialCoins = Match.Player.Coins;
        Match.Player.AddCombatCoins(-initialCoins);
        ui.DropOffer(slot, offer.Token, ScreenPoint(Match.Board.GetCell(1, 7)));
        Assert.AreEqual(1, rejected); Assert.AreEqual(1, rejectSounds);
        Assert.IsFalse(Match.Player.Owned.ContainsKey("rosie"));
        Assert.That(Match.Notice, Does.Contain("Moonkens"), "Insufficient funds must retain its useful error.");
        Match.Player.AddCombatCoins(initialCoins);
        ui.DropOffer(slot, offer.Token, Center(Find("Bench drop")));
        yield return null; yield return null;
        var unit = Match.Player.Owned["rosie"];
        Assert.AreEqual(UnitLocation.Bench, unit.Location);
        int coins = Match.Player.Coins, copies = unit.Copies, paid = unit.Paid;
        int reserved = unit.BankReservedCost, cost = unit.BankRedeployCost;
        Assert.AreEqual(initialCoins, coins); Assert.AreEqual(0, paid);
        string notice = Match.Notice;
        rejected = rejectSounds = 0;
        ui.ToggleBankContents(); yield return null;
        var gesture = Find("bank-unit-rosie").GetComponent<UnitCardGesture>();
        var pointer = new PointerEventData(EventSystem.current) { position = Center((RectTransform)gesture.transform), pointerId = 71 };
        gesture.OnPointerDown(pointer);
        pointer.position = new Vector2(-100, -100);
        gesture.OnDrag(pointer);
        Assert.IsNotNull(Find("Dragged Whelp"));
        gesture.OnPointerUp(pointer);
        gesture.OnEndDrag(pointer);
        Assert.IsNull(Find("Dragged Whelp"));
        yield return null; yield return null;
        Assert.AreEqual(0, rejected); Assert.AreEqual(0, rejectSounds);
        Assert.AreEqual(notice, Match.Notice);
        Assert.AreSame(unit, Match.Player.Owned["rosie"]);
        Assert.AreEqual(UnitLocation.Bench, unit.Location);
        Assert.AreEqual(coins, Match.Player.Coins); Assert.AreEqual(copies, unit.Copies); Assert.AreEqual(paid, unit.Paid);
        Assert.IsFalse(Match.Actors.ContainsKey(unit));
        Assert.IsNotNull(Find("bank-unit-rosie"), "The source card is restored after cancelling its preview.");
        Assert.AreEqual(1, Find("Bank contents").GetComponent<CanvasGroup>().alpha);

        var target = Match.Board.GetCell(1, 7);
        ui.Drop(unit, ScreenPoint(target));
        yield return null; yield return null;
        Assert.AreEqual(UnitLocation.Field, unit.Location); Assert.AreEqual(target.Coordinates, unit.Deployment);
        Assert.AreSame(unit, Match.Player.Owned["rosie"]);
        Assert.AreEqual(coins - cost, Match.Player.Coins); Assert.AreEqual(copies, unit.Copies); Assert.AreEqual(paid + reserved, unit.Paid);
        Assert.AreEqual(0, unit.BankRedeployCost); Assert.AreSame(Match.Actors[unit], target.OccupiedBy);
    }

    void AssertLegalHighlightSet()
    {
        var highlights = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Where(line => line.name == "Legal cell").ToArray();
        var expected = Match.Board.GetAllCells().Where(cell => Match.Config.IsDeploymentLegal(Match.LocalSide, cell.Coordinates) && cell.OccupiedBy == null).ToArray();
        Assert.IsNotEmpty(highlights); Assert.AreEqual(expected.Length, highlights.Length);
        foreach (var line in highlights)
        {
            Assert.IsTrue(line.enabled); Assert.IsNotNull(line.sharedMaterial);
            Assert.IsTrue(expected.Any(cell => Vector2.Distance(Match.Mapper.GetWorldPosition(cell), line.transform.position) < .001f),
                "Only the existing free deployment cells receive the stronger highlight.");
        }
    }

    static IEnumerator CaptureDragHighlights()
    {
        string folder = Path.GetFullPath(Path.Combine("Logs", "roadmap-v1.2", "valid-cell-feedback-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "drag-valid-cells.png");
        ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.unscaledTime + 15;
        while ((!File.Exists(path) || new FileInfo(path).Length <= 24) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(File.Exists(path), "Native drag screenshot missing: " + path);
        byte[] bytes = File.ReadAllBytes(path); Assert.Greater(bytes.Length, 24);
        Assert.AreEqual(137, bytes[0]); Assert.AreEqual(80, bytes[1]); Assert.AreEqual(78, bytes[2]); Assert.AreEqual(71, bytes[3]);
        Assert.AreEqual(Screen.width, (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]);
        Assert.AreEqual(Screen.height, (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
        TestContext.WriteLine("Native valid-cell capture: " + path);
    }
    void Rejected(string message) { rejected++; }
    void Sound(string sound) { if (sound == "reject") rejectSounds++; }
    Vector2 ScreenPoint(BoardCell cell) => ui.GameCamera.WorldToScreenPoint(Match.Mapper.GetWorldPosition(cell));
    RectTransform Find(string name) => ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == name);
    static Vector2 Center(RectTransform rect)
    { Assert.IsNotNull(rect); Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)); }
}

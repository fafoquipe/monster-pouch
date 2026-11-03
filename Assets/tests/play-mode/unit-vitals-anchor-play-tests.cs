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
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UnitVitalsAnchorPlayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(name => typeof(LocalGameUI).GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    sealed class Figure
    {
        public BattleUnit Actor;
        public OwnedUnit Owned;
        public UnitPresentation View;
        public UnitArt Art;
        public UnitVitalsUI Vitals;
    }
    LocalGameUI ui;
    InteractionCaptureGameView captureView;
    readonly List<Figure> figures = new List<Figure>();
    string directory, previousLobbyStyle, captureDirectory;
    string[] previousDirectories;
    bool hadLobbyStyle, savedPreferences;
    int previousSleep, sequence;
    MatchController Match => ui.Match;

    [UnitySetUp] public IEnumerator Setup()
    {
        previousSleep = Screen.sleepTimeout;
        hadLobbyStyle = PlayerPrefs.HasKey(CardTableTheme.LobbyPreferenceKey);
        previousLobbyStyle = PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey);
        savedPreferences = true;
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchVitalsAnchorTests", Guid.NewGuid().ToString("N"));
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
        captureDirectory = Path.GetFullPath(Path.Combine("Logs", "roadmap-v1.2", "vitals-head-anchors-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            DestroyFigures();
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
                    string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchVitalsAnchorTests")) + Path.DirectorySeparatorChar;
                    string target = Path.GetFullPath(directory);
                    Assert.IsTrue(target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                }
            }
            finally { captureView?.RestoreSelection(); }
        }
        if (captureView != null) yield return captureView.WaitForRestoration();
    }

    [UnityTest] public IEnumerator AllCurrentCharactersKeepTheirHudJustAboveTheHeadAndFollowResizing()
    {
        yield return StartPresentationReview(BoardSide.Blue);
        // Running matches intentionally contain plain definitions only. Use the same
        // authored catalog that LocalGameUI reads before creating its runtime art copy.
        var sourceConfig = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        Assert.IsNotNull(sourceConfig);
        var roster = sourceConfig.Characters.Where(c => c != null).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Assert.GreaterOrEqual(roster.Length, 23, "Review every current character, including later roster additions.");
        for (int start = 0; start < roster.Length; start += 6)
        {
            DestroyFigures(); yield return null;
            for (int i = start; i < Math.Min(start + 6, roster.Length); i++)
            {
                int index = i - start;
                var figure = CreateFigure(roster[i].Id, index % 2 == 0 ? BoardSide.Blue : BoardSide.Red,
                    CellPoint(1 + index % 3 * 2, 3 + index / 3 * 4) - Vector3.right * Mathf.Abs(Match.Mapper.CellSize.x) * .5f);
                Assert.Greater(figure.Art.VitalsHeadAnchor.y, 0, roster[i].Id + " needs its own calibrated head anchor.");
                TestContext.WriteLine("Gallery " + (start / 6 + 1) + ", position " + (index + 1) + ": " + roster[i].Id + " / " + figure.Owned.Definition.DisplayName);
            }
            yield return null; yield return null;
            foreach (var figure in figures) AssertHeadClearance(figure);
            yield return Capture("roster-" + (start + 1).ToString("00") + "-" + Math.Min(start + 6, roster.Length).ToString("00"));
            foreach (var figure in figures)
            {
                // Use a runtime copy so the size check cannot rewrite an authored character.
                var scaled = JsonUtility.FromJson<UnitArt>(JsonUtility.ToJson(figure.Art));
                float tileWidth = Mathf.Abs(Match.Mapper.CellSize.x);
                float desiredWidth = figure.Art.AuthoredWorldWidth * figure.Art.GetInGameScale(tileWidth) * 1.25f;
                scaled.InGameSizePercent = -1;
                scaled.DisplayScale = desiredWidth / (UnitArt.AuthoredScaleMultiplier * scaled.WorldWidth);
                Vector3 before = figure.View.VitalsHeadWorldPosition - figure.Actor.transform.position;
                string stats = JsonUtility.ToJson(figure.Actor.BaseStats);
                figure.View.Configure(figure.Actor, Match.Mapper, scaled); figure.View.Face(0, 1);
                Vector3 after = figure.View.VitalsHeadWorldPosition - figure.Actor.transform.position;
                Assert.That(Vector3.Distance(after, before * 1.25f), Is.LessThan(.003f), figure.Art.Id + " must retain the head point when resized.");
                Assert.AreEqual(stats, JsonUtility.ToJson(figure.Actor.BaseStats));
            }
            yield return null; yield return null;
            foreach (var figure in figures) AssertHeadClearance(figure);
            if (start == 0) yield return Capture("roster-01-06-size-125-percent");
        }
    }

    [UnityTest] public IEnumerator ClosestCharacterDrawsLastRegardlessOfSpawnOrderForBothPlayerPerspectives()
    {
        foreach (var side in new[] { BoardSide.Blue, BoardSide.Red })
        {
            DestroyFigures();
            yield return StartPresentationReview(side);
            Assert.AreEqual(side == BoardSide.Red, Match.Mapper.Flipped);
            Vector3 localPoint = CellPoint(2, side == BoardSide.Blue ? 7 : 2);
            Vector3 rivalPoint = CellPoint(2, side == BoardSide.Blue ? 2 : 7);
            Assert.Less(ui.GameCamera.WorldToScreenPoint(localPoint).y, ui.GameCamera.WorldToScreenPoint(rivalPoint).y,
                "Both perspectives put the local formation nearer the player.");
            // Spawn the nearer unit first: creation order is deliberately the wrong drawing order.
            var nearer = CreateFigure("atori", side, localPoint);
            var farther = CreateFigure("atori", side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue, localPoint + Vector3.up * .10f);
            yield return null; yield return null;
            AssertDrawnInFront(nearer, farther);
            AssertHeadClearance(nearer); AssertHeadClearance(farther);
            var parent = nearer.Vitals.transform.parent;
            Assert.AreEqual("Unit vitals", parent.name);
            Assert.AreSame(parent, farther.Vitals.transform.parent);
            var pause = ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b => b.name == "Battle pause");
            Assert.AreSame(parent.parent, pause.transform.parent);
            Assert.Less(parent.GetSiblingIndex(), pause.transform.GetSiblingIndex(), "Sorting the bars cannot cover battle controls.");
            yield return Capture(side == BoardSide.Blue ? "overlap-blue-perspective" : "overlap-red-perspective");
            Vector3 oldNear = nearer.Actor.transform.position;
            nearer.Actor.transform.position = farther.Actor.transform.position;
            farther.Actor.transform.position = oldNear;
            yield return null; yield return null;
            AssertDrawnInFront(farther, nearer);
            AssertHeadClearance(nearer); AssertHeadClearance(farther);
        }
    }

    [UnityTest] public IEnumerator EnergyShieldAndStatusesStayTogetherAndDeadOrRevivingUnitsHideTheirHud()
    {
        yield return StartPresentationReview(BoardSide.Blue);
        var figure = CreateFigure("stein", BoardSide.Blue, CellPoint(2, 6));
        figure.Actor.ApplyDamage(7);
        figure.Actor.AddEnergy(figure.Actor.BaseStats.EnergyMax * .5f);
        yield return null; yield return null;
        AssertHeadClearance(figure);
        Assert.IsTrue(figure.Vitals.ShowsEnergy);
        Assert.That(figure.Vitals.EnergyFraction, Is.EqualTo(.5f).Within(.001f));
        float energyBottom = figure.Vitals.BottomEdge;
        typeof(BattleUnit).GetMethod("AddShield", Private).Invoke(figure.Actor, new object[] { 10, int.MaxValue });
        yield return null; yield return null;
        AssertHeadClearance(figure);
        Assert.Less(figure.Vitals.BottomEdge, energyBottom);
        Assert.That(figure.Vitals.ShieldFraction, Is.EqualTo(10f / figure.Actor.BaseStats.MaxHealth).Within(.001f));
        var statuses = figure.Vitals.GetComponent<UnitStatusBadges>();
        statuses.Present(new[] { new ActiveStatus("shield", -1), new ActiveStatus("haste", 4) }, new PassiveProgress[0], 0);
        yield return null;
        Assert.AreEqual(2, statuses.VisibleCount);
        Assert.IsTrue(statuses.GetComponentsInChildren<CombatBadgeRing>().All(b => b.transform.IsChildOf(figure.Vitals.transform)));
        Canvas.ForceUpdateCanvases();
        var mesh = figure.Vitals.canvasRenderer.GetMesh();
        try
        {
            Assert.IsTrue(mesh.colors.Any(c => c.r > .99f && c.g > .99f && c.b > .99f), "The shield remains white.");
            Assert.IsTrue(mesh.colors.Any(c => c.r > .9f && c.g > .6f && c.b < .3f), "Energy remains gold.");
        }
        finally { Object.Destroy(mesh); }
        yield return Capture("health-energy-shield-statuses");
        typeof(BattleUnit).GetMethod("BeginReviving", Private).Invoke(figure.Actor, null);
        yield return null; yield return null;
        Assert.IsFalse(figure.Vitals.gameObject.activeSelf);
        figure.Actor.ResetForCombat();
        yield return null; yield return null;
        Assert.IsTrue(figure.Vitals.gameObject.activeSelf); AssertHeadClearance(figure);
        figure.Actor.ApplyDamage(figure.Actor.CurrentHealth);
        yield return null; yield return null;
        Assert.IsFalse(figure.Vitals.gameObject.activeSelf);

        var summon = CreateFigure("dummy", BoardSide.Red, CellPoint(4, 6), true);
        yield return null; yield return null;
        Assert.IsTrue(summon.Actor.IsCombatSummon); Assert.IsTrue(summon.Vitals.gameObject.activeSelf);
        AssertHeadClearance(summon);
        summon.Actor.ApplyDamage(summon.Actor.CurrentHealth);
        yield return null; yield return null;
        Assert.IsFalse(summon.Vitals.gameObject.activeSelf);
    }

    IEnumerator StartPresentationReview(BoardSide localSide)
    {
        ui.CloseModal(); Match.Menu(); yield return null;
        Assert.IsTrue(Match.StartOnlineMatch(localSide, "popow", new[] { "stein" }, "bugaloo", new[] { "atori" }, 722));
        yield return null; yield return null;
        float deadline = Time.unscaledTime + 5;
        while ((!ui.BriefReady || ui.MapTransitionActive) && Time.unscaledTime < deadline) yield return null;
        Assert.IsFalse(ui.MapTransitionActive); Assert.IsTrue(ui.BriefReady);
        Match.SetReady(true); Match.SetOpponentReady(true);
        Assert.IsTrue(Match.RevealOpponent(new PreparationCommand[0], Match.Round));
        Assert.IsTrue(Match.StartOnlineCombat(Match.Round));
        typeof(MatchController).GetMethod("ClearActors", Private).Invoke(Match, null);
        Match.Board.BuildBoard(); ui.RenderPage();
        yield return new WaitForSecondsRealtime(.3f); yield return null;
        Assert.AreEqual(MatchPhase.Combat, Match.Phase);
        Assert.AreEqual(0, Match.Actors.Count, "The presentation gallery must not advance or modify combat actors.");
    }

    Figure CreateFigure(string id, BoardSide side, Vector3 position, bool summoned = false)
    {
        var definition = summoned && id == "dummy"
            ? ExpandedRoster.CreateSummonedDummy(UnitStats.FromDefinition(Match.Config.Get("kayon")))
            : Match.Config.Get(id);
        Assert.IsNotNull(definition, id);
        var owned = new OwnedUnit(definition);
        var go = new GameObject("Vitals review " + id);
        var actor = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
        actor.Initialize("vitals-review-" + id + "-" + ++sequence, side, UnitStats.FromDefinition(definition, owned), summoned);
        actor.transform.position = position;
        typeof(LocalGameUI).GetMethod("CreateVisual", Private).Invoke(ui, new object[] { actor, owned });
        var view = go.GetComponent<UnitPresentation>(); view.Face(0, 1); view.enabled = false;
        var catalog = (UnitArtCatalog)typeof(LocalGameUI).GetField("art", Private).GetValue(ui);
        var figure = new Figure { Actor = actor, Owned = owned, View = view, Art = catalog.Get(id),
            Vitals = ui.CanvasRoot.GetComponentsInChildren<UnitVitalsUI>(true).Single(v => v.name == "health-" + actor.UnitId) };
        figures.Add(figure);
        return figure;
    }

    void DestroyFigures()
    {
        foreach (var figure in figures)
        {
            if (ui != null)
                foreach (string field in new[] { "visuals", "bars", "barViews" })
                    ((IDictionary)typeof(LocalGameUI).GetField(field, Private).GetValue(ui)).Remove(figure.Actor);
            if (figure.Vitals != null) Object.Destroy(figure.Vitals.gameObject);
            if (figure.Actor != null) Object.Destroy(figure.Actor.gameObject);
        }
        figures.Clear();
    }

    void AssertHeadClearance(Figure figure)
    {
        Canvas.ForceUpdateCanvases();
        var rect = figure.Vitals.rectTransform;
        Vector2 bottom = RectTransformUtility.WorldToScreenPoint(null,
            rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMin + figure.Vitals.BottomEdge)));
        Vector2 head = ui.GameCamera.WorldToScreenPoint(figure.View.VitalsHeadWorldPosition);
        float canvasScale = ui.CanvasRoot.GetComponentInParent<Canvas>().scaleFactor;
        Assert.That((bottom.y - head.y) / canvasScale, Is.InRange(2.5f, 3.5f), figure.Actor.UnitId + " must clear the head with its lowest gauge edge.");
        Assert.That(Mathf.Abs(bottom.x - head.x) / canvasScale, Is.LessThan(.2f));
        Assert.AreEqual(new Vector2(52, 28), rect.sizeDelta);
        Assert.IsFalse(figure.Vitals.raycastTarget);
    }

    void AssertDrawnInFront(Figure near, Figure far)
    {
        Assert.Less(ui.GameCamera.WorldToScreenPoint(near.Actor.transform.position).y, ui.GameCamera.WorldToScreenPoint(far.Actor.transform.position).y);
        Assert.Greater(near.Vitals.transform.GetSiblingIndex(), far.Vitals.transform.GetSiblingIndex(), "The closer actor must win HUD overlap, even after the units cross.");
    }

    IEnumerator Capture(string name)
    {
        Directory.CreateDirectory(captureDirectory);
        string path = Path.Combine(captureDirectory, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.unscaledTime + 15;
        while ((!File.Exists(path) || new FileInfo(path).Length <= 24) && Time.unscaledTime < deadline) yield return null;
        Assert.IsTrue(File.Exists(path), "Native screenshot missing: " + path);
        byte[] bytes = File.ReadAllBytes(path); Assert.Greater(bytes.Length, 24);
        Assert.AreEqual(137, bytes[0]); Assert.AreEqual(80, bytes[1]); Assert.AreEqual(78, bytes[2]); Assert.AreEqual(71, bytes[3]);
        Assert.AreEqual(540, (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]);
        Assert.AreEqual(960, (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
        TestContext.WriteLine("Native vitals head anchor capture: " + path);
    }
    Vector3 CellPoint(int x, int y) => Match.Mapper.GetWorldPosition(Match.Board.GetCell(x, y));
}

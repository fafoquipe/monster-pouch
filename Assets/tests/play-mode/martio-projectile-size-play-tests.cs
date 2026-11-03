using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MartioProjectileSizePlayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(n => typeof(LocalGameUI).GetProperty(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    readonly string[] keys = { "monster-pouch.language", "monster-pouch.progress.v1", "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps" };
    string[] oldPaths, saved; bool[] existed; bool spanish;
    LocalGameUI ui; InteractionCaptureGameView view;
    [UnitySetUp] public IEnumerator Setup()
    {
        string root = Path.GetFullPath("Logs/local-validation/martio-projectile-size/isolated-profile-" + Guid.NewGuid().ToString("N"));
        oldPaths = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, Path.Combine(root, "store-" + i));
        saved = keys.Select(PlayerPrefs.GetString).ToArray(); existed = keys.Select(PlayerPrefs.HasKey).ToArray(); spanish = GameText.Spanish;
        foreach (string key in keys) PlayerPrefs.DeleteKey(key); GameText.SetLanguage(true);
        new LocalGuestProfileStore(Path.Combine(root, "store-0")).OpenOrCreate(true);
        view = new InteractionCaptureGameView(); yield return view.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.NotNull(ui); ui.Match.enabled = false;
        float until = Time.realtimeSinceStartup + 15;
        while (LoadingArtworkScreen.IsBlocking && Time.realtimeSinceStartup < until) yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking); ui.Match.Menu(); ui.CloseModal(); ui.RenderPage(); yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (ui != null) { ui.Match.Pause(false); ui.Match.Menu(); ui.gameObject.SetActive(false); Object.Destroy(ui.gameObject); }
        yield return null;
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, oldPaths[i]);
        GameText.SetLanguage(spanish);
        for (int i = 0; i < keys.Length; i++) { if (existed[i]) PlayerPrefs.SetString(keys[i], saved[i]); else PlayerPrefs.DeleteKey(keys[i]); }
        PlayerPrefs.Save(); view.RestoreSelection(); yield return view.WaitForRestoration();
    }
    object Actor(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(ui.Match.Simulation)).Cast<object>()
        .Single(a => a.GetType().GetField("Unit").GetValue(a) == (object)unit);


    [UnityTest] public IEnumerator HorizontalFlightSizeAndComparableCaptures() { yield return VerifyFlight(false); }
    [UnityTest] public IEnumerator DiagonalFlightSizeAndComparableCaptures() { yield return VerifyFlight(true); }

    IEnumerator VerifyFlight(bool diagonal)
    {
        var match = ui.Match;
        Assert.IsTrue(match.SetLocalAbilityAccess(AbilityAccessSnapshot.Unrestricted));
        Assert.IsTrue(match.StartMatch("bugaloo", new[] { "martio" }, 20261026));
        int slot = Enumerable.Range(0, match.Player.Offers.Count).First(i => match.Player.Offers[i]?.UnitId == "martio");
        Assert.IsTrue(match.BuyAndPlace(slot, match.Player.Offers[slot].Token, match.Board.GetCell(1, 8)));
        ui.RenderPage(); yield return null; yield return null;
        match.Ready(); ui.RenderPage(); yield return null; yield return null;
        Assert.AreEqual(MatchPhase.Combat, match.Phase);
        var martio = match.Actors.Values.Single(a => a.BaseStats.DefinitionId == "martio" && a.Side == match.Player.Side);
        var enemy = match.Actors.Values.First(a => a.Side != martio.Side);
        var units = match.Actors.Values.ToArray();
        foreach (var unit in units) match.Board.ReleaseUnit(unit);
        int parking = 0;
        foreach (var unit in units) {
            var cell = unit == martio ? match.Board.GetCell(1, 6) : unit == enemy ? match.Board.GetCell(diagonal ? 4 : 5, diagonal ? 3 : 6)
                : match.Board.GetCell(parking++ % 6, 0);
            Assert.IsTrue(match.Board.TryOccupyCell(unit, cell)); unit.transform.position = match.Mapper.GetWorldPosition(cell);
        }
        foreach (var actor in ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(match.Simulation))) {
            actor.GetType().GetField("NextMove").SetValue(actor, 99999); actor.GetType().GetField("NextAttack").SetValue(actor, 99999);
        }
        match.Advance(match.Config.OpeningAbilityWindowSeconds);
        typeof(CombatSimulation).GetMethod("QueueBasicAttack", Private).Invoke(match.Simulation, new object[] { Actor(martio), enemy });
        match.Advance(CombatSimulation.GetAttackWindup(martio));
        Assert.AreEqual(1, match.Simulation.MartioVolleys.Count);
        var volley = match.Simulation.MartioVolleys.Single();
        while (volley.Flight.Progress < 1.9) match.Advance(CombatSimulation.TickDuration);
        yield return null; yield return null;
        var effects = Object.FindFirstObjectByType<DocumentedBattleEffects>(); Assert.NotNull(effects);
        // The controller is disabled by setup. Stop interpolation at the exact
        // authoritative tick for reproducible rendering of both presentations.
        match.Pause(true);
        typeof(DocumentedBattleEffects).GetField("martioBetweenTicks", Private).SetValue(effects, CombatSimulation.TickDuration);
        typeof(DocumentedBattleEffects).GetMethod("DrawMartioFlights", Private).Invoke(effects, null);
        var images = ui.CanvasRoot.GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Martio projectile ") && i.enabled)
            .OrderBy(i => i.name).ToArray(); Assert.AreEqual(3, images.Length);
        var pointMethod = typeof(DocumentedBattleEffects).GetMethod("Point", Private);
        Vector2 Point(Vector3 world) => (Vector2)pointMethod.Invoke(effects, new object[] { world });
        Vector2 cellOrigin = Point(match.Mapper.GetWorldPosition(match.Board.GetCell(0, 0)));
        float cellPixels = Vector2.Distance(cellOrigin, Point(match.Mapper.GetWorldPosition(match.Board.GetCell(1, 0))));
        float flip = match.Mapper.Flipped ? -1 : 1;
        var measured = new System.Text.StringBuilder();
        measured.AppendLine("Native Unity GameView 540x960; before=legacy rect sizes applied to the exact same rendered frame; after=current renderer.");
        measured.AppendLine("cellPixels=" + cellPixels + "; progress=" + volley.Flight.Progress + "; speed=" + volley.Speed + "; range=" + volley.Flight.Range);
        for (int i = 0; i < 3; i++) {
            var p = volley.Flight.PositionAt((BraidedProjectile)i, volley.Flight.Progress);
            var world = match.Mapper.GetWorldPosition(match.Board.GetCell(0, 0)) +
                new Vector3((float)p.X * match.Mapper.CellSize.x * flip, (float)p.Y * match.Mapper.CellSize.y * flip) + Vector3.up * .4f;
            var expected = Point(world); expected = new Vector2(Mathf.Round(expected.x), Mathf.Round(expected.y));
            Assert.That(Vector2.Distance(images[i].rectTransform.anchoredPosition, expected), Is.LessThan(.01f), "Keep the exact simulation path and presentation anchor.");
            int frame = int.Parse(images[i].sprite.name.Substring(images[i].sprite.name.Length - 2));
            int bodyPixels = i == 0 ? new[] {92,95,96,95}[frame] : new[] {79,82,81,82}[frame];
            float bodyCells = images[i].rectTransform.sizeDelta.x / cellPixels * bodyPixels / Mathf.Max(images[i].sprite.rect.width, images[i].sprite.rect.height);
            var character = Resources.Load<MonsterPouch.Gameplay.Characters.CharacterDefinition>("MonsterPouch/Characters/martio/Character");
            Assert.NotNull(character);
            var visualSlot = character.Attack.ProjectileVisuals.Single(v => v.VariantId == (i == 0 ? "mars" : "moons"));
            Assert.IsTrue(visualSlot.OverrideSize, "The installed editable visual tiers are enabled.");
            Assert.AreEqual(2f, ProjectileVisualGeometry.MaximumTiles, "The user-approved maximum is two cells.");
            // Independent fixed clip envelopes approved with the actual four-frame art.
            // Body-only legacy constants predate the enabled editable visual tiers.
            float envelopePixels = i == 0 ? 154 : 151;
            float expectedBodyCells = 2f * Mathf.Clamp(visualSlot.SizePercent, 0, 100) / 100f * bodyPixels / envelopePixels;
            Assert.That(bodyCells, Is.EqualTo(expectedBodyCells).Within(.001f));
            Assert.IsFalse(images[i].raycastTarget);
            measured.AppendLine(images[i].name + "; sprite=" + images[i].sprite.name + "; bodyCells=" + bodyCells + "; rectPixels=" + images[i].rectTransform.sizeDelta.x + "; anchor=" + images[i].rectTransform.anchoredPosition);
        }
        double progress = volley.Flight.Progress;
        int[] health = units.Select(u => u.CurrentHealth).ToArray();
        var positions = images.Select(i => i.rectTransform.anchoredPosition).ToArray();
        var sizes = images.Select(i => i.rectTransform.sizeDelta).ToArray();
        string heading = diagonal ? "diagonal" : "horizontal";
        Capture(heading + "-after");
        try {
            for (int i = 0; i < images.Length; i++) images[i].rectTransform.sizeDelta = Vector2.one * cellPixels * (i == 0 ? .7f : .48f);
            Capture(heading + "-before");
        } finally { for (int i = 0; i < images.Length; i++) images[i].rectTransform.sizeDelta = sizes[i]; }
        Assert.AreEqual(progress, volley.Flight.Progress);
        CollectionAssert.AreEqual(health, units.Select(u => u.CurrentHealth).ToArray());
        CollectionAssert.AreEqual(positions, images.Select(i => i.rectTransform.anchoredPosition).ToArray());
        measured.AppendLine("PASS: identical simulation progress, HP, anchors, raycast behavior; only rendered sizes differ.");
        File.WriteAllText(Path.GetFullPath("Logs/local-validation/martio-projectile-size/" + heading + "-metrics.txt"), measured.ToString());
        match.Pause(false); match.Menu(); yield return null; yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>(true).Any(i => i.name.StartsWith("Martio projectile ")));
    }
    void Capture(string name)
    {
        var camera = ui.GameCamera; var canvas = ui.CanvasRoot.GetComponentInParent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; var order = canvas.sortingOrder; var distance = canvas.planeDistance;
        var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(Screen.width, Screen.height, 24); var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        try {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = short.MaxValue;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); image.Apply();
            string folder = Path.GetFullPath("Logs/local-validation/martio-projectile-size"); Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        }
        finally {
            camera.targetTexture = oldTarget; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
            canvas.planeDistance = distance; canvas.sortingOrder = order; Object.Destroy(image); Object.Destroy(target);
        }
    }
}

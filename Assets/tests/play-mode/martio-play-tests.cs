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

public sealed class MartioPlayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(n => typeof(LocalGameUI).GetProperty(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    readonly string[] keys = { "monster-pouch.language", "monster-pouch.progress.v1", "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps" };
    string[] oldPaths, saved; bool[] existed; bool spanish;
    LocalGameUI ui; InteractionCaptureGameView view;
    [UnitySetUp] public IEnumerator Setup()
    {
        string root = Path.GetFullPath("Logs/local-validation/martio/isolated-profile-" + Guid.NewGuid().ToString("N"));
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

    [UnityTest] public IEnumerator SelectPurchaseRenderThreeProjectilesShockwavePauseAndCleanup()
    {
        var match = ui.Match; var definition = match.Config.Get("martio"); Assert.NotNull(definition); Assert.IsFalse(definition.IsMonster);
        ui.OpenCharacterPicker(0); yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Button>(true).Any(b => b.name == "loadout-martio")); ui.CloseModal();
        typeof(LocalGameUI).GetMethod("ShowCollectionInfo", Private).Invoke(ui, new object[] { definition }); yield return null;
        foreach (var ability in definition.Tricks) {
            var icon = AbilityArtwork.Get(ability.EffectId); Assert.NotNull(icon); Assert.AreEqual(icon.rect.width, icon.rect.height);
            Assert.That(GameText.AbilityDescription(ability, definition), Does.Not.Contain("{"));
        }
        Capture("collection"); ui.CloseModal();
        Assert.IsTrue(match.SetLocalAbilityAccess(AbilityAccessSnapshot.Unrestricted));
        Assert.IsTrue(match.StartMatch("bugaloo", new[] { "martio" }, 20261025));
        int slot = Enumerable.Range(0, match.Player.Offers.Count).First(i => match.Player.Offers[i]?.UnitId == "martio");
        var cell = match.Board.GetCell(2, 8);
        Assert.IsNull(cell.OccupiedBy);
        int coins = match.Player.Coins;
        Assert.IsTrue(match.BuyAndPlace(slot, match.Player.Offers[slot].Token, cell));
        Assert.AreEqual(coins - 3, match.Player.Coins);
        var owned = match.Player.Owned["martio"]; owned.Tricks[0] = owned.Tricks[1] = true;
        ui.RenderPage(); yield return null; yield return null;
        var martio = match.Actors.Values.Single(a => a.BaseStats.DefinitionId == "martio" && a.Side == match.Player.Side);
        var orbit = martio.GetComponent<MartioCelestialOrbit>(); Assert.NotNull(orbit);
        var props = martio.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "martio-mars" || s.name.StartsWith("martio-moon-")).ToArray();
        Assert.AreEqual(3, props.Length); Assert.IsTrue(props.All(p => p.enabled && p.sprite != null));
        Capture("preparation");
        match.Ready(); ui.RenderPage(); yield return null; yield return null;
        Assert.AreEqual(MatchPhase.Combat, match.Phase);
        var enemy = match.Actors.Values.First(a => a.Side != martio.Side);
        var destination = match.Board.GetCell(2, 6);
        if (destination.OccupiedBy != null && destination.OccupiedBy != enemy) {
            var occupant = (BattleUnit)destination.OccupiedBy; var free = match.Board.GetAllCells().First(c => c.OccupiedBy == null && c.X == 5);
            match.Board.ReleaseUnit(occupant); Assert.IsTrue(match.Board.TryOccupyCell(occupant, free)); occupant.transform.position = match.Mapper.GetWorldPosition(free);
        }
        match.Board.ReleaseUnit(enemy); Assert.IsTrue(match.Board.TryOccupyCell(enemy, destination)); enemy.transform.position = match.Mapper.GetWorldPosition(destination);
        foreach (var actor in ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(match.Simulation))) {
            actor.GetType().GetField("NextMove").SetValue(actor, 99999); actor.GetType().GetField("NextAttack").SetValue(actor, 99999);
        }
        match.Advance(match.Config.OpeningAbilityWindowSeconds);
        typeof(CombatSimulation).GetMethod("QueueBasicAttack", Private).Invoke(match.Simulation, new object[] { Actor(martio), enemy });
        match.Advance(CombatSimulation.GetAttackWindup(martio)); yield return null; yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Image>().Count(i => i.name.StartsWith("Martio projectile ") && i.enabled) >= 3);
        Capture("projectiles");
        match.Advance(.4f); yield return null; yield return null;
        var wave = ui.CanvasRoot.GetComponentsInChildren<Image>().FirstOrDefault(i => i.name == "Martio shockwave" && i.enabled);
        Assert.NotNull(wave, "Upgrade 1 must draw the expanding ring from a real projectile impact.");
        Capture("shockwave");
        float clock = match.Simulation.Elapsed; match.Pause(true); match.Advance(1); yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(clock, match.Simulation.Elapsed); match.Pause(false); match.Menu(); yield return null; yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>(true).Any(i => i.name == "Martio shockwave"));
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
            string folder = Path.GetFullPath("Logs/local-validation/martio"); Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        }
        finally {
            camera.targetTexture = oldTarget; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
            canvas.planeDistance = distance; canvas.sortingOrder = order; Object.Destroy(image); Object.Destroy(target);
        }
    }
}

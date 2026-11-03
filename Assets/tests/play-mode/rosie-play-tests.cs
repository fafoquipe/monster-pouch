using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class RosiePlayTests
{
    LocalGameUI ui;
    bool originalLobbyPlayback;
    readonly Dictionary<string, string> preferences = new Dictionary<string, string>();
    readonly HashSet<string> existed = new HashSet<string>();
    static readonly string[] Keys = { "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps" };
    [UnitySetUp] public IEnumerator SetUp()
    {
        originalLobbyPlayback = LobbyAnimationDirector.PlaybackEnabled;
        LobbyAnimationDirector.PlaybackEnabled = false;
        foreach (string key in Keys) { if (PlayerPrefs.HasKey(key)) existed.Add(key); preferences[key] = PlayerPrefs.GetString(key); }
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); ui.Match.enabled = false;
    }
    [UnityTearDown] public IEnumerator TearDown()
    {
        LobbyAnimationDirector.PlaybackEnabled = originalLobbyPlayback;
        if (ui != null) { ui.Match.Pause(false); ui.CloseModal(); ui.Match.Menu(); ui.Match.enabled = true; }
        foreach (string key in Keys) { if (existed.Contains(key)) PlayerPrefs.SetString(key, preferences[key]); else PlayerPrefs.DeleteKey(key); }
        yield return null;
    }
    void Capture(string name)
    {
        string folder = Path.GetFullPath("Logs/local-validation/rosie"); Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
    }
    [UnityTest] public IEnumerator CollectionHasCurrentStaticPortraitIconsAndLiveDescription()
    {
        var rosie = ui.Match.Config.Get("rosie"); Assert.NotNull(rosie);
        ui.OpenCharacterPicker(0); yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Button>(true).Any(b => b.name == "loadout-rosie"));
        typeof(LocalGameUI).GetMethod("ShowCollectionInfo", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, new object[] { rosie });
        yield return new WaitForSeconds(.6f);
        var details = ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Character details");
        var figure = details.GetComponentInChildren<LobbyFrameGraphic>();
        Assert.NotNull(figure.CurrentSprite); Assert.IsFalse(figure.HasAuthoredFrames);
        foreach (var effect in new[] { rosie.BaseAbility }.Concat(rosie.Tricks))
        {
            var icon = AbilityArtwork.Get(effect.EffectId); Assert.NotNull(icon);
            Assert.AreEqual(64, icon.rect.width, .1f); Assert.AreEqual(64, icon.rect.height, .1f); Assert.AreEqual(FilterMode.Point, icon.texture.filterMode);
        }
        ui.CanvasRoot.GetComponentsInChildren<Button>().First(b => b.name == "ability-2").onClick.Invoke(); yield return null;
        string expected=GameText.AbilityDescription(rosie.Tricks[2],rosie);
        Assert.IsNotEmpty(expected);StringAssert.DoesNotContain("{",expected);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text==expected),
            "The card must show the current configured description, without restoring an older duration or bonus.");
        Capture("rosie-collection"); yield return null; yield return null;
    }
    [UnityTest] public IEnumerator RoseHasArtworkAndSway_FreezesOnPause_AndCleansOnExit()
    {
        Assert.IsTrue(ui.Match.StartMatch("anuik", new[] { "rosie" })); ui.Match.Player.AddCombatCoins(100);
        var offer = ui.Match.Player.Offers[0]; Assert.AreEqual("rosie", offer.UnitId);
        Assert.IsTrue(ui.Match.BuyAndPlace(0, offer.Token, ui.Match.Board.GetCell(2, 8)));
        var owned = ui.Match.Player.Owned["rosie"]; for (int i = 0; i < 3; i++) owned.Tricks[i] = true;
        ui.Match.Ready(); yield return null;
        var rosie = ui.Match.Actors[owned]; var ally = ui.Match.Actors[ui.Match.Player.Monster];
        Assert.IsTrue(ui.Match.Board.TryRepositionUnit(ally, ui.Match.Board.GetCell(2, 5)));
        CombatSimulation.HealingRoseFlight flight = null; ui.Match.HealingRoseLaunched += f => flight = f;
        rosie.AddEnergy(rosie.BaseStats.EnergyMax);
        for (int i = 0; i < 100 && flight == null; i++) { ui.Match.Simulation.Step(.1f); yield return null; }
        Assert.NotNull(flight);
        var image = ui.CanvasRoot.GetComponentsInChildren<Image>().First(i => i.name == "Rosie healing rose");
        var aura = ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i => i.name == "Rosie healing aura");
        Assert.NotNull(image.sprite); Assert.AreEqual(FilterMode.Point, image.sprite.texture.filterMode);
        Assert.AreEqual("rosie-open-rose-v1", image.sprite.name);
        Assert.NotNull(aura.sprite); Assert.AreEqual(FilterMode.Point, aura.sprite.texture.filterMode);
        Assert.IsFalse(aura.raycastTarget);
        var worldOrigin = ui.Match.Mapper.GetWorldPosition(flight.Origin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.CanvasRoot, ui.GameCamera.WorldToScreenPoint(worldOrigin), null, out var cellStart);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.CanvasRoot, ui.GameCamera.WorldToScreenPoint(worldOrigin + Vector3.right * ui.Match.Mapper.CellSize.x), null, out var cellEnd);
        float cellPixels = Vector2.Distance(cellStart, cellEnd);
        Assert.AreEqual(cellPixels * flight.Width, aura.rectTransform.sizeDelta.x, 1);
        var direction = flight.Destination; var rotation = image.rectTransform.localRotation;
        ui.Match.Simulation.Step(.2f); yield return null; yield return null;
        Assert.AreNotEqual(rotation, image.rectTransform.localRotation); Assert.AreSame(direction, flight.Destination);
        Capture("rosie-healing-rose"); yield return null; yield return null;
        ui.Match.Pause(true); yield return null;
        var position = image.rectTransform.anchoredPosition; rotation = image.rectTransform.localRotation;
        var auraPosition = aura.rectTransform.anchoredPosition; var auraColor = aura.color;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(position, image.rectTransform.anchoredPosition); Assert.AreEqual(rotation, image.rectTransform.localRotation);
        Assert.AreEqual(auraPosition, aura.rectTransform.anchoredPosition); Assert.AreEqual(auraColor, aura.color);
        ui.Match.Pause(false); ui.Match.Menu(); yield return null; yield return null;
        Assert.IsFalse(flight.Active); Assert.IsTrue(image == null); Assert.IsTrue(aura == null);
    }
}

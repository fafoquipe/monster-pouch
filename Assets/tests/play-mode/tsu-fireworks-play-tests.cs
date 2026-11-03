using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TsuFireworksPlayTests
{
    LocalGameUI ui;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (ui != null) { ui.Match.Pause(false); ui.Match.Menu(); ui.Match.enabled = true; }
        yield return null;
    }
    IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); ui.Match.enabled = false;
        Assert.IsTrue(ui.Match.StartMatch("anuik", new[] { "tsu" }, 736)); ui.Match.Player.AddCombatCoins(30);
        int slot = System.Array.FindIndex(ui.Match.Player.Offers.ToArray(), o => o != null && o.UnitId == "tsu");
        Assert.GreaterOrEqual(slot, 0);
        Assert.IsTrue(ui.Match.BuyAndPlace(slot, ui.Match.Player.Offers[slot].Token, ui.Match.Board.GetCell(2, 6)));
        ui.Match.Ready(); yield return null;
    }
    [UnityTest] public IEnumerator FireworksPauseAndCleanUpAfterAnimation()
    {
        yield return Setup();
        var fx = ui.GetComponent<DocumentedBattleEffects>();
        var target = ui.Match.Actors[ui.Match.Bot.Monster];
        var routine = (IEnumerator)typeof(DocumentedBattleEffects).GetMethod("ChocolateMiniExplosions", Private)
            .Invoke(fx, new object[] { target.transform.position + Vector3.up * .5f });
        fx.StartCoroutine(routine); yield return new WaitForSeconds(.46f);
        ui.Match.Pause(true);
        var root = ui.CanvasRoot.GetComponentsInChildren<RectTransform>().First(t => t.name == "tsu-gum-fireworks");
        var sparks = root.GetComponentsInChildren<Image>().Where(i => i.name == "firework-spark").ToArray();
        Assert.AreEqual(24, sparks.Length); Assert.IsTrue(sparks.Any(i => i.enabled));
        Assert.IsTrue(sparks.Where(i=>i.enabled).All(i=>i.rectTransform.sizeDelta.x>=4),"Firework heads must remain legible at mobile scale.");
        Assert.IsTrue(root.GetComponentsInChildren<Image>().All(i => !i.raycastTarget));
        var before = sparks.Select(i => i.rectTransform.anchoredPosition).ToArray();
        yield return new WaitForSecondsRealtime(.15f);
        CollectionAssert.AreEqual(before, sparks.Select(i => i.rectTransform.anchoredPosition).ToArray());
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/screenshots"));
        Directory.CreateDirectory(folder);
        yield return Application.isBatchMode ? null : new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, "tsu-fireworks.png")); yield return null;
        ui.Match.Pause(false); yield return new WaitForSeconds(.9f);
        Assert.IsTrue(root == null);
    }
    [UnityTest] public IEnumerator AbilitiesKeepVisualEffectsWithoutWordCaptionsAndPreserveTokoroSleep()
    {
        yield return Setup();
        var fx=ui.GetComponent<DocumentedBattleEffects>();
        var actor=ui.Match.Actors[ui.Match.Player.Owned["tsu"]];
        var method=typeof(DocumentedBattleEffects).GetMethod("AbilityUsed",Private);
        var layer=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Documented battle effects");
        foreach(var kind in new[]{"tsu-root","bugaloo-super","sepora-stealth","trimol-boulder","stein-lightning","hymay-super","flo-flower","popow-hook","generic-super"})
            method.Invoke(fx,new object[]{actor,kind});
        var pulses=layer.GetComponentsInChildren<RectTransform>().Where(t=>t.name=="ability-feedback").ToArray();
        Assert.AreEqual(9,pulses.Length);
        Assert.IsTrue(pulses.All(p=>p.GetComponentsInChildren<Image>().Length==4),"Existing visual effects must stay intact.");
        Assert.IsTrue(pulses.All(p=>p.GetComponentsInChildren<Text>().Length==0),"Abilities must not add word captions.");
        method.Invoke(fx,new object[]{actor,"tokoro-sleep"});
        Assert.AreEqual("ZZZ",layer.GetComponentsInChildren<Text>().Single(t=>t.text.Length>0).text,"Tokoro's sleep cue was explicitly left for later.");
    }
    [UnityTest] public IEnumerator ColoredGumHasDistinctColorsAndPausesInFlight()
    {
        yield return Setup();
        var fx = ui.GetComponent<DocumentedBattleEffects>();
        var source = ui.Match.Actors[ui.Match.Player.Owned["tsu"]];
        var target = ui.Match.Actors[ui.Match.Player.Monster];
        var method = typeof(DocumentedBattleEffects).GetMethod("ColoredGumLaunched", Private);
        foreach (var buff in new[] { GumBuff.Speed, GumBuff.Shield, GumBuff.Damage })
            method.Invoke(fx, new object[] { source, target, buff, 1f });
        yield return new WaitForSeconds(.2f); ui.Match.Pause(true);
        var roots = ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(t => t.name.StartsWith("tsu-colored-gum-")).ToArray();
        Assert.AreEqual(3, roots.Length);
        var colors = roots.Select(t => t.GetComponentsInChildren<Image>().First(i => i.name == "gum-ball").sprite.texture.GetPixel(10, 10)).ToArray();
        Assert.AreEqual(3, colors.Distinct().Count());
        var before = roots.Select(t => t.anchoredPosition).ToArray();
        yield return new WaitForSecondsRealtime(.15f);
        CollectionAssert.AreEqual(before, roots.Select(t => t.anchoredPosition).ToArray());
        ui.Match.Pause(false); yield return new WaitForSeconds(1.3f);
        Assert.IsTrue(roots.All(t => t == null));
    }
    [UnityTest] public IEnumerator GumColorsTheActualRootedCellAndClearsOnMenu()
    {
        yield return Setup();
        var fx = ui.GetComponent<DocumentedBattleEffects>();
        BattleUnit rooted = null;
        for (int i = 0; i < 200 && rooted == null && !ui.Match.Simulation.Finished; i++)
        {
            ui.Match.Simulation.Step(.1f);
            rooted = ui.Match.Simulation.GumRootedUnits.FirstOrDefault();
        }
        Assert.IsNotNull(rooted, "Tsu must deliver a sticky attack during the actual combat.");
        typeof(DocumentedBattleEffects).GetMethod("RefreshGumTiles", Private).Invoke(fx, null);
        var floor = fx.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "tsu-sticky-pink-cell");
        Assert.AreEqual(ui.Match.Mapper.GetWorldPosition(rooted.CurrentCell), floor.transform.position);
        Color pink = floor.sprite.texture.GetPixel(10, 10);
        Assert.Greater(pink.r, pink.g); Assert.Greater(pink.b, pink.g);
        Assert.AreEqual(FilterMode.Point, floor.sprite.texture.filterMode);
        Assert.AreEqual(21, floor.sortingOrder, "The pink goo belongs on the board, below the units.");
        ui.Match.Pause(true);
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/screenshots"));
        Directory.CreateDirectory(folder);
        yield return Application.isBatchMode ? null : new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, "tsu-casilla-rosa.png")); yield return null;
        ui.Match.Pause(false); ui.Match.Menu();
        typeof(DocumentedBattleEffects).GetMethod("RefreshGumTiles", Private).Invoke(fx, null);
        yield return null; Assert.IsTrue(floor == null);
    }
}

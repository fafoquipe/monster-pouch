using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CombatStatusEffectsPlayTests
{
    LocalGameUI ui;CombatStatusEffects fx;
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"atong","flo"}));ui.Match.Ready();
        fx=ui.GetComponent<CombatStatusEffects>();Assert.IsNotNull(fx);
    }
    [UnityTearDown] public IEnumerator Cleanup(){ui.Match.Pause(false);ui.Match.Menu();yield return null;}
    void Invoke(string name,params object[] args)=>typeof(CombatStatusEffects).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(fx,args);
    [UnityTest] public IEnumerator HealAuraLightsGroundPausesAndReusesItsObjects()
    {
        var unit=ui.Match.Actors[ui.Match.Player.Monster];
        Invoke("Healed",unit,10);yield return null;
        var glow=Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Single(r=>r.name=="floor-heal"&&r.gameObject.activeSelf);
        Assert.IsTrue(glow.sharedMaterial.shader.isSupported);Assert.AreEqual("MonsterPouch/PixelGroundGlow",glow.sharedMaterial.shader.name);
        Assert.Greater(glow.color.g,glow.color.r);Assert.Greater(glow.color.a,0);Assert.AreEqual(21,glow.sortingOrder);
        Assert.Less(glow.sortingOrder,unit.GetComponent<MonsterPouch.Gameplay.Presentation.UnitPresentation>().Renderer.sortingOrder);
        Assert.AreEqual(FilterMode.Point,glow.sprite.texture.filterMode);
        int count=fx.CreatedEffectCount;Vector3 scale=glow.transform.localScale;
        ui.Match.Pause(true);yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(scale,glow.transform.localScale);ui.Match.Pause(false);
        yield return new WaitForSeconds(1.1f);Assert.AreEqual(0,fx.ActiveEffectCount);
        Invoke("Healed",unit,10);Assert.AreEqual(count,fx.CreatedEffectCount);
        ui.Match.Menu();yield return null;Assert.AreEqual(0,fx.ActiveEffectCount);Assert.IsFalse(glow.gameObject.activeSelf);
    }
    [UnityTest] public IEnumerator StatusMarkersFollowStateAndCriticalHasDedicatedBurst()
    {
        var unit=ui.Match.Actors[ui.Match.Player.Monster];
        typeof(MonsterPouch.Gameplay.Units.BattleUnit).GetProperty("IsSlowed").SetValue(unit,true);
        yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="fx-slow"));
        Invoke("Critical",unit,unit);Invoke("ResetAttack",unit);yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="fx-critical"));
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="fx-reset"));
        unit.ResetForCombat();yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="fx-slow"));
        fx.enabled=false;Assert.AreEqual(0,fx.ActiveEffectCount);
        Invoke("Healed",unit,10);Assert.AreEqual(0,fx.ActiveEffectCount);
    }
}

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
    [UnityTest] public IEnumerator CharacterControlEffectsStayVisibleTogetherAndResetCleanly()
    {
        var unit=ui.Match.Actors[ui.Match.Player.Monster];
        var view=unit.GetComponent<MonsterPouch.Gameplay.Presentation.UnitPresentation>();
        yield return null;yield return null;
        var restingColor=view.Renderer.color;
        float restingHeight=view.VisualRoot.localPosition.y;
        var cell=unit.CurrentCell;var position=unit.transform.position;
        var type=typeof(MonsterPouch.Gameplay.Units.BattleUnit);
        type.GetProperty("IsStunned").SetValue(unit,true);
        type.GetProperty("IsPopowStunned").SetValue(unit,true);
        type.GetProperty("IsElectrocuted").SetValue(unit,true);
        type.GetProperty("IsSlowed").SetValue(unit,true);
        type.GetProperty("SlowEffectId").SetValue(unit,"jazar-radiation");
        yield return null;yield return null;
        Assert.Greater(view.VisualRoot.localPosition.y,.1f);
        Assert.Less(view.Renderer.color.r,.35f);
        Assert.AreSame(cell,unit.CurrentCell);Assert.AreEqual(position,unit.transform.position);
        var roots=ui.CanvasRoot.GetComponentsInChildren<RectTransform>();
        foreach(var name in new[]{"fx-stun","fx-popow-stun","fx-electric-stun"})
            Assert.IsFalse(roots.Any(r=>r.name==name),"Stun uses its HUD circle: "+name);
        Assert.IsTrue(roots.Any(r=>r.name=="status-electric-stun"));
        Assert.IsTrue(roots.Any(r=>r.name=="fx-toxic"));
        var toxic=roots.Single(r=>r.name=="fx-toxic").GetComponentsInChildren<UnityEngine.UI.Image>().First(i=>i.enabled);
        Assert.AreEqual("status-toxic",toxic.sprite.name);
        unit.ResetForCombat();yield return null;yield return null;
        Assert.IsFalse(unit.IsPopowStunned||unit.IsElectrocuted);Assert.IsNull(unit.SlowEffectId);
        Assert.AreEqual(restingColor,view.Renderer.color,"Reset must restore the current artwork's resting tint.");
        // Anuik's current idle floats by .10 +/- .012; reset removes the stun lift,
        // while retaining the authored idle instead of forcing the figure onto the ground.
        Assert.That(view.VisualRoot.localPosition.y,Is.EqualTo(restingHeight).Within(.025f));
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="fx-toxic"));
    }
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

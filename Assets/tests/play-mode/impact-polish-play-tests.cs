using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class ImpactPolishPlayTests
{
    private LocalGameUI ui;
    private DocumentedBattleEffects effects;
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); ui.Match.enabled = false;
        effects = ui.GetComponent<DocumentedBattleEffects>(); Assert.IsNotNull(effects);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (ui != null) { ui.Match.Pause(false); ui.Match.Menu(); }
        yield return null;
    }
    private RectTransform Find(string name) => ui.CanvasRoot.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
    private void Invoke(string method, params object[] args) => typeof(DocumentedBattleEffects).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects, args);
    private BattleUnit Deploy(string id)
    {
        ui.Match.Player.AddCombatCoins(30);
        int slot = System.Array.FindIndex(ui.Match.Player.Offers.ToArray(), o => o != null && o.UnitId == id);
        Assert.GreaterOrEqual(slot, 0);
        Assert.IsTrue(ui.Match.BuyAndPlace(slot, ui.Match.Player.Offers[slot].Token, ui.Match.Board.GetCell(0, 9)));
        return ui.Match.Actors[ui.Match.Player.Owned[id]];
    }

    [UnityTest] public IEnumerator PopowDashEventDisplaysAreaFreezesOnPauseAndCleansUpOnMenu()
    {
        Assert.IsTrue(ui.Match.StartMatch("popow", new[] { "flo" })); ui.Match.Ready();
        ui.Match.Simulation.Step(ui.Match.Config.OpeningAbilityWindowSeconds);
        var popow = ui.Match.Actors[ui.Match.Player.Monster]; popow.AddEnergy(popow.BaseStats.EnergyMax);
        ui.Match.Simulation.Step(AbilityTiming.Cast(popow.BaseStats,"popow-super")+CombatSimulation.TickDuration); yield return null;
        Assert.IsNotNull(Find("dash-affected-lane"), "The authoritative dash event must display its affected lane.");
        var aura=Find("dash-contact-aura"); Assert.IsNotNull(aura);
        Assert.IsFalse(aura.GetComponent<Image>().raycastTarget);
        var arcs=ui.CanvasRoot.GetComponentsInChildren<Image>().Where(i=>i.name=="hook-trail-core").ToArray();
        Assert.AreEqual(14,arcs.Length);
        Assert.Greater(arcs.Select(i=>Mathf.RoundToInt(i.transform.localEulerAngles.z)).Distinct().Count(),4);
        Assert.IsTrue(arcs.All(i=>!i.raycastTarget));
        var ember = Find("dash-ember"); Assert.IsNotNull(ember);
        Assert.IsFalse(ember.GetComponent<Image>().raycastTarget);
        ui.Match.Pause(true); var frozen = ember.anchoredPosition; var frozenAura=aura.anchoredPosition;
        yield return new WaitForSecondsRealtime(.12f); Assert.AreEqual(frozen, ember.anchoredPosition);
        Assert.AreEqual(frozenAura,aura.anchoredPosition);
        ui.Match.Menu(); yield return null; yield return null;
        Assert.IsNull(Find("popow-flaming-dash")); Assert.IsNull(Find("popow-dash-flames"));
    }

    [UnityTest] public IEnumerator AreaEffectsDrawOnlyTheirAuthoritativeCellsPauseAndCleanUp()
    {
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"gochan"}));ui.Match.Ready();
        var source=ui.Match.Actors[ui.Match.Player.Monster];
        var cells=new[]{ui.Match.Board.GetCell(2,5),ui.Match.Board.GetCell(1,5),ui.Match.Board.GetCell(3,5),
            ui.Match.Board.GetCell(2,4),ui.Match.Board.GetCell(2,6)};
        Invoke("AreaEffect",source,"gochan-cross",cells);yield return null;
        var floor=GameObject.Find("area-floor-gochan-cross");Assert.IsNotNull(floor);
        Assert.AreEqual(5,floor.GetComponentsInChildren<SpriteRenderer>().Length);
        Assert.IsTrue(floor.GetComponentsInChildren<SpriteRenderer>().All(tile=>tile.sharedMaterial==
            Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial")),"Area colors must remain visible without scene lights.");
        Assert.AreEqual(15,Find("area-effects-gochan-cross").GetComponentsInChildren<Image>().Count(i=>i.name=="area-fire"));
        var flame=Find("area-fire");ui.Match.Pause(true);var size=flame.sizeDelta;
        yield return new WaitForSecondsRealtime(.15f);Assert.AreEqual(size,flame.sizeDelta);
        ui.Match.Pause(false);ui.Match.Menu();yield return null;yield return null;
        Assert.AreEqual(0,effects.ActiveAreaCount);Assert.IsNull(GameObject.Find("area-floor-gochan-cross"));
    }

    [UnityTest] public IEnumerator AnuikBounceStartsAtHitEnemyWithoutWindupAndSurvivesCasterReset()
    {
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"flo"})); var next=Deploy("flo"); ui.Match.Ready();
        var anuik=ui.Match.Actors[ui.Match.Player.Monster];
        var origin=ui.Match.Actors[ui.Match.Bot.Monster];
        ui.Match.Pause(true);
        var method=typeof(LocalGameUI).GetMethod("ReboundProjectile",BindingFlags.Instance|BindingFlags.NonPublic);
        ui.StartCoroutine((IEnumerator)method.Invoke(ui,new object[]{anuik,origin,next,.4f}));
        var heart=Find("projectile-anuik-bounce"); Assert.IsNotNull(heart,"The ricochet releases at impact, without another windup.");
        Vector2 expected=ui.ToCanvas(ui.GameCamera.WorldToScreenPoint(origin.transform.position+Vector3.up*.5f));
        expected=new Vector2(Mathf.Round(expected.x),-Mathf.Round(expected.y));
        Assert.AreEqual(expected,heart.anchoredPosition,"The ricochet must start at the struck enemy, not Anuik.");
        anuik.ResetForCombat();
        yield return new WaitForSecondsRealtime(.12f);
        Assert.IsNotNull(heart);Assert.AreEqual(expected,heart.anchoredPosition);
        ui.Match.Pause(false);yield return new WaitForSeconds(.1f);
        Assert.That(Vector2.Distance(expected,heart.anchoredPosition),Is.GreaterThan(1));
        ui.Match.Menu();yield return null;yield return null;
        Assert.IsNull(Find("projectile-anuik-bounce"));
    }

    [UnityTest] public IEnumerator RevivalExplosionAndInstantTauntHaveVisibleDistinctFeedback()
    {
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"gochan"}));ui.Match.Ready();
        var source=ui.Match.Actors[ui.Match.Player.Monster];var cells=new[]{source.CurrentCell};
        Invoke("AreaEffect",source,"anuik-revive-impact",cells);yield return null;
        Assert.AreEqual(8,Find("area-effects-anuik-revive-impact").GetComponentsInChildren<Image>().Count(i=>i.name=="revive-explosion-heart"));
        Invoke("AreaEffect",source,"bugaloo-taunt",cells);
        typeof(BattleUnit).GetProperty("IsTaunted").SetValue(source,true);
        yield return null;yield return null;
        Assert.IsNull(Find("fx-taunt"),"Persistent taunt uses the normal status circle, without a separate head marker.");
        Assert.IsNotNull(Find("status-taunt"),"The affected target keeps its existing HUD badge.");
        yield return new WaitForSeconds(1.1f);
        Assert.IsNull(GameObject.Find("area-floor-bugaloo-taunt"),"The red pulse lasts one second, not the taunt's whole duration.");
        Assert.IsNull(Find("fx-taunt"));
        Assert.IsNotNull(Find("status-taunt"));
        typeof(BattleUnit).GetProperty("IsTaunted").SetValue(source,false);yield return null;yield return null;
        Assert.IsNull(Find("fx-taunt"));
        Assert.IsFalse(Find("status-taunt").gameObject.activeInHierarchy);
    }

    [UnityTest] public IEnumerator JazzarSlimeHasViscousTailAndSplashThatPauseAndCleanUp()
    {
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"jazar"}));var jazzar=Deploy("jazar");ui.Match.Ready();
        var target=ui.Match.Actors[ui.Match.Bot.Monster];
        Invoke("Attacked",jazzar,target,true);
        float deadline=Time.time+CombatSimulation.GetAttackWindup(jazzar)+.5f;
        while(Find("jazzar-toxic-flight")==null&&Time.time<deadline)yield return null;
        var glob=Find("jazzar-toxic-glob");Assert.IsNotNull(glob);
        Assert.AreEqual("fx-pixel-toxic-glob",glob.GetComponent<Image>().sprite.name);
        Assert.AreEqual(3,Find("jazzar-toxic-flight").GetComponentsInChildren<Image>().Count(i=>i.name=="jazzar-viscous-tail"));
        ui.Match.Pause(true);var position=glob.anchoredPosition;var size=glob.sizeDelta;
        yield return new WaitForSecondsRealtime(.12f);Assert.AreEqual(position,glob.anchoredPosition);Assert.AreEqual(size,glob.sizeDelta);
        Invoke("Impacted",jazzar,target,3);yield return null;
        var splash=Find("jazzar-toxic-splash");Assert.IsNotNull(splash);
        Assert.AreEqual(7,splash.GetComponentsInChildren<Image>().Count(i=>i.name=="toxic-splash-droplet"));
        Assert.IsTrue(splash.GetComponentsInChildren<Image>().All(i=>!i.raycastTarget));
        ui.Match.Menu();yield return null;yield return null;
        Assert.IsNull(Find("jazzar-toxic-flight"));Assert.IsNull(Find("jazzar-toxic-splash"));
    }

    [UnityTest] public IEnumerator FloShowsFlowerInHandsBeforeSpawningAndLeavesOneFlowerAfterPlanting()
    {
        Assert.IsTrue(ui.Match.StartMatch("anuik", new[] { "flo" })); var flo = Deploy("flo");
        ui.Match.Ready(); flo.AddEnergy(100);
        RectTransform carried = null;
        for (int i = 0; i < 100 && carried == null; i++)
        { ui.Match.Simulation.Step(.1f); yield return null; carried = Find("flower-in-hands"); }
        Assert.IsNotNull(carried);
        Assert.IsNotNull(carried.GetComponent<Image>().sprite);
        Assert.AreEqual("flo-healing-bloom-v1", carried.GetComponent<Image>().sprite.name);
        Assert.AreEqual(0, ui.Match.Actors.Values.Count(a => a.BaseStats.DefinitionId == "flower" && a.Side == flo.Side));
        ui.Match.Pause(true); var position = carried.anchoredPosition;
        yield return new WaitForSecondsRealtime(.12f); Assert.AreEqual(position, carried.anchoredPosition);
        ui.Match.Pause(false);
        for (int i = 0; i < 6; i++) { ui.Match.Simulation.Step(.1f); yield return new WaitForSeconds(.1f); }
        Assert.IsNull(Find("flo-flower-planting"), "The held flower must be removed after the real summon appears.");
        Assert.AreEqual(1, ui.Match.Actors.Values.Count(a => a.BaseStats.DefinitionId == "flower" && a.Side == flo.Side));
        var planted = ui.Match.Actors.Values.Single(a => a.BaseStats.DefinitionId == "flower" && a.Side == flo.Side);
        Assert.AreEqual("flo-healing-bloom-v1", planted.GetComponent<MonsterPouch.Gameplay.Presentation.UnitPresentation>().Renderer.sprite.name);
        string folder = System.IO.Path.GetFullPath("Logs/local-validation/floral-art-20261020");
        System.IO.Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "flo-planted-flower.png"));
        yield return null; yield return null;
    }

    [UnityTest] public IEnumerator HymayWhipIsCurvedAndRetractsOnImpactAndResetCancelsWindup()
    {
        Assert.IsTrue(ui.Match.StartMatch("anuik", new[] { "hymay" })); var hymay = Deploy("hymay"); ui.Match.Ready();
        var target = ui.Match.Actors[ui.Match.Bot.Monster];
        float windup = CombatSimulation.GetAttackWindup(hymay);
        Invoke("Attacked", hymay, target, true); hymay.ResetForCombat();
        yield return new WaitForSeconds(windup + .1f); Assert.IsNull(Find("hymay-whip"));
        Invoke("Attacked", hymay, target, true);
        // User-editable windup must elapse before a ranged attack releases its effect.
        float deadline = Time.time + windup + .5f;
        while (Find("hymay-whip") == null && Time.time < deadline) yield return null;
        yield return null;
        var whip = Find("hymay-whip"); Assert.IsNotNull(whip);
        var segments = whip.GetComponentsInChildren<Image>().Where(i => i.name == "whip-strand").ToArray();
        Assert.Greater(segments.Length, 3);
        Assert.Greater(segments.Select(s => Mathf.RoundToInt(s.transform.localEulerAngles.z)).Distinct().Count(), 2, "The rope must form a visible curve instead of a stretched straight stick.");
        Assert.IsTrue(segments.All(s => !s.raycastTarget));
        Assert.IsNull(Find("documented-projectile-hymay"));
        Invoke("Impacted", hymay, target, 4); yield return new WaitForSeconds(.3f);
        Assert.IsNull(Find("hymay-whip")); Assert.IsNull(Find("punch-contact"));
    }
}

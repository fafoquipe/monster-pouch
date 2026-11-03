using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class AlveyrBattleInfoPlayTests
{
    LocalGameUI ui;
    EronyaMapTestAccount account;
    bool languageExisted,spanishBefore;
    string languageBefore;
    const string LanguageKey="monster-pouch.language";

    [UnitySetUp] public IEnumerator LoadIsolatedBattle()
    {
        languageExisted=PlayerPrefs.HasKey(LanguageKey);languageBefore=PlayerPrefs.GetString(LanguageKey);spanishBefore=GameText.Spanish;
        account=new EronyaMapTestAccount(0);GameText.SetLanguage(true);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);Assert.IsFalse(ui.MapTransitionActive);
        Assert.IsTrue(ui.Match.StartMatch("alveyr",new[]{"palista"},10429));
        yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
    }

    [UnityTearDown] public IEnumerator RestoreAccountAndLanguage()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();}
        account?.Dispose();account=null;
        GameText.SetLanguage(spanishBefore);
        if(languageExisted)PlayerPrefs.SetString(LanguageKey,languageBefore);else PlayerPrefs.DeleteKey(LanguageKey);
        PlayerPrefs.Save();yield return null;
    }

    [UnityTest] public IEnumerator OpenCardTracksDamageHealthSpeedAndEnergyWithoutAdvancingCombat()
    {
        ui.Match.Ready();yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Combat,ui.Match.Phase);
        var owned=ui.Match.Player.Monster;var actor=ui.Match.Actors[owned];var simulation=ui.Match.Simulation;
        var baseline=simulation.CaptureBattleStats(actor);float elapsed=simulation.Elapsed;
        ui.ShowInspection(owned,false);yield return null;
        var label=ui.CanvasRoot.GetComponentsInChildren<Text>().Single(t=>t.name=="Battle live stats");
        string before=label.text;
        var clock=((IEnumerable)typeof(CombatSimulation).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(simulation)).Cast<object>()
            .Single(a=>(BattleUnit)a.GetType().GetField("Unit").GetValue(a)==actor);
        typeof(UnitStats).GetMethod("AddMaxHealth",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(actor.BaseStats,new object[]{50});
        actor.ApplyDamage(7);actor.AddEnergy(3);
        clock.GetType().GetField("DamageBonus").SetValue(clock,3);
        clock.GetType().GetField("SpeedBonus").SetValue(clock,.25f);
        yield return null;yield return null;
        var current=simulation.CaptureBattleStats(actor);
        Assert.AreNotEqual(before,label.text);Assert.AreEqual(BattleInfoText.Format(current,true),label.text);
        Assert.AreEqual(baseline.BaseHealth,current.BaseHealth);Assert.AreEqual(baseline.BaseDamage,current.BaseDamage);
        Assert.AreEqual(baseline.BaseAttacksPerSecond,current.BaseAttacksPerSecond);
        Assert.AreNotEqual(baseline.CurrentHealth,current.CurrentHealth);Assert.Greater(current.CurrentDamage,baseline.CurrentDamage);
        Assert.Greater(current.CurrentAttacksPerSecond,baseline.CurrentAttacksPerSecond);
        Assert.That(current.Energy,Is.EqualTo(actor.Energy));
        Assert.AreEqual(4,label.text.Split('\n').Length);
        Canvas.ForceUpdateCanvases();Assert.LessOrEqual(label.preferredHeight,label.rectTransform.rect.height+1,"Four rows must fit without clipping.");
        GameText.SetLanguage(false);yield return null;
        Assert.AreEqual(BattleInfoText.Format(current,false),label.text);
        Canvas.ForceUpdateCanvases();Assert.LessOrEqual(label.preferredHeight,label.rectTransform.rect.height+1,"Four English rows must fit without clipping.");
        var sheet=(RectTransform)label.transform.parent;
        var statsBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(sheet,label.rectTransform);
        foreach(var tile in sheet.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("skill-slot-")))
        {
            var tileBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(sheet,(RectTransform)tile.transform);
            Assert.GreaterOrEqual(statsBounds.min.y,tileBounds.max.y+8,"Live stats must leave space above every skill tile.");
        }
        Assert.AreEqual(elapsed,simulation.Elapsed,"Reading and refreshing the card cannot advance combat.");
    }

    // Requires Core legacy-parity: the UI alone cannot remove free round progression.
    [UnityTest] public IEnumerator EronyaHeroBuysOneChosenTrickWithMoonkensThroughTheRealCard()
    {
        // Use an authored positive price (Serya/Cael currently have one) so a
        // zero-cost Alveyr default cannot make a currency test pass vacuously.
        var priced=ui.Match.Config.Units.FirstOrDefault(d=>d.Ruleset==CombatRuleset.Eronya&&d.IsMonster&&
            d.Tricks[0]!=null&&d.Tricks[0].Cost>0&&!AbilityBalance.IsRetiredArmorEffect(d.Tricks[0].EffectId??d.Tricks[0].Id));
        Assert.IsNotNull(priced,"This integration fixture requires one authored Eronya hero with a positive Trick price.");
        ui.Match.Menu();Assert.IsTrue(ui.Match.StartMatch(priced.Id,new[]{"palista"},10430));yield return null;
        var owned=ui.Match.Player.Monster;
        Assert.AreEqual(0,owned.TrickCount,"New heroes must not receive a free round Trick.");
        int cost=System.Math.Max(0,owned.Definition.Tricks[0].Cost);
        ui.Match.Player.AddCombatCoins(cost+100);int before=ui.Match.Player.Coins;
        ui.ShowInspection(owned,false);yield return new WaitForSecondsRealtime(.3f);
        var tile=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="ability-0");
        ExecuteEvents.Execute(tile.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        yield return null;
        var buy=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="ability-upgrade");
        Assert.IsTrue(buy.IsInteractable(),"Eronya cannot have a separate UI purchase exclusion.");
        string visible=string.Join("\n",ui.CanvasRoot.GetComponentsInChildren<Text>().Select(t=>t.text));
        StringAssert.DoesNotContain("gratis por ronda",visible);StringAssert.DoesNotContain("TRICK POR RONDA",visible);
        ExecuteEvents.Execute(buy.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        yield return null;
        Assert.IsTrue(owned.Tricks[0]);Assert.AreEqual(1,owned.TrickCount);
        Assert.AreEqual(before-cost,ui.Match.Player.Coins);
    }
}

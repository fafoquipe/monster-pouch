using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ShieldEffectCardsPlayTests
{
    LocalGameUI ui;
    EronyaMapTestAccount account;
    bool spanishBefore,languageExisted;
    string languageBefore;
    const string LanguageKey="monster-pouch.language";

    [UnitySetUp] public IEnumerator LoadIsolatedMenu()
    {
        languageExisted=PlayerPrefs.HasKey(LanguageKey);languageBefore=PlayerPrefs.GetString(LanguageKey);spanishBefore=GameText.Spanish;
        account=new EronyaMapTestAccount(0);GameText.SetLanguage(true);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);Assert.IsFalse(ui.MapTransitionActive);
        Assert.IsTrue(ui.Match.StartMatch("alveyr",new[]{"palista"},103106));yield return null;
    }

    [UnityTearDown] public IEnumerator RestoreAccountAndLanguage()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();}
        account?.Dispose();account=null;
        GameText.SetLanguage(spanishBefore);
        if(languageExisted)PlayerPrefs.SetString(LanguageKey,languageBefore);else PlayerPrefs.DeleteKey(LanguageKey);
        PlayerPrefs.Save();yield return null;
    }

    [UnityTest] public IEnumerator ApprovedDefenseAndShieldBonusCardsAreVisibleWithResolvedCopy()
    {
        var cases=new[]{
            new[]{"guarda-veterano","guarda-veterano-dialogo-con-la-raiz","skill-slot--3"},
            new[]{"protectora-del-mausoleo","protectora-del-mausoleo-memoria-de-la-roca","skill-slot--3"},
            new[]{"protectora-del-mausoleo","protectora-del-mausoleo-memoria-compartida","ability-1"},
            new[]{"la-minera","la-minera-fractura","skill-slot--1"},
            new[]{"zephalontropo","zephalontropo-presion-calibrada","skill-slot--3"},
            new[]{"zephalontropo","zephalontropo-camara-de-precision","ability-0"}
        };
        foreach(var item in cases)
        {
            var definition=ui.Match.Config.Get(item[0]);Assert.IsNotNull(definition,item[0]);
            var ability=new[]{definition.BaseAbility,definition.PassiveAbility}.Concat(definition.Tricks)
                .Single(a=>a!=null&&(a.EffectId??a.Id)==item[1]);
            Assert.IsFalse(AbilityBalance.IsRetiredArmorEffect(item[1]),item[1]);
            ui.ShowInspection(new OwnedUnit(definition),false);yield return null;
            var tile=ui.CanvasRoot.GetComponentsInChildren<Button>().SingleOrDefault(b=>b.name==item[2]);
            Assert.IsNotNull(tile,item[1]+" must have a visible, nonempty card.");
            ExecuteEvents.Execute(tile.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            yield return null;
            var viewport=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Description viewport");
            string rendered=viewport.GetComponentInChildren<Text>().text;
            Assert.AreEqual(GameText.AbilityDescription(ability,definition,true),rendered,item[1]);
            Assert.IsNotEmpty(rendered);StringAssert.DoesNotContain("{",rendered);StringAssert.DoesNotContain("retirado",rendered);
        }
    }
}

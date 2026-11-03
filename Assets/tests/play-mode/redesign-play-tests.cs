using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class RedesignPlayTests
{
    LocalGameUI ui;bool spanish;string savedLanguage;bool languageExisted;
    [UnitySetUp] public IEnumerator Setup()
    {
        LobbyAnimationDirector.PlaybackEnabled=false;
        spanish=GameText.Spanish;languageExisted=PlayerPrefs.HasKey("monster-pouch.language");savedLanguage=PlayerPrefs.GetString("monster-pouch.language");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        LobbyAnimationDirector.PlaybackEnabled=false;
        if(ui!=null){ui.CloseModal();ui.Match.Pause(false);ui.Match.Menu();}
        GameText.SetLanguage(spanish);
        if(languageExisted)PlayerPrefs.SetString("monster-pouch.language",savedLanguage);else PlayerPrefs.DeleteKey("monster-pouch.language");
        yield return null;
    }
    [UnityTest] public IEnumerator CompactSheetHasExplicitPurchaseAndAllAbilitiesHaveDedicatedArt()
    {
        GameText.SetLanguage(true);ui.Match.StartMatch("anuik",new[]{"jazar"},80);ui.Match.Player.AddCombatCoins(30);
        ui.ShowInspection(ui.Match.Player.Monster,false);yield return new WaitForSecondsRealtime(.3f);
        var sheet=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Unit sheet");
        Assert.Greater(sheet.rect.height,0);
        Assert.LessOrEqual(sheet.rect.height,ui.CanvasRoot.rect.height,"The current ability sheet must fit the viewport.");
        var row=sheet.GetComponentsInChildren<Button>().Single(b=>b.name=="ability-0");
        int coins=ui.Match.Player.Coins;row.onClick.Invoke();
        Assert.AreEqual(coins,ui.Match.Player.Coins);
        var buy=sheet.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text.StartsWith("MEJORAR")==true);
        buy.onClick.Invoke();Assert.IsTrue(ui.Match.Player.Monster.Tricks[0]);
        foreach(var unit in ui.Match.Config.Units)
        foreach(var ability in new[]{unit.BaseAbility}.Concat(unit.Tricks).Where(a=>a!=null))
        {
            var sprite=AbilityArtwork.Get(ability.EffectId??ability.Id);
            Assert.IsNotNull(sprite,unit.Id+": "+ability.EffectId);
            Assert.Greater(sprite.rect.width,0);Assert.Greater(sprite.rect.height,0);
            StringAssert.DoesNotContain("{",GameText.AbilityDescription(ability,unit));
        }
        var illustrations=ui.Match.Config.Units.SelectMany(u=>new[]{u.BaseAbility}.Concat(u.Tricks))
            .Where(a=>a!=null).Select(a=>AbilityArtwork.Get(a.EffectId??a.Id)).ToArray();
        Assert.AreEqual(ui.Match.Config.Units.Count()*4,illustrations.Length);
        Assert.AreEqual(illustrations.Length,illustrations.Select(s=>(s.texture,s.rect)).Distinct().Count(),"Every ability uses a different illustration.");
    }
    [UnityTest] public IEnumerator BothLanguagesWorkAndEveryCharacterKeepsItsCurrentStaticPortrait()
    {
        var names=Resources.LoadAll<MonsterPouch.Gameplay.Characters.CharacterDefinition>("MonsterPouch/Characters")
            .ToDictionary(c=>c.Id,c=>c.General.DisplayName);
        foreach(bool es in new[]{true,false})
        {
            GameText.SetLanguage(es);ui.RenderPage();yield return null;
            Assert.AreEqual(es?"LISTO":"READY",GameText.Ready);
            foreach(var definition in ui.Match.Config.Units)
            {
                Assert.AreEqual(names[definition.Id],definition.DisplayName,
                    "Runtime must preserve the current asset name while retaining its stable ID.");
                Assert.IsNotEmpty(GameText.AbilityName(definition.BaseAbility));
                Assert.IsNotEmpty(GameText.AbilityDescription(definition.BaseAbility,definition));
                typeof(LocalGameUI).GetMethod("ShowCollectionInfo",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,new object[]{definition});
                var sheet=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Unit sheet");
                Assert.IsTrue(sheet.GetComponentsInChildren<Text>().Any(t=>t.text==names[definition.Id].ToUpperInvariant()),
                    definition.Id+": details must show the current name in both languages.");
                var pose=ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Last();
                var drawing=pose.GetComponentInChildren<LobbyFrameGraphic>();
                Assert.IsFalse(drawing.HasAuthoredFrames,definition.Id);
                var before=drawing.CurrentSprite;
                Assert.IsFalse(pose.HasAuthoredFrames,definition.Id);
                Assert.IsFalse(pose.PlayGesture());pose.Advance(120);yield return null;
                Assert.AreSame(before,drawing.CurrentSprite,definition.Id+" must preserve its current portrait.");
                Assert.IsTrue(drawing.IsAtRest,definition.Id+" restarted a retired gesture.");
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(4,drawing.canvasRenderer.GetMesh().vertexCount,definition.Id+" must render one complete drawing.");
                Assert.AreEqual(Vector3.one,pose.transform.localScale);
                ui.CloseModal();
            }
        }
    }
    [UnityTest] public IEnumerator JazzarPoolBubblesForThreeSecondsAndPauseFreezesIt()
    {
        ui.Match.StartMatch("anuik",new[]{"jazar"},81);ui.Match.Player.AddCombatCoins(30);
        var offers=ui.Match.Player.Offers;Assert.IsTrue(ui.Match.BuyAndPlace(0,offers[0].Token,ui.Match.Board.GetCell(0,9)));
        Assert.IsTrue(ui.Match.BuyTrick("jazar",2,1,offers[1].Token));ui.Match.Ready();
        yield return new WaitForSeconds(.35f);
        var floor=GameObject.Find("area-floor-jazar-front-row");Assert.IsNotNull(floor);
        Assert.AreEqual(6,floor.GetComponentsInChildren<SpriteRenderer>().Length);
        Assert.IsTrue(floor.GetComponentsInChildren<SpriteRenderer>().All(s=>s.sprite.name=="fx-pixel-toxic-puddle"));
        var bubble=ui.CanvasRoot.GetComponentsInChildren<Image>().First(i=>i.name=="toxic-bubble");
        ui.Match.Pause(true);var size=bubble.rectTransform.sizeDelta;
        yield return new WaitForSecondsRealtime(.15f);Assert.AreEqual(size,bubble.rectTransform.sizeDelta);
        ui.Match.Pause(false);yield return new WaitForSeconds(2.1f);Assert.IsNotNull(floor);
        yield return new WaitForSeconds(.7f);Assert.IsTrue(floor==null);
    }
}

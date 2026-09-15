using System.Collections;
using System.Linq;
using MonsterPouch.Local;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SeptemberUIPlayTests
{
    LocalGameUI ui;
    string[] keys={"monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string[] saved;bool[] existed;
    [UnitySetUp] public IEnumerator Setup()
    {
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        yield return null;
    }
    static Button ButtonNamed(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name==name);
    [UnityTest] public IEnumerator UpgradeClickBuysImmediatelyAndCombatShowsRockAndInstantRay()
    {
        Assert.IsTrue(ui.Match.StartMatch("sepora",new[]{"trimol"}));ui.Match.Player.AddCombatCoins(30);
        var offer=ui.Match.Player.Offers[0];
        Assert.IsTrue(ui.Match.BuyAndPlace(0,offer.Token,ui.Match.Board.GetCell(2,6)));
        yield return new WaitForSeconds(.8f);
        var owned=ui.Match.Player.Owned["trimol"];int before=ui.Match.Player.Coins;
        ui.InspectOffer(1,ui.Match.Player.Offers[1].Token);yield return null;
        var sheet=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Unit sheet");
        var buy=sheet.GetComponentsInChildren<Button>().First(b=>b.name.EndsWith(" MT"));
        buy.onClick.Invoke();Assert.IsTrue(owned.Tricks[0]);Assert.AreEqual(before-owned.Definition.Tricks[0].Cost,ui.Match.Player.Coins);
        Assert.IsNull(ui.Match.Player.Offers[1]);ui.CloseModal();yield return null;
        ui.Match.Ready();yield return null;
        var rock=ui.CanvasRoot.GetComponentsInChildren<Image>().First(i=>i.name=="rolling-rock");
        Assert.IsNotNull(rock.sprite);Assert.AreEqual(FilterMode.Point,rock.sprite.texture.filterMode);
        Quaternion rotation=rock.transform.localRotation;yield return null;Assert.AreNotEqual(rotation,rock.transform.localRotation);
        var sepora=ui.Match.Actors[ui.Match.Player.Monster];var target=ui.Match.Actors[ui.Match.Bot.Monster];
        Assert.IsTrue(ui.Match.Board.TryRepositionUnit(sepora,ui.Match.Board.GetCell(5,5)));
        Assert.IsTrue(ui.Match.Board.TryRepositionUnit(target,ui.Match.Board.GetCell(5,4)));
        sepora.AddEnergy(100);ui.Match.Simulation.Step(.1f);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="instant-ray-sepora"));
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Image>().Any(i=>i.name=="ray-contact"));
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>().Any(i=>i.name=="documented-projectile-sepora"));
    }
    [UnityTest] public IEnumerator HomeDeckAndCollectionUseCompleteDocumentedRoster()
    {
        Assert.AreEqual(19,ui.Match.Config.Units.Length);Assert.AreEqual("home",ui.MenuPage);
        Assert.IsNotNull(Resources.Load<Sprite>("MonsterPouch/UI/home-background"));
        ui.OpenDecks();yield return null;Assert.AreEqual("decks",ui.MenuPage);
        for(int i=0;i<7;i++)Assert.IsNotNull(ButtonNamed("deck-slot-"+i));
        ui.OpenCharacterPicker(-1);yield return null;
        ButtonNamed("loadout-sepora").onClick.Invoke();yield return null;
        ui.OpenHome();yield return null;ButtonNamed("JUGAR CONTRA BOT").onClick.Invoke();yield return null;
        Assert.AreEqual("sepora",ui.Match.Player.Monster.Definition.Id);
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
    }
    [UnityTest] public IEnumerator IndependentBriefSelectionAndRealRerollRemainFunctional()
    {
        ui.OpenDecks();ui.OpenCharacterPicker(0);yield return null;
        ButtonNamed("loadout-hymay").onClick.Invoke();yield return null;
        Assert.Contains("hymay",ui.SelectedWhelps.ToArray());
        ui.SelectDeck(1);yield return null;Assert.IsFalse(ui.SelectedWhelps.Contains("hymay"));
        ui.SelectDeck(0);yield return null;Assert.IsTrue(ui.SelectedWhelps.Contains("hymay"));
        ui.OpenHome();ButtonNamed("JUGAR CONTRA BOT").onClick.Invoke();yield return new WaitForSeconds(.5f);
        int before=ui.Match.Player.Rerolls;ButtonNamed("Reroll").onClick.Invoke();yield return new WaitForSeconds(.8f);
        Assert.AreEqual(before-1,ui.Match.Player.Rerolls);Assert.IsTrue(ui.BriefReady);
        Assert.IsFalse(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.text.Contains("Mantén para")||t.text.Contains("Arrastra al tablero")));
    }
}

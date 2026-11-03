using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MenuPolishPlayTests
{
    EronyaMapTestAccount mapAccount;
    LocalGameUI ui;
    bool existed;string saved;
    [UnitySetUp] public IEnumerator Load()
    {
        mapAccount=new EronyaMapTestAccount();
        existed=PlayerPrefs.HasKey(CardTableTheme.PreferenceKey);saved=PlayerPrefs.GetString(CardTableTheme.PreferenceKey);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Restore()
    {
        if(existed)PlayerPrefs.SetString(CardTableTheme.PreferenceKey,saved);else PlayerPrefs.DeleteKey(CardTableTheme.PreferenceKey);
        PlayerPrefs.Save();
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        yield return null;
        mapAccount?.Dispose();
    }
    void Inspect(OwnedUnit unit,bool collection,int selected)
    {
        typeof(LocalGameUI).GetMethod("ShowAbilityCard",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(ui,new object[]{unit,false,collection,selected,null});
    }
    RectTransform Sheet()=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Unit sheet");

    [UnityTest] public IEnumerator MoonkenPricesMatchBalanceAndBuyingAnUpgradeStillSpendsTheDisplayedAmount()
    {
        var match=ui.Match;
        Assert.IsTrue(match.StartMatch("bugaloo",new[]{"flo"},417));match.Player.AddCombatCoins(100);
        Assert.IsTrue(match.BuyAndPlace(0,match.Player.Offers[0].Token,match.Board.GetCell(1,6)));
        var unit=match.Player.Owned["flo"];int cost=unit.Definition.Tricks[0].Cost;
        Inspect(unit,false,0);yield return new WaitForSecondsRealtime(.3f);
        var buy=Sheet().Find("ability-upgrade").GetComponent<Button>();
        Assert.IsTrue(buy.interactable);
        var badge=buy.transform.Find("Moonken cost");Assert.IsNotNull(badge);
        Assert.AreEqual(cost.ToString(),badge.GetComponentInChildren<Text>().text);
        Assert.AreSame(ui.MoonIcon,badge.GetComponentInChildren<Image>().sprite);
        Assert.IsFalse(Sheet().GetComponentsInChildren<Text>().Any(t=>t.text.Contains(" MK")));
        int before=match.Player.Coins;buy.onClick.Invoke();yield return null;
        Assert.IsTrue(unit.Tricks[0]);Assert.AreEqual(before-cost,match.Player.Coins);
        Assert.IsFalse(Sheet().Find("ability-upgrade").GetComponent<Button>().interactable);
        var purchasedPrice=Sheet().Find("ability-upgrade/Moonken cost");
        Assert.IsTrue(purchasedPrice==null||!purchasedPrice.gameObject.activeSelf);
        match.Menu();
        foreach(var definition in match.Config.Units)
        {
            Inspect(new OwnedUnit(definition),true,0);yield return null;
            Assert.IsFalse(Sheet().Find("ability-upgrade").GetComponent<Button>().interactable,"Collection must not spend coins.");
            foreach(var text in Sheet().GetComponentsInChildren<Text>())
                Assert.IsFalse(text.text.Contains(" MK"),definition.Id);
            for(int i=0;i<3;i++)Assert.AreEqual(definition.Tricks[i].Cost.ToString(),
                Sheet().Find("ability-"+i+"/Moonken cost").GetComponentInChildren<Text>().text);
        }
    }

    [UnityTest] public IEnumerator GuestSeesHostMapFirstThenOwnMapAndMenuRestoresPersonalChoice()
    {
        ui.SelectTableSkin(TableStyle.GrandCasaHotel);
        yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);ui.CloseModal();
        var match=ui.Match;
        Assert.IsTrue(match.StartOnlineMatch(BoardSide.Red,"sepora",new[]{"tsu"},"bugaloo",new[]{"tsu"},417,"lounge","royal"));
        yield return null;ui.RenderPage();yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
        Assert.AreEqual(TableStyle.CampoReal,ui.DisplayedArenaStyle);
        var royal=Object.FindFirstObjectByType<NationArenaScenery>();Assert.IsTrue(royal.IsVisible);Assert.AreEqual("royal",royal.Artwork.NationId);
        var cellSize=match.Mapper.CellSize;
        match.Ready();match.SetOpponentReady();Assert.IsTrue(match.RevealOpponent(new PreparationCommand[0],1));
        Assert.IsTrue(match.StartOnlineCombat(1));
        foreach(var actor in match.Actors.Values)actor.ApplyDamage(actor.CurrentHealth);
        match.Advance(.1f);Assert.IsTrue(match.NextOnlineRound(1));yield return null;ui.RenderPage();
        yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
        Assert.AreEqual(TableStyle.GrandCasaHotel,ui.DisplayedArenaStyle);Assert.IsTrue(royal.IsVisible);Assert.AreEqual("lounge",royal.Artwork.NationId);
        Assert.IsTrue(Object.FindFirstObjectByType<CardTableArena>().LoungeEnabled);
        Assert.AreEqual(cellSize,match.Mapper.CellSize);
        Assert.AreEqual("lounge",PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
        match.Menu();ui.RenderPage();yield return null;
        Assert.AreEqual(TableStyle.GrandCasaHotel,ui.DisplayedArenaStyle);
        Assert.AreEqual("lounge",PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
    }
}

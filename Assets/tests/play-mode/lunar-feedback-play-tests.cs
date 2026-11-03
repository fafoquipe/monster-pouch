using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class LunarFeedbackPlayTests
{
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        ui.Match.StartMatch("bugaloo",12);ui.Match.Ready();ui.RenderPage();yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {if(ui!=null){ui.Match.Menu();ui.RenderPage();ui.Match.enabled=true;}yield return null;}
    RectTransform Find(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);
    void StartIncome(bool local)
    {
        var actor=ui.Match.Actors.Values.First(a=>(a.Side==ui.Match.LocalSide)==local);
        typeof(MatchController).GetMethod("QueueMoonkenDelivery",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(ui.Match,new object[]{actor,1});
    }
    IEnumerator Advance(float duration)
    {float elapsed=0;while(elapsed<duration){float dt=Mathf.Min(Time.unscaledDeltaTime,duration-elapsed);ui.Match.Advance(dt);elapsed+=dt;yield return null;}}
    [UnityTest] public IEnumerator IncomeShinesFirstThenFliesToHudWithSameCurrencyArtwork()
    {
        int coins=ui.Match.Player.Coins;StartIncome(true);
        Assert.IsNotNull(Find("Blotan shining Moonken"));Assert.IsNull(Find("Sold Moonken"));
        Assert.AreSame(ui.MoonIcon,Find("Moonken").GetComponent<Image>().sprite);
        Assert.AreEqual(coins,ui.Match.Player.Coins);
        float startHeight=Find("Blotan shining Moonken").anchoredPosition.y;
        yield return Advance(.3f);Assert.IsNotNull(Find("Blotan shining Moonken"));
        Assert.Greater(Find("Blotan shining Moonken").anchoredPosition.y,startHeight+35);
        yield return Advance(.5f);
        Assert.IsNull(Find("Blotan shining Moonken"));Assert.IsNotNull(Find("Sold Moonken"));
        Assert.AreEqual(coins,ui.Match.Player.Coins,"No payment while the Moonken is still flying.");
        Assert.AreSame(ui.MoonIcon,Find("Sold Moonken").GetComponent<Image>().sprite);
        bool popped=false;float until=Time.time+.9f;
        while(Time.time<until){ui.Match.Advance(Time.unscaledDeltaTime);popped|=Find("Moonkens icon").localScale.x>1.03f;yield return null;}
        Assert.IsTrue(popped);Assert.IsNull(Find("Sold Moonken"));
        Assert.AreEqual(coins+1,ui.Match.Player.Coins,"Pay exactly once, at arrival.");
        var balance=(Text)typeof(LocalGameUI).GetField("currencyText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        Assert.AreEqual((coins+1).ToString(),balance.text,"The visible HUD must update on arrival too.");
    }
    [UnityTest] public IEnumerator EnemyIncomeNeverFliesIntoTheLocalWallet()
    {
        StartIncome(false);yield return Advance(.8f);
        Assert.IsNull(Find("Blotan shining Moonken"));Assert.IsNull(Find("Sold Moonken"));
    }
    [UnityTest] public IEnumerator LeavingCombatClearsTheOverheadIncomeEffect()
    {
        StartIncome(true);ui.Match.Menu();yield return null;yield return null;
        Assert.IsNull(Find("Blotan shining Moonken"));Assert.IsNull(Find("Sold Moonken"));
    }
    [UnityTest] public IEnumerator PausingFreezesDeliveryAndItsPayment()
    {
        int before=ui.Match.Player.Coins;StartIncome(true);ui.Match.Pause(true);
        yield return new WaitForSecondsRealtime(.35f);ui.Match.Advance(2);
        Assert.AreEqual(before,ui.Match.Player.Coins);Assert.IsNotNull(Find("Blotan shining Moonken"));
        ui.Match.Pause(false);yield return Advance(1.4f);
        Assert.AreEqual(before+1,ui.Match.Player.Coins);
    }
    [UnityTest] public IEnumerator EndingTheRoundDoesNotLoseAnIncomeAlreadyFlying()
    {
        int before=ui.Match.Player.Coins;StartIncome(true);yield return Advance(.8f);
        typeof(MatchController).GetField("<Phase>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui.Match,MatchPhase.RoundResult);
        yield return Advance(.6f);
        Assert.AreEqual(before+1,ui.Match.Player.Coins);Assert.IsNull(Find("Sold Moonken"));
    }
}

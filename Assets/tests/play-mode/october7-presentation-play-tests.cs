using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class October7PresentationPlayTests
{
    LocalGameUI ui;MatchConfig config;
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        config=Object.Instantiate(ui.Match.Config);
        foreach(var definition in config.Units){definition.MaxHealth=10000;definition.Damage=0;}
        ui.Match.Configure(config,ui.Match.Board,ui.Match.Mapper);
    }
    [UnityTearDown] public IEnumerator Cleanup(){ui.Match.Menu();Object.Destroy(config);yield return null;}
    void Purchase(string id)
    {
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{id},417));ui.Match.Player.AddCombatCoins(100);
        var offer=ui.Match.Player.Offers[0];Assert.IsTrue(ui.Match.BuyAndPlace(0,offer.Token,ui.Match.Board.GetCell(2,6)));
        offer=ui.Match.Player.Offers[1];Assert.IsTrue(ui.Match.BuyTrick(id,1,1,offer.Token));
        ui.Match.Ready();
    }
    [UnityTest] public IEnumerator BlotanTeleportsWithoutWalkingAndKeepsCoinAnimationAsVisibleHologram()
    {
        config.Get("blotan").BaseAbility.Parameters=new[]{new AbilityParameter{Key="interval",Value=2}};
        Purchase("blotan");yield return null;
        var actor=ui.Match.Actors.Values.Single(a=>a.Side==ui.Match.LocalSide&&a.BaseStats.DefinitionId=="blotan");
        var view=actor.GetComponent<UnitPresentation>();
        ui.Match.Advance(2);yield return null;
        Assert.IsTrue(actor.IsInvisible);Assert.IsFalse(view.Renderer.forceRenderingOff);
        Assert.That(view.Renderer.color.a,Is.InRange(.3f,.5f));Assert.IsFalse(view.IsMoving);
        Assert.That(Vector3.Distance(actor.transform.position,ui.Match.Mapper.GetWorldPosition(actor.CurrentCell)),Is.LessThan(.01f));
        var special=typeof(UnitPresentation).GetField("specialActive",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.IsTrue((bool)special.GetValue(view),"The attack reset must not cancel the coin animation.");
        ui.Match.Advance(.5f);yield return null;Assert.IsFalse(actor.IsInvisible);Assert.IsFalse(view.Renderer.forceRenderingOff);
    }
    [UnityTest] public IEnumerator ChocolatePeriodicHitCreatesMiniExplosionsAndCleansThemUp()
    {
        var tsu=config.Get("tsu");tsu.AttackInterval=100;tsu.AttackWindup=.1f;tsu.AttackRange=20;
        tsu.BaseAbility.Parameters=new[]{new AbilityParameter{Key="every",Value=1},new AbilityParameter{Key="duration",Value=2}};
        Purchase("tsu");yield return null;
        GameObject burst=null;
        for(int tick=0;tick<50&&burst==null;tick++)
        {ui.Match.Advance(.1f);yield return null;burst=GameObject.Find("tsu-gum-fireworks");}
        Assert.IsNotNull(burst,"The real periodic damage event must produce the mini explosions.");
        Assert.Greater(burst.transform.childCount,3);ui.Match.Menu();yield return null;yield return null;
        Assert.IsTrue(burst==null,"Effects must not leak into the next round or the menu.");
    }
}

using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ClashPolishPlayTests
{
    LocalGameUI ui;
    [UnitySetUp]public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown]public IEnumerator Cleanup(){if(ui!=null){ui.CloseModal();ui.Match.Menu();}yield return null;}
    [UnityTest]public IEnumerator CharacterPickerHasStaticPortraits()
    {
        ui.OpenDecks();
        ui.OpenCharacterPicker(0);
        yield return new WaitForSecondsRealtime(.3f);
        var grid=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Collection grid");
        Assert.IsEmpty(grid.GetComponentsInChildren<LobbyPersonality>());
        var images=grid.GetComponentsInChildren<Image>().Where(i=>i.sprite!=null).ToArray();
        var frames=images.Select(i=>i.sprite).ToArray();var positions=images.Select(i=>i.transform.localPosition).ToArray();
        yield return new WaitForSecondsRealtime(1.1f);
        CollectionAssert.AreEqual(frames,images.Select(i=>i.sprite).ToArray());
        CollectionAssert.AreEqual(positions,images.Select(i=>i.transform.localPosition).ToArray());
    }
    [UnityTest]public IEnumerator FirstRoundDefaultEnemyIsStaticAndCurrentBotFormationIsPrivate()
    {
        ui.Match.StartMatch("anuik",new[]{"stein"},905);
        yield return new WaitForSecondsRealtime(.3f);
        var snapshot=ui.Match.InitialOpponent;
        var echoes=Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None);
        Assert.AreEqual(1,echoes.Length);
        var view=echoes[0];var position=view.FootPosition;
        var sprite=view.Renderer.sprite;var scale=view.Renderer.transform.localScale;
        var inspection=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="echo-"+snapshot.UnitId);
        var hitPosition=inspection.anchoredPosition;
        Assert.AreEqual(snapshot.UnitId,view.UnitId);
        Assert.AreEqual(snapshot.Position,view.SnapshotPosition);
        Assert.AreEqual(ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(snapshot.Position.x,snapshot.Position.y)),position);
        Assert.IsFalse(view.IsHologram,"The first round reveals the hero's solid default pose.");
        Assert.IsNotNull(sprite);Assert.IsTrue(view.Renderer.enabled);Assert.AreEqual(1,view.Renderer.color.a);
        Assert.IsTrue(ui.Match.Actors.Values.All(a=>a.Side==BoardSide.Blue));
        Assert.IsTrue(ui.Match.Bot.TrySetLocation(ui.Match.Bot.Monster.Definition.Id,MonsterPouch.Gameplay.Match.UnitLocation.Field,new Vector2Int(5,0),out _));
        Assert.AreNotEqual(snapshot.Position,ui.Match.Bot.Monster.Deployment,"The fixture must actually move the hidden bot.");
        ui.RenderPage();yield return null;
        var same=Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None).Single();
        Assert.AreEqual(position,same.FootPosition);Assert.AreSame(sprite,same.Renderer.sprite);
        Assert.AreEqual(scale,same.Renderer.transform.localScale);Assert.IsFalse(same.IsHologram);
        Assert.AreEqual(hitPosition,ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="echo-"+snapshot.UnitId).anchoredPosition,
            "The long-press target must remain on the revealed pose, not the bot's private cell.");
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text=="PREPARACIÓN"||t.text=="PREPARATION"));
    }
    [UnityTest]public IEnumerator HealthAndEnergyAreLegibleSeparateGauges()
    {
        ui.Match.StartMatch("anuik",new[]{"stein"},906);ui.Match.Player.AddCombatCoins(30);
        var offer=ui.Match.Player.Offers[0];Assert.IsTrue(ui.Match.BuyAndPlace(0,offer.Token,ui.Match.Board.GetCell(2,8)));
        ui.Match.Ready();yield return null;
        var actor=ui.Match.Actors.First(p=>p.Key.Definition.Id=="stein"&&p.Value.Side==BoardSide.Blue).Value;
        actor.ApplyDamage(13);actor.AddEnergy(actor.BaseStats.EnergyMax*.5f);yield return null;yield return null;
        float health=(float)actor.CurrentHealth/actor.BaseStats.MaxHealth;
        var bars=ui.CanvasRoot.GetComponentsInChildren<UnitVitalsUI>();
        Assert.IsTrue(bars.Any(b=>b.ShowsEnergy&&Mathf.Abs(b.EnergyFraction-.5f)<.001f&&Mathf.Abs(b.HealthFraction-health)<.001f));
        Assert.IsTrue(bars.All(b=>!b.raycastTarget&&b.rectTransform.rect.height>=26));
        Canvas.ForceUpdateCanvases();
        foreach(var bar in bars)
        {
            Assert.IsNotNull(bar.GetComponent<CanvasRenderer>(),"Each gauge must actually render.");
            var mesh=bar.canvasRenderer.GetMesh();
            Assert.Greater(mesh.vertexCount,12,"Health and energy require visible geometry.");Object.Destroy(mesh);
        }
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().All(t=>t.font!=null));
        Assert.IsNotNull(Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold"));
        Assert.IsNotNull(Resources.Load<Font>("MonsterPouch/UI/Fonts/LilitaOne-Regular"));
    }
}

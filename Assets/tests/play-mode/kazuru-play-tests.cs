using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class KazuruPlayTests
{
    LocalGameUI ui;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    readonly Dictionary<string,string> preferences=new Dictionary<string,string>();
    readonly HashSet<string> existed=new HashSet<string>();
    static readonly string[] Keys={"monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    [UnitySetUp] public IEnumerator Setup()
    {
        foreach(var key in Keys){if(PlayerPrefs.HasKey(key))existed.Add(key);preferences[key]=PlayerPrefs.GetString(key);}
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.Match.Pause(false);ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        foreach(var key in Keys){if(existed.Contains(key))PlayerPrefs.SetString(key,preferences[key]);else PlayerPrefs.DeleteKey(key);}
        yield return null;
    }
    [UnityTest] public IEnumerator SelectableAsMonsterWithReadableArtAndAnimatedPreparationRings()
    {
        var definition=ui.Match.Config.Get("kazuru");Assert.NotNull(definition);Assert.IsTrue(definition.IsMonster);
        ui.OpenCharacterPicker(-1);yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Button>(true).Any(b=>b.name=="loadout-kazuru"));
        ui.CloseModal();ui.OpenCharacterPicker(0);yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="loadout-kazuru"));
        ui.CloseModal();
        typeof(LocalGameUI).GetMethod("ShowCollectionInfo",Private).Invoke(ui,new object[]{definition});yield return null;
        var graphic=ui.CanvasRoot.GetComponentsInChildren<LobbyFrameGraphic>().FirstOrDefault(g=>g.UnitId=="kazuru");
        Assert.NotNull(graphic);Assert.NotNull(graphic.CurrentSprite);
        StringAssert.Contains("kazuru-idle-pixel-v2",graphic.CurrentSprite.texture.name);
        Assert.AreEqual(128,graphic.CurrentSprite.texture.width);
        foreach(var effect in new[]{definition.BaseAbility}.Concat(definition.Tricks))
        {
            Assert.NotNull(AbilityArtwork.Get(effect.EffectId));
            Assert.That(GameText.AbilityDescription(effect,definition),Does.Not.Contain("{"));
        }
        Capture("collection");ui.CloseModal();
        typeof(LocalGameUI).GetField("chosen",Private).SetValue(ui,"kazuru");
        ui.OpenHome();ui.RenderPage();yield return null;yield return null;
        var home=ui.CanvasRoot.GetComponentsInChildren<LobbyFrameGraphic>().First(g=>g.UnitId=="kazuru");
        Assert.That(home.CurrentSprite.texture.name,Is.EqualTo("kazuru-idle-v1"));
        Assert.Greater(home.CurrentSprite.texture.width,128,"Detailed idle belongs to Battle Home; Brief retains its pixel version.");
        Capture("lobby-idle");
        Assert.IsTrue(ui.Match.StartMatch("kazuru",new[]{"rosie","suki","pokrak"},321));
        ui.RenderPage();yield return null;yield return null;
        var unit=ui.Match.Actors.Values.Single(a=>a.BaseStats.DefinitionId=="kazuru"&&a.Side==ui.Match.Player.Side);
        var orbit=unit.GetComponent<KazuruRingOrbit>();Assert.NotNull(orbit);
        var rings=unit.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name.StartsWith("kazuru-orbit-")).ToArray();
        Assert.AreEqual(3,rings.Length);Assert.IsTrue(rings.All(r=>r.enabled&&r.sprite!=null));
        var before=rings.Select(r=>r.transform.position).ToArray();
        var body=unit.GetComponent<UnitPresentation>().Renderer;var sprite=body.sprite;
        yield return new WaitForSeconds(.6f);
        Assert.IsTrue(rings.Where((r,i)=>Vector3.Distance(before[i],r.transform.position)>.02f).Any());
        Assert.AreNotSame(sprite,body.sprite,"Preparation must animate the authored breathing pose.");
        Capture("preparation");
        var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/kazuru/Character");
        Assert.IsTrue(character.General.IsMonster);
        Assert.AreEqual(3,character.Upgrades.Length);
        Assert.IsTrue(character.Visuals.DirectionalAnimations.All(a=>a.Idle.Length>1&&a.Attack.Length>1&&a.Special.Length>1&&a.Death.Length>1));
    }
    object Actor(BattleUnit unit)=>((IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(ui.Match.Simulation)).Cast<object>()
        .Single(a=>a.GetType().GetField("Unit").GetValue(a)==(object)unit);
    [UnityTest] public IEnumerator SuperGroundRingsAndLethalHaloFollowAuthoritativeClock()
    {
        var match=ui.Match;
        Assert.IsTrue(match.StartMatch("kazuru",new[]{"rosie"},322));match.Player.Monster.Tricks[1]=true;
        match.Ready();ui.RenderPage();yield return null;yield return null;
        var unit=match.Actors.Values.Single(a=>a.BaseStats.DefinitionId=="kazuru"&&a.Side==match.Player.Side);
        var enemy=match.Actors.Values.First(a=>a.Side!=unit.Side);
        foreach(var actor in ((IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(match.Simulation)))
        {actor.GetType().GetField("NextMove").SetValue(actor,99999);actor.GetType().GetField("NextAttack").SetValue(actor,99999);}
        match.Simulation.Step(match.Config.OpeningAbilityWindowSeconds);
        unit.AddEnergy(unit.BaseStats.EnergyMax);match.Simulation.Step(.4f);
        yield return null;yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="kazuru-rising-rings"));
        var ground=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().First(r=>r.name=="ground-ring");
        Assert.That(ground.rect.width,Is.EqualTo(ground.rect.height).Within(.01f),"The ring artwork already has the correct ellipse; never squash its padded atlas twice.");
        yield return new WaitForSeconds(.2f);Capture("super");
        typeof(CombatSimulation).GetMethod("DirectDamage",Private).Invoke(match.Simulation,new[]{Actor(enemy),Actor(unit),(object)10000});
        yield return null;yield return null;
        Assert.AreEqual(1,unit.CurrentHealth);Assert.IsTrue(match.Simulation.KazuruHaloActive(unit));
        var halo=unit.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="kazuru-orbit-0");
        Assert.AreEqual("halo",halo.sprite.name);Capture("halo");
        match.Simulation.Step(2);yield return null;
        Assert.IsFalse(match.Simulation.KazuruHaloActive(unit));Assert.AreEqual("tilted",halo.sprite.name);
    }
    [UnityTest] public IEnumerator BurstShowsASecondProjectileFromTheCombatClockAndPauseDelaysItsRelease()
    {
        var match=ui.Match;
        Assert.IsTrue(match.StartMatch("kazuru",new[]{"rosie","suki","pokrak"},323));
        match.Player.Monster.Tricks[0]=true;match.Ready();ui.RenderPage();yield return null;yield return null;
        var unit=match.Actors.Values.Single(a=>a.BaseStats.DefinitionId=="kazuru"&&a.Side==match.Player.Side);
        var enemy=match.Actors.Values.First(a=>a.Side!=unit.Side);
        var cell=match.Board.GetAllCells().First(c=>c.OccupiedBy==null&&
            Mathf.Max(Mathf.Abs(c.X-unit.CurrentCell.X),Mathf.Abs(c.Y-unit.CurrentCell.Y))==1);
        match.Board.ReleaseUnit(enemy);Assert.IsTrue(match.Board.TryOccupyCell(enemy,cell));
        foreach(var actor in ((IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(match.Simulation)))
        {actor.GetType().GetField("NextMove").SetValue(actor,99999);actor.GetType().GetField("NextAttack").SetValue(actor,99999);}
        match.Simulation.Step(match.Config.OpeningAbilityWindowSeconds);
        int attacks=0,followups=0;match.Attacked+=(a,b,p)=>{if(a==unit)attacks++;};
        match.KazuruRingLaunched+=(a,b,t)=>{if(a==unit)followups++;};
        typeof(CombatSimulation).GetMethod("QueueBasicAttack",Private).Invoke(match.Simulation,new object[]{Actor(unit),enemy});
        float windup=CombatSimulation.GetAttackWindup(unit);
        yield return new WaitForSeconds(windup+.02f);
        var before=ui.CanvasRoot.GetComponentsInChildren<Image>().Where(i=>i.name=="documented-projectile-kazuru").Select(i=>i.GetInstanceID()).ToArray();
        float clock=match.Simulation.Elapsed;match.Pause(true);match.Advance(1);yield return null;
        Assert.AreEqual(clock,match.Simulation.Elapsed);Assert.AreEqual(0,followups);
        match.Pause(false);
        float gap=unit.BaseStats.AbilityValue("kazuru-ring-throw","burstInterval",.2f);
        match.Advance((AbilityTiming.Ticks(windup)+Mathf.Max(1,AbilityTiming.Ticks(gap)))*CombatSimulation.TickDuration);
        Assert.AreEqual(1,attacks,"The second ring must not restart the attack animation or count as another basic attack.");
        Assert.AreEqual(1,followups);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Image>().Any(i=>i.name=="documented-projectile-kazuru"&&!before.Contains(i.GetInstanceID())),
            "The authoritative second release must produce a new visible ring.");
        yield return null;
    }

    void Capture(string name)
    {
        var camera=ui.GameCamera;var canvas=ui.CanvasRoot.GetComponentInParent<Canvas>();
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var order=canvas.sortingOrder;var distance=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(Screen.width,Screen.height,24);var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder=short.MaxValue;
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            string folder=Path.GetFullPath("Logs/local-validation/kazuru");Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=oldCamera;
            canvas.planeDistance=distance;canvas.sortingOrder=order;Object.Destroy(image);Object.Destroy(target);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
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

public sealed class PokrakPlayTests
{
    LocalGameUI ui;
    bool originalLobbyPlayback;
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    readonly Dictionary<string,string> preferences = new Dictionary<string,string>();
    readonly HashSet<string> existed = new HashSet<string>();
    static readonly string[] Keys = { "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps" };
    [UnitySetUp] public IEnumerator Setup()
    {
        originalLobbyPlayback=LobbyAnimationDirector.PlaybackEnabled;
        LobbyAnimationDirector.PlaybackEnabled=false;
        preferences.Clear();existed.Clear();
        foreach(string key in Keys){if(PlayerPrefs.HasKey(key))existed.Add(key);preferences[key]=PlayerPrefs.GetString(key);}
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        LobbyAnimationDirector.PlaybackEnabled=originalLobbyPlayback;
        if(ui!=null){ui.Match.Pause(false);ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        foreach(string key in Keys){if(existed.Contains(key))PlayerPrefs.SetString(key,preferences[key]);else PlayerPrefs.DeleteKey(key);}
        yield return null;
    }
    void Capture(string name)
    {
        var folder=Path.GetFullPath("Logs/local-validation/pokrak");Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
    }
    [UnityTest] public IEnumerator CollectionShowsCurrentPokrakPortraitIconsAndKeepsBattleAnimations()
    {
        var definition=ui.Match.Config.Get("pokrak");Assert.NotNull(definition);
        ui.OpenCharacterPicker(0);yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Button>(true).Any(b=>b.name=="loadout-pokrak"));
        typeof(LocalGameUI).GetMethod("ShowCollectionInfo",Private).Invoke(ui,new object[]{definition});yield return null;
        var details=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Character details");
        var figure=details.GetComponentInChildren<LobbyFrameGraphic>();
        Assert.AreEqual("pokrak",figure.UnitId);
        Assert.IsFalse(figure.HasAuthoredFrames);Assert.NotNull(figure.CurrentSprite);
        foreach(var effect in new[]{definition.BaseAbility}.Concat(definition.Tricks))
        {
            var icon=AbilityArtwork.Get(effect.EffectId);Assert.NotNull(icon);
            Assert.AreEqual(new Vector2(64,64),icon.rect.size);Assert.AreEqual(FilterMode.Point,icon.texture.filterMode);
            string description=GameText.AbilityDescription(effect,definition);
            Assert.IsNotEmpty(description);StringAssert.DoesNotContain("{",description);
        }
        var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/pokrak/Character");
        foreach(var clip in character.Visuals.DirectionalAnimations)
        {
            Assert.AreNotSame(clip.Idle[0],clip.Move[1]);Assert.AreNotSame(clip.Idle[0],clip.Attack[1]);
            Assert.AreNotSame(clip.Special[0],clip.Special[1]);Assert.AreNotSame(clip.Idle[0],clip.Death.Last());
        }
        yield return new WaitForSeconds(.35f);Capture("collection");yield return null;yield return null;
    }
    void Place(Dictionary<string,OwnedUnit> roster,string id,int x,int y,bool upgraded=false)
    {
        var unit=new OwnedUnit(ui.Match.Config.Get(id));if(upgraded)for(int i=0;i<3;i++)unit.Tricks[i]=true;
        typeof(OwnedUnit).GetProperty("Location").SetValue(unit,UnitLocation.Field);
        typeof(OwnedUnit).GetProperty("Deployment").SetValue(unit,new Vector2Int(x,y));roster.Add(id,unit);
    }
    [UnityTest] public IEnumerator BubblesCoverAlliesOnBothTeamsPauseAndExpireWithoutExtraHealth()
    {
        var match=ui.Match;Assert.IsTrue(match.StartMatch("anuik",new[]{"pokrak","suki","rosie"},116));
        typeof(MatchController).GetMethod("ClearActors",Private).Invoke(match,null);match.Board.BuildBoard();
        var player=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Private).GetValue(match.Player);
        var bot=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Private).GetValue(match.Bot);
        player.Clear();bot.Clear();
        Place(player,"pokrak",2,7,true);Place(player,"anuik",1,7);Place(player,"suki",2,9);Place(player,"rosie",4,8);
        Place(bot,"pokrak",3,2,true);Place(bot,"anuik",4,2);Place(bot,"suki",3,0);Place(bot,"rosie",1,1);
        match.Ready();ui.RenderPage();yield return null;yield return null;
        var clocks=(IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(match.Simulation);
        foreach(var clock in clocks){clock.GetType().GetField("NextMove").SetValue(clock,9999);clock.GetType().GetField("NextAttack").SetValue(clock,9999);}
        var pokraks=match.Actors.Values.Where(a=>a.BaseStats.DefinitionId=="pokrak").ToArray();Assert.AreEqual(2,pokraks.Length);
        var areas=new List<BoardCell[]>();match.Simulation.AreaEffect+=(u,k,c)=>{if(k=="pokrak-bubbles")areas.Add(c);};
        match.Simulation.Step(match.Config.OpeningAbilityWindowSeconds);
        foreach(var actor in pokraks)actor.AddEnergy(actor.BaseStats.EnergyMax);
        match.Simulation.Step(AbilityTiming.Cast(pokraks[0].BaseStats,"pokrak-super")+CombatSimulation.TickDuration);yield return null;yield return null;
        Assert.AreEqual(2,areas.Count);Assert.IsTrue(areas.All(c=>c.Length==13));
        var roots=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="fx-pokrak-guard").ToArray();
        Assert.AreEqual(6,roots.Length,"Pokrak, Anuik and Suki get bubbles; diagonal Rosie stays out.");
        foreach(var root in roots)
        {
            var dome=root.GetComponentsInChildren<Image>().Single(i=>i.name=="torso-dome");
            Assert.AreEqual("status-bubble",dome.sprite.name);Assert.AreEqual(FilterMode.Point,dome.sprite.texture.filterMode);
            Assert.AreEqual(dome.rectTransform.sizeDelta.x,dome.rectTransform.sizeDelta.y,.01f,"Bubble is round.");
            Assert.Less(dome.color.a,.8f);Assert.IsFalse(dome.raycastTarget);
        }
        var badge=CombatHudSymbols.Get("pokrak-guard");
        Assert.AreEqual("hud-pokrak-guard",badge.name);Assert.AreEqual(new Vector2(16,16),badge.rect.size);
        Assert.AreEqual(FilterMode.Point,badge.texture.filterMode);
        foreach(var actor in match.Actors.Values)Assert.AreEqual(0,actor.Shield);
        Capture("bubbles-both-teams");yield return null;yield return null;
        match.Pause(true);yield return null;var before=roots.Select(r=>r.anchoredPosition).ToArray();float clockBefore=match.Simulation.Elapsed;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(clockBefore,match.Simulation.Elapsed);CollectionAssert.AreEqual(before,roots.Select(r=>r.anchoredPosition));
        float duration=pokraks[0].BaseStats.AbilityValue("pokrak-long-bubbles","duration",3);
        match.Pause(false);match.Simulation.Step(duration-CombatSimulation.TickDuration);yield return null;
        Assert.AreEqual(6,ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Count(r=>r.name=="fx-pokrak-guard"));
        match.Simulation.Step(CombatSimulation.TickDuration);yield return null;yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="fx-pokrak-guard"));
        foreach(var actor in pokraks)Assert.AreEqual(0,match.Simulation.PokrakBubbleReduction(actor));
        match.Menu();yield return null;
    }
}

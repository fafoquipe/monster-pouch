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

public sealed class SukiPlayTests
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
        var folder=Path.GetFullPath("Logs/local-validation/suki");Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
    }
    [UnityTest] public IEnumerator CollectionUsesSquarePixelIconsAndRealAdditiveDescription()
    {
        var definition=ui.Match.Config.Get("suki");Assert.NotNull(definition);
        ui.OpenCharacterPicker(0);yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Button>(true).Any(b=>b.name=="loadout-suki"));
        typeof(LocalGameUI).GetMethod("ShowCollectionInfo",Private).Invoke(ui,new object[]{definition});yield return null;
        // The brief behind the detail modal can also contain a static Suki portrait.
        var figure=ui.CanvasRoot.GetComponentsInChildren<LobbyFrameGraphic>().Last(f=>f.UnitId=="suki");
        Assert.IsFalse(figure.HasAuthoredFrames);Assert.NotNull(figure.CurrentSprite);
        foreach(var effect in new[]{definition.BaseAbility}.Concat(definition.Tricks))
        {
            var icon=AbilityArtwork.Get(effect.EffectId);Assert.NotNull(icon);
            Assert.AreEqual(new Vector2(64,64),icon.rect.size);Assert.AreEqual(FilterMode.Point,icon.texture.filterMode);
            string description=GameText.AbilityDescription(effect,definition);
            Assert.IsNotEmpty(description);StringAssert.DoesNotContain("{",description);
        }
        yield return new WaitForSeconds(.35f);Capture("collection");yield return null;yield return null;
    }

    void Place(Dictionary<string,OwnedUnit> roster,string id,int x,int y,bool upgrades=false)
    {
        var unit=new OwnedUnit(ui.Match.Config.Get(id));if(upgrades){unit.Tricks[0]=true;unit.Tricks[2]=true;}
        typeof(OwnedUnit).GetProperty("Location").SetValue(unit,UnitLocation.Field);
        typeof(OwnedUnit).GetProperty("Deployment").SetValue(unit,new Vector2Int(x,y));roster.Add(id,unit);
    }
    [UnityTest] public IEnumerator ConfiguredFishRecipientsFreezeOnPauseAndWhiteBarsRenderForBothTeams()
    {
        var match=ui.Match;Assert.IsTrue(match.StartMatch("anuik",new[]{"suki","flo","rosie"},115));
        typeof(MatchController).GetMethod("ClearActors",Private).Invoke(match,null);match.Board.BuildBoard();
        var player=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Private).GetValue(match.Player);
        var bot=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Private).GetValue(match.Bot);
        player.Clear();bot.Clear();
        Place(player,"suki",2,7,true);Place(player,"anuik",0,8);Place(player,"flo",4,8);Place(player,"rosie",3,9);
        Place(bot,"suki",3,2,true);Place(bot,"anuik",0,1);Place(bot,"flo",4,1);Place(bot,"rosie",2,0);
        match.Ready();ui.RenderPage();yield return null;yield return null;
        var suki=match.Actors.Values.Where(a=>a.BaseStats.DefinitionId=="suki").ToArray();Assert.AreEqual(2,suki.Length);
        // Keep the preview formation still; real action and expiry clocks continue.
        var actors=(IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(match.Simulation);
        foreach(var clock in actors)
        {
            clock.GetType().GetField("NextMove").SetValue(clock,9999);
            clock.GetType().GetField("NextAttack").SetValue(clock,9999);
        }
        var flights=new List<CombatSimulation.FishShieldFlight>();match.FishShieldLaunched+=flights.Add;
        match.Simulation.Step(match.Config.OpeningAbilityWindowSeconds);
        foreach(var unit in suki)unit.AddEnergy(unit.BaseStats.EnergyMax);
        match.Simulation.Step(AbilityTiming.Cast(suki[0].BaseStats,"suki-super")+CombatSimulation.TickDuration);yield return null;yield return null;
        int recipients=Mathf.RoundToInt(suki[0].BaseStats.AbilityValue("suki-fish-shield","targets",3));
        int shield=Mathf.RoundToInt(suki[0].BaseStats.AbilityValue("suki-fish-shield","shield",15)+
            suki[0].BaseStats.AbilityValue("suki-extra-shield","shieldBonus",10));
        Assert.Greater(recipients,0);Assert.Greater(shield,0);
        Assert.AreEqual(recipients*suki.Length,flights.Count);
        foreach(var source in suki)
        {
            var targets=flights.Where(f=>f.Source==source).Select(f=>f.Target).ToArray();
            Assert.AreEqual(recipients,targets.Distinct().Count());
            Assert.IsTrue(targets.All(t=>t!=source&&t.Side==source.Side&&t.IsAlive));
        }
        var images=ui.CanvasRoot.GetComponentsInChildren<Image>().Where(i=>i.name=="Suki shield fish").ToArray();Assert.AreEqual(flights.Count,images.Length);
        Assert.IsTrue(images.All(i=>i.sprite!=null&&i.sprite.texture.filterMode==FilterMode.Point&&!i.raycastTarget));
        match.Simulation.Step(.2f);yield return null;yield return null;
        Capture("fish-arcs");yield return null;yield return null;
        match.Pause(true);yield return null;var positions=images.Select(i=>i.rectTransform.anchoredPosition).ToArray();
        yield return new WaitForSecondsRealtime(.2f);
        CollectionAssert.AreEqual(positions,images.Select(i=>i.rectTransform.anchoredPosition));
        match.Pause(false);match.Simulation.Step(flights[0].Duration-.2f);yield return null;yield return null;
        Assert.IsTrue(flights.All(f=>f.Arrived&&!f.Active));Assert.IsTrue(images.All(i=>i==null));
        var shielded=flights.Select(f=>f.Target).Concat(suki).Distinct().ToArray();
        Assert.IsTrue(shielded.All(a=>a.FishShield==shield));
        Assert.IsTrue(match.Actors.Values.Except(shielded).All(a=>a.FishShield==0));
        Canvas.ForceUpdateCanvases();
        foreach(var unit in suki)
        {
            var hud=ui.CanvasRoot.GetComponentsInChildren<UnitVitalsUI>().Single(h=>h.name=="health-"+unit.UnitId);
            Assert.AreEqual(Mathf.Clamp01(shield/(float)unit.BaseStats.MaxHealth),hud.ShieldFraction,.001f);
            var mesh=hud.canvasRenderer.GetMesh();
            try
            {
                float y0=hud.rectTransform.rect.yMin;
                var white=Enumerable.Range(0,mesh.vertexCount).Where(i=>mesh.vertices[i].y>=y0-.01f&&mesh.vertices[i].y<=y0+5.01f&&mesh.colors[i].r>.99f&&mesh.colors[i].g>.99f&&mesh.colors[i].b>.99f).ToArray();
                Assert.AreEqual(4,white.Length,"Exactly one white shield fill for "+unit.Side);
            }
            finally{Object.Destroy(mesh);}
        }
        Capture("white-shields-both-sides");yield return null;yield return null;
        match.Menu();yield return null;Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>().Any(i=>i.name=="Suki shield fish"));
    }

    [UnityTest] public IEnumerator EightViewsHaveDistinctHopFeetAttackCastAndDefeat()
    {
        var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/suki/Character");Assert.NotNull(character);
        var art=character.Visuals;Assert.AreEqual(UnitTravelMotion.AlternatingHop,art.TravelMotion);
        for(int d=0;d<8;d++)
        {
            var clip=art.DirectionalAnimations[d];Assert.AreNotSame(clip.HopLeft[1],clip.HopRight[1]);
            Assert.AreNotSame(clip.Idle[0],clip.Attack[1]);Assert.AreNotSame(clip.Special[0],clip.Special[1]);
            Assert.AreNotSame(clip.Idle[0],clip.Death.Last());
        }
        yield return null;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class SharedMapHudPlayTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    static readonly TableStyle[] Styles={TableStyle.Default,TableStyle.GrandCasaHotel,TableStyle.CampoReal,
        TableStyle.Naturalis,TableStyle.Aquarion,TableStyle.Geothar,TableStyle.Volkanar,TableStyle.CasinoNight};
    readonly string[] keys={"monster-pouch.language",CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey,
        "monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;string[] oldDirectories,saved;bool[] existed;bool spanish;
    LocalGameUI ui;InteractionCaptureGameView view;
    Rect safe;

    [UnitySetUp] public IEnumerator Setup()
    {
        // Retire all old profile readers before assigning isolated stores or yielding.
        foreach(var previous in Object.FindObjectsByType<LocalGameUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {previous.gameObject.SetActive(false);Object.Destroy(previous.gameObject);}
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchSharedHud",Guid.NewGuid().ToString("N"));
        oldDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,Path.Combine(root,"store-"+i));
        new LocalGuestProfileStore(Path.Combine(root,"store-0")).OpenOrCreate(true);
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        GameText.SetLanguage(true);CardTableTheme.Select(TableStyle.Default);
        view=new InteractionCaptureGameView();yield return view.Open();
        var preview=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        preview.GetMethod("Resize").Invoke(null,new object[]{"393x852"});
        for(int i=0;i<120&&(Screen.width!=393||Screen.height!=852);i++)yield return null;
        Assert.AreEqual(new Vector2Int(393,852),new Vector2Int(Screen.width,Screen.height));
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+10;
        while(LoadingArtworkScreen.IsBlocking&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        ui.enabled=false;
        safe=Screen.safeArea;
        Assert.IsTrue(ui.Match.StartMatch("popow",null,20261025));
        yield return null;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.gameObject.SetActive(false);Object.Destroy(ui.gameObject);ui=null;}
        yield return null;
        if(oldDirectories!=null)for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,oldDirectories[i]);
        GameText.SetLanguage(spanish);
        if(saved!=null)for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();
        if(view!=null){view.RestoreSelection();yield return view.WaitForRestoration();}
        if(root!=null&&Directory.Exists(root))
        {
            string parent=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchSharedHud"))+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(parent,StringComparison.OrdinalIgnoreCase));
            Directory.Delete(target,true);
        }
    }

    [UnityTest] public IEnumerator EightMapsKeepTheSameNativeHudInPreparationAndHealthComparison()
    {
        var arena=ui.Match.Mapper.GetComponent<CardTableArena>();
        var grid=arena.BoardWorldBounds;var cells=ui.Match.Mapper.CellSize;
        var team=ui.Match.Player.SelectedWhelpIds.ToArray();var monster=ui.Match.Player.Monster.Definition.Id;
        var actorPositions=ui.Match.Actors.Values.ToDictionary(a=>a,a=>a.transform.position);
        Dictionary<string,Rect> expected=null,expectedBank=null;
        foreach(var style in Styles)
        {
            yield return Present(style,true);
            Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);Assert.AreEqual(40,ui.Match.Remaining);
            Assert.AreEqual(safe,Screen.safeArea);Assert.AreEqual(new Vector2Int(393,852),new Vector2Int(Screen.width,Screen.height));
            Assert.AreEqual(grid,arena.BoardWorldBounds);Assert.AreEqual(cells,ui.Match.Mapper.CellSize);
            CollectionAssert.AreEqual(team,ui.Match.Player.SelectedWhelpIds);Assert.AreEqual(monster,ui.Match.Player.Monster.Definition.Id);
            foreach(var actor in actorPositions)Assert.AreEqual(actor.Value,actor.Key.transform.position);
            var snapshot=Snapshot("Brief tray","Enemy round HUD","Player round HUD","Round label","Clock","Battle pause");
            if(expected==null)expected=snapshot;else Same(expected,snapshot,style.ToString());
            Assert.AreEqual(new Vector2(350,202),Node("Brief drop").sizeDelta);
            Assert.AreEqual(Vector3.one,Node("Shared brief").localScale);
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Casino reference brief"));
            foreach(string name in new[]{"Bench drop","LISTO","Reroll"})Assert.IsNotNull(Node(name).GetComponent<Button>());
            ui.ToggleBankContents();yield return null;Canvas.ForceUpdateCanvases();
            var bank=Snapshot("Bank contents");if(expectedBank==null)expectedBank=bank;else Same(expectedBank,bank,style+" open bank");
            Invoke("CloseBankContents");yield return null;
        }

        // Finish only one timed round, never a complete match or a reward grant.
        ui.Match.Ready();Assert.AreEqual(MatchPhase.Combat,ui.Match.Phase);
        foreach(var actor in ui.Match.Actors.Values)actor.Initialize(actor.UnitId,actor.Side,new UnitStats(101,0,99,100,100));
        ui.Match.Simulation.Begin(ui.Match.Actors.Values.ToArray());
        ui.Match.Advance(40);
        Assert.AreEqual(MatchPhase.RoundResult,ui.Match.Phase);Assert.IsNotNull(ui.Match.Simulation.HealthSummary);
        ui.Match.Advance(ui.Match.RoundHealthStartTime+ui.Match.Config.RoundUI.HealthDuration*.8f);
        Assert.AreEqual(MatchPhase.RoundResult,ui.Match.Phase);
        expected=null;
        var referenceTotal=new Dictionary<bool,Rect>();var referenceFill=new Dictionary<bool,Vector2>();
        var referenceSprite=new Dictionary<bool,Sprite>();
        foreach(var style in Styles)
        {
            yield return Present(style,false);
            Assert.AreEqual(MatchPhase.RoundResult,ui.Match.Phase);Assert.AreEqual(safe,Screen.safeArea);
            var snapshot=Snapshot("Brief tray","Enemy round HUD","Player round HUD","Round label","Clock","Battle pause",
                "Blue health track","Red health track");
            if(expected==null)expected=snapshot;else Same(expected,snapshot,style+" health");
            var bars=ui.CanvasRoot.GetComponentsInChildren<RoundHealthBarArtwork>();Assert.AreEqual(2,bars.Length);
            foreach(var bar in bars)
            {
                Assert.AreEqual(CardTableTheme.MapId(style),bar.MapId);Assert.AreEqual("classic",bar.ArtworkId);
                Assert.AreEqual("classic-v1",bar.Fill.sprite.texture.name);
                Assert.AreEqual(new Vector2(444,52),((RectTransform)bar.transform).sizeDelta);
                if(!referenceTotal.ContainsKey(bar.IsLocal))
                {referenceTotal.Add(bar.IsLocal,bar.TotalRect);referenceFill.Add(bar.IsLocal,bar.FillCenter);referenceSprite.Add(bar.IsLocal,bar.Fill.sprite);}
                else
                {Assert.AreEqual(referenceTotal[bar.IsLocal],bar.TotalRect);Assert.AreEqual(referenceFill[bar.IsLocal],bar.FillCenter);Assert.AreSame(referenceSprite[bar.IsLocal],bar.Fill.sprite);}
                Assert.That(bar.Fill.fillAmount,Is.InRange(0,1));
                var total=bar.GetComponentsInChildren<Text>().Single();
                Assert.AreEqual(bar.IsLocal,total.color.g>total.color.r,"Keep green local health and red rival health.");
            }
            Assert.AreNotEqual(bars[0].Fill.sprite.rect,bars[1].Fill.sprite.rect,"Local and rival still use distinct colored source regions.");
            Assert.AreEqual(grid,arena.BoardWorldBounds);Assert.AreEqual(cells,ui.Match.Mapper.CellSize);
        }
        Assert.AreEqual(0,new LocalGuestProfileStore(Path.Combine(root,"store-0")).Reload().Trophies);
        Assert.IsFalse(ui.Match.IsOnline);
        var invalid=new GameObject("Unknown map artwork fixture",typeof(RectTransform));
        try
        {
            var artwork=invalid.AddComponent<RoundHealthBarArtwork>();
            Assert.Throws<InvalidOperationException>(()=>artwork.Configure("__unknown-map__",true,"Fill"),
                "An unknown map must not silently become valid through the shared artwork resolver.");
        }
        finally{Object.Destroy(invalid);}
    }

    IEnumerator Present(TableStyle style,bool preparation)
    {
        CardTableTheme.Select(style);
        typeof(MatchController).GetProperty("HostMap").SetValue(ui.Match,CardTableTheme.MapId(style));
        typeof(MatchController).GetProperty("GuestMap").SetValue(ui.Match,CardTableTheme.MapId(style));
        Invoke("SetArenaStyle",style);Invoke("ApplyViewportLayout",safe);ui.RenderPage();
        typeof(LocalGameUI).GetField("briefOpen",Hidden).SetValue(ui,preparation?1f:0f);
        Invoke("ApplyBriefPose");Invoke("UpdateRoundHud");Invoke("UpdateRerollFeedback");
        foreach(var actor in ui.Match.Actors.Values)actor.GetComponent<UnitPresentation>()?.Freeze();
        yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
        Assert.IsFalse(ui.MapTransitionActive);
    }
    void Invoke(string method,params object[] args)=>typeof(LocalGameUI).GetMethod(method,Hidden).Invoke(ui,args);
    RectTransform Node(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name==name);
    Dictionary<string,Rect> Snapshot(params string[] roots)
    {
        var result=new Dictionary<string,Rect>();
        foreach(string name in roots)Collect(Node(name),name,result);
        return result;
    }
    static void Collect(RectTransform root,string key,Dictionary<string,Rect> result)
    {
        var corners=new Vector3[4];root.GetWorldCorners(corners);
        result.Add(key,Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y));
        for(int i=0;i<root.childCount;i++)if(root.GetChild(i) is RectTransform child)Collect(child,key+"/"+i+":"+child.name,result);
    }
    static void Same(Dictionary<string,Rect> expected,Dictionary<string,Rect> actual,string context)
    {
        CollectionAssert.AreEquivalent(expected.Keys,actual.Keys,context+" changed the control hierarchy.");
        foreach(var item in expected)
        {
            var rect=actual[item.Key];
            Assert.That(rect.x,Is.EqualTo(item.Value.x).Within(.05f),context+" x: "+item.Key);
            Assert.That(rect.y,Is.EqualTo(item.Value.y).Within(.05f),context+" y: "+item.Key);
            Assert.That(rect.width,Is.EqualTo(item.Value.width).Within(.05f),context+" width: "+item.Key);
            Assert.That(rect.height,Is.EqualTo(item.Value.height).Within(.05f),context+" height: "+item.Key);
        }
    }
}

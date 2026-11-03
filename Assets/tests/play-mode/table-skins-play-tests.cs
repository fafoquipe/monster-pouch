using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class TableSkinsPlayTests
{
    const string Scene="Assets/Scenes/main-scene.unity";
    readonly string[] keys={CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey,"monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string[] saved;bool[] existed;LocalGameUI ui;LobbyAnimationDirector director;
    PropertyInfo guestOverride;string previousGuestDirectory,guestDirectory;
    DeckLibrary Decks=>(DeckLibrary)typeof(LocalGameUI).GetField("decks",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);

    [UnitySetUp] public IEnumerator LoadRealBrief()
    {
        LobbyAnimationDirector.PlaybackEnabled=false;
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();
        guestOverride=typeof(LocalGameUI).GetProperty("GuestProfileDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic);
        previousGuestDirectory=(string)guestOverride.GetValue(null);
        guestDirectory=Path.Combine(Path.GetTempPath(),"MonsterPouchTableSkins",Guid.NewGuid().ToString("N"));
        guestOverride.SetValue(null,guestDirectory);
        var store=new LocalGuestProfileStore(guestDirectory);store.OpenOrCreate(true);
        for(int i=0;i<5;i++)Assert.AreEqual(GuestTrophyResultStatus.Applied,store.RecordTrophyResult(new GuestTrophyResultRequest {
            MatchId=Guid.NewGuid().ToString("N"),OpponentId="style-fixture",SourceModeId="online",
            PlayerWins=3,OpponentWins=0,VerifiedDigest=new string('A',64)
        }).Status);
        yield return LoadBrief();
    }
    IEnumerator LoadBrief()
    {
        yield return SceneManager.LoadSceneAsync(Scene);yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);
        ui.Match.enabled=false;ui.OpenDecks();director=ui.LobbyDirector;director.enabled=false;
        yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
    }
    [UnityTearDown] public IEnumerator RestoreEverySavedPreference()
    {
        LobbyAnimationDirector.PlaybackEnabled=false;
        try
        {
            if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.gameObject.SetActive(false);Object.Destroy(ui.gameObject);}
            if(director!=null)director.enabled=true;
        }
        finally
        {
            if(saved!=null)for(int i=0;i<keys.Length;i++)
            {
                if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);
            }
            PlayerPrefs.Save();
        }
        yield return null;
        if(guestOverride!=null)guestOverride.SetValue(null,previousGuestDirectory);
        if(guestDirectory!=null&&Directory.Exists(guestDirectory))
        {
            string parent=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchTableSkins"))+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(guestDirectory);Assert.IsTrue(target.StartsWith(parent,StringComparison.OrdinalIgnoreCase));
            Directory.Delete(target,true);
        }
    }

    [UnityTest] public IEnumerator AllCombatMapsPersistWithoutChangingLobbyLoadoutFiguresOrSourceArt()
    {
        string brief=Decks.Serialize();var figures=CaptureFigures();
        var catalog=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");Assert.IsNotNull(catalog);
        var originals=ui.Match.Config.Units.ToDictionary(d=>d.Id,d=>catalog.Get(d.Id).Portrait);
        var lobby=CardTableTheme.Lobby;
        var lobbyArtwork=EnvironmentStyle.Find(CardTableTheme.MapId(lobby)).LobbyBackground;
        Assert.IsNotNull(lobbyArtwork);

        foreach(var selected in new[]{TableStyle.CampoReal,TableStyle.GrandCasaHotel,TableStyle.Default})
        {
            ui.ShowMapSkins();yield return null;
            var previousModal=ScenarioPanel();
            var scenery=CaptureArenaSprites();
            Assert.IsNotEmpty(scenery,"The real arena must provide source sprites to preserve.");
            yield return Tap(SkinButton(selected));
            Assert.IsTrue(previousModal==null,"Choosing a skin leaked the old modal.");
            AssertSingleScenarioModal();Assert.IsEmpty(ScenarioPanel().GetComponentsInChildren<LobbyPersonality>());
            Assert.AreEqual(CardTableTheme.MapId(selected),PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
            Assert.AreEqual(selected,CardTableTheme.Current);
            Assert.AreEqual(lobby,CardTableTheme.Lobby);
            var arena=Object.FindFirstObjectByType<CardTableArena>();Assert.IsNotNull(arena);
            Assert.AreEqual(selected==TableStyle.GrandCasaHotel,arena.LoungeEnabled);
            Assert.AreSame(lobbyArtwork,Background().sprite,"Selecting a combat map must preserve the lobby artwork.");
            AssertBriefUnchanged(brief,figures);AssertOriginalAssets(catalog,originals);
            foreach(var item in scenery)
            {
                Assert.AreSame(item.Sprite,item.Renderer.sprite,item.Renderer.name+": selecting a theme replaced a source scene sprite.");
                Assert.AreEqual(item.Position,item.Renderer.transform.position,item.Renderer.name);
                Assert.AreEqual(item.Scale,item.Renderer.transform.localScale,item.Renderer.name);
            }
            ui.CloseModal();yield return null;
            Assert.AreEqual(0,ScenarioPanels().Length);
            // Reload the real scene, so this cannot pass by checking only a static preference getter.
            yield return LoadBrief();
            Assert.AreEqual(selected,CardTableTheme.Current);
            Assert.AreEqual(lobby,CardTableTheme.Lobby);
            Assert.AreSame(lobbyArtwork,Background().sprite,"The lobby was lost after loading the scene.");
            Assert.AreEqual(selected==TableStyle.GrandCasaHotel,Object.FindFirstObjectByType<CardTableArena>().LoungeEnabled);
            AssertBriefUnchanged(brief,figures);AssertOriginalAssets(catalog,originals);
            ui.OpenHome();yield return new WaitForSecondsRealtime(.35f);
            Assert.AreSame(lobbyArtwork,Background().sprite,"Home must preserve the selected lobby's current artwork.");
            ui.OpenDecks();yield return new WaitForSecondsRealtime(.35f);AssertBriefUnchanged(brief,figures);
        }
    }

    [UnityTest] public IEnumerator SelectorButtonsAreReachableAtMobileSizeAndNavigationKeepsPortraitsStatic()
    {
        string brief=Decks.Serialize();var figures=CaptureFigures();
        int portraits=ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Length;
        Assert.Greater(portraits,0);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>()[0].PlayGesture());
        for(int cycle=0;cycle<4;cycle++)
        {
            ui.ShowMapSkins();yield return null;director.Advance(.01f);
            AssertSingleScenarioModal();Assert.AreEqual(0,director.ActiveCount,"A covered Brief kept an animation slot.");
            AssertMobileTargets(ScenarioPanel());
            Assert.AreEqual(8,ScenarioPanel().GetComponentsInChildren<Button>().Length,"Three styles, two collection tabs, Lobby/Map and Close must remain reachable.");
            var old=ScenarioPanel();yield return Tap(SkinButton(cycle%2==0?TableStyle.GrandCasaHotel:TableStyle.Default));
            Assert.IsTrue(old==null);AssertSingleScenarioModal();AssertMobileTargets(ScenarioPanel());
            AssertBriefUnchanged(brief,figures);
            var back=ScenarioPanel().GetComponentsInChildren<Button>().Single(b=>b.name=="×");
            old=ScenarioPanel();yield return Tap(back);
            Assert.IsTrue(old==null);Assert.AreEqual(0,ScenarioPanels().Length);
            Assert.AreEqual("decks",ui.MenuPage,"Styles should return directly to the Brief.");
            Assert.AreEqual(portraits,ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Length);
            Assert.AreEqual(0,director.ActiveCount);
            AssertBriefUnchanged(brief,figures);
        }
        Assert.AreEqual(portraits,ui.CanvasRoot.GetComponentsInChildren<LobbyFrameGraphic>(true).Length,
            "Rebuilding the themed Brief leaked a disabled drawing.");
    }

    sealed class FigureSnapshot
    {
        public string Id;public Sprite Sprite;public Texture Texture;public Vector2 Size;public Vector3 LocalFoot;
    }
    FigureSnapshot[] CaptureFigures()
    {
        Canvas.ForceUpdateCanvases();
        return ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Select(p=>{
            var d=p.GetComponentInChildren<LobbyFrameGraphic>();Assert.IsNotNull(d,p.UnitId);
            return new FigureSnapshot{Id=p.UnitId,Sprite=d.CurrentSprite,Texture=d.mainTexture,Size=d.rectTransform.rect.size,
                LocalFoot=ui.CanvasRoot.InverseTransformPoint(d.transform.TransformPoint(d.FootAnchor))};
        }).OrderBy(p=>p.Id).ToArray();
    }
    void AssertBriefUnchanged(string brief,FigureSnapshot[] expected)
    {
        Assert.AreEqual("decks",ui.MenuPage);Assert.AreEqual(brief,Decks.Serialize(),"A scenery change modified the loadout.");
        for(int i=1;i<keys.Length;i++)
        {Assert.AreEqual(existed[i],PlayerPrefs.HasKey(keys[i]),keys[i]);Assert.AreEqual(saved[i],PlayerPrefs.GetString(keys[i]),keys[i]);}
        var actual=CaptureFigures();Assert.AreEqual(expected.Length,actual.Length);
        for(int i=0;i<actual.Length;i++)
        {
            Assert.AreEqual(expected[i].Id,actual[i].Id);
            Assert.AreEqual(expected[i].Sprite.name,actual[i].Sprite.name,actual[i].Id);
            Assert.AreEqual(expected[i].Sprite.rect,actual[i].Sprite.rect,actual[i].Id);
            Assert.AreEqual(expected[i].Sprite.pivot,actual[i].Sprite.pivot,actual[i].Id);
            Assert.AreSame(expected[i].Texture,actual[i].Texture,actual[i].Id);
            Assert.AreEqual(expected[i].Size,actual[i].Size,actual[i].Id+": the theme resized a character.");
            Assert.Less(Vector3.Distance(expected[i].LocalFoot,actual[i].LocalFoot),.01f,actual[i].Id+": the theme moved a character's feet.");
        }
    }
    static void AssertOriginalAssets(UnitArtCatalog catalog,Dictionary<string,Sprite> originals)
    {
        foreach(var pair in originals)Assert.AreSame(pair.Value,catalog.Get(pair.Key).Portrait,pair.Key+": original portrait was replaced.");
    }
    sealed class ArenaSnapshot
    {public SpriteRenderer Renderer;public Sprite Sprite;public Vector3 Position,Scale;}
    ArenaSnapshot[] CaptureArenaSprites()=>Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Where(r=>r.name=="table-surface"||r.name=="casino-table-path"||r.name=="table-frame")
        .Select(r=>new ArenaSnapshot{Renderer=r,Sprite=r.sprite,Position=r.transform.position,Scale=r.transform.localScale}).ToArray();
    RectTransform[] ScenarioPanels()=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="Scenario skins").ToArray();
    RectTransform ScenarioPanel()=>ScenarioPanels().Single();
    void AssertSingleScenarioModal()=>Assert.AreEqual(1,ScenarioPanels().Length,"A hidden or active duplicate scenario modal remains.");
    Button SkinButton(TableStyle style)=>ScenarioPanel().GetComponentsInChildren<Button>()
        .Single(b=>b.name=="use-style-"+style);
    Image Background()=>ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Square scenery");
    void AssertMobileTargets(RectTransform panel)
    {
        Canvas.ForceUpdateCanvases();var corners=new Vector3[4];var safe=Screen.safeArea;
        foreach(var button in panel.GetComponentsInChildren<Button>())
        {
            Assert.IsTrue(button.interactable,button.name);
            var rect=(RectTransform)button.transform;
            rect.GetWorldCorners(corners);
            var padding=button.GetComponent<Image>().raycastPadding;
            float scaleX=(corners[3].x-corners[0].x)/rect.rect.width;
            float scaleY=(corners[1].y-corners[0].y)/rect.rect.height;
            Assert.GreaterOrEqual((rect.rect.width-padding.x-padding.z)*scaleX,43.99f,button.name+": actual touch target too narrow.");
            Assert.GreaterOrEqual((rect.rect.height-padding.y-padding.w)*scaleY,43.99f,button.name+": actual touch target too short.");
            foreach(var corner in corners)
            {
                var point=RectTransformUtility.WorldToScreenPoint(null,corner);
                Assert.That(point.x,Is.InRange(safe.xMin-.1f,safe.xMax+.1f),button.name);
                Assert.That(point.y,Is.InRange(safe.yMin-.1f,safe.yMax+.1f),button.name);
            }
        }
    }
    IEnumerator Tap(Button button)
    {
        yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();Assert.IsTrue(button.interactable);
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),
            button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsNotEmpty(hits);var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.AreSame(button.gameObject,handler,button.name+": the real pointer target is blocked.");
        ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
    }
}

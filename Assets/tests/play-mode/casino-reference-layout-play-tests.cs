using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class CasinoReferenceLayoutPlayTests
{
    LocalGameUI ui;
    CardTableArena arena;
    static readonly PropertyInfo[] Stores=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    static readonly string[] PreferenceKeys={CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey,
        "monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string[] previousStores,savedPreferences;bool[] existed;string qaRoot;
    static readonly string[] Chips={"chip-stack","chip-blue-stack-small","chip-green-stack-small",
        "chip-red-stack-tall","chip-white-stack-tall","chip-blue-cluster","chip-mixed-pile-2","chip-red-cluster","chip-green-stack-tall"};

    [UnitySetUp] public IEnumerator Setup()
    {
        foreach(var old in Object.FindObjectsByType<LocalGameUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {old.gameObject.SetActive(false);Object.Destroy(old.gameObject);}
        previousStores=Stores.Select(p=>(string)p.GetValue(null)).ToArray();
        savedPreferences=PreferenceKeys.Select(PlayerPrefs.GetString).ToArray();
        existed=PreferenceKeys.Select(PlayerPrefs.HasKey).ToArray();
        qaRoot=Path.Combine(Path.GetTempPath(),"EronyaCasinoLayoutQa",Guid.NewGuid().ToString("N"));
        for(int i=0;i<Stores.Length;i++)Stores[i].SetValue(null,Path.Combine(qaRoot,"store-"+i));
        var store=new LocalGuestProfileStore(Path.Combine(qaRoot,"store-0"));store.OpenOrCreate(true);
        // Access fixture belongs to the disposable account; never a production reward.
        for(int i=0;i<20;i++)store.RecordTrophyResult(new GuestTrophyResultRequest{
            MatchId=Guid.NewGuid().ToString("N"),OpponentId="casino-layout-fixture",SourceModeId="online",
            VerifiedDigest=new string('A',64),PlayerWins=3,OpponentWins=0});
        foreach(string key in PreferenceKeys)PlayerPrefs.DeleteKey(key);
        CardTableTheme.Select(TableStyle.GrandCasaHotel);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+15;
        while(LoadingArtworkScreen.IsBlocking&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        arena=ui.Match.Mapper.GetComponent<CardTableArena>();
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"stein"},31024));
        yield return null;yield return new WaitForSecondsRealtime(.4f);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null)
        {
            ui.CloseModal();ui.Match.Menu();ui.Match.enabled=false;
            ui.gameObject.SetActive(false);Object.Destroy(ui.gameObject);ui=null;
        }
        yield return null;
        if(previousStores!=null)for(int i=0;i<Stores.Length;i++)Stores[i].SetValue(null,previousStores[i]);
        if(savedPreferences!=null)for(int i=0;i<PreferenceKeys.Length;i++)
            if(existed[i])PlayerPrefs.SetString(PreferenceKeys[i],savedPreferences[i]);else PlayerPrefs.DeleteKey(PreferenceKeys[i]);
        PlayerPrefs.Save(); // QA account files are retained for inspection; no personal store was opened.
    }

    [UnityTest] public IEnumerator WalnutCasinoKeepsItsIdentityAndUsesTheCommonCaseSize()
    {
        Assert.IsTrue(arena.LoungeEnabled);
        Assert.That(arena.FramePortraitBounds.center.x,Is.EqualTo(270).Within(1));
        Assert.That(arena.FramePortraitBounds.width/arena.FramePortraitBounds.height,Is.EqualTo(344f*CardTableArena.HorizontalExpansion/474f).Within(.005f));
        Assert.That(ui.Match.Mapper.CellSize.x,Is.EqualTo(arena.OriginalCellSize.x*CardTableArena.HorizontalExpansion).Within(.00001f));
        Assert.AreEqual(arena.OriginalCellSize.y,ui.Match.Mapper.CellSize.y);
        foreach(string name in Chips)
        {
            var r=Sprite(name);Assert.IsFalse(r.enabled,name+" must not overlap the supplied walnut original.");
        }
        var scenery=arena.GetComponent<NationArenaScenery>();
        Assert.IsNotNull(scenery);Assert.IsTrue(scenery.IsVisible);Assert.AreEqual("lounge",scenery.Artwork.NationId);
        Assert.IsTrue(scenery.UsesPaintedGrid);
        Assert.That(arena.PortraitPoint(Sprite("chip-stack").transform.position).x,Is.GreaterThan(arena.BoardPortraitBounds.xMax));
        Assert.That(VisiblePortrait(Sprite("chip-mixed-pile-2")).yMin,Is.GreaterThan(arena.BoardPortraitBounds.yMax));
        Assert.AreEqual(new Vector3(-1.12f,11.54f,0),Sprite("shuffler-cart").transform.position);
        AssertDiceHidden();
        var drop=Find("Brief drop");
        Assert.AreEqual("Shared brief",drop.parent.name);
        Assert.That(drop.rect.width,Is.EqualTo(350).Within(.01f));
        Assert.AreEqual(Vector3.one,drop.parent.localScale,"Map decoration must not scale the common case controls.");
        Assert.That(drop.parent.localScale.x,Is.EqualTo(drop.parent.localScale.y).Within(.00001f),"Do not distort portraits or case artwork.");
        Assert.That(VisiblePortrait(Sprite("table-frame")).yMax,Is.LessThan(CardTableArena.BriefTop));
        Assert.IsFalse(Find("Bench drop").IsChildOf(drop.parent),"Keep the independent bank controls intact.");
        yield return null;
    }

    [UnityTest] public IEnumerator CasinoNightIsIndependentAndPreservesOriginalAccessAndCaseGeometry()
    {
        Rect originalCase=ScreenRect(Find("Brief drop")),originalBoard=arena.BoardWorldBounds;
        var originalCells=ui.Match.Board.GetAllCells().Select(c=>ui.Match.Mapper.GetWorldPosition(c)).ToArray();
        Assert.AreEqual(ui.CanSelectTrophyBoard(TableStyle.GrandCasaHotel),ui.CanSelectTrophyBoard(TableStyle.CasinoNight));
        CardTableTheme.Select(TableStyle.CasinoNight);arena.SetTheme(TableStyle.CasinoNight);
        yield return null;Canvas.ForceUpdateCanvases();
        var scenery=arena.GetComponent<NationArenaScenery>();
        Assert.IsNotNull(scenery);Assert.IsTrue(scenery.IsVisible);Assert.IsTrue(scenery.UsesContinuousArtwork);
        Assert.AreEqual("casino-night",scenery.Artwork.NationId);Assert.AreEqual(60,scenery.TileCount);
        Assert.AreEqual(originalCase,ScreenRect(Find("Brief drop")));Assert.AreEqual(originalBoard,arena.BoardWorldBounds);
        CollectionAssert.AreEqual(originalCells,ui.Match.Board.GetAllCells().Select(c=>ui.Match.Mapper.GetWorldPosition(c)).ToArray());
        foreach(string name in Chips)Assert.IsFalse(Sprite(name).enabled,"Original props must not overlap Casino Noche.");
        Assert.IsEmpty(scenery.GetComponentsInChildren<Collider2D>());Assert.IsEmpty(scenery.GetComponentsInChildren<Collider>());
        CardTableTheme.Select(TableStyle.GrandCasaHotel);arena.SetTheme(TableStyle.GrandCasaHotel);yield return null;
        Assert.AreEqual("lounge",CardTableTheme.MapId(CardTableTheme.Current));
        Assert.AreEqual("lounge",scenery.Artwork.NationId);Assert.IsTrue(scenery.IsVisible);
        foreach(string name in Chips)Assert.IsFalse(Sprite(name).enabled,"Old props must not overlap the walnut original.");
    }

    [Test] public void AireolaReusesRoyalAccessWithoutCreatingAnotherTrophyStage()
    {
        var threshold=typeof(LocalGameUI).GetMethod("TrophyBoardThreshold",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.AreEqual(600L,(long?)threshold.Invoke(ui,new object[]{TableStyle.CampoReal}));
        Assert.AreEqual(threshold.Invoke(ui,new object[]{TableStyle.CampoReal}),threshold.Invoke(ui,new object[]{TableStyle.Aireola}));
        Assert.AreEqual(ui.CanSelectTrophyBoard(TableStyle.CampoReal),ui.CanSelectTrophyBoard(TableStyle.Aireola));
        Assert.AreEqual(5,GuestTrophyLadder.CreatePolicy().Stages.Count());
    }

    [UnityTest] public IEnumerator MobileAndDesktopKeepRegisteredCellsCommonCaseAndStableSourceGeometry()
    {
        var layouts=new[]{new Vector2(360,640),new Vector2(393,852),new Vector2(1080,2400),
            new Vector2(768,1024),new Vector2(1366,768),new Vector2(1920,1080)};
        var original=Chips.ToDictionary(n=>n,n=>Sprite(n).bounds);
        var frame=arena.FramePortraitBounds;var grid=arena.BoardPortraitBounds;
        foreach(var size in layouts)
        {
            float fit=Mathf.Min(Screen.width/size.x,Screen.height/size.y)*.9f;
            var extent=size*fit;
            var safe=new Rect((new Vector2(Screen.width,Screen.height)-extent)*.5f,extent);
            Apply(safe);yield return null;Canvas.ForceUpdateCanvases();
            Assert.AreEqual(frame,arena.FramePortraitBounds);Assert.AreEqual(grid,arena.BoardPortraitBounds);
            foreach(string chip in Chips)Assert.AreEqual(original[chip],Sprite(chip).bounds,chip+" moved during "+size);
            var drop=ScreenRect(Find("Brief drop"));
            Assert.GreaterOrEqual(drop.xMin,safe.xMin-.1f);Assert.LessOrEqual(drop.xMax,safe.xMax+.1f);
            Assert.GreaterOrEqual(drop.yMin,safe.yMin-.1f);Assert.LessOrEqual(drop.yMax,safe.yMax+.1f);
            for(int y=0;y<BoardManager.Height;y++)for(int x=0;x<BoardManager.Width;x++)
            {
                var cell=ui.Match.Board.GetCell(x,y);var world=ui.Match.Mapper.GetWorldPosition(cell);
                var screen=ui.GameCamera.WorldToScreenPoint(world);
                Assert.IsTrue(safe.Contains(screen));
                Assert.IsTrue(ui.Match.Mapper.TryGetCell(ui.GameCamera.ScreenToWorldPoint(screen),out var picked));
                Assert.AreSame(cell,picked);
                var hits=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
                Assert.IsFalse(hits.Any(h=>h.gameObject.transform.IsChildOf(Find("Shared brief"))),"The suitcase blocks "+cell.Coordinates);
            }
            var sceneryCamera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c=>c.name=="Full screen scenery");
            var anchor=Sprite("chip-stack").transform.position;
            Assert.Less(Vector2.Distance(ui.GameCamera.WorldToScreenPoint(anchor),sceneryCamera.WorldToScreenPoint(anchor)),1.1f);
            AssertDiceHidden();
        }
    }

    [UnityTest] public IEnumerator ThemeCyclesKeepOneBoardWidthWithoutAccumulationOrDiceShadows()
    {
        var positions=Chips.ToDictionary(n=>n,n=>Sprite(n).transform.position);
        var frame=arena.FramePortraitBounds;var originalGrid=arena.BoardPortraitBounds;
        for(int cycle=0;cycle<3;cycle++)
        {
            foreach(var style in new[]{TableStyle.Default,TableStyle.CampoReal,TableStyle.GrandCasaHotel})
            {
                arena.SetTheme(style);arena.SetTheme(style);
                Assert.That(ui.Match.Mapper.CellSize.x,Is.EqualTo(arena.OriginalCellSize.x*CardTableArena.HorizontalExpansion).Within(.00001f));
                Assert.AreEqual(arena.OriginalCellSize.y,ui.Match.Mapper.CellSize.y);
                AssertDiceHidden();
                foreach(string chip in Chips)Assert.AreEqual(positions[chip],Sprite(chip).transform.position);
            }
            Assert.AreEqual(frame,arena.FramePortraitBounds);Assert.AreEqual(originalGrid,arena.BoardPortraitBounds);
        }
        yield return null;
    }
    [UnityTest] public IEnumerator ThemeChangesAndNewMatchesKeepCellHitsAndActorFootAnchorsRegistered()
    {
        var grid=arena.BoardWorldBounds;
        var cellSize=ui.Match.Mapper.CellSize;
        var cellCentres=new Vector3[BoardManager.Width,BoardManager.Height];
        for(int y=0;y<BoardManager.Height;y++)for(int x=0;x<BoardManager.Width;x++)
            cellCentres[x,y]=ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(x,y));
        var initialActors=ui.Match.Actors.Values.ToArray();
        Assert.IsNotEmpty(initialActors);
        var initialFeet=initialActors.ToDictionary(a=>a,a=>a.transform.position);
        // Compare the stable configured size, never breathing/squash animation frames.
        var scaleField=typeof(UnitPresentation).GetField("baseScale",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.IsNotNull(scaleField);
        var initialScales=initialActors.ToDictionary(a=>a,a=>(float)scaleField.GetValue(a.GetComponent<UnitPresentation>()));
        float monsterScale=(float)scaleField.GetValue(ui.Match.Actors[ui.Match.Player.Monster].GetComponent<UnitPresentation>());
        var styles=new[]{TableStyle.Default,TableStyle.CampoReal,TableStyle.GrandCasaHotel};
        for(int cycle=0;cycle<3;cycle++)foreach(var style in styles)
        {
            arena.SetTheme(style);arena.SetTheme(style);yield return null;
            Assert.AreEqual(grid,arena.BoardWorldBounds);Assert.AreEqual(cellSize,ui.Match.Mapper.CellSize);
            AssertCellsAndActorsRegistered(cellCentres);
            foreach(var actor in initialActors)
            {
                Assert.AreEqual(initialFeet[actor],actor.transform.position,"Changing cosmetics moved an existing actor.");
                Assert.AreEqual(initialScales[actor],(float)scaleField.GetValue(actor.GetComponent<UnitPresentation>()));
            }
        }
        // Exercise the real selector between matches so newly configured actors
        // cannot inherit a narrower mapper from the previous cosmetic theme.
        foreach(var style in styles)
        {
            ui.Match.Menu();ui.SelectTableSkin(style);
            yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);ui.CloseModal();
            Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"stein"},31024,CardTableTheme.MapId(style)));
            yield return null;
            Assert.AreEqual(grid,arena.BoardWorldBounds);Assert.AreEqual(cellSize,ui.Match.Mapper.CellSize);
            AssertCellsAndActorsRegistered(cellCentres);
            Assert.That((float)scaleField.GetValue(ui.Match.Actors[ui.Match.Player.Monster].GetComponent<UnitPresentation>()),
                Is.EqualTo(monsterScale).Within(.00001f),"The same character changes size between maps.");
        }
    }
    void AssertCellsAndActorsRegistered(Vector3[,] expected)
    {
        for(int y=0;y<BoardManager.Height;y++)for(int x=0;x<BoardManager.Width;x++)
        {
            var cell=ui.Match.Board.GetCell(x,y);
            Assert.Less(Vector3.Distance(expected[x,y],ui.Match.Mapper.GetWorldPosition(cell)),.00001f);
            // Include points near cell edges, not only their centres.
            foreach(float dx in new[]{-.49f,0,.49f})foreach(float dy in new[]{-.49f,0,.49f})
            {
                var world=expected[x,y]+new Vector3(dx*ui.Match.Mapper.CellSize.x,dy*ui.Match.Mapper.CellSize.y,0);
                var screen=ui.GameCamera.WorldToScreenPoint(world);
                Assert.IsTrue(ui.Match.Mapper.TryGetCell(ui.GameCamera.ScreenToWorldPoint(screen),out var hit));
                Assert.AreSame(cell,hit,"Cosmetic changes shifted a cell's touch boundary.");
            }
        }
        foreach(var actor in ui.Match.Actors.Values)
        {
            Assert.IsNotNull(actor.CurrentCell);
            Assert.Less(Vector3.Distance(actor.transform.position,expected[actor.CurrentCell.X,actor.CurrentCell.Y]),.00001f,
                "The board moved under an actor's foot anchor.");
        }
    }
    void AssertDiceHidden()
    {
        foreach(string name in new[]{"die-red","die-white","die-red-shadow","die-white-shadow"})
        {
            var die=Sprite(name);Assert.IsNotNull(die.sprite,"Keep the source resource: "+name);Assert.IsFalse(die.enabled,name);
        }
        var root=arena.transform.Find("Modular arena scenery");
        if(root==null)return;
        foreach(var shadow in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Projected prop shadows")))
            foreach(string name in new[]{"die-red","die-white"})
                Assert.AreNotSame(Sprite(name).sprite.texture,shadow.sharedMaterial.mainTexture,"Hidden dice still cast shadows.");
    }
    SpriteRenderer Sprite(string name)=>Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Single(s=>s.name==name&&s.gameObject.scene==arena.gameObject.scene);
    Rect VisiblePortrait(SpriteRenderer r)
    {
        var w=arena.VisibleWorldBounds(r);var a=arena.PortraitPoint(new Vector3(w.xMin,w.yMax));var b=arena.PortraitPoint(new Vector3(w.xMax,w.yMin));
        return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
    }
    RectTransform Find(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name==name);
    void Apply(Rect safe)=>typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,new object[]{safe});
    static Rect ScreenRect(RectTransform r)
    {
        var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);
    }
}

// Shared disposable account for map UI tests. Never opens the user's profile stores.
internal sealed class EronyaMapTestAccount : IDisposable
{
    static readonly PropertyInfo[] Stores = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(n => typeof(LocalGameUI).GetProperty(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    static readonly string[] Keys = { CardTableTheme.PreferenceKey, CardTableTheme.LobbyPreferenceKey,
        "monster-pouch.progress.v1", "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps" };
    readonly string[] directories, values;
    readonly bool[] present;
    public EronyaMapTestAccount(int wins=20)
    {
        RetireReaders();
        directories = Stores.Select(p => (string)p.GetValue(null)).ToArray();
        values = Keys.Select(PlayerPrefs.GetString).ToArray(); present = Keys.Select(PlayerPrefs.HasKey).ToArray();
        string root = Path.Combine(Path.GetTempPath(), "EronyaMapUiQa", Guid.NewGuid().ToString("N"));
        for (int i = 0; i < Stores.Length; i++) Stores[i].SetValue(null, Path.Combine(root, "store-" + i));
        var store = new LocalGuestProfileStore(Path.Combine(root, "store-0")); store.OpenOrCreate(true);
        // 600-trophy access fixture for the existing Royal board, in this temporary account only.
        for (int i = 0; i < wins; i++) store.RecordTrophyResult(new GuestTrophyResultRequest {
            MatchId = Guid.NewGuid().ToString("N"), OpponentId = "map-ui-fixture", SourceModeId = "online",
            VerifiedDigest = new string('A', 64), PlayerWins = 3, OpponentWins = 0 });
        foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
    }
    static void RetireReaders()
    {
        foreach (var old in Object.FindObjectsByType<LocalGameUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { old.gameObject.SetActive(false); Object.Destroy(old.gameObject); }
    }
    public void Dispose()
    {
        RetireReaders();
        for (int i = 0; i < Stores.Length; i++) Stores[i].SetValue(null, directories[i]);
        for (int i = 0; i < Keys.Length; i++)
            if (present[i]) PlayerPrefs.SetString(Keys[i], values[i]); else PlayerPrefs.DeleteKey(Keys[i]);
        PlayerPrefs.Save();
    }
}

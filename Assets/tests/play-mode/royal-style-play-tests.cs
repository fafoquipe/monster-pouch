using System.Collections;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class RoyalStylePlayTests
{
    EronyaMapTestAccount mapAccount;
    LocalGameUI ui;
    string saved,savedLobby;bool existed,hadLobby;
    [UnitySetUp] public IEnumerator Load()
    {
        // This Royal-only account earns 1500 trophies through 50 ordinary wins (current policy: +30).
        mapAccount=new EronyaMapTestAccount(wins:50);
        existed=PlayerPrefs.HasKey(CardTableTheme.PreferenceKey);saved=PlayerPrefs.GetString(CardTableTheme.PreferenceKey);
        hadLobby=PlayerPrefs.HasKey(CardTableTheme.LobbyPreferenceKey);savedLobby=PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey);
        CardTableTheme.SelectLobby(TableStyle.GrandCasaHotel);
        CardTableTheme.Select(TableStyle.GrandCasaHotel);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Restore()
    {
        if(existed)PlayerPrefs.SetString(CardTableTheme.PreferenceKey,saved);else PlayerPrefs.DeleteKey(CardTableTheme.PreferenceKey);
        if(hadLobby)PlayerPrefs.SetString(CardTableTheme.LobbyPreferenceKey,savedLobby);else PlayerPrefs.DeleteKey(CardTableTheme.LobbyPreferenceKey);
        PlayerPrefs.Save();
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        yield return null;

        mapAccount?.Dispose();
    }
    [UnityTest] public IEnumerator RoyalSelectionPersistsWithItsOwnPanoramaAndTheSharedPlayableGrid()
    {
        var arena=Object.FindFirstObjectByType<CardTableArena>();var hotelBounds=arena.BoardPortraitBounds;
        Vector2 hotelCellSize=ui.Match.Mapper.CellSize;
        var bounds=hotelBounds;
        Vector2 cellSize=hotelCellSize;
        var original=GameObject.Find("grand-cas-hotel").GetComponentsInChildren<SpriteRenderer>(true);
        var sprites=original.Select(r=>r.sprite).ToArray();var enabled=original.Select(r=>r.enabled).ToArray();
        ui.OpenHome();yield return new WaitForSecondsRealtime(.35f);
        var lobbyHome=Background().sprite;Assert.IsNotNull(lobbyHome);
        ui.OpenDecks();yield return new WaitForSecondsRealtime(.35f);
        var lobbyDeck=Background().sprite;Assert.IsNotNull(lobbyDeck);
        ui.ShowMapSkins();yield return null;
        var choose=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-CampoReal");
        long royalThreshold=MonsterPouch.Gameplay.Profiles.GuestTrophyLadder.CreatePolicy().Stages
            .Single(stage=>stage.BoardId=="royal").MinimumTrophies;
        Assert.That(ui.AccountTrophies,Is.GreaterThanOrEqualTo(royalThreshold),"The isolated Royal fixture must earn access under the current policy.");
        Assert.IsTrue(ui.CanSelectTrophyBoard(TableStyle.CampoReal),"Royal must be unlocked through normal progression before exercising selection.");
        Assert.IsTrue(choose.interactable,"Royal's real gallery button must be enabled before the click.");
        choose.onClick.Invoke();yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
        Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Current);Assert.AreEqual("royal",PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
        Assert.AreSame(lobbyDeck,Background().sprite,"Selecting the match map must preserve the local deck background.");
        yield return Shot("campo-real-estilos");
        ui.CloseModal();yield return Shot("campo-real-brief");
        ui.OpenHome();yield return new WaitForSecondsRealtime(.35f);
        Assert.AreSame(lobbyHome,Background().sprite);yield return Shot("campo-real-inicio");
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"stein"},1905));ui.RenderPage();yield return new WaitForSecondsRealtime(.4f);
        var scenery=Object.FindFirstObjectByType<NationArenaScenery>();Assert.IsTrue(scenery.IsVisible);
        Assert.AreEqual("royal",scenery.Artwork.NationId);Assert.IsTrue(scenery.UsesContinuousArtwork);
        Assert.Less(Vector2.Distance(bounds.center,scenery.TilePortraitBounds.center),.001f);
        Assert.Less(Vector2.Distance(bounds.size,scenery.TilePortraitBounds.size),.001f);
        Assert.AreEqual(arena.BoardPortraitBounds,scenery.TilePortraitBounds);
        Assert.AreEqual(arena.FramePortraitBounds,scenery.FramePortraitBounds,"Campo Real must match the hotel's border thickness on every side.");
        var panorama=scenery.GetComponentsInChildren<MeshFilter>().Single(r=>r.name=="Nation continuous environment").sharedMesh;
        Assert.AreEqual(165,panorama.vertexCount,"All painted intersections and four corners belong to one continuous mesh.");
        Assert.IsNull(GameObject.Find("Royal carved frame"),"The obsolete wooden frame must not overlap the painted stone.");
        Assert.AreEqual(cellSize,ui.Match.Mapper.CellSize);
        NationArenaSceneryPlayTests.AssertOpaqueGrid(scenery,arena,ui.Match.Mapper);
        Assert.IsTrue(original.All(r=>!r.enabled),"Hotel decorations overlap the royal battlefield.");
        var modular=Object.FindFirstObjectByType<ModularArenaScenery>();Assert.IsFalse(modular.IsVisible);
        var surroundings=scenery.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="Nation outer atmosphere");
        foreach(var screen in new[]{Vector3.zero,new Vector3(Screen.width,Screen.height)})
        {
            var world=ui.GameCamera.ScreenToWorldPoint(screen);world.z=0;
            Assert.IsTrue(surroundings.bounds.Contains(world),"Royal scenery does not cover the full physical screen.");
        }
        for(int y=5;y<10;y++)for(int x=0;x<6;x++)Assert.IsTrue(ui.Match.Place("bugaloo",ui.Match.Board.GetCell(x,y)),ui.Match.Notice);
        ui.Match.Place("bugaloo",ui.Match.Board.GetCell(2,6));
        yield return Shot("campo-real-partida");
        ui.Match.Menu();ui.SelectTableSkin(TableStyle.GrandCasaHotel);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
        Assert.IsTrue(scenery.IsVisible);Assert.AreEqual("lounge",scenery.Artwork.NationId);Assert.IsTrue(arena.LoungeEnabled);
        Assert.AreEqual(hotelBounds,arena.BoardPortraitBounds);Assert.AreEqual(hotelCellSize,ui.Match.Mapper.CellSize);
        for(int i=0;i<original.Length;i++){Assert.AreSame(sprites[i],original[i].sprite);Assert.AreEqual(enabled[i],original[i].enabled);}
        ui.OpenDecks();yield return new WaitForSecondsRealtime(.35f);
        Assert.AreSame(lobbyDeck,Background().sprite);yield return Shot("grand-casa-hotel-brief");
        ui.SelectTableSkin(TableStyle.CampoReal);
        // Selection is persisted inside the covered transition callback, not on request.
        float royalTransitionDeadline=Time.realtimeSinceStartup+10f;
        while(ui.MapTransitionActive&&Time.realtimeSinceStartup<royalTransitionDeadline)yield return null;
        Assert.IsFalse(ui.MapTransitionActive,"Royal's transition must finish before reloading the scene.");
        Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Current,"Royal must be committed before scene reload.");
        Assert.AreEqual("royal",PlayerPrefs.GetString(CardTableTheme.PreferenceKey),"Royal's preference must be saved before scene reload.");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Current);
        Assert.IsTrue(Object.FindFirstObjectByType<NationArenaScenery>().IsVisible);
        Assert.AreEqual("royal",Object.FindFirstObjectByType<NationArenaScenery>().Artwork.NationId);
        Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Lobby);
        ui.OpenHome();yield return new WaitForSecondsRealtime(.35f);
        Assert.AreSame(lobbyHome,Background().sprite);
    }
    Image Background()=>ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Square scenery"||i.name=="Painted lobby artwork");
    static IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.25f);yield return Application.isBatchMode ? null : new WaitForEndOfFrame();
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/royal-style-validation"));
        Directory.CreateDirectory(folder);ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        yield return null;
    }
}

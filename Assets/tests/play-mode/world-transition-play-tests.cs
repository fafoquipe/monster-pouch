using System.Collections;
using System.Linq;
using System.IO;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class WorldTransitionPlayTests
{
    EronyaMapTestAccount mapAccount;
    LocalGameUI ui;string saved;bool existed;
    [UnitySetUp] public IEnumerator Load()
    {
        mapAccount=new EronyaMapTestAccount();
        existed=PlayerPrefs.HasKey(CardTableTheme.PreferenceKey);saved=PlayerPrefs.GetString(CardTableTheme.PreferenceKey);
        CardTableTheme.Select(TableStyle.Default);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Restore()
    {
        Time.timeScale=1;
        var transition=Object.FindFirstObjectByType<MapIrisTransition>();if(transition!=null)transition.Cancel();
        if(existed)PlayerPrefs.SetString(CardTableTheme.PreferenceKey,saved);else PlayerPrefs.DeleteKey(CardTableTheme.PreferenceKey);
        PlayerPrefs.Save();yield return null;
        mapAccount?.Dispose();
    }
    Image Background()=>ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Square scenery");
    [UnityTest] public IEnumerator MapsChangeUnderBlackAndHomeArtworkSurvivesEveryPageRebuild()
    {
        var arena=Object.FindFirstObjectByType<CardTableArena>();var grid=arena.BoardPortraitBounds;
        var lobby=CardTableTheme.Lobby;
        var artwork=CardTableTheme.NationArtwork(lobby)?.LobbyBackground??MonsterPouch.Gameplay.Presentation.EnvironmentStyle.Find(CardTableTheme.MapId(lobby))?.LobbyBackground;
        foreach(var style in new[]{TableStyle.GrandCasaHotel,TableStyle.CampoReal,TableStyle.Default})
        {
            bool before=arena.LoungeEnabled;
            ui.SelectTableSkin(style);Assert.IsTrue(ui.MapTransitionActive);Assert.AreEqual(before,arena.LoungeEnabled);
            Assert.IsFalse(ui.BeginBoardPointer(new Vector2(Screen.width/2,Screen.height/2)));
            ui.RenderPage(); // Online HUD and resize rebuilds must not remove the overlay.
            yield return new WaitForSecondsRealtime(MapIrisTransition.CloseSeconds+.18f);
            var transition=Object.FindFirstObjectByType<MapIrisTransition>();
            Assert.IsNotNull(transition);Assert.IsTrue(transition.IsCovered);
            Assert.AreEqual(style==TableStyle.GrandCasaHotel,arena.LoungeEnabled);
            var minis=transition.GetComponentsInChildren<Image>();
            Assert.AreEqual(1,minis.Length);Assert.IsTrue(minis.All(i=>i.sprite!=null&&!i.raycastTarget));
            Assert.AreSame(CardTableTheme.MiniWorld(style),minis[0].sprite,"Only the destination map is shown.");
            Assert.AreEqual(new Vector2(260,260),minis[0].rectTransform.sizeDelta);
            Assert.AreEqual(grid,arena.BoardPortraitBounds);
            yield return Shot("iris-"+CardTableTheme.MapId(style));
            yield return new WaitForSecondsRealtime(MapIrisTransition.Duration);
            Assert.IsFalse(ui.MapTransitionActive);
            ui.CloseModal();ui.OpenHome();yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(lobby,CardTableTheme.Lobby);
            Assert.AreSame(artwork,Background().sprite,"A combat map change must preserve the lobby's current artwork.");
            yield return Shot("home-"+CardTableTheme.MapId(style));
        }
    }
    [UnityTest] public IEnumerator LatestSelectionWinsAndTransitionFinishesWhileGameTimeIsPaused()
    {
        Time.timeScale=0;
        ui.SelectTableSkin(TableStyle.GrandCasaHotel);yield return new WaitForSecondsRealtime(.1f);
        ui.SelectTableSkin(TableStyle.CampoReal);ui.RenderPage();
        yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
        Assert.IsFalse(ui.MapTransitionActive);Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Current);
        Assert.IsTrue(Object.FindFirstObjectByType<NationArenaScenery>().IsVisible);
        Assert.AreEqual("royal",Object.FindFirstObjectByType<NationArenaScenery>().Artwork.NationId);
        ui.CloseModal();ui.SelectTableSkin(TableStyle.GrandCasaHotel);
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"tsu"},842));
        ui.CloseModal();
        yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase); // No stale style callback reopens a selector in battle.
        yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Scenario skins"));
    }
    static IEnumerator Shot(string name)
    {
        yield return Application.isBatchMode ? null : new WaitForEndOfFrame();
        Directory.CreateDirectory("Logs/world-polish");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/world-polish/"+name+".png"));yield return null;
    }
}


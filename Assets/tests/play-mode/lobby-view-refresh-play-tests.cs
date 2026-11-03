using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class LobbyViewRefreshPlayTests
{
    LocalGameUI ui;EronyaMapTestAccount account;bool playbackBefore;
    [UnitySetUp] public IEnumerator LoadIsolatedMenu()
    {
        playbackBefore=LobbyAnimationDirector.PlaybackEnabled;LobbyAnimationDirector.PlaybackEnabled=false;
        account=new EronyaMapTestAccount(0);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);Assert.IsFalse(ui.MapTransitionActive);
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
    }
    [UnityTearDown] public IEnumerator RestoreIsolatedMenu()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();}
        account?.Dispose();account=null;LobbyAnimationDirector.PlaybackEnabled=playbackBefore;yield return null;
    }
    [UnityTest] public IEnumerator LobbyBriefAndPickerUse43NewStillsAndExistingTaurisWithoutReplacingProfileAvatars()
    {
        AssertMenuFigures();
        var avatars=ui.CanvasRoot.GetComponentsInChildren<PlayerAvatarGraphic>();Assert.IsNotEmpty(avatars);
        foreach(var avatar in avatars)Assert.IsEmpty(avatar.GetComponentsInChildren<LobbyFrameGraphic>());
        ui.OpenDecks();yield return null;yield return null;Assert.AreEqual("decks",ui.MenuPage);AssertMenuFigures();
        ui.OpenCharacterPicker(0);yield return null;yield return null;AssertMenuFigures();
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
    }
    void AssertMenuFigures()
    {
        var figures=ui.CanvasRoot.GetComponentsInChildren<LobbyFrameGraphic>();Assert.IsNotEmpty(figures);
        foreach(var figure in figures)
        {
            Assert.AreEqual(figure.UnitId!="tauris",figure.UsesLobbyView,figure.UnitId);
            Assert.IsNotNull(figure.CurrentSprite,figure.UnitId);
            Assert.IsFalse(figure.HasAuthoredFrames,figure.UnitId);Assert.IsTrue(figure.IsAtRest,figure.UnitId);
        }
    }
}

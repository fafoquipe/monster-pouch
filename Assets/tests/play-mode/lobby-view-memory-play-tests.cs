using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class LobbyViewMemoryPlayTests
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
        account?.Dispose();account=null;LobbyAnimationDirector.PlaybackEnabled=playbackBefore;
        yield return null;LobbyViewSpriteCache.TrimUnused();yield return null;
    }
    [UnityTest] public IEnumerator CasePoolLoadsVisibleCardsScrollsAllIdsAndReopensWithoutChangingGuest()
    {
        var property=typeof(LocalGameUI).GetProperty("GuestProfileDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic);
        string profileFile=Path.Combine((string)property.GetValue(null),"guest-profile.json");
        string before=File.ReadAllText(profileFile);
        var show=typeof(LocalGameUI).GetMethod("ShowCasePool",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(show);
        foreach(string worldId in new[]{"naturalis","aquarion","geothar","volkanar","aireola","naturalis"})
        {
            show.Invoke(ui,new object[]{worldId});yield return null;yield return null;
            var ids=GuestProgressionCatalogue.GetWorld(worldId).CharacterIds;
            var grid=ui.CanvasRoot.GetComponentsInChildren<CollectionGridWindow>().Single(g=>g.ItemCount==ids.Length&&g.ItemIds.SequenceEqual(ids));
            Assert.AreEqual(Mathf.Min(8,ids.Length),grid.ActiveCellCount);
            foreach(string id in ids)
            {
                Assert.IsTrue(grid.EnsureVisible(id));yield return null;
                Assert.LessOrEqual(grid.ActiveCellCount,10);Assert.LessOrEqual(grid.PooledCellCount,10);
                var cards=grid.GetComponentsInChildren<LobbyFrameGraphic>();
                Assert.AreEqual(grid.ActiveCellCount,cards.Length);
                Assert.IsTrue(cards.Any(g=>g.UnitId==id),id);
                foreach(var card in cards)
                {
                    Assert.IsNotNull(card.CurrentSprite,card.UnitId);
                    Assert.AreEqual(card.UnitId!="tauris",card.UsesLobbyView,card.UnitId);
                }
                AssertBoundedAndLive();
            }
            ui.CloseModal();yield return null;yield return null;
            LobbyViewSpriteCache.TrimUnused();yield return null;AssertBoundedAndLive();
        }
        Assert.AreEqual(before,File.ReadAllText(profileFile),"Browsing a Case cannot change ownership, balance or identity.");
    }
    [UnityTest] public IEnumerator BriefPickerAndHiddenHierarchyReacquireWithoutBlankingPedestalOrProfile()
    {
        AssertBoundedAndLive();
        var avatars=ui.CanvasRoot.GetComponentsInChildren<PlayerAvatarGraphic>();Assert.IsNotEmpty(avatars);
        foreach(var avatar in avatars)Assert.IsEmpty(avatar.GetComponentsInChildren<LobbyFrameGraphic>());
        int menuLeases=LobbyViewSpriteCache.ActiveLeaseCount;
        ui.ShowProfileAvatars();yield return null;yield return null;
        var profile=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Avatar content");
        Assert.IsEmpty(profile.GetComponentsInChildren<LobbyFrameGraphic>());
        var icons=profile.GetComponentsInChildren<PlayerAvatarGraphic>();Assert.IsNotEmpty(icons);
        var iconTextures=icons.Select(icon=>icon.mainTexture).ToArray();
        LobbyViewSpriteCache.TrimUnused();yield return null;
        Assert.AreEqual(menuLeases,LobbyViewSpriteCache.ActiveLeaseCount);
        for(int i=0;i<icons.Length;i++){Assert.IsTrue(iconTextures[i]!=null);Assert.AreSame(iconTextures[i],icons[i].mainTexture);}
        ui.CloseModal();yield return null;AssertBoundedAndLive();
        ui.OpenDecks();yield return null;yield return null;AssertBoundedAndLive();
        ui.OpenCharacterPicker(0);yield return null;yield return null;AssertBoundedAndLive();
        var grid=ui.CanvasRoot.GetComponentsInChildren<CollectionGridWindow>().First();
        var ids=grid.ItemIds.ToArray();
        foreach(string id in new[]{ids.First(),ids.Last(),ids.First()})
        {Assert.IsTrue(grid.EnsureVisible(id));yield return null;AssertBoundedAndLive();}
        grid.gameObject.SetActive(false);yield return null;LobbyViewSpriteCache.TrimUnused();yield return null;
        AssertBoundedAndLive();
        grid.gameObject.SetActive(true);yield return null;yield return null;AssertBoundedAndLive();
        ui.CloseModal();yield return null;yield return null;AssertBoundedAndLive();
        ui.OpenHome();yield return null;yield return null;AssertBoundedAndLive();
        Assert.IsNotEmpty(ui.CanvasRoot.GetComponentsInChildren<LobbyFrameGraphic>());
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
    }
    void AssertBoundedAndLive()
    {
        var figures=Object.FindObjectsByType<LobbyFrameGraphic>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
            .Where(g=>g.isActiveAndEnabled).ToArray();
        foreach(var graphic in figures)Assert.IsNotNull(graphic.CurrentSprite,graphic.UnitId);
        Assert.AreEqual(figures.Count(g=>g.UsesLobbyView),LobbyViewSpriteCache.ActiveLeaseCount);
        Assert.LessOrEqual(LobbyViewSpriteCache.IdleEntryCount,LobbyViewSpriteCache.MaximumIdleEntries);
        Assert.LessOrEqual(LobbyViewSpriteCache.IdleRgbaBytes,LobbyViewSpriteCache.MaximumIdleRgbaBytes);
        foreach(var shadow in ui.CanvasRoot.GetComponentsInChildren<LobbyContactShadow>())
        {Assert.IsFalse(float.IsNaN(shadow.FootAnchor.x));Assert.IsFalse(float.IsNaN(shadow.FootAnchor.y));}
    }
}

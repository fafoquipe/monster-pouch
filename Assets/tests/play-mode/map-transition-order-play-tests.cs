using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class MapTransitionOrderPlayTests
{
    EronyaMapTestAccount account;
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Load()
    {
        account=new EronyaMapTestAccount();CardTableTheme.Select(TableStyle.Default);
        CardTableTheme.SelectLobby(TableStyle.Naturalis);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);Assert.IsFalse(ui.MapTransitionActive);
        ui.OpenDecks();yield return new WaitForSecondsRealtime(.4f);
    }
    [UnityTearDown] public IEnumerator Restore(){account?.Dispose();yield return null;}
    string ArenaId()=>Object.FindFirstObjectByType<NationArenaScenery>().Artwork.NationId;

    [UnityTest] public IEnumerator MapSelectionAndRapidReplacementKeepOldArtAndPreferencesUntilBlack()
    {
        ui.SelectTableSkin(TableStyle.Aquarion);ui.RenderPage();
        Assert.AreEqual(TableStyle.Default,CardTableTheme.Current,"A request must not save the new map while the old view is exposed.");
        Assert.AreEqual("classic",ArenaId());
        ui.SelectTableSkin(TableStyle.CasinoNight);ui.RenderPage();
        Assert.AreEqual(TableStyle.Default,CardTableTheme.Current);
        yield return ObserveSwap("map-selection","classic","casino-night",ArenaId,
            changed=>Assert.AreEqual(changed?TableStyle.CasinoNight:TableStyle.Default,CardTableTheme.Current));
        Assert.AreEqual(TableStyle.Naturalis,CardTableTheme.Lobby);
    }

    [UnityTest] public IEnumerator LobbySelectionAndPageRebuildKeepOldBackgroundAndPreferenceUntilBlack()
    {
        var old=LobbyArtwork.Find("naturalis").Background;
        var next=LobbyArtwork.Find("casino-night").Background;
        Func<string> visible=()=>
        {
            var sprite=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Painted lobby artwork").sprite;
            return sprite==old?"naturalis":sprite==next?"casino-night":"unexpected";
        };
        ui.SelectLobbyStyle(TableStyle.CasinoNight);ui.RenderPage();
        Assert.AreEqual(TableStyle.Naturalis,CardTableTheme.Lobby);
        Assert.AreEqual("naturalis",visible());
        yield return ObserveSwap("lobby-selection","naturalis","casino-night",visible,
            changed=>Assert.AreEqual(changed?TableStyle.CasinoNight:TableStyle.Naturalis,CardTableTheme.Lobby));
        Assert.AreEqual(TableStyle.Default,CardTableTheme.Current);
    }

    [UnityTest] public IEnumerator OnlineRoundMapRebuildKeepsTheOldArenaUntilBlack()
    {
        Assert.IsTrue(ui.Match.StartOnlineMatch(BoardSide.Blue,"bugaloo",new[]{"stein"},"popow",new[]{"tsu"},291026,"classic","royal"));
        ui.RenderPage();Assert.AreEqual(1,ui.Match.Round);Assert.AreEqual("classic",ArenaId());
        // Advance the disposable match through its real round setup; no production result or reward.
        typeof(MatchController).GetMethod("BeginRound",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui.Match,null);
        ui.RenderPage();Assert.AreEqual(2,ui.Match.Round);Assert.AreEqual("royal",ui.Match.RoundMap);
        Assert.AreEqual("classic",ArenaId());
        yield return ObserveSwap("online-round","classic","royal",ArenaId,
            changed=>Assert.AreEqual(TableStyle.Default,CardTableTheme.Current,"An online round must not save its map as the local choice."));
    }

    IEnumerator ObserveSwap(string route,string old,string next,Func<string> visible,Action<bool> checkSelection)
    {
        var transition=ui.GetComponentsInChildren<MapIrisTransition>(true).Single();
        var curtain=transition.GetComponentInChildren<IrisCurtain>();
        var miniature=transition.GetComponentsInChildren<Image>().Single(i=>i.name=="Destination mini-world");
        Assert.AreSame(CardTableTheme.MiniWorld(CardTableTheme.FromMapId(next)),miniature.sprite);
        Assert.IsTrue(miniature.preserveAspect);
        bool closing=false,swapped=false,revealed=false;int coveredAt=-1,swappedAt=-1;
        var trace=new List<string>{"frame,progress,radius,visible,covered,miniatureAlpha"};
        float deadline=Time.realtimeSinceStartup+8;
        try
        {
            while(transition.IsRunning&&Time.realtimeSinceStartup<deadline)
            {
                // Force the exact redraw that used to observe eagerly saved preferences.
                ui.RenderPage();Canvas.ForceUpdateCanvases();
                string current=visible();
                trace.Add(FormattableString.Invariant($"{Time.frameCount},{transition.Progress:F4},{curtain.Radius:F4},{current},{transition.IsCovered},{miniature.color.a:F3}"));
                if(current==old)
                {
                    Assert.IsFalse(swapped,"The old scene must never return after a committed swap.");
                    if(curtain.Radius>0)closing=true;
                    else if(coveredAt<0)coveredAt=Time.frameCount;
                }
                else
                {
                    Assert.AreEqual(next,current,"Only the requested destination may appear.");
                    if(!swapped)
                    {
                        Assert.GreaterOrEqual(coveredAt,0,"At least one sampled opaque frame must precede the scene change.");
                        Assert.Greater(Time.frameCount,coveredAt);
                        Assert.IsTrue(transition.IsCovered);Assert.AreEqual(0,curtain.Radius);
                        Assert.That(miniature.color.a,Is.EqualTo(1).Within(.001f));
                        swapped=true;swappedAt=Time.frameCount;
                    }
                    if(curtain.Radius>0)
                    {Assert.Greater(Time.frameCount,swappedAt,"No reveal in the swap frame.");revealed=true;}
                }
                checkSelection(swapped);
                yield return null;
            }
        }
        finally
        {
            string directory=Path.GetFullPath("Logs/local-validation/transition-order");Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory,route+".csv"),trace);
        }
        Assert.IsFalse(transition.IsRunning);Assert.IsTrue(closing);Assert.IsTrue(swapped);Assert.IsTrue(revealed);
        Assert.AreEqual(next,visible());checkSelection(true);
    }
}

public sealed class MapTransitionHitchPlayTests
{
    [UnityTest] public IEnumerator LongFramesAndCallbackReentryCannotSkipTheBlackFrameOrReopenTheCurtain()
    {
        var host=new GameObject("Transition hitch fixture");var transition=MapIrisTransition.Create(host.transform);
        var advance=typeof(MapIrisTransition).GetMethod("Advance",BindingFlags.NonPublic|BindingFlags.Instance);
        int first=0,second=0;
        try
        {
            transition.Play(TableStyle.CasinoNight,()=>
            {
                Assert.AreEqual(0,transition.GetComponentInChildren<IrisCurtain>().Radius);first++;
                transition.Play(TableStyle.Aireola,()=>{Assert.AreEqual(0,transition.GetComponentInChildren<IrisCurtain>().Radius);second++;});
                Assert.AreEqual(0,transition.GetComponentInChildren<IrisCurtain>().Radius,"Reentry while covered must remain covered.");
            });
            advance.Invoke(transition,new object[]{5f});
            Assert.AreEqual(0,first);Assert.AreEqual(0,transition.GetComponentInChildren<IrisCurtain>().Radius);
            advance.Invoke(transition,new object[]{5f});Assert.AreEqual(0,first,"Same-frame calls cannot bypass the opaque frame.");
            yield return null;
            if(first==0)advance.Invoke(transition,new object[]{5f});
            Assert.AreEqual(1,first);Assert.AreEqual(0,second);
            Assert.AreEqual(TableStyle.Aireola,transition.TargetStyle);Assert.AreEqual(0,transition.GetComponentInChildren<IrisCurtain>().Radius);
            advance.Invoke(transition,new object[]{5f});Assert.AreEqual(0,second);
            advance.Invoke(transition,new object[]{5f});Assert.AreEqual(0,second);
            yield return null;
            if(second==0)advance.Invoke(transition,new object[]{5f});
            Assert.AreEqual(1,second);Assert.IsTrue(transition.IsRunning);
            advance.Invoke(transition,new object[]{5f});Assert.IsFalse(transition.IsRunning);
        }
        finally {Object.Destroy(host);}
        yield return null;
    }
}

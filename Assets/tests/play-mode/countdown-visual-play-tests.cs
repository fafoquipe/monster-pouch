using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class CountdownVisualPlayTests
{
    LocalGameUI ui;
    PhaseCountdownHud countdown;
    CanvasGroup group;
    Text number;
    string captures;
    InteractionCaptureGameView captureView;

    [UnitySetUp] public IEnumerator Setup()
    {
        captureView=new InteractionCaptureGameView();
        yield return captureView.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();
        Assert.IsNotNull(ui);
        ui.CloseModal();ui.Match.enabled=false;
        Assert.IsTrue(ui.Match.StartOnlineMatch(BoardSide.Blue,"bugaloo",new[]{"stein"},"popow",new[]{"tsu"},51932));
        yield return null;yield return null;
        countdown=ui.GetComponent<PhaseCountdownHud>();
        group=ui.CanvasRoot.GetComponentsInChildren<CanvasGroup>().Single(g=>g.name=="Central phase countdown");
        number=group.GetComponentInChildren<Text>();
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            if(countdown!=null){countdown.enabled=true;countdown.Suppressed=false;}
            if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
            yield return null;
        }
        finally { captureView?.RestoreSelection(); }
        if(captureView!=null)yield return captureView.WaitForRestoration();
    }

    [UnityTest] public IEnumerator PreparationNumbersChangeAtFiveAndOneWithAReadableContour()
    {
        ui.Match.Advance(30.1f);
        yield return null;yield return null;
        Assert.AreEqual(10,countdown.VisibleNumber);
        Color normal=number.color;
        Assert.That(normal.a,Is.InRange(.25f,.30f),"Earlier preparation numbers retain their original subdued appearance.");
        captures=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Logs","roadmap-v1.2","countdown-visual-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(captures);
        foreach(int seconds in new[]{6,5,4,3,2,1})
        {
            ui.Match.Advance(ui.Match.Remaining-(seconds-.1f));
            yield return null;yield return null;
            Assert.AreEqual(seconds,countdown.VisibleNumber);
            Assert.AreEqual(seconds.ToString(),number.text);
            if(seconds==6)Assert.AreEqual(normal,number.color);
            else AssertUrgencyColor(seconds);
            AssertContour();
            Assert.IsFalse(group.blocksRaycasts);Assert.IsFalse(group.interactable);Assert.IsFalse(number.raycastTarget);
            float remaining=ui.Match.Remaining;
            if(seconds==6||seconds==5||seconds==1)yield return Capture(seconds);
            Assert.AreEqual(remaining,ui.Match.Remaining,"The visual cue must never advance or replace the match clock.");
            Assert.IsFalse(ui.Match.OnlineReady,"The visual cue must not press Ready for the player.");
        }
        countdown.Suppressed=true;
        Assert.AreEqual(0,group.alpha,"A modal must be able to hide even the final red number immediately.");
        Debug.Log("Countdown visual native captures: "+captures);
    }

    [UnityTest] public IEnumerator CombatUsesTheSameUrgencyAndHidesAtZero()
    {
        ui.Match.Ready();ui.Match.SetOpponentReady();
        Assert.IsTrue(ui.Match.RevealOpponent(Array.Empty<PreparationCommand>(),ui.Match.Round));
        Assert.IsTrue(ui.Match.StartOnlineCombat(ui.Match.Round));
        yield return null;yield return null;
        Assert.AreEqual(0,countdown.VisibleNumber,"Combat still starts its central warning only at five seconds.");
        var remainingProperty=typeof(MatchController).GetProperty(nameof(MatchController.Remaining),BindingFlags.Public|BindingFlags.Instance);
        foreach(int seconds in new[]{5,4,3,2,1,0})
        {
            // Supply a clock snapshot without spending 40 seconds simulating an unrelated battle.
            remainingProperty.SetValue(ui.Match,seconds);
            yield return null;yield return null;
            Assert.AreEqual(seconds,countdown.VisibleNumber);
            if(seconds==0)Assert.AreEqual(0,group.alpha,"No red zero should linger after the countdown.");
            else AssertUrgencyColor(seconds);
        }
    }

    void AssertUrgencyColor(int seconds)
    {
        Color color=number.color;
        Assert.Greater(color.a,.9f,"Urgent digits need a readable fill under the existing animated group opacity.");
        if(seconds==1)
        {
            Assert.That(color.r,Is.InRange(.5f,.8f));Assert.Less(color.g,.06f);Assert.Less(color.b,.12f);
        }
        else
        {
            Assert.Greater(color.r,.95f);Assert.That(color.g,Is.InRange(.4f,.65f));Assert.Less(color.b,.2f);
        }
    }

    void AssertContour()
    {
        var outline=number.GetComponent<Outline>();
        Assert.IsNotNull(outline);Assert.IsTrue(outline.enabled);
        Assert.Less(outline.effectColor.r,.15f);Assert.Less(outline.effectColor.g,.08f);Assert.Less(outline.effectColor.b,.08f);
        Assert.Greater(outline.effectColor.a,.9f);
        Assert.That(Mathf.Abs(outline.effectDistance.x),Is.InRange(2.5f,3.5f));
        Assert.AreEqual(Mathf.Abs(outline.effectDistance.x),Mathf.Abs(outline.effectDistance.y));
        Canvas.ForceUpdateCanvases();
        var mesh=number.canvasRenderer.GetMesh();
        Assert.Greater(mesh.vertexCount,4,"The outline must render geometry around the digit.");
    }

    IEnumerator Capture(int seconds)
    {
        yield return new WaitForSecondsRealtime(.10f);
        Assert.Greater(group.alpha,.1f,"Capture the visible entry, not its later fade-out.");
        // Freeze only the presentation during screenshot I/O; the match remains disabled.
        countdown.enabled=false;
        try
        {
            string path=Path.Combine(captures,"countdown-"+seconds+".png");
            Assert.IsFalse(File.Exists(path));
            ScreenCapture.CaptureScreenshot(path);
            for(int frame=0;frame<120;frame++)
            {
                yield return null;
                if(frame>=2&&File.Exists(path)&&new FileInfo(path).Length>24)break;
            }
            Assert.IsTrue(File.Exists(path),"Missing native countdown capture.");
            byte[] bytes=File.ReadAllBytes(path);Assert.Greater(bytes.Length,24);
            Assert.AreEqual(137,bytes[0]);Assert.AreEqual(80,bytes[1]);Assert.AreEqual(78,bytes[2]);Assert.AreEqual(71,bytes[3]);
        }
        finally{countdown.enabled=true;}
    }
}

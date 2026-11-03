using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEngine;
using Object=UnityEngine.Object;
public static class MonsterPouchCasinoHudReview
{
    public static string Capture()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null)return "Enter Play Mode first.";
        ui.StartCoroutine(Review(ui));return "Capturing casino and countdown previews.";
    }
    static IEnumerator Shot(string name,float delay=.25f)
    {
        yield return new WaitForSecondsRealtime(delay);yield return new WaitForEndOfFrame();
        string dir=Path.GetFullPath("Logs/local-validation/screenshots");Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir,name+".png"));yield return new WaitForSecondsRealtime(.20f);
    }
    static IEnumerator Review(LocalGameUI ui)
    {
        var previous=CardTableTheme.Current;
        try
        {
            ui.CloseModal();ui.Match.Menu();CardTableTheme.Select(TableStyle.GrandCasaHotel);
            foreach(string size in new[]{"1920x1200","1600x2560","1080x2340"})
            {
                MonsterPouchLayoutPreview.Resize(size);yield return new WaitForSecondsRealtime(.5f);
                MonsterPouchLayoutPreview.Preparation();yield return Shot("casino-table-"+size,.8f);
                ui.Match.Menu();
            }
            var online=ui.GetComponent<OnlineRoom>();var type=typeof(OnlineRoom).GetNestedType("Packet",BindingFlags.NonPublic);
            var packet=Activator.CreateInstance(type,true);type.GetField("playerName").SetValue(packet,"Rival Érika");
            typeof(OnlineRoom).GetField("opponent",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(online,packet);
            ui.Match.enabled=false;
            ui.Match.StartOnlineMatch(BoardSide.Red,"bugaloo",new[]{"stein"},"popow",new[]{"tsu"},332,"lounge","lounge");
            yield return new WaitForSecondsRealtime(.4f);ui.Match.SetOpponentReady(true);
            yield return Shot("casino-opponent-ready",.5f);
            ui.Match.Advance(30.1f);yield return Shot("countdown-preparation-10",.14f);
            ui.Match.Advance(7.0f);yield return Shot("countdown-preparation-3",.14f);
            ui.Match.SetReady(true);ui.Match.RevealOpponent(new PreparationCommand[0],ui.Match.Round);ui.Match.StartOnlineCombat(ui.Match.Round);
            typeof(MatchController).GetProperty("Remaining").SetValue(ui.Match,5f);
            yield return Shot("countdown-combat-5",.14f);
        }
        finally{ui.Match.Menu();CardTableTheme.Select(previous);}
        ui.OpenHome();MonsterPouchLocalBridge.Reply("casino-hud-review","Casino wide/tablet/phone and ready/countdown captures completed; map preference restored.");
    }
}


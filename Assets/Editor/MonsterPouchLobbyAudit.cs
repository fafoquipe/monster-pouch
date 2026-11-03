using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

// Captures the real UI drawing at fixed gesture times for visual review.
public static class MonsterPouchLobbyAudit
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string All()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        ui.StartCoroutine(Record(ui,null));return "Lobby audit queued.";
    }
    public static string Character(string id)
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        ui.StartCoroutine(Record(ui,id));return "Gesture sequence queued: "+id;
    }
    public static string TrimolScale()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        ui.StartCoroutine(CompareTrimol(ui));return "Trimol scale comparison queued.";
    }
    static IEnumerator CompareTrimol(LocalGameUI ui)
    {
        ui.CloseModal();ui.Match.Menu();ui.OpenDecks();
        var decks=(DeckLibrary)typeof(LocalGameUI).GetField("decks",Private).GetValue(ui);
        var original=decks.Current.Whelps;
        var director=ui.LobbyDirector;director.enabled=false;
        try
        {
            decks.Current.Whelps=(string[])original.Clone();
            if(!decks.Current.Whelps.Contains("trimol"))decks.Current.Whelps[6]="trimol";
            typeof(LocalGameUI).GetMethod("ApplyDeck",Private).Invoke(ui,null);ui.OpenDecks();
            yield return new WaitForSecondsRealtime(.5f);
            MonsterPouchPlayInspection.Capture("trimol-final-lobby");
            yield return null;
        }
        finally
        {
            decks.Current.Whelps=original;
            typeof(LocalGameUI).GetMethod("ApplyDeck",Private).Invoke(ui,null);
        }
        ui.Match.enabled=false;ui.Match.StartMatch("bugaloo",new[]{"trimol"},1905);
        ui.Match.Player.AddCombatCoins(30);var offer=ui.Match.Player.Offers[0];
        ui.Match.BuyAndPlace(0,offer.Token,ui.Match.Board.GetCell(3,8));
        yield return new WaitForSecondsRealtime(.5f);
        MonsterPouchPlayInspection.Capture("trimol-final-battle");
        yield return null;
        ui.Match.Menu();ui.OpenDecks();ui.Match.enabled=true;director.enabled=true;
        MonsterPouchLocalBridge.Reply("trimol-scale-comparison","Captured lobby and battlefield at independent scales; saved deck untouched.");
    }
    static IEnumerator Record(LocalGameUI ui,string id)
    {
        ui.CloseModal();ui.Match.Menu();ui.OpenDecks();
        var director=ui.LobbyDirector;director.enabled=false;
        yield return null;
        string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/local-validation/lobby-audit"));
        Directory.CreateDirectory(directory);
        var ids=id==null?ui.Match.Config.Units.Select(u=>u.Id).Distinct().ToArray():new[]{id};
        foreach(string character in ids)
        {
            ui.CloseModal();
            typeof(LocalGameUI).GetMethod("ShowCollectionInfo",Private).Invoke(ui,new object[]{ui.Match.Config.Get(character)});
            yield return new WaitForSecondsRealtime(.3f);
            var pose=ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Last();
            var times=id==null?new[]{-1f,.48f,.88f}:new[]{-1f,.04f,.12f,.20f,.29f,.38f,.47f,.56f,.65f,.74f,.83f,.92f,.99f};
            float previous=0;
            foreach(float time in times)
            {
                if(time>=0)
                {
                    if(!pose.IsGesturing)pose.PlayGesture();
                    pose.Advance((time-previous)*pose.GestureDuration);previous=time;
                }
                yield return new WaitForEndOfFrame();
                string name=character+"-"+(time<0?"idle":Mathf.RoundToInt(time*100).ToString("D2"));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
                yield return null;
            }
        }
        ui.CloseModal();director.enabled=true;
        MonsterPouchLocalBridge.Reply("lobby-audit", "Captured "+ids.Length+" characters in "+directory);
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local;
using UnityEngine;

public static class MonsterPouchLobbyMotionPreview
{
    public static string Brief()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)return "Enter Play Mode first.";
        ui.Match.Menu();ui.OpenDecks();ui.StartCoroutine(Capture(ui));return "Recording fluid gestures in the Brief.";
    }
    static IEnumerator Capture(LocalGameUI ui)
    {
        var director=ui.LobbyDirector;director.enabled=false;
        yield return new WaitForSecondsRealtime(.3f);
        MonsterPouchPlayInspection.Capture("lobby-fluid-rest");
        var poses=ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>();
        var hero=poses.Single(p=>p.Featured);hero.PlayGesture();
        var kayon=poses.FirstOrDefault(p=>p.UnitId=="kayon")??poses.Last();kayon.PlayGesture();
        string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/local-validation/lobby-fluid-sequence"));
        Directory.CreateDirectory(dir);
        for(int frame=0;frame<120;frame++)
        {
            director.Advance(1f/24);yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,frame.ToString("D3")+".png"));
            yield return new WaitForSecondsRealtime(1f/24);
        }
        director.enabled=true;
        MonsterPouchLocalBridge.Reply("lobby-fluid-preview","120 frames captured; visible portraits="+poses.Length+"; authored="+poses.Count(p=>p.HasAuthoredFrames));
    }
    public static string Character(string id)
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();if(ui==null||!Application.isPlaying)return "Enter Play Mode first.";
        ui.CloseModal();typeof(LocalGameUI).GetMethod("ShowCollectionInfo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{ui.Match.Config.Get(id)});
        var pose=ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Last();pose.PlayGesture();
        return "Showing "+id+"; authored="+pose.HasAuthoredFrames;
    }
}

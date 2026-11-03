using System.Collections;
using System.IO;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEngine;

public static class MonsterPouchCabinetSwipeReview
{
    public static string Capture()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null||ui.Match.Phase!=MatchPhase.Menu)return "Open the lobby first.";
        ui.StartCoroutine(Review(ui));return "Recording the Brief after normal and repeated swipes.";
    }
    static IEnumerator Review(LocalGameUI ui)
    {
        string folder=Path.GetFullPath("Logs/local-validation/cabinet-swipe");Directory.CreateDirectory(folder);
        ui.OpenHome();yield return new WaitForSecondsRealtime(.4f);
        var right=new Vector2(Screen.width*.65f,0);
        ui.SwipeMenu(right);yield return new WaitForSecondsRealtime(.45f);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"cabinet-after-swipe.png"));
        yield return null;
        for(int i=0;i<3;i++){ui.SwipeMenu(-right);ui.SwipeMenu(right);}
        yield return new WaitForSecondsRealtime(.45f);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"cabinet-after-repeated-swipes.png"));
        File.WriteAllText(Path.Combine(folder,"capture.txt"),"Captured the Brief after one swipe and six consecutive swipes; final tab="+ui.MenuTab);
    }
}

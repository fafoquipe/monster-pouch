using System.Collections;
using System.IO;
using System.Reflection;
using MonsterPouch.Local;
using UnityEngine;

public static class MonsterPouchOctober8Review
{
    public static string CaptureLayouts()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)return "Enter Play Mode first.";
        ui.StartCoroutine(Capture(ui));return "Portrait review queued.";
    }
    static IEnumerator Capture(LocalGameUI ui)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        string original=(string)typeof(LocalGameUI).GetField("chosen",flags).GetValue(ui);
        int width=Screen.width,height=Screen.height;
        ui.Match.Menu();
        bool hadStyle=PlayerPrefs.HasKey(CardTableTheme.PreferenceKey);
        string style=PlayerPrefs.GetString(CardTableTheme.PreferenceKey,"classic");
        typeof(LocalGameUI).GetField("chosen",flags).SetValue(ui,"popow");
        string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/portrait-review"));
        Directory.CreateDirectory(dir);
        foreach(string theme in new[]{"royal","lounge","classic"})
        foreach(string size in new[]{"1080x2340","1536x2048","1920x1080"})
        {
            PlayerPrefs.SetString(CardTableTheme.PreferenceKey,theme);
            typeof(LocalGameUI).GetMethod("LoadTableArtwork",flags).Invoke(ui,null);
            MonsterPouchLayoutPreview.Resize(size);
            yield return new WaitForSecondsRealtime(.6f);
            ui.OpenHome();ui.RenderPage();yield return null;
            while(ui.MapTransitionActive)yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,theme+"-home-"+size+".png"));
            yield return new WaitForSecondsRealtime(.5f);
            ui.OpenDecks();yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,theme+"-brief-"+size+".png"));
            yield return new WaitForSecondsRealtime(.5f);
        }
        if(hadStyle)PlayerPrefs.SetString(CardTableTheme.PreferenceKey,style);
        else PlayerPrefs.DeleteKey(CardTableTheme.PreferenceKey);
        typeof(LocalGameUI).GetMethod("LoadTableArtwork",flags).Invoke(ui,null);
        typeof(LocalGameUI).GetField("chosen",flags).SetValue(ui,original);
        MonsterPouchLayoutPreview.Resize(width+"x"+height);ui.OpenHome();ui.RenderPage();
        MonsterPouchLocalBridge.Reply("portrait-review","Captured phone, tablet and laptop: "+dir);
    }
}

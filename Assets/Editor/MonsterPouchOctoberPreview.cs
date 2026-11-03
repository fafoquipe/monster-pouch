using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Temporary runtime fixtures; no scene or deck asset is changed by this review.
public static class MonsterPouchOctoberPreview
{
    const string Saved="MonsterPouch.OctoberPreview.Skin";
    static LocalGameUI UI=>Object.FindFirstObjectByType<LocalGameUI>();
    public static string Skin(string skin)
    {
        if(!SessionState.GetBool(Saved+".saved",false))
        {
            SessionState.SetBool(Saved+".saved",true);
            SessionState.SetBool(Saved+".existed",PlayerPrefs.HasKey(CardTableTheme.PreferenceKey));
            SessionState.SetString(Saved,PlayerPrefs.GetString(CardTableTheme.PreferenceKey,"classic"));
        }
        UI.Match.Menu();UI.SelectTableSkin(skin=="lounge");UI.OpenDecks();
        return "Preview "+skin;
    }
    public static string Skins(){UI.Match.Menu();UI.ShowTableSkins();return "Skin selector";}
    public static string Restore()
    {
        if(SessionState.GetBool(Saved+".saved",false))
        {
            if(UI!=null)UI.SelectTableSkin(SessionState.GetString(Saved,"classic")=="lounge");
            if(SessionState.GetBool(Saved+".existed",false))PlayerPrefs.SetString(CardTableTheme.PreferenceKey,SessionState.GetString(Saved,"classic"));
            else PlayerPrefs.DeleteKey(CardTableTheme.PreferenceKey);
            PlayerPrefs.Save();SessionState.SetBool(Saved+".saved",false);
        }
        if(UI!=null){UI.CloseModal();UI.Match.Menu();UI.OpenHome();UI.LobbyDirector.enabled=true;}
        return "Restored saved skin";
    }
    public static string Character(string request)
    {
        var pieces=request.Split(',');string id=pieces[0];
        UI.Match.Menu();UI.OpenDecks();UI.LobbyDirector.enabled=false;
        Show(id);
        if(pieces.Length>1)
        {
            var pose=UI.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Last();pose.PlayGesture();
            pose.Advance(float.Parse(pieces[1],System.Globalization.CultureInfo.InvariantCulture)*pose.GestureDuration);
        }
        return "Character preview "+request;
    }
    static void Show(string id)
    {
        var definition=UI.Match.Config.Get(id);
        typeof(LocalGameUI).GetMethod("ShowCharacterPreview",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(UI,new object[]{definition,-2,definition.IsMonster,null});
    }
    public static string AllCharacters(){UI.StartCoroutine(Record());return "Recording original idles and main gesture moments";}
    public static string Motion(string id)
    {
        UI.Match.Menu();UI.OpenDecks();UI.StartCoroutine(RecordMotion(id));
        return "Recording 60 fps gesture: "+id;
    }
    static IEnumerator RecordMotion(string id)
    {
        var director=UI.LobbyDirector;director.enabled=false;Show(id);yield return null;
        var pose=UI.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Last();
        pose.PlayGesture();float duration=pose.GestureDuration;
        string folder=Path.GetFullPath("Logs/local-validation/october-motion/"+id);Directory.CreateDirectory(folder);
        int count=Mathf.CeilToInt((duration+1)*60);
        for(int frame=0;frame<count;frame++)
        {
            if(frame>=30)pose.Advance(1f/60);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,frame.ToString("D4")+".png"));
            yield return null;
        }
        director.enabled=true;
        MonsterPouchLocalBridge.Reply("october-motion-"+id,"Captured "+count+" frames at 60 fps in "+folder);
    }
    static IEnumerator Record()
    {
        UI.Match.Menu();UI.OpenDecks();var director=UI.LobbyDirector;director.enabled=false;
        string folder=Path.GetFullPath("Logs/local-validation/october-rigs");Directory.CreateDirectory(folder);
        foreach(string id in UI.Match.Config.Units.Select(d=>d.Id).Distinct())
        {
            Show(id);yield return null;
            var pose=UI.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Last();
            foreach(float progress in new[]{0f,.32f,.62f,.87f})
            {
                if(progress>0)
                {
                    if(!pose.IsGesturing)pose.PlayGesture();
                    // Character() can jump to a sample; this recorder advances monotonically.
                    float delta=progress==.32f?.32f:progress==.62f?.30f:.25f;
                    pose.Advance(delta*pose.GestureDuration);
                }
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,id+"-"+Mathf.RoundToInt(progress*100).ToString("D2")+".png"));
                yield return null;
            }
        }
        UI.CloseModal();director.enabled=true;
        MonsterPouchLocalBridge.Reply("october-rigs","Captured all original idles plus three gesture moments in "+folder);
    }
    public static string Assets()
    {
        return string.Join("\n",new[]{"TableSkins/lounge-deck","TableSkins/table-decor","LobbyRigs/props","LobbyRigs/aky-wings"}.Select(path=>
        {
            var texture=Resources.Load<Texture2D>("MonsterPouch/UI/"+path);
            return path+": "+(texture==null?"MISSING":texture.width+"x"+texture.height+" filter="+texture.filterMode+" mipmaps="+texture.mipmapCount);
        }));
    }
}

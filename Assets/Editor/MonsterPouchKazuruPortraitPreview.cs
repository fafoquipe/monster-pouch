using System.Collections;
using System.IO;
using System.Reflection;
using MonsterPouch.Local;
using UnityEngine;

public static class MonsterPouchKazuruPortraitPreview
{
    public static string Capture()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null) return "Enter Play Mode first.";
        ui.StartCoroutine(Record(ui));
        return "Capturing Kazuru lobby and Brief portraits.";
    }
    public static string CaptureHome(string id)
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null) return "Enter Play Mode first.";
        ui.StartCoroutine(RecordHome(ui,id));return "Capturing centered home portrait: "+id;
    }
    static IEnumerator RecordHome(LocalGameUI ui,string id)
    {
        var chosen=typeof(LocalGameUI).GetField("chosen",BindingFlags.Instance|BindingFlags.NonPublic);
        string previous=(string)chosen.GetValue(ui);
        chosen.SetValue(ui,id);ui.CloseModal();ui.OpenHome();ui.RenderPage();
        yield return null;yield return null;
        Shot(ui,"centered-"+id);
        chosen.SetValue(ui,previous);ui.RenderPage();
        MonsterPouchLocalBridge.Reply("portrait-centered-"+id,"Captured.");
    }
    static IEnumerator Record(LocalGameUI ui)
    {
        var chosen=typeof(LocalGameUI).GetField("chosen",BindingFlags.Instance|BindingFlags.NonPublic);
        string previous=(string)chosen.GetValue(ui);
        chosen.SetValue(ui,"kazuru");
        ui.CloseModal();ui.OpenHome();ui.RenderPage();
        yield return null;yield return null;
        Shot(ui,"lobby");
        ui.OpenCharacterPicker(-1);
        yield return null;yield return null;
        Shot(ui,"brief-grid");
        ui.CloseModal();ui.OpenDecks();ui.RenderPage();
        yield return null;yield return null;
        Shot(ui,"brief");
        chosen.SetValue(ui,previous);ui.OpenHome();ui.RenderPage();
        MonsterPouchLocalBridge.Reply("kazuru-portrait-preview","Captured lobby, Brief grid and Brief pedestal.");
    }
    static void Shot(LocalGameUI ui,string name)
    {
        var camera=ui.GameCamera;var canvas=ui.CanvasRoot.GetComponentInParent<Canvas>();
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var order=canvas.sortingOrder;var distance=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(Screen.width,Screen.height,24);
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder=short.MaxValue;
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            string folder=Path.GetFullPath("Logs/local-validation/kazuru-portrait-size");
            Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=oldCamera;
            canvas.planeDistance=distance;canvas.sortingOrder=order;Object.Destroy(image);Object.Destroy(target);
        }
    }
}

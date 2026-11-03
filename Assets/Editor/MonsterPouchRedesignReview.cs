using System.Collections;
using System.IO;
using System.Linq;
using MonsterPouch.Local;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class MonsterPouchRedesignReview
{
    public static string CaptureAll()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null)return "Enter Play Mode first";
        ui.StartCoroutine(Shots(ui));return "Capturing phone/tablet, three themes, home/brief/match";
    }
    public static string CaptureMaps()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null)return "Enter Play Mode first";
        ui.StartCoroutine(MapShots(ui));return "Capturing original map integration";
    }
    static IEnumerator MapShots(LocalGameUI ui)
    {
        var map=CardTableTheme.Current;
        try
        {
            foreach(string size in new[]{"1080x2340","1600x2560","1920x1200"})
            {
                MonsterPouchLayoutPreview.Resize(size);yield return new WaitForSecondsRealtime(.8f);
                foreach(var style in new[]{TableStyle.Default,TableStyle.GrandCasaHotel,TableStyle.CampoReal})
                {
                    ui.CloseModal();ui.Match.Menu();ui.SelectTableSkin(style);yield return new WaitForSecondsRealtime(1.6f);
                    MonsterPouchLayoutPreview.Preparation();yield return new WaitForSecondsRealtime(.8f);
                    yield return Shot(ui,"restored-"+size+"-"+CardTableTheme.MapId(style));
                }
            }
        }
        finally { CardTableTheme.Select(map); }
        ui.Match.Menu();ui.SelectTableSkin(map);ui.CloseModal();ui.OpenHome();
        MonsterPouchLayoutPreview.Resize("1080x2340");
        MonsterPouchLocalBridge.Reply("restored-map-review","Nine original-map captures finished; map preference restored.");
    }
    static IEnumerator Shots(LocalGameUI ui)
    {
        var lobby=CardTableTheme.Lobby;var map=CardTableTheme.Current;
        try
        {
            foreach(string size in new[]{"1080x2340","1600x2560"})
            {
                MonsterPouchLayoutPreview.Resize(size);yield return new WaitForSecondsRealtime(1);
                foreach(var style in new[]{TableStyle.Default,TableStyle.GrandCasaHotel,TableStyle.CampoReal})
                {
                    CardTableTheme.Select(style);CardTableTheme.SelectLobby(style);
                    // Loading a scene is unnecessary: choosing a map refreshes art and preserves transitions.
                    ui.CloseModal();ui.Match.Menu();ui.SelectTableSkin(style);yield return new WaitForSecondsRealtime(1.6f);
                    ui.CloseModal();ui.OpenHome();yield return new WaitForSecondsRealtime(.8f);
                    yield return Shot(ui,size+"-"+CardTableTheme.MapId(style)+"-home");
                    ui.OpenDecks();yield return new WaitForSecondsRealtime(.8f);
                    yield return Shot(ui,size+"-"+CardTableTheme.MapId(style)+"-brief");
                    ui.ShowTableSkins();yield return new WaitForSecondsRealtime(.3f);
                    yield return Shot(ui,size+"-"+CardTableTheme.MapId(style)+"-styles");
                    MonsterPouchLayoutPreview.Preparation();yield return new WaitForSecondsRealtime(.8f);
                    yield return Shot(ui,size+"-"+CardTableTheme.MapId(style)+"-match");
                }
            }
        }
        finally{CardTableTheme.Select(map);CardTableTheme.SelectLobby(lobby);}
        ui.Match.Menu();ui.SelectTableSkin(map);ui.CloseModal();ui.OpenHome();
        MonsterPouchLocalBridge.Reply("redesign-visual-review","24 screenshots captured; preferences restored");
    }
    static IEnumerator Shot(LocalGameUI ui,string name)
    {
        yield return new WaitForEndOfFrame();string dir=Path.GetFullPath("Logs/local-validation/screenshots");Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir,"final-"+name+".png"));
        MonsterPouchLocalBridge.Reply("final-"+name,MonsterPouchLayoutPreview.Measure());
        yield return new WaitForSecondsRealtime(.3f);
    }
    public static string AuditTextures()
    {
        var lines=new System.Collections.Generic.List<string>{"path,width,height,mipmaps,readWrite,androidOverride,androidFormat"};
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/resources","Assets/art/ui","Assets/art/ambientations"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            importer.GetSourceTextureWidthAndHeight(out int w,out int h);var android=importer.GetPlatformTextureSettings("Android");
            lines.Add($"{path},{w},{h},{importer.mipmapEnabled},{importer.isReadable},{android.overridden},{android.format}");
        }
        Directory.CreateDirectory("docs");File.WriteAllLines("docs/texture-audit-20261013.csv",lines);return (lines.Count-1)+" texture settings recorded";
    }
}

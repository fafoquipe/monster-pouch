using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicit UI-only import. Combat portraits, atlases and balance remain untouched.
public static class MonsterPouchIdleArtwork
{
    const string Root="Assets/resources/MonsterPouch/UI/";
    const string Lobby=Root+"LobbyFrames/clips.json";
    const string Report="Logs/local-validation/idle-artwork-20261017";
    static readonly string[] Whelps={"suki","rosie","pokrak"};
    static readonly string[] Monsters={"anuik","bugaloo","popow","sepora","tauris"};
    [Serializable] public sealed class Manifest { public LobbyFrameClip[] clips; }

    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var lobby=JsonUtility.FromJson<Manifest>(File.ReadAllText(Lobby));
        foreach(string id in Whelps)
        {
            string path=Root+"NewWhelpIdles/"+id+".png";
            ExportNativeIdle("ArtArchive/IdleArtwork-20261017/"+id+"-source.png",path);
            Import(path);
            var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");
            RestoreWhelpIdle(character,lobby.clips.Single(c=>c.id==id));
        }
        File.WriteAllText(Lobby,JsonUtility.ToJson(lobby,true));
        var home=new Manifest {clips=Monsters.Select(id=>
        {
            string path=Root+"BattleHomeIdles/"+id+".png";
            Import(path);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return IdleClip(id,path,texture);
        }).ToArray()};
        File.WriteAllText(Root+"BattleHomeIdles/clips.json",JsonUtility.ToJson(home,true));
        AssetDatabase.ImportAsset(Lobby);
        AssetDatabase.ImportAsset(Root+"BattleHomeIdles/clips.json");
        AssetDatabase.SaveAssets();
        foreach(string field in new[]{"clips","idles","homeIdles"})
            typeof(LobbyFrameGraphic).GetField(field,BindingFlags.Static|BindingFlags.NonPublic)?.SetValue(null,null);
        Directory.CreateDirectory(Report);
        string report="Imported 3 native 64px Whelp idles and 5 detailed Monster home idles. Brief/collection Monster and combat art unchanged.";
        File.WriteAllText(Report+"/import.txt",report);
        return report;
    }

    // Character installers call this after rebuilding combat clips, so a later
    // balance/art reinstall keeps the deliberately separate UI idle.
    public static void RestoreWhelpIdle(CharacterDefinition character,LobbyFrameClip clip)
    {
        string path=Root+"NewWhelpIdles/"+clip.id+".png";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(sprite==null)return;
        var ui=IdleClip(clip.id,path,sprite.texture);
        clip.idleTexture=ui.idleTexture;clip.idleRect=ui.idleRect;
        clip.idleBounds=ui.idleBounds;clip.idleAnchor=ui.idleAnchor;
        character.Card=sprite;character.Icon=sprite;EditorUtility.SetDirty(character);
        MonsterPouchBriefPortraits.Fit(character);
    }

    static LobbyFrameClip IdleClip(string id,string path,Texture2D texture)
    {
        var b=VisibleBounds(texture);
        return new LobbyFrameClip {id=id,idleTexture=path.Substring("Assets/resources/".Length).Replace(".png",""),
            idleRect=new[]{0f,0f,(float)texture.width,(float)texture.height},
            idleBounds=new[]{(float)b.x,(float)b.y,(float)b.width,(float)b.height},
            // Anuik uses its body axis; Sepora uses the midpoint between her feet.
            idleAnchor=new[]{id=="anuik"?texture.width*.46f:id=="sepora"?texture.width*.50f:b.x+b.width*.5f,(float)b.y},frames=Array.Empty<LobbyFramePose>()};
    }
    static RectInt VisibleBounds(Texture2D texture)
    {
        // Imported UI textures keep no CPU copy. Authoring still measures the
        // preserved source PNG; temporary decoding is released after the query.
        if(!texture.isReadable)
        {
            string path=AssetDatabase.GetAssetPath(texture);
            if(string.IsNullOrEmpty(path))throw new InvalidDataException("No source for idle: "+texture.name);
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!source.LoadImage(File.ReadAllBytes(path)))throw new InvalidDataException("Cannot decode idle: "+path);
                if(source.width!=texture.width||source.height!=texture.height)
                    throw new InvalidDataException("Idle source/import dimensions differ; regenerate the clip at its imported resolution: "+path);
                return VisibleBounds(source);
            }
            finally{Object.DestroyImmediate(source);}
        }
        var pixels=texture.GetPixels32();int x0=texture.width,y0=texture.height,x1=-1,y1=-1;
        for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>128)
        {x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}
        if(x1<x0)throw new InvalidDataException("Empty idle: "+texture.name);
        return new RectInt(x0,y0,x1-x0+1,y1-y0+1);
    }
    static void ExportNativeIdle(string sourcePath,string path)
    {
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        source.LoadImage(File.ReadAllBytes(sourcePath));var b=VisibleBounds(source);
        int height=64,width=Mathf.RoundToInt(b.width*64f/b.height);
        var target=new Texture2D(width+4,height+4,TextureFormat.RGBA32,false);
        var pixels=new Color32[(width+4)*(height+4)];var original=source.GetPixels32();
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int sx=b.x+Mathf.Min(b.width-1,Mathf.FloorToInt((x+.5f)*b.width/width));
            int sy=b.y+Mathf.Min(b.height-1,Mathf.FloorToInt((y+.5f)*b.height/height));
            var pixel=original[sy*source.width+sx];pixel.a=pixel.a>128?(byte)255:(byte)0;
            pixels[(y+2)*(width+4)+x+2]=pixel;
        }
        target.SetPixels32(pixels);target.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path,target.EncodeToPNG());Object.DestroyImmediate(source);Object.DestroyImmediate(target);
    }
    static void Import(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.isReadable=false;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
    }
    public static void RunPlayTests()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new InvalidOperationException("Wait for current tests.");
        Directory.CreateDirectory(Report);SessionState.SetString("MonsterPouch.Validation.PendingRun","idle-artwork-20261017/play");
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter {testMode=TestMode.PlayMode,testNames=new[]{"LobbyIdleArtworkPlayTests"}}));
    }
}


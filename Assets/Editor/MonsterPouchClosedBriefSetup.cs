using System;
using System.IO;
using System.Linq;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class MonsterPouchClosedBriefSetup
{
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before importing the closed Brief.");
        const string path="Assets/art/ui/brief/brief-closed.png";
        MonsterPouchSpriteAnimationImporter.PrepareChromaSource("Assets/art/ui/brief/brief-closed-source.png",path,new Color32(255,0,255,255));
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        Rect bounds;
        try
        {
            if(!ImageConversion.LoadImage(source,File.ReadAllBytes(path),false))throw new InvalidDataException(path);
            var pixels=source.GetPixels32();int left=source.width,bottom=source.height,right=0,top=0;
            for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)
            {
                if(pixels[y*source.width+x].a<32)continue;
                left=Mathf.Min(left,x);bottom=Mathf.Min(bottom,y);right=Mathf.Max(right,x+1);top=Mathf.Max(top,y+1);
            }
            if(right<=left||top<=bottom)throw new InvalidDataException("Closed case has no visible pixels.");
            left=Mathf.Max(0,left-2);bottom=Mathf.Max(0,bottom-2);right=Mathf.Min(source.width,right+2);top=Mathf.Min(source.height,top+2);
            bounds=new Rect(left,bottom,right-left,top-bottom);
        }
        finally {UnityEngine.Object.DestroyImmediate(source);}
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
        importer.npotScale=TextureImporterNPOTScale.None;importer.spritePixelsPerUnit=100;importer.isReadable=false;
        importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects().FirstOrDefault(r=>r.name=="brief-closed");
        var rect=new SpriteRect{name="brief-closed",rect=bounds,pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,spriteID=old==null?GUID.Generate():old.spriteID};
        provider.SetSpriteRects(new[]{rect});
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(rect.name,rect.spriteID)});
        provider.Apply();importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name=="brief-closed");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/main-scene.unity")scene=EditorSceneManager.OpenScene("Assets/Scenes/main-scene.unity");
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.ClosedBriefArt=sprite;
        EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Closed Brief imported with original alpha and visible bounds "+bounds);
    }
}

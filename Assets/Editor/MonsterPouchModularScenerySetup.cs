using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
public static class MonsterPouchModularScenerySetup
{
    [Serializable] sealed class Part { public string name;public float x,y,width,height; }
    [Serializable] sealed class Parts {public Part[] sprites;}
    [MenuItem("Monster Pouch/Art/Configure modular arenas")]
    public static string Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before configuring scenery.");
        foreach(string id in new[]{"classic","royal"})
        {
            var sprites=ImportAtlas(id);
            string path="Assets/resources/MonsterPouch/Styles/"+id+"-scenery.asset";
            var scenery=AssetDatabase.LoadAssetAtPath<ArenaSceneryDefinition>(path);
            if(scenery==null)
            {
                scenery=ScriptableObject.CreateInstance<ArenaSceneryDefinition>();
                if(id=="classic")ConfigurePark(scenery,sprites);else ConfigureRoyal(scenery,sprites);
                AssetDatabase.CreateAsset(scenery,path);
            }
            var style=AssetDatabase.LoadAssetAtPath<EnvironmentStyle>("Assets/resources/MonsterPouch/Styles/"+id+".asset");
            style.Scenery=scenery;EditorUtility.SetDirty(style);
        }
        AssetDatabase.SaveAssets();return "Park and royal maps now use independently editable props; existing custom layouts preserved.";
    }
    public static string ConfigureFurniture()
    {
        var sprites=ImportAtlas("furniture");const string path="Assets/resources/MonsterPouch/Styles/LobbyFurniture.asset";
        var config=AssetDatabase.LoadAssetAtPath<LobbyFurnitureDefinition>(path);
        if(config==null)
        {
            config=ScriptableObject.CreateInstance<LobbyFurnitureDefinition>();
            config.Shelf=sprites["shelf"];config.Pedestal=sprites["pedestal"];config.Cornice=sprites["cornice"];config.Post=sprites["post"];
            config.PedestalFootHeight=.31f;AssetDatabase.CreateAsset(config,path);
        }
        AssetDatabase.SaveAssets();return "Separate wooden shelves, posts, cornice and pedestals imported with resizable borders.";
    }
    public static string ConfigureExistingLamps()
    {
        var path="Assets/resources/MonsterPouch/Styles/royal-scenery.asset";
        var style=AssetDatabase.LoadAssetAtPath<ArenaSceneryDefinition>(path);
        foreach(var element in style.Elements)if(element.Sprite!=null&&element.Sprite.name=="candles")element.LightStrength=.20f;
        EditorUtility.SetDirty(style);AssetDatabase.SaveAssets();return "Royal candles now illuminate nearby character sprites.";
    }
    internal static Dictionary<string,Sprite> ImportAtlas(string id)
    {
        string path="Assets/art/ambientations/modular/"+id+"-props.png";
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=id=="furniture"?400:100;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.isReadable=false;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        var android=importer.GetPlatformTextureSettings("Android");android.name="Android";android.overridden=true;android.maxTextureSize=2048;
        android.format=TextureImporterFormat.ETC2_RGBA8;android.textureCompression=TextureImporterCompression.CompressedHQ;android.compressionQuality=100;
        importer.SetPlatformTextureSettings(android);
        var factory=new SpriteDataProviderFactories();factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects().ToDictionary(r=>r.name,r=>r.spriteID);
        var parts=JsonUtility.FromJson<Parts>(File.ReadAllText(Path.ChangeExtension(path,"parts.json")));
        var rects=parts.sprites.Select(p=>new SpriteRect{name=p.name,rect=new Rect(p.x,p.y,p.width,p.height),
            border=id!="furniture"?Vector4.zero:p.name=="post"?new Vector4(8,64,8,64):p.name=="pedestal"?Vector4.zero:new Vector4(40,20,40,20),
            alignment=SpriteAlignment.BottomCenter,pivot=new Vector2(.5f,0),spriteID=old.TryGetValue(p.name,out var guid)?guid:GUID.Generate()}).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name,s=>s);
    }
    static SceneryElement Prop(Dictionary<string,Sprite> sprites,string sprite,string name,SceneryAnchor anchor,float x,float y,float width,bool flip=false)
        =>new SceneryElement{Name=name,Sprite=sprites[sprite],Anchor=anchor,Foot=new Vector2(x,y),Width=width,FlipX=flip,LightStrength=sprite=="candles"?.20f:0,Shadow=sprite=="bunting"?0:.18f};
    static void ConfigurePark(ArenaSceneryDefinition d,Dictionary<string,Sprite> s)
    {
        d.Floor=new Color(.30f,.58f,.61f);d.Wall=new Color(.57f,.57f,.71f);d.WallDetail=new Color(.52f,.51f,.64f);
        d.Trim=new Color(.65f,.56f,.42f);d.Saturation=.78f;
        var items=new List<SceneryElement>{
            Prop(s,"castle","Castillo inflable",SceneryAnchor.BoardTop,0,-8,248),
            Prop(s,"shelf","Estante izquierdo",SceneryAnchor.BoardTop,-212,-14,88),
            Prop(s,"shelf","Estante derecho",SceneryAnchor.BoardTop,212,-14,88,true),
            Prop(s,"bunting","Banderines izquierdos",SceneryAnchor.BoardTop,-195,-144,220),
            Prop(s,"bunting","Banderines derechos",SceneryAnchor.BoardTop,195,-144,220,true)};
        foreach(float y in new[]{-145f,30,200})
        {
            items.Add(Prop(s,"foam","Bloques laterales izquierdos "+y,SceneryAnchor.BoardLeft,-60,y,96));
            items.Add(Prop(s,"foam","Bloques laterales derechos "+y,SceneryAnchor.BoardRight,60,y+36,96,true));
        }
        foreach(float x in new[]{-430f,430})
        {
            items.Add(Prop(s,"shelf","Estante exterior "+x,SceneryAnchor.BoardTop,x,-10,115,x>0));
            items.Add(Prop(s,"foam","Juguetes exteriores "+x,SceneryAnchor.Room,x,60,135,x>0));
            items.Add(Prop(s,"foam","Juguetes inferiores "+x,SceneryAnchor.Room,x,420,125,x<0));
        }
        d.Elements=items.ToArray();
    }
    static void ConfigureRoyal(ArenaSceneryDefinition d,Dictionary<string,Sprite> s)
    {
        d.FloorTexture=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/resources/MonsterPouch/UI/TableSkins/royal-wood.png");
        d.Floor=new Color(.76f,.68f,.57f);d.Wall=new Color(.54f,.50f,.43f);d.WallDetail=new Color(.45f,.42f,.36f);
        d.Trim=new Color(.64f,.50f,.28f);d.StoneWall=d.Carpets=true;d.Saturation=.82f;
        var items=new List<SceneryElement>{Prop(s,"throne","Trono",SceneryAnchor.BoardTop,0,-8,130)};
        foreach(int side in new[]{-1,1})
        {
            items.Add(Prop(s,"column","Columna principal "+side,SceneryAnchor.BoardTop,side*213,-8,62));
            items.Add(Prop(s,"planter","Macetero del trono "+side,SceneryAnchor.BoardTop,side*126,-8,68));
            items.Add(Prop(s,"candles","Candelabro del trono "+side,SceneryAnchor.BoardTop,side*245,-2,43));
            items.Add(Prop(s,"column","Columna exterior "+side,SceneryAnchor.BoardTop,side*425,-8,76));
            items.Add(Prop(s,"candles","Candelabro exterior "+side,SceneryAnchor.BoardTop,side*360,0,50));
            items.Add(Prop(s,"planter","Macetero lateral "+side,side<0?SceneryAnchor.BoardLeft:SceneryAnchor.BoardRight,side*58,0,82));
            items.Add(Prop(s,"candles","Candelabro lateral "+side,side<0?SceneryAnchor.BoardLeft:SceneryAnchor.BoardRight,side*54,250,40));
        }
        d.Elements=items.ToArray();
    }
}

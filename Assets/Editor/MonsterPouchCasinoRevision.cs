using System;
using System.Collections.Generic;
using System.IO;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
public static class MonsterPouchCasinoRevision
{
    [Serializable] sealed class Foot {public string sprite;public float bottom;}
    [Serializable] sealed class Feet {public Foot[] feet;}
    [MenuItem("Monster Pouch/Art/Configure Grand Casa casino table")]
    public static string Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var sprites=MonsterPouchModularScenerySetup.ImportAtlas("casino-accessories");
        const string floorPath="Assets/art/ambientations/casino/casino-table-path.png";
        AssetDatabase.ImportAsset(floorPath);var importer=(TextureImporter)AssetImporter.GetAtPath(floorPath);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        var android=importer.GetPlatformTextureSettings("Android");android.name="Android";android.overridden=true;android.maxTextureSize=2048;
        android.format=TextureImporterFormat.ETC2_RGB4;android.textureCompression=TextureImporterCompression.CompressedHQ;android.compressionQuality=100;
        importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
        const string path="Assets/resources/MonsterPouch/Styles/lounge-scenery.asset";
        var d=AssetDatabase.LoadAssetAtPath<ArenaSceneryDefinition>(path);
        bool create=d==null;if(create)d=ScriptableObject.CreateInstance<ArenaSceneryDefinition>();else Undo.RecordObject(d,"Casino table scenery");
        d.Tabletop=true;d.FloorTexture=AssetDatabase.LoadAssetAtPath<Sprite>(floorPath);d.FloorTileWidth=941;d.Floor=new Color(.90f,.90f,.90f,1);
        d.Saturation=.86f;d.ShadowLength=.48f;d.ShadowDirection=new Vector2(.22f,1);
        var props=new List<SceneryElement>();
        var feet=JsonUtility.FromJson<Feet>(File.ReadAllText("Assets/art/ambientations/modular/casino-feet.json"));
        void Add(Sprite sprite,string name,SceneryAnchor anchor,float x,float y,float width,float shadow=.36f)
        {
            float padding=2f/sprite.rect.height;
            foreach(var foot in feet.feet)if(foot.sprite==sprite.name){padding=foot.bottom;break;}
            props.Add(new SceneryElement{Name=name,BottomPadding=padding,Sprite=sprite,Anchor=anchor,Foot=new Vector2(x,y),Width=width,Shadow=shadow,Rotation=0,Order=10,
                LightStrength=sprite==sprites["lamp"]?.18f:0,LightRadius=440});
        }
        Sprite Chip(string id)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/art/ambientations/casino/chips/"+id+".png");
        // The nine original scene chip objects own the reference composition.
        // Keep the existing six peripheral accessories for wide desktop windows.
        // Wider screens reveal open felt instead of adding repeated betting stations.
        Add(sprites["lamp"],"Lampara de mesa -1",SceneryAnchor.BoardLeft,-255,-172,76,.40f);
        Add(sprites["tray"],"Bandeja del croupier -1",SceneryAnchor.BoardLeft,-146,-127,139,.40f);
        Add(sprites["cards"],"Cartas extendidas -1",SceneryAnchor.BoardLeft,-130,86,101,.29f);
        Add(sprites["lamp"],"Lampara de mesa 1",SceneryAnchor.BoardRight,248,-90,83,.40f);
        Add(sprites["shoe"],"Mazo del croupier 1",SceneryAnchor.BoardRight,226,72,75,.37f);
        Add(Chip("chip-mixed-pile-2"),"Apuestas laterales 1",SceneryAnchor.BoardRight,140,-30,90,.38f);
        d.Elements=props.ToArray();
        if(create)AssetDatabase.CreateAsset(d,path);else EditorUtility.SetDirty(d);
        var style=AssetDatabase.LoadAssetAtPath<EnvironmentStyle>("Assets/resources/MonsterPouch/Styles/lounge.asset");
        style.Scenery=d;EditorUtility.SetDirty(style);
        const string numbersPath="Assets/resources/MonsterPouch/DamageNumbers.asset";
        if(AssetDatabase.LoadAssetAtPath<DamageNumberSettings>(numbersPath)==null)AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DamageNumberSettings>(),numbersPath);
        AssetDatabase.SaveAssets();
        return "Grand Casa: original dark green felt, "+props.Count+" upright casino pieces, open felt without decorative lines; damage limits asset created.";
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

// Explicit revision of the rejected sparse park, never automatically reruns over authored edits.
public static class MonsterPouchParkSceneryRevision
{
    [MenuItem("Monster Pouch/Art/Rebuild detailed park from separate pieces")]
    public static string Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var original=MonsterPouchModularScenerySetup.ImportAtlas("classic");
        var foam=MonsterPouchModularScenerySetup.ImportAtlas("park-foam");
        var details=MonsterPouchModularScenerySetup.ImportAtlas("park-details");
        var shelves=MonsterPouchModularScenerySetup.ImportAtlas("park-shelves");
        var floor=ImportFloor();
        var d=AssetDatabase.LoadAssetAtPath<ArenaSceneryDefinition>("Assets/resources/MonsterPouch/Styles/classic-scenery.asset");
        Undo.RecordObject(d,"Restore densely furnished park");
        d.FloorTexture=floor;d.FloorTileWidth=360;d.Floor=new Color(.86f,.92f,.92f);
        d.Wall=new Color(.62f,.54f,.85f);d.WallDetail=new Color(.48f,.39f,.71f);
        d.Trim=new Color(.50f,.41f,.65f);d.Saturation=.86f;d.FloorStartsAboveBoard=16;
        d.StoneWall=d.Carpets=false;
        var items=new List<SceneryElement>();
        void Add(Dictionary<string,Sprite> sheet,string sprite,string name,SceneryAnchor anchor,float x,float y,float width,float shadow=.08f,int order=0)
        {
            items.Add(new SceneryElement{Name=name,Sprite=sheet[sprite],Anchor=anchor,Foot=new Vector2(x,y),Width=width,Shadow=shadow,Order=order});
        }
        Add(original,"castle","Castillo inflable central",SceneryAnchor.BoardTop,0,-10,270,.14f,12);
        // Different cabinets form a continuous backdrop rather than two tiny identical props.
        for(int side=-1;side<=1;side+=2)
        {
            Add(shelves,side<0?"bears":"robots","Estante alto "+side,SceneryAnchor.BoardTop,side*205,-16,112,.10f,10);
            Add(shelves,"castle-toys","Estante estrecho "+side,SceneryAnchor.BoardTop,side*140,-13,54,.08f,11);
            Add(shelves,"trucks","Estante bajo "+side,SceneryAnchor.BoardTop,side*304,-7,112,.10f,13);
            Add(shelves,side<0?"robots":"bears","Estante exterior "+side,SceneryAnchor.BoardTop,side*414,-16,100,.10f,10);
            Add(shelves,"trucks","Estante exterior bajo "+side,SceneryAnchor.BoardTop,side*520,-5,114,.08f,13);
        }
        for(int i=0;i<7;i++)
        {
            float x=-600+i*200;
            Add(original,"bunting","Banderines "+i,SceneryAnchor.BoardTop,x,-160+(i%2)*12,218,0,-20);
        }
        for(int i=0;i<19;i++)
        {
            float x=-850+i*95;
            Add(details,i%3==0?"wide-cloud":"cloud","Nube de pared "+i,SceneryAnchor.BoardTop,x,-225+(i%3)*32,46+(i%3)*8,0,-30);
            Add(details,i%2==0?"cloud":"wide-cloud","Nube superior "+i,SceneryAnchor.BoardTop,x+36,-336+(i%4)*14,42+(i%3)*7,0,-30);
        }
        string[] shapes={"yellow-arch","blue-cube","violet-ramp","red-cylinder","teal-cube","yellow-cube",
            "blue-cylinder","red-cube","violet-arch","blue-beam","teal-block","yellow-beam"};
        string[] balls={"red-ball","blue-ball","yellow-ball","violet-ball","pink-ball","teal-ball"};
        // Three interlocking banks extend beyond portrait and tablet crop, with an irregular inner edge.
        for(int side=-1;side<=1;side+=2)
        for(int lane=0;lane<4;lane++)
        for(int row=0;row<19;row++)
        {
            int n=row*7+lane*11+(side>0?5:0);
            float width=48+(n%5)*4+lane*3;
            float distance=24+lane*52+(n%4)*7;
            float y=22+row*44+lane*13+(side>0?14:0)+(n%3-1)*5;
            string shape=shapes[(row+lane*5+(side>0?3:0))%shapes.Length];
            float x=side*distance;
            var anchor=side<0?SceneryAnchor.BoardLeft:SceneryAnchor.BoardRight;
            // BoardLeft/Right is vertically centered. The authored strip starts at the board's top.
            float localY=y-280;
            Add(foam,shape,"Bloque "+side+" "+lane+" "+row,anchor,x,localY,width,.13f,100+row*10+lane);
            // Balls follow the uneven shore, filling gaps without making a repeated group.
            float ballWidth=22+(n%4)*3;
            Add(details,balls[n%balls.Length],"Pelota "+side+" "+lane+" "+row,anchor,
                side*Mathf.Max(12,distance-width*.45f-3),localY+12,ballWidth,.12f,105+row*10+lane);
            if(row%2==0)
                Add(details,balls[(n+2)%balls.Length],"Pelota pequena "+side+" "+lane+" "+row,anchor,
                    side*(distance+width*.36f),localY+14,18+(n%3)*3,.12f,106+row*10+lane);
        }
        // The far edge is populated too, so wide displays reveal a room instead of blank color.
        for(int side=-1;side<=1;side+=2)
        for(int row=0;row<8;row++)
        {
            var anchor=side<0?SceneryAnchor.BoardLeft:SceneryAnchor.BoardRight;
            Add(foam,shapes[(row*3+(side>0?2:0))%shapes.Length],"Bloque exterior "+side+" "+row,
                anchor,side*(270+row%3*68),-225+row*114,100+row%3*12,.13f,100+row*22);
        }
        // A low bank closes the foreground below the pouch without crossing the playable area.
        for(int i=0;i<17;i++)
        {
            float x=-400+i*82;
            Add(foam,i%3==0?"red-cube":i%3==1?"yellow-beam":"blue-beam","Borde frontal "+i,
                SceneryAnchor.BoardBottom,x,310+(i%2)*12,92,.14f,500+i);
        }
        d.Elements=items.ToArray();
        EditorUtility.SetDirty(d);AssetDatabase.SaveAssets();
        return "Detailed park saved: "+items.Count+" independent pieces, 12 foam shapes, 6 balls, 4 cabinets, clouds and tiled floor.";
    }
    public static string PackPieces()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        const string path="Assets/art/ambientations/modular/park-pieces.spriteatlasv2";
        var d=AssetDatabase.LoadAssetAtPath<ArenaSceneryDefinition>("Assets/resources/MonsterPouch/Styles/classic-scenery.asset");
        var atlas=new SpriteAtlasAsset();
        var sprites=d.Elements.Select(e=>(UnityEngine.Object)e.Sprite).Distinct().ToArray();atlas.Add(sprites);
        SpriteAtlasAsset.Save(atlas,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        ConfigurePacking();
        return "Packed "+sprites.Length+" unique park sprites; tiled floor kept independent.";
    }
    public static string ConfigurePacking()
    {
        const string path="Assets/art/ambientations/modular/park-pieces.spriteatlasv2";
        var importer=AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
        if(importer==null)throw new InvalidOperationException("Missing park Sprite Atlas V2 importer.");
        // V2 settings belong to the importer; SpriteAtlasAsset setters no longer apply them.
        var packing=importer.packingSettings;packing.enableRotation=false;packing.enableTightPacking=false;packing.padding=4;
        importer.packingSettings=packing;
        var texture=importer.textureSettings;texture.filterMode=FilterMode.Point;texture.generateMipMaps=false;texture.readable=false;texture.sRGB=true;
        importer.textureSettings=texture;importer.includeInBuild=true;
        var platform=importer.GetPlatformSettings("DefaultTexturePlatform");platform.name="DefaultTexturePlatform";platform.maxTextureSize=2048;
        platform.textureCompression=TextureImporterCompression.Uncompressed;importer.SetPlatformSettings(platform);
        var android=importer.GetPlatformSettings("Android");android.name="Android";android.overridden=true;android.maxTextureSize=2048;
        android.format=TextureImporterFormat.ETC2_RGBA8;android.textureCompression=TextureImporterCompression.CompressedHQ;android.compressionQuality=100;
        importer.SetPlatformSettings(android);importer.SaveAndReimport();
        return "Park Atlas V2: Point filtering, fixed upright packing, Android ETC2 RGBA8.";
    }
    static Sprite ImportFloor()
    {
        const string path="Assets/art/ambientations/modular/park-floor.png";
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        var android=importer.GetPlatformTextureSettings("Android");android.name="Android";android.overridden=true;
        android.maxTextureSize=2048;android.format=TextureImporterFormat.ETC2_RGB4;
        android.textureCompression=TextureImporterCompression.CompressedHQ;android.compressionQuality=100;
        importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}


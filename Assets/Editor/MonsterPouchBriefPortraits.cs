using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Separate UI slices discard animation-cell padding without changing combat poses.
public static class MonsterPouchBriefPortraits
{
    [MenuItem("Monster Pouch/Art/Fit new Whelps in Brief")]
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var reports=new System.Collections.Generic.List<string>();
        foreach(string id in new[]{"suki","rosie","pokrak"})
        {
            var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+id+"/Character");
            Vector2 before=character.Portrait.rect.size;
            Fit(character);
            reports.Add(id+": "+before+" -> "+character.Portrait.rect.size+"; combat canvas "+character.Visuals.Idle(UnitFacing.South).rect.size);
        }
        AssetDatabase.SaveAssets();
        string report=string.Join("\n",reports);
        Directory.CreateDirectory("Logs/local-validation/preparation-idles-20261017");
        File.WriteAllText("Logs/local-validation/preparation-idles-20261017/brief-fit.txt",report);
        return report;
    }

    public static void Fit(CharacterDefinition character)
    {
        var source=character.Visuals.Idle(UnitFacing.South);
        string path=AssetDatabase.GetAssetPath(source),name=character.Id+"-brief-portrait";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var slices=provider.GetSpriteRects().ToList();
        Rect region=slices.Single(s=>s.name==source.name).rect;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        Rect crop;
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(path)))throw new InvalidDataException(path);
            var pixels=texture.GetPixels32();
            int left=(int)region.xMax,bottom=(int)region.yMax,right=-1,top=-1;
            for(int y=(int)region.yMin;y<(int)region.yMax;y++)
            for(int x=(int)region.xMin;x<(int)region.xMax;x++)
                if(pixels[y*texture.width+x].a>32)
                {left=Math.Min(left,x);bottom=Math.Min(bottom,y);right=Math.Max(right,x);top=Math.Max(top,y);}
            if(right<left)throw new InvalidDataException("Empty portrait: "+character.Id);
            int padding=Math.Max(1,Mathf.RoundToInt(Math.Max(right-left+1,top-bottom+1)*.015f));
            crop=Rect.MinMaxRect(Math.Max(region.xMin,left-padding),Math.Max(region.yMin,bottom-padding),
                Math.Min(region.xMax,right+1+padding),Math.Min(region.yMax,top+1+padding));
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
        var portrait=slices.FirstOrDefault(s=>s.name==name);
        if(portrait==null){portrait=new SpriteRect{name=name,spriteID=GUID.Generate()};slices.Add(portrait);}
        portrait.rect=crop;portrait.pivot=Vector2.one*.5f;portrait.alignment=SpriteAlignment.Center;
        provider.SetSpriteRects(slices.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(slices.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name==name);
        character.Portrait=character.Visuals.Portrait=sprite;EditorUtility.SetDirty(character);
        var catalog=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        var art=catalog.Get(character.Id);
        if(art!=null){art.Portrait=sprite;EditorUtility.SetDirty(catalog);}
    }
}

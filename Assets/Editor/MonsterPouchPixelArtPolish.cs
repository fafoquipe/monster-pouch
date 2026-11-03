using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

public static class MonsterPouchPixelArtPolish
{
    public static string Import()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before importing art.");
        const string path="Assets/resources/MonsterPouch/UI/TableSkins/royal-arena-pixel.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;
        PreservePixels(importer);
        var art=AssetDatabase.LoadAssetAtPath<UnitArtCatalog>("Assets/resources/MonsterPouch/UnitArt.asset").Get("anuik");
        var sprites=new HashSet<Sprite>{art.Portrait};
        foreach(var clip in art.DirectionalAnimations)
            foreach(var sprite in clip.Idle.Concat(clip.Move).Concat(clip.Attack).Concat(clip.Death).Concat(clip.Revive))sprites.Add(sprite);
        var paths=sprites.Where(s=>s!=null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
        foreach(string asset in paths)PreservePixels((TextureImporter)AssetImporter.GetAtPath(asset));
        AssetDatabase.SaveAssets();
        return "Royal pixel background imported; Anuik keeps its original texture pixels, slices and animation references; "+paths.Length+" textures checked.";
    }
    static void PreservePixels(TextureImporter importer)
    {
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;
        foreach(string platform in new[]{"Android","Standalone"})
        {
            var settings=importer.GetPlatformTextureSettings(platform);
            settings.overridden=true;settings.maxTextureSize=4096;settings.format=TextureImporterFormat.RGBA32;
            settings.textureCompression=TextureImporterCompression.Uncompressed;settings.crunchedCompression=false;
            importer.SetPlatformTextureSettings(settings);
        }
        importer.SaveAndReimport();
    }
}

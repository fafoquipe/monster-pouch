using System;
using UnityEditor;
using UnityEngine;

public static class MonsterPouchInflatableImport
{
    public static string Import()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before importing art.");
        foreach(string name in new[]{"toy-inflatable-room","toy-inflatable-frame"})
        {
            string path="Assets/resources/MonsterPouch/UI/TableSkins/"+name+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;
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
        AssetDatabase.SaveAssets();return "Inflatable toy park and native frame slices imported with pixel-art settings.";
    }
}

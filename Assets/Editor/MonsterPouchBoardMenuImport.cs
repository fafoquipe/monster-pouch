using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Only the five new menu images are imported; existing character art is untouched.
public static class MonsterPouchBoardMenuImport
{
    public static string Apply()
    {
        var report=new StringBuilder();
        foreach(string id in new[]{"shop","brief","battle","guide","challenges"})
        {
            string path="Assets/resources/MonsterPouch/UI/BoardMenu/"+id+".png";
            if(!File.Exists(path))throw new FileNotFoundException("Missing menu illustration",path);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true;
            importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.crunchedCompression=false;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=2048;
            importer.isReadable=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.spriteAlignment=(int)SpriteAlignment.Center;
            settings.spritePivot=Vector2.one*.5f;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.texture.filterMode!=FilterMode.Point)
                throw new InvalidDataException("Incorrect menu import: "+id);
            report.AppendLine(id+": "+sprite.rect.size+", full rectangle, transparent alpha, point filtered.");
        }
        Directory.CreateDirectory("Logs/local-validation/board-navigation");
        File.WriteAllText("Logs/local-validation/board-navigation/import.txt",report.ToString());
        return report.ToString();
    }
}

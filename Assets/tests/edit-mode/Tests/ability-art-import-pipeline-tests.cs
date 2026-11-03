using System;
using System.IO;
using System.Linq;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AbilityArtImportPipelineTests
{
    const string Root="Assets/resources/MonsterPouch/UI/AbilityArt/";
    const ImportAssetOptions Import=ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport;

    [Test] public void DeclaredBasicAndOverrideSpritesLoadAfterForcedReimport()
    {
        var catalog=JsonUtility.FromJson<AbilityUiManifest>(File.ReadAllText(Root+"catalog.json"));
        var entries=catalog.entries.Where(e=>!string.IsNullOrEmpty(e.resource)).ToArray();
        Assert.GreaterOrEqual(entries.Count(e=>e.key.EndsWith("/basic",StringComparison.Ordinal)),24);
        foreach(var entry in entries)
        {
            string path="Assets/resources/"+entry.resource+".png";
            var before=AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(before,entry.key+" importer path");
            string guid=AssetDatabase.AssetPathToGUID(path);
            float ppu=before.spritePixelsPerUnit;Vector2 pivot=before.spritePivot;
            // The real AssetPostprocessor runs here; checking YAML alone is insufficient.
            AssetDatabase.ImportAsset(path,Import);
            var after=(TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterType.Sprite,after.textureType,entry.key+" must remain Sprite after the import pipeline");
            Assert.AreEqual(guid,AssetDatabase.AssetPathToGUID(path),entry.key+" GUID");
            Assert.AreEqual(ppu,after.spritePixelsPerUnit,entry.key+" PPU");
            Assert.AreEqual(pivot,after.spritePivot,entry.key+" pivot");
            Sprite sprite=string.IsNullOrEmpty(entry.spriteName)?Resources.Load<Sprite>(entry.resource):
                Resources.LoadAll<Sprite>(entry.resource).FirstOrDefault(s=>s.name==entry.spriteName);
            Assert.IsNotNull(sprite,entry.key+" Resources.Load Sprite after forced reimport");
        }
    }

    [Test] public void LegacyDynamicAbilityAtlasesRemainTexturesAfterForcedReimport()
    {
        var paths=Directory.GetFiles(Root,"abilities-*.png",SearchOption.TopDirectoryOnly);
        Assert.IsNotEmpty(paths);
        foreach(string file in paths)
        {
            string path=file.Replace('\\','/');
            string resource=path.Substring("Assets/resources/".Length);
            resource=resource.Substring(0,resource.Length-4);
            string guid=AssetDatabase.AssetPathToGUID(path);
            AssetDatabase.ImportAsset(path,Import);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterType.Default,importer.textureType,path);
            Assert.AreEqual(guid,AssetDatabase.AssetPathToGUID(path),path+" GUID");
            Assert.IsNotNull(Resources.Load<Texture2D>(resource),path+" dynamic Sprite.Create source");
            Assert.AreEqual(FilterMode.Point,importer.filterMode,path);
            Assert.IsFalse(importer.mipmapEnabled,path);
            Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression,path);
        }
    }
}

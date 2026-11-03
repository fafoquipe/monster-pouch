using UnityEditor;
using UnityEngine;

public sealed class MonsterPouchHealthBarImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/resources/MonsterPouch/UI/HealthBars/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
        importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
    public static void Import()
    {
        foreach(string id in new[]{"classic","lounge","royal"})
            AssetDatabase.ImportAsset("Assets/resources/MonsterPouch/UI/HealthBars/"+id+"-v1.png",ImportAssetOptions.ForceUpdate);
    }
}

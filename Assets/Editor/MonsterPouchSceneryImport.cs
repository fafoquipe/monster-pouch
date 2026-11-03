using UnityEditor;
using UnityEngine;

// Preserve the coarse pixels on Android as well as in the editor.
public sealed class MonsterPouchSceneryImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/MonsterPouch/UI/TableSkins/wings-",System.StringComparison.OrdinalIgnoreCase))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
        importer.wrapMode=TextureWrapMode.Clamp;
    }
}

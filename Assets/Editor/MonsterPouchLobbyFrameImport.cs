using UnityEditor;
using UnityEngine;

// Keep authored pixel rectangles exact on desktop and mobile. No packing or
// downsampling may change the atlas coordinates stored in clips.json.
public sealed class MonsterPouchLobbyFrameImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.Contains("/UI/LobbyFrames/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.mipmapEnabled=false;
        // Registered frames may land between screen pixels on different phones.
        // Bilinear sampling prevents outline shimmer without blending poses.
        importer.filterMode=FilterMode.Bilinear;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=4096;
        importer.isReadable=false;
        foreach(string platform in new[]{"Android","iPhone","Standalone"})
            importer.ClearPlatformTextureSettings(platform);
    }
}

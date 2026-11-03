using UnityEditor;
using UnityEngine;
// Import the reference idle and its authored gestures without resampling their source PNGs.
public sealed class MonsterPouchLobbyGestureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.Contains("/UI/LobbyGestures/"))return;
        var t=(TextureImporter)assetImporter;
        t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;
        // Subpixel sampling prevents one-screen-pixel stepping as articulated
        // hands rotate. The source texture, dimensions and idle pixels are kept.
        t.filterMode=assetPath.EndsWith("-idle.png")?FilterMode.Bilinear:FilterMode.Point;
        t.wrapMode=TextureWrapMode.Clamp;t.npotScale=TextureImporterNPOTScale.None;
        t.alphaIsTransparency=true;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=4096;
    }
}

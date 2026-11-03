using UnityEditor;
using UnityEngine;

public sealed class MonsterPouchTableArtImport:AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/resources/MonsterPouch/UI/TableSkins/")&&
           !assetPath.StartsWith("Assets/resources/MonsterPouch/UI/LobbyRigs/"))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Sprite;
        texture.spriteImportMode=SpriteImportMode.Single;
        texture.spritePixelsPerUnit=100;
        texture.npotScale=TextureImporterNPOTScale.None;
        texture.maxTextureSize=2048;
        texture.mipmapEnabled=false;
        texture.filterMode=assetPath.Contains("/LobbyRigs/")?FilterMode.Bilinear:FilterMode.Point;
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.alphaSource=TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency=true;
        texture.textureCompression=TextureImporterCompression.Uncompressed;
        texture.isReadable=false;
        if(assetPath.Contains("/TableSkins/"))
        {
            var android=texture.GetPlatformTextureSettings("Android");android.name="Android";android.overridden=true;
            android.maxTextureSize=2048;android.format=texture.DoesSourceTextureHaveAlpha()?TextureImporterFormat.ETC2_RGBA8:TextureImporterFormat.ETC2_RGB4;
            android.textureCompression=TextureImporterCompression.CompressedHQ;android.compressionQuality=100;
            texture.SetPlatformTextureSettings(android);
        }
        var settings=new TextureImporterSettings();texture.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;texture.SetTextureSettings(settings);
    }
}

using UnityEditor;
using UnityEngine;

public sealed class MonsterPouchCombatHudArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/resources/MonsterPouch/UI/CombatIcons/combat-status-atlas.png") return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.isReadable = false;
    }
}

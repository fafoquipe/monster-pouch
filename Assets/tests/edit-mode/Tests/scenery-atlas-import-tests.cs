using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

public sealed class SceneryAtlasImportTests
{
    [Test] public void ParkAtlasPreservesPixelFilteringAndAndroidCompression()
    {
        AssertPixelAtlas("Assets/art/ambientations/modular/park-pieces.spriteatlasv2");
    }

    [TestCase("brief-classic-cabinet-v2")]
    [TestCase("brief-lounge-cabinet-v2")]
    [TestCase("brief-royal-cabinet-v2")]
    [TestCase("casino-accessories-props")]
    [TestCase("royal-props")]
    [TestCase("furniture-props")]
    public void ModularAtlasPreservesPixelFilteringAndAndroidCompression(string sheet)
    {
        AssertPixelAtlas("Assets/art/ambientations/modular/" + sheet + ".spriteatlasv2");
    }

    static void AssertPixelAtlas(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
        Assert.IsNotNull(importer, path);
        Assert.IsTrue(importer.includeInBuild);
        Assert.AreEqual(FilterMode.Point, importer.textureSettings.filterMode);
        Assert.IsFalse(importer.textureSettings.generateMipMaps);
        Assert.IsFalse(importer.textureSettings.readable);
        Assert.IsFalse(importer.packingSettings.enableRotation);
        Assert.IsFalse(importer.packingSettings.enableTightPacking);
        var android = importer.GetPlatformSettings("Android");
        Assert.IsTrue(android.overridden);
        Assert.AreEqual(TextureImporterFormat.ETC2_RGBA8, android.format);
        Assert.AreEqual(2048, android.maxTextureSize);
    }
}

using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MonsterPouch.Local;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LoadingArtworkImportTests
{
    const string AssetPath="Assets/resources/MonsterPouch/UI/Branding/loading-screen-supplied.png";
    const string SuppliedHash="A72CDCD49C5A7D164C94E25884A8AFE960ABBEEBF9D716DF710DF5FF10CC1143";

    [Test] public void EronyaLogoUsesTheOriginalTransparentPngAndPreservesEveryVisibleEdge()
    {
        const string path="Assets/resources/MonsterPouch/UI/Branding/eronya-logo.png";
        using(var hash=SHA256.Create())
            Assert.AreEqual("72B855F77F68AAA40484F748F241D9EB1950B2475D7D036F5F1C3A069754718E",
                string.Concat(hash.ComputeHash(File.ReadAllBytes(path)).Select(b=>b.ToString("X2"))));
        var logo=SuppliedBranding.Logo;
        Assert.IsNotNull(logo);Assert.AreSame(logo,SuppliedBranding.Logo);
        Assert.AreEqual(1536,logo.texture.width);Assert.AreEqual(1024,logo.texture.height);
        Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(path),logo.texture);
        var pixels=logo.texture.GetPixels32();var rect=logo.rect;
        Assert.Less(rect.height,800,"Transparent padding must not shrink the readable logo.");
        Assert.Greater(rect.width,1400,"Retain the leaves and flames at both sides.");
        Assert.IsTrue(pixels.Any(p=>p.a==0),"The logo must keep real transparency.");
        for(int y=0;y<logo.texture.height;y++)for(int x=0;x<logo.texture.width;x++)
            if(pixels[y*logo.texture.width+x].a>=4 && !rect.Contains(new Vector2(x,y)))
                Assert.Fail("Visible logo pixel was cropped at "+x+","+y);
        Assert.AreEqual(4,logo.vertices.Length);
    }

    [Test] public void ImportedLoadingSpriteIsTheExactSuppliedImageAtFullResolution()
    {
        using(var hash=SHA256.Create())
            Assert.AreEqual(SuppliedHash,string.Concat(hash.ComputeHash(File.ReadAllBytes(AssetPath)).Select(b=>b.ToString("X2"))),"Do not redraw, crop, re-encode or replace the supplied artwork.");
        Assert.AreEqual("MonsterPouch/UI/Branding/loading-screen-supplied",LoadingArtworkScreen.ResourcePath);
        var resource=Resources.Load<Sprite>(LoadingArtworkScreen.ResourcePath);
        Assert.IsNotNull(resource);Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath),resource);
        Assert.AreEqual(new Rect(0,0,1330,1182),resource.rect);
        Assert.AreEqual(1330,resource.texture.width);Assert.AreEqual(1182,resource.texture.height);
        Assert.AreEqual(FilterMode.Point,resource.texture.filterMode);
        Assert.AreEqual(1,resource.texture.mipmapCount);
        Assert.AreEqual(4,resource.vertices.Length,"The single sprite must retain its complete rectangular artwork.");
        Assert.That(resource.uv.Min(p=>p.x),Is.EqualTo(0).Within(.00001f));Assert.That(resource.uv.Max(p=>p.x),Is.EqualTo(1).Within(.00001f));
        Assert.That(resource.uv.Min(p=>p.y),Is.EqualTo(0).Within(.00001f));Assert.That(resource.uv.Max(p=>p.y),Is.EqualTo(1).Within(.00001f));
    }

    [Test] public void AndroidBootUsesTheSuppliedArtworkWithUniformFill()
    {
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath),settings.FindProperty("androidSplashScreen").objectReferenceValue);
        Assert.AreEqual(AndroidSplashScreenScale.ScaleToFill,PlayerSettings.Android.splashScreenScale);
        Assert.AreEqual(Color.black,PlayerSettings.SplashScreen.backgroundColor);
    }

    [Test] public void ImporterPreservesPixelArtForDesktopAndroidAndIosWithoutResizingOrCompression()
    {
        var importer=AssetImporter.GetAtPath(AssetPath) as TextureImporter;Assert.IsNotNull(importer);
        Assert.AreEqual(TextureImporterType.Sprite,importer.textureType);Assert.AreEqual(SpriteImportMode.Single,importer.spriteImportMode);
        Assert.AreEqual(FilterMode.Point,importer.filterMode);Assert.IsFalse(importer.mipmapEnabled);
        Assert.AreEqual(TextureImporterNPOTScale.None,importer.npotScale);
        Assert.AreEqual(TextureImporterAlphaSource.FromInput,importer.alphaSource);Assert.IsTrue(importer.alphaIsTransparency);
        Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);Assert.IsFalse(importer.crunchedCompression);
        Assert.GreaterOrEqual(importer.maxTextureSize,1330);
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        Assert.AreEqual(SpriteMeshType.FullRect,settings.spriteMeshType);
        foreach(string platform in new[]{"Standalone","Android","iPhone"})
        {
            var configured=importer.GetPlatformTextureSettings(platform);
            var effective=configured.overridden?configured:importer.GetDefaultPlatformTextureSettings();
            Assert.AreEqual(TextureImporterCompression.Uncompressed,effective.textureCompression,platform);
            Assert.IsFalse(effective.crunchedCompression,platform);Assert.GreaterOrEqual(effective.maxTextureSize,1330,platform);
        }
    }

    [TestCase(320,568)]
    [TestCase(393,852)]
    [TestCase(1080,2400)]
    [TestCase(768,1024)]
    [TestCase(1536,2048)]
    [TestCase(1024,768)]
    [TestCase(1920,1080)]
    [TestCase(2560,1600)]
    [TestCase(2520,1080)]
    [TestCase(3440,1440)]
    [TestCase(5120,1440)]
    public void CoverGeometryPreservesProportionAndOnlyAllowsSideMarginsBeyond21By9(float width,float height)
    {
        var source=new Vector2(1330,1182);
        var fit=LoadingArtworkScreen.CoverArtwork(new Vector2(width,height),source);
        float coveredWidth=Mathf.Min(width,height*LobbyArtwork.MaximumCoverAspect);
        Assert.That(fit.width/fit.height,Is.EqualTo(source.x/source.y).Within(.00001f));
        float focusShift=Mathf.Min(fit.width*.05f,Mathf.Max(0,fit.width-width)*.5f);
        Assert.That(fit.center.x,Is.EqualTo(width*.5f-focusShift).Within(.001f));Assert.That(fit.center.y,Is.EqualTo(height*.5f).Within(.001f));
        Assert.LessOrEqual(fit.xMin,(width-coveredWidth)*.5f+.001f);
        Assert.GreaterOrEqual(fit.xMax,(width+coveredWidth)*.5f-.001f);
        Assert.LessOrEqual(fit.yMin,.001f);Assert.GreaterOrEqual(fit.yMax,height-.001f);
        if(width>height*LobbyArtwork.MaximumCoverAspect)
            Assert.That(fit.width,Is.EqualTo(coveredWidth).Within(.001f));
        // Both central faces remain on-screen through the supported portrait/landscape crops.
        foreach(var point in new[]{new Vector2(560,1182-470),new Vector2(675,1182-625),new Vector2(800,1182-435),new Vector2(955,1182-620)})
        {
            var pixel=fit.position+point*(fit.width/source.x);
            Assert.That(pixel.x,Is.InRange(0,width));Assert.That(pixel.y,Is.InRange(0,height));
        }
    }
}

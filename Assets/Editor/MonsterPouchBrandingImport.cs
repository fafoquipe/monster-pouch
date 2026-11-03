using System.IO;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

public sealed class MonsterPouchBrandingImport : AssetPostprocessor
{
    const string Source="Assets/resources/MonsterPouch/UI/Branding/supplied-logo-and-icon.png";
    const string IconFolder="Assets/art/branding";
    void OnPreprocessTexture()
    {
        if(assetPath!=Source)return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
        foreach(var platform in new[]{"Android","Standalone","iPhone"})importer.ClearPlatformTextureSettings(platform);
    }

    [MenuItem("Monster Pouch/Aplicar logo e icono suministrados")]
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Stop Play Mode before saving branding.");
        AssetDatabase.ImportAsset(Source,ImportAssetOptions.ForceUpdate);
        Directory.CreateDirectory(IconFolder);
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        source.LoadImage(File.ReadAllBytes(Source));
        var rect=SuppliedBranding.IconRect;
        // Import the supplied right-hand tile intact; never redraw the logo or icon.
        var legacy=SaveTexture("launcher-icon",(int)rect.width,source.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height));
        Object.DestroyImmediate(source);
        var foregroundPixels=new Color[1080*1080];
        // Fill the launcher mask; its outer border may bleed into the mask edges.
        const int contentSize=920,offset=(1080-contentSize)/2;
        for(int y=0;y<contentSize;y++)for(int x=0;x<contentSize;x++)
            foregroundPixels[(y+offset)*1080+x+offset]=legacy.GetPixelBilinear((x+.5f)/contentSize,(y+.5f)/contentSize);
        var foreground=SaveTexture("launcher-adaptive-foreground",1080,foregroundPixels);
        var backgroundPixels=new Color[1080*1080];
        for(int i=0;i<backgroundPixels.Length;i++)backgroundPixels[i]=new Color(.04f,.075f,.30f,1);
        var background=SaveTexture("launcher-adaptive-background",1080,backgroundPixels);
        var target=NamedBuildTarget.Android;
        foreach(var kind in new[]{AndroidPlatformIconKind.Legacy,AndroidPlatformIconKind.Round,AndroidPlatformIconKind.Adaptive})
        {
            var icons=PlayerSettings.GetPlatformIcons(target,kind);
            foreach(var icon in icons)
            {
                if(kind==AndroidPlatformIconKind.Adaptive)icon.SetTextures(new[]{background,foreground});
                else icon.SetTexture(legacy);
            }
            PlayerSettings.SetPlatformIcons(target,kind,icons);
        }
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown,new[]{legacy},IconKind.Any);
        PlayerSettings.Android.renderOutsideSafeArea=true;
        PlayerSettings.Android.startInFullscreen=true;
        PlayerSettings.Android.fullscreenMode=FullScreenMode.FullScreenWindow;
        AssetDatabase.SaveAssets();
        return "Original logo imported; supplied right-hand icon assigned to legacy, round and adaptive Android slots; full-screen safe-area rendering enabled.";
    }
    static Texture2D SaveTexture(string name,int size,Color[] pixels)
    {
        string path=IconFolder+"/"+name+".asset";
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null){texture=new Texture2D(size,size,TextureFormat.RGBA32,false);AssetDatabase.CreateAsset(texture,path);}
        texture.name=name;texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
        texture.SetPixels(pixels);texture.Apply(false,false);EditorUtility.SetDirty(texture);
        return texture;
    }
}

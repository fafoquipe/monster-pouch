using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// Applies the creator's supplied image only while building the dedicated DEV APK.
public sealed class MonsterPouchDeveloperBuildIcon : AssetPostprocessor
{
    public const string Source = "Assets/art/branding/dev-build-icon.png";
    public const string Overlay = "Assets/art/branding/dev-build-icon-transparent.asset";
    void OnPreprocessTexture()
    {
        if (assetPath != Source) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (var platform in new[] { "Android", "Standalone", "iPhone" }) importer.ClearPlatformTextureSettings(platform);
    }
    public static string Prepare()
    {
        AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(Source) == null) throw new InvalidOperationException("Falta el icono DEV aprobado.");
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(Overlay) == null)
        {
            var empty = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            empty.name = "DEV icon transparent layer";
            empty.SetPixels(new Color[4]); empty.Apply();
            AssetDatabase.CreateAsset(empty, Overlay);
        }
        AssetDatabase.SaveAssets();
        return "Icono DEV original preparado; ajustes del icono general conservados.";
    }
    public static IDisposable UseForBuild(bool dedicatedDeveloperBuild)
    {
        if (!dedicatedDeveloperBuild) return null;
        Prepare();
        var snapshot = new IconScope();
        try { snapshot.Apply(); return snapshot; }
        catch { snapshot.Dispose(); throw; }
    }
    sealed class IconScope : IDisposable
    {
#pragma warning disable 0618
        static readonly PlatformIconKind[] Kinds = { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Adaptive };
#pragma warning restore 0618
        readonly Texture2D[] general = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        readonly PlatformIcon[][] slots = Kinds.Select(k => PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, k)).ToArray();
        readonly Texture2D[][][] textures;
        bool disposed;
        internal IconScope() { textures = slots.Select(group => group.Select(slot => slot.GetTextures().ToArray()).ToArray()).ToArray(); }
        internal void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Source);
            var transparent = AssetDatabase.LoadAssetAtPath<Texture2D>(Overlay);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { source }, IconKind.Any);
            for (int group = 0; group < Kinds.Length; group++)
            {
                foreach (var slot in slots[group])
                    if (Kinds[group] == AndroidPlatformIconKind.Adaptive) slot.SetTextures(new[] { source, transparent });
                    else slot.SetTexture(source);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, Kinds[group], slots[group]);
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            for (int group = 0; group < Kinds.Length; group++)
            {
                for (int slot = 0; slot < slots[group].Length; slot++) slots[group][slot].SetTextures(textures[group][slot]);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, Kinds[group], slots[group]);
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, general, IconKind.Any);
            disposed = true;
        }
    }
}

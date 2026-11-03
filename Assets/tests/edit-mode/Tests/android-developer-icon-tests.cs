using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

public sealed class AndroidDeveloperIconTests
{
    static Type Adapter => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("MonsterPouchDeveloperBuildIcon")).First(t => t != null);
    static object Call(string method, params object[] args) => Adapter.GetMethod(method).Invoke(null, args);
#pragma warning disable 0618
    static readonly PlatformIconKind[] Kinds = { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Adaptive };
#pragma warning restore 0618
    static string Signature() => string.Join("|", PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any).Select(AssetDatabase.GetAssetPath)) + "#" +
        string.Join("#", Kinds.Select(k => string.Join("|", PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, k)
            .Select(icon => string.Join(",", icon.GetTextures().Select(AssetDatabase.GetAssetPath))))));

    [Test] public void NormalBuildDoesNotChangeIcons()
    {
        string before = Signature(); Assert.IsNull(Call("UseForBuild", false)); Assert.AreEqual(before, Signature());
    }
    [Test] public void DedicatedDevUsesExactImageAndRestoresEverySlotEvenAfterBuildFailure()
    {
        string before = Signature(); IDisposable scope = null;
        string source = (string)Adapter.GetField("Source").GetRawConstantValue();
        try
        {
            scope = (IDisposable)Call("UseForBuild", true);
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(source);
            Assert.IsNotNull(image); Assert.AreEqual(1254, image.width); Assert.AreEqual(1254, image.height);
            var importer = (TextureImporter)AssetImporter.GetAtPath(source);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression); Assert.IsFalse(importer.mipmapEnabled);
            CollectionAssert.AreEqual(new[] { image }, PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any));
            foreach (var kind in Kinds)
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind); Assert.IsNotEmpty(slots);
                foreach (var icon in slots)
                {
                    Assert.AreSame(image, icon.GetTexture(0));
                    if (kind == AndroidPlatformIconKind.Adaptive)
                    {
                        Assert.AreEqual(2, icon.GetTextures().Length);
                        Assert.IsTrue(icon.GetTexture(1).GetPixels().All(pixel => pixel.a == 0));
                    }
                }
            }
            throw new IOException("Synthetic build failure");
        }
        catch (IOException error) { Assert.AreEqual("Synthetic build failure", error.Message); }
        finally { scope?.Dispose(); }
        Assert.AreEqual(before, Signature()); scope.Dispose(); Assert.AreEqual(before, Signature());
    }
}

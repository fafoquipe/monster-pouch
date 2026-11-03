using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEditor;

public sealed class AndroidDeveloperBuildRecipeTests
{
    static Type Recipe=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchAndroidDeveloperVariant")).First(t=>t!=null);
    static object Call(string method,params object[] args)=>Recipe.GetMethod(method,BindingFlags.Public|BindingFlags.Static).Invoke(null,args);

    [Test] public void TimestampUsesLaPazAcrossUtcDateBoundaryAndOutputDirectoriesCannotCollide()
    {
        var instant=new DateTimeOffset(2026,10,24,1,2,3,TimeSpan.Zero);
        Assert.AreEqual("2026-10-23_21-02-03",Call("Timestamp",instant));
        string first=(string)Call("OutputDirectory",Path.GetTempPath(),instant,"first");
        string second=(string)Call("OutputDirectory",Path.GetTempPath(),instant,"second");
        Assert.AreNotEqual(first,second);StringAssert.Contains(Path.Combine("Builds","ANDROID DEVBUILD"),first);
        Assert.IsFalse(Path.GetFileName(first).Any(c=>Path.GetInvalidFileNameChars().Contains(c)));
        Assert.AreEqual("com.monsterpouch.game.devbuild",Call("ApplicationIdentifier","com.monsterpouch.game"));
        Assert.AreEqual("com.monsterpouch.game.devbuild",Call("ApplicationIdentifier","com.monsterpouch.game.devbuild"));
    }

    [Test] public void EveryDevelopmentRecipeAddsUnlockDefineAndReleaseNeverDoes()
    {
        var dev=(BuildPlayerOptions)Call("Options","scene","out.apk",true,true);
        Assert.AreEqual(BuildTarget.Android,dev.target);
        Assert.IsTrue((dev.options&BuildOptions.Development)!=0);
        CollectionAssert.AreEqual(new[]{AndroidDeveloperContent.Define},dev.extraScriptingDefines);
        foreach(bool development in new[]{false,true})
        {
            var normal=(BuildPlayerOptions)Call("Options","scene","out.apk",development,false);
            CollectionAssert.AreEqual(development?new[]{AndroidDeveloperContent.Define}:Array.Empty<string>(),normal.extraScriptingDefines);
            Assert.AreEqual(development,(normal.options&BuildOptions.Development)!=0);
        }
        var error=Assert.Throws<TargetInvocationException>(()=>Call("Options","scene","out.apk",false,true));
        Assert.IsInstanceOf<ArgumentException>(error.InnerException);
    }

    [Test] public void DevelopmentRequestDefaultsToIsolatedUnlocksAndReleaseRequestStaysNormal()
    {
        var builder=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchAndroidBuild")).First(t=>t!=null);
        var requestType=builder.GetNestedType("Request",BindingFlags.Public);
        var normalize=builder.GetMethod("Normalize",BindingFlags.NonPublic|BindingFlags.Static);
        foreach(bool development in new[]{false,true})
        {
            var request=Activator.CreateInstance(requestType);
            requestType.GetField("applicationIdentifier").SetValue(request,"com.monsterpouch.fixture");
            requestType.GetField("development").SetValue(request,development);
            requestType.GetField("unlockAllContent").SetValue(request,false);
            requestType.GetField("outputDirectory").SetValue(request,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")));
            var normalized=normalize.Invoke(null,new[]{request});
            Assert.AreEqual(development,requestType.GetField("unlockAllContent").GetValue(normalized));
            Assert.AreEqual("com.monsterpouch.fixture"+(development?".devbuild":""),requestType.GetField("applicationIdentifier").GetValue(normalized));
        }
    }

    [Test] public void NewMenuButtonAndItsValidationAreRegisteredWithoutInvokingABuild()
    {
        var builder=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchAndroidBuild")).First(t=>t!=null);
        string menu=(string)Recipe.GetField("MenuPath").GetRawConstantValue();
        var attributes=builder.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
            .SelectMany(m=>m.GetCustomAttributes<MenuItem>()).Where(a=>a.menuItem==menu).ToArray();
        Assert.AreEqual(2,attributes.Length);
        Assert.AreEqual(1,attributes.Count(a=>a.validate));
        Assert.AreEqual(1,attributes.Count(a=>!a.validate));
    }
}

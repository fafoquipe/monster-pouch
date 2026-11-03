using System;
using System.IO;
using MonsterPouch.Gameplay.Progression;
using NUnit.Framework;
using UnityEngine;

public sealed class LocalProgressReadTests
{
    string directory;
    LocalProgressStore store;
    ProgressionConfig config;
    string Primary=>Path.Combine(directory,"progress.json");
    string Backup=>Path.Combine(directory,"progress.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchProgressReadTests",Guid.NewGuid().ToString("N"));
        store=new LocalProgressStore(directory);config=ScriptableObject.CreateInstance<ProgressionConfig>();
        config.ExperiencePerMatch=3;config.ExperiencePerWin=2;config.ExperiencePerLevel=new[]{100};
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(config);
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchProgressReadTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [Test] public void ReadingAbsentProgressDoesNotCreateAProfileOrImportAnything()
    {
        Assert.IsNull(store.ReadExisting());Assert.IsFalse(store.NeedsRecovery);
        Assert.IsFalse(File.Exists(Primary));Assert.IsFalse(File.Exists(Backup));
        Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp"));
    }
    [Test] public void ReadingACommittedReceiptDoesNotAwardOrRewriteIt()
    {
        store.LoadOrImport("");store.Award(config,"bo5-session",true,out _);
        string primary=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var read=store.ReadExisting();Assert.AreEqual(5,read.Experience);
        CollectionAssert.AreEqual(new[]{"bo5-session"},read.CompletedMatches);
        read.CompletedMatches.Clear();read.Experience=1000;
        var reread=new LocalProgressStore(directory).ReadExisting();
        Assert.AreEqual(5,reread.Experience);CollectionAssert.AreEqual(new[]{"bo5-session"},reread.CompletedMatches);
        Assert.AreEqual(primary,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void ReadOnlyRecoveryReportsAnOlderGenerationWithoutRepairingThePrimary()
    {
        store.LoadOrImport("");store.Award(config,"older",false,out _);store.Award(config,"latest",true,out _);
        string backup=File.ReadAllText(Backup);File.WriteAllText(Primary,"damaged");
        var recovered=store.ReadExisting();Assert.IsTrue(store.NeedsRecovery);
        CollectionAssert.AreEqual(new[]{"older"},recovered.CompletedMatches);
        Assert.AreEqual("damaged",File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
}

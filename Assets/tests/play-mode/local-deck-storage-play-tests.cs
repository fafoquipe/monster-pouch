using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class LocalDeckStoragePlayTests
{
    static readonly PropertyInfo DirectoryOverride=typeof(LocalGameUI).GetProperty("DeckDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic);
    readonly string[] keys={"monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string directory,previousDirectory,legacy;
    string[] saved;
    bool[] existed;
    LocalGameUI ui;
    string Primary=>Path.Combine(directory,"decks.json");
    [UnitySetUp] public IEnumerator Setup()
    {
        previousDirectory=(string)DirectoryOverride.GetValue(null);
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchDeckPlayTests",Guid.NewGuid().ToString("N"));
        DirectoryOverride.SetValue(null,directory);
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var library=DeckLibrary.Restore(null,catalogue,"anuik",new[]{"stein","kayon"});
        library.Active=1;library.Assign(-1,"popow",catalogue);
        library.Active=2;library.Assign(-1,"bugaloo",catalogue);library.Active=0;
        legacy=library.Serialize();UnityEngine.Object.Destroy(catalogue);
        PlayerPrefs.SetString(keys[0],legacy);PlayerPrefs.SetString(keys[1],"anuik");PlayerPrefs.SetString(keys[2],"stein,kayon");
        yield return LoadScene();
    }
    IEnumerator LoadScene()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();}
        yield return null;
        DirectoryOverride.SetValue(null,previousDirectory);
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchDeckPlayTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [UnityTest] public IEnumerator SelectingADeckSurvivesSceneReloadAndKeepsOriginalLegacyPreferences()
    {
        Assert.AreEqual(0,ui.ActiveDeck);
        ui.SelectDeck(2);yield return null;
        Assert.AreEqual(2,ui.ActiveDeck);
        Assert.AreEqual(legacy,PlayerPrefs.GetString(keys[0]));
        Assert.AreEqual("anuik",PlayerPrefs.GetString(keys[1]));
        Assert.AreEqual("stein,kayon",PlayerPrefs.GetString(keys[2]));
        yield return LoadScene();
        Assert.AreEqual(2,ui.ActiveDeck);
        var stored=new LocalDeckStore(directory).LoadOrImport("",ui.Match.Config,"anuik",null);
        Assert.AreEqual("bugaloo",stored.Current.Monster);
        Assert.AreEqual("anuik",stored.Decks[0].Monster);Assert.AreEqual("popow",stored.Decks[1].Monster);
    }
    [UnityTest] public IEnumerator LockedWriteKeepsPreviousDeckAndRetryCommitsTheRequestedSelection()
    {
        string before=File.ReadAllText(Primary);
        using(new FileStream(Path.Combine(directory,"decks.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            ui.SelectDeck(2);yield return null;
            Assert.AreEqual(0,ui.ActiveDeck);
            Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsNotNull(Retry());
        }
        Retry().onClick.Invoke();yield return null;
        Assert.AreEqual(2,ui.ActiveDeck);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="Deck storage notice"));
        yield return LoadScene();
        Assert.AreEqual(2,ui.ActiveDeck);
    }
    [UnityTest] public IEnumerator CorruptPrimaryDisplaysReadOnlyBackupAndSelectionCannotOverwriteIt()
    {
        ui.SelectDeck(1);yield return null;
        string backup=File.ReadAllText(Path.Combine(directory,"decks.backup.json"));
        File.WriteAllText(Primary,"corrupt primary");
        yield return LoadScene();
        Assert.AreEqual(0,ui.ActiveDeck);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(GameText.Spanish?"Se muestra un respaldo":"A backup is shown")));
        ui.SelectDeck(2);yield return null;Assert.AreEqual(0,ui.ActiveDeck);
        Retry().onClick.Invoke();yield return null;
        Assert.AreEqual("corrupt primary",File.ReadAllText(Primary));
        Assert.AreEqual(backup,File.ReadAllText(Path.Combine(directory,"decks.backup.json")));
        Assert.AreEqual(legacy,PlayerPrefs.GetString(keys[0]));
    }
    Button Retry()=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Retry deck storage");
}

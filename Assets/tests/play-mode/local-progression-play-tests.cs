using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Progression;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class LocalProgressionPlayTests
{
    const string ProgressKey="monster-pouch.progress.v1";
    static readonly PropertyInfo DirectoryOverride=typeof(LocalGameUI).GetProperty("ProgressionDirectoryOverride",BindingFlags.NonPublic|BindingFlags.Static);
    static readonly PropertyInfo[] AccountDirectories=new[]{"GuestProfileDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.NonPublic|BindingFlags.Static)).ToArray();
    string[] previousAccountDirectories;
    string directory,previousDirectory,previousLegacy,legacy;
    bool hadLegacy;
    LocalGameUI ui;
    string Primary=>Path.Combine(directory,"progress.json");
    [UnitySetUp] public IEnumerator Setup()
    {
        previousDirectory=(string)DirectoryOverride.GetValue(null);
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchProgressPlayTests",Guid.NewGuid().ToString("N"));
        DirectoryOverride.SetValue(null,directory);
        previousAccountDirectories=AccountDirectories.Select(p=>(string)p.GetValue(null)).ToArray();
        AccountDirectories[0].SetValue(null,Path.Combine(directory,"Guest"));AccountDirectories[1].SetValue(null,Path.Combine(directory,"Decks"));
        hadLegacy=PlayerPrefs.HasKey(ProgressKey);previousLegacy=PlayerPrefs.GetString(ProgressKey);
        var old=new PlayerProgress{Level=7,Experience=19};old.CompletedMatches.Add("legacy-match");old.Unlocks.Add("anuik");
        legacy=old.Serialize();PlayerPrefs.SetString(ProgressKey,legacy);
        yield return LoadScene();
    }
    IEnumerator LoadScene()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Pause(false);ui.Match.Menu();}
        yield return null;
        DirectoryOverride.SetValue(null,previousDirectory);
        for(int i=0;i<AccountDirectories.Length;i++)AccountDirectories[i].SetValue(null,previousAccountDirectories[i]);
        if(hadLegacy)PlayerPrefs.SetString(ProgressKey,previousLegacy);else PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchProgressPlayTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);
        Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [UnityTest] public IEnumerator CompletedBO5PersistsOnceAcrossCallbacksAndSceneReloadWithoutRewritingLegacy()
    {
        Assert.AreEqual(legacy,new LocalProgressStore(directory).LoadOrImport("").Serialize());
        var match=ui.Match;
        yield return WinMatch();
        Assert.AreEqual(MatchPhase.MatchResult,match.Phase);
        var expected=PlayerProgress.Restore(legacy);
        Assert.IsTrue(expected.Award(Resources.Load<ProgressionConfig>("MonsterPouch/Progression"),match.SessionId,true));
        Assert.AreEqual(expected.Serialize(),new LocalProgressStore(directory).LoadOrImport("").Serialize());
        string committed=File.ReadAllText(Primary);
        match.Pause(true);match.Pause(false);yield return null;
        Assert.AreEqual(committed,File.ReadAllText(Primary),"Repeated match-result notifications must not award again.");
        Assert.AreEqual(legacy,PlayerPrefs.GetString(ProgressKey),"The migration source remains a recovery copy.");
        yield return LoadScene();
        Assert.AreEqual(expected.Serialize(),new LocalProgressStore(directory).LoadOrImport("").Serialize());
        ui.ShowProgression();yield return null;
        var labels=ui.CanvasRoot.Find("Player progression").GetComponentsInChildren<Text>();
        Assert.IsTrue(labels.Any(t=>t.text==expected.Experience+" / "+Resources.Load<ProgressionConfig>("MonsterPouch/Progression").Requirement(expected.Level)+" XP"));
        Assert.IsTrue(labels.Any(t=>t.text==ui.Match.Config.Get("anuik").DisplayName));
    }
    [UnityTest] public IEnumerator FailedWriteRetriesAfterReturningToMenuAndCommitsOnlyOnce()
    {
        string before=File.ReadAllText(Primary),sessionId;
        using(var writer=new FileStream(Path.Combine(directory,"progress.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            yield return WinMatch();sessionId=ui.Match.SessionId;
            ui.Match.Menu();yield return null;
            Assert.AreEqual(before,File.ReadAllText(Primary));
        }
        ui.ShowProgression();yield return null;
        Retry().onClick.Invoke();yield return null;
        var expected=PlayerProgress.Restore(legacy);
        expected.Award(Resources.Load<ProgressionConfig>("MonsterPouch/Progression"),sessionId,true);
        Assert.AreEqual(expected.Serialize(),new LocalProgressStore(directory).LoadOrImport("").Serialize());
        string saved=File.ReadAllText(Primary);
        ui.Match.Pause(true);ui.Match.Pause(false);yield return null;
        Assert.AreEqual(saved,File.ReadAllText(Primary));
    }
    IEnumerator WinMatch()
    {
        var match=ui.Match;
        match.StartMatch("popow",20261030);Assert.AreEqual(MatchPhase.Preparation,match.Phase);yield return null;
        for(int win=1;win<=match.WinsToWin;win++)
        {
            match.Ready();
            for(int tick=0;tick<200&&match.Phase==MatchPhase.Combat;tick++)
            {
                foreach(var actor in match.Actors.Values.Concat(match.Simulation.CombatOnlyUnits)
                    .Where(a=>a!=null&&a.IsAlive&&a.Side==BoardSide.Red).Distinct().ToArray())
                    actor.ApplyDamage(actor.CurrentHealth);
                match.Advance(.1f);yield return null;
            }
            Assert.AreEqual(win,match.PlayerWins);
            if(win<match.WinsToWin){match.Advance(match.RoundTransitionDuration+.1f);yield return null;}
        }
        Assert.AreEqual(MatchPhase.MatchResult,match.Phase);
    }
    [UnityTest] public IEnumerator CorruptPrimaryDisplaysBackupAndRetryCannotOverwriteIt()
    {
        var store=new LocalProgressStore(directory);var config=Resources.Load<ProgressionConfig>("MonsterPouch/Progression");
        store.Award(config,"first",true,out _);store.Award(config,"latest",true,out _);
        string backup=File.ReadAllText(Path.Combine(directory,"progress.backup.json"));
        File.WriteAllText(Primary,"corrupt primary");
        yield return LoadScene();
        ui.ShowProgression();yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(GameText.Spanish?"Se muestra un respaldo":"A backup is shown")));
        Retry().onClick.Invoke();yield return null;
        Assert.AreEqual("corrupt primary",File.ReadAllText(Primary));
        Assert.AreEqual(backup,File.ReadAllText(Path.Combine(directory,"progress.backup.json")));
        Assert.AreEqual(legacy,PlayerPrefs.GetString(ProgressKey));
    }
    [UnityTest] public IEnumerator CorruptionWithoutBackupShowsAnErrorInsteadOfInventingEmptyProgress()
    {
        File.WriteAllText(Primary,"corrupt without backup");
        yield return LoadScene();
        ui.ShowProgression();yield return null;
        var panel=ui.CanvasRoot.Find("Player progression");
        Assert.IsTrue(panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(GameText.Spanish?"No se pudo cargar":"Progress could not be loaded")));
        Assert.IsFalse(panel.GetComponentsInChildren<Text>().Any(t=>t.text.EndsWith(" XP")),"A broken save must not be presented as zero XP.");
        Retry().onClick.Invoke();yield return null;
        Assert.AreEqual("corrupt without backup",File.ReadAllText(Primary));
        Assert.AreEqual(legacy,PlayerPrefs.GetString(ProgressKey));
    }
    Button Retry()=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>
        b.GetComponentInChildren<Text>()?.text==(GameText.Spanish?"REINTENTAR":"RETRY"));
}

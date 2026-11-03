using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Progression;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class LateTutorialReviewPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(name=>typeof(LocalGameUI).GetProperty(name,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    string root,guestBefore,guestFolder;
    string[] previousDirectories;
    LocalGameUI ui;
    string GuestDirectory=>Path.Combine(root,guestFolder);
    string ProgressDirectory=>Path.Combine(root,"PlayerProgress");
    string GuestPrimary=>Path.Combine(GuestDirectory,"guest-profile.json");
    string ProgressPrimary=>Path.Combine(ProgressDirectory,"progress.json");
    [UnitySetUp] public IEnumerator Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchLateTutorialReviewTests",Guid.NewGuid().ToString("N"));
        guestFolder="GuestProfile";
        previousDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,GuestDirectory);Overrides[1].SetValue(null,ProgressDirectory);
        Overrides[2].SetValue(null,Path.Combine(root,"PlayerDecks"));
        // Seed isolated stores rather than reading or changing player preferences.
        new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(true);guestBefore=File.ReadAllText(GuestPrimary);
        new LocalProgressStore(ProgressDirectory).LoadOrImport("");
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        new LocalDeckStore(Path.Combine(root,"PlayerDecks")).LoadOrImport(null,catalogue,"popow",StarterRoster.Whelps);
        UnityEngine.Object.Destroy(catalogue);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Pause(false);ui.Match.Menu();}
        yield return null;
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,previousDirectories[i]);
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchLateTutorialReviewTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [UnityTest] public IEnumerator NormalBo5ReviewVerifiesTheCommittedResultWithoutCompletingTutorialModules()
    {
        ui.ShowLateTutorialReview();ButtonNamed("Open BO5 review").onClick.Invoke();yield return null;
        ButtonNamed("Start BO5 review").onClick.Invoke();yield return null;
        Assert.IsFalse(ui.Match.IsTraining);Assert.IsFalse(ui.Match.IsOnline);
        string session=ui.Match.SessionId;Assert.IsNotEmpty(session);
        yield return WinCurrentMatch();
        Assert.IsTrue(new LocalProgressStore(ProgressDirectory).ReadExisting().CompletedMatches.Contains(session));
        string committed=File.ReadAllText(ProgressPrimary);
        ui.Match.Menu();ui.ShowNormalBo5Review();yield return null;
        StringAssert.Contains(GameText.Spanish?"Experiencia guardada":"Experience saved",Status);
        ui.ShowNormalBo5Review();yield return null;
        Assert.AreEqual(committed,File.ReadAllText(ProgressPrimary));Assert.AreEqual(guestBefore,File.ReadAllText(GuestPrimary));
        ButtonNamed("View BO5 experience").onClick.Invoke();yield return null;
        Assert.IsNotNull(ui.CanvasRoot.Find("Player progression"));
    }
    [UnityTest] public IEnumerator FailedXpSaveIsNotPresentedAsSavedAndTheExistingRetryCommitsIt()
    {
        ui.ShowNormalBo5Review();ButtonNamed("Start BO5 review").onClick.Invoke();yield return null;
        string before=File.ReadAllText(ProgressPrimary),session=ui.Match.SessionId;
        using(new FileStream(Path.Combine(ProgressDirectory,"progress.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            yield return WinCurrentMatch();ui.Match.Menu();yield return null;
            Assert.AreEqual(before,File.ReadAllText(ProgressPrimary));
        }
        ui.ShowNormalBo5Review();yield return null;
        StringAssert.Contains(GameText.Spanish?"aún no está guardada":"has not been saved",Status);
        ButtonNamed("View BO5 experience").onClick.Invoke();yield return null;
        ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text==
            (GameText.Spanish?"REINTENTAR":"RETRY")).onClick.Invoke();yield return null;
        ui.ShowNormalBo5Review();yield return null;
        StringAssert.Contains(GameText.Spanish?"Experiencia guardada":"Experience saved",Status);
        CollectionAssert.AreEqual(new[]{session},new LocalProgressStore(ProgressDirectory).ReadExisting().CompletedMatches);
        Assert.AreEqual(guestBefore,File.ReadAllText(GuestPrimary));
        // A later damaged primary must not continue displaying a success cached in UI memory.
        File.WriteAllText(ProgressPrimary,"damaged after result");ui.ShowNormalBo5Review();yield return null;
        StringAssert.Contains(GameText.Spanish?"necesita recuperarse":"needs recovery",Status);
        Assert.AreEqual("damaged after result",File.ReadAllText(ProgressPrimary));
    }
    [UnityTest] public IEnumerator AbortedReviewAndTrainingCannotProduceACompletedBo5Receipt()
    {
        string before=File.ReadAllText(ProgressPrimary);
        ui.ShowNormalBo5Review();ButtonNamed("Start BO5 review").onClick.Invoke();yield return null;
        ui.Match.Menu();
        Assert.IsTrue(ui.Match.StartTraining("popow",StarterRoster.Whelps));
        ui.Match.Advance(50);ui.Match.Menu();ui.ShowNormalBo5Review();yield return null;
        StringAssert.Contains(GameText.Spanish?"Al terminar la partida":"Finishing the match",Status);
        Assert.AreEqual(before,File.ReadAllText(ProgressPrimary));Assert.AreEqual(guestBefore,File.ReadAllText(GuestPrimary));
    }
    [UnityTest] public IEnumerator MultiplayerReviewOpensTheExistingFormsWithoutStartingANetworkSession()
    {
        string progressBefore=File.ReadAllText(ProgressPrimary);
        ui.ShowLateTutorialReview();ButtonNamed("Open multiplayer review").onClick.Invoke();yield return null;
        var online=ui.GetComponent<OnlineRoom>();Assert.IsNotNull(online);
        Assert.IsFalse(online.Active);Assert.IsFalse(online.Busy);
        ButtonNamed("Review open friend rooms").onClick.Invoke();yield return null;
        Assert.IsNotNull(ui.CanvasRoot.Find("Online room"));Assert.IsFalse(online.Active);Assert.IsFalse(online.Busy);
        ui.ShowMultiplayerReview();ButtonNamed("Review open room code").onClick.Invoke();yield return null;
        Assert.IsNotNull(ui.CanvasRoot.Find("Join room"));Assert.IsFalse(online.Active);Assert.IsFalse(online.Busy);
        Assert.IsFalse(ui.Match.IsOnline);Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
        Assert.AreEqual(progressBefore,File.ReadAllText(ProgressPrimary));Assert.AreEqual(guestBefore,File.ReadAllText(GuestPrimary));
    }
    [UnityTest] public IEnumerator NewGuestRequiresAnObservedOfflineBo5AndSavedXpBeforeMultiplayerReview()
    {
        // Create a separate new identity through the real store, with no manual
        // stage changes or tutorial completion receipts.
        guestFolder="NewGuestProfile";Overrides[0].SetValue(null,GuestDirectory);
        var guestStore=new LocalGuestProfileStore(GuestDirectory);guestStore.OpenOrCreate(false);
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,guestStore.ChooseStarter(catalogue,"popow"));
        UnityEngine.Object.Destroy(catalogue);guestBefore=File.ReadAllText(GuestPrimary);
        yield return ReloadNewGuestAndExitFirstContact();
        ui.ShowLateTutorialReview();yield return null;
        Assert.IsFalse(ButtonNamed("Open multiplayer review").interactable);
        // A queued callback/direct method cannot bypass the disabled button.
        ButtonNamed("Open multiplayer review").onClick.Invoke();ui.ShowMultiplayerReview();yield return null;
        Assert.IsNull(ui.CanvasRoot.Find("Multiplayer review"));
        ui.ShowNormalBo5Review();ButtonNamed("Start BO5 review").onClick.Invoke();yield return null;
        string session=ui.Match.SessionId;
        using(new FileStream(Path.Combine(ProgressDirectory,"progress.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {yield return WinCurrentMatch();ui.Match.Menu();yield return null;}
        ui.ShowLateTutorialReview();yield return null;
        Assert.IsFalse(ButtonNamed("Open multiplayer review").interactable,"Finishing without committed XP is insufficient.");
        ui.ShowProgression();yield return null;
        ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text==
            (GameText.Spanish?"REINTENTAR":"RETRY")).onClick.Invoke();yield return null;
        Assert.IsTrue(new LocalProgressStore(ProgressDirectory).ReadExisting().CompletedMatches.Contains(session));
        ui.ShowLateTutorialReview();yield return null;
        Assert.IsTrue(ButtonNamed("Open multiplayer review").interactable);
        ButtonNamed("Open multiplayer review").onClick.Invoke();yield return null;
        Assert.IsNotNull(ui.CanvasRoot.Find("Multiplayer review"));
        Assert.IsFalse(ui.GetComponent<OnlineRoom>().Active);Assert.IsFalse(ui.GetComponent<OnlineRoom>().Busy);
        Assert.AreEqual(guestBefore,File.ReadAllText(GuestPrimary));
        Assert.IsEmpty(guestStore.OpenOrCreate(false).CompletedTutorialModules);
        // The generic saved receipt survives, but its offline-review context is
        // intentionally session-only and cannot be reconstructed after reload.
        yield return ReloadNewGuestAndExitFirstContact();ui.ShowLateTutorialReview();yield return null;
        Assert.IsFalse(ButtonNamed("Open multiplayer review").interactable);
        Assert.IsTrue(new LocalProgressStore(ProgressDirectory).ReadExisting().CompletedMatches.Contains(session));
        Assert.AreEqual(guestBefore,File.ReadAllText(GuestPrimary));
    }
    IEnumerator ReloadNewGuestAndExitFirstContact()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        Assert.IsTrue(ui.FirstContactTutorialActive);
        ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
    }
    IEnumerator WinCurrentMatch()
    {
        var match=ui.Match;Assert.AreEqual(MatchPhase.Preparation,match.Phase);
        for(int win=1;win<=3;win++)
        {
            match.Ready();
            for(int tick=0;tick<200&&match.Phase==MatchPhase.Combat;tick++)
            {
                foreach(var actor in match.Actors.Values.Concat(match.Simulation.CombatOnlyUnits)
                    .Where(a=>a!=null&&a.IsAlive&&a.Side==BoardSide.Red).Distinct().ToArray())actor.ApplyDamage(actor.CurrentHealth);
                match.Advance(.1f);yield return null;
            }
            Assert.AreEqual(win,match.PlayerWins);
            if(win<3){match.Advance(match.RoundTransitionDuration+.1f);yield return null;}
        }
        Assert.AreEqual(MatchPhase.MatchResult,match.Phase);
    }
    Button ButtonNamed(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
    string Status=>ui.CanvasRoot.GetComponentsInChildren<Text>().Single(t=>t.name=="BO5 review saved experience").text;
}

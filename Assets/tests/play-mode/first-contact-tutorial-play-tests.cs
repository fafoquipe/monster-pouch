using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class FirstContactTutorialPlayTests
{
    static readonly string[] OverrideNames={"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"};
    static readonly PropertyInfo[] Overrides=OverrideNames.Select(name=>typeof(LocalGameUI).GetProperty(name,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    static readonly FieldInfo StepField=typeof(LocalGameUI).GetField("firstContactStep",BindingFlags.Instance|BindingFlags.NonPublic);
    readonly string[] keys={"monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;
    string[] previousDirectories,saved;
    bool[] existed;
    LocalGameUI ui;
    string GuestDirectory=>Path.Combine(root,"GuestProfile");
    string GuestPrimary=>Path.Combine(GuestDirectory,"guest-profile.json");
    string DeckPrimary=>Path.Combine(root,"PlayerDecks","decks.json");
    string ProgressPrimary=>Path.Combine(root,"PlayerProgress","progress.json");
    int Step=>(int)StepField.GetValue(ui);
    LocalGuestProfile ReadGuest()=>new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);

    [SetUp] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchFirstContactPlayTests",Guid.NewGuid().ToString("N"));
        previousDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,GuestDirectory);Overrides[1].SetValue(null,Path.Combine(root,"PlayerProgress"));
        Overrides[2].SetValue(null,Path.Combine(root,"PlayerDecks"));
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);
    }
    IEnumerator LoadScene()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var previousUI=ui;ui=null;
        if(previousUI!=null){previousUI.CloseModal();previousUI.Match.Menu();previousUI.gameObject.SetActive(false);UnityEngine.Object.Destroy(previousUI.gameObject);}
        yield return null;Assert.IsTrue(previousUI==null,"Destroy the fixture UI before removing its isolated profile.");
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,previousDirectories[i]);
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchFirstContactPlayTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }

    [UnityTest] public IEnumerator NewAccountUsesRealTapHoldAndDragWithoutCombatDeckChangesOrExperience()
    {
        yield return StartFromChoice();
        Assert.IsTrue(ui.Match.IsTraining);Assert.IsNull(ui.Match.SessionId);
        Assert.AreEqual(UnitLocation.Brief,ui.Match.Player.Monster.Location);
        Assert.IsFalse(ui.Match.Player.Offers.Any(o=>o!=null));
        Assert.IsFalse(ui.Match.Actors.Values.Any(a=>a.Side!=ui.Match.LocalSide));
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="LISTO"));
        string progress=File.ReadAllText(ProgressPrimary),deck=File.ReadAllText(DeckPrimary);
        float remaining=ui.Match.Remaining;ui.Match.Advance(1000);
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);Assert.AreEqual(remaining,ui.Match.Remaining);
        yield return PlaceByTap(new Vector2Int(1,7));Assert.AreEqual(1,Step);
        ui.ShowInspection(ui.Match.Player.Monster,false);yield return null;
        Assert.AreEqual(1,Step,"Opening an info card without a hold must not complete the gesture.");
        ui.CloseModal();yield return HoldMonster();Assert.AreEqual(2,Step);
        // A tap can move a unit normally, but this task explicitly verifies dragging.
        ui.Select(ui.Match.Player.Monster);yield return null;
        TapCell(new Vector2Int(4,7));yield return null;Assert.AreEqual(2,Step);
        var position=ui.Match.Player.Monster.Deployment;
        DragTo(new Vector2Int(2,1));yield return null;
        Assert.AreEqual(position,ui.Match.Player.Monster.Deployment);Assert.AreEqual(2,Step);
        DragTo(new Vector2Int(2,6));yield return null;yield return null;
        Assert.AreEqual(3,Step);CollectionAssert.AreEqual(new[]{"first-contact"},ReadGuest().CompletedTutorialModules);
        Assert.AreEqual(GuestOnboardingStage.Tutorial,ReadGuest().OnboardingStage);
        Assert.AreEqual("positioning",ReadGuest().NextTutorialModuleId);
        Assert.AreEqual(progress,File.ReadAllText(ProgressPrimary));Assert.AreEqual(deck,File.ReadAllText(DeckPrimary));
        string receipt=File.ReadAllText(GuestPrimary);
        ButtonNamed("Finish first contact tutorial").onClick.Invoke();yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);Assert.IsFalse(ui.FirstContactTutorialActive);
        Assert.IsTrue(ui.GuidedTutorialActive,"Continue enters the next module without another menu visit.");
        Assert.AreEqual(progress,File.ReadAllText(ProgressPrimary));
        ui.Match.Menu();yield return null;
        Assert.IsTrue(ui.StartFirstContactTutorial());yield return null;
        yield return PlaceByTap(new Vector2Int(1,7));yield return HoldMonster();
        DragTo(new Vector2Int(4,7));yield return null;yield return null;
        Assert.AreEqual(receipt,File.ReadAllText(GuestPrimary),"Repeating the practice must not create another completion receipt.");
        Assert.AreEqual(progress,File.ReadAllText(ProgressPrimary));Assert.AreEqual(deck,File.ReadAllText(DeckPrimary));
    }

    [UnityTest] public IEnumerator FailedCompletionSaveKeepsThePracticeCompleteAndRetriesWithoutRewards()
    {
        yield return StartFromChoice();yield return PlaceByTap(new Vector2Int(1,7));yield return HoldMonster();
        string guest=File.ReadAllText(GuestPrimary),progress=File.ReadAllText(ProgressPrimary),deck=File.ReadAllText(DeckPrimary);
        using(new FileStream(Path.Combine(GuestDirectory,"guest-profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            DragTo(new Vector2Int(4,7));yield return null;yield return null;
            Assert.AreEqual(3,Step);Assert.IsNotNull(ButtonNamed("Retry first contact tutorial"));
            Assert.AreEqual(guest,File.ReadAllText(GuestPrimary));Assert.AreEqual(progress,File.ReadAllText(ProgressPrimary));
            ButtonNamed("Retry first contact tutorial").onClick.Invoke();yield return null;
            Assert.AreEqual(guest,File.ReadAllText(GuestPrimary));
        }
        ButtonNamed("Retry first contact tutorial").onClick.Invoke();yield return null;yield return null;
        CollectionAssert.AreEqual(new[]{"first-contact"},ReadGuest().CompletedTutorialModules);
        Assert.IsNotNull(ButtonNamed("Finish first contact tutorial"));
        Assert.AreEqual(progress,File.ReadAllText(ProgressPrimary));Assert.AreEqual(deck,File.ReadAllText(DeckPrimary));
    }

    [UnityTest] public IEnumerator LeavingAnIncompleteModuleAllowsManualResumeAndReloadWithoutNewStarterOrRewards()
    {
        yield return StartFromChoice();yield return PlaceByTap(new Vector2Int(1,7));
        string guest=File.ReadAllText(GuestPrimary),progress=File.ReadAllText(ProgressPrimary),deck=File.ReadAllText(DeckPrimary);
        ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);Assert.IsFalse(ui.FirstContactTutorialActive);
        Assert.AreEqual(guest,File.ReadAllText(GuestPrimary));
        ButtonNamed("Resume first contact tutorial").onClick.Invoke();yield return null;yield return null;
        Assert.AreEqual(0,Step);Assert.IsTrue(ui.FirstContactTutorialActive);
        Assert.AreEqual(UnitLocation.Brief,ui.Match.Player.Monster.Location);
        ButtonNamed("Exit first contact tutorial").onClick.Invoke();yield return null;
        yield return LoadScene();
        Assert.IsTrue(ui.FirstContactTutorialActive,"Only this already-started, incomplete onboarding resumes on reload.");
        Assert.AreEqual(0,Step);Assert.AreEqual(guest,File.ReadAllText(GuestPrimary));
        Assert.AreEqual(progress,File.ReadAllText(ProgressPrimary));Assert.AreEqual(deck,File.ReadAllText(DeckPrimary));
    }

    IEnumerator StartFromChoice()
    {
        yield return LoadScene();
        Assert.IsFalse(ui.FirstContactTutorialActive);Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
        ButtonNamed("Starter choice bugaloo").onClick.Invoke();yield return null;
        ButtonNamed("Confirm starter selection").onClick.Invoke();yield return null;yield return null;yield return null;
        Assert.IsTrue(ui.FirstContactTutorialActive,"Confirming the new account's starter choice starts the practice.");
        Assert.AreEqual(0,Step);
    }
    IEnumerator PlaceByTap(Vector2Int destination)
    {
        var card=ui.CanvasRoot.GetComponentsInChildren<UnitCardGesture>().Single();
        var rect=(RectTransform)card.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        card.OnPointerDown(pointer);card.OnPointerUp(pointer);yield return null;
        TapCell(destination);yield return null;yield return null;
        Assert.AreEqual(destination,ui.Match.Player.Monster.Deployment);
    }
    IEnumerator HoldMonster()
    {
        Vector2 point=MonsterPoint();Assert.IsTrue(ui.BeginBoardPointer(point));
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.15f);
        Assert.AreEqual(2,Step);ui.EndBoardPointer(point);ui.CloseModal();yield return null;
    }
    void TapCell(Vector2Int destination)
    {var point=CellPoint(destination);Assert.IsTrue(ui.BeginBoardPointer(point));ui.EndBoardPointer(point);}
    void DragTo(Vector2Int destination)
    {Assert.IsTrue(ui.BeginBoardPointer(MonsterPoint()));var target=CellPoint(destination);ui.MoveBoardPointer(target);ui.EndBoardPointer(target);}
    Vector2 MonsterPoint()=>ui.GameCamera.WorldToScreenPoint(ui.Match.Actors[ui.Match.Player.Monster].transform.position+Vector3.up*.45f);
    Vector2 CellPoint(Vector2Int cell)=>ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(cell.x,cell.y)));
    Button ButtonNamed(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name==name);
}

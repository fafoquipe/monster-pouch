using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class GuidedTutorialPlayTests
{
    static readonly string[] OverrideNames={"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"};
    static readonly PropertyInfo[] Overrides=OverrideNames.Select(name=>typeof(LocalGameUI).GetProperty(name,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    static readonly string[] Modules={"first-contact","positioning","attacks-health","simple-combat","preparation-economy","tactical-bank"};
    readonly string[] keys={"monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root,beforeProgress,beforeDeck;
    string[] previousDirectories,saved;
    bool[] existed;
    LocalGameUI ui;
    string GuestDirectory=>Path.Combine(root,"GuestProfile");
    string GuestPrimary=>Path.Combine(GuestDirectory,"guest-profile.json");
    string DeckPrimary=>Path.Combine(root,"PlayerDecks","decks.json");
    string ProgressPrimary=>Path.Combine(root,"PlayerProgress","progress.json");
    LocalGuestProfile ReadGuest()=>new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);

    [SetUp] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchGuidedPlayTests",Guid.NewGuid().ToString("N"));
        previousDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,GuestDirectory);Overrides[1].SetValue(null,Path.Combine(root,"PlayerProgress"));
        Overrides[2].SetValue(null,Path.Combine(root,"PlayerDecks"));
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);
    }
    IEnumerator LoadScene()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        yield return new WaitForSecondsRealtime(.45f);
    }
    IEnumerator LoadModule(string module)
    {
        var store=new LocalGuestProfileStore(GuestDirectory);store.OpenOrCreate(false);
        var catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,"bugaloo"));
        UnityEngine.Object.Destroy(catalogue);
        foreach(var earlier in Modules.TakeWhile(id=>id!=module))
            Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(earlier));
        yield return LoadScene();
        Assert.IsTrue(ui.GuidedTutorialActive);Assert.AreEqual(module,ReadGuest().NextTutorialModuleId);
        Assert.IsNull(ui.Match.SessionId);Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
        beforeProgress=File.ReadAllText(ProgressPrimary);beforeDeck=File.ReadAllText(DeckPrimary);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var previousUI=ui;ui=null;
        if(previousUI!=null){previousUI.CloseModal();previousUI.Match.Menu();previousUI.gameObject.SetActive(false);UnityEngine.Object.Destroy(previousUI.gameObject);}
        yield return null;Assert.IsTrue(previousUI==null,"Destroy the fixture UI before removing its isolated profile.");
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,previousDirectories[i]);
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchGuidedPlayTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }

    [UnityTest] public IEnumerator PositioningRequiresRealMovesRejectedEnemyZoneAndUniqueCellExchange()
    {
        yield return LoadModule("positioning");
        Capture("positioning");
        var dummy=ui.Match.TrainingTarget;var targetCell=dummy.CurrentCell;int hp=dummy.CurrentHealth;
        Assert.IsFalse(ReadGuest().CompletedTutorialModules.Contains("positioning"));
        yield return CompletePositioning();
        AssertSaved("positioning");Assert.AreEqual(targetCell,dummy.CurrentCell);Assert.AreEqual(hp,dummy.CurrentHealth);
        Assert.AreEqual(ui.Match.Player.Owned.Values.Count(u=>u.Location==UnitLocation.Field),
            ui.Match.Player.Owned.Values.Where(u=>u.Location==UnitLocation.Field).Select(u=>u.Deployment).Distinct().Count());
        ButtonNamed("Continue guided tutorial").onClick.Invoke();yield return null;yield return null;
        Assert.IsTrue(ui.GuidedTutorialActive);Assert.AreEqual(TrainingLesson.AttacksHealth,ui.Match.CurrentTrainingLesson);
        AssertPersistentGameDataUnchanged();
    }

    [UnityTest] public IEnumerator AttacksLessonWaitsForRangeInspectionRealDamageAndAutomaticAbility()
    {
        yield return LoadModule("attacks-health");
        Assert.IsFalse(HasButton("Begin guided combat"));
        yield return PlaceMonsterByTap();
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="Attack range preview"));
        Assert.IsFalse(HasButton("Begin guided combat"),"Inspecting the ability through a hold is still required.");
        yield return HoldMonster();Assert.IsTrue(HasButton("Begin guided combat"));
        ButtonNamed("Begin guided combat").onClick.Invoke();yield return null;
        Assert.AreEqual(MatchPhase.Combat,ui.Match.Phase);
        int initialPlayerHealth=ui.Match.Actors[ui.Match.Player.Monster].CurrentHealth;
        // The technical target's HP is derived from the real energy cycle:
        // max(existing HP, (ceil(EnergyMax/EnergyPerAttack)+2) * real damage).
        // We never inject energy or emit the ability event from this test.
        for(int i=0;i<1500&&ui.Match.TrainingAbilityCount==0;i++)
        {ui.Match.Advance(.1f);if(i%5==0)yield return null;}
        yield return null;yield return null;
        Assert.Greater(ui.Match.TrainingAttackCount,0);Assert.Greater(ui.Match.TrainingDamageDealt,0);
        Assert.Greater(ui.Match.TrainingAbilityCount,0);
        Assert.IsFalse(ui.Match.Paused,"Completing the lesson must allow the real Super animation to finish.");
        Assert.AreEqual(initialPlayerHealth,ui.Match.Actors[ui.Match.Player.Monster].CurrentHealth,"The passive target must not hurt the learner.");
        AssertSaved("attacks-health");
    }

    [UnityTest] public IEnumerator SimpleCombatRequiresPlacementAndAnActualWinAgainstTheOtherStarter()
    {
        yield return LoadModule("simple-combat");
        Assert.AreEqual("popow",ui.Match.Bot.Monster.Definition.Id);
        Assert.IsFalse(HasButton("Begin guided combat"));
        yield return PlaceMonsterByTap();ButtonNamed("Begin guided combat").onClick.Invoke();yield return null;
        for(int i=0;i<2000&&!ui.Match.TrainingCombatFinished;i++)
        {ui.Match.Advance(.1f);if(i%5==0)yield return null;}
        yield return null;yield return null;
        Assert.IsTrue(ui.Match.TrainingCombatFinished);Assert.IsTrue(ui.Match.TrainingCombatWon);
        Assert.AreEqual(0,ui.Match.PlayerWins);Assert.AreEqual(0,ui.Match.BotWins);
        AssertSaved("simple-combat");
    }

    [UnityTest] public IEnumerator EconomyUsesActualOfferPurchaseRerollAndUpgradeOfTheSameFieldUnit()
    {
        yield return LoadModule("preparation-economy");yield return WaitForBrief();
        Capture("preparation-economy");
        string id=ui.Match.TrainingWhelpId;int initialCoins=ui.Match.Player.Coins,initialRerolls=ui.Match.Player.Rerolls;
        int firstPrice=ui.Match.Player.GetOfferCost(0);
        yield return DragOfferToField(0,FreeCell());
        Assert.IsTrue(ui.Match.Player.Owned.TryGetValue(id,out var unit));Assert.AreEqual(UnitLocation.Field,unit.Location);
        Assert.AreEqual(initialCoins-firstPrice,ui.Match.Player.Coins);
        var deployed=unit.Deployment;ButtonNamed("Reroll").onClick.Invoke();yield return WaitForBrief();
        Assert.AreEqual(initialRerolls-1,ui.Match.Player.Rerolls);
        var offer=ui.CanvasRoot.GetComponentsInChildren<BriefOfferGesture>().First(g=>g.UnitId==id);
        var pointer=PointerAt((RectTransform)offer.transform);offer.OnPointerDown(pointer);offer.OnPointerUp(pointer);
        yield return null;yield return null;
        Assert.AreSame(unit,ui.Match.Player.Owned[id]);Assert.AreEqual(deployed,unit.Deployment);
        Assert.AreEqual(2,unit.Copies);Assert.AreEqual(1,unit.TrickCount);
        Assert.AreEqual(1,ui.Match.Player.Owned.Values.Count(u=>u.Definition.Id==id));AssertSaved("preparation-economy");
    }

    [UnityTest] public IEnumerator GiftedTutorialWhelpKeepsZeroInvestmentSuppressesCopiesAndRedeploysTheSameUnit()
    {
        yield return LoadModule("tactical-bank");yield return WaitForBrief();
        Capture("tactical-bank");
        var unit=ui.Match.Player.Owned[ui.Match.TrainingWhelpId];int coins=ui.Match.Player.Coins,copies=unit.Copies,tricks=unit.TrickCount;
        int refund=unit.SaleRefund;
        Assert.AreEqual(0,unit.Paid,"The tutorial gift must not acquire an invented purchase history.");
        Assert.AreEqual(0,coins,"This lesson retains its existing zero budget.");
        int remaining=ui.Match.Player.RemainingCopies(unit.Definition.Id),rerolls=ui.Match.Player.Rerolls;
        Assert.Greater(remaining,0,"This practice must retain unpurchased copies to demonstrate bank suppression.");
        int staleSlot=Enumerable.Range(0,ui.Match.Player.Offers.Count).First(i=>ui.Match.Player.Offers[i]!=null);
        var stale=ui.Match.Player.Offers[staleSlot];
        DragUnit(unit,RectCenter("Bench drop"));yield return null;yield return null;
        Assert.AreEqual(UnitLocation.Bench,unit.Location);Assert.AreEqual(coins+refund,ui.Match.Player.Coins);
        int withdrawal=unit.BankRedeployCost;Assert.AreEqual(0,withdrawal);
        Assert.IsFalse(ui.Match.Player.Offers.Any(o=>o!=null&&o.UnitId==unit.Definition.Id));
        Assert.AreEqual(remaining,ui.Match.Player.RemainingCopies(unit.Definition.Id),"Banking hides offers without deleting copies from the finite pool.");
        Assert.IsFalse(ui.Match.BuyAndPlace(staleSlot,stale.Token,FreeCell()),"An offer captured before banking cannot create another copy.");
        Assert.AreEqual(1,ui.Match.Player.Owned.Values.Count(u=>u.Definition.Id==unit.Definition.Id));
        // This lesson has only one Whelp type. While it is banked there are no
        // eligible copies, so reroll must not consume a charge or reveal offers.
        Assert.IsFalse(ui.Match.Reroll(ui.Match.Player.OfferVersion));yield return null;
        Assert.AreEqual(rerolls,ui.Match.Player.Rerolls);Assert.AreEqual(coins+refund,ui.Match.Player.Coins);
        Assert.AreEqual(remaining,ui.Match.Player.RemainingCopies(unit.Definition.Id));
        Assert.IsFalse(ui.Match.Player.Offers.Any(o=>o!=null&&o.UnitId==unit.Definition.Id));
        ButtonNamed("Bench drop").onClick.Invoke();yield return null;yield return null;
        Capture("tactical-bank-open");
        var card=ui.CanvasRoot.GetComponentsInChildren<UnitCardGesture>().Single(g=>g.name=="bank-unit-"+unit.Definition.Id);
        var pointer=PointerAt((RectTransform)card.transform);card.OnPointerDown(pointer);
        pointer.position=CellPoint(FreeCell());card.OnDrag(pointer);card.OnPointerUp(pointer);yield return null;yield return null;
        Assert.AreEqual(UnitLocation.Field,unit.Location);Assert.AreEqual(coins+refund-withdrawal,ui.Match.Player.Coins);
        Assert.AreEqual(remaining,ui.Match.Player.RemainingCopies(unit.Definition.Id));
        Assert.AreEqual(copies,unit.Copies);Assert.AreEqual(tricks,unit.TrickCount);AssertSaved("tactical-bank");
    }

    [UnityTest] public IEnumerator CompletionSaveFailureKeepsTheActionsAndRetriesWithoutChangingOtherProgress()
    {
        yield return LoadModule("positioning");string before=File.ReadAllText(GuestPrimary);
        using(new FileStream(Path.Combine(GuestDirectory,"guest-profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            yield return CompletePositioning();
            Assert.IsTrue(HasButton("Retry guided tutorial"));Assert.AreEqual(before,File.ReadAllText(GuestPrimary));
            AssertPersistentGameDataUnchanged();
        }
        ButtonNamed("Retry guided tutorial").onClick.Invoke();yield return null;yield return null;
        AssertSaved("positioning");
    }

    [UnityTest] public IEnumerator ExitResumeReloadAndReviewDoNotRepeatStarterOrCompletionReceipts()
    {
        yield return LoadModule("positioning");string before=File.ReadAllText(GuestPrimary);
        ButtonNamed("Exit guided tutorial").onClick.Invoke();yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);Assert.IsFalse(ui.GuidedTutorialActive);
        ButtonNamed("Resume tutorial").onClick.Invoke();yield return null;yield return null;
        Assert.IsTrue(ui.GuidedTutorialActive);Assert.AreEqual(TrainingLesson.Positioning,ui.Match.CurrentTrainingLesson);
        ButtonNamed("Exit guided tutorial").onClick.Invoke();yield return null;
        yield return LoadScene();Assert.IsTrue(ui.GuidedTutorialActive);
        Assert.AreEqual(before,File.ReadAllText(GuestPrimary));Assert.AreEqual(TrainingLesson.Positioning,ui.Match.CurrentTrainingLesson);
        yield return CompletePositioning();AssertSaved("positioning");string completed=File.ReadAllText(GuestPrimary);
        ButtonNamed("Exit guided tutorial").onClick.Invoke();yield return null;yield return null;
        ui.NavigateMenu(3);yield return null;yield return null;
        ButtonNamed("Open tutorial review").onClick.Invoke();yield return null;
        ButtonNamed("Review tutorial positioning").onClick.Invoke();yield return null;yield return null;
        Assert.IsTrue(ui.GuidedTutorialActive);Assert.AreEqual(TrainingLesson.Positioning,ui.Match.CurrentTrainingLesson);
        yield return CompletePositioning();
        Assert.AreEqual(completed,File.ReadAllText(GuestPrimary));AssertPersistentGameDataUnchanged();
    }

    IEnumerator CompletePositioning()
    {
        var monster=ui.Match.Player.Monster;
        DragUnit(monster,CellPoint(FreeCell()));yield return null;yield return null;
        Vector2Int own=monster.Deployment;
        var enemy=ui.Match.Board.GetAllCells().First(cell=>!ui.Match.Config.IsDeploymentLegal(ui.Match.LocalSide,cell.Coordinates));
        DragUnit(monster,CellPoint(enemy));yield return null;
        Assert.AreEqual(own,monster.Deployment);
        var ally=ui.Match.Player.Owned.Values.First(unit=>!unit.Definition.IsMonster&&unit.Location==UnitLocation.Field);
        var other=ally.Deployment;DragUnit(monster,CellPoint(ui.Match.Board.GetCell(other.x,other.y)));
        yield return null;yield return null;
        Assert.AreEqual(other,monster.Deployment);Assert.AreEqual(own,ally.Deployment);
    }
    IEnumerator PlaceMonsterByTap()
    {
        var card=ui.CanvasRoot.GetComponentsInChildren<UnitCardGesture>().Single();var pointer=PointerAt((RectTransform)card.transform);
        card.OnPointerDown(pointer);card.OnPointerUp(pointer);yield return null;
        var point=CellPoint(FreeCell());Assert.IsTrue(ui.BeginBoardPointer(point));ui.EndBoardPointer(point);
        yield return null;yield return null;yield return null;
        Assert.AreEqual(UnitLocation.Field,ui.Match.Player.Monster.Location);
    }
    IEnumerator HoldMonster()
    {
        var point=UnitPoint(ui.Match.Player.Monster);Assert.IsTrue(ui.BeginBoardPointer(point));
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.15f);ui.EndBoardPointer(point);ui.CloseModal();yield return null;yield return null;
    }
    IEnumerator WaitForBrief()
    {
        // Reroll closes and reopens the real Brief; wait for its presentation to
        // finish instead of bypassing the input gate or changing offer tokens.
        yield return null;
        float timeout=Time.realtimeSinceStartup+3;
        while(!ui.BriefReady&&Time.realtimeSinceStartup<timeout)yield return null;
        Assert.IsTrue(ui.BriefReady);
    }
    IEnumerator DragOfferToField(int slot,BoardCell cell)
    {
        var gesture=ui.CanvasRoot.GetComponentsInChildren<BriefOfferGesture>().Single(g=>g.name=="offer-"+slot);
        var pointer=PointerAt((RectTransform)gesture.transform);gesture.OnPointerDown(pointer);
        pointer.position=CellPoint(cell);gesture.OnDrag(pointer);gesture.OnPointerUp(pointer);yield return null;yield return null;
    }
    void DragUnit(OwnedUnit unit,Vector2 destination)
    {Assert.IsTrue(ui.BeginBoardPointer(UnitPoint(unit)));ui.MoveBoardPointer(destination);ui.EndBoardPointer(destination);}
    BoardCell FreeCell()=>ui.Match.Board.GetAllCells().Where(cell=>ui.Match.Config.IsDeploymentLegal(ui.Match.LocalSide,cell.Coordinates)&&
        !cell.IsBlocked&&cell.OccupiedBy==null&&cell.ReservedBy==null).OrderBy(cell=>cell.Y).ThenBy(cell=>cell.X).First();
    Vector2 UnitPoint(OwnedUnit unit)=>ui.GameCamera.WorldToScreenPoint(ui.Match.Actors[unit].transform.position+Vector3.up*.45f);
    Vector2 CellPoint(BoardCell cell)=>ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell));
    Vector2 RectCenter(string name)
    {var rect=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(item=>item.name==name);return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));}
    static PointerEventData PointerAt(RectTransform rect)=>new PointerEventData(EventSystem.current)
        {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
    Button ButtonNamed(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(button=>button.name==name);
    bool HasButton(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Any(button=>button.name==name);
    void AssertSaved(string module)
    {
        Assert.IsTrue(ReadGuest().CompletedTutorialModules.Contains(module));Assert.IsTrue(HasButton("Continue guided tutorial"));
        Assert.IsNull(ui.Match.SessionId);Assert.AreEqual(GuestOnboardingStage.Tutorial,ReadGuest().OnboardingStage);
        AssertPersistentGameDataUnchanged();
    }
    void AssertPersistentGameDataUnchanged()
    {Assert.AreEqual(beforeProgress,File.ReadAllText(ProgressPrimary));Assert.AreEqual(beforeDeck,File.ReadAllText(DeckPrimary));}
    void Capture(string module)
    {
        var camera=ui.GameCamera;var canvas=ui.CanvasRoot.GetComponentInParent<Canvas>();
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var order=canvas.sortingOrder;var distance=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(Screen.width,Screen.height,24);var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder=short.MaxValue;
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            string folder=Path.GetFullPath("Logs/roadmap-v1.2");Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,"tutorial-"+module+".png"),image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=oldCamera;
            canvas.planeDistance=distance;canvas.sortingOrder=order;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);
        }
    }
}

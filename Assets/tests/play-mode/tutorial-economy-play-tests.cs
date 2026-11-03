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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class TutorialEconomyPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] keys={"monster-pouch.language","monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string directory;string[] prior,saved;bool[] existed;bool spanish;
    LocalGameUI ui;MatchConfig catalogue;InteractionCaptureGameView view;
    string Guest=>Path.Combine(directory,"Guest");
    string Primary=>Path.Combine(Guest,"guest-profile.json");
    LocalGuestProfileStore Store=>new LocalGuestProfileStore(Guest);

    [UnitySetUp] public IEnumerator Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchTutorialEconomyPlay",Guid.NewGuid().ToString("N"));
        prior=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,Guest);Overrides[1].SetValue(null,Path.Combine(directory,"Progress"));Overrides[2].SetValue(null,Path.Combine(directory,"Decks"));
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);GameText.SetLanguage(true);
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var profile=Store.OpenOrCreate(false);Assert.AreEqual(GuestPermanentEconomy.WelcomeSaturnkens,profile.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,Store.ChooseStarter(catalogue,"bugaloo"));
        foreach(string module in GuestTutorialModules.OrderedIds.Take(6))Store.CompleteTutorialModule(module);
        Assert.IsEmpty(Store.Reload().AbilityUnlocks,"Training completion cannot grant permanent upgrades.");
        view=new InteractionCaptureGameView();yield return view.Open();yield return Load();yield return Resize(393,852);
    }
    IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+10;
        while(LoadingArtworkScreen.IsBlocking&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);ui.Match.Menu();ui.CloseModal();ui.RenderPage();yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var previousUI=ui;ui=null;
        if(previousUI!=null){previousUI.CloseModal();previousUI.Match.Menu();previousUI.gameObject.SetActive(false);Object.Destroy(previousUI.gameObject);}
        yield return null;Assert.IsTrue(previousUI==null,"Destroy the old UI before its account is removed.");
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,prior[i]);
        GameText.SetLanguage(spanish);
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();
        if(catalogue!=null)Object.Destroy(catalogue);
        view?.RestoreSelection();if(view!=null)yield return view.WaitForRestoration();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchTutorialEconomyPlay"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    object Call(string name,params object[] args)=>typeof(LocalGameUI).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,args);
    void Show(string id,int index=-1)=>Call("ShowAbilityCard",new OwnedUnit(ui.Match.Config.Get(id)),false,true,index,null);
    Button Button(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Last(b=>b.name==name&&b.gameObject.activeInHierarchy);
    RectTransform Rect(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Last(r=>r.name==name&&r.gameObject.activeInHierarchy);
    void Credit(PermanentCurrency currency,long amount,string id)
    {Assert.AreEqual(GuestVendingStatus.Committed,Store.CreditFreeCurrency("fixture-"+id,"synthetic-permanent-ui-only",currency,amount).Status);}
    void Fund(long earth=100,long sun=100)
    {Credit(PermanentCurrency.Earthcoins,earth,"earth");Credit(PermanentCurrency.Suncoins,sun,"sun");ui.RenderPage();}

    [UnityTest] public IEnumerator CollectionPouchAndChosenMachineCompleteThroughRealScreensAndPersistOnce()
    {
        Assert.IsTrue(ui.StartNextTutorial());yield return null;
        Assert.IsFalse(Button("Continue economy tutorial").interactable);
        yield return Capture("collection-guide-393x852");
        Button("Tutorial blue pouch").onClick.Invoke();yield return null;
        Assert.AreEqual(1,Store.Reload().TutorialPouches.Count);
        Assert.AreEqual(1,Store.Reload().WalletEvents.Count,"Claiming does not credit the hidden reward.");
        yield return Capture("blue-pouch-393x852");
        var open=Button("Open tutorial pouch");open.onClick.Invoke();open.onClick.Invoke();yield return null;
        var earned=Store.Reload();Assert.AreEqual(2,earned.WalletEvents.Count);Assert.IsEmpty(earned.Pouches);
        Assert.IsEmpty(earned.PouchVictories);Assert.IsFalse(string.IsNullOrEmpty(earned.TutorialPouches.Single().OpenedUtc));
        var receipt=earned.WalletEvents.Single(e=>e.Id==earned.TutorialPouches.Single().Id);
        Assert.AreEqual("+"+receipt.Amounts.Single().Amount,Rect("Tutorial reward amount").GetComponent<Text>().text);
        yield return Capture("blue-pouch-reward-393x852");
        Button("Continue tutorial reward").onClick.Invoke();yield return null;
        Button("Tutorial deck").onClick.Invoke();yield return null;
        Assert.Contains("deck",Store.Reload().TutorialMenuVisits);
        Button("Return to economy tutorial").onClick.Invoke();yield return null;
        Button("Tutorial collection").onClick.Invoke();yield return null;
        Assert.Contains("collection",Store.Reload().TutorialMenuVisits);
        Button("Return to economy tutorial").onClick.Invoke();yield return null;
        Button("Tutorial abilities").onClick.Invoke();yield return null;
        Assert.IsFalse(Store.Reload().TutorialMenuVisits.Contains("ability"));
        Button("ability-0").onClick.Invoke();yield return null;
        Assert.Contains("ability",Store.Reload().TutorialMenuVisits);Assert.IsEmpty(Store.Reload().AbilityUnlocks);
        Button("character-details-back").onClick.Invoke();yield return null;
        Button("Tutorial wallet").onClick.Invoke();yield return null;
        yield return Capture("tutorial-wallet-393x852");
        Button("Continue tutorial wallet").onClick.Invoke();yield return null;
        Assert.IsTrue(Button("Continue economy tutorial").interactable);
        Button("Continue economy tutorial").onClick.Invoke();yield return null;
        Assert.AreEqual(GuestTutorialModules.FirstMachine,Store.Reload().NextTutorialModuleId);
        Assert.IsNotNull(Button("Tutorial choose machine"));
        yield return Resize(1280,720);Call("ShowEconomyTutorial");yield return null;
        yield return Capture("machine-guide-1280x720");
        Button("Tutorial choose machine").onClick.Invoke();yield return null;
        var machineButtons=ui.CanvasRoot.GetComponentsInChildren<Button>().Where(b=>b.name=="Machine artwork button"&&b.gameObject.activeInHierarchy).ToArray();
        Assert.AreEqual(23,machineButtons.Length);machineButtons[4].onClick.Invoke();yield return null;
        Assert.AreEqual(14,ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("Reward ")&&r.gameObject.activeInHierarchy));
        long before=Store.Reload().FreeBalance(PermanentCurrency.Saturncoins);
        Button("Draw vending machine").onClick.Invoke();yield return null;
        Assert.AreEqual(before,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        var confirm=Button("Confirm spend Saturnkens");confirm.onClick.Invoke();confirm.onClick.Invoke();yield return null;
        Assert.AreEqual(before-5,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(1,Store.Reload().WalletEvents.Count(e=>e.Kind==GuestWalletEventKind.MachineDraw));
        Assert.AreEqual(GuestTutorialModules.FirstMachine,Store.Reload().NextTutorialModuleId,"The confirmed reward must be viewed before advancing.");
        yield return Capture("tutorial-machine-reward-1280x720");
        Button("Continue after vending reward").onClick.Invoke();yield return null;
        Assert.AreEqual(GuestTutorialModules.FirstBo5,Store.Reload().NextTutorialModuleId);Assert.IsTrue(Store.Reload().TutorialMachineReady);
        string committed=File.ReadAllText(Primary);yield return Load();
        Assert.IsFalse(ui.StartNextTutorial());Assert.AreEqual(committed,File.ReadAllText(Primary));
        Call("ShowTutorialReviewMenu");yield return null;
        Button("Review tutorial "+GuestTutorialModules.ProgressionCollection).onClick.Invoke();yield return null;
        Button("Tutorial blue pouch").onClick.Invoke();yield return null;
        Assert.IsNotNull(Button("Continue tutorial reward"));Assert.AreEqual(committed,File.ReadAllText(Primary));
        Call("ExitEconomyTutorial");yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="Return to economy tutorial"));
    }
    [UnityTest,Platform("Win")] public IEnumerator OpeningFailureShowsRetryAndReloadKeepsUnopenedGiftWithoutDuplication()
    {
        Assert.IsTrue(ui.StartNextTutorial());Button("Tutorial blue pouch").onClick.Invoke();yield return null;
        string claimed=File.ReadAllText(Primary);int roll=Store.Reload().TutorialPouches.Single().ResourceRoll;
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Button("Open tutorial pouch").onClick.Invoke();yield return null;
            Assert.IsNotNull(Button("Retry economy tutorial"));Assert.AreEqual(claimed,File.ReadAllText(Primary));
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.name=="Tutorial reward amount"));
        }
        Call("ExitEconomyTutorial");yield return Load();Assert.IsTrue(ui.StartNextTutorial());
        Button("Tutorial blue pouch").onClick.Invoke();yield return null;
        Assert.AreEqual(roll,Store.Reload().TutorialPouches.Single().ResourceRoll);
        Button("Open tutorial pouch").onClick.Invoke();yield return null;
        Assert.AreEqual(2,Store.Reload().WalletEvents.Count);Assert.IsEmpty(Store.Reload().Pouches);
        Assert.AreEqual(GuestTutorialModules.ProgressionCollection,Store.Reload().NextTutorialModuleId);
    }
    [UnityTest,Platform("Win")] public IEnumerator MenuVisitFailureCannotMarkProgressAndRetryPreservesTheErrorUntilItSaves()
    {
        Assert.IsTrue(ui.StartNextTutorial());yield return null;
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Button("Tutorial deck").onClick.Invoke();yield return null;
            Assert.IsNotNull(Button("Retry economy tutorial"));Assert.IsEmpty(Store.Reload().TutorialMenuVisits);
            Button("Retry economy tutorial").onClick.Invoke();yield return null;
            Assert.IsNotNull(Button("Retry economy tutorial"));Assert.IsEmpty(Store.Reload().TutorialMenuVisits);
        }
        Button("Retry economy tutorial").onClick.Invoke();yield return null;
        Assert.Contains("deck",Store.Reload().TutorialMenuVisits);Assert.IsNotNull(Button("Tutorial blue pouch"));
        Assert.IsFalse(Button("Continue economy tutorial").interactable);
    }

    IEnumerator Resize(int width,int height)
    {
#if UNITY_EDITOR
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        type.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{width+"x"+height});
#else
        Screen.SetResolution(width,height,FullScreenMode.Windowed);
#endif
        for(int frame=0;frame<120&&(Screen.width!=width||Screen.height!=height);frame++)yield return null;
        Assert.AreEqual(new Vector2Int(width,height),new Vector2Int(Screen.width,Screen.height));
        yield return null;yield return null;Call("UpdateLayout");ui.RenderPage();yield return null;
    }
    IEnumerator Capture(string name)
    {
        string folder=Environment.GetEnvironmentVariable("MONSTER_POUCH_TUTORIAL_ECONOMY_CAPTURES");
        if(string.IsNullOrEmpty(folder))folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","local-validation","tutorial-economy");
        Directory.CreateDirectory(folder);string path=Path.Combine(folder,name+"-"+Path.GetFileName(directory)+".png");
        float settle=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<settle)yield return null;
        Canvas.ForceUpdateCanvases();yield return null;yield return null;int width=Screen.width,height=Screen.height;
        ScreenCapture.CaptureScreenshot(path);
        for(int frame=0;frame<120;frame++){yield return null;if(frame>1&&File.Exists(path)&&new FileInfo(path).Length>24)break;}
        Assert.IsTrue(File.Exists(path),"The native tutorial capture must be written.");byte[] bytes=File.ReadAllBytes(path);Assert.Greater(bytes.Length,24);
        Assert.AreEqual(width,(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19]);
        Assert.AreEqual(height,(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23]);
    }
}

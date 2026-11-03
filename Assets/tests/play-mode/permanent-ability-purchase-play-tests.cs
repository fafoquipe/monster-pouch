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

public sealed class PermanentAbilityPurchasePlayTests
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
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchPermanentAbilityPlay",Guid.NewGuid().ToString("N"));
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
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);ui.Match.Menu();Call("ExitEconomyTutorial");ui.RenderPage();yield return null;
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
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchPermanentAbilityPlay"))+Path.DirectorySeparatorChar;
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

    [UnityTest] public IEnumerator InspectionNeverSpendsAndExplicitConfirmationUnlocksOnceWithBothCurrencies()
    {
        Fund(150,200);Show("atori");yield return null;
        Assert.IsFalse(Button("ability-upgrade").interactable,"The automatic base ability has no permanent purchase.");
        Assert.AreEqual(3,Rect("Unit sheet").GetComponentsInChildren<RectTransform>().Count(r=>r.name=="Permanent ability locked"));
        string before=File.ReadAllText(Primary);
        Button("ability-0").onClick.Invoke();Button("ability-0").onClick.Invoke();yield return null;
        Assert.AreEqual(before,File.ReadAllText(Primary),"A double tap only inspects a collection ability.");
        Assert.IsTrue(Button("ability-upgrade").interactable);
        Assert.AreEqual(2,Button("ability-upgrade").GetComponentsInChildren<Text>().Count(t=>t.name=="Permanent cost value"&&t.text=="100"));
        Assert.IsFalse(Rect("Unit sheet").GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Moonken cost"),"Permanent prices use the two account currencies.");
        yield return Capture("locked-ability-393x852");
        Button("ability-upgrade").onClick.Invoke();yield return null;
        Assert.AreEqual(before,File.ReadAllText(Primary),"Opening confirmation is read-only.");
        yield return Capture("confirm-100-earth-100-sun-393x852");
        var confirm=Button("Confirm permanent ability purchase");confirm.onClick.Invoke();confirm.onClick.Invoke();yield return null;
        var after=Store.Reload();string ability=catalogue.Get("atori").Tricks[0].Id;
        Assert.IsTrue(after.OwnsAbility("atori",ability));Assert.AreEqual(1,after.AbilityUnlocks.Count);
        Assert.AreEqual(1,after.WalletEvents.Count(e=>e.Kind==GuestWalletEventKind.AbilityPurchase));
        Assert.AreEqual(50,after.FreeBalance(PermanentCurrency.Earthcoins));Assert.AreEqual(100,after.FreeBalance(PermanentCurrency.Suncoins));
        Assert.AreEqual(20,after.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsFalse(Button("ability-upgrade").interactable);Assert.AreEqual(1,Rect("Unit sheet").GetComponentsInChildren<Text>().Count(t=>t.name=="Permanent ability owned"));
        yield return Resize(1280,720);Show("atori",0);yield return null;yield return Capture("purchased-ability-1280x720");
        string committed=File.ReadAllText(Primary);yield return Load();Show("atori",0);yield return null;
        Button("ability-0").onClick.Invoke();Button("ability-0").onClick.Invoke();
        Assert.IsFalse(Button("ability-upgrade").interactable);Assert.AreEqual(committed,File.ReadAllText(Primary));
        Assert.IsEmpty(Store.Reload().VendingMachines,"Welcome currency and ability purchases do not automatically spin machines.");
    }
    [UnityTest] public IEnumerator MissingEitherCurrencyOrCharacterBlocksPaymentAndAFreeUnlockWinsAConfirmationRace()
    {
        Fund(100,99);Show("atori",1);yield return null;
        string before=File.ReadAllText(Primary);Button("ability-upgrade").onClick.Invoke();yield return null;
        var confirm=Button("Confirm permanent ability purchase");Assert.IsFalse(confirm.interactable);confirm.onClick.Invoke();
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual("Saldo insuficiente",Rect("Permanent purchase status").GetComponent<Text>().text);
        yield return Capture("insufficient-one-sunken-393x852");
        Show("flo",0);yield return null;Assert.IsFalse(Button("ability-upgrade").interactable);
        Assert.IsFalse(Store.Reload().OwnsCharacter("flo"));yield return Capture("locked-character-393x852");
        Credit(PermanentCurrency.Suncoins,1,"remaining-sun");Show("atori",1);Button("ability-upgrade").onClick.Invoke();yield return null;
        string ability=catalogue.Get("atori").Tricks[1].Id;
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,Store.UnlockAbility(catalogue,"fixture-free-ability","atori",ability,GuestUnlockSource.NormalPouch));
        var granted=Store.Reload();Button("Confirm permanent ability purchase").onClick.Invoke();yield return null;
        Assert.IsTrue(Store.Reload().OwnsAbility("atori",ability));Assert.IsFalse(Button("ability-upgrade").interactable);
        Assert.AreEqual(granted.WalletEvents.Count,Store.Reload().WalletEvents.Count);
        Assert.AreEqual(100,Store.Reload().FreeBalance(PermanentCurrency.Earthcoins));Assert.AreEqual(100,Store.Reload().FreeBalance(PermanentCurrency.Suncoins));
    }
    [UnityTest,Platform("Win")] public IEnumerator FailedWriteRetriesOnceAndTheNextMatchUsesOwnedUpgradesWhileCombatStillSpendsMoonkens()
    {
        Fund();Show("atori",2);Button("ability-upgrade").onClick.Invoke();yield return null;
        string before=File.ReadAllText(Primary);
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Button("Confirm permanent ability purchase").onClick.Invoke();yield return null;
            Assert.IsNotNull(Button("Retry permanent ability purchase"));Assert.AreEqual(before,File.ReadAllText(Primary));
            Assert.IsFalse(Store.Reload().OwnsAbility("atori",catalogue.Get("atori").Tricks[2].Id));
        }
        var retry=Button("Retry permanent ability purchase");retry.onClick.Invoke();retry.onClick.Invoke();yield return null;
        var purchased=Store.Reload();Assert.AreEqual(1,purchased.WalletEvents.Count(e=>e.Kind==GuestWalletEventKind.AbilityPurchase));
        Assert.AreEqual(0,purchased.FreeBalance(PermanentCurrency.Earthcoins));Assert.AreEqual(0,purchased.FreeBalance(PermanentCurrency.Suncoins));
        ui.CloseModal();Assert.IsTrue((bool)Call("PrepareCurrentDeckForMatch"));
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"atori"},20261024));yield return null;
        var player=ui.Match.Player;var definition=ui.Match.Config.Get("atori");
        Assert.AreEqual(2,player.AbilityAccess.CopyLimit(definition));Assert.IsFalse(player.AbilityAccess.Allows(definition,0));Assert.IsTrue(player.AbilityAccess.Allows(definition,2));
        Assert.AreEqual(1,player.AbilityAccess.CopyLimit(ui.Match.Config.Get("bugui")));
        Assert.AreEqual(4,ui.Match.Bot.AbilityAccess.CopyLimit(definition));
        player.AddCombatCoins(100-player.Coins);var offer=player.Offers.First(o=>o!=null&&o.UnitId=="atori");
        int slot=Enumerable.Range(0,player.Offers.Count).Single(i=>player.Offers[i]==offer);Assert.IsTrue(ui.Match.Buy(slot,offer.Token));
        var owned=player.Owned["atori"];ui.ShowInspection(owned,false);yield return null;
        Assert.IsTrue(Button("ability-0").GetComponent<AbilityAvailabilityVisual>().IsGrayscale);
        Assert.IsTrue(Button("ability-1").GetComponent<AbilityAvailabilityVisual>().IsGrayscale);
        Assert.IsTrue(Button("ability-2").GetComponent<AbilityAvailabilityVisual>().IsAvailable);
        Button("ability-2").onClick.Invoke();int combatBefore=player.Coins;
        string walletBefore=File.ReadAllText(Primary);Button("ability-upgrade").onClick.Invoke();yield return null;
        Assert.IsTrue(owned.Tricks[2]);Assert.AreEqual(2,owned.Copies);Assert.AreEqual(combatBefore-definition.Tricks[2].Cost,player.Coins);
        Assert.AreEqual(walletBefore,File.ReadAllText(Primary),"The per-match upgrade only spends Moonkens.");
        Assert.IsFalse(ui.Match.UpgradeMonster(0),"The new account has not unlocked a Monster upgrade.");
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
        string folder=Environment.GetEnvironmentVariable("MONSTER_POUCH_PERMANENT_ABILITY_CAPTURES");
        if(string.IsNullOrEmpty(folder))folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","local-validation","permanent-abilities");
        Directory.CreateDirectory(folder);string path=Path.Combine(folder,name+"-"+Path.GetFileName(directory)+".png");
        float settle=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<settle)yield return null;
        Canvas.ForceUpdateCanvases();yield return null;yield return null;int width=Screen.width,height=Screen.height;
        ScreenCapture.CaptureScreenshot(path);
        for(int frame=0;frame<120;frame++){yield return null;if(frame>1&&File.Exists(path)&&new FileInfo(path).Length>24)break;}
        Assert.IsTrue(File.Exists(path),"The native permanent-ability capture must be written.");byte[] bytes=File.ReadAllBytes(path);Assert.Greater(bytes.Length,24);
        Assert.AreEqual(width,(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19]);
        Assert.AreEqual(height,(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23]);
    }
}

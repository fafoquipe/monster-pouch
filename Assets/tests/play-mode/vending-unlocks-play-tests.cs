using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class VendingUnlocksPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    static readonly PropertyInfo CatalogueOverride=typeof(LocalGameUI).GetProperty("VendingCatalogueOverride",BindingFlags.Static|BindingFlags.NonPublic);
    readonly string[] keys={"monster-pouch.language","monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;string[] prior,saved;bool[] existed;bool spanish;
    PlayerMachineCatalogue catalogue,oldCatalogue;MatchConfig characters;LocalGameUI ui;InteractionCaptureGameView view;
    string Guest=>Path.Combine(root,"Guest");
    LocalGuestProfileStore Store=>new LocalGuestProfileStore(Guest);
    [SetUp] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchVendingPlay",Guid.NewGuid().ToString("N"));
        prior=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,Guest);Overrides[1].SetValue(null,Path.Combine(root,"Progress"));Overrides[2].SetValue(null,Path.Combine(root,"Decks"));
        oldCatalogue=(PlayerMachineCatalogue)CatalogueOverride.GetValue(null);
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);GameText.SetLanguage(true);
        characters=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        Store.OpenOrCreate(false);Store.ChooseStarter(characters,"bugaloo");
        foreach(string module in GuestTutorialModules.OrderedIds.TakeWhile(GuestTutorialModules.IsImplemented))Store.CompleteTutorialModule(module);
        catalogue=ScriptableObject.CreateInstance<PlayerMachineCatalogue>();catalogue.Machines=new[]{new PlayerMachineEntry{
            Configuration=TestConfiguration(),Artwork=Resources.Load<Sprite>("MonsterPouch/UI/Lobby/Header/vending")}};
        CatalogueOverride.SetValue(null,catalogue);view=new InteractionCaptureGameView();
    }
    IEnumerator Load()
    {
        yield return view.Open();yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+10;
        while(LoadingArtworkScreen.IsBlocking&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);ui.Match.Menu();ui.CloseModal();ui.RenderPage();yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var previousUI=ui;ui=null;
        if(previousUI!=null){previousUI.CloseModal();previousUI.Match.Menu();previousUI.gameObject.SetActive(false);UnityEngine.Object.Destroy(previousUI.gameObject);}
        yield return null;Assert.IsTrue(previousUI==null,"Destroy the old UI before removing its isolated wallet.");
        CatalogueOverride.SetValue(null,oldCatalogue);
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,prior[i]);
        GameText.SetLanguage(spanish);for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();UnityEngine.Object.DestroyImmediate(catalogue);UnityEngine.Object.DestroyImmediate(characters);
        view.RestoreSelection();yield return view.WaitForRestoration();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchVendingPlay"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [UnityTest] public IEnumerator EarnedSaturnkensWaitForExplicitChoiceAndOneConfirmationUnlocksTheCurrentCharacter()
    {
        long initial=Store.Reload().FreeBalance(PermanentCurrency.Saturncoins);
        Assert.AreEqual(GuestPermanentEconomy.WelcomeSaturnkens,initial);
        Gift(10);yield return Load();
        Assert.AreEqual(initial+10,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsEmpty(Store.Reload().VendingMachines,"Earning currency must never start a draw.");
        Button("Lobby vending").onClick.Invoke();yield return null;
        Assert.IsFalse(Store.Reload().OwnsCharacter("flo"));
        Button("Machine artwork button").onClick.Invoke();yield return null;
        Assert.AreEqual(14,ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Count(t=>t.name.StartsWith("Reward ",StringComparison.Ordinal)&&t.gameObject.activeInHierarchy));
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text==characters.Get("flo").DisplayName));
        yield return Capture("machine-detail");
        Button("Draw vending machine").onClick.Invoke();yield return null;
        Assert.AreEqual(initial+10,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins));Assert.IsFalse(Store.Reload().OwnsCharacter("flo"));
        var confirm=Button("Confirm spend Saturnkens");confirm.onClick.Invoke();confirm.onClick.Invoke();yield return null;
        var profile=Store.Reload();Assert.IsTrue(profile.OwnsCharacter("flo"));Assert.AreEqual(initial+5,profile.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(1,profile.VendingMachines.Single().State.DrawCount);Assert.AreEqual(1,profile.CharacterUnlocks.Count);
        Assert.IsTrue(ui.SelectedWhelps.Contains("flo"),"A newly owned Whelp fills the starter brief through its existing normalization.");
        Assert.IsNotNull(Button("Continue after vending reward"));yield return Capture("committed-character");
        Button("Continue after vending reward").onClick.Invoke();yield return null;
        Assert.AreEqual((initial+5).ToString(),ui.CanvasRoot.GetComponentsInChildren<Text>().Single(t=>t.name=="Vending Saturnkens balance").text);
        ui.CloseModal();ui.RenderPage();yield return null;
        Assert.AreEqual(initial+5,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins),"Reading gifts cannot restore spent currency.");
    }
    [UnityTest,Platform("Win")] public IEnumerator MissingApprovedCatalogueAndInsufficientFundsNeverDrawAndGiftImportRetriesSafely()
    {
        // Spend the real one-time welcome through four explicitly requested draws
        // on another fixture machine. The target Flo machine remains untouched.
        var other=TestConfiguration("bugaloo");
        for(int draw=0;draw<4;draw++)
        {
            var preview=Store.PreviewMachine(characters,other);
            var result=Store.DrawMachine(characters,other,new MachineDrawRequest{RequestId="welcome-spend-"+draw,
                MachineId=other.Definition.MachineId,CatalogVersion=other.Definition.CatalogVersion,ExpectedRevision=preview.MachineRevision});
            Assert.AreEqual(GuestVendingStatus.Committed,result.Status);
        }
        Assert.AreEqual(0,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        int priorEvents=Store.Reload().WalletEvents.Count;
        Gift(4);
        // The daily receipt already exists. Deny the separate guest-wallet commit
        // to simulate an interruption exactly between the two persisted stores.
        string primary=Path.Combine(Guest,"guest-profile.json"),before=File.ReadAllText(primary);
        using(new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            yield return Load();
            Assert.AreEqual(before,File.ReadAllText(primary));Assert.AreEqual(priorEvents,Store.Reload().WalletEvents.Count);
            Assert.IsFalse(Store.Reload().VendingMachines.Any(m=>m.Configuration.Definition.MachineId=="test-flo"));Assert.IsFalse(Store.Reload().OwnsCharacter("flo"));
        }
        ui.RenderPage();yield return null;
        string id=Store.Reload().ProfileId;Assert.AreEqual(4,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        for(int i=0;i<3;i++)ui.RenderPage();
        Assert.AreEqual(priorEvents+1,Store.Reload().WalletEvents.Count);Assert.AreEqual(id,Store.Reload().ProfileId);
        Button("Lobby vending").onClick.Invoke();yield return null;Button("Machine artwork button").onClick.Invoke();yield return null;
        Assert.IsFalse(Button("Draw vending machine").interactable);
        Assert.IsFalse(Store.Reload().VendingMachines.Any(m=>m.Configuration.Definition.MachineId=="test-flo"));
        catalogue.Machines[0].Configuration.EnabledForLocalPlay=false;
        ui.CloseModal();ui.RenderPage();Button("Lobby vending").onClick.Invoke();yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="Machine artwork button"&&b.gameObject.activeInHierarchy));
        Assert.AreEqual(4,Store.Reload().FreeBalance(PermanentCurrency.Saturncoins));Assert.IsFalse(Store.Reload().OwnsCharacter("flo"));
    }
    [UnityTest] public IEnumerator ApprovedCatalogueShowsAllCurrentMachineArtAndDailyGiftUsesItsBluePouch()
    {
        CatalogueOverride.SetValue(null,null);yield return Load();
        var approved=Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue");
        Assert.IsNotNull(approved);Assert.DoesNotThrow(()=>approved.Validate(characters));
        Assert.AreEqual(23,approved.Machines.Length);
        Assert.AreEqual(23,approved.Machines.Select(m=>m.Configuration.Definition.CharacterId).Distinct().Count());
        string before=File.ReadAllText(Path.Combine(Guest,"guest-profile.json"));
        Button("Lobby vending").onClick.Invoke();yield return null;
        foreach(var entry in approved.Machines)
        {
            string id=entry.Configuration.Definition.CharacterId;
            var expected=Resources.Load<Sprite>("MonsterPouch/Vending/Artwork/"+id);
            Assert.IsNotNull(expected,id);Assert.AreSame(expected,entry.Artwork,id);
            Assert.AreEqual(FilterMode.Point,expected.texture.filterMode,id);Assert.AreEqual(1,expected.texture.mipmapCount,id);
            var pouch=Resources.Load<Sprite>("MonsterPouch/Pouches/Artwork/"+id);Assert.IsNotNull(pouch,id);
            Assert.AreEqual(FilterMode.Point,pouch.texture.filterMode,id);Assert.AreEqual(1,pouch.texture.mipmapCount,id);
            Assert.AreEqual(PermanentCurrency.Saturncoins,entry.Configuration.Cost.Currency);Assert.AreEqual(5,entry.Configuration.Cost.Amount);
            var card=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Machine "+entry.Configuration.Definition.MachineId&&t.gameObject.activeInHierarchy);
            Assert.IsTrue(card.GetComponentsInChildren<Image>().Any(i=>i.sprite==expected),id);
            Assert.IsTrue(card.GetComponentsInChildren<Text>().Any(t=>t.text==characters.Get(id).DisplayName),id);
            Assert.IsTrue(card.GetComponentsInChildren<Button>().Any(b=>b.name=="Machine artwork button"),id);
        }
        yield return Capture("approved-catalogue-23",true);ui.CloseModal();
        Button("Lobby settings").onClick.Invoke();Button("Daily gift").onClick.Invoke();yield return null;
        var blue=Resources.Load<Sprite>("MonsterPouch/UI/Lobby/Header/daily-pouch");Assert.IsNotNull(blue);
        Assert.AreEqual(FilterMode.Point,blue.texture.filterMode);Assert.AreEqual(1,blue.texture.mipmapCount);
        Assert.IsTrue(Button("Gift artwork").GetComponentsInChildren<Image>().Any(i=>i.sprite==blue));
        yield return Capture("approved-daily-blue-pouch",true);
        Assert.AreEqual(before,File.ReadAllText(Path.Combine(Guest,"guest-profile.json")),"Viewing approved artwork cannot spend, open or grant anything.");
    }
    [UnityTest] public IEnumerator ObtainedUpgradeUsesItsOwnIconAndReviewingItDoesNotGrantAgain()
    {
        yield return Load();
#if UNITY_EDITOR
        var resize=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        resize.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{"393x852"});
        for(int f=0;f<120&&(Screen.width!=393||Screen.height!=852);f++)yield return null;
        Assert.AreEqual(new Vector2Int(393,852),new Vector2Int(Screen.width,Screen.height));
        typeof(LocalGameUI).GetMethod("UpdateLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
        ui.RenderPage();yield return null;
#endif
        Button("Lobby vending").onClick.Invoke();yield return null;
        Button("Machine artwork button").onClick.Invoke();yield return null;
        Button("Draw vending machine").onClick.Invoke();yield return null;
        Button("Confirm spend Saturnkens").onClick.Invoke();yield return null;
        Assert.IsTrue(Store.Reload().OwnsCharacter("flo"));
        Button("Continue after vending reward").onClick.Invoke();yield return null;
        Button("Draw vending machine").onClick.Invoke();yield return null;
        Button("Confirm spend Saturnkens").onClick.Invoke();yield return null;
        var profile=Store.Reload();var receipt=profile.WalletEvents.Last(e=>e.Kind==GuestWalletEventKind.MachineDraw);
        var entry=catalogue.Machines.Single();var reward=entry.Configuration.Definition.Rewards.Single(r=>r.RewardId==receipt.Draw.RewardId);
        Assert.AreEqual(MachineRewardKind.Ability,reward.Kind);
        var ability=characters.Get("flo").Tricks.Single(t=>t.Id==reward.AbilityId);
        var expected=AbilityArtwork.Get(ability.EffectId??ability.Id);Assert.IsNotNull(expected);
        var icon=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Confirmed ability icon");
        Assert.IsTrue(icon.GetComponentsInChildren<Image>().Any(i=>i.sprite==expected));
        Assert.IsTrue(profile.OwnsAbility("flo",ability.Id));Assert.AreEqual(10,profile.FreeBalance(PermanentCurrency.Saturncoins));
        yield return Capture("approved-obtained-ability-icon",true);
        string committed=File.ReadAllText(Path.Combine(Guest,"guest-profile.json"));
        typeof(LocalGameUI).GetMethod("ShowVendingReward",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{entry,receipt});
        yield return null;
        Assert.IsNotNull(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Confirmed ability icon"));
        Assert.AreEqual(committed,File.ReadAllText(Path.Combine(Guest,"guest-profile.json")));
    }
    [UnityTest] public IEnumerator PrizeListUsesImagesAmountsAndGenericCategoriesWithoutSpending()
    {
        CatalogueOverride.SetValue(null,null);yield return Load();
        var approved=Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue");
        var entry=approved.Machines.Single(m=>m.Configuration.Definition.CharacterId=="flo");
        string before=File.ReadAllText(Path.Combine(Guest,"guest-profile.json"));
        foreach(string dimensions in new[]{"393x852","1280x720"})
        {
#if UNITY_EDITOR
            var preview=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
            preview.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{dimensions});
            var size=dimensions.Split('x');
            float deadline=Time.realtimeSinceStartup+10;
            while((Screen.width!=int.Parse(size[0])||Screen.height!=int.Parse(size[1]))&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(dimensions,Screen.width+"x"+Screen.height);
            typeof(LocalGameUI).GetMethod("UpdateLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
#endif
            ui.CloseModal();ui.RenderPage();Button("Lobby vending").onClick.Invoke();yield return null;
            var machine=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Machine "+entry.Configuration.Definition.MachineId);
            machine.GetComponentsInChildren<Button>().Single().onClick.Invoke();yield return null;
            foreach(var reward in entry.Configuration.Definition.Rewards)
            {
                var card=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Reward "+reward.RewardId);
                Assert.IsTrue(card.GetComponentsInChildren<Image>().Any(i=>i.sprite!=null),reward.RewardId+" needs existing reward art.");
                var labels=card.GetComponentsInChildren<Text>();
                Assert.IsFalse(labels.Any(t=>t.text.Contains("%")),"Prize browsing must not expose probabilities.");
                if(reward.Kind==MachineRewardKind.Resource)
                {
                    foreach(var amount in entry.Configuration.Packages.Single(p=>p.RewardId==reward.RewardId).Amounts)
                    {
                        var quantity=labels.Single(t=>t.name=="Preview amount "+amount.Currency);
                        Assert.AreEqual(amount.Amount.ToString(),quantity.text);
                        using(var geometry=new TextGenerator())
                        {
                            geometry.Populate(quantity.text,quantity.GetGenerationSettings(quantity.rectTransform.rect.size));
                            Assert.Greater(geometry.vertexCount,4,"The currency quantity was truncated instead of rendered.");
                        }
                    }
                }
                else
                {
                    Assert.AreEqual(reward.Kind==MachineRewardKind.Character?"PERSONAJE":"NUEVA HABILIDAD",
                        labels.Single(t=>t.name=="Preview category").text);
                    Assert.IsFalse(labels.Any(t=>t.text==characters.Get("flo").DisplayName));
                    foreach(var ability in characters.Get("flo").Tricks)
                        Assert.IsFalse(labels.Any(t=>t.text==GameText.Resolve(ability.Name)));
                }
            }
            yield return Capture("reward-images-"+dimensions,true);
            ui.CanvasRoot.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Vending rewards viewport").verticalNormalizedPosition=0;
            yield return null;yield return Capture("reward-resources-"+dimensions,true);
            Assert.AreEqual(before,File.ReadAllText(Path.Combine(Guest,"guest-profile.json")),"Browsing or scrolling prizes must not draw or debit.");
        }
    }
    void Gift(int amount)
    {
        var gifts=new LocalDailyGiftStore(Path.Combine(Guest,"DailyGifts"));
        gifts.Claim(Store.Reload().ProfileId,DateTime.UtcNow,new DailyGiftTable{Id="synthetic-play-gift",Rewards=new[]{
            new DailyGiftReward{Currency=DailyGiftCurrency.Saturnkens,Amount=amount,Chances=10000}}},out _);
    }
    Button Button(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Last(b=>b.name==name&&b.gameObject.activeInHierarchy);
    IEnumerator Capture(string name,bool required=false)
    {
        string folder=Environment.GetEnvironmentVariable("MONSTER_POUCH_VENDING_CAPTURES");
        if(string.IsNullOrEmpty(folder))
        {if(!required)yield break;folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","local-validation","vending-approved");}
        Directory.CreateDirectory(folder);string path=Path.Combine(folder,name+".png");ScreenCapture.CaptureScreenshot(path);
        for(int i=0;i<30;i++){yield return null;if(i>1&&File.Exists(path)&&new FileInfo(path).Length>24)yield break;}
        Assert.Fail("The current vending UI capture was not written.");
    }
    PlayerMachineConfiguration TestConfiguration(string characterId="flo")
    {
        var rewards=new[]{new MachineReward{RewardId="character",Kind=MachineRewardKind.Character}}
            .Concat(characters.Get(characterId).Tricks.Select((t,i)=>new MachineReward{RewardId="ability-"+i,Kind=MachineRewardKind.Ability,AbilityId=t.Id}))
            .Concat(Enumerable.Range(0,10).Select(i=>new MachineReward{RewardId="resource-"+i,Kind=MachineRewardKind.Resource})).ToArray();
        return new PlayerMachineConfiguration{EnabledForLocalPlay=true,ApprovalId="synthetic-play-only",
            Cost=new PlayerMachineAmount{Currency=PermanentCurrency.Saturncoins,Amount=5},
            Definition=new MachineDefinition{MachineId="test-"+characterId,CharacterId=characterId,CatalogVersion=1,Rewards=rewards},
            Policy=new MachinePolicy{PolicyId="synthetic-guaranteed",Version=1,CharacterChanceByDraw=Enumerable.Repeat(1d,10).ToArray(),
                RewardWeights=rewards.Where(r=>r.Kind!=MachineRewardKind.Character).Select(r=>new MachineWeight{RewardId=r.RewardId,Weight=1}).ToArray()},
            Packages=Enumerable.Range(0,10).Select(i=>new PlayerMachinePackage{RewardId="resource-"+i,
                Amounts=new[]{new PlayerMachineAmount{Currency=PermanentCurrency.Earthcoins,Amount=i+1}}}).ToArray()};
    }
}

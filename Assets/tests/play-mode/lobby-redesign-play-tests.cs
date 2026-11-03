using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class LobbyRedesignPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}.Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] keys={"monster-pouch.language","monster-pouch.player-name",CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey,"monster-pouch.decks.v1","monster-pouch.progress.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;string[] previous,saved;bool[] existed;bool spanish;LocalGameUI ui;string identity;
    float oldEffects,oldMusic;bool hadEffects,hadMusic;
    object gameView;PropertyInfo sizeSelection;int previousSize;Vector2Int previousPixels;MethodInfo resize;
    string GuestDirectory=>Path.Combine(root,"Guest");
    LocalGuestProfileStore Store=>new LocalGuestProfileStore(GuestDirectory);

    [SetUp] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchLobbyRedesignTests",Guid.NewGuid().ToString("N"));
        previous=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,GuestDirectory);Overrides[1].SetValue(null,Path.Combine(root,"Progress"));Overrides[2].SetValue(null,Path.Combine(root,"Decks"));
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        hadEffects=PlayerPrefs.HasKey("monster-pouch.volume");oldEffects=PlayerPrefs.GetFloat("monster-pouch.volume");
        hadMusic=PlayerPrefs.HasKey("monster-pouch.music-volume");oldMusic=PlayerPrefs.GetFloat("monster-pouch.music-volume");
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);GameText.SetLanguage(true);
        var assemblies=AppDomain.CurrentDomain.GetAssemblies();
        var viewType=assemblies.Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);
        var windowType=assemblies.Select(a=>a.GetType("UnityEditor.EditorWindow")).First(t=>t!=null);
        var previewType=assemblies.Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        gameView=windowType.GetMethod("GetWindow",new[]{typeof(Type)}).Invoke(null,new object[]{viewType});
        sizeSelection=viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
        previousSize=(int)sizeSelection.GetValue(gameView);previousPixels=new Vector2Int(Screen.width,Screen.height);
        resize=previewType.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static);
    }
    IEnumerator Resize(int width,int height)
    {
        resize.Invoke(null,new object[]{width+"x"+height});
        for(int f=0;f<120&&(Screen.width!=width||Screen.height!=height);f++)yield return null;
        Assert.AreEqual(new Vector2Int(width,height),new Vector2Int(Screen.width,Screen.height));
        yield return null;yield return null;
        if(ui!=null){typeof(LocalGameUI).GetMethod("UpdateLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);ui.RenderPage();yield return null;}
    }
    IEnumerator Load(bool legacy=true)
    {
        var p=Store.OpenOrCreate(legacy);identity=p.ProfileId;
        yield return Resize(393,852);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float readyDeadline=Time.realtimeSinceStartup+10f;
        while(LoadingArtworkScreen.IsBlocking&&Time.realtimeSinceStartup<readyDeadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking,"The loading artwork must reveal the lobby before checking its controls.");
    }
    IEnumerator LoadEstablishedAccount()
    {
        var store=Store;identity=store.OpenOrCreate(false).ProfileId;
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(config,"bugaloo"));
        foreach(string module in GuestTutorialModules.OrderedIds.Take(6))
            Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(module));
        Object.Destroy(config);yield return Load(false);
    }
    Button Button(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Last(b=>b.name==name&&b.gameObject.activeInHierarchy);
    RectTransform Rect(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Last(t=>t.name==name&&t.gameObject.activeInHierarchy);
    static Rect Bounds(RectTransform rt){var c=new Vector3[4];rt.GetWorldCorners(c);return UnityEngine.Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
    IEnumerator Capture(string name,bool required=false)
    {
        string dir=Environment.GetEnvironmentVariable("MONSTER_POUCH_LOBBY_CAPTURES");
        if(string.IsNullOrEmpty(dir))
        {if(!required)yield break;dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","local-validation","pouch-room");}
        Directory.CreateDirectory(dir);string p=Path.Combine(dir,name+".png");ScreenCapture.CaptureScreenshot(p);
        for(int f=0;f<30;f++){yield return null;if(f>=2&&File.Exists(p)&&new FileInfo(p).Length>24)break;}
        if(required)Assert.IsTrue(File.Exists(p)&&new FileInfo(p).Length>24,"The native pouch-room capture must be written.");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        // Stop the old scene before restoring directory overrides or deleting its
        // account. Resize() also uses this field before loading the next scene.
        var previousUI=ui;ui=null;
        if(previousUI!=null)
        {
            previousUI.CloseModal();previousUI.Match.Menu();
            previousUI.gameObject.SetActive(false);Object.Destroy(previousUI.gameObject);
        }
        yield return null;
        Assert.IsTrue(previousUI==null,"The previous UI must be destroyed before its temporary account is removed.");
        if(identity!=null){PlayerPrefs.DeleteKey(LobbyAvatarSelection.PreferenceKey(identity));PlayerPrefs.DeleteKey("monster-pouch.lobby.mode."+identity);}
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,previous[i]);
        GameText.SetLanguage(spanish);for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();
        if(hadEffects)PlayerPrefs.SetFloat("monster-pouch.volume",oldEffects);else PlayerPrefs.DeleteKey("monster-pouch.volume");
        if(hadMusic)PlayerPrefs.SetFloat("monster-pouch.music-volume",oldMusic);else PlayerPrefs.DeleteKey("monster-pouch.music-volume");PlayerPrefs.Save();
        if(gameView!=null){sizeSelection.SetValue(gameView,previousSize);gameView.GetType().GetMethod("Repaint").Invoke(gameView,null);for(int f=0;f<120&&(Screen.width!=previousPixels.x||Screen.height!=previousPixels.y);f++)yield return null;}
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchLobbyRedesignTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [UnityTest] public IEnumerator FooterTouchesScreenEdgeAndOriginalPedestalRemainsAtPhoneSizes()
    {
        yield return Load();
        foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(393,852),new Vector2Int(768,1024)})
        {
            yield return Resize(size.x,size.y);Canvas.ForceUpdateCanvases();
            Assert.That(Bounds(Rect("Navigation")).yMin,Is.EqualTo(0).Within(1));
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Current brief"));
            var pedestal=Rect("Home wooden pedestal").GetComponent<Image>();
            Assert.AreSame(Resources.Load<LobbyFurnitureDefinition>("MonsterPouch/Styles/Furniture/classic").Pedestal,pedestal.sprite,"Keep the original base.");
            foreach(string name in new[]{"Lobby trophies","Lobby settings","Lobby news","Lobby missions","Lobby vending","Lobby resource Earthkens","Lobby resource Sunkens","Selected battle mode","BATALLA","Locked event"})
            {var bounds=Bounds((RectTransform)Button(name).transform);Assert.GreaterOrEqual(bounds.height,43.9f,name);Assert.GreaterOrEqual(bounds.width,43.9f,name);}
            foreach(var face in ui.CanvasRoot.GetComponentsInChildren<LobbyControlFace>())Assert.IsNotNull(face.GetComponent<CanvasRenderer>(),face.name);
            foreach(var face in ui.CanvasRoot.GetComponentsInChildren<PlayerAvatarGraphic>())Assert.IsNotNull(face.GetComponent<CanvasRenderer>(),face.CharacterId);
            Assert.Less(Bounds(Rect("Selected battle mode")).yMin,Bounds(Rect("Choose featured Monster")).yMin);
            Assert.Greater(Bounds(Rect("Lobby resource Earthkens")).yMin,Bounds(Rect("Lobby trophies")).yMax);
            Assert.Greater(Bounds(Rect("Lobby vending")).yMin,Bounds(Rect("Lobby missions")).yMax);
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Pouchpass reserved"));
            Assert.AreEqual(1,Rect("Lobby vending").GetComponentsInChildren<Button>().Length);
            Assert.AreEqual("0",Rect("Lobby trophy amount").GetComponent<Text>().text);
            yield return Capture("lobby-"+size.x+"x"+size.y);
        }
    }
    [UnityTest] public IEnumerator ModeIsSelectedAbovePlayAndRoutesToExistingBotAndFriendFlows()
    {
        yield return Load();ui.ShowBattleModes();Button("mode-friend").onClick.Invoke();yield return null;
        Assert.AreEqual("friend",ui.SelectedLobbyMode);Assert.IsTrue(Rect("Selected battle mode").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("AMIGO")));
        Button("BATALLA").onClick.Invoke();yield return null;Assert.IsNotNull(Rect("Online room"));ui.CloseModal();
        ui.ShowBattleModes();yield return null;
        var duel=Button("mode-duel");Assert.IsTrue(duel.interactable);
        Assert.IsTrue(duel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("DUELO")));
        // Selecting DUELO starts live UGS discovery. Its screen and cancellation
        // are covered with isolated state by AutomaticDuelScreenPlayTests.
        // This fixture retains the existing AMIGO/BOT navigation checks.
        yield return Capture("battle-modes");Button("mode-bot").onClick.Invoke();yield return null;
        Button("BATALLA").onClick.Invoke();yield return null;Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);ui.Match.Menu();
        yield return Load();Assert.AreEqual("bot",ui.SelectedLobbyMode);
    }
    [UnityTest] public IEnumerator AvatarUnlockAndSelectionPersistWithoutChangingBriefOrOwnership()
    {
        var store=Store;identity=store.OpenOrCreate(false).ProfileId;
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(config,"popow"));
        foreach(string module in GuestTutorialModules.OrderedIds.Take(6))Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(module));
        Object.Destroy(config);yield return Load(false);
        ui.ShowProfileAvatars();yield return null;Assert.IsFalse(Button("Player avatar pokrak").interactable);Assert.IsFalse(ui.SelectProfileAvatar("pokrak"));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,store.UnlockCharacter(ui.Match.Config,"test:cosmetic-unlock","pokrak",GuestUnlockSource.NormalPouch));
        // Settle the existing inventory normalizer before measuring cosmetic-only changes.
        ui.RenderPage();yield return null;
        ui.ShowProfileAvatars();yield return null;Assert.IsTrue(Button("Player avatar pokrak").interactable);
        string guestFile=Path.Combine(GuestDirectory,"guest-profile.json"),deckFile=Path.Combine(root,"Decks","decks.json");
        string guest=File.ReadAllText(guestFile),deck=File.ReadAllText(deckFile);
        Assert.IsTrue(ui.SelectProfileAvatar("pokrak"));yield return null;
        Assert.AreEqual(guest,File.ReadAllText(guestFile));Assert.AreEqual(deck,File.ReadAllText(deckFile));
        Assert.AreEqual("pokrak",ui.SelectedProfileAvatar);yield return Capture("battle-card");
        yield return Load(false);Assert.AreEqual("pokrak",ui.SelectedProfileAvatar);
        ui.ShowProfileAvatars();yield return null;yield return Capture("profile-icons");
    }
    [UnityTest] public IEnumerator HeaderLinksSettingsAndDailyGiftUseRealLocalState()
    {
        yield return Load();
        var profileBefore=Store.OpenOrCreate(true);
        string deck=File.ReadAllText(Path.Combine(root,"Decks","decks.json"));
        Button("Lobby news").onClick.Invoke();yield return null;Assert.IsNotNull(Rect("Lobby notice"));ui.CloseModal();
        Button("Lobby missions").onClick.Invoke();yield return null;Assert.IsNotNull(Button("Open existing tutorials"));yield return Capture("missions");ui.CloseModal();
        Button("Lobby vending").onClick.Invoke();yield return null;Assert.IsNotNull(Rect("Lobby vending panel"));yield return Capture("vending");ui.CloseModal();
        Button("Lobby settings").onClick.Invoke();yield return null;yield return Capture("quick-menu");
        Button("Friendly room").onClick.Invoke();yield return null;Assert.IsNotNull(Rect("Online room"));ui.CloseModal();
        Button("Lobby settings").onClick.Invoke();Button("Open settings").onClick.Invoke();yield return null;yield return Capture("settings");
        var slider=Rect("Settings Effects slider").GetComponent<Slider>();slider.value=.37f;
        Assert.That(PlayerPrefs.GetFloat("monster-pouch.volume"),Is.EqualTo(.37f).Within(.001f));
        Assert.That(((AudioSource)typeof(LocalGameUI).GetField("audioSource",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui)).volume,Is.EqualTo(.37f).Within(.001f));
        Button("Settings rename").onClick.Invoke();yield return null;
        Rect("Player name").GetComponent<InputField>().text="Fafo";Button("Save player name").onClick.Invoke();yield return null;
        Assert.AreEqual("Fafo",PlayerPrefs.GetString("monster-pouch.player-name"));ui.CloseModal();
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Gift available"&&r.gameObject.activeInHierarchy));
        Button("Lobby settings").onClick.Invoke();Button("Daily gift").onClick.Invoke();yield return null;yield return Capture("daily-gift-ready");
        Assert.IsFalse(Rect("Daily gift panel").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("%")),"Opening a gift must not show a probability table.");
        Assert.IsTrue(Button("Claim daily gift").interactable);
        Assert.IsTrue(Button("Gift artwork").interactable);Button("Gift artwork").onClick.Invoke();yield return null;yield return Capture("daily-gift-claimed");
        Assert.IsNotNull(Rect("Daily gift reward"));
        Assert.IsFalse(Rect("Daily gift reward").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("%")));
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Gift available"&&r.gameObject.activeInHierarchy));
        var gifts=new LocalDailyGiftStore(Path.Combine(GuestDirectory,"DailyGifts"));var earned=gifts.Read(identity);
        Assert.AreEqual(1,earned.Receipts.Count);
        Assert.AreEqual("+"+earned.Receipts[0].Amount,Rect("Daily gift reward amount").GetComponent<Text>().text);
        Assert.AreEqual(earned.Receipts[0].Currency.ToString(),Rect("Daily gift reward currency").GetComponent<Text>().text);
        Button("Close daily gift reward").onClick.Invoke();yield return null;
        Button("Lobby resource Earthkens").onClick.Invoke();yield return null;
        Assert.IsFalse(Button("Claim daily gift").interactable);Assert.IsFalse(Button("Gift artwork").interactable);
        Button("Claim daily gift").onClick.Invoke();yield return null;Assert.AreEqual(1,gifts.Read(identity).Receipts.Count);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Daily gift reward"&&r.gameObject.activeInHierarchy),"A repeated claim must not present another reward.");
        ui.CloseModal();yield return Capture("lobby-gift-claimed");Assert.AreEqual(LobbyAccountSummary.Number(earned.Balance(DailyGiftCurrency.Earthkens)),Rect("Lobby amount Earthkens").GetComponent<Text>().text);
        Assert.AreEqual(LobbyAccountSummary.Number(earned.Balance(DailyGiftCurrency.Sunkens)),Rect("Lobby amount Sunkens").GetComponent<Text>().text);
        var profileAfter=Store.OpenOrCreate(true);
        Assert.AreEqual(1,profileAfter.WalletEvents.Count,"The daily receipt must credit the spendable wallet once.");
        Assert.AreEqual(earned.Receipts[0].Amount,profileAfter.WalletEvents[0].Amounts.Single().Amount);
        var expectedCurrency=earned.Receipts[0].Currency==DailyGiftCurrency.Earthkens?PermanentCurrency.Earthcoins:
            earned.Receipts[0].Currency==DailyGiftCurrency.Sunkens?PermanentCurrency.Suncoins:PermanentCurrency.Saturncoins;
        Assert.AreEqual(expectedCurrency,profileAfter.WalletEvents[0].Amounts.Single().Currency);
        // A gift changes the wallet and its revision, never ownership or onboarding.
        profileAfter.WalletEvents=profileBefore.WalletEvents;profileAfter.Revision=profileBefore.Revision;
        Assert.AreEqual(JsonUtility.ToJson(profileBefore),JsonUtility.ToJson(profileAfter));
        Assert.AreEqual(deck,File.ReadAllText(Path.Combine(root,"Decks","decks.json")));
        yield return Load();Button("Lobby resource Earthkens").onClick.Invoke();yield return null;Assert.IsFalse(Button("Claim daily gift").interactable);
        Assert.AreEqual(1,Store.OpenOrCreate(true).WalletEvents.Count,"Reloading imports the same receipt without granting again.");
    }
    [UnityTest] public IEnumerator PouchRoomOpensFiveEmptySlotsSafelyAndClosesWithoutChangingTheAccount()
    {
        yield return LoadEstablishedAccount();ui.RenderPage();yield return null;
        // The first read starts today's empty cycle. It grants no currency or character.
        var before=Store.Reload();Button("Lobby cases").onClick.Invoke();yield return null;
        var initialized=Store.Reload();Assert.AreEqual(before.WalletEvents.Count,initialized.WalletEvents.Count);
        foreach(PermanentCurrency currency in Enum.GetValues(typeof(PermanentCurrency)))Assert.AreEqual(before.FreeBalance(currency),initialized.FreeBalance(currency));
        Assert.AreEqual(before.CharacterUnlocks.Count,initialized.CharacterUnlocks.Count);Assert.AreEqual(before.Starter.Deck.Monster,initialized.Starter.Deck.Monster);
        Button("Close pouch room").onClick.Invoke();yield return null;
        var savedFiles=Directory.GetFiles(root,"*.json",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllText);
        foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(393,852),new Vector2Int(1280,720)})
        {
            GameText.SetLanguage(size.x!=1280);yield return Resize(size.x,size.y);
            var safe=size.x==393?new Rect(19,34,355,780):new Rect(0,0,size.x,size.y);
            typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{safe});
            ui.RenderPage();yield return null;
            var entry=Button("Lobby cases");
            Assert.AreEqual("DAILY CASES",entry.GetComponentInChildren<Text>().text);
            entry.onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
            var room=Rect("Pouch room panel");
            var slots=room.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("Pouch room slot ",StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(5,slots.Length);
            for(int index=0;index<slots.Length;index++)
            {
                var slot=slots.Single(s=>s.name=="Pouch room slot "+index);
                Assert.IsEmpty(slot.GetComponentsInChildren<Button>(),"Empty containers cannot spend or open an invented reward.");
                Assert.AreEqual(GameText.Spanish?"VACÍO":"EMPTY",slot.GetComponentInChildren<Text>().text);
            }
            foreach(var control in slots.Concat(new[]{(RectTransform)Button("Close pouch room").transform}))
            {
                var bounds=Bounds(control);
                Assert.GreaterOrEqual(bounds.xMin,safe.xMin-1,control.name);Assert.LessOrEqual(bounds.xMax,safe.xMax+1,control.name);
                Assert.GreaterOrEqual(bounds.yMin,safe.yMin-1,control.name);Assert.LessOrEqual(bounds.yMax,safe.yMax+1,control.name);
            }
            yield return Capture("pouch-room-"+size.x+"x"+size.y,true);
            Button("Close pouch room").onClick.Invoke();yield return null;
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Pouch room panel"&&r.gameObject.activeInHierarchy));
            Assert.IsNotNull(Button("Lobby settings"));
        }
        CollectionAssert.AreEquivalent(savedFiles.Keys,Directory.GetFiles(root,"*.json",SearchOption.AllDirectories),"Navigation cannot create an inventory or reward receipt.");
        foreach(var savedFile in savedFiles)Assert.AreEqual(savedFile.Value,File.ReadAllText(savedFile.Key),savedFile.Key);
        GameText.SetLanguage(true);
    }
    [UnityTest] public IEnumerator FullMatchVictoryUnlocksOnePouchAndOpeningsKeepTheirSlotsAndCommittedRewards()
    {
        yield return LoadEstablishedAccount();
        yield return CompletePouchMatch(true);
        var earned=Store.Reload();Assert.AreEqual(1,earned.Pouches.Count);Assert.AreEqual(GuestPouchKind.Blue,earned.Pouches[0].Kind);
        Assert.AreEqual(ui.Match.SessionId,earned.PouchVictories.Single().MatchId);
        Assert.AreEqual("bot",earned.PouchVictories.Single().SourceModeId);
        ui.Match.Pause(true);ui.Match.Pause(false);yield return null;
        Assert.AreEqual(1,Store.Reload().Pouches.Count,"Repeated result notifications cannot grant another opportunity.");
        ui.Match.Menu();ui.RenderPage();yield return null;
        Button("Lobby cases").onClick.Invoke();yield return null;
        var open=Button("Pouch room slot 0");open.onClick.Invoke();open.onClick.Invoke();yield return null;
        var opened=Store.Reload();Assert.AreEqual(1,opened.PouchOpenings.Count);Assert.AreEqual(1,opened.Pouches.Count);
        var receipt=opened.PouchOpenings.Single();Assert.AreEqual(GuestPouchRewardKind.Resources,receipt.RewardKind);
        var reward=receipt.Amounts.Single();Assert.AreEqual(earned.FreeBalance(reward.Currency)+reward.Amount,opened.FreeBalance(reward.Currency));
        Assert.AreEqual("+"+reward.Amount,Rect("Pouch room reward amount").GetComponent<Text>().text);
        yield return Capture("pouch-room-resource-reward-393x852",true);
        Button("Continue after pouch reward").onClick.Invoke();yield return null;
        Assert.IsEmpty(Rect("Pouch room slot 0").GetComponentsInChildren<Button>());
        Assert.AreEqual("VACÍO",Rect("Pouch room slot 1").GetComponentInChildren<Text>().text);

        // Synthetic completed victories only control rare art/prize selection in this
        // isolated fixture; the first opportunity above uses the real BO5 callback.
        GrantFixturePouch("fixture-room-two",0);GrantFixturePouch("fixture-room-three",0);
        GrantFixturePouch("fixture-room-character",7500,0,0,0);GrantFixturePouch("fixture-room-five",0,0);
        ui.CloseModal();Button("Lobby cases").onClick.Invoke();yield return null;
        var full=Store.Reload();Assert.AreEqual(5,full.Pouches.Count);
        var dedicated=full.Pouches.Single(p=>p.SlotIndex==3);Assert.AreEqual(GuestPouchKind.Character,dedicated.Kind);
        Assert.IsFalse(full.OwnsCharacter(dedicated.CharacterId));
        var expected=Resources.Load<Sprite>("MonsterPouch/Pouches/Artwork/"+dedicated.CharacterId);
        Assert.IsNotNull(expected);Assert.IsTrue(Rect("Pouch room artwork 3").GetComponentsInChildren<Image>().Any(i=>i.sprite==expected));
        yield return Capture("pouch-room-earned-five-393x852",true);
        yield return Resize(1280,720);Button("Lobby cases").onClick.Invoke();yield return null;
        yield return Capture("pouch-room-earned-five-1280x720",true);
        Button("Pouch room slot 3").onClick.Invoke();yield return null;
        Assert.AreEqual(ui.Match.Config.Get(dedicated.CharacterId).DisplayName,Rect("Pouch room reward character").GetComponent<Text>().text);
        Assert.IsTrue(Store.Reload().OwnsCharacter(dedicated.CharacterId));Assert.AreEqual(2,Store.Reload().PouchOpenings.Count);
        yield return Capture("pouch-room-character-reward-1280x720",true);
        yield return Load(false);
        Assert.AreEqual(5,Store.Reload().Pouches.Count);Assert.AreEqual(2,Store.Reload().PouchOpenings.Count);
        yield return CompletePouchMatch(false);
        Assert.AreEqual(5,Store.Reload().Pouches.Count);Assert.AreEqual(5,Store.Reload().PouchVictories.Count,"A completed defeat does not grant a victory receipt.");
    }
    [UnityTest,Platform("Win")] public IEnumerator FailedPouchVictoryAndOpeningRetryWithoutLosingOrDuplicatingTheReceipt()
    {
        yield return LoadEstablishedAccount();
        int walletEventsBefore=Store.Reload().WalletEvents.Count;
        string primary=Path.Combine(GuestDirectory,"guest-profile.json"),before=File.ReadAllText(primary);
        using(new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            yield return CompletePouchMatch(true);ui.Match.Menu();yield return null;
            Assert.AreEqual(before,File.ReadAllText(primary));
        }
        ui.RenderPage();Button("Lobby cases").onClick.Invoke();yield return null;
        Assert.AreEqual(1,Store.Reload().Pouches.Count);Assert.AreEqual(1,Store.Reload().PouchVictories.Count);
        string granted=File.ReadAllText(primary);
        using(new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Button("Pouch room slot 0").onClick.Invoke();yield return null;
            Assert.IsNotNull(Rect("Lobby notice"));Assert.AreEqual(granted,File.ReadAllText(primary));
            Assert.IsEmpty(Store.Reload().PouchOpenings);Assert.AreEqual(walletEventsBefore,Store.Reload().WalletEvents.Count);
        }
        ui.CloseModal();Button("Lobby cases").onClick.Invoke();yield return null;
        Button("Pouch room slot 0").onClick.Invoke();yield return null;
        Assert.AreEqual(1,Store.Reload().PouchOpenings.Count);Assert.AreEqual(walletEventsBefore+1,Store.Reload().WalletEvents.Count);
        Button("Continue after pouch reward").onClick.Invoke();yield return null;
        Assert.IsEmpty(Rect("Pouch room slot 0").GetComponentsInChildren<Button>());
        yield return Load(false);Assert.AreEqual(1,Store.Reload().Pouches.Count);Assert.AreEqual(1,Store.Reload().PouchOpenings.Count);
    }
    [UnityTest] public IEnumerator PouchRoomPagesReachEveryAccumulatedRewardAndKeepTheCurrentPageAfterOpening()
    {
        yield return LoadEstablishedAccount();
        for(int i=0;i<12;i++)
            if(i%5<3)GrantFixturePouch("page-win-"+i,0);else GrantFixturePouch("page-win-"+i,0,0);
        Assert.AreEqual(12,Store.Reload().Pouches.Count);
        Button("Lobby cases").onClick.Invoke();yield return null;
        string primary=Path.Combine(GuestDirectory,"guest-profile.json"),before=File.ReadAllText(primary);
        for(int page=0;page<3;page++)
        {
            Canvas.ForceUpdateCanvases();var room=Rect("Pouch room panel");
            Assert.AreEqual("PÁGINA "+(page+1)+" / 3",Rect("Pouch room page number").GetComponent<Text>().text);
            var slots=room.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("Pouch room slot ",StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(5,slots.Length);
            for(int row=0;row<5;row++)Assert.IsTrue(slots.Any(t=>t.name=="Pouch room slot "+(page*5+row)));
            Assert.AreEqual(page>0,Button("Pouch room previous page").interactable);
            Assert.AreEqual(page<2,Button("Pouch room next page").interactable);
            Assert.IsFalse(room.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("%")),"The room never displays prize probabilities.");
            foreach(var control in slots.Concat(new[]{(RectTransform)Button("Pouch room next page").transform,(RectTransform)Button("Pouch room previous page").transform}))
            {
                var bounds=Bounds(control);Assert.GreaterOrEqual(bounds.xMin,-1);Assert.LessOrEqual(bounds.xMax,Screen.width+1);
                Assert.GreaterOrEqual(bounds.yMin,-1);Assert.LessOrEqual(bounds.yMax,Screen.height+1);
            }
            if(page<2){Button("Pouch room next page").onClick.Invoke();yield return null;}
        }
        Assert.AreEqual(before,File.ReadAllText(primary),"Pagination cannot change the inventory or pay rewards.");
        yield return Capture("pouch-room-infinite-page-three-393x852",true);
        var open=Button("Pouch room slot 10");open.onClick.Invoke();open.onClick.Invoke();yield return null;
        Assert.AreEqual(1,Store.Reload().PouchOpenings.Count);
        Assert.AreEqual(Store.Reload().Pouches.Single(p=>p.SlotIndex==10).Id,Store.Reload().PouchOpenings.Single().PouchId);
        Button("Continue after pouch reward").onClick.Invoke();yield return null;
        Assert.AreEqual("PÁGINA 3 / 3",Rect("Pouch room page number").GetComponent<Text>().text);
        Assert.IsEmpty(Rect("Pouch room slot 10").GetComponentsInChildren<Button>());
        Button("Pouch room previous page").onClick.Invoke();yield return null;
        Button("Pouch room slot 5").onClick.Invoke();yield return null;
        Assert.AreEqual(2,Store.Reload().PouchOpenings.Count);
        yield return Load(false);Assert.AreEqual(12,Store.Reload().Pouches.Count);Assert.AreEqual(2,Store.Reload().PouchOpenings.Count);
        Button("Lobby cases").onClick.Invoke();yield return null;
        Assert.AreEqual("PÁGINA 1 / 3",Rect("Pouch room page number").GetComponent<Text>().text);
        Assert.IsNotNull(Button("Pouch room slot 0"));
    }

    [UnityTest] public IEnumerator PouchHookRequiresACompleteBestOfFiveAndAcceptsBothBotAndOnlineVictories()
    {
        yield return LoadEstablishedAccount();
        var match=ui.Match;match.StartMatch("bugaloo",StarterRoster.Whelps.ToArray(),20261025);
        var capture=typeof(LocalGameUI).GetMethod("UpdatePouchRoomVictory",BindingFlags.Instance|BindingFlags.NonPublic);
        var roundUI=match.Config.RoundUI;int oldWins=roundUI.maxRoundsToWin;
        Action<MatchPhase,int,int,bool> state=(phase,player,rival,training)=>{
            SetPouchMatchProperty("SessionId",Guid.NewGuid().ToString("N"));SetPouchMatchProperty("Phase",phase);
            SetPouchMatchProperty("PlayerWins",player);SetPouchMatchProperty("BotWins",rival);SetPouchMatchProperty("IsTraining",training);
            capture.Invoke(ui,new object[]{true});
        };
        try
        {
            state(MatchPhase.RoundResult,3,0,false);state(MatchPhase.MatchResult,3,0,true);
            state(MatchPhase.MatchResult,2,0,false);state(MatchPhase.MatchResult,3,3,false);
            state(MatchPhase.MatchResult,3,-1,false);
            roundUI.maxRoundsToWin=1;state(MatchPhase.MatchResult,1,0,false);
            Assert.AreEqual(0,Store.Reload().Pouches.Count);
            roundUI.maxRoundsToWin=3;
            foreach(bool online in new[]{false,true})
            {
                SetPouchMatchProperty("IsOnline",online);state(MatchPhase.MatchResult,3,2,false);
                capture.Invoke(ui,new object[]{true});
            }
            var profile=Store.Reload();Assert.AreEqual(2,profile.Pouches.Count);
            CollectionAssert.AreEquivalent(new[]{"bot","online"},profile.PouchVictories.Select(r=>r.SourceModeId));
        }
        finally{roundUI.maxRoundsToWin=oldWins;SetPouchMatchProperty("IsOnline",false);match.Menu();}
        yield return null;
    }
    void SetPouchMatchProperty(string name,object value)
    {
        var property=ui.Match.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public);
        Assert.IsNotNull(property,name);property.GetSetMethod(true).Invoke(ui.Match,new[]{value});
    }

    IEnumerator CompletePouchMatch(bool won)
    {
        var match=ui.Match;int before=Store.Reload().Pouches.Count;
        match.StartMatch("bugaloo",StarterRoster.Whelps.ToArray(),20261024);yield return null;
        Assert.AreEqual(3,match.WinsToWin);
        for(int round=1;round<=match.WinsToWin;round++)
        {
            match.Ready();
            for(int tick=0;tick<200&&match.Phase==MatchPhase.Combat;tick++)
            {
                var losingSide=won?match.RivalSide:match.LocalSide;
                foreach(var actor in match.Actors.Values.Concat(match.Simulation.CombatOnlyUnits)
                    .Where(a=>a!=null&&a.IsAlive&&a.Side==losingSide).Distinct().ToArray())actor.ApplyDamage(actor.CurrentHealth);
                match.Advance(.1f);yield return null;
            }
            Assert.AreEqual(round,won?match.PlayerWins:match.BotWins);
            if(round<match.WinsToWin)
            {
                Assert.AreEqual(MatchPhase.RoundResult,match.Phase);
                Assert.AreEqual(before,Store.Reload().Pouches.Count,"A single round must not unlock an estuche.");
                match.Advance(match.RoundTransitionDuration+.1f);yield return null;
            }
        }
        Assert.AreEqual(MatchPhase.MatchResult,match.Phase);
    }
    void GrantFixturePouch(string id,params int[] values)
    {
        var rolls=new Queue<int>(values);Func<int,int> sample=maximum=>rolls.Count>0?rolls.Dequeue():0;
        var config=(GuestPouchRoomConfiguration)typeof(LocalGameUI).GetMethod("PouchRoomConfiguration",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
        var utc=DateTime.UtcNow;var request=new GuestPouchVictoryRequest{MatchId=id,SourceModeId="bot",Completed=true,Won=true,CompletedUtc=utc};
        var result=(GuestPouchRoomResult)typeof(LocalGuestProfileStore).GetMethod("RecordPouchVictoryCore",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(Store,new object[]{ui.Match.Config,config,request,utc,sample});
        Assert.AreEqual(GuestPouchRoomStatus.Granted,result.Status);Assert.IsEmpty(rolls);
    }
    [UnityTest] public IEnumerator AccountSummaryUsesTrophiesAndKeepsLargeAmountsInsideHeaderInBothLanguages()
    {
        yield return Load();
        for(int i=0;i<52;i++)RecordTrophyFixture(true);
        Assert.IsTrue((bool)typeof(LocalGameUI).GetMethod("ReloadGuestAvailability",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null));
        Assert.Throws<ArgumentException>(()=>ui.SetLobbyAccountSummary(new LobbyAccountSummary(Guid.NewGuid().ToString("N"),1,1,1)));
        foreach(bool language in new[]{true,false})
        {
            GameText.SetLanguage(language);ui.SetLobbyAccountSummary(new LobbyAccountSummary(identity,18140,18140,1546));yield return Resize(320,568);
            Assert.AreEqual("1 560",Rect("Lobby trophy amount").GetComponent<Text>().text,"Copas use the persisted account, never the presentation override.");
            Assert.AreEqual("18 140",Rect("Lobby amount Earthkens").GetComponent<Text>().text);
            Assert.IsFalse(Rect("Lobby trophies").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("NIVEL")||t.text.Contains("LEVEL")));
            yield return null;Canvas.ForceUpdateCanvases();
            foreach(string name in new[]{"Lobby trophy amount","Lobby amount Earthkens","Lobby amount Sunkens"})
            {var text=Rect(name).GetComponent<Text>();Assert.AreEqual(1,text.cachedTextGenerator.lineCount,name);Assert.GreaterOrEqual(text.cachedTextGenerator.characterCountVisible,text.text.Length,name);}
        }
        ui.SetLobbyAccountSummary(null);GameText.SetLanguage(true);ui.RenderPage();
    }

    void RecordTrophyFixture(bool won)
    {
        var result=Store.RecordTrophyResult(new GuestTrophyResultRequest {
            MatchId=Guid.NewGuid().ToString("N"),OpponentId="ui-test-peer",SourceModeId="online",
            PlayerWins=won?3:0,OpponentWins=won?0:3,VerifiedDigest=new string('A',64)
        });
        Assert.AreEqual(GuestTrophyResultStatus.Applied,result.Status);
    }
    [UnityTest] public IEnumerator AccountTrophiesOpenBoardsAndRoyalRelocksAfterDemotion()
    {
        yield return Load();
        Assert.AreEqual("0",Rect("Lobby trophy amount").GetComponent<Text>().text);
        ui.ShowMapSkins();yield return null;
        Assert.IsFalse(Button("use-style-CampoReal").interactable);
        Assert.IsTrue(Button("use-style-GrandCasaHotel").interactable,"Keep the existing Hotel access.");
        ui.SelectTableSkin(TableStyle.CampoReal);Assert.AreEqual(TableStyle.Naturalis,CardTableTheme.Current);
        ui.CloseModal();Button("Lobby trophies").onClick.Invoke();yield return null;
        var board=Object.FindFirstObjectByType<MonsterPouch.Local.Boards.BoardProgressionScreen>();Assert.IsNotNull(board);
        Assert.IsTrue(board.GetComponentsInChildren<Button>().Single(b=>b.name=="BoardScreen.Close").interactable,"The screen must have a touch exit.");
        var labels=board.GetComponentsInChildren<Text>();
        Assert.AreEqual("0",labels.Single(t=>t.name=="BoardScreen.TrophyValue").text);
        Assert.AreEqual("−5",labels.Single(t=>t.name=="BoardScreen.LossValue").text);
        yield return Capture("trophy-boards-zero",true);
        board.ShowBoards();yield return null;
        Assert.IsFalse(board.GetComponentsInChildren<Button>().Single(b=>b.name=="BoardScreen.Map.royal").interactable);
        board.Close();yield return null;
        for(int i=0;i<20;i++)RecordTrophyFixture(true);
        ui.ShowMapSkins();yield return null;
        Assert.AreEqual(600,ui.AccountTrophies);Assert.IsTrue(Button("use-style-CampoReal").interactable);
        ui.SelectTableSkin(TableStyle.CampoReal);
        float deadline=Time.realtimeSinceStartup+5;
        while(ui.MapTransitionActive&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(ui.MapTransitionActive);Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Current);
        ui.CloseModal();ui.OpenHome();yield return null;
        Button("Lobby trophies").onClick.Invoke();yield return null;
        board=Object.FindFirstObjectByType<MonsterPouch.Local.Boards.BoardProgressionScreen>();
        Assert.AreEqual("−30",board.GetComponentsInChildren<Text>().Single(t=>t.name=="BoardScreen.LossValue").text);
        yield return Capture("trophy-boards-unlocked",true);
        yield return Resize(1280,720);yield return Capture("trophy-boards-unlocked-pc",true);
        yield return Resize(393,852);
        board.Close();yield return null;
        RecordTrophyFixture(false);
        ui.ShowMapSkins();yield return null;
        Assert.AreEqual(570,ui.AccountTrophies);Assert.AreEqual(TableStyle.Volkanar,CardTableTheme.Current);
        Assert.IsFalse(Button("use-style-CampoReal").interactable);
        Assert.AreEqual(600,Store.Reload().HighestTrophies,"Current trophies control selection, not the record.");
        yield return Capture("trophy-royal-relocked",true);
    }

    [UnityTest] public IEnumerator NationRoadUsesFiveCurrentThresholdsAndSeparatesBothCosmetics()
    {
        yield return Load();
        var styles=new[]{TableStyle.Naturalis,TableStyle.Aquarion,TableStyle.Geothar,TableStyle.Volkanar,TableStyle.CampoReal};
        var ids=new[]{"naturalis","aquarion","geothar","volkanar","royal"};
        var losses=new[]{5,10,15,25,30};
        for(int stage=0;stage<styles.Length;stage++)
        {
            if(stage>0)for(int win=0;win<5;win++)RecordTrophyFixture(true);
            ui.ShowMapSkins();yield return null;
            Assert.AreEqual(stage*150,ui.AccountTrophies);
            for(int candidate=0;candidate<styles.Length;candidate++)
                Assert.AreEqual(candidate<=stage,ui.CanSelectTrophyBoard(styles[candidate]),ids[candidate]);
            Assert.IsTrue(ui.CanSelectTrophyBoard(TableStyle.Default));Assert.IsTrue(ui.CanSelectTrophyBoard(TableStyle.GrandCasaHotel));
            if(stage+1<styles.Length)
            {
                var selected=CardTableTheme.Current;var lobby=CardTableTheme.Lobby;
                ui.SelectTableSkin(styles[stage+1]);ui.SelectLobbyStyle(styles[stage+1]);
                Assert.AreEqual(selected,CardTableTheme.Current);Assert.AreEqual(lobby,CardTableTheme.Lobby);
            }
            var state=(MonsterPouch.Local.Boards.BoardScreenState)typeof(LocalGameUI).GetMethod("TrophyBoardState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
            Assert.AreEqual(ids[stage],state.CurrentBoardId);Assert.AreEqual(losses[stage],state.DefeatLoss);Assert.AreEqual(30,state.VictoryGain);
            Assert.AreEqual(stage==4?(long?)null:(stage+1)*150,state.NextStageAt);
            Assert.AreEqual(stage==4?450:(stage+1)*150,state.AdjacentStageMinimum);
            Assert.AreEqual(7,state.Options.Length);
            CollectionAssert.AreEquivalent(new[]{"classic"},state.Options.Where(o=>o.Status==MonsterPouch.Local.Boards.BoardOptionStatus.OwnedCosmetic).Select(o=>o.BoardId));
            Assert.AreEqual(MonsterPouch.Local.Boards.BoardOptionStatus.PaidCosmetic,state.Options.Single(o=>o.BoardId=="lounge").Status);
            Assert.IsTrue(state.Options.Single(o=>o.BoardId=="lounge").CanSelect,"Preserve current beta access without inventing paid ownership.");
        }
        RecordTrophyFixture(true);ui.ShowMapSkins();yield return null;
        Assert.AreEqual(630,ui.AccountTrophies,"Campo Real is the last stage, not a cap.");
        foreach(var style in new[]{TableStyle.Naturalis,TableStyle.Aquarion,TableStyle.Geothar,TableStyle.Volkanar})
            Assert.IsTrue(Button("use-style-"+style).interactable);
        Assert.IsNotNull(ui.CanvasRoot.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Map gallery viewport"));

    }
}

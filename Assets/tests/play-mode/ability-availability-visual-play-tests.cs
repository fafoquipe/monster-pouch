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

public sealed class AbilityAvailabilityVisualPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] keys={"monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps","monster-pouch.language"};
    string directory,captures;string[] oldDirectories,saved;bool[] existed;bool spanish;
    UnitDefinition pricedDefinition;int[] originalCosts;
    LocalGameUI ui;
    InteractionCaptureGameView captureView;

    [UnitySetUp] public IEnumerator Setup()
    {
        // A previous fixture's UI must stop reading its old profile before setup yields.
        foreach(var previous in Object.FindObjectsByType<LocalGameUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {previous.gameObject.SetActive(false);Object.Destroy(previous.gameObject);}
        ui=null;
        pricedDefinition=null;originalCosts=null;captures=null;
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchAbilityAvailabilityTests",Guid.NewGuid().ToString("N"));
        oldDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,Path.Combine(directory,"store-"+i));
        new LocalGuestProfileStore(Path.Combine(directory,"store-0")).OpenOrCreate(true);
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        captureView=new InteractionCaptureGameView();
        yield return captureView.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"atori"}));
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            if(pricedDefinition!=null&&originalCosts!=null)
                for(int i=0;i<originalCosts.Length;i++)pricedDefinition.Tricks[i].Cost=originalCosts[i];
            if(ui!=null)
            {
                ui.CloseModal();ui.Match.Pause(false);ui.Match.Menu();
                // Dispose the reader while its temporary profile still exists.
                ui.gameObject.SetActive(false);Object.Destroy(ui.gameObject);ui=null;
            }
            yield return null;
            for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,oldDirectories[i]);
            GameText.SetLanguage(spanish);
            for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
            PlayerPrefs.Save();
            string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchAbilityAvailabilityTests"))+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
            if(Directory.Exists(target))Directory.Delete(target,true);
        }
        finally { captureView?.RestoreSelection(); }
        if(captureView!=null)yield return captureView.WaitForRestoration();
    }
    RectTransform Sheet=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Unit sheet");
    Button Button(string name)=>Sheet.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    AbilityAvailabilityVisual Card(int index)=>Button("ability-"+index).GetComponent<AbilityAvailabilityVisual>();
    void SetBudget(int value)=>ui.Match.Player.AddCombatCoins(value-ui.Match.Player.Coins);
    void AssertDetails(OwnedUnit owned,int index)
    {
        var detail=Sheet.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Selected ability");
        var description=detail.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Description viewport").GetComponentInChildren<Text>();
        Assert.AreEqual(LocalGameUI.AbilityDetailText(owned,index<0?owned.Definition.BaseAbility:owned.Definition.Tricks[index]),description.text);
    }
    void AssertAllGrey()
    {
        for(int i=0;i<3;i++){Assert.IsTrue(Card(i).IsGrayscale);Assert.IsTrue(Button("ability-"+i).interactable,"Grey abilities remain inspectable.");}
    }

    IEnumerator Capture(string name)
    {
        if(captures==null)
        {
            captures=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","roadmap-v1.2","ability-availability-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffZ"));
            Directory.CreateDirectory(captures);
        }
        float settleUntil=Time.realtimeSinceStartup+.3f;
        while(Time.realtimeSinceStartup<settleUntil)yield return null;
        Canvas.ForceUpdateCanvases();yield return null;yield return null;
        int width=Screen.width,height=Screen.height;
        string path=Path.Combine(captures,name+".png");Assert.IsFalse(File.Exists(path));
        ScreenCapture.CaptureScreenshot(path);
        float deadline=Time.realtimeSinceStartup+10f;
        do
        {
            yield return null;
            if(File.Exists(path)&&new FileInfo(path).Length>24)break;
        }while(Time.realtimeSinceStartup<deadline);
        Assert.IsTrue(File.Exists(path),"Native screenshot missing: "+path);
        byte[] bytes=File.ReadAllBytes(path);Assert.Greater(bytes.Length,24);
        Assert.AreEqual(137,bytes[0]);Assert.AreEqual(80,bytes[1]);Assert.AreEqual(78,bytes[2]);Assert.AreEqual(71,bytes[3]);
        Assert.AreEqual(width,(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19]);
        Assert.AreEqual(height,(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23]);
    }

    [UnityTest] public IEnumerator OnlyCurrentlyAffordableUpgradesHaveColorWhileGreyAbilitiesStillOpenDetails()
    {
        SetBudget(100);var offer=ui.Match.Player.Offers[0];Assert.IsTrue(ui.Match.Buy(0,offer.Token));
        var owned=ui.Match.Player.Owned["atori"];
        pricedDefinition=owned.Definition;originalCosts=pricedDefinition.Tricks.Select(t=>t.Cost).ToArray();
        for(int i=0;i<3;i++)owned.Definition.Tricks[i].Cost=new[]{1,5,9}[i];
        SetBudget(5);ui.ShowInspection(owned,false);yield return null;yield return null;
        Assert.IsTrue(Card(0).IsAvailable);Assert.IsTrue(Card(1).IsAvailable);Assert.IsTrue(Card(2).IsGrayscale);
        Assert.IsFalse(Sheet.GetComponentsInChildren<AbilityAvailabilityVisual>().Single(v=>v.name=="Base ability").IsGrayscale);
        Assert.IsFalse(Button("ability-upgrade").interactable,"The colored base ability is not purchasable.");
        Assert.IsFalse(Sheet.GetComponentsInChildren<AbilityAvailabilityVisual>().Single(v=>v.name=="Illustration").IsGrayscale);
        int coins=ui.Match.Player.Coins,copies=owned.Copies;
        Button("ability-2").onClick.Invoke();AssertDetails(owned,2);
        Assert.IsFalse(Button("ability-upgrade").interactable);
        Button("ability-2").onClick.Invoke();Assert.AreEqual(coins,ui.Match.Player.Coins);Assert.AreEqual(copies,owned.Copies);
        yield return Capture("affordable-0-1-grey-2-detail-open");
        Button("ability-0").onClick.Invoke();AssertDetails(owned,0);Assert.IsTrue(Button("ability-upgrade").interactable);
        Button("ability-upgrade").onClick.Invoke();yield return null;yield return null;
        Assert.IsTrue(owned.Tricks[0]);Assert.AreEqual(coins-1,ui.Match.Player.Coins);Assert.AreEqual(copies+1,owned.Copies);
        AssertAllGrey();AssertDetails(owned,0);
        yield return Capture("purchased-0-remaining-unaffordable-grey");
        SetBudget(20);yield return null;yield return null;
        Assert.IsTrue(Card(0).IsGrayscale,"Purchased abilities are no longer purchasable.");
        Assert.IsTrue(Card(1).IsAvailable);Assert.IsTrue(Card(2).IsAvailable);
    }

    [UnityTest] public IEnumerator LobbyCatalogueAndBaseAbilitiesAlwaysKeepColorWithoutEnablingPurchases()
    {
        ui.Match.Menu();yield return null;
        var show=typeof(LocalGameUI).GetMethod("ShowAbilityCard",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(var definition in ui.Match.Config.Units.Where(unit=>unit!=null))
        {
            var owned=new OwnedUnit(definition);
            show.Invoke(ui,new object[]{owned,false,true,-1,null});
            Assert.IsFalse(Sheet.GetComponentsInChildren<AbilityAvailabilityVisual>().Any(v=>v.IsGrayscale),definition.Id);
            Assert.IsFalse(Button("ability-upgrade").interactable);
            for(int i=0;i<definition.Tricks.Length;i++)
            {
                if(definition.Tricks[i]==null)continue;
                Button("ability-"+i).onClick.Invoke();AssertDetails(owned,i);
                Assert.IsFalse(Card(i).IsGrayscale,definition.Id+":"+definition.Tricks[i].Id);
                Assert.IsFalse(Button("ability-upgrade").interactable);
                Assert.IsFalse(Sheet.GetComponentsInChildren<AbilityAvailabilityVisual>().Single(v=>v.name=="Illustration").IsGrayscale);
            }
            Button("Base ability").onClick.Invoke();AssertDetails(owned,-1);
            Assert.IsFalse(Sheet.GetComponentsInChildren<AbilityAvailabilityVisual>().Single(v=>v.name=="Illustration").IsGrayscale);
            yield return null;
        }
        yield return Capture("lobby-color-base-and-upgrades");
    }

    [UnityTest] public IEnumerator UnaffordableUpgradeOnlyDesaturatesArtworkAndRestoresLiveWhenBudgetChanges()
    {
        var owned=ui.Match.Player.Monster;SetBudget(0);ui.ShowInspection(owned,false);yield return null;
        AssertAllGrey();
        var tile=Button("ability-0");
        Assert.AreNotEqual("MonsterPouch/UI/AbilityGrayscale",tile.GetComponent<Image>().material.shader.name);
        foreach(var label in tile.GetComponentsInChildren<Text>())
            Assert.AreNotEqual(Card(0).GetComponentsInChildren<Image>().First(i=>i.material.shader.name.Contains("Grayscale")).material,label.material);
        Button("ability-0").onClick.Invoke();AssertDetails(owned,0);
        Assert.IsTrue(Sheet.GetComponentsInChildren<AbilityAvailabilityVisual>().Single(v=>v.name=="Illustration").IsGrayscale);
        SetBudget(100);yield return null;yield return null;
        Assert.IsFalse(Card(0).IsGrayscale);Assert.IsTrue(Button("ability-upgrade").interactable);
        Button("Base ability").onClick.Invoke();AssertDetails(owned,-1);
        Assert.IsFalse(Sheet.GetComponentsInChildren<AbilityAvailabilityVisual>().Single(v=>v.name=="Illustration").IsGrayscale);
        Assert.IsFalse(Button("ability-upgrade").interactable);
    }

    [UnityTest] public IEnumerator ContextLocksMissingCopiesAndEnemyOrCollectionCardsKeepInspectionButNeverGainPurchaseColor()
    {
        var owned=ui.Match.Player.Monster;SetBudget(100);ui.ShowInspection(owned,false);yield return null;yield return null;
        Assert.IsTrue(Card(0).IsAvailable);
        ui.Match.LockPreparation(true);yield return null;yield return null;AssertAllGrey();
        Button("ability-0").onClick.Invoke();AssertDetails(owned,0);Assert.IsFalse(Button("ability-upgrade").interactable);
        ui.Match.LockPreparation(false);ui.Match.Pause(true);yield return null;yield return null;AssertAllGrey();
        ui.Match.Pause(false);Assert.IsTrue(ui.Match.SetReady(true));yield return null;yield return null;AssertAllGrey();
        Assert.IsTrue(ui.Match.SetReady(false));yield return null;yield return null;Assert.IsTrue(Card(0).IsAvailable);
        ui.ShowInspection(ui.Match.Bot.Monster,true);yield return null;yield return null;AssertAllGrey();
        Button("ability-1").onClick.Invoke();AssertDetails(ui.Match.Bot.Monster,1);
        var show=typeof(LocalGameUI).GetMethod("ShowAbilityCard",BindingFlags.Instance|BindingFlags.NonPublic);
        show.Invoke(ui,new object[]{owned,false,true,-1,null});yield return null;yield return null;AssertAllGrey();
        Button("ability-2").onClick.Invoke();AssertDetails(owned,2);

        var offer=ui.Match.Player.Offers[0];
        ui.ShowInspection(new OwnedUnit(ui.Match.Config.Get(offer.UnitId)),false);yield return null;yield return null;AssertAllGrey();
        Assert.IsTrue(ui.Match.Buy(0,offer.Token));var whelp=ui.Match.Player.Owned[offer.UnitId];
        Assert.IsTrue(ui.Match.Store(whelp.Definition.Id,true));
        ui.ShowInspection(whelp,false);yield return null;yield return null;AssertAllGrey();
        Button("ability-0").onClick.Invoke();AssertDetails(whelp,0);
        ui.Match.Player.EndPreparation();yield return null;yield return null;AssertAllGrey();
    }

    [UnityTest] public IEnumerator GrayscaleMaterialReallyDesaturatesTheRenderedSpriteAndRestoresItsOriginalMaterial()
    {
        var go=new GameObject("Availability shader probe",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        var graphic=go.GetComponent<Image>();Material original=graphic.material;
        bool available=false;var visual=go.AddComponent<AbilityAvailabilityVisual>();visual.Configure(()=>available);
        Assert.IsTrue(visual.IsGrayscale);Assert.IsTrue(graphic.material.shader.isSupported);
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        source.SetPixels(new[]{Color.red,Color.red,Color.red,Color.red});source.Apply();
        var target=RenderTexture.GetTemporary(8,8,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var previous=RenderTexture.active;var readback=new Texture2D(8,8,TextureFormat.RGBA32,false,true);
        try
        {
            Graphics.Blit(source,target,graphic.material);RenderTexture.active=target;
            readback.ReadPixels(new Rect(0,0,8,8),0,0);readback.Apply();Color sample=readback.GetPixel(4,4);
            Assert.That(sample.r,Is.EqualTo(sample.g).Within(.01f));Assert.That(sample.g,Is.EqualTo(sample.b).Within(.01f));
            Assert.Greater(sample.r,.1f);Assert.Less(sample.r,.7f,"A red source becomes visible grayscale, not a blank image.");
            available=true;visual.Refresh();Assert.IsTrue(visual.IsAvailable);Assert.AreSame(original,graphic.material);
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.Destroy(source);Object.Destroy(readback);Object.Destroy(go);
        }
        yield return null;
    }
}

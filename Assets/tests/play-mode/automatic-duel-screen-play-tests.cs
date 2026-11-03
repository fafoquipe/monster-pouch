using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class AutomaticDuelScreenPlayTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] preferenceKeys={"monster-pouch.language",CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey,
        "monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;string[] oldDirectories,saved;bool[] existed;bool spanish;
    LocalGameUI ui;OnlineRoom online;InteractionCaptureGameView view;CancellationTokenSource cancellation;

    [UnitySetUp] public IEnumerator Setup()
    {
        foreach(var previous in Object.FindObjectsByType<LocalGameUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {previous.gameObject.SetActive(false);Object.Destroy(previous.gameObject);}
        ui=null;online=null;cancellation=null;
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchAutomaticDuelTests",Guid.NewGuid().ToString("N"));
        oldDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,Path.Combine(root,"store-"+i));
        new LocalGuestProfileStore(Path.Combine(root,"store-0")).OpenOrCreate(true);
        saved=preferenceKeys.Select(PlayerPrefs.GetString).ToArray();existed=preferenceKeys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        GameText.SetLanguage(true);CardTableTheme.Select(TableStyle.Default);
        view=new InteractionCaptureGameView();yield return view.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        online=(OnlineRoom)typeof(LocalGameUI).GetField("online",Hidden).GetValue(ui);Assert.IsNotNull(online);
        float deadline=Time.realtimeSinceStartup+10;
        while(LoadingArtworkScreen.IsBlocking&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        typeof(LocalGameUI).GetField("chosen",Hidden).SetValue(ui,"bugaloo");
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        ResetSearchFixture();
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.gameObject.SetActive(false);Object.Destroy(ui.gameObject);ui=null;}
        yield return null;
        if(oldDirectories!=null)for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,oldDirectories[i]);
        GameText.SetLanguage(spanish);
        if(saved!=null)for(int i=0;i<preferenceKeys.Length;i++)
        {if(existed[i])PlayerPrefs.SetString(preferenceKeys[i],saved[i]);else PlayerPrefs.DeleteKey(preferenceKeys[i]);}
        PlayerPrefs.Save();
        if(view!=null){view.RestoreSelection();yield return view.WaitForRestoration();}
        if(root!=null&&Directory.Exists(root))
        {
            string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchAutomaticDuelTests"))+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
            Directory.Delete(target,true);
        }
    }

    [UnityTest] public IEnumerator MobileSearchShowsMonsterSearchingAndCancelWithoutRoomCodes()
    {yield return VerifySearchAndCancel(393,852);}
    [UnityTest] public IEnumerator DesktopSearchShowsMonsterSearchingAndCancelWithoutRoomCodes()
    {yield return VerifySearchAndCancel(1280,720);}
    [UnityTest] public IEnumerator ClosingTheSearchScreenCancelsItsPendingOperation()
    {
        yield return Resize(393,852);ShowSearchFixture();yield return null;
        Assert.IsFalse(cancellation.IsCancellationRequested);
        ui.CloseModal();yield return null;
        Assert.IsTrue(cancellation.IsCancellationRequested,"Closing the screen must not leave an invisible search.");
        Assert.IsNull(typeof(LocalGameUI).GetField("roomView",Hidden).GetValue(ui));
        AssertNoNetwork();
    }

    IEnumerator VerifySearchAndCancel(int width,int height)
    {
        yield return Resize(width,height);ShowSearchFixture();
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
        var modal=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Automatic duel");
        var labels=modal.GetComponentsInChildren<Text>().Where(t=>!string.IsNullOrWhiteSpace(t.text)).ToArray();
        CollectionAssert.AreEquivalent(new[]{"Buscando","CANCELAR"},labels.Select(t=>t.text).ToArray(),
            "The search screen must show no room ID, code entry or technical service details.");
        Assert.IsEmpty(modal.GetComponentsInChildren<InputField>());
        var cancel=modal.GetComponentsInChildren<Button>().Single();
        Assert.IsTrue(cancel.interactable);
        var portrait=modal.GetComponentsInChildren<Image>().FirstOrDefault(i=>i.sprite!=null&&
            i.rectTransform.rect.width>=200&&i.rectTransform.rect.height>=200);
        Assert.IsNotNull(portrait,"The selected Monster must appear above the searching label.");
        foreach(var target in labels.Select(t=>t.rectTransform).Concat(new[]{portrait.rectTransform,cancel.GetComponent<RectTransform>()}))
            AssertInsideScreen(target);
        AssertNoNetwork();
        yield return new WaitForEndOfFrame();
        string folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","local-validation","automatic-duel");
        Directory.CreateDirectory(folder);string path=Path.Combine(folder,"search-"+width+"x"+height+".png");
        var frame=ScreenCapture.CaptureScreenshotAsTexture();
        try{Assert.AreEqual(width,frame.width);Assert.AreEqual(height,frame.height);File.WriteAllBytes(path,frame.EncodeToPNG());}
        finally{Object.Destroy(frame);}
        Assert.Greater(new FileInfo(path).Length,24);
        cancel.onClick.Invoke();yield return null;
        Assert.IsTrue(cancellation.IsCancellationRequested,"Cancel must reach the pending operation's cancellation token.");
        Assert.IsNull(typeof(LocalGameUI).GetField("roomView",Hidden).GetValue(ui));
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Automatic duel"));
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);AssertNoNetwork();
    }
    IEnumerator Resize(int width,int height)
    {
        var preview=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        preview.GetMethod("Resize").Invoke(null,new object[]{width+"x"+height});
        for(int i=0;i<120&&(Screen.width!=width||Screen.height!=height);i++)yield return null;
        Assert.AreEqual(new Vector2Int(width,height),new Vector2Int(Screen.width,Screen.height));
        yield return null;yield return null;
    }
    void ShowSearchFixture()
    {
        // Exercise only the real presentation and cancellation path. SearchDuel would call live UGS.
        cancellation=new CancellationTokenSource();
        typeof(OnlineRoom).GetField("duelCancellation",Hidden).SetValue(online,cancellation);
        typeof(OnlineRoom).GetProperty("DuelState").SetValue(online,AutomaticDuelState.Searching);
        typeof(OnlineRoom).GetProperty("DuelPending").SetValue(online,true);
        typeof(OnlineRoom).GetProperty("AutomaticDuel").SetValue(online,true);
        typeof(LocalGameUI).GetMethod("ShowAutomaticDuelSearch",Hidden).Invoke(ui,null);
    }
    void ResetSearchFixture()
    {
        if(online!=null)
        {
            typeof(OnlineRoom).GetField("duelCancellation",Hidden).SetValue(online,null);
            typeof(OnlineRoom).GetProperty("DuelState").SetValue(online,AutomaticDuelState.Idle);
            typeof(OnlineRoom).GetProperty("DuelPending").SetValue(online,false);
            typeof(OnlineRoom).GetProperty("AutomaticDuel").SetValue(online,false);
        }
        cancellation?.Dispose();cancellation=null;
    }
    void AssertNoNetwork()
    {
        Assert.IsFalse(online.Active);Assert.IsFalse(online.Busy);Assert.IsFalse(ui.Match.IsOnline);
        Assert.IsNull(typeof(OnlineRoom).GetField("duelDirectory",Hidden).GetValue(online));
        Assert.IsNull(typeof(OnlineRoom).GetField("manager",Hidden).GetValue(online));
    }
    static void AssertInsideScreen(RectTransform target)
    {
        var corners=new Vector3[4];target.GetWorldCorners(corners);
        foreach(var corner in corners)
        {
            var pixel=RectTransformUtility.WorldToScreenPoint(null,corner);
            Assert.That(pixel.x,Is.InRange(-1f,Screen.width+1f),target.name+" is outside the horizontal viewport.");
            Assert.That(pixel.y,Is.InRange(-1f,Screen.height+1f),target.name+" is outside the vertical viewport.");
        }
    }
}

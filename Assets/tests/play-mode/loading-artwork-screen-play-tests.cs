using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[Explicit("Visual/window validation is pending screen-control authorization. Use LoadingArtworkMeshPlayTests for geometry.")]
public sealed class LoadingArtworkScreenPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] keys={"monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps","monster-pouch.language"};
    string directory,captures;string[] oldDirectories,saved;bool[] existed;bool spanish;float timeScale;int targetFrameRate;
    InteractionCaptureGameView view;LocalGameUI ui;GameObject ownedEvents,probe;
    string GuestDirectory=>Path.Combine(directory,"guest");
    string GuestFile=>Path.Combine(GuestDirectory,"guest-profile.json");
    bool SelectorOpen=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Starter selection");
    LocalGuestProfile ReadGuest()=>new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(false);

    [UnitySetUp] public IEnumerator Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchLoadingArtworkTests",Guid.NewGuid().ToString("N"));captures=null;
        oldDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        Overrides[0].SetValue(null,GuestDirectory);Overrides[1].SetValue(null,Path.Combine(directory,"progress"));Overrides[2].SetValue(null,Path.Combine(directory,"decks"));
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        foreach(string key in keys)PlayerPrefs.DeleteKey(key);
        timeScale=Time.timeScale;targetFrameRate=Application.targetFrameRate;Time.timeScale=1;
        LoadingArtworkScreen.Current?.Dismiss();yield return null;
        view=new InteractionCaptureGameView();yield return view.Open();
        if(EventSystem.current==null)ownedEvents=new GameObject("Loading artwork test events",typeof(EventSystem));
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            LoadingArtworkScreen.Current?.Dismiss();
            if(probe!=null)Object.Destroy(probe);if(ownedEvents!=null)Object.Destroy(ownedEvents);
            // Stop the old UI before deleting its disposable profile or resizing the next fixture.
            // Its Update would otherwise rebuild the lobby against the removed account store.
            var previousUI=ui;ui=null;
            if(previousUI!=null)
            {
                previousUI.CloseModal();previousUI.Match.Pause(false);previousUI.Match.Menu();
                previousUI.gameObject.SetActive(false);Object.Destroy(previousUI.gameObject);
            }
            yield return null;
            Assert.IsTrue(previousUI==null,"Destroy the old UI before its QA account is removed.");
            Time.timeScale=timeScale;Application.targetFrameRate=targetFrameRate;
            for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,oldDirectories[i]);
            GameText.SetLanguage(spanish);
            for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
            PlayerPrefs.Save();
        }
        finally { view?.RestoreSelection(); }
        yield return null;
        if(view!=null)yield return view.WaitForRestoration();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchLoadingArtworkTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }

    IEnumerator Resize(int width,int height)
    {
#if UNITY_EDITOR
        var preview=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        preview.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{width+"x"+height});
#else
        Screen.SetResolution(width,height,FullScreenMode.Windowed);
#endif
        float deadline=Time.realtimeSinceStartup+10;
        while((Screen.width!=width||Screen.height!=height)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.AreEqual(new Vector2Int(width,height),new Vector2Int(Screen.width,Screen.height));
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
    }

    static Rect Bounds(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(p=>p.x),corners.Min(p=>p.y),corners.Max(p=>p.x),corners.Max(p=>p.y));
    }
    static void Contains(Rect outer,Rect inner,string message)
    {
        Assert.GreaterOrEqual(inner.xMin,outer.xMin-.1f,message+" left");Assert.GreaterOrEqual(inner.yMin,outer.yMin-.1f,message+" bottom");
        Assert.LessOrEqual(inner.xMax,outer.xMax+.1f,message+" right");Assert.LessOrEqual(inner.yMax,outer.yMax+.1f,message+" top");
    }

    void CheckArtwork(LoadingArtworkScreen screen)
    {
        Assert.AreSame(screen,LoadingArtworkScreen.Current);Assert.AreEqual(1,Object.FindObjectsByType<LoadingArtworkScreen>(FindObjectsSortMode.None).Length);
        var canvas=screen.GetComponent<Canvas>();Assert.IsNotNull(canvas);Assert.AreEqual(RenderMode.ScreenSpaceOverlay,canvas.renderMode);
        Assert.AreEqual(32000,canvas.sortingOrder);Assert.IsTrue(canvas.pixelPerfect);
        Assert.AreEqual(1,canvas.scaleFactor,"Loading artwork uses physical pixels.");
        var image=screen.GetComponentsInChildren<Image>().Single(i=>i.name=="Loading artwork image");
        var background=screen.GetComponentsInChildren<Image>().Single(i=>i.name=="Loading artwork background");
        Assert.AreSame(Resources.Load<Sprite>(LoadingArtworkScreen.ResourcePath),image.sprite);Assert.IsTrue(image.preserveAspect);
        Assert.AreEqual(Color.white,image.color);Assert.AreEqual(Image.Type.Simple,image.type);
        Assert.IsFalse(image.GetComponentsInParent<Mask>().Any());Assert.IsFalse(image.GetComponentsInParent<RectMask2D>().Any());
        var full=new Rect(0,0,Screen.width,Screen.height);Contains(Bounds(background.rectTransform),full,"Full-screen backdrop covers the window");
        // Inspect the geometry actually generated by UGUI, including preserveAspect and complete texture UVs.
        var populate=typeof(Image).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null);
        var vertices=new List<UIVertex>();
        using(var helper=new VertexHelper()){populate.Invoke(image,new object[]{helper});helper.GetUIVertexStream(vertices);}
        Assert.AreEqual(6,vertices.Count,"A complete uncut rectangular image uses two triangles.");
        var world=vertices.Select(v=>image.rectTransform.TransformPoint(v.position)).ToArray();
        var rendered=Rect.MinMaxRect(world.Min(v=>v.x),world.Min(v=>v.y),world.Max(v=>v.x),world.Max(v=>v.y));
        float coveredWidth=Mathf.Min(Screen.width,Screen.height*LobbyArtwork.MaximumCoverAspect);
        Contains(rendered,new Rect((Screen.width-coveredWidth)*.5f,0,coveredWidth,Screen.height),"Artwork covers the physical display up to 21:9");
        var ideal=LoadingArtworkScreen.CoverArtwork(new Vector2(Screen.width,Screen.height),image.sprite.rect.size);
        float aspect=1330f/1182;
        float left=Mathf.Floor(ideal.xMin),bottom=Mathf.Floor(ideal.yMin);
        float expectedHeight=Mathf.Ceil(Mathf.Max(ideal.yMax-bottom,(ideal.xMax-left)/aspect));
        Assert.That(rendered.xMin,Is.EqualTo(left).Within(.01f));Assert.That(rendered.yMin,Is.EqualTo(bottom).Within(.01f));
        Assert.That(rendered.width,Is.EqualTo(expectedHeight*aspect).Within(.01f));Assert.That(rendered.height,Is.EqualTo(expectedHeight).Within(.01f));
        Assert.That(rendered.width/rendered.height,Is.EqualTo(aspect).Within(.00001f),"Uniform scale survives pixel adjustment.");
        Contains(rendered,ideal,"Pixel alignment must not shrink the ideal cover frame");
        // Flooring the origin costs <1 px; the least sufficient integer height costs <1 more.
        Assert.Less(rendered.height-ideal.height,2.01f,"Only the minimal pixel alignment expansion is allowed.");
        Assert.Less(rendered.width-ideal.width,2*aspect+.01f);
        var box=Bounds(image.rectTransform);
        foreach(float edge in new[]{box.xMin,box.yMin,box.xMax,box.yMax})
            Assert.That(edge,Is.EqualTo(Mathf.Round(edge)).Within(.01f),"UGUI receives pixel-aligned bounds.");
        if(Screen.width*9L>Screen.height*21L)
        {
            Assert.Greater(rendered.xMin,0);Assert.Less(rendered.xMax,Screen.width);
            Assert.Less(rendered.width-coveredWidth,2*aspect+.01f,"Extreme margins change only by the pixel alignment guard.");
        }
        var logo=screen.GetComponentsInChildren<Image>().Single(i=>i.name=="Loading artwork logo");
        Assert.AreSame(SuppliedBranding.Logo,logo.sprite);Assert.IsTrue(logo.preserveAspect);Assert.IsFalse(logo.raycastTarget);
        Contains(PortraitViewport.NormalizeSafeArea(new Vector2(Screen.width,Screen.height),Screen.safeArea),Bounds(logo.rectTransform),"Existing logo stays safe and separate from background cropping");
        Assert.That(vertices.Min(v=>v.uv0.x),Is.EqualTo(0).Within(.00001f));Assert.That(vertices.Max(v=>v.uv0.x),Is.EqualTo(1).Within(.00001f));
        Assert.That(vertices.Min(v=>v.uv0.y),Is.EqualTo(0).Within(.00001f));Assert.That(vertices.Max(v=>v.uv0.y),Is.EqualTo(1).Within(.00001f));
    }

    IEnumerator Capture(string name,bool duringFade=false)
    {
        if(captures==null){captures=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","roadmap-v1.2","loading-artwork-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffZ"));Directory.CreateDirectory(captures);}
        if(duringFade)
        {
            Assert.IsNotNull(LoadingArtworkScreen.Current);Assert.Greater(LoadingArtworkScreen.Current.Opacity,0);Assert.Less(LoadingArtworkScreen.Current.Opacity,1);
        }
        else {yield return null;yield return null;}
        string path=Path.Combine(captures,name+".png");Assert.IsFalse(File.Exists(path));
        int width=Screen.width,height=Screen.height;ScreenCapture.CaptureScreenshot(path);float deadline=Time.realtimeSinceStartup+10;
        do {yield return null;if(File.Exists(path)&&new FileInfo(path).Length>24)break;}while(Time.realtimeSinceStartup<deadline);
        Assert.IsTrue(File.Exists(path),"Native screenshot missing: "+path);byte[] bytes=File.ReadAllBytes(path);Assert.Greater(bytes.Length,24);
        CollectionAssert.AreEqual(new byte[]{137,80,78,71,13,10,26,10},bytes.Take(8).ToArray());
        Assert.AreEqual(width,(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19]);Assert.AreEqual(height,(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23]);
    }

    List<RaycastResult> Hits(Vector2 point)
    {
        Canvas.ForceUpdateCanvases();var result=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},result);Assert.IsNotEmpty(result);return result;
    }
    void Click(Button button)
    {
        Assert.IsTrue(button.interactable);var point=Bounds(button.GetComponent<RectTransform>()).center;
        var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits(point)[0].gameObject);
        Assert.AreSame(button.gameObject,target,"The intended control must receive the top visible pointer hit.");
        ExecuteEvents.Execute(target,new PointerEventData(EventSystem.current){position=point},ExecuteEvents.pointerClickHandler);
    }
    Button Named(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    const float LoadingTimeout = LoadingArtworkScreen.MinimumDisplaySeconds + LoadingArtworkScreen.FadeSeconds + 6;

    IEnumerator WaitUntilGone()
    {
        float deadline=Time.realtimeSinceStartup+LoadingTimeout;
        while(LoadingArtworkScreen.Current!=null&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsNull(LoadingArtworkScreen.Current,"The ready screen must release the loading overlay.");yield return null;
        Assert.AreEqual(0,Object.FindObjectsByType<LoadingArtworkScreen>(FindObjectsSortMode.None).Length);
    }
    IEnumerator LoadReadyScene()
    {
        var launch=LoadingArtworkScreen.Show();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        Assert.AreSame(launch,LoadingArtworkScreen.Current,"The launch overlay survives until the destination finishes Awake.");
        Assert.IsTrue(launch.IsFading,"The real ready-view hook must request completion of the existing startup overlay.");
        yield return WaitUntilGone();yield return null;yield return null;
    }

    [UnityTest] public IEnumerator SuppliedArtworkCoversPortraitTabletDesktopAndKeepsOnlyExtremeSideMargins()
    {
        var screen=LoadingArtworkScreen.Show();Assert.AreEqual(1,screen.Opacity);Assert.IsFalse(screen.IsFading);
        var sizes=new[]{new Vector2Int(320,568),new Vector2Int(393,852),new Vector2Int(1080,2400),new Vector2Int(1536,2048),new Vector2Int(1024,768),new Vector2Int(1920,1080),new Vector2Int(2520,1080),new Vector2Int(3440,1440),new Vector2Int(5120,1440)};
        foreach(var size in sizes){yield return Resize(size.x,size.y);CheckArtwork(screen);yield return Capture(size.x+"x"+size.y);}
        Assert.IsFalse(screen.IsFading,"Elapsed time alone cannot dismiss loading before readiness.");Assert.AreEqual(1,screen.Opacity);
    }

    [UnityTest] public IEnumerator UnscaledFadeBlocksInputUntilCompleteAndCleansTheSingletonOnDismissal()
    {
        int clicks=0;probe=new GameObject("Loading input probe",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
        var canvas=probe.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31990;
        var target=new GameObject("Probe button",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));target.transform.SetParent(probe.transform,false);
        var rect=target.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var button=target.GetComponent<Button>();button.onClick.AddListener(()=>clicks++);
        Assert.GreaterOrEqual(LoadingArtworkScreen.MinimumDisplaySeconds,3f,"Startup artwork needs time to be read.");
        Assert.GreaterOrEqual(LoadingArtworkScreen.FadeSeconds,.6f,"Startup uses a deliberate reveal, independent of the map transition.");
        bool navigation=EventSystem.current.sendNavigationEvents;float shownAt=Time.realtimeSinceStartup;
        var screen=LoadingArtworkScreen.Show();Assert.AreSame(screen,LoadingArtworkScreen.Show());yield return null;yield return null;
        Assert.IsFalse(EventSystem.current.sendNavigationEvents);
        Assert.AreSame(screen,Hits(new Vector2(Screen.width*.5f,Screen.height*.5f))[0].gameObject.GetComponentInParent<LoadingArtworkScreen>());
        Assert.AreEqual(0,clicks);Time.timeScale=0;screen.CompleteLoading();screen.CompleteLoading();Assert.IsTrue(screen.IsFading);
        float start=Time.realtimeSinceStartup,previous=1;bool intermediate=false;
        while(LoadingArtworkScreen.Current!=null&&Time.realtimeSinceStartup-start<LoadingTimeout)
        {
            float alpha=screen.Opacity;Assert.That(alpha,Is.InRange(0f,previous+.0001f));previous=alpha;
            if(alpha>0&&alpha<1){intermediate=true;Assert.GreaterOrEqual(Time.realtimeSinceStartup-shownAt,LoadingArtworkScreen.MinimumDisplaySeconds-.05f,"The supplied image must remain readable before fading.");}
            if(alpha>0)Assert.AreSame(screen,Hits(new Vector2(Screen.width*.5f,Screen.height*.5f))[0].gameObject.GetComponentInParent<LoadingArtworkScreen>());
            yield return null;
        }
        Assert.IsTrue(intermediate,"The loading art fades progressively rather than disappearing in one step.");
        Assert.Less(Time.realtimeSinceStartup-start,LoadingTimeout);yield return WaitUntilGone();Assert.AreEqual(navigation,EventSystem.current.sendNavigationEvents);Click(button);Assert.AreEqual(1,clicks);
        var replacement=LoadingArtworkScreen.Show();Assert.AreNotSame(screen,replacement);replacement.CompleteLoading();replacement.Dismiss();
        Assert.IsNull(LoadingArtworkScreen.Current);yield return null;Click(button);Assert.AreEqual(2,clicks);
        Assert.AreEqual(0,Object.FindObjectsByType<LoadingArtworkScreen>(FindObjectsSortMode.None).Length);
        var disabled=LoadingArtworkScreen.Show();disabled.enabled=false;yield return null;
        Assert.IsNull(LoadingArtworkScreen.Current);Click(button);Assert.AreEqual(3,clicks);
        var afterDisable=LoadingArtworkScreen.Show();yield return null;
        Assert.AreEqual(1,Object.FindObjectsByType<LoadingArtworkScreen>(FindObjectsSortMode.None).Length);
        afterDisable.Dismiss();yield return null;Assert.AreEqual(navigation,EventSystem.current.sendNavigationEvents);
    }

    [UnityTest] public IEnumerator SlowStartupCannotConsumeTheArtworkBeforeItsFirstVisibleFrame()
    {
        Time.timeScale=0;
        var screen=LoadingArtworkScreen.Show();screen.CompleteLoading();
        // Simulate synchronous Awake/resource loading before Unity can render the image.
        System.Threading.Thread.Sleep((int)((LoadingArtworkScreen.MinimumDisplaySeconds+.25f)*1000));
        yield return null;yield return null;
        Assert.AreEqual(1,screen.Opacity,"The first visible frames must show the complete artwork, not a nearly finished fade.");
        yield return new WaitForSecondsRealtime(2.5f);
        Assert.AreEqual(1,screen.Opacity,"Loading time before rendering must not shorten the readable hold.");
        yield return WaitUntilGone();
    }

    [UnityTest] public IEnumerator ALongFrameDuringTheFadeCannotDismissTheArtworkInOneStep()
    {
        var screen=LoadingArtworkScreen.Show();screen.CompleteLoading();
        float deadline=Time.realtimeSinceStartup+LoadingTimeout;
        while(screen!=null&&screen.Opacity>=1&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsNotNull(screen);Assert.That(screen.Opacity,Is.InRange(.9f,.99999f));
        System.Threading.Thread.Sleep(1000);
        yield return null;yield return null;
        Assert.IsNotNull(LoadingArtworkScreen.Current,"A slow startup frame must not skip the entire fade.");
        Assert.Greater(screen.Opacity,0);Assert.IsTrue(LoadingArtworkScreen.IsBlocking);
        yield return WaitUntilGone();
    }

    [UnityTest] public IEnumerator ExistingAccountReturnsToLobbyWithoutStarterOrTutorialAndKeepsItsIdentity()
    {
        var stored=new LocalGuestProfileStore(GuestDirectory).OpenOrCreate(true);string receipt=File.ReadAllText(GuestFile);
        yield return LoadReadyScene();
        Assert.IsFalse(SelectorOpen);Assert.IsFalse(ui.Match.IsTraining);Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
        Assert.AreEqual(stored.ProfileId,ReadGuest().ProfileId);Assert.AreEqual(receipt,File.ReadAllText(GuestFile));
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="BATALLA"),"An existing account resumes its lobby.");
        // Readiness without a new launch must never manufacture another loading screen.
        LoadingArtworkScreen.RevealReadyView(ui);yield return null;Assert.IsNull(LoadingArtworkScreen.Current);
        var originalGroup=ui.CanvasRoot.GetComponent<CanvasGroup>();bool interactable=originalGroup==null||originalGroup.interactable,raycasts=originalGroup==null||originalGroup.blocksRaycasts;
        LoadingArtworkScreen.Show();LoadingArtworkScreen.RevealReadyView(ui);
        var blocked=ui.CanvasRoot.GetComponent<CanvasGroup>();Assert.IsNotNull(blocked);Assert.IsFalse(blocked.interactable);Assert.IsFalse(blocked.blocksRaycasts);
        float fadeDeadline=Time.realtimeSinceStartup+LoadingTimeout;
        while(LoadingArtworkScreen.Current!=null&&LoadingArtworkScreen.Current.Opacity>=.65f&&Time.realtimeSinceStartup<fadeDeadline)yield return null;
        yield return Capture("fade-reveals-existing-lobby",true);
        yield return WaitUntilGone();
        var restored=ui.CanvasRoot.GetComponent<CanvasGroup>();
        if(originalGroup==null)Assert.IsNull(restored);else{Assert.AreSame(originalGroup,restored);Assert.AreEqual(interactable,restored.interactable);Assert.AreEqual(raycasts,restored.blocksRaycasts);}
        yield return Capture("existing-lobby-ready-after-loading");
        foreach(var size in new[]{new Vector2Int(393,852),new Vector2Int(1920,1080)})
        {
            yield return Resize(size.x,size.y);
            yield return Capture("existing-lobby-"+size.x+"x"+size.y);
            ui.ShowProgression();yield return null;
            var logo=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.sprite==SuppliedBranding.Logo);
            Assert.IsTrue(logo.preserveAspect,"The independent logo must retain its proportions.");
            yield return Capture("eronya-progress-logo-"+size.x+"x"+size.y);
            ui.CloseModal();yield return null;
            typeof(LocalGameUI).GetMethod("ShowLobbySettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
            yield return null;
            Assert.AreEqual(1,ui.CanvasRoot.GetComponentsInChildren<Image>().Count(i=>i.sprite==SuppliedBranding.Logo));
            yield return Capture("eronya-settings-logo-"+size.x+"x"+size.y);
            ui.CloseModal();yield return null;
        }
        Assert.AreEqual(receipt,File.ReadAllText(GuestFile),"Branding and resizing must preserve the existing account.");
    }

    [UnityTest] public IEnumerator NewAccountStillChoosesItsMonsterThenEntersAndResumesTheTutorial()
    {
        yield return LoadReadyScene();Assert.IsTrue(SelectorOpen);Assert.IsNull(ReadGuest().Starter);Assert.IsFalse(ui.Match.IsTraining);
        yield return Capture("new-account-starter-selection");
        Assert.IsFalse(Named("Confirm starter selection").interactable);
        Click(Named("Starter choice popow"));yield return null;Click(Named("Confirm starter selection"));
        float deadline=Time.realtimeSinceStartup+5;
        while(!ui.FirstContactTutorialActive&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(SelectorOpen);Assert.IsTrue(ui.FirstContactTutorialActive);Assert.IsTrue(ui.Match.IsTraining);
        yield return Capture("new-account-tutorial");
        var chosen=ReadGuest();Assert.AreEqual("popow",chosen.Starter.Deck.Monster);Assert.AreEqual(GuestOnboardingStage.Tutorial,chosen.OnboardingStage);
        string identity=chosen.ProfileId,receipt=File.ReadAllText(GuestFile);
        yield return LoadReadyScene();
        deadline=Time.realtimeSinceStartup+5;while(!ui.FirstContactTutorialActive&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(SelectorOpen);Assert.IsTrue(ui.FirstContactTutorialActive);Assert.AreEqual("popow",ui.Match.Player.Monster.Definition.Id);
        Assert.AreEqual(identity,ReadGuest().ProfileId);Assert.AreEqual(receipt,File.ReadAllText(GuestFile));
    }
}

// Native UGUI geometry only. No GameView/window API, scene load, capture, input or profile access.
public sealed class LoadingArtworkMeshPlayTests
{
    static void Contains(Rect outer,Rect inner,string message)
    {
        Assert.GreaterOrEqual(inner.xMin,outer.xMin-.1f,message+" left");Assert.GreaterOrEqual(inner.yMin,outer.yMin-.1f,message+" bottom");
        Assert.LessOrEqual(inner.xMax,outer.xMax+.1f,message+" right");Assert.LessOrEqual(inner.yMax,outer.yMax+.1f,message+" top");
    }

    [TestCase(320,568)]
    [TestCase(393,852)]
    [TestCase(1080,2400)]
    [TestCase(768,1024)]
    [TestCase(1536,2048)]
    [TestCase(1024,768)]
    [TestCase(1920,1080)]
    [TestCase(2560,1600)]
    [TestCase(2520,1080)]
    [TestCase(3440,1440)]
    [TestCase(5120,1440)]
    public void NativePixelAdjustedMeshCoversVirtualViewport(int width,int height)
    {
        GameObject root=null;
        try
        {
            var sprite=Resources.Load<Sprite>(LoadingArtworkScreen.ResourcePath);
            Assert.IsNotNull(sprite);Assert.AreEqual(new Rect(0,0,1330,1182),sprite.rect);
            root=new GameObject("Loading mesh geometry probe",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.pixelPerfect=true;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;
            // The virtual viewport is a child rect. It never resizes the real canvas or window.
            var viewport=new GameObject("Virtual viewport",typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform,false);viewport.anchorMin=viewport.anchorMax=viewport.pivot=Vector2.zero;
            viewport.anchoredPosition=Vector2.zero;viewport.sizeDelta=new Vector2(width,height);
            var image=new GameObject("Unrendered loading mesh",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
            image.enabled=false; // Mesh is populated manually; nothing is submitted for display.
            image.transform.SetParent(viewport,false);image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
            image.rectTransform.anchorMin=image.rectTransform.anchorMax=image.rectTransform.pivot=Vector2.zero;
            var ideal=LoadingArtworkScreen.CoverArtwork(new Vector2(width,height),sprite.rect.size);
            var align=typeof(LoadingArtworkScreen).GetMethod("AlignArtworkToPixels",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.IsNotNull(align,"The installed runtime must include the pixel-cover repair.");
            var fit=(Rect)align.Invoke(null,new object[]{ideal,sprite.rect.size});
            image.rectTransform.anchoredPosition=fit.position;image.rectTransform.sizeDelta=fit.size;
            Canvas.ForceUpdateCanvases();
            Assert.AreSame(canvas,image.canvas);Assert.AreEqual(1,canvas.scaleFactor);Assert.IsTrue(canvas.pixelPerfect);
            Assert.IsFalse(image.isActiveAndEnabled,"This fixture must not display its test image.");
            // This calls the real native PixelAdjustRect path on the current canvas.
            var adjusted=image.GetPixelAdjustedRect();
            Assert.That(adjusted.width,Is.EqualTo(fit.width).Within(.01f));Assert.That(adjusted.height,Is.EqualTo(fit.height).Within(.01f));
            var populate=typeof(Image).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null);
            var vertices=new List<UIVertex>();
            using(var helper=new VertexHelper()){populate.Invoke(image,new object[]{helper});helper.GetUIVertexStream(vertices);}
            Assert.AreEqual(6,vertices.Count);
            var local=vertices.Select(v=>viewport.InverseTransformPoint(image.rectTransform.TransformPoint(v.position))).ToArray();
            var rendered=Rect.MinMaxRect(local.Min(v=>v.x),local.Min(v=>v.y),local.Max(v=>v.x),local.Max(v=>v.y));
            float coveredWidth=Mathf.Min(width,height*LobbyArtwork.MaximumCoverAspect);
            Contains(rendered,new Rect((width-coveredWidth)*.5f,0,coveredWidth,height),"Native mesh covers virtual viewport up to 21:9");
            Contains(rendered,ideal,"Pixel adjustment cannot shrink the ideal frame");
            float aspect=1330f/1182,left=Mathf.Floor(ideal.xMin),bottom=Mathf.Floor(ideal.yMin);
            float expectedHeight=Mathf.Ceil(Mathf.Max(ideal.yMax-bottom,(ideal.xMax-left)/aspect));
            Assert.That(rendered.xMin,Is.EqualTo(left).Within(.01f));Assert.That(rendered.yMin,Is.EqualTo(bottom).Within(.01f));
            Assert.That(rendered.width,Is.EqualTo(expectedHeight*aspect).Within(.01f));Assert.That(rendered.height,Is.EqualTo(expectedHeight).Within(.01f));
            Assert.That(rendered.width/rendered.height,Is.EqualTo(aspect).Within(.00001f));
            Assert.Less(rendered.height-ideal.height,2.01f);Assert.Less(rendered.width-ideal.width,2*aspect+.01f);
            if(width*9L>height*21L)
            {
                Assert.Greater(rendered.xMin,0);Assert.Less(rendered.xMax,width);
                Assert.Less(rendered.width-coveredWidth,2*aspect+.01f);
            }
            Assert.That(vertices.Min(v=>v.uv0.x),Is.EqualTo(0).Within(.00001f));Assert.That(vertices.Max(v=>v.uv0.x),Is.EqualTo(1).Within(.00001f));
            Assert.That(vertices.Min(v=>v.uv0.y),Is.EqualTo(0).Within(.00001f));Assert.That(vertices.Max(v=>v.uv0.y),Is.EqualTo(1).Within(.00001f));
        }
        finally
        {
            // Synchronous NUnit cases: remove the private objects before any frame can render.
            if(root!=null)Object.DestroyImmediate(root);
        }
    }
}

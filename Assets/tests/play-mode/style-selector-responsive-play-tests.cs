using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class StyleSelectorResponsivePlayTests
{
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(name => typeof(LocalGameUI).GetProperty(name, BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] keys = { CardTableTheme.PreferenceKey, CardTableTheme.LobbyPreferenceKey, "monster-pouch.language",
        "monster-pouch.progress.v1", "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps" };
    string directory;
    string[] oldDirectories, saved;
    bool[] existed;
    bool spanish;
    LocalGameUI ui;
    Canvas canvas;
    CanvasScaler scaler;
#if UNITY_EDITOR
    object editorGameView;
    PropertyInfo editorSizeSelection;
    int previousEditorSizeIndex;
    Vector2Int previousEditorPixels;
#endif

    [UnitySetUp] public IEnumerator Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchStyleSelectorTests", Guid.NewGuid().ToString("N"));
        oldDirectories = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        for (int i=0;i<Overrides.Length;i++) Overrides[i].SetValue(null,Path.Combine(directory,"store-"+i));
        new LocalGuestProfileStore(Path.Combine(directory,"store-0")).OpenOrCreate(true);
        saved = keys.Select(PlayerPrefs.GetString).ToArray(); existed = keys.Select(PlayerPrefs.HasKey).ToArray(); spanish = GameText.Spanish;
#if UNITY_EDITOR
        // Batch GameView may begin at 640x480. Use the existing Editor preview
        // helper to render the compact portrait, preserving the user's selection.
        var assemblies=AppDomain.CurrentDomain.GetAssemblies();
        var viewType=assemblies.Select(a=>a.GetType("UnityEditor.GameView")).FirstOrDefault(t=>t!=null);
        var windowType=assemblies.Select(a=>a.GetType("UnityEditor.EditorWindow")).FirstOrDefault(t=>t!=null);
        var previewType=assemblies.Select(a=>a.GetType("MonsterPouchLayoutPreview")).FirstOrDefault(t=>t!=null);
        Assert.IsNotNull(viewType); Assert.IsNotNull(windowType); Assert.IsNotNull(previewType);
        editorGameView=windowType.GetMethod("GetWindow",new[]{typeof(Type)}).Invoke(null,new object[]{viewType});
        editorSizeSelection=viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
        previousEditorSizeIndex=(int)editorSizeSelection.GetValue(editorGameView);
        previousEditorPixels=new Vector2Int(Screen.width,Screen.height);
        previewType.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{"360x640"});
        for(int frame=0;frame<120&&(Screen.width!=360||Screen.height!=640);frame++)yield return null;
        Assert.AreEqual(new Vector2Int(360,640),new Vector2Int(Screen.width,Screen.height),"The Editor did not render the requested compact portrait.");
#endif
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.IsNotNull(ui);
        ui.Match.enabled = false; ui.enabled = false;
        canvas = ui.CanvasRoot.GetComponentInParent<Canvas>(); scaler = canvas.GetComponent<CanvasScaler>();
        Debug.Log("Style selector regression: Screen="+Screen.width+"x"+Screen.height+", initial Canvas scale="+canvas.scaleFactor);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        // A surviving UI would reload its deleted temporary profile while the next
        // fixture waits for GameView to resize. Retire it before restoring any state.
        var previousUI=ui;ui=null;
        if(previousUI!=null)
        {
            previousUI.CloseModal();
            previousUI.gameObject.SetActive(false);
            Object.Destroy(previousUI.gameObject);
        }
        yield return null;
        Assert.IsTrue(previousUI==null,"The temporary UI must be destroyed before its profile is removed.");
#if UNITY_EDITOR
        if(editorGameView!=null&&editorSizeSelection!=null)
        {
            editorSizeSelection.SetValue(editorGameView,previousEditorSizeIndex);
            editorGameView.GetType().GetMethod("Repaint",BindingFlags.Public|BindingFlags.Instance).Invoke(editorGameView,null);
            for(int frame=0;frame<120&&(Screen.width!=previousEditorPixels.x||Screen.height!=previousEditorPixels.y);frame++)yield return null;
        }
#endif
        for (int i=0;i<Overrides.Length;i++) Overrides[i].SetValue(null,oldDirectories[i]);
        GameText.SetLanguage(spanish);
        for (int i=0;i<keys.Length;i++) { if (existed[i]) PlayerPrefs.SetString(keys[i],saved[i]); else PlayerPrefs.DeleteKey(keys[i]); }
        PlayerPrefs.Save();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchStyleSelectorTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory); Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target)) Directory.Delete(target,true);
#if UNITY_EDITOR
        if(editorGameView!=null&&editorSizeSelection!=null)
            Assert.AreEqual(previousEditorSizeIndex,(int)editorSizeSelection.GetValue(editorGameView),"Restore the original Editor GameView size selection.");
#endif
    }

    RectTransform Panel => ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Scenario skins");
    StyleSelectorTouchTargets Layout => Panel.GetComponent<StyleSelectorTouchTargets>();
    void SetScale(float scale)
    {
        scaler.scaleFactor=scale; canvas.scaleFactor=scale; ui.CanvasRoot.anchoredPosition=Vector2.zero;
        Canvas.ForceUpdateCanvases(); Layout.Refresh(); Canvas.ForceUpdateCanvases();
    }
    static Rect VisualBounds(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    static Rect HitBounds(Button button)
    {
        var image=button.GetComponent<Image>(); Rect visual=VisualBounds(image.rectTransform),local=image.rectTransform.rect;
        Vector4 pad=image.raycastPadding; float sx=visual.width/local.width,sy=visual.height/local.height;
        return Rect.MinMaxRect(visual.xMin+pad.x*sx,visual.yMin+pad.y*sy,visual.xMax-pad.z*sx,visual.yMax-pad.w*sy);
    }
    bool FullyVisible(Component component)
    {
        var gallery=component.GetComponentInParent<LobbySkinScrollView>();
        if(gallery==null)return true;
        var scroll=gallery.GetComponent<ScrollRect>();
        var row=component.transform;
        while(row.parent!=null&&row.parent!=scroll.content)row=row.parent;
        var bounds=VisualBounds((RectTransform)row);var viewport=VisualBounds(scroll.viewport);
        return bounds.yMin>=viewport.yMin-.01f&&bounds.yMax<=viewport.yMax+.01f;
    }

    void CheckGeometryAndText()
    {
        Rect safe=VisualBounds(Panel); var buttons=Panel.GetComponentsInChildren<Button>().Where(FullyVisible).ToArray();
        int choices=buttons.Count(b=>b.name.StartsWith("use-style-",StringComparison.Ordinal)||b.name=="use-lobby-aireola");
        Assert.AreEqual(5+choices,buttons.Length);var targets=buttons.Select(HitBounds).ToArray();
        for(int i=0;i<buttons.Length;i++)
        {
            Assert.That(((RectTransform)buttons[i].transform).rect.height,Is.EqualTo(68),"Artwork geometry must remain unchanged.");
            Assert.GreaterOrEqual(targets[i].width,43.99f,buttons[i].name);Assert.GreaterOrEqual(targets[i].height,43.99f,buttons[i].name);
            Assert.GreaterOrEqual(targets[i].xMin,safe.xMin-.01f);Assert.LessOrEqual(targets[i].xMax,safe.xMax+.01f);
            Assert.GreaterOrEqual(targets[i].yMin,safe.yMin-.01f);Assert.LessOrEqual(targets[i].yMax,safe.yMax+.01f);
            for(int j=i+1;j<targets.Length;j++)Assert.IsFalse(targets[i].Overlaps(targets[j]),buttons[i].name+" overlaps "+buttons[j].name);
        }
        foreach(var label in Panel.GetComponentsInChildren<Text>().Where(FullyVisible))
        {
            Assert.GreaterOrEqual(label.fontSize*canvas.scaleFactor,11.99f,label.text);
            Assert.LessOrEqual(label.preferredWidth,label.rectTransform.rect.width+.1f,"Text exceeds its width: "+label.text);
            Assert.LessOrEqual(label.preferredHeight,label.rectTransform.rect.height+.1f,"Text is truncated: "+label.text);
            Rect textBounds=VisualBounds(label.rectTransform);
            Assert.GreaterOrEqual(textBounds.xMin,safe.xMin-.1f);Assert.LessOrEqual(textBounds.xMax,safe.xMax+.1f);
            Assert.GreaterOrEqual(textBounds.yMin,safe.yMin-.1f);Assert.LessOrEqual(textBounds.yMax,safe.yMax+.1f);
        }
    }

    [UnityTest] public IEnumerator RealSelectorFitsTextAndTargetsAtPortraitPhoneAndTabletScalesInBothLanguages()
    {
        foreach(bool language in new[]{false,true})
        {
            GameText.SetLanguage(language);ui.ShowLobbySkins();yield return null;
            var gallery=Panel.GetComponentInChildren<LobbySkinScrollView>();var scroll=gallery.GetComponent<ScrollRect>();
            foreach(float offset in new[]{0f,352f,978f})
            {
                scroll.content.anchoredPosition=new Vector2(0,Mathf.Min(offset,scroll.content.rect.height-scroll.viewport.rect.height));
                scroll.onValueChanged.Invoke(Vector2.zero);yield return null;
                foreach(Vector2 size in new[]{new Vector2(320,568),new Vector2(360,640),new Vector2(360,800),new Vector2(393,873),
                    new Vector2(412,915),new Vector2(768,1024),new Vector2(800,1280)})
                {
                    PortraitViewport.Fit(size,new Rect(8,24,size.x-16,size.y-56),out float scale);
                    SetScale(scale);yield return null;CheckGeometryAndText();
                }
            }
        }
        // Rectangles are exercised at exact device scales; this is not a hardware screenshot claim.
    }

    [UnityTest] public IEnumerator CompactSelectorExpandedEdgesReceiveRealPointerRaycastsWithoutBlockingNeighbors()
    {
        const float smallScale=512f/960f;
        Assert.GreaterOrEqual(Screen.width,288,"The test display must fit the compact portrait.");
        Assert.GreaterOrEqual(Screen.height,512,"The test display must fit the compact portrait.");
        foreach(bool language in new[]{false,true})
        {
            GameText.SetLanguage(language);ui.ShowLobbySkins();yield return null;SetScale(smallScale);yield return null;
            CheckGeometryAndText();
            foreach(var button in Panel.GetComponentsInChildren<Button>().Where(FullyVisible))
            {
                Rect target=HitBounds(button);
                foreach(var point in new[]{target.center,new Vector2(target.xMin+.25f,target.center.y),
                    new Vector2(target.xMax-.25f,target.center.y),new Vector2(target.center.x,target.yMin+.25f),new Vector2(target.center.x,target.yMax-.25f)})
                {
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                    Assert.IsNotEmpty(hits,button.name+" at "+point);
                    Assert.AreSame(button.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),button.name+" expanded edge is blocked.");
                }
            }
            // Capture the actual rendered selector after the compact layout has painted.
            // WaitForEndOfFrame can stall a hidden batch GameView, so advance normal frames.
            yield return null; yield return null;
            string captureDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Logs","roadmap-v1.2"));
            Directory.CreateDirectory(captureDirectory);
            string capturePath=Path.Combine(captureDirectory,language?"style-selector-compact-es.png":"style-selector-compact-en.png");
            if(File.Exists(capturePath))File.Delete(capturePath);
            ScreenCapture.CaptureScreenshot(capturePath);
            for(int frame=0;frame<30;frame++)
            {
                yield return null;
                if(frame>=2&&File.Exists(capturePath)&&new FileInfo(capturePath).Length>24)break;
            }
            Assert.IsTrue(File.Exists(capturePath),"The compact selector screenshot was not written: "+capturePath);
            Assert.Greater(new FileInfo(capturePath).Length,24,"The compact selector screenshot is empty.");
            Debug.Log("Compact style selector capture: "+capturePath);
        }
    }

    [UnityTest] public IEnumerator ExistingSelectorRefreshesAfterCanvasScaleChangeAndRestoresOriginalFontsOnLargerScreens()
    {
        GameText.SetLanguage(false);ui.ShowMapSkins();yield return null;
        SetScale(1);var labels=Panel.GetComponentsInChildren<Text>();var fonts=labels.Select(t=>t.fontSize).ToArray();
        scaler.scaleFactor=512f/960f;canvas.scaleFactor=scaler.scaleFactor;yield return null;yield return null;
        CheckGeometryAndText();
        scaler.scaleFactor=1;canvas.scaleFactor=1;yield return null;yield return null;
        CollectionAssert.AreEqual(fonts,labels.Select(t=>t.fontSize).ToArray());CheckGeometryAndText();
    }
}

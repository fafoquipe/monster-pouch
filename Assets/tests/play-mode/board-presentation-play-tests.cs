using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local.Boards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BoardPresentationPlayTests
{
    BoardProgressionScreen screen;
    GameObject ownedEventSystem;
    string captures;
#if UNITY_EDITOR
    object gameView;
    PropertyInfo selectedSize;
    MethodInfo resize;
    int originalSizeIndex;
    Vector2Int originalPixels;
#endif

    [UnitySetUp] public IEnumerator Setup()
    {
        if(EventSystem.current==null)ownedEventSystem=new GameObject("Board presentation test events",typeof(EventSystem));
#if UNITY_EDITOR
        var assemblies=AppDomain.CurrentDomain.GetAssemblies();
        Type viewType=assemblies.Select(a=>a.GetType("UnityEditor.GameView")).FirstOrDefault(t=>t!=null);
        Type windowType=assemblies.Select(a=>a.GetType("UnityEditor.EditorWindow")).FirstOrDefault(t=>t!=null);
        Type previewType=assemblies.Select(a=>a.GetType("MonsterPouchLayoutPreview")).FirstOrDefault(t=>t!=null);
        Assert.IsNotNull(viewType); Assert.IsNotNull(windowType); Assert.IsNotNull(previewType);
        gameView=windowType.GetMethod("GetWindow",new[]{typeof(Type)}).Invoke(null,new object[]{viewType});
        selectedSize=viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
        originalSizeIndex=(int)selectedSize.GetValue(gameView);
        originalPixels=new Vector2Int(Screen.width,Screen.height);
        resize=previewType.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static);
#endif
        yield return Resize(360,640);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(screen!=null)screen.Close();
        if(ownedEventSystem!=null)Object.Destroy(ownedEventSystem);
        yield return null;
#if UNITY_EDITOR
        if(gameView!=null&&selectedSize!=null)
        {
            selectedSize.SetValue(gameView,originalSizeIndex);
            gameView.GetType().GetMethod("Repaint",BindingFlags.Public|BindingFlags.Instance).Invoke(gameView,null);
            for(int frame=0;frame<120&&(Screen.width!=originalPixels.x||Screen.height!=originalPixels.y);frame++)yield return null;
            Assert.AreEqual(originalSizeIndex,(int)selectedSize.GetValue(gameView));
        }
#endif
    }

    IEnumerator Resize(int width,int height)
    {
#if UNITY_EDITOR
        resize.Invoke(null,new object[]{width+"x"+height});
#else
        Screen.SetResolution(width,height,FullScreenMode.Windowed);
#endif
        for(int frame=0;frame<120&&(Screen.width!=width||Screen.height!=height);frame++)yield return null;
        Assert.AreEqual(new Vector2Int(width,height),new Vector2Int(Screen.width,Screen.height),"Render at the requested native test resolution.");
        yield return null;
    }

    static BoardScreenState State(bool spanish,long trophies)
    {
        bool royal=trophies>=150;
        return new BoardScreenState {
            Spanish=spanish,Trophies=trophies,CurrentBoardId=royal?"royal":"classic",StageMinimum=royal?150:0,
            NextStageAt=royal?(long?)null:150,AdjacentBoardId=royal?"classic":"royal",AdjacentIsNext=!royal,
            VictoryGain=30,DefeatLoss=royal?30:5,SelectedBoardId=royal?"royal":"classic",SelectionDefined=true,
            Options=new[] {
                new BoardSelectionOption{BoardId="classic",CanSelect=true,Status=BoardOptionStatus.Available},
                new BoardSelectionOption{BoardId="royal",CanSelect=royal,Status=royal?BoardOptionStatus.Available:BoardOptionStatus.LockedByTrophies,UnlockAt=150},
                new BoardSelectionOption{BoardId="lounge",CanSelect=false,Status=BoardOptionStatus.PaidCosmetic}
            }
        };
    }

    T Node<T>(string name) where T:Component
    {
        var matches=screen.GetComponentsInChildren<T>(true).Where(component=>component.name=="BoardScreen."+name).ToArray();
        Assert.AreEqual(1,matches.Length,"Expected one node: BoardScreen."+name);
        return matches[0];
    }

    static Rect Bounds(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }

    static void Contains(Rect outer,Rect inner,string label)
    {
        Assert.GreaterOrEqual(inner.xMin,outer.xMin-.1f,label+" left");
        Assert.GreaterOrEqual(inner.yMin,outer.yMin-.1f,label+" bottom");
        Assert.LessOrEqual(inner.xMax,outer.xMax+.1f,label+" right");
        Assert.LessOrEqual(inner.yMax,outer.yMax+.1f,label+" top");
    }

    void Click(string node)
    {
        Button button=Node<Button>(node);
        Assert.IsTrue(button.interactable,node+" must be interactable before the pointer test.");
        var pointer=new PointerEventData(EventSystem.current){position=Bounds((RectTransform)button.transform).center};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsNotEmpty(hits,node+" receives no screen-space raycast.");
        var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.AreSame(button.gameObject,target,node+" is obscured by another UI element.");
        ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);
    }

    void Numeric(string node,string expected,float minimumLogicalSize)
    {
        Text text=Node<Text>(node);
        Assert.AreEqual(expected,text.text,node);
        Assert.AreSame(Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold"),text.font,"Use the existing clear Lato numerals.");
        Assert.IsTrue(text.font.dynamic,"Keep dynamic vector font data for native pixel rasterization.");
        Assert.AreEqual(Vector3.one,text.transform.localScale,node+" must not stretch the glyph raster.");
        Assert.AreEqual(0,text.GetComponents<Shadow>().Length,node+" must not blur digits with shadows or outlines.");
        float physicalScale=Bounds(text.rectTransform).height/text.rectTransform.rect.height;
        float canvasScale=screen.GetComponent<Canvas>().scaleFactor;
        Assert.That(physicalScale,Is.EqualTo(canvasScale).Within(.01f),node+" uses the display density without extra transform stretching.");
        Assert.That(text.pixelsPerUnit,Is.EqualTo(canvasScale).Within(.01f),node+" rasterizes the dynamic font at the actual Canvas resolution.");
        Assert.GreaterOrEqual(text.fontSize*physicalScale,minimumLogicalSize*canvasScale-.01f,node+" physical font size follows the readable logical size");
        Assert.LessOrEqual(text.preferredWidth,text.rectTransform.rect.width+.1f,node+" clips horizontally");
        Assert.LessOrEqual(text.preferredHeight,text.rectTransform.rect.height+.1f,node+" clips vertically");
    }

    void CheckRoad(bool spanish,long trophies)
    {
        Canvas canvas=screen.GetComponent<Canvas>();
        Assert.IsNotNull(canvas);Assert.AreEqual(RenderMode.ScreenSpaceOverlay,canvas.renderMode);
        Assert.IsTrue(canvas.pixelPerfect);Assert.GreaterOrEqual(canvas.scaleFactor,1);
        Assert.That(screen.GetComponent<CanvasScaler>().scaleFactor,Is.EqualTo(canvas.scaleFactor).Within(.001f));
        if(Screen.width==1080&&Screen.height==2400)Assert.That(canvas.scaleFactor,Is.EqualTo(3).Within(.001f));
        if(Screen.width==1440&&Screen.height==3200)Assert.That(canvas.scaleFactor,Is.EqualTo(4).Within(.001f));
        if(Screen.width==1536&&Screen.height==2048)Assert.That(canvas.scaleFactor,Is.EqualTo(3.2f).Within(.001f));
        Assert.AreEqual("road",screen.CurrentView);
        Assert.AreEqual(spanish?"TABLEROS":"BOARDS",Node<Text>("Title").text);
        Text value=Node<Text>("TrophyValue");
        Assert.AreEqual(trophies.ToString(),new string(value.text.Where(char.IsDigit).ToArray()),"All trophy digits remain present, including six-digit values.");
        Numeric("TrophyValue",value.text,26);
        Numeric("WinValue","+30",23);
        string loss=Node<Text>("LossValue").text;
        Assert.AreEqual("-"+(trophies>=150?30:5),loss.Replace('−','-'));
        Numeric("LossValue",loss,23);
        if(trophies<150)
        {
            string progress=Node<Text>("ProgressValue").text;
            Assert.AreEqual(trophies+"/150",progress.Replace(" ",""));
            Numeric("ProgressValue",progress,18);
            Assert.AreEqual("+150",Node<Text>("AdjacentThresholdValue").text);
            Assert.IsNotNull(Node<Image>("AdjacentThresholdIcon"));
        }
        else
        {
            Assert.AreEqual("+150",Node<Text>("ThresholdValue").text);
            Assert.IsNotNull(Node<Image>("ThresholdIcon"));
        }
        Rect safe=Bounds(Node<RectTransform>("SafeArea"));
        Contains(Screen.safeArea,safe,"Safe area");
        Assert.LessOrEqual(safe.width/canvas.scaleFactor,540.1f,"Portrait column limits are logical units, not a physical-pixel cap.");
        Assert.GreaterOrEqual(safe.width/canvas.scaleFactor,319.9f,"Keep a readable logical content column.");
        if(Screen.width>=1080&&Screen.height>Screen.width)
            Assert.Greater(safe.width,1000,"Dense portrait displays must not reduce the entire UI to a narrow physical column.");
        Contains(safe,Bounds(Node<RectTransform>("Header")),"Header");
        Contains(safe,Bounds(Node<RectTransform>("Footer")),"Footer");
        Contains(safe,Bounds(Node<RectTransform>("Viewport")),"Scrollable viewport");
        foreach(Text text in screen.GetComponentsInChildren<Text>())
        {
            Assert.IsFalse(text.text.IndexOf("arena",StringComparison.OrdinalIgnoreCase)>=0,"Use the approved board terminology: "+text.text);
            Assert.IsFalse(text.text.IndexOf("Desde 150",StringComparison.OrdinalIgnoreCase)>=0);
            Assert.IsFalse(text.text.IndexOf("From 150",StringComparison.OrdinalIgnoreCase)>=0);
            Assert.LessOrEqual(text.preferredHeight,text.rectTransform.rect.height+.1f,"Text is truncated: "+text.text);
        }
        foreach(Button button in screen.GetComponentsInChildren<Button>())
        {
            Rect target=Bounds((RectTransform)button.transform);
            Assert.GreaterOrEqual(target.width,44*canvas.scaleFactor-.01f,button.name+" logical touch width at current density");
            Assert.GreaterOrEqual(target.height,44*canvas.scaleFactor-.01f,button.name+" logical touch height at current density");
        }
    }

    [UnityTest] public IEnumerator NativePortraitAndPcWindowLayoutsKeepApprovedValuesReadableInBothLanguages()
    {
        // Landscape cases are desktop windows, never mobile rotation support.
        foreach(Vector2Int size in new[]{new Vector2Int(320,568),new Vector2Int(360,640),new Vector2Int(393,852),
            new Vector2Int(768,1024),new Vector2Int(800,1280),new Vector2Int(1024,768),new Vector2Int(1280,720),
            new Vector2Int(1080,2400),new Vector2Int(1440,3200),new Vector2Int(1536,2048)})
        {
            yield return Resize(size.x,size.y);
            foreach(bool spanish in new[]{true,false})foreach(long trophies in new long[]{0,149,150,100000})
            {
                BoardScreenState state=State(spanish,trophies);
                if(screen==null)screen=BoardProgressionScreen.Open(state);else screen.Present(state);
                screen.ShowRoad();yield return null;yield return null;Canvas.ForceUpdateCanvases();
                CheckRoad(spanish,trophies);
            }
        }
        // The final board has no trophy ceiling: retain every digit even at the
        // largest representable balance, without shrinking below readable size.
        foreach(Vector2Int size in new[]{new Vector2Int(320,568),new Vector2Int(1440,3200)})
        {
            yield return Resize(size.x,size.y);
            foreach(bool spanish in new[]{true,false})
            {
                screen.Present(State(spanish,long.MaxValue));screen.ShowRoad();
                yield return null;yield return null;Canvas.ForceUpdateCanvases();
                CheckRoad(spanish,long.MaxValue);
            }
        }
        foreach(Vector2Int size in new[]{new Vector2Int(320,568),new Vector2Int(800,1280),
            new Vector2Int(1080,2400),new Vector2Int(1440,3200),new Vector2Int(1536,2048)})
        foreach(bool spanish in new[]{true,false})
        {
            yield return Resize(size.x,size.y);
            if(screen!=null)screen.Close();yield return null;
            var state=State(spanish,150);
            state.Options[2].CanSelect=true;state.Options[2].Status=BoardOptionStatus.OwnedCosmetic;
            int applied=0;string appliedId=null;
            screen=BoardProgressionScreen.Open(state,id=>{applied++;appliedId=id;});
            screen.ShowBoards();yield return null;yield return null;Canvas.ForceUpdateCanvases();
            float scale=screen.GetComponent<Canvas>().scaleFactor;
            ScrollRect scroll=Node<ScrollRect>("Scroll");
            Rect header=Bounds(Node<RectTransform>("Header")),footer=Bounds(Node<RectTransform>("Footer"));
            Rect viewport=Bounds(Node<RectTransform>("Viewport"));
            Assert.IsTrue(scroll.vertical);Assert.IsFalse(scroll.horizontal);
            foreach(Text text in screen.GetComponentsInChildren<Text>())
                Assert.LessOrEqual(text.preferredHeight,text.rectTransform.rect.height+.1f,"Board selector truncates: "+text.text);
            foreach(string id in new[]{"classic","royal","lounge"})
            {
                Rect target=Bounds((RectTransform)Node<Button>("Map."+id).transform);
                Assert.GreaterOrEqual(target.width,44*scale-.01f);Assert.GreaterOrEqual(target.height,44*scale-.01f);
            }
            scroll.verticalNormalizedPosition=0;
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            Contains(viewport,Bounds((RectTransform)Node<Button>("Map.lounge").transform),"The final board card is reachable by scrolling.");
            Assert.AreEqual(header,Bounds(Node<RectTransform>("Header")),"Scrolling must not move the fixed header.");
            Assert.AreEqual(footer,Bounds(Node<RectTransform>("Footer")),"Scrolling must not move the fixed footer.");
            Click("Map.lounge");yield return null;yield return null;Canvas.ForceUpdateCanvases();
            Click("Apply");yield return null;yield return null;Canvas.ForceUpdateCanvases();
            Assert.AreEqual(1,applied);Assert.AreEqual("lounge",appliedId);
            Click("Back");yield return null;yield return null;
            Assert.AreEqual("road",screen.CurrentView);
        }
    }

    [UnityTest] public IEnumerator SelectionUsesInjectedPermissionsAndDoesNotInferOwnershipOrMutateTheInput()
    {
        var state=State(true,150);int selected=0;string selectedId=null;
        screen=BoardProgressionScreen.Open(state,id=>{selected++;selectedId=id;});
        yield return null;
        state.Trophies=100000;
        Assert.AreEqual("150",new string(Node<Text>("TrophyValue").text.Where(char.IsDigit).ToArray()),"The view owns a snapshot until Present is called.");
        state.Trophies=150;
        Canvas.ForceUpdateCanvases();Click("MapTab");yield return null;yield return null;Canvas.ForceUpdateCanvases();
        Assert.AreEqual("boards",screen.CurrentView);
        Assert.IsFalse(Node<Button>("Map.lounge").interactable,"No paid board ownership may be inferred.");
        Assert.IsFalse(Node<Button>("Apply").interactable,"The unchanged selection cannot be applied again.");
        Click("Map.classic");yield return null;yield return null;
        Assert.AreEqual(0,selected,"Previewing a card is not applying its selection.");
        Assert.IsTrue(Node<Button>("Apply").interactable);
        Canvas.ForceUpdateCanvases();Click("Apply");yield return null;
        Assert.AreEqual(1,selected);Assert.AreEqual("classic",selectedId);
        Assert.AreEqual("royal",state.SelectedBoardId,"Presentation must not rewrite the input account snapshot.");
        Assert.AreEqual(150,state.Trophies);Assert.AreEqual("royal",state.CurrentBoardId);

        state.SelectionDefined=false;
        foreach(var option in state.Options)option.CanSelect=true;
        screen.Present(state);screen.ShowBoards();yield return null;yield return null;
        foreach(string id in new[]{"classic","royal","lounge"})Assert.IsFalse(Node<Button>("Map."+id).interactable,"An undefined selection scenario cannot imply an unlock policy.");
        Assert.IsFalse(Node<Button>("Apply").interactable);
        Assert.AreEqual(1,selected);

        state.SelectionDefined=true;state.Trophies=0;state.CurrentBoardId="classic";state.SelectedBoardId="classic";
        state.Options[1].CanSelect=true;
        screen.Present(state);screen.ShowBoards();yield return null;yield return null;
        Assert.IsTrue(Node<Button>("Map.royal").interactable,"Explicit injected permissions take precedence over current trophies; demotion rules are not inferred by UI.");
    }

    [UnityTest] public IEnumerator TrophySlotsAcceptProvidedArtAndNativeCapturesPreservePixelDimensions()
    {
        screen=BoardProgressionScreen.Open(State(true,149));yield return null;yield return null;
        Image slot=Node<Image>("AdjacentThresholdIcon");Assert.IsNull(slot.sprite,"Pending artwork is not replaced by an invented emoji.");
        var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f));
        screen.SetTrophyIcon(sprite);Assert.AreSame(sprite,slot.sprite);
        screen.SetTrophyIcon(null);Object.Destroy(sprite);Object.Destroy(texture);yield return null;
        captures=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Logs","roadmap-v1.2","board-presentation-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(captures);
        yield return Capture(320,568,false,100000,"phone-small-en-100000");
        yield return Capture(360,640,true,149,"phone-es-149");
        yield return Capture(800,1280,false,0,"tablet-en-0");
        yield return Capture(1024,768,true,150,"pc-window-es-150");
        yield return Capture(320,568,true,150,"phone-maps-es-150",true);
        yield return Capture(800,1280,false,150,"tablet-maps-en-150",true);
        yield return Capture(1080,2400,true,149,"phone-dense1080-es-149");
        yield return Capture(1440,3200,false,100000,"phone-dense1440-en-100000");
        yield return Capture(1536,2048,true,150,"tablet-dense1536-es-150");
        yield return Capture(1080,2400,true,150,"phone-dense1080-maps-es-150",true);
        yield return Capture(1536,2048,false,150,"tablet-dense1536-maps-en-150",true);
        Debug.Log("Board presentation native captures: "+captures);
    }

    IEnumerator Capture(int width,int height,bool spanish,long trophies,string name,bool maps=false)
    {
        yield return Resize(width,height);
        var state=State(spanish,trophies);
        if(maps){state.Options[2].CanSelect=true;state.Options[2].Status=BoardOptionStatus.OwnedCosmetic;}
        screen.Present(state);screen.ShowRoad();
        yield return null;yield return null;Canvas.ForceUpdateCanvases();CheckRoad(spanish,trophies);
        if(maps){screen.ShowBoards();yield return null;yield return null;Canvas.ForceUpdateCanvases();}
        string path=Path.Combine(captures,name+".png");Assert.IsFalse(File.Exists(path));
        ScreenCapture.CaptureScreenshot(path);
        for(int frame=0;frame<120;frame++)
        {
            yield return null;
            if(frame>=2&&File.Exists(path)&&new FileInfo(path).Length>24)break;
        }
        Assert.IsTrue(File.Exists(path),"Native screenshot missing: "+path);
        byte[] bytes=File.ReadAllBytes(path);Assert.Greater(bytes.Length,24);
        Assert.AreEqual(137,bytes[0]);Assert.AreEqual(80,bytes[1]);Assert.AreEqual(78,bytes[2]);Assert.AreEqual(71,bytes[3]);
        Assert.AreEqual(width,(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19]);
        Assert.AreEqual(height,(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23]);
    }
}

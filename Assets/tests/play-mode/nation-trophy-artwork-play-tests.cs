using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using MonsterPouch.Local.Boards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class NationTrophyArtworkPlayTests
{
    const string Root="MonsterPouch/UI/Progression/Trophies/";
    readonly string captureRun=Guid.NewGuid().ToString("N");
    BoardProgressionScreen screen;
    GameObject testButton;
#if UNITY_EDITOR
    object gameView;
    PropertyInfo selectedSize;
    MethodInfo resize;
    int originalSize;
    Vector2Int originalPixels;
#endif

    [UnitySetUp] public IEnumerator Setup()
    {
#if UNITY_EDITOR
        var assemblies=AppDomain.CurrentDomain.GetAssemblies();
        Type view=assemblies.Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);
        Type window=assemblies.Select(a=>a.GetType("UnityEditor.EditorWindow")).First(t=>t!=null);
        Type preview=assemblies.Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        gameView=window.GetMethod("GetWindow",new[]{typeof(Type)}).Invoke(null,new object[]{view});
        selectedSize=view.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        originalSize=(int)selectedSize.GetValue(gameView);originalPixels=new Vector2Int(Screen.width,Screen.height);
        resize=preview.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static);
#endif
        yield return null;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(screen!=null)screen.Close();
        if(testButton!=null){testButton.SetActive(false);Object.Destroy(testButton);}
        yield return null;
#if UNITY_EDITOR
        if(gameView!=null)
        {
            selectedSize.SetValue(gameView,originalSize);
            gameView.GetType().GetMethod("Repaint",BindingFlags.Public|BindingFlags.Instance).Invoke(gameView,null);
            for(int i=0;i<120&&(Screen.width!=originalPixels.x||Screen.height!=originalPixels.y);i++)yield return null;
        }
#endif
    }

    static Sprite Trophy(string id)=>Resources.Load<Sprite>(Root+id);
    T Node<T>(string name) where T:Component=>screen.GetComponentsInChildren<T>(true).Single(x=>x.name=="BoardScreen."+name);
    static Rect Bounds(RectTransform transform)
    {
        var corners=new Vector3[4];transform.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    static void Inside(Rect outer,Rect inner)
    {
        Assert.That(inner.xMin,Is.GreaterThanOrEqualTo(outer.xMin-.1f));
        Assert.That(inner.xMax,Is.LessThanOrEqualTo(outer.xMax+.1f));
        Assert.That(inner.yMin,Is.GreaterThanOrEqualTo(outer.yMin-.1f));
        Assert.That(inner.yMax,Is.LessThanOrEqualTo(outer.yMax+.1f));
    }
    static BoardScreenState State(int index)
    {
        var policy=GuestTrophyLadder.CreatePolicy();var stages=policy.Stages;
        var stage=stages[index];var next=index+1<stages.Length?stages[index+1]:null;
        var adjacent=next??stages[index-1];
        return new BoardScreenState {
            Spanish=true,Trophies=stage.MinimumTrophies,CurrentBoardId=stage.BoardId,StageMinimum=stage.MinimumTrophies,
            NextStageAt=next?.MinimumTrophies,AdjacentBoardId=adjacent.BoardId,AdjacentStageMinimum=adjacent.MinimumTrophies,
            AdjacentIsNext=next!=null,VictoryGain=30,DefeatLoss=stage.DefeatLossOverride??5,
            SelectionDefined=true,SelectedBoardId="lounge",
            Options=stages.Select(s=>new BoardSelectionOption {BoardId=s.BoardId,UnlockAt=s.MinimumTrophies,
                CanSelect=s.MinimumTrophies<=stage.MinimumTrophies,
                Status=s.MinimumTrophies<=stage.MinimumTrophies?BoardOptionStatus.Available:BoardOptionStatus.LockedByTrophies})
                .Concat(new[]{new BoardSelectionOption{BoardId="lounge",CanSelect=true,Status=BoardOptionStatus.OwnedCosmetic}}).ToArray()
        };
    }

    [UnityTest] public IEnumerator ApprovedFiveSpritesLoadWithPixelImportSettings()
    {
        var sprites=new System.Collections.Generic.HashSet<Sprite>();
        foreach(string id in new[]{"naturalis","aquarion","geothar","volkanar","royal"})
        {
            Sprite sprite=Trophy(id);Assert.IsNotNull(sprite,id+" approved artwork must be imported.");
            Assert.IsTrue(sprites.Add(sprite),"Each stage keeps its own approved material.");
            Assert.AreEqual(FilterMode.Point,sprite.texture.filterMode,id);
            Assert.AreEqual(TextureWrapMode.Clamp,sprite.texture.wrapMode,id);
            Assert.AreEqual(1,sprite.texture.mipmapCount,id);
            Assert.LessOrEqual(sprite.texture.width,256,id);Assert.LessOrEqual(sprite.texture.height,256,id);
            Assert.Greater(sprite.texture.width,32,id);
        }
        yield return null;
    }

    [UnityTest] public IEnumerator RoadAndLockedCardsUseTheirOwnNationWhileCosmeticSelectionKeepsTheAccountStage()
    {
        foreach(var size in new[]{new Vector2Int(393,852),new Vector2Int(1280,720)})
        {
            yield return Resize(size);
            var stages=GuestTrophyLadder.CreatePolicy().Stages;
            for(int i=0;i<stages.Length;i++)
            {
                var state=State(i);
                if(screen==null)screen=BoardProgressionScreen.Open(state,null,null,Trophy);else screen.Present(state);
                screen.ShowRoad();yield return null;Canvas.ForceUpdateCanvases();
                var current=Node<Image>(i==stages.Length-1?"ThresholdIcon":"CurrentRangeIcon");
                Assert.AreSame(Trophy(stages[i].BoardId),current.sprite,"The chosen casino skin does not change the account stage trophy.");
                var adjacent=Node<Image>(i==stages.Length-1?"AdjacentRangeIcon":"AdjacentThresholdIcon");
                Assert.AreSame(Trophy(state.AdjacentBoardId),adjacent.sprite);
                Assert.AreEqual(state.Trophies.ToString(),new string(Node<Text>("TrophyValue").text.Where(char.IsDigit).ToArray()));
                Inside(Bounds((RectTransform)current.transform.parent),Bounds(current.rectTransform));
                Inside(Bounds((RectTransform)adjacent.transform.parent),Bounds(adjacent.rectTransform));
                if(i==0||i==stages.Length-1)yield return Capture("road-"+stages[i].BoardId+"-"+size.x+"x"+size.y);
                screen.ShowBoards();yield return null;Canvas.ForceUpdateCanvases();
                foreach(var option in state.Options.Where(o=>o.Status==BoardOptionStatus.LockedByTrophies))
                {
                    var icon=Node<Image>("MapThresholdIcon."+option.BoardId);
                    Assert.AreSame(Trophy(option.BoardId),icon.sprite);
                    Assert.IsFalse(Node<Button>("Map."+option.BoardId).interactable,"Art must not unlock a board.");
                    Inside(Bounds((RectTransform)icon.transform.parent),Bounds(icon.rectTransform));
                }
                Assert.AreEqual("lounge",state.SelectedBoardId);Assert.AreEqual(stages[i].MinimumTrophies,state.Trophies);
            }
        }
    }

    [UnityTest] public IEnumerator StyleThresholdKeepsOriginalButtonAndCentersNumberWithItsOwnTrophy()
    {
        var draw=typeof(LocalGameUI).GetMethod("LayoutLobbyRequirement",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.IsNotNull(draw);
        foreach(var stage in GuestTrophyLadder.CreatePolicy().Stages.Skip(1))
        {
            testButton=new GameObject("isolated threshold",typeof(RectTransform),typeof(Image),typeof(Button));
            var rect=(RectTransform)testButton.transform;rect.sizeDelta=new Vector2(356,68);
            var button=testButton.GetComponent<Button>();button.interactable=false;
            var child=new GameObject("threshold",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));child.transform.SetParent(rect,false);
            var label=child.GetComponent<Text>();label.font=Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold");label.fontSize=19;
            label.text="+"+stage.MinimumTrophies;label.alignment=TextAnchor.MiddleCenter;
            var iconNode=new GameObject("Lobby requirement trophy",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            iconNode.transform.SetParent(rect,false);var icon=iconNode.GetComponent<Image>();
            icon.sprite=Trophy(stage.BoardId);icon.raycastTarget=false;icon.preserveAspect=true;
            draw.Invoke(null,new object[]{label,icon});yield return null;Canvas.ForceUpdateCanvases();
            Assert.AreSame(Trophy(stage.BoardId),icon.sprite);Assert.IsFalse(icon.raycastTarget);
            Assert.AreEqual(new Vector2(356,68),rect.sizeDelta);Assert.IsFalse(button.interactable);
            Assert.AreEqual("+"+stage.MinimumTrophies,label.text);Assert.AreEqual(new Vector2(32,32),icon.rectTransform.sizeDelta);
            Inside(Bounds(rect),Bounds(label.rectTransform));Inside(Bounds(rect),Bounds(icon.rectTransform));
            Assert.That(Bounds(icon.rectTransform).xMin-Bounds(label.rectTransform).xMax,Is.EqualTo(6).Within(.1f));
            Assert.That((Bounds(label.rectTransform).xMin+Bounds(icon.rectTransform).xMax)*.5f,Is.EqualTo(Bounds(rect).center.x).Within(.1f));
            testButton.SetActive(false);Object.Destroy(testButton);testButton=null;yield return null;
        }
    }

    IEnumerator Resize(Vector2Int size)
    {
#if UNITY_EDITOR
        resize.Invoke(null,new object[]{size.x+"x"+size.y});
#else
        Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);
#endif
        for(int i=0;i<120&&(Screen.width!=size.x||Screen.height!=size.y);i++)yield return null;
        Assert.AreEqual(size,new Vector2Int(Screen.width,Screen.height));yield return null;
    }
    IEnumerator Capture(string name)
    {
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Logs","local-validation","nation-trophies-v2",captureRun));
        Directory.CreateDirectory(folder);string file=Path.Combine(folder,name+".png");
        Assert.IsFalse(File.Exists(file));
        ScreenCapture.CaptureScreenshot(file);
        for(int i=0;i<120;i++){yield return null;if(i>=2&&File.Exists(file)&&new FileInfo(file).Length>24)break;}
        Assert.IsTrue(File.Exists(file));
        var bytes=File.ReadAllBytes(file);Assert.Greater(bytes.Length,24);
        Assert.AreEqual(Screen.width,(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19]);
        Assert.AreEqual(Screen.height,(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23]);
    }
}

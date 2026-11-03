using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using MonsterPouch.Local.Boards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ProgressionRoadPlayTests
{
    ProgressionRoadScreen screen;
    object gameView;PropertyInfo selectedSize;MethodInfo resize;int originalSize;Vector2Int originalPixels;
    [UnitySetUp] public IEnumerator Setup()
    {
#if UNITY_EDITOR
        var assemblies=AppDomain.CurrentDomain.GetAssemblies();
        var viewType=assemblies.Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);
        var windowType=assemblies.Select(a=>a.GetType("UnityEditor.EditorWindow")).First(t=>t!=null);
        var previewType=assemblies.Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        gameView=windowType.GetMethod("GetWindow",new[]{typeof(Type)}).Invoke(null,new object[]{viewType});
        selectedSize=viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
        originalSize=(int)selectedSize.GetValue(gameView);originalPixels=new Vector2Int(Screen.width,Screen.height);
        resize=previewType.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static);
#endif
        yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(screen!=null)screen.Close();yield return null;
#if UNITY_EDITOR
        if(gameView!=null)
        {
            selectedSize.SetValue(gameView,originalSize);gameView.GetType().GetMethod("Repaint").Invoke(gameView,null);
            for(int i=0;i<120&&(Screen.width!=originalPixels.x||Screen.height!=originalPixels.y);i++)yield return null;
            Assert.AreEqual(originalSize,(int)selectedSize.GetValue(gameView));
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
        for(int i=0;i<120&&(Screen.width!=width||Screen.height!=height);i++)yield return null;
        Assert.AreEqual(new Vector2Int(width,height),new Vector2Int(Screen.width,Screen.height));yield return null;
    }
    static ProgressionRoadState Fixture()=>new ProgressionRoadState{
        Spanish=true,Trophies=650,
        Worlds=GuestProgressionCatalogue.Worlds.Select(w=>new RoadWorld{
            BoardId=w.BoardId,Name=w.Id,StartsAt=w.StartTrophies,
            Characters=w.CharacterIds.Select(id=>new RoadCharacter{Id=id,Name=id,Owned=id=="suki",HideLockedArtwork=!GuestProgressionCatalogue.IsEronya(id)}).ToArray()
        }).ToArray(),
        Rewards=GuestProgressionCatalogue.Milestones.Select(m=>new RoadReward{
            Id=m.Id,Trophies=m.RequiredTrophies,Title="CASE",IconResource="MonsterPouch/UI/Lobby/Header/daily-pouch",
            Status=m.RequiredTrophies<650?RoadRewardStatus.Claimed:m.RequiredTrophies==650?RoadRewardStatus.Claimable:RoadRewardStatus.Locked
        }).ToArray()
    };
    Transform Named(string name)=>screen.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    [UnityTest] public IEnumerator ClaimStatesDoNotDoubleSubmitAndNeverRevealLockedLegacyArtwork()
    {
        yield return Resize(393,852);var state=Fixture();int calls=0;
        screen=ProgressionRoadScreen.Open(state,id=>{
            calls++;state.Rewards.Single(r=>r.Id==id).Status=RoadRewardStatus.Claimed;screen.Present(state);
        },()=>{});yield return null;
        var claim=Named("Claim road-v1-0650").GetComponent<Button>();claim.onClick.Invoke();claim.onClick.Invoke();
        Assert.AreEqual(1,calls);yield return null;
        Assert.IsFalse(screen.GetComponentsInChildren<Button>().Any(b=>b.name=="Claim road-v1-0700"));
        var hidden=Named("Road character anuik").GetComponentInChildren<LobbyFrameGraphic>();
        Assert.AreEqual(Color.black,hidden.color);Assert.IsTrue(hidden.IsAtRest);Assert.IsNotNull(hidden.CurrentSprite);
        Assert.AreEqual("???",Named("Road character anuik").GetComponentsInChildren<Text>().Single(t=>t.name=="Character name").text);
        var gray=Named("Road character cael").GetComponentInChildren<AbilityAvailabilityVisual>();Assert.IsNotNull(gray);Assert.IsTrue(gray.IsGrayscale);
        var owned=Named("Road character suki").GetComponentInChildren<LobbyFrameGraphic>();Assert.AreEqual(Color.white,owned.color);Assert.IsTrue(owned.IsAtRest);
        Assert.AreEqual(0,screen.GetComponentsInChildren<PlayerAvatarGraphic>().Length,"Road characters use full bodies, never profile icons.");
    }
    [UnityTest] public IEnumerator FourResolutionsScrollBothEndsAndKeepStepsAndBodiesInsideTheirTiles()
    {
        foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(393,852),new Vector2Int(1280,720),new Vector2Int(2560,1080)})
        {
            yield return Resize(size.x,size.y);
            if(screen==null)screen=ProgressionRoadScreen.Open(Fixture(),_=>{},()=>{});yield return null;
            var scroll=screen.RoadScroll;Assert.IsTrue(scroll.vertical);Assert.IsFalse(scroll.horizontal);
            Assert.Greater(scroll.content.rect.height,scroll.viewport.rect.height);
            scroll.verticalNormalizedPosition=0;yield return null;Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(0).Within(.001f));
            scroll.verticalNormalizedPosition=1;yield return null;Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(1).Within(.001f));
            screen.FocusCurrent();yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y,0);Assert.Less(scroll.content.anchoredPosition.y,scroll.content.rect.height-scroll.viewport.rect.height);
            foreach(var platform in screen.GetComponentsInChildren<ProgressionStepGraphic>())
            {
                var node=(RectTransform)platform.transform.parent;float width=((RectTransform)node.parent).rect.width;
                Assert.GreaterOrEqual(node.anchoredPosition.x,0);Assert.LessOrEqual(node.anchoredPosition.x+node.rect.width,width);
            }
            foreach(var body in screen.GetComponentsInChildren<LobbyFrameGraphic>())
            {
                Assert.LessOrEqual(body.BodyHeight,60.1f);Assert.IsTrue(body.IsAtRest);
            }
            Assert.AreEqual(30,screen.GetComponentsInChildren<ProgressionStepGraphic>().Length);
        }
    }
    [TestCase(0,0)][TestCase(49,0)][TestCase(50,50)][TestCase(299,250)][TestCase(300,300)]
    [TestCase(1499,1450)][TestCase(1500,1500)][TestCase(1501,1500)]
    public void CurrentLightTracksActualTrophiesAtBoundaries(long trophies,long step)=>Assert.AreEqual(step,ProgressionRoadLayout.CurrentStep(trophies));
}

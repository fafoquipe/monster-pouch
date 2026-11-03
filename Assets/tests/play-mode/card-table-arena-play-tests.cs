using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CardTableArenaPlayTests
{
    LocalGameUI ui;
    CardTableArena arena;
    MethodInfo applyViewport;
    TableStyle savedStyle;

    [UnitySetUp] public IEnumerator Setup()
    {
        savedStyle=CardTableTheme.Current;
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        arena=ui.Match.Mapper.GetComponent<CardTableArena>();Assert.IsNotNull(arena);Assert.IsTrue(arena.IsApplied);
        applyViewport=typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.Instance|BindingFlags.NonPublic);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        CardTableTheme.Select(savedStyle);
        if(ui!=null)
        {
            ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;
            Rect safe=Screen.safeArea.width>0?Screen.safeArea:new Rect(0,0,Screen.width,Screen.height);
            applyViewport?.Invoke(ui,new object[]{safe});
        }
        yield return null;
    }

    [UnityTest] public IEnumerator ExpandedBoardPreservesEveryCellAndRepeatedApplyDoesNotCompoundGeometry()
    {
        Rect current=arena.BoardPortraitBounds;
        // Camera framing has changed since the former 20% screen-size target.
        // Keep the current framing and verify playable bounds and world expansion.
        Assert.Greater(current.xMin,0);Assert.Less(current.xMax,540);
        Assert.Greater(current.yMin,0);
        Assert.Less(current.yMax,CardTableArena.BriefTop-8);
        Assert.AreEqual(arena.OriginalCellSize.y,ui.Match.Mapper.CellSize.y,"Enlarging the board must not compress its rows.");
        Assert.That(ui.Match.Mapper.CellSize.x,Is.EqualTo(arena.OriginalCellSize.x*CardTableArena.HorizontalExpansion).Within(.00001f));
        for(int y=0;y<BoardManager.Height;y++)for(int x=0;x<BoardManager.Width;x++)
        {
            var cell=ui.Match.Board.GetCell(x,y);Vector3 center=ui.Match.Mapper.GetWorldPosition(cell);
            Assert.IsTrue(current.Contains(arena.PortraitPoint(center)));
            foreach(float horizontal in new[]{-.49f,0,.49f})foreach(float vertical in new[]{-.49f,0,.49f})
            {
                Vector3 point=center+new Vector3(ui.Match.Mapper.CellSize.x*horizontal,ui.Match.Mapper.CellSize.y*vertical);
                Assert.IsTrue(ui.Match.Mapper.TryGetCell(point,out var mapped));Assert.AreSame(cell,mapped,"A visible cell no longer maps back to its logical coordinate.");
            }
        }
        var surface=GameObject.Find("table-surface").transform;
        Vector3 scale=surface.localScale,position=surface.position;
        Assert.AreSame(arena,CardTableArena.Apply(ui.Match.Board,ui.Match.Mapper,ui.GameCamera));
        Assert.AreEqual(current,arena.BoardPortraitBounds);Assert.AreEqual(scale,surface.localScale);Assert.AreEqual(position,surface.position);
        Assert.AreEqual(1,Object.FindObjectsByType<CardTableArena>(FindObjectsSortMode.None).Length);
        Assert.IsFalse(ui.CanvasRoot.GetComponentInParent<Canvas>().pixelPerfect);
        Assert.AreEqual(60,Application.targetFrameRate);yield return null;
    }

    [UnityTest] public IEnumerator CasinoRestoresAuthoredChipsCartAndOriginalShadowsWithoutDice()
    {
        CardTableTheme.Select(TableStyle.GrandCasaHotel);
        arena.SetTheme(TableStyle.GrandCasaHotel);
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"stein"},31005));yield return null;yield return null;
        var all=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var item in all.Where(item=>item.name.StartsWith("chip-")&&!item.name.EndsWith("-shadow")&&item.GetComponent<SpriteRenderer>()!=null))
            Assert.IsTrue(item.gameObject.activeInHierarchy&&item.GetComponent<SpriteRenderer>().enabled,"A reference chip is hidden: "+item.name);
        foreach(string name in new[]{"die-red","die-white","die-red-shadow","die-white-shadow"})
            Assert.IsFalse(all.Single(item=>item.name==name).GetComponent<SpriteRenderer>().enabled,name);
        foreach(string name in new[]{"table-surface","table-frame","casino-table-path"})Assert.IsNotNull(GameObject.Find(name));
        AssertOriginalTransform("clown-tower",new Vector3(-6.94f,11.62f,0),new Vector3(.6811174f,.68163997f,1.077728f));
        var actualCart=GameObject.Find("shuffler-cart").transform;
        Assert.AreEqual(0,arena.PortraitPoint(actualCart.position).x-
            arena.PortraitPoint(new Vector3(-1.12f,11.54f,0)).x,.001f,"Restore the reference cart position without the later 48px offset.");
        AssertOriginalTransform("shuffler-cart",new Vector3(actualCart.position.x,11.54f,0),new Vector3(.60354984f,.5484415f,1.019054f));
        AssertOriginalTransform("die-white",new Vector3(4.72f,-6.6f,0),new Vector3(.20785834f,.19727473f,.8843f));
        AssertOriginalTransform("die-red",new Vector3(5.22f,-4.83f,0),new Vector3(.22165145f,.20259362f,1.1590223f));
        foreach(string name in new[]{"clown-tower","shuffler-cart"})
        {
            var item=GameObject.Find(name);Assert.IsNotNull(item,name);
            var sprite=item.GetComponent<SpriteRenderer>();Assert.IsTrue(sprite.enabled);Assert.IsNotNull(sprite.sprite);
            Rect world=arena.VisibleWorldBounds(sprite);
            Vector2 topLeft=arena.PortraitPoint(new Vector3(world.xMin,world.yMax));
            Vector2 bottomRight=arena.PortraitPoint(new Vector3(world.xMax,world.yMin));
            var visible=Rect.MinMaxRect(topLeft.x,topLeft.y,bottomRight.x,bottomRight.y);
            Assert.IsFalse(visible.Overlaps(arena.BoardPortraitBounds),name+" overlaps playable cells.");
            var shadow=GameObject.Find(name+"-shadow");Assert.IsNotNull(shadow,name+" lost its original shadow.");
            Assert.IsTrue(shadow.GetComponent<SpriteRenderer>().enabled,
                "The original model's shadow should remain visible: "+name);
        }
        var scenery=arena.GetComponent<ModularArenaScenery>();
        Assert.IsNotNull(scenery);Assert.IsTrue(scenery.IsVisible);Assert.IsNotNull(scenery.Definition);
        var cart=GameObject.Find("shuffler-cart").transform;Vector3 cartPosition=cart.position,cartScale=cart.localScale;
        foreach(string removed in new[]{"Card table atmosphere","Table lamp","Card case","Left brass glint","Right brass glint","Left playing cards 1","Right playing cards 1",
            "Background playing card 1","Background playing card 2","Shuffler feeding card","Warm overhead light","Soft lower felt light"})
            Assert.IsNull(GameObject.Find(removed),removed+" remains on the board margin.");
        Assert.AreEqual(ui.Match.Config.RoundUI.enemyPanelPosition,ToTopLeft(Find("Enemy round HUD")));
        Assert.AreEqual(new Vector2(399,44)+ui.Match.Config.RoundUI.timerPositionOffset,ToTopLeft(Find("Clock")));
        Assert.AreEqual(new Vector2(374,13)+ui.Match.Config.RoundUI.roundLabelPositionOffset,ToTopLeft(Find("Round label")));
        Assert.AreEqual(new Vector2(287,42),ToTopLeft(Find("Battle pause"))+Find("Battle pause").rect.size*.5f);
        yield return new WaitForSecondsRealtime(.12f);
        Assert.AreEqual(cartPosition,cart.position,"The original wooden cart should stay planted.");
        Assert.AreEqual(cartScale,cart.localScale);
    }

    [UnityTest] public IEnumerator EveryDeploymentCellRemainsPlayableAndTheBriefFitsDifferentSafeAreaRatios()
    {
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"stein"},31003));
        yield return null;yield return new WaitForSecondsRealtime(.4f);
        var hero=ui.Match.Player.Monster;
        for(int y=5;y<10;y++)for(int x=0;x<6;x++)
        {
            var cell=ui.Match.Board.GetCell(x,y);Assert.IsTrue(ui.Match.Place(hero.Definition.Id,cell),ui.Match.Notice);
            Assert.AreEqual(cell.Coordinates,hero.Deployment);
            Assert.AreEqual(ui.Match.Mapper.GetWorldPosition(cell),ui.Match.Actors[hero].transform.position);
        }
        yield return null;
        var ratios=new[]{new Vector2(360,640),new Vector2(390,844),new Vector2(768,1024),new Vector2(844,390)};
        foreach(Vector2 size in ratios)
        {
            float fit=Mathf.Min(Screen.width/size.x,Screen.height/size.y)*.9f;
            Vector2 extent=size*fit;
            var safe=new Rect((new Vector2(Screen.width,Screen.height)-extent)*.5f,extent);
            applyViewport.Invoke(ui,new object[]{safe});yield return null;Canvas.ForceUpdateCanvases();
            foreach(string name in new[]{"Brief tray","Brief drop","Brief closed","Bench drop","Battle resources","Reroll","LISTO"})
            {
                Rect rect=ScreenRect(Find(name));
                Assert.GreaterOrEqual(rect.xMin,safe.xMin-.1f,name+" extends outside the safe area.");
                Assert.GreaterOrEqual(rect.yMin,safe.yMin-.1f,name);
                Assert.LessOrEqual(rect.xMax,safe.xMax+.1f,name);
                Assert.LessOrEqual(rect.yMax,safe.yMax+.1f,name);
            }
            var tray=Find("Brief tray");
            for(int y=0;y<10;y++)for(int x=0;x<6;x++)
            {
                var cell=ui.Match.Board.GetCell(x,y);Vector3 world=ui.Match.Mapper.GetWorldPosition(cell);
                Vector3 screen=ui.GameCamera.WorldToScreenPoint(world);
                Assert.IsTrue(safe.Contains(screen),"A board cell falls outside the safe portrait.");
                Vector3 roundtrip=ui.GameCamera.ScreenToWorldPoint(screen);
                Assert.IsTrue(ui.Match.Mapper.TryGetCell(roundtrip,out var picked));Assert.AreSame(cell,picked);
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
                Assert.IsFalse(hits.Any(hit=>hit.gameObject.transform.IsChildOf(tray)),"A Brief control blocks cell "+cell.Coordinates+" at ratio "+size);
            }
            Vector3 bottom=ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(0,9));
            bottom.y-=Mathf.Abs(ui.Match.Mapper.CellSize.y)*.5f;
            float boardBottom=ui.GameCamera.WorldToScreenPoint(bottom).y;
            Assert.Greater(boardBottom,ScreenRect(Find("Brief drop")).yMax,"The last cell touches or enters the Brief hit area.");
        }
    }

    [UnityTest] public IEnumerator CombatScoreHudAlsoClearsTheExpandedBottomRowWithoutEditingItsSettings()
    {
        Vector2 configured=ui.Match.Config.RoundUI.playerPanelPosition;
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"stein"},31004));ui.Match.Ready();yield return null;yield return null;
        Rect hud=ScreenRect(Find("Player round HUD"));
        Vector3 bottom=ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(5,9));
        bottom.y-=Mathf.Abs(ui.Match.Mapper.CellSize.y)*.5f;
        Assert.Greater(ui.GameCamera.WorldToScreenPoint(bottom).y,hud.yMax);
        Assert.AreEqual(configured,ui.Match.Config.RoundUI.playerPanelPosition,"Layout should not rewrite the saved presentation settings.");
    }

    RectTransform Find(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(rect=>rect.name==name);
    static Vector2 ToTopLeft(RectTransform rect)=>new Vector2(rect.anchoredPosition.x,-rect.anchoredPosition.y);
    static void AssertOriginalTransform(string name,Vector3 position,Vector3 scale)
    {
        var item=GameObject.Find(name);Assert.IsNotNull(item,name);
        Assert.Less(Vector3.Distance(position,item.transform.localPosition),.00001f,name+" moved away from its original scene position.");
        Assert.Less(Vector3.Distance(scale,item.transform.localScale),.00001f,name+" changed its original toy-room scale.");
        Assert.AreEqual(Quaternion.identity,item.transform.localRotation,name);
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
}

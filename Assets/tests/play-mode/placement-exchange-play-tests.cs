using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class PlacementExchangePlayTests
{
    LocalGameUI ui;
    MatchConfig original, config;
    static readonly Vector2Int Left = new Vector2Int(0,8), Right = new Vector2Int(5,8);

    [UnitySetUp]
    public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>();
        ui.Match.enabled = false;
        original = ui.Match.Config;
        config = Object.Instantiate(original);
        config.RoundIncome = new[] { 100 };
        ui.Match.Configure(config,ui.Match.Board,ui.Match.Mapper);
        int seed = Enumerable.Range(1,100).First(s =>
        {
            var pouch = new PouchState(config,s,"bugaloo",BoardSide.Blue,new[] { "atori","flo" });
            pouch.BeginPreparation(1);
            return pouch.Offers.Where(o => o != null).Select(o => o.UnitId).Distinct().Count() == 2;
        });
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[] { "atori","flo" },seed));
        yield return new WaitForSecondsRealtime(.4f);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if(ui != null)
        {
            ui.CloseModal(); ui.Match.Menu();
            ui.Match.Configure(original,ui.Match.Board,ui.Match.Mapper);
        }
        if(config != null) Object.Destroy(config);
        yield return null;
    }

    [UnityTest]
    public IEnumerator FieldExchangeMovesBothVisibleFiguresAndRestoresExactDestinations()
    {
        var first=Buy("atori"); var second=Buy("flo");
        Assert.IsTrue(ui.Match.Place(first.Definition.Id,Cell(Left)));
        Assert.IsTrue(ui.Match.Place(second.Definition.Id,Cell(Right)));
        ui.RenderPage(); yield return null;
        var firstActor=ui.Match.Actors[first]; var secondActor=ui.Match.Actors[second];

        Assert.IsTrue(ui.Match.Place(first.Definition.Id,Cell(Right)));
        Assert.IsTrue(ui.IsPlacementExchanging);
        Assert.IsTrue(firstActor.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff),
            "Destination actors must be hidden synchronously, without a teleport frame.");
        Assert.IsTrue(secondActor.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff));
        yield return VerifyBothTravel();
        Assert.AreSame(firstActor,ui.Match.Actors[first]); Assert.AreSame(secondActor,ui.Match.Actors[second]);
        AssertField(first,Right); AssertField(second,Left);
        AssertRestored();
    }

    [UnityTest]
    public IEnumerator BriefAndBenchUnitsCannotExchangeWithOccupiedField()
    {
        var first=Buy("atori"); var second=Buy("flo");
        int oldSlot=first.BriefSlot;
        Assert.IsTrue(ui.Match.Place(second.Definition.Id,Cell(Right)));
        ui.RenderPage(); yield return null;
        Assert.IsFalse(ui.Match.Place(first.Definition.Id,Cell(Right)));
        Assert.IsFalse(ui.IsPlacementExchanging);
        Assert.AreEqual(UnitLocation.Brief,first.Location); Assert.AreEqual(oldSlot,first.BriefSlot);
        AssertField(second,Right);
        Assert.IsTrue(ui.Match.Store(first.Definition.Id,true));
        ui.RenderPage(); yield return null;
        Assert.IsFalse(ui.Match.Place(first.Definition.Id,Cell(Right)));
        Assert.AreEqual(UnitLocation.Bench,first.Location); AssertField(second,Right);
        yield return null;
        AssertRestored();
    }

    [UnityTest]
    public IEnumerator PurchasedOfferCannotExchangeWithOccupiedFieldOrSpendCurrency()
    {
        var occupant=Buy("atori");
        Assert.IsTrue(ui.Match.Place(occupant.Definition.Id,Cell(Right)));
        ui.RenderPage(); yield return null;
        int slot=OfferSlot("flo"); var offer=ui.Match.Player.Offers[slot];
        int coins=ui.Match.Player.Coins, version=ui.Match.Player.OfferVersion;
        int copies=ui.Match.Player.RemainingCopies("flo");
        Assert.IsFalse(ui.Match.BuyAndPlace(slot,offer.Token,Cell(Right)));
        Assert.IsFalse(ui.IsPlacementExchanging);
        Assert.IsFalse(ui.Match.Player.Owned.ContainsKey("flo"));
        Assert.AreSame(offer,ui.Match.Player.Offers[slot]);
        Assert.AreEqual(coins,ui.Match.Player.Coins); Assert.AreEqual(version,ui.Match.Player.OfferVersion);
        Assert.AreEqual(copies,ui.Match.Player.RemainingCopies("flo"));
        AssertField(occupant,Right);
        yield return null;
        AssertRestored();
    }

    [UnityTest]
    public IEnumerator FieldUnitCanJoinPartiallyFilledBenchWithoutExchanging()
    {
        var first=Buy("atori"); var second=Buy("flo");
        Assert.IsTrue(ui.Match.Place(first.Definition.Id,Cell(Left)));
        Assert.IsTrue(ui.Match.Store(second.Definition.Id,true));
        ui.RenderPage(); yield return null;
        Assert.IsTrue(ui.Match.Store(first.Definition.Id,true));
        Assert.IsFalse(ui.IsPlacementExchanging);
        Assert.AreEqual(UnitLocation.Bench,second.Location); Assert.AreEqual(UnitLocation.Bench,first.Location);
        yield return null;
        AssertRestored();
    }

    [UnityTest]
    public IEnumerator RapidReplacementAndEnteringCombatCancelOldOverlaysAndRestoreRenderers()
    {
        var first=Buy("atori"); var second=Buy("flo");
        Assert.IsTrue(ui.Match.Place(first.Definition.Id,Cell(Left)));
        Assert.IsTrue(ui.Match.Place(second.Definition.Id,Cell(Right)));
        ui.RenderPage(); yield return null;
        Assert.IsTrue(ui.Match.Place(first.Definition.Id,Cell(Right)));
        yield return new WaitForSecondsRealtime(.06f);
        Assert.IsTrue(ui.Match.Place(first.Definition.Id,Cell(Left)));
        Assert.IsTrue(ui.IsPlacementExchanging);
        Assert.AreEqual(2,MotionPieces().Length,"Replacing an exchange leaves exactly the current pair.");
        ui.Match.Ready();
        Assert.IsFalse(ui.IsPlacementExchanging,"Phase changes clean up before combat can render.");
        yield return null;
        AssertRestored();
    }

    [UnityTest]
    public IEnumerator OwnForegroundFigureWinsOverHiddenEchoButVisibleEnemyRemainsInspectable()
    {
        config.Units=config.Units.Where(u => !u.IsMonster || u.Id=="bugaloo" || u.Id=="popow").ToArray();
        Assert.IsTrue(ui.Match.StartMatch("popow",new[] { "atori" },17));
        var own=ui.Match.Player.Monster;
        Assert.IsTrue(ui.Match.Place("popow",Cell(new Vector2Int(3,5))));
        ui.RenderPage(); yield return null;
        var ownView=ui.Match.Actors[own].GetComponent<UnitPresentation>();
        ownView.Freeze();
        var foreground=ownView.Renderer;
        var echo=Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None)
            .Single(v => v.gameObject.activeInHierarchy);
        Assert.AreEqual("bugaloo",echo.UnitId);
        Assert.AreEqual(new Vector2Int(3,4),echo.SnapshotPosition);
        Assert.Greater(foreground.sortingOrder,echo.Renderer.sortingOrder);
        Vector2 ownPoint, enemyPoint;
        var ownPixels=ReadSpriteTexture(foreground.sprite.texture);
        var enemyPixels=ReadSpriteTexture(echo.Renderer.sprite.texture);
        try
        {
            ownPoint=FindOpaqueScreenPoint(foreground,p => OpaqueAt(foreground,ownPixels,p)
                && OpaqueAt(echo.Renderer,enemyPixels,p));
            float ownTop=ui.GameCamera.WorldToScreenPoint(foreground.bounds.max).y;
            enemyPoint=FindOpaqueScreenPoint(echo.Renderer,p => p.y>ownTop+2 && OpaqueAt(echo.Renderer,enemyPixels,p));
        }
        finally { Object.Destroy(ownPixels); Object.Destroy(enemyPixels); }

        Assert.IsTrue(ui.BeginBoardPointer(ownPoint));
        Assert.AreSame(own,typeof(LocalGameUI).GetField("pressedUnit",System.Reflection.BindingFlags.Instance|
            System.Reflection.BindingFlags.NonPublic).GetValue(ui),"The visible foreground pixel must select the local unit.");
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.15f);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.text==own.Definition.DisplayName.ToUpperInvariant()),
            "The foreground hold must show the unit's current display name, not its stable ID.");
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.text==GameText.Resolve("Habilidades del rival")));
        ui.EndBoardPointer(ownPoint); ui.CloseModal(); yield return null;

        Assert.IsTrue(ui.BeginBoardPointer(enemyPoint));
        yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.15f);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t =>
            t.text==ui.Match.InspectOpponent(echo.UnitId).Definition.DisplayName.ToUpperInvariant()));
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.text==GameText.Resolve("Habilidades del rival")),
            "An exposed part of the rival must still show its upgrades.");
        ui.EndBoardPointer(enemyPoint); ui.CloseModal();
    }

    Vector2 FindOpaqueScreenPoint(SpriteRenderer renderer,System.Func<Vector2,bool> accept)
    {
        Vector2 min=ui.GameCamera.WorldToScreenPoint(renderer.bounds.min);
        Vector2 max=ui.GameCamera.WorldToScreenPoint(renderer.bounds.max);
        Vector2 center=(min+max)*.5f, best=Vector2.zero;
        float distance=float.PositiveInfinity;
        for(int y=Mathf.CeilToInt(min.y);y<max.y;y+=2)
        for(int x=Mathf.CeilToInt(min.x);x<max.x;x+=2)
        {
            var point=new Vector2(x+.5f,y+.5f);
            float d=(point-center).sqrMagnitude;
            if(d<distance && accept(point)) { best=point; distance=d; }
        }
        Assert.IsFalse(float.IsPositiveInfinity(distance),"The test needs visible pixels in the overlapping/exposed portion of "+renderer.name);
        return best;
    }

    bool OpaqueAt(SpriteRenderer renderer,Texture2D texture,Vector2 screen)
    {
        float depth=ui.GameCamera.WorldToScreenPoint(renderer.transform.position).z;
        Vector2 local=renderer.transform.InverseTransformPoint(ui.GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,depth)));
        if(renderer.flipX)local.x=-local.x; if(renderer.flipY)local.y=-local.y;
        var sprite=renderer.sprite;
        Vector2 pixel=local*sprite.pixelsPerUnit+sprite.pivot;
        if(pixel.x<0 || pixel.y<0 || pixel.x>=sprite.rect.width || pixel.y>=sprite.rect.height) return false;
        pixel+=sprite.rect.position;
        return texture.GetPixel(Mathf.FloorToInt(pixel.x),Mathf.FloorToInt(pixel.y)).a>.75f;
    }

    static Texture2D ReadSpriteTexture(Texture source)
    {
        var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(source,target); RenderTexture.active=target;
            copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0); copy.Apply();
        }
        finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); }
        return copy;
    }

    IEnumerator VerifyBothTravel()
    {
        Assert.IsTrue(ui.IsPlacementExchanging);
        var pieces=MotionPieces(); Assert.AreEqual(2,pieces.Length);
        var start=pieces.ToDictionary(p => p.name,p => p.anchoredPosition);
        yield return new WaitForSecondsRealtime(.14f);
        Assert.IsTrue(ui.IsPlacementExchanging,"The swap should have an observable intermediate position.");
        foreach(var piece in MotionPieces())
        {
            Assert.Greater(Vector2.Distance(start[piece.name],piece.anchoredPosition),3,
                "Both figures must visibly travel: "+piece.name);
            Assert.IsTrue(piece.GetComponentsInChildren<Image>().Any(i => i.color.a > .1f && i.sprite != null));
        }
        yield return new WaitForSecondsRealtime(.4f); yield return null;
        Assert.IsFalse(ui.IsPlacementExchanging);
    }

    OwnedUnit Buy(string id)
    {
        int slot=OfferSlot(id);
        Assert.IsTrue(ui.Match.Buy(slot,ui.Match.Player.Offers[slot].Token));
        return ui.Match.Player.Owned[id];
    }
    int OfferSlot(string id) => Enumerable.Range(0,ui.Match.Player.Offers.Count)
        .First(i => ui.Match.Player.Offers[i]?.UnitId == id);
    BoardCell Cell(Vector2Int p) => ui.Match.Board.GetCell(p.x,p.y);
    RectTransform[] MotionPieces() => ui.CanvasRoot.GetComponentsInChildren<RectTransform>()
        .Where(r => r.name.StartsWith("exchange-piece-")).ToArray();
    void AssertField(OwnedUnit unit,Vector2Int expected)
    {
        Assert.AreEqual(UnitLocation.Field,unit.Location); Assert.AreEqual(expected,unit.Deployment);
        var actor=ui.Match.Actors[unit];
        Assert.AreSame(Cell(expected),actor.CurrentCell);
        Assert.Less(Vector3.Distance(ui.Match.Mapper.GetWorldPosition(Cell(expected)),actor.transform.position),.0001f);
    }
    void AssertRestored()
    {
        Assert.IsFalse(ui.IsPlacementExchanging);
        Assert.IsEmpty(MotionPieces());
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Placement exchange"));
        foreach(var actor in ui.Match.Actors.Values)
        {
            Assert.IsTrue(actor.GetComponentsInChildren<Renderer>(true).All(r => !r.forceRenderingOff));
            Assert.IsFalse(actor.GetComponent<UnitPresentation>().IsMoving,"A preparation swap does not trigger combat hops.");
        }
        foreach(var bar in ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r => r.name.StartsWith("health-")))
        {
            var group=bar.GetComponent<CanvasGroup>();
            if(group != null) Assert.AreEqual(1,group.alpha,"A completed exchange must restore its health bar.");
        }
    }
}

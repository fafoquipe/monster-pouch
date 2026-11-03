using System.Collections;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class AttackRangePreviewPlayTests
{
    LocalGameUI ui;
    AttackRangeGraphic Preview => ui.CanvasRoot.GetComponentInChildren<AttackRangeGraphic>(true);

    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>();
        ui.Match.enabled = false;
        Assert.IsTrue(ui.Match.StartMatch("popow",new[] { "rosie" },721));
        yield return null; yield return null;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (ui != null) { ui.CloseModal(); ui.Match.Menu(); }
        yield return null;
    }

    [UnityTest] public IEnumerator SelectionShowsClippedRangeAndTargetWithoutChangingTheBoard()
    {
        var match = ui.Match;
        var owned = match.Player.Monster;
        Assert.IsTrue(match.Place(owned.Definition.Id,match.Board.GetCell(0,8)));
        var actors = match.Actors.Values.ToArray();
        var positions = actors.Select(a=>a.CurrentCell).ToArray();
        ui.Select(owned);
        yield return null; yield return null;
        Assert.IsTrue(Preview.gameObject.activeSelf);
        Assert.IsFalse(Preview.raycastTarget);
        Assert.AreEqual(owned.Deployment,Preview.SourceCell);
        Assert.AreEqual(match.InitialOpponent.Position,Preview.TargetCell);
        AssertTargetCenteredInCell();
        int range = UnitStats.FromDefinition(owned,match.Round).AttackRange;
        AssertBounds(new Vector2Int(0,8),range);
        Assert.Less(Preview.PathProgress,1,"The path should begin drawing at the selected character.");
        Canvas.ForceUpdateCanvases();
        int initialVertices = VertexCount();
        yield return new WaitForSecondsRealtime(.24f);
        Assert.That(Preview.PathProgress,Is.InRange(.05f,.95f));
        Canvas.ForceUpdateCanvases();
        Assert.Greater(VertexCount(),initialVertices,"Dashes appear progressively along the fixed path.");
        var drawing = Preview.canvasRenderer.GetMesh();
        try
        {
            Assert.IsTrue(drawing.colors.Any(c=>c.a>0 && c.a<.5f),
                "New dashes fade in at the advancing end instead of appearing abruptly.");
        }
        finally { Object.Destroy(drawing); }
        Capture("range-path-drawing");
        yield return new WaitForSecondsRealtime(.75f);
        Assert.AreEqual(1,Preview.PathProgress);
        Canvas.ForceUpdateCanvases();
        var mesh = Preview.canvasRenderer.GetMesh();
        try
        {
            Assert.Greater(mesh.vertexCount,100,"The boundary and path must be dashed, with a visible star.");
            Assert.IsTrue(mesh.colors.Any(c=>c.r>.9f && c.g>.7f && c.b<.6f),"The endpoint star is gold.");
            Assert.IsTrue(mesh.colors.Where(c=>c.r>.9f && c.g>.7f).All(c=>Mathf.Abs(c.a-.5f)<.01f),
                "The star's colored bands stay half transparent over the character.");
        }
        finally { Object.Destroy(mesh); }
        CollectionAssert.AreEqual(actors,match.Actors.Values);
        CollectionAssert.AreEqual(positions,actors.Select(a=>a.CurrentCell));
        Assert.IsTrue(actors.All(a=>ReferenceEquals(a.CurrentCell.OccupiedBy,a)));
        yield return new WaitForSecondsRealtime(.3f);
        Capture("selected-range-cell-center",true);
    }

    [UnityTest] public IEnumerator OfferAndFieldDragFollowTheHoveredCellAndHideOutsideTheField()
    {
        var match = ui.Match;
        var offer = match.Player.Offers.First(o=>o!=null && o.UnitId=="rosie");
        int slot = Enumerable.Range(0,match.Player.Offers.Count).First(i=>match.Player.Offers[i]==offer);
        yield return new WaitForSecondsRealtime(.6f);
        var first = match.Board.GetCell(0,9);
        ui.PreviewOffer(slot,offer.Token,ScreenPoint(first));
        yield return null;
        Assert.IsTrue(Preview.gameObject.activeSelf);
        Assert.AreEqual(first.Coordinates,Preview.SourceCell);
        AssertBounds(first.Coordinates,match.Config.Get("rosie").AttackRange);
        Assert.IsFalse(match.Player.Owned.ContainsKey("rosie"),"A preview must not buy the offer.");
        ui.DropOffer(slot,offer.Token,ScreenPoint(first));
        yield return null; yield return null;
        var owned = match.Player.Owned["rosie"];
        Assert.AreEqual(first.Coordinates,owned.Deployment);
        var next = match.Board.GetCell(5,6);
        ui.DragPreview(owned,ScreenPoint(next));
        yield return null;
        Assert.AreEqual(next.Coordinates,Preview.SourceCell);
        Assert.AreEqual(first.Coordinates,owned.Deployment,"Hovering must not move the actor.");
        Capture("dragged-range");
        ui.DragPreview(owned,new Vector2(-100,-100));
        yield return null;
        Assert.IsFalse(Preview.gameObject.activeSelf);
        ui.EndDragPreview();
        yield return null;
        Assert.AreEqual(first.Coordinates,Preview.SourceCell);
        ui.Drop(owned,ScreenPoint(next));
        yield return null; yield return null;
        Assert.AreEqual(next.Coordinates,owned.Deployment);
        Assert.AreEqual(next.Coordinates,Preview.SourceCell);
    }

    [UnityTest] public IEnumerator EnemyHiddenRepositionDoesNotLeakAndCombatHidesPreview()
    {
        var match = ui.Match;
        ui.Select(match.Player.Monster);
        yield return null; yield return null;
        var revealed = match.InitialOpponent.Position;
        var hidden = match.Config.FormationCells(match.Bot.Monster.Definition,match.RivalSide)
            .First(p=>p!=revealed && !match.Bot.Owned.Values.Any(u=>u.Location==UnitLocation.Field && u.Deployment==p));
        Assert.IsTrue(match.Bot.TrySetLocation(match.Bot.Monster.Definition.Id,UnitLocation.Field,hidden,out _));
        ui.RenderPage();
        yield return null;
        Assert.AreEqual(revealed,Preview.TargetCell,"Only the revealed formation may drive the preview.");
        match.Ready();
        yield return null; yield return null;
        Assert.IsFalse(Preview.gameObject.activeSelf);
    }

    [UnityTest] public IEnumerator CombatClickShowsLiveRangeAndActualTargetForEitherSideWithoutOpeningInspection()
    {
        var match=ui.Match;
        match.Ready();yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Combat,match.Phase);
        match.Simulation.Step(2f);
        var actor=match.Actors[match.Player.Monster];
        var target=match.Simulation.CurrentTarget(actor);
        Assert.IsNotNull(target);
        void Tap(BattleUnit unit)
        {
            Vector2 point=ui.GameCamera.WorldToScreenPoint(unit.transform.position+Vector3.up*.45f);
            Assert.IsTrue(ui.BeginBoardPointer(point));ui.EndBoardPointer(point);
        }
        Tap(actor);yield return null;yield return null;
        Assert.IsTrue(Preview.gameObject.activeSelf);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Inspection shade"));
        Assert.AreEqual(actor.CurrentCell.Coordinates,Preview.SourceCell);
        Assert.AreEqual(target.CurrentCell.Coordinates,Preview.TargetCell);
        Assert.AreEqual(1,Preview.PathProgress,"The current target is immediately visible during combat.");
        AssertBounds(actor.CurrentCell.Coordinates,actor.BaseStats.AttackRange);
        var free=match.Board.GetAllCells().First(c=>!c.IsBlocked&&c.OccupiedBy==null);
        Assert.IsTrue(match.Board.TryRepositionUnit(target,free));
        target.transform.position=match.Mapper.GetWorldPosition(free);
        yield return null;
        Assert.AreEqual(free.Coordinates,Preview.TargetCell);
        Assert.Less(Vector2.Distance(ui.ToCanvas(ui.GameCamera.WorldToScreenPoint(target.transform.position)),Preview.Target.Value),.1f);
        Tap(target);yield return null;yield return null;
        Assert.AreEqual(target.CurrentCell.Coordinates,Preview.SourceCell);
        Assert.AreEqual(match.Simulation.CurrentTarget(target)?.CurrentCell?.Coordinates,Preview.TargetCell);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Inspection shade"));
        Tap(actor);yield return null;
        target.ApplyDamage(int.MaxValue);yield return null;
        Assert.IsNull(Preview.TargetCell,"A defeated enemy must not retain a target marker.");
        actor.ApplyDamage(int.MaxValue);yield return null;
        Assert.IsFalse(Preview.gameObject.activeSelf);
    }

    [UnityTest] public IEnumerator FlippedOnlineBoardKeepsRangeAndTargetAligned()
    {
        var match = ui.Match;
        match.Menu();
        Assert.IsTrue(match.StartOnlineMatch(BoardSide.Red,"popow",new[] { "rosie" },"bugaloo",new[] { "flo" },921,
            CardTableTheme.MapId(CardTableTheme.Current),CardTableTheme.MapId(CardTableTheme.Current)));
        ui.Select(match.Player.Monster);
        yield return null; yield return null;
        Assert.IsTrue(match.Mapper.Flipped);
        AssertBounds(match.Player.Monster.Deployment,UnitStats.FromDefinition(match.Player.Monster,match.Round).AttackRange);
        Assert.AreEqual(match.InitialOpponent.Position,Preview.TargetCell);
        AssertTargetCenteredInCell();
        Assert.Less(Preview.Target.Value.y,Preview.Source.y,"The opponent stays above the local player.");
    }

    void AssertTargetCenteredInCell()
    {
        var cell = Preview.TargetCell.Value;
        Vector2 center = ui.ToCanvas(ScreenPoint(ui.Match.Board.GetCell(cell.x,cell.y)));
        Assert.Less(Vector2.Distance(center,Preview.Target.Value),.5f,
            "The endpoint star stays in the center of the target's occupied tile.");
    }

    Vector2 ScreenPoint(BoardCell cell) => ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell));
    int VertexCount()
    {
        var mesh = Preview.canvasRenderer.GetMesh();
        try { return mesh.vertexCount; }
        finally { Object.Destroy(mesh); }
    }
    void AssertBounds(Vector2Int cell, int range)
    {
        var match = ui.Match;
        var min = match.Mapper.GetWorldPosition(match.Board.GetCell(Mathf.Max(0,cell.x-range),Mathf.Max(0,cell.y-range)));
        var max = match.Mapper.GetWorldPosition(match.Board.GetCell(Mathf.Min(5,cell.x+range),Mathf.Min(9,cell.y+range)));
        var half = new Vector3(Mathf.Abs(match.Mapper.CellSize.x),Mathf.Abs(match.Mapper.CellSize.y))*.5f;
        var a = ui.ToCanvas(ui.GameCamera.WorldToScreenPoint(Vector3.Min(min,max)-half));
        var b = ui.ToCanvas(ui.GameCamera.WorldToScreenPoint(Vector3.Max(min,max)+half));
        Assert.That(Preview.RangeBounds.xMin,Is.EqualTo(Mathf.Min(a.x,b.x)).Within(.5f));
        Assert.That(Preview.RangeBounds.yMin,Is.EqualTo(Mathf.Min(a.y,b.y)).Within(.5f));
        Assert.That(Preview.RangeBounds.xMax,Is.EqualTo(Mathf.Max(a.x,b.x)).Within(.5f));
        Assert.That(Preview.RangeBounds.yMax,Is.EqualTo(Mathf.Max(a.y,b.y)).Within(.5f));
    }

    void Capture(string name, bool verifyVisibleStar = false)
    {
        // Camera rendering works in batch PlayMode, where WaitForEndOfFrame never fires.
        var camera = ui.GameCamera;
        var canvas = ui.CanvasRoot.GetComponentInParent<Canvas>();
        var mode = canvas.renderMode;
        int oldOrder = canvas.sortingOrder;
        var oldCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var oldTarget = camera.targetTexture;
        var active = RenderTexture.active;
        var target = new RenderTexture(Screen.width,Screen.height,24);
        var image = new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            // Reproduce ScreenSpaceOverlay: all world sprites must remain behind
            // the UI even when this diagnostic temporarily uses a camera canvas.
            canvas.sortingOrder = short.MaxValue;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); image.Apply();
            string folder = Path.Combine(Application.dataPath,"../Logs/local-validation/attack-range-preview");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            if (verifyVisibleStar)
            {
                var cell = Preview.TargetCell.Value;
                Vector2 screen = ScreenPoint(ui.Match.Board.GetCell(cell.x,cell.y));
                var area = new RectInt(Mathf.RoundToInt(screen.x)-16,Mathf.RoundToInt(screen.y)-16,32,32);
                var withMarker = image.GetPixels(area.x,area.y,area.width,area.height);
                Preview.enabled = false;
                Canvas.ForceUpdateCanvases(); camera.Render();
                image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); image.Apply();
                var withoutMarker = image.GetPixels(area.x,area.y,area.width,area.height);
                int changed = Enumerable.Range(0,withMarker.Length).Count(i=>
                    Mathf.Abs(withMarker[i].r-withoutMarker[i].r)+
                    Mathf.Abs(withMarker[i].g-withoutMarker[i].g)+
                    Mathf.Abs(withMarker[i].b-withoutMarker[i].b)>.2f);
                Assert.Greater(changed,75,"The rendered star must be visible at the target's tile center.");
            }
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = active;
            if (Preview != null) Preview.enabled = true;
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            canvas.sortingOrder = oldOrder;
            Object.Destroy(image); Object.Destroy(target);
        }
    }
}

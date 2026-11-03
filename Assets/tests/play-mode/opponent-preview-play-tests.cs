using System.Collections;
using System.Linq;
using MonsterPouch.Local;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class OpponentPreviewPlayTests
{
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); ui.Match.enabled = false;
    }
    [UnityTearDown] public IEnumerator End()
    {
        if (ui != null) { ui.CloseModal(); ui.Match.Menu(); }
        yield return null;
    }

    [UnityTest] public IEnumerator InitialHeroIsMirroredAndLongPressShowsItsRealUpgradesWithoutRevealingNewUnits()
    {
        ui.Match.StartMatch("bugaloo", new[] { "stein" }, 1905); yield return null;
        var snapshot = ui.Match.InitialOpponent;
        var rivalDefault = ui.Match.Config.FormationCells(ui.Match.Bot.Monster.Definition, BoardSide.Blue)[0];
        Assert.AreEqual(new Vector2Int(5-rivalDefault.x, 9-rivalDefault.y), snapshot.Position);
        var view = Echo(snapshot.UnitId);
        Assert.IsFalse(view.IsHologram);
        Assert.AreEqual(Color.white, view.Renderer.color);
        CheckAnchor(snapshot, view);
        var inspect = ui.Match.InspectOpponent(snapshot.UnitId);
        CollectionAssert.AreEqual(ui.Match.Bot.Monster.Tricks, inspect.Tricks);
        Assert.AreNotSame(ui.Match.Bot.Monster, inspect);
        var hidden = ui.Match.Bot.Owned.Values.FirstOrDefault(u => !u.Definition.IsMonster);
        if (hidden != null) Assert.IsNull(ui.Match.InspectOpponent(hidden.Definition.Id));
        Assert.IsTrue(ui.Match.Actors.Values.All(a => a.Side == BoardSide.Blue));
        Assert.IsNull(view.GetComponentInChildren<MonsterPouch.Gameplay.Units.BattleUnit>());
        CheckTarget(view);
        Vector2 screen = ui.GameCamera.WorldToScreenPoint(view.Renderer.bounds.center);
        Assert.IsTrue(ui.BeginBoardPointer(screen)); yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.15f);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.text == GameText.Resolve("Habilidades del rival")));
        ui.EndBoardPointer(screen); ui.CloseModal();

        // Compare against the actual red combat presentation, including transparent
        // sprite margins and authored foot pivot. A cell-sized thumbnail fails this.
        var sprite = view.Renderer.sprite;
        var size = view.Renderer.bounds.size;
        var scale = view.Renderer.transform.lossyScale;
        ui.Match.Ready();
        var combat = ui.Match.Actors[ui.Match.Bot.Monster].GetComponent<UnitPresentation>().Renderer;
        Assert.AreSame(combat.sprite, sprite);
        Assert.Less(Vector3.Distance(combat.bounds.size, size), .0001f);
        Assert.Less(Vector3.Distance(combat.transform.lossyScale, scale), .0001f);
        Assert.IsFalse(view.gameObject.activeInHierarchy, "A combat actor and its echo must never render together.");
        yield return null;
        Assert.AreEqual(0, Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None).Length);
    }

    [UnityTest] public IEnumerator ConsecutiveRefreshesLeaveOneFullSizeStationaryHologramPerRevealedCell()
    {
        ui.Match.StartMatch("anuik", new[] { "stein" }, 1906); ui.Match.Ready();
        var expected = ui.Match.Actors.Where(p => p.Value.Side == BoardSide.Red).ToDictionary(
            p => p.Key.Definition.Id, p => p.Value.GetComponent<UnitPresentation>().Renderer.bounds.size);
        EndRound();
        for (int i = 0; i < 5; i++) ui.RenderPage();
        // Includes objects waiting for deferred destruction: only the newest may render.
        Assert.AreEqual(ui.Match.PreviousOpponent.Count, Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None).Length);
        yield return null;
        var views = Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None);
        Assert.AreEqual(ui.Match.PreviousOpponent.Count, views.Length);
        Assert.AreEqual(views.Length, views.Select(v => v.SnapshotPosition).Distinct().Count());
        foreach (var snapshot in ui.Match.PreviousOpponent)
        {
            var view = Echo(snapshot.UnitId);
            CheckAnchor(snapshot, view); CheckTarget(view);
            Assert.IsTrue(view.IsHologram);
            Assert.That(view.Renderer.color.a, Is.InRange(.5f, .8f));
            Assert.Less(Vector3.Distance(expected[snapshot.UnitId], view.Renderer.bounds.size), .0001f);
        }
        Assert.IsTrue(ui.Match.Actors.Values.All(a => a.Side == BoardSide.Blue));
        foreach (var unit in ui.Match.Bot.Owned.Values.Where(u => !u.IsPositionLocked(ui.Match.Round)))
        {
            var free = ui.Match.Config.RedDeployment.First(p => !ui.Match.Bot.Owned.Values.Any(o => o.Location == UnitLocation.Field && o.Deployment == p));
            ui.Match.Bot.TrySetLocation(unit.Definition.Id, UnitLocation.Field, free, out _);
        }
        ui.RenderPage(); yield return null;
        var positions = Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None).ToDictionary(v => v.UnitId, v => v.Renderer.bounds);
        yield return new WaitForSecondsRealtime(.16f);
        foreach (var snapshot in ui.Match.PreviousOpponent)
        {
            var view = Echo(snapshot.UnitId);
            CheckAnchor(snapshot, view);
            Assert.AreEqual(positions[snapshot.UnitId], view.Renderer.bounds, "Holograms must remain still and retain the previous formation.");
        }
        ui.Match.Menu();
        Assert.AreEqual(0, Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None).Length, "Menu transition must hide world previews immediately.");
    }

    [UnityTest] public IEnumerator SameColumnHologramRendersBehindOwnFrontFigureAtFullSize()
    {
        ui.Match.StartMatch("bugaloo", new[] { "stein" }, 1905);
        var enemyCell = new Vector2Int(3, 4);
        foreach (var unit in ui.Match.Bot.Owned.Values.Where(u => u != ui.Match.Bot.Monster && u.Location == UnitLocation.Field && u.Deployment == enemyCell))
        {
            var free = ui.Match.Config.RedDeployment.First(p => !ui.Match.Bot.Owned.Values.Any(o => o.Location == UnitLocation.Field && o.Deployment == p));
            Assert.IsTrue(ui.Match.Bot.TrySetLocation(unit.Definition.Id, UnitLocation.Field, free, out _));
        }
        Assert.IsTrue(ui.Match.Bot.TrySetLocation(ui.Match.Bot.Monster.Definition.Id, UnitLocation.Field, enemyCell, out _));
        Assert.IsTrue(ui.Match.Place(ui.Match.Player.Monster.Definition.Id, ui.Match.Board.GetCell(3,5)));
        ui.Match.Ready(); EndRound(); yield return null;
        var view = Echo(ui.Match.Bot.Monster.Definition.Id);
        var own = ui.Match.Actors[ui.Match.Player.Monster].GetComponent<UnitPresentation>();
        own.Freeze();
        CheckAnchor(ui.Match.PreviousOpponent.Single(e => e.UnitId == view.UnitId), view);
        Assert.AreEqual(enemyCell, view.SnapshotPosition);
        Assert.AreEqual(new Vector2Int(3,5), ui.Match.Player.Monster.Deployment);
        Assert.AreEqual(own.Renderer.sortingLayerID, view.Renderer.sortingLayerID);
        Assert.Less(view.Renderer.sortingOrder, own.Renderer.sortingOrder, "A farther figure cannot be pasted over the player's nearer figure.");
        Assert.IsNull(view.GetComponentInParent<Canvas>());
        Assert.Greater(OpaqueOverlap(own.Renderer, view.Renderer), 0,
            "This must exercise overlapping visible silhouettes, not only distinct logical cells.");
        // If the Canvas portrait regression returns, it bypasses world depth entirely.
        var target = Target(view.UnitId);
        Assert.IsFalse(target.GetComponentsInChildren<Image>().Any(i => i.sprite != null && i.enabled));
    }

    void EndRound()
    {
        foreach (var actor in ui.Match.Actors.Values.Where(a => a.Side == BoardSide.Red)) actor.ApplyDamage(actor.CurrentHealth);
        ui.Match.Advance(.2f); ui.Match.NextRound();
        Assert.AreEqual(MatchPhase.Preparation, ui.Match.Phase);
    }
    OpponentEchoView Echo(string id) => Object.FindObjectsByType<OpponentEchoView>(FindObjectsSortMode.None).Single(v => v.UnitId == id);
    RectTransform Target(string id) => ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t => t.name == "echo-" + id);
    void CheckAnchor(MatchController.OpponentEcho snapshot, OpponentEchoView view)
    {
        Vector3 expected = ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(snapshot.Position.x, snapshot.Position.y));
        Assert.Less(Vector3.Distance(expected, view.FootPosition), .0001f);
        Assert.AreEqual(snapshot.Position, view.SnapshotPosition);
    }
    void CheckTarget(OpponentEchoView view)
    {
        var corners = new Vector3[4]; Target(view.UnitId).GetWorldCorners(corners);
        Vector2 expectedMin = ui.GameCamera.WorldToScreenPoint(view.Renderer.bounds.min);
        Vector2 expectedMax = ui.GameCamera.WorldToScreenPoint(view.Renderer.bounds.max);
        Assert.Less(Vector2.Distance(corners[0], expectedMin), 1.1f);
        Assert.Less(Vector2.Distance(corners[2], expectedMax), 1.1f);
    }
    static Texture2D ReadTexture(Texture source)
    {
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        try { Graphics.Blit(source, rt); RenderTexture.active = rt; copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0); copy.Apply(); }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
        return copy;
    }
    int OpaqueOverlap(SpriteRenderer own, SpriteRenderer enemy)
    {
        var ownPixels = ReadTexture(own.sprite.texture); var enemyPixels = ReadTexture(enemy.sprite.texture);
        int count = 0;
        try
        {
            Vector2 min = ui.GameCamera.WorldToScreenPoint(own.bounds.min), max = ui.GameCamera.WorldToScreenPoint(own.bounds.max);
            float depth = ui.GameCamera.WorldToScreenPoint(own.transform.position).z;
            for (int y = Mathf.FloorToInt(min.y); y < max.y; y++) for (int x = Mathf.FloorToInt(min.x); x < max.x; x++)
            {
                var world = ui.GameCamera.ScreenToWorldPoint(new Vector3(x+.5f,y+.5f,depth));
                if (Opaque(own, ownPixels, world) && Opaque(enemy, enemyPixels, world)) count++;
            }
        }
        finally { Object.Destroy(ownPixels); Object.Destroy(enemyPixels); }
        return count;
    }
    static bool Opaque(SpriteRenderer renderer, Texture2D pixels, Vector3 world)
    {
        Vector2 local = renderer.transform.InverseTransformPoint(world);
        if (renderer.flipX) local.x = -local.x; if (renderer.flipY) local.y = -local.y;
        Vector2 texel = local * renderer.sprite.pixelsPerUnit + renderer.sprite.pivot;
        if (texel.x < 0 || texel.y < 0 || texel.x >= renderer.sprite.rect.width || texel.y >= renderer.sprite.rect.height) return false;
        texel += renderer.sprite.rect.position;
        return pixels.GetPixel(Mathf.FloorToInt(texel.x), Mathf.FloorToInt(texel.y)).a >= .8f;
    }
}

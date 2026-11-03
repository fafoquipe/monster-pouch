using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class CombatHudPlayTests
{
    GameObject root;
    [TearDown] public void Cleanup() { if (root != null) Object.DestroyImmediate(root); }

    [UnityTest] public IEnumerator CircularIconsKeepTheirOwnTimersAndDoNotDropTheSeventhStatus()
    {
        root = new GameObject("HUD test", typeof(RectTransform), typeof(Canvas));
        var go = new GameObject("health", typeof(RectTransform)); go.transform.SetParent(root.transform, false);
        var hud = go.AddComponent<UnitStatusBadges>();
        var ids = new[] { "stealth", "attack-up", "haste", "guard", "burn", "shield", "slow", "anti-heal" };
        var states = ids.Select(id => new ActiveStatus(id, id == "stealth" ? 4 : -1)).ToArray();
        hud.Present(states, new[] { new PassiveProgress("trimol-boulder", 2, 4) }, 0);
        yield return null;
        Canvas.ForceUpdateCanvases();
        foreach (var ring in hud.GetComponentsInChildren<CombatBadgeRing>())
        {
            Assert.IsNotNull(ring.GetComponent<CanvasRenderer>(), "Every circular plate must actually render.");
            var mesh = ring.canvasRenderer.GetMesh();
            try { Assert.Greater(mesh.vertexCount, 100, "A status needs visible circular background and countdown geometry."); }
            finally { Object.Destroy(mesh); }
        }
        Assert.AreEqual(9, hud.VisibleCount);
        Assert.IsTrue(hud.GetComponentsInChildren<Image>().All(i => i.sprite != null && !i.raycastTarget));
        Assert.IsTrue(hud.GetComponentsInChildren<Image>().All(i => i.sprite.texture.name == "combat-status-atlas" && i.sprite.texture.filterMode == FilterMode.Bilinear), "The authored images must replace the procedural glyphs.");
        Assert.IsTrue(hud.GetComponentsInChildren<Text>().All(t => t.text == "" || t.text == "2"));
        var ghost = hud.GetComponentsInChildren<CombatBadgeRing>().Single(r => r.name == "status-stealth");
        states[0] = new ActiveStatus("stealth", 2);
        hud.Present(states.Reverse().ToArray(), new[] { new PassiveProgress("trimol-boulder", 3, 4) }, .2f);
        Assert.AreEqual(.5f, ghost.RemainingFraction, .001f);
        Assert.IsTrue(hud.GetComponentsInChildren<CombatBadgeRing>().Single(r => r.name == "passive-trimol-boulder").IsReady);
        hud.Present(new[] { new ActiveStatus("control-immunity", 5), new ActiveStatus("invulnerable", 1) }, new PassiveProgress[0], .2f);
        Assert.IsTrue(hud.GetComponentsInChildren<CombatBadgeRing>().Single(r => r.name == "status-control-immunity").Shimmers);
        Assert.IsFalse(hud.GetComponentsInChildren<CombatBadgeRing>().Single(r => r.name == "status-invulnerable").Shimmers);
        var gray = CombatHudSymbols.Tint("invulnerable"); Assert.Less(Mathf.Abs(gray.r - gray.b), .1f);
        Assert.AreNotSame(CombatHudSymbols.Get("control-immunity"), CombatHudSymbols.Get("invulnerable"));
        hud.Present(states.Skip(1).ToArray(), new PassiveProgress[0], .3f);
        Assert.IsFalse(ghost.gameObject.activeSelf); Assert.AreEqual(7, hud.VisibleCount);
        hud.enabled = false; Assert.AreEqual(0, hud.VisibleCount);
    }

    [UnityTest] public IEnumerator StarsStayLeftAndGoldEnergyDiffersFromBothTeamsHealth()
    {
        root = new GameObject("HUD test", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        foreach (var side in new[] { BoardSide.Blue, BoardSide.Red })
        {
            var definition = DocumentedRoster.CreateStein(); var owned = new OwnedUnit(definition);
            for (int i = 0; i < 3; i++) owned.Tricks[i] = true;
            var actor = new GameObject("actor").AddComponent<WhelpUnit>(); actor.transform.SetParent(root.transform);
            actor.Initialize(side.ToString(), side, UnitStats.FromDefinition(definition, owned)); actor.AddEnergy(actor.BaseStats.EnergyMax * .5f);
            var go = new GameObject("health", typeof(RectTransform)); go.transform.SetParent(root.transform, false);
            LocalGameUI.SetRect((RectTransform)go.transform, new Rect(100, 100, 52, 28));
            var vitals = go.AddComponent<UnitVitalsUI>(); vitals.Refresh(actor, owned, 0, BoardSide.Blue);
            yield return null; Canvas.ForceUpdateCanvases();
            var mesh = vitals.canvasRenderer.GetMesh();
            try
            {
                var vertices = mesh.vertices; var colors = mesh.colors;
                Assert.IsTrue(Enumerable.Range(0, vertices.Length).Any(i => vertices[i].x < -1 && colors[i].r > .9f && colors[i].g > .5f), "Stars must be LEFT of the gauge.");
                var charge = Enumerable.Range(0, vertices.Length).Where(i => vertices[i].y >= -21.1f && vertices[i].y <= -15.9f).Select(i => colors[i]).ToArray();
                Assert.IsTrue(charge.Any(c => c.r > .9f && c.g > .6f && c.b < .3f), "Energy is gold on both teams.");
                Assert.IsTrue(colors.Any(c => side == BoardSide.Blue ? c.r < .4f && c.g > .6f && c.b > .8f : c.r > .8f && c.g < .4f && c.b < .4f));
            }
            finally { Object.Destroy(mesh); }
        }
    }

    [UnityTest] public IEnumerator BlotanKeepsHisHudWhileInvisibleAndPauseFreezesTheRing()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null;
        var ui = Object.FindFirstObjectByType<LocalGameUI>(); var match = ui.Match; match.enabled = false;
        Assert.IsTrue(match.StartMatch("anuik", new[] { "blotan" }, 103)); match.Player.AddCombatCoins(30);
        var offer = match.Player.Offers[0]; Assert.IsTrue(match.BuyAndPlace(0, offer.Token, match.Board.GetCell(2, 8)));
        match.Ready(); yield return null; yield return null;
        var actor = match.Actors.Values.Single(a => a.Side == match.LocalSide && a.BaseStats.DefinitionId == "blotan");
        var clocks = (IEnumerable)typeof(CombatSimulation).GetField("actors", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(match.Simulation);
        var clock = clocks.Cast<object>().Single(c => c.GetType().GetField("Unit").GetValue(c) == (object)actor);
        clock.GetType().GetField("StealthUntil").SetValue(clock, 40);
        match.Simulation.Step(.1f); yield return null; yield return null;
        var vitals = ui.CanvasRoot.GetComponentsInChildren<UnitVitalsUI>().Single(v => v.name == "health-" + actor.UnitId);
        Assert.IsTrue(vitals.gameObject.activeInHierarchy); Assert.IsTrue(actor.IsInvisible);
        var ring = vitals.GetComponentsInChildren<CombatBadgeRing>().Single(r => r.name == "status-stealth");
        match.Simulation.Step(.5f); yield return null; yield return null;
        Assert.Less(ring.RemainingFraction, 1); float before = ring.RemainingFraction;
        match.Pause(true); yield return new WaitForSecondsRealtime(.2f); Assert.AreEqual(before, ring.RemainingFraction);
        match.Pause(false); match.Menu(); yield return null;
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<UnitStatusBadges>().Any());
    }
}

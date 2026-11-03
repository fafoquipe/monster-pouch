using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TaurisPositionMarkPlayTests
{
    GameObject root;
    [TearDown] public void Cleanup() { if (root != null) Object.DestroyImmediate(root); }

    [UnityTest] public IEnumerator MarkUsesExistingAbilityArtBesideHealthAndFollowsHudVisibility()
    {
        root = new GameObject("mark UI test", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var definition = DocumentedRoster.CreateAtori(); var owned = new OwnedUnit(definition);
        var actorObject = new GameObject("marked actor"); actorObject.transform.SetParent(root.transform);
        var actor = actorObject.AddComponent<WhelpUnit>(); actor.Initialize("actor", BoardSide.Blue, UnitStats.FromDefinition(definition, owned));
        var go = new GameObject("health", typeof(RectTransform)); go.transform.SetParent(root.transform, false);
        LocalGameUI.SetRect((RectTransform)go.transform, new Rect(100, 100, 52, 28));
        var vitals = go.AddComponent<UnitVitalsUI>(); vitals.Refresh(actor, owned, 0, BoardSide.Blue);
        Assert.IsNull(go.GetComponentInChildren<UnitPositionMarkUI>(true));
        typeof(OwnedUnit).GetProperty("MarkedPositionRound").SetValue(owned, 1);
        vitals.Refresh(actor, owned, 0, BoardSide.Blue);
        yield return null; Canvas.ForceUpdateCanvases();
        var marker = go.GetComponentInChildren<UnitPositionMarkUI>(true);
        Assert.IsNotNull(marker); Assert.IsTrue(marker.Visible); Assert.IsNotNull(marker.Icon);
        Assert.AreSame(AbilityArtwork.Get("tauris-mark"), marker.Icon);
        Assert.IsTrue(marker.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget));
        var rect = (RectTransform)marker.transform;
        Assert.AreEqual(new Vector2(1, 0), rect.anchorMin); Assert.Greater(rect.anchoredPosition.x, 0);
        var mesh = marker.GetComponent<CombatBadgeRing>().canvasRenderer.GetMesh();
        try { Assert.Greater(mesh.vertexCount, 0); } finally { Object.Destroy(mesh); }
        go.SetActive(false); Assert.IsFalse(marker.gameObject.activeInHierarchy);
        go.SetActive(true); Assert.IsTrue(marker.gameObject.activeInHierarchy);
        actorObject.SetActive(false); vitals.Refresh(actor, owned, 0, BoardSide.Blue);
        Assert.IsFalse(marker.Visible);
        actorObject.SetActive(true); vitals.Refresh(actor, owned, 0, BoardSide.Blue);
        Assert.IsTrue(marker.Visible);
        // Removing combat statuses for preparation must not remove the persistent mark.
        var statuses = go.AddComponent<UnitStatusBadges>(); statuses.Refresh(null, actor);
        Assert.IsTrue(marker.Visible); Assert.AreEqual(1, go.GetComponentsInChildren<UnitPositionMarkUI>(true).Length);
        actor.ApplyDamage(99999); vitals.Refresh(actor, owned, 0, BoardSide.Blue);
        Assert.IsFalse(marker.Visible);
    }
}

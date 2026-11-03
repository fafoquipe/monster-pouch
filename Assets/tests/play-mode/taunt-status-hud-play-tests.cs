using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// Presentation tests only. Actual taunt selection, duration and winner remain in Core.
public sealed class TauntStatusHudPlayTests
{
    GameObject root;
    UnitStatusBadges hud;
    readonly PassiveProgress[] noProgress = Array.Empty<PassiveProgress>();

    [SetUp] public void Setup()
    {
        root = new GameObject("taunted target health", typeof(RectTransform));
        hud = root.AddComponent<UnitStatusBadges>();
    }
    [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(root); }

    CombatStatusSnapshot Taunt(string effect, int tick, int start=100, int end=150,
        int reset=1, long revision=1, string source="taunter") =>
        new CombatStatusSnapshot("taunt", effect, source, revision, reset, start, end, 0, 1, tick, revision);

    void Present(CombatStatusSnapshot status, int tick, bool duplicateLegacy=true)
    {
        var legacy = duplicateLegacy ? new[] { new ActiveStatus("taunt", status.RemainingSeconds) }
            : Array.Empty<ActiveStatus>();
        hud.PresentFrame(legacy, noProgress,
            new CombatStatusFrame(status.UnitResetVersion, tick, status.ApplicationRevision, true, new[] { status }), tick * .1f);
    }
    CombatBadgeRing Ring() => hud.GetComponentsInChildren<CombatBadgeRing>().Single(r => r.name == "status-taunt");

    [TestCase("alveyr-escudo-de-los-vientos", 50)]
    [TestCase("korran-bastion-imperial", 15)]
    public void ExistingCircleUsesRealIntervalOnLateObservationAndDeduplicatesLegacy(string effect, int duration)
    {
        int start=100, end=start+duration, now=start+duration/2;
        Present(Taunt(effect, now, start, end), now);
        var ring=Ring();
        Assert.AreEqual(1, hud.VisibleCount);
        Assert.AreEqual((float)(end-now)/duration, ring.RemainingFraction, .0001f);
        Assert.AreEqual(new Vector2(8,9), ring.rectTransform.anchoredPosition);
        Assert.AreEqual(new Vector2(17,17), ring.rectTransform.sizeDelta);
        Assert.AreSame(CombatHudSymbols.Get("taunt"), ring.GetComponentInChildren<Image>().sprite);
        Assert.IsNotNull(ring.GetComponentInChildren<Image>().sprite);
        Present(Taunt(effect, now+1, start, end), now+1);
        Assert.AreSame(ring, Ring());
        Assert.Less(ring.RemainingFraction, (float)(end-now)/duration);
        float frozen=ring.RemainingFraction;
        Present(Taunt(effect, now+1, start, end), now+1);
        Assert.AreEqual(frozen, ring.RemainingFraction, "No independent presentation clock.");
    }

    [Test] public void AuthoritativeRefreshAndSourceReplacementReuseOneCircle()
    {
        Present(Taunt("alveyr-escudo-de-los-vientos",125),125);
        var ring=Ring(); Assert.AreEqual(.5f,ring.RemainingFraction,.0001f);
        Present(Taunt("korran-bastion-imperial",125,125,155,1,2,"korran"),125);
        Assert.AreSame(ring,Ring()); Assert.AreEqual(1,hud.VisibleCount);
        Assert.AreEqual(1,ring.RemainingFraction,.0001f);
        Present(Taunt("korran-bastion-imperial",140,125,155,1,2,"korran"),140);
        Assert.AreEqual(.5f,ring.RemainingFraction,.0001f);
    }

    [Test] public void ExpiryInactiveFrameDisableAndRoundReentryClearTheCircle()
    {
        Present(Taunt("alveyr-escudo-de-los-vientos",125),125);
        var ring=Ring();
        hud.PresentFrame(Array.Empty<ActiveStatus>(),noProgress,
            new CombatStatusFrame(1,150,2,true,Array.Empty<CombatStatusSnapshot>()),15);
        Assert.Zero(hud.VisibleCount); Assert.IsFalse(ring.gameObject.activeSelf);
        Present(Taunt("alveyr-escudo-de-los-vientos",125),125);
        hud.PresentFrame(Array.Empty<ActiveStatus>(),noProgress,
            new CombatStatusFrame(1,125,3,false,Array.Empty<CombatStatusSnapshot>()),12.5f);
        Assert.Zero(hud.VisibleCount); Assert.IsFalse(ring.gameObject.activeSelf);
        Present(Taunt("korran-bastion-imperial",0,0,15,2,1),0);
        Assert.AreEqual(1,hud.VisibleCount); Assert.AreEqual(1,Ring().RemainingFraction,.0001f);
        hud.enabled=false; Assert.Zero(hud.VisibleCount);
        hud.enabled=true;
        hud.PresentFrame(Array.Empty<ActiveStatus>(),noProgress,
            new CombatStatusFrame(3,0,1,true,Array.Empty<CombatStatusSnapshot>()),0);
        Assert.Zero(hud.VisibleCount);
    }
}

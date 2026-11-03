using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class UnitStatusFrameTests
{
    GameObject root;
    UnitStatusBadges view;
    static readonly ActiveStatus[] EmptyLegacy = Array.Empty<ActiveStatus>();
    static readonly PassiveProgress[] EmptyProgress = Array.Empty<PassiveProgress>();
    [SetUp] public void SetUp()
    { root = new GameObject("Status frame test", typeof(RectTransform)); view = root.AddComponent<UnitStatusBadges>(); }
    [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(root); }
    static CombatStatusSnapshot State(string id, int start, int end, int now, int reset = 1,
        CombatStatusTimingKind kind = CombatStatusTimingKind.Timed, float capacity = 1)
        => new CombatStatusSnapshot(id, "test-effect", "test-source", 1, reset, start, end, 0, capacity, now, 1, kind);
    static CombatStatusFrame Frame(int tick, params CombatStatusSnapshot[] states)
        => new CombatStatusFrame(1, tick, 1, true, states);
    CombatBadgeRing Ring(string id) => root.GetComponentsInChildren<CombatBadgeRing>().Single(r => r.name == "status-" + id);

    [Test] public void DelayedObservationUsesCapturedIntervalAndNotFirstObservation()
    {
        view.PresentFrame(EmptyLegacy, EmptyProgress, Frame(15, State("root", 0, 20, 15)), 1.5f);
        Assert.AreEqual(.25f, Ring("root").RemainingFraction, .0001f);
        view.PresentFrame(EmptyLegacy, EmptyProgress, Frame(18, State("root", 0, 20, 18)), 1.8f);
        Assert.AreEqual(.1f, Ring("root").RemainingFraction, .0001f);
    }
    [Test] public void SnapshotReplacesMatchingLegacyAliasAndPreservesOtherLegacyStates()
    {
        var legacy = new[] { new ActiveStatus("guard", 30), new ActiveStatus("burn", 4) };
        view.PresentFrame(legacy, EmptyProgress, Frame(15, State("damage-reduction", 0, 20, 15)), 1.5f);
        Assert.AreEqual(2, view.VisibleCount);
        Assert.AreEqual(.25f, Ring("damage-reduction").RemainingFraction, .0001f);
        Assert.NotNull(Ring("burn"));
    }
    [Test] public void SameTickRemovalAndInactiveFrameClearImmediately()
    {
        view.PresentFrame(EmptyLegacy, EmptyProgress, Frame(5, State("root", 0, 20, 5)), .5f);
        view.PresentFrame(EmptyLegacy, EmptyProgress, Frame(5), .5f);
        Assert.AreEqual(0, view.VisibleCount);
        view.PresentFrame(EmptyLegacy, EmptyProgress, Frame(5, State("root", 0, 20, 5)), .5f);
        view.PresentFrame(new[] { new ActiveStatus("burn", 4) }, EmptyProgress,
            new CombatStatusFrame(1, 5, 2, false, Array.Empty<CombatStatusSnapshot>()), .5f);
        Assert.AreEqual(0, view.VisibleCount);
    }
    [Test] public void NewApplicationUpdatesOneBadgeAndResetDoesNotKeepOldTimer()
    {
        view.PresentFrame(EmptyLegacy, EmptyProgress, Frame(15, State("root", 0, 20, 15)), 1.5f);
        var first = Ring("root");
        view.PresentFrame(EmptyLegacy, EmptyProgress, Frame(15, State("root", 15, 55, 15)), 1.5f);
        Assert.AreEqual(1, view.VisibleCount); Assert.AreSame(first, Ring("root"));
        Assert.AreEqual(1, first.RemainingFraction);
        view.PresentFrame(EmptyLegacy, EmptyProgress,
            new CombatStatusFrame(2, 15, 3, true, new[] { State("root", 10, 30, 15, 2) }), 1.5f);
        Assert.AreEqual(1, view.VisibleCount); Assert.AreEqual(.75f, Ring("root").RemainingFraction, .0001f);
    }
    [Test] public void EmptySnapshotFramePreservesLegacyOnlyRecipient()
    {
        view.PresentFrame(new[] { new ActiveStatus("burn", 4) }, EmptyProgress, Frame(5), .5f);
        Assert.AreEqual(1, view.VisibleCount); Assert.NotNull(Ring("burn"));
    }
    [Test] public void NonTimedEffectsStaySteadyAndShieldCapacityIsNotTime()
    {
        view.PresentFrame(EmptyLegacy, EmptyProgress,
            Frame(15, State("empowered", 0, 20, 15, kind: CombatStatusTimingKind.Consumption),
                State("shield", 0, 20, 15, capacity: 40)), 1.5f);
        Assert.AreEqual(1, Ring("empowered").RemainingFraction);
        Assert.AreEqual(.25f, Ring("shield").RemainingFraction, .0001f);
    }
    [Test] public void UnknownSymbolsNeverDisplayGuardArtwork()
    { Assert.IsNull(CombatHudSymbols.Get("unknown-new-state")); }
}

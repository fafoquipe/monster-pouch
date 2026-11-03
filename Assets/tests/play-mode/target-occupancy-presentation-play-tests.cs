using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class TargetOccupancyPresentationPlayTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Advance = typeof(UnitPresentation).GetMethod("AdvancePresentation", Hidden);
    readonly List<GameObject> roots = new List<GameObject>();
    readonly List<CombatSimulation> simulations = new List<CombatSimulation>();
    BoardManager board;
    BoardWorldMapper mapper;
    CombatSimulation sim;
    BattleUnit walker, hymay;
    UnitPresentation view;

    [TearDown] public void Cleanup()
    {
        foreach (var simulation in simulations) simulation.Stop();
        foreach (var root in roots) Object.DestroyImmediate(root);
        roots.Clear(); simulations.Clear();
    }

    void CreateMovingFixture()
    {
        var root = new GameObject("target-occupancy-play"); roots.Add(root);
        board = root.AddComponent<BoardManager>(); board.BuildBoard();
        mapper = root.AddComponent<BoardWorldMapper>(); mapper.Configure(board, root.transform, Vector2.one, Vector2.zero);
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig"); Assert.IsNotNull(config);
        var character = config.Characters.Single(c => c.Id == "hymay");
        var definition = character.CreateRuntimeDefinition();
        definition.Damage = 0; definition.AttackRange = 9; definition.AttackInterval = 100;
        definition.EnergyPerAttack = definition.EnergyOnDamage = definition.EnergyPerSecond = 0;
        definition.BaseAbility.Parameters = new[]
        {
            new AbilityParameter { Key = "pullDuration", Value = .6f },
            new AbilityParameter { Key = "postArrivalStun", Value = 1f }
        };
        hymay = CreateUnit(root, "hook-source", BoardSide.Blue, UnitStats.FromDefinition(definition), 2, 7);
        walker = CreateUnit(root, "walker", BoardSide.Red, new UnitStats(1000, 0, moveInterval: 1f), 2, 4);
        view = walker.gameObject.AddComponent<UnitPresentation>(); view.enabled = false;
        var walkerArt = config.Characters.Single(c => c.Id == "stein").Visuals;
        view.Configure(walker, mapper, walkerArt);
        view.SnapToCell();
        sim = new CombatSimulation(board); simulations.Add(sim);
        sim.Moved += (unit, from, to) =>
        { if (unit == walker) view.Move(mapper.GetWorldPosition(from), mapper.GetWorldPosition(to), unit.BaseStats.MoveInterval); };
        sim.Begin(new[] { hymay, walker }); sim.Step(.1f);
        Assert.AreSame(board.GetCell(2, 4), walker.CurrentCell);
        Assert.AreSame(board.GetCell(2, 5), walker.ReservedCell);
        // Sample after this character's hop preparation, while its reserved step is airborne.
        var hop = TileHopProfile.For(walkerArt.Id);
        Sample((hop.Prepare + hop.Air * .5f) * walker.BaseStats.MoveInterval);
        Assert.IsTrue(view.IsMoving);
        Assert.Greater(Vector3.Distance(walker.transform.position, mapper.GetWorldPosition(walker.CurrentCell)), .001f);
    }

    static BattleUnit CreateUnit(GameObject parent, string id, BoardSide side, UnitStats stats, int x, int y)
    {
        var go = new GameObject(id); go.transform.SetParent(parent.transform);
        var unit = go.AddComponent<WhelpUnit>(); unit.Initialize(id, side, stats);
        Assert.IsTrue(parent.GetComponent<BoardManager>().TryOccupyCell(unit, x, y)); return unit;
    }

    void Sample(float seconds) => Advance.Invoke(view, new object[] { seconds });
    static void Passive(BattleUnit unit) => typeof(BattleUnit).GetProperty("IsPassiveTrainingTarget").SetValue(unit, true);

    [UnityTest] public IEnumerator CancellingAReservedStepStopsItsRendererAtTheOccupiedCell()
    {
        foreach (string reason in new[] { "reset", "unit-disable", "object-disable", "reservation-cancel", "stop" })
        {
            CreateMovingFixture();
            var origin = walker.CurrentCell; var abandoned = walker.ReservedCell;
            Passive(walker); // Do not start an unrelated new step after the cancellation.
            if (reason == "reset") walker.ResetForCombat();
            if (reason == "unit-disable") walker.enabled = false;
            if (reason == "object-disable") { view.enabled = true; walker.gameObject.SetActive(false); }
            if (reason == "reservation-cancel") board.CancelReservation(walker);
            if (reason == "stop") sim.Stop(); else sim.Step(.1f);
            if (reason == "object-disable") { walker.gameObject.SetActive(true); view.enabled = false; }
            Sample(2f);
            Assert.IsFalse(view.IsMoving, reason);
            Assert.IsNull(abandoned.ReservedBy, reason);
            Assert.IsNull(abandoned.OccupiedBy, reason);
            Assert.AreSame(origin, walker.CurrentCell, reason);
            Assert.That(Vector3.Distance(mapper.GetWorldPosition(origin), walker.transform.position), Is.LessThan(.0001f), reason);
            sim.Stop();
            yield return null;
        }
    }

    [UnityTest] public IEnumerator HookReplacesAMovingRouteWithoutReplayingItsReservedDestination()
    {
        CreateMovingFixture();
        var abandoned = walker.ReservedCell;
        Vector3 contactPosition = walker.transform.position;
        // Invoke the real control transition at a deterministic mid-step instant.
        var clock = typeof(CombatSimulation).GetMethod("Clock", Hidden);
        var hook = typeof(CombatSimulation).GetMethod("StartHookPull", Hidden);
        Assert.IsNotNull(clock); Assert.IsNotNull(hook);
        var landing = board.GetCell(2, 6);
        Assert.IsTrue((bool)hook.Invoke(sim, new[] { clock.Invoke(sim, new object[] { hymay }),
            clock.Invoke(sim, new object[] { walker }), landing }));
        Assert.IsTrue(walker.IsBeingPulled);
        Assert.IsTrue(view.IsBeingPulled);
        Assert.IsNull(abandoned.ReservedBy);
        Assert.IsNull(abandoned.OccupiedBy);
        Assert.That(Vector3.Distance(contactPosition, walker.transform.position), Is.LessThan(.0001f),
            "The hook must begin at the rendered contact position, without snapping back to the step origin.");
        Sample(.3f);
        Vector3 middle = Vector3.Lerp(contactPosition, mapper.GetWorldPosition(landing), .5f);
        Assert.That(Vector3.Distance(middle, walker.transform.position), Is.LessThan(.0001f));
        sim.Step(.6f); Sample(.3f);
        Assert.IsFalse(view.IsMoving); Assert.IsFalse(view.IsBeingPulled);
        Assert.AreSame(landing, walker.CurrentCell);
        Assert.That(Vector3.Distance(mapper.GetWorldPosition(landing), walker.transform.position), Is.LessThan(.0001f));
        sim.Step(.5f); Sample(.5f); // Past the abandoned step's original arrival time.
        Assert.AreSame(landing, walker.CurrentCell);
        Assert.IsNull(abandoned.OccupiedBy);
        Assert.That(Vector3.Distance(mapper.GetWorldPosition(landing), walker.transform.position), Is.LessThan(.0001f));
        yield return null;
    }
}

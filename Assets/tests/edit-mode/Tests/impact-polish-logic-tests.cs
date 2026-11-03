using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class ImpactPolishLogicTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation simulation;
        [SetUp] public void Setup()
        {
            var go = new GameObject("impact-polish-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void Cleanup()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side = BoardSide.Blue, params int[] tricks)
        {
            var go = new GameObject(definition.Id + "-" + objects.Count); objects.Add(go);
            var unit = go.AddComponent<WhelpUnit>(); var owned = new OwnedUnit(definition);
            foreach (int index in tricks) owned.Tricks[index] = true;
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        BattleUnit Target(int x, int y, BoardSide side = BoardSide.Red) => Unit(new UnitDefinition {
            Id = "target", MaxHealth = 10000, Damage = 0, AttackRange = 9, AttackInterval = 100,
            AttackWindup = .1f, MoveInterval = 100 }, x, y, side);
        void Begin(params BattleUnit[] units) { simulation = new CombatSimulation(board); simulation.Begin(units); }
        static void Charge(BattleUnit unit) => unit.AddEnergy(unit.BaseStats.EnergyMax);

        [Test] public void DashCrossesFarthestTargetAndSignalsExactLaneBeforeMovement()
        {
            var popow = Unit(DocumentedRoster.CreatePopow(), 2, 8);
            var near = Target(2, 6); var far = Target(2, 2); var outside = Target(3, 4);
            Begin(popow, near, far, outside); Charge(popow);
            var order = new List<string>();
            simulation.DashStarted += (actor, from, through, to, width, duration) => {
                Assert.AreSame(popow, actor); Assert.AreEqual(new Vector2Int(2, 8), from.Coordinates);
                Assert.AreSame(far.CurrentCell, through); Assert.AreEqual(new Vector2Int(2, 1), to.Coordinates);
                Assert.AreEqual(1, width); Assert.AreEqual(.4f, duration, .001f); order.Add("dash");
            };
            simulation.Moved += (actor, _, __) => { if (actor == popow) order.Add("move"); };
            simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { "dash", "move" }, order);
            Assert.AreEqual(new Vector2Int(2, 1), popow.CurrentCell.Coordinates);
            Assert.IsFalse(near.IsStunned); Assert.IsFalse(far.IsStunned);
            simulation.Step(.4f);
            Assert.IsTrue(near.IsStunned); Assert.IsTrue(far.IsStunned); Assert.IsFalse(outside.IsStunned);
            Assert.AreSame(far, board.GetCell(2, 2).OccupiedBy);
        }

        [Test] public void DashSkipsBlockedAndReservedLandingsBeyondTarget()
        {
            var popow = Unit(DocumentedRoster.CreatePopow(), 2, 8); var far = Target(2, 3);
            var ally = Target(5, 8, BoardSide.Blue);
            Assert.IsTrue(board.TrySetCellBlocked(2, 2, true));
            Begin(popow, far, ally); Assert.IsTrue(board.TryReserveCell(ally, 2, 1)); Charge(popow);
            simulation.Step(.1f);
            Assert.AreEqual(new Vector2Int(2, 0), popow.CurrentCell.Coordinates);
            Assert.AreSame(ally, board.GetCell(2, 1).ReservedBy); Assert.IsFalse(far.IsStunned);
            simulation.Step(.4f); Assert.IsTrue(far.IsStunned);
        }

        [Test] public void DashAtBoardEdgeChoosesDeterministicLateralCellAndStillHitsTarget()
        {
            var popow = Unit(DocumentedRoster.CreatePopow(), 2, 8); var far = Target(2, 0);
            var near = Target(2, 4); var blocker = Target(1, 0, BoardSide.Blue);
            Begin(popow, far, near, blocker); Charge(popow); simulation.Step(.1f);
            Assert.AreEqual(new Vector2Int(3, 0), popow.CurrentCell.Coordinates);
            Assert.IsFalse(far.IsStunned); Assert.IsFalse(near.IsStunned);
            simulation.Step(.4f);
            Assert.IsTrue(far.IsStunned); Assert.IsTrue(near.IsStunned);
            Assert.AreSame(blocker, board.GetCell(1, 0).OccupiedBy);
        }

        [Test] public void DashDiagonalHitsCrossedCentersAndExcludesOutsideLane()
        {
            var popow = Unit(DocumentedRoster.CreatePopow(), 1, 8); var far = Target(4, 2);
            var inside = Target(2, 6); var outside = Target(4, 5);
            Begin(popow, far, inside, outside); Charge(popow); simulation.Step(.1f);
            Assert.Greater(Vector2.Dot((Vector2)(popow.CurrentCell.Coordinates - far.CurrentCell.Coordinates), new Vector2(3, -6)), 0);
            Assert.IsFalse(inside.IsStunned); Assert.IsFalse(far.IsStunned);
            simulation.Step(.4f);
            Assert.IsTrue(inside.IsStunned); Assert.IsTrue(far.IsStunned); Assert.IsFalse(outside.IsStunned);
        }

        [Test] public void DashDoesNotPunchBeforeTravelFinishes()
        {
            var definition = DocumentedRoster.CreatePopow();
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "travelDuration", Value = .8f } };
            var popow = Unit(definition, 2, 8); var far = Target(2, 2);
            Begin(popow, far); Charge(popow); int punches = 0;
            simulation.Attacked += (actor, _, __) => { if (actor == popow) punches++; };
            simulation.Step(.8f); Assert.AreEqual(0, punches);
            simulation.Step(.1f); Assert.AreEqual(1, punches);
            Assert.AreEqual(10000,far.CurrentHealth,"Dash completion starts the basic preparation.");
            simulation.Step(.2f); Assert.AreEqual(10000,far.CurrentHealth);
            simulation.Step(.1f); Assert.AreEqual(9992,far.CurrentHealth);
        }

        [Test] public void FloExtractsFlowerBeforePlantingAndHonorsConfiguredDuration()
        {
            var definition = DocumentedRoster.CreateFlo();
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "castDuration", Value = .3f } };
            var flo = Unit(definition, 2, 7, BoardSide.Blue, 1); var enemy = Target(5, 0);
            Begin(flo, enemy); var events = new List<string>();
            simulation.FlowerCastStarted += (actor, cell, duration) => {
                Assert.AreSame(flo, actor); Assert.AreEqual(new Vector2Int(2, 6), cell.Coordinates);
                Assert.AreEqual(.3f, duration, .001f); events.Add("extract");
            };
            simulation.Summoned += (_, __) => events.Add("plant");
            simulation.Step(.1f); Assert.AreEqual(0, simulation.CombatOnlyUnits.Count);
            Assert.AreEqual(0, flo.Energy); Assert.AreEqual(new Vector2Int(2, 7), flo.CurrentCell.Coordinates);
            simulation.Step(.2f); Assert.AreEqual(0, simulation.CombatOnlyUnits.Count);
            simulation.Step(.1f); Assert.AreEqual(1, simulation.CombatOnlyUnits.Count);
            CollectionAssert.AreEqual(new[] { "extract", "plant" }, events);
        }

        [Test] public void FloRechecksCellAtPlantTimeWithoutOverlappingAnEntrant()
        {
            var flo = Unit(DocumentedRoster.CreateFlo(), 2, 7, BoardSide.Blue, 1);
            var ally = Target(5, 8, BoardSide.Blue); var enemy = Target(5, 0);
            Begin(flo, ally, enemy); simulation.Step(.1f);
            Assert.IsTrue(board.TryRepositionUnit(ally, board.GetCell(2, 6)));
            simulation.Step(.5f);
            Assert.AreEqual(1, simulation.CombatOnlyUnits.Count);
            Assert.AreSame(ally, board.GetCell(2, 6).OccupiedBy);
            var flower = simulation.CombatOnlyUnits.Single();
            Assert.AreNotEqual(new Vector2Int(2, 6), flower.CurrentCell.Coordinates);
            Assert.AreSame(flower, flower.CurrentCell.OccupiedBy);
        }

        [TestCase(false)] [TestCase(true)] public void FloResetOrDeathCancelsUnfinishedFlower(bool death)
        {
            var flo = Unit(DocumentedRoster.CreateFlo(), 2, 7, BoardSide.Blue, 1);
            var ally = Target(4, 8, BoardSide.Blue); var enemy = Target(5, 0);
            Begin(flo, ally, enemy); simulation.Step(.1f);
            if (death) flo.ApplyDamage(10000); else flo.ResetForCombat();
            simulation.Step(1); Assert.AreEqual(0, simulation.CombatOnlyUnits.Count);
        }

        [Test] public void BuguiUnlimitedRetainsFivePercentPerHitAndContinuesAfterTenStacks()
        {
            var normalDefinition = DocumentedRoster.CreateBugui(); normalDefinition.AttackInterval = 1.5f;
            normalDefinition.Damage = 1; normalDefinition.AttackWindup = .1f;
            var unlimitedDefinition = DocumentedRoster.CreateBugui(); unlimitedDefinition.AttackInterval = 1.5f;
            unlimitedDefinition.Damage = 1; unlimitedDefinition.AttackWindup = .1f;
            var normal = Unit(normalDefinition, 1, 5); var unlimited = Unit(unlimitedDefinition, 3, 5, BoardSide.Blue, 1);
            var enemy = Target(2, 4); Begin(normal, unlimited, enemy);
            var normalTimes = new List<float>(); var unlimitedTimes = new List<float>();
            simulation.Attacked += (actor, _, __) => {
                if (actor == normal) normalTimes.Add(simulation.Elapsed);
                if (actor == unlimited) unlimitedTimes.Add(simulation.Elapsed);
            };
            simulation.Step(30);
            Assert.AreEqual(5, AbilityBalance.Resolve(unlimitedDefinition.Tricks[1]).Single(p => p.Key == "speedPercent").Value);
            Assert.GreaterOrEqual(normalTimes.Count, 20); Assert.Greater(unlimitedTimes.Count, normalTimes.Count);
            CollectionAssert.AreEqual(normalTimes.Take(11).ToArray(), unlimitedTimes.Take(11).ToArray());
            Assert.Less(unlimitedTimes[19], normalTimes[19]);
        }
    }
}

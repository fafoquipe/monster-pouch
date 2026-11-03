using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TargetOccupancyTests
    {
        private GameObject root;
        private BoardManager board;
        private CombatSimulation sim;

        [SetUp] public void Setup()
        {
            root = new GameObject("target-occupancy");
            board = root.AddComponent<BoardManager>();
            board.BuildBoard();
        }

        [TearDown] public void Cleanup() { sim?.Stop(); Object.DestroyImmediate(root); }

        private static int Y(BoardSide side, int y) => side == BoardSide.Blue ? y : 9 - y;
        private static BoardSide Other(BoardSide side) => side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;

        private BattleUnit Unit(string id, BoardSide side, int x, int y, int range = 1,
            float move = .8f, bool documented = false, TargetPolicy policy = TargetPolicy.NearestReachable, int iq = 1)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform);
            var unit = go.AddComponent<MonsterUnit>();
            unit.Initialize(id, side, UnitStats.FromDefinition(new UnitDefinition
            {
                Id = id, UsesDocumentedRules = documented, MaxHealth = 1000, Damage = 10,
                AttackRange = range, MoveInterval = move, AttackInterval = 100, AttackWindup = .1f,
                AttackHold = 0, AttackRecovery = 0, TargetPolicy = policy, IQSpeed = iq,
                EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0
            }));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void ReservedRearCellDoesNotOutrankFrontOccupantUntilArrival(BoardSide side)
        {
            var attacker = Unit("ranged", side, 2, Y(side, 7), range: 2);
            var front = Unit("front", Other(side), 3, Y(side, 5));
            var rear = Unit("rear", Other(side), 2, Y(side, 4));
            BoardCell origin = rear.CurrentCell, destination = board.GetCell(2, Y(side, 5));
            var result = BoardMovementResolver.ReserveMovement(board,
                new[] { new BoardMovementIntent(rear, destination) });
            Assert.AreEqual(BoardMovementStatus.Reserved, result[0].Status);
            var step = new CombatMovementStep(rear, 8);
            foreach (var order in new[] { new[] { rear, front }, new[] { front, rear } })
            {
                var selection = CombatTargetSelector.SelectTarget(board, attacker, order);
                Assert.AreSame(front, selection.Target);
                Assert.AreEqual(CombatTargetSelectionStatus.ReadyToAttack, selection.Status);
            }
            Assert.IsFalse(step.Advance(board, 7));
            Assert.AreSame(origin, rear.CurrentCell);
            Assert.AreSame(rear, origin.OccupiedBy);
            Assert.IsNull(destination.OccupiedBy);
            Assert.AreSame(rear, destination.ReservedBy);
            Assert.IsFalse(board.TryReserveCell(front, destination), "Navigation still respects the reservation.");
            Assert.IsTrue(step.Advance(board, 8));
            Assert.IsTrue(step.Arrived);
            Assert.AreSame(destination, rear.CurrentCell);
            Assert.IsNull(origin.OccupiedBy);
            Assert.IsNull(rear.ReservedCell);
            Assert.AreSame(rear, CombatTargetSelector.SelectTarget(board, attacker, new[] { front, rear }).Target);
        }

        [TestCase(BoardSide.Blue, false, false)] [TestCase(BoardSide.Red, false, false)]
        [TestCase(BoardSide.Blue, true, false)] [TestCase(BoardSide.Red, true, false)]
        [TestCase(BoardSide.Blue, false, true)] [TestCase(BoardSide.Red, false, true)]
        [TestCase(BoardSide.Blue, true, true)] [TestCase(BoardSide.Red, true, true)]
        public void FirstRangedShotAfterOpeningPauseUsesFrontOccupant(
            BoardSide side, bool documented, bool reverseOrder)
        {
            var attacker = Unit(reverseOrder ? "z-ranged" : "a-ranged", side, 2, Y(side, 8),
                range: 2, move: .1f, documented: documented);
            var front = Unit("m-front", Other(side), 3, Y(side, 5), range: 9, documented: documented);
            var rear = Unit(reverseOrder ? "a-rear" : "z-rear", Other(side), 2, Y(side, 4), documented: documented, iq: 9);
            var shots = new List<BattleUnit>();
            int starts = 0;
            sim = new CombatSimulation(board, openingWindowSeconds: 1f);
            sim.Moved += (unit, from, to) => { Assert.AreSame(from, unit.CurrentCell); starts++; };
            sim.Attacked += (unit, target, ranged) => { if (unit == attacker) shots.Add(target); };
            sim.Begin(reverseOrder ? new[] { rear, front, attacker } : new[] { attacker, front, rear });
            sim.Step(1f);
            Assert.AreEqual(0, starts, "Preserve the complete opening pause.");
            Assert.IsEmpty(shots);
            Assert.IsNull(sim.CurrentTarget(attacker));
            sim.Step(.1f);
            Assert.IsEmpty(shots);
            Assert.AreEqual(Y(side, 4), rear.CurrentCell.Y);
            Assert.AreEqual(Y(side, 5), rear.ReservedCell.Y);
            sim.Step(.1f);
            Assert.AreEqual(1, shots.Count);
            Assert.AreSame(front, shots[0], "The rear troop has not arrived at its reserved cell.");
            Assert.AreSame(front, sim.CurrentTarget(attacker));
            Assert.AreSame(rear, board.GetCell(2, Y(side, 5)).ReservedBy);
            Assert.IsNull(board.GetCell(2, Y(side, 5)).OccupiedBy);
            // Arrival does not steal an existing in-range lock.
            sim.Step(.7f);
            Assert.AreEqual(Y(side, 5), rear.CurrentCell.Y);
            Assert.AreSame(front, sim.CurrentTarget(attacker));
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void LowestHealthPolicyStillSelectsMovingRearTroopAtItsOccupiedCell(BoardSide side)
        {
            var attacker = Unit("atori-policy", side, 2, Y(side, 7), range: 4, policy: TargetPolicy.LowestHealth);
            var front = Unit("front", Other(side), 3, Y(side, 5));
            var rear = Unit("rear", Other(side), 2, Y(side, 4));
            rear.ApplyDamage(900);
            Assert.IsTrue(board.TryReserveCell(rear, 2, Y(side, 5)));
            Assert.AreSame(rear, CombatTargetSelector.SelectTarget(board, attacker, new[] { front, rear }).Target);
        }

        [TestCase(false)] [TestCase(true)]
        public void ReservationDoesNotMoveATargetOutOfRangeBeforeProjectileRelease(bool documented)
        {
            var attacker = Unit("ranged", BoardSide.Blue, 2, 8, range: 2, documented: documented);
            var target = Unit("target", BoardSide.Red, 2, 6, range: 9, documented: documented);
            sim = new CombatSimulation(board);
            sim.Begin(new[] { attacker, target });
            target.IsPassiveTrainingTarget = true;
            sim.Step(.1f);
            Assert.IsTrue(board.TryReserveCell(target, 2, 5));
            sim.Step(.3f);
            Assert.AreEqual(990, target.CurrentHealth, "Range is validated against the reached cell, not the reserved one.");
            Assert.AreEqual(6, target.CurrentCell.Y);
            Assert.AreSame(target, sim.CurrentTarget(attacker));
        }

        [TestCase(false, 1f)] [TestCase(true, 1f)]
        [TestCase(false, 1.5f)] [TestCase(true, 1.5f)]
        public void ConfiguredOpeningWindowRemainsUnchanged(bool documented, float openingWindow)
        {
            var attacker = Unit("ranged", BoardSide.Blue, 2, 8, range: 3, documented: documented);
            var target = Unit("target", BoardSide.Red, 2, 5, range: 9, documented: documented);
            sim = new CombatSimulation(board, openingWindowSeconds: openingWindow);
            int shots = 0;
            sim.Attacked += (unit, victim, ranged) => { if (unit == attacker) shots++; };
            sim.Begin(new[] { attacker, target });
            sim.Step(openingWindow);
            Assert.AreEqual(0, shots);
            sim.Step(.1f);
            Assert.AreEqual(1, shots);
            Assert.AreSame(target, sim.CurrentTarget(attacker));
        }

        [TestCase(false)] [TestCase(true)]
        public void ReleasedProjectileKeepsItsCapturedTargetWhenTargetMoves(bool documented)
        {
            var attacker = Unit("ranged", BoardSide.Blue, 2, 8, range: 3, documented: documented);
            var target = Unit("target", BoardSide.Red, 2, 5, range: 9, documented: documented);
            var other = Unit("other", BoardSide.Red, 0, 0, range: 9, documented: documented);
            sim = new CombatSimulation(board);
            int impacts = 0;
            sim.Impacted += (source, victim, damage) =>
            { if (source == attacker) { Assert.AreSame(target, victim); impacts++; } };
            sim.Begin(new[] { attacker, target, other });
            sim.Step(.2f); // Attack at .1, release at .2, arrival at .5.
            Assert.AreEqual(0, impacts);
            Assert.IsTrue(board.TryRepositionUnit(target, board.GetCell(5, 0)));
            sim.Step(.3f);
            Assert.AreEqual(1, impacts);
            Assert.AreEqual(990, target.CurrentHealth);
            Assert.AreEqual(1000, other.CurrentHealth);
        }

        [TestCase("reset")] [TestCase("death")] [TestCase("disable")] [TestCase("teleport")]
        public void InterruptedStepCannotCommitAStaleDestination(string interruption)
        {
            var unit = Unit("moving", BoardSide.Blue, 2, 8);
            var origin = unit.CurrentCell; var destination = board.GetCell(2, 7);
            Assert.IsTrue(board.TryReserveCell(unit, destination));
            var step = new CombatMovementStep(unit, 8);
            if (interruption == "reset") unit.ResetForCombat();
            if (interruption == "death") unit.ApplyDamage(1000);
            if (interruption == "disable") unit.gameObject.SetActive(false);
            if (interruption == "teleport")
            {
                Assert.IsTrue(board.TryRepositionUnit(unit, board.GetCell(4, 8)));
                Assert.IsTrue(board.TryReserveCell(unit, 4, 7));
            }
            Assert.IsTrue(step.Advance(board, 8));
            Assert.IsFalse(step.Arrived);
            Assert.IsNull(destination.OccupiedBy);
            Assert.IsNull(destination.ReservedBy);
            if (interruption == "teleport") Assert.AreSame(board.GetCell(4, 7), unit.ReservedCell);
            else { Assert.AreSame(origin, unit.CurrentCell); Assert.IsNull(unit.ReservedCell); }
        }

        [TestCase(false)] [TestCase(true)]
        public void StoppingOrResettingCombatCancelsOutstandingTravel(bool restart)
        {
            var unit = Unit("moving", BoardSide.Blue, 2, 8);
            var enemy = Unit("enemy", BoardSide.Red, 2, 4, range: 9);
            sim = new CombatSimulation(board);
            sim.Begin(new[] { unit, enemy }); sim.Step(.1f);
            var origin = unit.CurrentCell; var destination = unit.ReservedCell;
            Assert.IsNotNull(destination);
            if (restart) sim.Begin(new[] { unit, enemy }); else sim.Stop();
            Assert.AreSame(origin, unit.CurrentCell);
            Assert.IsNull(unit.ReservedCell);
            Assert.IsNull(destination.ReservedBy);
            Assert.IsNull(destination.OccupiedBy);
        }
    }
}

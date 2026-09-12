using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TargetLockTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<BattleUnit> attacks = new List<BattleUnit>();
        private BoardManager board;

        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("target-lock-board");
            objects.Add(root);
            board = root.AddComponent<BoardManager>();
            board.BuildBoard();
            attacks.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private BattleUnit Unit(string id, int x, int y, UnitStats stats = null, BoardSide side = BoardSide.Red)
        {
            var go = new GameObject(id);
            objects.Add(go);
            var unit = go.AddComponent<MonsterUnit>();
            unit.Initialize(id, side, stats ?? new UnitStats(1000, 0, 9, attackInterval: 100));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }

        private CombatSimulation Begin(BattleUnit actor, params BattleUnit[] enemies)
        {
            var units = new List<BattleUnit> { actor };
            units.AddRange(enemies);
            var sim = new CombatSimulation(board);
            sim.Attacked += (source, target, projectile) => { if (source == actor) attacks.Add(target); };
            sim.Begin(units);
            return sim;
        }

        private void Move(BattleUnit unit, int x, int y) =>
            Assert.IsTrue(board.TryRepositionUnit(unit, board.GetCell(x, y)));

        private void OnlyAttacks(BattleUnit target, int minimum = 2)
        {
            Assert.GreaterOrEqual(attacks.Count, minimum);
            foreach (BattleUnit attacked in attacks) Assert.AreSame(target, attacked);
        }

        [TestCase(1, BoardSide.Blue)]
        [TestCase(3, BoardSide.Blue)]
        [TestCase(1, BoardSide.Red)]
        [TestCase(3, BoardSide.Red)]
        public void MovingTarget_IsPursuedInsteadOfSwitchingToTheNewAdjacentEnemy(int range, BoardSide side)
        {
            int Y(int value) => side == BoardSide.Blue ? value : 9 - value;
            BoardSide other = side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;
            var actor = Unit("actor", 2, Y(8), new UnitStats(1000, 1, range, .4f, .1f), side);
            var original = Unit("original", 2, Y(8 - range), side: other);
            var intruder = Unit("intruder", 5, Y(0), side: other);
            var sim = Begin(actor, original, intruder);
            sim.Step(.1f);
            OnlyAttacks(original, 1);
            Move(original, 2, Y(1));
            Move(intruder, 2, Y(7));
            sim.Step(3f);
            OnlyAttacks(original);
            Assert.AreEqual(1000, intruder.CurrentHealth);
            Assert.AreNotEqual(Y(8), actor.CurrentCell.Y);
        }

        [Test]
        public void TargetIsLockedDuringPursuitBeforeTheFirstAttack()
        {
            var actor = Unit("actor", 2, 9, new UnitStats(1000, 1, 1, .4f, .1f), BoardSide.Blue);
            var original = Unit("original", 2, 5);
            var intruder = Unit("intruder", 5, 0);
            var sim = Begin(actor, original, intruder);
            sim.Step(.1f);
            Assert.IsEmpty(attacks);
            Assert.AreEqual(8, actor.CurrentCell.Y);
            Move(intruder, 2, 7);
            sim.Step(3f);
            OnlyAttacks(original);
            Assert.AreEqual(1000, intruder.CurrentHealth);
        }

        [Test]
        public void LowestHealthPolicy_OnlyAppliesWhenAcquiringAnOpponent()
        {
            var actor = Unit("atori", 2, 8, new UnitStats(1000, 1, 9, .4f,
                targetPolicy: TargetPolicy.LowestHealth), BoardSide.Blue);
            var original = Unit("original", 2, 4, new UnitStats(50, 0, 9, 100));
            var intruder = Unit("intruder", 5, 0, new UnitStats(100, 0, 9, 100));
            var sim = Begin(actor, original, intruder);
            sim.Step(.1f);
            intruder.ApplyDamage(99);
            sim.Step(1.5f);
            OnlyAttacks(original);
            Assert.AreEqual(1, intruder.CurrentHealth);
        }

        [Test]
        public void NewlySummonedLowHealthDummy_DoesNotStealTheKayonLock()
        {
            var actor = Unit("actor", 2, 8, new UnitStats(1000, 1, 3, .4f,
                targetPolicy: TargetPolicy.LowestHealth), BoardSide.Blue);
            var kayon = Unit("kayon", 2, 5, new UnitStats(1000, 0, 9, 100,
                summonInterval: .4f, maxLivingSummons: 1));
            var sim = Begin(actor, kayon);
            int summons = 0;
            sim.Summoned += (unit, definition) => summons++;
            sim.Step(2f);
            Assert.Greater(summons, 0);
            OnlyAttacks(kayon);
        }

        [Test]
        public void TemporarilyBlockedRoute_WaitsAndResumesAgainstTheSameOpponent()
        {
            var actor = Unit("actor", 2, 8, new UnitStats(1000, 1, 1, .4f, .1f), BoardSide.Blue);
            var original = Unit("original", 2, 7);
            var intruder = Unit("intruder", 5, 0);
            var sim = Begin(actor, original, intruder);
            sim.Step(.1f);
            Move(original, 0, 0);
            Move(intruder, 2, 7);
            Assert.IsTrue(board.TrySetCellBlocked(0, 1, true));
            Assert.IsTrue(board.TrySetCellBlocked(1, 0, true));
            Assert.IsTrue(board.TrySetCellBlocked(1, 1, true));
            sim.Step(1f);
            Assert.AreEqual(1, attacks.Count);
            Assert.AreEqual(1000, intruder.CurrentHealth);
            Assert.IsTrue(board.TrySetCellBlocked(1, 1, false));
            sim.Step(4f);
            OnlyAttacks(original);
        }

        [Test]
        public void PermanentlyKilledTarget_AllowsAnotherOpponent()
        {
            var actor = Unit("actor", 2, 8, new UnitStats(1000, 1, 1, .4f, .1f), BoardSide.Blue);
            var original = Unit("original", 2, 7, new UnitStats(1, 0, 9, 100));
            var next = Unit("next", 5, 0);
            var sim = Begin(actor, original, next);
            sim.Step(.2f);
            Assert.IsFalse(original.IsAlive);
            Assert.IsNull(original.CurrentCell);
            sim.Step(3f);
            Assert.Greater(attacks.Count, 1);
            Assert.AreSame(original, attacks[0]);
            for (int i = 1; i < attacks.Count; i++) Assert.AreSame(next, attacks[i]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResettingActorOrStartingANewRound_ClearsItsPreviousTarget(bool newRound)
        {
            var actor = Unit("actor", 2, 8, new UnitStats(1000, 1, 3, .4f, .1f), BoardSide.Blue);
            var original = Unit("original", 2, 5);
            var next = Unit("next", 5, 0);
            var sim = Begin(actor, original, next);
            sim.Step(.1f);
            OnlyAttacks(original, 1);
            Move(original, 2, 0);
            Move(next, 2, 7);
            attacks.Clear();
            if (newRound) sim.Begin(new[] { actor, original, next });
            else actor.ResetForCombat();
            sim.Step(1f);
            OnlyAttacks(next);
        }

        [Test]
        public void AnuikRevival_ReleasesAttackersAndDoesNotStealTheirNewTargetOnReturn()
        {
            var actor = Unit("actor", 2, 8, new UnitStats(1000, 1, 9, .3f,
                targetPolicy: TargetPolicy.LowestHealth), BoardSide.Blue);
            var anuik = Unit("anuik", 2, 4, new UnitStats(50, 0, 9, 100, revivesPerCombat: 1));
            var intruder = Unit("intruder", 5, 0, new UnitStats(100, 0, 9, 100));
            var sim = Begin(actor, anuik, intruder);
            sim.Step(.1f);
            OnlyAttacks(anuik, 1);
            anuik.ApplyDamage(50);
            intruder.ApplyDamage(60);
            sim.Step(.1f);
            Assert.IsTrue(anuik.IsReviving);
            sim.Step(.6f);
            Assert.Greater(attacks.Count, 1);
            Assert.AreSame(intruder, attacks[1]);
            sim.Step(.7f);
            Assert.IsTrue(anuik.IsAlive);
            Assert.IsFalse(anuik.IsReviving);
            for (int i = 1; i < attacks.Count; i++) Assert.AreSame(intruder, attacks[i]);
            Assert.Less(intruder.CurrentHealth, 40);
        }

        [Test]
        public void AnuikAsTheOnlyOpponent_CanBeAcquiredAgainAfterRevival()
        {
            var actor = Unit("actor", 2, 8, new UnitStats(1000, 1, 3, .3f), BoardSide.Blue);
            var anuik = Unit("anuik", 2, 5, new UnitStats(50, 0, 9, 100, revivesPerCombat: 1));
            var sim = Begin(actor, anuik);
            sim.Step(.1f);
            anuik.ApplyDamage(50);
            sim.Step(.7f);
            Assert.IsTrue(anuik.IsReviving);
            Assert.IsFalse(sim.Finished);
            Assert.AreEqual(1, attacks.Count);
            sim.Step(.8f);
            Assert.IsTrue(anuik.IsAlive);
            OnlyAttacks(anuik);
        }

        [Test]
        public void RevivedAnuik_CanAcquireANewOpponentForHisOwnAttacks()
        {
            var anuik = Unit("anuik", 2, 8, new UnitStats(50, 1, 9, .3f,
                targetPolicy: TargetPolicy.LowestHealth, revivesPerCombat: 1), BoardSide.Blue);
            var original = Unit("original", 2, 4, new UnitStats(40, 0, 9, 100));
            var next = Unit("next", 5, 0, new UnitStats(100, 0, 9, 100));
            var sim = Begin(anuik, original, next);
            sim.Step(.1f);
            OnlyAttacks(original, 1);
            anuik.ApplyDamage(50);
            next.ApplyDamage(99);
            sim.Step(.1f);
            Assert.IsTrue(anuik.IsReviving);
            sim.Step(1f);
            Assert.IsTrue(anuik.IsAlive);
            Assert.Greater(attacks.Count, 1);
            Assert.AreSame(next, attacks[1]);
        }
    }
}

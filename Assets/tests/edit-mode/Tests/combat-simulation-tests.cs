using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CombatSimulationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("simulation-board");
            objects.Add(go);
            board = go.AddComponent<BoardManager>();
            board.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private BattleUnit Unit(string id, BoardSide side, int x, int y, UnitStats stats, bool monster = true)
        {
            var go = new GameObject(id);
            objects.Add(go);
            BattleUnit unit = monster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, stats);
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SimultaneousTeamElimination_IsDrawRegardlessOfQuadrantAndIQ(bool monsters)
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(10, 10, iqSpeed: 99), monsters);
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(10, 10, iqSpeed: 1), monsters);
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            sim.Step(0.2f);
            Assert.IsTrue(sim.Finished);
            Assert.AreEqual(0, blue.CurrentHealth);
            Assert.AreEqual(0, red.CurrentHealth);
            Assert.IsNull(sim.Winner); // B beats A for reservations, never for mutual elimination.
            Assert.IsNull(blue.CurrentCell);
            Assert.IsNull(red.CurrentCell);
            Assert.AreEqual(0, sim.PendingImpactCount);
        }

        [Test]
        public void EqualSimultaneousDeath_IsDrawAndDoesNotPickAnID()
        {
            var blue = Unit("zzz", BoardSide.Blue, 2, 0, new UnitStats(10, 10));
            var red = Unit("aaa", BoardSide.Red, 1, 1, new UnitStats(10, 10));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { red, blue });
            sim.Step(0.2f);
            Assert.IsTrue(sim.Finished);
            Assert.IsNull(sim.Winner);
            StringAssert.Contains("simultánea", sim.Reason);
        }

        [Test]
        public void MonsterDeath_DoesNotEndRoundWhileEnemyWhelpSurvives()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(20, 10));
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(1, 0));
            var whelp = Unit("whelp", BoardSide.Red, 5, 0, new UnitStats(999, 0), false);
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red, whelp });
            sim.Step(0.2f);
            Assert.IsFalse(red.IsAlive);
            Assert.IsNull(sim.Winner);
            Assert.IsTrue(whelp.IsAlive);
            Assert.IsFalse(sim.Finished);
        }

        [Test]
        public void BothMonstersDead_SurvivingWhelpsContinueFighting()
        {
            var blue = Unit("blue-monster", BoardSide.Blue, 2, 4, new UnitStats(10, 10));
            var red = Unit("red-monster", BoardSide.Red, 3, 4, new UnitStats(10, 10));
            var blueWhelp = Unit("blue-whelp", BoardSide.Blue, 0, 4, new UnitStats(20, 3), false);
            var redWhelp = Unit("red-whelp", BoardSide.Red, 5, 4, new UnitStats(20, 3), false);
            var sim = new CombatSimulation(board);
            int whelpHits = 0;
            sim.Impacted += (source, target, amount) =>
            {
                if (source.Category == UnitCategory.Whelp && target.Category == UnitCategory.Whelp && amount > 0)
                    whelpHits++;
            };
            sim.Begin(new[] { blue, red, blueWhelp, redWhelp });
            sim.Step(0.2f);
            Assert.IsFalse(blue.IsAlive);
            Assert.IsFalse(red.IsAlive);
            Assert.IsTrue(blueWhelp.IsAlive);
            Assert.IsTrue(redWhelp.IsAlive);
            Assert.IsFalse(sim.Finished);
            for (int i = 0; i < 400 && !sim.Finished; i++) sim.Step(0.1f);
            Assert.Greater(whelpHits, 0);
            Assert.IsTrue(sim.Finished);
            Assert.IsFalse(blueWhelp.IsAlive && redWhelp.IsAlive, "The fight must finish by eliminating a remaining Whelp.");
        }

        [Test]
        public void LastWhelpDeath_EndsRoundWithoutRequiringASurvivingMonster()
        {
            var blue = Unit("blue-whelp", BoardSide.Blue, 2, 4, new UnitStats(20, 10), false);
            var red = Unit("red-whelp", BoardSide.Red, 3, 4, new UnitStats(5, 1), false);
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            Assert.IsFalse(sim.Finished);
            sim.Step(0.2f);
            Assert.IsTrue(blue.IsAlive);
            Assert.IsFalse(red.IsAlive);
            Assert.IsTrue(sim.Finished);
            Assert.AreEqual(BoardSide.Blue, sim.Winner);
            StringAssert.Contains("todas sus unidades", sim.Reason);
        }

        [Test]
        public void DeadShootersProjectile_StillImpactsWhileItsTeamHasASurvivor()
        {
            var shooter = Unit("blue-shooter", BoardSide.Blue, 2, 4, new UnitStats(1, 7, 3), false);
            var ally = Unit("blue-ally", BoardSide.Blue, 0, 9, new UnitStats(50, 0));
            var enemy = Unit("red", BoardSide.Red, 2, 3, new UnitStats(50, 2));
            var sim = new CombatSimulation(board);
            int projectileHits = 0;
            sim.Impacted += (source, target, amount) => { if (source == shooter) projectileHits++; };
            sim.Begin(new[] { shooter, ally, enemy });
            sim.Step(0.2f);
            Assert.IsFalse(shooter.IsAlive);
            Assert.IsTrue(ally.IsAlive);
            Assert.IsFalse(sim.Finished);
            Assert.Greater(sim.PendingImpactCount, 0);
            sim.Step(0.2f);
            Assert.AreEqual(43, enemy.CurrentHealth);
            Assert.AreEqual(1, projectileHits);
        }

        [Test]
        public void TeamElimination_CancelsFutureProjectilesAndFreezesTheResult()
        {
            var shooter = Unit("blue-shooter", BoardSide.Blue, 2, 4, new UnitStats(1, 100, 3), false);
            var enemy = Unit("red", BoardSide.Red, 2, 3, new UnitStats(50, 2));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { shooter, enemy });
            sim.Step(0.1f);
            Assert.AreEqual(2, sim.PendingImpactCount);
            sim.Step(0.1f);
            Assert.IsTrue(sim.Finished);
            Assert.AreEqual(BoardSide.Red, sim.Winner);
            Assert.AreEqual(0, sim.PendingImpactCount);
            float finishedAt = sim.Elapsed;
            sim.Step(5f);
            Assert.AreEqual(50, enemy.CurrentHealth);
            Assert.AreEqual(finishedAt, sim.Elapsed);
            Assert.AreEqual(BoardSide.Red, sim.Winner);
        }

        [Test]
        public void Movement_WorksWithoutViewAndRemainsOrthogonal()
        {
            var blue = Unit("blue", BoardSide.Blue, 0, 9, new UnitStats(50, 3));
            var red = Unit("red", BoardSide.Red, 5, 0, new UnitStats(50, 3));
            var sim = new CombatSimulation(board);
            int events = 0;
            sim.Moved += (unit, from, to) =>
            {
                Assert.AreEqual(1, Mathf.Abs(to.X - from.X) + Mathf.Abs(to.Y - from.Y));
                Assert.AreSame(unit, to.OccupiedBy);
                events++;
            };
            sim.Begin(new[] { blue, red });
            sim.Step(0.1f);
            Assert.AreEqual(2, events);
            Assert.AreNotSame(blue.CurrentCell, red.CurrentCell);
            Assert.IsNull(blue.ReservedCell);
            Assert.IsNull(red.ReservedCell);
        }

        [Test]
        public void RangedProjectile_HasTravelTimeAndAppliesExactlyOnce()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(100, 10, 3, 30));
            var red = Unit("red", BoardSide.Red, 2, 1, new UnitStats(100, 10, 3, 30));
            var sim = new CombatSimulation(board);
            int hits = 0;
            sim.Impacted += (a, b, damage) => hits++;
            sim.Begin(new[] { blue, red });
            sim.Step(0.4f);
            Assert.AreEqual(100, blue.CurrentHealth);
            Assert.AreEqual(2, sim.PendingImpactCount);
            sim.Step(0.1f);
            Assert.AreEqual(90, blue.CurrentHealth);
            Assert.AreEqual(90, red.CurrentHealth);
            Assert.AreEqual(2, hits);
            sim.Step(0.5f);
            Assert.AreEqual(90, blue.CurrentHealth);
            Assert.AreEqual(2, hits);
        }

        [Test]
        public void Attack_WaitsForLogicalTravelToFinishWithoutConsultingTheView()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 6, new UnitStats(100, 5, moveInterval: 0.6f));
            var red = Unit("red", BoardSide.Red, 2, 4, new UnitStats(100, 1, 3));
            var sim = new CombatSimulation(board);
            int blueAttacks = 0;
            sim.Attacked += (actor, target, ranged) => { if (actor == blue) blueAttacks++; };
            sim.Begin(new[] { blue, red });
            sim.Step(0.1f);
            Assert.AreEqual(5, blue.CurrentCell.Y);
            sim.Step(0.5f);
            Assert.AreEqual(0, blueAttacks);
            sim.Step(0.1f);
            Assert.AreEqual(1, blueAttacks);
        }

        [Test]
        public void ArmorAndHeal_ApplyAtImpactAndNeverResurrectLethalDamage()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(10, 4, healOnHit: 2));
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(30, 1, armor: 2));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            blue.ApplyDamage(5);
            sim.Step(0.2f);
            Assert.AreEqual(6, blue.CurrentHealth); // 5 - 1 + 2
            Assert.AreEqual(28, red.CurrentHealth); // 30 - (4 - 2)
        }

        [Test]
        public void EveryThirdHitBonus_IsARealEffect()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(100, 2, attackInterval: 0.1f,
                bonusEveryHits: 3, bonusDamage: 5));
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(100, 0));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            sim.Step(0.6f);
            Assert.AreEqual(89, red.CurrentHealth); // Three hits, 2+2+7.
        }

        [Test]
        public void ConfiguredWindup_HelperMatchesActualDamageTick()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(100, 10, attackWindup: 0.3f));
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(100, 0, attackWindup: 0.2f));
            var sim = new CombatSimulation(board);
            float attackStarted = -1, impactAt = -1, announcedDelay = -1;
            sim.Attacked += (actor, target, ranged) =>
            {
                if (actor != blue) return;
                attackStarted = sim.Elapsed;
                announcedDelay = CombatSimulation.GetImpactDelay(actor, target);
            };
            sim.Impacted += (actor, target, damage) => { if (actor == blue) impactAt = sim.Elapsed; };
            sim.Begin(new[] { blue, red });
            sim.Step(0.3f);
            Assert.AreEqual(100, red.CurrentHealth);
            sim.Step(0.1f);
            Assert.AreEqual(90, red.CurrentHealth);
            Assert.AreEqual(0.3f, announcedDelay, 0.0001f);
            Assert.AreEqual(attackStarted + announcedDelay, impactAt, 0.0001f);
        }

        [Test]
        public void Stalemate_StopsAtTenSecondsWithNoDamageOrMovement()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(100, 0));
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(100, 0));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            sim.Step(9.9f);
            Assert.IsFalse(sim.Finished);
            sim.Step(0.1f);
            Assert.IsTrue(sim.Finished);
            Assert.IsNull(sim.Winner);
            Assert.AreEqual(10f, sim.Elapsed, 0.001f);
            Assert.AreEqual(0, sim.PendingImpactCount);
        }

        [Test]
        public void ActiveCombat_StopsAtFortySecondsWithoutInventingWinner()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(10000, 1));
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(10000, 1));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            sim.Step(40f);
            Assert.IsTrue(sim.Finished);
            Assert.IsNull(sim.Winner);
            Assert.AreEqual(40f, sim.Elapsed, 0.001f);
            Assert.Less(blue.CurrentHealth, 10000);
        }

        [Test]
        public void InputOrderAndFrameChunking_DoNotChangeCombat()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(100, 3, attackInterval: 0.7f));
            var red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(100, 2, attackInterval: 0.4f));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            for (int i = 0; i < 50; i++) sim.Step(0.1f);
            int blueHealth = blue.CurrentHealth, redHealth = red.CurrentHealth;
            sim.Begin(new[] { red, blue });
            for (int i = 0; i < 20; i++) sim.Step(0.25f);
            Assert.AreEqual(blueHealth, blue.CurrentHealth);
            Assert.AreEqual(redHealth, red.CurrentHealth);
            Assert.AreEqual(5f, sim.Elapsed, 0.001f);
        }

        [Test]
        public void StopAndRestart_ClearQueuedDamageAndRestoreLife()
        {
            var blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(100, 10, 3));
            var red = Unit("red", BoardSide.Red, 2, 1, new UnitStats(100, 10, 3));
            var sim = new CombatSimulation(board);
            sim.Begin(new[] { blue, red });
            sim.Step(0.1f);
            Assert.AreEqual(2, sim.PendingImpactCount);
            sim.Stop();
            sim.Step(5f);
            Assert.AreEqual(100, red.CurrentHealth);
            Assert.AreEqual(0, sim.PendingImpactCount);
            red.ApplyDamage(20);
            sim.Begin(new[] { blue, red });
            Assert.AreEqual(100, red.CurrentHealth);
            Assert.AreEqual(0, sim.Elapsed);
        }

        [Test]
        public void ConfiguredRangeAndLowestHealthPolicy_AffectSelection()
        {
            var actor = Unit("actor", BoardSide.Blue, 2, 4,
                new UnitStats(100, 3, 3, targetPolicy: TargetPolicy.LowestHealth));
            var near = Unit("near", BoardSide.Red, 2, 3, new UnitStats(100, 1));
            var injured = Unit("injured", BoardSide.Red, 4, 1, new UnitStats(100, 1), false);
            injured.ApplyDamage(50);
            CombatTargetSelection choice = CombatTargetSelector.SelectTarget(board, actor, new[] { near, injured });
            Assert.AreSame(injured, choice.Target);
            Assert.AreEqual(CombatTargetSelectionStatus.ReadyToAttack, choice.Status);
            Assert.AreSame(actor.CurrentCell, choice.AttackCell);
        }

        [Test]
        public void AcquiredTricks_ChangeInstanceStatsWithoutMutatingCatalogue()
        {
            MatchConfig config = MatchConfig.CreateDefault();
            try
            {
                UnitDefinition dummy = config.Get("dummy");
                var owned = new OwnedUnit(dummy);
                owned.Tricks[0] = owned.Tricks[1] = owned.Tricks[2] = true;
                UnitStats stats = UnitStats.FromDefinition(dummy, owned);
                Assert.AreEqual(dummy.MaxHealth + 18, stats.MaxHealth);
                Assert.AreEqual(2, stats.Armor);
                Assert.AreEqual(2, stats.HealOnHit);
                Assert.AreEqual(45, dummy.MaxHealth);
                UnitDefinition bugui = config.Get("bugui");
                var ranged = new OwnedUnit(bugui);
                ranged.Tricks[0] = ranged.Tricks[1] = ranged.Tricks[2] = true;
                UnitStats rangedStats = UnitStats.FromDefinition(bugui, ranged);
                Assert.AreEqual(bugui.Damage + 2, rangedStats.Attack);
                Assert.AreEqual(bugui.AttackRange + 1, rangedStats.AttackRange);
                Assert.AreEqual(bugui.AttackInterval * 0.75f, rangedStats.AttackInterval, 0.0001f);
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}

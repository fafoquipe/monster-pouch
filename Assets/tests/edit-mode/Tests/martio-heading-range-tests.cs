using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class MartioHeadingRangeTests
    {
        sealed class World : IMartioOccupancy
        {
            public readonly Dictionary<ProjectileGridPoint, string> Enemies = new Dictionary<ProjectileGridPoint, string>();
            public readonly List<MartioDamageEvent> Hits = new List<MartioDamageEvent>();
            public string EnemyOccupying(ProjectileGridPoint cell) => Enemies.TryGetValue(cell, out var id) ? id : null;
        }
        static ProjectileGridPoint P(int x, int y) => new ProjectileGridPoint(x, y);
        static string Key(MartioDamageEvent e) => e.Projectile + ":" + e.Kind + ":" + e.EnemyId + ":" + e.Damage;

        [Test]
        public void AcquisitionFourAndTravelSevenAreIndependent()
        {
            var config = new MartioPrototypeConfig();
            Assert.IsTrue(MartioAttackLaunch.CanStartAttack(config, P(2, 8), P(2, 4)));
            Assert.IsTrue(MartioAttackLaunch.CanStartAttack(config, P(2, 8), P(5, 4)));
            Assert.IsFalse(MartioAttackLaunch.CanStartAttack(config, P(2, 8), P(2, 3)));
            Assert.IsFalse(MartioAttackLaunch.CanStartAttack(config, P(2, 8), P(2, 8)));
            Assert.Throws<InvalidOperationException>(() => MartioAttackLaunch.Create(config, P(2, 8), P(2, 1), false, false, false));
            var world = new World(); world.Enemies[P(2, 1)] = "at-seven"; world.Enemies[P(2, 0)] = "at-eight";
            // The nearby target chooses the heading; leaving before impact does not erase the flight.
            var volley = MartioAttackLaunch.Create(config, P(2, 8), P(2, 4), false, false, false);
            volley.Advance(10, world, world.Hits.Add);
            Assert.AreEqual(12, world.Hits.Where(h => h.EnemyId == "at-seven").Sum(h => h.Damage));
            Assert.IsFalse(world.Hits.Any(h => h.EnemyId == "at-eight")); Assert.AreEqual(7, volley.Range);
            config.AttackRange.Value = 2;
            Assert.AreEqual(7, config.ProjectileTravelRange.Value);
        }

        [Test]
        public void PiercingNeverExtendsTheSevenCellTravelLimit()
        {
            var config = new MartioPrototypeConfig(); var world = new World();
            world.Enemies[P(2, 7)] = "near"; world.Enemies[P(2, 1)] = "far"; world.Enemies[P(2, 0)] = "beyond";
            var volley = MartioAttackLaunch.Create(config, P(2, 8), P(2, 7), false, true, false);
            volley.Advance(10, world, world.Hits.Add);
            Assert.IsTrue(volley.Complete); Assert.AreEqual(7, volley.Progress);
            Assert.AreEqual(12, world.Hits.Where(h => h.EnemyId == "near").Sum(h => h.Damage));
            Assert.AreEqual(12, world.Hits.Where(h => h.EnemyId == "far").Sum(h => h.Damage));
            Assert.IsFalse(world.Hits.Any(h => h.EnemyId == "beyond"));
        }

        [Test]
        public void AllCostsAreThreeTemporarilyAndAttackSpeedIsIndependentOfFlightSpeed()
        {
            var c = new MartioPrototypeConfig();
            Assert.AreEqual(3, c.BaseCost.Value); Assert.AreEqual(3, c.ProvisionalExplosionCost);
            Assert.AreEqual(3, c.PiercingCost.Value); Assert.AreEqual(3, c.MoonUpgradeCost.Value);
            Assert.IsTrue(c.CostsAreProvisional); Assert.IsTrue(c.ExplosionCostApproved);
            c.ProjectileForwardStepsPerSecond.Value = 2;
            Assert.AreEqual(1.3f, c.AttackIntervalSeconds.Value);
            CollectionAssert.DoesNotContain(c.PendingDecisions(), "Vida");
            CollectionAssert.DoesNotContain(c.PendingDecisions(), "Coste base");
        }

        [TestCase(-1, -1)] [TestCase(0, -1)] [TestCase(1, -1)] [TestCase(-1, 0)]
        [TestCase(1, 0)] [TestCase(-1, 1)] [TestCase(0, 1)] [TestCase(1, 1)]
        public void OriginalEightDirectionsKeepExactlyTheSamePattern(int dx, int dy)
        {
            foreach (BraidedProjectile projectile in Enum.GetValues(typeof(BraidedProjectile)))
            for (int step = 0; step <= 7; step++)
            {
                var original = BraidedProjectileGeometry.Node(P(2, 8), P(dx, dy), step, projectile, 1, 1);
                var actual = MartioFlightGeometry.Node(P(2, 8), P(dx, dy), step, projectile);
                Assert.AreEqual(original.X, actual.X, 1e-10); Assert.AreEqual(original.Y, actual.Y, 1e-10);
            }
        }

        [Test]
        public void UnalignedTargetUsesSamePatternAndCorrectHeadingWithoutSnapping()
        {
            var target = P(2, 5);
            foreach (BraidedProjectile projectile in Enum.GetValues(typeof(BraidedProjectile)))
            {
                var node = MartioFlightGeometry.Node(P(0, 8), P(2, -3), 3, projectile);
                Assert.AreEqual(target.X, node.X, 1e-10); Assert.AreEqual(target.Y, node.Y, 1e-10);
            }
            var world = new World(); world.Enemies[target] = "target";
            var volley = MartioAttackLaunch.Create(new MartioPrototypeConfig(), P(0, 8), target, false, false, false);
            volley.Advance(10, world, world.Hits.Add);
            Assert.AreEqual(12, world.Hits.Sum(h => h.Damage)); Assert.AreEqual(3, world.Hits.Count);
        }

        [Test]
        public void ScaledHeadingsDescribeTheSameTrajectory()
        {
            foreach (BraidedProjectile projectile in Enum.GetValues(typeof(BraidedProjectile)))
            for (int step = 0; step <= 7; step++)
            {
                var a = MartioFlightGeometry.Node(P(2, 8), P(2, -3), step, projectile);
                var b = MartioFlightGeometry.Node(P(2, 8), P(4, -6), step, projectile);
                Assert.AreEqual(a.X, b.X, 1e-10); Assert.AreEqual(a.Y, b.Y, 1e-10);
            }
        }

        [Test]
        public void SweptCellsAgreeWithIndependentDenseSamplingAcrossAllBoardHeadings()
        {
            for (int dx = -4; dx <= 4; dx++) for (int dy = -4; dy <= 4; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                foreach (BraidedProjectile projectile in Enum.GetValues(typeof(BraidedProjectile)))
                for (int step = 1; step <= 7; step++)
                {
                    var a = MartioFlightGeometry.Node(P(3, 5), P(dx, dy), step - 1, projectile);
                    var b = MartioFlightGeometry.Node(P(3, 5), P(dx, dy), step, projectile);
                    var sampled = new HashSet<ProjectileGridPoint>();
                    // Offset samples avoid exact corner instants; validate positive-length occupancy.
                    for (int i = 0; i < 601; i++)
                    {
                        double t = (i + .371) / 601;
                        sampled.Add(P((int)Math.Floor(a.X + (b.X - a.X) * t + .5),
                            (int)Math.Floor(a.Y + (b.Y - a.Y) * t + .5)));
                    }
                    var crossed = MartioFlightGeometry.TraceSegment(a, b).Where(c => !c.CornerOnly).Select(c => c.Cell).ToArray();
                    CollectionAssert.AreEquivalent(sampled, crossed, dx + "," + dy + " step " + step + " " + projectile);
                }
            }
        }

        [Test]
        public void EveryUnalignedImpactOccursInTheCellTheProjectileActuallyEnters()
        {
            var world = new World();
            for (int y = -2; y <= 10; y++) for (int x = -2; x <= 10; x++) world.Enemies[P(x, y)] = x + "," + y;
            var volley = new MartioProjectileSimulation(new MartioPrototypeConfig(), P(0, 8), P(2, -3), 7, 4.5,
                false, true, false, MartioRepeatHitPolicy.OncePerEnemy, MartioCornerPolicy.InteriorOnly);
            volley.Advance(10, world, world.Hits.Add);
            foreach (var hit in world.Hits)
            {
                double p = Math.Min(7, hit.Progress + 1e-7); int step = Math.Min(6, (int)Math.Floor(p));
                var a = MartioFlightGeometry.Node(P(0, 8), P(2, -3), step, hit.Projectile);
                var b = MartioFlightGeometry.Node(P(0, 8), P(2, -3), step + 1, hit.Projectile);
                var position = new MartioFlightPoint(a.X + (b.X - a.X) * (p - step), a.Y + (b.Y - a.Y) * (p - step));
                Assert.AreEqual(hit.ImpactCell, MartioFlightGeometry.OccupiedCell(position));
            }
        }

        [Test]
        public void UnalignedSweepsHaveTheSameHitsAtCoarseAndFineTimeSteps()
        {
            var a = new World(); var b = new World();
            for (int y = 0; y <= 8; y++) for (int x = 0; x <= 5; x++)
                if ((x + y) % 2 == 0 && (x != 0 || y != 8)) { a.Enemies[P(x,y)] = x + "," + y; b.Enemies[P(x,y)] = x + "," + y; }
            var c = new MartioPrototypeConfig();
            var large = MartioAttackLaunch.Create(c, P(0,8), P(2,5), true, true, false);
            var small = MartioAttackLaunch.Create(c, P(0,8), P(2,5), true, true, false);
            large.Advance(10,a,a.Hits.Add); for (int i = 0; i < 200; i++) small.Advance(.01,b,b.Hits.Add);
            CollectionAssert.AreEqual(a.Hits.Select(Key), b.Hits.Select(Key));
        }

        [Test]
        public void BoundaryAlignedFlightHasOnlyOneCellOwner()
        {
            var cells = MartioFlightGeometry.TraceSegment(new MartioFlightPoint(.5,0),new MartioFlightPoint(.5,2)).Where(c=>!c.CornerOnly).ToArray();
            Assert.IsTrue(cells.All(c=>c.Cell.X==1)); Assert.AreEqual(3,cells.Length);
        }

        [Test]
        public void InvalidHeadingAndMissingSpeedDoNotInventFallbacks()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>MartioFlightGeometry.Node(P(0,0),P(0,0),1,BraidedProjectile.Center));
            var c=new MartioPrototypeConfig();c.ProjectileForwardStepsPerSecond.IsDefined=false;
            Assert.Throws<InvalidOperationException>(()=>MartioAttackLaunch.Create(c,P(0,0),P(2,3),false,false,false));
        }
    }
}

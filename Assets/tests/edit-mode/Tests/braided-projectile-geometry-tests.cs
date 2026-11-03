using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BraidedProjectileGeometryTests
    {
        static ProjectileGridPoint Point(int x, int y) => new ProjectileGridPoint(x, y);
        static ProjectileGridPoint Node(int step, BraidedProjectile projectile) =>
            BraidedProjectileGeometry.Node(Point(3, 8), Point(0, -1), step, projectile, 1, 1);

        [Test]
        public void ThreeProjectilesMeetAtFirstCellThenAlternateBetweenSpreadAndCenter()
        {
            var center = BraidedProjectile.Center;
            var first = BraidedProjectile.FirstMoon;
            var second = BraidedProjectile.SecondMoon;
            foreach (var projectile in new[] { center, first, second }) Assert.AreEqual(Point(3, 7), Node(1, projectile));
            Assert.AreEqual(Point(3, 6), Node(2, center));
            Assert.AreEqual(Point(4, 6), Node(2, first));
            Assert.AreEqual(Point(2, 6), Node(2, second));
            foreach (var projectile in new[] { center, first, second }) Assert.AreEqual(Point(3, 5), Node(3, projectile));
            Assert.AreEqual(Point(2, 4), Node(4, first));
            Assert.AreEqual(Point(4, 4), Node(4, second));
        }

        [TestCase(-1, -1)] [TestCase(0, -1)] [TestCase(1, -1)] [TestCase(-1, 0)]
        [TestCase(1, 0)] [TestCase(-1, 1)] [TestCase(0, 1)] [TestCase(1, 1)]
        public void OpposingTeamsHaveExactlyRotatedTrajectories(int dx, int dy)
        {
            foreach (BraidedProjectile projectile in Enum.GetValues(typeof(BraidedProjectile)))
            for (int step = 0; step <= 12; step++)
            {
                var blue = BraidedProjectileGeometry.Node(Point(2, 8), Point(dx, dy), step, projectile, 1, 1);
                var red = BraidedProjectileGeometry.Node(Point(3, 1), Point(-dx, -dy), step, projectile, 1, 1);
                Assert.AreEqual(Point(5 - blue.X, 9 - blue.Y), red);
            }
        }

        [Test]
        public void DiagonalMoonActuallyCrossesItsIntermediateHorizontalCell()
        {
            var cells = BraidedProjectileGeometry.StepCrossings(Point(2, 7), Point(1, -1), 2,
                BraidedProjectile.FirstMoon, 1, 1);
            CollectionAssert.AreEqual(new[] { Point(3, 6), Point(4, 6), Point(5, 6) }, cells.Select(c => c.Cell));
            Assert.IsTrue(cells.All(c => !c.CornerOnly));
            var other = BraidedProjectileGeometry.StepCrossings(Point(2, 7), Point(1, -1), 2,
                BraidedProjectile.SecondMoon, 1, 1);
            CollectionAssert.AreEqual(new[] { Point(3, 6), Point(3, 5), Point(3, 4) }, other.Select(c => c.Cell));
        }

        [Test]
        public void CornerContactIsDistinguishedFromEnteringACellInsteadOfChoosingHitRules()
        {
            var cells = BraidedProjectileGeometry.TraceSegment(Point(2, 3), Point(3, 2));
            CollectionAssert.AreEquivalent(new[] { Point(2, 3), Point(3, 2) }, cells.Where(c => !c.CornerOnly).Select(c => c.Cell));
            CollectionAssert.AreEquivalent(new[] { Point(3, 3), Point(2, 2) }, cells.Where(c => c.CornerOnly).Select(c => c.Cell));
        }

        [Test]
        public void ReversedSweepsVisitTheSameCellsWithoutDuplicates()
        {
            for (int x = -5; x <= 5; x++) for (int y = -5; y <= 5; y++)
            {
                var forward = BraidedProjectileGeometry.TraceSegment(Point(0, 0), Point(x, y));
                var backward = BraidedProjectileGeometry.TraceSegment(Point(x, y), Point(0, 0));
                CollectionAssert.AreEquivalent(forward.Where(c => !c.CornerOnly).Select(c => c.Cell), backward.Where(c => !c.CornerOnly).Select(c => c.Cell));
                CollectionAssert.AreEquivalent(forward.Where(c => c.CornerOnly).Select(c => c.Cell), backward.Where(c => c.CornerOnly).Select(c => c.Cell));
                Assert.AreEqual(forward.Count, forward.Select(c => c.Cell).Distinct().Count());
            }
        }

        [Test]
        public void BoardEdgeDoesNotFoldAnOutsideMoonOntoTheCenterProjectile()
        {
            var left = BraidedProjectileGeometry.Node(Point(0, 5), Point(0, -1), 2, BraidedProjectile.SecondMoon, 1, 1);
            Assert.AreEqual(Point(-1, 3), left);
            Assert.AreNotEqual(Point(0, 3), left);
        }

        [Test]
        public void CallerCanChooseMoonIdentityAndAmplitudeWithoutChangingTheCenter()
        {
            var origin = Point(4, 9); var direction = Point(0, -1);
            var first = BraidedProjectileGeometry.Node(origin, direction, 2, BraidedProjectile.FirstMoon, 2, -1);
            var second = BraidedProjectileGeometry.Node(origin, direction, 2, BraidedProjectile.SecondMoon, 2, -1);
            Assert.AreEqual(Point(2, 7), first); Assert.AreEqual(Point(6, 7), second);
            Assert.AreEqual(Point(4, 7), BraidedProjectileGeometry.Node(origin, direction, 2, BraidedProjectile.Center, 2, -1));
        }

        [Test]
        public void InvalidDirectionAndUnboundedInputAreRejected()
        {
            Assert.Throws<ArgumentException>(() => BraidedProjectileGeometry.Node(Point(0, 0), Point(0, 0), 2, BraidedProjectile.Center, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BraidedProjectileGeometry.Node(Point(0, 0), Point(0, 1), -1, BraidedProjectile.Center, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BraidedProjectileGeometry.TraceSegment(Point(int.MinValue, 0), Point(int.MaxValue, 0)));
        }
    }
}

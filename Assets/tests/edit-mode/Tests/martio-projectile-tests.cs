using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class MartioProjectileTests
    {
        sealed class World : IMartioOccupancy
        {
            public readonly Dictionary<ProjectileGridPoint, string> Occupants = new Dictionary<ProjectileGridPoint, string>();
            public readonly List<MartioDamageEvent> Events = new List<MartioDamageEvent>();
            public string EnemyOccupying(ProjectileGridPoint cell) => Occupants.TryGetValue(cell, out var id) ? id : null;
            public void Put(int x, int y, string id) { Occupants[P(x, y)] = id; }
            public void Hit(MartioDamageEvent hit) { Events.Add(hit); }
            public int Damage(string id) => Events.Where(e => e.EnemyId == id).Sum(e => e.Damage);
        }
        static ProjectileGridPoint P(int x, int y) => new ProjectileGridPoint(x, y);
        static MartioProjectileSimulation Volley(bool explode = false, bool pierce = false, int range = 5,
            MartioPrototypeConfig config = null, bool upgraded = false, bool diagonal = false,
            MartioRepeatHitPolicy repeats = MartioRepeatHitPolicy.OncePerEnemy) =>
            new MartioProjectileSimulation(config ?? new MartioPrototypeConfig(), P(2, 8), diagonal ? P(1, -1) : P(0, -1),
                range, 1, explode, pierce, upgraded, repeats, MartioCornerPolicy.InteriorOnly);

        [Test]
        public void AllThreeHitSameRealOccupantForTwelveAndStopIndependently()
        {
            var world = new World(); world.Put(2, 7, "enemy");
            var volley = Volley(); volley.Advance(1, world, world.Hit);
            Assert.AreEqual(12, world.Damage("enemy")); Assert.AreEqual(3, world.Events.Count);
            CollectionAssert.AreEqual(new[] { 6, 3, 3 }, world.Events.Select(e => e.Damage));
            Assert.IsTrue(volley.Complete);
            volley.Advance(10, world, world.Hit); Assert.AreEqual(3, world.Events.Count);
        }

        [Test]
        public void EmptyLaunchCellIsNotACapturedTargetAndLateOccupantIsHit()
        {
            var world = new World(); world.Put(2, 7, "original"); var volley = Volley();
            volley.Advance(.2, world, world.Hit);
            world.Occupants.Clear(); world.Put(2, 7, "replacement");
            volley.Advance(.8, world, world.Hit);
            Assert.AreEqual(0, world.Damage("original")); Assert.AreEqual(12, world.Damage("replacement"));
        }

        [Test]
        public void EachMoonHitsItsOwnLateralCellWhileMarsKeepsGoing()
        {
            var world = new World(); world.Put(3, 6, "right"); world.Put(1, 6, "left"); world.Put(2, 4, "center");
            var volley = Volley(); volley.Advance(5, world, world.Hit);
            Assert.AreEqual(3, world.Damage("right")); Assert.AreEqual(3, world.Damage("left")); Assert.AreEqual(6, world.Damage("center"));
            Assert.AreEqual(BraidedProjectile.FirstMoon, world.Events.Single(e => e.EnemyId == "right").Projectile);
        }

        [Test]
        public void DiagonalMoonHitsIntermediateCellBeforeItsNextNode()
        {
            var world = new World(); world.Put(4, 7, "intermediate"); world.Put(5, 7, "node");
            var volley = Volley(diagonal: true); volley.Advance(2, world, world.Hit);
            Assert.AreEqual(3, world.Damage("intermediate")); Assert.AreEqual(0, world.Damage("node"));
            Assert.AreEqual(1.25, world.Events.Single().Progress, 1e-9);
            Assert.AreEqual(3.5, volley.Position(BraidedProjectile.FirstMoon).X, 1e-9);
        }

        [Test]
        public void ExplosionNeverAddsDamageToItsImpactCellAndOnlyCoversEightNeighbors()
        {
            var world = new World(); world.Put(2, 7, "center");
            for (int y = 6; y <= 8; y++) for (int x = 1; x <= 3; x++)
                if (x != 2 || y != 7) world.Put(x, y, "n" + x + y);
            // No enemy in launcher's cell in a real board.
            world.Occupants.Remove(P(2, 8)); world.Put(4, 7, "outside");
            var volley = Volley(explode: true); volley.Advance(1, world, world.Hit);
            Assert.AreEqual(12, world.Damage("center")); Assert.AreEqual(0, world.Damage("outside"));
            Assert.AreEqual(21, world.Events.Count(e => e.Kind == MartioDamageKind.AdjacentExplosion));
            Assert.IsTrue(world.Events.Where(e => e.Kind == MartioDamageKind.AdjacentExplosion).All(e => !e.RecipientCell.Equals(e.ImpactCell)));
            Assert.AreEqual(12, world.Damage("n16"));
        }

        [Test]
        public void ExplosionUsesIndependentFieldsAfterImpactAndUpgradeChanges()
        {
            var c = new MartioPrototypeConfig { MarsImpactDamage = 19, FirstMoonImpactDamage = 8,
                SecondMoonImpactDamage = 11, FirstMoonUpgradedDamage = 7, SecondMoonUpgradedDamage = 9,
                MarsExplosionDamage = 2, FirstMoonExplosionDamage = 4, SecondMoonExplosionDamage = 5 };
            var world = new World(); world.Put(2, 7, "center"); world.Put(1, 7, "neighbor");
            var volley = Volley(explode: true, config: c, upgraded: true); volley.Advance(1, world, world.Hit);
            Assert.AreEqual(38, world.Damage("center")); Assert.AreEqual(11, world.Damage("neighbor"));
            Assert.AreEqual(19, c.MarsImpactDamage); Assert.AreEqual(8, c.FirstMoonImpactDamage);
        }

        [Test]
        public void DamageSnapshotDoesNotChangeAnAlreadyLaunchedVolley()
        {
            var c = new MartioPrototypeConfig(); var world = new World(); world.Put(2, 7, "enemy");
            var volley = Volley(config: c); c.MarsImpactDamage = 100;
            volley.Advance(1, world, world.Hit); Assert.AreEqual(12, world.Damage("enemy"));
        }

        [Test]
        public void PiercingHitsAgainAndExplodesAtEachImpactButEndsAtFiniteRange()
        {
            var world = new World(); world.Put(2, 7, "a"); world.Put(2, 5, "b");
            world.Put(3, 7, "splash-a"); world.Put(3, 5, "splash-b"); world.Put(2, 3, "beyond-range");
            var volley = Volley(explode: true, pierce: true, range: 3); volley.Advance(99, world, world.Hit);
            Assert.IsTrue(volley.Complete); Assert.AreEqual(3, volley.Progress);
            Assert.Greater(world.Damage("a"), 0); Assert.Greater(world.Damage("b"), 0);
            Assert.Greater(world.Damage("splash-a"), 0); Assert.Greater(world.Damage("splash-b"), 0);
            Assert.AreEqual(0, world.Damage("beyond-range"));
            Assert.GreaterOrEqual(world.Events.Count(e => e.Kind == MartioDamageKind.Impact), 6);
        }

        [Test]
        public void SlowProjectileDoesNotDamageEveryFrameWhileContactPersists()
        {
            var world = new World(); world.Put(2, 7, "enemy"); var volley = Volley(pierce: true);
            for (int i = 0; i < 100; i++) volley.Advance(.01, world, world.Hit);
            Assert.AreEqual(12, world.Damage("enemy")); Assert.AreEqual(3, world.Events.Count);
        }

        [Test]
        public void LeavingAndReenteringNeverRepeatsDirectDamage()
        {
            var world = new World(); world.Put(2, 7, "enemy"); var volley = Volley(pierce: true);
            volley.Advance(.6, world, world.Hit); world.Occupants.Clear();
            volley.Advance(.1, world, world.Hit); world.Put(2, 7, "enemy");
            volley.Advance(.1, world, world.Hit); Assert.AreEqual(12, world.Damage("enemy"));
        }

        [Test]
        public void ClusteredEnemiesCanReceiveDirectDamageAndMultipleNeighborExplosions()
        {
            var world = new World(); world.Put(2, 7, "front"); world.Put(2, 6, "behind"); world.Put(3, 6, "beside");
            var volley = Volley(explode: true, pierce: true); volley.Advance(2, world, world.Hit);
            var front = world.Events.Where(e => e.EnemyId == "front").ToArray();
            Assert.AreEqual(12, front.Where(e => e.Kind == MartioDamageKind.Impact).Sum(e => e.Damage));
            Assert.AreEqual(9, front.Where(e => e.Kind == MartioDamageKind.AdjacentExplosion).Sum(e => e.Damage));
            Assert.AreEqual(2, front.Count(e => e.Kind == MartioDamageKind.AdjacentExplosion));
            Assert.IsTrue(front.Where(e => e.Kind == MartioDamageKind.AdjacentExplosion).All(e => !e.RecipientCell.Equals(e.ImpactCell)));
        }

        [Test]
        public void SameEnemyFollowingPiercingProjectileIntoAnotherCellIsNotHitAgain()
        {
            var world = new World(); world.Put(2, 7, "traveler"); var volley = Volley(explode: true, pierce: true);
            volley.Advance(1, world, world.Hit); world.Occupants.Clear(); world.Put(2, 6, "traveler");
            volley.Advance(1, world, world.Hit); world.Occupants.Clear(); world.Put(2, 5, "traveler");
            volley.Advance(1, world, world.Hit);
            Assert.AreEqual(12, world.Damage("traveler")); Assert.AreEqual(3, world.Events.Count);
        }

        [Test]
        public void NewOccupantEnteringASlowProjectileCellCanBeHit()
        {
            var world = new World(); var volley = Volley(pierce: true);
            volley.Advance(.6, world, world.Hit); world.Put(2, 7, "newcomer");
            volley.Advance(.1, world, world.Hit); Assert.AreEqual(12, world.Damage("newcomer"));
        }

        [Test]
        public void CoarseAndFineAdvancementGiveSameOrderedHitsOnStaticBoard()
        {
            var large = new World(); var small = new World();
            foreach (var world in new[] { large, small })
            { world.Put(2, 7, "a"); world.Put(3, 6, "b"); world.Put(1, 4, "c"); world.Put(2, 3, "d"); }
            var a = Volley(explode: true, pierce: true); var b = Volley(explode: true, pierce: true);
            a.Advance(100, large, large.Hit); for (int i = 0; i < 500; i++) b.Advance(.01, small, small.Hit);
            CollectionAssert.AreEqual(large.Events.Select(Key), small.Events.Select(Key));
        }
        static string Key(MartioDamageEvent e) => e.Projectile + ":" + e.Kind + ":" + e.EnemyId + ":" + e.Damage;

        [Test]
        public void DeathInImpactCallbackChangesNextProjectileOccupancyImmediately()
        {
            var world = new World(); world.Put(2, 7, "fragile"); var volley = Volley();
            volley.Advance(1, world, e => { world.Hit(e); world.Occupants.Remove(e.RecipientCell); });
            Assert.AreEqual(6, world.Damage("fragile")); Assert.AreEqual(1, world.Events.Count);
            Assert.IsFalse(volley.IsActive(BraidedProjectile.Center)); Assert.IsTrue(volley.IsActive(BraidedProjectile.FirstMoon));
        }

        [Test]
        public void ApprovedStatsAndRepeatRuleAreExplicitWhileMissingRulesFailClosed()
        {
            var c = new MartioPrototypeConfig();
            Assert.IsTrue(c.Health.IsDefined); Assert.AreEqual(40, c.Health.Value);
            Assert.IsTrue(c.AttackRange.IsDefined); Assert.AreEqual(4, c.AttackRange.Value);
            Assert.IsTrue(c.ProjectileTravelRange.IsDefined); Assert.AreEqual(7, c.ProjectileTravelRange.Value);
            Assert.IsTrue(c.BaseCost.IsDefined); Assert.AreEqual(3, c.BaseCost.Value);
            Assert.IsTrue(c.AttackIntervalSeconds.IsDefined); Assert.AreEqual(1.3f, c.AttackIntervalSeconds.Value);
            Assert.IsTrue(c.ProjectileForwardStepsPerSecond.IsDefined); Assert.AreEqual(4.5f, c.ProjectileForwardStepsPerSecond.Value);
            Assert.AreEqual(MartioRepeatHitPolicy.OncePerEnemy, c.RepeatHits); Assert.AreEqual(MartioCornerPolicy.InteriorOnly, c.CornerContacts);
            Assert.Throws<InvalidOperationException>(() => new MartioProjectileSimulation(c, P(0, 0), P(0, 1), 3, 1, false, false, false, c.RepeatHits, MartioCornerPolicy.Pending));
            Assert.Throws<InvalidOperationException>(() => Volley(pierce: true, repeats: MartioRepeatHitPolicy.Pending));
            Assert.DoesNotThrow(() => Volley(repeats: MartioRepeatHitPolicy.Pending));
        }

        [Test]
        public void CornerOnlyContactRequiresExplicitQAChoice()
        {
            foreach (var policy in new[] { MartioCornerPolicy.InteriorOnly, MartioCornerPolicy.IncludeCornerTouches })
            {
                var world = new World(); world.Put(1, 0, "corner");
                var volley = new MartioProjectileSimulation(new MartioPrototypeConfig(), P(0, 0), P(1, 1), 1, 1,
                    false, false, false, MartioRepeatHitPolicy.Pending, policy);
                volley.Advance(1, world, world.Hit);
                Assert.AreEqual(policy == MartioCornerPolicy.InteriorOnly ? 0 : 12, world.Damage("corner"));
            }
        }

        [Test]
        public void InvalidInputsFailBeforeSimulationAndZeroTimeDoesNotHit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Volley(range: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Volley(config: new MartioPrototypeConfig { MarsExplosionDamage = -1 }));
            var world = new World(); world.Put(2, 7, "enemy"); var volley = Volley();
            Assert.Throws<ArgumentOutOfRangeException>(() => volley.Advance(double.NaN, world, world.Hit));
            volley.Advance(0, world, world.Hit); Assert.IsEmpty(world.Events);
        }

        [Test]
        public void CardsReflectIndependentValuesWithoutFlightSpeedOrColorTags()
        {
            var c = new MartioPrototypeConfig { MarsExplosionDamage = 11, LateralTravelRange = 4, FanAngleDegrees = 25 };
            StringAssert.Contains("11", MartioAbilityText.Explosion(c));
            StringAssert.Contains("±25°", MartioAbilityText.MoonUpgrade(c));
            StringAssert.Contains("4 casillas", MartioAbilityText.MoonUpgrade(c));
            string all = MartioAbilityText.Basic(c) + MartioAbilityText.Explosion(c) + MartioAbilityText.Piercing + MartioAbilityText.MoonUpgrade(c);
            StringAssert.DoesNotContain("velocidad", all); StringAssert.DoesNotContain("<color", all);
        }
    }
}

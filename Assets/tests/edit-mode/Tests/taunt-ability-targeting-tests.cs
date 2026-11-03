using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TauntAbilityTargetingTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;
        private CombatSimulation simulation;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("taunt-ability-board");
            objects.Add(go);
            board = go.AddComponent<BoardManager>();
            board.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PopowUsesTaunterInsteadOfFarthestEnemyOnlyWhileTaunted(bool taunted)
        {
            var popow = Unit(Tune(DocumentedRoster.CreatePopow()), "10-popow", 2, 7);
            var bugaloo = Taunter(2, 5);
            var farthest = Target("30-farthest", 5, 0);
            Begin(popow, bugaloo, farthest);
            BoardCell through = null;
            simulation.DashStarted += (actor, _, target, __, ___, ____) => { if (actor == popow) through = target; };
            Charge(popow);
            if (taunted) Charge(bugaloo);
            simulation.Step(.1f);

            Assert.AreEqual(taunted, popow.IsTaunted);
            Assert.AreSame(taunted ? bugaloo.CurrentCell : farthest.CurrentCell, through);
        }

        [Test]
        public void TauntOverridesTheLowestHealthBasicTargetPolicy()
        {
            var atori = Unit(Tune(DocumentedRoster.CreateAtori()), "10-atori", 2, 5);
            var bugaloo = Taunter(2, 4);
            var weakest = Target("20-weakest", 3, 5);
            Begin(atori, bugaloo, weakest);
            weakest.ApplyDamage(900);
            BattleUnit target = null;
            simulation.Attacked += (actor, victim, _) => { if (actor == atori) target = victim; };
            Charge(bugaloo);
            simulation.Step(.1f);

            Assert.IsTrue(atori.IsTaunted);
            Assert.AreSame(bugaloo, target);
        }

        [Test]
        public void SteinSuperIncludesTheTaunterAndOtherDistinctEnemies()
        {
            var stein = Unit(Tune(DocumentedRoster.CreateStein()), "10-stein", 2, 5);
            var bugaloo = Taunter(2, 4);
            var other = Target("20-other", 3, 5);
            Begin(stein, bugaloo, other);
            var targets = new List<BattleUnit>();
            simulation.Impacted += (actor, victim, _) => { if (actor == stein) targets.Add(victim); };
            Charge(bugaloo);Charge(stein);
            simulation.Step(.1f);

            Assert.IsTrue(stein.IsTaunted);Assert.IsEmpty(targets);
            simulation.Step(.8f);
            CollectionAssert.AreEqual(new[] { bugaloo, other }, targets);
            Assert.AreEqual(980, bugaloo.CurrentHealth);
            Assert.AreEqual(980, other.CurrentHealth);
        }

        [Test]
        public void AreaSuperCentersOnTaunterAndKeepsItsSplashDamage()
        {
            var gochan = Unit(Tune(DocumentedRoster.CreateGochan()), "10-gochan", 2, 6);
            var bugaloo = Taunter(2, 4);
            var splash = Target("20-splash", 3, 4);
            Begin(gochan, bugaloo, splash);
            BattleUnit primary = null;
            simulation.Attacked += (actor, victim, _) => { if (actor == gochan) primary = victim; };
            Charge(bugaloo);
            Charge(gochan);
            simulation.Step(.8f);

            Assert.AreSame(bugaloo, primary);
            Assert.AreEqual(980, bugaloo.CurrentHealth);
            Assert.AreEqual(980, splash.CurrentHealth);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparingSuperRetriesTaunterWithoutSpendingAnotherEnergyBar(bool previousTargetDies)
        {
            var definition = Tune(DocumentedRoster.CreateTauris());
            definition.AttackWindup = .6f;
            definition.MoveInterval = .1f;
            var tauris = Unit(definition, "10-tauris", 2, 5);
            var previous = Target("20-previous", 1, 5);
            var bugaloo = Taunter(4, 5);
            Begin(tauris, previous, bugaloo);
            Charge(tauris);
            simulation.Step(.1f);
            Assert.AreEqual(0, tauris.Energy);
            Charge(bugaloo);
            simulation.Step(.2f);

            Assert.IsTrue(tauris.IsTaunted);
            Assert.AreEqual(tauris.BaseStats.EnergyMax, tauris.Energy);
            Assert.AreEqual(1000, previous.CurrentHealth);
            Assert.AreEqual(1000, bugaloo.CurrentHealth, "Taunt cannot let a melee Super hit from two cells away.");
            if (previousTargetDies) previous.ApplyDamage(10000);
            Until(() => !bugaloo.IsAlive);
            Assert.IsFalse(bugaloo.IsAlive, "The cancelled Super must remain available after reacquiring the taunter.");
            Assert.AreEqual(0, tauris.Energy, "The retry consumes exactly the refunded energy bar.");
            if (!previousTargetDies) Assert.AreEqual(1000, previous.CurrentHealth);
        }

        [Test]
        public void AlreadyReleasedProjectileKeepsItsCapturedTarget()
        {
            var definition = Tune(DocumentedRoster.CreateAnuik());
            definition.ProjectileSpeed = .5f;
            var anuik = Unit(definition, "10-anuik", 2, 5);
            var previous = Target("20-previous", 1, 5);
            var bugaloo = Taunter(4, 5);
            Begin(anuik, previous, bugaloo);
            simulation.Step(.2f);
            Charge(bugaloo);
            simulation.Step(.1f);
            Assert.IsTrue(anuik.IsTaunted);
            simulation.Step(2f);

            Assert.AreEqual(980, previous.CurrentHealth);
            Assert.AreEqual(1000, bugaloo.CurrentHealth);
        }

        [Test]
        public void TauntedFloStillCompletesItsUntargetedFlowerSummon()
        {
            var flo = Unit(Tune(DocumentedRoster.CreateFlo(), 0), "10-flo", 2, 5);
            var bugaloo = Taunter(2, 4);
            Begin(flo, bugaloo);
            Charge(bugaloo);
            Charge(flo);
            simulation.Step(.7f);

            Assert.IsTrue(flo.IsTaunted);
            Assert.AreEqual(1, simulation.CombatOnlyUnits.Count);
            Assert.AreEqual("flower", simulation.CombatOnlyUnits[0].BaseStats.DefinitionId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BounceLeavesFirstVictimAndReachesAnotherEnemyThreeCellsFromThatVictim(bool lethalFirstHit)
        {
            var anuik = Unit(Tune(DocumentedRoster.CreateAnuik()), "10-anuik", 2, 6, BoardSide.Blue, 2);
            var first = Target("20-first", 2, 5);
            var next = Target("30-next", 5, 2);
            Begin(anuik, first, next);
            if (lethalFirstHit) first.ApplyDamage(990);
            int attacks = 0, bounces = 0, bounceAbilities = 0;
            BattleUnit origin = null, destination = null;
            float travel = 0;
            simulation.Attacked += (actor, _, __) => { if (actor == anuik) attacks++; };
            simulation.AbilityUsed += (actor, effect) => { if (actor == anuik && effect == "anuik-bounce") bounceAbilities++; };
            simulation.ProjectileBounced += (actor, from, to, duration) =>
            {
                Assert.AreSame(anuik, actor);
                origin = from; destination = to; travel = duration; bounces++;
            };
            simulation.Step(.5f);

            Assert.AreSame(first, origin);
            Assert.AreSame(next, destination, "The bounce radius is measured from the first victim, even beyond Anuik's own range.");
            Assert.AreEqual(.1f, travel, .0001f);
            Assert.AreEqual(990, next.CurrentHealth);
            Assert.AreEqual(1, attacks, "A bounce cannot start another Anuik attack animation.");
            Assert.AreEqual(1, bounces);
            Assert.AreEqual(0, bounceAbilities, "The bounce event must not trigger a Super animation.");
        }

        [Test]
        public void TauntDuringAnOlderBasicDoesNotCancelOrDelayFloAlreadyPlanting()
        {
            var definition = Tune(DocumentedRoster.CreateFlo(), 0);
            definition.AttackWindup = .6f;
            var flo = Unit(definition, "10-flo", 2, 5);
            var previous = Target("20-previous", 1, 5);
            var bugaloo = Taunter(4, 5);
            Begin(flo, previous, bugaloo);
            simulation.Step(.1f);
            Charge(flo);
            simulation.Step(.1f);
            Charge(bugaloo);
            simulation.Step(.1f);

            Assert.IsTrue(flo.IsTaunted);
            Assert.AreEqual(0, flo.Energy);
            simulation.Step(.3f);
            Assert.AreEqual(0, simulation.CombatOnlyUnits.Count);
            simulation.Step(.1f);
            Assert.AreEqual(1, simulation.CombatOnlyUnits.Count, "Planting must finish at its original 0.7 s deadline.");
        }

        [TestCase(3f, false)]
        [TestCase(4f, true)]
        public void BounceRangeRejectsDistantEnemiesAndHonorsEditedRange(float range, bool reaches)
        {
            var definition = Tune(DocumentedRoster.CreateAnuik());
            definition.Tricks[2].Parameters = new[] { new AbilityParameter { Key = "bounceRange", Value = range } };
            var anuik = Unit(definition, "10-anuik", 2, 6, BoardSide.Blue, 2);
            var first = Target("20-first", 2, 5);
            var distant = Target("30-distant", 2, 1);
            Begin(anuik, first, distant);
            int bounces = 0;
            simulation.ProjectileBounced += (_, __, ___, ____) => bounces++;
            simulation.Step(.5f);

            Assert.AreEqual(reaches ? 1 : 0, bounces);
            Assert.AreEqual(reaches ? 990 : 1000, distant.CurrentHealth);
        }

        private static UnitDefinition Tune(UnitDefinition definition, int damage = 20)
        {
            definition.MaxHealth = 1000;
            definition.Damage = damage;
            definition.AttackInterval = 100;
            definition.AttackWindup = .1f;
            definition.AttackHold=definition.AttackRecovery=0; // Targeting fixtures isolate casts from basic return phases.
            definition.ProjectileSpeed = 100;
            definition.MoveInterval = 100;
            definition.EnergyPerAttack = definition.EnergyOnDamage = definition.EnergyPerSecond = 0;
            return definition;
        }

        private BattleUnit Taunter(int x, int y)
        {
            var definition = Tune(DocumentedRoster.CreateBugaloo(), 0);
            definition.AttackRange = 9;
            definition.AttackInterval = .1f;
            return Unit(definition, "00-bugaloo", x, y, BoardSide.Red);
        }

        private BattleUnit Target(string id, int x, int y) => Unit(Tune(new UnitDefinition
        { Id = "target", AttackRange = 9, UsesDocumentedRules = true }, 0), id, x, y, BoardSide.Red);

        private BattleUnit Unit(UnitDefinition definition, string id, int x, int y, BoardSide side = BoardSide.Blue, params int[] tricks)
        {
            var owned = new OwnedUnit(definition);
            foreach (int trick in tricks) owned.Tricks[trick] = true;
            var go = new GameObject(id);
            objects.Add(go);
            BattleUnit unit = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }

        private void Begin(params BattleUnit[] units)
        {
            simulation = new CombatSimulation(board);
            simulation.Begin(units);
        }

        private static void Charge(BattleUnit unit) => unit.AddEnergy(unit.BaseStats.EnergyMax);

        private void Until(Func<bool> predicate)
        {
            for (int i = 0; i < 60 && !predicate() && !simulation.Finished; i++) simulation.Step(.1f);
            Assert.IsTrue(predicate(), "Expected combat event did not resolve within six seconds.");
        }
    }
}

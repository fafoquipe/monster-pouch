using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CombatStatusFeedbackTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;
        private CombatSimulation simulation;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("status-feedback-board");
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

        private BattleUnit Unit(UnitDefinition definition, int x = 2, int y = 5,
            BoardSide side = BoardSide.Blue, params int[] tricks)
        {
            var owned = new OwnedUnit(definition);
            foreach (int index in tricks) owned.Tricks[index] = true;
            return RawUnit(UnitStats.FromDefinition(definition, owned), x, y, side);
        }

        private BattleUnit RawUnit(UnitStats stats, int x = 2, int y = 5,
            BoardSide side = BoardSide.Blue)
        {
            var go = new GameObject("feedback-unit-" + objects.Count);
            objects.Add(go);
            var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, stats);
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }

        private BattleUnit Target(int x = 2, int y = 4)
        {
            return RawUnit(new UnitStats(1000, 0, range: 9,
                attackInterval: 100, moveInterval: 100), x, y, BoardSide.Red);
        }

        private void Begin(params BattleUnit[] units)
        {
            simulation = new CombatSimulation(board);
            simulation.Begin(units);
        }

        private void Tick(int count = 1)
        {
            for (int i = 0; i < count; i++) simulation.Step(CombatSimulation.TickDuration);
        }

        private void Until(Func<bool> condition, string expectation, int maximumTicks = 30)
        {
            for (int i = 0; i < maximumTicks && !condition() && !simulation.Finished; i++) Tick();
            Assert.IsTrue(condition(), expectation);
        }

        [TestCase(30, 10)]
        [TestCase(3, 3)]
        [TestCase(0, 0)]
        public void DocumentedHealing_ReportsOnlyHealthActuallyRestored(int missingHealth, int expected)
        {
            var definition = DocumentedRoster.CreateAky();
            definition.Damage = 0;
            definition.AttackWindup = .1f;
            definition.AttackInterval = 100;
            var aky = Unit(definition, 2, 5, BoardSide.Blue, 1);
            var energyTarget = Unit(new UnitDefinition
            {
                Id = "energy-target", MaxHealth = 1000, Damage = 0,
                AttackRange = 1, AttackInterval = 100, AttackWindup = .1f,
                MoveInterval = 100, EnergyMax = 100
            }, 2, 4, BoardSide.Red);
            Begin(aky, energyTarget);
            aky.ApplyDamage(missingHealth);
            energyTarget.AddEnergy(20);
            var healedUnits = new List<BattleUnit>();
            var amounts = new List<int>();
            simulation.Healed += (unit, amount) => { healedUnits.Add(unit); amounts.Add(amount); };
            bool impacted = false;
            simulation.Impacted += (source, target, damage) => { if (source == aky) impacted = true; };

            Until(() => impacted, "Aky must resolve the energy-draining hit.");

            Assert.AreEqual(0, energyTarget.Energy);
            Assert.AreEqual(aky.BaseStats.MaxHealth - missingHealth + expected, aky.CurrentHealth);
            CollectionAssert.AreEqual(expected > 0 ? new[] { expected } : new int[0], amounts);
            CollectionAssert.AreEqual(expected > 0 ? new[] { aky } : new BattleUnit[0], healedUnits);
        }

        [TestCase(3, 3)]
        [TestCase(0, 0)]
        public void GenericHealing_ClampsFeedbackAndSuppressesFullHealth(int missingHealth, int expected)
        {
            var healer = RawUnit(new UnitStats(100, 1, attackInterval: 100, healOnHit: 10));
            var enemy = Target();
            Begin(healer, enemy);
            healer.ApplyDamage(missingHealth);
            var amounts = new List<int>();
            simulation.Healed += (unit, amount) =>
            {
                Assert.AreSame(healer, unit);
                amounts.Add(amount);
            };
            bool impacted = false;
            simulation.Impacted += (source, target, damage) => { if (source == healer) impacted = true; };

            Until(() => impacted, "The generic life-stealing attack must resolve.");

            Assert.AreEqual(healer.BaseStats.MaxHealth, healer.CurrentHealth);
            CollectionAssert.AreEqual(expected > 0 ? new[] { expected } : new int[0], amounts);
        }

        [Test]
        public void Atong_OnlyResolvedSecondAttackEmitsCriticalFeedback()
        {
            var definition = DocumentedRoster.CreateAtong();
            definition.Damage = 10;
            definition.AttackInterval = .3f;
            definition.AttackWindup = .1f;
            var atong = Unit(definition);
            var enemy = Target();
            Begin(atong, enemy);
            var hits = new List<int>();
            var criticalTargets = new List<BattleUnit>();
            simulation.Impacted += (source, target, damage) => { if (source == atong) hits.Add(damage); };
            simulation.CriticalImpacted += (source, target) =>
            {
                Assert.AreSame(atong, source);
                criticalTargets.Add(target);
            };

            Until(() => hits.Count == 1, "Atong's first normal attack must resolve.");
            Assert.AreEqual(10, hits[0]);
            Assert.IsEmpty(criticalTargets);
            Until(() => hits.Count == 2, "Atong's second critical attack must resolve.");

            CollectionAssert.AreEqual(new[] { 10, 15 }, hits);
            CollectionAssert.AreEqual(new[] { enemy }, criticalTargets);
        }

        [Test]
        public void EmpoweredNonCriticalAttack_DoesNotEmitCriticalFeedback()
        {
            var definition = DocumentedRoster.CreateTokoro();
            definition.Damage = 10;
            definition.AttackWindup = .1f;
            definition.AttackInterval = 100;
            var tokoro = Unit(definition);
            var enemy = Target();
            Begin(tokoro, enemy);
            tokoro.AddEnergy(100);
            int damageDealt = 0, criticalCount = 0;
            simulation.Impacted += (source, target, damage) => { if (source == tokoro) damageDealt += damage; };
            simulation.CriticalImpacted += (source, target) => criticalCount++;

            Until(() => damageDealt > 0, "Tokoro's charged headbutt must resolve.");

            Assert.Greater(damageDealt, tokoro.BaseStats.Attack);
            Assert.AreEqual(0, criticalCount, "Higher damage alone must never classify a hit as critical.");
        }

        [Test]
        public void InterruptedCriticalWindup_EmitsAttackResetButNoCriticalFeedback()
        {
            var atongDefinition = DocumentedRoster.CreateAtong();
            atongDefinition.AttackWindup = 1;
            atongDefinition.AttackInterval = 100;
            var atong = Unit(atongDefinition, 2, 5, BoardSide.Blue, 1);
            var steinDefinition = DocumentedRoster.CreateStein();
            steinDefinition.Damage = 0;
            steinDefinition.MaxHealth = 1000;
            steinDefinition.AttackWindup = .1f;
            steinDefinition.AttackInterval = 100;
            steinDefinition.ProjectileSpeed = 100;
            steinDefinition.BaseAbility.Parameters = new[]
            {
                new AbilityParameter { Key = "duration", Value = 2 },
                new AbilityParameter { Key = "targets", Value = 1 }
            };
            var stein = Unit(steinDefinition, 2, 4, BoardSide.Red);
            Begin(atong, stein);
            int attacks = 0, impacts = 0, resets = 0, criticalCount = 0;
            simulation.Attacked += (source, target, projectile) => { if (source == atong) attacks++; };
            simulation.Impacted += (source, target, damage) => { if (source == atong) impacts++; };
            simulation.AttackReset += unit => { if (unit == atong) resets++; };
            simulation.CriticalImpacted += (source, target) => criticalCount++;

            Until(() => atong.IsStunned, "Stein must interrupt Atong before his long windup finishes.");
            Assert.AreEqual(1, attacks);
            Assert.AreEqual(1, resets);
            Tick(12); // Past the cancelled one-second windup, still inside the two-second stun.

            Assert.IsTrue(atong.IsStunned);
            Assert.AreEqual(0, impacts);
            Assert.AreEqual(0, criticalCount);
            Assert.AreEqual(stein.BaseStats.MaxHealth, stein.CurrentHealth);
        }

        [Test]
        public void CombatReset_EmitsOnceWhenSimulationObservesTheReset()
        {
            var actor = RawUnit(new UnitStats(100, 1, attackInterval: 100, attackWindup: 1));
            var enemy = Target();
            simulation = new CombatSimulation(board);
            var resets = new List<BattleUnit>();
            simulation.AttackReset += resets.Add;
            simulation.Begin(new[] { actor, enemy });
            Tick();
            Assert.IsEmpty(resets, "Combat initialization is not an in-combat interruption.");

            actor.ResetForCombat();
            Tick();
            CollectionAssert.AreEqual(new[] { actor }, resets);
            Tick(5);
            CollectionAssert.AreEqual(new[] { actor }, resets, "A single reset must not emit on subsequent ticks.");
        }

        [Test]
        public void JazarRadiation_SetsSlowedAndCombatResetClearsIt()
        {
            var definition = DocumentedRoster.CreateJazar();
            definition.Damage = 0;
            definition.AttackWindup = .1f;
            definition.AttackInterval = 100;
            definition.ProjectileSpeed = 100;
            var jazar = Unit(definition);
            var enemy = Target();
            Begin(jazar, enemy);
            Assert.IsFalse(enemy.IsSlowed);

            Until(() => enemy.IsSlowed, "Jazar's radiation must publish the slowed state.");
            enemy.ResetForCombat();
            Assert.IsFalse(enemy.IsSlowed);
            Tick(5);

            Assert.IsFalse(enemy.IsSlowed, "The old combat clock must not restore a cleared slow.");
            Assert.IsFalse(jazar.IsSlowed);
        }

        [TestCase(25, 20)]
        [TestCase(0, 0)]
        public void FlowerDeath_EmitsOneAreaAtItsCellAndOnlyActualHealing(int missingHealth, int expected)
        {
            var flo = Unit(DocumentedRoster.CreateFlo(), 2, 7, BoardSide.Blue, 1);
            var ally = RawUnit(new UnitStats(100, 0, range: 9, attackInterval: 100), 1, 6);
            var enemy = Target(5, 0);
            Begin(flo, ally, enemy);
            var areas = new List<BoardCell>();
            var amounts = new List<int>();
            simulation.HealingArea += areas.Add;
            simulation.Healed += (unit, amount) =>
            {
                Assert.AreSame(ally, unit, "Full-health Flo must not emit a healing event.");
                amounts.Add(amount);
            };
            Until(() => simulation.CombatOnlyUnits.Count == 1, "Flo must summon his charged flower.");
            BattleUnit flower = simulation.CombatOnlyUnits[0];
            BoardCell flowerCell = flower.CurrentCell;
            ally.ApplyDamage(missingHealth);
            flower.ApplyDamage(flower.CurrentHealth);

            Until(() => areas.Count > 0, "A defeated flower must emit its healing area.");
            Tick(3);

            CollectionAssert.AreEqual(new[] { flowerCell }, areas);
            CollectionAssert.AreEqual(expected > 0 ? new[] { expected } : new int[0], amounts);
            Assert.AreEqual(ally.BaseStats.MaxHealth - missingHealth + expected, ally.CurrentHealth);
            Assert.IsNull(flower.CurrentCell, "The event must preserve the cell even after death releases it.");
        }
    }
}

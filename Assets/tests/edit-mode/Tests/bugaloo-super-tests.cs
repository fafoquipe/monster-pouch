using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BugalooSuperTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation simulation;

        [SetUp] public void SetUp()
        {
            var go = new GameObject("bugaloo-super-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void TearDown()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }
        BattleUnit Unit(UnitDefinition definition, BoardSide side, int x = 2, bool protection = false)
        {
            var owned = new OwnedUnit(definition); owned.Tricks[0] = protection;
            var go = new GameObject(side + "-" + definition.Id + objects.Count); objects.Add(go);
            BattleUnit unit = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, side == BoardSide.Blue ? 5 : 4));
            return unit;
        }
        static UnitDefinition Bugaloo(float duration = 1)
        {
            var definition = DocumentedRoster.CreateBugaloo();
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "duration", Value = duration } };
            definition.Damage = 0; definition.AttackWindup = .1f; definition.AttackInterval = 100;
            definition.EnergyPerAttack = definition.EnergyOnDamage = definition.EnergyPerSecond = 0;
            return definition;
        }
        BattleUnit Enemy(int damage = 0, float interval = 100, int health = 1000, int x = 2)
            => Unit(new UnitDefinition { Id = "enemy", MaxHealth = health, Damage = damage, AttackRange = 1,
                AttackInterval = interval, AttackWindup = .1f, MoveInterval = 100 }, BoardSide.Red, x);
        void Begin(params BattleUnit[] units)
        {
            simulation = new CombatSimulation(board); simulation.Begin(units);
        }
        void Activate(BattleUnit actor)
        {
            actor.AddEnergy(actor.BaseStats.EnergyMax); simulation.Step(.1f);
            Assert.IsTrue(actor.IsEnergyLocked); Assert.IsTrue(actor.IsReflecting);
            Assert.AreEqual(0, actor.Energy);
        }
        static void AssertInactive(BattleUnit actor)
        {
            Assert.IsFalse(actor.IsEnergyLocked); Assert.IsFalse(actor.IsReflecting); Assert.IsFalse(actor.IsProtected);
        }

        [Test] public void ReflectionLocksPassiveAttackDamageAndExternalEnergyWithoutRestarting()
        {
            var definition = Bugaloo(); definition.MaxHealth = 10000; definition.AttackInterval = .2f;
            definition.EnergyPerAttack = 25; definition.EnergyOnDamage = 40; definition.EnergyPerSecond = 30;
            var actor = Unit(definition, BoardSide.Blue); var enemy = Enemy(1, .2f);
            Begin(actor, enemy);
            int supers = 0, basics = 0, received = 0;
            simulation.AbilityUsed += (source, kind) => { if (source == actor && kind == "bugaloo-super") supers++; };
            simulation.Attacked += (source, target, projectile) => { if (source == actor) basics++; };
            simulation.Impacted += (source, target, amount) => { if (target == actor && amount > 0) received++; };
            Activate(actor);
            for (int i = 0; i < 9; i++)
            {
                Assert.AreEqual(0, actor.AddEnergy(1000)); simulation.Step(.1f);
                Assert.AreEqual(0, actor.Energy); Assert.IsTrue(actor.IsEnergyLocked); Assert.IsTrue(actor.IsReflecting);
            }
            Assert.AreEqual(1, supers); Assert.Greater(basics, 0); Assert.Greater(received, 0);
            Assert.IsFalse(actor.IsProtected);
            simulation.Step(.1f); AssertInactive(actor);
            float before = actor.Energy; Assert.Greater(actor.AddEnergy(1), 0); Assert.Greater(actor.Energy, before);
        }

        [Test] public void ConfiguredDurationExpiresAtZeroThenPassiveChargingAndNextSuperResume()
        {
            var definition = Bugaloo(.7f); definition.EnergyPerSecond = 10; definition.AttackInterval = .2f;
            var actor = Unit(definition, BoardSide.Blue, protection: true); var enemy = Enemy(); Begin(actor, enemy);
            Activate(actor); Assert.IsTrue(actor.IsProtected);
            simulation.Step(.6f); Assert.IsTrue(actor.IsReflecting); Assert.AreEqual(0, actor.Energy);
            simulation.Step(.1f); AssertInactive(actor); Assert.AreEqual(0, actor.Energy);
            simulation.Step(.1f); Assert.AreEqual(1, actor.Energy, .001f);
            // A second activation becomes possible once the normal attack cooldown is ready.
            actor.AddEnergy(100); simulation.Step(.2f);
            Assert.IsTrue(actor.IsEnergyLocked); Assert.IsTrue(actor.IsReflecting); Assert.IsTrue(actor.IsProtected);
            Assert.AreEqual(0, actor.Energy);
        }

        [TestCase(false, 20)] [TestCase(true, 10)]
        public void ReflectionUsesOriginalDamageAndProtectionOnlyDuringItsWindow(bool protection, int incoming)
        {
            var actor = Unit(Bugaloo(.5f), BoardSide.Blue, protection: protection); var enemy = Enemy(20, .5f);
            Begin(actor, enemy); Activate(actor); Assert.AreEqual(protection, actor.IsProtected);
            simulation.Step(.1f); Assert.AreEqual(110 - incoming, actor.CurrentHealth); Assert.AreEqual(980, enemy.CurrentHealth);
            simulation.Step(.5f); AssertInactive(actor);
            Assert.AreEqual(110 - incoming - 20, actor.CurrentHealth); Assert.AreEqual(980, enemy.CurrentHealth);
        }

        [TestCase("reset")] [TestCase("stop")] [TestCase("finish")] [TestCase("death")]
        public void EveryCombatExitClearsReflectionProtectionAndEnergyLock(string exit)
        {
            var actor = Unit(Bugaloo(), BoardSide.Blue, protection: true); var enemy = Enemy();
            Begin(actor, enemy); Activate(actor); Assert.IsTrue(actor.IsProtected);
            if (exit == "reset") { actor.ResetForCombat(); simulation.Step(.1f); }
            else if (exit == "stop") simulation.Stop();
            else if (exit == "finish") { enemy.ApplyDamage(10000); simulation.Step(.1f); Assert.IsTrue(simulation.Finished); }
            else { actor.ApplyDamage(10000); simulation.Step(.1f); }
            AssertInactive(actor); Assert.AreEqual(0, actor.Energy);
        }

        [Test] public void RestartingCombatClearsOldSuperTimerAndAllowsNormalCharging()
        {
            var definition = Bugaloo(); definition.EnergyPerSecond = 10;
            var actor = Unit(definition, BoardSide.Blue, protection: true); var enemy = Enemy();
            Begin(actor, enemy); Activate(actor);
            simulation.Begin(new[] { actor, enemy }); AssertInactive(actor); Assert.AreEqual(0, actor.Energy);
            simulation.Step(.1f); Assert.AreEqual(1, actor.Energy, .001f);
        }

        [Test] public void AtongEmitsSpecialOnlyWhenTheCriticalHitLands()
        {
            var definition = DocumentedRoster.CreateAtong(); definition.AttackInterval = .2f; definition.AttackWindup = .1f;
            var actor = Unit(definition, BoardSide.Blue); var enemy = Enemy(); Begin(actor, enemy);
            int specials = 0;
            simulation.AbilityUsed += (source, kind) => { if (source == actor && kind == "atong-critical") specials++; };
            simulation.Step(.2f); Assert.AreEqual(0, specials);
            simulation.Step(.1f); Assert.AreEqual(0, specials);
            simulation.Step(.1f); Assert.AreEqual(1, specials);
        }

        [Test] public void JazarEmitsSpecialForNewRadiationWithoutRepeatingOnUnchangedSlow()
        {
            var definition = DocumentedRoster.CreateJazar(); definition.AttackInterval = .2f;
            definition.AttackWindup = .1f; definition.ProjectileSpeed = 100;
            var actor = Unit(definition, BoardSide.Blue); var enemy = Enemy(); Begin(actor, enemy);
            int specials = 0;
            simulation.AbilityUsed += (source, kind) => { if (source == actor && kind == "jazar-radiation") specials++; };
            simulation.Step(.4f); Assert.IsTrue(enemy.IsSlowed); Assert.AreEqual(1, specials);
            simulation.Step(.6f); Assert.AreEqual(1, specials);
        }
    }
}

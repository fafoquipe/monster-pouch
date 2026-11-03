using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TsuColoredGumTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board; CombatSimulation sim;
        [SetUp] public void Setup()
        {
            var go = new GameObject("gum-test-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void Cleanup()
        {
            sim?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }
        [TestCase(0f, 2f)] [TestCase(4f, 1f)] [TestCase(8f, .5f)]
        public void GiftUsesEditableSpeedAndAppliesOnlyOnArrival(float editedSpeed, float expectedTravel)
        {
            var tsu = Tsu(editedSpeed);
            var ally = Dummy("ally", 2, 9, BoardSide.Blue);
            var enemy = Dummy("enemy", 2, 4, BoardSide.Red);
            Begin(tsu, ally, enemy);
            float travel = -1; int launches = 0;
            sim.ColoredGumLaunched += (from, to, kind, seconds) =>
            {
                Assert.AreSame(tsu, from); Assert.AreSame(ally, to); Assert.AreEqual(GumBuff.Speed, kind);
                travel = seconds; launches++;
            };
            sim.Step(.2f);
            Assert.AreEqual(1, launches); Assert.AreEqual(expectedTravel, travel, .001f);
            Assert.AreEqual(0, ClockValue(ally, "ExternalSpeedBonus"));
            sim.Step(travel - .1f);
            Assert.AreEqual(0, ClockValue(ally, "ExternalSpeedBonus"), "The buff cannot arrive before its projectile.");
            sim.Step(.1f);
            Assert.AreEqual(.5f, ClockValue(ally, "ExternalSpeedBonus"), .001f);
        }
        [Test] public void GiftCyclesSpeedShieldDamageAndDoesNotDamageAlly()
        {
            var tsu = Tsu(8, 1);
            var ally = Dummy("ally", 2, 9, BoardSide.Blue);
            var enemy = Dummy("enemy", 2, 4, BoardSide.Red);
            Begin(tsu, ally, enemy);
            var types = new List<GumBuff>();
            sim.ColoredGumLaunched += (_, __, kind, ___) => types.Add(kind);
            sim.Step(2.7f);
            CollectionAssert.AreEqual(new[] { GumBuff.Speed, GumBuff.Shield, GumBuff.Damage }, types);
            Assert.AreEqual(.5f, ClockValue(ally, "ExternalSpeedBonus"), .001f);
            Assert.AreEqual(0, ally.Shield); Assert.AreEqual(.3f, ClockValue(ally, "DamageReduction"), .001f);
            Assert.AreEqual(1.5f, ClockValue(ally, "ExternalDamageMultiplier"), .001f);
            Assert.AreEqual(1000, ally.CurrentHealth);
        }
        [Test] public void StickyCellExistsOnlyForAnActiveGumRoot()
        {
            var tsu = Tsu(0);
            var enemy = Dummy("enemy", 2, 4, BoardSide.Red);
            Begin(tsu, enemy);
            sim.Step(.5f);
            CollectionAssert.AreEqual(new[] { enemy }, sim.GumRootedUnits.ToArray());
            sim.Step(.7f);
            Assert.IsEmpty(sim.GumRootedUnits);
        }
        [Test] public void RepeatedGiftCyclesDoNotAccumulateExternalStats()
        {
            var tsu = Tsu(8, 1); var ally = Dummy("ally", 2, 9, BoardSide.Blue);
            Begin(tsu, ally, Dummy("enemy", 2, 4, BoardSide.Red));
            sim.Step(8.7f);
            Assert.AreEqual(.5f, ClockValue(ally, "ExternalSpeedBonus"), .001f);
            Assert.AreEqual(.5f, ClockValue(ally, "ExternalMoveBonus"), .001f);
            Assert.AreEqual(.3f, ClockValue(ally, "DamageReduction"), .001f);
            Assert.AreEqual(1.5f, ClockValue(ally, "ExternalDamageMultiplier"), .001f);
        }
        [Test] public void SpeedParameterIsNotPartOfPlayerDescription()
        {
            var definition = DocumentedRoster.CreateTsu();
            definition.Tricks[2].Parameters = new[] { new AbilityParameter { Key = "projectileSpeed", Value = 123 } };
            string text = AbilityBalance.Description(definition.Tricks[2], definition);
            StringAssert.DoesNotContain("123", text);
            StringAssert.DoesNotContain("proyectil", text);
            Assert.AreEqual(123, AbilityBalance.Resolve(definition.Tricks[2]).First(p => p.Key == "projectileSpeed").Value);
        }
        [Test] public void ZeroDamageRecipientKeepsItsGiftForLaterInnateDamage()
        {
            var tsu=Tsu(8,1);var ally=Dummy("ally",2,9,BoardSide.Blue);
            Begin(tsu,ally,Dummy("enemy",2,4,BoardSide.Red));sim.Step(2.7f);
            var actor=typeof(CombatSimulation).GetMethod("Clock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{ally});
            actor.GetType().GetField("DamageBonus").SetValue(actor,10);
            int damage=(int)typeof(CombatSimulation).GetMethod("BasicDamage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new[]{actor});
            Assert.AreEqual(15,damage,"The previously received +50% gift must still apply when innate damage is gained.");
        }
        [Test] public void StunDuringWindupCancelsTheUnreleasedGift()
        {
            var tsu = Tsu(0, 100, 1);
            var ally = Dummy("ally", 2, 9, BoardSide.Blue);
            var definition = DocumentedRoster.CreateStein();
            definition.Damage = 0; definition.MoveInterval = 100;
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "castDuration", Value = .1f } };
            var enemy = Unit(definition, "enemy", 2, 4, BoardSide.Red);
            Begin(tsu, ally, enemy); enemy.AddEnergy(enemy.BaseStats.EnergyMax);
            int launches = 0; sim.ColoredGumLaunched += (_, __, ___, ____) => launches++;
            sim.Step(1.5f);
            Assert.IsTrue(tsu.IsStunned); Assert.AreEqual(0, launches);
            Assert.AreEqual(0, ClockValue(ally, "ExternalSpeedBonus"));
        }
        [Test] public void ReleasedGiftStillArrivesIfTsuDiesInFlight()
        {
            var tsu = Tsu(0); var ally = Dummy("ally", 2, 9, BoardSide.Blue);
            var enemy = Dummy("enemy", 2, 4, BoardSide.Red);
            Begin(tsu, ally, enemy); sim.Step(.2f);
            tsu.ApplyDamage(10000); sim.Step(2f);
            Assert.AreEqual(.5f, ClockValue(ally, "ExternalSpeedBonus"), .001f);
        }
        BattleUnit Tsu(float speed, float interval = 100, float windup = .1f)
        {
            var definition = DocumentedRoster.CreateTsu();
            definition.MaxHealth = 1000; definition.Damage = 0; definition.AttackWindup = windup;
            definition.AttackInterval = interval; definition.MoveInterval = 100; definition.ProjectileSpeed = 4;
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "every", Value = 1 }, new AbilityParameter { Key = "duration", Value = .6f } };
            definition.Tricks[2].Parameters = new[] { new AbilityParameter { Key = "projectileSpeed", Value = speed } };
            return Unit(definition, "tsu", 2, 5, BoardSide.Blue, 2);
        }
        BattleUnit Dummy(string id, int x, int y, BoardSide side) => Unit(new UnitDefinition
        { Id = id, MaxHealth = 1000, Damage = 0, AttackRange = 9, AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100 }, id, x, y, side);
        BattleUnit Unit(UnitDefinition definition, string id, int x, int y, BoardSide side, params int[] tricks)
        {
            var owned = new OwnedUnit(definition); foreach (int trick in tricks) owned.Tricks[trick] = true;
            var go = new GameObject(id); objects.Add(go); var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, UnitStats.FromDefinition(definition, owned)); Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        void Begin(params BattleUnit[] units) { sim = new CombatSimulation(board); sim.Begin(units); }
        float ClockValue(BattleUnit unit, string field)
        {
            var actor = typeof(CombatSimulation).GetMethod("Clock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sim, new object[] { unit });
            return (float)actor.GetType().GetField(field).GetValue(actor);
        }
    }
}

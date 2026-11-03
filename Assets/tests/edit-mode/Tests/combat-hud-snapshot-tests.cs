using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public class CombatHudSnapshotTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<PassiveProgress> progress = new List<PassiveProgress>();
        readonly List<ActiveStatus> states = new List<ActiveStatus>();
        BoardManager board;
        CombatSimulation sim;
        [SetUp] public void Setup()
        { var go = new GameObject("hud board"); objects.Add(go); board = go.AddComponent<BoardManager>(); board.BuildBoard(); }
        [TearDown] public void Cleanup()
        { sim?.Stop(); foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); objects.Clear(); }
        BattleUnit Unit(UnitDefinition definition, BoardSide side, params int[] upgrades)
        {
            definition.MaxHealth = 100000; definition.AttackInterval = .7f; definition.AttackRange = 9;
            definition.EnergyPerAttack = definition.EnergyPerSecond = definition.EnergyOnDamage = 0;
            var owned = new OwnedUnit(definition); foreach (var index in upgrades) owned.Tricks[index] = true;
            var go = new GameObject(side.ToString()); objects.Add(go); var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, 2, side == BoardSide.Blue ? 5 : 4)); return unit;
        }
        BattleUnit Enemy() => Unit(new UnitDefinition { Id = "target", Damage = 0 }, BoardSide.Red);
        void Begin(BattleUnit unit, BattleUnit enemy) { sim = new CombatSimulation(board); sim.Begin(new[] { unit, enemy }); }
        object Clock(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sim))
            .Cast<object>().Single(a => a.GetType().GetField("Unit").GetValue(a) == (object)unit);
        static void Set(object actor, string name, object value) => actor.GetType().GetField(name).SetValue(actor, value);

        [TestCase("trimol", 4)] [TestCase("tsu", 4)] [TestCase("atong", 2)] [TestCase("popow", 3)]
        public void RealAttacksAdvanceReadyAndWrapWithoutEnergyReplacingThePassive(string id, int every)
        {
            var definition = DocumentedRoster.CreateAll().Single(d => d.Id == id);
            var unit = Unit(definition, BoardSide.Blue, id == "popow" ? new[] { 1 } : new int[0]);
            var enemy = Enemy(); Begin(unit, enemy);
            var sequence = new List<int>(); bool sawReady = false, wrapped = false;
            for (int i = 0; i < 150 && !wrapped; i++)
            {
                sim.CollectPassiveProgress(unit, progress); Assert.AreEqual(1, progress.Count);
                var p = progress[0]; Assert.AreEqual(every, p.Every);
                if (sequence.Count == 0 || sequence.Last() != p.Completed) sequence.Add(p.Completed);
                if (p.Ready) { sawReady = true; Assert.AreEqual(every - 1, p.Completed); }
                wrapped = sawReady && p.Completed == 0;
                sim.Step(.1f);
            }
            CollectionAssert.AreEqual(Enumerable.Range(0, every).Concat(new[] { 0 }).ToArray(), sequence);
            Assert.IsTrue(wrapped);
            if (id == "popow") Assert.Greater(unit.BaseStats.EnergyMax, 0);
            sim.Begin(new[] { unit, enemy });
            sim.CollectPassiveProgress(unit, progress); Assert.AreEqual(0, progress[0].Completed);
        }

        [Test] public void EditedPeriodAndAlwaysCriticalAreReflectedAndRefreshNeverConsumesAttacks()
        {
            var definition = DocumentedRoster.CreateTrimol();
            definition.BaseAbility.Parameters = new[] { new AbilityParameter { Key = "every", Value = 5 } };
            var unit = Unit(definition, BoardSide.Blue); Begin(unit, Enemy());
            for (int i = 0; i < 10; i++) { sim.CollectPassiveProgress(unit, progress); Assert.AreEqual(5, progress[0].Every); Assert.AreEqual(0, progress[0].Completed); }
            sim.Stop(); sim.CollectPassiveProgress(unit, progress); Assert.IsEmpty(progress);
        }

        [Test] public void EnergyCastDoesNotIncrementPopowCounterAndUnpurchasedPassiveIsAbsent()
        {
            var unit = Unit(DocumentedRoster.CreatePopow(), BoardSide.Blue, 1); var enemy = Enemy(); Begin(unit, enemy);
            unit.AddEnergy(unit.BaseStats.EnergyMax); sim.Step(.1f);
            sim.CollectPassiveProgress(unit, progress); Assert.AreEqual(0, progress[0].Completed);
            unit.Initialize(unit.UnitId, unit.Side, UnitStats.FromDefinition(DocumentedRoster.CreatePopow()));
            sim.Begin(new[] { unit, enemy }); sim.CollectPassiveProgress(unit, progress); Assert.IsEmpty(progress);
        }

        [Test] public void AlwaysCriticalIsReadyFromTheFirstAttack()
        {
            var unit = Unit(DocumentedRoster.CreateAtong(), BoardSide.Blue, 1); Begin(unit, Enemy());
            sim.CollectPassiveProgress(unit, progress); Assert.IsTrue(progress[0].Ready); Assert.AreEqual(1, progress[0].Every);
        }

        [Test] public void AtongControlImmunityIsTimedAndIsNotDamageInvulnerability()
        {
            var unit = Unit(DocumentedRoster.CreateAtong(), BoardSide.Blue, 2); Begin(unit, Enemy());
            sim.CollectStatuses(unit, states); Assert.AreEqual(5, states.Single(s => s.Id == "control-immunity").Remaining);
            Assert.IsFalse(states.Any(s => s.Id == "invulnerable"));
            sim.Step(5); sim.CollectStatuses(unit, states); Assert.IsFalse(states.Any(s => s.Id == "control-immunity"));
        }

        [Test] public void FloSummonsFlowerWithItsOwnDamageInvulnerabilityCountdown()
        {
            var unit = Unit(DocumentedRoster.CreateFlo(), BoardSide.Blue, 2); Begin(unit, Enemy());
            unit.AddEnergy(unit.BaseStats.EnergyMax);
            BattleUnit flower = null; sim.Summoned += (u, d) => { if (d.Id == "flower") flower = u; };
            for (int i = 0; i < 25 && flower == null; i++) sim.Step(.1f);
            Assert.IsNotNull(flower); sim.CollectStatuses(flower, states);
            Assert.AreEqual(1, states.Single(s => s.Id == "invulnerable").Remaining, .001f);
            Assert.IsFalse(states.Any(s => s.Id == "control-immunity"));
            sim.Step(.4f); sim.CollectStatuses(flower, states);
            Assert.AreEqual(.6f, states.Single(s => s.Id == "invulnerable").Remaining, .001f);
            sim.Step(.6f); sim.CollectStatuses(flower, states); Assert.IsFalse(states.Any(s => s.Id == "invulnerable"));
        }

        [Test] public void TemporaryAndPermanentSpeedBonusesCoexistAndExpireIndependently()
        {
            var unit = Unit(DocumentedRoster.CreateBugui(), BoardSide.Blue); Begin(unit, Enemy()); var clock = Clock(unit);
            Set(clock, "ExternalSpeedUntil", 5); Set(clock, "ExternalSpeedBonus", .3f); Set(clock, "ExternalSpeedId", "rose-haste"); Set(clock, "SpeedBonus", .2f); Set(clock, "Attacks", 3);
            sim.CollectStatuses(unit, states);
            Assert.AreEqual(.5f, states.Single(s => s.Id == "rose-haste").Remaining, .001f);
            Assert.AreEqual(-1, states.Single(s => s.Id == "haste").Remaining);
            Assert.AreEqual(3, states.Single(s => s.Id == "rhythm").Intensity);
            sim.Step(.5f); sim.CollectStatuses(unit, states);
            Assert.IsFalse(states.Any(s => s.Id == "rose-haste")); Assert.IsTrue(states.Any(s => s.Id == "haste"));
        }

        [Test] public void StealthSnapshotKeepsRemainingTimeAndClearsAtExpiryDeathAndStop()
        {
            var unit = Unit(DocumentedRoster.CreateBlotan(), BoardSide.Blue); Begin(unit, Enemy());
            Set(Clock(unit), "StealthUntil", 8); sim.Step(.1f); sim.CollectStatuses(unit, states);
            Assert.AreEqual(.7f, states.Single(s => s.Id == "stealth").Remaining, .001f);
            for (int i = 0; i < 5; i++) { sim.CollectStatuses(unit, states); Assert.AreEqual(.7f, states.Single(s => s.Id == "stealth").Remaining, .001f); }
            sim.Step(.7f); sim.CollectStatuses(unit, states); Assert.IsFalse(states.Any(s => s.Id == "stealth"));
            unit.ApplyDamage(unit.CurrentHealth); sim.CollectStatuses(unit, states); Assert.IsEmpty(states);
            sim.Stop(); sim.CollectStatuses(unit, states); Assert.IsEmpty(states);
        }
    }
}

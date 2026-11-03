using System;
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
    public sealed class SukiTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board; CombatSimulation simulation;
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [SetUp] public void Setup() { simulation = null; var go = new GameObject("Suki board"); objects.Add(go); board = go.AddComponent<BoardManager>(); board.BuildBoard(); }
        [TearDown] public void Cleanup() { simulation?.Stop(); foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); objects.Clear(); }
        BattleUnit Unit(UnitDefinition d, int x, int y, BoardSide side = BoardSide.Blue, params int[] tricks)
        {
            var go = new GameObject(d.Id + objects.Count); objects.Add(go); var owned = new OwnedUnit(d);
            foreach (int t in tricks) owned.Tricks[t] = true;
            var unit = go.AddComponent<WhelpUnit>(); unit.Initialize(go.name, side, UnitStats.FromDefinition(d, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        BattleUnit Ally(int x, int y, BoardSide side = BoardSide.Blue) => Unit(new UnitDefinition
        { Id = "ally", MaxHealth = 200, Damage = 0, AttackRange = 99, AttackInterval = 100, MoveInterval = 100 }, x, y, side);
        object Actor(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(simulation))
            .Cast<object>().Single(a => a.GetType().GetField("Unit").GetValue(a) == (object)unit);
        void Begin(params BattleUnit[] units)
        {
            if (simulation == null) simulation = new CombatSimulation(board);
            simulation.Begin(units);
            foreach (var unit in units) Actor(unit).GetType().GetField("NextMove").SetValue(Actor(unit), 99999);
        }
        void Cast(BattleUnit unit) { unit.AddEnergy(unit.BaseStats.EnergyMax); simulation.Step(.4f); }
        void Damage(BattleUnit source, BattleUnit target, int amount) => typeof(CombatSimulation).GetMethod("DirectDamage", Private)
            .Invoke(simulation, new[] { Actor(source), Actor(target), (object)amount });
        static AbilityParameter P(string key, float value) => new AbilityParameter { Key = key, Value = value };

        [Test] public void BaseStatsAndAdditiveBalanceRemainIndependent()
        {
            var d = DocumentedRoster.CreateSuki(); var owned = new OwnedUnit(d);
            Assert.AreEqual(AttackKind.Melee, d.AttackKind); Assert.AreEqual(50, d.MaxHealth); Assert.AreEqual(3, d.Damage);
            Assert.AreEqual(20, UnitStats.FromDefinition(d).EnergyMax);
            owned.Tricks[0] = true; Assert.AreEqual(20, UnitStats.FromDefinition(d, owned).EnergyMax);
            Assert.IsTrue(UnitStats.FromDefinition(d, owned).HasEffect("suki-shield-energy"));
            Assert.That(AbilityBalance.Description(d.Tricks[0], d), Does.Contain("+1 de energía"));
            Assert.AreEqual(10, AbilityBalance.ParameterEditorValue("suki-extra-shield", "shieldBonus", 10, d));
            Assert.AreEqual(12, AbilityBalance.ParameterStoredValue("suki-extra-shield", "shieldBonus", 12, d));
            Assert.That(AbilityBalance.Description(d.Tricks[2], d), Does.Contain("+10 de escudo").And.Not.Contain("25"));
            d.Tricks[2].Parameters = new[] { P("shieldBonus", 12) };
            Assert.That(AbilityBalance.Description(d.Tricks[2], d), Does.Contain("+12 de escudo"));
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void ShieldsSelfAndExactlyThreeNearestAllies_OnArrival(BoardSide side)
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 5, side);
            var near = Ally(2, 4, side); var second = Ally(3, 5, side); var third = Ally(2, 7, side); var far = Ally(5, 8, side);
            var enemy = Ally(1, 5, side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue);
            Begin(far, suki, second, enemy, near, third); var flights = new List<CombatSimulation.FishShieldFlight>();
            simulation.FishShieldLaunched += flights.Add;
            Cast(suki);
            Assert.AreEqual(3, flights.Count); CollectionAssert.AreEquivalent(new[] { near, second, third }, flights.Select(f => f.Target));
            Assert.AreEqual(15, suki.Shield); Assert.AreEqual(0, near.Shield); Assert.AreEqual(0, second.Shield);
            simulation.Step(.6f); Assert.AreEqual(0, near.Shield);
            simulation.Step(.1f); Assert.AreEqual(15, near.Shield); Assert.AreEqual(15, second.Shield); Assert.AreEqual(15, third.Shield);
            Assert.AreEqual(0, far.Shield); Assert.AreEqual(0, enemy.Shield); Assert.IsTrue(flights.All(f => f.Arrived && !f.Active));
            Assert.AreEqual(50, suki.CurrentHealth); Assert.AreEqual(200, near.CurrentHealth);
        }

        [Test] public void ExpiresFiveSecondsAfterEachRecipientGetsIt_WithoutErasingOtherShields()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); ally.AddShield(9); Cast(suki); simulation.Step(.7f);
            Assert.AreEqual(24, ally.Shield);
            var states = new List<ActiveStatus>(); simulation.CollectStatuses(suki, states);
            Assert.AreEqual(4.3f, states.Single(s => s.Id == "shield").Remaining, .001f);
            simulation.Step(4.2f); Assert.AreEqual(15, suki.Shield);
            simulation.Step(.1f); Assert.AreEqual(0, suki.Shield); Assert.AreEqual(24, ally.Shield);
            simulation.Step(.7f); Assert.AreEqual(9, ally.Shield); Assert.AreEqual(0, ally.FishShield);
        }

        [Test] public void DamageConsumesTemporaryThenPersistentShieldThenHealth()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); Cast(suki); simulation.Step(.7f); ally.AddShield(9);
            Damage(enemy, ally, 12); Assert.AreEqual(12, ally.Shield); Assert.AreEqual(3, ally.FishShield); Assert.AreEqual(200, ally.CurrentHealth);
            Damage(enemy, ally, 17); Assert.AreEqual(0, ally.Shield); Assert.AreEqual(195, ally.CurrentHealth);
            Damage(enemy, ally, 10); Assert.AreEqual(185, ally.CurrentHealth, "No damage reduction is granted.");
        }

        [Test] public void ThirdTrickAddsEditedBonusToEditedBase_ForSelfAndAllies()
        {
            var d = DocumentedRoster.CreateSuki(); d.BaseAbility.Parameters = new[] { P("shield", 20), P("duration", 2), P("travelDuration", .4f) };
            d.Tricks[2].Parameters = new[] { P("shieldBonus", 7) };
            var suki = Unit(d, 2, 8, BoardSide.Blue, 2); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); Cast(suki); simulation.Step(.4f); Assert.AreEqual(27, suki.Shield); Assert.AreEqual(27, ally.Shield);
            simulation.Step(1.6f); Assert.AreEqual(0, suki.Shield); Assert.AreEqual(27, ally.Shield);
            simulation.Step(.4f); Assert.AreEqual(0, ally.Shield);
        }

        [Test] public void OpeningTrickCastsAtZeroTimeForFreeAndOnlyOnce()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8, BoardSide.Blue, 1, 2); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            simulation = new CombatSimulation(board); var flights = new List<CombatSimulation.FishShieldFlight>(); simulation.FishShieldLaunched += flights.Add;
            Begin(suki, ally, enemy); Assert.AreEqual(1, flights.Count); Assert.AreEqual(0, flights[0].StartedAt);
            Assert.AreEqual(25, suki.Shield); Assert.AreEqual(0, suki.Energy); Assert.AreEqual(0, ally.Shield);
            simulation.Step(.7f); Assert.AreEqual(25, ally.Shield); simulation.Step(1); Assert.AreEqual(1, flights.Count);
        }

        [TestCase(0)] [TestCase(1)]
        public void CastWorksWithFewerThanThreeAllies(int allies)
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var enemy = Ally(5, 0, BoardSide.Red);
            var units = new List<BattleUnit> { suki, enemy }; if (allies == 1) units.Add(Ally(2, 7));
            Begin(units.ToArray()); int count = 0; simulation.FishShieldLaunched += f => count++;
            Cast(suki); Assert.AreEqual(allies, count); Assert.AreEqual(15, suki.Shield); Assert.AreEqual(0, suki.Energy);
        }

        [Test] public void InFlightFishFollowsItsRecipient_AndSurvivesCasterDeath()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); Cast(suki); suki.ApplyDamage(100);
            Assert.IsTrue(board.TryRepositionUnit(ally, board.GetCell(4, 7)));
            simulation.Step(.7f); Assert.AreEqual(15, ally.Shield);
        }

        [Test] public void ResetOrDeathOfRecipientCancelsFish_AndStopClearsTemporaryLayersOnly()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); CombatSimulation.FishShieldFlight flight = null; simulation.FishShieldLaunched += f => flight = f;
            Cast(suki); ally.ResetForCombat(); simulation.Step(.7f); Assert.AreEqual(0, ally.Shield); Assert.IsFalse(flight.Arrived);
            suki.AddShield(6); simulation.Stop(); Assert.AreEqual(6, suki.Shield); Assert.AreEqual(0, suki.FishShield);
        }

        [Test] public void RecastQueuesNewPoints_WithoutRefillingOrExtendingPreviousLayer()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); Cast(suki); simulation.Step(.7f); Damage(enemy, ally, 8); Assert.AreEqual(7, ally.Shield);
            simulation.Step(1); Cast(suki); simulation.Step(.7f); Assert.AreEqual(22, ally.Shield);
            simulation.Step(2.8f); Assert.AreEqual(22, ally.Shield);
            simulation.Step(.1f); Assert.AreEqual(15, ally.Shield, "The old 7 points expire on their original deadline.");
            simulation.Step(2.1f); Assert.AreEqual(0, ally.Shield);
        }

        [Test] public void ReceivingHitsGrantsTwoEnergyEvenWhenFullyAbsorbed()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, enemy); Cast(suki); simulation.Step(.4f);
            for (int i = 0; i < 3; i++) Damage(enemy, suki, 2);
            Assert.AreEqual(6, suki.Energy); Assert.AreEqual(9, suki.Shield); Assert.AreEqual(50, suki.CurrentHealth);
        }

        [Test] public void BasicAttackGrantsOneEnergyOnlyWhenItHits()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var enemy = Ally(2, 7, BoardSide.Red);
            Begin(suki, enemy); simulation.Step(.1f); Assert.AreEqual(0, suki.Energy);
            simulation.Step(.5f); Assert.AreEqual(1, suki.Energy); Assert.AreEqual(197, enemy.CurrentHealth);
        }

        [Test] public void SameTickIncomingAttacksEachGrantTwoEnergy()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8);
            var d = new UnitDefinition { Id = "enemy", MaxHealth = 200, Damage = 2, AttackRange = 1, AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100 };
            var a = Unit(d, 2, 7, BoardSide.Red); var b = Unit(d, 3, 8, BoardSide.Red);
            Begin(suki, a, b); Actor(suki).GetType().GetField("NextAttack").SetValue(Actor(suki), 99999);
            suki.RefreshFishShield(15, until: 100);
            simulation.Step(.3f); Assert.AreEqual(4, suki.Energy); Assert.AreEqual(11, suki.Shield); Assert.AreEqual(50, suki.CurrentHealth);
        }

        [Test] public void ShieldEnergyTrickCreditsActualAbsorbedDamage_ExcludesSelfAndOtherShields()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8, BoardSide.Blue, 0); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); Cast(suki); simulation.Step(.7f);
            Damage(enemy, ally, 6); Assert.AreEqual(6, suki.Energy);
            Damage(enemy, suki, 2); Assert.AreEqual(8, suki.Energy, "Self grants normal +2 per hit only, never shield feedback.");
            ally.AddShield(20); Damage(enemy, ally, 30);
            Assert.AreEqual(17, suki.Energy, "Only remaining 9 Suki shield points count, not persistent shield or HP.");
            Damage(enemy, ally, 5); Assert.AreEqual(17, suki.Energy);
        }

        [Test] public void ShieldFeedbackRequiresPurchasedTrick_AndUsesEditedRateAndCorrectSource()
        {
            var first = Unit(DocumentedRoster.CreateSuki(), 2, 8);
            var d = DocumentedRoster.CreateSuki(); d.Tricks[0].Parameters = new[] { P("energyPerShieldDamage", 2) };
            var second = Unit(d, 3, 8, BoardSide.Blue, 0); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(first, second, ally, enemy); Cast(first); simulation.Step(.7f);
            Damage(enemy, ally, 3); Assert.AreEqual(0, first.Energy); Assert.AreEqual(0, second.Energy);
            Cast(second); simulation.Step(.7f); Damage(enemy, ally, 4);
            Assert.AreEqual(0, second.Energy); Assert.AreEqual(0, first.Energy, "The older layer absorbs damage first.");
            first.ResetForCombat(); Damage(enemy, ally, 12); Assert.AreEqual(8, second.Energy, "Only 4 points came from the second caster.");
            Damage(enemy, ally, 9); Assert.AreEqual(20, second.Energy, "Feedback clamps at maximum energy.");
        }

        [Test] public void QueuedShieldCanExpireBeforeTheFrontLayer_AndDamageIsFifoAcrossTypes()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy);
            ally.AddShield(9, 50);
            ally.RefreshFishShield(15, suki, 10);
            simulation.Step(1); Assert.AreEqual(9, ally.Shield, "The queued layer's clock runs from application.");
            ally.RefreshFishShield(15, suki, 30);
            Damage(enemy, ally, 12);
            Assert.AreEqual(12, ally.FishShield); Assert.AreEqual(12, ally.Shield);
            Assert.AreEqual(200, ally.CurrentHealth);
        }

        [Test] public void ExpiryNeverGrantsEnergy_AndSourceResetInvalidatesFeedback()
        {
            var suki = Unit(DocumentedRoster.CreateSuki(), 2, 8, BoardSide.Blue, 0); var ally = Ally(2, 7); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(suki, ally, enemy); Cast(suki); simulation.Step(.7f); suki.ResetForCombat();
            Damage(enemy, ally, 4); Assert.AreEqual(0, suki.Energy);
            simulation.Step(5); Assert.AreEqual(0, suki.Energy); Assert.AreEqual(0, ally.Shield);
        }
    }
}

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
    public sealed class PokrakTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board; CombatSimulation sim;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void Setup() { var go = new GameObject("Pokrak board"); objects.Add(go); board = go.AddComponent<BoardManager>(); board.BuildBoard(); }
        [TearDown] public void Cleanup() { sim?.Stop(); foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); objects.Clear(); sim = null; }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side = BoardSide.Blue, params int[] tricks)
        {
            var go = new GameObject(definition.Id + objects.Count); objects.Add(go); var owned = new OwnedUnit(definition);
            foreach (int t in tricks) owned.Tricks[t] = true;
            var unit = go.AddComponent<WhelpUnit>(); unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        BattleUnit Ally(int x, int y, BoardSide side = BoardSide.Blue) => Unit(new UnitDefinition
        { Id = "ally", MaxHealth = 1000, Damage = 0, AttackRange = 99, AttackInterval = 100, MoveInterval = 100 }, x, y, side);
        object Actor(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(sim)).Cast<object>()
            .Single(a => a.GetType().GetField("Unit").GetValue(a) == (object)unit);
        void Begin(params BattleUnit[] units)
        {
            sim = new CombatSimulation(board); sim.Begin(units);
            foreach (var unit in units) { var a = Actor(unit); a.GetType().GetField("NextMove").SetValue(a, 99999); a.GetType().GetField("NextAttack").SetValue(a, 99999); }
        }
        void Cast(BattleUnit unit) { unit.AddEnergy(unit.BaseStats.EnergyMax); sim.Step(.4f); }
        void Damage(BattleUnit source, BattleUnit target, int amount) => typeof(CombatSimulation).GetMethod("DirectDamage", Private).Invoke(sim, new[] { Actor(source), Actor(target), (object)amount });
        static AbilityParameter P(string key, float value) => new AbilityParameter { Key = key, Value = value };

        [Test] public void DefaultsEnergyAndReadablePercentDescriptions()
        {
            var d = DocumentedRoster.CreatePokrak(); var s = UnitStats.FromDefinition(d);
            Assert.AreEqual(AttackKind.Melee, d.AttackKind); Assert.AreEqual(1, d.AttackRange);
            Assert.AreEqual(25, s.EnergyMax); Assert.AreEqual(1, s.EnergyPerAttack); Assert.AreEqual(2, s.EnergyOnDamage); Assert.AreEqual(0, s.EnergyPerSecond);
            Assert.That(AbilityBalance.Description(d.BaseAbility, d), Does.Contain("20%").And.Contain("2 s").And.Contain("3×3"));
            Assert.That(AbilityBalance.Description(d.Tricks[2], d), Does.Contain("25%").And.Not.Contain("+5"));
            d.Tricks[2].Parameters = new[] { P("reduction", 31) };
            Assert.That(AbilityBalance.Description(d.Tricks[2], d), Does.Contain("31%"));
        }

        [TestCase(false, 9)] [TestCase(true, 13)]
        public void AreaAddsOnlyMidpointsNotAnOuterRing(bool expanded, int count)
        {
            var center = new Vector2Int(2, 5); var cells = CombatSimulation.PokrakBubbleCells(center, 3, expanded ? 1 : 0);
            Assert.AreEqual(count, cells.Count); Assert.AreEqual(count, cells.Distinct().Count());
            for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++)
            {
                bool expected = Math.Abs(x) <= 1 && Math.Abs(y) <= 1 || expanded && (x == 0 && Math.Abs(y) == 2 || y == 0 && Math.Abs(x) == 2);
                Assert.AreEqual(expected, cells.Contains(center + new Vector2Int(x, y)), x + "," + y);
            }
        }

        [TestCase(BoardSide.Blue, false)] [TestCase(BoardSide.Red, false)]
        [TestCase(BoardSide.Blue, true)] [TestCase(BoardSide.Red, true)]
        public void ProtectsEveryAlliedCellIncludingSelfButNotEnemiesOrOutside(BoardSide side, bool expanded)
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 5, side, expanded ? new[] { 1 } : Array.Empty<int>());
            var units = new List<BattleUnit> { caster };
            foreach (var cell in CombatSimulation.PokrakBubbleCells(new Vector2Int(2, 5), 3, 1))
                if (cell != new Vector2Int(2, 5)) units.Add(Ally(cell.x, cell.y,
                    cell == new Vector2Int(3, 5) ? (side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue) : side));
            var outside = Ally(0, 3, side); units.Add(outside);
            var enemy = Ally(0, 4, side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue); units.Add(enemy);
            Begin(units.ToArray()); BoardCell[] affected = null; sim.AreaEffect += (u,k,c) => { if (k == "pokrak-bubbles") affected = c; };
            Cast(caster); Assert.AreEqual(expanded ? 13 : 9, affected.Length);
            foreach (var unit in units)
            {
                bool expected = unit.Side == side && affected.Contains(unit.CurrentCell);
                Assert.AreEqual(expected ? .2f : 0, sim.PokrakBubbleReduction(unit), .0001f, unit.UnitId);
                Assert.AreEqual(0, unit.Shield, "Bubbles never add HP shields.");
            }
        }

        [TestCase(false, 2f)] [TestCase(true, 3f)]
        public void ExactDurationAndStatusTimerExpireTogether(bool upgraded, float duration)
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 7, BoardSide.Blue, upgraded ? new[] { 0 } : Array.Empty<int>());
            var ally = Ally(2, 6); var enemy = Ally(5, 0, BoardSide.Red); Begin(caster, ally, enemy); Cast(caster);
            var states = new List<ActiveStatus>(); sim.CollectStatuses(ally, states);
            Assert.AreEqual(duration, states.Single(s => s.Id == "pokrak-guard").Remaining, .001f);
            sim.Step(duration - .1f); Assert.AreEqual(.2f, sim.PokrakBubbleReduction(ally), .001f);
            sim.Step(.1f); Assert.AreEqual(0, sim.PokrakBubbleReduction(ally));
            sim.CollectStatuses(ally, states); Assert.IsFalse(states.Any(s => s.Id == "pokrak-guard"));
        }

        [TestCase(false, 80)] [TestCase(true, 75)]
        public void ReducesDamageByTwentyOrTwentyFivePercentBeforeHpShields(bool upgraded, int reduced)
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 7, BoardSide.Blue, upgraded ? new[] { 2 } : Array.Empty<int>());
            var ally = Ally(2, 6); var enemy = Ally(5, 0, BoardSide.Red); Begin(caster, ally, enemy); Cast(caster);
            ally.AddShield(10); Damage(enemy, ally, 100);
            Assert.AreEqual(1000 - reduced + 10, ally.CurrentHealth); Assert.AreEqual(0, ally.Shield);
            Assert.AreEqual(.2f + (upgraded ? .05f : 0), sim.PokrakBubbleReduction(ally), .001f, "A hit does not consume the bubble.");
            int reflected = (int)typeof(CombatSimulation).GetMethod("ReflectedIncoming", Private).Invoke(sim, new[] { Actor(ally), (object)100 });
            Assert.AreEqual(reduced, reflected, "Bubbles also reduce reflected damage.");
        }

        [Test] public void FollowsRecipientAfterCastButDoesNotCatchLateArrivals()
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 7); var ally = Ally(2, 6); var outsider = Ally(5, 8); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(caster, ally, outsider, enemy); Cast(caster);
            Assert.IsTrue(board.TryRepositionUnit(ally, board.GetCell(0, 3)));
            Assert.IsTrue(board.TryRepositionUnit(outsider, board.GetCell(2, 6)));
            Assert.AreEqual(.2f, sim.PokrakBubbleReduction(ally), .001f); Assert.AreEqual(0, sim.PokrakBubbleReduction(outsider));
            caster.ApplyDamage(1000); sim.Step(.1f); Assert.AreEqual(.2f, sim.PokrakBubbleReduction(ally), .001f);
        }

        [Test] public void StrongestBubbleWinsWithoutExtendingItsLifeWithWeakerCasts()
        {
            var strong = Unit(DocumentedRoster.CreatePokrak(), 2, 7, BoardSide.Blue, 2);
            var weak = Unit(DocumentedRoster.CreatePokrak(), 3, 7); var ally = Ally(2, 6); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(strong, weak, ally, enemy); Cast(strong); sim.Step(.6f); Cast(weak);
            Assert.AreEqual(.25f, sim.PokrakBubbleReduction(ally), .001f);
            Damage(enemy, ally, 100); Assert.AreEqual(925, ally.CurrentHealth, "Protection does not add to 45%.");
            sim.Step(1); Assert.AreEqual(0, sim.PokrakBubbleReduction(ally), "The losing bubble is discarded, not suspended.");
            sim.Step(1); Assert.AreEqual(0, sim.PokrakBubbleReduction(ally));
        }

        [Test] public void RefreshResetAndStopDoNotLeakProtection()
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 7); var ally = Ally(2, 6); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(caster, ally, enemy); Cast(caster); sim.Step(.6f); Cast(caster); sim.Step(1);
            Assert.AreEqual(.2f, sim.PokrakBubbleReduction(ally), .001f);
            ally.ResetForCombat(); Assert.AreEqual(0, sim.PokrakBubbleReduction(ally));
            Assert.AreEqual(.2f, sim.PokrakBubbleReduction(caster), .001f);
            sim.Stop(); Assert.AreEqual(0, sim.PokrakBubbleReduction(caster));
            sim.Begin(new[] { caster, ally, enemy }); Assert.AreEqual(0, sim.PokrakBubbleReduction(caster));
        }

        [Test] public void EditedDurationReductionAndAreaAreUsedByCombat()
        {
            var d = DocumentedRoster.CreatePokrak(); d.BaseAbility.Parameters = new[] { P("width", 1), P("reduction", 30), P("duration", 1) };
            var caster = Unit(d, 2, 7); var ally = Ally(2, 6); var enemy = Ally(5, 0, BoardSide.Red); Begin(caster, ally, enemy); Cast(caster);
            Assert.AreEqual(.3f, sim.PokrakBubbleReduction(caster), .001f); Assert.AreEqual(0, sim.PokrakBubbleReduction(ally));
            sim.Step(1); Assert.AreEqual(0, sim.PokrakBubbleReduction(caster));
        }

        [Test] public void EdgeAreaIsClippedToBoard()
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 0, 9, BoardSide.Blue, 1); var enemy = Ally(5, 0, BoardSide.Red);
            Begin(caster, enemy); BoardCell[] cells = null; sim.AreaEffect += (u,k,c) => cells = c; Cast(caster);
            Assert.AreEqual(6, cells.Length); Assert.IsTrue(cells.All(c => c != null));
        }

        [Test] public void AttackEnergyArrivesOnClawContactAndDamageGrantsTwoPerHit()
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 7); var enemy = Ally(2, 6, BoardSide.Red); Begin(caster, enemy);
            Actor(caster).GetType().GetField("NextAttack").SetValue(Actor(caster), 1);
            sim.Step(.1f); Assert.AreEqual(0, caster.Energy); sim.Step(.5f); Assert.AreEqual(1, caster.Energy); Assert.AreEqual(997, enemy.CurrentHealth);
            Damage(enemy, caster, 1); Damage(enemy, caster, 1); Assert.AreEqual(5, caster.Energy);
            caster.AddShield(10); Damage(enemy, caster, 2); Assert.AreEqual(7, caster.Energy);
        }

        [Test] public void EachSimultaneousIncomingHitGrantsEnergyAndZeroDamageDoesNot()
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 7);
            var d = new UnitDefinition { Id = "enemy", MaxHealth = 200, Damage = 2, AttackRange = 1, AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100 };
            var first = Unit(d, 2, 6, BoardSide.Red); var second = Unit(d, 3, 7, BoardSide.Red); Begin(caster, first, second);
            foreach (var enemy in new[] { first, second }) Actor(enemy).GetType().GetField("NextAttack").SetValue(Actor(enemy), 1);
            sim.Step(.3f); Assert.AreEqual(4, caster.Energy); Damage(first, caster, 0); Assert.AreEqual(4, caster.Energy);
        }

        [Test] public void DoesNotCastBelowTwentyFiveAndCanCastWithoutNearbyEnemies()
        {
            var caster = Unit(DocumentedRoster.CreatePokrak(), 2, 7); var enemy = Ally(5, 0, BoardSide.Red); Begin(caster, enemy);
            caster.AddEnergy(24); sim.Step(1); Assert.AreEqual(0, sim.PokrakBubbleReduction(caster)); Assert.AreEqual(24, caster.Energy);
            caster.AddEnergy(1); sim.Step(.4f); Assert.AreEqual(.2f, sim.PokrakBubbleReduction(caster), .001f); Assert.AreEqual(0, caster.Energy);
        }
    }
}

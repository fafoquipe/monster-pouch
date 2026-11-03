using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TokoroHymayScopeTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation sim;

        [SetUp] public void Setup()
        {
            var go = new GameObject("scope-test-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void Cleanup()
        {
            sim?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(BoardSide.Blue, .5f)] [TestCase(BoardSide.Red, .5f)]
        [TestCase(BoardSide.Blue, 4f)] [TestCase(BoardSide.Red, 4f)]
        public void NewStunDuringExistingStunCreatesExactlyOneNewBatch(BoardSide side, float secondDuration)
        {
            RunStunScenario(side, secondDuration, 1f);
        }
        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void NewStunAfterExpiryAlsoCreatesOneNewBatch(BoardSide side)
        {
            RunStunScenario(side, 2f, 3.5f);
        }
        void RunStunScenario(BoardSide side, float secondDuration, float wait)
        {
            var definition = Tune(DocumentedRoster.CreateTokoro());
            Set(definition.Tricks[1], "travelDuration", .1f);
            var tokoro = Unit(definition, "00-tokoro", 2, 5, side, 1);
            var victim = Dummy("10-headbutt-target", 2, 4, Opposite(side));
            var steinDef = Tune(DocumentedRoster.CreateStein());
            steinDef.AttackInterval = .2f;
            Set(steinDef.BaseAbility, "duration", secondDuration);
            var stein = Unit(steinDef, "20-stein", 4, 5, Opposite(side));
            Begin(tokoro, victim, stein);
            int batches = 0;
            sim.ConesLaunched += (actor, _, __, ___) => { if (actor == tokoro) batches++; };
            tokoro.AddEnergy(tokoro.BaseStats.EnergyMax);
            sim.Step(.3f);
            Assert.IsTrue(tokoro.IsStunned); Assert.AreEqual(1, batches);
            sim.Step(wait);
            Assert.AreEqual(wait < 3, tokoro.IsStunned);
            Assert.AreEqual(1, batches, "The same stun cannot create a batch each second.");
            stein.AddEnergy(stein.BaseStats.EnergyMax);
            sim.Step(.5f);
            Assert.AreEqual(2, batches, "A second incoming stun must launch exactly one new batch, even if shorter.");
            sim.Step(1.2f);
            Assert.AreEqual(2, batches);
            var cones = sim.GroundEffects.Where(e => e.Kind == "cone").ToArray();
            Assert.AreEqual(cones.Length, cones.Select(e => e.Cell).Distinct().Count());
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void FocusUsesExactTwoByTwoAreaFromEitherPerspective(BoardSide side) => AssertFocus(side, 2, 2);
        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void EditedFocusDimensionsAreUsedByCombatAndDescription(BoardSide side) => AssertFocus(side, 4, 3);
        void AssertFocus(BoardSide side, int width, int height)
        {
            var definition = Tune(DocumentedRoster.CreateHymay());
            Set(definition.Tricks[0], "width", width); Set(definition.Tricks[0], "height", height);
            StringAssert.Contains(width + " × " + height, AbilityBalance.Description(definition.Tricks[0], definition));
            int direction = side == BoardSide.Blue ? -1 : 1;
            int right = -direction;
            var hymay = Unit(definition, "00-hymay", 2, 5, side, 0);
            var near = Dummy("20-near", 2 + right, 5, side);
            var wide = Dummy("30-wide", 2 + right * 2, 5, side);
            var behind = Dummy("40-behind", 2, 5 - direction, side);
            var far = Dummy("50-far", 5, side == BoardSide.Blue ? 9 : 0, side);
            var target = Dummy("10-pulled", 2, 5 + direction * 3, Opposite(side));
            Begin(hymay, near, wide, behind, far, target);
            hymay.AddEnergy(hymay.BaseStats.EnergyMax);
            for (int i = 0; i < 20 && !HasFocus(near, target); i++) sim.Step(.1f);
            Assert.IsTrue(target.IsBeingPulled);
            Assert.IsTrue(HasFocus(near, target));
            Assert.AreEqual(width == 4, HasFocus(wide, target));
            Assert.AreEqual(height == 3, HasFocus(behind, target));
            Assert.IsFalse(HasFocus(far, target), "A distant ally must not inherit a global focus.");
        }
        bool HasFocus(BattleUnit unit, BattleUnit target)
        {
            // A normal attack can select the same enemy. Inspect forced focus separately
            // so this check cannot pass just because only one enemy exists.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var actor = typeof(CombatSimulation).GetMethod("Clock", flags).Invoke(sim, new object[] { unit });
            return ReferenceEquals(actor.GetType().GetField("FocusTarget").GetValue(actor), target);
        }
        static UnitDefinition Tune(UnitDefinition definition)
        {
            definition.MaxHealth = 1000; definition.Damage = 0;
            definition.AttackInterval = 100; definition.AttackWindup = .1f; definition.MoveInterval = 100;
            definition.EnergyPerAttack = definition.EnergyOnDamage = definition.EnergyPerSecond = 0;
            Set(definition.BaseAbility, "castDuration", .1f);
            Set(definition.BaseAbility, "holdDuration", 0); Set(definition.BaseAbility, "recoveryDuration", 0);
            return definition;
        }
        static void Set(TrickDefinition effect, string key, float value)
        {
            var values = (effect.Parameters ?? new AbilityParameter[0]).ToList();
            values.RemoveAll(p => p.Key == key); values.Add(new AbilityParameter { Key = key, Value = value });
            effect.Parameters = values.ToArray();
        }
        BattleUnit Dummy(string id, int x, int y, BoardSide side) => Unit(new UnitDefinition
        { Id = id, MaxHealth = 1000, Damage = 0, AttackRange = 9, AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100 }, id, x, y, side);
        BattleUnit Unit(UnitDefinition definition, string id, int x, int y, BoardSide side, params int[] tricks)
        {
            var owned = new OwnedUnit(definition); foreach (int trick in tricks) owned.Tricks[trick] = true;
            var go = new GameObject(id); objects.Add(go); var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        void Begin(params BattleUnit[] units) { sim = new CombatSimulation(board); sim.Begin(units); }
        static BoardSide Opposite(BoardSide side) => side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;
    }
}

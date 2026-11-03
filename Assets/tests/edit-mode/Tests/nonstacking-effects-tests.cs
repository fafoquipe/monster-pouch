using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class NonStackingEffectsTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation sim;
        [SetUp] public void Setup()
        {
            var go = new GameObject("nonstacking-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void Cleanup()
        {
            sim?.Stop();
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
        }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side = BoardSide.Blue, params int[] upgrades)
        {
            definition.MaxHealth = 1000; definition.Damage = 0;
            definition.AttackInterval = definition.MoveInterval = 100;
            var owned = new OwnedUnit(definition); foreach (int index in upgrades) owned.Tricks[index] = true;
            var go = new GameObject(definition.Id + objects.Count); objects.Add(go);
            var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        BattleUnit Enemy() => Unit(new UnitDefinition { Id = "enemy" }, 5, 0, BoardSide.Red);
        void Begin(params BattleUnit[] units)
        {
            sim = new CombatSimulation(board); sim.Begin(units);
            foreach (var unit in units) { Set(Actor(unit), "NextAttack", 9999); Set(Actor(unit), "NextMove", 9999); }
        }
        object Call(string method, params object[] args) => typeof(CombatSimulation).GetMethod(method, Private).Invoke(sim, args);
        object Actor(BattleUnit unit) => Call("Clock", unit);
        static void Set(object actor, string field, object value) => actor.GetType().GetField(field).SetValue(actor, value);
        static T Get<T>(object actor, string field) => (T)actor.GetType().GetField(field).GetValue(actor);
        static AbilityParameter P(string key, float value) => new AbilityParameter { Key = key, Value = value };

        [Test] public void BuguiKeepsItsOwnStacksPlusOnlyOneExternalSpeedBonus()
        {
            var bugui = Unit(DocumentedRoster.CreateBugui(), 2, 8); Begin(bugui, Enemy());
            var actor = Actor(bugui); Set(actor, "Attacks", 10);
            Call("ApplyAttackSpeed", actor, .3f, 30, "rose-haste");
            Call("ApplyAttackSpeed", actor, .2f, 100, "haste");
            Call("ApplyAttackSpeed", actor, .25f, 80, "rose-haste");
            Assert.AreEqual(1.8f, (float)Call("AttackSpeed", actor, true), .001f);
            sim.Step(3);
            Assert.AreEqual(1.5f, (float)Call("AttackSpeed", actor, true), .001f, "The weaker applications must never return.");
            Call("ApplyAttackSpeed", actor, .5f, int.MaxValue, "haste");
            Call("ApplyAttackSpeed", actor, .5f, int.MaxValue, "haste");
            Assert.AreEqual(2f, (float)Call("AttackSpeed", actor, true), .001f);
        }

        [Test] public void SpeedWinnerKeepsItsOwnDuration_AndEqualStrengthUsesRemainingTime()
        {
            var unit = Unit(DocumentedRoster.CreateRosie(), 2, 8); Begin(unit, Enemy()); var actor = Actor(unit);
            Call("ApplyAttackSpeed", actor, .3f, 100, "haste");
            Call("ApplyAttackSpeed", actor, .5f, 20, "rose-haste");
            Call("ApplyAttackSpeed", actor, .5f, 15, "haste");
            Assert.AreEqual(20, Get<int>(actor, "ExternalSpeedUntil"));
            sim.Step(.5f);
            Call("ApplyAttackSpeed", actor, .5f, 35, "rose-haste");
            Assert.AreEqual(35, Get<int>(actor, "ExternalSpeedUntil"));
            sim.Step(3);
            Assert.AreEqual(1f, (float)Call("AttackSpeed", actor, true));
        }

        [Test] public void BugalooKeepsItsProtectionPlusOnlyOneExternalReduction()
        {
            var bugaloo = Unit(DocumentedRoster.CreateBugaloo(), 2, 8, BoardSide.Blue, 0);
            Begin(bugaloo, Enemy()); var actor = Actor(bugaloo); Set(actor, "SuperUntil", 30);
            Call("ApplyReduction", actor, .2f, 100, "guard");
            Call("ApplyReduction", actor, .3f, 20, "pokrak-guard");
            Call("ApplyReduction", actor, .25f, 150, "guard");
            Assert.AreEqual(35, (int)Call("Incoming", actor, 100), "50% own protection plus the winning 30% external shield.");
            sim.Step(2);
            Assert.AreEqual(50, (int)Call("Incoming", actor, 100), "Only Bugaloo's own protection remains.");
            sim.Step(1);
            Assert.AreEqual(100, (int)Call("Incoming", actor, 100));
        }

        [Test] public void GumAndPokrakCompeteForOneReduction_AndDiscardedBubblesDisappear()
        {
            var pokrak = Unit(DocumentedRoster.CreatePokrak(), 2, 7);
            var ally = Unit(DocumentedRoster.CreateRosie(), 2, 8); Begin(pokrak, ally, Enemy());
            var actor = Actor(ally);
            Call("CastPokrakBubbles", Actor(pokrak)); Assert.AreEqual(.2f, sim.PokrakBubbleReduction(ally), .001f);
            Call("ApplyReduction", actor, .3f, 10, "guard");
            Assert.AreEqual(0, sim.PokrakBubbleReduction(ally));
            Call("CastPokrakBubbles", Actor(pokrak));
            Assert.AreEqual(70, (int)Call("Incoming", actor, 100));
            sim.Step(1);
            Assert.AreEqual(100, (int)Call("Incoming", actor, 100));
            Assert.AreEqual(0, sim.PokrakBubbleReduction(ally));
        }

        [TestCase(StatusStacking.LongestDuration)] [TestCase(StatusStacking.Replace)]
        [TestCase(StatusStacking.AddDuration)] [TestCase(StatusStacking.Independent)]
        public void SerializedPoliciesCannotStackControlDuration(StatusStacking policy)
        {
            var definition = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            try
            {
                definition.Id = "stun"; definition.Stacking = policy;
                var rules = new StatusRules(new[] { definition });
                Assert.AreEqual(30, rules.Apply("stun", 5, 30, 10));
                Assert.AreEqual(35, rules.Apply("stun", 5, 30, 30));
                Assert.AreEqual(35, definition.ResolveExpiry(5, 30, 30));
                Assert.AreEqual(int.MaxValue, rules.Apply("stun", int.MaxValue - 5, 0, 10));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test] public void StunsKeepOnlyTheLongestApplicationIncludingItsVisual()
        {
            var unit = Unit(DocumentedRoster.CreateRosie(), 2, 8); Begin(unit, Enemy()); var actor = Actor(unit);
            Call("Stun", actor, 30, "electric");
            sim.Step(.5f); Call("Stun", actor, 10, "popow");
            Assert.AreEqual(30, Get<int>(actor, "StunUntil"));
            Assert.IsTrue(unit.IsElectrocuted); Assert.IsFalse(unit.IsPopowStunned);
            Call("Stun", actor, 30, "popow");
            Assert.AreEqual(35, Get<int>(actor, "StunUntil"));
            Assert.IsFalse(unit.IsElectrocuted); Assert.IsTrue(unit.IsPopowStunned);
            sim.Step(3); Assert.IsFalse(unit.IsStunned);
        }

        [Test] public void WeaknessAndAntiHealNeverBorrowTheWeakerDuration()
        {
            var unit = Unit(DocumentedRoster.CreateRosie(), 2, 8); Begin(unit, Enemy()); var actor = Actor(unit);
            Call("ApplyWeakness", actor, 1.5f, 20); Call("ApplyWeakness", actor, 1.25f, 100);
            Call("ApplyAntiHeal", actor, .6f, 20); Call("ApplyAntiHeal", actor, .2f, 100);
            Assert.AreEqual(150, (int)Call("Incoming", actor, 100));
            unit.ApplyDamage(500); Call("Heal", actor, 100); Assert.AreEqual(540, unit.CurrentHealth);
            sim.Step(2);
            Assert.AreEqual(100, (int)Call("Incoming", actor, 100));
            Call("Heal", actor, 100); Assert.AreEqual(640, unit.CurrentHealth);
        }

        [Test] public void GochansExplicitlyStackableMarksKeepIndependentLifespans()
        {
            var definition = DocumentedRoster.CreateGochan();
            definition.Tricks.Single(t => t.EffectId == "gochan-burn").Parameters = new[] { P("damage", 8), P("duration", 2) };
            var gochan = Unit(definition, 2, 8, BoardSide.Blue, 0, 1, 2);
            var enemy = Enemy(); Begin(gochan, enemy);
            void Mark()
            {
                var impactType = typeof(CombatSimulation).GetNestedType("PendingImpact", BindingFlags.NonPublic);
                var impact = System.Activator.CreateInstance(impactType, true);
                Set(impact, "Source", Actor(gochan)); Set(impact, "Effect", "gochan-super"); Set(impact, "Super", true);
                Call("ApplyHitEffects", impact, Actor(enemy));
            }
            Mark(); sim.Step(1); Assert.AreEqual(992, enemy.CurrentHealth);
            Mark(); sim.Step(1); Assert.AreEqual(976, enemy.CurrentHealth);
            sim.Step(1); Assert.AreEqual(968, enemy.CurrentHealth);
            sim.Step(2); Assert.AreEqual(968, enemy.CurrentHealth, "A new mark must not renew the older one.");
        }

        [Test] public void ResetRemovesReceivedEffectsWithoutLeakingThemIntoTheNextCombat()
        {
            var unit = Unit(DocumentedRoster.CreateRosie(), 2, 8); Begin(unit, Enemy()); var actor = Actor(unit);
            Call("ApplyAttackSpeed", actor, .5f, int.MaxValue, "haste");
            Call("ApplyReduction", actor, .3f, int.MaxValue, "guard");
            unit.ResetForCombat(); sim.Step(.1f);
            Assert.AreEqual(1f, (float)Call("AttackSpeed", actor, true));
            Assert.AreEqual(100, (int)Call("Incoming", actor, 100));
        }

        [Test] public void ExpiredHealthShieldCannotAbsorbDamageDueOnItsExpiryTick()
        {
            var gochan = Unit(DocumentedRoster.CreateGochan(), 2, 8, BoardSide.Blue, 0, 1, 2);
            var enemy = Enemy(); Begin(gochan, enemy);
            enemy.AddShield(20, 10);
            var impactType = typeof(CombatSimulation).GetNestedType("PendingImpact", BindingFlags.NonPublic);
            var impact = System.Activator.CreateInstance(impactType, true);
            Set(impact, "Source", Actor(gochan)); Set(impact, "Effect", "gochan-super"); Set(impact, "Super", true);
            Call("ApplyHitEffects", impact, Actor(enemy));
            sim.Step(1);
            Assert.AreEqual(0, enemy.Shield);
            Assert.AreEqual(998, enemy.CurrentHealth, "The burn lands at the shield deadline, after expiry.");
        }
    }
}

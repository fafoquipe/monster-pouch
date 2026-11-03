using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BuguiRetargetCadenceTests
    {
        sealed class Shot
        {
            public BattleUnit Source, Target;
            public float Started, Windup, Release;
        }

        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<float> replacementImpacts = new List<float>();
        BoardManager board;
        CombatSimulation simulation;

        [SetUp] public void Setup()
        {
            var go = new GameObject("bugui-cadence-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
            shots.Clear(); replacementImpacts.Clear();
        }

        [TearDown] public void Cleanup()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        BattleUnit Unit(string name, UnitDefinition definition, int x, int y, BoardSide side, bool unlimited = false)
        {
            var go = new GameObject(name); objects.Add(go);
            var unit = go.AddComponent<WhelpUnit>();
            var owned = new OwnedUnit(definition); if (unlimited) owned.Tricks[1] = true;
            unit.Initialize(name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }

        BattleUnit Bugui(string name, int x, int y, int iq = 10, float windup = .4f, float interval = 1.1f)
        {
            var definition = DocumentedRoster.CreateBugui();
            definition.Damage = 1; definition.MaxHealth = 10000; definition.AttackRange = 3;
            definition.AttackWindup = windup; definition.AttackInterval = interval;
            definition.ProjectileSpeed = 10; definition.MoveInterval = .1f; definition.IQSpeed = iq;
            return Unit(name, definition, x, y, BoardSide.Blue, true);
        }

        BattleUnit Target(string name, int x, int y) => Unit(name, new UnitDefinition {
            Id = name, MaxHealth = 10000, Damage = 0, AttackRange = 9,
            AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100
        }, x, y, BoardSide.Red);

        void Begin(params BattleUnit[] units)
        {
            simulation = new CombatSimulation(board);
            simulation.Attacked += (source, target, projectile) => {
                float windup = CombatSimulation.GetAttackWindup(source);
                shots.Add(new Shot { Source = source, Target = target, Started = simulation.Elapsed,
                    Windup = windup, Release = simulation.Elapsed + windup });
            };
            simulation.Begin(units);
        }

        List<Shot> Shots(BattleUnit source) => shots.FindAll(shot => shot.Source == source);
        void Until(float time) { while (simulation.Elapsed + .00001f < time) simulation.Step(.1f); }

        [TestCase(1)] [TestCase(10)]
        public void DeathAfterElevenStacksKeepsLaunchCadenceWithOnlyTheRequiredIQDelay(int iq)
        {
            var bugui = Bugui("bugui-test", 1, 8, iq, .2f, 1.2f);
            var control = Bugui("bugui-control", 4, 8, iq, .2f, 1.2f);
            var original = Target("original", 1, 7); var controlTarget = Target("control-target", 4, 7);
            var next = Target("next", 2, 0);
            Begin(bugui, control, original, controlTarget, next);
            while (Shots(bugui).Count < 11 && simulation.Elapsed < 25) simulation.Step(.1f);
            Assert.AreEqual(11, Shots(bugui).Count);
            Assert.AreEqual(11, Shots(control).Count);
            Until(Shots(bugui)[10].Release + .1f);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(2, 8)));
            simulation.Impacted += (source, target, damage) => {
                if (source == bugui && target == next && damage > 0) replacementImpacts.Add(simulation.Elapsed);
            };
            original.ApplyDamage(original.CurrentHealth);
            float deathDetectedAt = simulation.Elapsed + .1f;
            simulation.Step(.1f);
            float deadline = simulation.Elapsed + 4;
            while ((Shots(bugui).Count < 13 || Shots(control).Count < 13) && simulation.Elapsed < deadline)
                simulation.Step(.1f);
            var resumed = Shots(bugui); var uninterrupted = Shots(control);
            Assert.GreaterOrEqual(resumed.Count, 13); Assert.GreaterOrEqual(uninterrupted.Count, 13);
            float expected = Mathf.Max(uninterrupted[11].Release,
                deathDetectedAt + CombatSimulation.GetTargetReactionDelay(bugui));
            Assert.AreSame(next, resumed[11].Target);
            Assert.AreEqual(expected, resumed[11].Release, .0001f);
            Assert.AreEqual(0, resumed[11].Windup, "A resumed launch must not add a second preparation after the reaction.");
            Assert.AreEqual(uninterrupted[12].Release - uninterrupted[11].Release,
                resumed[12].Release - resumed[11].Release, .0001f,
                "The following launch must retain the accumulated attack speed, including beyond ten stacks.");
            Assert.AreEqual(.2f, resumed[12].Windup, .0001f, "Only the resumed launch skips its old preparation.");
            Until(expected + .1f);
            Assert.IsNotEmpty(replacementImpacts);
            Assert.AreEqual(expected + .1f, replacementImpacts[0], .0001f, "Projectile flight must still take its normal time.");
        }

        [Test]
        public void InterruptedPreparationResumesTheSameCountedAttackWithoutAwardingAnExtraStack()
        {
            var bugui = Bugui("bugui", 2, 8); var original = Target("original", 2, 7);
            var next = Target("next", 5, 0);
            Begin(bugui, original, next); simulation.Step(.1f);
            Assert.AreSame(original, Shots(bugui)[0].Target);
            Assert.AreEqual(.5f, Shots(bugui)[0].Release, .0001f);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
            original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            Until(.4f); Assert.AreEqual(1, Shots(bugui).Count);
            Until(.5f);
            Assert.AreEqual(2, Shots(bugui).Count);
            Assert.AreEqual(0, Shots(bugui)[1].Windup);
            Assert.AreSame(next, Shots(bugui)[1].Target);
            Until(1.6f);
            Assert.GreaterOrEqual(Shots(bugui).Count, 3);
            Assert.AreEqual(1.6f, Shots(bugui)[2].Release, .0001f,
                "One stack gives a 1.1 s cycle here; counting the resumed attack twice would incorrectly give 1.0 s.");
        }

        [Test]
        public void AReplacementOutsideRangeRequiresMovementAndANormalPreparation()
        {
            var bugui = Bugui("bugui", 2, 8); var original = Target("original", 2, 7);
            var next = Target("next", 2, 2);
            Begin(bugui, original, next); int moves = 0;
            simulation.Moved += (source, from, to) => { if (source == bugui) moves++; };
            simulation.Step(.1f); original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            while (Shots(bugui).Count < 2 && simulation.Elapsed < 3) simulation.Step(.1f);
            Assert.Greater(moves, 0); Assert.AreEqual(2, Shots(bugui).Count);
            Assert.AreSame(next, Shots(bugui)[1].Target);
            Assert.AreEqual(.4f, Shots(bugui)[1].Windup, .0001f);
            Assert.AreEqual(Shots(bugui)[1].Started + .4f, Shots(bugui)[1].Release, .0001f);
        }

        [Test]
        public void ASecondDeathDuringReactionDoesNotAddAnotherPreparationToThePlannedLaunch()
        {
            var bugui = Bugui("bugui", 2, 8, 1); var original = Target("original", 2, 7);
            var next = Target("next", 5, 0); var final = Target("final", 0, 0);
            Begin(bugui, original, next, final); simulation.Step(.1f);
            Assert.AreSame(original, Shots(bugui)[0].Target);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
            original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            Assert.IsTrue(board.TryRepositionUnit(final, board.GetCell(2, 9)));
            next.ApplyDamage(next.CurrentHealth); simulation.Step(.1f);
            Until(1.2f); Assert.AreEqual(1, Shots(bugui).Count);
            Until(1.3f); Assert.AreEqual(2, Shots(bugui).Count);
            Assert.AreSame(final, Shots(bugui)[1].Target);
            Assert.AreEqual(1.3f, Shots(bugui)[1].Release, .0001f);
            Assert.AreEqual(0, Shots(bugui)[1].Windup);
        }

        [Test]
        public void ResetBeforeTheResumedLaunchRemovesTheResumeAndRestartsFullPreparation()
        {
            var bugui = Bugui("bugui", 2, 8); var original = Target("original", 2, 7);
            var next = Target("next", 5, 0);
            Begin(bugui, original, next); simulation.Step(.1f);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
            original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            Assert.AreEqual(1, Shots(bugui).Count);
            bugui.ResetForCombat(); simulation.Step(.1f);
            Assert.AreEqual(2, Shots(bugui).Count);
            Assert.AreSame(next, Shots(bugui)[1].Target);
            Assert.AreEqual(.4f, Shots(bugui)[1].Windup, .0001f);
            Assert.AreEqual(.7f, Shots(bugui)[1].Release, .0001f);
        }
    }
}

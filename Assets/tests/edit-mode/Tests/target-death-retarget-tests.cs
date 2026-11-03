using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TargetDeathRetargetTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<BattleUnit> attacked = new List<BattleUnit>();
        BoardManager board;
        CombatSimulation simulation;
        BattleUnit observed;
        int resets;

        [SetUp] public void Setup()
        {
            var go = new GameObject("target-death-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
            attacked.Clear(); resets = 0;
        }

        [TearDown] public void Cleanup()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        static UnitDefinition Definition(string id, bool documented = false, int iq = 10) => new UnitDefinition
        {
            Id = id, MaxHealth = 1000, Damage = 7, AttackRange = 1,
            AttackInterval = 20, AttackWindup = .8f, MoveInterval = .1f,
            IQSpeed = iq, UsesDocumentedRules = documented
        };

        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side = BoardSide.Red)
        {
            var go = new GameObject(definition.Id); objects.Add(go);
            var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(definition.Id, side, UnitStats.FromDefinition(definition));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }

        BattleUnit Target(string id, int x, int y, bool revives = false)
        {
            var definition = Definition(id);
            definition.Damage = 0; definition.AttackRange = 9;
            definition.AttackInterval = 100; definition.AttackWindup = .1f;
            definition.RevivesPerCombat = revives ? 1 : 0; definition.ReviveDelay = .3f;
            return Unit(definition, x, y);
        }

        void Begin(BattleUnit actor, params BattleUnit[] targets)
        {
            observed = actor;
            var units = new List<BattleUnit> { actor }; units.AddRange(targets);
            simulation = new CombatSimulation(board);
            simulation.Attacked += (source, target, projectile) => { if (source == observed) attacked.Add(target); };
            simulation.AttackReset += source => { if (source == observed) resets++; };
            simulation.Begin(units);
        }

        [TestCase(false)] [TestCase(true)]
        public void DeadTargetCancelsWindupOnceAndNewAttackRestartsItsEntireWindup(bool documented)
        {
            var actor = Unit(Definition("actor", documented), 2, 8, BoardSide.Blue);
            var original = Target("a-original", 2, 7); var next = Target("z-next", 5, 0);
            Begin(actor, original, next); simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { original }, attacked);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
            int version = actor.AttackResetVersion;
            original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            Assert.AreEqual(1, resets); Assert.AreEqual(version + 1, actor.AttackResetVersion);
            simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { original, next }, attacked);
            simulation.Step(.7f); Assert.AreEqual(1000, next.CurrentHealth);
            simulation.Step(.1f); Assert.AreEqual(993, next.CurrentHealth);
            Assert.AreEqual(1, resets, "A dead target must not reset the attacker every tick.");
        }

        [TestCase(false)] [TestCase(true)]
        public void DeadTargetReleasesMovementWithoutWaitingForAbandonedWindupOrCooldown(bool documented)
        {
            var actor = Unit(Definition("actor", documented), 2, 8, BoardSide.Blue);
            var original = Target("a-original", 2, 7); var next = Target("z-next", 2, 3);
            Begin(actor, original, next); simulation.Step(.1f);
            original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            Assert.AreEqual(1, resets); Assert.AreEqual(8, actor.CurrentCell.Y);
            simulation.Step(.1f);
            Assert.AreEqual(8, actor.CurrentCell.Y);
            Assert.AreEqual(7, actor.ReservedCell.Y, "IQ 10 must begin pursuing after 0.1 seconds, without occupying its destination early.");
            Assert.AreEqual(UnitState.Moving, actor.State);
            CollectionAssert.AreEqual(new[] { original }, attacked);
        }

        [TestCase(1, 1f)] [TestCase(5, .6f)] [TestCase(10, .1f)]
        public void IQChangesReactionDelayButNeverDelaysCancellationOrSkipsTheNewWindup(int iq, float reaction)
        {
            var actor = Unit(Definition("actor", true, iq), 2, 8, BoardSide.Blue);
            Assert.AreEqual(reaction, CombatSimulation.GetTargetReactionDelay(actor), .001f);
            var original = Target("a-original", 2, 7); var next = Target("z-next", 5, 0);
            Begin(actor, original, next); simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { original }, attacked);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
            original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            Assert.AreEqual(1, resets); Assert.AreEqual(1, attacked.Count);
            simulation.Step(reaction - .1f);
            Assert.AreEqual(1, attacked.Count, "Reaction must honor this troop's IQ.");
            simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { original, next }, attacked);
            Assert.AreEqual(1000, next.CurrentHealth);
            simulation.Step(.7f); Assert.AreEqual(1000, next.CurrentHealth);
            simulation.Step(.1f); Assert.AreEqual(993, next.CurrentHealth);
        }

        [TestCase(false)] [TestCase(true)]
        public void FastReactionStillFinishesAnExistingStepBeforeAttackingTheReplacement(bool documented)
        {
            var definition = Definition("actor", documented, 10); definition.MoveInterval = .8f;
            var actor = Unit(definition, 2, 8, BoardSide.Blue);
            var original = Target("a-original", 2, 4); var next = Target("z-next", 5, 0);
            Begin(actor, original, next); simulation.Step(.1f);
            Assert.AreEqual(new Vector2Int(2, 8), actor.CurrentCell.Coordinates);
            Assert.AreEqual(new Vector2Int(2, 7), actor.ReservedCell.Coordinates);
            Assert.AreEqual(UnitState.Moving, actor.State); Assert.IsEmpty(attacked);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 7)));
            original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f);
            Assert.AreEqual(1, resets);
            simulation.Step(.1f);
            Assert.IsEmpty(attacked, "Finishing the IQ reaction must not skip the remaining movement time.");
            simulation.Step(.5f); Assert.IsEmpty(attacked);
            simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { next }, attacked);
            Assert.AreEqual(1000, next.CurrentHealth, "The new attack must still prepare its own strike.");
        }

        [TestCase(false)] [TestCase(true)]
        public void UnrelatedEnemyDeathDoesNotInterruptAnAttackAgainstALivingTarget(bool documented)
        {
            var actor = Unit(Definition("actor", documented), 2, 8, BoardSide.Blue);
            var original = Target("a-original", 2, 7); var unrelated = Target("z-unrelated", 5, 0);
            Begin(actor, original, unrelated); simulation.Step(.1f);
            int version = actor.AttackResetVersion;
            unrelated.ApplyDamage(unrelated.CurrentHealth); simulation.Step(.8f);
            Assert.AreEqual(0, resets); Assert.AreEqual(version, actor.AttackResetVersion);
            CollectionAssert.AreEqual(new[] { original }, attacked);
            Assert.AreEqual(993, original.CurrentHealth);
        }

        [TestCase(false)] [TestCase(true)]
        public void RevivingAnuikReleasesAttackAndCannotReceiveItsAbandonedHitAfterReturning(bool documented)
        {
            var actor = Unit(Definition("actor", documented), 2, 8, BoardSide.Blue);
            var anuik = Target("anuik", 2, 7, true); var next = Target("z-next", 5, 0);
            Begin(actor, anuik, next); simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { anuik }, attacked);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
            anuik.ApplyDamage(anuik.CurrentHealth); simulation.Step(.1f);
            Assert.IsTrue(anuik.IsReviving); Assert.AreEqual(1, resets);
            simulation.Step(.9f);
            Assert.IsTrue(anuik.IsAlive); Assert.IsFalse(anuik.IsReviving);
            Assert.AreEqual(500, anuik.CurrentHealth, "The pre-death attack must not strike the revived Anuik.");
            Assert.AreEqual(993, next.CurrentHealth);
            CollectionAssert.AreEqual(new[] { anuik, next }, attacked);
            Assert.AreEqual(1, resets);
        }

        [TestCase(false)] [TestCase(true)]
        public void SlowIQKeepsItsReplacementWhenAnuikReturnsDuringReaction(bool documented)
        {
            var definition = Definition("actor", documented, 1);
            definition.TargetPolicy = TargetPolicy.LowestHealth;
            var actor = Unit(definition, 2, 8, BoardSide.Blue);
            var anuik = Target("anuik", 2, 7, true); var next = Target("z-next", 5, 0);
            Begin(actor, anuik, next);
            // LowestHealth deliberately considers reachable enemies outside range. Give
            // Anuik the unique lowest health before this troop chooses its first target.
            anuik.ApplyDamage(1); simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { anuik }, attacked);
            Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
            anuik.ApplyDamage(anuik.CurrentHealth); simulation.Step(.1f);
            simulation.Step(.9f);
            Assert.IsTrue(anuik.IsAlive); Assert.AreEqual(1, attacked.Count);
            simulation.Step(.1f);
            CollectionAssert.AreEqual(new[] { anuik, next }, attacked,
                "The revived, lower-health enemy must not steal the replacement selected during reaction.");
            simulation.Step(.8f);
            Assert.AreEqual(993, next.CurrentHealth); Assert.AreEqual(500, anuik.CurrentHealth);
        }

        [Test] public void SteinCancelsItsUnreleasedSuperWhenItsPrimaryDies()
        {
            var definition = DocumentedRoster.CreateStein();
            definition.Damage = 10; definition.AttackWindup = .1f; definition.AttackInterval = 100;
            definition.ProjectileSpeed = 1; definition.IQSpeed = 1; definition.AttackRange = 5;
            var stein = Unit(definition, 2, 8, BoardSide.Blue);
            var primary = Target("a-primary", 2, 7); var secondary = Target("z-secondary", 2, 5);
            Begin(stein, primary, secondary);stein.AddEnergy(100);simulation.Step(.2f);
            Assert.IsEmpty(attacked,"The super has its own casting animation.");
            primary.ApplyDamage(primary.CurrentHealth); simulation.Step(.1f);
            Assert.AreEqual(1, resets);
            simulation.Step(.6f);
            Assert.AreEqual(1000, secondary.CurrentHealth,
                "Canceling the cast cannot leave an invisible delayed super behind.");
        }

        [Test] public void AtongStopsTheAuthoredHammerPoseWhenItsTargetDiesAndStartsFreshForTheNext()
        {
            var actor = Unit(Definition("atong", true), 2, 8, BoardSide.Blue);
            var original = Target("a-original", 2, 7); var next = Target("z-next", 5, 0);
            var texture = new Texture2D(4, 1, TextureFormat.RGBA32, false);
            var frames = new Sprite[4];
            try
            {
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), new Vector2(.5f, 0), 1);
                var art = new UnitArt { Id = "atong", Portrait = frames[0], ReferencePixelWidth = 1 };
                for (int direction = 0; direction < 8; direction++)
                    art.DirectionalAnimations[direction] = new UnitDirectionalAnimation {
                        Idle = new[] { frames[0] }, Attack = new[] { frames[1], frames[2], frames[3] },
                        AttackContactFrame = 1
                    };
                var view = actor.gameObject.AddComponent<UnitPresentation>(); view.Configure(actor, null, art);
                Begin(actor, original, next);
                simulation.Attacked += (source, target, projectile) => {
                    if (source == actor) view.Attack(Vector3.up, projectile, CombatSimulation.GetImpactDelay(source, target));
                };
                simulation.Step(.4f); view.AdvancePresentation(.3f);
                CollectionAssert.AreEqual(new[] { original }, attacked);
                Assert.IsTrue(board.TryRepositionUnit(next, board.GetCell(3, 8)));
                Assert.IsTrue(view.IsAttacking); Assert.IsFalse(view.AttackContactReached);
                original.ApplyDamage(original.CurrentHealth); simulation.Step(.1f); view.AdvancePresentation(.01f);
                Assert.IsFalse(view.IsAttacking, "Atong must not finish winding back against a dead target.");
                Assert.AreSame(frames[0], view.Renderer.sprite);
                simulation.Step(.1f); view.AdvancePresentation(0);
                Assert.IsTrue(view.IsAttacking); Assert.IsFalse(view.AttackContactReached);
                view.AdvancePresentation(.79f); Assert.IsFalse(view.AttackContactReached);
                view.AdvancePresentation(.01f); Assert.IsTrue(view.AttackContactReached);
                Assert.AreEqual(1, resets);
            }
            finally
            {
                foreach (Sprite frame in frames) if (frame != null) Object.DestroyImmediate(frame);
                Object.DestroyImmediate(texture);
            }
        }
    }
}

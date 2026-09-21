using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class UnitSpecialAnimationTests
    {
        private GameObject actorObject;
        private MonsterUnit actor;
        private UnitPresentation view;
        private UnitArt art;
        private Texture2D texture;
        private Sprite[] frames;

        [SetUp]
        public void Setup()
        {
            actorObject = new GameObject("special-animation-test");
            actor = actorObject.AddComponent<MonsterUnit>();
            actor.Initialize("special-test", BoardSide.Blue,
                new UnitStats(100, 10, attackInterval: .595f, attackWindup: .2f));
            texture = new Texture2D(10, 1, TextureFormat.RGBA32, false);
            frames = new Sprite[10];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), new Vector2(.5f, 0), 1);
            art = new UnitArt { Id = "special-test", Portrait = frames[0], ReferencePixelWidth = 1 };
            for (int d = 0; d < 8; d++)
                art.DirectionalAnimations[d] = new UnitDirectionalAnimation
                {
                    Idle = new[] { frames[0] },
                    Attack = new[] { frames[1], frames[2], frames[3] }, AttackContactFrame = 1,
                    Death = new[] { frames[4] }, Revive = new[] { frames[5] },
                    Special = new[] { frames[6], frames[7], frames[8], frames[9] },
                    SpecialDuration = .8f, FlipX = d >= 5, SpecialFlipX = false
                };
            view = actorObject.AddComponent<UnitPresentation>();
            view.Configure(actor, null, art);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actorObject);
            foreach (Sprite frame in frames) Object.DestroyImmediate(frame);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void SpecialPlaysEveryTemporalPoseThenReturnsToIdle()
        {
            view.UseAbility("special-test");
            view.AdvancePresentation(0);
            Assert.AreSame(frames[6], view.Renderer.sprite);
            for (int frame = 7; frame <= 9; frame++)
            {
                view.AdvancePresentation(.21f);
                Assert.AreSame(frames[frame], view.Renderer.sprite);
                Assert.IsTrue(view.IsUsingAbility);
            }
            view.AdvancePresentation(.18f);
            Assert.IsFalse(view.IsUsingAbility);
            view.AdvancePresentation(0);
            Assert.AreSame(frames[0], view.Renderer.sprite);
        }

        [Test]
        public void IndependentSpecialViewsDoNotInheritTheBasicPoseMirror()
        {
            view.Face(-1, 0);
            Assert.IsTrue(view.Renderer.flipX);
            view.UseAbility();
            view.AdvancePresentation(0);
            Assert.IsFalse(view.Renderer.flipX);
            view.AdvancePresentation(.81f);
            view.AdvancePresentation(0);
            Assert.IsTrue(view.Renderer.flipX, "Returning to idle restores the basic clip mirror.");

            view.Face(1, 0);
            art.Animation(UnitFacing.East).SpecialFlipX = true;
            view.UseAbility();
            view.AdvancePresentation(0);
            Assert.IsTrue(view.Renderer.flipX, "A special can also explicitly request its own mirror.");
        }

        [Test]
        public void CombatResetCancelsSpecialAndNextAttackRestartsItsFullWindup()
        {
            view.UseAbility();
            view.Attack(Vector3.right, false, .2f);
            view.AdvancePresentation(.15f);
            actor.ResetForCombat();
            view.AdvancePresentation(0);
            Assert.IsFalse(view.IsUsingAbility);
            Assert.IsFalse(view.IsAttacking);
            Assert.AreSame(frames[0], view.Renderer.sprite);
            view.Attack(Vector3.right, false, .2f);
            view.AdvancePresentation(.1f);
            Assert.IsFalse(view.AttackContactReached);
            Assert.AreSame(frames[1], view.Renderer.sprite);
            view.AdvancePresentation(.1f);
            Assert.IsTrue(view.AttackContactReached);
            Assert.AreSame(frames[2], view.Renderer.sprite);
        }

        [TestCase("death")]
        [TestCase("reviving")]
        [TestCase("revived")]
        [TestCase("freeze")]
        public void LifeCycleTransitionCancelsSpecialWithoutResumingAnOldCast(string transition)
        {
            view.UseAbility();
            view.AdvancePresentation(.21f);
            if (transition == "death") view.Die();
            else if (transition == "reviving") view.BeginRevive(.8f);
            else if (transition == "revived") view.Revive();
            else view.Freeze();
            Assert.IsFalse(view.IsUsingAbility);
            if (transition != "revived")
            {
                view.UseAbility();
                Assert.IsFalse(view.IsUsingAbility, "An inactive actor cannot start another cast.");
            }
            view.SetFrozen(false);
            view.Revive();
            view.AdvancePresentation(.1f);
            Assert.AreSame(frames[0], view.Renderer.sprite);
            Assert.IsFalse(view.IsUsingAbility);
        }

        [Test]
        public void SpecialOverlayDoesNotDelayAttackContactRecoveryOrMutateCombatState()
        {
            UnitStats stats = actor.BaseStats;
            int health = actor.CurrentHealth;
            int resetVersion = actor.AttackResetVersion;
            UnitState state = actor.State;
            view.Attack(Vector3.right, false, .2f);
            view.UseAbility();
            view.AdvancePresentation(.199f);
            Assert.IsFalse(view.AttackContactReached);
            view.AdvancePresentation(.001f);
            Assert.IsTrue(view.AttackContactReached);
            view.AdvancePresentation(.181f);
            Assert.IsFalse(view.IsAttacking, "Special poses must not pause the attack clock.");
            Assert.IsTrue(view.IsUsingAbility, "The longer special continues independently.");
            Assert.AreSame(stats, actor.BaseStats);
            Assert.AreEqual(10, actor.BaseStats.Attack);
            Assert.That(actor.BaseStats.AttackInterval, Is.EqualTo(.595f).Within(.00001f));
            Assert.AreEqual(health, actor.CurrentHealth);
            Assert.AreEqual(0, actor.Energy);
            Assert.AreEqual(state, actor.State);
            Assert.AreEqual(resetVersion, actor.AttackResetVersion);
            Assert.AreEqual(Vector3.zero, actor.transform.position);
        }

        [Test]
        public void TimedSuperKeepsItsSpecialPoseUntilSimulationUnlocksEnergy()
        {
            actor.IsEnergyLocked = true;
            view.AdvancePresentation(.3f);
            Assert.AreSame(frames[7], view.Renderer.sprite);
            view.AdvancePresentation(.25f);
            Assert.AreSame(frames[8], view.Renderer.sprite);
            Assert.IsTrue(actor.IsEnergyLocked, "Presentation cannot end the gameplay effect.");
            actor.IsEnergyLocked = false;
            view.AdvancePresentation(0);
            Assert.AreSame(frames[0], view.Renderer.sprite);
        }
    }
}

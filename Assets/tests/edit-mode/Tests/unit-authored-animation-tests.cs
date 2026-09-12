using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class UnitAuthoredAnimationTests
    {
        private GameObject actorObject;
        private UnitPresentation view;
        private Texture2D texture;
        private Sprite[] frames;

        [SetUp]
        public void Setup()
        {
            actorObject = new GameObject("authored-animation-test");
            var actor = actorObject.AddComponent<MonsterUnit>();
            actor.Initialize("dummy", BoardSide.Blue, new UnitStats(100, 10, attackInterval: .595f, attackWindup: .2f));
            texture = new Texture2D(12, 1, TextureFormat.RGBA32, false);
            frames = new Sprite[12];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), new Vector2(.5f, 0), 1);
            var art = new UnitArt { Id = "dummy" };
            for (int d = 0; d < 8; d++)
                art.DirectionalAnimations[d] = new UnitDirectionalAnimation
                {
                    Idle = new[] { frames[0], frames[1] }, IdleFramesPerSecond = 2,
                    Move = new[] { frames[2], frames[3], frames[4], frames[3] }, MoveFramesPerSecond = 10,
                    Attack = new[] { frames[5], frames[6], frames[7] }, AttackContactFrame = 1,
                    Death = new[] { frames[8], frames[9], frames[10] },
                    Revive = new[] { frames[8], frames[11], frames[11], frames[1] }, FlipX = d >= 5
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
        public void Revive_InterruptsDeathHoldsHeadstandAndWaitsForSimulationToRestoreIdle()
        {
            view.Die();
            view.BeginRevive(.8f);
            Assert.IsFalse(view.IsDying);
            Assert.IsTrue(view.IsReviving);
            view.AdvancePresentation(.4f);
            Assert.AreSame(frames[11], view.Renderer.sprite);
            Assert.IsTrue(view.Renderer.enabled);
            view.Attack(Vector3.left, true, .2f);
            Assert.IsFalse(view.IsAttacking);
            view.AdvancePresentation(1);
            Assert.IsTrue(view.IsReviving, "Presentation must not resurrect independently of simulation.");
            view.Revive();
            Assert.IsFalse(view.IsReviving);
            Assert.IsFalse(view.IsDying);
            Assert.AreSame(frames[0], view.Renderer.sprite);
            Assert.AreEqual(1, view.Renderer.color.a);
            Assert.AreEqual(Vector3.zero, view.VisualRoot.localPosition);
        }

        [Test]
        public void AuthoredIdleAndSteps_ChangeDrawingsWithoutMovingTheVisualFootAnchor()
        {
            view.AdvancePresentation(.51f);
            Assert.AreSame(frames[1], view.Renderer.sprite);
            view.Move(Vector3.zero, Vector3.right, .4f);
            view.AdvancePresentation(.11f);
            Assert.AreSame(frames[3], view.Renderer.sprite);
            Assert.That(actorObject.transform.position.x, Is.EqualTo(.275f).Within(.00001f));
            Assert.AreEqual(Vector3.zero, view.VisualRoot.localPosition);
            Assert.AreEqual(Quaternion.identity, view.VisualRoot.localRotation);
            Assert.IsFalse(view.Renderer.flipX);
            view.Face(-1, 0);
            Assert.IsTrue(view.Renderer.flipX, "Only the symmetric west view uses its explicitly configured mirror.");
        }

        [Test]
        public void AuthoredContactFrame_AppearsAtDamageTimeAndRecoversBeforeNextAttack()
        {
            view.Attack(Vector3.left, false, .2f);
            view.AdvancePresentation(.199f);
            Assert.AreSame(frames[5], view.Renderer.sprite);
            Assert.IsFalse(view.AttackContactReached);
            view.AdvancePresentation(.001f);
            Assert.AreSame(frames[6], view.Renderer.sprite);
            Assert.IsTrue(view.AttackContactReached);
            Assert.IsTrue(view.Renderer.flipX);
            view.AdvancePresentation(.1f);
            Assert.AreSame(frames[7], view.Renderer.sprite);
            view.AdvancePresentation(.081f);
            Assert.IsFalse(view.IsAttacking);
            Assert.AreEqual(Vector3.zero, actorObject.transform.position);
            Assert.AreEqual(Quaternion.identity, view.VisualRoot.localRotation);
        }

        [Test]
        public void AuthoredDeath_UsesCrouchAndFallenDrawingsAndFinishesAfterFreeze()
        {
            view.Die();
            view.Freeze();
            view.AdvancePresentation(.25f);
            Assert.AreSame(frames[9], view.Renderer.sprite);
            view.AdvancePresentation(.19f);
            Assert.AreSame(frames[10], view.Renderer.sprite);
            Assert.IsTrue(view.Renderer.enabled);
            Assert.AreEqual(Quaternion.identity, view.VisualRoot.localRotation, "The fallen pose is drawn, not a rigid rotation of standing art.");
            Assert.AreEqual(Vector3.zero, actorObject.transform.position);
            view.AdvancePresentation(.19f);
            Assert.IsTrue(view.DeathFinished);
            Assert.IsFalse(view.Renderer.enabled);
        }

        [Test]
        public void RoundFreeze_CancelsAuthoredAttackAndHoldsTheSelectedIdleFrame()
        {
            view.Attack(Vector3.right, false, .2f);
            view.AdvancePresentation(.1f);
            view.Freeze();
            Sprite settled = view.Renderer.sprite;
            view.AdvancePresentation(1f);
            Assert.AreSame(settled, view.Renderer.sprite);
            Assert.IsFalse(view.IsAttacking);
            Assert.AreEqual(Vector3.zero, view.VisualRoot.localPosition);
        }
    }
}

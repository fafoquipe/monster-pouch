using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class UnitPresentationTimingTests
    {
        private GameObject actorObject;
        private MonsterUnit actor;
        private UnitPresentation view;

        [SetUp]
        public void Setup()
        {
            actorObject = new GameObject("presentation-timing-test");
            actor = actorObject.AddComponent<MonsterUnit>();
            actor.Initialize("timing-test", BoardSide.Blue, new UnitStats(100, 10, attackInterval: .595f, attackWindup: .2f));
            view = actorObject.AddComponent<UnitPresentation>();
            view.Configure(actor, null, new UnitArt { Id = "popow" });
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(actorObject); }

        [Test]
        public void ResetRestartsAttackAnimationFromItsWindup()
        {
            view.Attack(Vector3.right,false,.5f);view.AdvancePresentation(.4f);
            actor.ResetForCombat();view.AdvancePresentation(.01f);Assert.IsFalse(view.IsAttacking);
            view.Attack(Vector3.right,false,.5f);view.AdvancePresentation(.1f);
            Assert.IsTrue(view.IsAttacking);Assert.IsFalse(view.AttackContactReached);
        }

        [Test]
        public void EveryRosterCharacterHasProceduralFallbackForMissingClips()
        {
            foreach(var definition in MonsterPouch.Gameplay.Match.DocumentedRoster.CreateAll())
            {
                actor.transform.position=Vector3.zero;
                view.Configure(actor,null,new UnitArt{Id=definition.Id});
                view.AdvancePresentation(.2f);Assert.AreNotEqual(Vector3.one,view.VisualRoot.localScale,definition.Id+" idle");
                view.Move(Vector3.zero,Vector3.right,1);view.AdvancePresentation(.25f);
                Assert.Greater(view.VisualRoot.localPosition.y,0,definition.Id+" move");
                view.Attack(Vector3.up,definition.AttackRange>1,.2f);view.AdvancePresentation(.1f);
                Assert.IsTrue(view.IsAttacking,definition.Id+" attack");
                view.Hit();view.AdvancePresentation(.05f);Assert.Less(view.Renderer.color.g,1,definition.Id+" hit");
                view.Die();view.AdvancePresentation(.7f);Assert.IsTrue(view.DeathFinished,definition.Id+" death");
            }
        }

        [Test]
        public void MeleeContactAndRecovery_FollowImpactDelayAndFitFastCadence()
        {
            view.Attack(Vector3.right, false, .2f);
            view.AdvancePresentation(.19f);
            Assert.IsFalse(view.AttackContactReached);
            Assert.Less(view.VisualRoot.localPosition.x, .15f);
            view.AdvancePresentation(.01f);
            Assert.IsTrue(view.AttackContactReached);
            Assert.That(view.VisualRoot.localPosition.x, Is.EqualTo(.15f).Within(.00001f));
            view.AdvancePresentation(.181f);
            Assert.IsFalse(view.IsAttacking, "The upgraded Popow must recover before its next .595 second attack.");
        }

        [Test]
        public void RangedRelease_UsesQuantizedWindupRatherThanProjectileArrival()
        {
            actor.Initialize("timing-test", BoardSide.Blue, new UnitStats(100, 10, range: 3, attackInterval: .675f, attackWindup: .17f));
            view.Attack(Vector3.right * 4, true, .6f);
            view.AdvancePresentation(.2f);
            Assert.IsTrue(view.AttackContactReached, "The pose releases at the .2 second tick; the star is still in flight.");
            view.AdvancePresentation(.181f);
            Assert.IsFalse(view.IsAttacking);
        }

        [Test]
        public void BugalooLanding_AndDeathAfterRoundFreeze_DoNotMoveTheLogicalRoot()
        {
            view.Configure(actor, null, new UnitArt { Id = "bugaloo" });
            view.Attack(Vector3.right, false, .3f);
            view.AdvancePresentation(.15f);
            Assert.Greater(view.VisualRoot.localPosition.y, .2f);
            Assert.AreEqual(Vector3.zero, actor.transform.position);
            view.AdvancePresentation(.15f);
            Assert.That(view.VisualRoot.localPosition.y, Is.EqualTo(0).Within(.00001f));
            Assert.Less(view.VisualRoot.localScale.y, 1f);
            Assert.AreEqual(Vector3.zero, actor.transform.position);
            view.Die();
            view.Freeze();
            view.AdvancePresentation(.7f);
            Assert.IsTrue(view.DeathFinished);
            Assert.IsFalse(view.Renderer.enabled);
            Assert.AreEqual(Vector3.zero, actor.transform.position);
        }
    }
}

using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class EronyaWholeBodyPresentationTests
    {
        GameObject root;
        MonsterUnit actor;
        UnitPresentation view;
        UnitArt art;
        Texture2D texture;
        Sprite[] poses;

        [SetUp] public void Setup()
        {
            root = new GameObject("eronya-whole-body-presentation");
            actor = root.AddComponent<MonsterUnit>();
            actor.Initialize("test", BoardSide.Blue, new UnitStats(100, 10));
            texture = new Texture2D(4, 1);
            poses = new Sprite[4];
            for (int i = 0; i < poses.Length; i++)
                poses[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), new Vector2(.5f, 0), 1);
            art = new UnitArt { Id = "serya", Portrait = poses[0], ReferencePixelWidth = 1,
                TravelMotion = UnitTravelMotion.WholeBodyHop, WholeBodyHopUsesPoses = true };
            for (int d = 0; d < 8; d++) art.DirectionalAnimations[d] = new UnitDirectionalAnimation
            { Idle = new[] { poses[0] }, HopLeft = new[] { poses[1], poses[2], poses[3] } };
            view = root.AddComponent<UnitPresentation>();
            view.Configure(actor, null, art);
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root);
            foreach (var pose in poses) Object.DestroyImmediate(pose);
            Object.DestroyImmediate(texture);
        }

        [TestCase(0, 1, UnitFacing.North)]
        [TestCase(1, 1, UnitFacing.NorthEast)]
        [TestCase(1, 0, UnitFacing.East)]
        [TestCase(1, -1, UnitFacing.SouthEast)]
        [TestCase(0, -1, UnitFacing.South)]
        [TestCase(-1, -1, UnitFacing.SouthWest)]
        [TestCase(-1, 0, UnitFacing.West)]
        [TestCase(-1, 1, UnitFacing.NorthWest)]
        public void AuthoredTripletHasOneArcAndKeepsDurationAcrossDirections(int dx, int dy, UnitFacing facing)
        {
            Vector3 to = new Vector3(dx, dy, 0);
            const float duration = .8f;
            var profile = TileHopProfile.For(art.Id);
            string stats = JsonUtility.ToJson(actor.BaseStats);
            view.Move(Vector3.zero, to, duration);
            view.AdvancePresentation(duration * .215f);
            Assert.AreEqual(facing, view.Facing);
            Assert.AreSame(poses[1], view.Renderer.sprite);
            Assert.That(root.transform.position.sqrMagnitude, Is.LessThan(.000001f));
            view.AdvancePresentation(duration * .395f);
            Assert.AreSame(poses[2], view.Renderer.sprite);
            Assert.That(Vector3.Distance(root.transform.position, to * .5f + Vector3.up * profile.Height), Is.LessThan(.00001f));
            Assert.That(view.VisualRoot.localPosition.sqrMagnitude, Is.LessThan(.000001f), "No second elevation on the body.");
            Assert.That(Vector3.Distance(root.transform.Find("ground-shadow").position,
                to * .5f + new Vector3(0, -.035f, .02f)), Is.LessThan(.00001f));
            view.AdvancePresentation(duration * .285f);
            Assert.AreSame(poses[3], view.Renderer.sprite);
            view.AdvancePresentation(duration * .104f);
            Assert.IsTrue(view.IsMoving);
            view.AdvancePresentation(duration * .001f);
            Assert.IsFalse(view.IsMoving);
            Assert.That(Vector3.Distance(to, root.transform.position), Is.LessThan(.00001f));
            Assert.AreSame(poses[0], view.Renderer.sprite);
            Assert.AreEqual(stats, JsonUtility.ToJson(actor.BaseStats));
            Assert.IsFalse(actor.IsFlying);
        }

        [Test] public void ExistingWholeBodyAssetsIgnoreInheritedTripletsUnlessExplicitlyOptedIn()
        {
            art.Id = "kazuru";
            art.WholeBodyHopUsesPoses = false;
            view.Configure(actor, null, art);
            view.Move(Vector3.zero, Vector3.right, 1);
            view.AdvancePresentation(.61f);
            Assert.AreSame(poses[0], view.Renderer.sprite);
            Assert.IsFalse(view.IsLeftFootHop);
        }

        [TestCase(.215f)] [TestCase(.61f)] [TestCase(.895f)]
        public void DeathWithoutReservationSettlesTheArcImmediately(float phase)
        {
            view.Move(Vector3.zero, Vector3.right, 1);
            view.AdvancePresentation(phase);
            Vector3 ground = new Vector3(root.transform.position.x, 0, 0);
            view.Die();
            Assert.IsFalse(view.IsMoving);
            Assert.IsTrue(view.IsDying);
            Assert.That(Vector3.Distance(ground, root.transform.position), Is.LessThan(.00001f));
            Assert.AreEqual(Vector3.zero, view.VisualRoot.localPosition);
            view.AdvancePresentation(1);
            Assert.IsTrue(view.DeathFinished);
            Assert.That(Vector3.Distance(ground, root.transform.position), Is.LessThan(.00001f));
        }

        [Test] public void PauseResumesTheSameStepAndRoundFreezeClearsUnmappedLift()
        {
            view.Move(Vector3.zero, Vector3.right, 1);
            view.AdvancePresentation(.61f);
            Vector3 paused = root.transform.position;
            view.AdvancePresentation(0);
            Assert.AreEqual(paused, root.transform.position);
            Assert.AreSame(poses[2], view.Renderer.sprite);
            view.AdvancePresentation(.1f);
            Assert.Greater(root.transform.position.x, paused.x);
            view.SetFrozen(true);
            Assert.IsFalse(view.IsMoving);
            Assert.That(root.transform.position.y, Is.EqualTo(0).Within(.00001f));
            Vector3 frozen = root.transform.position;
            view.AdvancePresentation(1);
            Assert.AreEqual(frozen, root.transform.position);
            view.SetFrozen(false);
            view.AdvancePresentation(.1f);
            Assert.IsFalse(view.IsMoving, "No residual step after unfreezing a finished round.");
        }

        [Test] public void DirectionChangesAndInstantPlacementDoNotAccumulateLift()
        {
            Vector3 from = Vector3.zero;
            foreach (Vector3 to in new[] { Vector3.right, Vector3.right + Vector3.up, Vector3.up })
            {
                view.Move(from, to, .5f);
                view.AdvancePresentation(.305f);
                Assert.AreSame(poses[2], view.Renderer.sprite);
                view.AdvancePresentation(.195f);
                Assert.That(Vector3.Distance(to, root.transform.position), Is.LessThan(.00001f));
                from = to;
            }
            view.Move(from, Vector3.up * 2, 1);
            view.AdvancePresentation(.61f);
            Vector3 placed = Vector3.left * 3;
            view.Move(root.transform.position, placed, 0);
            view.AdvancePresentation(0);
            Assert.AreEqual(placed, root.transform.position);
            Assert.AreSame(poses[0], view.Renderer.sprite);
            Assert.IsFalse(view.IsMoving);
        }
    }
}

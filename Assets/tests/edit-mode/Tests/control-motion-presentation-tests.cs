using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class ControlMotionPresentationTests
    {
        private readonly List<Object> objects = new List<Object>();
        private BoardManager board;
        private BoardWorldMapper mapper;
        private BattleUnit actor;
        private UnitPresentation view;
        private Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            var boardObject = New("motion-presentation-board");
            board = boardObject.AddComponent<BoardManager>();
            board.BuildBoard();
            mapper = boardObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, board.transform, Vector2.one, Vector2.zero);
            var texture = new Texture2D(80, 100);
            objects.Add(texture);
            sprite = Sprite.Create(texture, new Rect(0, 0, 80, 100), new Vector2(.5f, 0), 100);
            objects.Add(sprite);
            actor = New("motion-presentation-actor").AddComponent<WhelpUnit>();
            actor.Initialize(actor.name, BoardSide.Blue, new UnitStats(100, 10));
            Assert.IsTrue(board.TryOccupyCell(actor, 2, 5));
            view = actor.gameObject.AddComponent<UnitPresentation>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(.4f)]
        [TestCase(1.2f)]
        public void HymayBackflipTucksInMidairKeepsFacingAndLandsAtConfiguredTime(float duration)
        {
            Configure("hymay");
            view.Face(0, -1);
            UnitFacing initialFacing = view.Facing;
            Vector3 from = mapper.GetWorldPosition(actor.CurrentCell);
            Vector3 to = BeginFlight(duration, 2, 7);
            view.AdvancePresentation(duration * .5f);

            Assert.IsTrue(view.IsBackflipping);
            Assert.AreEqual(initialFacing, view.Facing, "Hymay keeps facing the hooked enemy during the retreat.");
            Assert.That(Vector3.Distance(Vector3.Lerp(from, to, .5f), actor.transform.position), Is.LessThan(.0001f));
            Assert.Greater(view.VisualRoot.localPosition.y, .8f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, view.VisualRoot.localEulerAngles.z)), Is.EqualTo(180).Within(.01f));
            var legs = view.VisualRoot.Find("body-rig").GetComponent<SpriteRenderer>();
            Assert.IsTrue(legs.enabled);
            Assert.Less(legs.transform.localScale.y, .3f, "The lower body must visibly fold into the somersault.");
            Assert.AreSame(sprite.texture, legs.sprite.texture, "The tuck uses Hymay's own drawing.");
            Assert.Less(actor.transform.Find("ground-shadow").GetComponent<SpriteRenderer>().color.a, .5f);

            actor.EndFlight();
            view.AdvancePresentation(duration * .5f);
            Assert.IsFalse(view.IsBackflipping);
            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(to, actor.transform.position);
            Assert.AreEqual(initialFacing, view.Facing);
            Assert.AreEqual(0, Mathf.DeltaAngle(0, view.VisualRoot.localEulerAngles.z), .001f);
        }

        [Test]
        public void FreezingABackflipSettlesAtItsCellAndPreventsFurtherPoseChanges()
        {
            Configure("hymay");
            Vector3 destination = BeginFlight(1f, 2, 7);
            view.AdvancePresentation(.4f);
            Assert.IsTrue(view.IsBackflipping);
            view.SetFrozen(true);
            Vector3 pose = view.VisualRoot.localPosition;
            Quaternion rotation = view.VisualRoot.localRotation;
            Vector3 scale = view.VisualRoot.localScale;
            view.AdvancePresentation(2f);

            Assert.IsTrue(view.IsFrozen);
            Assert.IsFalse(view.IsBackflipping);
            Assert.AreEqual(destination, actor.transform.position);
            Assert.AreEqual(pose, view.VisualRoot.localPosition);
            Assert.AreEqual(rotation, view.VisualRoot.localRotation);
            Assert.AreEqual(scale, view.VisualRoot.localScale);
        }

        [Test]
        public void ResumingFlowerAfterTauntAcceptsTheNewResetButLaterInterruptionsStillCancelIt()
        {
            Configure("flo");
            actor.InterruptAttack();
            view.PrepareFlower(.4f);
            view.AdvancePresentation(.2f);
            Assert.IsTrue(view.IsUsingAbility,"A resumed summon must not be cancelled by the reset that preceded it.");
            actor.InterruptAttack();
            view.AdvancePresentation(0);
            Assert.IsFalse(view.IsUsingAbility,"A later genuine interruption must still cancel the presentation.");
        }

        [Test]
        public void AkyKeepsItsExistingFlightPoseWithoutHymaysTuckOrSpin()
        {
            Configure("aky");
            Vector3 destination = BeginFlight(1f, 2, 0);
            view.AdvancePresentation(.5f);
            Assert.IsFalse(view.IsBackflipping);
            Assert.IsTrue(view.IsMoving);
            Assert.That(view.VisualRoot.localPosition.y, Is.EqualTo(.42f).Within(.001f));
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0, view.VisualRoot.localEulerAngles.z)), 5);
            Assert.IsFalse(view.VisualRoot.Find("body-rig").GetComponent<SpriteRenderer>().enabled);
            actor.EndFlight();
            view.AdvancePresentation(.5f);
            Assert.AreEqual(destination, actor.transform.position);
            Assert.IsFalse(view.IsMoving);
        }

        private Vector3 BeginFlight(float duration, int x, int y)
        {
            Vector3 from = mapper.GetWorldPosition(actor.CurrentCell);
            Assert.IsTrue(board.TryRepositionUnit(actor, board.GetCell(x, y)));
            Vector3 to = mapper.GetWorldPosition(actor.CurrentCell);
            actor.BeginFlight(duration);
            view.Move(from, to, .1f);
            return to;
        }

        private void Configure(string id)
        {
            var art = new UnitArt { Id = id, Portrait = sprite, ReferencePixelWidth = 80, WorldWidth = 1 };
            for (int i = 0; i < art.DirectionalAnimations.Length; i++)
                art.DirectionalAnimations[i] = new UnitDirectionalAnimation { Idle = new[] { sprite }, Special = new[] { sprite } };
            view.Configure(actor, mapper, art);
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            objects.Add(go);
            return go;
        }
    }
}

using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class KazuruWholeBodyHopTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        BoardWorldMapper mapper;
        MonsterUnit actor;
        UnitPresentation view;
        UnitArt art;

        [SetUp] public void Setup()
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/resources/MonsterPouch/Characters/kazuru/Character.asset");
            Assert.IsNotNull(character);
            art = character.Visuals;
            var boardObject = New("kazuru-hop-board");
            board = boardObject.AddComponent<BoardManager>();
            board.BuildBoard();
            mapper = boardObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, board.transform, new Vector2(1.4f, -1.2f), Vector2.zero);
            var definition = character.CreateRuntimeDefinition();
            actor = New("kazuru-hop-actor").AddComponent<MonsterUnit>();
            actor.Initialize("kazuru", BoardSide.Blue, UnitStats.FromDefinition(definition, new OwnedUnit(definition)));
            Assert.IsTrue(board.TryOccupyCell(actor, 2, 5));
            view = actor.gameObject.AddComponent<UnitPresentation>();
            view.Configure(actor, mapper, art);
        }

        GameObject New(string name)
        {
            var go = new GameObject(name);
            objects.Add(go);
            return go;
        }

        [TearDown] public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(0, -1, UnitFacing.North)]
        [TestCase(1, -1, UnitFacing.NorthEast)]
        [TestCase(1, 0, UnitFacing.East)]
        [TestCase(1, 1, UnitFacing.SouthEast)]
        [TestCase(0, 1, UnitFacing.South)]
        [TestCase(-1, 1, UnitFacing.SouthWest)]
        [TestCase(-1, 0, UnitFacing.West)]
        [TestCase(-1, -1, UnitFacing.NorthWest)]
        public void CurrentArtworkHopsAsOneBodyWithoutChangingArrivalOrOccupancy(int dx, int dy, UnitFacing facing)
        {
            string stats = JsonUtility.ToJson(actor.BaseStats);
            string artwork = JsonUtility.ToJson(art);
            var origin = actor.CurrentCell;
            var destination = board.GetCell(2 + dx, 5 + dy);
            Vector3 from = mapper.GetWorldPosition(origin), to = mapper.GetWorldPosition(destination);
            float duration = actor.BaseStats.MoveInterval;
            Assert.IsTrue(board.TryReserveCell(actor, destination));
            view.Move(from, to, duration);
            view.AdvancePresentation(0);
            float fullHeight = view.VisualRoot.localScale.y;

            // These are the pre-change travel phases, deliberately independent of the new profile.
            view.AdvancePresentation(duration * .215f);
            Assert.That(Vector3.Distance(from, actor.transform.position), Is.LessThan(.00001f));
            Assert.Less(view.VisualRoot.localScale.y, fullHeight, "A readable whole-body compression prepares the jump.");
            AssertWholeBodyPose(facing);

            view.AdvancePresentation(duration * .395f);
            Vector3 midpoint = Vector3.Lerp(from, to, .5f);
            Assert.That(actor.transform.position.x, Is.EqualTo(midpoint.x).Within(.00001f));
            Assert.Greater(actor.transform.position.y - midpoint.y, .12f * Mathf.Abs(mapper.CellSize.y),
                "The body must visibly clear the old shallow arc, including on diagonal routes.");
            Assert.Greater(view.VisualRoot.localScale.y, fullHeight);
            AssertWholeBodyPose(facing);
            Assert.AreSame(origin, actor.CurrentCell);
            Assert.AreSame(actor, origin.OccupiedBy);
            Assert.AreSame(destination, actor.ReservedCell);
            Assert.AreSame(actor, destination.ReservedBy);

            view.AdvancePresentation(duration * .285f);
            Assert.That(Vector3.Distance(to, actor.transform.position), Is.LessThan(.00001f));
            Assert.Less(view.VisualRoot.localScale.y, fullHeight, "The landing has its own compression.");
            AssertWholeBodyPose(facing);
            view.AdvancePresentation(duration * .104f);
            Assert.IsTrue(view.IsMoving, "The visual change must not shorten a tile step.");
            view.AdvancePresentation(duration * .001f);
            Assert.IsFalse(view.IsMoving, "The visual change must not extend a tile step.");
            Assert.That(Vector3.Distance(to, actor.transform.position), Is.LessThan(.00001f));
            Assert.AreSame(origin, actor.CurrentCell, "Presentation cannot commit a simulation reservation.");
            Assert.IsTrue(board.ConfirmMove(actor));
            view.AdvancePresentation(0);
            Assert.AreSame(destination, actor.CurrentCell);
            Assert.IsNull(actor.ReservedCell);
            Assert.AreEqual(stats, JsonUtility.ToJson(actor.BaseStats));
            Assert.AreEqual(artwork, JsonUtility.ToJson(art));
        }

        void AssertWholeBodyPose(UnitFacing facing)
        {
            Assert.AreEqual(facing, view.Facing);
            Assert.AreSame(art.Idle(facing), view.Renderer.sprite,
                "Travel retains Kazuru's current complete directional pose, without alternating raised-leg frames.");
            Assert.AreEqual(art.Animation(facing).FlipX, view.Renderer.flipX);
            Assert.IsTrue(view.Renderer.enabled);
            foreach (string name in new[] { "body-rig", "left-foot-rig", "right-foot-rig" })
                Assert.IsFalse(view.VisualRoot.Find(name).GetComponent<SpriteRenderer>().enabled, name);
        }

        [Test] public void ConsecutiveTilesKeepOnePoseAndCancellingAReservationStopsTheHop()
        {
            for (int step = 0; step < 2; step++)
            {
                var destination = board.GetCell(3 + step, 5);
                Vector3 from = mapper.GetWorldPosition(actor.CurrentCell), to = mapper.GetWorldPosition(destination);
                Assert.IsTrue(board.TryReserveCell(actor, destination));
                view.Move(from, to, actor.BaseStats.MoveInterval);
                view.AdvancePresentation(actor.BaseStats.MoveInterval * .61f);
                AssertWholeBodyPose(UnitFacing.East);
                if (step == 0)
                {
                    Assert.IsTrue(board.ConfirmMove(actor));
                    view.AdvancePresentation(actor.BaseStats.MoveInterval * .39f);
                    Assert.IsFalse(view.IsMoving);
                }
                else
                {
                    Assert.IsTrue(board.CancelReservation(actor));
                    view.AdvancePresentation(.01f);
                    Assert.IsFalse(view.IsMoving);
                    Assert.That(Vector3.Distance(from, actor.transform.position), Is.LessThan(.00001f));
                }
            }
        }

        [Test] public void FallbackCatalogUsesTheSameWholeBodyMotionAndBasicAttackKeepsItsClock()
        {
            var fallback = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>("Assets/resources/MonsterPouch/UnitArt.asset").Get("kazuru");
            Assert.AreEqual(UnitTravelMotion.WholeBodyHop, fallback.TravelMotion);
            Assert.AreEqual(fallback.TravelMotion, art.TravelMotion);
            float windup = CombatSimulation.GetAttackWindup(actor);
            view.Attack(Vector3.right * 4, false, windup);
            view.AdvancePresentation(windup - .001f);
            Assert.IsFalse(view.AttackContactReached);
            view.AdvancePresentation(.001f);
            Assert.IsTrue(view.AttackContactReached);
            var clip = art.Animation(view.Facing);
            Assert.AreSame(clip.Attack[clip.AttackContactFrame], view.Renderer.sprite);
            view.AdvancePresentation(actor.BaseStats.AttackHold + actor.BaseStats.AttackRecovery + .001f);
            Assert.IsFalse(view.IsAttacking);
        }
    }
}

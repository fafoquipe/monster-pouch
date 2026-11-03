using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class EronyaDisplacementPresentationTests
    {
        GameObject boardObject, unitObject;
        BoardManager board;
        BoardWorldMapper mapper;
        MonsterUnit actor;
        UnitPresentation view;
        UnitArt art;
        BoardCell from, to;
        Texture2D texture;
        Sprite[] poses;

        [SetUp] public void Setup()
        {
            boardObject = new GameObject("displacement-board");
            board = boardObject.AddComponent<BoardManager>(); board.BuildBoard();
            mapper = boardObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, board.transform, new Vector2(1.4f, -1.2f), Vector2.zero);
            unitObject = new GameObject("displacement-unit");
            actor = unitObject.AddComponent<MonsterUnit>();
            actor.Initialize("test", BoardSide.Blue, new UnitStats(100, 10));
            from = board.GetCell(2, 5); to = board.GetCell(3, 3);
            Assert.IsTrue(board.TryOccupyCell(actor, from));
            texture = new Texture2D(7, 1); poses = new Sprite[7];
            for (int i = 0; i < poses.Length; i++)
                poses[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), new Vector2(.5f, 0), 1);
            art = new UnitArt { Id = "dagata", Portrait = poses[0], ReferencePixelWidth = 1, VitalsHeadAnchor = Vector2.up,
                TravelMotion = UnitTravelMotion.WholeBodyHop, WholeBodyHopUsesPoses = true };
            for (int i = 0; i < 8; i++) art.DirectionalAnimations[i] = new UnitDirectionalAnimation
            {
                Idle = new[] { poses[0] }, HopLeft = new[] { poses[1], poses[2], poses[3] },
                OpeningJump = new[] { poses[1], poses[2], poses[3] },
                Tunnel = new[] { poses[4], poses[5], poses[6] }
            };
            view = unitObject.AddComponent<UnitPresentation>(); view.Configure(actor, mapper, art);
            view.SnapToCell();
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(unitObject); Object.DestroyImmediate(boardObject);
            foreach (var pose in poses) Object.DestroyImmediate(pose);
            Object.DestroyImmediate(texture);
        }

        CombatDisplacementSnapshot Commit(CombatDisplacementKind kind, long id = 1, bool instant = true)
        {
            Assert.IsTrue(board.TryRepositionUnit(actor, to));
            return new CombatDisplacementSnapshot(actor, from, to, kind, id, 0, instant ? 0 : 6, instant);
        }

        [TestCase(CombatDisplacementKind.OpeningJump, 2, 1)]
        public void OpeningMotionKeepsCommittedRootAndOccupancyWhileOnlyDrawingTravels(
            CombatDisplacementKind kind, int frame, int verticalSign)
        {
            string stats = JsonUtility.ToJson(actor.BaseStats);
            var motion = Commit(kind);
            view.PresentDisplacement(motion, 1);
            view.AdvancePresentation(.61f);
            Vector3 destination = mapper.GetWorldPosition(to);
            Vector3 ground = Vector3.Lerp(mapper.GetWorldPosition(from), destination, .5f);
            Assert.AreEqual(destination, unitObject.transform.position);
            Assert.AreSame(to, actor.CurrentCell); Assert.AreSame(actor, to.OccupiedBy);
            Assert.IsTrue(view.TryGetPresentationAnchors(out var anchors));
            Assert.AreSame(to, anchors.OccupiedCell);
            Assert.AreEqual(destination, anchors.OccupiedCellWorldBounds.center);
            Assert.That(Vector3.Distance(ground, anchors.GroundWorldPosition), Is.LessThan(.0001f));
            Assert.AreEqual(view.VisualRoot.position, anchors.FootPivotWorldPosition);
            Assert.IsNull(from.OccupiedBy); Assert.IsNull(actor.ReservedCell);
            Assert.IsFalse(view.IsMoving); Assert.IsTrue(view.IsPresentingDisplacement);
            Assert.AreSame(poses[frame], view.Renderer.sprite);
            Assert.That((view.VisualRoot.position.y - ground.y) * verticalSign, Is.GreaterThan(0));
            Assert.That(Vector3.Distance(unitObject.transform.Find("ground-shadow").position,
                ground + new Vector3(0, -.035f, .02f)), Is.LessThan(.0001f));
            Assert.IsFalse(actor.IsFlying); Assert.IsFalse(actor.IsInvisible);
            Assert.AreEqual(stats, JsonUtility.ToJson(actor.BaseStats));
            view.AdvancePresentation(.39f);
            Assert.IsFalse(view.IsPresentingDisplacement);
            Assert.AreEqual(Vector3.zero, view.VisualRoot.localPosition);
        }

        [Test] public void TimedTunnelUsesCoreTravelClockOutsideTheOpeningWindow()
        {
            art.Id="la-minera";
            var motion=Commit(CombatDisplacementKind.Tunnel,instant:false);
            actor.BeginFlight(.6f);view.PresentDisplacement(motion,0);
            Assert.IsTrue(view.IsPresentingDisplacement);Assert.AreSame(poses[4],view.Renderer.sprite);
            Vector3 preparation=view.VisualRoot.position;
            view.AdvancePresentation(9);
            Assert.AreEqual(preparation,view.VisualRoot.position,"Rendering cannot advance the Core tunnel clock.");
            actor.AdvanceFlight(.3f);view.AdvancePresentation(0);
            Assert.AreSame(poses[5],view.Renderer.sprite);
            Assert.IsTrue(actor.IsFlying);Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(mapper.GetWorldPosition(to),unitObject.transform.position);
            Assert.AreSame(actor,to.OccupiedBy);Assert.IsNull(from.OccupiedBy);
            actor.AdvanceFlight(.25f);view.AdvancePresentation(0);
            Assert.AreSame(poses[6],view.Renderer.sprite);
            actor.AdvanceFlight(.05f);view.AdvancePresentation(0);
            Assert.IsFalse(view.IsPresentingDisplacement);Assert.IsFalse(actor.IsFlying);
            Assert.AreSame(poses[0],view.Renderer.sprite);Assert.AreEqual(Vector3.zero,view.VisualRoot.localPosition);
        }

        [Test] public void InstantTunnelDoesNotInventAnOpeningWindowDelay()
        {
            view.PresentDisplacement(Commit(CombatDisplacementKind.Tunnel),9);
            Assert.IsFalse(view.IsPresentingDisplacement);Assert.IsFalse(actor.IsFlying);
            Assert.AreEqual(mapper.GetWorldPosition(to),unitObject.transform.position);
        }

        [TestCase(false)][TestCase(true)]
        public void TimedTunnelEndsWithCoreCancellationOrReset(bool reset)
        {
            var motion=Commit(CombatDisplacementKind.Tunnel,instant:false);
            actor.BeginFlight(.6f);view.PresentDisplacement(motion,9);
            actor.AdvanceFlight(.3f);view.AdvancePresentation(0);Assert.IsTrue(view.IsPresentingDisplacement);
            if(reset)actor.ResetForCombat();else actor.EndFlight();
            view.AdvancePresentation(0);Assert.IsFalse(view.IsPresentingDisplacement);
            Assert.AreEqual(mapper.GetWorldPosition(to),unitObject.transform.position);
            Assert.AreSame(actor,to.OccupiedBy);Assert.AreSame(poses[0],view.Renderer.sprite);
            Assert.AreEqual(Vector3.zero,view.VisualRoot.localPosition);
        }

        [Test] public void MissingOpeningWindowAndInstantForceOrTeleportNeverBorrowMoveInterval()
        {
            foreach (var kind in new[] { CombatDisplacementKind.OpeningJump, CombatDisplacementKind.Tunnel,
                CombatDisplacementKind.ForcedPush, CombatDisplacementKind.Teleport })
            {
                Assert.IsTrue(board.TryRepositionUnit(actor, from));
                var motion = Commit(kind, (long)kind + 1);
                view.PresentDisplacement(motion, 0);
                Assert.IsFalse(view.IsPresentingDisplacement);
                Assert.IsFalse(view.IsMoving);
                Assert.AreEqual(mapper.GetWorldPosition(to), unitObject.transform.position);
            }
        }

        [Test] public void DuplicateAndStaleEventsCannotRestartAnOpeningMotion()
        {
            var motion = Commit(CombatDisplacementKind.OpeningJump);
            view.PresentDisplacement(motion, 1); view.AdvancePresentation(.61f);
            Vector3 airborne = view.VisualRoot.position;
            view.PresentDisplacement(motion, 1);
            Assert.AreEqual(airborne, view.VisualRoot.position);
            actor.ResetForCombat();
            view.AdvancePresentation(0);
            Assert.IsFalse(view.IsPresentingDisplacement);
            view.PresentDisplacement(motion, 1);
            Assert.IsFalse(view.IsPresentingDisplacement);
            Assert.AreEqual(Vector3.zero, view.VisualRoot.localPosition);
        }

        [TestCase("root")] [TestCase("stun")] [TestCase("attack")]
        [TestCase("death")] [TestCase("freeze")] [TestCase("disable")]
        public void InterruptionRemovesOnlyTheCosmeticRoute(string reason)
        {
            // This EditMode fixture must opt its temporary instance into Unity callbacks.
            // Production UnitPresentation remains a normal PlayMode behaviour.
            if (reason == "disable") view.runInEditMode = true;
            var motion = Commit(CombatDisplacementKind.OpeningJump);
            view.PresentDisplacement(motion, 1); view.AdvancePresentation(.61f);
            if (reason == "root") typeof(BattleUnit).GetProperty("IsRooted").SetValue(actor, true);
            if (reason == "stun") typeof(BattleUnit).GetProperty("IsStunned").SetValue(actor, true);
            if (reason == "attack") view.Attack(mapper.GetWorldPosition(to) + Vector3.right, false, .3f);
            if (reason == "death") view.Die();
            if (reason == "freeze") view.SetFrozen(true);
            if (reason == "disable")
            {
                Assert.IsTrue(view.IsPresentingDisplacement, "A live cosmetic route must exist before disable.");
                unitObject.SetActive(false);
                Assert.IsFalse(view.IsPresentingDisplacement, "Unity OnDisable must cancel the route synchronously.");
                unitObject.SetActive(true);
            }
            view.AdvancePresentation(0);
            Assert.IsFalse(view.IsPresentingDisplacement, reason);
            Assert.AreSame(to, actor.CurrentCell, reason);
            Assert.AreSame(actor, to.OccupiedBy, reason);
            Assert.AreEqual(mapper.GetWorldPosition(to), unitObject.transform.position, reason);
            if (reason == "root")
            {
                view.Attack(mapper.GetWorldPosition(to) + Vector3.right, false, .3f);
                view.AdvancePresentation(.3f);
                Assert.IsTrue(view.AttackContactReached, "Root must not block a legal attack event.");
            }
        }

        [Test] public void PauseKeepsPhaseAndTimedPushHasNoArc()
        {
            var motion = Commit(CombatDisplacementKind.ForcedPush, instant: false);
            view.PresentDisplacement(motion); view.AdvancePresentation(.3f);
            Vector3 middle = Vector3.Lerp(mapper.GetWorldPosition(from), mapper.GetWorldPosition(to), .5f);
            Assert.That(Vector3.Distance(middle, view.VisualRoot.position), Is.LessThan(.0001f));
            view.AdvancePresentation(0);
            Assert.That(Vector3.Distance(middle, view.VisualRoot.position), Is.LessThan(.0001f));
            view.AdvancePresentation(.3f);
            Assert.IsFalse(view.IsPresentingDisplacement);
        }

        [Test] public void BodyReferenceIgnoresWeaponBoundsAndAttackSquash()
        {
            Assert.IsTrue(view.TryGetPresentationAnchors(out var resting));
            Assert.IsTrue(resting.HasCalibratedBodyReference);
            var weapon = Sprite.Create(texture, new Rect(0, 0, 7, 1), Vector2.zero, 1);
            try
            {
                view.Attack(mapper.GetWorldPosition(from) + Vector3.right, false, .3f);
                view.AdvancePresentation(.3f);
                view.Renderer.sprite = weapon;
                Assert.IsTrue(view.TryGetPresentationAnchors(out var attacking));
                Assert.AreEqual(resting.ReferenceBodyWorldSize, attacking.ReferenceBodyWorldSize);
                Assert.AreEqual(view.Renderer.sortingOrder, attacking.BodySortingOrder);
                Assert.AreEqual(view.Renderer.sortingLayerID, attacking.BodySortingLayerId);
                Assert.AreEqual(actor.CombatResetVersion, attacking.UnitResetVersion);
                Assert.AreEqual(new Vector3(1.4f, 1.2f, 0), attacking.OccupiedCellWorldBounds.size);
            }
            finally { Object.DestroyImmediate(weapon); }
        }

        [Test] public void VisualFlyerHasGroundedAnchorWhileFeetAndHeadHover()
        {
            art.Id = "alveyr"; art.TravelMotion = UnitTravelMotion.Flying;
            view.Configure(actor, mapper, art); view.SnapToCell(); view.AdvancePresentation(.1f);
            Assert.IsTrue(view.TryGetPresentationAnchors(out var anchors));
            Assert.AreEqual(mapper.GetWorldPosition(from), anchors.GroundWorldPosition);
            Assert.Greater(anchors.FootPivotWorldPosition.y, anchors.GroundWorldPosition.y);
            Assert.AreEqual(view.VitalsHeadWorldPosition, anchors.HeadWorldPosition);
            Assert.IsFalse(actor.IsFlying);
        }

        [Test] public void MissingBodyCalibrationIsExplicitAndUnmappedPreviewHasNoCellAnchor()
        {
            art.VitalsHeadAnchor = Vector2.zero;
            Assert.IsTrue(view.TryGetPresentationAnchors(out var anchors));
            Assert.IsFalse(anchors.HasCalibratedBodyReference);
            Assert.AreEqual(Vector2.zero, anchors.ReferenceBodyWorldSize);
            view.Configure(actor, null, art);
            Assert.IsFalse(view.TryGetPresentationAnchors(out _));
        }
    }
}

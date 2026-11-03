using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class UnitWalkShadowTests
    {
        private GameObject actorObject;
        private MonsterUnit actor;
        private UnitPresentation view;
        private UnitArt art;
        private Texture2D texture;
        private Sprite idle, firstStep, secondStep;

        [SetUp]
        public void SetUp()
        {
            actorObject=new GameObject("walk-shadow-test");
            actor=actorObject.AddComponent<MonsterUnit>();
            actor.Initialize("test",BoardSide.Blue,new UnitStats(100,10));
            texture=new Texture2D(3,1);
            idle=Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(.5f,0),1);
            firstStep=Sprite.Create(texture,new Rect(1,0,1,1),new Vector2(.5f,0),1);
            secondStep=Sprite.Create(texture,new Rect(2,0,1,1),new Vector2(.5f,0),1);
            art=new UnitArt{Id="bugaloo",Portrait=idle,ReferencePixelWidth=1};
            for(int i=0;i<8;i++)art.DirectionalAnimations[i]=new UnitDirectionalAnimation
            {
                Idle=new[]{idle},Move=new[]{firstStep,secondStep},MoveFramesPerSecond=8
            };
            view=actorObject.AddComponent<UnitPresentation>();
            view.Configure(actor,null,art);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(idle);Object.DestroyImmediate(firstStep);Object.DestroyImmediate(secondStep);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void HopHoldsTheIdlePoseAndKeepsFacingItsTravel()
        {
            view.Move(Vector3.zero,Vector3.right,1);
            view.Face(-1,0); // A target search must not turn the walking body backwards.
            view.AdvancePresentation(.18f);
            Assert.AreEqual(0,actor.transform.position.y,"Preparation is planted, before the short flight.");
            view.AdvancePresentation(.44f);
            Assert.AreEqual(UnitFacing.East,view.Facing);
            Assert.AreSame(idle,view.Renderer.sprite);
            Assert.Greater(actor.transform.position.y,0);
            Assert.Less(Quaternion.Angle(Quaternion.identity,view.Renderer.transform.localRotation),2);
            Assert.AreEqual(Vector3.zero,view.VisualRoot.localPosition,"Gait must not move the shared foot anchor.");
            Assert.IsFalse(view.IsAttacking);
        }

        [Test]
        public void ConsecutiveTilesEachHaveTheirOwnHopAndGroundedLanding()
        {
            view.Move(Vector3.zero,Vector3.right,.13f);
            view.AdvancePresentation(.13f);
            Assert.That(actor.transform.position.y,Is.EqualTo(0).Within(.00001f));
            view.Move(Vector3.right,Vector3.right*2,.13f);
            view.AdvancePresentation(.08f);
            Assert.AreSame(idle,view.Renderer.sprite);
            Assert.Greater(actor.transform.position.y,0,"The second tile has a new airborne arc.");
            Assert.Less(actor.transform.position.y,.1f,"A short-legged character makes a low hop.");
        }

        [Test]
        public void WalkOnlyMirrorDoesNotAffectHopIdleOrAttackFacing()
        {
            art.Animation(UnitFacing.East).MoveFlipX=true;
            art.Animation(UnitFacing.East).Attack=new[]{firstStep,secondStep};
            view.Move(Vector3.zero,Vector3.right,.4f);
            view.AdvancePresentation(.1f);
            Assert.IsFalse(view.Renderer.flipX);
            view.AdvancePresentation(.3f);
            Assert.IsFalse(view.Renderer.flipX);
            view.Attack(Vector3.right*2,false,.2f);
            view.AdvancePresentation(.1f);
            Assert.IsFalse(view.Renderer.flipX);
        }

        [Test]
        public void KayonSummonUsesTheExistingWoodenPropThroughIdleAttackAndDeath()
        {
            art.Id = "dummy";
            actor.Initialize("kayon/dummy-1", BoardSide.Blue, new UnitStats(14, 2), true);
            view.Configure(actor, null, art);
            Sprite wood = view.Renderer.sprite;
            Assert.AreEqual("kayon-wooden-dummy", wood.name);
            Assert.AreSame(Resources.Load<Texture2D>("MonsterPouch/UI/LobbyRigs/props"), wood.texture);
            Assert.AreEqual(0, wood.pivot.y, "The legless wooden base rests on the board.");
            Assert.AreSame(idle, art.Portrait, "Summons must not overwrite the source catalog.");
            foreach (UnitFacing direction in System.Enum.GetValues(typeof(UnitFacing)))
            {
                float angle = (int)direction * Mathf.PI / 4;
                view.Face(Mathf.RoundToInt(Mathf.Sin(angle)), -Mathf.RoundToInt(Mathf.Cos(angle)));
                view.AdvancePresentation(.1f);
                Assert.AreSame(wood, view.Renderer.sprite);
            }
            view.Attack(Vector3.right, false, .2f);
            view.AdvancePresentation(.2f);
            Assert.AreSame(wood, view.Renderer.sprite, "Attack must not restore the old white body.");
            view.Die();
            view.AdvancePresentation(.2f);
            Assert.AreSame(wood, view.Renderer.sprite);
            Assert.AreEqual(14, actor.CurrentHealth, "Presentation cannot damage the summon.");
        }

        [Test]
        public void WoodenSummonAlternatesSidewaysHopsAndLandsOnTheExactTile()
        {
            art.Id = "dummy";
            actor.Initialize("kayon/dummy-1", BoardSide.Blue, new UnitStats(14, 2), true);
            view.Configure(actor, null, art);
            var shadow = actorObject.transform.Find("ground-shadow").GetComponent<SpriteRenderer>();
            float groundY = shadow.transform.position.y;
            float midpoint = TileHopProfile.For("dummy").Prepare + TileHopProfile.For("dummy").Air * .5f;
            view.Move(Vector3.zero, Vector3.right, 1);
            view.AdvancePresentation(midpoint);
            float firstSway = view.VisualRoot.localPosition.x;
            float firstTilt = Mathf.DeltaAngle(0, view.VisualRoot.localEulerAngles.z);
            Assert.Greater(firstSway, .04f);
            Assert.Greater(firstTilt, 10f);
            Assert.That(shadow.transform.position.y, Is.EqualTo(groundY).Within(.00001f));
            view.AdvancePresentation(1 - midpoint);
            Assert.AreEqual(Vector3.right, actor.transform.position);
            Assert.That(view.VisualRoot.localPosition.x, Is.EqualTo(0).Within(.00001f));
            view.Move(Vector3.right, Vector3.right * 2, 1);
            view.AdvancePresentation(midpoint);
            Assert.Less(view.VisualRoot.localPosition.x * firstSway, 0, "Each new hop leans to the other side.");
            Assert.Less(Mathf.DeltaAngle(0, view.VisualRoot.localEulerAngles.z) * firstTilt, 0);
            Assert.That(shadow.transform.position.y, Is.EqualTo(groundY).Within(.00001f));
            view.AdvancePresentation(1 - midpoint);
            Assert.AreEqual(Vector3.right * 2, actor.transform.position);
            Assert.IsFalse(view.IsMoving);
        }

        [Test]
        public void OtherSummonsKeepTheirOwnArt()
        {
            art.Id = "flower";
            actor.Initialize("flo/flower-1", BoardSide.Blue, new UnitStats(20, 0), true);
            view.Configure(actor, null, art);
            Assert.AreSame(idle, view.Renderer.sprite);
        }

        [TestCase("bugaloo")]
        [TestCase("flo")]
        [TestCase("tokoro")]
        [TestCase("dummy")]
        [TestCase("flower")]
        public void ShadowStaysAboveTheBoardOnTheGroundAndDisappearsOnDeath(string id)
        {
            art.Id=id;
            view.Configure(actor,null,art);
            var shadow=actorObject.transform.Find("ground-shadow").GetComponent<SpriteRenderer>();
            float groundY=shadow.transform.position.y;
            view.Move(Vector3.zero,Vector3.right,.4f);
            view.AdvancePresentation(.17f);
            Assert.That(shadow.transform.position.y,Is.EqualTo(groundY).Within(.00001f));
            Assert.AreEqual(Quaternion.identity,shadow.transform.rotation);
            Assert.Greater(shadow.sortingOrder,18,"The board surface is rendered at order 18.");
            Assert.Less(shadow.sortingOrder,view.Renderer.sortingOrder);
            Assert.IsTrue(shadow.enabled);
            Assert.Greater(shadow.transform.localScale.x,shadow.transform.localScale.y);
            view.Die();
            Assert.IsFalse(shadow.enabled);
            view.AdvancePresentation(.1f);
            Assert.IsFalse(shadow.enabled);
            view.Revive();
            Assert.IsTrue(shadow.enabled);
        }
    }
}

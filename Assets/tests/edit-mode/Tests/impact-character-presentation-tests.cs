using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class ImpactCharacterPresentationTests
    {
        private GameObject actorObject;
        private MonsterUnit actor;
        private UnitPresentation view;
        private Texture2D texture;
        private Sprite sprite;

        [SetUp]
        public void Setup()
        {
            texture=new Texture2D(80,100);
            sprite=Sprite.Create(texture,new Rect(0,0,80,100),new Vector2(.5f,0),100);
            actorObject=new GameObject("character-impact-test");
            actor=actorObject.AddComponent<MonsterUnit>();
            actor.Initialize("impact-test",BoardSide.Blue,new UnitStats(100,10,attackInterval:.65f,attackWindup:.2f));
            view=actorObject.AddComponent<UnitPresentation>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        private void Configure(string id)
        {
            var art=new UnitArt{Id=id,Portrait=sprite,ReferencePixelWidth=80,WorldWidth=1};
            for(int i=0;i<8;i++)art.DirectionalAnimations[i]=new UnitDirectionalAnimation
            {
                Idle=new[]{sprite},Move=new[]{sprite,sprite},Attack=new[]{sprite,sprite,sprite},
                Special=new[]{sprite,sprite,sprite},AttackContactFrame=1,
                AttackLungeDistance=.24f,AttackImpactSquash=.16f,SpecialDuration=.6f,SpecialLift=.1f
            };
            view.Configure(actor,null,art);
        }

        [Test]
        public void PopowHoldsWindupThenSnapsAtAuthoritativeContact()
        {
            Configure("popow");
            view.Attack(Vector3.right,false,.2f);
            view.AdvancePresentation(.16f);
            Assert.Less(view.VisualRoot.localPosition.x,0,"The fist remains wound up before its final fast drive.");
            Assert.IsFalse(view.AttackContactReached);
            view.AdvancePresentation(.04f);
            Assert.IsTrue(view.AttackContactReached);
            Assert.That(view.VisualRoot.localPosition.x,Is.EqualTo(.24f).Within(.0001f));
            view.AdvancePresentation(.02f);
            Assert.That(view.VisualRoot.localPosition.x,Is.EqualTo(.24f).Within(.0001f),"A short contact hold makes the punch readable.");
            Assert.AreEqual(Vector3.zero,actor.transform.position);
            view.AdvancePresentation(.17f);
            Assert.IsFalse(view.IsAttacking);
        }

        [Test]
        public void AtongRaisesAndSlamsJawWhileItsBodyStaysOnTheCell()
        {
            Configure("atong");
            view.Attack(Vector3.right,false,.2f);
            view.AdvancePresentation(.15f);
            Transform jaw=view.VisualRoot.Find("hammer-jaw");
            Assert.IsTrue(jaw.GetComponent<SpriteRenderer>().enabled);
            float raised=jaw.localPosition.y;
            Assert.IsFalse(view.Renderer.enabled,"The original drawing is articulated as lower body and jaw.");
            view.AdvancePresentation(.05f);
            Assert.Less(jaw.localPosition.y,raised-.2f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0,jaw.localEulerAngles.z)),50);
            Assert.AreEqual(Vector3.zero,actor.transform.position);
        }

        [TestCase(0,1)] [TestCase(1,1)] [TestCase(1,0)] [TestCase(1,-1)]
        [TestCase(0,-1)] [TestCase(-1,-1)] [TestCase(-1,0)] [TestCase(-1,1)]
        public void AtongPullsHeadFarBehindTargetThenSlamsForwardAtThreeSecondContact(int x,int y)
        {
            actor.Initialize("impact-test",BoardSide.Blue,new UnitStats(100,10,attackInterval:3.8f,attackWindup:3));
            Configure("atong");
            Vector3 direction=new Vector3(x,y,0).normalized;
            view.Attack(direction,false,3);
            view.AdvancePresentation(0);
            Transform jaw=view.VisualRoot.Find("hammer-jaw");
            Vector3 resting=jaw.position;
            view.AdvancePresentation(2.4f);
            Assert.Less(Vector3.Dot(jaw.position-resting,direction),-.38f,"The head must travel behind the body, not merely bob upward.");
            Assert.IsFalse(view.AttackContactReached);
            Assert.AreEqual(Vector3.zero,view.VisualRoot.localPosition,"The lower body stays planted during the exaggerated head swing.");
            Assert.IsTrue(view.VisualRoot.Find("hammer-neck").GetComponent<SpriteRenderer>().enabled);
            if(x!=0)Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0,jaw.localEulerAngles.z)),50);
            view.AdvancePresentation(.6f);
            Assert.IsTrue(view.AttackContactReached);
            Assert.Greater(Vector3.Dot(jaw.position-resting,direction),.29f,"The hammer follows through toward its target exactly at contact.");
            if(x!=0)Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0,jaw.localEulerAngles.z)),80);
            Assert.AreEqual(Vector3.zero,actor.transform.position);
        }

        [Test]
        public void AtongLongCustomWindupStillShowsContactAcrossRenderedFrames()
        {
            actor.Initialize("impact-test",BoardSide.Blue,new UnitStats(100,10,attackInterval:1,attackWindup:3));
            Configure("atong");
            view.Attack(Vector3.right,false,3);
            view.AdvancePresentation(3.03f);
            Assert.IsTrue(view.IsAttacking,"A windup longer than the attack interval must not erase the slam after one frame.");
            Transform jaw=view.VisualRoot.Find("hammer-jaw");
            Assert.IsTrue(jaw.GetComponent<SpriteRenderer>().enabled);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0,jaw.localEulerAngles.z)),80);
            view.AdvancePresentation(.16f);
            Assert.IsFalse(view.IsAttacking);
        }

        [Test]
        public void AtongCriticalKeepsTheSlamAndResetCancelsTheArticulatedPose()
        {
            Configure("atong");
            view.Attack(Vector3.right,false,.2f);
            view.AdvancePresentation(.2f);
            view.UseAbility("atong-critical");
            Assert.IsFalse(view.IsUsingAbility,"Critical damage must not replace the jaw's contact pose with a generic special animation.");
            view.AdvancePresentation(.01f);
            Assert.IsTrue(view.VisualRoot.Find("hammer-jaw").GetComponent<SpriteRenderer>().enabled);
            actor.ResetForCombat();
            view.AdvancePresentation(.01f);
            Assert.IsFalse(view.IsAttacking);
            Assert.IsFalse(view.VisualRoot.Find("hammer-jaw").GetComponent<SpriteRenderer>().enabled);
            Assert.IsFalse(view.VisualRoot.Find("hammer-neck").GetComponent<SpriteRenderer>().enabled);
            view.Attack(Vector3.right,false,.2f);
            view.AdvancePresentation(.1f);
            Assert.IsFalse(view.AttackContactReached,"After a reset the full windup starts again.");
        }

        [Test]
        public void HymayLoadsRearLegThenReleasesWhipWithoutSlidingLogicalCell()
        {
            Configure("hymay");
            view.Attack(Vector3.right,true,.8f);
            view.AdvancePresentation(.15f);
            Assert.Less(view.VisualRoot.localPosition.x,0);
            Assert.Greater(view.VisualRoot.localPosition.y,0);
            view.AdvancePresentation(.05f);
            Assert.IsTrue(view.AttackContactReached,"Release follows windup, never projectile arrival.");
            Assert.Greater(view.VisualRoot.localPosition.x,.2f);
            Assert.AreEqual(Vector3.zero,actor.transform.position);
        }

        [Test]
        public void FloCastCannotBeRestartedByFollowingAbilityNotification()
        {
            Configure("flo");
            view.PrepareFlower(.5f);
            view.AdvancePresentation(.24f);
            Assert.IsTrue(view.IsUsingAbility);
            view.UseAbility("flo-super");
            view.AdvancePresentation(.27f);
            Assert.IsFalse(view.IsUsingAbility,"Planting completes at the simulation's original cast deadline.");
        }

        [Test]
        public void AttackResetCancelsFloPreparationAndRestartsNextWindup()
        {
            Configure("flo");
            view.PrepareFlower(.5f);
            view.AdvancePresentation(.2f);
            actor.ResetForCombat();
            view.AdvancePresentation(.01f);
            Assert.IsFalse(view.IsUsingAbility);
            view.Attack(Vector3.right,false,.2f);
            view.AdvancePresentation(.1f);
            Assert.IsTrue(view.IsAttacking);
            Assert.IsFalse(view.AttackContactReached);
        }

        [Test]
        public void PopowDashFacesItsRouteEvenWhenItInterruptsAnotherWalkingDirection()
        {
            Configure("popow");
            view.Move(Vector3.zero,Vector3.up,1);
            Assert.AreEqual(UnitFacing.North,view.Facing);
            view.Dash(Vector3.zero,Vector3.right*2,new Vector3(2,-2,0),.4f);
            Assert.AreEqual(UnitFacing.East,view.Facing,"A dash must replace the previous walk facing immediately.");
            view.AdvancePresentation(.1f);
            Assert.AreEqual(UnitFacing.East,view.Facing);
            view.AdvancePresentation(.2f);
            Assert.AreEqual(UnitFacing.South,view.Facing,"A lateral fallback landing must turn with the second route segment.");
            Assert.IsTrue(view.IsDashing);
        }

        [Test]
        public void PopowDashPassesThroughTargetAndIgnoresDuplicateMovedEvent()
        {
            Configure("popow");
            Vector3 target=new Vector3(2,2,0),end=Vector3.right*4;
            view.Dash(Vector3.zero,target,end,.4f);
            view.Move(Vector3.zero,end,1);
            view.AdvancePresentation(.2f);
            Assert.IsTrue(view.IsDashing);
            Assert.That(Vector3.Distance(actor.transform.position,target),Is.LessThan(.0001f));
            view.AdvancePresentation(.2f);
            Assert.AreEqual(end,actor.transform.position);
            Assert.IsFalse(view.IsMoving);
            Assert.IsFalse(view.IsDashing);
            SpriteRenderer shadow=actorObject.transform.Find("ground-shadow").GetComponent<SpriteRenderer>();
            Assert.That(shadow.color.a,Is.EqualTo(.55f).Within(.0001f));
        }
    }
}

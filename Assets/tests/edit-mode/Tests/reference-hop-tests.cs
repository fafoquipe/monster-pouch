using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class ReferenceHopTests
    {
        GameObject go; MonsterUnit actor; UnitPresentation view; UnitArt art; Texture2D texture; Sprite[] poses;
        [SetUp] public void Setup()
        {
            go=new GameObject("reference-hop-test"); actor=go.AddComponent<MonsterUnit>();
            actor.Initialize("test",BoardSide.Blue,new UnitStats(100,10));
            texture=new Texture2D(7,1); poses=new Sprite[7];
            for(int i=0;i<poses.Length;i++)poses[i]=Sprite.Create(texture,new Rect(i,0,1,1),new Vector2(.5f,0),1);
            art=new UnitArt{Id="flo",Portrait=poses[0],ReferencePixelWidth=1,TravelMotion=UnitTravelMotion.AlternatingHop};
            for(int d=0;d<8;d++)art.DirectionalAnimations[d]=new UnitDirectionalAnimation
            {
                Idle=new[]{poses[0]},Move=new[]{poses[1],poses[2],poses[3],poses[4]},
                HopLeft=new[]{poses[1],poses[2],poses[3]},HopRight=new[]{poses[4],poses[5],poses[6]}
            };
            view=go.AddComponent<UnitPresentation>();view.Configure(actor,null,art);
        }
        [TearDown] public void Cleanup()
        {Object.DestroyImmediate(go);foreach(var pose in poses)Object.DestroyImmediate(pose);Object.DestroyImmediate(texture);}
        [Test] public void ConsecutiveTilesAlternateTheSupportingFootAtAllThreePhases()
        {
            var p=TileHopProfile.For("flo");
            for(int step=0;step<2;step++)
            {
                Vector3 from=Vector3.right*step,to=Vector3.right*(step+1);
                view.Move(from,to,1);
                view.AdvancePresentation(p.Prepare*.5f);
                Assert.AreEqual(step==0,view.IsLeftFootHop);
                Assert.AreSame(poses[step==0?1:4],view.Renderer.sprite);
                Assert.That(go.transform.position.y,Is.EqualTo(0).Within(.00001f));
                view.AdvancePresentation(p.Prepare*.5f+p.Air*.5f);
                Assert.AreSame(poses[step==0?2:5],view.Renderer.sprite);
                Assert.Greater(go.transform.position.y,0);
                view.AdvancePresentation(p.Air*.5f+.02f);
                Assert.AreSame(poses[step==0?3:6],view.Renderer.sprite);
                view.AdvancePresentation(1-p.Prepare-p.Air);
                Assert.AreEqual(to,go.transform.position);
                Assert.AreSame(poses[0],view.Renderer.sprite);
            }
        }
        [Test] public void FootAlternationSurvivesFacingChangesAndDoesNotFlipTheWholeCharacter()
        {
            view.Move(Vector3.zero,Vector3.right,.3f);view.AdvancePresentation(.3f);
            view.Move(Vector3.right,Vector3.right+Vector3.up,.3f);view.AdvancePresentation(.18f);
            Assert.IsFalse(view.IsLeftFootHop);Assert.AreSame(poses[5],view.Renderer.sprite);
            Assert.IsFalse(view.Renderer.flipX);
        }
        [Test] public void InstantPlacementDoesNotConsumeTheNextFoot()
        {
            view.Move(Vector3.zero,Vector3.right,0);
            view.Move(Vector3.right,Vector3.right*2,1);view.AdvancePresentation(.1f);
            Assert.IsTrue(view.IsLeftFootHop);Assert.AreSame(poses[1],view.Renderer.sprite);
        }
        [Test] public void FlyingArtworkGlidesWithoutGroundTakeoffOrLandingAndDoesNotGrantImmunity()
        {
            art.TravelMotion=UnitTravelMotion.Flying;view.Configure(actor,null,art);
            view.Move(Vector3.zero,Vector3.right,1);view.AdvancePresentation(.1f);
            Assert.Greater(go.transform.position.x,0,"Flight is already traveling during a terrestrial launch phase.");
            Assert.That(go.transform.position.y,Is.EqualTo(0).Within(.00001f));
            Assert.Greater(view.VisualRoot.localPosition.y,.08f);
            Assert.IsFalse(actor.IsFlying,"Visual flight must not grant the combat flight state.");
            view.AdvancePresentation(.9f);
            Assert.AreEqual(Vector3.right,go.transform.position);
            Assert.Greater(view.VisualRoot.localPosition.y,.08f,"The resting flyer stays hovering.");
        }
    }
}


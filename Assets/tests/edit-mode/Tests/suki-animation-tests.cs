using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class SukiAnimationTests
    {
        GameObject actorObject;
        WhelpUnit actor;
        UnitPresentation view;
        UnitArt art;
        [SetUp] public void Setup()
        {
            var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/suki/Character");
            Assert.NotNull(character);art=character.Visuals;
            actorObject=new GameObject("Suki animation review");actor=actorObject.AddComponent<WhelpUnit>();
            actor.Initialize("suki-review",BoardSide.Blue,UnitStats.FromDefinition(character.CreateRuntimeDefinition()));
            view=actorObject.AddComponent<UnitPresentation>();view.Configure(actor,null,art);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(actorObject);}

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        [TestCase(4)][TestCase(5)][TestCase(6)][TestCase(7)]
        public void FishThrowFacesItsAllyInsteadOfThePreviousEnemy(int direction)
        {
            float angle=direction*Mathf.PI/4;
            var forward=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
            view.Attack(-forward*3,false,.35f);view.AdvancePresentation(1);
            view.Face(Mathf.RoundToInt(forward.x),-Mathf.RoundToInt(forward.y));
            view.UseAbility("suki-super");var clip=art.Animation((UnitFacing)direction);
            float cast=AbilityTiming.Cast(actor.BaseStats,"suki-super");
            view.AdvancePresentation(cast-.001f);
            Assert.AreSame(clip.Special[0],view.Renderer.sprite,"Keep the fish in hand during preparation.");
            Assert.AreEqual(direction>4,view.Renderer.flipX,"Prepare toward the correct side.");
            view.AdvancePresentation(.001f);
            Assert.AreSame(clip.Special[1],view.Renderer.sprite,"Release exactly when the fish launches.");
            Assert.AreEqual(direction>4,view.Renderer.flipX,"Release toward the correct side.");
            Assert.Greater(Vector3.Dot(view.VisualRoot.localPosition,forward),0,"Throwing body follows the ally, not the prior enemy.");
            Assert.AreEqual(Vector3.zero,actor.transform.position,"A throw must not move the occupied cell.");
        }

        [Test] public void PunchAnticipatesThenHoldsContactAndReturnsToIdle()
        {
            var clip=art.Animation(UnitFacing.East);float windup=CombatSimulation.GetAttackWindup(actor);
            view.Attack(Vector3.right*2,false,windup);
            view.AdvancePresentation(windup*.25f);Assert.AreSame(clip.Idle[0],view.Renderer.sprite);
            view.AdvancePresentation(windup*.5f);Assert.AreNotSame(clip.Idle[0],view.Renderer.sprite);Assert.IsFalse(view.AttackContactReached);
            view.AdvancePresentation(windup*.25f);Assert.AreSame(art.Attack(UnitFacing.East),view.Renderer.sprite);
            Assert.IsTrue(view.AttackContactReached);Assert.Greater(view.VisualRoot.localPosition.x,.2f);
            view.AdvancePresentation(actor.BaseStats.AttackHold*.8f);Assert.AreSame(art.Attack(UnitFacing.East),view.Renderer.sprite);
            view.AdvancePresentation(actor.BaseStats.AttackHold*.2f+actor.BaseStats.AttackRecovery+.01f);view.AdvancePresentation(0);
            Assert.IsFalse(view.IsAttacking);Assert.AreSame(clip.Idle[0],view.Renderer.sprite);
            Assert.AreEqual(Vector3.zero,actor.transform.position);
        }

        [Test] public void ConsecutiveHopsUseDifferentFeetAndFinishOnTheTile()
        {
            var clip=art.Animation(UnitFacing.East);var profile=TileHopProfile.For("suki");
            Sprite first=null;
            for(int step=0;step<2;step++)
            {
                view.Move(Vector3.right*step,Vector3.right*(step+1),1);
                view.AdvancePresentation(profile.Prepare+profile.Air*.5f);
                Assert.Greater(actor.transform.position.y,0);
                if(step==0){first=view.Renderer.sprite;Assert.AreSame(clip.HopLeft[1],first);}
                else{Assert.AreSame(clip.HopRight[1],view.Renderer.sprite);Assert.AreNotSame(first,view.Renderer.sprite);}
                view.AdvancePresentation(1);Assert.AreEqual(Vector3.right*(step+1),actor.transform.position);
            }
        }

        [Test] public void DefeatShowsTheCollapsedDrawingBeforeFading()
        {
            view.Face(0,1);view.Die();view.AdvancePresentation(.36f);
            var frames=art.Animation(UnitFacing.South).Death;
            Assert.AreSame(frames[frames.Length-1],view.Renderer.sprite);
            Assert.AreEqual(1,view.Renderer.color.a);Assert.IsFalse(view.DeathFinished);
            view.AdvancePresentation(.3f);Assert.IsTrue(view.DeathFinished);Assert.IsFalse(view.Renderer.enabled);
        }
    }
}

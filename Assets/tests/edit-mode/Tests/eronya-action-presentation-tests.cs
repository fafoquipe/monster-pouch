using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class EronyaActionPresentationTests
    {
        GameObject root,actorObject;
        BoardManager board;BoardWorldMapper mapper;BattleUnit actor,enemy;
        UnitPresentation view;UnitArt art;UnitDefinition definition;
        CombatSimulation simulation;Texture2D texture;Sprite[] frames;
        CombatActionPreparation sample;
        [SetUp]public void Setup()
        {
            root=new GameObject("action-presentation-fixture");board=root.AddComponent<BoardManager>();board.BuildBoard();
            mapper=root.AddComponent<BoardWorldMapper>();mapper.Configure(board,root.transform,new Vector2(1.4f,-1.2f),Vector2.zero);
            texture=new Texture2D(4,1);frames=Enumerable.Range(0,4).Select(i=>Sprite.Create(texture,new Rect(i,0,1,1),new Vector2(.5f,0),1)).ToArray();
        }
        [TearDown]public void Cleanup()
        {simulation?.Stop();Object.DestroyImmediate(root);foreach(var sprite in frames)Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);}
        void Create(string id,bool instantSuper=false)
        {
            definition=EronyaRoster.CreateAll().Single(d=>d.Id==id);
            // Explicit fixture phases exercise presentation without assuming factory/human defaults.
            definition.AttackHold=.1f;definition.AttackRecovery=.2f;
            definition.BaseAbility.Parameters=definition.BaseAbility.Parameters.Where(p=>
                p.Key!="castDuration"&&p.Key!="holdDuration"&&p.Key!="recoveryDuration").Concat(new[]{
                    new AbilityParameter{Key="castDuration",Value=.4f},
                    new AbilityParameter{Key="holdDuration",Value=instantSuper?0:.1f},
                    new AbilityParameter{Key="recoveryDuration",Value=instantSuper?0:.2f}}).ToArray();
            actorObject=new GameObject(id);actorObject.transform.SetParent(root.transform);
            actor=actorObject.AddComponent<MonsterUnit>();actor.Initialize(id,BoardSide.Blue,UnitStats.FromDefinition(definition));
            Assert.IsTrue(board.TryOccupyCell(actor,2,6));
            var enemyObject=new GameObject("target");enemyObject.transform.SetParent(root.transform);
            enemy=enemyObject.AddComponent<MonsterUnit>();enemy.Initialize("target",BoardSide.Red,new UnitStats(10000,0));
            enemy.IsPassiveTrainingTarget=true;Assert.IsTrue(board.TryOccupyCell(enemy,2,5));
            art=new UnitArt{Id=id,ReferencePixelWidth=1,VitalsHeadAnchor=Vector2.up,TravelMotion=id=="alveyr"?UnitTravelMotion.Flying:UnitTravelMotion.WholeBodyHop};
            for(int d=0;d<8;d++)art.DirectionalAnimations[d]=new UnitDirectionalAnimation{
                Idle=new[]{frames[3]},Attack=new[]{frames[0],frames[1],frames[2]},Special=new[]{frames[0],frames[1],frames[2]},AttackContactFrame=1};
            view=actorObject.AddComponent<UnitPresentation>();view.Configure(actor,mapper,art);view.SnapToCell();
            view.BindActionPreparation(()=>sample,definition.BaseAbility.EffectId);
        }
        public static IEnumerable<string> NewIds()=>EronyaRoster.CreateAll().Select(d=>d.Id);
        [TestCaseSource(nameof(NewIds))]
        public void TwentyCharactersHoldPreparationAndReleaseOnlyOnEvents(string id)
        {
            Create(id);var cell=actor.CurrentCell;int health=enemy.CurrentHealth;
            sample=new CombatActionPreparation(actor,enemy,false,.9f);view.AdvancePresentation(2);
            Assert.AreSame(frames[0],view.Renderer.sprite,"Rendering cannot invent basic contact.");
            sample=default;actor.AttackWindupOverride=0;view.Attack(enemy.transform.position,false,0);view.AdvancePresentation(.016f);
            Assert.AreSame(frames[1],view.Renderer.sprite,"The authored basic hold must display confirmed contact on the next frame.");
            var tail=BasicAttackTiming.Tail(actor);
            bool fallback=tail.x<=0&&tail.y<=0;
            float hold=fallback?CombatSimulation.TickDuration:tail.x;
            float recovery=fallback?CombatSimulation.TickDuration:Mathf.Max(.0001f,tail.y);
            view.AdvancePresentation(Mathf.Max(0,hold+recovery*.8f-.016f));Assert.AreSame(frames[2],view.Renderer.sprite);
            sample=new CombatActionPreparation(actor,enemy,true,.9f);view.AdvancePresentation(2);
            Assert.AreSame(frames[0],view.Renderer.sprite,"Super must remain in preparation until release.");
            sample=default;view.UseAbility(definition.BaseAbility.EffectId);view.AdvancePresentation(0);
            Assert.AreSame(frames[1],view.Renderer.sprite,"AbilityUsed starts release, not another windup.");
            view.AdvancePresentation(1);Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
            Assert.AreSame(cell,actor.CurrentCell);Assert.AreSame(actor,cell.OccupiedBy);Assert.AreEqual(health,enemy.CurrentHealth);
            Assert.IsFalse(actor.IsFlying);
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        [TestCase(4)][TestCase(5)][TestCase(6)][TestCase(7)]
        public void SuperReleaseFollowsItsNewTargetAfterAnOppositeBasic(int heading)
        {
            Create("korran");
            foreach(var clip in art.DirectionalAnimations)
            {clip.SpecialLungeDistance=.25f;clip.SpecialLift=0;}
            float angle=heading*Mathf.PI/4;
            var direction=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
            var actorPosition=actorObject.transform.position;var cell=actor.CurrentCell;
            int health=enemy.CurrentHealth;float energy=actor.Energy;
            view.Attack(actorPosition-direction,false,0);
            // A different cast target is supplied by the actual preparation reader.
            enemy.transform.position=actorPosition+direction;
            sample=new CombatActionPreparation(actor,enemy,true,.9f);view.AdvancePresentation(0);
            sample=default;view.UseAbility(definition.BaseAbility.EffectId);view.AdvancePresentation(0);
            var visual=actorObject.transform.Find("unit-visual");Assert.IsNotNull(visual);
            Assert.AreSame(frames[1],view.Renderer.sprite,"The real release event selects contact.");
            Assert.That(Vector3.Dot(visual.localPosition,direction),Is.EqualTo(.25f).Within(.0001f),
                "The body must follow the new cast heading, not the opposite previous basic.");
            Assert.That(Vector3.Cross(visual.localPosition,direction).sqrMagnitude,Is.LessThan(.000001f));
            Assert.AreEqual(actorPosition,actorObject.transform.position);Assert.AreSame(cell,actor.CurrentCell);
            Assert.AreSame(actor,cell.OccupiedBy);Assert.AreEqual(health,enemy.CurrentHealth);Assert.AreEqual(energy,actor.Energy);
            view.AdvancePresentation(2);Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
        }
        [Test]public void EditedSuperHoldAndRecoveryStartAtReleaseWithoutRepeatingPreparation()
        {
            Create("korran");view.UseAbility(definition.BaseAbility.EffectId);view.AdvancePresentation(0);
            Assert.AreSame(frames[1],view.Renderer.sprite);
            view.AdvancePresentation(.09f);Assert.AreSame(frames[1],view.Renderer.sprite);
            view.AdvancePresentation(.17f);Assert.AreSame(frames[2],view.Renderer.sprite);
            view.AdvancePresentation(.05f);Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
        }
        [Test]public void ExplicitZeroSuperTailDoesNotInventAClipLengthRecovery()
        {
            Create("korran",true);view.UseAbility(definition.BaseAbility.EffectId);view.AdvancePresentation(0);
            Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
        }
        [Test]public void PassiveEventsDoNotRestartTheSuperBodyAndResetCancelsIt()
        {
            Create("korran");view.UseAbility("korran-passive-only");view.AdvancePresentation(0);
            Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
            view.UseAbility(definition.BaseAbility.EffectId);view.AdvancePresentation(0);Assert.IsTrue(view.IsUsingAbility);
            actor.ResetForCombat();view.AdvancePresentation(0);Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
        }
        [Test]public void SourceCancellationAndStunDoNotLeavePreparationOrAReleasedPose()
        {
            Create("korran");sample=new CombatActionPreparation(actor,enemy,true,.5f);view.AdvancePresentation(0);
            sample=default;view.AdvancePresentation(0);Assert.AreSame(frames[3],view.Renderer.sprite);
            view.UseAbility(definition.BaseAbility.EffectId);actor.IsStunned=true;view.AdvancePresentation(0);
            Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
            float idleGreen=view.Renderer.color.g;view.Hit();view.AdvancePresentation(0);
            Assert.Less(view.Renderer.color.g,idleGreen,"Cancelling an action for stun must preserve the shared hurt flash.");
        }
        [Test]public void ExistingUnboundPreviewKeepsIdleWithoutSamplingCombat()
        {
            Create("korran");view.Configure(actor,mapper,art);sample=new CombatActionPreparation(actor,enemy,true,.9f);
            view.AdvancePresentation(3);Assert.AreSame(frames[3],view.Renderer.sprite);Assert.IsFalse(view.IsUsingAbility);
        }
        [Test]public void RealCorePreparationReadsDoNotAdvanceDamageManaOrCastRelease()
        {
            Create("korran");simulation=new CombatSimulation(board,19,0);simulation.Begin(new[]{actor,enemy});
            view.BindActionPreparation(()=>simulation.CaptureActionPreparation(actor),definition.BaseAbility.EffectId);
            simulation.AbilityUsed+=(source,effect)=>{if(source==actor)view.UseAbility(effect);};
            simulation.Step(.2f);var basic=simulation.CaptureActionPreparation(actor);
            Assert.AreSame(actor,basic.Source);Assert.IsFalse(basic.IsSuper);Assert.Greater(basic.Progress,0);
            actor.AddEnergy(actor.BaseStats.EnergyMax);
            // Core now finishes the real basic phases before starting an energy ability.
            for(int i=0;i<120&&!simulation.CaptureActionPreparation(actor).IsSuper;i++)simulation.Step(.1f);
            var cast=simulation.CaptureActionPreparation(actor);Assert.IsTrue(cast.IsSuper);
            float elapsed=simulation.Elapsed,energy=actor.Energy;int hp=enemy.CurrentHealth;
            for(int i=0;i<20;i++){simulation.CaptureActionPreparation(actor);view.AdvancePresentation(.1f);}
            Assert.AreSame(frames[0],view.Renderer.sprite);Assert.AreEqual(elapsed,simulation.Elapsed);
            Assert.AreEqual(energy,actor.Energy);Assert.AreEqual(hp,enemy.CurrentHealth);
            simulation.Step(AbilityTiming.Cast(actor.BaseStats,definition.BaseAbility.EffectId));view.AdvancePresentation(0);
            Assert.IsNull(simulation.CaptureActionPreparation(actor).Source);Assert.AreSame(frames[1],view.Renderer.sprite);
        }
        [Test]public void NextMovementAndDisableCancelTheCosmeticReturn()
        {
            Create("korran");view.UseAbility(definition.BaseAbility.EffectId);view.AdvancePresentation(0);
            view.Move(actorObject.transform.position,actorObject.transform.position+Vector3.right,1);
            Assert.IsFalse(view.IsUsingAbility);Assert.IsFalse(view.IsAttacking);
            view.SnapToCell();view.UseAbility(definition.BaseAbility.EffectId);view.runInEditMode=true;
            actorObject.SetActive(false);Assert.IsFalse(view.IsUsingAbility);actorObject.SetActive(true);
            sample=default;view.AdvancePresentation(0);Assert.AreSame(frames[3],view.Renderer.sprite);
        }
    }
}

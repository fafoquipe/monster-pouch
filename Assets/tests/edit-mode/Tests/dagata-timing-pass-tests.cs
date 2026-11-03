using System;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class DagataTimingPassTests
    {
        GameObject root;
        BoardManager board;
        BoardWorldMapper mapper;
        MonsterUnit actor,enemy;
        CombatSimulation simulation;
        UnitDefinition definition;
        UnitPresentation view;
        Texture2D texture;
        Sprite[] frames;
        [SetUp]public void Setup()
        {
            root=new GameObject("dagata-timing");board=root.AddComponent<BoardManager>();board.BuildBoard();
            mapper=root.AddComponent<BoardWorldMapper>();mapper.Configure(board,root.transform,new Vector2(1,-1),Vector2.zero);
        }
        [TearDown]public void Cleanup()
        {
            simulation?.Stop();Object.DestroyImmediate(root);
            if(frames!=null)foreach(var frame in frames)Object.DestroyImmediate(frame);
            if(texture!=null)Object.DestroyImmediate(texture);
        }
        static AbilityParameter P(string key,float value)=>new AbilityParameter{Key=key,Value=value};
        void Create(bool zeroJump=false,float[] jumpPhases=null)
        {
            definition=EronyaRoster.CreateAll().Single(d=>d.Id=="dagata");
            definition.AttackInterval=.7f;definition.AttackWindup=.2f;definition.AttackHold=.08f;definition.AttackRecovery=.12f;
            var phases=jumpPhases??new[]{zeroJump?0:.2f,zeroJump?0:.4f,zeroJump?0:.1f,zeroJump?0:.2f};
            definition.PassiveAbility.Parameters=new[]{P("castDuration",phases[0]),P("travelDuration",phases[1]),P("holdDuration",phases[2]),P("recoveryDuration",phases[3])};
            var owned=new OwnedUnit(definition);owned.Tricks[0]=owned.Tricks[1]=true;
            var go=new GameObject("dagata");go.transform.SetParent(root.transform);actor=go.AddComponent<MonsterUnit>();
            actor.Initialize("dagata",BoardSide.Blue,UnitStats.FromDefinition(definition,owned));board.TryOccupyCell(actor,2,6);
            go=new GameObject("enemy");go.transform.SetParent(root.transform);enemy=go.AddComponent<MonsterUnit>();
            enemy.Initialize("enemy",BoardSide.Red,new UnitStats(10000,0));enemy.IsPassiveTrainingTarget=true;board.TryOccupyCell(enemy,2,2);
            simulation=new CombatSimulation(board,1,0);simulation.Begin(new[]{actor,enemy});
        }
        void CreateView()
        {
            texture=new Texture2D(4,1);frames=new Sprite[4];
            for(int i=0;i<4;i++)frames[i]=Sprite.Create(texture,new Rect(i,0,1,1),new Vector2(.5f,0),1);
            var art=new UnitArt{Id="dagata",ReferencePixelWidth=1,VitalsHeadAnchor=Vector2.up,TravelMotion=UnitTravelMotion.WholeBodyHop};
            for(int i=0;i<8;i++)art.DirectionalAnimations[i]=new UnitDirectionalAnimation{
                Idle=new[]{frames[3]},Attack=new[]{frames[0],frames[1],frames[2]},Special=new[]{frames[0],frames[1],frames[2]},
                HopLeft=new[]{frames[0],frames[1],frames[2]},OpeningJump=new[]{frames[0],frames[1],frames[2]},AttackContactFrame=1};
            view=actor.gameObject.AddComponent<UnitPresentation>();view.Configure(actor,mapper,art);view.SnapToCell();
            view.BindActionPreparation(()=>simulation.CaptureActionPreparation(actor),definition.BaseAbility.EffectId);
            view.BindDagataTiming(()=>simulation.Elapsed,()=>simulation.CaptureDagataJump(actor));
        }
        [Test]public void JumpCommitsAndGrantsShieldOnlyAtLandingThenReleasesRecovery()
        {
            Create();var start=simulation.CaptureDagataJump(actor);Assert.IsTrue(start.Active);
            Assert.AreSame(start.From,actor.CurrentCell);Assert.AreSame(start.To,actor.ReservedCell);Assert.AreEqual(0,actor.EronyaShieldDisplay);
            Assert.IsFalse(actor.IsFlying);Assert.IsFalse(actor.IsInvisible);
            simulation.Step(.5f);Assert.AreSame(start.From,actor.CurrentCell);Assert.AreEqual(0,actor.EronyaShieldDisplay);
            simulation.Step(.1f);Assert.AreSame(start.To,actor.CurrentCell);Assert.IsNull(actor.ReservedCell);
            Assert.AreEqual(10,actor.EronyaShieldDisplay);Assert.IsTrue(simulation.CaptureDagataJump(actor).Active);
            simulation.Step(.2f);Assert.IsTrue(simulation.CaptureDagataJump(actor).Active);
            simulation.Step(.1f);Assert.IsFalse(simulation.CaptureDagataJump(actor).Active);
            Assert.IsFalse(actor.IsFlying);Assert.IsFalse(actor.IsInvisible);
        }
        [TestCase("root")][TestCase("stun")][TestCase("death")][TestCase("reset")][TestCase("target-death")]
        [TestCase("disable")][TestCase("displaced")]
        public void InterruptedJumpNeverCommitsOrGrantsItsLandingReward(string reason)
        {
            Create();var start=simulation.CaptureDagataJump(actor);simulation.Step(.3f);
            if(reason=="root")typeof(BattleUnit).GetProperty("IsRooted").SetValue(actor,true);
            if(reason=="stun")typeof(BattleUnit).GetProperty("IsStunned").SetValue(actor,true);
            if(reason=="death")actor.ApplyDamage(10000);
            if(reason=="disable")actor.gameObject.SetActive(false);
            if(reason=="displaced")Assert.IsTrue(board.TryRepositionUnit(actor,board.GetCell(0,6)));
            if(reason=="reset")
            {
                actor.ResetForCombat();Assert.IsFalse(simulation.CaptureDagataJump(actor).Active);
                simulation.Step(.1f);var fresh=simulation.CaptureDagataJump(actor);
                Assert.IsTrue(fresh.Active);Assert.AreNotEqual(start.ResetVersion,fresh.ResetVersion);
                Assert.AreEqual(0,actor.EronyaShieldDisplay);Assert.AreSame(start.From,actor.CurrentCell);
                return;
            }
            if(reason=="target-death")enemy.ApplyDamage(10000);
            simulation.Step(.1f);
            Assert.IsFalse(ReferenceEquals(actor.CurrentCell,start.To));
            Assert.IsFalse(ReferenceEquals(start.To.ReservedBy,actor));Assert.AreEqual(0,actor.EronyaShieldDisplay);
        }
        [Test]public void ZeroJumpPhasesLandOnceWithoutInventingFlightOrAPause()
        {
            Create(true);Assert.IsFalse(simulation.CaptureDagataJump(actor).Active);Assert.IsNull(actor.ReservedCell);
            Assert.AreEqual(10,actor.EronyaShieldDisplay);Assert.IsFalse(actor.IsFlying);
            simulation.Step(.1f);Assert.AreEqual(10,actor.EronyaShieldDisplay);
        }
        [Test]public void JumpDrawingSamplesSimulationAndStopsAdvancingWhenSimulationStops()
        {
            Create();CreateView();view.AdvancePresentation(0);Assert.AreSame(frames[0],view.Renderer.sprite);
            Vector3 initial=view.VisualRoot.position;view.AdvancePresentation(99);Assert.AreEqual(initial,view.VisualRoot.position);
            simulation.Step(.4f);view.AdvancePresentation(0);Assert.AreSame(frames[1],view.Renderer.sprite);
            Vector3 midway=view.VisualRoot.position;view.AdvancePresentation(99);Assert.AreEqual(midway,view.VisualRoot.position);
            simulation.Step(.2f);view.AdvancePresentation(0);Assert.AreSame(frames[2],view.Renderer.sprite);
            actor.IsPassiveTrainingTarget=true;simulation.Step(.3f);view.AdvancePresentation(0);
            Assert.AreSame(frames[3],view.Renderer.sprite);Assert.IsFalse(view.IsPresentingDisplacement);
            Assert.AreEqual(Vector3.zero,view.VisualRoot.localPosition);
        }
        [Test]public void BasicUsesEditedPreparationAndKeepsTheExistingCadence()
        {
            Create(true);int before=enemy.CurrentHealth;
            simulation.Step(.1f);Assert.AreEqual(before,enemy.CurrentHealth);
            simulation.Step(.1f);Assert.AreEqual(before,enemy.CurrentHealth);
            simulation.Step(.1f);Assert.AreEqual(before-15,enemy.CurrentHealth,"8 basic plus existing opening bonus7 at actual contact.");
            simulation.Step(.6f);Assert.AreEqual(before-15,enemy.CurrentHealth);
            simulation.Step(.1f);Assert.AreEqual(before-23,enemy.CurrentHealth,"Second impact follows .7 seconds later.");
        }
        [Test]public void SuperReleaseShowsHoldAndRecoveryWithoutRepeatingPreparation()
        {
            Create(true);actor.IsPassiveTrainingTarget=true;CreateView();
            view.UseAbility("dagata-doble-ascua");view.AdvancePresentation(0);Assert.AreSame(frames[1],view.Renderer.sprite);
            view.AdvancePresentation(50);Assert.AreSame(frames[1],view.Renderer.sprite,"Paused simulation does not run the Super drawing ahead.");
            simulation.Step(.2f);view.AdvancePresentation(0);Assert.IsTrue(view.IsUsingAbility);Assert.AreSame(frames[2],view.Renderer.sprite);
            simulation.Step(.1f);view.AdvancePresentation(0);Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(frames[3],view.Renderer.sprite);
        }
        [Test]public void EditedPassiveValuesReachTheActualJumpTimeline()
        {
            Create(jumpPhases:new[]{.3f,.8f,.2f,.4f});
            var start=simulation.CaptureDagataJump(actor);
            Assert.IsTrue(start.Active);
            Assert.AreEqual(3,start.Timing.Depart-start.Timing.Start);
            Assert.AreEqual(8,start.Timing.Land-start.Timing.Depart);
            Assert.AreEqual(2,start.Timing.Recover-start.Timing.Land);
            Assert.AreEqual(4,start.Timing.End-start.Timing.Recover);
            simulation.Step(1f);Assert.AreSame(start.From,actor.CurrentCell);Assert.AreEqual(0,actor.EronyaShieldDisplay);
            simulation.Step(.1f);Assert.AreSame(start.To,actor.CurrentCell);Assert.AreEqual(10,actor.EronyaShieldDisplay);
            simulation.Step(.5f);Assert.IsTrue(simulation.CaptureDagataJump(actor).Active);
            simulation.Step(.1f);Assert.IsFalse(simulation.CaptureDagataJump(actor).Active);
        }
        [Test]public void EveryJumpPhaseIsEditableIncludingExplicitZero()
        {
            var effect=new TrickDefinition{EffectId="dagata-entrada-entre-humo",Parameters=new[]{P("castDuration",0),P("travelDuration",.71f),P("holdDuration",.11f),P("recoveryDuration",.21f)}};
            var values=EronyaAbilityParameters.Resolve(effect).ToDictionary(p=>p.Key,p=>p.Value);
            Assert.AreEqual(0,values["castDuration"]);Assert.AreEqual(.8f,values["travelDuration"]);
            Assert.AreEqual(.2f,values["holdDuration"]);Assert.AreEqual(.3f,values["recoveryDuration"]);
        }
    }
}

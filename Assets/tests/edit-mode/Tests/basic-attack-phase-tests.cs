using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BasicAttackPhaseTests
    {
        GameObject root;BoardManager board;CombatSimulation sim;
        readonly List<Object> assets=new List<Object>();
        [SetUp] public void Setup(){root=new GameObject("basic-phases");board=root.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown] public void Cleanup(){sim?.Stop();Object.DestroyImmediate(root);foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear();}
        BattleUnit Unit(UnitDefinition definition,int y,BoardSide side)
        {
            var go=new GameObject(definition.Id);go.transform.SetParent(root.transform);
            var unit=go.AddComponent<WhelpUnit>();unit.Initialize(definition.Id+y,side,UnitStats.FromDefinition(definition));
            Assert.IsTrue(board.TryOccupyCell(unit,2,y));return unit;
        }
        [Test] public void EntireShippedRosterHasIndependentlyEditableNonZeroContactAndReturn()
        {
            foreach(var def in Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").Units)
            {
                var stats=UnitStats.FromDefinition(def);
                Assert.Greater(stats.AttackHold,0,def.Id);Assert.Greater(stats.AttackRecovery,0,def.Id);
                var own=UnitAbilityOwnership.ForCombat(def);
                Assert.AreEqual(def.AttackHold,own.AttackHold);Assert.AreEqual(def.AttackRecovery,own.AttackRecovery);
            }
            var edited=DocumentedRoster.CreateAll().Single(d=>d.Id=="aky");
            edited.AttackHold=0;edited.AttackRecovery=.57f;
            Assert.AreEqual(0,UnitStats.FromDefinition(edited).AttackHold);
            Assert.AreEqual(.57f,UnitStats.FromDefinition(edited).AttackRecovery,.0001f);
        }
        [TestCase(false)] [TestCase(true)]
        public void DamageStaysAtChargeButNextAttackWaitsForContactAndReturn(bool documented)
        {
            var definition=DocumentedRoster.CreateAll().Single(d=>d.Id=="aky");
            definition.UsesDocumentedRules=documented;definition.AttackWindup=.3f;definition.AttackHold=.3f;definition.AttackRecovery=.4f;
            definition.AttackInterval=.1f;definition.Damage=7;definition.MaxHealth=1000;
            definition.EnergyMax=0;
            var actor=Unit(definition,5,BoardSide.Blue);
            var enemy=Unit(new UnitDefinition{Id="target",Damage=0,MaxHealth=1000,AttackInterval=100,AttackWindup=.1f,MoveInterval=100},4,BoardSide.Red);
            sim=new CombatSimulation(board);int attacks=0,hits=0;
            sim.Attacked+=(a,b,p)=>{if(a==actor)attacks++;};sim.Impacted+=(a,b,d)=>{if(a==actor)hits++;};
            sim.Begin(new[]{actor,enemy});
            sim.Step(.3f);Assert.AreEqual(1,attacks);Assert.AreEqual(0,hits);
            sim.Step(.1f);Assert.AreEqual(1,hits);Assert.AreEqual(993,enemy.CurrentHealth);
            sim.Step(.6f);Assert.AreEqual(1,attacks,"The contact/return pose was cut short.");
            sim.Step(.1f);Assert.AreEqual(2,attacks);
        }
        [Test] public void PresentationHoldsContactThenAdvancesReturnFramesAndAllowsZeroPhases()
        {
            var definition=new UnitDefinition{Id="aky",AttackWindup=.3f,AttackHold=.3f,AttackRecovery=.4f};
            var actor=Unit(definition,5,BoardSide.Blue);
            var texture=new Texture2D(4,1);assets.Add(texture);var frames=new Sprite[4];
            for(int i=0;i<4;i++){frames[i]=Sprite.Create(texture,new Rect(i,0,1,1),Vector2.one*.5f,1);assets.Add(frames[i]);}
            var art=new UnitArt{Id="aky",Portrait=frames[0]};
            for(int i=0;i<8;i++)art.DirectionalAnimations[i]=new UnitDirectionalAnimation{Idle=new[]{frames[0]},Attack=frames,AttackContactFrame=1};
            var view=actor.gameObject.AddComponent<UnitPresentation>();view.Configure(actor,null,art);
            view.Attack(Vector3.down,false,.3f);
            view.AdvancePresentation(.29f);Assert.AreSame(frames[0],view.Renderer.sprite);
            view.AdvancePresentation(.02f);Assert.AreSame(frames[1],view.Renderer.sprite);
            view.AdvancePresentation(.28f);Assert.AreSame(frames[1],view.Renderer.sprite,"Pose must remain held.");
            view.AdvancePresentation(.18f);Assert.AreSame(frames[2],view.Renderer.sprite,"Return should advance authored frames.");
            view.AdvancePresentation(.3f);view.AdvancePresentation(.01f);Assert.AreSame(frames[0],view.Renderer.sprite);
        }
    }
}

using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public class AbilityBalanceResetTests
    {
        readonly List<GameObject> objects=new List<GameObject>();
        BoardManager board;CombatSimulation sim;
        [SetUp] public void Setup(){var go=new GameObject("board");objects.Add(go);board=go.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown] public void Cleanup(){sim?.Stop();for(int i=objects.Count-1;i>=0;i--)Object.DestroyImmediate(objects[i]);objects.Clear();}
        BattleUnit Unit(UnitDefinition definition,int x,int y,BoardSide side)
        {
            var go=new GameObject(definition.Id);objects.Add(go);var unit=go.AddComponent<WhelpUnit>();
            unit.Initialize(definition.Id,side,UnitStats.FromDefinition(definition));Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
        }
        BattleUnit Target(int x=2,int y=4)=>Unit(new UnitDefinition{Id="target",MaxHealth=1000,Damage=0,AttackRange=9,AttackInterval=100},x,y,BoardSide.Red);
        [Test] public void EditedAbilityValuesAreDetachedAndUsedByCombatAndDescription()
        {
            var def=DocumentedRoster.CreateAtong();def.AttackWindup=.1f;def.AttackInterval=.3f;def.Damage=10;
            def.BaseAbility.Parameters=new[]{new AbilityParameter{Key="every",Value=1},new AbilityParameter{Key="damagePercent",Value=300}};
            var actor=Unit(def,2,5,BoardSide.Blue);var target=Target();
            Assert.That(AbilityBalance.Description(def.BaseAbility),Does.Contain("300%"));
            def.BaseAbility.Parameters[1].Value=900;
            sim=new CombatSimulation(board);sim.Begin(new[]{actor,target});sim.Step(.2f);
            Assert.AreEqual(970,target.CurrentHealth);
        }
        [TestCase(5f,.8f)] [TestCase(10f,.4f)] [TestCase(20f,.2f)]
        public void ProjectileSpeedControlsTravel(float speed,float seconds)
        {
            var def=DocumentedRoster.CreateAnuik();def.ProjectileSpeed=speed;
            var actor=Unit(def,2,8,BoardSide.Blue);var target=Target();
            Assert.AreEqual(seconds,CombatSimulation.GetProjectileTravelTime(actor,target),.001f);
        }
        [Test] public void RayIgnoresProjectileSpeed()
        {
            var def=DocumentedRoster.CreateSepora();def.ProjectileSpeed=.1f;
            var actor=Unit(def,2,8,BoardSide.Blue);var target=Target();
            Assert.AreEqual(0,CombatSimulation.GetImpactDelay(actor,target));
        }
        [Test] public void ResetCancelsPendingHitAndStartsNewFullWindup()
        {
            var def=DocumentedRoster.CreateAtori();def.AttackWindup=.5f;def.AttackInterval=2;
            var actor=Unit(def,2,5,BoardSide.Blue);var target=Target();int attacks=0;
            sim=new CombatSimulation(board);sim.Attacked+=(a,t,p)=>{if(a==actor)attacks++;};sim.Begin(new[]{actor,target});
            sim.Step(.4f);Assert.AreEqual(1,attacks);actor.ResetForCombat();sim.Step(.2f);
            Assert.AreEqual(2,attacks);Assert.AreEqual(1000,target.CurrentHealth);
            sim.Step(.4f);Assert.AreEqual(1000-def.Damage,target.CurrentHealth);
        }
        [TestCase(.6f,0)] [TestCase(.1f,1)]
        public void StunCancelsOnlyUnreleasedProjectiles(float windup,int expectedHits)
        {
            var def=DocumentedRoster.CreateAnuik();def.AttackWindup=windup;def.AttackInterval=10;def.ProjectileSpeed=2;
            var actor=Unit(def,2,5,BoardSide.Blue);
            var steinDef=DocumentedRoster.CreateStein();steinDef.AttackWindup=.1f;steinDef.AttackInterval=10;steinDef.Damage=0;
            steinDef.BaseAbility.Parameters=new[]{new AbilityParameter{Key="duration",Value=2}};
            var stein=Unit(steinDef,2,4,BoardSide.Red);int hits=0;
            sim=new CombatSimulation(board);sim.Impacted+=(a,t,d)=>{if(a==actor&&d>0)hits++;};sim.Begin(new[]{actor,stein});sim.Step(1f);
            Assert.AreEqual(expectedHits,hits);Assert.Greater(actor.AttackResetVersion,1);
        }
    }
}

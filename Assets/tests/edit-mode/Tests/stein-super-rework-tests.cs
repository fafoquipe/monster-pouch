using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class SteinSuperReworkTests
    {
        GameObject root;BoardManager board;CombatSimulation sim;
        [SetUp]public void Setup(){root=new GameObject("stein-super");board=root.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown]public void Cleanup(){sim?.Stop();Object.DestroyImmediate(root);}
        BattleUnit Unit(UnitDefinition definition,int x,int y,BoardSide side,params int[] upgrades)
        {
            var owned=new OwnedUnit(definition);foreach(int upgrade in upgrades)owned.Tricks[upgrade]=true;
            var go=new GameObject(definition.Id);go.transform.SetParent(root.transform);
            var unit=go.AddComponent<WhelpUnit>();unit.Initialize(definition.Id,side,UnitStats.FromDefinition(definition,owned));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
        }
        BattleUnit Target(int index,int x,int y)=>Unit(new UnitDefinition{Id="enemy-"+index,MaxHealth=1000,Damage=0,
            AttackRange=9,AttackInterval=100,MoveInterval=100},x,y,BoardSide.Red);
        [TestCase(false,false,3,2f)] [TestCase(true,false,3,3.5f)]
        [TestCase(false,true,4,2f)] [TestCase(true,true,4,3.5f)]
        public void EnergySuperHitsDistinctTargetsAndStunsForTheRequestedDuration(bool longer,bool fourth,int targets,float duration)
        {
            var def=DocumentedRoster.CreateStein();def.AttackInterval=100;def.EnergyPerAttack=0;
            var stars=new List<int>();if(longer)stars.Add(0);if(fourth)stars.Add(1);
            var stein=Unit(def,2,5,BoardSide.Blue,stars.ToArray());
            var enemies=new[]{Target(0,2,4),Target(1,1,4),Target(2,3,4),Target(3,2,3),Target(4,0,3)};
            var hits=new List<BattleUnit>();int casts=0;
            sim=new CombatSimulation(board);sim.Impacted+=(a,t,d)=>{if(a==stein&&t.IsStunned)hits.Add(t);};
            sim.AbilityUsed+=(u,id)=>{if(u==stein&&id=="stein-super")casts++;};
            sim.Begin(new[]{stein}.Concat(enemies).ToArray());stein.AddEnergy(100);
            sim.Step(.1f);Assert.AreEqual(1,casts);Assert.AreEqual(0,stein.Energy);Assert.IsEmpty(hits);
            for(int i=0;i<12&&hits.Count==0;i++)sim.Step(.1f);
            Assert.AreEqual(targets,hits.Count);Assert.AreEqual(targets,hits.Distinct().Count());
            Assert.AreEqual(targets,enemies.Count(e=>e.IsStunned));
            Assert.IsTrue(hits.All(e=>e.IsElectrocuted&&!e.IsPopowStunned));
            sim.Step(duration-.1f);Assert.IsTrue(hits.All(e=>e.IsStunned));
            sim.Step(.1f);Assert.IsTrue(hits.All(e=>!e.IsStunned));Assert.AreEqual(1,casts);
            Assert.IsTrue(hits.All(e=>!e.IsElectrocuted));
        }
        [Test]public void LoneEnemyReceivesOnlyOneSuperBolt()
        {
            var def=DocumentedRoster.CreateStein();def.AttackInterval=100;
            var stein=Unit(def,2,5,BoardSide.Blue,1);var enemy=Target(0,2,4);int hits=0;
            sim=new CombatSimulation(board);sim.Impacted+=(a,t,d)=>{if(a==stein)hits++;};sim.Begin(new[]{stein,enemy});stein.AddEnergy(100);
            for(int i=0;i<12&&hits==0;i++)sim.Step(.1f);
            Assert.AreEqual(1,hits);Assert.AreEqual(1000-def.Damage,enemy.CurrentHealth);
        }
    }
}

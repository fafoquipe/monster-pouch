using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MonsterPouch.Gameplay.Tests.EditMode
{
 public sealed class EnergySuperActionTests
 {
  GameObject root;BoardManager board;CombatSimulation sim;
  [SetUp]public void Setup(){root=new GameObject("energy-super-tests");board=root.AddComponent<BoardManager>();board.BuildBoard();}
  [TearDown]public void Cleanup(){sim?.Stop();Object.DestroyImmediate(root);}
  static void Set(TrickDefinition ability,string key,float value)
  {ability.Parameters=AbilityBalance.Resolve(ability).Where(p=>p.Key!=key).Concat(new[]{new AbilityParameter{Key=key,Value=value}}).ToArray();}
  BattleUnit Unit(UnitDefinition def,int x,int y,BoardSide side)
  {var go=new GameObject(def.Id+x);go.transform.SetParent(root.transform);var u=go.AddComponent<WhelpUnit>();u.Initialize(go.name,side,UnitStats.FromDefinition(def));Assert.IsTrue(board.TryOccupyCell(u,x,y));return u;}
  object Clock(BattleUnit unit)=>((IEnumerable)typeof(CombatSimulation).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sim)).Cast<object>().Single(c=>(BattleUnit)c.GetType().GetField("Unit").GetValue(c)==unit);
  static int Count(object c)=>(int)c.GetType().GetField("Attacks").GetValue(c);
  [TestCase("bugaloo")][TestCase("sepora")][TestCase("popow")][TestCase("kayon")][TestCase("flo")]
  [TestCase("stein")][TestCase("tokoro")][TestCase("tauris")][TestCase("gochan")][TestCase("hymay")]
  [TestCase("suki")][TestCase("pokrak")][TestCase("kazuru")][TestCase("rosie")]
  public void EveryEnergySuperBlocksBasicsAndRestartsFirstHitPreparation(string id)
  {
   var def=DocumentedRoster.CreateAll().Single(d=>d.Id==id);
   def.AttackInterval=5;def.AttackWindup=.4f;def.AttackHold=def.AttackRecovery=0;
   def.AttackRange=9;def.IQSpeed=10;def.Damage=7;def.MaxHealth=10000;
   def.EnergyPerAttack=def.EnergyOnDamage=def.EnergyPerSecond=0;
   Set(def.BaseAbility,"castDuration",.4f);Set(def.BaseAbility,"holdDuration",.2f);Set(def.BaseAbility,"recoveryDuration",.3f);
   Set(def.BaseAbility,"duration",.8f);Set(def.BaseAbility,"travelDuration",.3f);Set(def.BaseAbility,"pullDuration",.2f);
   var actor=Unit(def,2,5,BoardSide.Blue);
   var enemyDef=new UnitDefinition{Id="target",MaxHealth=100000,Damage=0,AttackRange=9,AttackInterval=100,MoveInterval=100,IQSpeed=10};
   var a=Unit(enemyDef,2,3,BoardSide.Red);var b=Unit(enemyDef,3,3,BoardSide.Red);
   sim=new CombatSimulation(board);sim.Begin(new[]{actor,a,b});actor.AddEnergy(actor.BaseStats.EnergyMax);
   var clock=Clock(actor);float cast=-1,first=-1;
   sim.AbilityUsed+=(u,kind)=>{if(u==actor&&kind==id+"-super")cast=sim.Elapsed;};
   for(int i=0;i<40;i++) {
    int count=Count(clock);sim.Step(.1f);
    if(Count(clock)>count) {
     Assert.IsFalse((bool)clock.GetType().GetField("EnergySuperActive").GetValue(clock),"Basic started during Super: "+id);
     Assert.GreaterOrEqual(sim.Elapsed,1f-.001f,"Preparation/hold/recovery was cut short.");
     if(first<0){first=sim.Elapsed;Assert.AreEqual(.4f,CombatSimulation.GetAttackWindup(actor),.001f);}
    }
   }
   Assert.AreEqual(.1f,cast,.001f);Assert.Greater(first,cast,"Basic never resumed.");
   Assert.Less(first,4f,"The previous five-second basic cooldown must not be reused after Super.");
  }
  [Test]public void BugalooDoesNotHitUntilSuperEndsPlusFullWindup()
  {
   var def=DocumentedRoster.CreateBugaloo();def.AttackWindup=.4f;def.AttackInterval=5;def.AttackHold=def.AttackRecovery=0;
   def.Damage=7;def.EnergyPerAttack=def.EnergyOnDamage=def.EnergyPerSecond=0;
   Set(def.BaseAbility,"duration",1);Set(def.BaseAbility,"castDuration",0);Set(def.BaseAbility,"holdDuration",0);Set(def.BaseAbility,"recoveryDuration",0);
   var actor=Unit(def,2,5,BoardSide.Blue);var enemy=Unit(new UnitDefinition{Id="target",MaxHealth=1000,Damage=0,AttackInterval=100,MoveInterval=100},2,4,BoardSide.Red);
   sim=new CombatSimulation(board);sim.Begin(new[]{actor,enemy});actor.AddEnergy(100);
   sim.Step(1);Assert.AreEqual(0,Count(Clock(actor)));Assert.AreEqual(1000,enemy.CurrentHealth);
   sim.Step(.1f);Assert.AreEqual(1,Count(Clock(actor)));Assert.AreEqual(1000,enemy.CurrentHealth);
   sim.Step(.3f);Assert.AreEqual(1000,enemy.CurrentHealth);
   sim.Step(.1f);Assert.AreEqual(993,enemy.CurrentHealth);
  }
 }
}


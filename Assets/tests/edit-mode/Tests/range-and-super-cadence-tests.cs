using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
namespace MonsterPouch.Gameplay.Tests.EditMode
{
 public sealed class RangeAndSuperCadenceTests
 {
  readonly List<GameObject> objects=new List<GameObject>();BoardManager board;CombatSimulation sim;
  [SetUp]public void Setup(){var go=new GameObject("range-test");objects.Add(go);board=go.AddComponent<BoardManager>();board.BuildBoard();}
  [TearDown]public void Cleanup(){sim?.Stop();foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);objects.Clear();}
  BattleUnit Unit(UnitDefinition def,int x,int y,BoardSide side)
  {var go=new GameObject(def.Id);objects.Add(go);var u=go.AddComponent<WhelpUnit>();u.Initialize(def.Id+"-"+x+"-"+y,side,UnitStats.FromDefinition(def));Assert.IsTrue(board.TryOccupyCell(u,x,y));return u;}
  UnitDefinition Def(string id,bool documented,int iq,float windup)=>new UnitDefinition{Id=id,UsesDocumentedRules=documented,MaxHealth=1000,Damage=7,AttackRange=1,AttackInterval=2,AttackWindup=windup,MoveInterval=.2f,IQSpeed=iq};
  [TestCase(false,1)] [TestCase(true,1)] [TestCase(false,10)] [TestCase(true,10)]
  public void EscapingDuringWindupNeverTakesRemoteDamageAndIQControlsRecognition(bool documented,int iq)
  {
   var a=Unit(Def("attacker",documented,iq,.5f),2,8,BoardSide.Blue);
   var d=Def("old",false,10,.1f);d.Damage=0;d.AttackRange=9;d.AttackInterval=100;
   var old=Unit(d,2,7,BoardSide.Red);d=Def("next",false,10,.1f);d.Damage=0;d.AttackRange=9;d.AttackInterval=100;
   var next=Unit(d,4,0,BoardSide.Red);sim=new CombatSimulation(board);var starts=new List<(float time,BattleUnit target)>();
   sim.Attacked+=(s,t,p)=>{if(s==a)starts.Add((sim.Elapsed,t));};sim.Begin(new[]{a,old,next});sim.Step(.1f);
   Assert.IsTrue(board.TryRepositionUnit(old,board.GetCell(0,0)));Assert.IsTrue(board.TryRepositionUnit(next,board.GetCell(3,8)));
   sim.Step(.5f);Assert.AreEqual(1000,old.CurrentHealth);
   if(iq==1)Assert.AreEqual(1,starts.Count,"Slow recognition lets the old attack miss before changing target.");
   else Assert.AreEqual(next,starts.Last().target);
   sim.Step(2);Assert.Less(next.CurrentHealth,1000);Assert.AreEqual(1000,old.CurrentHealth);
  }
  [TestCase("gochan")] [TestCase("tokoro")] [TestCase("stein")] [TestCase("hymay")] [TestCase("tauris")]
  public void SuperReservesItsOwnPhasesBeforeBasicPreparation(string id)
  {
   var def=DocumentedRoster.CreateAll().Single(d=>d.Id==id);def.AttackInterval=2;def.EnergyPerAttack=0;def.EnergyOnDamage=0;
   var a=Unit(def,2,6,BoardSide.Blue);var victim=Def("victim",false,10,.1f);victim.MaxHealth=10000;victim.Damage=0;victim.AttackRange=9;victim.AttackInterval=100;
   var b=Unit(victim,2,5,BoardSide.Red);var c=Unit(victim,3,5,BoardSide.Red);
   sim=new CombatSimulation(board);sim.Begin(new[]{a,b,c});a.AddEnergy(a.BaseStats.EnergyMax);
   float cast=-1;var starts=new List<float>();sim.AbilityUsed+=(s,e)=>{if(s==a&&e==id+"-super")cast=sim.Elapsed;};sim.Attacked+=(s,t,p)=>{if(s==a)starts.Add(sim.Elapsed);};
   float duration=AbilityTiming.Duration(a.BaseStats,id+"-super",0);
   sim.Step(.1f);Assert.GreaterOrEqual(cast,0);sim.Step(Mathf.Max(0,duration-.1f));
   Assert.IsTrue(starts.All(t=>Mathf.Abs(t-cast)<.001f),"No basic action is allowed immediately after the super.");
   sim.Step(4f);Assert.IsTrue(starts.Any(t=>t>=cast+duration-.001f),"Recovery also waits for Tokoro's self-stun.");
  }
 }
}

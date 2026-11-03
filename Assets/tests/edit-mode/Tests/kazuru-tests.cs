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
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class KazuruTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board; CombatSimulation sim;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void Setup()
        { var go=new GameObject("Kazuru test board");objects.Add(go);board=go.AddComponent<BoardManager>();board.BuildBoard(); }
        [TearDown] public void Cleanup()
        { sim?.Stop();foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);objects.Clear(); }
        BattleUnit Unit(UnitDefinition definition,int x,int y,BoardSide side=BoardSide.Blue,int trick=-1)
        {
            var go=new GameObject(definition.Id+objects.Count);objects.Add(go);
            var owned=new OwnedUnit(definition);if(trick>=0)owned.Tricks[trick]=true;
            var unit=go.AddComponent<WhelpUnit>();unit.Initialize(go.name,side,UnitStats.FromDefinition(definition,owned));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
        }
        BattleUnit Dummy(int x,int y,BoardSide side=BoardSide.Blue)=>Unit(new UnitDefinition
        {Id="dummy-test",MaxHealth=1000,Damage=10,AttackRange=99,AttackInterval=100,MoveInterval=100},x,y,side);
        object Actor(BattleUnit u)=>((IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(sim)).Cast<object>()
            .Single(a=>a.GetType().GetField("Unit").GetValue(a)==(object)u);
        object Call(string name,params object[] args)=>typeof(CombatSimulation).GetMethod(name,Private).Invoke(sim,args);
        void Set(BattleUnit u,string name,object value)=>Actor(u).GetType().GetField(name).SetValue(Actor(u),value);
        void Begin(params BattleUnit[] units)
        {
            sim=new CombatSimulation(board);sim.Begin(units);
            foreach(var u in units){Set(u,"NextMove",99999);Set(u,"NextAttack",99999);}
        }
        int Attack(BattleUnit u)=>(int)Call("BasicDamage",Actor(u));
        void Cast(BattleUnit u)=>Call("CastKazuruRings",Actor(u));
        void Damage(BattleUnit source,BattleUnit target,int amount)=>Call("DirectDamage",Actor(source),Actor(target),amount);
        static AbilityParameter P(string key,float value)=>new AbilityParameter{Key=key,Value=value};

        [Test] public void MonsterHasThreeEditableChoicesAndThrowChangesRealStats()
        {
            var d=DocumentedRoster.CreateKazuru();Assert.IsTrue(d.IsMonster);Assert.AreEqual(0,d.BaseCost);
            Assert.AreEqual(3,d.Tricks.Length);Assert.AreEqual(AttackKind.Melee,d.AttackKind);
            var owned=new OwnedUnit(d);owned.Tricks[0]=true;
            var s=UnitStats.FromDefinition(d,owned);Assert.AreEqual(2,s.AttackRange);Assert.AreEqual(d.Damage,s.Attack);
            d.Tricks[0].RangeBonus=3;d.Tricks[0].DamageBonus=7;
            s=UnitStats.FromDefinition(d,owned);Assert.AreEqual(4,s.AttackRange);Assert.AreEqual(d.Damage,s.Attack,"Obsolete saved damage bonuses must not survive the burst redesign.");
            Assert.That(AbilityBalance.Description(d.Tricks[0],d),Does.Contain("+3").And.Contain("70%").And.Not.Contain("{"));
            foreach(var effect in new[]{d.BaseAbility}.Concat(d.Tricks))
                Assert.That(AbilityBalance.Description(effect,d),Does.Not.Contain("{"));
        }

        [Test] public void SuperIncludesSelfAndDiagonalsExcludesEnemyAndExpiresAtFiveSeconds()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7);var ally=Dummy(3,6);var outside=Dummy(4,7);var enemy=Dummy(1,7,BoardSide.Red);
            Begin(k,ally,outside,enemy);Cast(k);
            Assert.AreEqual(21,Attack(k));Assert.AreEqual(25,Attack(ally));Assert.AreEqual(10,Attack(outside));Assert.AreEqual(10,Attack(enemy));
            sim.Step(4.9f);Assert.AreEqual(25,Attack(ally));sim.Step(.1f);Assert.AreEqual(10,Attack(ally));Assert.AreEqual(6,Attack(k));
        }

        [Test] public void EditableAreaAndDamageAreUsedAtRelease()
        {
            var d=DocumentedRoster.CreateKazuru();d.BaseAbility.Parameters=new[]{P("width",5),P("damage",22),P("duration",1)};
            var k=Unit(d,2,7);var far=Dummy(4,9);var enemy=Dummy(5,0,BoardSide.Red);Begin(k,far,enemy);
            Cast(k);Assert.AreEqual(32,Attack(far));sim.Step(1);Assert.AreEqual(10,Attack(far));
        }

        [Test] public void EnergySuperChargesAndReleasesWithoutEnemyInRange()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7);var enemy=Dummy(5,0,BoardSide.Red);Begin(k,enemy);
            k.AddEnergy(k.BaseStats.EnergyMax);sim.Step(.1f);
            Assert.AreEqual(0,k.Energy);Assert.AreEqual(6,Attack(k),"Bonus starts at release, not preparation.");
            sim.Step(.3f);Assert.AreEqual(21,Attack(k));
            sim.Step(.6f);Assert.IsFalse(k.IsEnergyLocked);
        }

        [Test] public void AtongCriticalMultipliesBasePlusReceivedDamage()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7);
            var d=DocumentedRoster.CreateAtong();d.Damage=10;
            var atong=Unit(d,3,7);var enemy=Dummy(3,6,BoardSide.Red);Begin(k,atong,enemy);Cast(k);
            Set(atong,"Attacks",1);Call("QueueBasicAttack",Actor(atong),enemy);
            var pending=((IEnumerable)typeof(CombatSimulation).GetField("pending",Private).GetValue(sim)).Cast<object>().Single();
            Assert.AreEqual(Mathf.RoundToInt(25*1.5f),pending.GetType().GetField("Damage").GetValue(pending));
        }

        [Test] public void ReceivedDamageBuffsCompeteAndDoNotReturnAfterWinnerExpires()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7);var a=Dummy(3,7);var enemy=Dummy(5,0,BoardSide.Red);Begin(k,a,enemy);
            Call("ApplyDamageBoost",Actor(a),0,1.5f,int.MaxValue);Assert.AreEqual(15,Attack(a));
            Cast(k);Cast(k);Assert.AreEqual(25,Attack(a));
            Call("ApplyDamageBoost",Actor(a),0,1.5f,int.MaxValue);Assert.AreEqual(25,Attack(a));
            sim.Step(5);Assert.AreEqual(10,Attack(a),"Discarded Tsu damage must not return.");
            Set(a,"DamageBonus",7);Cast(k);Assert.AreEqual(32,Attack(a),"Innate rewards coexist.");
        }

        [Test] public void StrongerPercentDamageDiscardsFlatBuffInsteadOfCombiningIt()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7);var a=Dummy(3,7);var e=Dummy(5,0,BoardSide.Red);Begin(k,a,e);
            Cast(k);Call("ApplyDamageBoost",Actor(a),0,4f,int.MaxValue);Assert.AreEqual(40,Attack(a));
            sim.Step(5);Assert.AreEqual(40,Attack(a));
        }

        [Test] public void HaloSurvivesAtOneBlocksEveryDamageAndExpiresOnlyOnce()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7,BoardSide.Blue,1);var e=Dummy(3,7,BoardSide.Red);Begin(k,e);
            Damage(e,k,10000);Assert.AreEqual(1,k.CurrentHealth);Assert.IsTrue(sim.KazuruHaloActive(k));
            Damage(e,k,10000);Assert.AreEqual(1,k.CurrentHealth);
            sim.Step(1.9f);Damage(e,k,10000);Assert.AreEqual(1,k.CurrentHealth);
            sim.Step(.1f);Assert.IsFalse(sim.KazuruHaloActive(k));
            Damage(e,k,1);Assert.IsFalse(k.IsAlive);
            sim.Begin(new[]{k,e});Damage(e,k,10000);Assert.AreEqual(1,k.CurrentHealth,"New combat restores one halo.");
        }

        [Test] public void HaloInterceptsExecuteAndSimultaneousImpactsAfterHealthShields()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7,BoardSide.Blue,1);var t=Unit(DocumentedRoster.CreateTauris(),2,6,BoardSide.Red);
            Begin(k,t);k.AddShield(200);
            Damage(t,k,100);Assert.IsFalse(sim.KazuruHaloActive(k));Assert.AreEqual(90,k.CurrentHealth);
            Call("QueueImpact",Actor(t),k,10000,"tauris-super",true,0,false,false);
            Call("ResolveDocumentedImpacts");Assert.AreEqual(1,k.CurrentHealth);Assert.IsTrue(sim.KazuruHaloActive(k));
            Call("QueueImpact",Actor(t),k,10000,"bugaloo-reflected-moon",false,0,false,true);
            Call("ResolveDocumentedImpacts");Assert.AreEqual(1,k.CurrentHealth);
        }

        [Test] public void RenewalHealsExactlyThreeDelayedTicksOnlySelfAndRecastDoesNotStack()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7,BoardSide.Blue,2);var a=Dummy(3,7);var e=Dummy(5,0,BoardSide.Red);Begin(k,a,e);
            k.ApplyDamage(60);a.ApplyDamage(50);Cast(k);Assert.AreEqual(30,k.CurrentHealth);
            sim.Step(.9f);Assert.AreEqual(30,k.CurrentHealth);sim.Step(.1f);Assert.AreEqual(40,k.CurrentHealth);
            sim.Step(1);Assert.AreEqual(50,k.CurrentHealth);sim.Step(1);Assert.AreEqual(60,k.CurrentHealth);
            sim.Step(1);Assert.AreEqual(60,k.CurrentHealth);Assert.AreEqual(950,a.CurrentHealth);
            Cast(k);Cast(k);sim.Step(1);Assert.AreEqual(70,k.CurrentHealth);
        }

        [Test] public void RenewalHonorsAntiHealAndCannotResurrect()
        {
            var k=Unit(DocumentedRoster.CreateKazuru(),2,7,BoardSide.Blue,2);var a=Dummy(3,7);var e=Dummy(5,0,BoardSide.Red);Begin(k,a,e);
            k.ApplyDamage(60);Set(k,"AntiHealUntil",100);Set(k,"AntiHealMultiplier",.4f);Cast(k);
            sim.Step(1);Assert.AreEqual(34,k.CurrentHealth);
            k.ApplyDamage(100);sim.Step(3);Assert.IsFalse(k.IsAlive);Assert.AreEqual(0,k.CurrentHealth);
        }
    }
}

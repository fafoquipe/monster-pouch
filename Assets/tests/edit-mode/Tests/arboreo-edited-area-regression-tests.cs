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
    // Real scheduler, mana, recipients and shield ledger. Requires native Unity runner;
    // offline compilation of this fixture is not execution of these cases.
    public sealed class ArboreoEditedAreaRegressionTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Active="arboreo-abrazo-del-bosque";
        GameObject root;BoardManager board;CombatSimulation simulation;
        BattleUnit caster,enemy;int releases,areaEvents;
        readonly List<BoardCell[]> footprints=new List<BoardCell[]>();
        [SetUp] public void Setup()
        {root=new GameObject("arboreo-area-regression");board=root.AddComponent<BoardManager>();board.BuildBoard();releases=areaEvents=0;footprints.Clear();}
        [TearDown] public void Cleanup(){simulation?.Stop();Object.DestroyImmediate(root);simulation=null;}
        object Clock(BattleUnit unit)=>((IEnumerable)typeof(CombatSimulation).GetField("actors",Hidden).GetValue(simulation))
            .Cast<object>().Single(a=>a.GetType().GetField("Unit").GetValue(a)==(object)unit);
        BattleUnit Unit(UnitDefinition definition,int x,int y,BoardSide side,bool trick=false)
        {
            var go=new GameObject(definition.Id);go.transform.SetParent(root.transform);
            var owned=new OwnedUnit(definition);owned.Tricks[0]=trick;
            var unit=go.AddComponent<WhelpUnit>();unit.Initialize(go.name,side,UnitStats.FromDefinition(definition,owned));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
        }
        void Begin(AbilityAreaDefinition area,bool shield,int enemyX,int enemyY,bool zeroShield=false)
        {
            var definition=EronyaRoster.CreateAll().Single(d=>d.Id=="arboreo");
            definition.BaseAbility.Area=area;
            definition.Damage=0;definition.AttackInterval=definition.MoveInterval=1000;
            definition.EnergyPerAttack=definition.EnergyPerSecond=definition.EnergyOnDamage=0;
            if(zeroShield)definition.Tricks[0].Parameters=new[]{new AbilityParameter{Key="shield",Value=0},new AbilityParameter{Key="duration",Value=4}};
            caster=Unit(definition,2,4,BoardSide.Blue,shield);
            enemy=Unit(new UnitDefinition{Id="stationary-enemy",MaxHealth=1000,Damage=0,AttackRange=1,AttackInterval=1000,MoveInterval=1000},enemyX,enemyY,BoardSide.Red);
            enemy.IsPassiveTrainingTarget=true;
            simulation=new CombatSimulation(board,123,0);simulation.Begin(new[]{caster,enemy});
            var clock=Clock(caster);clock.GetType().GetField("NextAttack").SetValue(clock,int.MaxValue);
            clock.GetType().GetField("NextMove").SetValue(clock,int.MaxValue);
            simulation.AbilityUsed+=(unit,effect)=>{if(unit==caster&&effect==Active)releases++;};
            simulation.AreaEffect+=(unit,effect,cells)=>{if(unit==caster){areaEvents++;if(effect=="edited-area:"+Active)footprints.Add(cells);}};
            caster.AddEnergy(caster.BaseStats.EnergyMax);
        }
        bool Plan()
        {
            var args=new[]{Clock(caster),null};
            return (bool)typeof(CombatSimulation).GetMethod("EronyaNaturalisTryPlanCast",Hidden).Invoke(simulation,args);
        }
        void AdvanceToRelease()
        {
            for(int i=0;i<30&&releases==0;i++)simulation.Step(.1f);
            Assert.AreEqual(1,releases,"A full-energy legal cast must release exactly once.");
            Assert.AreEqual(0,caster.Energy,.001f,"The normal scheduler consumes the energy cost.");
        }
        static AbilityAreaDefinition EmptyOffense(bool square)=>square?
            new AbilityAreaDefinition{Shape=AbilityAreaShape.Square,SquareSize=0}:
            new AbilityAreaDefinition{Shape=AbilityAreaShape.Cross,UpWidth=1,UpDepth=2};

        [Test] public void EditedSquarePlansDiagonalRivalWithoutTelegraphOrBasicMutationAndActuallyCasts()
        {
            Begin(new AbilityAreaDefinition{Shape=AbilityAreaShape.Square,SquareSize=3},false,3,5);
            var clock=Clock(caster);var before=clock.GetType().GetField("LockedTarget").GetValue(clock);
            Assert.IsTrue(Plan());Assert.IsTrue(Plan());
            Assert.AreEqual(0,areaEvents);Assert.AreEqual(0,releases);Assert.AreEqual(0,caster.Shield);
            Assert.AreEqual(caster.BaseStats.EnergyMax,caster.Energy);
            Assert.AreSame(before,clock.GetType().GetField("LockedTarget").GetValue(clock));
            AdvanceToRelease();
            Assert.AreEqual(989,enemy.CurrentHealth);Assert.AreEqual(1,footprints.Count);
            CollectionAssert.Contains(footprints[0],enemy.CurrentCell);Assert.AreEqual(9,footprints[0].Length);
        }
        [TestCase(false)] [TestCase(true)]
        public void EditedEmptyOffensePreservesPurchasedSelfShieldOnceWithNormalCostAndDuration(bool square)
        {
            Begin(EmptyOffense(square),true,2,3);
            Assert.IsTrue(Plan());Assert.AreEqual(0,areaEvents);Assert.AreEqual(0,caster.Shield);
            AdvanceToRelease();
            Assert.AreEqual(18,caster.Shield);Assert.AreEqual(1000,enemy.CurrentHealth);
            Assert.IsFalse(enemy.IsTaunted);Assert.IsFalse(enemy.IsRooted);
            Assert.AreEqual(1,footprints.Count);CollectionAssert.DoesNotContain(footprints[0],enemy.CurrentCell);
            simulation.Step(3.9f);Assert.AreEqual(18,caster.Shield);
            simulation.Step(.1f);Assert.AreEqual(0,caster.Shield);
            Assert.AreEqual(1,releases);
        }
        [TestCase(false)] [TestCase(true)]
        public void EmptyCustomAreaWithoutSelfTrickDoesNotSpendEnergyOrEmitEffects(bool square)
        {
            Begin(EmptyOffense(square),false,2,3);
            Assert.IsFalse(Plan());simulation.Step(1);
            Assert.AreEqual(0,releases);Assert.AreEqual(0,areaEvents);Assert.AreEqual(0,caster.Shield);
            Assert.AreEqual(caster.BaseStats.EnergyMax,caster.Energy);Assert.AreEqual(1000,enemy.CurrentHealth);
        }
        [TestCase(false)] [TestCase(true)]
        public void OriginalAndAbsentAreaKeepManhattanActivationAndExistingShield(bool absent)
        {
            Begin(absent?null:new AbilityAreaDefinition(),true,3,5);
            Assert.IsFalse(Plan());simulation.Step(1);Assert.AreEqual(0,releases);
            Assert.AreEqual(0,areaEvents);Assert.AreEqual(caster.BaseStats.EnergyMax,caster.Energy);
            Assert.IsTrue(board.TryRepositionUnit(enemy,board.GetCell(2,3)));Assert.IsTrue(Plan());
            AdvanceToRelease();Assert.AreEqual(989,enemy.CurrentHealth);Assert.AreEqual(18,caster.Shield);
            Assert.AreEqual(0,footprints.Count);Assert.AreEqual(1,areaEvents);
        }
        [TestCase(false)] [TestCase(true)]
        public void SelfShieldCannotEnableAnEmptyCastWithoutAnOriginallyLegalRivalOrUsefulShield(bool zeroShield)
        {
            Begin(EmptyOffense(true),true,zeroShield?2:5,zeroShield?3:9,zeroShield);
            Assert.IsFalse(Plan());simulation.Step(1);
            Assert.AreEqual(0,releases);Assert.AreEqual(0,areaEvents);Assert.AreEqual(0,caster.Shield);
            Assert.AreEqual(caster.BaseStats.EnergyMax,caster.Energy);
        }
    }
}

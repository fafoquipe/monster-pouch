using System;
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
    // Four focused native scenarios. Compiled offline; execution belongs to the integrator.
    public sealed class MaestroTravelingWaveTests
    {
        GameObject root;BoardManager board;CombatSimulation sim;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp]public void Setup(){root=new GameObject("maestro-wave-test");board=root.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown]public void Cleanup(){sim?.Stop();Object.DestroyImmediate(root);}
        BattleUnit Add(UnitDefinition d,BoardSide side,int x,int y,bool tricks=false)
        {
            var go=new GameObject(d.Id+"/"+x+"/"+y);go.transform.SetParent(root.transform);
            var owned=new OwnedUnit(d);if(tricks)for(int i=0;i<owned.Tricks.Length;i++)owned.Tricks[i]=true;
            var unit=go.AddComponent<MonsterUnit>();unit.Initialize(go.name,side,UnitStats.FromDefinition(d,owned));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y));unit.IsPassiveTrainingTarget=true;return unit;
        }
        BattleUnit Maestro(BoardSide side,int x,int y,int width,int length,float speed,bool tricks=false)
        {
            var d=EronyaAquarionDefinitions.CreateAll().Single(u=>u.Id=="maestro-de-las-mareas");
            d.MaxHealth=1000;d.Damage=0;d.AttackInterval=100;d.MoveInterval=100;
            foreach(var p in d.BaseAbility.Parameters)
            {if(p.Key=="width")p.Value=width;if(p.Key=="length")p.Value=length;if(p.Key=="waveSpeed")p.Value=speed;}
            return Add(d,side,x,y,tricks);
        }
        BattleUnit Dummy(BoardSide side,int x,int y)=>Add(new UnitDefinition{Id="wave-dummy",MaxHealth=1000,Damage=0,
            AttackRange=1,AttackInterval=100,MoveInterval=100},side,x,y);
        void Begin(params BattleUnit[] units){sim=new CombatSimulation(board,1,0);sim.Begin(units);foreach(var u in units)u.IsPassiveTrainingTarget=true;}
        void Release(BattleUnit source)
        {
            object clock=typeof(CombatSimulation).GetMethod("Clock",Private).Invoke(sim,new object[]{source});
            typeof(CombatSimulation).GetMethod("EronyaAquarionResolveWave",Private).Invoke(sim,new[]{clock});
            typeof(CombatSimulation).GetMethod("EronyaFlushPackets",Private).Invoke(sim,null);
        }
        [Test]public void WidthLengthAndPiercingAreIndependentAndEachUnitIsHitOnlyOnce()
        {
            var a=Maestro(BoardSide.Blue,2,8,3,3,3);
            var first=Dummy(BoardSide.Red,1,7);var second=Dummy(BoardSide.Red,3,6);
            var last=Dummy(BoardSide.Red,2,5);var outside=Dummy(BoardSide.Red,4,7);var beyond=Dummy(BoardSide.Red,2,4);
            Begin(a,first,second,last,outside,beyond);
            int launches=0;sim.AbilityUsed+=(u,e)=>{if(e==MaestroWaveFlight.EffectId)launches++;};
            Release(a);Assert.AreEqual(1000,first.CurrentHealth);
            sim.Step(.3f);Assert.AreEqual(992,first.CurrentHealth);Assert.AreEqual(1000,second.CurrentHealth);
            sim.Step(.8f);
            Assert.AreEqual(992,first.CurrentHealth);Assert.AreEqual(992,second.CurrentHealth);Assert.AreEqual(992,last.CurrentHealth);
            Assert.AreEqual(1000,outside.CurrentHealth);Assert.AreEqual(1000,beyond.CurrentHealth);Assert.AreEqual(1,launches);
        }
        [Test]public void SpeedChangesContactTickAndDoesNotApplyDamageOrStunAtRelease()
        {
            var a=Maestro(BoardSide.Blue,2,8,3,5,2);var target=Dummy(BoardSide.Red,2,5);
            Begin(a,target);
            int casts=0;float released=-1;
            sim.AbilityUsed+=(u,e)=>{if(e==MaestroWaveFlight.EffectId){casts++;released=sim.Elapsed;a.IsPassiveTrainingTarget=true;}};
            a.IsPassiveTrainingTarget=false;a.AddEnergy(100);
            for(int i=0;i<30&&casts==0;i++)sim.Step(.1f);
            Assert.AreEqual(1,casts);Assert.GreaterOrEqual(released,0);Assert.AreEqual(0,a.Energy);
            sim.Step(1.2f);
            Assert.AreEqual(1000,target.CurrentHealth);Assert.IsFalse(target.IsStunned);
            sim.Step(.1f);Assert.AreEqual(992,target.CurrentHealth);Assert.IsTrue(target.IsStunned);
            Assert.AreEqual(1,casts);Assert.AreEqual(0,a.Energy);
            var slow=new MaestroWaveFlight(2,8,-1,3,5,2);
            var fast=new MaestroWaveFlight(2,8,-1,3,5,6);
            Assert.Less(fast.TravelSeconds,slow.TravelSeconds);
            Assert.AreEqual(.1f,MaestroWaveFlight.NormalizeSpeed(0));
            Assert.AreEqual(.1f,MaestroWaveFlight.NormalizeSpeed(-5));
        }
        [Test]public void RedWideWaveClipsAtWallAndHealsOnCrossingWhileShieldStartsAtRelease()
        {
            var a=Maestro(BoardSide.Red,0,7,7,9,2,true);var victim=Dummy(BoardSide.Blue,2,9);
            var blocked=Dummy(BoardSide.Blue,3,9);Assert.IsTrue(board.TrySetCellBlocked(3,8,true));
            var ally=Dummy(BoardSide.Red,1,8);Begin(a,victim,ally,blocked);ally.ApplyDamage(30);
            Release(a);Assert.Greater(a.Shield,0);Assert.AreEqual(970,ally.CurrentHealth);
            sim.Step(.4f);Assert.AreEqual(978,ally.CurrentHealth);Assert.AreEqual(1000,victim.CurrentHealth);
            sim.Step(1);Assert.AreEqual(990,victim.CurrentHealth);Assert.AreEqual(978,ally.CurrentHealth);
            Assert.AreEqual(1000,blocked.CurrentHealth,"Only the segment that reaches a wall ends there.");
            var wave=new MaestroWaveFlight(0,7,1,7,9,2);
            Assert.AreEqual(7,wave.Width);Assert.AreEqual(0,wave.MinX);Assert.AreEqual(3,wave.MaxX);
            wave.Sample(wave.TravelSeconds,out var b,out _,out _);
            Assert.LessOrEqual(b.xMax,BoardManager.Width-.5f);Assert.AreEqual(BoardManager.Height-.5f,b.yMax,.0001f);
            sim.Step(3);Assert.AreEqual(0,a.Shield,"The release shield still expires after four seconds.");
        }
        [Test]public void MovingAnchorDeathResetAndEndDoNotCreateChaseOrGhostImpacts()
        {
            var a=Maestro(BoardSide.Blue,2,8,3,7,2);var anchor=Dummy(BoardSide.Red,2,6);
            var later=Dummy(BoardSide.Red,1,3);var ally=Dummy(BoardSide.Blue,5,8);
            Begin(a,anchor,later,ally);Release(a);
            Assert.IsTrue(board.TryRepositionUnit(anchor,board.GetCell(5,5)));
            a.ApplyDamage(2000);sim.Step(3);
            Assert.AreEqual(1000,anchor.CurrentHealth);Assert.AreEqual(992,later.CurrentHealth,"Released water continues after source death.");
            Cleanup();Setup();
            a=Maestro(BoardSide.Blue,2,8,3,7,2);anchor=Dummy(BoardSide.Red,2,6);
            later=Dummy(BoardSide.Red,1,3);ally=Dummy(BoardSide.Blue,5,8);
            Begin(a,anchor,later,ally);Release(a);a.ResetForCombat();sim.Step(4);
            Assert.AreEqual(1000,later.CurrentHealth,"Source reset invalidates the released wave.");
            sim.Stop();Begin(a,anchor,later,ally);sim.Step(4);
            Assert.AreEqual(1000,later.CurrentHealth,"A new combat must not inherit the old wave.");
        }
    }
}

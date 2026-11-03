using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MobilePerformanceContractTests
{
    GameObject root;BoardManager board;CombatSimulation simulation;BattleUnit source,target;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [SetUp] public void SetUp()
    {
        root=new GameObject("status-projection-performance-test");board=root.AddComponent<BoardManager>();board.BuildBoard();
        var d=EronyaRoster.CreateAll().First(x=>x.Id=="alveyr");d.MaxHealth=1000;d.EnergyPerSecond=0;
        source=Unit(d,BoardSide.Blue,2,6);target=Unit(d,BoardSide.Red,2,4);
        simulation=new CombatSimulation(board,1,0);simulation.Begin(new[]{source,target});
        typeof(BattleUnit).GetProperty("IsPassiveTrainingTarget").SetValue(source,true);
        typeof(BattleUnit).GetProperty("IsPassiveTrainingTarget").SetValue(target,true);
    }
    BattleUnit Unit(UnitDefinition d,BoardSide side,int x,int y)
    {
        var go=new GameObject(side+"-fixture");go.transform.SetParent(root.transform);
        var unit=go.AddComponent<MonsterUnit>();unit.Initialize(go.name,side,UnitStats.FromDefinition(d,new OwnedUnit(d)));
        Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
    }
    [TearDown] public void TearDown(){simulation?.Stop();Object.DestroyImmediate(root);}
    object Clock(BattleUnit unit)=>typeof(CombatSimulation).GetMethod("Clock",Private).Invoke(simulation,new object[]{unit});
    void Root(string id,int duration)
    {
        var method=typeof(CombatSimulation).GetMethod("EronyaApplyStatus",Private);var parameters=method.GetParameters();
        method.Invoke(simulation,new object[]{Clock(source),Clock(target),Enum.Parse(parameters[2].ParameterType,"Root"),id,1f,duration,Enum.Parse(parameters[6].ParameterType,"RefreshStrongest")});
    }
    void Remove(string id)=>typeof(CombatSimulation).GetMethod("EronyaRemoveEffect",Private).Invoke(simulation,new object[]{Clock(target),id});
    [Test] public void RepeatedLiveReadsReuseImmutableSnapshotWithoutChangingGameplay()
    {
        Root("fixture-root",50);var before=simulation.CaptureStatusFrame(target);Assert.IsTrue(before.Statuses.Any(s=>s.Id=="root"));
        int hp=target.CurrentHealth;float energy=target.Energy;
        for(int i=0;i<1000;i++)
        {
            var next=simulation.CaptureStatusFrame(target);Assert.AreSame(before.Statuses,next.Statuses);
            Assert.AreEqual(before.Revision,next.Revision);Assert.AreEqual(before.SimulationTick,next.SimulationTick);
        }
        Assert.AreEqual(hp,target.CurrentHealth);Assert.AreEqual(energy,target.Energy);
    }
    [Test] public void SameTickRemovalAndReplacementDoNotWaitForAnotherSimulationStep()
    {
        Root("fixture-root",50);var original=simulation.CaptureStatusFrame(target);var held=original.Statuses.Single(s=>s.Id=="root");
        Remove("fixture-root");var cleared=simulation.CaptureStatusFrame(target);
        Assert.AreEqual(original.SimulationTick,cleared.SimulationTick);Assert.IsFalse(cleared.Statuses.Any(s=>s.Id=="root"));Assert.Greater(cleared.Revision,original.Revision);
        Root("replacement-root",90);var replaced=simulation.CaptureStatusFrame(target);
        Assert.AreEqual("replacement-root",replaced.Statuses.Single(s=>s.Id=="root").EffectId);
        Assert.AreEqual("fixture-root",original.Statuses.Single(s=>s.Id=="root").EffectId);Assert.AreEqual(held.ExpiresTick,original.Statuses.Single(s=>s.Id=="root").ExpiresTick);
    }
    [Test] public void TickUpdatesRemainingTimeAndStopInvalidatesImmediately()
    {
        Root("fixture-root",50);var before=simulation.CaptureStatusFrame(target);simulation.Step(.1f);
        var after=simulation.CaptureStatusFrame(target);Assert.Less(after.Statuses.Single(s=>s.Id=="root").RemainingSeconds,before.Statuses.Single(s=>s.Id=="root").RemainingSeconds);
        simulation.Stop();Assert.IsFalse(simulation.CaptureStatusFrame(target).IsActive);
    }
    [Test] public void PublicFrameConstructorStillDefensivelyCopiesInput()
    {
        var input=new[]{new CombatStatusSnapshot("root","a","b",1,1,0,50,0,1,0)};
        var frame=new CombatStatusFrame(1,0,1,true,input);input[0]=default;
        Assert.AreEqual("root",frame.Statuses[0].Id);
        Assert.Throws<NotSupportedException>(()=>((IList<CombatStatusSnapshot>)frame.Statuses)[0]=default);
    }
    [Test] public void PeriodicReadOnlyViewIsReusedAndRemainsLiveAcrossReset()
    {
        var ledger=new EronyaPeriodicLedger();var view=ledger.Effects;Assert.AreSame(view,ledger.Effects);
        ledger.Apply("s",1,"t",1,"p",EronyaPeriodicKind.Poison,3,0,50);Assert.AreEqual(1,view.Count);
        ledger.Reset();Assert.AreEqual(0,view.Count);Assert.AreSame(view,ledger.Effects);
        Assert.Throws<NotSupportedException>(()=>((IList<EronyaPeriodicLedger.Effect>)view).Clear());
    }
    [Test] public void ShieldReadOnlyViewIsReusedAndCannotMutateLedger()
    {
        var ledger=new EronyaShieldLedger();var view=ledger.Layers;Assert.AreSame(view,ledger.Layers);
        ledger.Grant("s",1,1,"shield",3,100,0,50);Assert.AreEqual(1,view.Count);
        Assert.Throws<NotSupportedException>(()=>((IList<EronyaShieldLedger.Layer>)view).Clear());
    }
}

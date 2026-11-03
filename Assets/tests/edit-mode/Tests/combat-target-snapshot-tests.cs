using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatTargetSnapshotTests
{
    [Test] public void ObservingTheActualLockNeverRetargetsAndDeadTargetsDisappearImmediately()
    {
        var root=new GameObject("Target observation test");
        try
        {
            var board=root.AddComponent<BoardManager>();board.BuildBoard();
            BattleUnit Unit(string id,BoardSide side,int x,int y)
            {
                var go=new GameObject(id);go.transform.SetParent(root.transform);
                var unit=go.AddComponent<MonsterUnit>();
                unit.Initialize(id,side,new UnitStats(1000,0,2,.4f,.1f));
                Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
            }
            var source=Unit("source",BoardSide.Blue,2,8);
            var first=Unit("first",BoardSide.Red,2,7);
            var second=Unit("second",BoardSide.Red,5,0);
            var sim=new CombatSimulation(board);
            Assert.IsNull(sim.CurrentTarget(source));
            sim.Begin(new List<BattleUnit>{source,first,second});sim.Step(.1f);
            Assert.AreSame(first,sim.CurrentTarget(source));
            float elapsed=sim.Elapsed;var cell=source.CurrentCell;int health=first.CurrentHealth;
            for(int i=0;i<100;i++)Assert.AreSame(first,sim.CurrentTarget(source));
            Assert.AreEqual(elapsed,sim.Elapsed);Assert.AreSame(cell,source.CurrentCell);Assert.AreEqual(health,first.CurrentHealth);
            first.ApplyDamage(1000);Assert.IsNull(sim.CurrentTarget(source));
            sim.Step(1f);Assert.AreSame(second,sim.CurrentTarget(source),"Pursuit is visible even before an attack.");
            source.ApplyDamage(1000);Assert.IsNull(sim.CurrentTarget(source));
        }
        finally { Object.DestroyImmediate(root); }
    }
}

using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

public sealed class October7AbilitiesTests
{
    readonly List<GameObject> objects=new List<GameObject>();
    BoardManager board;CombatSimulation simulation;
    [SetUp] public void Setup(){var go=new GameObject("board");objects.Add(go);board=go.AddComponent<BoardManager>();board.BuildBoard();}
    [TearDown] public void Cleanup(){simulation?.Stop();foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);objects.Clear();}
    BattleUnit Unit(UnitDefinition definition,BoardSide side,int x,int y,params int[] tricks)
    {
        var owned=new OwnedUnit(definition);foreach(int i in tricks)owned.Tricks[i]=true;
        var go=new GameObject(side+"-"+definition.Id);objects.Add(go);var unit=go.AddComponent<WhelpUnit>();
        unit.Initialize(go.name,side,UnitStats.FromDefinition(definition,owned));Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
    }
    BattleUnit Target()=>Unit(new UnitDefinition{Id="target",MaxHealth=10000,Damage=0,AttackRange=20,AttackInterval=100,MoveInterval=100},BoardSide.Red,2,4);
    BattleUnit Blotan(bool upgraded)
    {
        var def=DocumentedRoster.CreateBlotan();def.MaxHealth=10000;def.Damage=1;def.AttackRange=20;def.MoveInterval=100;
        def.BaseAbility.Parameters=new[]{new AbilityParameter{Key="interval",Value=2}};
        return Unit(def,BoardSide.Blue,2,5,upgraded?new[]{1}:new int[0]);
    }
    void Begin(params BattleUnit[] units){simulation=new CombatSimulation(board,417);simulation.Begin(units);}
    [Test] public void IncomeTeleportsOnceToFreeCellAndConcealsForExactlyHalfASecond()
    {
        var blotan=Blotan(true);var target=Target();var origin=blotan.CurrentCell;Begin(blotan,target);
        int income=0,teleports=0;simulation.CurrencyEarned+=(s,n)=>income+=n;
        simulation.Teleported+=(actor,from,to)=>{teleports++;Assert.AreSame(origin,from);Assert.AreSame(actor,to.OccupiedBy);};
        simulation.Step(1.9f);Assert.IsFalse(blotan.IsInvisible);Assert.AreEqual(0,income);
        simulation.Step(.1f);Assert.AreEqual(1,income);Assert.AreEqual(1,teleports);Assert.IsTrue(blotan.IsInvisible);
        Assert.AreNotSame(origin,blotan.CurrentCell);Assert.IsNull(origin.OccupiedBy);
        Assert.LessOrEqual(Mathf.Abs(origin.X-blotan.CurrentCell.X),2);
        Assert.LessOrEqual(Mathf.Abs(origin.Y-blotan.CurrentCell.Y),2);
        var landing=blotan.CurrentCell;simulation.Step(.4f);Assert.IsTrue(blotan.IsInvisible);Assert.AreSame(landing,blotan.CurrentCell);
        simulation.Step(.1f);Assert.IsFalse(blotan.IsInvisible);
    }
    [TestCase(0,2)] [TestCase(1,0)] [TestCase(0,0)]
    public void EscapeHonorsEditedRadiiAndDuration(int radiusX,int radiusY)
    {
        var def=DocumentedRoster.CreateBlotan();def.MaxHealth=10000;def.MoveInterval=100;def.AttackRange=20;
        def.BaseAbility.Parameters=new[]{new AbilityParameter{Key="interval",Value=2}};
        def.Tricks[1].Parameters=new[]{new AbilityParameter{Key="duration",Value=.8f},
            new AbilityParameter{Key="radiusX",Value=radiusX},new AbilityParameter{Key="radiusY",Value=radiusY}};
        var blotan=Unit(def,BoardSide.Blue,2,5,1);var origin=blotan.CurrentCell;Begin(blotan,Target());
        simulation.Step(2);
        Assert.IsTrue(blotan.IsInvisible);
        Assert.LessOrEqual(Mathf.Abs(origin.X-blotan.CurrentCell.X),radiusX);
        Assert.LessOrEqual(Mathf.Abs(origin.Y-blotan.CurrentCell.Y),radiusY);
        simulation.Step(.7f);Assert.IsTrue(blotan.IsInvisible);
        simulation.Step(.1f);Assert.IsFalse(blotan.IsInvisible);
    }
    [Test] public void BlockedLocalRadiusNeverEscapesToADistantFreeTile()
    {
        var blotan=Blotan(true);var enemy=Target();var origin=blotan.CurrentCell;
        foreach(var cell in board.GetAllCells().Where(c=>c.OccupiedBy==null&&Mathf.Abs(c.X-origin.X)<=2&&Mathf.Abs(c.Y-origin.Y)<=2))
            Unit(new UnitDefinition{Id="block-"+cell.X+"-"+cell.Y},BoardSide.Blue,cell.X,cell.Y);
        Begin(blotan,enemy);simulation.Step(2);
        Assert.AreSame(origin,blotan.CurrentCell);Assert.IsTrue(blotan.IsInvisible);
        Assert.IsTrue(board.GetAllCells().Any(c=>c.OccupiedBy==null));
    }
    [Test] public void BasicIncomeDoesNotConcealOrTeleportWithoutSecondUpgrade()
    {
        var blotan=Blotan(false);var origin=blotan.CurrentCell;Begin(blotan,Target());
        simulation.Step(2);Assert.IsFalse(blotan.IsInvisible);Assert.AreSame(origin,blotan.CurrentCell);
    }
    [Test] public void FullBoardStillPaysAndConcealsWithoutOverwritingOccupants()
    {
        var blotan=Blotan(true);var enemy=Target();
        foreach(var cell in board.GetAllCells().Where(c=>c.OccupiedBy==null))
            Unit(new UnitDefinition{Id="filler-"+cell.X+"-"+cell.Y},BoardSide.Blue,cell.X,cell.Y);
        var origin=blotan.CurrentCell;Begin(blotan,enemy);int coins=0;simulation.CurrencyEarned+=(s,n)=>coins+=n;
        simulation.Step(2);Assert.AreEqual(1,coins);Assert.IsTrue(blotan.IsInvisible);Assert.AreSame(origin,blotan.CurrentCell);
        Assert.AreEqual(60,board.GetAllCells().Count(c=>c.OccupiedBy!=null));
    }
    [Test] public void ChocolateBurstsFollowPeriodicDamageAndStopAfterRootExpires()
    {
        var def=DocumentedRoster.CreateTsu();def.Damage=0;def.AttackInterval=100;def.AttackWindup=.1f;
        def.BaseAbility.Parameters=new[]{new AbilityParameter{Key="every",Value=1},new AbilityParameter{Key="duration",Value=2}};
        var tsu=Unit(def,BoardSide.Blue,2,5,1);var target=Target();Begin(tsu,target);
        var bursts=new List<float>();var damage=new List<int>();
        simulation.AreaEffect+=(actor,kind,cells)=>{if(kind=="tsu-chocolate"){bursts.Add(simulation.Elapsed);Assert.AreSame(tsu,actor);Assert.AreSame(target.CurrentCell,cells.Single());}};
        simulation.Impacted+=(actor,victim,n)=>{if(n>0&&actor==tsu)damage.Add(n);};
        simulation.Step(4);
        Assert.AreEqual(2,bursts.Count);Assert.That(bursts[1]-bursts[0],Is.EqualTo(1).Within(.001f));
        CollectionAssert.AreEqual(new[]{2,4},damage);Assert.AreEqual(9994,target.CurrentHealth);Assert.IsFalse(target.IsRooted);
    }
}

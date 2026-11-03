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
    public sealed class AbilityAreaShapeTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<GameObject> objects=new List<GameObject>();
        BoardManager board;CombatSimulation simulation;
        [TearDown] public void Cleanup() { simulation?.Stop();foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);objects.Clear();simulation=null; }
        BattleUnit Unit(UnitDefinition definition,int x,int y,BoardSide side)
        {
            var go=new GameObject(definition.Id+objects.Count);objects.Add(go);
            var unit=go.AddComponent<WhelpUnit>();unit.Initialize(go.name,side,UnitStats.FromDefinition(definition));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
        }
        object Actor(BattleUnit unit)=>((IEnumerable)typeof(CombatSimulation).GetField("actors",Hidden).GetValue(simulation))
            .Cast<object>().Single(a=>a.GetType().GetField("Unit").GetValue(a)==(object)unit);
        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void PokrakUsesExactFixedRectangleUnionForRecipientsAndEvent(BoardSide side)
        {
            var go=new GameObject("shape-board");objects.Add(go);board=go.AddComponent<BoardManager>();board.BuildBoard();
            var definition=DocumentedRoster.CreatePokrak();
            definition.BaseAbility.Area=new AbilityAreaDefinition{Shape=AbilityAreaShape.Cross,UpWidth=3,UpDepth=4,RightWidth=3,RightDepth=2};
            var caster=Unit(definition,2,4,side);var units=new List<BattleUnit>{caster};
            for(int y=0;y<BoardManager.Height;y++)for(int x=0;x<BoardManager.Width;x++)
                if(x!=2||y!=4)units.Add(Unit(new UnitDefinition{Id="ally",MaxHealth=1000,Damage=0,AttackRange=1,AttackInterval=100,MoveInterval=100},x,y,
                    x==5&&y==0?(side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue):side));
            simulation=new CombatSimulation(board);simulation.Begin(units);
            var events=new List<BoardCell[]>();simulation.AreaEffect+=(u,k,c)=>{if(k=="edited-area:pokrak-bubbles")events.Add(c);};
            typeof(CombatSimulation).GetMethod("CastPokrakBubbles",Hidden).Invoke(simulation,new[]{Actor(caster)});
            var expected=AbilityAreaResolver.Resolve(definition.BaseAbility.Area,2,4,BoardManager.Width,BoardManager.Height);
            Assert.AreEqual(1,events.Count);Assert.AreEqual(events[0].Length,events[0].Distinct().Count());
            CollectionAssert.AreEquivalent(expected,events[0].Select(c=>new AbilityAreaCell(c.X,c.Y)));
            foreach(var unit in units)
                Assert.AreEqual(unit.Side==side&&events[0].Contains(unit.CurrentCell)?.2f:0,simulation.PokrakBubbleReduction(unit),.0001f,unit.UnitId);
            Assert.AreEqual(0,simulation.PokrakBubbleReduction(caster),"Disabled center must not be implicitly included.");
        }
        [Test] public void CombatSnapshotCopiesAreaAndPreservesUnrelatedSavedValues()
        {
            var definition=DocumentedRoster.CreatePokrak();definition.BaseAbility.Cost=7;
            definition.BaseAbility.Parameters=new[]{new AbilityParameter{Key="width",Value=2},new AbilityParameter{Key="reduction",Value=31}};
            definition.BaseAbility.Area=new AbilityAreaDefinition{Shape=AbilityAreaShape.Cross,UpWidth=3,UpDepth=4};
            var snapshot=UnitAbilityOwnership.ForCombat(definition);
            Assert.AreEqual(7,snapshot.BaseAbility.Cost);
            Assert.AreEqual(2,snapshot.BaseAbility.Parameters.Single(p=>p.Key=="width").Value);
            Assert.AreEqual(31,snapshot.BaseAbility.Parameters.Single(p=>p.Key=="reduction").Value);
            var stats=UnitStats.FromDefinition(definition);definition.BaseAbility.Area.UpDepth=9;
            Assert.AreEqual(4,snapshot.BaseAbility.Area.UpDepth);Assert.AreEqual(4,stats.AreaFor("pokrak-bubbles").UpDepth);
            var returned=stats.AreaFor("pokrak-bubbles");returned.UpDepth=0;Assert.AreEqual(4,stats.AreaFor("pokrak-bubbles").UpDepth);
        }
        [Test] public void OriginalPreservesEvenWidthAndAbsentAreaWithoutWritingAssets()
        {
            var definition=DocumentedRoster.CreatePokrak();definition.BaseAbility.Area=null;
            definition.BaseAbility.Parameters=new[]{new AbilityParameter{Key="width",Value=2}};
            var stats=UnitStats.FromDefinition(definition);
            Assert.IsNull(stats.AreaFor("pokrak-bubbles"));
            Assert.AreEqual(9,CombatSimulation.PokrakBubbleCells(new Vector2Int(2,4),2).Count);
            var preview=AbilityAreaCatalog.OriginalPreview("pokrak-bubbles",(effect,key,fallback)=>key=="width"?2:fallback,_=>false,2,4);
            Assert.AreEqual(9,preview.Length);Assert.IsNull(definition.BaseAbility.Area);
        }
        [Test] public void CrossAllowsEvenDepthAndClipsWithoutDoubleCounting()
        {
            var shape=new AbilityAreaDefinition{Shape=AbilityAreaShape.Cross,UpWidth=3,UpDepth=4,RightWidth=3,RightDepth=4};
            var cells=AbilityAreaResolver.Resolve(shape,4,4,9,9);
            Assert.AreEqual(23,cells.Length);Assert.AreEqual(cells.Length,cells.Distinct().Count());
            Assert.IsFalse(cells.Contains(new AbilityAreaCell(4,4)));
            Assert.AreEqual(0,AbilityAreaResolver.Resolve(new AbilityAreaDefinition{Shape=AbilityAreaShape.Square,SquareSize=0},4,4,9,9).Length);
        }
        [Test] public void TargetingAndTravellingProjectilesAreNotAreaEditors()
        {
            foreach(string effect in new[]{"antidota-floracion","suki-fish-shield","stein-stun","rosie-healing-rose","maestro-de-las-mareas-oleaje","dagata-doble-corte"})
                Assert.IsFalse(AbilityAreaCatalog.Supports(effect),effect);
        }
    }
}

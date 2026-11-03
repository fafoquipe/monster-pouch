using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEditor;
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
        BattleUnit Unit(UnitDefinition definition,int x,int y,BoardSide side,int trick=-1)
        {
            var go=new GameObject(definition.Id+objects.Count);objects.Add(go);
            OwnedUnit owned=null;if(trick>=0){owned=new OwnedUnit(definition);owned.Tricks[trick]=true;}
            var unit=go.AddComponent<WhelpUnit>();unit.Initialize(go.name,side,UnitStats.FromDefinition(definition,owned));
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
        [TestCase(0,1,1)] [TestCase(1,5,3)] [TestCase(2,13,6)] [TestCase(3,24,10)]
        public void DiamondUsesManhattanRadiusAndClipsAtBoardEdges(int radius,int centerCount,int cornerCount)
        {
            var area=new AbilityAreaDefinition{Shape=AbilityAreaShape.Diamond,DiamondRadius=radius};
            var center=AbilityAreaResolver.Resolve(area,2,4,BoardManager.Width,BoardManager.Height);
            var corner=AbilityAreaResolver.Resolve(area,0,0,BoardManager.Width,BoardManager.Height);
            Assert.AreEqual(centerCount,center.Length);Assert.AreEqual(cornerCount,corner.Length);
            Assert.AreEqual(center.Length,center.Distinct().Count());Assert.AreEqual(corner.Length,corner.Distinct().Count());
            foreach(var cell in center)Assert.LessOrEqual(Math.Abs(cell.X-2)+Math.Abs(cell.Y-4),radius);
            foreach(var cell in corner)Assert.LessOrEqual(cell.X+cell.Y,radius);
        }
        [Test] public void DiamondHandlesInvalidAndExtremeValuesWithoutOverflow()
        {
            var area=new AbilityAreaDefinition{Shape=AbilityAreaShape.Diamond,DiamondRadius=-1};
            Assert.IsEmpty(AbilityAreaResolver.Resolve(area,2,4,BoardManager.Width,BoardManager.Height));
            area.DiamondRadius=int.MaxValue;
            Assert.AreEqual(BoardManager.Width*BoardManager.Height,AbilityAreaResolver.Resolve(area,2,4,BoardManager.Width,BoardManager.Height).Length);
            Assert.IsFalse(AbilityAreaResolver.Contains(area,int.MinValue,0));
            Assert.IsEmpty(AbilityAreaResolver.Resolve(area,int.MinValue,0,BoardManager.Width,BoardManager.Height));
        }
        [Test] public void DiamondRadiusSurvivesSerializationAndDetachedCombatCopies()
        {
            var ability=ScriptableObject.CreateInstance<CharacterAbility>();
            try
            {
                var serialized=new SerializedObject(ability);
                var area=serialized.FindProperty("Definition").FindPropertyRelative("Area");
                area.FindPropertyRelative("Shape").enumValueIndex=(int)AbilityAreaShape.Diamond;
                area.FindPropertyRelative("DiamondRadius").intValue=3;
                serialized.ApplyModifiedProperties();
                var editorType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchAbilityAreaEditor"))
                    .FirstOrDefault(t=>t!=null);
                Assert.NotNull(editorType,"The shared Unity area editor must be available.");
                var preview=(AbilityAreaDefinition)editorType.GetMethod("Read",BindingFlags.Public|BindingFlags.Static)
                    .Invoke(null,new object[]{area});
                Assert.AreEqual(AbilityAreaShape.Diamond,preview.Shape);Assert.AreEqual(3,preview.DiamondRadius);
                var copy=ability.Copy();
                Assert.AreEqual(AbilityAreaShape.Diamond,copy.Area.Shape);Assert.AreEqual(3,copy.Area.DiamondRadius);
                var definition=DocumentedRoster.CreatePokrak();definition.BaseAbility.Area=copy.Area;
                var stats=UnitStats.FromDefinition(definition);copy.Area.DiamondRadius=1;
                Assert.AreEqual(3,stats.AreaFor("pokrak-bubbles").DiamondRadius);
                var returned=stats.AreaFor("pokrak-bubbles");returned.DiamondRadius=0;
                Assert.AreEqual(3,stats.AreaFor("pokrak-bubbles").DiamondRadius);
            }
            finally{Object.DestroyImmediate(ability);}
        }
        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void DiamondHealingTrickUsesTheSharedAreaAndExcludesTheSeparateSelfHeal(BoardSide side)
        {
            var go=new GameObject("healing-board");objects.Add(go);board=go.AddComponent<BoardManager>();board.BuildBoard();
            var definition=EronyaRoster.CreateAll().Single(d=>d.Id=="arboreo");
            definition.Tricks[2].Area=new AbilityAreaDefinition{Shape=AbilityAreaShape.Diamond,DiamondRadius=1};
            var caster=Unit(definition,2,4,side,2);caster.IsPassiveTrainingTarget=true;
            var adjacent=Unit(new UnitDefinition{Id="adjacent",MaxHealth=100},3,4,side);adjacent.IsPassiveTrainingTarget=true;
            var diagonal=Unit(new UnitDefinition{Id="diagonal",MaxHealth=100},3,3,side);diagonal.IsPassiveTrainingTarget=true;
            var enemy=Unit(new UnitDefinition{Id="enemy",MaxHealth=1000},5,9,side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue);
            enemy.IsPassiveTrainingTarget=true;
            simulation=new CombatSimulation(board);simulation.Begin(new[]{caster,adjacent,diagonal,enemy});
            caster.ApplyDamage(90);adjacent.ApplyDamage(30);diagonal.ApplyDamage(30);
            var footprints=new List<BoardCell[]>();
            simulation.AreaEffect+=(unit,effect,cells)=>{if(unit==caster&&effect=="edited-area:arboreo-corazon-ancestral")footprints.Add(cells);};
            simulation.Step(.1f);
            Assert.AreEqual(47,caster.CurrentHealth);Assert.AreEqual(80,adjacent.CurrentHealth);Assert.AreEqual(70,diagonal.CurrentHealth);
            Assert.AreEqual(1,footprints.Count);
            CollectionAssert.AreEquivalent(new[]{board.GetCell(1,4),board.GetCell(3,4),board.GetCell(2,3),board.GetCell(2,5)},footprints[0]);
        }
        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void DiamondShieldTrickProtectsOnlyAlliesInItsArea(BoardSide side)
        {
            var go=new GameObject("shield-board");objects.Add(go);board=go.AddComponent<BoardManager>();board.BuildBoard();
            var definition=EronyaRoster.CreateAll().Single(d=>d.Id=="korran");
            definition.Tricks[0].Area=new AbilityAreaDefinition{Shape=AbilityAreaShape.Diamond,DiamondRadius=1};
            var caster=Unit(definition,2,4,side,0);
            var adjacent=Unit(new UnitDefinition{Id="adjacent",MaxHealth=100},3,4,side);adjacent.IsPassiveTrainingTarget=true;
            var diagonal=Unit(new UnitDefinition{Id="diagonal",MaxHealth=100},3,3,side);diagonal.IsPassiveTrainingTarget=true;
            var enemy=Unit(new UnitDefinition{Id="enemy",MaxHealth=1000},5,9,side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue);
            enemy.IsPassiveTrainingTarget=true;
            simulation=new CombatSimulation(board);simulation.Begin(new[]{caster,adjacent,diagonal,enemy});
            var footprints=new List<BoardCell[]>();
            simulation.AreaEffect+=(unit,effect,cells)=>{if(unit==caster&&effect=="edited-area:korran-raiz-de-piedra")footprints.Add(cells);};
            caster.AddEnergy(caster.BaseStats.EnergyMax);
            for(int i=0;i<30&&footprints.Count==0;i++)simulation.Step(.1f);
            Assert.AreEqual(1,footprints.Count);Assert.AreEqual(8,adjacent.Shield);Assert.AreEqual(0,diagonal.Shield);
            CollectionAssert.AreEquivalent(new[]{board.GetCell(1,4),board.GetCell(3,4),board.GetCell(2,3),board.GetCell(2,5)},footprints[0]);
        }
        [Test] public void TargetingAndTravellingProjectilesAreNotAreaEditors()
        {
            foreach(string effect in new[]{"antidota-floracion","suki-fish-shield","stein-stun","rosie-healing-rose","maestro-de-las-mareas-oleaje","dagata-doble-corte"})
                Assert.IsFalse(AbilityAreaCatalog.Supports(effect),effect);
        }
    }
}

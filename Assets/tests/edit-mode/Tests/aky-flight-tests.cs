using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class AkyFlightTests
    {
        readonly List<Object> created=new List<Object>();
        BoardManager board; CombatSimulation simulation;
        [SetUp] public void Setup(){var go=New("board");board=go.AddComponent<BoardManager>();board.BuildBoard();simulation=new CombatSimulation(board);}
        GameObject New(string name){var go=new GameObject(name);created.Add(go);return go;}
        [TearDown] public void Cleanup(){simulation?.Stop();for(int i=created.Count-1;i>=0;i--)if(created[i]!=null)Object.DestroyImmediate(created[i]);created.Clear();}
        BattleUnit Unit(UnitDefinition definition,int x,int y,BoardSide side,bool flight=false)
        {
            var owned=new OwnedUnit(definition);owned.Tricks[0]=flight;
            var unit=New(definition.Id+created.Count).AddComponent<WhelpUnit>();
            unit.Initialize(unit.name,side,UnitStats.FromDefinition(definition,owned));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
        }
        UnitDefinition Aky(float duration)
        {
            var definition=DocumentedRoster.CreateAky();definition.MaxHealth=1000;
            definition.Tricks[0].Parameters=new[]{new AbilityParameter{Key="duration",Value=duration}};
            return definition;
        }
        BattleUnit Enemy(BoardSide side=BoardSide.Red,int y=0)=>Unit(new UnitDefinition{Id="enemy",MaxHealth=1000,Damage=10,AttackRange=9,AttackInterval=.3f,AttackWindup=.1f,MoveInterval=100},3,y,side);

        [TestCase(BoardSide.Blue,.7f)] [TestCase(BoardSide.Red,2.3f)]
        public void FlightUsesEditedDurationAndEnemiesAcquireOnlyAfterLanding(BoardSide side,float duration)
        {
            int end=side==BoardSide.Blue?0:9;
            var aky=Unit(Aky(duration),2,9-end,side,true);
            var enemy=Enemy(side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue,end);
            int attacks=0;simulation.Attacked+=(a,t,p)=>attacks++;
            simulation.Begin(new[]{aky,enemy});
            Assert.IsTrue(aky.IsFlying);Assert.AreEqual(duration,aky.FlightDuration,.0001f);
            Assert.AreEqual(end,aky.CurrentCell.Y);Assert.AreSame(aky,board.GetCell(2,end).OccupiedBy);
            Assert.AreEqual(0,aky.ApplyDamage(900));
            simulation.Step(duration-.1f);
            Assert.IsTrue(aky.IsFlying);Assert.AreEqual(0,attacks);Assert.IsFalse(simulation.Finished);
            simulation.Step(.1f);
            Assert.IsFalse(aky.IsFlying);Assert.Greater(attacks,0);
            simulation.Step(.2f);Assert.Less(aky.CurrentHealth,aky.BaseStats.MaxHealth);
        }
        [Test] public void FlightIgnoresAreaDamageAndDoesNotTriggerGroundTraps()
        {
            var aky=Unit(Aky(3),2,9,BoardSide.Blue,true);
            var ally=Unit(new UnitDefinition{Id="ally",MaxHealth=1000,Damage=0,MoveInterval=100,AttackInterval=100},3,0,BoardSide.Blue);
            var definition=DocumentedRoster.CreateGochan();definition.AttackWindup=.1f;definition.ProjectileSpeed=100;
            var enemy=Unit(definition,4,0,BoardSide.Red);
            simulation.Begin(new[]{aky,ally,enemy});simulation.Step(.5f);
            Assert.IsTrue(aky.IsFlying);Assert.AreEqual(1000,aky.CurrentHealth);Assert.Less(ally.CurrentHealth,1000);
        }
        [Test] public void ResetAndStopClearFlightState()
        {
            var aky=Unit(Aky(3),2,9,BoardSide.Blue,true);var enemy=Enemy();
            simulation.Begin(new[]{aky,enemy});aky.ResetForCombat();simulation.Step(.1f);Assert.IsFalse(aky.IsFlying);
            simulation.Begin(new[]{aky,enemy}); // Already at the far edge: no flight to the same cell.
            Assert.IsFalse(aky.IsFlying);
            board.TryRepositionUnit(aky,board.GetCell(2,9));simulation.Begin(new[]{aky,enemy});Assert.IsTrue(aky.IsFlying);
            simulation.Stop();Assert.IsFalse(aky.IsFlying);
        }
        [Test] public void BlockedBackCellUsesNearestFreeLandingCell()
        {
            var aky=Unit(Aky(1),2,9,BoardSide.Blue,true);var enemy=Enemy();
            var blocker=Unit(new UnitDefinition{Id="blocker",Damage=0},2,0,BoardSide.Red);
            simulation.Begin(new[]{aky,enemy,blocker});Assert.AreEqual(1,aky.CurrentCell.Y);Assert.IsTrue(aky.IsFlying);
        }
        [Test] public void FlightRemainsVisibleAndTraversesTheBoardOverItsConfiguredTime()
        {
            var aky=Unit(Aky(2),2,9,BoardSide.Blue,true);var enemy=Enemy();
            var mapper=board.gameObject.AddComponent<BoardWorldMapper>();mapper.Configure(board,board.transform,Vector2.one,Vector2.zero);
            var texture=new Texture2D(4,4);created.Add(texture);var sprite=Sprite.Create(texture,new Rect(0,0,4,4),new Vector2(.5f,0),4);created.Add(sprite);
            var art=new UnitArt{Id="aky",Portrait=sprite,ReferencePixelWidth=4};
            for(int i=0;i<8;i++)art.DirectionalAnimations[i]=new UnitDirectionalAnimation{Idle=new[]{sprite},Special=new[]{sprite}};
            var view=aky.gameObject.AddComponent<UnitPresentation>();view.Configure(aky,mapper,art);
            Vector3 start=mapper.GetWorldPosition(aky.CurrentCell),end=mapper.GetWorldPosition(board.GetCell(2,0));
            simulation.Moved+=(actor,from,to)=>{if(actor==aky)view.Move(mapper.GetWorldPosition(from),mapper.GetWorldPosition(to),actor.BaseStats.MoveInterval);};
            simulation.Begin(new[]{aky,enemy});simulation.Step(1);view.AdvancePresentation(1);
            Assert.AreEqual(Vector3.Lerp(start,end,.5f),aky.transform.position);
            Assert.IsTrue(view.Renderer.enabled);Assert.AreEqual(1,view.Renderer.color.a);Assert.Greater(view.VisualRoot.localPosition.y,0);
            simulation.Step(1);view.AdvancePresentation(1);
            Assert.IsFalse(aky.IsFlying);Assert.AreEqual(end,aky.transform.position);
        }
        [Test] public void DescriptionUsesEditedDurationAndMinimumCombatTick()
        {
            var definition=Aky(2.4f);
            Assert.That(AbilityBalance.Description(definition.Tricks[0],definition),Does.Contain("2.4 s"));
            definition.Tricks[0].Parameters[0].Value=0;
            Assert.That(AbilityBalance.Description(definition.Tricks[0],definition),Does.Contain("0.1 s"));
        }
    }
}

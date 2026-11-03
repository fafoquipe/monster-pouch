using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public class GddV4ResolutionTests
    {
        GameObject root;BoardManager board;
        [SetUp] public void Setup(){root=new GameObject("gdd-v4");board=root.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);Time.timeScale=1;}
        BattleUnit Add(string id,BoardSide side,int hp,int x,int y,bool monster=false,int iq=1)
        {
            var go=new GameObject(id);go.transform.SetParent(root.transform);
            BattleUnit u=monster?(BattleUnit)go.AddComponent<MonsterUnit>():go.AddComponent<WhelpUnit>();
            u.Initialize(id,side,new UnitStats(hp,0,iqSpeed:iq));Assert.IsTrue(board.TryOccupyCell(u,x,y));return u;
        }
        [Test] public void TimeoutCountsCurrentMonsterHealthAndRoundedUpHalfOfCurrentWhelpHealth()
        {
            var blue=Add("blue",BoardSide.Blue,10,2,6,true);
            var ally=Add("ally",BoardSide.Blue,9,0,7);ally.ApplyDamage(4);
            var red=Add("red",BoardSide.Red,11,2,2,true);
            var result=RoundTiebreakers.Timeout(new[]{blue,ally,red});
            Assert.AreEqual(10,result.AbsorbedHealth[blue]);
            Assert.AreEqual(13,result.Health.BlueTotal);
            Assert.AreEqual(3,result.AbsorbedHealth[ally]);
            Assert.AreEqual(24,result.AbsorbedHealth.Values.Sum());
            Assert.AreEqual(10,blue.CurrentHealth);Assert.AreEqual(5,ally.CurrentHealth);
            Assert.AreEqual(2,result.BlueRemaining);Assert.AreEqual(0,result.RedRemaining);
            Assert.AreEqual(BoardSide.Blue,result.Winner);
            var reversed=RoundTiebreakers.Timeout(new[]{red,ally,blue});
            Assert.AreEqual(result.Winner,reversed.Winner);Assert.AreEqual(result.BlueRemaining,reversed.BlueRemaining);
        }
        [Test] public void LivingMonsterRemainsTheTieRepresentativeWithoutTakingTheAllysHealth()
        {
            var blue=Add("monster",BoardSide.Blue,2,2,6,true);
            var ally=Add("strong-ally",BoardSide.Blue,20,0,7);
            var red=Add("red",BoardSide.Red,11,2,2,true);
            var result=RoundTiebreakers.Timeout(new[]{blue,ally,red});
            Assert.AreSame(blue,result.Blue);Assert.AreEqual(2,result.AbsorbedHealth[blue]);
            Assert.AreEqual(12,result.Health.BlueTotal);
            Assert.AreEqual(BoardSide.Blue,result.Winner);
        }
        [Test] public void DeadMonsterContributesZeroAndEveryLivingWhelpStillContributesOnlyHalf()
        {
            var blue=Add("monster",BoardSide.Blue,10,2,6,true);blue.ApplyDamage(10);
            var ally=Add("strong",BoardSide.Blue,15,0,7);
            var weak=Add("injured",BoardSide.Blue,30,1,7);weak.ApplyDamage(25);
            var red=Add("red",BoardSide.Red,16,2,2,true);
            var result=RoundTiebreakers.Timeout(new[]{blue,ally,weak,red});
            Assert.AreSame(ally,result.Blue);Assert.IsFalse(result.AbsorbedHealth.ContainsKey(blue));
            Assert.AreEqual(8,result.AbsorbedHealth[ally]);Assert.AreEqual(11,result.Health.BlueTotal);
            Assert.AreEqual(BoardSide.Red,result.Winner);
        }
        [Test] public void InRoundPositionUsesCellBeforeIQAndIQBeforeQuadrant()
        {
            var first=Add("first",BoardSide.Red,10,0,0,iq:1);
            var second=Add("second",BoardSide.Blue,10,3,4,iq:10);
            Assert.AreEqual(BoardPriorityResult.First,BoardPriorityResolver.CompareInRoundPosition(first,second));
            Assert.IsTrue(board.TryRepositionUnit(first,board.GetCell(2,4)));
            Assert.AreEqual(BoardPriorityResult.Second,BoardPriorityResolver.CompareInRoundPosition(first,second));
        }
        [Test] public void SameSideSamePriorityDoesNotGainAnArbitraryWinner()
        {
            Assert.AreEqual(BoardPriorityResult.Unresolved,BoardPriorityResolver.CompareTerritorialAdvantage(BoardSide.Blue,BoardSide.Blue,BoardSide.Blue));
            var one=Add("a",BoardSide.Blue,10,2,5);var two=Add("z",BoardSide.Blue,10,1,6);
            // These cells differ in value; select equal-value cells within the same quadrant.
            board.TryRepositionUnit(one,board.GetCell(2,7));
            board.TryRepositionUnit(two,board.GetCell(1,6));
            Assert.AreEqual(one.CurrentCell.Value,two.CurrentCell.Value);
            Assert.IsNull(RoundTiebreakers.Champion(new[]{one,two}));
        }
        [Test] public void TwoUnitsInWinningQuadrantDoNotLetLosingQuadrantSkipToTerritory()
        {
            var a=Add("A",BoardSide.Red,10,2,0);
            var b=Add("B1",BoardSide.Blue,10,3,0);
            var otherB=Add("B2",BoardSide.Blue,10,4,1);
            var destination=board.GetCell(0,0);
            var result=BoardReservationResolver.Resolve(board,new[]{
                new BoardReservationIntent(a,destination),new BoardReservationIntent(b,destination),new BoardReservationIntent(otherB,destination)});
            Assert.AreEqual(BoardReservationOutcome.Rejected,result[0].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Unresolved,result[1].Outcome);
            Assert.AreEqual(BoardReservationOutcome.Unresolved,result[2].Outcome);
            Assert.IsNull(destination.ReservedBy);
        }
        [Test] public void OpponentEchoesRememberLockInAndNeverExposeTheNextRoster()
        {
            var config=MatchConfig.CreateDefault();
            try
            {
                var mapper=root.AddComponent<BoardWorldMapper>();mapper.Configure(board,root.transform,Vector2.one,Vector2.zero);
                var match=root.AddComponent<MatchController>();match.Configure(config,board,mapper);match.StartMatch("bugaloo",12);
                Assert.IsEmpty(match.PreviousOpponent);Assert.IsTrue(match.Actors.Values.All(u=>u.Side==BoardSide.Blue));
                Assert.IsNotNull(match.InitialOpponent);
                var initial=match.InitialOpponent.Position;
                var ownDefault=config.FormationCells(match.Bot.Monster.Definition,BoardSide.Blue).First();
                Assert.AreEqual(new Vector2Int(5-ownDefault.x,9-ownDefault.y),initial);
                match.Bot.TrySetLocation(match.Bot.Monster.Definition.Id,UnitLocation.Field,new Vector2Int(0,0),out _);
                Assert.AreEqual(initial,match.InitialOpponent.Position,"Round one shows only the default tile, never the bot's new move.");
                match.Ready();var snapshot=match.PreviousOpponent.Select(e=>(e.UnitId,e.Position,e.Round)).ToArray();
                Assert.Greater(snapshot.Length,0);Assert.IsTrue(match.Actors.Values.Any(u=>u.Side==BoardSide.Red));
                for(int i=0;i<160&&match.Phase==MatchPhase.Combat;i++)
                {
                    foreach(var actor in match.Actors.Values.Where(u=>u.Side==BoardSide.Red&&u.IsAlive).ToArray())actor.ApplyDamage(actor.CurrentHealth);
                    match.Advance(.1f);
                }
                Assert.AreEqual(MatchPhase.RoundResult,match.Phase);match.NextRound();
                Assert.IsTrue(match.Actors.Values.All(u=>u.Side==BoardSide.Blue));
                CollectionAssert.AreEqual(snapshot,match.PreviousOpponent.Select(e=>(e.UnitId,e.Position,e.Round)).ToArray());
                var before=match.PreviousOpponent.Select(e=>e.Position).ToArray();
                match.Bot.TrySetLocation(match.Bot.Monster.Definition.Id,UnitLocation.Field,new Vector2Int(0,0),out _);
                CollectionAssert.AreEqual(before,match.PreviousOpponent.Select(e=>e.Position).ToArray());
                match.Menu();Assert.IsEmpty(match.PreviousOpponent);
            }
            finally{Object.DestroyImmediate(config);}
        }
    }
}


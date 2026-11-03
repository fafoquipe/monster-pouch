using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

public sealed class BriefCopyUpgradeTests
{
    MatchConfig config;MatchController host,guest;
    [SetUp] public void Setup()
    {
        config=MatchConfig.CreateDocumented();config.RoundIncome=new[]{100};
        host=Create("Brief copy host");guest=Create("Brief copy guest");
        Assert.IsTrue(host.StartOnlineMatch(BoardSide.Blue,"bugaloo",new[]{"atori"},"popow",new[]{"atori"},741));
        Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Red,"popow",new[]{"atori"},"bugaloo",new[]{"atori"},741));
    }
    MatchController Create(string name)
    {
        var go=new GameObject(name);var board=go.AddComponent<BoardManager>();board.BuildBoard();
        var mapper=go.AddComponent<BoardWorldMapper>();mapper.Configure(board,go.transform,new Vector2(1,-1),Vector2.zero);
        var match=go.AddComponent<MatchController>();match.Configure(config,board,mapper);return match;
    }
    [TearDown] public void Cleanup()
    {
        foreach(var match in new[]{host,guest})if(match!=null){match.Menu();Object.DestroyImmediate(match.gameObject);}
        Object.DestroyImmediate(config);Time.timeScale=1;
    }
    OwnedUnit PrepareCopy()
    {
        var initial=host.Player.Offers[0];Assert.AreEqual("atori",initial.UnitId);
        var destination=host.Board.GetAllCells().First(c=>config.IsDeploymentLegal(host.LocalSide,c.Coordinates)&&!c.IsOccupied&&!c.IsReserved&&!c.IsBlocked);
        Assert.IsTrue(host.BuyAndPlace(0,initial.Token,destination));Assert.IsTrue(host.ChooseTrick("atori",2));return host.Player.Owned["atori"];
    }
    static string Snapshot(MatchController match)=>string.Join("|",match.Player.Coins,match.Player.Rerolls,match.Player.OfferVersion,match.Player.PoolCount,
        string.Join(";",match.Player.Offers.Select(o=>o==null?"null":o.UnitId+":"+o.Token)),
        string.Join(";",match.Player.Owned.Values.OrderBy(u=>u.Definition.Id).Select(u=>u.Definition.Id+":"+u.Copies+":"+u.Paid+":"+u.Location+":"+u.Deployment+":"+u.BriefSlot+":"+u.NextTrick+":"+string.Join(",",u.Tricks))),
        string.Join(";",match.Actors.Values.Select(a=>a.GetInstanceID()+":"+a.CurrentCell.Coordinates)),
        string.Join(";",match.PreparationCommands.Select(c=>JsonUtility.ToJson(c))));

    [Test] public void CopyDroppedOnRivalActorUpgradesItsOwnUnitWithoutTouchingTheRival()
    {
        var own=PrepareCopy();var ownActor=host.Actors[own];var ownCell=ownActor.CurrentCell;
        var rivalOwned=host.Bot.Monster;var rivalCell=host.Board.GetCell(rivalOwned.Deployment.x,rivalOwned.Deployment.y);
        var go=new GameObject("Visible rival occupied-cell fixture");go.transform.SetParent(host.transform,false);
        var rival=go.AddComponent<MonsterUnit>();rival.Initialize("test-visible-popow",BoardSide.Red,UnitStats.FromDefinition(rivalOwned.Definition,rivalOwned,false));
        Assert.IsTrue(host.Board.TryOccupyCell(rival,rivalCell.X,rivalCell.Y));
        var offer=host.Player.Offers[1];int balance=host.Player.Coins,price=host.Player.GetOfferCost(1),paid=own.Paid,actors=host.Actors.Count;
        int rivalHealth=rival.CurrentHealth;var rivalTricks=rivalOwned.Tricks.ToArray();int rivalCopies=rivalOwned.Copies;
        Assert.IsTrue(host.BuyAndPlace(1,offer.Token,rivalCell));
        Assert.AreEqual(balance-price,host.Player.Coins);Assert.AreEqual(paid+price,own.Paid);Assert.AreEqual(2,own.Copies);
        CollectionAssert.AreEqual(new[]{false,false,true},own.Tricks);Assert.AreSame(ownActor,host.Actors[own]);
        Assert.AreSame(ownCell,ownActor.CurrentCell);Assert.AreSame(ownActor,ownCell.OccupiedBy);Assert.AreEqual(ownCell.Coordinates,own.Deployment);
        Assert.AreSame(rival,rivalCell.OccupiedBy);Assert.AreSame(rivalCell,rival.CurrentCell);Assert.AreEqual(rivalHealth,rival.CurrentHealth);
        Assert.AreEqual(rivalCopies,rivalOwned.Copies);CollectionAssert.AreEqual(rivalTricks,rivalOwned.Tricks);Assert.AreEqual(actors,host.Actors.Count);
        Assert.IsNull(host.Player.Offers[1]);
    }

    [TestCase("stale")][TestCase("funds")][TestCase("pause")][TestCase("ready")][TestCase("locked")][TestCase("null")][TestCase("foreign")]
    public void CopyRoutingStillRejectsUnavailablePurchasesAndNonBoardDropsWithoutMutating(string rejection)
    {
        if(rejection=="pause")
        {
            // Online preparation intentionally cannot pause; exercise the real local guard.
            host.Menu();Assert.IsTrue(host.StartMatch("bugaloo",new[]{"atori"},741));
            Assert.IsFalse(host.IsOnline);
        }
        PrepareCopy();var offer=host.Player.Offers[1];long token=offer.Token;
        var destination=host.Actors[host.Player.Monster].CurrentCell;
        switch(rejection)
        {
            case "stale":token++;break;
            case "funds":host.Player.AddCombatCoins(-host.Player.Coins);break;
            case "pause":host.Pause(true);Assert.IsTrue(host.Paused);Assert.AreEqual(0,Time.timeScale);break;
            case "ready":Assert.IsTrue(host.SetReady(true));break;
            case "locked":host.LockPreparation(true);break;
            case "null":destination=null;break;
            case "foreign":destination=new BoardCell(destination.X,destination.Y,destination.Value,destination.Quadrant,destination.Side);break;
        }
        string before=Snapshot(host);Assert.IsFalse(host.BuyAndPlace(1,token,destination));Assert.AreEqual(before,Snapshot(host));
    }

    [Test] public void CopyDropRecordsOneNormalPurchaseAndBothPeersReplayTheSelectedTrickIdentically()
    {
        var own=PrepareCopy();var actor=host.Actors[own];var originalCell=actor.CurrentCell;
        var monster=host.Player.Monster;var monsterActor=host.Actors[monster];var monsterCell=monsterActor.CurrentCell;
        int commands=host.PreparationCommands.Length;var offer=host.Player.Offers[1];
        Assert.IsTrue(host.BuyAndPlace(1,offer.Token,monsterCell));
        Assert.AreEqual(commands+1,host.PreparationCommands.Length);
        CollectionAssert.AreEqual(new[]{"buy-place","choose","buy"},host.PreparationCommands.Select(c=>c.kind));
        Assert.AreEqual(2,host.PreparationCommands[1].index);Assert.AreEqual(1,host.PreparationCommands.Last().slot);
        Assert.AreSame(actor,host.Actors[own]);Assert.AreSame(originalCell,actor.CurrentCell);Assert.AreSame(monsterActor,host.Actors[monster]);Assert.AreSame(monsterCell,monsterActor.CurrentCell);
        string before=Snapshot(host);Assert.IsFalse(host.BuyAndPlace(1,offer.Token,monsterCell));Assert.AreEqual(before,Snapshot(host));
        host.Ready();guest.Ready();host.SetOpponentReady();guest.SetOpponentReady();
        Assert.IsTrue(host.RevealOpponent(guest.PreparationCommands,1));Assert.IsTrue(guest.RevealOpponent(host.PreparationCommands,1));
        Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
        var replay=guest.Bot.Owned["atori"];Assert.AreEqual(own.Deployment,replay.Deployment);Assert.AreEqual(own.Copies,replay.Copies);CollectionAssert.AreEqual(own.Tricks,replay.Tricks);
        Assert.AreEqual(host.Player.Coins,guest.Bot.Coins);
    }

    [Test] public void CopyDropsConsumeOnlyTheFourExistingCopiesWithoutDuplicatingActors()
    {
        var own=PrepareCopy();var actor=host.Actors[own];var cell=actor.CurrentCell;var destination=host.Actors[host.Player.Monster].CurrentCell;
        int actors=host.Actors.Count;
        for(int count=2;count<=4;count++)
        {
            int slot=Enumerable.Range(0,3).Where(i=>host.Player.Offers[i]!=null).DefaultIfEmpty(-1).First();
            if(slot<0){Assert.IsTrue(host.Reroll(host.Player.OfferVersion));slot=Enumerable.Range(0,3).First(i=>host.Player.Offers[i]!=null);}
            var offer=host.Player.Offers[slot];int cost=host.Player.GetOfferCost(slot),balance=host.Player.Coins;
            Assert.IsTrue(host.BuyAndPlace(slot,offer.Token,destination));Assert.AreEqual(balance-cost,host.Player.Coins);Assert.AreEqual(count,own.Copies);
            Assert.AreSame(actor,host.Actors[own]);Assert.AreSame(cell,actor.CurrentCell);Assert.AreEqual(actors,host.Actors.Count);
        }
        Assert.AreEqual(0,host.Player.RemainingCopies("atori"));Assert.IsTrue(own.Tricks.All(active=>active));Assert.IsTrue(host.Player.Offers.All(o=>o==null));
        Assert.IsFalse(host.Reroll(host.Player.OfferVersion));Assert.AreEqual(4,own.Copies);
    }
}

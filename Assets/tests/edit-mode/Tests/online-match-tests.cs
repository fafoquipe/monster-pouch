using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class OnlineMatchTests
{
    MatchConfig config;MatchController host,guest;
    [SetUp] public void Setup()
    {
        config=ScriptableObject.CreateInstance<MatchConfig>();config.Units=DocumentedRoster.CreateAll();config.RoundIncome=new[]{50};
        host=Create("host");guest=Create("guest");
        Assert.IsTrue(host.StartOnlineMatch(BoardSide.Blue,"bugaloo",new[]{"blotan","tsu"},"sepora",new[]{"blotan","tsu"},417));
        Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Red,"sepora",new[]{"blotan","tsu"},"bugaloo",new[]{"blotan","tsu"},417));
    }
    MatchController Create(string name)
    {
        var go=new GameObject(name);var board=go.AddComponent<BoardManager>();board.BuildBoard();
        var mapper=go.AddComponent<BoardWorldMapper>();mapper.Configure(board,go.transform,new Vector2(1,-1),Vector2.zero);
        var match=go.AddComponent<MatchController>();match.Configure(config,board,mapper);return match;
    }
    [TearDown] public void Cleanup()
    {if(host!=null){host.Menu();Object.DestroyImmediate(host.gameObject);}if(guest!=null){guest.Menu();Object.DestroyImmediate(guest.gameObject);}Object.DestroyImmediate(config);}
    void Prepare(MatchController match)
    {
        for(int slot=0;slot<3;slot++)
        {
            var offer=match.Player.Offers[slot];if(offer==null)continue;
            Assert.IsTrue(match.BuyAndPlace(slot,offer.Token,match.Board.GetCell(slot,match.LocalSide==BoardSide.Blue?6:3)),match.Notice);
        }
        Assert.IsTrue(match.Reroll(match.Player.OfferVersion));
        var unit=match.Player.Owned.Values.First(u=>!u.Definition.IsMonster);
        Assert.IsTrue(match.Place(unit.Definition.Id,match.Board.GetCell(5,match.LocalSide==BoardSide.Blue?8:1)));
    }
    void Reveal()
    {
        host.Ready();guest.Ready();host.SetOpponentReady();guest.SetOpponentReady();
        Assert.IsTrue(host.RevealOpponent(guest.PreparationCommands,host.Round));
        Assert.IsTrue(guest.RevealOpponent(host.PreparationCommands,guest.Round));
    }
    [Test] public void BothPeersReplayPurchasesAndReachIdenticalCombatResultsAcrossRounds()
    {
        for(int round=1;round<=2;round++)
        {
            if(round==1){Prepare(host);Prepare(guest);}
            Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
            Assert.IsTrue(host.StartOnlineCombat(round));Assert.IsTrue(guest.StartOnlineCombat(round));
            for(int tick=0;tick<405;tick++){host.Advance(.1f);guest.Advance(.1f);}
            Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());Assert.AreEqual(host.PlayerWins,guest.BotWins);
            host.Advance(10);guest.Advance(10);Assert.AreEqual(round,host.Round,"Online rounds wait for the host barrier.");
            if(round==1){Assert.IsTrue(host.NextOnlineRound(round));Assert.IsTrue(guest.NextOnlineRound(round));}
        }
    }
    [TestCase("royal","lounge")]
    [TestCase("naturalis","aquarion")]
    [TestCase("geothar","volkanar")]
    [TestCase("volkanar","royal")]
    [TestCase("classic","naturalis")]
    [TestCase("casino-night","lounge")]
    [TestCase("naturalis","casino-night")]
    [TestCase("aireola","royal")]
    [TestCase("royal","aireola")]
    public void MapsAlternateForBothPeersAndTheirChoicesDoNotChangeMidMatch(string hostMap,string guestMap)
    {
        host.Menu();guest.Menu();
        Assert.IsTrue(host.StartOnlineMatch(BoardSide.Blue,"bugaloo",new[]{"tsu"},"sepora",new[]{"tsu"},417,hostMap,guestMap));
        Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Red,"sepora",new[]{"tsu"},"bugaloo",new[]{"tsu"},417,guestMap,hostMap));
        for(int round=1;round<=4;round++)
        {
            string expected=round%2==1?hostMap:guestMap;
            Assert.AreEqual(expected,host.RoundMap);Assert.AreEqual(expected,guest.RoundMap);
            foreach(var peer in new[]{host,guest})
            {
                Assert.AreEqual(hostMap,peer.MapForSide(BoardSide.Blue));
                Assert.AreEqual(guestMap,peer.MapForSide(BoardSide.Red));
            }
            Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
            Assert.IsTrue(host.StartOnlineCombat(round));Assert.IsTrue(guest.StartOnlineCombat(round));
            foreach(var match in new[]{host,guest})
            {
                var losingSide=round%2==1?BoardSide.Red:BoardSide.Blue;
                foreach(var actor in match.Actors.Values.Where(a=>a.Side==losingSide))actor.ApplyDamage(actor.CurrentHealth);
                match.Advance(.1f);Assert.AreEqual(MatchPhase.RoundResult,match.Phase);
            }
            Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
            Assert.IsTrue(host.NextOnlineRound(round));Assert.IsTrue(guest.NextOnlineRound(round));
        }
    }
    [TestCase(null)][TestCase("")][TestCase("../royal")][TestCase("unknown")][TestCase("Aireola")][TestCase("aireola ")][TestCase("../aireola")][TestCase("Naturalis")][TestCase("../naturalis")]
    public void InvalidMapsAreRejectedBeforeStartingMatch(string id)
    {
        host.Menu();
        Assert.IsFalse(host.StartOnlineMatch(BoardSide.Blue,"bugaloo",new[]{"tsu"},"sepora",new[]{"tsu"},417,"royal",id));
        Assert.IsFalse(host.IsOnline);Assert.AreEqual(MatchPhase.Menu,host.Phase);
    }
    [Test] public void ThreeBankedTypesReplayOnBothPeersAndWithdrawWithoutLosingProgressionNextRound()
    {
        host.Menu();guest.Menu();var team=new[]{"blotan","tsu","stein"};
        Assert.IsTrue(host.StartOnlineMatch(BoardSide.Blue,"bugaloo",team,"sepora",team,417));
        Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Red,"sepora",team,"bugaloo",team,417));
        foreach(var match in new[]{host,guest})
        {
            for(int count=0;count<3;count++)
            {
                int slot=Enumerable.Range(0,3).Where(i=>match.Player.Offers[i]!=null).DefaultIfEmpty(-1).First();
                if(slot<0){Assert.IsTrue(match.Reroll(match.Player.OfferVersion));slot=Enumerable.Range(0,3).First(i=>match.Player.Offers[i]!=null);}
                Assert.IsTrue(match.BuyAndPlace(slot,match.Player.Offers[slot].Token,null,true));
            }
        }
        Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
        Assert.AreEqual(3,host.Bot.Owned.Values.Count(u=>u.Location==UnitLocation.Bench));
        Assert.AreEqual(3,guest.Bot.Owned.Values.Count(u=>u.Location==UnitLocation.Bench));
        Assert.IsTrue(host.StartOnlineCombat(1));Assert.IsTrue(guest.StartOnlineCombat(1));
        for(int tick=0;tick<405;tick++){host.Advance(.1f);guest.Advance(.1f);}
        Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
        Assert.IsTrue(host.NextOnlineRound(1));Assert.IsTrue(guest.NextOnlineRound(1));
        foreach(var match in new[]{host,guest})
        {
            var unit=match.Player.Owned["blotan"];int copies=unit.Copies,paid=unit.Paid,reserved=unit.BankReservedCost;
            int balance=match.Player.Coins,cost=unit.BankRedeployCost;
            Assert.IsTrue(match.Place("blotan",match.Board.GetCell(1,match.LocalSide==BoardSide.Blue?7:2)));
            Assert.AreEqual(copies,unit.Copies);Assert.AreEqual(paid+reserved,unit.Paid);
            Assert.AreEqual(balance-cost,match.Player.Coins);Assert.AreEqual(0,unit.BankRedeployCost);
            Assert.IsTrue(match.Reroll(match.Player.OfferVersion));
            Assert.IsTrue(match.Player.Offers.All(o=>o==null||o.UnitId=="blotan"));
        }
        Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
        Assert.AreEqual(2,host.Bot.Owned.Values.Count(u=>u.Location==UnitLocation.Bench));
    }
    [Test] public void PreparationStaysPrivateAndReadyLocksFurtherPurchases()
    {
        Prepare(host);Assert.AreEqual(1,guest.Bot.Owned.Count);Assert.AreEqual(1,host.Bot.Owned.Count);
        Assert.IsTrue(host.Actors.Values.All(a=>a.Side==host.LocalSide));
        Assert.IsFalse(guest.RevealOpponent(host.PreparationCommands,1));
        host.Ready();Assert.IsFalse(host.Reroll(host.Player.OfferVersion));Assert.IsFalse(host.StartOnlineCombat(1));
    }
    void RestartBankPeers(int income=50)
    {
        host.Menu();guest.Menu();config.RoundIncome=new[]{income};var team=new[]{"tsu"};
        Assert.IsTrue(host.StartOnlineMatch(BoardSide.Blue,"bugaloo",team,"sepora",team,417));
        Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Red,"sepora",team,"bugaloo",team,417));
    }
    OwnedUnit BuyBankTwin(MatchController match,bool improved=false)
    {
        var cell=match.Board.GetCell(1,match.LocalSide==BoardSide.Blue?7:2);
        Assert.IsTrue(match.BuyAndPlace(0,match.Player.Offers[0].Token,cell),match.Notice);
        var field=match.Player.Owned["tsu"];
        if(improved)
            Assert.IsTrue(match.BuyTrick("tsu",2,1,match.Player.Offers[1].Token),match.Notice);
        int slot=improved?2:1,balance=match.Player.Coins;
        Assert.IsTrue(match.BuyAndPlace(slot,match.Player.Offers[slot].Token,null,true),match.Notice);
        var reserve=match.Player.FindOwned("tsu",true);
        Assert.AreNotSame(field,reserve);Assert.AreEqual(balance,match.Player.Coins);
        Assert.AreSame(field,match.Player.FindOwned("tsu",false));
        Assert.AreEqual(0,reserve.Paid);Assert.AreEqual(1,reserve.Copies);
        Assert.IsTrue(match.Player.Offers.All(o=>o==null));
        Assert.AreEqual(4,match.Player.RemainingCopies("tsu")+match.Player.AllOwned.Where(u=>u.Definition.Id=="tsu").Sum(u=>u.Copies));
        Assert.IsTrue(match.Actors.ContainsKey(field));Assert.IsFalse(match.Actors.ContainsKey(reserve));
        return field;
    }
    [Test] public void BankReservationReplaysBothSameTypeInstancesAndTheirSeparateLedgers()
    {
        RestartBankPeers();BuyBankTwin(host);BuyBankTwin(guest);
        Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
        foreach(var match in new[]{host,guest})
        {
            var field=match.Bot.FindOwned("tsu",false);var reserve=match.Bot.FindOwned("tsu",true);
            Assert.AreNotSame(field,reserve);Assert.AreEqual(UnitLocation.Field,field.Location);
            Assert.AreEqual(UnitLocation.Bench,reserve.Location);Assert.AreEqual(0,reserve.Paid);
            Assert.AreEqual(Mathf.Max(0,reserve.Definition.BaseCost),reserve.BankReservedCost);
            Assert.AreEqual(1,match.Bot.BankReserves.Count);
        }
    }
    [TestCase(false)][TestCase(true)]
    public void SameTypeBankFusionReplaysTheCorrectInstanceAndPreservesExistingTricks(bool intoBank)
    {
        RestartBankPeers();
        foreach(var match in new[]{host,guest})
        {
            var field=BuyBankTwin(match,true);var reserve=match.Player.FindOwned("tsu",true);
            var originalCell=field.Deployment;int paid=field.Paid,balance=match.Player.Coins;
            int nextPrice=Mathf.Max(0,field.Definition.Tricks[0].Cost);
            if(intoBank)
            {
                Assert.IsTrue(match.Store(field,true),match.Notice);
                Assert.AreSame(reserve,match.Player.Owned["tsu"]);Assert.IsFalse(match.Player.Owns(field));
                Assert.AreEqual(balance+paid,match.Player.Coins);Assert.AreEqual(0,reserve.Paid);
                Assert.IsTrue(reserve.Tricks[0]);Assert.IsTrue(reserve.Tricks[2]);Assert.IsFalse(reserve.Tricks[1]);
                int price=Mathf.Max(0,field.Definition.BaseCost)+nextPrice+Mathf.Max(0,field.Definition.Tricks[2].Cost);
                Assert.AreEqual(price,reserve.BankRedeployCost);Assert.AreEqual(3,reserve.Copies);
                Assert.IsFalse(match.PreparationCommands.Last().sourceBench);
                Assert.IsTrue(match.Place(reserve,match.Board.GetCell(originalCell.x,originalCell.y)),match.Notice);
                Assert.IsTrue(match.PreparationCommands.Last().sourceBench);
                Assert.AreEqual(balance+paid-price,match.Player.Coins);Assert.AreEqual(price,reserve.Paid);
                Assert.IsTrue(match.Actors.ContainsKey(reserve));Assert.IsFalse(match.Actors.ContainsKey(field));
            }
            else
            {
                // Dropping on another actor still upgrades the reserved copy's field twin.
                var monsterCell=match.Actors[match.Player.Monster].CurrentCell;
                Assert.IsTrue(match.Place(reserve,monsterCell),match.Notice);
                Assert.AreSame(field,match.Player.Owned["tsu"]);Assert.IsFalse(match.Player.Owns(reserve));
                Assert.AreEqual(balance-nextPrice,match.Player.Coins);Assert.AreEqual(paid+nextPrice,field.Paid);
                Assert.AreEqual(originalCell,field.Deployment);Assert.AreEqual(3,field.Copies);
                Assert.IsTrue(field.Tricks[0]);Assert.IsTrue(field.Tricks[2]);Assert.IsFalse(field.Tricks[1]);
                Assert.IsTrue(match.PreparationCommands.Last().sourceBench);
                Assert.AreSame(match.Actors[match.Player.Monster],monsterCell.OccupiedBy);
            }
            Assert.IsEmpty(match.Player.BankReserves);
            Assert.AreEqual(1,match.Player.AllOwned.Count(u=>u.Definition.Id=="tsu"));
            Assert.AreEqual(1,match.Actors.Keys.Count(u=>u.Definition.Id=="tsu"));
        }
        Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
    }
    [Test] public void SellingReservedTwinReplaysBankIdentityAndLeavesFieldActorUntouched()
    {
        RestartBankPeers();
        foreach(var match in new[]{host,guest})
        {
            var field=BuyBankTwin(match);var reserve=match.Player.FindOwned("tsu",true);
            var actor=match.Actors[field];int balance=match.Player.Coins;
            Assert.IsTrue(match.Sell(reserve));Assert.IsTrue(match.PreparationCommands.Last().sourceBench);
            Assert.AreEqual(balance,match.Player.Coins);Assert.AreSame(field,match.Player.Owned["tsu"]);
            Assert.AreSame(actor,match.Actors[field]);Assert.IsEmpty(match.Player.BankReserves);
            Assert.IsFalse(match.Sell(reserve));Assert.AreEqual(balance,match.Player.Coins);
        }
        Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
    }
    [Test] public void UnaffordableBankFusionRejectsWithoutRecordingOrSpendingEitherInstance()
    {
        RestartBankPeers(Mathf.Max(0,config.Get("tsu").BaseCost));
        foreach(var match in new[]{host,guest})
        {
            var field=BuyBankTwin(match);var reserve=match.Player.FindOwned("tsu",true);
            int commands=match.PreparationCommands.Length,version=match.Player.OfferVersion;
            Assert.Greater(match.Player.GetDeploymentCost(reserve),match.Player.Coins);
            Assert.IsFalse(match.Place(reserve,match.Actors[field].CurrentCell));
            Assert.AreEqual(commands,match.PreparationCommands.Length);Assert.AreEqual(version,match.Player.OfferVersion);
            Assert.AreEqual(0,match.Player.Coins);Assert.AreSame(reserve,match.Player.FindOwned("tsu",true));
            Assert.AreSame(field,match.Player.FindOwned("tsu",false));Assert.AreEqual(1,field.Copies);
            Assert.AreEqual(0,field.TrickCount);Assert.AreEqual(0,reserve.Paid);
        }
        Reveal();Assert.AreEqual(host.OnlineDigest(),guest.OnlineDigest());
    }
    [Test] public void RepeatedAndOutOfRoundRevealsAreRejected()
    {
        Reveal();Assert.IsFalse(host.RevealOpponent(guest.PreparationCommands,1));
        Assert.IsFalse(host.StartOnlineCombat(2));Assert.IsFalse(host.NextOnlineRound(1));
    }
    [Test] public void IllegalOpponentPlacementIsRejected()
    {
        host.Ready();host.SetOpponentReady();
        Assert.IsFalse(host.RevealOpponent(new[]{new PreparationCommand{kind="place",unit="sepora",x=0,y=9}},1));
    }
    [Test] public void PreparationCommandLimitStillAllowsBothPlayersToStartCombat()
    {
        var monster=host.Player.Owned["bugaloo"];
        var cell=host.Board.GetCell(monster.Deployment.x,monster.Deployment.y);
        for(int i=0;i<MatchController.MaxPreparationCommands;i++)Assert.IsTrue(host.Place("bugaloo",cell));
        Assert.IsFalse(host.Place("bugaloo",cell));
        Reveal();Assert.IsTrue(host.OnlineReady);
        Assert.IsTrue(host.StartOnlineCombat(1));Assert.IsTrue(guest.StartOnlineCombat(1));
    }
    [Test] public void OneReadyPlayerWaitsUntilTheOtherReadiesOrPreparationTimeExpires()
    {
        host.Ready();guest.SetOpponentReady();
        host.Advance(39);guest.Advance(39);
        Assert.IsTrue(host.OnlineReady);Assert.IsFalse(guest.OnlineReady);
        Assert.AreEqual(MatchPhase.Preparation,host.Phase);Assert.AreEqual(MatchPhase.Preparation,guest.Phase);
        Assert.IsFalse(host.StartOnlineCombat(1));Assert.IsFalse(guest.StartOnlineCombat(1));
        guest.Advance(1);Assert.IsTrue(guest.OnlineReady);
        host.SetOpponentReady();
        Assert.IsTrue(host.RevealOpponent(guest.PreparationCommands,1));
        Assert.IsTrue(guest.RevealOpponent(host.PreparationCommands,1));
        Assert.IsTrue(host.StartOnlineCombat(1));Assert.IsTrue(guest.StartOnlineCombat(1));
    }
    [Test] public void GuestBoardRotatesVisuallyAndTouchMapsBackToCanonicalCell()
    {
        Assert.IsTrue(guest.Mapper.Flipped);Assert.IsFalse(host.Mapper.Flipped);
        foreach(var cell in guest.Board.GetAllCells())
        {
            var world=guest.Mapper.GetWorldPosition(cell);
            Assert.IsTrue(guest.Mapper.TryGetCell(world,out var mapped));Assert.AreSame(cell,mapped);
        }
    }
    [TestCase("mp-2026-10-24-v15-permanent-abilities",false)]
    [TestCase("mp-2026-10-25-v16-online-trophies",false)]
    [TestCase("mp-2026-10-25-v17-forward-mark",false)]
    [TestCase("mp-2026-10-25-v18-nearest-duel",false)]
    [TestCase("mp-2026-10-25-v19-nation-ladder",false)]
    [TestCase("mp-2026-10-27-v20-eronya-rules",true)]
    [TestCase("mp-2026-10-23-v13-bank-economy",false)]
    public void ConnectionApprovalRejectsPreviousCombatRuntimeEvenWithMatchingBalance(string protocol,bool accepted)
    {
        const string balance="same-approved-catalogue";
        var payload=System.Text.Encoding.UTF8.GetBytes(protocol+":"+balance);
        Assert.AreEqual(accepted,OnlineRoom.AcceptsConnectionVersion(payload,balance));
    }
    [Test] public void ConnectionApprovalRejectsDifferentBalanceAndMissingPayload()
    {
        var payload=System.Text.Encoding.UTF8.GetBytes("mp-2026-10-27-v20-eronya-rules:other-catalogue");
        Assert.IsFalse(OnlineRoom.AcceptsConnectionVersion(payload,"same-approved-catalogue"));
        Assert.IsFalse(OnlineRoom.AcceptsConnectionVersion(null,"same-approved-catalogue"));
    }
    [TestCase("ABC123",true)][TestCase(" abc123 ",true)][TestCase("ABC",false)][TestCase("ABC-12",false)]
    public void RoomCodesAreValidatedBeforeCallingServices(string value,bool valid)=>Assert.AreEqual(valid,OnlineRoom.ValidCode(value));
}

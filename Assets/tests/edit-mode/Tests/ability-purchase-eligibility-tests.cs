using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

public sealed class AbilityPurchaseEligibilityTests
{
    MatchConfig config;
    [SetUp] public void Setup()
    {
        config=MatchConfig.CreateDocumented();config.RoundIncome=new[]{100};
        foreach(string id in new[]{"atori","bugaloo"})
            for(int i=0;i<3;i++)config.Get(id).Tricks[i].Cost=new[]{1,5,9}[i];
    }
    [TearDown] public void Cleanup()=>Object.DestroyImmediate(config);
    PouchState Pouch()
    {
        var pouch=new PouchState(config,27,"bugaloo",BoardSide.Blue,new[]{"atori"});
        Assert.IsTrue(pouch.BeginPreparation(1));return pouch;
    }
    PouchState OwnedWhelp(int budget)
    {
        var pouch=Pouch();var offer=pouch.Offers[0];
        Assert.IsTrue(pouch.TryBuy(0,offer.Token,out _));
        pouch.AddCombatCoins(budget-pouch.Coins);return pouch;
    }
    static string Snapshot(PouchState pouch)=>string.Join("|",pouch.Coins,pouch.Rerolls,pouch.OfferVersion,pouch.PoolCount,pouch.IsPreparation,
        string.Join(";",pouch.Offers.Select(o=>o==null?"null":o.UnitId+":"+o.Token)),
        string.Join(";",pouch.Owned.Values.Select(u=>u.Definition.Id+":"+u.Copies+":"+u.Paid+":"+u.NextTrick+":"+u.Location+":"+u.UpgradePurchasedRound+":"+string.Join(",",u.Tricks))));

    [TestCase(0,0)] [TestCase(0,5)] [TestCase(0,100)]
    [TestCase(1,0)] [TestCase(1,5)] [TestCase(1,100)]
    [TestCase(2,0)] [TestCase(2,5)] [TestCase(2,100)]
    public void WhelpPreviewIsReadOnlyAndMatchesTheActualSpecificPurchase(int index,int budget)
    {
        var pouch=OwnedWhelp(budget);var offer=pouch.Offers[1];string before=Snapshot(pouch);
        bool available=pouch.CanBuyTrick("atori",index,1,offer.Token,out string previewReason);
        Assert.AreEqual(before,Snapshot(pouch),"A presentation query must not consume a copy, token, budget or selection.");
        bool bought=pouch.TryBuyTrick("atori",index,1,offer.Token,out string actualReason);
        Assert.AreEqual(available,bought);Assert.AreEqual(previewReason,actualReason);
        if(bought){Assert.IsTrue(pouch.Owned["atori"].Tricks[index]);Assert.AreEqual(budget-config.Get("atori").Tricks[index].Cost,pouch.Coins);}
        else Assert.AreEqual(before,Snapshot(pouch));
    }

    [Test] public void TheInspectedAbilityCostDoesNotUseOrChangeThePreviouslySelectedTrick()
    {
        var pouch=OwnedWhelp(1);var offer=pouch.Offers[1];
        Assert.IsTrue(pouch.TrySelectTrick("atori",2,out _));Assert.AreEqual(9,pouch.GetOfferCost(1));
        string before=Snapshot(pouch);
        Assert.IsTrue(pouch.CanBuyTrick("atori",0,1,offer.Token,out _));
        Assert.IsFalse(pouch.CanBuyTrick("atori",1,1,offer.Token,out _));
        Assert.IsFalse(pouch.CanBuyTrick("atori",2,1,offer.Token,out _));
        Assert.AreEqual(before,Snapshot(pouch));Assert.AreEqual(2,pouch.Owned["atori"].NextTrick);
    }

    [Test] public void MissingCopyStaleTokenBankAndEndedPreparationAreUnavailableWithoutMutation()
    {
        var pouch=OwnedWhelp(100);var offer=pouch.Offers[1];string before=Snapshot(pouch);
        Assert.IsFalse(pouch.CanBuyTrick("atori",0,1,offer.Token+1,out _));
        Assert.IsFalse(pouch.CanBuyTrick("atori",0,0,offer.Token,out _));
        Assert.AreEqual(before,Snapshot(pouch));
        Assert.IsTrue(pouch.TrySetLocation("atori",UnitLocation.Bench,Vector2Int.zero,out _));
        before=Snapshot(pouch);Assert.IsFalse(pouch.CanBuyTrick("atori",0,1,offer.Token,out _));Assert.AreEqual(before,Snapshot(pouch));
        pouch.EndPreparation();before=Snapshot(pouch);
        Assert.IsFalse(pouch.CanUpgradeMonster(0));Assert.IsFalse(pouch.CanBuyTrick("atori",0,1,offer.Token,out _));
        Assert.AreEqual(before,Snapshot(pouch));
    }

    [TestCase(0)] [TestCase(5)] [TestCase(100)]
    public void MonsterPreviewMatchesAffordabilityAndOneUpgradeLimitWithoutSpending(int budget)
    {
        for(int index=0;index<3;index++)
        {
            var pouch=Pouch();pouch.AddCombatCoins(budget-pouch.Coins);string before=Snapshot(pouch);
            bool available=pouch.CanUpgradeMonster(index);Assert.AreEqual(before,Snapshot(pouch));
            Assert.AreEqual(available,pouch.TryUpgradeMonster(index,out _));
            if(available)for(int other=0;other<3;other++)Assert.IsFalse(pouch.CanUpgradeMonster(other));
            else Assert.AreEqual(before,Snapshot(pouch));
        }
    }
}

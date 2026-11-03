using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

public sealed class SeryaEligibilityFeedbackTests
{
    GameObject root;
    MatchConfig config;
    MatchController match;
    PouchState pouch;
    [SetUp]public void Setup()
    {
        config=MatchConfig.CreateDocumented();config.RoundIncome=new[]{100};
        config.Get("serya").Tricks[0].Cost=2;config.Get("serya").Tricks[1].Cost=3;config.Get("serya").Tricks[2].Cost=4;
        pouch=new PouchState(config,27,"serya",BoardSide.Blue,new[]{"atori"});Assert.IsTrue(pouch.BeginPreparation(1));
        root=new GameObject("eligibility-feedback");match=root.AddComponent<MatchController>();
        typeof(MatchController).GetProperty("Config").SetValue(match,config);
        typeof(MatchController).GetProperty("Player").SetValue(match,pouch);
        typeof(MatchController).GetProperty("Phase").SetValue(match,MatchPhase.Preparation);
    }
    [TearDown]public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(config);}
    [Test]public void EligibleFirstTrickHasNoBlockingReasonAndQueryDoesNotSpend()
    {
        int coins=pouch.Coins;Assert.IsTrue(match.CanPurchaseAbility(pouch.Monster,0,out _));
        Assert.AreEqual("",match.PurchaseAbilityUnavailableReason(pouch.Monster,0));
        Assert.AreEqual(coins,pouch.Coins);Assert.AreEqual(0,pouch.Monster.TrickCount);
    }
    [Test]public void SecondLeaderTrickExplainsExistingLimitWithoutRelaxingIt()
    {
        Assert.IsTrue(pouch.TryUpgradeMonster(0,out _));int coins=pouch.Coins;
        Assert.IsFalse(match.CanPurchaseAbility(pouch.Monster,1,out _));
        Assert.AreEqual("Tu héroe ya eligió su única mejora para esta partida.",match.PurchaseAbilityUnavailableReason(pouch.Monster,1));
        Assert.IsFalse(pouch.TryUpgradeMonster(1,out var reason));
        Assert.AreEqual(reason,match.PurchaseAbilityUnavailableReason(pouch.Monster,1));
        Assert.AreEqual(coins,pouch.Coins);Assert.AreEqual(1,pouch.Monster.TrickCount);
    }
    [Test]public void UnaffordableFirstTrickReportsMoonkensAndUpdatesWithoutMutation()
    {
        pouch.AddCombatCoins(-pouch.Coins);
        Assert.AreEqual("No tienes suficientes Moonkens.",match.PurchaseAbilityUnavailableReason(pouch.Monster,2));
        Assert.AreEqual(0,pouch.Coins);pouch.AddCombatCoins(4);
        Assert.AreEqual("",match.PurchaseAbilityUnavailableReason(pouch.Monster,2));Assert.AreEqual(4,pouch.Coins);
    }
    [Test]public void PausedPurchaseReportsPauseInsteadOfAnInvitationToBuy()
    {
        typeof(MatchController).GetProperty("Paused").SetValue(match,true);int coins=pouch.Coins;
        Assert.IsFalse(match.CanPurchaseAbility(pouch.Monster,0,out _));
        Assert.AreEqual("Reanuda la partida para comprar Tricks.",match.PurchaseAbilityUnavailableReason(pouch.Monster,0));
        Assert.AreEqual(coins,pouch.Coins);
    }
}

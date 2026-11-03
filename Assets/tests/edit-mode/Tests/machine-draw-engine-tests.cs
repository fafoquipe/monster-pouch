using System;
using System.Linq;
using MonsterPouch.Gameplay.Machines;
using NUnit.Framework;
using UnityEngine;

// Synthetic IDs and weights are confined to tests. They are neither a reward
// catalogue nor an approved commercial probability policy.
internal static class MachineQaFixtures
{
    internal const string Character="qa-character-prize";
    internal static readonly string[] Abilities={"qa-ability-0","qa-ability-1","qa-ability-2"};
    internal static MachineDefinition Definition()=>new MachineDefinition{
        MachineId="qa-machine",CharacterId="qa-character",CatalogVersion=1,
        Rewards=new[]{new MachineReward{RewardId=Character,Kind=MachineRewardKind.Character}}
            .Concat(Abilities.Select((id,i)=>new MachineReward{RewardId="qa-ability-prize-"+i,Kind=MachineRewardKind.Ability,AbilityId=id}))
            .Concat(Enumerable.Range(0,10).Select(i=>new MachineReward{RewardId="qa-resource-slot-"+i,Kind=MachineRewardKind.Resource})).ToArray()
    };
    internal static MachinePolicy Policy(MachineDefinition definition)=>new MachinePolicy{
        PolicyId="qa-provisional-roadmap-only",Version=1,
        CharacterChanceByDraw=new[]{.01,.01,.01,.01,.01,.02,.04,.08,.20,1d},
        RewardWeights=definition.Rewards.Select(reward=>new MachineWeight{RewardId=reward.RewardId,Weight=1d}).ToArray()
    };
    internal static MachineOwnership Ownership(bool character=false,int abilityCount=0)=>new MachineOwnership{
        CharacterOwned=character,AbilityIds=Abilities.Take(abilityCount).ToArray()
    };
    internal static MachineDrawRequest Request(MachineDefinition definition,MachineState state,string id=null)=>new MachineDrawRequest{
        RequestId=id??"qa-draw-"+(state.DrawCount+1),MachineId=definition.MachineId,
        CatalogVersion=definition.CatalogVersion,ExpectedRevision=state.Revision
    };
    internal static double SampleFor(MachinePreview preview,string rewardId)
    {
        double start=0;
        foreach(var outcome in preview.Outcomes)
        {if(outcome.RewardId==rewardId)return start+outcome.Probability*.5;start+=outcome.Probability;}
        throw new InvalidOperationException("The requested QA outcome is not eligible.");
    }
    internal static string Snapshot(object value)=>JsonUtility.ToJson(value);
    internal static void AssertDistribution(MachinePreview preview)
    {
        Assert.AreEqual(MachineStatus.Ready,preview.Status,preview.Reason);
        Assert.Greater(preview.Outcomes.Length,0);
        Assert.AreEqual(1d,preview.Outcomes.Sum(item=>item.Probability),1e-10);
        Assert.IsTrue(preview.Outcomes.All(item=>item.Probability>0&&!double.IsNaN(item.Probability)&&!double.IsInfinity(item.Probability)));
        Assert.AreEqual(preview.Outcomes.Length,preview.Outcomes.Select(item=>item.RewardId).Distinct().Count());
    }
}

public sealed class MachineDrawEngineTests
{
    MachineDefinition definition;
    MachinePolicy policy;
    MachineOwnership ownership;
    MachineState state;
    [SetUp] public void Setup()
    {definition=MachineQaFixtures.Definition();policy=MachineQaFixtures.Policy(definition);ownership=MachineQaFixtures.Ownership();state=MachineDrawEngine.CreateState(definition);}

    [Test] public void InitialPreviewExposesFourteenUniqueRewardsWithOnlyCharacterAndResourcesEligible()
    {
        string before=MachineQaFixtures.Snapshot(state);
        var preview=MachineDrawEngine.Preview(definition,state,ownership,policy);
        MachineQaFixtures.AssertDistribution(preview);
        Assert.AreEqual(14,preview.Rewards.Length);Assert.AreEqual(11,preview.Outcomes.Length);
        Assert.AreEqual(14,preview.Rewards.Select(item=>item.RewardId).Distinct().Count());
        Assert.AreEqual(.01,preview.Outcomes.Single(item=>item.RewardId==MachineQaFixtures.Character).Probability,1e-12);
        foreach(var reward in definition.Rewards.Where(item=>item.Kind==MachineRewardKind.Resource))
            Assert.AreEqual(.099,preview.Outcomes.Single(item=>item.RewardId==reward.RewardId).Probability,1e-12);
        foreach(var reward in definition.Rewards.Where(item=>item.Kind==MachineRewardKind.Ability))
        {
            var displayed=preview.Rewards.Single(item=>item.RewardId==reward.RewardId);
            Assert.AreEqual("Locked",displayed.Status.ToString());Assert.AreEqual(0,displayed.Probability);
        }
        Assert.AreEqual(1,preview.NextDrawNumber);Assert.IsFalse(preview.FirstAbilityGuaranteed);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
    }
    [Test] public void ConditionalCharacterChanceAndForcedTenthDrawAreNotFlatWeights()
    {
        for(int draw=1;draw<=10;draw++)
        {
            var preview=MachineDrawEngine.Preview(definition,state,ownership,policy);MachineQaFixtures.AssertDistribution(preview);
            Assert.AreEqual(policy.CharacterChanceByDraw[draw-1],preview.Outcomes.Single(item=>item.RewardId==MachineQaFixtures.Character).Probability,1e-12);
            string selected=draw==10?MachineQaFixtures.Character:
                preview.Outcomes.First(item=>definition.Rewards.Single(r=>r.RewardId==item.RewardId).Kind==MachineRewardKind.Resource).RewardId;
            var result=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),MachineQaFixtures.SampleFor(preview,selected));
            Assert.AreEqual(MachineStatus.Drawn,result.Status,result.Reason);Assert.AreEqual(selected,result.Receipt.RewardId);
            state=result.State;
        }
        Assert.AreEqual(10,state.DrawCount);Assert.Contains(MachineQaFixtures.Character,state.ClaimedRewardIds);
        var after=MachineDrawEngine.Preview(definition,state,ownership,policy);
        Assert.IsTrue(after.FirstAbilityGuaranteed);Assert.AreEqual(3,after.Outcomes.Length);
        Assert.IsTrue(after.Outcomes.All(item=>item.RewardId.StartsWith("qa-ability-prize-",StringComparison.Ordinal)));
        foreach(var item in after.Outcomes)Assert.AreEqual(1d/3,item.Probability,1e-12);
    }
    [Test] public void ExplicitRelativeWeightsAreNormalizedInsideTheGuaranteedAbilityBranch()
    {
        ownership=MachineQaFixtures.Ownership(true);
        for(int i=0;i<3;i++)policy.RewardWeights.Single(item=>item.RewardId=="qa-ability-prize-"+i).Weight=i+1;
        var preview=MachineDrawEngine.Preview(definition,state,ownership,policy);MachineQaFixtures.AssertDistribution(preview);
        for(int i=0;i<3;i++)
        {
            string id="qa-ability-prize-"+i;
            Assert.AreEqual((i+1)/6d,preview.Outcomes.Single(item=>item.RewardId==id).Probability,1e-12);
            var result=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),MachineQaFixtures.SampleFor(preview,id));
            Assert.AreEqual(id,result.Receipt.RewardId);Assert.AreEqual(MachineStatus.Drawn,result.Status);
        }
    }
    [TestCase("reward")] [TestCase("ability")] [TestCase("kind")]
    public void DuplicateIdsOrIncorrectKindsCannotBecomeAValidFourteenSlotCatalogue(string fault)
    {
        if(fault=="reward")definition.Rewards[5].RewardId=definition.Rewards[4].RewardId;
        if(fault=="ability")definition.Rewards[2].AbilityId=definition.Rewards[1].AbilityId;
        if(fault=="kind")definition.Rewards[4].Kind=MachineRewardKind.Character;
        string before=MachineQaFixtures.Snapshot(definition);
        Assert.Throws<ArgumentException>(()=>MachineDrawEngine.CreateState(definition));
        Assert.AreEqual(MachineStatus.InvalidCatalog,MachineDrawEngine.Preview(definition,state,ownership,policy).Status);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(definition));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void ExternalCharacterAndZeroToThreeAbilitiesAreRecognizedWithoutDuplicateDraws(int ownedAbilities)
    {
        ownership=MachineQaFixtures.Ownership(true,ownedAbilities);
        var preview=MachineDrawEngine.Preview(definition,state,ownership,policy);MachineQaFixtures.AssertDistribution(preview);
        Assert.AreEqual(ownedAbilities<3,preview.FirstAbilityGuaranteed);
        Assert.AreEqual(ownedAbilities<3?3-ownedAbilities:10,preview.Outcomes.Length);
        Assert.AreEqual("Claimed",preview.Rewards.Single(item=>item.RewardId==MachineQaFixtures.Character).Status.ToString());
        for(int i=0;i<ownedAbilities;i++)Assert.AreEqual("Claimed",preview.Rewards.Single(item=>item.RewardId=="qa-ability-prize-"+i).Status.ToString());
        var result=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.5);
        Assert.AreEqual(MachineStatus.Drawn,result.Status,result.Reason);
        Assert.AreEqual(1,result.State.DrawCount);Assert.AreEqual(1,result.State.Receipts.Count);
        Assert.AreEqual(ownedAbilities+2,result.State.ClaimedRewardIds.Count);
        Assert.AreEqual(ownedAbilities<3?MachineRewardKind.Ability:MachineRewardKind.Resource,
            definition.Rewards.Single(item=>item.RewardId==result.Receipt.RewardId).Kind);
    }
    [Test] public void MidProgressExternalAcquisitionGuaranteesOnlyAnAbilityStillMissing()
    {
        for(int i=0;i<3;i++)
        {
            var preview=MachineDrawEngine.Preview(definition,state,ownership,policy);
            string resource=preview.Outcomes.First(item=>item.RewardId.StartsWith("qa-resource",StringComparison.Ordinal)).RewardId;
            state=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),MachineQaFixtures.SampleFor(preview,resource)).State;
        }
        ownership=MachineQaFixtures.Ownership(true,2);
        var guarantee=MachineDrawEngine.Preview(definition,state,ownership,policy);
        Assert.IsTrue(guarantee.FirstAbilityGuaranteed);Assert.AreEqual(1,guarantee.Outcomes.Length);
        Assert.AreEqual("qa-ability-prize-2",guarantee.Outcomes[0].RewardId);
        var draw=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.999999);
        Assert.AreEqual(MachineStatus.Drawn,draw.Status,draw.Reason);Assert.AreEqual("qa-ability-prize-2",draw.Receipt.RewardId);
        Assert.AreEqual(4,draw.State.DrawCount);Assert.AreEqual("FirstAbility",draw.Receipt.Guarantee.ToString());
        Assert.IsFalse(MachineDrawEngine.Preview(definition,draw.State,ownership,policy).FirstAbilityGuaranteed);
    }
    [Test] public void RetryingAConfirmedRequestReturnsItsOriginalResultDespiteDifferentSample()
    {
        var request=MachineQaFixtures.Request(definition,state,"qa-repeat");
        string before=MachineQaFixtures.Snapshot(state);
        var drawn=MachineDrawEngine.TryDraw(definition,state,ownership,policy,request,0);
        Assert.AreEqual(MachineStatus.Drawn,drawn.Status);Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
        string committed=MachineQaFixtures.Snapshot(drawn.State),receipt=MachineQaFixtures.Snapshot(drawn.Receipt);
        var retry=MachineDrawEngine.TryDraw(definition,drawn.State,ownership,policy,request,.99);
        Assert.AreEqual(MachineStatus.AlreadyApplied,retry.Status,retry.Reason);
        Assert.AreEqual(receipt,MachineQaFixtures.Snapshot(retry.Receipt));Assert.AreEqual(committed,MachineQaFixtures.Snapshot(retry.State));
        Assert.AreNotSame(drawn.State,retry.State);
        request.ExpectedRevision++;
        var conflict=MachineDrawEngine.TryDraw(definition,drawn.State,ownership,policy,request,.4);
        Assert.AreEqual(MachineStatus.Conflict,conflict.Status);Assert.IsNull(conflict.State);
        Assert.AreEqual(committed,MachineQaFixtures.Snapshot(drawn.State));
    }
    [Test] public void ReusingARequestUnderAnotherPolicyCannotChangeTheConfirmedPrize()
    {
        var request=MachineQaFixtures.Request(definition,state,"qa-policy-retry");
        var draw=MachineDrawEngine.TryDraw(definition,state,ownership,policy,request,.8);
        string before=MachineQaFixtures.Snapshot(draw.State);policy.Version++;
        var retry=MachineDrawEngine.TryDraw(definition,draw.State,ownership,policy,request,.1);
        Assert.AreEqual(MachineStatus.Conflict,retry.Status);Assert.IsNull(retry.State);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(draw.State));
    }
    [TestCase(-.1)] [TestCase(1d)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
    public void InvalidSamplesCannotConsumeARewardOrAdvanceState(double sample)
    {
        string before=MachineQaFixtures.Snapshot(state);
        var result=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),sample);
        Assert.AreEqual(MachineStatus.InvalidSample,result.Status);Assert.IsNull(result.State);Assert.IsNull(result.Receipt);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
    }
    [TestCase("catalogue")] [TestCase("signature")] [TestCase("revision")] [TestCase("claimed")]
    public void InvalidCatalogueOrStateFailsWithoutRepairingOrMutatingTheInput(string fault)
    {
        if(fault=="catalogue")definition.Rewards=definition.Rewards.Take(13).ToArray();
        if(fault=="signature")definition.CatalogVersion++;
        if(fault=="revision")state.Revision++;
        if(fault=="claimed")state.ClaimedRewardIds.Add("not-in-catalogue");
        string before=MachineQaFixtures.Snapshot(state),catalogue=MachineQaFixtures.Snapshot(definition);
        var result=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.5);
        Assert.Contains(result.Status,new[]{MachineStatus.InvalidCatalog,MachineStatus.InvalidState});
        Assert.IsNull(result.State);Assert.IsNull(result.Receipt);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));Assert.AreEqual(catalogue,MachineQaFixtures.Snapshot(definition));
    }
    [Test] public void UnknownExternalAbilityOrEmptyRequestDoesNotGrantOrAdvanceAnything()
    {
        string before=MachineQaFixtures.Snapshot(state);ownership.AbilityIds=new[]{"unrelated-ability"};
        var invalidOwnership=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.5);
        Assert.AreEqual(MachineStatus.InvalidOwnership,invalidOwnership.Status);Assert.IsNull(invalidOwnership.State);
        ownership=MachineQaFixtures.Ownership();var request=MachineQaFixtures.Request(definition,state);request.RequestId="";
        var invalidRequest=MachineDrawEngine.TryDraw(definition,state,ownership,policy,request,.5);
        Assert.AreEqual(MachineStatus.InvalidRequest,invalidRequest.Status);Assert.IsNull(invalidRequest.State);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
    }
    [Test] public void NoEligibleConfiguredWeightReportsMissingConfigurationWithoutInventingAReward()
    {
        ownership=MachineQaFixtures.Ownership(true,3);policy.RewardWeights=Array.Empty<MachineWeight>();
        string before=MachineQaFixtures.Snapshot(state);
        var preview=MachineDrawEngine.Preview(definition,state,ownership,policy);
        Assert.AreEqual(MachineStatus.NeedsConfiguration,preview.Status);
        var draw=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.2);
        Assert.AreEqual(MachineStatus.NeedsConfiguration,draw.Status);Assert.IsNull(draw.State);Assert.IsNull(draw.Receipt);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
    }
    [TestCase("negative")] [TestCase("nan")] [TestCase("infinite")] [TestCase("chance")] [TestCase("unknown")]
    public void InvalidPolicyRemainsUnconfiguredAndCannotModifyInputs(string fault)
    {
        if(fault=="negative")policy.RewardWeights[4].Weight=-1;
        if(fault=="nan")policy.RewardWeights[4].Weight=double.NaN;
        if(fault=="infinite")policy.RewardWeights[4].Weight=double.PositiveInfinity;
        if(fault=="chance")policy.CharacterChanceByDraw[0]=1.01;
        if(fault=="unknown")policy.RewardWeights[4].RewardId="unknown-slot";
        string before=MachineQaFixtures.Snapshot(state);
        var result=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.5);
        Assert.AreEqual(MachineStatus.NeedsConfiguration,result.Status);Assert.IsNull(result.State);Assert.IsNull(result.Receipt);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
    }
    [TestCase("wrong-prize")] [TestCase("missing-guarantee")]
    public void PersistedHistoryCannotReplaceOrEraseTheFirstAbilityGuarantee(string fault)
    {
        var first=MachineDrawEngine.Preview(definition,state,ownership,policy);
        state=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),
            MachineQaFixtures.SampleFor(first,MachineQaFixtures.Character)).State;
        state=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.5).State;
        Assert.AreEqual(MachineGuarantee.FirstAbility,state.Receipts[1].Guarantee);
        if(fault=="wrong-prize")
        {
            state.ClaimedRewardIds.Remove(state.Receipts[1].RewardId);
            state.Receipts[1].RewardId="qa-resource-slot-0";state.ClaimedRewardIds.Add("qa-resource-slot-0");
        }
        state.Receipts[1].Guarantee=MachineGuarantee.None;
        string before=MachineQaFixtures.Snapshot(state);
        var rejected=MachineDrawEngine.TryDraw(definition,state,ownership,policy,MachineQaFixtures.Request(definition,state),.5);
        Assert.AreEqual(MachineStatus.InvalidState,rejected.Status);Assert.IsNull(rejected.State);Assert.IsNull(rejected.Receipt);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
    }
}

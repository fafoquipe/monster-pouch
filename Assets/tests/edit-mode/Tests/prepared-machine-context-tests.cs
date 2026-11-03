using System;
using System.Linq;
using MonsterPouch.Gameplay.Machines;
using NUnit.Framework;

public sealed class PreparedMachineContextTests
{
    MachineDefinition definition;
    MachinePolicy policy;
    PreparedMachineContext context;

    [SetUp] public void Setup()
    {
        definition=MachineQaFixtures.Definition();policy=MachineQaFixtures.Policy(definition);
        context=MachineDrawEngine.Prepare(definition,policy);
    }

    [TestCase(-1)] [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void PreparedAndMutableApisAgreeForEveryDrawRetryAndCompleteMachine(int externalAbilities)
    {
        var mutable=MachineDrawEngine.CreateState(definition);var prepared=context.CreateState();
        var ownership=MachineQaFixtures.Ownership(externalAbilities>=0,Math.Max(0,externalAbilities));
        var rng=new Random(20261020+externalAbilities);
        Same(mutable,prepared);
        for(int draw=0;draw<15;draw++)
        {
            var expected=MachineDrawEngine.Preview(definition,mutable,ownership,policy);
            Same(expected,context.Preview(prepared,ownership));
            var request=MachineQaFixtures.Request(definition,mutable);double sample=rng.NextDouble();
            var expectedResult=MachineDrawEngine.TryDraw(definition,mutable,ownership,policy,request,sample);
            var actual=context.TryDraw(prepared,ownership,request,sample);Same(expectedResult,actual);
            if(expected.Status==MachineStatus.Complete)
            {Assert.AreEqual(MachineStatus.Complete,actual.Status);Assert.IsNull(actual.State);return;}
            Assert.AreEqual(MachineStatus.Drawn,actual.Status,actual.Reason);
            mutable=expectedResult.State;prepared=actual.State;
            var expectedRetry=MachineDrawEngine.TryDraw(definition,mutable,ownership,policy,request,.99);
            var actualRetry=context.TryDraw(prepared,ownership,request,.99);
            Assert.AreEqual(MachineStatus.AlreadyApplied,actualRetry.Status);Same(expectedRetry,actualRetry);
        }
        Assert.Fail("Both APIs must reach completion within fourteen paid draws.");
    }

    [Test] public void PreparedAndMutableApisAgreeWhenOwnershipChangesBetweenDraws()
    {
        var state=context.CreateState();var ownership=MachineQaFixtures.Ownership();
        for(int draw=0;draw<3;draw++)
        {
            var preview=context.Preview(state,ownership);
            string resource=preview.Outcomes.First(item=>item.RewardId.StartsWith("qa-resource",StringComparison.Ordinal)).RewardId;
            state=context.TryDraw(state,ownership,MachineQaFixtures.Request(definition,state),MachineQaFixtures.SampleFor(preview,resource)).State;
        }
        ownership=MachineQaFixtures.Ownership(true,2);
        var actual=context.Preview(state,ownership);
        Same(MachineDrawEngine.Preview(definition,state,ownership,policy),actual);
        Assert.IsTrue(actual.FirstAbilityGuaranteed);Assert.AreEqual("qa-ability-prize-2",actual.Outcomes.Single().RewardId);
        var request=MachineQaFixtures.Request(definition,state);
        Same(MachineDrawEngine.TryDraw(definition,state,ownership,policy,request,.8),context.TryDraw(state,ownership,request,.8));
    }

    [Test] public void PreparedSnapshotRetainsNoMutableCatalogueOrPolicyReference()
    {
        var state=context.CreateState();var ownership=MachineQaFixtures.Ownership();
        var request=MachineQaFixtures.Request(definition,state);string initial=MachineQaFixtures.Snapshot(state);
        string preview=MachineQaFixtures.Snapshot(context.Preview(state,ownership));
        string draw=MachineQaFixtures.Snapshot(context.TryDraw(state,ownership,request,.5));
        string originalMachine=definition.MachineId,originalPolicy=policy.PolicyId;
        // Exercise scalar DTO fields, nested reward/weight objects and array slots.
        definition.MachineId="mutated";definition.CharacterId="mutated";definition.CatalogVersion=99;
        foreach(var reward in definition.Rewards)
        {reward.RewardId="mutated";reward.Kind=MachineRewardKind.Ability;reward.AbilityId="mutated";}
        definition.Rewards[0]=null;definition.Rewards=Array.Empty<MachineReward>();
        policy.PolicyId="mutated";policy.Version=99;
        for(int i=0;i<policy.CharacterChanceByDraw.Length;i++)policy.CharacterChanceByDraw[i]=double.NaN;
        foreach(var weight in policy.RewardWeights){weight.RewardId="mutated";weight.Weight=double.NaN;}
        policy.RewardWeights[0]=null;policy.RewardWeights=Array.Empty<MachineWeight>();policy.CharacterChanceByDraw=null;
        Assert.AreEqual(originalMachine,context.MachineId);Assert.AreEqual(1,context.CatalogVersion);
        Assert.AreEqual(originalPolicy,context.PolicyId);Assert.AreEqual(1,context.PolicyVersion);
        Assert.AreEqual(initial,MachineQaFixtures.Snapshot(context.CreateState()));
        Assert.AreEqual(preview,MachineQaFixtures.Snapshot(context.Preview(state,ownership)));
        Assert.AreEqual(draw,MachineQaFixtures.Snapshot(context.TryDraw(state,ownership,request,.5)));
        Assert.AreEqual(MachineStatus.InvalidCatalog,MachineDrawEngine.Preview(definition,state,ownership,policy).Status);
    }

    [Test] public void MutatingReturnedPreviewsCannotChangeFutureDistributionsOrDraws()
    {
        var state=context.CreateState();var ownership=MachineQaFixtures.Ownership();
        var first=context.Preview(state,ownership);string expected=MachineQaFixtures.Snapshot(first);
        foreach(var reward in first.Rewards){reward.RewardId="mutated";reward.Probability=99;reward.Status=MachineRewardStatus.Claimed;}
        foreach(var outcome in first.Outcomes){outcome.RewardId="mutated";outcome.Probability=99;}
        first.Rewards[0]=null;first.Outcomes[0]=null;first.FirstAbilityGuaranteed=true;first.NextDrawNumber=99;
        Assert.AreEqual(expected,MachineQaFixtures.Snapshot(context.Preview(state,ownership)));
        var result=context.TryDraw(state,ownership,MachineQaFixtures.Request(definition,state),0);
        Assert.AreEqual(MachineQaFixtures.Character,result.Receipt.RewardId);
        result.Preview.Outcomes[0].RewardId="mutated";result.Preview.Rewards[0].RewardId="mutated";
        Assert.AreEqual(expected,MachineQaFixtures.Snapshot(context.Preview(state,ownership)));
    }

    [Test] public void ReturnedStatesAndReceiptsAreDetachedFromInputsAndEachOther()
    {
        var state=context.CreateState();var ownership=MachineQaFixtures.Ownership();
        var request=MachineQaFixtures.Request(definition,state);string empty=MachineQaFixtures.Snapshot(state);
        var draw=context.TryDraw(state,ownership,request,0);string committed=MachineQaFixtures.Snapshot(draw.State);
        string receipt=MachineQaFixtures.Snapshot(draw.State.Receipts.Single());
        draw.Receipt.RewardId="mutated";draw.Receipt.PolicySignature="mutated";
        Assert.AreEqual(receipt,MachineQaFixtures.Snapshot(draw.State.Receipts.Single()));
        var retry=context.TryDraw(draw.State,ownership,request,.9);
        Assert.AreEqual(MachineStatus.AlreadyApplied,retry.Status);
        retry.Receipt.RewardId="mutated";retry.State.Receipts[0].RewardId="mutated";retry.State.ClaimedRewardIds.Clear();
        Assert.AreEqual(committed,MachineQaFixtures.Snapshot(draw.State));
        draw.State.ClaimedRewardIds.Clear();draw.State.Receipts.Clear();
        Assert.AreEqual(empty,MachineQaFixtures.Snapshot(state));Assert.AreEqual(empty,MachineQaFixtures.Snapshot(context.CreateState()));
        Assert.AreEqual(MachineQaFixtures.Character,context.TryDraw(state,ownership,request,0).Receipt.RewardId);
    }

    [TestCase("revision")] [TestCase("signature")] [TestCase("receipt")]
    public void ReusingContextNeverSkipsValidationOfChangedState(string fault)
    {
        var ownership=MachineQaFixtures.Ownership();var state=context.CreateState();
        state=context.TryDraw(state,ownership,MachineQaFixtures.Request(definition,state),0).State;
        Assert.AreEqual(MachineStatus.Ready,context.Preview(state,ownership).Status);
        if(fault=="revision")state.Revision++;
        if(fault=="signature")state.CatalogSignature="tampered";
        if(fault=="receipt")state.Receipts[0].RewardId="qa-resource-slot-0";
        string before=MachineQaFixtures.Snapshot(state);
        var preview=context.Preview(state,ownership);Assert.AreEqual(MachineStatus.InvalidState,preview.Status);
        var result=context.TryDraw(state,ownership,MachineQaFixtures.Request(definition,state),.5);
        Assert.AreEqual(MachineStatus.InvalidState,result.Status);Assert.IsNull(result.State);Assert.IsNull(result.Receipt);
        Assert.AreEqual(before,MachineQaFixtures.Snapshot(state));
        Assert.AreEqual(MachineStatus.Ready,context.Preview(context.CreateState(),ownership).Status);
    }

    [Test] public void OwnershipIsValidatedFreshWithoutBeingRetainedOrMutated()
    {
        var state=context.CreateState();var ownership=MachineQaFixtures.Ownership(true,1);
        string ownershipBefore=MachineQaFixtures.Snapshot(ownership),stateBefore=MachineQaFixtures.Snapshot(state);
        var previous=context.Preview(state,ownership);string previousBefore=MachineQaFixtures.Snapshot(previous);
        context.TryDraw(state,ownership,MachineQaFixtures.Request(definition,state),.5);
        Assert.AreEqual(ownershipBefore,MachineQaFixtures.Snapshot(ownership));Assert.AreEqual(stateBefore,MachineQaFixtures.Snapshot(state));
        ownership.AbilityIds[0]="not-a-valid-ability";
        Assert.AreEqual(MachineStatus.InvalidOwnership,context.Preview(state,ownership).Status);
        Assert.AreEqual(previousBefore,MachineQaFixtures.Snapshot(previous));
        ownership.AbilityIds=MachineQaFixtures.Abilities.Take(2).ToArray();
        Assert.AreEqual("qa-ability-prize-2",context.Preview(state,ownership).Outcomes.Single().RewardId);
    }

    [TestCase("catalogue")] [TestCase("policy")]
    public void FactoryRejectsInvalidSnapshotsWithoutRepairOrMutation(string fault)
    {
        if(fault=="catalogue")definition.Rewards[2].RewardId=definition.Rewards[1].RewardId;
        else policy.CharacterChanceByDraw[0]=-1;
        string definitionBefore=MachineQaFixtures.Snapshot(definition),policyBefore=MachineQaFixtures.Snapshot(policy);
        Assert.Throws<ArgumentException>(()=>MachineDrawEngine.Prepare(definition,policy));
        Assert.AreEqual(definitionBefore,MachineQaFixtures.Snapshot(definition));Assert.AreEqual(policyBefore,MachineQaFixtures.Snapshot(policy));
        Assert.AreEqual(MachineStatus.Ready,context.Preview(context.CreateState(),MachineQaFixtures.Ownership()).Status);
    }

    [Test] public void MissingEligibleWeightsHaveTheSameExplicitConfigurationFailure()
    {
        policy.RewardWeights=Array.Empty<MachineWeight>();context=MachineDrawEngine.Prepare(definition,policy);
        var state=context.CreateState();var ownership=MachineQaFixtures.Ownership(true,3);
        var preview=context.Preview(state,ownership);Assert.AreEqual(MachineStatus.NeedsConfiguration,preview.Status);
        Same(MachineDrawEngine.Preview(definition,state,ownership,policy),preview);
        var request=MachineQaFixtures.Request(definition,state);
        Same(MachineDrawEngine.TryDraw(definition,state,ownership,policy,request,.5),context.TryDraw(state,ownership,request,.5));
        Assert.AreEqual(0,state.DrawCount);Assert.IsEmpty(state.ClaimedRewardIds);Assert.IsEmpty(state.Receipts);
    }

    static void Same(object expected,object actual)=>Assert.AreEqual(MachineQaFixtures.Snapshot(expected),MachineQaFixtures.Snapshot(actual));
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MonsterPouch.Gameplay.Machines;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class MachineDrawSimulationTests
{
    // The cooperative loop enforces fifteen minutes itself; the runner's
    // default three-minute timeout must leave that guard time to report.
    [UnityTest,Category("MachineSimulation"),Timeout(960000)]
    public IEnumerator OneHundredThousandCompleteSyntheticMachinesRespectExternalOwnershipAndGuarantees()
    {
        const int machineCount=100000;
        var definition=MachineQaFixtures.Definition();var policy=MachineQaFixtures.Policy(definition);
        // One immutable QA configuration serves every complete machine. State
        // and external ownership are still independently validated every draw.
        var context=MachineDrawEngine.Prepare(definition,policy);
        string definitionBefore=MachineQaFixtures.Snapshot(definition),policyBefore=MachineQaFixtures.Snapshot(policy);
        var rewards=definition.Rewards.ToDictionary(reward=>reward.RewardId,StringComparer.Ordinal);
        var rng=new Random(20261020);
        var characterAttempts=new long[10];var characterHits=new long[10];
        long paidDraws=0,guaranteedAbilities=0,intermediateAcquisitions=0;
        int largestMachine=0;var elapsed=Stopwatch.StartNew();
        for(int machine=0;machine<machineCount;machine++)
        {
            int scenario=machine%10;
            bool ownsCharacter=scenario>=2&&scenario<=5;
            int initialAbilities=scenario>=2&&scenario<=5?scenario-2:0;
            var externalAbilities=new HashSet<string>(MachineQaFixtures.Abilities.Take(initialAbilities),StringComparer.Ordinal);
            var state=context.CreateState();
            var paidIds=new HashSet<string>(StringComparer.Ordinal);
            bool characterKnown=ownsCharacter,pendingAbility=ownsCharacter&&initialAbilities<3,externalApplied=false;
            MachineDrawReceipt lastReceipt=null;MachineDrawRequest lastRequest=null;
            int safety=0;
            while(true)
            {
                Require(++safety<17,"A 14-prize machine must terminate.");
                if(!externalApplied&&((scenario==8&&state.DrawCount>=3)||(scenario==9&&state.DrawCount>=2)||
                    ((scenario==6||scenario==7)&&characterKnown&&state.DrawCount>=3)))
                {
                    if(!characterKnown)pendingAbility=true;
                    ownsCharacter=true;characterKnown=true;externalApplied=true;intermediateAcquisitions++;
                    if(scenario==7||scenario==9)foreach(var ability in MachineQaFixtures.Abilities)externalAbilities.Add(ability);
                    else if(scenario==6)externalAbilities.Add(MachineQaFixtures.Abilities[0]);
                }
                var ownership=new MachineOwnership{CharacterOwned=ownsCharacter,AbilityIds=externalAbilities.OrderBy(id=>id,StringComparer.Ordinal).ToArray()};
                var known=new HashSet<string>(state.ClaimedRewardIds,StringComparer.Ordinal);
                if(ownsCharacter)known.Add(MachineQaFixtures.Character);
                foreach(var reward in definition.Rewards)
                    if(reward.Kind==MachineRewardKind.Ability&&externalAbilities.Contains(reward.AbilityId))known.Add(reward.RewardId);
                int abilitiesPending=definition.Rewards.Count(reward=>reward.Kind==MachineRewardKind.Ability&&!known.Contains(reward.RewardId));
                if(abilitiesPending==0)pendingAbility=false;
                var preview=context.Preview(state,ownership);
                if(preview.Status==MachineStatus.Complete)
                {
                    Require(known.Count==14&&state.ClaimedRewardIds.Count==14,"A completed machine must reconcile all fourteen rewards.");
                    Require(preview.Outcomes.Length==0&&preview.Rewards.All(reward=>reward.Status==MachineRewardStatus.Claimed),"A complete machine cannot offer a prize.");
                    var blocked=context.TryDraw(state,ownership,MachineQaFixtures.Request(definition,state,"qa-after-complete"),.5);
                    Require(blocked.Status==MachineStatus.Complete&&blocked.State==null&&blocked.Receipt==null,"A complete machine must reject an additional extraction.");
                    break;
                }
                if(preview.Status!=MachineStatus.Ready)Assert.Fail("Scenario "+scenario+": "+preview.Reason);
                Require(Math.Abs(1d-preview.Outcomes.Sum(outcome=>outcome.Probability))<=1e-10,"Probabilities must sum to one.");
                Require(pendingAbility==preview.FirstAbilityGuaranteed,"The independent ability guarantee tracker must match the preview.");
                foreach(var outcome in preview.Outcomes)
                {
                    Require(!known.Contains(outcome.RewardId),"An externally owned or previously drawn reward is ineligible.");
                    Require(outcome.Probability>0&&!double.IsNaN(outcome.Probability)&&!double.IsInfinity(outcome.Probability),"Every eligible probability must be finite and positive.");
                    if(!characterKnown)Require(rewards[outcome.RewardId].Kind!=MachineRewardKind.Ability,"Abilities cannot precede their character.");
                    if(pendingAbility)Require(rewards[outcome.RewardId].Kind==MachineRewardKind.Ability,"The guaranteed branch contains only missing abilities.");
                }
                int draw=state.DrawCount+1;
                if(!characterKnown)
                {
                    Require(draw<=10,"The character must arrive by extraction ten.");
                    characterAttempts[draw-1]++;
                    Require(Math.Abs(policy.CharacterChanceByDraw[draw-1]-preview.Outcomes.Single(outcome=>outcome.RewardId==MachineQaFixtures.Character).Probability)<=1e-12,"The QA character chance is conditional on the current draw.");
                }
                double sample=rng.NextDouble(),cumulative=0;
                string expected=null;
                foreach(var outcome in preview.Outcomes)
                {cumulative+=outcome.Probability;if(sample<cumulative){expected=outcome.RewardId;break;}}
                Require(expected!=null,"A normalized preview must cover every sample in [0,1).");
                var request=MachineQaFixtures.Request(definition,state,"qa-"+machine+"-"+draw);
                var result=context.TryDraw(state,ownership,request,sample);
                if(result.Status!=MachineStatus.Drawn)Assert.Fail(result.Reason);
                Require(expected==result.Receipt.RewardId,"The confirmed draw must use exactly the displayed distribution.");
                Require(paidIds.Add(result.Receipt.RewardId),"A reward cannot be drawn twice.");
                Require(draw==result.State.DrawCount&&draw==result.Receipt.DrawNumber&&draw==result.State.Revision&&draw==result.State.Receipts.Count,"Only a real draw advances counts, revision and receipt history.");
                Require(result.State.ClaimedRewardIds.Count==result.State.ClaimedRewardIds.Distinct().Count(),"Reconciled claims must be unique.");
                var kind=rewards[result.Receipt.RewardId].Kind;
                if(pendingAbility)
                {
                    Require(kind==MachineRewardKind.Ability&&result.Receipt.Guarantee==MachineGuarantee.FirstAbility,"The first pending ability must be recorded in its receipt.");
                    pendingAbility=false;guaranteedAbilities++;
                }
                if(kind==MachineRewardKind.Character)
                {
                    Require(!characterKnown&&draw<=10,"A character cannot repeat or appear after draw ten.");
                    characterHits[draw-1]++;characterKnown=true;pendingAbility=abilitiesPending>0;
                }
                state=result.State;lastReceipt=result.Receipt;lastRequest=request;paidDraws++;
            }
            Require(characterKnown&&state.DrawCount==paidIds.Count&&state.ClaimedRewardIds.Count==14&&state.DrawCount<=14,"Completion must reconcile fourteen unique rewards with no duplicate paid draw.");
            largestMachine=Math.Max(largestMachine,state.DrawCount);
            // One retry per completed machine verifies receipts remain immutable
            // even after all prizes have been claimed. No extra draw is recorded.
            var finalOwnership=new MachineOwnership{CharacterOwned=ownsCharacter,AbilityIds=externalAbilities.ToArray()};
            var retry=context.TryDraw(state,finalOwnership,lastRequest,.12345);
            if(retry.Status!=MachineStatus.AlreadyApplied)Assert.Fail(retry.Reason);
            Require(lastReceipt.RewardId==retry.Receipt.RewardId&&state.DrawCount==retry.State.DrawCount,"A retry cannot change its confirmed prize or advance the machine.");
            if((machine+1)%20000==0)
            {
                string progress="Synthetic machines completed: "+(machine+1)+" / "+machineCount;
                TestContext.Progress.WriteLine(progress);UnityEngine.Debug.Log(progress);
            }
            // Let EditMode return control to the editor without reducing the
            // number of complete machines or skipping any invariant.
            if((machine+1)%250==0)
            {
                Require(elapsed.Elapsed.TotalMinutes<15,"The complete-machine simulation exceeded its 15-minute safety limit.");
                yield return null;
            }
        }
        Assert.AreEqual(14,largestMachine);Assert.AreEqual(40000,intermediateAcquisitions);
        Assert.Greater(guaranteedAbilities,0);Assert.Greater(paidDraws,machineCount);
        Assert.AreEqual(definitionBefore,MachineQaFixtures.Snapshot(definition));Assert.AreEqual(policyBefore,MachineQaFixtures.Snapshot(policy));
        for(int draw=0;draw<10;draw++)
        {
            double chance=policy.CharacterChanceByDraw[draw],expected=characterAttempts[draw]*chance;
            double tolerance=Math.Max(12,8*Math.Sqrt(characterAttempts[draw]*chance*(1-chance)));
            Assert.LessOrEqual(Math.Abs(characterHits[draw]-expected),tolerance,"QA conditional distribution at draw "+(draw+1));
        }
        TestContext.Out.WriteLine("QA_ONLY_COMPLETE_MACHINES="+machineCount+";PAID_DRAWS="+paidDraws+
            ";FIRST_ABILITY_GUARANTEES="+guaranteedAbilities+";INTERMEDIATE_EXTERNAL_ACQUISITIONS="+intermediateAcquisitions+
            ";SEED=20261020;ELAPSED_MS="+elapsed.ElapsedMilliseconds);
        for(int draw=0;draw<10;draw++)TestContext.Out.WriteLine("DRAW="+(draw+1)+";CHARACTER_ELIGIBLE="+characterAttempts[draw]+
            ";CHARACTER_HITS="+characterHits[draw]+";CONDITIONAL_QA_CHANCE="+policy.CharacterChanceByDraw[draw].ToString("R",System.Globalization.CultureInfo.InvariantCulture));
    }
    // Avoid allocating NUnit constraint objects for every successful invariant
    // across more than a million draws; every failure still fails this test.
    static void Require(bool condition,string message){if(!condition)Assert.Fail(message);}
}

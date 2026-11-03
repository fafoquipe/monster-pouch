using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Machines.QA;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class QAMachineTransactionTests
{
    [Serializable] sealed class TestEnvelope {public int Version=1;public string Payload,Sha256;}
    string directory;
    EconomyDevelopmentConfig gate;
    QAMachineConfiguration configuration;
    static string TestRoot=>Environment.GetEnvironmentVariable("MONSTER_POUCH_QA_TEST_ROOT")??Path.GetTempPath();
    string Primary=>Path.Combine(directory,LocalQAMachineStore.SandboxDirectory,LocalQAMachineStore.FileName);
    string Backup=>Path.Combine(directory,LocalQAMachineStore.SandboxDirectory,LocalQAMachineStore.BackupFileName);

    [SetUp] public void Setup()
    {
        directory=Path.Combine(TestRoot,"MonsterPouch-QAMachineTests-"+Guid.NewGuid().ToString("N"));
        gate=ScriptableObject.CreateInstance<EconomyDevelopmentConfig>();gate.EnableLocalQA=true;
        var definition=MachineQaFixtures.Definition();
        configuration=new QAMachineConfiguration {
            Definition=definition,Policy=MachineQaFixtures.Policy(definition),InitialQACredits=20,DrawCost=1,
            InitialOwnership=MachineQaFixtures.Ownership(),
            ResourcePackages=definition.Rewards.Where(r=>r.Kind==MachineRewardKind.Resource).Select((r,i)=>
                new QAMachineResourcePackage {RewardId=r.RewardId,Resources=new[]{
                    new QAMachineResourceAmount {ResourceId="qa-opaque-resource",Amount=i+1}}}).ToArray()
        };
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(gate);
        string target=Path.GetFullPath(directory),root=Path.GetFullPath(TestRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(target).StartsWith("MonsterPouch-QAMachineTests-",StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a non-test directory.");
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    LocalQAMachineStore Store(Action beforeCommit=null)=>new LocalQAMachineStore(directory,configuration,beforeCommit);
    MachineDrawRequest Request(QAMachineSandbox state,string id=null)=>MachineQaFixtures.Request(configuration.Definition,state.Machine,id);
    QAMachineTransactionResult DrawReward(LocalQAMachineStore store,QAMachineSandbox state,string reward,string id=null)
    {
        var preview=store.Preview(gate).Preview;
        return store.TryDraw(gate,Request(state,id),MachineQaFixtures.SampleFor(preview,reward));
    }
    static string Json(object value)=>JsonUtility.ToJson(value);
    static string Hash(string text)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
    void RewritePayload(Func<string,string> rewrite)
    {
        var envelope=JsonUtility.FromJson<TestEnvelope>(File.ReadAllText(Primary));
        envelope.Payload=rewrite(envelope.Payload);envelope.Sha256=Hash(envelope.Payload);
        File.WriteAllText(Primary,Json(envelope));
    }

    [Test] public void DisabledGateCreatesNoSandboxAndNeverUsesTheWelcomeAmount()
    {
        gate.EnableLocalQA=false;gate.starterSaturncoins=999;
        var store=Store();Assert.AreEqual(QAMachineTransactionStatus.Disabled,store.OpenOrCreate(gate).Status);
        Assert.AreEqual(QAMachineTransactionStatus.Disabled,store.Preview(gate).Status);
        Assert.AreEqual(QAMachineTransactionStatus.Disabled,store.TryDraw(gate,null,.2).Status);
        Assert.IsFalse(Directory.Exists(directory));
    }
    [Test] public void SuccessfulCharacterAbilityAndOpaquePackageCommitDebitStateAndReceiptTogether()
    {
        var store=Store();var opened=store.OpenOrCreate(gate);
        Assert.AreEqual(QAMachineTransactionStatus.Initialized,opened.Status,opened.Reason);
        var initial=opened.Snapshot;
        var character=DrawReward(store,initial,MachineQaFixtures.Character,"qa-char");
        Assert.AreEqual(QAMachineTransactionStatus.Committed,character.Status);
        Assert.AreEqual(19,character.Snapshot.QACredits);Assert.AreEqual(1,character.Snapshot.Revision);
        Assert.IsTrue(character.Snapshot.CharacterOwned);Assert.AreEqual(1,character.Snapshot.Machine.DrawCount);
        Assert.AreEqual(1,character.Snapshot.Transactions.Count);Assert.AreEqual("qa-char",character.Receipt.Draw.RequestId);
        var ability=store.TryDraw(gate,Request(character.Snapshot,"qa-ability"),0);
        Assert.AreEqual(QAMachineTransactionStatus.Committed,ability.Status);
        Assert.AreEqual(18,ability.Snapshot.QACredits);Assert.AreEqual(1,ability.Snapshot.AbilityIds.Count);
        Assert.AreEqual(MachineGuarantee.FirstAbility,ability.Receipt.Draw.Guarantee);
        var package=DrawReward(store,ability.Snapshot,"qa-resource-slot-4","qa-resource");
        Assert.AreEqual(QAMachineTransactionStatus.Committed,package.Status);
        Assert.AreEqual(17,package.Snapshot.QACredits);Assert.AreEqual(5,package.Snapshot.Resources.Single().Amount);
        Assert.AreEqual(3,package.Snapshot.Transactions.Count);Assert.AreEqual(3,package.Snapshot.Machine.Receipts.Count);
        Assert.AreEqual(Json(package.Snapshot),Json(Store().OpenOrCreate(gate).Snapshot));
    }
    [Test] public void RestartAndOldRequestRetryKeepTheExactReceiptAndAllLaterProgress()
    {
        var store=Store();var initial=store.OpenOrCreate(gate).Snapshot;var request=Request(initial,"qa-retry");
        var first=store.TryDraw(gate,request,0);var second=store.TryDraw(gate,Request(first.Snapshot,"qa-later"),.3);
        string disk=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var retry=Store().TryDraw(gate,request,.99);
        Assert.AreEqual(QAMachineTransactionStatus.AlreadyApplied,retry.Status);
        Assert.AreEqual(Json(first.Receipt),Json(retry.Receipt));Assert.AreEqual(Json(second.Snapshot),Json(retry.Snapshot));
        Assert.AreEqual(disk,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void ReceiptCollisionAndCompetingStaleWriterCannotReplaceHistory()
    {
        var firstStore=Store();var first=firstStore.OpenOrCreate(gate).Snapshot;
        var secondStore=Store();var stale=secondStore.OpenOrCreate(gate).Snapshot;
        var draw=firstStore.TryDraw(gate,Request(first,"qa-id"),0);string before=File.ReadAllText(Primary);
        Assert.AreEqual(QAMachineTransactionStatus.Conflict,secondStore.TryDraw(gate,Request(stale,"qa-new-id"),.5).Status);
        Assert.AreEqual(QAMachineTransactionStatus.Conflict,secondStore.TryDraw(gate,Request(draw.Snapshot,"qa-id"),.5).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("cost")] [TestCase("package")] [TestCase("policy")]
    public void ConfigurationChangesCannotReuseOrResetAnExistingSandbox(string changed)
    {
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;store.TryDraw(gate,Request(state,"qa-original"),0);
        string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        if(changed=="cost")configuration.DrawCost=2;
        if(changed=="package")configuration.ResourcePackages[0].Resources[0].Amount=999;
        if(changed=="policy")configuration.Policy.Version++;
        var other=Store();Assert.AreEqual(QAMachineTransactionStatus.Conflict,other.OpenOrCreate(gate).Status);
        Assert.AreEqual(QAMachineTransactionStatus.Conflict,other.TryDraw(gate,Request(state,"qa-original"),.5).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void ZeroExplicitCreditsRejectDrawingWithoutWritingAnything()
    {
        configuration.InitialQACredits=0;var store=Store();var state=store.OpenOrCreate(gate).Snapshot;string before=File.ReadAllText(Primary);
        var result=store.TryDraw(gate,Request(state),0);
        Assert.AreEqual(QAMachineTransactionStatus.InsufficientFunds,result.Status);Assert.IsNull(result.Receipt);Assert.IsNull(result.Snapshot);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsFalse(File.Exists(Backup));
    }
    [TestCase(-.1)] [TestCase(double.NaN)] [TestCase(1d)]
    public void InvalidSamplesCannotDebitOrCreateAReceipt(double sample)
    {
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;string before=File.ReadAllText(Primary);
        var result=store.TryDraw(gate,Request(state),sample);
        Assert.AreEqual(QAMachineTransactionStatus.Rejected,result.Status);Assert.AreEqual(MachineStatus.InvalidSample,result.EngineStatus);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsNull(result.Receipt);
    }
    [Test] public void InjectedFailureAfterFlushCannotCommitAnyPartOfTheDraw()
    {
        bool fail=false;var store=Store(()=>{if(fail)throw new IOException("QA injected before atomic replace");});
        var state=store.OpenOrCreate(gate).Snapshot;string before=File.ReadAllText(Primary);fail=true;
        var request=Request(state,"qa-failed-then-retry");var failed=store.TryDraw(gate,request,0);
        Assert.AreEqual(QAMachineTransactionStatus.WriteFailed,failed.Status);Assert.IsNull(failed.Receipt);Assert.IsNull(failed.Snapshot);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsFalse(File.Exists(Backup));
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(Primary),"*.tmp"));
        var retried=Store().TryDraw(gate,request,0);Assert.AreEqual(QAMachineTransactionStatus.Committed,retried.Status);
        Assert.AreEqual(19,retried.Snapshot.QACredits);Assert.AreEqual(1,retried.Snapshot.Transactions.Count);
    }
    [Test] public void CorruptPrimaryUsesBackupReadOnlyAndCannotReplayACommittedDebit()
    {
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;var request=Request(state,"qa-confirmed");
        store.TryDraw(gate,request,0);File.WriteAllText(Primary,"corrupt-primary");string backup=File.ReadAllText(Backup);
        var restarted=Store();var loaded=restarted.OpenOrCreate(gate);
        Assert.IsTrue(restarted.NeedsRecovery);Assert.AreEqual(QAMachineTransactionStatus.RecoveryRequired,loaded.Status);
        Assert.AreEqual(20,loaded.Snapshot.QACredits);
        Assert.AreEqual(QAMachineTransactionStatus.RecoveryRequired,restarted.TryDraw(gate,request,0).Status);
        Assert.AreEqual("corrupt-primary",File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [TestCase("scope")] [TestCase("version")] [TestCase("envelope-version")]
    public void MissingExplicitPersistenceHeadersCannotAcquireSafeDefaults(string missing)
    {
        Store().OpenOrCreate(gate);
        if(missing=="envelope-version")File.WriteAllText(Primary,new Regex("\"Version\"\\s*:\\s*1\\s*,").Replace(File.ReadAllText(Primary),"",1));
        else RewritePayload(payload=>new Regex(missing=="scope"?"\"Scope\":\"InternalQA.MachineSandbox\",":"\"Version\":1,").Replace(payload,"",1));
        string before=File.ReadAllText(Primary);
        var persisted=JsonUtility.FromJson<TestEnvelope>(before);
        if(missing=="scope")StringAssert.DoesNotContain("\"Scope\"",persisted.Payload);
        else if(missing=="version")StringAssert.DoesNotContain("\"Version\"",persisted.Payload);
        else Assert.IsFalse(new Regex("\"Version\"\\s*:").IsMatch(before));
        Assert.Throws<InvalidDataException>(()=>Store().OpenOrCreate(gate));Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void ValidChecksumCannotHideRewrittenDebitOrRewardHistory()
    {
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;store.TryDraw(gate,Request(state),0);
        RewritePayload(payload=>payload.Replace("\"Cost\":1","\"Cost\":2"));string before=File.ReadAllText(Primary);
        var restarted=Store();Assert.AreEqual(QAMachineTransactionStatus.RecoveryRequired,restarted.OpenOrCreate(gate).Status);
        Assert.IsTrue(restarted.NeedsRecovery);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void InputAndReturnedDtoMutationCannotChangeConfigurationDiskOrReceipts()
    {
        var store=Store();configuration.InitialQACredits=999;configuration.DrawCost=999;
        configuration.ResourcePackages[0].Resources[0].Amount=999;configuration.Policy.CharacterChanceByDraw[0]=1;
        var state=store.OpenOrCreate(gate).Snapshot;Assert.AreEqual(20,state.QACredits);
        var draw=store.TryDraw(gate,Request(state,"qa-isolation"),0);string saved=File.ReadAllText(Primary),receipt=Json(draw.Snapshot.Transactions.Single());
        draw.Receipt.Cost=888;draw.Receipt.Draw.RewardId="mutated";
        Assert.AreEqual(receipt,Json(draw.Snapshot.Transactions.Single()));
        draw.Snapshot.QACredits=777;draw.Snapshot.Transactions[0].Cost=777;draw.Snapshot.Machine.ClaimedRewardIds.Clear();
        var reopened=store.OpenOrCreate(gate);Assert.AreEqual(19,reopened.Snapshot.QACredits);
        Assert.AreEqual(1,reopened.Snapshot.Transactions.Single().Cost);Assert.AreEqual(saved,File.ReadAllText(Primary));
    }
    [Test] public void QAExistingProfilesAndGuestPlayerFilesRemainByteForByteUntouched()
    {
        Directory.CreateDirectory(directory);string[] names={"qa-profile.json","guest-profile.json","player-profile.json"};
        foreach(var name in names)File.WriteAllText(Path.Combine(directory,name),"sentinel:"+name);
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;store.TryDraw(gate,Request(state),0);
        foreach(var name in names)Assert.AreEqual("sentinel:"+name,File.ReadAllText(Path.Combine(directory,name)));
    }
    [Test] public void EveryCompleteMachineRejectsAnotherDrawWithoutAnotherDebit()
    {
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;
        for(int i=0;i<14;i++)
        {
            var result=store.TryDraw(gate,Request(state,"qa-all-"+i),.5);
            Assert.AreEqual(QAMachineTransactionStatus.Committed,result.Status,result.Reason);state=result.Snapshot;
        }
        string before=File.ReadAllText(Primary);var rejected=store.TryDraw(gate,Request(state,"qa-fifteenth"),.5);
        Assert.AreEqual(QAMachineTransactionStatus.Rejected,rejected.Status);Assert.AreEqual(MachineStatus.Complete,rejected.EngineStatus);
        Assert.AreEqual(6,state.QACredits);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void ExternalInitialOwnershipIsExcludedWithoutChargingForThoseRewards()
    {
        configuration.InitialOwnership=MachineQaFixtures.Ownership(true,2);var store=Store();var state=store.OpenOrCreate(gate).Snapshot;
        var draw=store.TryDraw(gate,Request(state),.99);
        Assert.AreEqual("qa-ability-prize-2",draw.Receipt.Draw.RewardId);Assert.AreEqual(MachineGuarantee.FirstAbility,draw.Receipt.Draw.Guarantee);
        Assert.AreEqual(19,draw.Snapshot.QACredits);Assert.AreEqual(3,draw.Snapshot.AbilityIds.Count);Assert.AreEqual(1,draw.Snapshot.Transactions.Count);
    }
    [Test] public void ResourceOverflowLeavesTheLastWholeTransactionUnchanged()
    {
        foreach(var package in configuration.ResourcePackages)package.Resources[0].Amount=long.MaxValue;
        configuration.InitialOwnership=MachineQaFixtures.Ownership(true,3);
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;
        var first=DrawReward(store,state,"qa-resource-slot-0");string before=File.ReadAllText(Primary);
        var second=DrawReward(store,first.Snapshot,"qa-resource-slot-1");
        Assert.AreEqual(QAMachineTransactionStatus.Rejected,second.Status);Assert.AreEqual(before,File.ReadAllText(Primary));
        Assert.AreEqual(19,Store().OpenOrCreate(gate).Snapshot.QACredits);
    }
    [Test] public void ReplayAcceptsAValidResourceInTheSmallestFinalProbabilityInterval()
    {
        const double almostOne=.9999999999999999;
        configuration.Policy.CharacterChanceByDraw[0]=almostOne;
        foreach(var weight in configuration.Policy.RewardWeights)
            if(weight.RewardId.StartsWith("qa-resource",StringComparison.Ordinal))weight.Weight=weight.RewardId=="qa-resource-slot-0"?1:0;
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;
        var preview=store.Preview(gate).Preview;
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(almostOne),BitConverter.DoubleToInt64Bits(
            preview.Outcomes.Single(o=>o.RewardId==MachineQaFixtures.Character).Probability));
        var draw=store.TryDraw(gate,Request(state),almostOne);
        Assert.AreEqual(QAMachineTransactionStatus.Committed,draw.Status,draw.Reason);
        Assert.AreEqual("qa-resource-slot-0",draw.Receipt.Draw.RewardId);
        Assert.AreEqual(19,Store().OpenOrCreate(gate).Snapshot.QACredits);
    }
    [TestCase("chance")] [TestCase("weight")]
    public void AdjacentDoubleConfigurationsCannotShareOrRewriteTheSameSandbox(string field)
    {
        const double almostOne=.9999999999999999;
        var weight=configuration.Policy.RewardWeights.Single(w=>w.RewardId=="qa-resource-slot-0");
        if(field=="chance")configuration.Policy.CharacterChanceByDraw[0]=almostOne;
        else weight.Weight=almostOne;
        var original=Store();var state=original.OpenOrCreate(gate).Snapshot;
        var request=Request(state,"qa-exact-policy");
        Assert.AreEqual(QAMachineTransactionStatus.Committed,original.TryDraw(gate,request,0).Status);
        string primary=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        if(field=="chance")configuration.Policy.CharacterChanceByDraw[0]=1;
        else weight.Weight=1;
        var changed=Store();
        Assert.AreEqual(QAMachineTransactionStatus.Conflict,changed.OpenOrCreate(gate).Status);
        Assert.AreEqual(QAMachineTransactionStatus.Conflict,changed.TryDraw(gate,request,0).Status);
        Assert.AreEqual(primary,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        Assert.AreEqual(QAMachineTransactionStatus.AlreadyApplied,original.TryDraw(gate,request,.5).Status);
    }
    [Test] public void MissingEligibleWeightsDoNotWriteOrInventAConfiguredPrize()
    {
        configuration.Policy.RewardWeights=Array.Empty<MachineWeight>();configuration.InitialOwnership=MachineQaFixtures.Ownership(true,3);
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;string before=File.ReadAllText(Primary);
        var rejected=store.TryDraw(gate,Request(state),.5);
        Assert.AreEqual(QAMachineTransactionStatus.Rejected,rejected.Status);Assert.AreEqual(MachineStatus.NeedsConfiguration,rejected.EngineStatus);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void AnotherProcessLockCannotPartiallyWriteTheAggregate()
    {
        var store=Store();var state=store.OpenOrCreate(gate).Snapshot;string before=File.ReadAllText(Primary);
        using(var held=new FileStream(Path.Combine(Path.GetDirectoryName(Primary),"qa-machine-sandbox.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>Store().TryDraw(gate,Request(state),0));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("seed")] [TestCase("cost")] [TestCase("package")] [TestCase("amount")]
    [TestCase("ownership-null")] [TestCase("ability-ids-null")]
    public void MissingOrInvalidExplicitConfigurationCannotCreateAFile(string invalid)
    {
        if(invalid=="seed")configuration.InitialQACredits=-1;
        if(invalid=="cost")configuration.DrawCost=-1;
        if(invalid=="package")configuration.ResourcePackages=configuration.ResourcePackages.Take(9).ToArray();
        if(invalid=="amount")configuration.ResourcePackages[0].Resources[0].Amount=0;
        if(invalid=="ownership-null")configuration.InitialOwnership=null;
        if(invalid=="ability-ids-null")configuration.InitialOwnership.AbilityIds=null;
        Assert.Throws<ArgumentException>(()=>Store());Assert.IsFalse(Directory.Exists(directory));
    }
}

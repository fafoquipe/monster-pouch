using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class GuestVendingTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    PlayerMachineConfiguration machine;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    string Backup=>Path.Combine(directory,"guest-profile.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchGuestVendingTests",Guid.NewGuid().ToString("N"));
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        store=GuestHistoricalProfileFixture.CreateVersionSeven(directory,catalogue);
        machine=Configuration("flo");
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchGuestVendingTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [Test] public void UnconfiguredMachineAndEmptyWalletCannotSpendOrUnlockAnything()
    {
        string before=File.ReadAllText(Primary);
        Assert.AreEqual(GuestVendingStatus.NeedsConfiguration,store.PreviewMachine(catalogue,null).Status);
        machine.EnabledForLocalPlay=false;Assert.AreEqual(GuestVendingStatus.NeedsConfiguration,Draw("no-policy",0).Status);
        machine.EnabledForLocalPlay=true;machine.Packages[0].Amounts[0].Amount=0;
        Assert.AreEqual(GuestVendingStatus.NeedsConfiguration,Draw("empty-package",0).Status);
        machine=Configuration("flo");Assert.AreEqual(GuestVendingStatus.InsufficientFunds,Draw("empty-wallet",0).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsFalse(store.Reload().OwnsCharacter("flo"));
    }
    [Test] public void ApprovedCostCurrencyAndAmountAreExplicitAndNoCreditIsAutomatic()
    {
        machine.Cost=null;Assert.AreEqual(GuestVendingStatus.NeedsConfiguration,store.PreviewMachine(catalogue,machine).Status);
        machine.Cost=new PlayerMachineAmount{Currency=PermanentCurrency.Suncoins,Amount=5};
        Assert.AreEqual(0,store.Reload().FreeBalance(PermanentCurrency.Suncoins));
        Credit(4,PermanentCurrency.Suncoins);Assert.AreEqual(GuestVendingStatus.InsufficientFunds,Draw("no-credit",0).Status);
        store.CreditFreeCurrency("second-grant","test-only",PermanentCurrency.Suncoins,1);
        Assert.AreEqual(GuestVendingStatus.Committed,Draw("paid",0).Status);
        Assert.AreEqual(0,store.Reload().FreeBalance(PermanentCurrency.Suncoins));
    }
    [Test] public void DrawCommitsDebitCharacterAndMachineTogetherThenNextDrawGuaranteesAbility()
    {
        Credit(2);var first=Draw("first",0);Assert.AreEqual(GuestVendingStatus.Committed,first.Status);
        var saved=new LocalGuestProfileStore(directory).Reload();
        Assert.IsTrue(saved.OwnsCharacter("flo"));Assert.AreEqual(1,saved.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(1,saved.VendingMachines.Single().State.DrawCount);
        Assert.AreEqual("vending:first",saved.CharacterUnlocks.Single().ReceiptId);
        var preview=store.PreviewMachine(catalogue,machine);Assert.IsTrue(preview.Preview.FirstAbilityGuaranteed);
        var second=Draw("second",1);Assert.AreEqual(GuestVendingStatus.Committed,second.Status);
        saved=store.Reload();Assert.AreEqual(0,saved.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(1,saved.GetOwnedAbilityIds("flo").Count);Assert.AreEqual(2,saved.VendingMachines.Single().State.DrawCount);
        Assert.AreEqual(MachineGuarantee.FirstAbility,second.Receipt.Draw.Guarantee);
        CollectionAssert.IsSubsetOf(saved.GetOwnedAbilityIds("flo"),catalogue.Get("flo").Tricks.Select(t=>t.Id).ToArray());
    }
    [Test] public void DuplicateTapAndLateRetryReturnOriginalPrizeWithoutDebitOrRollback()
    {
        Credit(3);var first=Draw("first",0);Draw("second",1);
        string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var replay=new LocalGuestProfileStore(directory).DrawMachine(catalogue,machine,Request("first",0));
        Assert.AreEqual(GuestVendingStatus.AlreadyApplied,replay.Status);
        Assert.AreEqual(JsonUtility.ToJson(first.Receipt),JsonUtility.ToJson(replay.Receipt));
        Assert.AreEqual(2,replay.Profile.VendingMachines.Single().State.DrawCount);
        Assert.AreEqual(GuestVendingStatus.Conflict,Draw("first",1).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        Assert.AreEqual(1,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
    }
    [Test] public void ExternalOwnershipIsReconciledAndCompletedMachineCannotSpendAgain()
    {
        store.UnlockCharacter(catalogue,"external-character","flo",GuestUnlockSource.NormalPouch);
        store.UnlockAbility(catalogue,"external-skill","flo",catalogue.Get("flo").Tricks[0].Id,GuestUnlockSource.Mastery);
        Credit(20);var preview=store.PreviewMachine(catalogue,machine);
        Assert.IsTrue(preview.Preview.FirstAbilityGuaranteed);
        Assert.AreEqual(2,preview.Preview.Rewards.Count(r=>r.Status==MachineRewardStatus.Claimed));
        for(int i=0;i<12;i++)Assert.AreEqual(GuestVendingStatus.Committed,Draw("complete-"+i,i).Status,"draw "+i);
        var saved=store.Reload();Assert.AreEqual(8,saved.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(3,saved.GetOwnedAbilityIds("flo").Count);Assert.AreEqual(55,saved.FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(14,saved.VendingMachines.Single().State.ClaimedRewardIds.Count);
        string before=File.ReadAllText(Primary);
        Assert.AreEqual(GuestVendingStatus.Complete,Draw("over-complete",12).Status);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void FreeReceiptImportIsIdempotentAndCannotReuseIdForAnotherAmountOrDraw()
    {
        var grant=Credit(5);string before=File.ReadAllText(Primary);
        Assert.AreEqual(GuestVendingStatus.AlreadyApplied,Credit(5).Status);
        Assert.AreEqual(GuestVendingStatus.Conflict,Credit(6).Status);
        Assert.AreEqual(GuestVendingStatus.Conflict,Draw("earned-receipt",0).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(5,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
    }
    [Test] public void CatalogueChangesAreExplicitConflictsAndCannotReinterpretEarnedPrizes()
    {
        Credit(3);Assert.AreEqual(GuestVendingStatus.Committed,Draw("first",0).Status);string before=File.ReadAllText(Primary);
        // Keep all ten packages distinct so this exercises a valid configuration
        // conflicting with a committed snapshot, not configuration rejection.
        machine.Packages[0].Amounts[0].Amount=100;
        Assert.DoesNotThrow(()=>machine.Validate(catalogue));
        Assert.AreEqual(GuestVendingStatus.Conflict,Draw("second",1).Status);
        Assert.AreEqual(GuestVendingStatus.Conflict,Draw("first",0).Status);
        machine=Configuration("flo");machine.Definition.Rewards[1].AbilityId=catalogue.Get("atori").Tricks[0].Id;
        Assert.AreEqual(GuestVendingStatus.NeedsConfiguration,Draw("invalid-character-map",1).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test,Platform("Win")] public void FailedAtomicReplacePublishesNoDebitOrUnlockAndRetryRemainsAvailable()
    {
        Credit(2);string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>Draw("retry",0));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        Assert.IsFalse(store.Reload().OwnsCharacter("flo"));Assert.AreEqual(2,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp"));
        Assert.AreEqual(GuestVendingStatus.Committed,Draw("retry",0).Status);
    }
    [Test] public void DamagedLatestGenerationCannotReplaySpendUsingAnOlderBackup()
    {
        Credit(2);Draw("first",0);File.WriteAllText(Primary,"corrupt");
        Assert.AreEqual(GuestVendingStatus.RecoveryRequired,Draw("first",0).Status);
        Assert.AreEqual(GuestVendingStatus.RecoveryRequired,Credit(2).Status);
        Assert.AreEqual("corrupt",File.ReadAllText(Primary));
    }
    [Test] public void VersionFiveMigratesWithoutGrantingCurrencyOrRewritingExistingData()
    {
        var profile=store.Reload();profile.Version=5;string before=Encode(GuestHistoricalProfileFixture.WithoutTrophies(JsonUtility.ToJson(profile)));File.WriteAllText(Primary,before);
        var current=store.Reload();Assert.AreEqual(LocalGuestProfile.CurrentVersion,current.Version);Assert.AreEqual(profile.ProfileId,current.ProfileId);
        Assert.AreEqual(profile.Revision,current.Revision);Assert.IsEmpty(current.WalletEvents);Assert.IsEmpty(current.VendingMachines);
        Assert.AreEqual(before,File.ReadAllText(Primary));
        Credit(1);Assert.AreEqual(before,File.ReadAllText(Backup));
        Assert.AreEqual(GuestVendingStatus.Committed,Draw("after-migration",0).Status);
    }
    GuestVendingResult Credit(long amount,PermanentCurrency currency=PermanentCurrency.Saturncoins)=>
        store.CreditFreeCurrency("earned-receipt","test-only",currency,amount);
    GuestVendingResult Draw(string id,long revision)=>store.DrawMachine(catalogue,machine,Request(id,revision));
    MachineDrawRequest Request(string id,long revision)=>new MachineDrawRequest{RequestId=id,MachineId=machine.Definition.MachineId,CatalogVersion=1,ExpectedRevision=revision};
    PlayerMachineConfiguration Configuration(string character)
    {
        var rewards=new[]{new MachineReward{RewardId="character",Kind=MachineRewardKind.Character}}
            .Concat(catalogue.Get(character).Tricks.Select((t,i)=>new MachineReward{RewardId="ability-"+i,Kind=MachineRewardKind.Ability,AbilityId=t.Id}))
            .Concat(Enumerable.Range(0,10).Select(i=>new MachineReward{RewardId="resource-"+i,Kind=MachineRewardKind.Resource})).ToArray();
        // Synthetic test policy and quantities; no production asset or balance is supplied.
        return new PlayerMachineConfiguration{ApprovalId="synthetic-tests-only",EnabledForLocalPlay=true,
            Cost=new PlayerMachineAmount{Currency=PermanentCurrency.Saturncoins,Amount=1},
            Definition=new MachineDefinition{MachineId="test-"+character,CharacterId=character,CatalogVersion=1,Rewards=rewards},
            Policy=new MachinePolicy{PolicyId="synthetic-policy",Version=1,CharacterChanceByDraw=Enumerable.Repeat(1d,10).ToArray(),
                RewardWeights=rewards.Where(r=>r.Kind!=MachineRewardKind.Character).Select(r=>new MachineWeight{RewardId=r.RewardId,Weight=1}).ToArray()},
            Packages=Enumerable.Range(0,10).Select(i=>new PlayerMachinePackage{RewardId="resource-"+i,
                Amounts=new[]{new PlayerMachineAmount{Currency=PermanentCurrency.Earthcoins,Amount=i+1}}}).ToArray()};
    }
    [Serializable] sealed class Envelope{public int Version=1;public string Payload,Sha256;}
    static string Encode(string payload)
    {
        using(var hash=SHA256.Create())return JsonUtility.ToJson(new Envelope{Payload=payload,
            Sha256=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","")});
    }
}

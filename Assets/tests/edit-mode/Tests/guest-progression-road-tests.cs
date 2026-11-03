using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Progression;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class GuestProgressionRoadTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    GuestPouchRoomConfiguration resources;
    readonly DateTime now=new DateTime(2026,10,31,12,0,0,DateTimeKind.Utc);
    string Primary=>Path.Combine(directory,"guest-profile.json");
    [Serializable] sealed class Envelope {public int Version=1;public string Payload,Sha256;}
    [Serializable] sealed class ResourceTable {public string Id;public GuestPouchResourceReward[] Rewards;}
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"EronyaProgressionRoadTests",Guid.NewGuid().ToString("N"));
#if ERONYA_OFFLINE_HARNESS
        // IsMonster fixture generated from the 44 referenced Character.asset definitions by tools/finalize-roster.ps1.
        catalogue=new MatchConfig {Units=GuestProgressionCatalogue.Worlds.SelectMany(w=>w.CharacterIds).Select(id=>new UnitDefinition {
            Id=id,IsMonster=new[]{"anuik","bugaloo","cael","kazuru","tauris","drayven","korran","serya","alveyr","popow","sepora"}.Contains(id),
            Tricks=Enumerable.Range(1,3).Select(i=>new TrickDefinition{Id=id+"-"+i}).ToArray()}).ToArray()};
        resources=new GuestPouchRoomConfiguration {ApprovalId="approved-pouch-room-2026-10-24",ResourceTableId="small-daily-gift-v2",
            Resources=new[]{new GuestPouchResourceReward {Currency=PermanentCurrency.Earthcoins,Amount=10,Chances=10000}}};
#else
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        var table=JsonUtility.FromJson<ResourceTable>(Resources.Load<TextAsset>("MonsterPouch/UI/Lobby/Header/DailyGifts").text);
        resources=new GuestPouchRoomConfiguration {ApprovalId="approved-pouch-room-2026-10-24",ResourceTableId=table.Id,Resources=table.Rewards};
#endif
        store=new LocalGuestProfileStore(directory);store.OpenOrCreate(false);Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,"bugaloo"));
    }
    [TearDown] public void Cleanup()
    {
#if !ERONYA_OFFLINE_HARNESS
        UnityEngine.Object.DestroyImmediate(catalogue);
#endif
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"EronyaProgressionRoadTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    void WinTrophies(int count=1){for(int i=0;i<count;i++)Assert.AreEqual(GuestTrophyResultStatus.Applied,store.RecordTrophyResult(Request()).Status);}
    GuestTrophyResultRequest Request(bool won=true)=>new GuestTrophyResultRequest {MatchId=Guid.NewGuid().ToString("N"),OpponentId="opponent",SourceModeId="online",VerifiedDigest=new string('A',64),PlayerWins=won?3:0,OpponentWins=won?0:3};
    GuestPouchRoomResult Case(int kind=9999,int chance=0,int group=0,int world=0)
    {
        int call=0;
        return store.RecordPouchVictoryCore(catalogue,resources,new GuestPouchVictoryRequest {MatchId=Guid.NewGuid().ToString("N"),SourceModeId="bot",Completed=true,Won=true,CompletedUtc=now},now,max=>{
            if(call++==0)return kind;
            if(kind>=7000&&call==2)return Math.Min(world,max-1);
            if(max!=10000)return 0;
            // Following the permutation: group, character branch, resource.
            if(groupPending){groupPending=false;return group;}
            if(chancePending){chancePending=false;return chance;}
            return 0;
        });
    }
    bool groupPending=true,chancePending=true;
    GuestPouchRoomResult NewCase(int kind=9999,int chance=0,int group=0,int world=0){groupPending=true;chancePending=true;return Case(kind,chance,group,world);}
    GuestProgressionResult Claim(string id,int chance=0,int group=0)
    {
        int large=0;
        return store.ClaimProgressionMilestoneCore(catalogue,resources,id,now,max=>max!=10000?0:large++==0?chance:large==2?group:0);
    }
    GuestPouchOpenReceipt OpenCase(string id)=>store.OpenPouch(catalogue,resources,id,Guid.NewGuid().ToString("N"),now).OpenReceipt;
    void Persist(LocalGuestProfile profile,bool old=false)
    {
        var json=JObject.Parse(JsonUtility.ToJson(profile));
        if(old){json["Version"]=10;foreach(var key in new[]{"ProgressionClaims","ProgressionPouches","ProgressionOpenings","ProgressionTutorialVisitedUtc"})json.Remove(key);}
        string payload=json.ToString(Newtonsoft.Json.Formatting.None);
        using(var hash=SHA256.Create())File.WriteAllText(Primary,JsonUtility.ToJson(new Envelope {Payload=payload,Sha256=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","")}));
        string backup=Path.Combine(directory,"guest-profile.backup.json");if(File.Exists(backup))File.Delete(backup);
    }
    [Test] public void CatalogueHasThirtyUniqueStepsAndUniqueTwentyFourPlusTwentyPools()
    {
        var worlds=GuestProgressionCatalogue.Worlds;var ids=worlds.SelectMany(w=>w.CharacterIds).ToArray();
        Assert.AreEqual(5,worlds.Length);Assert.AreEqual(44,ids.Length);Assert.AreEqual(44,ids.Distinct().Count());
        Assert.AreEqual(20,ids.Count(GuestProgressionCatalogue.IsEronya));Assert.IsTrue(ids.All(id=>catalogue.Get(id)!=null));
        Assert.IsTrue(catalogue.Get("kazuru").IsMonster,"Kazuru retains its runtime Monster/HEROES category.");
        Assert.AreEqual(11,ids.Count(id=>catalogue.Get(id).IsMonster));
        Assert.Contains("kazuru",GuestProgressionCatalogue.GetWorld("naturalis").CharacterIds);
        Assert.Contains("atori",GuestProgressionCatalogue.GetWorld("volkanar").CharacterIds);
        Assert.Contains("atong",GuestProgressionCatalogue.GetWorld("geothar").CharacterIds);
        CollectionAssert.AreEqual(Enumerable.Range(1,30).Select(i=>i*50),GuestProgressionCatalogue.Milestones.Select(m=>m.RequiredTrophies));
        Assert.AreEqual(30,GuestProgressionCatalogue.Milestones.Select(m=>m.Id).Distinct().Count());
        Assert.IsTrue(worlds.All(w=>w.EndTrophies-w.StartTrophies==300));
        foreach(var w in worlds)Assert.AreEqual(4,w.CharacterIds.Count(GuestProgressionCatalogue.IsEronya));
    }
    [Test] public void ExactFiftyThreeHundredAndFifteenHundredBoundariesUseCurrentScore()
    {
        foreach(int boundary in Enumerable.Range(1,30).Select(i=>i*50)) {
            Assert.AreEqual((boundary-1)/50,GuestProgressionCatalogue.CurrentStep(boundary-1));
            Assert.AreEqual(boundary/50,GuestProgressionCatalogue.CurrentStep(boundary));
        }
        foreach(var world in GuestProgressionCatalogue.Worlds) {
            Assert.AreEqual(world.Id,GuestProgressionCatalogue.CurrentWorld(world.StartTrophies));
            Assert.AreEqual(world.Id,GuestProgressionCatalogue.CurrentWorld(world.EndTrophies-1));
        }
        Assert.AreEqual("royal",GuestProgressionCatalogue.CurrentWorld(1500));Assert.AreEqual(30,GuestProgressionCatalogue.CurrentStep(long.MaxValue));
    }
    [Test] public void RepeatedReadDoesNotWriteOrGrantAndLockedClaimIsRejected()
    {
        string before=File.ReadAllText(Primary);for(int i=0;i<3;i++)Assert.AreEqual(GuestProgressionStatus.Ready,store.ReadProgression(catalogue,resources,now).Status);
        Assert.AreEqual(GuestProgressionStatus.Locked,Claim("road-v1-0050").Status);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void EveryMilestoneClaimsExactlyOnceAcrossReloadAndDoesNotChangeTrophiesOrExistingOwnership()
    {
        WinTrophies(50);var before=store.Reload();var owned=before.GetOwnedCharacterIds().ToArray();
        foreach(var milestone in GuestProgressionCatalogue.Milestones) {
            Assert.AreEqual(GuestProgressionStatus.Claimed,Claim(milestone.Id).Status);
            store=new LocalGuestProfileStore(directory);string committed=File.ReadAllText(Primary);
            Assert.AreEqual(GuestProgressionStatus.AlreadyApplied,Claim(milestone.Id,9999,9999).Status);Assert.AreEqual(committed,File.ReadAllText(Primary));
        }
        var after=store.Reload();Assert.AreEqual(1500,after.Trophies);Assert.AreEqual(1500,after.HighestTrophies);
        CollectionAssert.AreEqual(owned,after.GetOwnedCharacterIds());Assert.AreEqual(30,after.ProgressionClaims.Count);Assert.AreEqual(20,after.ProgressionPouches.Count);
        Assert.AreEqual(before.FreeBalance(PermanentCurrency.Earthcoins)+100,after.FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(before.FreeBalance(PermanentCurrency.Suncoins)+100,after.FreeBalance(PermanentCurrency.Suncoins));
    }
    [Test] public void HistoricalMaximumKeepsMilestoneClaimableWhenCurrentCupsFall()
    {
        WinTrophies(10);store.RecordTrophyResult(Request(false));var read=store.ReadProgression(catalogue,resources,now).Snapshot;
        Assert.Less(read.CurrentTrophies,300);Assert.AreEqual(300,read.HighestTrophies);Assert.AreEqual("naturalis",read.CurrentWorldId);
        Assert.AreEqual(GuestMilestoneState.Claimable,read.Milestones.Single(m=>m.RequiredTrophies==300).State);
        Assert.AreEqual(GuestProgressionStatus.Claimed,Claim("road-v1-0300").Status);
    }
    [Test] public void TwentyEightyAndThreeToOneProbabilitiesHaveExactNonRandomBoundaries()
    {
        Assert.AreEqual(2000,Enumerable.Range(0,10000).Count(r=>GuestProgressionCatalogue.CharacterPrize(r,4)));
        Assert.AreEqual(0,Enumerable.Range(0,10000).Count(r=>GuestProgressionCatalogue.CharacterPrize(r,0)));
        string[] eligible={"cael","flo","antidota","rosie"};
        Assert.AreEqual(7500,Enumerable.Range(0,10000).Count(r=>GuestProgressionCatalogue.IsEronya(GuestProgressionCatalogue.SelectCharacter(eligible,r,3,1))));
        Assert.AreEqual("cael",GuestProgressionCatalogue.SelectCharacter(new[]{"cael"},9999,3,1));
        Assert.AreEqual("flo",GuestProgressionCatalogue.SelectCharacter(new[]{"flo"},0,3,1));
    }
    [Test] public void DailyCasesAreUnlimitedUseSeventyThirtyFromFirstVictoryAndNeverFutureWorlds()
    {
        for(int i=0;i<13;i++) {
            bool faction=i%2==1;var result=NewCase(faction?7000:6999);
            Assert.AreEqual(GuestPouchRoomStatus.Granted,result.Status);var pouch=result.Profile.Pouches.Last();
            Assert.AreEqual(faction?GuestPouchKind.Faction:GuestPouchKind.Blue,pouch.Kind);if(faction)Assert.AreEqual("naturalis",pouch.WorldId);
        }
        store=new LocalGuestProfileStore(directory);Assert.AreEqual(13,store.ReadPouchRoom(catalogue,resources,now).Snapshot.EarnedCount);
        WinTrophies(50);for(int i=0;i<5;i++)Assert.AreEqual(GuestProgressionCatalogue.Worlds[i].Id,NewCase(world:i).Profile.Pouches.Last().WorldId);
    }
    [Test] public void CaseCharacterBranchPersistsAcrossReloadAndNeverGrantsForeignOrOwnedCharacter()
    {
        var pouch=NewCase(chance:1999,group:0).Profile.Pouches.Last();
        store=new LocalGuestProfileStore(directory);var receipt=OpenCase(pouch.Id);
        Assert.AreEqual(GuestPouchRewardKind.Character,receipt.RewardKind);Assert.IsTrue(GuestProgressionCatalogue.IsEronya(receipt.CharacterId));
        Assert.Contains(receipt.CharacterId,GuestProgressionCatalogue.GetWorld(pouch.WorldId).CharacterIds);
        string committed=File.ReadAllText(Primary);Assert.AreEqual(receipt.CharacterId,OpenCase(pouch.Id).CharacterId);Assert.AreEqual(committed,File.ReadAllText(Primary));
        var other=NewCase(chance:2000).Profile.Pouches.Last();Assert.AreEqual(GuestPouchRewardKind.Resources,OpenCase(other.Id).RewardKind);
    }
    [Test] public void AcquiringSelectedCharacterElsewhereKeepsTwentyPercentBranchForRemainingPool()
    {
        WinTrophies(5);var claim=Claim("road-v1-0150",0,0);var pouch=claim.Profile.ProgressionPouches.Single();
        string candidate=GuestProgressionCatalogue.SelectCharacter(pouch.CharacterOrder.Where(id=>!claim.Profile.OwnsCharacter(id)).ToArray(),pouch.GroupRoll,pouch.EronyaWeight,pouch.LegacyWeight);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,store.UnlockCharacter(catalogue,"fixture-owned",candidate,GuestUnlockSource.Mastery));
        store=new LocalGuestProfileStore(directory);var result=store.OpenProgressionPouch(catalogue,resources,pouch.Id,now);
        Assert.AreEqual(GuestPouchRewardKind.Character,result.OpenReceipt.RewardKind);Assert.AreNotEqual(candidate,result.OpenReceipt.CharacterId);
        Assert.AreEqual(GuestProgressionStatus.AlreadyApplied,store.OpenProgressionPouch(catalogue,resources,pouch.Id,now).Status);
    }
    [Test] public void AllOwnedPoolGivesOnlyResourcesAndSnapshotReportsZeroCharacterChance()
    {
        foreach(string id in GuestProgressionCatalogue.GetWorld("naturalis").CharacterIds)
            if(!store.Reload().OwnsCharacter(id))store.UnlockCharacter(catalogue,"fixture-"+id,id,GuestUnlockSource.Mastery);
        var pouch=NewCase(chance:0).Profile.Pouches.Last();Assert.AreEqual(GuestPouchKind.Faction,pouch.Kind);
        var slot=store.ReadPouchRoom(catalogue,resources,now).Snapshot.Slots.Single(s=>s.PouchId==pouch.Id);
        Assert.IsFalse(slot.CanReceiveNewCharacter);Assert.AreEqual(0,slot.CharacterChanceBasisPoints);
        Assert.AreEqual(GuestPouchRewardKind.Resources,OpenCase(pouch.Id).RewardKind);
    }
    [Test] public void NewGroupExhaustionRedistributesToRemainingLegacyWithoutDuplicate()
    {
        foreach(string id in GuestProgressionCatalogue.GetWorld("naturalis").CharacterIds.Where(GuestProgressionCatalogue.IsEronya))
            store.UnlockCharacter(catalogue,"fixture-"+id,id,GuestUnlockSource.Mastery);
        var pouch=NewCase(chance:0,group:0).Profile.Pouches.Last();var receipt=OpenCase(pouch.Id);
        Assert.AreEqual(GuestPouchRewardKind.Character,receipt.RewardKind);Assert.IsFalse(GuestProgressionCatalogue.IsEronya(receipt.CharacterId));
    }
    [Test] public void InvalidRollOrMissingPoolNeverMutatesSave()
    {
        WinTrophies(5);string before=File.ReadAllText(Primary);
        Assert.AreEqual(GuestProgressionStatus.InvalidRequest,store.ClaimProgressionMilestoneCore(catalogue,resources,"road-v1-0150",now,max=>max).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));var original=catalogue.Units;catalogue.Units=original.Where(u=>u.Id!="cael").ToArray();
        Assert.AreEqual(GuestProgressionStatus.NeedsConfiguration,store.ReadProgression(catalogue,resources,now).Status);Assert.AreEqual(before,File.ReadAllText(Primary));catalogue.Units=original;
    }
    [Test] public void TamperedForeignPoolAndDuplicateClaimsAreRejected()
    {
        WinTrophies(5);Claim("road-v1-0150");var profile=store.Reload();profile.ProgressionPouches[0].CharacterOrder[0]="drayven";
        Assert.Throws<InvalidDataException>(()=>profile.Validate());profile=store.Reload();profile.ProgressionClaims.Add(profile.ProgressionClaims[0]);
        Assert.Throws<InvalidDataException>(()=>profile.Validate());
    }
    [Test] public void MigratesBothHistoricalLadderPoliciesWithoutChangingReceiptsCupsWalletOrBoardUnlocks()
    {
        var profile=store.Reload();var first=GuestTrophyLadder.CreateLegacyEngine();var state=first.CreateState(profile.ProfileId);
        for(int i=0;i<6;i++)state=Apply(first,state,true);
        var second=GuestTrophyLadder.CreatePreviousEngine();state=second.UpgradeState(state).State;
        for(int i=0;i<15;i++)state=Apply(second,state,true);
        var segment=state.HistoricalSegments.Single();state.Version=2;state.HistoricalPolicySignature=segment.PolicySignature;state.HistoricalMatchCount=segment.EndMatchIndex;state.HistoricalSegments=Array.Empty<TrophyPolicySegment>();
        profile.TrophyLadder=state;string receipts=JsonUtility.ToJson(state.Matches);Persist(profile,true);string oldFile=File.ReadAllText(Primary);
        var upgraded=store.Reload();Assert.AreEqual(oldFile,File.ReadAllText(Primary));Assert.AreEqual(630,upgraded.Trophies);Assert.AreEqual(630,upgraded.HighestTrophies);
        Assert.AreEqual(receipts,JsonUtility.ToJson(upgraded.TrophyLadder.Matches));Assert.AreEqual(20,upgraded.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsTrue(GuestTrophyLadder.CreateEngine().PreviewStage(upgraded.TrophyLadder).ReachedStages.Any(s=>s.BoardId=="royal"));
        Assert.Contains("aireola",GuestTrophyLadder.ReachedBoardIds(upgraded.TrophyLadder),"The old cosmetic Aireola shared the 600-cup royal unlock.");
        WinTrophies();Assert.AreEqual(660,store.Reload().Trophies);Assert.AreEqual(2,store.Reload().TrophyLadder.HistoricalSegments.Length);
    }
    TrophyLadderState Apply(TrophyLadderEngine engine,TrophyLadderState state,bool won)
    {
        var request=Request(won);return engine.Record(state,new TrophyMatchReceipt {PlayerId=state.PlayerId,OpponentPlayerId=request.OpponentId,
            MatchId=request.MatchId,SourceModeId=request.SourceModeId,EligibilityEvidenceId=GuestTrophyRules.Evidence(request),PlayerTrophiesBefore=state.CurrentTrophies,
            OpponentTrophiesBefore=0,Outcome=won?TrophyMatchOutcome.Victory:TrophyMatchOutcome.Defeat,Eligibility=TrophyMatchEligibility.ApprovedCompetitive},state.Revision).State;
    }
    [Test] public void OldSpecificCasesKeepIdentityRollValueAndOneTimeOpening()
    {
        var profile=store.Reload();profile.PouchRoomDay="2026-10-31";
        for(int i=0;i<4;i++) {
            string id="historical-case-"+i,match="historical-match-"+i;
            profile.PouchVictories.Add(new GuestPouchVictoryReceipt {MatchId=match,SourceModeId="bot",CompletedUtc=now.ToString("O"),RecordedUtc=now.ToString("O"),Day=profile.PouchRoomDay,PouchId=id,Status=GuestPouchRoomStatus.Granted});
            profile.Pouches.Add(new GuestPouchInstance {Id=id,MatchId=match,Day=profile.PouchRoomDay,SlotIndex=i,Kind=i==3?GuestPouchKind.Character:GuestPouchKind.Blue,
                CharacterId=i==3?"flo":"",CharacterRoll=i==3?2999:-1,ResourceRoll=0,Configuration=resources.Snapshot()});
        }
        Persist(profile,true);var receipt=OpenCase("historical-case-3");Assert.AreEqual("flo",receipt.CharacterId);
        Assert.AreEqual(GuestPouchRewardKind.Character,receipt.RewardKind);Assert.AreEqual("flo",OpenCase("historical-case-3").CharacterId);
        Assert.AreEqual(1,store.Reload().CharacterUnlocks.Count(r=>r.CharacterId=="flo"));
    }
    [Test] public void RetiredMachinesCannotChargeAndTutorialCanFinishWithRoadVisit()
    {
        string before=File.ReadAllText(Primary);Assert.AreEqual(GuestVendingStatus.Retired,store.PreviewMachine(catalogue,null).Status);Assert.AreEqual(before,File.ReadAllText(Primary));
        foreach(var module in GuestTutorialModules.OrderedIds.Take(6))Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(module));
        store.ClaimTutorialPouchCore(catalogue,resources,max=>0);store.OpenTutorialPouch();
        foreach(var visit in new[]{"deck","collection","ability","wallet"})store.RecordTutorialMenuVisit(visit);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.ProgressionCollection));
        Assert.IsTrue(store.RecordTutorialProgressionVisit().TutorialMachineReady);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.FirstMachine));
        Assert.AreEqual(0,store.Reload().VendingMachines.Count);
    }
    [Test] public void FailedAtomicClaimDoesNotPublishRewardAndRetryClaimsOnce()
    {
        WinTrophies(4);string before=File.ReadAllText(Primary);
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>Claim("road-v1-0100"));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp"));
        Assert.AreEqual(GuestProgressionStatus.Claimed,Claim("road-v1-0100").Status);
        Assert.AreEqual(GuestProgressionStatus.AlreadyApplied,Claim("road-v1-0100").Status);
        Assert.AreEqual(20,store.Reload().FreeBalance(PermanentCurrency.Earthcoins));
    }
    [Test] public void DamagedLatestSaveBlocksClaimAndOpenInsteadOfReplayingBackup()
    {
        WinTrophies(5);var claim=Claim("road-v1-0150");string id=claim.ClaimReceipt.PouchId;
        store.OpenProgressionPouch(catalogue,resources,id,now);File.WriteAllText(Primary,"corrupt");
        Assert.AreEqual(GuestProgressionStatus.RecoveryRequired,Claim("road-v1-0150").Status);
        Assert.AreEqual(GuestProgressionStatus.RecoveryRequired,store.OpenProgressionPouch(catalogue,resources,id,now).Status);
        Assert.AreEqual("corrupt",File.ReadAllText(Primary));
    }
    [Test] public void HistoricalMachineReceiptStillLoadsAndRetriesWithoutNewCharge()
    {
        var rewards=new[]{new MachineReward {RewardId="character",Kind=MachineRewardKind.Character}}
            .Concat(catalogue.Get("flo").Tricks.Select((t,i)=>new MachineReward {RewardId="ability-"+i,Kind=MachineRewardKind.Ability,AbilityId=t.Id}))
            .Concat(Enumerable.Range(0,10).Select(i=>new MachineReward {RewardId="resource-"+i,Kind=MachineRewardKind.Resource})).ToArray();
        var config=new PlayerMachineConfiguration {ApprovalId="historical-test-only",EnabledForLocalPlay=true,
            Cost=new PlayerMachineAmount {Currency=PermanentCurrency.Saturncoins,Amount=1},
            Definition=new MachineDefinition {MachineId="historical-flo",CharacterId="flo",CatalogVersion=1,Rewards=rewards},
            Policy=new MachinePolicy {PolicyId="historical-test-policy",Version=1,CharacterChanceByDraw=Enumerable.Repeat(1d,10).ToArray(),
                RewardWeights=rewards.Where(r=>r.Kind!=MachineRewardKind.Character).Select(r=>new MachineWeight {RewardId=r.RewardId,Weight=1}).ToArray()},
            Packages=Enumerable.Range(0,10).Select(i=>new PlayerMachinePackage {RewardId="resource-"+i,Amounts=new[]{new PlayerMachineAmount {Currency=PermanentCurrency.Earthcoins,Amount=i+1}}}).ToArray()};
        config.Validate(catalogue);var engine=MachineDrawEngine.Prepare(config.Definition,config.Policy);
        var request=new MachineDrawRequest {MachineId=config.Definition.MachineId,CatalogVersion=1,ExpectedRevision=0,RequestId="historic-draw"};
        var ownership=new MachineOwnership {CharacterOwned=false};var draw=engine.TryDraw(engine.CreateState(),ownership,request,0);
        var profile=store.Reload();profile.VendingMachines.Add(new GuestVendingMachine {Configuration=config,State=draw.State});
        profile.WalletEvents.Add(new GuestWalletEvent {Id=request.RequestId,SourceId=config.ApprovalId,TimestampUtc=now.ToString("O"),Kind=GuestWalletEventKind.MachineDraw,
            MachineId=request.MachineId,Draw=draw.Receipt,OwnershipBefore=ownership,Amounts=LocalGuestProfile.VendingAmounts(config,rewards[0])});
        profile.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt {ReceiptId=LocalGuestProfile.VendingUnlockId(request.RequestId),CharacterId="flo",TimestampUtc=now.ToString("O"),Source=GuestUnlockSource.Machine});
        Persist(profile,true);string before=File.ReadAllText(Primary);
        Assert.AreEqual(GuestVendingStatus.AlreadyApplied,store.DrawMachine(catalogue,config,request).Status);
        request.RequestId="new-draw";request.ExpectedRevision=1;Assert.AreEqual(GuestVendingStatus.Retired,store.DrawMachine(catalogue,config,request).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(19,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));Assert.IsTrue(store.Reload().OwnsCharacter("flo"));
    }
}

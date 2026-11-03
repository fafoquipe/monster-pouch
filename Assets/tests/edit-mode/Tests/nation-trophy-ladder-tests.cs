using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Progression;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class NationTrophyLadderTests
{
    string directory;
    LocalGuestProfileStore store;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchNationTrophyTests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchNationTrophyTests"))+Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(directory).StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    [TestCase(0,"naturalis",0,150)] [TestCase(150,"aquarion",10,300)]
    [TestCase(300,"geothar",15,450)] [TestCase(450,"volkanar",25,600)]
    [TestCase(600,"royal",30,-1)] [TestCase(900,"royal",30,-1)]
    public void ExactThresholdsUseCurrentStageLossAndTheLastStageHasNoCeiling(int score,string board,int loss,int next)
    {
        var engine=GuestTrophyLadder.CreateEngine();var state=engine.CreateState("player");
        for(int i=0;i<score/30;i++)state=Record(engine,state,true).State;
        var preview=engine.PreviewStage(state);Assert.AreEqual(TrophyLadderStatus.Ready,preview.Status);
        Assert.AreEqual(score,preview.CurrentTrophies);Assert.AreEqual(board,preview.BoardId);
        Assert.AreEqual(next<0?(long?)null:next,preview.NextStageAt);
        var result=Record(engine,state,false);Assert.AreEqual(-loss,result.RecordedMatch.AppliedDelta);
        Assert.AreEqual(score-loss,result.State.CurrentTrophies);Assert.AreEqual(score,result.State.HighestTrophies);
        Assert.AreEqual(30,Record(engine,result.State,true).RecordedMatch.AppliedDelta);
    }
    [TestCase(150,140,"naturalis")] [TestCase(300,285,"aquarion")]
    [TestCase(450,425,"geothar")] [TestCase(600,570,"volkanar")]
    public void DescendingUsesPreMatchStageThenSwitchesAccessToTheLowerStage(int start,int expected,string board)
    {
        var engine=GuestTrophyLadder.CreateEngine();var state=engine.CreateState("player");
        for(int i=0;i<start/30;i++)state=Record(engine,state,true).State;
        state=Record(engine,state,false).State;
        var preview=engine.PreviewStage(state);Assert.AreEqual(expected,state.CurrentTrophies);Assert.AreEqual(board,preview.BoardId);
        Assert.AreEqual(start,state.HighestTrophies);Assert.AreEqual(start,preview.NextStageAt);
        long lowerLoss=GuestTrophyLadder.CreatePolicy().Stages.Last(s=>s.MinimumTrophies<=expected).DefeatLossOverride.Value;
        Assert.AreEqual(-lowerLoss,Record(engine,state,false).RecordedMatch.AppliedDelta);
        var crossed=Record(engine,state,true);Assert.GreaterOrEqual(crossed.State.CurrentTrophies,start);
        Assert.AreEqual(30,crossed.RecordedMatch.AppliedDelta);
    }
    [Test] public void CatalogueContainsOnlyTheFiveApprovedProgressionStagesAndKeepsCosmeticsSeparate()
    {
        var policy=GuestTrophyLadder.CreatePolicy();var catalogue=GuestTrophyLadder.CreateCatalogue();
        CollectionAssert.AreEqual(new[]{"naturalis","aquarion","geothar","volkanar","royal"},policy.Stages.Select(s=>s.BoardId));
        CollectionAssert.AreEqual(new long[]{0,150,300,450,600},policy.Stages.Select(s=>s.MinimumTrophies));
        CollectionAssert.AreEqual(new long?[]{5,10,15,25,30},policy.Stages.Select(s=>s.DefeatLossOverride));
        Assert.AreEqual(TrophyBoardRole.InitialProgression,catalogue.Boards.Single(b=>b.BoardId=="naturalis").Role);
        Assert.AreEqual(TrophyBoardRole.OwnedCosmetic,catalogue.Boards.Single(b=>b.BoardId=="classic").Role);
        Assert.AreEqual(TrophyBoardRole.PaidCosmetic,catalogue.Boards.Single(b=>b.BoardId=="lounge").Role);
        Assert.IsFalse(catalogue.Boards.Any(b=>b.BoardId=="aireola"));
        policy.Stages[1].BoardId="classic";Assert.IsFalse(TrophyLadderEngine.TryCreate(policy,catalogue,out _,out _));
    }
    [Test] public void HistoricalR0PrefixMigratesInMemoryAndRetainsItsOldLossAfterNewResults()
    {
        var historical=WriteLegacyHistory(5,true,out var requests);string oldJson=File.ReadAllText(Primary);
        string oldMatches=JsonUtility.ToJson(historical.TrophyLadder);
        var loaded=store.Reload();Assert.AreEqual(120,loaded.Trophies);Assert.AreEqual(150,loaded.HighestTrophies);
        Assert.AreEqual(2,loaded.TrophyLadder.Version);Assert.AreEqual(6,loaded.TrophyLadder.HistoricalMatchCount);
        Assert.AreEqual(historical.TrophyLadder.PolicySignature,loaded.TrophyLadder.HistoricalPolicySignature);
        Assert.AreEqual(historical.Revision,loaded.Revision);Assert.AreEqual(historical.ProfileId,loaded.ProfileId);
        Assert.AreEqual(oldJson,File.ReadAllText(Primary));
        Assert.AreEqual(77,loaded.FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual("naturalis",GuestTrophyLadder.CreateEngine().PreviewStage(loaded.TrophyLadder).BoardId);
        Assert.AreEqual(-30,loaded.TrophyLadder.Matches.Last().AppliedDelta);
        var newWin=store.RecordTrophyResult(Request(true));Assert.AreEqual(150,newWin.Profile.Trophies);
        var newLoss=store.RecordTrophyResult(Request(false));Assert.AreEqual(-10,newLoss.AppliedDelta);Assert.AreEqual(140,newLoss.Profile.Trophies);
        var current=store.Reload();Assert.AreEqual(6,current.TrophyLadder.HistoricalMatchCount);
        for(int i=0;i<6;i++)Assert.AreEqual(JsonUtility.ToJson(historical.TrophyLadder.Matches[i]),JsonUtility.ToJson(current.TrophyLadder.Matches[i]));
        string after=File.ReadAllText(Primary);var retry=store.RecordTrophyResult(requests.Last());
        Assert.AreEqual(GuestTrophyResultStatus.AlreadyApplied,retry.Status);Assert.AreEqual(-30,retry.AppliedDelta);
        Assert.AreEqual(140,retry.Profile.Trophies);Assert.AreEqual(after,File.ReadAllText(Primary));
        Assert.AreEqual(oldMatches,JsonUtility.ToJson(historical.TrophyLadder),"Migration must not mutate the supplied R0 state.");
        Assert.AreEqual(77,current.FreeBalance(PermanentCurrency.Earthcoins));
    }
    [Test] public void EmptyHistoricalStateMigratesWithoutCreatingResultsOrChangingProfileRevision()
    {
        var historical=WriteLegacyHistory(0,false,out _);string before=File.ReadAllText(Primary);
        var current=store.Reload();Assert.AreEqual(0,current.Trophies);Assert.AreEqual(0,current.HighestTrophies);
        Assert.IsEmpty(current.TrophyLadder.Matches);Assert.AreEqual(0,current.TrophyLadder.HistoricalMatchCount);
        Assert.AreEqual(historical.Revision,current.Revision);Assert.AreEqual(before,File.ReadAllText(Primary));
        Assert.AreEqual(30,store.RecordTrophyResult(Request(true)).AppliedDelta);
    }
    [TestCase("delta")] [TestCase("signature")] [TestCase("score")]
    public void CorruptHistoricalAccountingIsRejectedBeforeMigrationOrAnyNewAward(string kind)
    {
        WriteLegacyHistory(5,true,out _);var payload=JObject.Parse(JsonUtility.ToJson(store.Reload()));
        // Recover the genuine R0 payload from disk, rather than the already migrated in-memory copy.
        payload=JObject.Parse((string)JObject.Parse(File.ReadAllText(Primary))["Payload"]);
        if(kind=="delta")payload["TrophyLadder"]["Matches"][5]["AppliedDelta"]=-10;
        if(kind=="signature")payload["TrophyLadder"]["PolicySignature"]="unapproved-policy";
        if(kind=="score")payload["TrophyLadder"]["CurrentTrophies"]=140;
        WritePayload(payload);string before=File.ReadAllText(Primary);
        Assert.AreEqual(GuestTrophyResultStatus.RecoveryRequired,store.RecordTrophyResult(Request()).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("HistoricalMatchCount",5)] [TestCase("HistoricalMatchCount",7)] [TestCase("HistoricalMatchCount",99)]
    public void APolicyBoundaryCannotMoveAcrossOldOrNewLossReceipts(string field,int value)
    {
        WriteLegacyHistory(5,true,out _);
        Assert.AreEqual(GuestTrophyResultStatus.Applied,store.RecordTrophyResult(Request(true)).Status);
        Assert.AreEqual(GuestTrophyResultStatus.Applied,store.RecordTrophyResult(Request(false)).Status);
        var payload=JObject.Parse(JsonUtility.ToJson(store.Reload()));payload["TrophyLadder"][field]=value;
        var altered=JsonUtility.FromJson<LocalGuestProfile>(payload.ToString(Newtonsoft.Json.Formatting.None));
        Assert.AreEqual(TrophyLadderStatus.InvalidState,GuestTrophyLadder.CreateEngine().UpgradeState(altered.TrophyLadder).Status,
            "The altered boundary must fail history validation independently of disk recovery.");

        // Each successful award leaves a valid prior generation. Reload must preserve that
        // recovery path rather than throw merely because the latest generation is invalid.
        string backup=Path.Combine(directory,"guest-profile.backup.json");
        Assert.IsTrue(File.Exists(backup));
        string backupBefore=File.ReadAllText(backup);
        var expected=JObject.Parse((string)JObject.Parse(backupBefore)["Payload"]);
        WritePayload(payload);string primaryBefore=File.ReadAllText(Primary);
        var recovered=store.Reload();Assert.IsTrue(store.NeedsRecovery);
        Assert.AreEqual((string)expected["ProfileId"],recovered.ProfileId);
        Assert.AreEqual((long)expected["Revision"],recovered.Revision);
        Assert.IsTrue(JToken.DeepEquals(expected["TrophyLadder"],JObject.Parse(JsonUtility.ToJson(recovered.TrophyLadder))),
            "Recovery must return exactly the valid backup history, never the altered primary.");
        Assert.AreEqual(GuestTrophyResultStatus.RecoveryRequired,store.RecordTrophyResult(Request()).Status);
        Assert.AreEqual(primaryBefore,File.ReadAllText(Primary));
        Assert.AreEqual(backupBefore,File.ReadAllText(backup));

        // With no valid generation left, the same semantic corruption must be fatal.
        // Both copies retain a valid envelope checksum so this exercises ladder validation.
        File.WriteAllText(backup,primaryBefore);
        Assert.Throws<InvalidDataException>(()=>store.Reload());
        Assert.AreEqual(primaryBefore,File.ReadAllText(Primary));
        Assert.AreEqual(primaryBefore,File.ReadAllText(backup));
    }
    [TestCase("HistoricalPolicySignature")] [TestCase("HistoricalMatchCount")]
    public void VersionTwoRequiresExplicitMigrationMetadata(string field)
    {
        store.OpenOrCreate(false);var payload=JObject.Parse(JsonUtility.ToJson(store.Reload()));
        ((JObject)payload["TrophyLadder"]).Remove(field);WritePayload(payload);
        Assert.Throws<InvalidDataException>(()=>store.Reload());
    }
    [Test] public void OneAccountsMigrationAndAwardsCannotChangeAnotherAccountsTrophies()
    {
        WriteLegacyHistory(5,true,out _);
        var second=new LocalGuestProfileStore(Path.Combine(directory,"separate"));var zero=second.OpenOrCreate(false);
        store.RecordTrophyResult(Request());var untouched=second.Reload();
        Assert.AreEqual(zero.ProfileId,untouched.ProfileId);Assert.AreEqual(0,untouched.Trophies);Assert.IsEmpty(untouched.TrophyLadder.Matches);
    }
    LocalGuestProfile WriteLegacyHistory(int wins,bool lose,out List<GuestTrophyResultRequest> requests)
    {
        var profile=store.OpenOrCreate(true);store.CreditFreeCurrency("existing-earth","test-only",PermanentCurrency.Earthcoins,77);
        profile=store.Reload();var legacy=GuestTrophyLadder.CreateLegacyEngine();var state=legacy.CreateState(profile.ProfileId);
        requests=new List<GuestTrophyResultRequest>();
        for(int i=0;i<wins+(lose?1:0);i++)
        {
            var request=Request(i<wins);requests.Add(request);
            var result=legacy.Record(state,Receipt(state,request),state.Revision);
            Assert.AreEqual(TrophyLadderStatus.Applied,result.Status);state=result.State;
        }
        state.Version=1;profile.TrophyLadder=state;profile.Revision+=state.Revision;
        var payload=JObject.Parse(JsonUtility.ToJson(profile));
        ((JObject)payload["TrophyLadder"]).Remove("HistoricalPolicySignature");
        ((JObject)payload["TrophyLadder"]).Remove("HistoricalMatchCount");WritePayload(payload);
        return profile;
    }
    static TrophyLadderResult Record(TrophyLadderEngine engine,TrophyLadderState state,bool won)
    {
        var result=engine.Record(state,Receipt(state,Request(won)),state.Revision);
        Assert.AreEqual(TrophyLadderStatus.Applied,result.Status,result.Reason);return result;
    }
    static TrophyMatchReceipt Receipt(TrophyLadderState state,GuestTrophyResultRequest request)=>new TrophyMatchReceipt {
        PlayerId=state.PlayerId,OpponentPlayerId=request.OpponentId,MatchId=request.MatchId,SourceModeId="online",
        EligibilityEvidenceId=GuestTrophyRules.Evidence(request),PlayerTrophiesBefore=state.CurrentTrophies,OpponentTrophiesBefore=0,
        Outcome=request.PlayerWins==3?TrophyMatchOutcome.Victory:TrophyMatchOutcome.Defeat,Eligibility=TrophyMatchEligibility.ApprovedCompetitive
    };
    static GuestTrophyResultRequest Request(bool won=true)=>new GuestTrophyResultRequest {
        MatchId=Guid.NewGuid().ToString("N"),OpponentId="nation-test-peer",SourceModeId="online",VerifiedDigest=new string('A',64),
        PlayerWins=won?3:0,OpponentWins=won?0:3
    };
    void WritePayload(JObject payload)
    {
        string json=payload.ToString(Newtonsoft.Json.Formatting.None),checksum;
        using(var sha=SHA256.Create())checksum=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","");
        File.WriteAllText(Primary,new JObject { ["Version"]=1,["Payload"]=json,["Sha256"]=checksum }.ToString());
    }
}

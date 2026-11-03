using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Progression;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class GuestTrophyTests
{
    string directory;
    LocalGuestProfileStore store;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    string Backup=>Path.Combine(directory,"guest-profile.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchGuestTrophyTests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchGuestTrophyTests"))+Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(directory).StartsWith(root,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    [TestCase(false)] [TestCase(true)]
    public void NewAndLegacyAccountsStartAtZeroWithoutConvertingCurrencyOrProgress(bool legacy)
    {
        var profile=store.OpenOrCreate(legacy);string before=File.ReadAllText(Primary);
        Assert.AreEqual(0,profile.Trophies);Assert.AreEqual(0,profile.HighestTrophies);
        Assert.IsEmpty(profile.TrophyLadder.Matches);Assert.AreEqual(profile.ProfileId,profile.TrophyLadder.PlayerId);
        Assert.AreEqual(profile.ProfileId,new LocalGuestProfileStore(directory).Reload().ProfileId);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(legacy,profile.HasLegacyAccess);
    }
    [Test] public void FullOnlineResultsPersistThresholdCurrentAndHighestAndApplyPreviousStageLoss()
    {
        store.OpenOrCreate(false);
        for(int i=0;i<5;i++)Assert.AreEqual(GuestTrophyResultStatus.Applied,store.RecordTrophyResult(Request()).Status);
        var current=new LocalGuestProfileStore(directory).Reload();Assert.AreEqual(150,current.Trophies);
        Assert.AreEqual("aquarion",GuestTrophyLadder.CreateEngine().PreviewStage(current.TrophyLadder).BoardId);
        var loss=store.RecordTrophyResult(Request(false));Assert.AreEqual(-10,loss.AppliedDelta);Assert.AreEqual(140,loss.Profile.Trophies);
        Assert.AreEqual(150,loss.Profile.HighestTrophies);
        loss=store.RecordTrophyResult(Request(false));Assert.AreEqual(-5,loss.AppliedDelta);Assert.AreEqual(135,loss.Profile.Trophies);
        Assert.AreEqual("naturalis",GuestTrophyLadder.CreateEngine().PreviewStage(loss.Profile.TrophyLadder).BoardId);
        for(int i=0;i<28;i++)store.RecordTrophyResult(Request(false));
        current=store.Reload();Assert.AreEqual(0,current.Trophies);Assert.AreEqual(150,current.HighestTrophies);
        Assert.AreEqual(0,store.RecordTrophyResult(Request(false)).AppliedDelta);
    }
    [Test] public void ReplayedOldResultReturnsItsOriginalDeltaWithoutWritingOrRevertingLaterMatches()
    {
        store.OpenOrCreate(true);var request=Request();var first=store.RecordTrophyResult(request);
        store.RecordTrophyResult(Request());string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var retry=new LocalGuestProfileStore(directory).RecordTrophyResult(request);
        Assert.AreEqual(GuestTrophyResultStatus.AlreadyApplied,retry.Status);Assert.AreEqual(60,retry.Profile.Trophies);
        Assert.AreEqual(30,retry.AppliedDelta);Assert.AreEqual(JsonUtility.ToJson(first.RecordedMatch),JsonUtility.ToJson(retry.RecordedMatch));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [TestCase("score")] [TestCase("outcome")] [TestCase("digest")] [TestCase("opponent")]
    public void SameMatchCannotBeReinterpreted(string change)
    {
        store.OpenOrCreate(false);var request=Request();store.RecordTrophyResult(request);string before=File.ReadAllText(Primary);
        if(change=="score")request.OpponentWins=2;
        if(change=="outcome"){request.PlayerWins=1;request.OpponentWins=3;}
        if(change=="digest")request.VerifiedDigest=new string('B',64);
        if(change=="opponent")request.OpponentId="another-peer";
        Assert.AreEqual(GuestTrophyResultStatus.Conflict,store.RecordTrophyResult(request).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(30,store.Reload().Trophies);
    }
    [TestCase("bot",3,0)] [TestCase("tutorial",3,0)] [TestCase("online",1,0)]
    [TestCase("online",3,3)] [TestCase("online",3,-1)] [TestCase("online",4,0)]
    public void BotsTutorialAndIncompleteOrImpossibleScoresNeverAward(string mode,int player,int opponent)
    {
        store.OpenOrCreate(false);string before=File.ReadAllText(Primary);var request=Request();
        request.SourceModeId=mode;request.PlayerWins=player;request.OpponentWins=opponent;
        Assert.AreEqual(GuestTrophyResultStatus.Ineligible,store.RecordTrophyResult(request).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("match")] [TestCase("digest")] [TestCase("self")]
    public void MissingVerificationOrIdentityCannotAward(string kind)
    {
        var profile=store.OpenOrCreate(false);string before=File.ReadAllText(Primary);var request=Request();
        if(kind=="match")request.MatchId="";if(kind=="digest")request.VerifiedDigest="unverified";
        if(kind=="self")request.OpponentId=profile.ProfileId;
        Assert.AreEqual(GuestTrophyResultStatus.InvalidRequest,store.RecordTrophyResult(request).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
    public void HistoricalProfilesMigrateAtZeroWithoutRewritingOrGrantingAnything(int version)
    {
        var original=store.OpenOrCreate(true);var payload=JObject.Parse(JsonUtility.ToJson(original));
        payload["Version"]=version;payload.Remove("TrophyLadder");WritePayload(payload);string before=File.ReadAllText(Primary);
        var restored=new LocalGuestProfileStore(directory).Reload();Assert.AreEqual(LocalGuestProfile.CurrentVersion,restored.Version);
        Assert.AreEqual(original.ProfileId,restored.ProfileId);Assert.AreEqual(original.Revision,restored.Revision);
        Assert.AreEqual(0,restored.Trophies);Assert.AreEqual(original.CollectionMode,restored.CollectionMode);
        Assert.AreEqual(JsonUtility.ToJson(original.Starter),JsonUtility.ToJson(restored.Starter));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        Assert.AreEqual(GuestTrophyResultStatus.Applied,store.RecordTrophyResult(Request()).Status);
        Assert.AreEqual(before,File.ReadAllText(Backup));Assert.AreEqual(30,store.Reload().Trophies);
    }
    [Test] public void VersionNineMigrationPreservesEarnedCurrencyAndCollectionState()
    {
        store.OpenOrCreate(false);
        store.CreditFreeCurrency("existing-credit","test-only",PermanentCurrency.Earthcoins,50);
        var original=store.Reload();var payload=JObject.Parse(JsonUtility.ToJson(original));
        payload["Version"]=9;payload.Remove("TrophyLadder");WritePayload(payload);string before=File.ReadAllText(Primary);
        var restored=store.Reload();Assert.AreEqual(0,restored.Trophies);
        Assert.AreEqual(original.FreeBalance(PermanentCurrency.Earthcoins),restored.FreeBalance(PermanentCurrency.Earthcoins));
        Assert.AreEqual(original.FreeBalance(PermanentCurrency.Saturncoins),restored.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(before,File.ReadAllText(Primary));
        store.RecordTrophyResult(Request());Assert.AreEqual(original.WalletEvents.Count,store.Reload().WalletEvents.Count);
    }
    [Test] public void OldSchemaCannotSmuggleANewTrophyStateEvenWhenItIsEmpty()
    {
        var profile=store.OpenOrCreate(true);var payload=JObject.Parse(JsonUtility.ToJson(profile));payload["Version"]=9;
        WritePayload(payload);string before=File.ReadAllText(Primary);
        Assert.Throws<InvalidDataException>(()=>store.Reload());Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("TrophyLadder")] [TestCase("Matches")] [TestCase("CurrentTrophies")]
    [TestCase("HighestTrophies")] [TestCase("Revision")] [TestCase("PolicySignature")]
    public void CurrentSchemaRequiresEveryTrophyFieldInsteadOfAssumingZero(string field)
    {
        store.OpenOrCreate(true);var payload=JObject.Parse(JsonUtility.ToJson(store.Reload()));
        if(field=="TrophyLadder")payload.Remove(field);else ((JObject)payload["TrophyLadder"]).Remove(field);
        WritePayload(payload);Assert.Throws<InvalidDataException>(()=>store.Reload());
    }
    [TestCase("PlayerTrophiesBefore")] [TestCase("Outcome")] [TestCase("AppliedDelta")]
    public void MissingReceiptValuesCannotDeserializeAsValidZeroes(string field)
    {
        store.OpenOrCreate(true);store.RecordTrophyResult(Request());
        var payload=JObject.Parse(JsonUtility.ToJson(store.Reload()));var match=(JObject)payload["TrophyLadder"]["Matches"][0];
        if(field=="AppliedDelta")match.Remove(field);else ((JObject)match["Receipt"]).Remove(field);
        WritePayload(payload);Assert.AreEqual(GuestTrophyResultStatus.RecoveryRequired,store.RecordTrophyResult(Request()).Status);
        Assert.IsTrue(store.NeedsRecovery);
    }
    [Test] public void RecoveryNeverAwardsAgainstAnOlderBalance()
    {
        store.OpenOrCreate(true);var request=Request();store.RecordTrophyResult(request);File.WriteAllText(Primary,"damaged");
        Assert.AreEqual(GuestTrophyResultStatus.RecoveryRequired,store.RecordTrophyResult(request).Status);
        Assert.AreEqual("damaged",File.ReadAllText(Primary));
    }
    [TestCase("CurrentTrophies","18446744073709551616")]
    [TestCase("HighestTrophies","9223372036854775808")]
    [TestCase("Revision","-9223372036854775809")]
    [TestCase("Version","4294967297")]
    public void OversizedStateIntegersAreRejectedBeforeUnityDeserialization(string field,string value)
    {
        store.OpenOrCreate(true);var payload=JObject.Parse(JsonUtility.ToJson(store.Reload()));
        payload["TrophyLadder"][field]=JToken.Parse(value);WritePayload(payload);string before=File.ReadAllText(Primary);
        Assert.Throws<InvalidDataException>(()=>store.Reload());Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("Outcome","4294967297")]
    [TestCase("DifferenceBandIndex","4294967296")]
    public void OversizedReceiptIntegersRequireRecoveryWithoutAwarding(string field,string value)
    {
        store.OpenOrCreate(true);store.RecordTrophyResult(Request());
        var payload=JObject.Parse(JsonUtility.ToJson(store.Reload()));var match=(JObject)payload["TrophyLadder"]["Matches"][0];
        if(field=="DifferenceBandIndex")match[field]=JToken.Parse(value);else match["Receipt"][field]=JToken.Parse(value);
        WritePayload(payload);string before=File.ReadAllText(Primary);
        Assert.AreEqual(GuestTrophyResultStatus.RecoveryRequired,store.RecordTrophyResult(Request()).Status);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test,Platform("Win")] public void FailedAtomicReplaceLeavesHistoryUntouchedAndRetryCanCommit()
    {
        store.OpenOrCreate(true);var request=Request();string before=File.ReadAllText(Primary);
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>store.RecordTrophyResult(request));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(0,store.Reload().Trophies);
        Assert.AreEqual(GuestTrophyResultStatus.Applied,store.RecordTrophyResult(request).Status);
    }
    [Test] public void ActiveConfigurationIsDetachedFromEditorTrialAndCatalogueRolesMatchApprovedBoards()
    {
        var policy=GuestTrophyLadder.CreatePolicy();Assert.AreEqual(GuestTrophyLadder.PolicyId,policy.PolicyId);
        Assert.AreNotEqual(TrophyLadderTrialConfiguration.CreatePolicy().PolicyId,policy.PolicyId);
        policy.Stages[1].MinimumTrophies=999;Assert.AreEqual(150,GuestTrophyLadder.CreatePolicy().Stages[1].MinimumTrophies);
        var catalogue=GuestTrophyLadder.CreateCatalogue();
        Assert.AreEqual(TrophyBoardRole.PaidCosmetic,catalogue.Boards.Single(b=>b.BoardId=="lounge").Role);
        Assert.AreEqual(TrophyBoardRole.OwnedCosmetic,catalogue.Boards.Single(b=>b.BoardId=="classic").Role);
    }
    static GuestTrophyResultRequest Request(bool win=true)=>new GuestTrophyResultRequest {
        MatchId=Guid.NewGuid().ToString("N"),OpponentId="online-peer:test:red",SourceModeId="online",
        PlayerWins=win?3:0,OpponentWins=win?0:3,VerifiedDigest=new string('A',64)
    };
    void WritePayload(JObject payload)
    {
        string json=payload.ToString(Newtonsoft.Json.Formatting.None),checksum;
        using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","");
        File.WriteAllText(Primary,new JObject { ["Version"]=1,["Payload"]=json,["Sha256"]=checksum }.ToString());
    }
}

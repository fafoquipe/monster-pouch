using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Progression;
using Newtonsoft.Json.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfileStore
    {
        public GuestTrophyResult RecordTrophyResult(GuestTrophyResultRequest request)
        {
            if(request==null||!GuestTrophyRules.ValidMatchId(request.MatchId)||
                !GuestTrophyRules.ValidOpponent(request.OpponentId)||!GuestTrophyRules.ValidDigest(request.VerifiedDigest))
                return TrophyResult(GuestTrophyResultStatus.InvalidRequest);
            if(request.SourceModeId!=GuestTrophyRules.OnlineMode||!GuestTrophyRules.CompleteScore(request.PlayerWins,request.OpponentWins))
                return TrophyResult(GuestTrophyResultStatus.Ineligible);
            using(Lock())
            {
                var profile=LoadExisting();
                if(NeedsRecovery)return TrophyResult(GuestTrophyResultStatus.RecoveryRequired,profile);
                if(profile==null)return TrophyResult(GuestTrophyResultStatus.MissingProfile);
                profile.Validate();
                if(request.OpponentId==profile.ProfileId)return TrophyResult(GuestTrophyResultStatus.InvalidRequest,profile);
                var prior=profile.TrophyLadder.Matches.FirstOrDefault(m=>m.Receipt.MatchId==request.MatchId);
                var receipt=new TrophyMatchReceipt {
                    PlayerId=profile.ProfileId,OpponentPlayerId=request.OpponentId,MatchId=request.MatchId,
                    SourceModeId=request.SourceModeId,EligibilityEvidenceId=GuestTrophyRules.Evidence(request),
                    PlayerTrophiesBefore=prior?.Receipt.PlayerTrophiesBefore??profile.Trophies,
                    // Opponent score is not used by the approved flat policy; never infer one from XP.
                    OpponentTrophiesBefore=0,
                    Outcome=request.PlayerWins==3?TrophyMatchOutcome.Victory:TrophyMatchOutcome.Defeat,
                    Eligibility=TrophyMatchEligibility.ApprovedCompetitive
                };
                var result=GuestTrophyLadder.CreateEngine().Record(profile.TrophyLadder,receipt,profile.TrophyLadder.Revision);
                if(result.Status==TrophyLadderStatus.AlreadyApplied)
                    return TrophyResult(GuestTrophyResultStatus.AlreadyApplied,profile,result.RecordedMatch);
                if(result.Status!=TrophyLadderStatus.Applied)
                    return TrophyResult(result.Status==TrophyLadderStatus.Conflict?GuestTrophyResultStatus.Conflict:
                        GuestTrophyResultStatus.InvalidRequest,profile,reason:result.Reason);
                profile.TrophyLadder=result.State;
                profile.Revision=checked(profile.Revision+1);
                Save(profile);
                return TrophyResult(GuestTrophyResultStatus.Applied,profile,result.RecordedMatch);
            }
        }
        static GuestTrophyResult TrophyResult(GuestTrophyResultStatus status,LocalGuestProfile profile=null,
            TrophyRecordedMatch match=null,string reason=null)=>new GuestTrophyResult {
                Status=status,Profile=profile,AppliedDelta=match?.AppliedDelta??0,RecordedMatch=match,Reason=reason
            };

        static void ValidateTrophyPayload(JObject root,int version)
        {
            if(version<10)
            {
                if(root.TryGetValue("TrophyLadder",StringComparison.Ordinal,out var old)&&old.Type!=JTokenType.Null)
                    throw new InvalidDataException("An older guest cannot contain trophy state.");
                return;
            }
            var state=RequireTrophyObject(root,"TrophyLadder");
            RequireTrophyFields(state,JTokenType.Integer,"Version","Revision","CurrentTrophies","HighestTrophies");
            RequireTrophyFields(state,JTokenType.String,"PlayerId","PolicySignature");
            RequireArray(state,"Matches");
            if((long)state["Version"]>=3) {
                RequireArray(state,"HistoricalSegments");
                foreach(var token in (JArray)state["HistoricalSegments"]) {
                    if(!(token is JObject segment))throw new InvalidDataException("Invalid historical trophy segment.");
                    RequireTrophyFields(segment,JTokenType.String,"PolicySignature");
                    RequireTrophyFields(segment,JTokenType.Integer,"EndMatchIndex");
                }
            } else if(state["HistoricalSegments"] is JArray segments&&segments.Count!=0)
                throw new InvalidDataException("An older trophy state cannot contain segmented policy history.");
            if((long)state["Version"]>=2)
            {
                RequireTrophyFields(state,JTokenType.String,"HistoricalPolicySignature");
                RequireTrophyFields(state,JTokenType.Integer,"HistoricalMatchCount");
            }
            else if(state.TryGetValue("HistoricalMatchCount",out var historicalCount)&&historicalCount.Type!=JTokenType.Null&&
                    (historicalCount.Type!=JTokenType.Integer||historicalCount.ToString(Newtonsoft.Json.Formatting.None)!="0")||
                    state.TryGetValue("HistoricalPolicySignature",out var historicalSignature)&&historicalSignature.Type!=JTokenType.Null&&
                    (historicalSignature.Type!=JTokenType.String||historicalSignature.ToString().Length!=0))
                throw new InvalidDataException("A version-one trophy state cannot contain a migrated policy prefix.");
            foreach(var token in (JArray)state["Matches"])
            {
                if(!(token is JObject match))throw new InvalidDataException("Invalid trophy match object.");
                RequireTrophyFields(match,JTokenType.Integer,"DifferenceBandIndex","AppliedDelta","TrophiesAfter");
                RequireTrophyFields(match,JTokenType.String,"StageId");
                var receipt=RequireTrophyObject(match,"Receipt");
                RequireTrophyFields(receipt,JTokenType.String,"PlayerId","OpponentPlayerId","MatchId","EligibilityEvidenceId","SourceModeId");
                RequireTrophyFields(receipt,JTokenType.Integer,"PlayerTrophiesBefore","OpponentTrophiesBefore","Outcome","Eligibility");
            }
        }
        static JObject RequireTrophyObject(JObject parent,string name)
        {
            if(!parent.TryGetValue(name,StringComparison.Ordinal,out var token)||!(token is JObject value))
                throw new InvalidDataException("Missing trophy object: "+name);
            return value;
        }
        static void RequireTrophyFields(JObject parent,JTokenType kind,params string[] names)
        {
            foreach(string name in names)
            {
                if(!parent.TryGetValue(name,StringComparison.Ordinal,out var token)||token.Type!=kind)
                    throw new InvalidDataException("Missing or invalid trophy field: "+name);
                // Newtonsoft retains integers beyond Int64 as BigInteger. Check
                // the destination width before Unity can deserialize them.
                if(kind==JTokenType.Integer)
                {
                    bool int32=name=="Version"||name=="DifferenceBandIndex"||name=="Outcome"||name=="Eligibility";
                    if(!long.TryParse(token.ToString(Newtonsoft.Json.Formatting.None),NumberStyles.Integer,
                        CultureInfo.InvariantCulture,out long value)||int32&&(value<int.MinValue||value>int.MaxValue))
                        throw new InvalidDataException("Trophy integer is outside its supported range: "+name);
                }
            }
        }
    }
}

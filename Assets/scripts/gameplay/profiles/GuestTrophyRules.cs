using System;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Progression;

namespace MonsterPouch.Gameplay.Profiles
{
    public enum GuestTrophyResultStatus { Applied, AlreadyApplied, Conflict, InvalidRequest, Ineligible, RecoveryRequired, MissingProfile }
    public sealed class GuestTrophyResultRequest
    {
        public string MatchId,OpponentId,SourceModeId,VerifiedDigest;
        public int PlayerWins,OpponentWins;
    }
    public sealed class GuestTrophyResult
    {
        public GuestTrophyResultStatus Status;
        public LocalGuestProfile Profile;
        public long AppliedDelta;
        public TrophyRecordedMatch RecordedMatch;
        public string Reason;
    }
    /// <summary>Approved local score bookkeeping. A matching peer digest is not server authority.</summary>
    public static class GuestTrophyLadder
    {
        public const string PolicyId="local-online.ladder.2026-10-25";
        public static TrophyLadderPolicy CreatePolicy()=>new TrophyLadderPolicy {
            PolicyId=PolicyId,Version=3,InitialTrophies=0,MinimumTrophies=0,MinimumRule=TrophyMinimumRule.ClampToMinimum,
            DifferenceBands=new[] {new TrophyDifferenceBand {WinGain=30,LossAmount=5}},
            Stages=new[] {
                new TrophyLadderStage {StageId="naturalis",BoardId="naturalis",MinimumTrophies=0,DefeatLossOverride=5},
                new TrophyLadderStage {StageId="aquarion",BoardId="aquarion",MinimumTrophies=300,DefeatLossOverride=10},
                new TrophyLadderStage {StageId="geothar",BoardId="geothar",MinimumTrophies=600,DefeatLossOverride=15},
                new TrophyLadderStage {StageId="volkanar",BoardId="volkanar",MinimumTrophies=900,DefeatLossOverride=25},
                new TrophyLadderStage {StageId="aireola",BoardId="aireola",MinimumTrophies=1200,DefeatLossOverride=30},
                new TrophyLadderStage {StageId="royal-court",BoardId="royal",MinimumTrophies=1500,DefeatLossOverride=30}
            }
        };
        public static TrophyBoardCatalogue CreateCatalogue() {
            var catalogue=CreatePreviousCatalogue();catalogue.Version=3;
            catalogue.Boards=catalogue.Boards.Concat(new[]{new TrophyBoardDefinition {BoardId="aireola",Role=TrophyBoardRole.Progression}}).ToArray();
            return catalogue;
        }
        internal static TrophyLadderPolicy CreatePreviousPolicy()=>new TrophyLadderPolicy {
            PolicyId=PolicyId,Version=2,InitialTrophies=0,MinimumTrophies=0,MinimumRule=TrophyMinimumRule.ClampToMinimum,
            DifferenceBands=new[] {new TrophyDifferenceBand {WinGain=30,LossAmount=5}},
            Stages=new[] {
                new TrophyLadderStage {StageId="naturalis",BoardId="naturalis",MinimumTrophies=0,DefeatLossOverride=5},
                new TrophyLadderStage {StageId="aquarion",BoardId="aquarion",MinimumTrophies=150,DefeatLossOverride=10},
                new TrophyLadderStage {StageId="geothar",BoardId="geothar",MinimumTrophies=300,DefeatLossOverride=15},
                new TrophyLadderStage {StageId="volkanar",BoardId="volkanar",MinimumTrophies=450,DefeatLossOverride=25},
                new TrophyLadderStage {StageId="royal-court",BoardId="royal",MinimumTrophies=600,DefeatLossOverride=30}
            }
        };
        internal static TrophyBoardCatalogue CreatePreviousCatalogue()=>new TrophyBoardCatalogue {
            CatalogueId="monster-pouch.boards.2026-10-22",Version=2,
            Boards=new[] {
                new TrophyBoardDefinition {BoardId="naturalis",Role=TrophyBoardRole.InitialProgression},
                new TrophyBoardDefinition {BoardId="aquarion",Role=TrophyBoardRole.Progression},
                new TrophyBoardDefinition {BoardId="geothar",Role=TrophyBoardRole.Progression},
                new TrophyBoardDefinition {BoardId="volkanar",Role=TrophyBoardRole.Progression},
                new TrophyBoardDefinition {BoardId="royal",Role=TrophyBoardRole.Progression},
                new TrophyBoardDefinition {BoardId="classic",Role=TrophyBoardRole.OwnedCosmetic},
                new TrophyBoardDefinition {BoardId="lounge",Role=TrophyBoardRole.PaidCosmetic}
            }
        };
        // Frozen R0 definitions reproduce already committed receipts, including the old -30 at 150.
        internal static TrophyLadderPolicy CreateLegacyPolicy()=>new TrophyLadderPolicy {
            PolicyId=PolicyId,Version=1,InitialTrophies=0,MinimumTrophies=0,MinimumRule=TrophyMinimumRule.ClampToMinimum,
            DifferenceBands=new[] {new TrophyDifferenceBand {WinGain=30,LossAmount=5}},
            Stages=new[] {
                new TrophyLadderStage {StageId="ball-pit",BoardId="classic",MinimumTrophies=0,DefeatLossOverride=5},
                new TrophyLadderStage {StageId="royal-court",BoardId="royal",MinimumTrophies=150,DefeatLossOverride=30}
            }
        };
        internal static TrophyBoardCatalogue CreateLegacyCatalogue()=>new TrophyBoardCatalogue {
            CatalogueId="monster-pouch.boards.2026-10-22",Version=1,
            Boards=new[] {
                new TrophyBoardDefinition {BoardId="classic",Role=TrophyBoardRole.InitialProgression},
                new TrophyBoardDefinition {BoardId="royal",Role=TrophyBoardRole.Progression},
                new TrophyBoardDefinition {BoardId="lounge",Role=TrophyBoardRole.PaidCosmetic}
            }
        };
        public static TrophyLadderEngine CreateEngine()
        {
            if(!TrophyLadderEngine.TryCreate(CreatePolicy(),CreateCatalogue(),out var engine,out var error,CreatePreviousEngine()))
                throw new InvalidOperationException(error);
            return engine;
        }
        public static string[] ReachedBoardIds(TrophyLadderState state)
        {
            var preview=CreateEngine().PreviewStage(state);
            if(preview.Status!=TrophyLadderStatus.Ready)return Array.Empty<string>();
            var ids=preview.ReachedStages.Select(s=>s.BoardId).Distinct(StringComparer.Ordinal).ToList();
            // Before this road, Aireola was a cosmetic sharing royal's 600-cup
            // gate. Keep that already-earned access when migrating old histories.
            long historicalEnd=state.HistoricalSegments.LastOrDefault()?.EndMatchIndex??0;
            if(historicalEnd>0&&state.Matches.Take((int)historicalEnd).Any(m=>m.TrophiesAfter>=600)&&!ids.Contains("aireola"))ids.Add("aireola");
            return ids.ToArray();
        }
        internal static TrophyLadderEngine CreatePreviousEngine()
        {
            if(!TrophyLadderEngine.TryCreate(CreatePreviousPolicy(),CreatePreviousCatalogue(),out var engine,out var error,CreateLegacyEngine()))
                throw new InvalidOperationException(error);
            return engine;
        }
        internal static TrophyLadderEngine CreateLegacyEngine()
        {
            if(!TrophyLadderEngine.TryCreate(CreateLegacyPolicy(),CreateLegacyCatalogue(),out var engine,out var error))
                throw new InvalidOperationException(error);
            return engine;
        }
    }
    internal static class GuestTrophyRules
    {
        internal const string OnlineMode="online";
        internal static bool ValidMatchId(string id)=>Guid.TryParseExact(id,"N",out _);
        internal static bool ValidOpponent(string id)=>!string.IsNullOrWhiteSpace(id)&&id.Length<=160&&
            id==id.Trim()&&!id.Any(char.IsControl);
        internal static bool ValidDigest(string digest)=>digest!=null&&digest.Length==64&&
            digest.All(c=>c>='0'&&c<='9'||c>='A'&&c<='F');
        internal static bool CompleteScore(int player,int opponent)=>(player==3&&opponent>=0&&opponent<3)||
            (opponent==3&&player>=0&&player<3);
        internal static string Evidence(GuestTrophyResultRequest request)=>"local-online:"+request.MatchId+":"+
            request.PlayerWins.ToString(CultureInfo.InvariantCulture)+":"+
            request.OpponentWins.ToString(CultureInfo.InvariantCulture)+":"+request.VerifiedDigest;
        internal static bool ValidEvidence(string evidence,string matchId,TrophyMatchOutcome outcome)
        {
            if(evidence==null)return false;
            var parts=evidence.Split(':');
            if(parts.Length!=5||parts[0]!="local-online"||parts[1]!=matchId||
                parts[2].Length!=1||parts[3].Length!=1||!ValidDigest(parts[4]))return false;
            int player=parts[2][0]-'0',opponent=parts[3][0]-'0';
            return CompleteScore(player,opponent)&&outcome==(player==3?TrophyMatchOutcome.Victory:TrophyMatchOutcome.Defeat);
        }
    }
}

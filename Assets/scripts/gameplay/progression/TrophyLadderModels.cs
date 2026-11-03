using System;

namespace MonsterPouch.Gameplay.Progression
{
    public enum TrophyBoardRole { Unspecified = 0, InitialProgression = 1, Progression = 2, PaidCosmetic = 3, OwnedCosmetic = 4 }
    [Serializable] public sealed class TrophyBoardDefinition
    {
        public string BoardId;
        public TrophyBoardRole Role;
    }
    [Serializable] public sealed class TrophyBoardCatalogue
    {
        public string CatalogueId;
        public int Version;
        public TrophyBoardDefinition[] Boards;
    }
    [Serializable] public sealed class TrophyDifferenceBand
    {
        // Bands are contiguous. null is allowed only on the last, unbounded band.
        public long? UpperOpponentDifferenceExclusive;
        public long WinGain;
        public long LossAmount;
    }
    [Serializable] public sealed class TrophyLadderStage
    {
        public string StageId;
        public long MinimumTrophies;
        public string BoardId;
        // Optional explicit stage loss; otherwise the configured difference band applies.
        public long? DefeatLossOverride;
    }
    public enum TrophyMinimumRule { Unspecified, ClampToMinimum }
    [Serializable] public sealed class TrophyLadderPolicy
    {
        public string PolicyId;
        public int Version;
        // Nullable inputs distinguish an explicit zero from absent configuration.
        public long? InitialTrophies;
        public long? MinimumTrophies;
        public TrophyMinimumRule MinimumRule;
        public TrophyDifferenceBand[] DifferenceBands;
        public TrophyLadderStage[] Stages;
    }
    public enum TrophyMatchOutcome { Unspecified, Victory, Defeat, Draw, Abandoned }
    public enum TrophyMatchEligibility { Unverified, Rejected, ApprovedCompetitive }
    public enum TrophyLadderStatus
    {
        Ready, Applied, AlreadyApplied, Conflict, NotEligible, NeedsConfiguration,
        InvalidRequest, InvalidState, UnsupportedVersion, ArithmeticOverflow
    }
    /// <summary>
    /// All identities, pre-match scores and eligibility are supplied by a trusted
    /// adjudicator. A local flag or evidence ID is not proof of server authority.
    /// </summary>
    [Serializable] public sealed class TrophyMatchReceipt
    {
        public string PlayerId;
        public string OpponentPlayerId;
        public string MatchId;
        public string EligibilityEvidenceId;
        public string SourceModeId;
        public long PlayerTrophiesBefore;
        public long OpponentTrophiesBefore;
        public TrophyMatchOutcome Outcome;
        public TrophyMatchEligibility Eligibility;
    }
    [Serializable] public sealed class TrophyRecordedMatch
    {
        public TrophyMatchReceipt Receipt;
        public int DifferenceBandIndex;
        public string StageId;
        public long AppliedDelta;
        public long TrophiesAfter;
    }
    /// <summary>A pure state DTO, not an active save format or an economic authority.</summary>
    [Serializable] public sealed class TrophyLadderState
    {
        public int Version;
        public string PlayerId;
        public string PolicySignature;
        // An explicitly migrated prefix retains its original accounting policy and every receipt.
        public string HistoricalPolicySignature = string.Empty;
        public long HistoricalMatchCount;
        public TrophyPolicySegment[] HistoricalSegments = Array.Empty<TrophyPolicySegment>();
        public long Revision;
        public long CurrentTrophies;
        public long HighestTrophies;
        public TrophyRecordedMatch[] Matches;
    }
    [Serializable] public sealed class TrophyPolicySegment
    {
        public string PolicySignature;
        public long EndMatchIndex;
    }
    public sealed class TrophyLadderResult
    {
        public TrophyLadderStatus Status;
        public string Reason;
        public TrophyLadderState State;
        public TrophyRecordedMatch RecordedMatch;
    }
    public sealed class TrophyStagePreview
    {
        public TrophyLadderStatus Status;
        public string Reason;
        public long CurrentTrophies;
        public long HighestTrophies;
        public string StageId;
        public string BoardId;
        public long StageMinimumTrophies;
        public long? NextStageAt;
        // Historical milestones only: this list is not equipment or an ownership grant.
        public TrophyLadderStage[] ReachedStages = Array.Empty<TrophyLadderStage>();
    }
}

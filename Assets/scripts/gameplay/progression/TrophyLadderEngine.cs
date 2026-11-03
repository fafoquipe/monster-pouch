using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MonsterPouch.Gameplay.Progression
{
    /// <summary>
    /// Pure account ladder accounting. No mode selection, match adjudication,
    /// board equipment/unlocks, IO, rewards or global ranking are performed here.
    /// </summary>
    public sealed class TrophyLadderEngine
    {
        const int StateVersion = 3;
        readonly TrophyLadderPolicy policy;
        readonly string signature;
        readonly TrophyLadderEngine predecessor;

        TrophyLadderEngine(TrophyLadderPolicy source, TrophyBoardCatalogue catalogue, TrophyLadderEngine approvedPredecessor)
        {
            policy = CopyPolicy(source);
            signature = Signature(policy, catalogue);
            predecessor = approvedPredecessor;
        }

        public static bool TryCreate(TrophyLadderPolicy policy, TrophyBoardCatalogue catalogue,
            out TrophyLadderEngine engine, out string reason, TrophyLadderEngine approvedPredecessor = null)
        {
            engine = null;
            reason = "An explicit versioned policy, initial score, minimum and board catalogue are required.";
            if (policy == null || !ValidId(policy.PolicyId) || policy.Version < 1 ||
                !policy.InitialTrophies.HasValue || !policy.MinimumTrophies.HasValue ||
                policy.InitialTrophies.Value < policy.MinimumTrophies.Value ||
                !Enum.IsDefined(typeof(TrophyMinimumRule), policy.MinimumRule) ||
                catalogue == null || !ValidId(catalogue.CatalogueId) || catalogue.Version < 1 || catalogue.Boards == null)
                return false;
            var boards = new Dictionary<string, TrophyBoardRole>(StringComparer.Ordinal);
            foreach (var board in catalogue.Boards)
            {
                if (board == null || !ValidId(board.BoardId) || boards.ContainsKey(board.BoardId) ||
                    !Enum.IsDefined(typeof(TrophyBoardRole), board.Role))
                { reason = "Board identities and roles must be explicit and unique."; return false; }
                boards.Add(board.BoardId, board.Role);
            }
            if (boards.Values.Count(role => role == TrophyBoardRole.InitialProgression) != 1)
            { reason = "Exactly one initial progression board must be identified."; return false; }
            if (policy.DifferenceBands == null || policy.DifferenceBands.Length == 0)
            { reason = "At least one explicit adjustment band is required."; return false; }
            for (int i = 0; i < policy.DifferenceBands.Length; i++)
            {
                var band = policy.DifferenceBands[i];
                if (band == null || band.WinGain <= 0 || band.LossAmount <= 0 ||
                    (i == policy.DifferenceBands.Length - 1) != !band.UpperOpponentDifferenceExclusive.HasValue)
                { reason = "Positive gains/losses and one final unbounded band are required."; return false; }
                if (i == 0) continue;
                var previous = policy.DifferenceBands[i - 1];
                if ((band.UpperOpponentDifferenceExclusive.HasValue &&
                     band.UpperOpponentDifferenceExclusive.Value <= previous.UpperOpponentDifferenceExclusive.Value) ||
                    band.WinGain <= previous.WinGain || band.LossAmount >= previous.LossAmount)
                { reason = "Higher opponent-difference bands must grant more on victory and deduct less on defeat."; return false; }
            }
            if (policy.Stages == null || policy.Stages.Length == 0)
            { reason = "Explicit progression ranges are required."; return false; }
            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < policy.Stages.Length; i++)
            {
                var stage = policy.Stages[i];
                if (stage == null || !ValidId(stage.StageId) || !stageIds.Add(stage.StageId) ||
                    (stage.DefeatLossOverride.HasValue && stage.DefeatLossOverride.Value <= 0) ||
                    !ValidId(stage.BoardId) || !boards.TryGetValue(stage.BoardId, out var role) ||
                    role == TrophyBoardRole.PaidCosmetic || role == TrophyBoardRole.OwnedCosmetic || role == TrophyBoardRole.Unspecified ||
                    (i == 0 && (stage.MinimumTrophies != policy.MinimumTrophies.Value || role != TrophyBoardRole.InitialProgression)) ||
                    (i > 0 && stage.MinimumTrophies <= policy.Stages[i - 1].MinimumTrophies))
                { reason = "Stages must cover the score domain in ascending ranges using eligible boards, beginning with the initial board."; return false; }
            }
            if (policy.Stages.Length > 1 && policy.InitialTrophies.Value >= policy.Stages[1].MinimumTrophies)
            { reason = "The explicit starting score must resolve to the initial progression stage."; return false; }
            engine = new TrophyLadderEngine(policy, catalogue, approvedPredecessor);
            reason = "";
            return true;
        }

        public TrophyLadderState CreateState(string playerId)
        {
            if (!ValidId(playerId)) throw new ArgumentException("A stable player identity is required.", nameof(playerId));
            return new TrophyLadderState { Version = StateVersion, PlayerId = playerId, PolicySignature = signature,
                CurrentTrophies = policy.InitialTrophies.Value, HighestTrophies = policy.InitialTrophies.Value,
                Revision = 0, Matches = Array.Empty<TrophyRecordedMatch>() };
        }

        // Explicit, in-memory migration from one configured predecessor. Historical adjustments
        // are validated before the signature changes, and remain a replayable immutable prefix.
        public TrophyLadderResult UpgradeState(TrophyLadderState state)
        {
            if (state == null) return Result(TrophyLadderStatus.InvalidState, "Missing ladder state.", null);
            if (state.PolicySignature == signature)
            {
                var status = Validate(state, out var reason);
                if (status != TrophyLadderStatus.Ready) return Result(status, reason, null);
                var current = CanonicalState(state);
                return Result(TrophyLadderStatus.Ready, "", current);
            }
            if (predecessor == null)
                return Result(TrophyLadderStatus.Conflict, "No approved predecessor matches this policy signature.", null);
            if(state.PolicySignature!=predecessor.signature) {
                var intermediate=predecessor.UpgradeState(state);
                if(intermediate.Status!=TrophyLadderStatus.Ready)return intermediate;
                state=intermediate.State;
            }
            var previousStatus = predecessor.Validate(state, out var previousReason);
            if (previousStatus != TrophyLadderStatus.Ready) return Result(previousStatus, previousReason, null);
            if (predecessor.policy.InitialTrophies != policy.InitialTrophies || predecessor.policy.MinimumTrophies != policy.MinimumTrophies)
                return Result(TrophyLadderStatus.Conflict, "This migration requires the same initial/minimum score.", null);
            var next = CanonicalState(state);
            next.Version = StateVersion; next.PolicySignature = signature;
            long previousEnd=next.HistoricalSegments.LastOrDefault()?.EndMatchIndex??0;
            if(state.Matches.LongLength>previousEnd)
                next.HistoricalSegments=next.HistoricalSegments.Concat(new[]{new TrophyPolicySegment {
                    PolicySignature=predecessor.signature,EndMatchIndex=state.Matches.LongLength}}).ToArray();
            var validation = Validate(next, out var error);
            return validation == TrophyLadderStatus.Ready ? Result(validation, "", next) : Result(validation, error, null);
        }

        public TrophyLadderResult Record(TrophyLadderState state, TrophyMatchReceipt receipt, long expectedRevision)
        {
            var valid = Validate(state, out var reason);
            if (valid != TrophyLadderStatus.Ready) return Result(valid, reason, null);
            if (!ValidReceiptIdentity(receipt) || expectedRevision < 0)
                return Result(TrophyLadderStatus.InvalidRequest, "A complete adjudicated match receipt is required.", state);
            if (!StringComparer.Ordinal.Equals(state.PlayerId, receipt.PlayerId))
                return Result(TrophyLadderStatus.Conflict, "The receipt belongs to another player.", state);
            // Check the full historical payload before current revision, score or eligibility.
            // Retries cannot change opponent scores, invert outcomes or erase later matches.
            var prior = state.Matches.FirstOrDefault(item => item.Receipt.MatchId == receipt.MatchId);
            if (prior != null)
                return SameReceipt(prior.Receipt, receipt)
                    ? Result(TrophyLadderStatus.AlreadyApplied, "This match was already applied.", state, prior)
                    : Result(TrophyLadderStatus.Conflict, "This match ID carries a different adjudication.", state);
            if (state.Revision != expectedRevision || state.CurrentTrophies != receipt.PlayerTrophiesBefore)
                return Result(TrophyLadderStatus.Conflict, "The revision or adjudicated pre-match score differs from current state.", state);
            if (state.Matches.Any(item => item.Receipt.EligibilityEvidenceId == receipt.EligibilityEvidenceId))
                return Result(TrophyLadderStatus.Conflict, "This adjudication evidence belongs to another recorded match.", state);
            var status = Calculate(receipt, out var recorded, out reason);
            if (status != TrophyLadderStatus.Ready) return Result(status, reason, state);
            var next = CanonicalState(state);
            next.Version = StateVersion;
            next.Matches = next.Matches.Concat(new[] { recorded }).ToArray();
            next.Revision = checked(next.Revision + 1);
            next.CurrentTrophies = recorded.TrophiesAfter;
            next.HighestTrophies = Math.Max(state.HighestTrophies, next.CurrentTrophies);
            return Result(TrophyLadderStatus.Applied, "", next, recorded);
        }

        public TrophyStagePreview PreviewStage(TrophyLadderState state)
        {
            var status = Validate(state, out var reason);
            if (status != TrophyLadderStatus.Ready) return new TrophyStagePreview { Status = status, Reason = reason };
            int index = StageIndex(state.CurrentTrophies);
            var stage = policy.Stages[index];
            return new TrophyStagePreview { Status = TrophyLadderStatus.Ready, Reason = "", CurrentTrophies = state.CurrentTrophies,
                HighestTrophies = state.HighestTrophies, StageId = stage.StageId, BoardId = stage.BoardId,
                StageMinimumTrophies = stage.MinimumTrophies,
                NextStageAt = index + 1 < policy.Stages.Length ? (long?)policy.Stages[index + 1].MinimumTrophies : null,
                ReachedStages = ReachedStages(state).Select(s =>
                    new TrophyLadderStage { StageId = s.StageId, MinimumTrophies = s.MinimumTrophies, BoardId = s.BoardId,
                        DefeatLossOverride = s.DefeatLossOverride }).ToArray() };
        }
        IEnumerable<TrophyLadderStage> ReachedStages(TrophyLadderState state)
        {
            var stages=policy.Stages.Where(s=>s.MinimumTrophies<=state.HighestTrophies).ToList();
            var canonical=CanonicalState(state);
            foreach(var segment in canonical.HistoricalSegments) {
                var previous=FindPredecessor(segment.PolicySignature);
                long high=state.Matches.Take((int)segment.EndMatchIndex).Select(m=>m.TrophiesAfter).DefaultIfEmpty(policy.InitialTrophies.Value).Max();
                foreach(var stage in previous.policy.Stages.Where(s=>s.MinimumTrophies<=high))
                    if(!stages.Any(s=>s.BoardId==stage.BoardId))stages.Add(stage);
            }
            return stages;
        }

        TrophyLadderStatus Calculate(TrophyMatchReceipt receipt, out TrophyRecordedMatch recorded, out string reason)
        {
            recorded = null; reason = "";
            if (receipt.OpponentTrophiesBefore < policy.MinimumTrophies.Value)
            { reason = "The adjudicated opponent score is below the configured minimum."; return TrophyLadderStatus.InvalidRequest; }
            if (receipt.Eligibility != TrophyMatchEligibility.ApprovedCompetitive)
            { reason = "Competitive eligibility has not been approved by an adjudicator."; return TrophyLadderStatus.NotEligible; }
            if (receipt.Outcome != TrophyMatchOutcome.Victory && receipt.Outcome != TrophyMatchOutcome.Defeat)
            { reason = "Only victory and defeat are configured; other outcomes require an approved rule."; return TrophyLadderStatus.NeedsConfiguration; }
            // Decimal represents the entire difference of two long scores exactly.
            decimal difference = (decimal)receipt.OpponentTrophiesBefore - receipt.PlayerTrophiesBefore;
            int index = 0;
            while (policy.DifferenceBands[index].UpperOpponentDifferenceExclusive.HasValue &&
                difference >= policy.DifferenceBands[index].UpperOpponentDifferenceExclusive.Value) index++;
            var band = policy.DifferenceBands[index];
            var stage = policy.Stages[StageIndex(receipt.PlayerTrophiesBefore)];
            long loss = stage.DefeatLossOverride ?? band.LossAmount;
            decimal after = (decimal)receipt.PlayerTrophiesBefore +
                (receipt.Outcome == TrophyMatchOutcome.Victory ? (decimal)band.WinGain : -(decimal)loss);
            if (after < policy.MinimumTrophies.Value)
            {
                if (policy.MinimumRule != TrophyMinimumRule.ClampToMinimum)
                { reason = "The approved minimum-score behavior has not been configured."; return TrophyLadderStatus.NeedsConfiguration; }
                after = policy.MinimumTrophies.Value;
            }
            if (after > long.MaxValue || after < long.MinValue)
            { reason = "The explicit adjustment exceeds the score representation."; return TrophyLadderStatus.ArithmeticOverflow; }
            long finalScore = (long)after;
            recorded = new TrophyRecordedMatch { Receipt = CopyReceipt(receipt), DifferenceBandIndex = index, StageId = stage.StageId,
                TrophiesAfter = finalScore, AppliedDelta = checked(finalScore - receipt.PlayerTrophiesBefore) };
            return TrophyLadderStatus.Ready;
        }

        TrophyLadderStatus Validate(TrophyLadderState state, out string reason)
        {
            reason = "";
            if (state != null && state.Version > StateVersion)
            { reason = "The ladder state version is unsupported."; return TrophyLadderStatus.UnsupportedVersion; }
            if (state == null || state.Version < 1 || state.Version > StateVersion || !ValidId(state.PlayerId) || !ValidId(state.PolicySignature) ||
                state.Matches == null || state.Revision < 0 || state.Revision != state.Matches.LongLength)
            { reason = "The ladder state is incomplete or inconsistent."; return TrophyLadderStatus.InvalidState; }
            if (state.PolicySignature != signature)
            { reason = "Policy or board catalogue changed; explicit migration is required."; return TrophyLadderStatus.Conflict; }
            bool historical = !string.IsNullOrEmpty(state.HistoricalPolicySignature);
            if (state.HistoricalMatchCount < 0 || state.HistoricalMatchCount > state.Matches.LongLength ||
                state.Version == 1 && (historical || state.HistoricalMatchCount != 0) ||
                !historical && state.HistoricalMatchCount != 0 ||
                historical && (predecessor == null || state.HistoricalPolicySignature != predecessor.signature ||
                    predecessor.policy.InitialTrophies != policy.InitialTrophies || predecessor.policy.MinimumTrophies != policy.MinimumTrophies))
            { reason = "The historical policy prefix is invalid or unapproved."; return TrophyLadderStatus.InvalidState; }
            if(state.Version>=3&&(historical||state.HistoricalMatchCount!=0)||state.HistoricalSegments==null||
                state.Version<3&&state.HistoricalSegments.Length!=0)
            {reason="Invalid segmented policy history version.";return TrophyLadderStatus.InvalidState;}
            long end=0;
            TrophyLadderEngine previousSegmentEngine=null;
            var seenPolicies=new HashSet<string>(StringComparer.Ordinal);
            foreach(var segment in state.HistoricalSegments) {
                var engine=FindPredecessor(segment?.PolicySignature);
                if(segment==null||engine==null||!seenPolicies.Add(segment.PolicySignature)||segment.EndMatchIndex<=end||
                    segment.EndMatchIndex>state.Matches.LongLength||engine.policy.InitialTrophies!=policy.InitialTrophies||
                    engine.policy.MinimumTrophies!=policy.MinimumTrophies||previousSegmentEngine!=null&&engine.FindPredecessor(previousSegmentEngine.signature)==null)
                {reason="Invalid segmented policy prefix.";return TrophyLadderStatus.InvalidState;}
                end=segment.EndMatchIndex;
                previousSegmentEngine=engine;
            }
            long current = policy.InitialTrophies.Value, highest = current;
            long matchIndex = 0;
            var matches = new HashSet<string>(StringComparer.Ordinal);
            var evidence = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in state.Matches)
            {
                var calculator = matchIndex < state.HistoricalMatchCount ? predecessor : this;
                var historicalSegment=state.HistoricalSegments.FirstOrDefault(s=>matchIndex<s.EndMatchIndex);
                if(historicalSegment!=null)calculator=FindPredecessor(historicalSegment.PolicySignature);
                if (item == null || !ValidReceiptIdentity(item.Receipt) || item.Receipt.PlayerId != state.PlayerId ||
                    item.Receipt.PlayerTrophiesBefore != current || !matches.Add(item.Receipt.MatchId) ||
                    !evidence.Add(item.Receipt.EligibilityEvidenceId) ||
                    calculator.Calculate(item.Receipt, out var expected, out _) != TrophyLadderStatus.Ready ||
                    expected.DifferenceBandIndex != item.DifferenceBandIndex || expected.StageId != item.StageId || expected.AppliedDelta != item.AppliedDelta ||
                    expected.TrophiesAfter != item.TrophiesAfter)
                { reason = "The ladder history does not reproduce its recorded adjustments."; return TrophyLadderStatus.InvalidState; }
                current = item.TrophiesAfter; highest = Math.Max(highest, current); matchIndex++;
            }
            if (state.CurrentTrophies != current || state.HighestTrophies != highest)
            { reason = "Current or highest trophies differ from recorded history."; return TrophyLadderStatus.InvalidState; }
            return TrophyLadderStatus.Ready;
        }

        TrophyLadderEngine FindPredecessor(string target)
        {
            for(var engine=predecessor;engine!=null;engine=engine.predecessor)
                if(engine.signature==target)return engine;
            return null;
        }
        static TrophyLadderState CanonicalState(TrophyLadderState state)
        {
            var next=CopyState(state);
            if(next.Version<3&&next.HistoricalMatchCount>0)
                next.HistoricalSegments=new[]{new TrophyPolicySegment {PolicySignature=next.HistoricalPolicySignature,EndMatchIndex=next.HistoricalMatchCount}};
            next.HistoricalPolicySignature=string.Empty;next.HistoricalMatchCount=0;next.Version=StateVersion;
            return next;
        }

        static bool ValidReceiptIdentity(TrophyMatchReceipt receipt) => receipt != null &&
            ValidId(receipt.PlayerId) && ValidId(receipt.OpponentPlayerId) && receipt.PlayerId != receipt.OpponentPlayerId &&
            ValidId(receipt.MatchId) && ValidId(receipt.EligibilityEvidenceId) && ValidId(receipt.SourceModeId);
        int StageIndex(long score)
        {
            int index = 0;
            while (index + 1 < policy.Stages.Length && policy.Stages[index + 1].MinimumTrophies <= score) index++;
            return index;
        }
        static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id == id.Trim();
        static bool SameReceipt(TrophyMatchReceipt a, TrophyMatchReceipt b) => a.PlayerId == b.PlayerId &&
            a.OpponentPlayerId == b.OpponentPlayerId && a.MatchId == b.MatchId && a.EligibilityEvidenceId == b.EligibilityEvidenceId &&
            a.SourceModeId == b.SourceModeId && a.PlayerTrophiesBefore == b.PlayerTrophiesBefore &&
            a.OpponentTrophiesBefore == b.OpponentTrophiesBefore && a.Outcome == b.Outcome && a.Eligibility == b.Eligibility;
        static TrophyMatchReceipt CopyReceipt(TrophyMatchReceipt receipt) => new TrophyMatchReceipt {
            PlayerId = receipt.PlayerId, OpponentPlayerId = receipt.OpponentPlayerId, MatchId = receipt.MatchId,
            EligibilityEvidenceId = receipt.EligibilityEvidenceId, SourceModeId = receipt.SourceModeId,
            PlayerTrophiesBefore = receipt.PlayerTrophiesBefore, OpponentTrophiesBefore = receipt.OpponentTrophiesBefore,
            Outcome = receipt.Outcome, Eligibility = receipt.Eligibility };
        static TrophyRecordedMatch CopyRecorded(TrophyRecordedMatch item) => item == null ? null : new TrophyRecordedMatch {
            Receipt = CopyReceipt(item.Receipt), DifferenceBandIndex = item.DifferenceBandIndex, StageId = item.StageId,
            AppliedDelta = item.AppliedDelta, TrophiesAfter = item.TrophiesAfter };
        static TrophyLadderState CopyState(TrophyLadderState state) => state == null ? null : new TrophyLadderState {
            Version = state.Version, PlayerId = state.PlayerId, PolicySignature = state.PolicySignature, Revision = state.Revision,
            HistoricalPolicySignature = state.HistoricalPolicySignature, HistoricalMatchCount = state.HistoricalMatchCount,
            HistoricalSegments = state.HistoricalSegments?.Select(s=>s==null?null:new TrophyPolicySegment {PolicySignature=s.PolicySignature,EndMatchIndex=s.EndMatchIndex}).ToArray(),
            CurrentTrophies = state.CurrentTrophies, HighestTrophies = state.HighestTrophies,
            Matches = state.Matches.Select(CopyRecorded).ToArray() };
        static TrophyLadderResult Result(TrophyLadderStatus status, string reason, TrophyLadderState state, TrophyRecordedMatch item = null) =>
            new TrophyLadderResult { Status = status, Reason = reason, State = CopyState(state), RecordedMatch = CopyRecorded(item) };
        static TrophyLadderPolicy CopyPolicy(TrophyLadderPolicy source) => new TrophyLadderPolicy {
            PolicyId = source.PolicyId, Version = source.Version, InitialTrophies = source.InitialTrophies,
            MinimumTrophies = source.MinimumTrophies, MinimumRule = source.MinimumRule,
            DifferenceBands = source.DifferenceBands.Select(b => new TrophyDifferenceBand {
                UpperOpponentDifferenceExclusive = b.UpperOpponentDifferenceExclusive, WinGain = b.WinGain, LossAmount = b.LossAmount }).ToArray(),
            Stages = source.Stages.Select(s => new TrophyLadderStage { StageId = s.StageId, MinimumTrophies = s.MinimumTrophies,
                BoardId = s.BoardId, DefeatLossOverride = s.DefeatLossOverride }).ToArray() };

        static string Signature(TrophyLadderPolicy policy, TrophyBoardCatalogue catalogue)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteText(writer, "MonsterPouch.TrophyLadder.Policy.v1"); WriteText(writer, policy.PolicyId); writer.Write(policy.Version);
                writer.Write(policy.InitialTrophies.Value); writer.Write(policy.MinimumTrophies.Value); writer.Write((int)policy.MinimumRule);
                writer.Write(policy.DifferenceBands.Length);
                foreach (var band in policy.DifferenceBands)
                {
                    writer.Write(band.UpperOpponentDifferenceExclusive.HasValue);
                    if (band.UpperOpponentDifferenceExclusive.HasValue) writer.Write(band.UpperOpponentDifferenceExclusive.Value);
                    writer.Write(band.WinGain); writer.Write(band.LossAmount);
                }
                writer.Write(policy.Stages.Length);
                foreach (var stage in policy.Stages)
                {
                    WriteText(writer, stage.StageId); writer.Write(stage.MinimumTrophies); WriteText(writer, stage.BoardId);
                    writer.Write(stage.DefeatLossOverride.HasValue);
                    if (stage.DefeatLossOverride.HasValue) writer.Write(stage.DefeatLossOverride.Value);
                }
                WriteText(writer, catalogue.CatalogueId); writer.Write(catalogue.Version); writer.Write(catalogue.Boards.Length);
                foreach (var board in catalogue.Boards.OrderBy(b => b.BoardId, StringComparer.Ordinal))
                { WriteText(writer, board.BoardId); writer.Write((int)board.Role); }
                writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "");
            }
        }
        static void WriteText(BinaryWriter writer, string value)
        {
            writer.Write(value.Length);
            foreach (char character in value) writer.Write((ushort)character);
        }
    }
}

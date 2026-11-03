using System;
using System.Linq;
using MonsterPouch.Gameplay.Progression;
using NUnit.Framework;

// All IDs, thresholds and adjustments here are synthetic fixtures, not game balance.
public sealed class TrophyLadderEngineTests
{
    TrophyLadderPolicy policy;
    TrophyBoardCatalogue catalogue;
    TrophyLadderEngine engine;
    TrophyLadderState state;
    static TrophyLadderPolicy Policy() => new TrophyLadderPolicy {
        PolicyId = "qa-ladder", Version = 1, InitialTrophies = 100, MinimumTrophies = 0,
        MinimumRule = TrophyMinimumRule.Unspecified,
        DifferenceBands = new[] {
            new TrophyDifferenceBand { UpperOpponentDifferenceExclusive = 0, WinGain = 10, LossAmount = 30 },
            new TrophyDifferenceBand { UpperOpponentDifferenceExclusive = 10, WinGain = 20, LossAmount = 20 },
            new TrophyDifferenceBand { UpperOpponentDifferenceExclusive = null, WinGain = 30, LossAmount = 10 } },
        Stages = new[] {
            new TrophyLadderStage { StageId = "qa-stage-a", BoardId = "qa-initial", MinimumTrophies = 0 },
            new TrophyLadderStage { StageId = "qa-stage-b", BoardId = "qa-free", MinimumTrophies = 120 },
            new TrophyLadderStage { StageId = "qa-stage-c", BoardId = "qa-free", MinimumTrophies = 160 } } };
    static TrophyBoardCatalogue Catalogue() => new TrophyBoardCatalogue {
        CatalogueId = "qa-boards", Version = 1, Boards = new[] {
            new TrophyBoardDefinition { BoardId = "qa-initial", Role = TrophyBoardRole.InitialProgression },
            new TrophyBoardDefinition { BoardId = "qa-free", Role = TrophyBoardRole.Progression },
            new TrophyBoardDefinition { BoardId = "qa-paid", Role = TrophyBoardRole.PaidCosmetic } } };
    [SetUp] public void SetUp() { policy = Policy(); catalogue = Catalogue(); Prepare(); }
    void Prepare()
    {
        Assert.IsTrue(TrophyLadderEngine.TryCreate(policy, catalogue, out engine, out var reason), reason);
        state = engine.CreateState("qa-player");
    }
    TrophyMatchReceipt Receipt(string match = "qa-match", TrophyMatchOutcome outcome = TrophyMatchOutcome.Victory, long? opponent = null) =>
        new TrophyMatchReceipt { PlayerId = state.PlayerId, OpponentPlayerId = "qa-opponent", MatchId = match,
            EligibilityEvidenceId = "qa-evidence-" + match, SourceModeId = "qa-explicit-mode",
            PlayerTrophiesBefore = state.CurrentTrophies, OpponentTrophiesBefore = opponent ?? state.CurrentTrophies,
            Outcome = outcome, Eligibility = TrophyMatchEligibility.ApprovedCompetitive };
    TrophyLadderResult Apply(string match, TrophyMatchOutcome outcome = TrophyMatchOutcome.Victory, long? opponent = null)
    {
        var result = engine.Record(state, Receipt(match, outcome, opponent), state.Revision);
        Assert.AreEqual(TrophyLadderStatus.Applied, result.Status, result.Reason); state = result.State; return result;
    }

    [Test] public void PolicyDefaultsCannotCreateAPlayableLadder()
    {
        var empty = new TrophyLadderPolicy(); Assert.IsNull(empty.InitialTrophies); Assert.IsNull(empty.MinimumTrophies);
        Assert.IsNull(empty.DifferenceBands); Assert.IsNull(empty.Stages);
        Assert.IsFalse(TrophyLadderEngine.TryCreate(empty, catalogue, out var invalid, out _)); Assert.IsNull(invalid);
    }
    [TestCase("missing-initial")][TestCase("missing-minimum")][TestCase("initial-below-minimum")]
    [TestCase("initial-skips-first")][TestCase("missing-bands")][TestCase("empty-bands")]
    [TestCase("zero-win")][TestCase("negative-loss")][TestCase("win-reversed")][TestCase("loss-reversed")]
    [TestCase("band-gap-order")][TestCase("unbounded-middle")][TestCase("bounded-final")]
    [TestCase("missing-stages")][TestCase("stage-gap-at-minimum")][TestCase("stage-order")]
    [TestCase("duplicate-stage")][TestCase("unknown-board")][TestCase("paid-board")]
    [TestCase("wrong-first-board")][TestCase("unknown-role")][TestCase("two-initial")]
    [TestCase("zero-stage-loss")][TestCase("negative-stage-loss")]
    public void MissingOrContradictoryExplicitConfigurationFails(string variation)
    {
        switch (variation)
        {
            case "missing-initial": policy.InitialTrophies = null; break;
            case "missing-minimum": policy.MinimumTrophies = null; break;
            case "initial-below-minimum": policy.InitialTrophies = -1; break;
            case "initial-skips-first": policy.InitialTrophies = 120; break;
            case "missing-bands": policy.DifferenceBands = null; break;
            case "empty-bands": policy.DifferenceBands = Array.Empty<TrophyDifferenceBand>(); break;
            case "zero-win": policy.DifferenceBands[0].WinGain = 0; break;
            case "negative-loss": policy.DifferenceBands[0].LossAmount = -1; break;
            case "win-reversed": policy.DifferenceBands[2].WinGain = 1; break;
            case "loss-reversed": policy.DifferenceBands[2].LossAmount = 100; break;
            case "band-gap-order": policy.DifferenceBands[1].UpperOpponentDifferenceExclusive = -1; break;
            case "unbounded-middle": policy.DifferenceBands[0].UpperOpponentDifferenceExclusive = null; break;
            case "bounded-final": policy.DifferenceBands[2].UpperOpponentDifferenceExclusive = 100; break;
            case "missing-stages": policy.Stages = null; break;
            case "stage-gap-at-minimum": policy.Stages[0].MinimumTrophies = 1; break;
            case "stage-order": policy.Stages[2].MinimumTrophies = 119; break;
            case "duplicate-stage": policy.Stages[1].StageId = policy.Stages[0].StageId; break;
            case "unknown-board": policy.Stages[1].BoardId = "qa-not-catalogued"; break;
            case "paid-board": policy.Stages[1].BoardId = "qa-paid"; break;
            case "wrong-first-board": policy.Stages[0].BoardId = "qa-free"; break;
            case "unknown-role": catalogue.Boards[1].Role = TrophyBoardRole.Unspecified; break;
            case "two-initial": catalogue.Boards[1].Role = TrophyBoardRole.InitialProgression; break;
            case "zero-stage-loss": policy.Stages[0].DefeatLossOverride = 0; break;
            case "negative-stage-loss": policy.Stages[1].DefeatLossOverride = -1; break;
        }
        Assert.IsFalse(TrophyLadderEngine.TryCreate(policy, catalogue, out var invalid, out var reason));
        Assert.IsNull(invalid); Assert.IsNotEmpty(reason);
    }
    [TestCase(99L, 10L, -30L)][TestCase(100L, 20L, -20L)]
    [TestCase(109L, 20L, -20L)][TestCase(110L, 30L, -10L)]
    public void OpponentDifferenceBoundariesUseExplicitMonotonicAmounts(long rival, long gain, long loss)
    {
        var victory = engine.Record(state, Receipt("win", TrophyMatchOutcome.Victory, rival), 0);
        var defeat = engine.Record(state, Receipt("lose", TrophyMatchOutcome.Defeat, rival), 0);
        Assert.AreEqual(TrophyLadderStatus.Applied, victory.Status); Assert.AreEqual(TrophyLadderStatus.Applied, defeat.Status);
        Assert.AreEqual(gain, victory.RecordedMatch.AppliedDelta); Assert.AreEqual(loss, defeat.RecordedMatch.AppliedDelta);
        Assert.AreEqual(100, state.CurrentTrophies); Assert.IsEmpty(state.Matches);
    }
    [Test] public void HistoricalHighSurvivesLossAndCurrentRangeCanMoveDown()
    {
        Apply("win"); Assert.AreEqual("qa-stage-b", engine.PreviewStage(state).StageId);
        Apply("loss", TrophyMatchOutcome.Defeat);
        var preview = engine.PreviewStage(state); Assert.AreEqual(100, preview.CurrentTrophies); Assert.AreEqual(120, preview.HighestTrophies);
        Assert.AreEqual("qa-stage-a", preview.StageId);
        CollectionAssert.AreEqual(new[] { "qa-stage-a", "qa-stage-b" }, preview.ReachedStages.Select(s => s.StageId).ToArray());
    }
    [Test] public void OneExplicitFlatBandAndStageLossesSupportPromotionDescentAndLaterRetry()
    {
        policy.DifferenceBands = new[] { new TrophyDifferenceBand { WinGain = 11, LossAmount = 19 } };
        policy.Stages = policy.Stages.Take(2).ToArray(); policy.Stages[1].MinimumTrophies = 111;
        policy.Stages[0].DefeatLossOverride = 7; policy.Stages[1].DefeatLossOverride = 13; Prepare();
        // A single explicit band makes opponent rating irrelevant until a different policy is approved.
        Assert.AreEqual(11, engine.Record(state, Receipt(opponent: 0), 0).RecordedMatch.AppliedDelta);
        Assert.AreEqual(11, engine.Record(state, Receipt(opponent: 9999), 0).RecordedMatch.AppliedDelta);
        var lowLoss = engine.Record(state, Receipt("low-loss", TrophyMatchOutcome.Defeat), 0);
        Assert.AreEqual(-7, lowLoss.RecordedMatch.AppliedDelta); Assert.AreEqual("qa-stage-a", lowLoss.RecordedMatch.StageId);
        Apply("promotion"); Assert.AreEqual("qa-stage-b", engine.PreviewStage(state).StageId);
        var defeat = Receipt("descent", TrophyMatchOutcome.Defeat); var lost = engine.Record(state, defeat, state.Revision); state = lost.State;
        Assert.AreEqual(-13, lost.RecordedMatch.AppliedDelta); Assert.AreEqual("qa-stage-b", lost.RecordedMatch.StageId);
        Assert.AreEqual(98, state.CurrentTrophies); Assert.AreEqual("qa-stage-a", engine.PreviewStage(state).StageId);
        Apply("later-1"); Apply("later-2"); Assert.AreEqual(120, state.CurrentTrophies);
        var retry = engine.Record(state, defeat, 1);
        Assert.AreEqual(TrophyLadderStatus.AlreadyApplied, retry.Status); Assert.AreEqual(120, retry.State.CurrentTrophies);
        Assert.AreEqual(-13, retry.RecordedMatch.AppliedDelta); Assert.AreEqual(4, retry.State.Revision);
    }
    [Test] public void StageLossOverridesAreCopiedAndPartOfPolicyIdentity()
    {
        policy.Stages[0].DefeatLossOverride = 7; Prepare(); policy.Stages[0].DefeatLossOverride = 8;
        var receipt = Receipt(outcome: TrophyMatchOutcome.Defeat);
        Assert.AreEqual(-7, engine.Record(state, receipt, 0).RecordedMatch.AppliedDelta);
        Assert.IsTrue(TrophyLadderEngine.TryCreate(policy, catalogue, out var changed, out _));
        Assert.AreEqual(TrophyLadderStatus.Conflict, changed.Record(state, receipt, 0).Status);
        Assert.AreEqual(100, state.CurrentTrophies); Assert.IsEmpty(state.Matches);
    }
    [Test] public void LastRangeIsFiniteAndOpenWhileTrophiesContinueGrowing()
    {
        for (int i = 0; i < 10; i++) Apply("up-" + i);
        var preview = engine.PreviewStage(state);
        Assert.AreEqual(300, state.CurrentTrophies); Assert.AreEqual("qa-stage-c", preview.StageId); Assert.IsNull(preview.NextStageAt);
        Assert.AreEqual(3, preview.ReachedStages.Length); Assert.AreEqual("qa-free", preview.BoardId);
        Assert.IsFalse(preview.ReachedStages.Any(s => s.BoardId == "qa-paid"));
        Assert.AreEqual(preview.ReachedStages[1].BoardId, preview.ReachedStages[2].BoardId, "Repeated free boards are allowed.");
    }
    [Test] public void RetryAfterLaterMatchesKeepsOriginalOpponentScoreAndCurrentProgress()
    {
        var original = Receipt("first", TrophyMatchOutcome.Victory, 110);
        state = engine.Record(state, original, 0).State; Apply("later");
        var retry = engine.Record(state, original, 0);
        Assert.AreEqual(TrophyLadderStatus.AlreadyApplied, retry.Status); Assert.AreEqual(150, retry.State.CurrentTrophies);
        Assert.AreEqual(2, retry.State.Revision); Assert.AreEqual(30, retry.RecordedMatch.AppliedDelta);
        Assert.AreEqual(110, retry.RecordedMatch.Receipt.OpponentTrophiesBefore);
    }
    [TestCase("opponent-score")][TestCase("player-score")][TestCase("opponent-id")]
    [TestCase("outcome")][TestCase("eligibility")][TestCase("mode")][TestCase("evidence")]
    public void RetriedMatchCannotChangeAnyAdjudicatedPayload(string field)
    {
        var receipt = Receipt(); state = engine.Record(state, receipt, 0).State;
        switch (field)
        {
            case "opponent-score": receipt.OpponentTrophiesBefore++; break;
            case "player-score": receipt.PlayerTrophiesBefore++; break;
            case "opponent-id": receipt.OpponentPlayerId = "qa-other-rival"; break;
            case "outcome": receipt.Outcome = TrophyMatchOutcome.Defeat; break;
            case "eligibility": receipt.Eligibility = TrophyMatchEligibility.Rejected; break;
            case "mode": receipt.SourceModeId = "qa-other-mode"; break;
            case "evidence": receipt.EligibilityEvidenceId = "qa-other-proof"; break;
        }
        var result = engine.Record(state, receipt, 0); Assert.AreEqual(TrophyLadderStatus.Conflict, result.Status);
        Assert.AreEqual(120, state.CurrentTrophies); Assert.AreEqual(1, state.Revision);
    }
    [Test] public void BothPlayersCanUseTheSharedMatchIdWithoutSharingTheirStates()
    {
        var hostReceipt = Receipt("shared", TrophyMatchOutcome.Victory);
        var guest = engine.CreateState("qa-opponent");
        var guestReceipt = new TrophyMatchReceipt { PlayerId = guest.PlayerId, OpponentPlayerId = state.PlayerId,
            MatchId = "shared", EligibilityEvidenceId = "qa-other-adjudication", SourceModeId = "qa-explicit-mode",
            PlayerTrophiesBefore = 100, OpponentTrophiesBefore = 100, Outcome = TrophyMatchOutcome.Defeat,
            Eligibility = TrophyMatchEligibility.ApprovedCompetitive };
        Assert.AreEqual(120, engine.Record(state, hostReceipt, 0).State.CurrentTrophies);
        Assert.AreEqual(80, engine.Record(guest, guestReceipt, 0).State.CurrentTrophies);
        Assert.AreEqual(TrophyLadderStatus.Conflict, engine.Record(state, guestReceipt, 0).Status);
    }
    [Test] public void StaleScoreRevisionAndEvidenceReuseFailWithoutConsumingNewMatch()
    {
        var old = Receipt("old"); Apply("first");
        Assert.AreEqual(TrophyLadderStatus.Conflict, engine.Record(state, old, state.Revision).Status);
        var fresh = Receipt("second"); Assert.AreEqual(TrophyLadderStatus.Conflict, engine.Record(state, fresh, 0).Status);
        fresh.EligibilityEvidenceId = state.Matches[0].Receipt.EligibilityEvidenceId;
        Assert.AreEqual(TrophyLadderStatus.Conflict, engine.Record(state, fresh, state.Revision).Status);
        Assert.AreEqual(1, state.Matches.Length);
    }
    [TestCase(TrophyMatchOutcome.Draw)][TestCase(TrophyMatchOutcome.Abandoned)][TestCase(TrophyMatchOutcome.Unspecified)]
    public void UnapprovedOutcomeRulesNeverAwardOrDeduct(TrophyMatchOutcome outcome)
    {
        var result = engine.Record(state, Receipt(outcome: outcome), 0);
        Assert.AreEqual(TrophyLadderStatus.NeedsConfiguration, result.Status); Assert.AreEqual(100, result.State.CurrentTrophies);
        Assert.IsEmpty(result.State.Matches);
    }
    [TestCase(TrophyMatchEligibility.Unverified)][TestCase(TrophyMatchEligibility.Rejected)]
    public void EligibilityMustComeFromAuthority(TrophyMatchEligibility eligibility)
    {
        var receipt = Receipt(); receipt.Eligibility = eligibility;
        Assert.AreEqual(TrophyLadderStatus.NotEligible, engine.Record(state, receipt, 0).Status); Assert.IsEmpty(state.Matches);
    }
    [Test] public void AnUnconfiguredFloorDoesNotSilentlyClampOrConsumeDefeat()
    {
        policy.InitialTrophies = 0; Prepare(); var receipt = Receipt(outcome: TrophyMatchOutcome.Defeat);
        for (int i = 0; i < 2; i++)
        {
            var result = engine.Record(state, receipt, 0); Assert.AreEqual(TrophyLadderStatus.NeedsConfiguration, result.Status);
            Assert.AreEqual(0, result.State.Revision); Assert.IsEmpty(result.State.Matches); Assert.AreEqual(0, result.State.CurrentTrophies);
        }
    }
    [Test] public void ExplicitFloorRecordsZeroDeltaDefeatAndRetryCannotDeductAfterLaterVictory()
    {
        policy.InitialTrophies = 0; policy.MinimumRule = TrophyMinimumRule.ClampToMinimum; Prepare();
        var defeat = Receipt("at-floor", TrophyMatchOutcome.Defeat); var first = engine.Record(state, defeat, 0); state = first.State;
        Assert.AreEqual(TrophyLadderStatus.Applied, first.Status); Assert.AreEqual(0, first.RecordedMatch.AppliedDelta);
        Assert.AreEqual(1, state.Revision); Apply("later-win");
        var retry = engine.Record(state, defeat, 0); Assert.AreEqual(TrophyLadderStatus.AlreadyApplied, retry.Status);
        Assert.AreEqual(20, retry.State.CurrentTrophies); Assert.AreEqual(2, retry.State.Revision);
    }
    [Test] public void ExactLongScoresCannotOverflowOpponentDifferenceOrWrapWins()
    {
        policy.MinimumTrophies = long.MinValue; policy.InitialTrophies = long.MinValue;
        policy.Stages = new[] { new TrophyLadderStage { StageId = "qa-only", BoardId = "qa-initial", MinimumTrophies = long.MinValue } };
        Prepare(); var low = engine.Record(state, Receipt(opponent: long.MaxValue), 0);
        Assert.AreEqual(TrophyLadderStatus.Applied, low.Status); Assert.AreEqual(30, low.RecordedMatch.AppliedDelta);
        policy.InitialTrophies = long.MaxValue; Prepare();
        var high = engine.Record(state, Receipt(opponent: long.MinValue), 0);
        Assert.AreEqual(TrophyLadderStatus.ArithmeticOverflow, high.Status); Assert.AreEqual(long.MaxValue, high.State.CurrentTrophies);
        Assert.IsEmpty(high.State.Matches);
    }
    [TestCase(5L, 0L, -5L)][TestCase(0L, 0L, 0L)][TestCase(-100L, -105L, -5L)]
    public void ExplicitMinimumNeverTurnsDefeatIntoAnIncrease(long initial, long minimum, long expectedDelta)
    {
        policy.InitialTrophies = initial; policy.MinimumTrophies = minimum; policy.Stages[0].MinimumTrophies = minimum;
        policy.MinimumRule = TrophyMinimumRule.ClampToMinimum; Prepare();
        var result = Apply("floor", TrophyMatchOutcome.Defeat);
        Assert.AreEqual(expectedDelta, result.RecordedMatch.AppliedDelta); Assert.LessOrEqual(state.CurrentTrophies, initial);
        Assert.AreEqual(initial, state.HighestTrophies); Assert.AreEqual(1, state.Matches.Length);
    }
    [TestCase(0L)][TestCase(-100L)]
    public void OpponentScoreBelowExplicitMinimumIsRejectedWithoutApplyingOrConsumingReceipt(long minimum)
    {
        policy.MinimumTrophies = minimum; policy.Stages[0].MinimumTrophies = minimum; Prepare();
        var receipt = Receipt(opponent: minimum - 1);
        var rejected = engine.Record(state, receipt, 0);
        Assert.AreEqual(TrophyLadderStatus.InvalidRequest, rejected.Status); Assert.AreEqual(100, state.CurrentTrophies);
        Assert.AreEqual(0, rejected.State.Revision); Assert.IsEmpty(rejected.State.Matches);
        receipt.OpponentTrophiesBefore = minimum;
        var valid = engine.Record(state, receipt, 0); Assert.AreEqual(TrophyLadderStatus.Applied, valid.Status);
        valid.State.Matches[0].Receipt.OpponentTrophiesBefore = minimum - 1;
        Assert.AreEqual(TrophyLadderStatus.InvalidState, engine.PreviewStage(valid.State).Status);
    }
    [TestCase("score")][TestCase("highest")][TestCase("revision")][TestCase("missing-history")]
    [TestCase("delta")][TestCase("band")][TestCase("stage")][TestCase("after")][TestCase("duplicate-match")][TestCase("invalid-eligibility")]
    public void TamperedHistoryOrBalancesFailClosed(string part)
    {
        Apply("first"); Apply("second");
        switch (part)
        {
            case "score": state.CurrentTrophies++; break;
            case "highest": state.HighestTrophies++; break;
            case "revision": state.Revision++; break;
            case "missing-history": state.Matches = null; break;
            case "delta": state.Matches[0].AppliedDelta++; break;
            case "band": state.Matches[0].DifferenceBandIndex++; break;
            case "stage": state.Matches[0].StageId = "qa-fake-stage"; break;
            case "after": state.Matches[0].TrophiesAfter++; break;
            case "duplicate-match": state.Matches[1].Receipt.MatchId = state.Matches[0].Receipt.MatchId; break;
            case "invalid-eligibility": state.Matches[0].Receipt.Eligibility = TrophyMatchEligibility.Unverified; break;
        }
        Assert.AreEqual(TrophyLadderStatus.InvalidState, engine.PreviewStage(state).Status);
        var result = engine.Record(state, Receipt("new"), state.Revision);
        Assert.AreEqual(TrophyLadderStatus.InvalidState, result.Status); Assert.IsNull(result.State);
    }
    [Test] public void FutureVersionAndChangedPolicyRequireExplicitMigrationWithoutReset()
    {
        Apply("first"); policy.DifferenceBands[2].WinGain++;
        Assert.IsTrue(TrophyLadderEngine.TryCreate(policy, catalogue, out var changed, out _));
        Assert.AreEqual(TrophyLadderStatus.Conflict, changed.PreviewStage(state).Status); Assert.AreEqual(120, state.CurrentTrophies);
        state.Version = 3; Assert.AreEqual(TrophyLadderStatus.UnsupportedVersion, engine.PreviewStage(state).Status);
        Assert.AreEqual(1, state.Matches.Length);
    }
    [Test] public void CatalogueOrderingDoesNotChangePolicyButAReclassifiedBoardDoes()
    {
        catalogue.Boards = catalogue.Boards.Reverse().ToArray();
        Assert.IsTrue(TrophyLadderEngine.TryCreate(policy, catalogue, out var reordered, out _));
        Assert.AreEqual(TrophyLadderStatus.Ready, reordered.PreviewStage(state).Status);
        catalogue.Version++; Assert.IsTrue(TrophyLadderEngine.TryCreate(policy, catalogue, out var changed, out _));
        Assert.AreEqual(TrophyLadderStatus.Conflict, changed.PreviewStage(state).Status);
    }
    [Test] public void CallerMutationsCannotAlterPreparedRulesInputsOrSiblingResults()
    {
        policy.DifferenceBands[1].WinGain = 999; policy.Stages[0].BoardId = "qa-paid"; catalogue.Boards[0].Role = TrophyBoardRole.PaidCosmetic;
        var receipt = Receipt(); var result = engine.Record(state, receipt, 0); receipt.OpponentTrophiesBefore = 9999;
        Assert.AreEqual(120, result.State.CurrentTrophies); Assert.AreEqual(100, result.RecordedMatch.Receipt.OpponentTrophiesBefore);
        result.RecordedMatch.Receipt.OpponentPlayerId = "changed";
        Assert.AreEqual("qa-opponent", result.State.Matches[0].Receipt.OpponentPlayerId);
        var preview = engine.PreviewStage(result.State); preview.ReachedStages[0].BoardId = "changed";
        Assert.AreEqual("qa-initial", engine.PreviewStage(state).BoardId);
        Assert.AreEqual(100, state.CurrentTrophies); Assert.IsEmpty(state.Matches);
    }
    [TestCase("null")][TestCase("missing-opponent")][TestCase("same-player")][TestCase("missing-mode")][TestCase("missing-evidence")]
    public void IncompleteIdentitiesDoNotRecordOrAward(string part)
    {
        var receipt = Receipt();
        switch (part)
        {
            case "null": receipt = null; break;
            case "missing-opponent": receipt.OpponentPlayerId = " "; break;
            case "same-player": receipt.OpponentPlayerId = receipt.PlayerId; break;
            case "missing-mode": receipt.SourceModeId = null; break;
            case "missing-evidence": receipt.EligibilityEvidenceId = null; break;
        }
        Assert.AreEqual(TrophyLadderStatus.InvalidRequest, engine.Record(state, receipt, 0).Status); Assert.IsEmpty(state.Matches);
    }
}

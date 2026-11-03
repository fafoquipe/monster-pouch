using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Progression;
using NUnit.Framework;
using UnityEngine;

public sealed class TrophyLadderTrialTests
{
    TrophyLadderEngine engine;
    TrophyLadderState state;
    int sequence;
    [SetUp] public void SetUp()
    {
        var catalogue = TrophyLadderTrialConfiguration.CreateCatalogue();
        Assert.IsTrue(TrophyLadderEngine.TryCreate(TrophyLadderTrialConfiguration.CreatePolicy(), catalogue, out engine, out var reason), reason);
        state = engine.CreateState("qa-trial-player"); sequence = 0;
    }
    TrophyMatchReceipt Receipt(TrophyMatchOutcome outcome, long? opponent = null)
    {
        string id = "qa-trial-match-" + ++sequence;
        return new TrophyMatchReceipt { PlayerId = state.PlayerId, OpponentPlayerId = "qa-trial-rival", MatchId = id,
            EligibilityEvidenceId = "evidence-" + id, SourceModeId = "qa-trial-test",
            PlayerTrophiesBefore = state.CurrentTrophies, OpponentTrophiesBefore = opponent ?? state.CurrentTrophies,
            Outcome = outcome, Eligibility = TrophyMatchEligibility.ApprovedCompetitive };
    }
    TrophyLadderResult Apply(TrophyMatchOutcome outcome)
    {
        var result = engine.Record(state, Receipt(outcome), state.Revision);
        Assert.AreEqual(TrophyLadderStatus.Applied, result.Status, result.Reason); state = result.State; return result;
    }
    void Wins(int count) { for (int i = 0; i < count; i++) Apply(TrophyMatchOutcome.Victory); }

    [Test] public void FiveVictoriesReachRoyalAtExactly150UsingExistingFreeBoards()
    {
        Assert.AreEqual("classic", engine.PreviewStage(state).BoardId); Wins(4);
        Assert.AreEqual(120, state.CurrentTrophies); Assert.AreEqual("classic", engine.PreviewStage(state).BoardId);
        Wins(1); var preview = engine.PreviewStage(state);
        Assert.AreEqual(150, state.CurrentTrophies); Assert.AreEqual("royal", preview.BoardId); Assert.IsNull(preview.NextStageAt);
        CollectionAssert.AreEqual(new[] { "classic", "royal" }, preview.ReachedStages.Select(s => s.BoardId).ToArray());
    }
    [Test] public void DefeatAtFinalStageLoses30AndFollowingDefeatInFirstStageLoses5()
    {
        Wins(5); var first = Apply(TrophyMatchOutcome.Defeat);
        Assert.AreEqual(-30, first.RecordedMatch.AppliedDelta); Assert.AreEqual(120, state.CurrentTrophies);
        var second = Apply(TrophyMatchOutcome.Defeat);
        Assert.AreEqual(-5, second.RecordedMatch.AppliedDelta); Assert.AreEqual(115, state.CurrentTrophies);
        Assert.AreEqual(150, state.HighestTrophies);
    }
    [Test] public void ZeroFloorRecordsDefeatWithoutNegativeScoreOrLaterDuplicateLoss()
    {
        var loss = Receipt(TrophyMatchOutcome.Defeat); state = engine.Record(state, loss, 0).State;
        Assert.AreEqual(0, state.CurrentTrophies); Assert.AreEqual(1, state.Revision); Wins(1);
        var retry = engine.Record(state, loss, 0);
        Assert.AreEqual(TrophyLadderStatus.AlreadyApplied, retry.Status); Assert.AreEqual(30, retry.State.CurrentTrophies);
        Assert.AreEqual(0, retry.RecordedMatch.AppliedDelta); Assert.AreEqual(2, retry.State.Revision);
    }
    [Test] public void RetriedFinalStageLossKeepsOriginalAdjustmentAfterLaterPromotion()
    {
        Wins(5); var loss = Receipt(TrophyMatchOutcome.Defeat); state = engine.Record(state, loss, 5).State;
        Wins(2); Assert.AreEqual(180, state.CurrentTrophies);
        var retry = engine.Record(state, loss, 5);
        Assert.AreEqual(TrophyLadderStatus.AlreadyApplied, retry.Status); Assert.AreEqual(-30, retry.RecordedMatch.AppliedDelta);
        Assert.AreEqual(180, retry.State.CurrentTrophies); Assert.AreEqual(8, retry.State.Revision);
    }
    [Test] public void CopasContinueAfterLastStageWithoutAddingBoardsOrIncludingHotel()
    {
        Wins(20); var preview = engine.PreviewStage(state);
        Assert.AreEqual(600, state.CurrentTrophies); Assert.AreEqual("royal", preview.BoardId);
        Assert.AreEqual(2, preview.ReachedStages.Length); Assert.IsFalse(preview.ReachedStages.Any(s => s.BoardId == "lounge"));
        Assert.IsNull(preview.NextStageAt);
    }
    [Test] public void OpponentDifferenceAndWinStreakNeverChangeTheTrialAdjustment()
    {
        Wins(2);
        foreach (long rival in new[] { 0L, 9999L })
        {
            var win = engine.Record(state, Receipt(TrophyMatchOutcome.Victory, rival), state.Revision);
            var loss = engine.Record(state, Receipt(TrophyMatchOutcome.Defeat, rival), state.Revision);
            Assert.AreEqual(30, win.RecordedMatch.AppliedDelta); Assert.AreEqual(-5, loss.RecordedMatch.AppliedDelta);
        }
        Wins(5);
        foreach (long rival in new[] { 0L, 9999L })
        {
            var win = engine.Record(state, Receipt(TrophyMatchOutcome.Victory, rival), state.Revision);
            var loss = engine.Record(state, Receipt(TrophyMatchOutcome.Defeat, rival), state.Revision);
            Assert.AreEqual(30, win.RecordedMatch.AppliedDelta); Assert.AreEqual(-30, loss.RecordedMatch.AppliedDelta);
        }
    }
    [Test] public void FactoryReturnsIndependentTrialPoliciesWithoutChangingAState()
    {
        var policy = TrophyLadderTrialConfiguration.CreatePolicy(); policy.Stages[0].DefeatLossOverride = 999;
        policy.DifferenceBands[0].WinGain = 999;
        var second = TrophyLadderTrialConfiguration.CreatePolicy();
        Assert.AreEqual(5, second.Stages[0].DefeatLossOverride); Assert.AreEqual(30, second.DifferenceBands[0].WinGain);
        Assert.AreEqual(0, state.CurrentTrophies); Assert.IsEmpty(state.Matches);
        Assert.That(second.PolicyId, Does.StartWith("internal-qa."));
    }
}

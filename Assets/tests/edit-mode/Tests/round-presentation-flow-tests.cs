using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class RoundPresentationFlowTests
    {
        GameObject root;
        MatchConfig config;
        MatchController match;

        [SetUp] public void Setup()
        {
            root = new GameObject("round-presentation-flow");
            config = MatchConfig.CreateDefault(); config.RoundIncome = new[] { 0 };
            var board = root.AddComponent<BoardManager>(); board.BuildBoard();
            var mapper = root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, root.transform, Vector2.one, Vector2.zero);
            match = root.AddComponent<MatchController>(); match.Configure(config, board, mapper);
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(config); Time.timeScale = 1;
        }

        void FinishRound(BoardSide winner)
        {
            match.Ready();
            foreach (var actor in match.Actors.Values.Where(actor => actor.Side != winner).ToArray())
                actor.ApplyDamage(actor.CurrentHealth);
            match.Advance(.1f);
            Assert.AreEqual(winner, match.LastRoundWinner);
            Assert.AreEqual(0, match.RoundResultElapsed);
        }

        [Test]
        public void TimelineDefaultsAndInvalidValuesRemainUsable()
        {
            var settings = new RoundPresentationSettings();
            Assert.AreEqual(3, settings.WinsToWin);
            Assert.AreEqual(.42f, settings.StampStartTime, .00001f);
            Assert.AreEqual(1.74f, settings.TransitionDuration, .00001f);
            Assert.AreEqual(4.62f, settings.StampStartTimeFor(true), .00001f);
            Assert.AreEqual(5.94f, settings.TransitionDurationFor(true), .00001f);
            settings.timeoutBannerDuration = float.NaN;
            Assert.AreEqual(1.2f, settings.TimeoutBannerDuration);
            settings.maxRoundsToWin = 99; Assert.AreEqual(5, settings.WinsToWin);
            settings.maxRoundsToWin = 0; Assert.AreEqual(1, settings.WinsToWin);
            settings.healthComparisonDuration = float.NaN;
            Assert.AreEqual(3.2f, settings.HealthDuration);
            settings.delayBeforeBetweenRoundsUI = -2;
            settings.betweenRoundsPanelAnimationDuration = float.NaN;
            settings.flagStampAnimationDuration = float.PositiveInfinity;
            settings.delayAfterFlagStamp = -10;
            settings.nextRoundStartDelay = float.NegativeInfinity;
            Assert.AreEqual(0, settings.DelayBeforeUI); Assert.AreEqual(.22f, settings.PanelDuration);
            Assert.AreEqual(.42f, settings.StampDuration); Assert.AreEqual(0, settings.AfterStampDelay);
            Assert.AreEqual(.25f, settings.NextRoundDelay);
            Assert.IsFalse(float.IsNaN(settings.TransitionDuration));
            Assert.IsFalse(float.IsInfinity(settings.TransitionDuration));
        }

        [Test]
        public void OfflinePlayerMapsAreStoredAndSurviveRematch()
        {
            Assert.IsTrue(match.StartMatch("popow",null,123,"royal","lounge"));
            Assert.AreEqual("royal",match.MapForSide(BoardSide.Blue));
            Assert.AreEqual("lounge",match.MapForSide(BoardSide.Red));
            FinishRound(BoardSide.Blue);match.Rematch();
            Assert.AreEqual("royal",match.MapForSide(BoardSide.Blue));
            Assert.AreEqual("lounge",match.MapForSide(BoardSide.Red));
            match.Menu();Assert.IsFalse(match.StartMatch("popow",null,123,"unknown"));
        }

        void StartPassiveCombat()
        {
            match.StartMatch("popow", 123); match.Ready();
            foreach (var actor in match.Actors.Values)
                actor.Initialize(actor.UnitId, actor.Side, new UnitStats(101, 0, 99, 100, 100));
            match.Simulation.Begin(match.Actors.Values.ToArray());
        }

        [Test]
        public void TimeoutWithBothTeamsStandingWaitsForBannerThenHealthThenFlags()
        {
            StartPassiveCombat(); match.Advance(40);
            Assert.AreEqual(0, match.Remaining);
            Assert.IsTrue(match.UsesHealthTiebreaker);
            Assert.AreEqual(config.RoundUI.TimeoutBannerDuration, match.RoundHealthStartTime);
            Assert.AreEqual(match.RoundHealthStartTime + config.RoundUI.HealthDuration, match.RoundCardsStartTime);
            Assert.AreEqual(match.RoundCardsStartTime + config.RoundUI.PanelDuration, match.RoundStampStartTime);
            match.Advance(config.RoundUI.TransitionDuration + .01f);
            Assert.AreEqual(MatchPhase.RoundResult, match.Phase, "Timeout must not skip its extended sequence.");
            match.Advance(match.RoundTransitionDuration - match.RoundResultElapsed + .01f);
            Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            Assert.IsFalse(match.UsesHealthTiebreaker);
        }

        [TestCase(.1f)] [TestCase(40f)]
        public void EliminationEvenOnTheLastTickSkipsTimeoutAndHealth(float endTime)
        {
            StartPassiveCombat(); match.Advance(endTime - .1f);
            foreach (var actor in match.Actors.Values.Where(a => a.Side == BoardSide.Red))
                actor.ApplyDamage(actor.CurrentHealth);
            match.Advance(.1f);
            Assert.IsNull(match.Simulation.TimeoutResult);
            Assert.IsFalse(match.UsesHealthTiebreaker);
            Assert.AreEqual(config.RoundUI.DelayBeforeUI, match.RoundCardsStartTime);
            Assert.AreEqual(config.RoundUI.TransitionDuration, match.RoundTransitionDuration);
        }

        [Test]
        public void ScoreIsCommittedBeforeResultNotificationAndNextRoundStartsWithoutAnyUI()
        {
            var edited = config.Get("bugaloo");
            edited.MaxHealth = 137; edited.Damage = 17; edited.AttackWindup = .8f; edited.AttackInterval = 2.7f;
            var originalDefinitions = config.Units.ToArray();
            config.RoundUI.delayBeforeBetweenRoundsUI = .11f;
            config.RoundUI.betweenRoundsPanelAnimationDuration = .23f;
            config.RoundUI.flagStampAnimationDuration = .37f;
            config.RoundUI.delayAfterFlagStamp = .41f;
            config.RoundUI.nextRoundStartDelay = .53f;
            match.StartMatch("bugaloo", 123);
            int resultNotifications = 0;
            match.Changed += () => {
                if (match.Phase != MatchPhase.RoundResult) return;
                resultNotifications++;
                Assert.AreEqual(1, match.PlayerWins); Assert.AreEqual(0, match.BotWins);
                Assert.AreEqual(BoardSide.Blue, match.LastRoundWinner);
                Assert.AreEqual(0, match.RoundResultElapsed);
            };
            FinishRound(BoardSide.Blue);
            Assert.AreEqual(1, resultNotifications);
            Assert.AreEqual(1, match.Round);
            match.Advance(config.RoundUI.TransitionDuration - .01f);
            Assert.AreEqual(MatchPhase.RoundResult, match.Phase);
            match.Advance(.02f);
            Assert.AreEqual(MatchPhase.Preparation, match.Phase); Assert.AreEqual(2, match.Round);
            Assert.AreEqual(40, match.Remaining); Assert.AreEqual(0, match.RoundResultElapsed);
            Assert.IsNull(match.LastRoundWinner); Assert.IsNull(match.Simulation);
            Assert.AreEqual(1, match.PlayerWins);
            CollectionAssert.AreEqual(originalDefinitions, config.Units);
            Assert.AreEqual(137, edited.MaxHealth); Assert.AreEqual(17, edited.Damage);
            Assert.AreEqual(.8f, edited.AttackWindup); Assert.AreEqual(2.7f, edited.AttackInterval);
            Assert.AreEqual(137, match.Actors[match.Player.Monster].CurrentHealth);
        }

        [Test]
        public void PausingTheResultFreezesItsTimelineAndResumingContinuesIt()
        {
            match.StartMatch("bugaloo", 123); FinishRound(BoardSide.Blue);
            match.Advance(.3f); match.Pause(true); match.Advance(100);
            Assert.AreEqual(.3f, match.RoundResultElapsed, .00001f);
            Assert.AreEqual(MatchPhase.RoundResult, match.Phase); Assert.AreEqual(1, match.Round);
            match.Pause(false); match.Advance(config.RoundUI.TransitionDuration - .3f + .01f);
            Assert.AreEqual(MatchPhase.Preparation, match.Phase); Assert.AreEqual(2, match.Round);
            Assert.AreEqual(0, match.RoundResultElapsed);
        }

        [TestCase(BoardSide.Blue, true)] [TestCase(BoardSide.Red, false)]
        public void FirstThreeWinsEndTheSeriesAndFinalResultNeverAutoStartsAnotherRound(BoardSide winner, bool alternating)
        {
            match.StartMatch("bugaloo", 123); Assert.AreEqual(3, match.WinsToWin);
            int finalRound = alternating ? 5 : 3;
            BoardSide other = winner == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;
            for (int round = 1; round <= finalRound; round++)
            {
                FinishRound(alternating && round % 2 == 0 ? other : winner);
                if (round < finalRound)
                {
                    Assert.AreEqual(MatchPhase.RoundResult, match.Phase);
                    match.Advance(config.RoundUI.TransitionDuration);
                    Assert.AreEqual(MatchPhase.Preparation, match.Phase);
                }
            }
            Assert.AreEqual(MatchPhase.MatchResult, match.Phase);
            Assert.AreEqual(3, winner == BoardSide.Blue ? match.PlayerWins : match.BotWins);
            Assert.AreEqual(alternating ? 2 : 0, winner == BoardSide.Blue ? match.BotWins : match.PlayerWins);
            match.Advance(100); match.NextRound();
            Assert.AreEqual(MatchPhase.MatchResult, match.Phase); Assert.AreEqual(finalRound, match.Round);
            Assert.AreEqual(winner, match.LastRoundWinner); Assert.AreEqual(100, match.RoundResultElapsed);
        }

        [Test]
        public void MenuAndRematchClearTheOldWinnerAndResultClock()
        {
            match.StartMatch("bugaloo", 123); FinishRound(BoardSide.Blue); match.Advance(.5f);
            match.Menu(); Assert.IsNull(match.LastRoundWinner); Assert.AreEqual(0, match.RoundResultElapsed);
            match.StartMatch("popow", 456); FinishRound(BoardSide.Red); match.Advance(.5f);
            match.Rematch();
            Assert.AreEqual(1, match.Round); Assert.AreEqual(0, match.PlayerWins); Assert.AreEqual(0, match.BotWins);
            Assert.AreEqual(0, match.RoundResultElapsed); Assert.IsNull(match.LastRoundWinner);
            Assert.AreEqual(MatchPhase.Preparation, match.Phase);
        }
    }
}

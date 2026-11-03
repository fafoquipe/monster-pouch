using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>The controller owns this timeline; visual panels only display its progress.</summary>
    [Serializable]
    public sealed class RoundPresentationSettings
    {
        [Range(1, 5)] public int maxRoundsToWin = 3;
        [Min(0)] public float delayBeforeBetweenRoundsUI = .2f;
        [Min(0)] public float betweenRoundsPanelAnimationDuration = .22f;
        [Min(0)] public float flagStampAnimationDuration = .42f;
        [Min(0)] public float delayAfterFlagStamp = .65f;
        [Min(0)] public float nextRoundStartDelay = .25f;
        [Min(0)] public float victoryConfettiDuration = 3f;
        [Min(0)] public float timeoutBannerDuration = 1.2f;
        [Min(0)] public float healthComparisonDuration = 3.2f;
        public Vector2 timerPositionOffset = Vector2.zero;
        public Vector2 roundLabelPositionOffset = Vector2.zero;
        [Min(0)] public float spacingBetweenFlags = 7f;
        public Vector2 enemyPanelPosition = new Vector2(18, 16);
        public Vector2 playerPanelPosition = new Vector2(338, 704);

        public int WinsToWin => Mathf.Clamp(maxRoundsToWin, 1, 5);
        public float DelayBeforeUI => Duration(delayBeforeBetweenRoundsUI, .2f);
        public float PanelDuration => Duration(betweenRoundsPanelAnimationDuration, .22f);
        public float StampDuration => Duration(flagStampAnimationDuration, .42f);
        public float AfterStampDelay => Duration(delayAfterFlagStamp, .65f);
        public float NextRoundDelay => Duration(nextRoundStartDelay, .25f);
        public float ConfettiDuration => Duration(victoryConfettiDuration, 3f);
        public float HealthDuration => Duration(healthComparisonDuration, 3.2f);
        public float TimeoutBannerDuration => Duration(timeoutBannerDuration, 1.2f);
        public float CardsStartTime(bool healthTiebreaker) => healthTiebreaker
            ? TimeoutBannerDuration + HealthDuration : DelayBeforeUI;
        public float StampStartTimeFor(bool healthTiebreaker) => CardsStartTime(healthTiebreaker) + PanelDuration;
        public float TransitionDurationFor(bool healthTiebreaker) =>
            StampStartTimeFor(healthTiebreaker) + StampDuration + AfterStampDelay + NextRoundDelay;
        public float StampStartTime => StampStartTimeFor(false);
        public float TransitionDuration => TransitionDurationFor(false);

        private static float Duration(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0, value);
    }
}

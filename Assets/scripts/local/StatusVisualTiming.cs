using System;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Local
{
    // Presentation arithmetic only. The core supplies the winner and its interval.
    // This type does not track, apply, expire, arbitrate, or cache gameplay effects.
    public readonly struct StatusVisualTiming
    {
        public readonly bool HasClock;
        public readonly float Fraction, AgeSeconds;

        StatusVisualTiming(bool hasClock, float fraction, float ageSeconds)
        { HasClock = hasClock; Fraction = fraction; AgeSeconds = ageSeconds; }

        // Core contract 1.3-1.5. Consume its actual type rather than creating a
        // second status DTO or reconstructing an interval from first observation.
        public static StatusVisualTiming FromSnapshot(CombatStatusSnapshot snapshot) =>
            FromSnapshot(snapshot.AppliedTick, snapshot.ExpiresTick,
                snapshot.RemainingSeconds, snapshot.IsPermanent ||
                snapshot.TimingKind != CombatStatusTimingKind.Timed, .1f);

        public static StatusVisualTiming FromSnapshot(int appliedTick, int expiresTick,
            float remainingSeconds, bool isPermanent, float secondsPerTick)
        {
            if (isPermanent || remainingSeconds < 0 || expiresTick == int.MaxValue)
                return new StatusVisualTiming(false, 1, 0);
            double total = ((long)expiresTick - appliedTick) * (double)secondsPerTick;
            if (total <= 0 || double.IsNaN(total) || double.IsInfinity(total) ||
                float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds))
                return new StatusVisualTiming(false, 1, 0);
            double remaining = Math.Max(0, Math.Min(total, remainingSeconds));
            return new StatusVisualTiming(true, (float)(remaining / total),
                (float)(total - remaining));
        }
    }
}

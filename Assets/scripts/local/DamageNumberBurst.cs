using System;

namespace MonsterPouch.Local
{
    // Only presentation state. Impact timestamps come from CombatSimulation.Elapsed;
    // animation age advances separately, so catch-up ticks and pauses keep their meaning.
    public struct DamageNumberBurst
    {
        public const float BufferSeconds = .18f;
        public const float GapSeconds = .5f;
        public const float SegmentSeconds = 1f;
        const float ReadableAfterChange = .55f;
        const float TickTolerance = .00001f;
        public long Amount { get; private set; }
        public bool Critical { get; private set; }
        public bool Visible { get; private set; }
        public bool Closed { get; private set; }
        public float Age { get; private set; }
        public float FirstImpact { get; private set; }
        public float LastImpact { get; private set; }
        float shownAt, changedAt;
        public float VisibleAge => Visible ? Age - shownAt : 0;
        public float Duration => Critical ? 1.2f : .95f;
        public float FadeAge => Math.Min(VisibleAge, Duration - ReadableAfterChange + Age - changedAt);
        public bool Expired => Visible && VisibleAge >= Duration && Age - changedAt >= ReadableAfterChange;

        public void Begin(int amount, float impactTime)
        {
            this = default;
            Amount = amount; FirstImpact = LastImpact = impactTime;
        }
        public bool TryAdd(int amount, float impactTime)
        {
            if (amount <= 0 || Closed || impactTime < LastImpact - TickTolerance ||
                impactTime - LastImpact > GapSeconds + TickTolerance ||
                impactTime - FirstImpact >= SegmentSeconds - TickTolerance) return false;
            Amount += amount; LastImpact = impactTime; changedAt = Age;
            return true;
        }
        public void MarkCritical() { Critical = true; }
        public void Show() { if (!Visible) { Visible = true; shownAt = Age; } }
        public void Close() { Closed = true; Show(); }
        public void Advance(float delta)
        {
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            Age += delta;
            if (!Visible && Age >= BufferSeconds) Show();
        }
    }
}

using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public enum GestureResult { None, Tap, Hold, Drag }
    // Shared by board, mouse and touch UI. A gesture commits exactly once.
    public sealed class PointerGesture
    {
        public const float FeedbackDelay = .4f;
        public const float FeedbackDuration = .3f;
        public const float HoldDuration = FeedbackDelay + FeedbackDuration;
        public bool Active { get; private set; }
        public bool Dragging { get; private set; }
        public bool Held { get; private set; }
        Vector2 origin;
        float started;
        public bool ShowHoldFeedback(float now) => Active && !Dragging && !Held && now - started >= FeedbackDelay;
        public float HoldProgress(float now) => Active && !Dragging ? Mathf.Clamp01((now - started - FeedbackDelay) / FeedbackDuration) : 0;
        public void Begin(Vector2 position, float now) { origin = position; started = now; Active = true; Dragging = Held = false; }
        public GestureResult Move(Vector2 position, float now, float threshold = 12)
        {
            if (!Active || Held) return GestureResult.None;
            if (Vector2.Distance(origin, position) > threshold) Dragging = true;
            if (!Dragging && now - started >= HoldDuration) { Held = true; return GestureResult.Hold; }
            return GestureResult.None;
        }
        public GestureResult End()
        {
            if (!Active) return GestureResult.None;
            Active = false;
            return Held ? GestureResult.None : Dragging ? GestureResult.Drag : GestureResult.Tap;
        }
        public void Cancel() { Active = Dragging = Held = false; }
    }
}

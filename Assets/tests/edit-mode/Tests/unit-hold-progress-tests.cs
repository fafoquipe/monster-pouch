using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

public sealed class UnitHoldProgressTests
{
    [Test] public void FeedbackWaitsFourTenthsThenCountsDownForThreeTenths()
    {
        var gesture = new PointerGesture();
        Assert.AreEqual(.4f, PointerGesture.FeedbackDelay);
        Assert.AreEqual(.3f, PointerGesture.FeedbackDuration);
        Assert.AreEqual(.7f, PointerGesture.HoldDuration,.0001f);
        Assert.AreEqual(0, gesture.HoldProgress(10));
        gesture.Begin(Vector2.zero, 10);
        Assert.AreEqual(0, gesture.HoldProgress(10));
        Assert.IsFalse(gesture.ShowHoldFeedback(10.399f));
        Assert.AreEqual(0,gesture.HoldProgress(10.399f));
        Assert.IsTrue(gesture.ShowHoldFeedback(10.401f));
        Assert.AreEqual(.5f, gesture.HoldProgress(10.55f), .0001f);
        Assert.AreEqual(1, gesture.HoldProgress(10.701f));
        Assert.AreEqual(1, gesture.HoldProgress(100));
        Assert.AreEqual(0, gesture.HoldProgress(9));
    }

    [Test] public void ThresholdCommitsInspectionOnceAndReleaseCannotBecomeATap()
    {
        var gesture = new PointerGesture();
        gesture.Begin(Vector2.zero, 1);
        Assert.AreEqual(GestureResult.None, gesture.Move(Vector2.zero, 1.699f));
        Assert.AreEqual(GestureResult.Hold, gesture.Move(Vector2.zero, 1.701f));
        Assert.IsFalse(gesture.ShowHoldFeedback(1.71f));
        Assert.AreEqual(GestureResult.None, gesture.Move(Vector2.zero, 3));
        Assert.AreEqual(GestureResult.None, gesture.End());
        Assert.AreEqual(GestureResult.None, gesture.End());
        Assert.AreEqual(0, gesture.HoldProgress(2));
    }

    [Test] public void CrossingDragThresholdPermanentlyCancelsHoldUntilANewPress()
    {
        var gesture = new PointerGesture();
        gesture.Begin(Vector2.zero, 2);
        gesture.Move(new Vector2(13, 0), 2.1f);
        Assert.IsTrue(gesture.Dragging);
        Assert.AreEqual(0, gesture.HoldProgress(2.3f));
        Assert.AreEqual(GestureResult.None, gesture.Move(Vector2.zero, 3));
        Assert.AreEqual(GestureResult.Drag, gesture.End());
        Assert.AreEqual(GestureResult.None, gesture.End());
        gesture.Begin(Vector2.zero, 4);
        Assert.AreEqual(0, gesture.HoldProgress(4));
        Assert.AreEqual(GestureResult.Hold, gesture.Move(Vector2.zero, 4.701f));
    }

    [Test] public void EarlyReleaseAndCancellationDiscardProgressWithoutTriggeringInspection()
    {
        var gesture = new PointerGesture();
        gesture.Begin(Vector2.zero, 0);
        Assert.AreEqual(GestureResult.Tap, gesture.End());
        Assert.AreEqual(0, gesture.HoldProgress(10));
        gesture.Begin(Vector2.zero, 10);
        Assert.AreEqual(.5f, gesture.HoldProgress(10.55f),.0001f);
        gesture.Cancel();
        Assert.IsFalse(gesture.Active);
        Assert.AreEqual(0, gesture.HoldProgress(11));
        Assert.AreEqual(GestureResult.None, gesture.Move(Vector2.zero, 11));
        Assert.AreEqual(GestureResult.None, gesture.End());
        gesture.Begin(Vector2.zero, 20);
        Assert.AreEqual(0, gesture.HoldProgress(20));
    }

    [Test] public void ExactFeedbackAndHoldBoundariesKeepTheExistingTwelvePixelDragThreshold()
    {
        var gesture = new PointerGesture();
        gesture.Begin(Vector2.zero, 0);
        Assert.IsTrue(gesture.ShowHoldFeedback(PointerGesture.FeedbackDelay));
        Assert.AreEqual(0, gesture.HoldProgress(PointerGesture.FeedbackDelay));
        Assert.AreEqual(GestureResult.Hold, gesture.Move(new Vector2(12, 0), PointerGesture.HoldDuration));
        Assert.AreEqual(GestureResult.None, gesture.Move(new Vector2(40, 0), 1));
        Assert.AreEqual(GestureResult.None, gesture.End());
        gesture.Begin(Vector2.zero, 0);
        Assert.AreEqual(GestureResult.None, gesture.Move(new Vector2(12.01f, 0), PointerGesture.HoldDuration));
        Assert.IsTrue(gesture.Dragging);
        Assert.AreEqual(GestureResult.Drag, gesture.End());
    }
}

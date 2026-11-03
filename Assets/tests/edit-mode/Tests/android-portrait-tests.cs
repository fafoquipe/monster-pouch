using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class AndroidPortraitTests
{
    [TestCase(360,640)][TestCase(360,800)][TestCase(393,873)][TestCase(412,915)][TestCase(800,1280)][TestCase(915,412)]
    public void BoardAndControlsFitInsideNotchAndGestureInsetsWithoutStretching(int width,int height)
    {
        var safe=new Rect(8,24,width-16,height-56);
        var view=PortraitViewport.Fit(new Vector2(width,height),safe,out float scale);
        Assert.GreaterOrEqual(view.xMin,safe.xMin-.01f);Assert.LessOrEqual(view.xMax,safe.xMax+.01f);
        Assert.GreaterOrEqual(view.yMin,safe.yMin-.01f);Assert.LessOrEqual(view.yMax,safe.yMax+.01f);
        Assert.That(view.width/view.height,Is.EqualTo(540f/960).Within(.00001));
        Assert.That(Vector2.Distance(view.center,safe.center),Is.LessThan(.001));Assert.Greater(scale,0);
    }
    [Test] public void InvalidSafeAreaFallsBackToScreenDuringAndroidWindowResize()
    {
        var view=PortraitViewport.Fit(new Vector2(360,800),new Rect(float.NaN,0,0,0),out float scale);
        Assert.That(view.width,Is.EqualTo(360).Within(.01));Assert.IsFalse(float.IsNaN(scale));
    }
}

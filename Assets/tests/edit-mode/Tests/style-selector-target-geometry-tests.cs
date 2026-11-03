using System.Linq;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class StyleSelectorTargetGeometryTests
{
    // Current selector button rectangles in the original 540x960 composition.
    static readonly Rect[] Buttons = {
        new Rect(28,36,196,68), new Rect(236,36,196,68), new Rect(444,36,68,68),
        new Rect(28,116,236,68), new Rect(276,116,236,68),
        new Rect(148,344,356,68), new Rect(148,542,356,68), new Rect(148,740,356,68)
    };
    [TestCase(320,568)][TestCase(360,640)][TestCase(360,800)][TestCase(393,873)]
    [TestCase(412,915)][TestCase(768,1024)][TestCase(800,1280)]
    public void CurrentSelectorTargetsReach44PixelsInsideSafePortraitWithoutOverlap(int width, int height)
    {
        var safe = new Rect(8,24,width-16,height-56);
        var portrait = PortraitViewport.Fit(new Vector2(width,height),safe,out float scale);
        var visuals = Buttons.Select(rect => new Rect(portrait.x + rect.x * scale,
            portrait.yMax - rect.yMax * scale, rect.width * scale, rect.height * scale)).ToArray();
        var targets = new Rect[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
        {
            var pad = StyleSelectorTouchTargets.CalculatePadding(visuals,i,portrait,Vector2.one*scale);
            Rect visual = visuals[i];
            targets[i] = Rect.MinMaxRect(visual.xMin + pad.x*scale,visual.yMin + pad.y*scale,
                visual.xMax - pad.z*scale,visual.yMax - pad.w*scale);
            Assert.GreaterOrEqual(targets[i].width,43.99f,"Target "+i);
            Assert.GreaterOrEqual(targets[i].height,43.99f,"Target "+i);
            Assert.GreaterOrEqual(targets[i].xMin,portrait.xMin-.01f); Assert.LessOrEqual(targets[i].xMax,portrait.xMax+.01f);
            Assert.GreaterOrEqual(targets[i].yMin,portrait.yMin-.01f); Assert.LessOrEqual(targets[i].yMax,portrait.yMax+.01f);
        }
        for (int i = 0; i < targets.Length; i++) for (int j = i+1; j < targets.Length; j++)
            Assert.IsFalse(targets[i].Overlaps(targets[j]),"Targets "+i+" and "+j+" overlap.");
    }

    [Test] public void OldFullWidthAssumptionMissesSmallPhoneSafeAreaDefect()
    {
        PortraitViewport.Fit(new Vector2(320,568),new Rect(8,24,304,512),out float small);
        PortraitViewport.Fit(new Vector2(360,640),new Rect(8,24,344,584),out float compact);
        Assert.That(68*small,Is.EqualTo(36.26667f).Within(.001f));
        Assert.That(68*compact,Is.EqualTo(41.36667f).Within(.001f));
        Assert.Less(68*compact,44); Assert.Greater(68*(360f/540f),44,"The previous test used full width and missed the inset.");
    }
}

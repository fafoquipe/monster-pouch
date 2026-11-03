using System.Reflection;
using MonsterPouch.Local.Boards;
using NUnit.Framework;
using UnityEngine;

public sealed class BoardPresentationSafeAreaTests
{
    static Rect Calculate(Rect safe,Vector2 size)
    {
        var method=typeof(BoardProgressionScreen).GetMethod("CalculatePortraitRect",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.IsNotNull(method,"Use the same calculation as the rendered board screen.");
        return (Rect)method.Invoke(null,new object[]{safe,size});
    }

    static float Scale(Rect safe,Vector2 size)
    {
        var method=typeof(BoardProgressionScreen).GetMethod("CalculateCanvasScale",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.IsNotNull(method,"Use the same density calculation as the rendered board screen.");
        return (float)method.Invoke(null,new object[]{safe,size});
    }

    [TestCase(320,568,0,24,320,520)]
    [TestCase(393,852,0,34,393,771)]
    [TestCase(768,1024,16,24,736,944)]
    [TestCase(800,1280,0,24,800,1220)]
    [TestCase(1024,768,0,0,1024,768)]
    [TestCase(1280,720,0,0,1280,720)]
    [TestCase(1024,768,10.2f,15.7f,997.1f,713.2f)]
    [TestCase(320,568,-12,-24,344,616)]
    [TestCase(1080,2400,0,0,1080,2400)]
    [TestCase(1440,3200,0,0,1440,3200)]
    [TestCase(1536,2048,0,0,1536,2048)]
    [TestCase(1080,2400,0,96,1080,2208)]
    [TestCase(1440,3200,0,128,1440,2944)]
    [TestCase(1536,2048,32,48,1472,1904)]
    public void PortraitContentStaysInsideAsymmetricSafeInsetsWithWholeLogicalDimensions(
        float width,float height,float x,float y,float safeWidth,float safeHeight)
    {
        var safe=new Rect(x,y,safeWidth,safeHeight);
        Rect actual=Calculate(safe,new Vector2(width,height));
        float scale=Scale(safe,new Vector2(width,height));
        Assert.GreaterOrEqual(scale,1);
        Assert.Greater(actual.width,0);Assert.Greater(actual.height,0);
        Assert.LessOrEqual(actual.width/scale,540);
        Assert.GreaterOrEqual(actual.xMin,Mathf.Max(0,safe.xMin));
        Assert.GreaterOrEqual(actual.yMin,Mathf.Max(0,safe.yMin));
        Assert.LessOrEqual(actual.xMax,Mathf.Min(width,safe.xMax));
        Assert.LessOrEqual(actual.yMax,Mathf.Min(height,safe.yMax));
        foreach(float dimension in new[]{actual.width/scale,actual.height/scale})
            Assert.That(dimension,Is.EqualTo(Mathf.Round(dimension)).Within(.001f),"The layout uses whole logical dimensions; fractional Canvas scale is valid.");
        float usableLeft=Mathf.Ceil(Mathf.Max(0,safe.xMin)),usableRight=Mathf.Floor(Mathf.Min(width,safe.xMax));
        Assert.That(actual.center.x,Is.EqualTo((usableLeft+usableRight)*.5f).Within(1));
    }

    [TestCase(320,568,1,320)]
    [TestCase(1080,2400,3,360)]
    [TestCase(1440,3200,4,360)]
    [TestCase(1536,2048,3.2f,360)]
    public void DenseDisplaysKeepTheReadableLogicalColumnInsteadOfAPhysicalPixelCap(
        float width,float height,float expectedScale,float expectedLogicalWidth)
    {
        var size=new Vector2(width,height);var safe=new Rect(0,0,width,height);
        float scale=Scale(safe,size);Rect actual=Calculate(safe,size);
        Assert.That(scale,Is.EqualTo(expectedScale).Within(.0001f));
        Assert.That(actual.width/scale,Is.EqualTo(expectedLogicalWidth).Within(1));
        if(width>=1080)Assert.Greater(actual.width,1000,"A dense screen must not leave the UI trapped in a 540-physical-pixel column.");
    }

    [Test] public void MissingSafeAreaUsesTheScreenWithoutExpandingBeyondItsEdges()
    {
        Rect actual=Calculate(Rect.zero,new Vector2(360,640));
        Assert.AreEqual(new Rect(0,0,360,640),actual);
    }
}

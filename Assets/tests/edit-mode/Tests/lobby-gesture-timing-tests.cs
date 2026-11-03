using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
public sealed class CurrentTileHopProfileTests
{
    [Test] public void AtongPreparesLongerThanHeFliesAndShortLegsKeepLowHops()
    {
        var atong=TileHopProfile.For("atong");Assert.Greater(atong.Prepare,atong.Air);
        Assert.LessOrEqual(TileHopProfile.For("jazar").Height,.07f);
        Assert.LessOrEqual(TileHopProfile.For("dummy").Height,.1f);
        Assert.IsTrue(TileHopProfile.For("anuik").Walk);
    }
}

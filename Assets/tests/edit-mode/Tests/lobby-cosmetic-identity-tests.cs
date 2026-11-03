using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;

public sealed class LobbyCosmeticIdentityTests
{
    [Test] public void ExistingSavedValuesRemainExactAndAireolaIsIndependent()
    {
        foreach (string id in new[] { "classic", "lounge", "royal", "naturalis", "aquarion", "geothar", "volkanar", "casino-night" })
            Assert.AreEqual(id, LobbyThemeIdentity.Resolve(id));
        Assert.AreEqual("aireola", LobbyThemeIdentity.Resolve("aireola"));
        Assert.AreNotEqual(LobbyThemeIdentity.Resolve("royal"), LobbyThemeIdentity.Resolve("aireola"));
        Assert.AreEqual(9, LobbyThemeIdentity.Ids.Count);
        Assert.AreEqual(9, LobbyThemeIdentity.Ids.Distinct().Count());
    }
    [TestCase(393,852)]
    [TestCase(768,1024)]
    [TestCase(1280,720)]
    [TestCase(2520,1080)]
    [TestCase(3440,1440)]
    [TestCase(5120,1440)]
    public void CleanScenesCoverNormalScreensAndKeepOnlyExtremeSideMarginsWithoutMovingPedestals(int screenWidth,int screenHeight)
    {
        float scale=UnityEngine.Mathf.Min(screenWidth/540f,screenHeight/960f);
        var visible=UnityEngine.Rect.MinMaxRect(270-screenWidth/(2*scale),480-screenHeight/(2*scale),
            270+screenWidth/(2*scale),480+screenHeight/(2*scale));
        foreach(string id in LobbyThemeIdentity.Ids)
        {
            var art=LobbyArtwork.Find(id);Assert.IsNotNull(art,id);
            Assert.IsNotNull(art.Background,id);Assert.IsNotNull(art.Pedestal,id);
            Assert.AreNotSame(art.Background.texture,art.Pedestal.texture,id);
            var frame=art.Frame(visible);
            Assert.Greater(frame.height,0,id);
            Assert.AreEqual(art.Background.rect.width/art.Background.rect.height,frame.width/frame.height,.00001f,id);
            float coveredWidth=UnityEngine.Mathf.Min(visible.width,visible.height*LobbyArtwork.MaximumCoverAspect);
            Assert.LessOrEqual(frame.xMin,visible.center.x-coveredWidth*.5f+.001f,id);
            Assert.GreaterOrEqual(frame.xMax,visible.center.x+coveredWidth*.5f-.001f,id);
            Assert.LessOrEqual(frame.yMin,visible.yMin+.001f,id);Assert.GreaterOrEqual(frame.yMax,visible.yMax-.001f,id);
            if((long)screenWidth * 9 > (long)screenHeight * 21)
            {
                Assert.AreEqual(visible.height*LobbyArtwork.MaximumCoverAspect,frame.width,.001f,id);
                Assert.Greater(frame.xMin,visible.xMin,id);Assert.Less(frame.xMax,visible.xMax,id);
            }
            Assert.AreEqual(visible.center.x,frame.center.x,.001f,id);Assert.AreEqual(visible.center.y,frame.center.y,.001f,id);
            foreach(var feet in new[]{new UnityEngine.Vector2(270,552),new UnityEngine.Vector2(270,291)})
            {
                var pedestal=art.PedestalFrame(feet);
                Assert.AreEqual(art.Pedestal.rect.width/art.Pedestal.rect.height,pedestal.width/pedestal.height,.00001f,id);
                Assert.AreEqual(feet.x,pedestal.x+pedestal.width*art.PedestalFootAnchor.x,.001f,id);
                Assert.AreEqual(feet.y,pedestal.y+pedestal.height*art.PedestalFootAnchor.y,.001f,id);
                Assert.AreEqual(art.DisplayPedestalWidth,pedestal.width,.001f,id);
                Assert.AreEqual(frame,art.Frame(visible),"Moving a character must not reframe the scene.");
            }
        }
    }

    [Test] public void UnknownPreferencesKeepTheExistingNaturalisFallback()
    {
        foreach (string value in new[] { null, "", "unknown", "AIREOLA", "aireola " })
        {
            Assert.IsFalse(LobbyThemeIdentity.IsKnown(value));
            Assert.AreEqual("naturalis", LobbyThemeIdentity.Resolve(value));
        }
    }
}

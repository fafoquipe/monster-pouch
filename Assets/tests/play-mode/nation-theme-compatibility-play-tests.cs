using System;
using System.Linq;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class NationThemeCompatibilityPlayTests
{
    [TestCase("naturalis")] [TestCase("aquarion")] [TestCase("geothar")] [TestCase("volkanar")]
    public void NationHealthBarsKeepMapIdentityAndShareTheApprovedLocalAndRivalRegions(string id)
    {
        var roots=new GameObject[4];
        try
        {
            for(int side=0;side<2;side++)
            {
                bool local=side==0;
                var reference=Create(roots,side*2,"classic",local);
                var nation=Create(roots,side*2+1,id,local);
                Assert.AreEqual(id,nation.MapId);Assert.AreEqual("classic",nation.ArtworkId);Assert.AreEqual(local,nation.IsLocal);
                Assert.AreSame(reference.Fill.sprite,nation.Fill.sprite,"Resolve to the canonical cache before slicing the sheet.");
                Assert.AreEqual(reference.TotalRect,nation.TotalRect);Assert.AreEqual(reference.FillCenter,nation.FillCenter);
                Assert.AreEqual(reference.GetComponentsInChildren<Image>().Single(i=>i.name=="Empty health").color,
                    nation.GetComponentsInChildren<Image>().Single(i=>i.name=="Empty health").color);
                nation.SetValue(25,100);Assert.AreEqual(.25f,nation.Fill.fillAmount);
            }
            var localBar=roots[1].GetComponent<RoundHealthBarArtwork>();var rivalBar=roots[3].GetComponent<RoundHealthBarArtwork>();
            Assert.AreSame(localBar.Fill.sprite.texture,rivalBar.Fill.sprite.texture);
            Assert.AreNotEqual(localBar.Fill.sprite.rect,rivalBar.Fill.sprite.rect,"Keep green local and red rival source regions distinct.");
        }
        finally{foreach(var item in roots)if(item!=null)Object.DestroyImmediate(item);}
    }

    static RoundHealthBarArtwork Create(GameObject[] roots,int index,string map,bool local)
    {
        var go=new GameObject(map+" health fixture",typeof(RectTransform));roots[index]=go;
        ((RectTransform)go.transform).sizeDelta=new Vector2(400,80);
        var bar=go.AddComponent<RoundHealthBarArtwork>();bar.Configure(map,local,"Fill");return bar;
    }

    [TestCase("naturalis",TableStyle.Naturalis)] [TestCase("aquarion",TableStyle.Aquarion)]
    [TestCase("geothar",TableStyle.Geothar)] [TestCase("volkanar",TableStyle.Volkanar)]
    [TestCase("classic",TableStyle.Default)] [TestCase("lounge",TableStyle.GrandCasaHotel)] [TestCase("royal",TableStyle.CampoReal)]
    [TestCase("casino-night",TableStyle.CasinoNight)]
    [TestCase("aireola",TableStyle.Aireola)]
    public void EveryActiveMapKeepsItsStableIdentity(string id,TableStyle style)
    {
        Assert.IsTrue(CardTableTheme.KnownMap(id));Assert.AreEqual(style,CardTableTheme.FromMapId(id));Assert.AreEqual(id,CardTableTheme.MapId(style));
        Assert.IsNotNull(CardTableTheme.MiniWorld(style));
        {
            var art=CardTableTheme.NationArtwork(style);Assert.IsNotNull(art);Assert.AreEqual(id,art.NationId);
            Assert.IsNotNull(art.LobbyBackground);Assert.AreSame(art.Miniature,CardTableTheme.MiniWorld(style));
            Assert.IsNotNull(art.CombatPanorama);Assert.Greater(art.PlayableArea.width,0);Assert.Greater(art.PlayableArea.height,0);
            Assert.IsNotNull(art.CombatPortrait);Assert.Greater(art.PortraitPlayableArea.width,0);Assert.Greater(art.PortraitPlayableArea.height,0);
            Assert.That(art.PortraitPlayableArea.xMin,Is.InRange(0,1));Assert.That(art.PortraitPlayableArea.xMax,Is.InRange(0,1));
            Assert.That(art.PortraitPlayableArea.yMin,Is.InRange(0,1));Assert.That(art.PortraitPlayableArea.yMax,Is.InRange(0,1));
            Assert.AreSame(art.CombatPanorama.texture,art.LobbyBackground.texture);
        }
    }

    [Test] public void NewPreferencesStartAtNaturalisAndExistingCosmeticIdentitiesAreNotRenamed()
    {
        var keys=new[]{CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey};
        var had=keys.Select(PlayerPrefs.HasKey).ToArray();var saved=keys.Select(PlayerPrefs.GetString).ToArray();
        try
        {
            foreach(string key in keys)PlayerPrefs.DeleteKey(key);
            Assert.AreEqual(TableStyle.Naturalis,CardTableTheme.Current);Assert.AreEqual(TableStyle.Naturalis,CardTableTheme.Lobby);
            foreach(string id in new[]{"classic","lounge","royal","casino-night","aireola"})
            {
                foreach(string key in keys)PlayerPrefs.SetString(key,id);
                Assert.AreEqual(id,CardTableTheme.MapId(CardTableTheme.Current));Assert.AreEqual(id,CardTableTheme.MapId(CardTableTheme.Lobby));
            }
            Assert.AreEqual(0,(int)TableStyle.Default);Assert.AreEqual(1,(int)TableStyle.GrandCasaHotel);Assert.AreEqual(2,(int)TableStyle.CampoReal);
            Assert.AreEqual(8,(int)TableStyle.Aireola);Assert.IsTrue(CardTableTheme.KnownMap("aireola"));Assert.IsFalse(CardTableTheme.KnownMap("Naturalis"));
        }
        finally{for(int i=0;i<keys.Length;i++)if(had[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);PlayerPrefs.Save();}
    }
}

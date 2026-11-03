using System.Collections;
using System.Linq;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class InflatableMapPlayTests
{
    EronyaMapTestAccount account;
    LocalGameUI ui;
    CardTableArena arena;
    [UnitySetUp] public IEnumerator Load()
    {
        account=new EronyaMapTestAccount();CardTableTheme.Select(TableStyle.Default);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        arena=Object.FindFirstObjectByType<CardTableArena>();
    }
    [UnityTearDown] public IEnumerator Restore()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=false;}
        yield return null;account?.Dispose();
    }
    [UnityTest] public IEnumerator SuppliedParkKeepsAllSixtyCellsAndSuppressesTheOldFrame()
    {
        var grid=arena.BoardPortraitBounds;var size=ui.Match.Mapper.CellSize;
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"stein"},733));ui.RenderPage();
        yield return new WaitForSecondsRealtime(.4f);
        var park=arena.GetComponent<NationArenaScenery>();
        Assert.IsNotNull(park);Assert.IsTrue(park.IsVisible);Assert.AreEqual("classic",park.Artwork.NationId);
        Assert.IsTrue(park.UsesPaintedGrid);Assert.AreEqual(60,park.TileCount);
        Assert.AreEqual(grid,park.TilePortraitBounds);Assert.AreEqual(size,ui.Match.Mapper.CellSize);
        Assert.AreEqual(2,park.GetComponentsInChildren<MeshRenderer>().Length);
        Assert.IsEmpty(park.GetComponentsInChildren<Collider>());Assert.IsEmpty(park.GetComponentsInChildren<Collider2D>());
        NationArenaSceneryPlayTests.AssertOpaqueGrid(park,arena,ui.Match.Mapper);
        AssertLegacySceneryHidden();
        for(int y=5;y<10;y++)for(int x=0;x<6;x++)Assert.IsTrue(ui.Match.Place("bugaloo",ui.Match.Board.GetCell(x,y)));
    }
    [UnityTest] public IEnumerator ParkRoyalAndAireolaTransitionsPreserveGeometryAndSourceResources()
    {
        var originals=GameObject.Find("grand-cas-hotel").GetComponentsInChildren<SpriteRenderer>(true);
        var sprites=originals.Select(r=>r.sprite).ToArray();var positions=originals.Select(r=>r.transform.position).ToArray();
        var grid=arena.BoardPortraitBounds;var size=ui.Match.Mapper.CellSize;
        foreach(var style in new[]{TableStyle.Default,TableStyle.CampoReal,TableStyle.Aireola,TableStyle.GrandCasaHotel,TableStyle.Default})
        {
            ui.SelectTableSkin(style);ui.CloseModal();yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.1f);
            var scenery=arena.GetComponent<NationArenaScenery>();Assert.IsTrue(scenery.IsVisible);
            Assert.AreEqual(CardTableTheme.MapId(style),scenery.Artwork.NationId);AssertLegacySceneryHidden();
            Assert.AreEqual(grid,arena.BoardPortraitBounds);Assert.AreEqual(size,ui.Match.Mapper.CellSize);
            for(int i=0;i<originals.Length;i++)
            {Assert.AreSame(sprites[i],originals[i].sprite);Assert.AreEqual(positions[i],originals[i].transform.position);}
        }
        Assert.AreEqual(TableStyle.Default,CardTableTheme.Current);Assert.AreEqual("classic",PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
    }
    void AssertLegacySceneryHidden()
    {
        var old=arena.GetComponent<InflatableArenaScenery>();Assert.IsTrue(old==null||!old.IsVisible);
        var royal=arena.GetComponent<RoyalArenaScenery>();Assert.IsTrue(royal==null||!royal.IsVisible);
        var modular=arena.GetComponent<ModularArenaScenery>();Assert.IsTrue(modular==null||!modular.IsVisible);
        foreach(var original in GameObject.Find("grand-cas-hotel").GetComponentsInChildren<SpriteRenderer>(true))Assert.IsFalse(original.enabled);
    }
}

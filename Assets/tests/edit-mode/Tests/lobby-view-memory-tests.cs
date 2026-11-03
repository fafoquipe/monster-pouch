using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class LobbyViewMemoryTests
{
    readonly List<GameObject> objects=new List<GameObject>();
    GameObject Make(string name,params Type[] components)
    {
        var go=new GameObject(name,components);objects.Add(go);return go;
    }
    LobbyFrameGraphic Figure(string id,bool active=true,bool lobby=true)
    {
        var go=Make("memory fixture "+id,typeof(RectTransform),typeof(CanvasRenderer));
        go.SetActive(active);var graphic=go.AddComponent<LobbyFrameGraphic>();
        Assert.IsTrue(graphic.Configure(id,false,LobbyArtworkContext.Standard,lobby),id);
        return graphic;
    }
    [TearDown] public void ReleaseFixtures()
    {
        foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();LobbyViewSpriteCache.TrimUnused();
    }
    [Test] public void CaseWindowKeepsExactRowBoundariesAndBriefDefaults()
    {
        CollectionAssert.AreEqual(Enumerable.Range(0,8),BriefCollectionFilter.VisibleIndices(11,2,0,620,176,176));
        CollectionAssert.AreEqual(Enumerable.Range(4,7),BriefCollectionFilter.VisibleIndices(11,2,436,620,176,176));
        CollectionAssert.AreEqual(new[]{2,3},BriefCollectionFilter.VisibleIndices(11,2,176,176,176,176));
        CollectionAssert.AreEqual(new[]{0,1,2,3},BriefCollectionFilter.VisibleIndices(8,4,0,124));
        Assert.IsEmpty(BriefCollectionFilter.VisibleIndices(8,4,124,12));
        CollectionAssert.AreEqual(new[]{4,5,6,7},BriefCollectionFilter.VisibleIndices(8,4,136,124));
        Assert.IsEmpty(BriefCollectionFilter.VisibleIndices(11,2,0,620,0,176));
        Assert.IsEmpty(BriefCollectionFilter.VisibleIndices(11,2,float.NaN,620,176,176));
    }
    [Test] public void CaseGridReusesShellsReachesLastItemAndAdaptsToViewportHeight()
    {
        var viewport=Make("case viewport",typeof(RectTransform),typeof(ScrollRect)).GetComponent<RectTransform>();
        viewport.sizeDelta=new Vector2(452,620);
        var content=Make("content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
        LocalGameUI.SetRect(content,new Rect(0,0,452,1056));
        var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
        var grid=viewport.gameObject.AddComponent<CollectionGridWindow>();
        var ids=Enumerable.Range(0,11).Select(i=>"item-"+i).ToArray();
        int releases=0;
        grid.Configure(scroll,ids,2,0,parent=>{
            var cell=Make("cell",typeof(RectTransform)).GetComponent<RectTransform>();cell.SetParent(parent,false);return cell;
        },(cell,index)=>cell.name=ids[index],cell=>releases++,null,226,176,226,176);
        Assert.AreEqual(8,grid.ActiveCellCount);Assert.AreEqual(8,grid.PooledCellCount);
        Assert.IsTrue(grid.EnsureVisible("item-10"));Assert.AreEqual(436,content.anchoredPosition.y,.001f);
        Assert.IsTrue(content.Cast<Transform>().Any(t=>t.gameObject.activeSelf&&t.name=="item-10"));
        Assert.Greater(releases,0);Assert.LessOrEqual(grid.PooledCellCount,10);
        foreach(RectTransform card in content)
        {
            if(!card.gameObject.activeSelf)continue;
            Assert.AreEqual(226,card.rect.width,.001f);Assert.AreEqual(176,card.rect.height,.001f);
        }
        viewport.sizeDelta=new Vector2(452,176);Assert.IsTrue(grid.EnsureVisible("item-0"));
        Assert.AreEqual(2,grid.ActiveCellCount);Assert.AreEqual(0,content.anchoredPosition.y,.001f);
        Assert.IsFalse(grid.EnsureVisible("absent"));
        viewport.gameObject.SetActive(false);viewport.gameObject.SetActive(true);
        Assert.AreEqual(2,grid.ActiveCellCount);
    }
    [Test] public void SharedPortraitSurvivesOtherConsumerDisableTrimAndDestroy()
    {
        int baseline=LobbyViewSpriteCache.ActiveLeaseCount;
        var first=Figure("anuik");var second=Figure("anuik");
        var shared=first.CurrentSprite;var imported=shared.texture;
        Assert.AreSame(shared,second.CurrentSprite);Assert.AreEqual(baseline+2,LobbyViewSpriteCache.ActiveLeaseCount);
        first.gameObject.SetActive(false);LobbyViewSpriteCache.TrimUnused();
        Assert.IsNull(first.CurrentSprite);Assert.AreSame(shared,second.CurrentSprite);Assert.IsTrue(shared!=null);
        Object.DestroyImmediate(first.gameObject);Assert.AreEqual(baseline+1,LobbyViewSpriteCache.ActiveLeaseCount);
        second.gameObject.SetActive(false);LobbyViewSpriteCache.TrimUnused();
        Assert.IsTrue(shared==null,"Only the owned runtime sprite is destroyed after its final lease.");
        Assert.IsTrue(imported!=null,"The imported shared texture is never destroyed by cache eviction.");
        second.gameObject.SetActive(true);Assert.IsNotNull(second.CurrentSprite);
        Assert.AreSame(imported,second.CurrentSprite.texture);Assert.AreEqual(baseline+1,LobbyViewSpriteCache.ActiveLeaseCount);
    }
    [Test] public void InactiveConfigureAndRebindDoNotKeepInvisibleLeasesOrDoubleAcquire()
    {
        int baseline=LobbyViewSpriteCache.ActiveLeaseCount;
        var graphic=Figure("anuik",false);Assert.AreEqual(baseline,LobbyViewSpriteCache.ActiveLeaseCount);
        Assert.IsNull(graphic.CurrentSprite);
        graphic.gameObject.SetActive(true);Assert.AreEqual(baseline+1,LobbyViewSpriteCache.ActiveLeaseCount);
        Assert.IsTrue(graphic.Configure("bugaloo",false,LobbyArtworkContext.Standard,true));
        Assert.AreEqual("bugaloo",graphic.UnitId);Assert.IsNotNull(graphic.CurrentSprite);
        Assert.AreEqual(baseline+1,LobbyViewSpriteCache.ActiveLeaseCount);
        graphic.gameObject.SetActive(false);Assert.AreEqual(baseline,LobbyViewSpriteCache.ActiveLeaseCount);
        Assert.IsTrue(graphic.Configure("suki",false,LobbyArtworkContext.Standard,true));
        LobbyViewSpriteCache.TrimUnused();Assert.AreEqual(baseline,LobbyViewSpriteCache.ActiveLeaseCount);
        graphic.gameObject.SetActive(true);Assert.IsNotNull(graphic.CurrentSprite);Assert.AreEqual("suki",graphic.UnitId);
        Assert.AreEqual(baseline+1,LobbyViewSpriteCache.ActiveLeaseCount);
    }
    [Test] public void Visiting43StillsBoundsIdleCacheWhilePreservingActiveConsumer()
    {
        var held=Figure("anuik");var heldSprite=held.CurrentSprite;
        var ids=GuestProgressionCatalogue.Worlds.SelectMany(w=>w.CharacterIds).Distinct().Where(id=>id!="tauris").ToArray();
        Assert.AreEqual(43,ids.Length);
        foreach(string id in ids)
        {
            var visited=Figure(id);Assert.IsTrue(visited.UsesLobbyView,id);
            Assert.IsNull(visited.SourceClip.idleSprite,"Manifest metadata must not retain new sprites: "+id);
            Object.DestroyImmediate(visited.gameObject);
            Assert.LessOrEqual(LobbyViewSpriteCache.IdleEntryCount,LobbyViewSpriteCache.MaximumIdleEntries,id);
            Assert.LessOrEqual(LobbyViewSpriteCache.IdleRgbaBytes,LobbyViewSpriteCache.MaximumIdleRgbaBytes,id);
            Assert.AreSame(heldSprite,held.CurrentSprite);Assert.IsTrue(heldSprite!=null,id);
        }
        LobbyViewSpriteCache.TrimUnused();Assert.AreEqual(0,LobbyViewSpriteCache.IdleEntryCount);
        Assert.AreSame(heldSprite,held.CurrentSprite);
    }
    [Test] public void TaurukFallbackAndNonOptedInSpritesRemainOutsideOwnedCache()
    {
        int baseline=LobbyViewSpriteCache.ActiveLeaseCount;
        var fallback=Figure("tauris");var legacy=Figure("anuik",true,false);
        var fallbackSprite=fallback.CurrentSprite;var legacySprite=legacy.CurrentSprite;
        Assert.IsFalse(fallback.UsesLobbyView);Assert.IsFalse(legacy.UsesLobbyView);
        Assert.AreEqual(baseline,LobbyViewSpriteCache.ActiveLeaseCount);
        fallback.gameObject.SetActive(false);legacy.gameObject.SetActive(false);LobbyViewSpriteCache.TrimUnused();
        Assert.IsTrue(fallbackSprite!=null);Assert.IsTrue(legacySprite!=null);
        fallback.gameObject.SetActive(true);legacy.gameObject.SetActive(true);
        Assert.AreSame(fallbackSprite,fallback.CurrentSprite);Assert.AreSame(legacySprite,legacy.CurrentSprite);
    }
}

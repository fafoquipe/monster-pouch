using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class StatusHudOnlyPlayTests
{
    GameObject root;
    UnitStatusBadges hud;
    [SetUp] public void Setup()
    {
        root=new GameObject("status HUD target",typeof(RectTransform));
        hud=root.AddComponent<UnitStatusBadges>();
    }
    [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(root);}

    [TestCase("stun")][TestCase("electric-stun")]
    [TestCase("poison")][TestCase("regeneration")][TestCase("damage-suppressed")]
    [TestCase("burn")][TestCase("haste")][TestCase("anti-heal")]
    [TestCase("damage-reduction")][TestCase("empowered")]
    public void SharedCircleUsesCoreDurationAndRemovesOnExpiry(string id)
    {
        void Present(int tick)
        {
            var snapshot=new CombatStatusSnapshot(id,"test-effect","source",1,1,100,140,0,1,tick);
            hud.PresentFrame(new[]{new ActiveStatus(id,snapshot.RemainingSeconds)},Array.Empty<PassiveProgress>(),
                new CombatStatusFrame(1,tick,1,true,new[]{snapshot}),tick*.1f);
        }
        Present(110);
        var ring=hud.GetComponentsInChildren<CombatBadgeRing>().Single();
        Assert.AreEqual(1,hud.VisibleCount,"Legacy and canonical projections share one badge.");
        Assert.AreEqual("status-"+id,ring.name);
        Assert.AreEqual(new Vector2(8,9),ring.rectTransform.anchoredPosition);
        Assert.AreEqual(new Vector2(17,17),ring.rectTransform.sizeDelta);
        Assert.AreEqual(.75f,ring.RemainingFraction,.0001f);
        var image=ring.GetComponentInChildren<Image>();Assert.IsNotNull(image.sprite);
        Assert.AreSame(CombatHudSymbols.Get(id),image.sprite);
        Present(120); Assert.AreSame(ring,hud.GetComponentsInChildren<CombatBadgeRing>().Single());
        Assert.AreEqual(.5f,ring.RemainingFraction,.0001f);
        hud.PresentFrame(Array.Empty<ActiveStatus>(),Array.Empty<PassiveProgress>(),
            new CombatStatusFrame(1,140,2,true,Array.Empty<CombatStatusSnapshot>()),14);
        Assert.Zero(hud.VisibleCount);Assert.IsFalse(ring.gameObject.activeSelf);
    }

    [Test] public void ImmobilizationStunAndCureRemainSeparateStatesInTheSameRow()
    {
        var ids=new[]{"root","stun","regeneration"};
        var statuses=ids.Select(id=>new CombatStatusSnapshot(id,"test-effect","source",1,1,0,40,0,1,10)).ToArray();
        hud.PresentFrame(Array.Empty<ActiveStatus>(),Array.Empty<PassiveProgress>(),
            new CombatStatusFrame(1,10,1,true,statuses),1);
        Assert.AreEqual(3,hud.VisibleCount);
        var rings=hud.GetComponentsInChildren<CombatBadgeRing>();
        for(int i=0;i<ids.Length;i++)
        {
            var ring=rings.Single(r=>r.name=="status-"+ids[i]);
            Assert.AreEqual(new Vector2(8+i*18,9),ring.rectTransform.anchoredPosition);
            Assert.AreEqual(.75f,ring.RemainingFraction,.0001f);
        }
        Assert.AreNotSame(CombatHudSymbols.Get("root"),CombatHudSymbols.Get("stun"));
        Assert.AreNotSame(CombatHudSymbols.Get("stun"),CombatHudSymbols.Get("regeneration"));
        hud.enabled=false;Assert.Zero(hud.VisibleCount);
    }
}

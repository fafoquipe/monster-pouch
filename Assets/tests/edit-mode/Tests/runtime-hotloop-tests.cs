using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RuntimeHotloopTests
{
    [Test] public void RowCacheReusesOnlyMatchingIdentitySourceResetLanguageAndValues()
    {
        var cache = new BattleInfoText.RowCache();
        var owner = new object(); var source = new object();
        string Read(object id, object actor, int reset, bool spanish, decimal hp = 40, float energy = 2)
            => cache.Rows(id, actor, reset, hp, 50, 7, 5, 1.25m, 1, energy, 10, spanish);
        var first = Read(owner, source, 1, false);
        Assert.AreEqual(BattleInfoText.Rows(40,50,7,5,1.25m,1,2,10,false), first);
        Assert.AreSame(first, Read(owner, source, 1, false));
        var damaged = Read(owner, source, 1, false, 39);
        Assert.AreNotEqual(first, damaged);
        Assert.AreEqual(BattleInfoText.Rows(39,50,7,5,1.25m,1,2,10,false), damaged);
        Assert.AreNotEqual(damaged, Read(owner, source, 1, false, 39, 3));
        Assert.AreEqual(BattleInfoText.Rows(40,50,7,5,1.25m,1,2,10,true), Read(owner,source,1,true));
        var stable = Read(owner,source,1,false);
        Assert.AreNotSame(stable, Read(new object(),source,1,false));
        stable = Read(owner,source,1,false);
        Assert.AreNotSame(stable, Read(owner,new object(),1,false));
        stable = Read(owner,source,1,false);
        Assert.AreNotSame(stable, Read(owner,source,2,false));
        stable = Read(owner,source,1,false); cache.Clear();
        Assert.AreNotSame(stable, Read(owner,source,1,false));
    }

    [Test] public void BadgeKeysPreserveCanonicalMergeCategoryAndRecycledCounter()
    {
        var root = new GameObject("hotloop badges", typeof(RectTransform));
        try
        {
            var badges = root.AddComponent<UnitStatusBadges>();
            var legacy = new[] { new ActiveStatus("guard", 2), new ActiveStatus("sleep", 3) };
            var progress = new[] { new PassiveProgress("damage-reduction", 2, 4) };
            var state = new CombatStatusSnapshot("damage-reduction","fixture","source",1,0,0,20,0,1,0);
            var frame = new CombatStatusFrame(0,0,1,true,new[] { state });
            badges.PresentFrame(legacy,progress,frame,0);
            Assert.AreEqual(3,badges.VisibleCount);
            Assert.IsNotNull(root.transform.Find("status-damage-reduction"));
            Assert.IsNull(root.transform.Find("status-guard"));
            var passive = root.transform.Find("passive-damage-reduction");
            Assert.AreEqual("2",passive.GetComponentInChildren<Text>().text);
            var icon = passive.GetComponentInChildren<Image>(); Assert.IsFalse(icon.enabled);
            badges.PresentFrame(legacy,new[] { new PassiveProgress("damage-reduction",0,4) },frame,0);
            Assert.AreEqual("",passive.GetComponentInChildren<Text>().text);
            badges.Present(Array.Empty<ActiveStatus>(),Array.Empty<PassiveProgress>(),0);
            Assert.AreEqual(0,badges.VisibleCount);
            badges.Present(Array.Empty<ActiveStatus>(),new[] { new PassiveProgress("bonus-hit",1,4) },0);
            var rebound = root.GetComponentsInChildren<Text>().Single();
            Assert.AreEqual("1",rebound.text);
            Assert.AreEqual("passive-bonus-hit",rebound.transform.parent.name);
        }
        finally { Object.DestroyImmediate(root); }
    }
}

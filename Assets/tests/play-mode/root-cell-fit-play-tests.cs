using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class RootCellFitPlayTests
{
    GameObject owner;
    RootVinesView view;
    Sprite[] back,front;
    [SetUp] public void Setup()
    {
        owner=new GameObject("root cell fit");view=owner.AddComponent<RootVinesView>();
        back=new Sprite[4];front=new Sprite[4];
        for(int i=0;i<4;i++)
        {
            back[i]=Resources.Load<Sprite>("MonsterPouch/StatusPresentation/RootVines/root-vines-back-"+i.ToString("00"));
            front[i]=Resources.Load<Sprite>("MonsterPouch/StatusPresentation/RootVines/root-vines-front-"+i.ToString("00"));
            Assert.NotNull(back[i]);Assert.NotNull(front[i]);
        }
        view.Configure(back,front);
    }
    [TearDown] public void Cleanup(){Object.DestroyImmediate(owner);}
    void Present(RootVinesLayout fit,Vector3 cellCenter,float age,int reset=1)
    {
        view.Present(true,reset,age,cellCenter+new Vector3(fit.AnchorOffsetX,fit.AnchorOffsetY,0),
            new Vector2(fit.WorldWidth,fit.WorldHeight),
            new Vector2(RootVinesLayout.NormalizedVisibleWidth,RootVinesLayout.NormalizedVisibleHeight),0,40);
    }
    [TestCase(1.2774138f,1.1745076f)]
    [TestCase(1.4051552f,1.1745076f)]
    [TestCase(1f,.55f)]
    public void MaturePairFillsCellWithoutCrossingAnyEdge(float width,float height)
    {
        RootVinesLayout fit;Assert.IsTrue(RootVinesLayout.TryFitCell(width,height,out fit));
        var center=new Vector3(2,3,0);Present(fit,center,1);
        var pair=owner.GetComponentsInChildren<SpriteRenderer>();Assert.AreEqual(2,pair.Length);
        Assert.AreEqual(39,pair[0].sortingOrder);Assert.AreEqual(41,pair[1].sortingOrder);
        foreach(var renderer in pair)
        {
            Assert.AreEqual(512,renderer.sprite.pixelsPerUnit);
            Assert.AreEqual(new Vector2(256,88),renderer.sprite.pivot);
            Vector3 min=renderer.transform.TransformPoint(new Vector3(-RootVinesLayout.LeftExtent,-RootVinesLayout.BottomExtent,0));
            Vector3 max=renderer.transform.TransformPoint(new Vector3(RootVinesLayout.RightExtent,RootVinesLayout.TopExtent,0));
            Assert.AreEqual(center.x-width/2,min.x,.00001f);Assert.AreEqual(center.y-height/2,min.y,.00001f);
            Assert.AreEqual(center.x+width/2,max.x,.00001f);Assert.AreEqual(center.y+height/2,max.y,.00001f);
        }
        var initial=pair.Select(r=>r.transform.position).ToArray();
        Present(fit,center,0); // duration refresh cannot shrink an existing enclosure
        Assert.AreEqual(back[3],pair[0].sprite);Assert.AreEqual(front[3],pair[1].sprite);
        Assert.AreEqual(initial[0],pair[0].transform.position);Assert.AreEqual(initial[1],pair[1].transform.position);
        view.Hide();Assert.IsTrue(pair.All(r=>!r.enabled));
    }
    [Test] public void EveryGrowthFrameSharesCellAndNewGenerationRestartsOnlyGrowth()
    {
        RootVinesLayout fit;Assert.IsTrue(RootVinesLayout.TryFitCell(1,.8f,out fit));
        var center=new Vector3(-4,5,0);
        for(int i=0;i<4;i++)
        {
            Present(fit,center,(i+.1f)*.06f);
            var pair=owner.GetComponentsInChildren<SpriteRenderer>();
            Assert.AreEqual(back[i],pair[0].sprite);Assert.AreEqual(front[i],pair[1].sprite);
            Assert.AreEqual(fit.ScaleX,pair[0].transform.localScale.x,.00001f);
            Assert.AreEqual(fit.ScaleY,pair[0].transform.localScale.y,.00001f);
            Assert.AreEqual(pair[0].transform.position,pair[1].transform.position);
        }
        Present(fit,center,0,2);Assert.AreEqual(back[0],owner.GetComponentsInChildren<SpriteRenderer>()[0].sprite);
        Assert.Zero(owner.GetComponentsInChildren<Collider>().Length);
        Assert.Zero(owner.GetComponentsInChildren<Collider2D>().Length);
    }

    [Test] public void CanonicalShieldUsesHudEvenWithoutBodyCalibrationAndLegacyShieldRemains()
    {
        var unitGo=new GameObject("shield recipient");unitGo.transform.SetParent(owner.transform);
        var unit=unitGo.AddComponent<WhelpUnit>();var definition=DocumentedRoster.CreateStein();
        unit.Initialize("recipient",BoardSide.Blue,UnitStats.FromDefinition(definition,new OwnedUnit(definition)));
        typeof(BattleUnit).GetMethod("AddShield",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(unit,new object[]{10,int.MaxValue});
        var effects=owner.AddComponent<CombatStatusEffects>();
        var has=typeof(CombatStatusEffects).GetMethod("HasStatus",BindingFlags.Instance|BindingFlags.NonPublic);
        bool LegacyMarker()=>(bool)has.Invoke(effects,new object[]{unit,"shield"});
        Assert.IsTrue(LegacyMarker());
        Assert.IsNull(unit.GetComponent<MonsterPouch.Gameplay.Presentation.UnitPresentation>());
        var frames=(Dictionary<BattleUnit,CombatStatusFrame>)typeof(CombatStatusEffects)
            .GetField("canonicalVisualFrames",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effects);
        var shield=new CombatStatusSnapshot("shield","protectora-del-mausoleo-memoria-de-la-roca","source",1,
            unit.CombatResetVersion,0,40,0,10,10);
        frames[unit]=new CombatStatusFrame(unit.CombatResetVersion,10,1,true,new[]{shield});
        Assert.IsFalse(LegacyMarker(),"The canonical shield already has its status circle.");
        frames[unit]=new CombatStatusFrame(unit.CombatResetVersion-1,10,1,true,new[]{shield});
        Assert.IsTrue(LegacyMarker(),"A stale generation cannot suppress the current shield.");
        frames.Clear();Assert.IsTrue(LegacyMarker());
    }
}

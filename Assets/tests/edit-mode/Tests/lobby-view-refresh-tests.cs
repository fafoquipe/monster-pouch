using System;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class LobbyViewRefreshTests
{
    static LobbyFrameClip Asymmetric()=>new LobbyFrameClip{idleBounds=new[]{0f,0f,300f,400f},idleAnchor=new[]{100f,0f}};
    [Test] public void BodyAxisStaysSeparateFromAsymmetricPropBounds()
    {
        var clip=Asymmetric();Assert.AreEqual(new Vector2(100,0),LobbyViewLayout.SourceAnchor(clip));
        Assert.AreNotEqual(150,LobbyViewLayout.SourceAnchor(clip).x);
    }
    [Test] public void FitLeavesRoomForTheLongerSideOfAnAsymmetricDrawing()
        =>Assert.That(LobbyViewLayout.FitScale(new Rect(0,0,200,240),Asymmetric(),1),Is.EqualTo(.47f).Within(.0001f));
    [TestCase(.9f)] [TestCase(1f)] [TestCase(1.25f)]
    public void AuthoredContextScaleAppliesExactlyOnce(float percent)
    {
        float baseline=LobbyViewLayout.FitScale(new Rect(0,0,200,240),Asymmetric(),1);
        Assert.That(LobbyViewLayout.FitScale(new Rect(0,0,200,240),Asymmetric(),percent),Is.EqualTo(baseline*percent).Within(.0001f));
    }
    [Test] public void FloatingBodyCenterAndFootSupportHaveDistinctTargets()
    {
        var box=new Rect(10,20,200,240);
        Assert.AreEqual(new Vector2(110,140),LobbyViewLayout.TargetAnchor(box,true));
        Assert.AreEqual(new Vector2(110,20),LobbyViewLayout.TargetAnchor(box,false));
        var clip=Asymmetric();clip.idleFloating=true;clip.idleAnchor=new[]{100f,200f};
        Assert.That(LobbyViewLayout.FitScale(box,clip,1),Is.EqualTo(.47f).Within(.0001f));
    }
    [Test] public void MissingOptionalAnchorHasAStableBoundsFallback()
    {
        var clip=Asymmetric();clip.idleAnchor=null;
        Assert.AreEqual(new Vector2(150,0),LobbyViewLayout.SourceAnchor(clip));
    }
    [Serializable] sealed class Manifest { public LobbyFrameClip[] clips; }
    [Test] public void All43MenuStillsLoadTheirOwnTexturesAndRegisteredAnchors()
    {
        var json=Resources.Load<TextAsset>("MonsterPouch/UI/LobbyViews/clips");Assert.IsNotNull(json);
        var clips=JsonUtility.FromJson<Manifest>(json.text).clips;Assert.AreEqual(43,clips.Length);
        CollectionAssert.DoesNotContain(clips.Select(c=>c.id).ToArray(),"tauris");
        Assert.AreEqual(43,clips.Select(c=>c.id).Distinct().Count());
        var expectedIds="aky,alboguera,alveyr,antidota,anuik,arboreo,atong,atori,blotan,bugaloo,bugui,cael,canonero-del-orvek,dagata,drayven,flo,gochan,golem-tectonico,guarda-veterano,hymay,jabalinero,jazar,kayon,kazuru,korran,la-minera,legionario,maestro-de-las-mareas,martio,palista,pokrak,popow,protectora-del-mausoleo,rosie,sepora,serya,stein,suki,titanica,tokoro,trimol,tsu,zephalontropo".Split(',');
        CollectionAssert.AreEquivalent(expectedIds,clips.Select(c=>c.id).ToArray());
        var go=new GameObject("Lobby still acceptance",typeof(RectTransform),typeof(CanvasRenderer));
        try
        {
            var graphic=go.AddComponent<LobbyFrameGraphic>();graphic.rectTransform.sizeDelta=new Vector2(200,230);
            foreach(var clip in clips)
            {
                var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+clip.id+"/Character");Assert.IsNotNull(character,clip.id);
                var texture=Resources.Load<Texture2D>("MonsterPouch/UI/LobbyViews/"+clip.id);Assert.IsNotNull(texture,clip.id);
                Assert.AreEqual(FilterMode.Point,texture.filterMode,clip.id);Assert.AreEqual(1,texture.mipmapCount,clip.id);
                foreach(var context in new[]{LobbyArtworkContext.Standard,LobbyArtworkContext.BattleHome})
                {
                    Assert.IsTrue(graphic.Configure(clip.id,false,context,useLobbyView:true),clip.id);
                    Assert.IsTrue(graphic.UsesLobbyView,clip.id);Assert.IsFalse(graphic.HasAuthoredFrames,clip.id);
                    Assert.AreSame(texture,graphic.CurrentSprite.texture,clip.id);
                    Assert.AreEqual(LobbyViewLayout.SourceAnchor(clip),graphic.IdleSourceAnchor,clip.id);
                    Assert.AreEqual(context==LobbyArtworkContext.BattleHome?character.Visuals.GetLobbyScale():character.Visuals.GetBriefScale(),graphic.ContextScale,clip.id);
                }
            }
        }
        finally{Object.DestroyImmediate(go);}
    }
    // Partial delivery: Tauruk has no new still. Both menu contexts must keep
    // the exact existing idle object, texture, anchor and human scale.
    [TestCase(LobbyArtworkContext.Standard)]
    [TestCase(LobbyArtworkContext.BattleHome)]
    public void TaurisKeepsExistingIdleWhenNewMenuStillsAreEnabled(LobbyArtworkContext context)
    {
        Assert.IsNull(Resources.Load<Texture2D>("MonsterPouch/UI/LobbyViews/tauris"));
        var go=new GameObject("Tauruk existing fallback",typeof(RectTransform),typeof(CanvasRenderer));
        try
        {
            var graphic=go.AddComponent<LobbyFrameGraphic>();
            Assert.IsTrue(graphic.Configure("tauris",false,context));
            var clip=graphic.SourceClip;var sprite=graphic.CurrentSprite;
            var anchor=graphic.IdleSourceAnchor;float scale=graphic.ContextScale;
            Assert.IsNotNull(sprite);Assert.IsFalse(graphic.UsesLobbyView);
            // Reuse the same component after a different character's new still.
            Assert.IsTrue(graphic.Configure("aky",false,context,useLobbyView:true));
            Assert.IsTrue(graphic.UsesLobbyView);Assert.AreNotSame(sprite,graphic.CurrentSprite);
            Assert.IsTrue(graphic.Configure("tauris",false,context,useLobbyView:true));
            Assert.IsFalse(graphic.UsesLobbyView);Assert.IsFalse(graphic.HasAuthoredFrames);
            Assert.AreSame(clip,graphic.SourceClip);Assert.AreSame(sprite,graphic.CurrentSprite);
            Assert.AreEqual(anchor,graphic.IdleSourceAnchor);Assert.AreEqual(scale,graphic.ContextScale);
            Assert.IsTrue(graphic.IsAtRest);
        }
        finally{Object.DestroyImmediate(go);}
    }
    [Test] public void NonMenuOptOutKeepsExistingIdleAfterNewMenuViewLoads()
    {
        var go=new GameObject("Original non-menu art",typeof(RectTransform),typeof(CanvasRenderer));
        try
        {
            var graphic=go.AddComponent<LobbyFrameGraphic>();
            Assert.IsTrue(graphic.Configure("aky",false,LobbyArtworkContext.Standard));
            var clip=graphic.SourceClip;var sprite=graphic.CurrentSprite;
            Assert.IsTrue(graphic.Configure("aky",false,LobbyArtworkContext.Standard,useLobbyView:true));
            Assert.IsTrue(graphic.UsesLobbyView);Assert.AreNotSame(sprite,graphic.CurrentSprite);
            Assert.IsTrue(graphic.Configure("aky",false,LobbyArtworkContext.Standard,useLobbyView:false));
            Assert.IsFalse(graphic.UsesLobbyView);Assert.AreSame(clip,graphic.SourceClip);
            Assert.AreSame(sprite,graphic.CurrentSprite);
        }
        finally{Object.DestroyImmediate(go);}
    }
    [Test] public void AuthoredStandardAnimationKeepsItsOriginalIdleAndAtlas()
    {
        var go=new GameObject("Original gesture acceptance",typeof(RectTransform),typeof(CanvasRenderer));
        try
        {
            var graphic=go.AddComponent<LobbyFrameGraphic>();
            Assert.IsTrue(graphic.Configure("aky",true,LobbyArtworkContext.Standard));
            var clip=graphic.SourceClip;var sprite=graphic.CurrentSprite;int poses=graphic.PoseCount;
            Assert.IsTrue(graphic.Configure("aky",true,LobbyArtworkContext.Standard,useLobbyView:true));
            Assert.IsFalse(graphic.UsesLobbyView);Assert.AreSame(clip,graphic.SourceClip);
            Assert.AreSame(sprite,graphic.CurrentSprite);Assert.AreEqual(poses,graphic.PoseCount);
        }
        finally{Object.DestroyImmediate(go);}
    }
}

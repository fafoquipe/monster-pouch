using System;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class LobbyGroundAnchorTests
{
    GameObject root;
    LobbyFrameGraphic body;
    LobbyContactShadow shadow;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [SetUp] public void Setup()
    {
        root=new GameObject("figure support",typeof(RectTransform));
        var parent=(RectTransform)root.transform;parent.sizeDelta=new Vector2(90,104);parent.pivot=new Vector2(.21f,.73f);
        body=Child<LobbyFrameGraphic>("body");shadow=Child<LobbyContactShadow>("shadow");
    }
    T Child<T>(string name) where T:MaskableGraphic
    {
        var child=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)child.transform;
        rect.SetParent(root.transform,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=rect.offsetMax=Vector2.zero;return child.AddComponent<T>();
    }
    [TearDown] public void Cleanup()=>Object.DestroyImmediate(root);
    static Vector3[] Mesh(MaskableGraphic graphic)
    {
        using(var helper=new VertexHelper())
        {
            graphic.GetType().GetMethod("OnPopulateMesh",Private,null,new[]{typeof(VertexHelper)},null).Invoke(graphic,new object[]{helper});
            var points=new Vector3[helper.currentVertCount];var vertex=new UIVertex();
            for(int i=0;i<points.Length;i++){helper.PopulateUIVertex(ref vertex,i);points[i]=vertex.position;}
            return points;
        }
    }
    void AssertRegistered(string id)
    {
        var vertices=Mesh(body);Assert.AreEqual(4,vertices.Length,id+": retain the complete unwarped quad.");
        var source=body.SourceClip;var bounds=source.idleBounds;var rect=body.CurrentSprite.rect;
        var scaleX=(vertices[1].x-vertices[0].x)/rect.width;
        var scaleY=(vertices[3].y-vertices[0].y)/rect.height;
        Assert.AreEqual(scaleX,scaleY,.0001f,id+": uniform scale.");
        var visibleFoot=vertices[0]+new Vector3((bounds[0]+bounds[2]*.5f)*scaleX,bounds[1]*scaleY);
        var target=body.rectTransform.rect;
        Assert.AreEqual(target.center.x,visibleFoot.x,.001f,id+": visible body centered.");
        Assert.AreEqual(target.yMin,visibleFoot.y,.001f,id+": visible body on support.");
        var shadowVertices=Mesh(shadow);
        Assert.Less((body.transform.TransformPoint(visibleFoot)-shadow.transform.TransformPoint(shadowVertices[0])).magnitude,.001f,id);
    }
    [TestCase(90,104)] [TestCase(200,230)]
    public void All44IdlesShareTheShadowSupportAtBothSurfaceSizes(float width,float height)
    {
        ((RectTransform)root.transform).sizeDelta=new Vector2(width,height);
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        try
        {
            var ids=config.Units.Where(u=>u!=null).Select(u=>u.Id).Distinct().ToArray();Assert.AreEqual(44,ids.Length);
            foreach(var id in ids)foreach(var context in new[]{LobbyArtworkContext.Standard,LobbyArtworkContext.BattleHome})
            {
                Assert.IsTrue(body.Configure(id,false,context),id);
                AssertRegistered(id+"/"+context);
                Assert.IsFalse(body.HasAuthoredFrames,id);
                var sprite=body.CurrentSprite;body.SampleFrame(.5f);Assert.AreSame(sprite,body.CurrentSprite,id);
            }
        }
        finally{Object.DestroyImmediate(config);}
    }
    [Test] public void ExistingAsymmetricBoundsAreCenteredWithoutMutatingTheirManifest()
    {
        Assert.IsTrue(body.Configure("korran"));var source=body.SourceClip;
        var before=(float[])source.idleAnchor.Clone();Assert.AreNotEqual(source.idleAnchor[0],body.IdleSourceAnchor.x);
        AssertRegistered("asymmetric Korran");CollectionAssert.AreEqual(before,source.idleAnchor);
        ((RectTransform)root.transform).sizeDelta*=1.6f;root.transform.localScale=new Vector3(1.25f,1.25f,1);
        AssertRegistered("resized asymmetric Korran");CollectionAssert.AreEqual(before,source.idleAnchor);
    }
    [Test] public void MissingLobbyManifestReusesMartiosSerializedIdleAndCalibration()
    {
        var art=CharacterUiArtwork.GetCharacter("martio").Visuals;float size=art.InGameSizePercent;
        Assert.IsTrue(body.Configure("martio"));Assert.IsTrue(body.UsesCharacterIdle);
        Assert.AreSame(art.Idle(UnitFacing.South),body.CurrentSprite);
        Assert.AreEqual(art.ReferencePixelWidth,body.SourceClip.idleBounds[2]);
        Assert.AreEqual(art.VitalsHeadAnchor.y*body.CurrentSprite.pixelsPerUnit,body.SourceClip.idleBounds[3]);
        AssertRegistered("Martio fallback");Assert.AreEqual(size,art.InGameSizePercent);
    }
    [Test] public void NativeCombatPivotsAndHumanScalesRemainUnmodified()
    {
        foreach(var id in new[]{"suki","tsu","martio","alveyr"})
        {
            var art=CharacterUiArtwork.GetCharacter(id).Visuals;float scale=art.InGameSizePercent;
            var sprites=Enum.GetValues(typeof(UnitFacing)).Cast<UnitFacing>().Select(art.Idle).ToArray();
            var pivots=sprites.Select(s=>s.pivot).ToArray();
            Assert.IsTrue(body.Configure(id));body.SampleFrame(.9f);
            CollectionAssert.AreEqual(pivots,sprites.Select(s=>s.pivot).ToArray(),id);
            Assert.AreEqual(scale,art.InGameSizePercent,id);
        }
    }
    [TestCase(LobbyArtworkContext.Standard,1.25f)] [TestCase(LobbyArtworkContext.BattleHome,1.75f)]
    public void ContextPercentageScalesBodyAndShadowOnceAfterFit(LobbyArtworkContext context,float expected)
    {
        Assert.IsTrue(body.Configure("korran",false,context));
        var art=new UnitArt{LobbySizePercent=100,BriefSizePercent=100,InGameSizePercent=13};
        typeof(LobbyFrameGraphic).GetField("presentationArt",Private).SetValue(body,art);
        var beforeBody=Mesh(body);var beforeShadow=Mesh(shadow);
        art.LobbySizePercent=175;art.BriefSizePercent=125;
        var afterBody=Mesh(body);var afterShadow=Mesh(shadow);
        Assert.AreEqual(expected,(afterBody[1].x-afterBody[0].x)/(beforeBody[1].x-beforeBody[0].x),.0001f);
        Assert.AreEqual(expected,(afterShadow[1].x-afterShadow[0].x)/(beforeShadow[1].x-beforeShadow[0].x),.0001f);
        AssertRegistered("context percentage");Assert.AreEqual(13,art.InGameSizePercent);
    }
}

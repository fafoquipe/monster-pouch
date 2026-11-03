using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class CasinoAuthoredShadowsPlayTests
{
    EronyaMapTestAccount mapAccount;
    TableStyle previous;
    LocalGameUI ui;
    CardTableArena arena;
    readonly Dictionary<SpriteRenderer,Pose> originals=new Dictionary<SpriteRenderer,Pose>();
    sealed class Pose
    {
        public Vector3 Position,Scale;
        public Quaternion Rotation;
        public Color Color;
        public int Order;
        public bool FlipX,FlipY;
        public Sprite Sprite;
        public Material Material;
        public Pose(SpriteRenderer renderer)
        {
            Position=renderer.transform.position;Scale=renderer.transform.localScale;Rotation=renderer.transform.rotation;
            Color=renderer.color;Order=renderer.sortingOrder;FlipX=renderer.flipX;FlipY=renderer.flipY;
            Sprite=renderer.sprite;Material=renderer.sharedMaterial;
        }
        public void AssertUnchanged(SpriteRenderer renderer)
        {
            string name=renderer.name;
            Assert.AreEqual(Position,renderer.transform.position,name+" position");
            Assert.AreEqual(Scale,renderer.transform.localScale,name+" scale");
            Assert.AreEqual(Rotation,renderer.transform.rotation,name+" rotation");
            Assert.AreEqual(Color,renderer.color,name+" color");Assert.AreEqual(Order,renderer.sortingOrder,name+" order");
            Assert.AreEqual(FlipX,renderer.flipX,name+" flipX");Assert.AreEqual(FlipY,renderer.flipY,name+" flipY");
            Assert.AreSame(Sprite,renderer.sprite,name+" sprite");Assert.AreSame(Material,renderer.sharedMaterial,name+" material");
        }
    }
    [UnitySetUp] public IEnumerator Setup()
    {
        mapAccount=new EronyaMapTestAccount();
        previous=CardTableTheme.Current;CardTableTheme.Select(TableStyle.GrandCasaHotel);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        arena=ui.Match.Mapper.GetComponent<CardTableArena>();Assert.IsNotNull(arena);
        originals.Clear();
        foreach(var sprite in Sprites().Where(s=>s.name=="clown-tower-shadow"||s.name=="shuffler-cart-shadow"||
            s.name.StartsWith("chip-",StringComparison.Ordinal)&&s.name.EndsWith("-shadow",StringComparison.Ordinal)))
            originals.Add(sprite,new Pose(sprite));
        Assert.AreEqual(11,originals.Count,"The scene keeps the two large prop shadows and all nine chip shadows.");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        CardTableTheme.Select(previous);yield return null;
        mapAccount?.Dispose();
    }
    [UnityTest] public IEnumerator OriginalSilhouettesSwitchWithTheMapWithoutChangingTheirAuthoredProperties()
    {
        var clown=originals.Keys.Single(s=>s.name=="clown-tower-shadow");
        var cart=originals.Keys.Single(s=>s.name=="shuffler-cart-shadow");
        Assert.That(clown.color.a,Is.EqualTo(.6431373f).Within(.000001f));Assert.AreEqual(24,clown.sortingOrder);Assert.IsTrue(clown.flipY);
        Assert.That(cart.color.a,Is.EqualTo(.5803922f).Within(.000001f));Assert.AreEqual(9,cart.sortingOrder);
        for(int cycle=0;cycle<3;cycle++)
            foreach(var style in new[]{TableStyle.Default,TableStyle.CampoReal,TableStyle.GrandCasaHotel})
            {
                arena.SetTheme(style);arena.SetTheme(style);yield return null;
                foreach(var pair in originals)
                {
                    Assert.IsFalse(pair.Key.enabled,pair.Key.name+" must not overlap the supplied original.");
                    Assert.IsTrue(pair.Key.gameObject.activeInHierarchy,pair.Key.name);pair.Value.AssertUnchanged(pair.Key);
                }
                foreach(var die in Sprites().Where(s=>s.name=="die-red"||s.name=="die-white"||s.name=="die-red-shadow"||s.name=="die-white-shadow"))
                    Assert.IsFalse(die.enabled,die.name);
            }
    }
    [UnityTest] public IEnumerator OldPropsAndProjectedShadowsStayHiddenUnderTheSuppliedOriginal()
    {
        for(int cycle=0;cycle<2;cycle++)
        {
            arena.SetTheme(TableStyle.Default);arena.SetTheme(TableStyle.GrandCasaHotel);yield return null;
            var scenery=arena.GetComponent<ModularArenaScenery>();
            Assert.IsFalse(scenery.IsVisible);
            foreach(var pair in originals){Assert.IsFalse(pair.Key.enabled);pair.Value.AssertUnchanged(pair.Key);}
            var original=arena.GetComponent<NationArenaScenery>();
            Assert.IsTrue(original.IsVisible);Assert.AreEqual("lounge",original.Artwork.NationId);
            Assert.AreEqual(2,original.GetComponentsInChildren<MeshRenderer>().Length);
        }
    }
    SpriteRenderer[] Sprites()=>Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Where(s=>s.gameObject.scene==arena.gameObject.scene).ToArray();
}

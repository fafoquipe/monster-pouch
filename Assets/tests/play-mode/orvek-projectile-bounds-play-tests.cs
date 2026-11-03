using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Real renderer/Image geometry, isolated and unrendered. No GameView, capture, input or profile changes.
public sealed class OrvekProjectileBoundsPlayTests
{
    [Test] public void InvalidFiniteBoundsKeepLegacyFlightWhileExplicitZeroStillHides()
    {
        Texture2D texture=null;Sprite sprite=null;GameObject root=null;
        try
        {
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
            sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);sprite.name="Isolated invalid projectile bounds";
            root=new GameObject("Unrendered projectile bounds check",typeof(RectTransform));root.SetActive(false);
            var image=root.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;
            var slot=new ProjectileVisualSlot{OverrideSize=true,SizePercent=57.6f,Frames=new[]{new ProjectileVisualFrame{Sprite=sprite,VisibleRect=Rect.zero}}};
            LogAssert.Expect(LogType.Error,"Missing projectile visible bounds: "+sprite.name);
            ProjectileVisualSizing.Apply(image,slot,50,new Vector2(25,25));
            Assert.AreEqual(new Vector2(25,25),image.rectTransform.sizeDelta,"Invalid metadata must retain the existing flight instead of hiding it.");
            ProjectileVisualSizing.Apply(image,slot,50,new Vector2(30,30));
            Assert.AreEqual(new Vector2(30,30),image.rectTransform.sizeDelta,"The fallback follows the current renderer pose.");
            slot.SizePercent=0;ProjectileVisualSizing.Apply(image,slot,50,new Vector2(30,30));
            Assert.AreEqual(Vector2.zero,image.rectTransform.sizeDelta,"An explicit visual zero remains intentional.");
        }
        finally{if(root!=null)Object.DestroyImmediate(root);if(sprite!=null)Object.DestroyImmediate(sprite);if(texture!=null)Object.DestroyImmediate(texture);}
    }

    [TestCase(0f)] [TestCase(25f)] [TestCase(57.6f)] [TestCase(100f)]
    public void OrvekRealPlaybackKeepsEveryFrameVisibleAtPositiveEditedSizes(float percent)
    {
        var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/canonero-del-orvek/Character");
        Assert.IsNotNull(character);var attack=character.Attack;Assert.IsNotNull(attack);
        string before=JsonUtility.ToJson(attack);
        var registered=attack.ProjectileVisuals.Single(s=>s.VariantId=="slag-flight");
        Assert.AreEqual("volkanar-slag",registered.RendererId);Assert.AreEqual(4,registered.Frames.Length);
        Assert.IsTrue(registered.Frames.All(f=>f.Sprite!=null&&f.VisibleRect.width>0&&f.VisibleRect.height>0),"Apply the measured field delta first.");
        var slot=new ProjectileVisualSlot{VariantId=registered.VariantId,RendererId=registered.RendererId,OverrideSize=true,SizePercent=percent,Frames=registered.Frames};
        float maximum=0;
        try
        {
            using(var playback=new ProjectileVisualPlayback(character,attack,slot))
            {
                for(int i=0;i<registered.Frames.Length;i++)
                {
                    float progress=(i+.5f)/registered.Frames.Length;
                    playback.Sample(slot,progress*playback.Duration,progress,50);
                    var image=playback.Layers[playback.Head];Assert.IsFalse(image.gameObject.activeInHierarchy);
                    Assert.IsNotNull(image.sprite);Assert.Greater(image.color.a,0);Assert.AreSame(registered.Frames[i].Sprite,image.sprite);
                    if(percent==0){Assert.AreEqual(Vector2.zero,image.rectTransform.sizeDelta);continue;}
                    Assert.Greater(image.rectTransform.sizeDelta.x,0);Assert.Greater(image.rectTransform.sizeDelta.y,0);
                    var vertices=new List<UIVertex>();
                    using(var helper=new VertexHelper())
                    {
                        typeof(Image).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(image,new object[]{helper});
                        helper.GetUIVertexStream(vertices);
                    }
                    Assert.AreEqual(6,vertices.Count);Assert.IsTrue(vertices.All(v=>v.color.a>0));
                    float width=vertices.Max(v=>v.position.x)-vertices.Min(v=>v.position.x),height=vertices.Max(v=>v.position.y)-vertices.Min(v=>v.position.y);
                    var visible=registered.Frames[i].VisibleRect;
                    maximum=Mathf.Max(maximum,width*visible.width/50,height*visible.height/50);
                    Assert.That(width/height,Is.EqualTo(image.sprite.rect.width/image.sprite.rect.height).Within(.00001f));
                }
            }
            Assert.That(maximum,Is.EqualTo(percent*2/100).Within(.0001f));
        }
        finally{Assert.AreEqual(before,JsonUtility.ToJson(attack),"The inspector's persisted values must remain untouched.");}
    }
}

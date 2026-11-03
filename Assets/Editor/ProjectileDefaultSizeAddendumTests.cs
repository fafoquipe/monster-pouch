using System;
using System.Collections.Generic;
using System.IO;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class ProjectileDefaultSizeAddendumTests
{
    const int Pixels=256;
    const float Cell=64;
    [Serializable] public sealed class SampleRow
    {public bool enabled;public float percent,envelopePixels;public int renderedPixels,renderedMajor;}
    [Serializable] public sealed class Result
    {public string character,renderer;public SampleRow[] samples;public bool originalOwnerUnchanged;}

    [TestCase("zephalontropo",false,"flight","aireola-crossbow")]
    [TestCase("zephalontropo",true,"flight","aireola-sphere")]
    [TestCase("alboguera",false,"flight","aireola-note")]
    [TestCase("jabalinero",false,"flight","aquarion-javelin")]
    [TestCase("jabalinero",true,"binding-javelin","aquarion-binding-javelin")]
    [TestCase("canonero-del-orvek",false,"slag-flight","volkanar-slag")]
    public void RealFlightRendersDefaultAndRecoversAfterZero(string id,bool ability,string variant,string renderer)
    {
        var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
            "Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");
        Assert.IsNotNull(character);
        Object owner=ability?(Object)character.Ability:character.Attack;
        var original=ProjectileVisualSizing.Resolve(character,ability?ProjectileVisualSource.Ability:ProjectileVisualSource.Basic,variant);
        Assert.IsNotNull(original);Assert.AreEqual(renderer,original.RendererId);
        string before=JsonUtility.ToJson(owner);
        var slot=new ProjectileVisualSlot{VariantId=original.VariantId,RendererId=original.RendererId,
            Frames=original.Frames,OverrideSize=false,SizePercent=original.SizePercent};
        var host=new GameObject("Projectile default addendum QA"){hideFlags=HideFlags.HideAndDontSave};
        var target=new RenderTexture(Pixels,Pixels,24,RenderTextureFormat.ARGB32);
        var readback=new Texture2D(Pixels,Pixels,TextureFormat.RGBA32,false,true);
        var sheet=new Texture2D(Pixels*7,Pixels,TextureFormat.RGBA32,false,true);
        var previousTarget=RenderTexture.active;
        try
        {
            var cameraObject=new GameObject("Isolated render camera");cameraObject.transform.SetParent(host.transform);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=Pixels*.5f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.cullingMask=1<<31;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=target;
            var canvasObject=new GameObject("Isolated real UI canvas",typeof(RectTransform),typeof(Canvas));
            canvasObject.transform.SetParent(host.transform);canvasObject.layer=31;
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=camera;canvas.planeDistance=1;
            var comparisonObject=new GameObject("Runtime sampler comparison",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            comparisonObject.transform.SetParent(host.transform);var runtimeImage=comparisonObject.GetComponent<Image>();
            var rows=new List<SampleRow>();
            using(var playback=new ProjectileVisualPlayback(character,owner,slot))
            {
                var mounted=playback.AttachTo(canvas.transform);mounted.anchorMin=mounted.anchorMax=mounted.pivot=Vector2.one*.5f;
                mounted.anchoredPosition=Vector2.zero;
                foreach(var child in mounted.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                // Same Image instance: default, hide, small positive, normal, max, hide, normal again.
                float[] percentages={original.SizePercent,0,5,50,100,0,50};
                for(int i=0;i<percentages.Length;i++)
                {
                    slot.OverrideSize=i!=0;slot.SizePercent=percentages[i];
                    float elapsed=.01f;float progress=elapsed/playback.Duration;
                    playback.Sample(slot,elapsed,progress,Cell);
                    var image=playback.Layers[playback.Head];
                    Assert.IsNotNull(image.sprite);Assert.Greater(image.color.a,0);
                    Assert.IsTrue(ProjectileVisualSizing.TryVisibleRect(slot,image.sprite,out var visible));
                    Vector2 fit=ProjectileVisualGeometry.Fit(image.rectTransform.sizeDelta,image.sprite.rect.size,image.preserveAspect);
                    fit=Vector2.Scale(fit,image.rectTransform.localScale);
                    float envelope=Mathf.Max(Mathf.Abs(fit.x)*visible.width,Mathf.Abs(fit.y)*visible.height);
                    float expected=i==0?Cell*.5f:Cell*2*slot.SizePercent/100;
                    Assert.That(envelope,Is.EqualTo(expected).Within(.02f),renderer+" default/0/50/100 meaning");
                    if(AireolaFlightRendering.Owns(renderer)||AquarionFlightRendering.Owns(renderer))
                    {
                        var pose=AireolaFlightRendering.Owns(renderer)
                            ?AireolaFlightRendering.Sample(AireolaFlightRendering.Resolve(renderer),elapsed,Cell)
                            :AquarionFlightRendering.Sample(image.sprite,slot,Cell,AquarionFlightRendering.Width(renderer));
                        pose.Apply(runtimeImage);ProjectileVisualSizing.Apply(runtimeImage,slot,Cell,pose.Size);
                        Assert.AreSame(image.sprite,runtimeImage.sprite);
                        Assert.That(Vector2.Distance(image.rectTransform.sizeDelta,runtimeImage.rectTransform.sizeDelta),Is.LessThan(.0001f));
                    }
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    readback.ReadPixels(new Rect(0,0,Pixels,Pixels),0,0);readback.Apply();
                    var pixels=readback.GetPixels32();int count=0,minX=Pixels,minY=Pixels,maxX=-1,maxY=-1;
                    for(int y=0;y<Pixels;y++)for(int x=0;x<Pixels;x++)
                    {
                        if(pixels[y*Pixels+x].a<ProjectileVisualGeometry.MinimumVisibleAlpha)continue;
                        count++;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);
                    }
                    int major=count==0?0:Mathf.Max(maxX-minX+1,maxY-minY+1);
                    if(expected==0)Assert.AreEqual(0,count,"Zero must hide the actual rendered flight.");
                    else
                    {
                        Assert.Greater(count,0,"Positive size must produce visible GPU pixels, including after zero.");
                        Assert.LessOrEqual(major,Mathf.CeilToInt(expected)+2,"Rendered alpha must stay inside the calibrated envelope.");
                    }
                    sheet.SetPixels(i*Pixels,0,Pixels,Pixels,readback.GetPixels());
                    rows.Add(new SampleRow{enabled=slot.OverrideSize,percent=slot.SizePercent,envelopePixels=envelope,renderedPixels=count,renderedMajor=major});
                }
            }
            Assert.AreEqual(before,JsonUtility.ToJson(owner),"The actual saved owner, including human overrides, must remain untouched.");
            string folder="Logs/local-validation/projectile-default-addendum";Directory.CreateDirectory(folder);
            sheet.Apply();File.WriteAllBytes(folder+"/"+renderer+".png",sheet.EncodeToPNG());
            File.WriteAllText(folder+"/"+renderer+".json",JsonUtility.ToJson(new Result{character=id,renderer=renderer,
                samples=rows.ToArray(),originalOwnerUnchanged=true},true));
        }
        finally
        {
            RenderTexture.active=previousTarget;Object.DestroyImmediate(host);target.Release();
            Object.DestroyImmediate(target);Object.DestroyImmediate(readback);Object.DestroyImmediate(sheet);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Integrator-only, explicit tests. No import callback, scene load, profile access or asset save.
public sealed class ProjectileSizesPassTests
{
    const float Cell = 64;
    const int Pixels = 256;
    static CharacterDefinition Character(string id) => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
        "Assets/resources/MonsterPouch/Characters/" + id + "/Character.asset");
    static Object Owner(CharacterDefinition c, string name) => name == "Attack" ? (Object)c.Attack
        : name == "Ability" ? c.Ability : c.Upgrades[int.Parse(name.Substring(7)) - 1];
    static ProjectileVisualSlot Slot(CharacterDefinition c, string name, string variant) =>
        ProjectileVisualSizing.Resolve(c, name == "Attack" ? ProjectileVisualSource.Basic
        : name == "Ability" ? ProjectileVisualSource.Ability
        : (ProjectileVisualSource)((int)ProjectileVisualSource.Upgrade1 + int.Parse(name.Substring(7)) - 1), variant);
    static ProjectileVisualSlot Clone(ProjectileVisualSlot s) => new ProjectileVisualSlot {
        VariantId=s.VariantId, RendererId=s.RendererId, Frames=s.Frames,
        OverrideSize=s.OverrideSize, SizePercent=s.SizePercent
    };

    static readonly (string id,string owner,string variant,string renderer,bool enabled,float percent)[] Expected = {
        ("alboguera","Attack","flight","aireola-note",true,17.5f),
        ("antidota","Attack","flight","naturalis-catalog",true,17.5f),
        ("anuik","Attack","flight","catalog",true,25f),
        ("anuik","Attack","charged","catalog-charged",true,30f),
        ("bugaloo","Ability","flight","reflected-moon",true,35f),
        ("bugui","Attack","flight","catalog",true,25f),
        ("cael","Attack","flight","naturalis-catalog",true,17.5f),
        ("canonero-del-orvek","Attack","slag-flight","volkanar-slag",true,17.5f),
        ("canonero-del-orvek","Ability","crater-bomb-flight","volkanar-crater-bomb",true,35f),
        ("gochan","Attack","flight","fire-seal",true,17.5f),
        ("gochan","Ability","flight","fire-seal",true,35f),
        ("guarda-veterano","Attack","flight","naturalis-catalog",true,17.5f),
        ("jabalinero","Attack","flight","aquarion-javelin",true,17.5f),
        ("jabalinero","Ability","binding-javelin","aquarion-binding-javelin",true,35f),
        ("jazar","Attack","flight","toxic",true,25f),
        ("kayon","Attack","flight","catalog",true,17.5f),
        ("kazuru","Attack","flight","catalog-spin",true,17.5f),
        ("maestro-de-las-mareas","Attack","flight","aquarion-water-bolt",true,17.5f),
        ("martio","Attack","mars","mars",true,25f),
        ("martio","Attack","moons","moons",true,16f),
        ("protectora-del-mausoleo","Attack","flight","catalog",true,17.5f),
        ("rosie","Attack","flight","catalog-wave",true,17.5f),
        ("rosie","Ability","flight","healing-rose",true,35f),
        ("stein","Attack","flight","catalog",true,17.5f),
        ("suki","Ability","flight","fish",true,35f),
        ("tokoro","Upgrade2","flight","cone",true,30f),
        ("trimol","Attack","flight","rock",true,25f),
        ("trimol","Ability","flight","large-rock",true,31.25f),
        ("tsu","Attack","flight","basic-gum",true,25f),
        ("tsu","Ability","flight","sticky-gum",true,32.5f),
        ("tsu","Upgrade3","speed","speed-gum",true,32.5f),
        ("tsu","Upgrade3","shield","shield-gum",true,32.5f),
        ("tsu","Upgrade3","damage","damage-gum",true,32.5f),
        ("zephalontropo","Attack","flight","aireola-crossbow",true,17.5f),
        ("zephalontropo","Ability","flight","aireola-sphere",true,35f),
    };

    [Test]
    public void Saved35SlotsMatchReviewedDelta()
    {
        Assert.AreEqual(35, Expected.Length);
        foreach (var row in Expected)
        {
            var c=Character(row.id);Assert.IsNotNull(c,row.id);
            var slot=Slot(c,row.owner,row.variant);Assert.IsNotNull(slot,row.id+"/"+row.owner);
            Assert.AreEqual(row.renderer,slot.RendererId);
            Assert.AreEqual(row.enabled,slot.OverrideSize,row.id+" enabled");
            Assert.That(slot.SizePercent,Is.EqualTo(row.percent).Within(.0001f),row.id+" percent");
            Assert.GreaterOrEqual(slot.SizePercent,0);Assert.LessOrEqual(slot.SizePercent,100);
            for(int frameIndex=0;frameIndex<slot.Frames.Length;frameIndex++)
            {
                var frame=slot.Frames[frameIndex];
                string identity=ProjectileAlphaReadOnlyProbe.FrameIdentity(row.id,row.owner,row.variant,frameIndex,frame?.Sprite);
                Assert.IsNotNull(frame,identity);Assert.IsNotNull(frame.Sprite,identity);
                Rect measured=MonsterPouchProjectileVisualRegistration.Measure(frame.Sprite);
                string context=identity+" measured="+measured+" saved="+frame.VisibleRect;
                Assert.That(Mathf.Abs(measured.width-frame.VisibleRect.width),Is.LessThan(.002f),context+" width");
                Assert.That(Mathf.Abs(measured.height-frame.VisibleRect.height),Is.LessThan(.002f),context+" height");
            }
        }
    }

    [Serializable] public sealed class RenderRow {
        public string mode;public float envelopePixels,visibleWidth,visibleHeight,fullSpriteWidth,fullSpriteHeight;
        public int renderedPixels,renderedMajor;
    }
    [Serializable] public sealed class RenderResult {
        public string id,owner,variant;public RenderRow[] rows;public bool ownerUnchanged;
    }
    [TestCase("bugaloo","Ability","flight",0.7f)]
    [TestCase("martio","Attack","mars",0.5f)]
    [TestCase("martio","Attack","moons",0.32f)]
    [TestCase("cael","Attack","flight",0.35f)]
    [TestCase("canonero-del-orvek","Attack","slag-flight",0.35f)]
    [TestCase("canonero-del-orvek","Ability","crater-bomb-flight",0.7f)]
    [TestCase("zephalontropo","Ability","flight",0.7f)]
    [TestCase("trimol","Ability","flight",.625f)]
    [TestCase("tsu","Upgrade3","shield",.65f)]
    [TestCase("rosie","Ability","flight",0.7f)]
    public void RealSavedFlightRendersAndRecoversWithoutMutatingOwner(string id,string ownerName,string variant,float expectedCells)
    {
        var character=Character(id);Assert.IsNotNull(character);
        var owner=Owner(character,ownerName);var original=Slot(character,ownerName,variant);
        Assert.IsNotNull(original);string before=JsonUtility.ToJson(owner);
        var slot=Clone(original);var host=new GameObject("Projectile sizes pass QA"){hideFlags=HideFlags.HideAndDontSave};
        var target=new RenderTexture(Pixels,Pixels,24,RenderTextureFormat.ARGB32);
        var readback=new Texture2D(Pixels,Pixels,TextureFormat.RGBA32,false,true);
        var sheet=new Texture2D(Pixels*4,Pixels,TextureFormat.RGBA32,false,true);
        var oldTarget=RenderTexture.active;
        try
        {
            var cameraObject=new GameObject("Isolated camera");cameraObject.transform.SetParent(host.transform);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=Pixels*.5f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.cullingMask=1<<31;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=target;
            var canvasObject=new GameObject("Isolated UI",typeof(RectTransform),typeof(Canvas));
            canvasObject.transform.SetParent(host.transform);canvasObject.layer=31;
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=camera;canvas.planeDistance=1;
            var rows=new List<RenderRow>();
            using(var playback=new ProjectileVisualPlayback(character,owner,slot))
            {
                var mounted=playback.AttachTo(canvas.transform);mounted.anchorMin=mounted.anchorMax=mounted.pivot=Vector2.one*.5f;
                mounted.anchoredPosition=Vector2.zero;
                foreach(var child in mounted.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                for(int mode=0;mode<4;mode++)
                {
                    slot.OverrideSize=mode==1||original.OverrideSize;slot.SizePercent=mode==1?0:original.SizePercent;
                    playback.Sample(slot,.125f,.25f,Cell);
                    var head=playback.Layers[playback.Head];Assert.IsNotNull(head.sprite);
                    Assert.IsTrue(ProjectileVisualSizing.TryVisibleRect(slot,head.sprite,out var envelope));
                    Vector2 fit=Vector2.Scale(ProjectileVisualGeometry.Fit(head.rectTransform.sizeDelta,head.sprite.rect.size,head.preserveAspect),head.rectTransform.localScale);
                    float major=Mathf.Max(Mathf.Abs(fit.x)*envelope.width,Mathf.Abs(fit.y)*envelope.height);
                    Assert.That(major,Is.EqualTo(mode==1?0:expectedCells*Cell).Within(.03f));
                    for(int layer=0;layer<playback.Layers.Length;layer++)
                    {
                        var image=playback.Layers[layer];image.enabled=mode==3||layer==playback.Head;
                        if(mode==1)Assert.That(image.rectTransform.sizeDelta.sqrMagnitude,Is.LessThan(.00001f),"Zero also hides satellites");
                    }
                    // First three columns isolate the full head sprite without rotation, to measure alpha.
                    // Fourth keeps the actual sampled rotation, positions and all satellite layers.
                    if(mode!=3){head.rectTransform.localRotation=Quaternion.identity;head.rectTransform.anchoredPosition=Vector2.zero;}
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    readback.ReadPixels(new Rect(0,0,Pixels,Pixels),0,0);readback.Apply();
                    var pixels=readback.GetPixels32();int count=0,minX=Pixels,minY=Pixels,maxX=-1,maxY=-1;
                    for(int y=0;y<Pixels;y++)for(int x=0;x<Pixels;x++)
                    {
                        if(pixels[y*Pixels+x].a<ProjectileVisualGeometry.MinimumVisibleAlpha)continue;
                        count++;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);
                    }
                    int renderedMajor=count==0?0:Mathf.Max(maxX-minX+1,maxY-minY+1);
                    if(mode==1)Assert.AreEqual(0,count);
                    else
                    {
                        Assert.Greater(count,0,"Positive saved size must render, including after zero");
                        if(mode!=3){Assert.LessOrEqual(renderedMajor,Mathf.CeilToInt(major)+2);Assert.Greater(renderedMajor,major*.35f);}
                    }
                    rows.Add(new RenderRow{mode=new[]{"saved-head","hidden","restored-head","actual-composite"}[mode],envelopePixels=major,
                        fullSpriteWidth=fit.x,fullSpriteHeight=fit.y,visibleWidth=count==0?0:maxX-minX+1,visibleHeight=count==0?0:maxY-minY+1,
                        renderedPixels=count,renderedMajor=renderedMajor});
                    sheet.SetPixels(mode*Pixels,0,Pixels,Pixels,readback.GetPixels());
                }
            }
            Assert.AreEqual(before,JsonUtility.ToJson(owner));
            string folder="Logs/local-validation/projectile-sizes-pass";Directory.CreateDirectory(folder);
            string key=id+"-"+ownerName+"-"+variant;sheet.Apply();File.WriteAllBytes(folder+"/"+key+".png",sheet.EncodeToPNG());
            File.WriteAllText(folder+"/"+key+".json",JsonUtility.ToJson(new RenderResult{id=id,owner=ownerName,variant=variant,rows=rows.ToArray(),ownerUnchanged=true},true));
        }
        finally
        {
            RenderTexture.active=oldTarget;Object.DestroyImmediate(host);target.Release();
            Object.DestroyImmediate(target);Object.DestroyImmediate(readback);Object.DestroyImmediate(sheet);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class AnuikContourPlayTests
{
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        yield return null;
    }
    static string Output(string name)
    {
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/anuik-contour-validation"));
        Directory.CreateDirectory(folder);return Path.Combine(folder,name);
    }
    [UnityTest] public IEnumerator AnuikKeepsItsBaseProportionsAndNewArtThroughoutWalkingAttackAndRevival()
    {
        var art=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt").Get("anuik");
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"stein"},733));ui.RenderPage();yield return null;
        var actor=ui.Match.Actors[ui.Match.Player.Monster];var view=actor.GetComponent<UnitPresentation>();
        view.Face(0,1);yield return null;
        var material=Resources.Load<Material>("MonsterPouch/AnuikContour");Assert.IsNotNull(material);Assert.IsTrue(material.shader.isSupported);
        Assert.AreSame(material,view.Renderer.sharedMaterial);
        Assert.That(art.WorldWidth*art.DisplayScale*1.2f,Is.EqualTo(1.95f).Within(.01f));
        foreach(var clip in art.DirectionalAnimations)
        {
            Assert.AreEqual(4,clip.Idle.Length);Assert.AreEqual(4,clip.Move.Length);Assert.AreEqual(3,clip.Attack.Length);Assert.AreEqual(2,clip.Death.Length);Assert.AreEqual(6,clip.Revive.Length);
            Assert.IsTrue(clip.Idle.Concat(clip.Move).Concat(clip.Attack).Concat(clip.Death).Concat(clip.Revive).All(s=>s!=null&&s.name.StartsWith("anuik-reference-v2")));
        }
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4;view.Face(Mathf.RoundToInt(Mathf.Sin(angle)),Mathf.RoundToInt(-Mathf.Cos(angle)));
            yield return null;
            Assert.AreEqual(Mathf.Abs(view.Renderer.transform.lossyScale.x),Mathf.Abs(view.Renderer.transform.lossyScale.y),.0001f,"Anuik was stretched.");
        }
        view.Face(0,1);yield return new WaitForSecondsRealtime(.2f);yield return Application.isBatchMode ? null : new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Output("anuik-en-partida.png"));yield return null;
        var foot=actor.transform.position;
        view.Attack(foot+Vector3.up,false,.5f);yield return new WaitForSecondsRealtime(.22f);
        Assert.IsTrue(view.Renderer.sprite.name.StartsWith("anuik-reference-v2"));
        // The October 15 authored heart gesture adds bounded body accents.
        // Preserve that animation, then verify that it returns to uniform base scale.
        Assert.IsTrue(art.AuthoredBodyMotion);
        float baseScale=UnitPresentation.PresentationScale(art,ui.Match.Mapper.CellSize.x);
        Assert.That(view.VisualRoot.localScale.x/baseScale,Is.InRange(.75f,1.3f));
        Assert.That(view.VisualRoot.localScale.y/baseScale,Is.InRange(.75f,1.3f));
        Assert.AreEqual(foot,actor.transform.position,"Presentation must not move the gameplay anchor.");
        yield return new WaitForSecondsRealtime(view.AttackContactDelay+actor.BaseStats.AttackHold+actor.BaseStats.AttackRecovery+.1f);
        yield return null;
        Assert.IsFalse(view.IsAttacking);
        Assert.AreEqual(Mathf.Abs(view.Renderer.transform.lossyScale.x),Mathf.Abs(view.Renderer.transform.lossyScale.y),.001f);
        view.BeginRevive(1.4f);yield return new WaitForSecondsRealtime(.7f);
        Assert.IsTrue(view.Renderer.sprite.name.StartsWith("anuik-reference-v2"));
        Assert.AreSame(material,view.Renderer.sharedMaterial);yield return Application.isBatchMode ? null : new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Output("anuik-resurreccion.png"));yield return null;
    }

    [UnityTest] public IEnumerator ActualSpriteShaderPreservesPaintedDetailAcrossEveryCombatPose()
    {
        var art=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt").Get("anuik");
        var sprites=new HashSet<Sprite>{art.Portrait};
        foreach(var clip in art.DirectionalAnimations)foreach(var sprite in clip.Idle.Concat(clip.Move).Concat(clip.Attack).Concat(clip.Death).Concat(clip.Revive))sprites.Add(sprite);
        Assert.AreEqual(60,sprites.Count);
        var root=new GameObject("Anuik contour render audit");
        var cameraObject=new GameObject("Anuik audit camera",typeof(Camera));
        var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;
        camera.transform.position=new Vector3(1000,1000,-10);camera.nearClipPlane=.1f;camera.farClipPlane=20;
        var spriteObject=new GameObject("Anuik audit pose",typeof(SpriteRenderer));spriteObject.transform.SetParent(root.transform);spriteObject.layer=31;
        var renderer=spriteObject.GetComponent<SpriteRenderer>();renderer.sharedMaterial=Resources.Load<Material>("MonsterPouch/AnuikContour");
        var texture=new RenderTexture(384,384,24,RenderTextureFormat.ARGB32){antiAliasing=1};texture.Create();camera.targetTexture=texture;
        var readback=new Texture2D(384,384,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        int poses=0,visible=0,whiteEdges=0;var report=new System.Text.StringBuilder();
        var actualMaterial=renderer.sharedMaterial;
        var referenceMaterial=new Material(Shader.Find("Sprites/Default"));
        try
        {
            foreach(var sprite in sprites)
            {
                renderer.sprite=sprite;renderer.transform.position=new Vector3(1000-sprite.bounds.center.x,1000-sprite.bounds.center.y,0);
                camera.orthographicSize=Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y)*.62f;
                camera.Render();RenderTexture.active=texture;readback.ReadPixels(new Rect(0,0,384,384),0,0);readback.Apply();
                var pixels=readback.GetPixels32();int count=0,white=0,colored=0;
                renderer.sharedMaterial=referenceMaterial;camera.Render();
                readback.ReadPixels(new Rect(0,0,384,384),0,0);readback.Apply();
                var reference=readback.GetPixels32();renderer.sharedMaterial=actualMaterial;
                int checkedPixels=0,repainted=0;
                for(int y=1;y<383;y++)for(int x=1;x<383;x++)
                {
                    int at=y*384+x;var c=pixels[at];if(c.a<128)continue;count++;
                    var original=reference[at];
                    if(original.a>=128&&c.a>=128)
                    {
                        checkedPixels++;
                        if(Mathf.Abs(c.r-original.r)>3||Mathf.Abs(c.g-original.g)>3||Mathf.Abs(c.b-original.b)>3)repainted++;
                    }
                    bool edge=pixels[at-1].a<128||pixels[at+1].a<128||pixels[at-384].a<128||pixels[at+384].a<128;
                    if(edge&&c.r>205&&c.g>205&&c.b>205)white++;
                    if(edge&&Mathf.Max(c.r,Mathf.Max(c.g,c.b))>35)colored++;
                }
                Assert.Greater(count,2000,sprite.name+": character disappeared.");
                Assert.Greater(checkedPixels,1000,sprite.name+": too little painted texture remained.");
                Assert.AreEqual(0,repainted,sprite.name+": shader overwrote the painted colors or added a black contour.");
                whiteEdges+=white;visible+=count;poses++;
                if(sprite==art.Idle(UnitFacing.South))
                {
                    camera.backgroundColor=new Color(.88f,.90f,.91f,1);camera.Render();RenderTexture.active=texture;
                    readback.ReadPixels(new Rect(0,0,384,384),0,0);readback.Apply();File.WriteAllBytes(Output("anuik-borde-fondo-claro.png"),readback.EncodeToPNG());
                    camera.backgroundColor=new Color(.08f,.10f,.12f,1);camera.Render();RenderTexture.active=texture;
                    readback.ReadPixels(new Rect(0,0,384,384),0,0);readback.Apply();File.WriteAllBytes(Output("anuik-borde-fondo-oscuro.png"),readback.EncodeToPNG());camera.backgroundColor=Color.clear;
                }
                if(poses%12==0)yield return null;
            }
            report.AppendLine("GPU sprite render audit: "+poses+" distinct poses; visible pixels="+visible+"; native visible pixel colors preserved against Sprites/Default; no synthetic black outline.");
            File.WriteAllText(Output("render-audit.txt"),report.ToString());
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;texture.Release();
            Object.Destroy(texture);Object.Destroy(readback);Object.Destroy(root);Object.Destroy(cameraObject);Object.Destroy(referenceMaterial);
        }
        yield return null;
    }
}

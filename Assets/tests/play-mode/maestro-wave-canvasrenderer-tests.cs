using System.Collections;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

// Isolated, no scene/profile/settings/save changes. Uses the real imported wave art.
// Root construction deliberately matches EffectRoot -> Rect: only a RectTransform.
public sealed class MaestroWaveCanvasRendererTests
{
    GameObject canvasObject,cameraObject;
    RenderTexture target;
    Texture2D capture;
    Mesh mesh;
    Canvas canvas;
    Camera camera;
    RectTransform FreshEffectRoot()
    {
        var go=new GameObject("maestro-wave-contract-root",typeof(RectTransform));
        var rect=go.GetComponent<RectTransform>();
        rect.SetParent(canvasObject.transform,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
        rect.sizeDelta=Vector2.zero;
        return rect;
    }
    [SetUp]public void Setup()
    {
        canvasObject=new GameObject("maestro-wave-contract-canvas",typeof(RectTransform),typeof(Canvas));
        canvas=canvasObject.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.WorldSpace;
        canvasObject.GetComponent<RectTransform>().sizeDelta=new Vector2(8,4);
        canvasObject.transform.position=new Vector3(1000,1000,0);
        canvasObject.layer=31;
    }
    [TearDown]public void Cleanup()
    {
        if(camera!=null)camera.targetTexture=null;
        if(target!=null){target.Release();Object.DestroyImmediate(target);}
        if(capture!=null)Object.DestroyImmediate(capture);
        if(mesh!=null)Object.DestroyImmediate(mesh);
        if(cameraObject!=null)Object.DestroyImmediate(cameraObject);
        if(canvasObject!=null)Object.DestroyImmediate(canvasObject);
    }
    [Test]public void RectOnlyRootAutomaticallyReceivesExactlyOneCanvasRenderer()
    {
        var root=FreshEffectRoot();
        Assert.IsNull(root.GetComponent<CanvasRenderer>(),"Fixture must start with the same RectTransform-only root as runtime.");
        var graphic=root.gameObject.AddComponent<MaestroWaveGraphic>();
        Assert.NotNull(root.GetComponent<CanvasRenderer>());
        Assert.AreEqual(1,root.GetComponents<CanvasRenderer>().Length);
        Assert.AreSame(root.GetComponent<CanvasRenderer>(),graphic.canvasRenderer);
        Assert.AreEqual(Vector2.zero,root.sizeDelta,"The contract does not rely on a sized Image prefab.");
    }
    [UnityTest]public IEnumerator FreshRootRendersBothWaveViewsAndClearsWithoutMissingComponent()
    {
        var root=FreshEffectRoot();root.gameObject.layer=31;
        var graphic=root.gameObject.AddComponent<MaestroWaveGraphic>();
        Assert.NotNull(graphic.canvasRenderer);
        var art=Resources.Load<Sprite>(MaestroWaveArt.Resource);
        Assert.NotNull(art,"Install the base wave and simple V2 PNG before running the render check.");
        cameraObject=new GameObject("maestro-wave-contract-camera",typeof(Camera));
        camera=cameraObject.GetComponent<Camera>();camera.enabled=false;
        camera.transform.position=new Vector3(1000,1000,-10);
        camera.orthographic=true;camera.orthographicSize=2;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.cullingMask=1<<31;
        canvas.worldCamera=camera;
        target=new RenderTexture(256,128,24,RenderTextureFormat.ARGB32);target.Create();
        camera.targetTexture=target;
        capture=new Texture2D(256,128,TextureFormat.RGBA32,false);
        graphic.Begin(art,1);
        graphic.Add(MaestroWaveArt.Front,1,new Vector2(-3,.35f),new Vector2(-3,1.15f),
            new Vector2(3,1.15f),new Vector2(3,.35f));
        graphic.Add(MaestroWaveArt.Rear,1,new Vector2(-3,-1.15f),new Vector2(-3,-.35f),
            new Vector2(3,-.35f),new Vector2(3,-1.15f));
        graphic.Complete();
        yield return null;
        Canvas.ForceUpdateCanvases();
        mesh=graphic.canvasRenderer.GetMesh();Assert.NotNull(mesh);
        Assert.AreEqual(8,mesh.vertexCount,"Both real wave quads must reach CanvasRenderer.");
        var visible=RenderPixels();
        Assert.Greater(Visible(visible,64,128),40,"Front view must actually contribute framebuffer pixels.");
        Assert.Greater(Visible(visible,0,64),40,"Rear view must actually contribute framebuffer pixels.");
        // Begin/Complete with no segments is the finished-body state; it must clear the renderer.
        graphic.Begin(art,0);graphic.Complete();
        yield return null;
        Canvas.ForceUpdateCanvases();
        var cleared=RenderPixels();
        Assert.AreEqual(0,Visible(cleared,0,128),"No water may remain after the graphic is cleared.");
        LogAssert.NoUnexpectedReceived();
    }
    Color32[] RenderPixels()
    {
        var previous=RenderTexture.active;
        try
        {
            camera.Render();RenderTexture.active=target;
            capture.ReadPixels(new Rect(0,0,256,128),0,0);capture.Apply();
            return capture.GetPixels32();
        }
        finally{RenderTexture.active=previous;}
    }
    static int Visible(Color32[] pixels,int firstRow,int lastRow)
    {
        int count=0;
        for(int y=firstRow;y<lastRow;y++)for(int x=0;x<256;x++)
        {var p=pixels[y*256+x];if(p.a>16&&(p.r>16||p.g>16||p.b>16))count++;}
        return count;
    }
}

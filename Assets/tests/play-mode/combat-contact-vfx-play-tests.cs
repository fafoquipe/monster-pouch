using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

// Isolated fixture: no scene, profile, PlayerPrefs, global save or project setting changes.
public sealed class CombatContactVfxPlayTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    readonly List<GameObject> objects=new List<GameObject>();
    MatchController match;CombatSimulation simulation;BoardManager board;BoardWorldMapper mapper;
    CombatStatusEffects effects;DocumentedBattleEffects documented;RectTransform canvas;Camera camera;
    BattleUnit source,target;
    GameObject Make(string name,params Type[] types){var go=new GameObject(name,types);objects.Add(go);return go;}
    void Set(string name,object value)=>typeof(MatchController).GetProperty(name).SetValue(match,value);
    void Tick(int tick)=>typeof(CombatSimulation).GetField("tick",Hidden).SetValue(simulation,tick);
    void Update()=>typeof(CombatStatusEffects).GetMethod("LateUpdate",Hidden).Invoke(effects,null);
    IList Active=>(IList)typeof(CombatStatusEffects).GetField("active",Hidden).GetValue(effects);
    void Expire(){foreach(var fx in Active)fx.GetType().GetField("Age").SetValue(fx,10f);Update();}
    BattleUnit Unit(string id,BoardSide side)
    {
        var stats=new UnitStats(100,1,range:5,attackInterval:100,moveInterval:100);
        typeof(UnitStats).GetField("definitionId",Hidden).SetValue(stats,id);
        var unit=Make(id).AddComponent<WhelpUnit>();unit.Initialize(id+objects.Count,side,stats);return unit;
    }
    CombatImpactPresentationSnapshot Capture(bool applied=true,bool periodic=false,bool ability=false)
        =>(CombatImpactPresentationSnapshot)typeof(CombatSimulation).GetMethod("CaptureImpactPresentation",Hidden)
            .Invoke(simulation,new object[]{source,target,"fixture",ability,periodic,applied,!ability&&!periodic});
    void Publish(CombatImpactPresentationSnapshot snapshot,int amount=1)
    {
        Action callback=()=>((Delegate)typeof(MatchController).GetField("Impacted",Hidden).GetValue(match))?.DynamicInvoke(source,target,amount);
        typeof(CombatSimulation).GetMethod("WithImpactPresentation",Hidden).Invoke(simulation,new object[]{snapshot,callback});
    }
    CombatAttackPresentationSnapshot Flight(long id=1)
    {
        var result=(CombatAttackPresentationSnapshot)typeof(CombatAttackPresentationSnapshot).GetConstructors(Hidden).Single()
            .Invoke(new object[]{source,target,"fixture",false,id,.3f});
        effects.TrackContactFlight(result,true,null);return result;
    }
    RectTransform ContactRoot()=>canvas.GetComponentsInChildren<RectTransform>().Single(x=>x.name=="fx-contact"||x.name=="fx-projectile-dissolve");
    [SetUp] public void Setup()
    {
        board=Make("contact-board").AddComponent<BoardManager>();board.BuildBoard();
        mapper=Make("contact-mapper").AddComponent<BoardWorldMapper>();mapper.Configure(board,null,new Vector2(1.4f,-1.17f),Vector2.zero);
        simulation=new CombatSimulation(board);match=Make("contact-match").AddComponent<MatchController>();match.enabled=false;
        Set("Board",board);Set("Mapper",mapper);Set("Simulation",simulation);Set("Phase",MatchPhase.Combat);
        canvas=Make("contact-canvas",typeof(RectTransform)).GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(540,960);
        camera=Make("contact-camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
        var owner=Make("contact-presenters");effects=owner.AddComponent<CombatStatusEffects>();effects.Configure(match,canvas,camera);
        documented=owner.AddComponent<DocumentedBattleEffects>();documented.Configure(match,canvas,camera);
        source=Unit("gochan",BoardSide.Blue);target=Unit("target",BoardSide.Red);
        source.transform.position=new Vector3(-1,0,0);target.transform.position=new Vector3(1,0,0);
    }
    [TearDown] public void Cleanup()
    {
        simulation?.Stop();for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);objects.Clear();
    }
    [Test] public void RealReceiptOnceAndTwoPacketsStayDistinctWithoutChangingHealth()
    {
        var hit=Capture();Publish(hit);Publish(hit);Assert.AreEqual(1,effects.ActiveContactCount);
        Publish(Capture());Assert.AreEqual(2,effects.ActiveContactCount);
        Assert.AreEqual(100,target.CurrentHealth);Assert.AreEqual(0,target.Shield);
        Assert.IsNull(simulation.CurrentImpactPresentation);
    }
    [Test] public void PeriodicAndUnappliedMissNeverCreateAHitFlash()
    {
        Publish(Capture(false),0);Publish(Capture(true,true),3);Assert.Zero(effects.ActiveContactCount);
        Publish(Capture(),0);Assert.AreEqual(1,effects.ActiveContactCount,"Precise fractional HP/shield receipt can round to zero HUD damage.");
    }
    [Test] public void LethalContactKeepsSettledAnchorAfterTargetMovesOrDespawns()
    {
        var hit=Capture();target.ApplyDamage(100);target.transform.position=new Vector3(10,10,0);Publish(hit);
        var fx=Active.Cast<object>().Single(x=>x.GetType().GetField("Contact").GetValue(x)!=null);
        Vector3 anchor=(Vector3)fx.GetType().GetField("Anchor").GetValue(fx);
        Assert.AreEqual(hit.Position+Vector3.up*Mathf.Abs(mapper.CellSize.y)*.28f,anchor);
        Object.DestroyImmediate(target.gameObject);Update();Assert.AreEqual(1,effects.ActiveContactCount);
        Assert.AreEqual(anchor,(Vector3)fx.GetType().GetField("Anchor").GetValue(fx));
    }
    [Test] public void EndpointWithoutHitOnlyDissolvesAndAppliedContactConsumesItsFlight()
    {
        Flight();Assert.AreEqual(1,effects.PendingContactFlightCount);Tick(1);Update();
        Tick(4);Publish(Capture());Assert.Zero(effects.PendingContactFlightCount);Update();Assert.AreEqual(1,effects.ActiveContactCount);
        Expire();Flight(2);Tick(5);Update();Tick(9);Update();Assert.Zero(effects.PendingContactFlightCount);
        Assert.AreEqual(1,effects.ActiveContactCount);Assert.AreEqual("fx-projectile-dissolve",ContactRoot().name);
        Assert.IsFalse(ContactRoot().Find("torso-dome").GetComponent<Image>().enabled);
        Assert.IsTrue(effects.GetComponentsInChildren<SpriteRenderer>().All(x=>!x.enabled||!x.gameObject.activeInHierarchy));
    }
    [Test] public void ReleaseSurvivesCasterDeathButPreparationResetCancels()
    {
        Flight();source.ResetForCombat();Tick(1);Update();Assert.Zero(effects.PendingContactFlightCount);
        Flight(2);Tick(2);Update();source.ApplyDamage(100);Tick(6);Update();Assert.AreEqual(1,effects.ActiveContactCount);
        Assert.AreEqual("fx-projectile-dissolve",ContactRoot().name);
    }
    [Test] public void PauseDisableResetAndPoolReuseLeaveNoResidualContact()
    {
        Publish(Capture());var root=ContactRoot();int created=effects.CreatedEffectCount;
        Set("Paused",true);var before=root.anchoredPosition;Update();Assert.AreEqual(before,root.anchoredPosition);
        Set("Paused",false);Expire();Assert.Zero(effects.ActiveContactCount);Publish(Capture());
        Assert.AreSame(root,ContactRoot());Assert.AreEqual(created,effects.CreatedEffectCount);
        Flight();effects.enabled=false;Assert.Zero(effects.PendingContactFlightCount);Assert.Zero(effects.ActiveContactCount);
        effects.enabled=true;Publish(Capture());Set("Simulation",new CombatSimulation(board));Update();Assert.Zero(effects.ActiveContactCount);
    }
    [Test] public void CapturedContextRestoresEvenWhenSubscriberThrows()
    {
        var hit=Capture();Action thrower=()=>throw new InvalidOperationException("fixture");
        Assert.Throws<TargetInvocationException>(()=>typeof(CombatSimulation).GetMethod("WithImpactPresentation",Hidden)
            .Invoke(simulation,new object[]{hit,thrower}));
        Assert.IsNull(simulation.CurrentImpactPresentation);
    }
    [TestCase("anuik")][TestCase("bugui")][TestCase("kayon")][TestCase("tauris")][TestCase("stein")]
    public void AuthoredLegacyCatalogImpactHasOneOwner(string id)
    {
        typeof(UnitStats).GetField("definitionId",Hidden).SetValue(source.BaseStats,id);
        Publish(Capture());Assert.AreEqual(1,effects.ActiveContactCount);
        Assert.NotNull(ContactRoot().Find("torso-dome").GetComponent<Image>().sprite);
        Assert.Zero(canvas.GetComponentsInChildren<RectTransform>().Count(x=>x.name=="ability-feedback"));
    }

    [Serializable] sealed class ContactCaptureEvidence
    {
        public string character,phase="paused-onset",baselineFile,contactFile;
        public int width,height,activeGraphics,actorPixels,changedPixels,contactPixels,outsidePixels;
        public int minX,minY,maxX,maxY;
        public float cellPixels,projectionError;
        public bool passed;
    }

    // Ignore sub-byte/rounding noise. The two buffers have the SAME actor, camera and
    // canvas; only contact Graphics are toggled, so actor/number pixels cannot pass.
    static void MeasureContactPixels(Color32[] baseline,Color32[] contact,int width,RectInt region,ContactCaptureEvidence result)
    {
        Assert.AreEqual(baseline.Length,contact.Length);
        result.minX=width;result.minY=baseline.Length/width;result.maxX=result.maxY=-1;
        for(int i=0;i<baseline.Length;i++)
        {
            Color32 a=baseline[i],b=contact[i];
            if(Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))<8)continue;
            int x=i%width,y=i/width;result.changedPixels++;
            if(region.Contains(new Vector2Int(x,y)))result.contactPixels++;else result.outsidePixels++;
            result.minX=Mathf.Min(result.minX,x);result.minY=Mathf.Min(result.minY,y);
            result.maxX=Mathf.Max(result.maxX,x);result.maxY=Mathf.Max(result.maxY,y);
        }
    }

    RectTransform[] CaptureEffectRoots()
    {
        var pooled=Active.Cast<object>().Where(x=>x.GetType().GetField("Contact").GetValue(x)!=null)
            .Select(x=>(RectTransform)x.GetType().GetField("Root").GetValue(x));
        var authored=(IEnumerable<RectTransform>)typeof(DocumentedBattleEffects).GetField("impactVisuals",Hidden).GetValue(documented);
        return pooled.Concat(authored).Where(x=>x!=null&&x.gameObject.activeInHierarchy).Distinct().ToArray();
    }

    void ClearCaptureEffects()
    {
        foreach(var root in CaptureEffectRoots())root.gameObject.SetActive(false);
        documented.StopAllCoroutines();
        typeof(DocumentedBattleEffects).GetMethod("ClearImpactVisuals",Hidden).Invoke(documented,null);
        typeof(CombatStatusEffects).GetMethod("Clear",Hidden).Invoke(effects,null);
    }

    static Texture2D CaptureContactFrame(Camera renderCamera,RenderTexture texture)
    {
        Canvas.ForceUpdateCanvases();renderCamera.Render();
        var previous=RenderTexture.active;Texture2D capture=null;
        try
        {
            RenderTexture.active=texture;capture=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            capture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);capture.Apply();return capture;
        }
        catch {if(capture!=null)Object.DestroyImmediate(capture);throw;}
        finally {RenderTexture.active=previous;}
    }

    [Test,Explicit("Integrator-only deterministic onset render; matched effect OFF/ON PNGs and pixel assertions. No scene/profile writes.")]
    public void RenderRepresentativeContactsAtGameCellScale()
    {
        const int width=540,height=360;
        Assert.IsTrue(board.TryOccupyCell(target,1,0));target.transform.position=mapper.GetWorldPosition(target.CurrentCell);
        source.transform.position=target.transform.position-Vector3.right*mapper.CellSize.x;
        var texture=new RenderTexture(width,height,24);texture.Create();camera.targetTexture=texture;
        camera.orthographicSize=3.5f;camera.transform.position=target.transform.position+new Vector3(0,.3f,-10);
        // The runtime converts screen pixels with camera=null (the game's Overlay canvas).
        // Keep that exact coordinate contract in a pixel-unit WorldSpace capture canvas.
        // A separate orthographic pixel camera renders it to RT; no production renderer changes.
        var renderCamera=Make("contact-pixel-capture-camera").AddComponent<Camera>();renderCamera.enabled=false;
        renderCamera.orthographic=true;renderCamera.orthographicSize=height*.5f;
        renderCamera.transform.position=new Vector3(width*.5f,height*.5f,-10);
        renderCamera.targetTexture=texture;renderCamera.cullingMask=1<<31;
        renderCamera.clearFlags=CameraClearFlags.SolidColor;renderCamera.backgroundColor=new Color(.16f,.19f,.22f);
        var display=canvas.gameObject.AddComponent<Canvas>();display.renderMode=RenderMode.WorldSpace;display.worldCamera=renderCamera;
        canvas.sizeDelta=new Vector2(width,height);canvas.position=new Vector3(width*.5f,height*.5f,0);canvas.localScale=Vector3.one;
        var body=Make("fixture-recipient-body",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        body.transform.SetParent(canvas,false);body.transform.SetAsFirstSibling();body.raycastTarget=false;
        body.sprite=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt").Get("bugaloo").Idle(UnitFacing.South);
        Assert.NotNull(body.sprite);body.preserveAspect=true;
        Vector3 screen=camera.WorldToScreenPoint(target.transform.position);
        Vector2 expected=new Vector2(screen.x-width*.5f,screen.y-height*.5f);
        float cellPixels=Vector2.Distance(camera.WorldToScreenPoint(target.transform.position),
            camera.WorldToScreenPoint(target.transform.position+Vector3.right*Mathf.Abs(mapper.CellSize.x)));
        body.rectTransform.anchorMin=body.rectTransform.anchorMax=Vector2.one*.5f;
        body.rectTransform.pivot=body.sprite.pivot/body.sprite.rect.size;
        body.rectTransform.sizeDelta=body.sprite.rect.size*(cellPixels*.7f/body.sprite.rect.width);
        body.rectTransform.anchoredPosition=expected;
        int radius=Mathf.CeilToInt(cellPixels*.8f);
        var region=new RectInt(Mathf.FloorToInt(screen.x)-radius,Mathf.FloorToInt(screen.y)-radius,2*radius+1,2*radius+1);
        // This is an isolated match. Pause BEFORE synchronous spawn so every renderer draws
        // its native onset at age=0, regardless of import time/frame time. No lifetime is edited.
        Set("Paused",true);effects.GetComponent<CombatDamageNumbers>().enabled=false;
        string output=System.IO.Path.Combine(Application.temporaryCachePath,"combat-contact-vfx-capture-v2-20261101");
        System.IO.Directory.CreateDirectory(output);
        try
        {
            foreach(string id in new[]{"atong","golem-tectonico","arboreo","gochan","canonero-del-orvek","anuik","palista","cael","jabalinero"})
            {
                ClearCaptureEffects();Canvas.ForceUpdateCanvases();
                Vector2 documentedPoint=(Vector2)typeof(DocumentedBattleEffects).GetMethod("Point",Hidden).Invoke(documented,new object[]{target.transform.position});
                Vector2 pooledPoint=(Vector2)typeof(CombatStatusEffects).GetMethod("ContactPoint",Hidden).Invoke(effects,new object[]{target.transform.position});
                float projectionError=Mathf.Max(Vector2.Distance(expected,documentedPoint),Vector2.Distance(expected,pooledPoint));
                Assert.Less(projectionError,.05f,"Capture canvas must preserve runtime screen-pixel projection.");
                typeof(UnitStats).GetField("definitionId",Hidden).SetValue(source.BaseStats,id);
                var hit=Capture();
                Action contact=()=>
                {
                    string kind=id=="arboreo"||id=="cael"?"naturalis-basic-contact":id=="palista"?"aireola-basic-contact":null;
                    if(kind!=null)typeof(DocumentedBattleEffects).GetMethod("AreaEffect",Hidden).Invoke(documented,new object[]{source,kind,new[]{target.CurrentCell}});
                    ((Delegate)typeof(MatchController).GetField("Impacted",Hidden).GetValue(match))?.DynamicInvoke(source,target,1);
                };
                typeof(CombatSimulation).GetMethod("WithImpactPresentation",Hidden).Invoke(simulation,new object[]{hit,contact});
                // No yield/WaitForSeconds between creation, controls and readback.
                foreach(var go in objects)if(go!=null)foreach(var child in go.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                var graphics=CaptureEffectRoots().SelectMany(x=>x.GetComponentsInChildren<Graphic>())
                    .Where(x=>x.isActiveAndEnabled&&x.color.a>0).Distinct().ToArray();
                Assert.IsNotEmpty(graphics,id+": no live contact Graphics at synchronous onset.");
                Texture2D background=null,baseline=null,rendered=null;
                try
                {
                    foreach(var graphic in graphics)graphic.enabled=false;
                    body.enabled=false;background=CaptureContactFrame(renderCamera,texture);
                    body.enabled=true;baseline=CaptureContactFrame(renderCamera,texture);
                    foreach(var graphic in graphics)graphic.enabled=true;
                    rendered=CaptureContactFrame(renderCamera,texture);
                    var actorControl=new ContactCaptureEvidence();
                    MeasureContactPixels(background.GetPixels32(),baseline.GetPixels32(),width,region,actorControl);
                    var report=new ContactCaptureEvidence{character=id,width=width,height=height,activeGraphics=graphics.Length,
                        actorPixels=actorControl.contactPixels,cellPixels=cellPixels,projectionError=projectionError,
                        baselineFile=id+"-baseline.png",contactFile=id+".png"};
                    MeasureContactPixels(baseline.GetPixels32(),rendered.GetPixels32(),width,region,report);
                    report.passed=report.actorPixels>=8&&report.contactPixels>=8&&report.outsidePixels==0;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,report.baselineFile),baseline.EncodeToPNG());
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,report.contactFile),rendered.EncodeToPNG());
                    System.IO.File.WriteAllText(System.IO.Path.Combine(output,id+".json"),JsonUtility.ToJson(report,true));
                    Assert.GreaterOrEqual(report.actorPixels,8,id+": actor control failed; capture pipeline is blank.");
                    Assert.GreaterOrEqual(report.contactPixels,8,id+": the effect adds no visible pixels near the recipient; actor-only capture is NOT a pass.");
                    Assert.Zero(report.outsidePixels,id+": changed pixels outside the recipient region; check projection/foreign UI.");
                }
                finally
                {
                    foreach(var graphic in graphics)if(graphic!=null)graphic.enabled=true;body.enabled=true;
                    if(background!=null)Object.DestroyImmediate(background);if(baseline!=null)Object.DestroyImmediate(baseline);
                    if(rendered!=null)Object.DestroyImmediate(rendered);
                }
            }
            TestContext.WriteLine("Verified native onset pixel deltas: "+output+". Review the nine effects visually; this fixture does not claim gameplay acceptance.");
        }
        finally
        {
            ClearCaptureEffects();Set("Paused",false);camera.targetTexture=null;renderCamera.targetTexture=null;
            texture.Release();Object.DestroyImmediate(texture);
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class BugalooLunarShieldPlayTests
{
    LocalGameUI ui;
    CombatStatusEffects effects;
    BattleUnit actor;
    UnitPresentation view;

    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        effects=ui.GetComponent<CombatStatusEffects>();Assert.IsNotNull(effects);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.Match.Pause(false);ui.Match.Menu();ui.Match.enabled=true;}
        yield return null;
    }
    IEnumerator Activate(bool protection=false)
    {
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"atori","flo"},74321));
        if(protection){ui.Match.Player.AddCombatCoins(100);Assert.IsTrue(ui.Match.UpgradeMonster(0));}
        ui.Match.Ready();actor=ui.Match.Actors[ui.Match.Player.Monster];
        view=actor.GetComponent<UnitPresentation>();view.SnapToCell();view.Face(0,1);
        actor.AddEnergy(actor.BaseStats.EnergyMax);
        for(int tick=0;tick<20&&!actor.IsReflecting;tick++)ui.Match.Advance(.1f);
        Assert.IsTrue(actor.IsReflecting);Assert.AreEqual(protection,actor.IsProtected);
        ui.RenderPage();
        yield return new WaitForSeconds(.4f);yield return null;
    }
    RectTransform Root(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>()
        .FirstOrDefault(r=>r.name==name);
    Image[] Shields()=>Root("fx-reflect").GetComponentsInChildren<Image>().Where(i=>i.enabled).ToArray();

    [UnityTest] public IEnumerator LunarShieldsAndGuardStayFixedAndTranslucentForTheWholeReflection()
    {
        yield return Activate();
        var art=Resources.Load<Sprite>("MonsterPouch/UI/CombatIcons/bugaloo-lunar-shield");
        Assert.IsNotNull(art);Assert.AreEqual(FilterMode.Point,art.texture.filterMode);
        var shields=Shields();Assert.AreEqual(3,shields.Length);
        foreach(var shield in shields)
        {
            Assert.AreSame(art,shield.sprite);Assert.IsFalse(shield.raycastTarget);
            Assert.That(shield.color.a,Is.EqualTo(.55f).Within(.01f));
        }
        var positions=shields.Select(s=>s.rectTransform.anchoredPosition).ToArray();
        var rotations=shields.Select(s=>s.rectTransform.localRotation).ToArray();
        var sizes=shields.Select(s=>s.rectTransform.sizeDelta).ToArray();
        var body=view.Renderer.sprite;var bodyPosition=view.VisualRoot.localPosition;
        var bodyScale=view.VisualRoot.localScale;var anchor=actor.transform.position;
        int health=actor.CurrentHealth;float energy=actor.Energy;
        for(int sample=0;sample<4;sample++)
        {
            yield return new WaitForSeconds(.23f);
            for(int i=0;i<shields.Length;i++)
            {
                Assert.AreEqual(positions[i],shields[i].rectTransform.anchoredPosition,"The shields must not orbit.");
                Assert.AreEqual(rotations[i],shields[i].rectTransform.localRotation,"No shield spin.");
                Assert.AreEqual(sizes[i],shields[i].rectTransform.sizeDelta,"No looping scale animation.");
            }
            Assert.AreSame(body,view.Renderer.sprite,"Hold one guard drawing, never cycle special frames.");
            Assert.AreEqual(bodyPosition,view.VisualRoot.localPosition);Assert.AreEqual(bodyScale,view.VisualRoot.localScale);
            Assert.AreEqual(Quaternion.identity,view.VisualRoot.localRotation);
        }
        Assert.AreEqual(anchor,actor.transform.position);Assert.AreEqual(health,actor.CurrentHealth);
        Assert.AreEqual(energy,actor.Energy);Assert.IsTrue(actor.IsReflecting);
        yield return CaptureIfRequested("bugaloo-lunar-shields");
    }

    [UnityTest] public IEnumerator ProtectionSharesTheThreeShieldsAndPausingThenResettingCleansThemUp()
    {
        yield return Activate(true);
        Assert.AreEqual(3,Shields().Length);Assert.IsNull(Root("fx-protection"),"No second dome over the lunar shields.");
        foreach(var shield in Shields())Assert.That(shield.color.a,Is.EqualTo(.6f).Within(.01f));
        var root=Root("fx-reflect");Vector2 position=root.anchoredPosition;
        ui.Match.Pause(true);yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(position,root.anchoredPosition);Assert.AreEqual(3,Shields().Length);
        ui.Match.Pause(false);yield return CaptureIfRequested("bugaloo-protected-lunar-shields");
        actor.ResetForCombat();yield return null;yield return null;
        Assert.IsNull(Root("fx-reflect"));Assert.IsNull(Root("fx-protection"));Assert.IsFalse(view.IsUsingAbility);
    }

    [UnityTest] public IEnumerator DeathDisableAndMenuReleaseShieldsAndReuseTheEffectPool()
    {
        yield return Activate();
        int created=effects.CreatedEffectCount;
        effects.enabled=false;Assert.AreEqual(0,effects.ActiveEffectCount);Assert.IsNull(Root("fx-reflect"));
        effects.enabled=true;yield return null;yield return null;
        Assert.AreEqual(3,Shields().Length);Assert.AreEqual(created,effects.CreatedEffectCount);
        actor.ApplyDamage(int.MaxValue);yield return null;yield return null;
        Assert.IsNull(Root("fx-reflect"));
        ui.Match.Menu();yield return null;Assert.AreEqual(0,effects.ActiveEffectCount);
    }

    IEnumerator CaptureIfRequested(string name)
    {
        string directory=Environment.GetEnvironmentVariable("MONSTER_POUCH_LUNAR_CAPTURE_DIR");
        if(string.IsNullOrEmpty(directory))yield break;
        Directory.CreateDirectory(directory);
#if UNITY_EDITOR
        var assemblies=AppDomain.CurrentDomain.GetAssemblies();
        var gameViewType=assemblies.Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);
        var windowType=assemblies.Select(a=>a.GetType("UnityEditor.EditorWindow")).First(t=>t!=null);
        var previewType=assemblies.Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        var gameView=windowType.GetMethod("GetWindow",new[]{typeof(Type)}).Invoke(null,new object[]{gameViewType});
        var selected=gameViewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
        int previous=(int)selected.GetValue(gameView);
        try
        {
            previewType.GetMethod("Resize",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{"720x1280"});
            for(int frame=0;frame<120&&(Screen.width!=720||Screen.height!=1280);frame++)yield return null;
            ui.RenderPage();yield return null;yield return null;Canvas.ForceUpdateCanvases();
            string path=Path.Combine(directory,name+".png");ScreenCapture.CaptureScreenshot(path);
            for(int frame=0;frame<120;frame++)
            {
                yield return null;
                if(frame>=2&&File.Exists(path)&&new FileInfo(path).Length>24)break;
            }
            Assert.IsTrue(File.Exists(path),"Native in-game capture missing.");
        }
        finally
        {
            selected.SetValue(gameView,previous);
            gameViewType.GetMethod("Repaint",BindingFlags.Public|BindingFlags.Instance).Invoke(gameView,null);
        }
#else
        yield return null;
#endif
    }
}

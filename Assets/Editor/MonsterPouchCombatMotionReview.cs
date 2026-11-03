using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Explicit review commands; no import, scene mutation or automatic execution on editor reload.
public static class MonsterPouchCombatMotionReview
{
    public const string Output="docs/combat-motion-20261015/";
    static readonly string[] Ids={"aky","anuik","atong","atori","blotan","bugaloo","bugui","flo","gochan","hymay","jazar","kayon","popow","sepora","stein","tauris","tokoro","trimol","tsu"};
    static TestRunnerApi runner;
    static bool validationActive;
    static CharacterDefinition Character(string id)=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");
    static string Kind(string id)
    {
        switch(id)
        {
            case "blotan": return "blotan-income";
            case "trimol": return "trimol-boulder";
            case "bugaloo": case "flo": case "gochan": case "hymay": case "kayon":
            case "popow": case "sepora": case "stein": case "tauris": case "tokoro": return id+"-super";
            default:return "";
        }
    }

    public static string RunValidation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||validationActive)throw new InvalidOperationException("Wait for the active editor/test run.");
        Directory.CreateDirectory(Output);
        runner=ScriptableObject.CreateInstance<TestRunnerApi>();TestRunnerApi.RegisterTestCallback(new Results());validationActive=true;
        var names=new[]{"CharacterCombatMotionTests","ImpactCharacterPresentationTests","UnitAuthoredAnimationTests","UnitSpecialAnimationTests",
            "UnitPresentationTimingTests","ControlMotionPresentationTests","TaurisAbilityDurationTests","AbilityTimingTests","ReferenceHopTests","ReferenceRosterQualityTests"};
        runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=names.Select(x=>"MonsterPouch.Gameplay.Tests.EditMode."+x).ToArray()}));
        return "Combat animation checks started.";
    }
    sealed class Results:ICallbacks
    {
        public void RunStarted(ITestAdaptor x){} public void TestStarted(ITestAdaptor x){} public void TestFinished(ITestResultAdaptor x){}
        static bool Ours(ITestResultAdaptor r)=>r.FullName.StartsWith("MonsterPouch.Gameplay.Tests.EditMode.CharacterCombatMotionTests")||(r.HasChildren&&r.Children.Any(Ours));
        public void RunFinished(ITestResultAdaptor r)
        {
            if(!validationActive||!Ours(r))return;validationActive=false;
            TestRunnerApi.SaveResultToFile(r,Path.GetFullPath(Output+"tests.xml"));
            File.WriteAllText(Output+"test-status.txt",r.ResultState+"; passed="+r.PassCount+"; failed="+r.FailCount+"; skipped="+r.SkipCount);
        }
    }

    [Serializable] public sealed class Clip { public string name;public float cast,hold,recovery;public float[] times; }
    [Serializable] public sealed class Entry { public string id,attackName,abilityName;public Clip attack,ability; }
    [Serializable] public sealed class Report { public int cell=192,frames=32; public string generated;public Entry[] characters; }
    static float[] Times(float cast,float hold,float recovery)
    {
        float[] prepare={0,.08f,.16f,.24f,.32f,.4f,.48f,.56f,.64f,.70f,.75f,.79f,.82f,.85f,.88f,.91f,.94f,.96f,.98f,.995f};
        var times=prepare.Select(x=>x*cast).ToList();times.Add(cast);times.Add(cast+hold*.5f);times.Add(cast+hold);
        foreach(float f in new[]{.1f,.2f,.35f,.5f,.65f,.8f,.95f,1})times.Add(cast+hold+recovery*f);
        times.Add(cast+hold+recovery+.16f);return times.ToArray();
    }

    public static string Capture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||validationActive)throw new InvalidOperationException("Finish tests/Play Mode before rendering.");
        Directory.CreateDirectory(Output);var entries=new List<Entry>();int rendered=0;
        foreach(string id in Ids)
        {
            var character=Character(id);var definition=character.CreateRuntimeDefinition();var stats=UnitStats.FromDefinition(definition,new OwnedUnit(definition));
            string kind=Kind(id);
            var entry=new Entry{id=id,attackName=definition.BasicAttackName,abilityName=definition.BaseAbility.Name};
            float cast=AbilityTiming.Ticks(stats.AttackWindup)*CombatSimulation.TickDuration;
            entry.attack=new Clip{name="Ataque",cast=cast,hold=stats.AttackHold,recovery=stats.AttackRecovery,times=Times(cast,stats.AttackHold,stats.AttackRecovery)};
            if(kind.Length>0)
            {
                float abilityCast=AbilityTiming.HasPhases(stats,kind)?AbilityTiming.Cast(stats,kind):character.Visuals.GetAbilityAnimationDuration(UnitFacing.South)*character.Visuals.Animation(UnitFacing.South).SpecialReleasePoint;
                float hold=AbilityTiming.Hold(stats,kind);
                float back=AbilityTiming.HasPhases(stats,kind)?AbilityTiming.Recovery(stats,kind):character.Visuals.GetAbilityAnimationDuration(UnitFacing.South)-abilityCast;
                entry.ability=new Clip{name="Habilidad",cast=abilityCast,hold=hold,recovery=back,times=Times(abilityCast,hold,back)};
            }
            CaptureCharacter(character,definition,entry,kind);
            entries.Add(entry);rendered++;File.WriteAllText(Output+"capture-status.txt",rendered+"/19 characters rendered");
        }
        var report=new Report{generated=DateTime.UtcNow.ToString("o"),characters=entries.ToArray()};
        File.WriteAllText(Output+"preview-data.js","window.COMBAT_REVIEW="+JsonUtility.ToJson(report)+";");
        File.WriteAllText(Output+"capture-status.txt","COMPLETE: 19 characters, front/right attacks and active abilities, captured from UnitPresentation.");
        return "Saved runtime animation review to "+Output;
    }

    static void CaptureCharacter(CharacterDefinition character,UnitDefinition definition,Entry entry,string kind)
    {
        const int cell=192,frames=32;
        int rows=entry.ability==null?2:4;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Combat review "+entry.id);SceneManager.MoveGameObjectToScene(root,scene);
        var target=new RenderTexture(cell,cell,24,RenderTextureFormat.ARGB32);
        var frame=new Texture2D(cell,cell,TextureFormat.RGBA32,false);
        var atlas=new Texture2D(cell*frames,cell*rows,TextureFormat.RGBA32,false);
        RenderTexture old=RenderTexture.active;
        try
        {
            var cameraObject=new GameObject("Review camera");cameraObject.transform.SetParent(root.transform);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=2.30f;
            camera.transform.position=new Vector3(0,.90f,-10);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.065f,.09f,.14f,1);camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=target;
            Bounds envelope=default;bool measured=false;
            // First measure every real pose (including the shadow); then keep one fixed camera
            // for this character's entire review. Extended fists/jaws must never hit the frame edge.
            for(int pass=0;pass<2;pass++)
            {
            for(int row=0;row<rows;row++)
            {
                var go=new GameObject(entry.id);go.transform.SetParent(root.transform);
                var actor=go.AddComponent<MonsterUnit>();actor.Initialize(entry.id,BoardSide.Blue,UnitStats.FromDefinition(definition,new OwnedUnit(definition)));
                var view=go.AddComponent<UnitPresentation>();view.Configure(actor,null,character.Visuals);
                bool special=row>=2;var clip=special?entry.ability:entry.attack;
                Vector3 direction=row%2==0?Vector3.down:Vector3.right;
                view.Face(row%2==0?0:1,row%2==0?1:0);
                if(!special)
                {
                    clip.cast=CombatSimulation.GetAttackWindup(actor);
                    clip.times=Times(clip.cast,clip.hold,clip.recovery);
                    view.Attack(direction*4,definition.AttackKind==AttackKind.Ranged,clip.cast);
                }
                else if(entry.id=="flo")view.PrepareFlower(clip.cast);
                else view.UseAbility(kind);
                float previous=0;
                for(int f=0;f<frames;f++)
                {
                    float time=clip.times[f];view.SendMessage("AdvancePresentation",Mathf.Max(0,time-previous));previous=time;
                    // A final zero step restores idle after the action's terminal sample.
                    if(f==frames-1)view.SendMessage("AdvancePresentation",0f);
                    if(pass==0)
                    {
                        foreach(var renderer in go.GetComponentsInChildren<SpriteRenderer>())
                            if(renderer.enabled && renderer.sprite!=null)
                            { if(!measured){envelope=renderer.bounds;measured=true;}else envelope.Encapsulate(renderer.bounds); }
                        continue;
                    }
                    camera.Render();RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,cell,cell),0,0);frame.Apply();
                    atlas.SetPixels32(f*cell,(rows-1-row)*cell,cell,cell,frame.GetPixels32());
                }
                Object.DestroyImmediate(go);
            }
            if(pass==0)
            {
                camera.transform.position=new Vector3(envelope.center.x,envelope.center.y,-10);
                camera.orthographicSize=Mathf.Max(envelope.extents.x,envelope.extents.y)+.20f;
            }
            }
            atlas.Apply();File.WriteAllBytes(Output+entry.id+".png",atlas.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(frame);Object.DestroyImmediate(atlas);
            Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}

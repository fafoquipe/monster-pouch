using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MonsterPouchStatusEffectsSetup
{
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var shader=Shader.Find("MonsterPouch/PixelGroundGlow");if(shader==null)throw new InvalidOperationException("Missing glow shader.");
        const string path="Assets/resources/MonsterPouch/GroundGlowMaterial.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)AssetDatabase.CreateAsset(new Material(shader),path);
        else {material.shader=shader;EditorUtility.SetDirty(material);}
        AssetDatabase.SaveAssets();return "Reusable pixel light material imported.";
    }
    public static string Preview()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        ui.Match.StartMatch("anuik",new[]{"flo","jazar","atong"});ui.Match.Ready();ui.Match.enabled=false;
        var units=ui.Match.Actors.Values.ToArray();
        for(int i=0;i<units.Length;i++)
        {
            var cell=ui.Match.Board.GetCell(1+i%4,4+i/4);
            if(ui.Match.Board.TryRepositionUnit(units[i],cell))units[i].transform.position=ui.Match.Mapper.GetWorldPosition(cell);
        }
        var fx=ui.GetComponent<CombatStatusEffects>();
        typeof(CombatStatusEffects).GetMethod("HealArea",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fx,new object[]{units[0].CurrentCell});
        typeof(CombatStatusEffects).GetMethod("Healed",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fx,new object[]{units[0],20});
        if(units.Length>1)typeof(CombatStatusEffects).GetMethod("Critical",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fx,new object[]{units[0],units[1]});
        MonsterPouchPlayInspection.Capture("status-effects");return "VFX preview ready.";
    }
    public static string BuildWindows()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        const string path="Builds/Windows-Effects/Monster Pouch.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/main-scene.unity"},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        string result=report.summary.result+"; errors="+report.summary.totalErrors+"; path="+Path.GetFullPath(path);
        File.WriteAllText("Logs/local-validation/status-effects-build.txt",result);
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(result);
        return result;
    }
}

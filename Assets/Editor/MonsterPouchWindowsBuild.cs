using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class MonsterPouchWindowsBuild
{
    const string Key="MonsterPouch.WindowsBuild.";
    static bool busy;
    [Serializable] sealed class Report
    {
        public string status="Pending",exePath,error,startedUtc,completedUtc;
        public long bytes;public int errors;public bool platformRestored;
    }
    static MonsterPouchWindowsBuild(){EditorApplication.update+=Continue;}
    [MenuItem("Monster Pouch/Build/Windows EXE")]
    public static void FromMenu()=>Request();
    public static string Request()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||BuildPipeline.isBuildingPlayer||SessionState.GetString(Key+"folder","")!="")throw new InvalidOperationException("Unity is busy.");
        string folder=Path.GetFullPath("Builds/Windows/"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));Directory.CreateDirectory(folder);
        SessionState.SetInt(Key+"target",(int)EditorUserBuildSettings.activeBuildTarget);SessionState.SetString(Key+"folder",folder);SessionState.SetInt(Key+"phase",0);
        Save(folder,new Report{exePath=Path.Combine(folder,"MonsterPouch.exe"),startedUtc=DateTime.UtcNow.ToString("o")});return folder;
    }
    static void Save(string folder,Report report)=>File.WriteAllText(Path.Combine(folder,"build-report.json"),JsonUtility.ToJson(report,true));
    static void Continue()
    {
        string folder=SessionState.GetString(Key+"folder","");
        if(folder==""||busy||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer)return;
        var original=(BuildTarget)SessionState.GetInt(Key+"target",(int)BuildTarget.StandaloneWindows64);
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(folder,"build-report.json")));
        try
        {
            int phase=SessionState.GetInt(Key+"phase",0);
            if(phase==0)
            {
                SessionState.SetInt(Key+"phase",1);
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64&&!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new InvalidOperationException("Cannot activate Windows.");
                return;
            }
            if(phase==1)
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)return;
                if(EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("Scripts do not compile.");
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
                busy=true;
                var screenMode=PlayerSettings.fullScreenMode;
                int width=PlayerSettings.defaultScreenWidth,height=PlayerSettings.defaultScreenHeight;
                bool resizable=PlayerSettings.resizableWindow;
                try
                {
                    PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
                    PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;
                    PlayerSettings.resizableWindow=true;
                    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/main-scene.unity"},locationPathName=report.exePath,target=BuildTarget.StandaloneWindows64,options=BuildOptions.DetailedBuildReport});
                    report.errors=(int)result.summary.totalErrors;report.bytes=(long)result.summary.totalSize;
                    report.status=result.summary.result.ToString();
                    if(report.status!="Succeeded")report.error="Windows build failed. See Editor.log.";
                }
                finally
                {
                    PlayerSettings.fullScreenMode=screenMode;PlayerSettings.defaultScreenWidth=width;PlayerSettings.defaultScreenHeight=height;
                    PlayerSettings.resizableWindow=resizable;busy=false;
                }
                SessionState.SetInt(Key+"phase",2);Save(folder,report);
                if(original!=BuildTarget.StandaloneWindows64&&!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(original),original))throw new InvalidOperationException("Could not restore original platform.");
                return;
            }
            if(EditorUserBuildSettings.activeBuildTarget!=original)return;
            report.platformRestored=true;report.completedUtc=DateTime.UtcNow.ToString("o");Save(folder,report);SessionState.EraseString(Key+"folder");
        }
        catch(Exception error)
        {busy=false;report.status="Failed";report.error=error.ToString();report.platformRestored=EditorUserBuildSettings.activeBuildTarget==original;report.completedUtc=DateTime.UtcNow.ToString("o");Save(folder,report);SessionState.EraseString(Key+"folder");Debug.LogException(error);}
    }
}

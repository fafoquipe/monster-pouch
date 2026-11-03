using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class DuelQaBuild
{
    const string Key="DuelQaBuild.";
    static bool busy;
    static DuelQaBuild() { EditorApplication.update += Continue; }
    public static string Status() => "target=" + EditorUserBuildSettings.activeBuildTarget + "; playing=" + EditorApplication.isPlaying + "; compiling=" + EditorApplication.isCompiling + "; build=" + BuildPipeline.isBuildingPlayer + "; failed=" + EditorUtility.scriptCompilationFailed;
    public static string Request(string destination)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer || SessionState.GetString(Key+"output", "") != "") throw new InvalidOperationException("Editor busy.");
        string output = Path.GetFullPath(destination); Directory.CreateDirectory(output);
        SessionState.SetString(Key+"output", output);
        SessionState.SetInt(Key+"original", (int)EditorUserBuildSettings.activeBuildTarget);
        SessionState.SetInt(Key+"phase", 0);
        SessionState.SetBool(Key+"scriptsOnly", false);
        return "QA build queued: " + output;
    }
    public static string RequestScripts(string destination)
    {
        string result=Request(destination); SessionState.SetBool(Key+"scriptsOnly",true); return result;
    }
    static void Continue()
    {
        string output=SessionState.GetString(Key+"output", "");
        if (output=="" || busy || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
        var original=(BuildTarget)SessionState.GetInt(Key+"original", (int)BuildTarget.StandaloneWindows64);
        try
        {
            int phase=SessionState.GetInt(Key+"phase",0);
            if(phase==0)
            {
                SessionState.SetInt(Key+"phase",1);
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64 && !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64)) throw new InvalidOperationException("Cannot switch QA target.");
                return;
            }
            if(phase==1)
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)return;
                if(EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("Editor compile failed.");
                busy=true;
                try { Build(output); }
                finally { busy=false; SessionState.SetInt(Key+"phase",2); }
            }
            if(EditorUserBuildSettings.activeBuildTarget!=original)
            {
                if(!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(original),original))throw new InvalidOperationException("Cannot restore original target.");
                return;
            }
            File.WriteAllText(Path.Combine(output,"editor-restored.txt"),Status());
            SessionState.EraseString(Key+"output");
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(output,"build-error.txt"),error.ToString());
            SessionState.EraseString(Key+"output"); Debug.LogException(error);
        }
    }
    static void Build(string output)
    {
        string company = PlayerSettings.companyName, product = PlayerSettings.productName;
        var mode = PlayerSettings.fullScreenMode; bool background = PlayerSettings.runInBackground;
        try
        {
            // Separate PlayerPrefs namespace, including Authentication session keys, from the user's game.
            PlayerSettings.companyName = "MonsterPouchLocalQA"; PlayerSettings.productName = "DuelQA20261026";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/main-scene.unity" }, target = BuildTarget.StandaloneWindows64,
                locationPathName = Path.Combine(output, "DuelQA.exe"),
                options = BuildOptions.Development | (SessionState.GetBool(Key+"scriptsOnly",false)?BuildOptions.BuildScriptsOnly:BuildOptions.None), extraScriptingDefines = new[] { "MONSTER_POUCH_DUEL_QA" }
            });
            File.WriteAllText(Path.Combine(output, "build-result.txt"), result.summary.result + " errors=" + result.summary.totalErrors);
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("QA build failed.");
        }
        finally
        {
            PlayerSettings.companyName = company; PlayerSettings.productName = product;
            PlayerSettings.fullScreenMode = mode; PlayerSettings.runInBackground = background;
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(output,"settings-restored.txt"),
                "company="+PlayerSettings.companyName+"; product="+PlayerSettings.productName+"; mode="+PlayerSettings.fullScreenMode+"; background="+PlayerSettings.runInBackground);
        }
    }
}

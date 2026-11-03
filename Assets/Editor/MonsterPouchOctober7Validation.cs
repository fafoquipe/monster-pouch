using System;
using System.IO;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MonsterPouchOctober7Validation
{
    public static string BuildPeer()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before building.");
        AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/OnlineValidation/MonsterPouch.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/main-scene.unity"},
            locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        string text=result.summary.result+"; errors="+result.summary.totalErrors+"; "+path;
        MonsterPouchLocalBridge.Reply("online-peer-build",text);return text;
    }
    public static string Host()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
        OnlineSmokeDriver.Begin(true,"",Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/local-validation/online-host.json")));
        return "Online host validation started.";
    }
    public static void OnlineMenu()=>UnityEngine.Object.FindFirstObjectByType<LocalGameUI>().OpenOnline();
}

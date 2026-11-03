using System;
using System.IO;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MonsterPouchOctober7Save
{
    static double nextSave;
    static MonsterPouchOctober7Save(){EditorApplication.update+=AutoSaveBalance;}
    static void AutoSaveBalance()
    {
        if(EditorApplication.timeSinceStartup<nextSave||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        nextSave=EditorApplication.timeSinceStartup+3;
        // Save authored assets only. Never copy runtime unit stats back into balance.
        foreach(var config in Resources.FindObjectsOfTypeAll<MatchConfig>())
            if(AssetDatabase.Contains(config)&&EditorUtility.IsDirty(config))AssetDatabase.SaveAssetIfDirty(config);
    }
    [MenuItem("Monster Pouch/Guardar proyecto y copia de balance")]
    public static string SaveProject()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Sal de Play Mode para guardar las escenas.");
        AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveOpenScenes())throw new InvalidOperationException("No se guardaron todas las escenas.");
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Backups/Balance"));
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"balance-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");
        File.WriteAllText(path,JsonUtility.ToJson(config,true));
        return "Proyecto y escenas guardados. Balance: "+path;
    }
    public static string MigrateBlotan()
    {
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        var trick=config?.Get("blotan")?.Tricks?[1];
        if(trick==null||trick.Id!="blotan-health")throw new InvalidOperationException("La segunda mejora de Blotan no coincide; no se modificó el balance.");
        var canonical=DocumentedRoster.CreateBlotan().Tricks[1];
        // Only the requested mechanic changes. Cost and all other authored values survive.
        trick.Name=canonical.Name;trick.Description=canonical.Description;trick.HealthBonus=0;
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);
        return SaveProject();
    }
}

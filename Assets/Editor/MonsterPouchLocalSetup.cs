using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MonsterPouchLocalSetup
{
    [MenuItem("Monster Pouch/Prepare complete local game")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before setup.");
        MonsterPouchArtSetup.Setup();
        const string configPath = "Assets/Resources/MonsterPouch/MatchConfig.asset";
        if (AssetDatabase.LoadAssetAtPath<MatchConfig>(configPath)==null) AssetDatabase.CreateAsset(MatchConfig.CreateDefault(),configPath);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/main-scene.unity");
        var root=GameObject.Find("local-game");if(root==null)root=new GameObject("local-game");
        if(root.GetComponent<MatchController>()==null)root.AddComponent<MatchController>();
        var ui=root.GetComponent<LocalGameUI>();if(ui==null)ui=root.AddComponent<LocalGameUI>();
        foreach(var p in Object.FindObjectsByType<PrototypeUnitSpawner>(FindObjectsInactive.Include,FindObjectsSortMode.None)){p.enabled=false;EditorUtility.SetDirty(p);}
        foreach(var d in Object.FindObjectsByType<BoardCellCenterDebugView>(FindObjectsInactive.Include,FindObjectsSortMode.None)){d.enabled=false;EditorUtility.SetDirty(d);}
        var camera=Object.FindFirstObjectByType<Camera>();camera.orthographic=true;camera.orthographicSize=11.4f;camera.transform.position=new Vector3(-.9944f,-.45f,-10);
        ui.MoonIcon=FindSprite("moon", "Assets/art/ui/resources");
        ui.RerollIcon=FindSprite("reroll", "Assets/art/ui/resources");
        var brief=GameObject.Find("briefcase");if(brief!=null)ui.BriefArt=brief.GetComponent<SpriteRenderer>()?.sprite;
        PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        MonsterPouchLocalBridge.Reply("setup","Main scene connected; prototype disabled; configuration/art imported; Windows 540x960.");
    }
    static Sprite FindSprite(string fragment,string folder)
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Sprite",new[]{folder}))
        {string path=AssetDatabase.GUIDToAssetPath(guid);if(path.ToLowerInvariant().Contains(fragment))return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();}return null;
    }
}

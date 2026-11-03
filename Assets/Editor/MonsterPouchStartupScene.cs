using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MonsterPouchStartupScene
{
    const string MainScene = "Assets/Scenes/main-scene.unity";
    const string MatchConfig = "Assets/resources/MonsterPouch/MatchConfig.asset";

    static MonsterPouchStartupScene()
    {
        EditorApplication.delayCall += OpenMainSceneWhenEditorIsEmpty;
    }

    static void OpenMainSceneWhenEditorIsEmpty()
    {
        RepairMainAssetName();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(scene.path) || scene.isDirty || scene.rootCount > 1)
            return;

        // Unity creates an untitled scene with a single Main Camera. Replace only that
        // untouched placeholder, never a saved or edited user scene.
        if (scene.rootCount == 1 && scene.GetRootGameObjects()[0].name != "Main Camera")
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) != null)
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
    }

    static void RepairMainAssetName()
    {
        // Unity reports the empty ScriptableObject name as a filename mismatch.
        // Set only the object's display name through the editor API; serialized balance data is untouched.
        UnityEngine.Object config = AssetDatabase.LoadMainAssetAtPath(MatchConfig);
        if (config == null || config.name == "MatchConfig") return;
        config.name = "MatchConfig";
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
    }
}

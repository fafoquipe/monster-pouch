using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MonsterPouchBenchSetup
{
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode before setup.");
        const string path="Assets/art/ui/bench/mini-brief.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048;
        importer.SaveAndReimport();
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/main-scene.unity")scene=EditorSceneManager.OpenScene("Assets/Scenes/main-scene.unity");
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        ui.BenchArt=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(ui.BenchArt==null)throw new System.InvalidOperationException("Mini Brief sprite failed to import.");
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
    public static void PreviewBrief()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Enter Play Mode for the layout review.");
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        var match=ui.Match;
        match.StartMatch("bugaloo");
        match.enabled=false; // Editor-only visual review; never changes the saved scene or build.
        // Keep all three offers visible in the Brief for the open-case layout review.
        ui.RenderPage();
    }
    public static void PreviewOpen()
    {
        PreviewBrief();
    }
    public static void PreviewClosed()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Enter Play Mode for the layout review.");
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        var match=ui.Match;
        match.Ready(); // Use the normal combat transition, including the Brief close behavior.
        match.enabled=false;
        ui.RenderPage();
    }
}

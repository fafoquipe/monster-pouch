using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MonsterPouchSeptemberSetup
{
    public static string ApplyAttackCorrections()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        var flo=config?.Get("flo");
        if(flo==null)throw new InvalidOperationException("Flo is missing from the playable catalog.");
        flo.AttackKind=AttackKind.Melee;flo.AttackRange=1;
        if(flo.Damage<=0)flo.Damage=3;
        flo.BasicAttackName="Golpe";flo.BasicAttackDescription="Golpea cuerpo a cuerpo.";
        var sepora=config.Get("sepora");
        if(sepora!=null)sepora.BaseAbility.Description=DocumentedRoster.CreateSepora().BaseAbility.Description;
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        return "Flo: melee, range 1; other configured character balance preserved.";
    }
    public static string BuildAttacksWindows()=>Build(BuildTarget.StandaloneWindows64,"Builds/Windows-Attacks/Monster Pouch.exe");
    public static string BuildIdleWindows()=>Build(BuildTarget.StandaloneWindows64,"Builds/Windows-Idle/Monster Pouch.exe");
    public static string CancelInterruptedValidation()
    {
        var api=typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi);
        var field=api.GetField("m_testJobDataHolder",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var holder=field?.GetValue(null);
        int canceled=0;
        if(holder!=null)
        {
            var runners=(System.Array)holder.GetType().GetMethod("GetAllRunners").Invoke(holder,null);
            foreach(var runner in runners)
            {
                var data=runner.GetType().GetMethod("GetData").Invoke(runner,null);
                var guid=(string)data.GetType().GetField("guid").GetValue(data);
                if(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.CancelTestRun(guid))canceled++;
            }
        }
        SessionState.EraseString("MonsterPouch.Validation.PendingRun");
        EditorApplication.isPlaying=false;
        return "Canceled interrupted validation runs: "+canceled+"; exit requested.";
    }
    public static string RepairVisualReferences()
    {
        OpenGame();
        var ui=UnityEngine.Object.FindFirstObjectByType<MonsterPouch.Local.LocalGameUI>();
        if(ui==null)throw new InvalidOperationException("LocalGameUI is missing from main scene.");
        ui.BriefArt=SpriteAt("Assets/art/utilities/briefcase.png");
        ui.BenchArt=SpriteAt("Assets/art/ui/bench/mini-brief.png");
        ui.ClosedBriefArt=SpriteAt("Assets/art/ui/brief/brief-closed.png");
        ui.MoonIcon=SpriteAt("Assets/art/ui/resources/ui-moon-icon.png");
        ui.RerollIcon=SpriteAt("Assets/art/ui/resources/ui-reroll-icon.png");
        EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);AssetDatabase.SaveAssets();
        return "Brief, closed brief, bench, moon and reroll references verified and saved.";
    }
    static Sprite SpriteAt(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderByDescending(s=>s.rect.width*s.rect.height).FirstOrDefault();
        if(result==null)throw new InvalidOperationException("Missing sprite at "+path);
        return result;
    }
    public static string RepairUIScript()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        const string oldPath="Assets/scripts/local/local-game-ui.cs";
        const string newPath="Assets/scripts/local/LocalGameUI.cs";
        if(AssetDatabase.LoadMainAssetAtPath(oldPath)!=null)
        {
            string error=AssetDatabase.MoveAsset(oldPath,newPath);
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
        }
        AssetDatabase.Refresh();
        return "Main partial MonoBehaviour renamed via AssetDatabase; GUID preserved.";
    }
    public static string DiagnoseScene()
    {
        return string.Join("\n",UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)
            .Select(t=>"Missing behaviour: "+t.name))+"\nUI script type: "+
            AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/scripts/local/local-game-ui.cs")?.GetClass()+"\nLocalUIinstances="+
            UnityEngine.Object.FindObjectsByType<MonsterPouch.Local.LocalGameUI>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
    }
    const string ScenePath="Assets/Scenes/main-scene.unity";
    [MenuItem("Monster Pouch/Recovery/Open playable scene")]
    public static void OpenGame()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        StageUtility.GoToMainStage();EditorSceneManager.OpenScene(ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
    }
    [MenuItem("Monster Pouch/Recovery/Apply September content")]
    public static void Setup()
    {
        OpenGame();
        const string path="Assets/resources/MonsterPouch/MatchConfig.asset";
        var current=AssetDatabase.LoadAssetAtPath<MatchConfig>(path);
        var source=MatchConfig.CreateDocumented();
        if(current==null)AssetDatabase.CreateAsset(source,path);
        else {EditorUtility.CopySerialized(source,current);UnityEngine.Object.DestroyImmediate(source);EditorUtility.SetDirty(current);}
        ImportBackground("Assets/resources/MonsterPouch/UI/home-background.png");
        ImportBackground("Assets/resources/MonsterPouch/UI/deck-background.png");
        PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait=true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
        PlayerSettings.allowedAutorotateToLandscapeLeft=false;PlayerSettings.allowedAutorotateToLandscapeRight=false;
        PlayerSettings.runInBackground=true;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        Debug.Log("September setup: 5 Monsters,14 Whelps,3 local decks,portrait layout,main scene ready.");
    }
    static void ImportBackground(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new FileNotFoundException(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.maxTextureSize=2048;importer.SaveAndReimport();
    }
    [MenuItem("Monster Pouch/Build/Windows September")]
    public static string BuildWindows()=>Build(BuildTarget.StandaloneWindows64,"Builds/Windows-September/Monster Pouch.exe");
    [MenuItem("Monster Pouch/Build/Android September APK")]
    public static string BuildAndroid()
    {
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new InvalidOperationException("Install Android Build Support for this Unity version.");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.monsterpouch.game");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle=false;
        return Build(BuildTarget.Android,"Builds/Android-September/Monster Pouch.apk");
    }
    static string Build(BuildTarget target,string path)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before building.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName=path,target=target,options=BuildOptions.None});
        string result=report.summary.result+"; errors="+report.summary.totalErrors+"; bytes="+report.summary.totalSize+"; path="+Path.GetFullPath(path);
        Directory.CreateDirectory("Logs/local-validation");File.WriteAllText("Logs/local-validation/september-"+target+".txt",result);
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(result);
        return result;
    }
}

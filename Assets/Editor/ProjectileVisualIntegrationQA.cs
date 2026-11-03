using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;

// Temporary integration runner. No automatic tests, saves, scene changes or profile edits.
[InitializeOnLoad]
public static class ProjectileVisualIntegrationQA
{
    const string Pending="ProjectileVisualIntegrationQA.Pending";
    const string Folder="Logs/local-validation/projectile-visual-editor";
    static ProjectileVisualIntegrationQA(){TestRunnerApi.RegisterTestCallback(new Results());}
    [Serializable] public class SceneRecord { public string path; public bool dirty; }
    [Serializable] public class State {
        public bool playing,compiling,updating,compilationFailed;
        public SceneRecord[] scenes; public string[] dirtyAssets;
        public string selectedAsset; public bool balanceSpanish; public string gameLanguage;
    }
    public static string Inspect()
    {
        var value=new State{
            playing=EditorApplication.isPlayingOrWillChangePlaymode,compiling=EditorApplication.isCompiling,
            updating=EditorApplication.isUpdating,compilationFailed=EditorUtility.scriptCompilationFailed,
            scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>{var s=SceneManager.GetSceneAt(i);return new SceneRecord{path=s.path,dirty=s.isDirty};}).ToArray(),
            dirtyAssets=Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(o=>EditorUtility.IsPersistent(o)&&EditorUtility.IsDirty(o))
                .Select(AssetDatabase.GetAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p).ToArray(),
            selectedAsset=AssetDatabase.GetAssetPath(Selection.activeObject),
            balanceSpanish=EditorPrefs.GetBool("MonsterPouch.Balance.Spanish",false),gameLanguage=PlayerPrefs.GetString("monster-pouch.language","")};
        return JsonUtility.ToJson(value,true);
    }
    [Serializable] public class IconRecord {public string key,path;public bool loaded;}
    [Serializable] public class IconReport {public int count;public bool passed;public IconRecord[] icons;}
    public static string VerifyIcons()
    {
        var rows=new List<IconRecord>();
        Action<string,Sprite> add=(key,sprite)=>rows.Add(new IconRecord{key=key,path=AssetDatabase.GetAssetPath(sprite),loaded=sprite!=null});
        var characters=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").Characters;
        foreach(var c in characters){add(c.Id+"/profile",c.Icon);add(c.Id+"/basic",AbilityUiCatalog.Get(c.Id+"/basic"));}
        add("martio-orbits",AbilityArtwork.Get("martio-orbits"));add("blotan-health",AbilityUiCatalog.Get("blotan-health"));
        add("monsters/normal",LeaderBadgeArtwork.Get("martio",false));add("monsters/upgraded",LeaderBadgeArtwork.Get("martio",true));
        var report=new IconReport{count=rows.Count,passed=characters.Length==24&&rows.All(r=>r.loaded),icons=rows.ToArray()};
        string json=JsonUtility.ToJson(report,true);Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/native-icons.json",json);return json;
    }
    public static string ReimportUIIcons()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first.");
        var manifest=JsonUtility.FromJson<AbilityUiManifest>(Resources.Load<TextAsset>("MonsterPouch/UI/AbilityArt/catalog").text);
        var paths=manifest.entries.Where(e=>!string.IsNullOrEmpty(e.resource)).Select(e=>"Assets/resources/"+e.resource+".png").Distinct().ToArray();
        foreach(var path in paths)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        foreach(var path in paths)if((AssetImporter.GetAtPath(path) as TextureImporter)?.textureType!=TextureImporterType.Sprite)
            throw new InvalidOperationException("Importer still rejects Sprite: "+path);
        return "Reimported and verified "+paths.Length+" declared ability Sprite resources.";
    }
    public static string RestoreGameZoom()
    {
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var view=Resources.FindObjectsOfTypeAll(type).FirstOrDefault();if(view==null)return "GameView not open.";
        var field=type.GetField("m_ZoomArea",BindingFlags.Instance|BindingFlags.NonPublic);
        var zoom=field?.GetValue(view);var scale=zoom?.GetType().GetProperty("scale",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(scale==null||!scale.CanWrite)return "Zoom restoration unavailable.";
        var before=(Vector2)scale.GetValue(zoom);scale.SetValue(zoom,Vector2.one);((EditorWindow)view).Repaint();
        return "GameView zoom restored from "+before+" to original 1x.";
    }
    public static string Language(string language)
    {
        if(!MonsterPouchNationNativePreview.Status().StartsWith("Ready"))throw new InvalidOperationException("Only change language inside the recoverable QA preview.");
        if(language!="es"&&language!="en")throw new ArgumentException("Use es or en.");
        GameText.SetLanguage(language=="es");return "QA language="+language;
    }
    [Serializable] public class CardRecord {public string name,spritePath;public Rect screen;}
    [Serializable] public class CardReport {public int width,height;public bool actionOverlapsNavigation;public CardRecord[] controls;}
    public static string InspectCard()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)throw new InvalidOperationException("Use isolated native preview first.");
        Canvas.ForceUpdateCanvases();
        var rows=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r=>new[]{"Unit sheet","ability-upgrade","Navigation","Selected ability","skill-slot--2","skill-slot--1"}.Contains(r.name)||r.name.StartsWith("art-"))
            .Select(r=>{var corners=new Vector3[4];r.GetWorldCorners(corners);var sprite=r.GetComponentsInChildren<Image>().FirstOrDefault(i=>i.sprite!=null)?.sprite;
                return new CardRecord{name=r.name,screen=Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y),spritePath=AssetDatabase.GetAssetPath(sprite)};}).ToArray();
        var action=rows.FirstOrDefault(r=>r.name=="ability-upgrade");var navigation=rows.FirstOrDefault(r=>r.name=="Navigation");
        var report=new CardReport{width=Screen.width,height=Screen.height,controls=rows,actionOverlapsNavigation=action!=null&&navigation!=null&&action.screen.Overlaps(navigation.screen)};
        return JsonUtility.ToJson(report,true);
    }
    public static string Run(string mode)
    {
        bool active=(bool)typeof(TestRunnerApi).GetMethod("IsRunActive",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        if(active||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
            throw new InvalidOperationException("Editor is busy.");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("An open scene has unsaved changes; do not discard them.");
        if(mode!="edit"&&mode!="play"&&mode!="ui"&&mode!="ui-import"&&mode!="ui-art"&&mode!="preview"&&mode!="martio-edit"&&mode!="martio-play"&&mode!="maps-edit"&&mode!="maps-play"&&mode!="core-combat"&&mode!="core-presentation"&&mode!="core-play"&&mode!="lobby-play"&&mode!="catalog-edit"&&mode!="catalog-play"&&mode!="gallery-play")throw new ArgumentException("Unknown focused run.");
        string path=Path.GetFullPath(Folder+"/native-"+mode+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".xml");
        SessionState.SetString(Pending,path);
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter{testMode=mode=="edit"||mode=="ui-import"||mode=="preview"||mode=="martio-edit"||mode=="maps-edit"||mode=="core-combat"||mode=="core-presentation"||mode=="catalog-edit"?TestMode.EditMode:TestMode.PlayMode,
            testNames=mode=="preview"?new[]{"ProjectileVisualPlaybackTests"}:
                mode=="martio-edit"?new[]{"MonsterPouch.Gameplay.Tests.EditMode.EronyaWholeBodyPresentationTests","MonsterPouch.Gameplay.Tests.EditMode.MartioFanReworkTests","MonsterPouch.Gameplay.Tests.EditMode.MartioCombatIntegrationTests","MonsterPouch.Gameplay.Tests.EditMode.MartioProjectileTests","MonsterPouch.Gameplay.Tests.EditMode.MartioHeadingRangeTests"}:
                mode=="martio-play"?new[]{"MartioFanReworkPlayTests"}:
                mode=="maps-edit"?new[]{"CasinoNightIdentityTests","LobbyCosmeticIdentityTests","OnlineMatchTests"}:
                mode=="maps-play"?new[]{"LobbyThemeSelectionPlayTests","NationThemeCompatibilityPlayTests","NationArenaSceneryPlayTests"}:
                mode=="lobby-play"?new[]{"LobbyThemeSelectionPlayTests"}:
                mode=="catalog-edit"?new[]{"MonsterPouch.Gameplay.Tests.EditMode.BalanceInspectorLayoutTests"}:
                mode=="catalog-play"?new[]{"CharacterPreviewPlayTests"}:
                mode=="gallery-play"?new[]{"MapMiniatureResourcePlayTests","MapMiniatureGalleryPlayTests","NationArenaSceneryPlayTests"}:
                mode=="core-combat"?new[]{"MonsterPouch.Gameplay.Tests.EditMode.EronyaGeotharIntegrationTests"}:
                mode=="core-presentation"?new[]{"MonsterPouch.Gameplay.Tests.EditMode.EronyaDisplacementPresentationTests","MonsterPouch.Gameplay.Tests.EditMode.EronyaWholeBodyPresentationTests","MonsterPouch.Tests.EronyaRoundUiTests","UnitStatusFrameTests"}:
                mode=="core-play"?new[]{"EronyaRootOwnershipPlayTests","MonsterPouch.Local.Tests.PlayMode.StatusPresentationPlayTests","ProjectileVisualControlsTests"}:
                mode=="ui-import"?new[]{"AbilityArtImportPipelineTests"}:
                mode=="ui-art"?new[]{"AbilitySummaryCardsPlayTests.RegisteredBasicPassiveActiveAndUpgradeArtLoadsAsSprites","AbilitySummaryCardsPlayTests.BasicCardActionAndStatusRemainAboveNavigation"}:
                mode=="ui"?new[]{"AbilitySummaryCardsPlayTests.RegisteredBasicPassiveActiveAndUpgradeArtLoadsAsSprites","AbilitySummaryCardsPlayTests.MissingAbilityArtworkUsesNeutralPlaceholder","AbilitySummaryCardsPlayTests.BasicCardActionAndStatusRemainAboveNavigation"}:
                mode=="edit"?new[]{"EronyaBalanceOwnerUndoTests","MonsterPouch.Gameplay.Tests.EditMode.AbilitySummaryDescriptionTests"}:
                new[]{"ProjectileVisualControlsTests","AbilityDescriptionPlayTests"}}));
        return path;
    }
    [Serializable] public sealed class VisualReview { public string id,note; public bool passed; }
    public static string ImportFaction(string faction)=>EronyaThreeFactionImportValidation.ValidateBatch(faction,false);
    public static string InspectGestureImport()
    {
        var rows=new List<object>();
        foreach(var id in new[]{"cael","guarda-veterano","arboreo","antidota"})
        {
            string path="Assets/resources/MonsterPouch/Characters/"+id+"/Art/lobby-gesture.png";
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.GetSourceTextureWidthAndHeight(out int w,out int h);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            rows.Add(new{id,path,sourceWidth=w,sourceHeight=h,maxSize=ti.maxTextureSize,
                defaults=ti.GetDefaultPlatformTextureSettings(),standalone=ti.GetPlatformTextureSettings("Standalone"),
                importerPpu=ti.spritePixelsPerUnit,spritePpu=sprite.pixelsPerUnit,rect=sprite.rect.ToString(),dirty=EditorUtility.IsDirty(ti)});
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented);
    }
    public static string ReimportNaturalisGestures()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Exit Play first.");
        var paths=new[]{"cael","guarda-veterano","arboreo","antidota"}.Select(id=>"Assets/resources/MonsterPouch/Characters/"+id+"/Art/lobby-gesture.png").ToArray();
        foreach(var path in paths)if(EditorUtility.IsDirty(AssetImporter.GetAtPath(path)))throw new InvalidOperationException("Dirty importer: "+path);
        foreach(var path in paths)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        return InspectGestureImport();
    }
    public static string ShowMapGallery()
    {
        if(!MonsterPouchNationNativePreview.Status().StartsWith("Ready; QA only;"))throw new InvalidOperationException("Use isolated QA preview.");
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        typeof(LocalGameUI).GetMethod("ShowMapSkins",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(ui,null);
        return "QA map gallery opened.";
    }
    static string uiState="Not started.";
    public static string FactionUiStatus()=>uiState;
    public static string RunFactionUi(string faction)
    {
        if(uiState.StartsWith("Running ",StringComparison.Ordinal))throw new InvalidOperationException("UI batch already active.");
        // UI and presentation have independent evidence. A domain reload must not
        // discard an already reviewed presentation report or prevent UI sampling.
        var presentation=MonsterPouchEronyaPresentationNativeSmoke.Status();
        if(presentation.Contains("\"status\": \"running\""))throw new InvalidOperationException("Finish presentation sampling before resizing the UI.");
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!MonsterPouchNationNativePreview.Status().StartsWith("Ready; QA only;"))throw new InvalidOperationException("Use the existing isolated native session.");
        uiState="Running "+faction;
        ui.StartCoroutine(MonsterPouchNationUiAcceptance.Run(ui,faction,Path.GetFullPath(Folder+"/faction-ui"),result=>uiState=JsonUtility.ToJson(result,true)));
        return uiState;
    }
    [Serializable] public sealed class VisualReviews { public VisualReview[] reviews; }
    public static string ReviewPresentation(string json)
    {
        var value=JsonUtility.FromJson<VisualReviews>(json);
        if(value?.reviews==null||value.reviews.Length!=4)throw new ArgumentException("Supply exactly four explicitly inspected character reviews.");
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchEronyaPresentationNativeSmoke")).FirstOrDefault(t=>t!=null);
        var method=type?.GetMethod("RecordVisualReview",BindingFlags.Public|BindingFlags.Static);
        if(method==null)throw new InvalidOperationException("Native presentation helper not installed.");
        object result=null;
        foreach(var review in value.reviews)result=method.Invoke(null,new object[]{review.id,review.passed,review.note});
        return (string)result;
    }
    sealed class Results:ICallbacks
    {
        public void RunStarted(ITestAdaptor test){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){}
        public void RunFinished(ITestResultAdaptor result)
        {
            string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));TestRunnerApi.SaveResultToFile(result,path);
            bool empty=result.PassCount+result.FailCount+result.SkipCount==0;
            File.WriteAllText(path+".txt",(empty?"INVALID: no tests matched":result.ResultState)+"; passed="+result.PassCount+"; failed="+result.FailCount+"; skipped="+result.SkipCount);
            SessionState.EraseString(Pending);
        }
    }
}

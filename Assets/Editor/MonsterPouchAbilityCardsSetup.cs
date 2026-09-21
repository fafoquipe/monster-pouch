using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MonsterPouchAbilityCardsSetup
{
    public static string PreviewCard()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<MonsterPouch.Local.LocalGameUI>();
        ui.Match.StartMatch("anuik",new[]{"trimol"});ui.Match.enabled=false;
        ui.ShowInspection(ui.Match.Player.Monster,false);
        MonsterPouchPlayInspection.Capture("ability-card");return "Card preview ready.";
    }
    public static string PreviewHome()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<MonsterPouch.Local.LocalGameUI>();
        ui.CloseModal();ui.Match.Menu();ui.OpenHome();
        MonsterPouchPlayInspection.Capture("menu-items");return "Home preview ready.";
    }
    [MenuItem("Monster Pouch/Content/Apply ability cards")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        foreach(string id in new[]{"brief","guia","sonido","figuras","batalla"})
        {
            string path="Assets/resources/MonsterPouch/UI/MenuItems/"+id+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=512;importer.SaveAndReimport();
        }
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        Undo.RecordObject(config,"Initialize editable ability values");
        int count=0;
        foreach(var unit in config.Units)
        {
            if(unit.ProjectileSpeed<=0)unit.ProjectileSpeed=10;
            foreach(var effect in new[]{unit.BaseAbility}.Concat(unit.Tricks??Array.Empty<TrickDefinition>()))
            {
                if(effect==null)continue;
                effect.Parameters=AbilityBalance.Resolve(effect);
                effect.Description=AbilityBalance.Description(effect);count++;
            }
        }
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        return "Imported 5 menu objects; initialized "+count+" abilities without replacing character balance.";
    }
    [MenuItem("Monster Pouch/Build/Windows Ability Cards")]
    public static string BuildWindows()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        const string path="Builds/Windows-Cards/Monster Pouch.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/main-scene.unity"},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        string result=report.summary.result+"; errors="+report.summary.totalErrors+"; path="+Path.GetFullPath(path);
        File.WriteAllText("Logs/local-validation/ability-cards-build.txt",result);
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(result);
        return result;
    }
}

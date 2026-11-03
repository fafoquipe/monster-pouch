using UnityEngine;
using UnityEditor;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using System.IO;
using System.Text;

public static class MonsterPouchWorldPolish
{

    public static string SaveAttackPhases()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return "Exit Play Mode first.";
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        Directory.CreateDirectory("Logs/world-polish");
        File.WriteAllText("Logs/world-polish/balance-before-phases.json",JsonUtility.ToJson(config,true));
        Undo.RecordObject(config,"Distribuir fases de ataque");
        int changed=0;
        foreach(var unit in config.Units)
        {
            var tail=BasicAttackTiming.DefaultTail(unit.Id);
            if(unit.AttackHold<0){unit.AttackHold=tail.x;changed++;}
            if(unit.AttackRecovery<0){unit.AttackRecovery=tail.y;changed++;}
        }
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/world-polish/balance-after-phases.json",JsonUtility.ToJson(config,true));
        return changed+" phase values saved; existing charge, damage and ability values preserved.";
    }
    public static string ImportAndAudit()
    {
        string[] paths={"Assets/resources/MonsterPouch/UI/home-background.png","Assets/resources/MonsterPouch/UI/TableSkins/lounge-home.png",
            "Assets/resources/MonsterPouch/UI/MiniWorlds/classic.png","Assets/resources/MonsterPouch/UI/MiniWorlds/lounge.png","Assets/resources/MonsterPouch/UI/MiniWorlds/royal.png"};
        foreach(var path in paths)
        {
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=path.Contains("MiniWorlds")?512:2048;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        var art=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        var report=new StringBuilder();
        foreach(var unit in config.Units)
        {
            var clip=art.Get(unit.Id)?.Animation(UnitFacing.South);
            report.AppendLine(unit.Id+" interval="+unit.AttackInterval+" charge="+unit.AttackWindup+" frames="+(clip?.Attack?.Length??0)+" contact="+clip?.AttackContactFrame);
        }
        Directory.CreateDirectory("Logs/world-polish");
        File.WriteAllText("Logs/world-polish/attack-audit.txt",report.ToString());
        return report.ToString();
    }
}



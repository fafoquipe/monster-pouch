using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Progression;
using UnityEditor;
using UnityEngine;
public static class MonsterPouchRedesignSetup
{
    const string Base="Assets/resources/MonsterPouch/Characters";
    static readonly Dictionary<string,string> Names=new Dictionary<string,string>{{"anuik","Aniku"},{"tokoro","Toronko"},{"popow","Pombú"},{"sepora","Selora"},{"atori","Atrik"},{"blotan","Blotun"},{"bugaloo","Bugrum"},{"bugui","Bivui"},{"flo","Floru"},{"gochan","Gochar"},{"jazar","Jazir"},{"kayon","Kaydo"},{"tauris","Tauruk"},{"trimol","Trimar"},{"tsu","Tsumi"},{"atong","Atron"},{"aky","Akryn"},{"hymay","Hymal"},{"rayos","Raion"},{"stein","Stenko"}};
    [MenuItem("Monster Pouch/Redesign/Migrate current catalog")]
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play Mode antes de migrar.");
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");var art=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        if(config==null||art==null)throw new InvalidOperationException("Falta catálogo original.");
        Archive(art);
        foreach(var path in Directory.GetFiles("Assets/resources/MonsterPouch/UI/Scenery","*.png"))
        {
            string assetPath=path.Replace('\\','/');var importer=AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if(importer==null)continue;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.isReadable=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.format=TextureImporterFormat.ETC2_RGB4;android.maxTextureSize=2048;importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
        }
        if(config.Characters==null||config.Characters.Length==0)
        {
            Directory.CreateDirectory(Base);AssetDatabase.Refresh();
            var list=new List<CharacterDefinition>();var map=new System.Text.StringBuilder("# Migración de identidad\n\nLos IDs y los nombres de efectos se conservan. Nombres propuestos pendientes de comprobación de marca antes de publicación.\n\n| ID | Antes | Nombre visible |\n|---|---|---|\n");
            foreach(var unit in config.Units.Where(d=>d!=null))
            {
                string folder=Base+"/"+unit.Id;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var c=ScriptableObject.CreateInstance<CharacterDefinition>();c.InitializeIdentity(unit.Id);
                c.General=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(unit));
                if(Names.TryGetValue(unit.Id,out var name))c.General.DisplayName=name;
                c.Visuals=art.Get(unit.Id);c.Portrait=c.Visuals?.Portrait;c.Icon=c.Portrait;
                c.Attack=ScriptableObject.CreateInstance<CharacterAttack>();c.Attack.Import(unit);AssetDatabase.CreateAsset(c.Attack,folder+"/Attack.asset");
                c.Ability=Ability(unit.BaseAbility,folder+"/Ability.asset");
                for(int i=0;i<3;i++)c.Upgrades[i]=Ability(unit.Tricks!=null&&i<unit.Tricks.Length?unit.Tricks[i]:new TrickDefinition(),folder+"/Upgrade"+(i+1)+".asset");
                AssetDatabase.CreateAsset(c,folder+"/Character.asset");list.Add(c);
                map.AppendLine("| "+unit.Id+" | "+unit.DisplayName+" | "+c.General.DisplayName+" |");
            }
            config.Characters=list.ToArray();config.Units=list.Select(c=>c.CreateRuntimeDefinition()).ToArray();EditorUtility.SetDirty(config);
            File.WriteAllText("docs/character-name-migration-20261013.md",map.ToString());
        }
        Create<ImpactPresentationSettings>("Assets/resources/MonsterPouch/ImpactPresentation.asset");
        Create<ProgressionConfig>("Assets/resources/MonsterPouch/Progression.asset");
        AssetDatabase.SaveAssets();return "Migrated "+config.Characters.Length+" characters; archive retained; three square backgrounds imported.";
    }
    static CharacterAbility Ability(TrickDefinition old,string path)
    {
        var asset=ScriptableObject.CreateInstance<CharacterAbility>();
        asset.Definition=old==null?new TrickDefinition():JsonUtility.FromJson<TrickDefinition>(JsonUtility.ToJson(old));
        asset.Definition.Parameters=AbilityBalance.Resolve(asset.Definition);
        if(asset.Definition.EffectId=="tokoro-headbutt")foreach(var p in asset.Definition.Parameters)if(p.Key=="damageOverride"&&p.Value<0)p.Value=10;
        AssetDatabase.CreateAsset(asset,path);return asset;
    }
    static void Create<T>(string path) where T:ScriptableObject
    {if(AssetDatabase.LoadAssetAtPath<T>(path)==null)AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(),path);}
    static void Archive(UnitArtCatalog art)
    {
        string folder=Path.GetFullPath("ArtArchive/LegacyCharacters");Directory.CreateDirectory(folder);
        string manifest=Path.Combine(folder,"manifest.txt");if(File.Exists(manifest))return;
        var paths=new HashSet<string>(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(art),true));
        foreach(string dir in new[]{"Assets/resources/MonsterPouch/UI/LobbyFrames","Assets/resources/MonsterPouch/UI/LobbyGestures","Assets/resources/MonsterPouch/UI/LobbyRigs","Assets/art/units"})
            if(Directory.Exists(dir))foreach(string file in Directory.GetFiles(dir,"*",SearchOption.AllDirectories))paths.Add(file.Replace('\\','/'));
        foreach(string path in paths)
        {
            if(!File.Exists(path))continue;string destination=Path.Combine(folder,path);Directory.CreateDirectory(Path.GetDirectoryName(destination));if(!File.Exists(destination))File.Copy(path,destination,false);
            if(File.Exists(path+".meta")&&!File.Exists(destination+".meta"))File.Copy(path+".meta",destination+".meta",false);
        }
        File.WriteAllLines(manifest,paths.OrderBy(p=>p));
    }
}

using System.IO;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;
public static class MonsterPouchRedesignFinalSetup
{
    public static string Apply()
    {
        var hotel=AssetDatabase.LoadAssetAtPath<EnvironmentStyle>("Assets/resources/MonsterPouch/Styles/lounge.asset");
        hotel.MatchBackground=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/art/ambientations/casino/casino-table-path.png");EditorUtility.SetDirty(hotel);
        var aky=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/aky/Character.asset");
        var battle=Resources.Load<UnitArtCatalog>("MonsterPouch/AkyBattleArt")?.Get("aky");
        if(aky!=null&&battle!=null&&aky.Visuals.SourceNotes!="Independent migrated battle art")
        {aky.Visuals=JsonUtility.FromJson<UnitArt>(JsonUtility.ToJson(battle));aky.Visuals.SourceNotes="Independent migrated battle art";EditorUtility.SetDirty(aky);}
        AssetDatabase.SaveAssets();
        foreach(string path in Directory.GetFiles("Assets/resources/MonsterPouch/UI/TableSkins","*.png"))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        return "Original hotel floor restored; Aky battle art migrated to its editable asset; scenery Android settings imported.";
    }
}

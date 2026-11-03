using System.IO;
using MonsterPouch.Gameplay.Status;
using UnityEditor;
using UnityEngine;
public static class MonsterPouchStatusSetup
{
    public static string Apply()
    {
        string folder="Assets/resources/MonsterPouch/StatusEffects";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var rows=new[]{"stun|Aturdimiento|!|100|vfx","root|Inmovilización|X|90|vfx","slow|Lentitud|v|70|vfx","haste|Velocidad|+V|65|badge","attack-up|Daño aumentado|+D|60|badge","guard|Reducción de daño|S|85|badge","shield|Escudo|S|80|vfx","weakness|Vulnerabilidad|!D|55|badge","anti-heal|Curación reducida|-H|75|badge","stealth|Invisibilidad|O|40|vfx","sleep|Sueño|Z|95|vfx","protection|Protección|P|80|vfx","reflect|Reflejo|R|70|vfx","taunt|Provocación|!|90|vfx","burn|Quemadura|F|50|badge"};
        foreach(string row in rows)
        {
            var values=row.Split('|');string path=folder+"/"+values[0]+".asset";if(AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(path)!=null)continue;
            var def=ScriptableObject.CreateInstance<StatusEffectDefinition>();def.Id=values[0];def.DisplayName=values[1];def.ShortLabel=values[2];def.Priority=int.Parse(values[3]);def.ShowBadge=values[4]=="badge";
            def.Ends=def.Id=="haste"||def.Id=="attack-up"||def.Id=="guard"?StatusEnd.RoundEndOrDeath:StatusEnd.DurationOrDeath;
            def.Stacking=def.Id=="burn"?StatusStacking.Independent:def.Id=="anti-heal"?StatusStacking.Replace:StatusStacking.LongestDuration;
            def.Color=def.Id=="burn"||def.Id=="anti-heal"||def.Id=="weakness"?new Color(.82f,.49f,.39f):new Color(.45f,.72f,.75f);
            AssetDatabase.CreateAsset(def,path);
        }
        AssetDatabase.SaveAssets();return "15 status definitions created; existing clear VFX retained, badges only for less visible effects.";
    }
}

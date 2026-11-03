using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEditor;

public static class MonsterPouchSteinRework
{
    public static string Apply()
    {
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        if(config==null)throw new System.InvalidOperationException("MatchConfig not found.");
        var stein=config.Get("stein");
        if(stein==null)throw new System.InvalidOperationException("Stein not found.");
        Undo.RecordObject(config,"Stein energy super rework");
        var updated=DocumentedRoster.CreateStein();
        var old=stein.Tricks;
        foreach(var trick in updated.Tricks)
        {
            var previous=old.FirstOrDefault(t=>t!=null&&(t.EffectId??t.Id)==trick.EffectId);
            if(previous!=null)trick.Cost=previous.Cost;
            // Keep the existing energy gift, including any editor balance override.
            if(trick.EffectId=="stein-energy-gift"&&previous!=null)trick.Parameters=previous.Parameters;
        }
        stein.BasicAttackName=updated.BasicAttackName;stein.BasicAttackDescription=updated.BasicAttackDescription;
        stein.EnergyMax=updated.EnergyMax;stein.EnergyPerAttack=updated.EnergyPerAttack;
        stein.EnergyOnDamage=updated.EnergyOnDamage;stein.EnergyPerSecond=updated.EnergyPerSecond;
        stein.BaseAbility=updated.BaseAbility;stein.Tricks=updated.Tricks;
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        return "Stein: single basic; energy super 3/4 enemies, 2/3.5 s stun; third star energy gift preserved.";
    }
}

using System;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;

/// <summary>Applies only the user's Tauris melee correction to the existing configuration.</summary>
public static class MonsterPouchTaurisMeleeSetup
{
    private const string ConfigPath = "Assets/Resources/MonsterPouch/MatchConfig.asset";

    [MenuItem("Monster Pouch/Combat/Make Tauris melee")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before changing the saved combat configuration.");
        MatchConfig config = AssetDatabase.LoadAssetAtPath<MatchConfig>(ConfigPath);
        UnitDefinition tauris = config != null ? config.Get("tauris") : null;
        if (tauris == null) throw new InvalidOperationException("Tauris must already exist in MatchConfig.");
        if (tauris.BaseAbility == null) throw new InvalidOperationException("Tauris must retain his existing base ability.");

        UnitDefinition reference = ExpandedRoster.CreateTauris();
        if (tauris.AttackRange == reference.AttackRange && tauris.BaseAbility.Description == reference.BaseAbility.Description)
        {
            Debug.Log("Monster Pouch: Tauris already uses melee attacks and the matching description.");
            return;
        }
        Undo.RecordObject(config, "Apply Tauris melee correction");
        // Mutate these two fields only; never replace Tauris or rebuild the roster/art catalogs.
        tauris.AttackRange = reference.AttackRange;
        tauris.BaseAbility.Description = reference.BaseAbility.Description;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
        Debug.Log("Monster Pouch: Tauris is melee (range 1). Existing HP, damage, lethal cadence, timing, formation, upgrade, other units and artwork were preserved.");
    }
}

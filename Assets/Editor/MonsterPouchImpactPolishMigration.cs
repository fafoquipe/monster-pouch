using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;

/// <summary>Upgrades retired stock defaults without replacing the player's balance edits.</summary>
public static class MonsterPouchImpactPolishMigration
{
    [MenuItem("Monster Pouch/Rules/Apply dash flower and rhythm polish")]
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before migrating saved ability values.");
        MatchConfig config = AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/Resources/MonsterPouch/MatchConfig.asset");
        if (config == null) throw new InvalidOperationException("MatchConfig is missing.");
        Undo.RecordObject(config, "Update dash, flower cast and infinite rhythm");
        UnitDefinition bugui = config.Get("bugui");
        TrickDefinition unlimited = bugui?.Tricks?.FirstOrDefault(t => t != null && (t.EffectId == "bugui-unlimited-stacks" || t.Id == "bugui-unlimited-stacks"));
        if (unlimited != null)
        {
            var parameters = new List<AbilityParameter>(unlimited.Parameters ?? Array.Empty<AbilityParameter>());
            AbilityParameter speed = parameters.Find(p => p != null && p.Key == "speedPercent");
            if (speed == null) parameters.Add(new AbilityParameter { Key = "speedPercent", Value = 5 });
            else if (Mathf.Approximately(speed.Value, 2.5f)) speed.Value = 5;
            unlimited.Parameters = parameters.ToArray();
            unlimited.Description = AbilityBalance.Description(unlimited, bugui);
        }
        EnsureParameter(config.Get("popow")?.BaseAbility, "travelDuration", .38f);
        EnsureParameter(config.Get("popow")?.BaseAbility, "width", 1);
        EnsureParameter(config.Get("flo")?.BaseAbility, "castDuration", .45f);
        foreach (string id in new[] { "popow", "flo" })
        {
            UnitDefinition definition = config.Get(id);
            if (definition?.BaseAbility != null)
                definition.BaseAbility.Description = AbilityBalance.Description(definition.BaseAbility, definition);
        }
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        return "Bugui stock unlimited speed is 5%; custom values preserved. Popow lane width/travel and Flo cast duration are exposed.";
    }

    private static void EnsureParameter(TrickDefinition ability, string key, float value)
    {
        if (ability == null) return;
        var parameters = new List<AbilityParameter>(ability.Parameters ?? Array.Empty<AbilityParameter>());
        if (!parameters.Exists(p => p != null && p.Key == key))
            parameters.Add(new AbilityParameter { Key = key, Value = value });
        ability.Parameters = parameters.ToArray();
    }
}

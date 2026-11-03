using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using UnityEditor;

/// <summary>Replaces only Anuik's retired movement upgrade and preserves the saved price.</summary>
public static class MonsterPouchAnuikBounceMigration
{
    private const string ConfigPath = "Assets/Resources/MonsterPouch/MatchConfig.asset";

    [MenuItem("Monster Pouch/Rules/Replace Anuik jump with ricochet")]
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before updating Anuik's saved ability.");
        MatchConfig config = AssetDatabase.LoadAssetAtPath<MatchConfig>(ConfigPath);
        UnitDefinition anuik = config != null ? config.Get("anuik") : null;
        if (anuik == null || anuik.Tricks == null || anuik.Tricks.Length < 3)
            throw new InvalidOperationException("Anuik and all three upgrade slots must exist in MatchConfig.");

        Undo.RecordObject(config, "Replace Anuik jump with ricochet");
        TrickDefinition current = anuik.Tricks[2];
        if (current != null && current.Id == "anuik-bounce" && current.EffectId == "anuik-bounce")
        {
            var parameters = new List<AbilityParameter>(current.Parameters ?? Array.Empty<AbilityParameter>());
            if (!parameters.Exists(parameter => parameter != null && parameter.Key == "bounceDamageMultiplier"))
                parameters.Add(new AbilityParameter { Key = "bounceDamageMultiplier", Value = .5f });
            current.Parameters = parameters.ToArray();
        }
        else
        {
            int savedCost = current != null ? current.Cost : DocumentedRoster.CreateAnuik().Tricks[2].Cost;
            TrickDefinition replacement = DocumentedRoster.CreateAnuik().Tricks[2];
            replacement.Cost = savedCost;
            replacement.Parameters = AbilityBalance.Resolve(replacement);
            anuik.Tricks[2] = replacement;
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        return "Anuik's third upgrade is Ricochet Hearts; bounceDamageMultiplier=0.5; saved price preserved.";
    }
}

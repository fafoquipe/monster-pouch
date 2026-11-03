using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Read-only aggregation of the validation and ownership rules already used by the game.</summary>
public static class MonsterPouchCatalogueValidation
{
    [MenuItem("Tools/Monster Pouch/Roadmap/Validar catálogo y referencias")]
    public static void Run()
    {
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        var problems = Validate(config);
        if (problems.Length == 0)
            Debug.Log("Catálogo y referencias de Monster Pouch: sin errores. No se modificaron los recursos.", config);
        else
            Debug.LogError("Catálogo de Monster Pouch:\n" + string.Join("\n", problems), config);
    }

    public static string[] Validate(MatchConfig config)
    {
        var problems = new List<string>();
        if (config == null) return new[] { "Falta MatchConfig." };
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var abilityIds = new Dictionary<string, string>(StringComparer.Ordinal);
        MissingReferences(config, "MatchConfig", problems);
        if (config.Characters != null && config.Characters.Length > 0)
        {
            for (int i = 0; i < config.Characters.Length; i++)
            {
                var character = config.Characters[i];
                if (character == null)
                {
                    problems.Add("Characters[" + i + "]: falta la referencia al personaje.");
                    continue;
                }
                string label = Label(character.General?.DisplayName, character.Id, "Characters[" + i + "]");
                Identity(character.Id, label, ids, problems);
                if (string.IsNullOrWhiteSpace(character.General?.DisplayName))
                    problems.Add(label + ": falta el nombre visible.");
                string existing = character.Validate();
                if (!string.IsNullOrEmpty(existing)) problems.Add(label + ": " + existing);
                if (character.General != null && character.General.Id != character.Id)
                    problems.Add(label + ": General.Id no coincide con su ID estable.");
                MissingReferences(character, label, problems);
                if (character.Attack != null) MissingReferences(character.Attack, label + " / ataque", problems);
                ValidateAssetAbility(character.Ability, label + " / principal", character.General?.UsesDocumentedRules == true, abilityIds, problems);
                if (character.Upgrades != null)
                    for (int slot = 0; slot < character.Upgrades.Length; slot++)
                        ValidateAssetAbility(character.Upgrades[slot], label + " / mejora " + (slot + 1),
                            character.General?.UsesDocumentedRules == true, abilityIds, problems);
                if (character.General != null && character.Ability?.Definition != null &&
                    character.Upgrades != null && character.Upgrades.Length == 3 &&
                    character.Upgrades.All(a => a != null && a.Definition != null))
                    Ownership(character.CreateRuntimeDefinition(), label, problems);
            }
        }
        else if (config.Units == null || config.Units.Length == 0)
            problems.Add("MatchConfig: el catálogo está vacío.");
        else
        {
            // The legacy Units path is still supported by MatchConfig.CreateRuntimeConfig.
            // Validate it without importing or replacing it with the authored catalogue.
            for (int i = 0; i < config.Units.Length; i++)
            {
                var unit = config.Units[i];
                if (unit == null) { problems.Add("Units[" + i + "]: falta la definición."); continue; }
                string label = Label(unit.DisplayName, unit.Id, "Units[" + i + "]");
                Identity(unit.Id, label, ids, problems);
                if (string.IsNullOrWhiteSpace(unit.DisplayName)) problems.Add(label + ": falta el nombre visible.");
                Ability(unit.BaseAbility, label + " / principal", unit.UsesDocumentedRules, abilityIds, problems);
                if (unit.Tricks != null)
                    for (int slot = 0; slot < unit.Tricks.Length; slot++)
                        if (unit.UsesDocumentedRules || HasOptionalLegacyAbility(unit.Tricks[slot]))
                            Ability(unit.Tricks[slot], label + " / mejora " + (slot + 1), unit.UsesDocumentedRules, abilityIds, problems);
                if (HasOptionalLegacyAbility(unit.MonsterUpgrade))
                    Ability(unit.MonsterUpgrade, label + " / mejora Monster", unit.UsesDocumentedRules, abilityIds, problems);
                Ownership(unit, label, problems);
            }
        }
        return problems.ToArray();
    }

    static string Label(string name, string id, string fallback) =>
        (string.IsNullOrWhiteSpace(name) ? fallback : name) + " [ID: " + (id ?? "vacío") + "]";

    static void Identity(string id, string label, Dictionary<string, string> ids, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(id)) { problems.Add(label + ": falta ID estable."); return; }
        if (ids.TryGetValue(id, out var previous))
            problems.Add(label + ": ID repetido; también aparece en " + previous + ".");
        else ids.Add(id, label);
    }

    static void ValidateAssetAbility(CharacterAbility asset, string label, bool documented,
        Dictionary<string, string> ids, List<string> problems)
    {
        if (asset == null) { problems.Add(label + ": falta la referencia de habilidad."); return; }
        // SerializedObject can materialize a null inline class as an empty one.
        // Keep the value that was present before inspecting object references.
        var definition = asset.Definition;
        MissingReferences(asset, label, problems);
        Ability(definition, label, documented, ids, problems);
    }

    static bool HasOptionalLegacyAbility(TrickDefinition ability)
    {
        // Empty serialized placeholders are allowed only in optional legacy slots.
        // A partial entry with any identity, text, parameter or effect stays visible
        // to validation, so a missing ID cannot hide an actual configured upgrade.
        if (ability == null) return false;
        return !string.IsNullOrEmpty(ability.Id) || !string.IsNullOrEmpty(ability.EffectId) ||
            !string.IsNullOrEmpty(ability.Name) || !string.IsNullOrEmpty(ability.Description) ||
            (ability.Parameters != null && ability.Parameters.Length > 0) ||
            ability.Trigger != AbilityTrigger.Passive || ability.EveryAttacks != 0 ||
            ability.Cost != 0 || ability.HealthBonus != 0 || ability.DamageBonus != 0 ||
            (ability.AttackIntervalMultiplier != 0 && ability.AttackIntervalMultiplier != 1) ||
            ability.RangeBonus != 0 || ability.Armor != 0 || ability.HealOnHit != 0 ||
            ability.BonusEveryHits != 0 || ability.BonusDamage != 0;
    }

    static void Ability(TrickDefinition ability, string label, bool documented,
        Dictionary<string, string> ids, List<string> problems)
    {
        if (ability == null) { problems.Add(label + ": falta la definición de habilidad."); return; }
        Identity(ability.Id, label, ids, problems);
        if (documented && string.IsNullOrWhiteSpace(ability.EffectId))
            problems.Add(label + ": falta EffectId.");
        if (ability.Parameters == null) return;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in ability.Parameters)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key))
                problems.Add(label + ": parámetro vacío.");
            else
            {
                if (!keys.Add(parameter.Key)) problems.Add(label + ": parámetro repetido: " + parameter.Key + ".");
                if (float.IsNaN(parameter.Value) || float.IsInfinity(parameter.Value))
                    problems.Add(label + ": valor no finito en " + parameter.Key + ".");
            }
        }
    }

    static void Ownership(UnitDefinition unit, string label, List<string> problems)
    {
        if (!unit.UsesDocumentedRules) return;
        // This is the current combat ownership resolver, not a second catalogue
        // and not a source of display names, stats or replacement assets.
        var effective = UnitAbilityOwnership.ForCombat(unit);
        Slot(unit.BaseAbility, effective.BaseAbility, label + " / principal", problems);
        for (int slot = 0; slot < 3; slot++)
            Slot(unit.Tricks != null && slot < unit.Tricks.Length ? unit.Tricks[slot] : null,
                effective.Tricks[slot], label + " / mejora " + (slot + 1), problems);
    }

    static void Slot(TrickDefinition configured, TrickDefinition effective, string label, List<string> problems)
    {
        if (effective == null)
            problems.Add(label + ": no existe una habilidad registrada para este personaje y espacio.");
        else if (configured == null || configured.Id != effective.Id || configured.EffectId != effective.EffectId)
            problems.Add(label + ": referencia ajena o de otro espacio; el combate usa " + effective.Id + ".");
    }

    static void MissingReferences(Object asset, string label, List<string> problems)
    {
        using (var serialized = new SerializedObject(asset))
        {
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    problems.Add(label + ": enlace inexistente en " + property.propertyPath + ".");
        }
    }
}

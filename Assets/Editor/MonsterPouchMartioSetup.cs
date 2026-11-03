using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

public static class MonsterPouchMartioSetup
{
    const string Folder = "Assets/resources/MonsterPouch/Characters/martio";
    [MenuItem("Monster Pouch/Characters/Install Martio")]
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before installing Martio.");
        MartioArtSetup.Build();
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var visuals = Resources.Load<MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
        if (visuals == null) throw new InvalidDataException("Martio art is unavailable.");
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder + "/Character.asset");
        if (character == null) {
            var definition = MartioDefinition.Create();
            character = ScriptableObject.CreateInstance<CharacterDefinition>(); character.InitializeIdentity("martio");
            character.General = definition;
            character.Description = "Martio lanza Marte y dos lunas por una trayectoria entrelazada. Sus mejoras añaden ondas expansivas, perforación y dos grupos planetarios laterales.";
            character.Attack = ScriptableObject.CreateInstance<CharacterAttack>(); character.Attack.Import(definition);
            AssetDatabase.CreateAsset(character.Attack, Folder + "/Attack.asset");
            character.Ability = Ability(definition.BaseAbility, "Ability", visuals.UpgradeIcons[0]);
            character.Upgrades = definition.Tricks.Select((t, i) => Ability(t, "Upgrade" + (i + 1), visuals.UpgradeIcons[i])).ToArray();
            AssetDatabase.CreateAsset(character, Folder + "/Character.asset");
        }
        float previousSize = character.Visuals?.InGameSizePercent ?? -1;
        character.Visuals = visuals.Body;
        character.Visuals.InGameSizePercent = previousSize;
        character.Visuals.SourceNotes = "Martio: arte del creador y fotogramas derivados con saltos; requiere MartioCelestialOrbit para Marte y dos lunas.";
        character.Portrait = character.Card = character.Icon = visuals.PortraitRight;
        character.Ability.Icon = visuals.UpgradeIcons[0]; EditorUtility.SetDirty(character.Ability);
        for (int i = 0; i < 3; i++) { character.Upgrades[i].Icon = visuals.UpgradeIcons[i]; EditorUtility.SetDirty(character.Upgrades[i]); }
        EditorUtility.SetDirty(character);
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        var catalog = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        if (config == null || catalog == null) throw new InvalidDataException("The game catalogs are missing.");
        config.Characters = config.Characters.Where(c => c != null && c.Id != "martio").Concat(new[] { character }).ToArray();
        config.Units = config.Units.Where(u => u != null && u.Id != "martio").Concat(new[] { character.CreateRuntimeDefinition() }).ToArray();
        catalog.Units = catalog.Units.Where(a => a != null && a.Id != "martio").Concat(new[] { character.Visuals }).ToArray();
        EditorUtility.SetDirty(config); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        string issue = character.Validate(); if (issue.Length > 0) throw new InvalidDataException(issue);
        return "Martio installed in MatchConfig and UnitArt; 40 HP, range 4, travel 7, interval 1.3, cost 3; 3 square icons; eight hop views.";
    }
    static CharacterAbility Ability(TrickDefinition definition, string name, Sprite icon)
    {
        var asset = ScriptableObject.CreateInstance<CharacterAbility>(); asset.Definition = definition; asset.Icon = icon;
        AssetDatabase.CreateAsset(asset, Folder + "/" + name + ".asset"); return asset;
    }
}

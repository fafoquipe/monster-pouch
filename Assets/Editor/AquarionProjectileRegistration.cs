using System;
using MonsterPouch.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

// Editor-only. Call after import; the common registration preserves existing overrides and Undo.
public static class AquarionProjectileRegistration
{
    public static void Register()
    {
        const string characters = "Assets/resources/MonsterPouch/Characters/";
        const string vfx = "Assets/resources/MonsterPouch/Eronya/Aquarion/VFX/";
        var javelinOwner = AssetDatabase.LoadAssetAtPath<CharacterAttack>(characters + "jabalinero/Attack.asset");
        var bindingOwner = AssetDatabase.LoadAssetAtPath<CharacterAbility>(characters + "jabalinero/Ability.asset");
        var boltOwner = AssetDatabase.LoadAssetAtPath<CharacterAttack>(characters + "maestro-de-las-mareas/Attack.asset");
        var javelin = AssetDatabase.LoadAssetAtPath<Sprite>(vfx + "javelin.png");
        var binding = AssetDatabase.LoadAssetAtPath<Sprite>(vfx + "binding-javelin.png");
        var bolt = AssetDatabase.LoadAssetAtPath<Sprite>(vfx + "water-bolt.png");
        if (javelinOwner == null || bindingOwner == null || boltOwner == null || javelin == null || binding == null || bolt == null)
            throw new InvalidOperationException("Import Aquarion assets and sprites before projectile registration.");
        MonsterPouchProjectileVisualRegistration.Register(javelinOwner, "flight", "Javelin", "Jabalina", new[] { javelin }, rendererId: "aquarion-javelin");
        MonsterPouchProjectileVisualRegistration.Register(bindingOwner, "binding-javelin", "Binding javelin", "Jabalina de atadura", new[] { binding }, rendererId: "aquarion-binding-javelin");
        MonsterPouchProjectileVisualRegistration.Register(boltOwner, "flight", "Water bolt", "Orbe de agua", new[] { bolt }, rendererId: "aquarion-water-bolt");
    }
}

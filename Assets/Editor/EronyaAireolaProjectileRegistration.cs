#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEditor;
using MonsterPouch.Gameplay.Characters;
public static class EronyaAireolaProjectileRegistration
{
    // Invoke only after central import creates the real Character assets. No menu or auto-run.
    public static void Register(CharacterDefinition character)
    {
        if (character == null) return;
        if (character.Id == "zephalontropo")
        {
            var frames = new string[] { "Assets/art/units/zephalontropo/aireola-vfx/basic-flight-0.png",
                    "Assets/art/units/zephalontropo/aireola-vfx/basic-flight-1.png",
                    "Assets/art/units/zephalontropo/aireola-vfx/basic-flight-2.png",
                    "Assets/art/units/zephalontropo/aireola-vfx/basic-flight-3.png" }.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
            if (frames.Any(s => s == null)) throw new System.InvalidOperationException("Import all Aireola VFX sprites before registration.");
            MonsterPouchProjectileVisualRegistration.Register(character.Attack, "flight", "Crossbow bolt", "Virote de ballesta", frames, -1, "Shared Aireola flight sampler", "Sampler de vuelo Aireola compartido", rendererId: "aireola-crossbow");
        }
        if (character.Id == "zephalontropo")
        {
            var frames = new string[] { "Assets/art/units/zephalontropo/aireola-vfx/sphere-flight-0.png",
                    "Assets/art/units/zephalontropo/aireola-vfx/sphere-flight-1.png",
                    "Assets/art/units/zephalontropo/aireola-vfx/sphere-flight-2.png",
                    "Assets/art/units/zephalontropo/aireola-vfx/sphere-flight-3.png" }.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
            if (frames.Any(s => s == null)) throw new System.InvalidOperationException("Import all Aireola VFX sprites before registration.");
            MonsterPouchProjectileVisualRegistration.Register(character.Ability, "flight", "Compressed sphere", "Esfera comprimida", frames, -1, "Shared Aireola flight sampler", "Sampler de vuelo Aireola compartido", rendererId: "aireola-sphere");
        }
        if (character.Id == "alboguera")
        {
            var frames = new string[] { "Assets/art/units/alboguera/aireola-vfx/basic-flight-0.png",
                    "Assets/art/units/alboguera/aireola-vfx/basic-flight-1.png",
                    "Assets/art/units/alboguera/aireola-vfx/basic-flight-2.png",
                    "Assets/art/units/alboguera/aireola-vfx/basic-flight-3.png" }.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
            if (frames.Any(s => s == null)) throw new System.InvalidOperationException("Import all Aireola VFX sprites before registration.");
            MonsterPouchProjectileVisualRegistration.Register(character.Attack, "flight", "Musical note", "Nota musical", frames, -1, "Shared Aireola flight sampler", "Sampler de vuelo Aireola compartido", rendererId: "aireola-note");
        }
    }
}
#endif

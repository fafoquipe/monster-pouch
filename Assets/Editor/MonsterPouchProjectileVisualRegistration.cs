using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Called by character setup/importers. Adds presentation slots to the existing assets.</summary>
public static class MonsterPouchProjectileVisualRegistration
{
    public static void Register(UnityEngine.Object attackOrAbility, string variantId, string english, string spanish,
        IEnumerable<Sprite> sprites, float legacyPercent = -1, string noteEnglish = "", string noteSpanish = "", float initialPercent = 50, string rendererId = null)
    {
        if (!(attackOrAbility is CharacterAttack) && !(attackOrAbility is CharacterAbility))
            throw new ArgumentException("Use the existing CharacterAttack or CharacterAbility asset.");
        if (string.IsNullOrWhiteSpace(variantId)) throw new ArgumentException("A stable visual variant ID is required.");
        if (float.IsNaN(initialPercent) || float.IsInfinity(initialPercent) || initialPercent < 0 || initialPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(initialPercent), "Report out-of-range legacy sizes before choosing an initial override value.");
        var owner = new SerializedObject(attackOrAbility);
        owner.Update();
        var slots = owner.FindProperty("ProjectileVisuals");
        SerializedProperty slot = null;
        for (int i = 0; i < slots.arraySize; i++)
        {
            var candidate = slots.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("VariantId").stringValue == variantId) { slot = candidate; break; }
        }
        if (slot == null)
        {
            int index = slots.arraySize;
            slots.InsertArrayElementAtIndex(index); slot = slots.GetArrayElementAtIndex(index);
            slot.FindPropertyRelative("VariantId").stringValue = variantId;
            slot.FindPropertyRelative("OverrideSize").boolValue = false;
            slot.FindPropertyRelative("SizePercent").floatValue = initialPercent;
        }
        slot.FindPropertyRelative("NameEnglish").stringValue = english;
        if(rendererId!=null)slot.FindPropertyRelative("RendererId").stringValue=rendererId;
        slot.FindPropertyRelative("NameSpanish").stringValue = spanish;
        slot.FindPropertyRelative("LegacyPercent").floatValue = legacyPercent;
        slot.FindPropertyRelative("LegacyNoteEnglish").stringValue = noteEnglish;
        slot.FindPropertyRelative("LegacyNoteSpanish").stringValue = noteSpanish;
        var frames = slot.FindPropertyRelative("Frames");
        var source = (sprites ?? Array.Empty<Sprite>()).Where(s => s != null).Distinct().ToArray();
        frames.arraySize = source.Length;
        for (int i = 0; i < source.Length; i++)
        {
            var frame = frames.GetArrayElementAtIndex(i);
            frame.FindPropertyRelative("Sprite").objectReferenceValue = source[i];
            frame.FindPropertyRelative("VisibleRect").rectValue = Measure(source[i]);
        }
        owner.ApplyModifiedProperties(); // Standard Undo, dirty tracking and existing Save changes action.
    }

    public static Rect Measure(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            throw new ArgumentException("An imported sprite is required; register procedural bounds when creating its pixels.");
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot decode " + path);
            float x = (float)texture.width / sprite.texture.width, y = (float)texture.height / sprite.texture.height;
            Rect source = new Rect(sprite.rect.x * x, sprite.rect.y * y, sprite.rect.width * x, sprite.rect.height * y);
            return ProjectileVisualGeometry.AlphaBounds(texture.GetPixels32(), texture.width, texture.height, source);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    public static void RegisterRenderer(UnityEngine.Object owner,string variantId,string rendererId)
    {
        var serialized=new SerializedObject(owner);serialized.Update();
        var slots=serialized.FindProperty("ProjectileVisuals");
        for(int i=0;i<slots.arraySize;i++) {
            var slot=slots.GetArrayElementAtIndex(i);
            if(slot.FindPropertyRelative("VariantId").stringValue!=variantId)continue;
            slot.FindPropertyRelative("RendererId").stringValue=rendererId;
            serialized.ApplyModifiedProperties();return;
        }
        throw new InvalidOperationException("Register the actual finite-flight slot before its renderer: "+variantId);
    }

    static float LegacyBodyToVisiblePercent(IEnumerable<Sprite> sprites, float bodyTiles, float bodyPixels)
    {
        Vector2 envelope = Vector2.zero;
        foreach (var sprite in sprites ?? Array.Empty<Sprite>()) if (sprite != null)
            envelope = Vector2.Max(envelope, Vector2.Scale(sprite.rect.size, Measure(sprite).size));
        return bodyTiles * Mathf.Max(envelope.x, envelope.y) / bodyPixels / ProjectileVisualGeometry.MaximumTiles * 100;
    }

    // Explicit migration for pre-existing renderer variants; new characters use Register without adding an ID here.
    public static void RegisterLegacy(MatchConfig config, ProjectileArtCatalog catalog, MartioVisualAssets martio, Sprite fish)
    {
        foreach (var c in config.Characters ?? Array.Empty<CharacterDefinition>())
        {
            if (c == null) continue;
            var frames = catalog?.Get(c.Id)?.Flight;
            if (new[] { "anuik", "bugui", "kayon", "stein", "rosie", "kazuru", "gochan", "jazar", "trimol", "tsu" }.Contains(c.Id))
                Register(c.Attack, "flight", "Basic projectile", "Proyectil básico", frames);
            if (c.Id == "anuik") Register(c.Attack, "charged", "Charged heart", "Corazón cargado", frames);
            if (c.Id == "martio")
            {
                const string en = "Original reference measures the body, excluding its rings/glow. Custom sizes measure the full visible projectile. Original appearance is preserved until enabled.";
                const string es = "La referencia original mide el cuerpo y excluye anillos/resplandor. El tamaño personalizado mide el proyectil visible completo. El aspecto original se conserva hasta activarlo.";
                Register(c.Attack, "mars", "Mars", "Marte", martio?.Mars.Flight, 80, en, es,
                    LegacyBodyToVisiblePercent(martio?.Mars.Flight, .8f, 95));
                Register(c.Attack, "moons", "Moons (both)", "Lunas (ambas)", martio?.Moon.Flight, 50, en, es,
                    LegacyBodyToVisiblePercent(martio?.Moon.Flight, .5f, 81));
            }
            if (c.Id == "suki") Register(c.Ability, "flight", "Shield fish", "Pez de escudo", new[] { fish });
            if (c.Id == "rosie") Register(c.Ability, "flight", "Healing rose", "Rosa curativa", frames);
            if (c.Id == "gochan") Register(c.Ability, "flight", "Super fire seal", "Sello de fuego de la súper", null);
            if (c.Id == "trimol") Register(c.Ability, "flight", "Large boulder", "Roca grande", null);
            if (c.Id == "bugaloo") Register(c.Ability, "flight", "Reflected moon", "Luna reflejada", null);
            if (c.Id == "tsu")
            {
                Register(c.Ability, "flight", "Sticky gum", "Chicle adhesivo", null);
                Register(c.Upgrades[2], "speed", "Speed gum", "Chicle de velocidad", null);
                Register(c.Upgrades[2], "shield", "Shield gum", "Chicle de escudo", null);
                Register(c.Upgrades[2], "damage", "Damage gum", "Chicle de daño", null);
            }
            if (c.Id == "tokoro") Register(c.Upgrades[1], "flight", "Thrown ice cream", "Helado arrojado", null);
            // One-time legacy producer declarations. The preview dispatches by renderer family only.
            if(c.Id=="anuik")RegisterRenderer(c.Attack,"charged","catalog-charged");
            if(c.Id=="kazuru")RegisterRenderer(c.Attack,"flight","catalog-spin");
            if(c.Id=="rosie"){RegisterRenderer(c.Attack,"flight","catalog-wave");RegisterRenderer(c.Ability,"flight","healing-rose");}
            if(c.Id=="gochan"){RegisterRenderer(c.Attack,"flight","fire-seal");RegisterRenderer(c.Ability,"flight","fire-seal");}
            if(c.Id=="trimol"){RegisterRenderer(c.Attack,"flight","rock");RegisterRenderer(c.Ability,"flight","large-rock");}
            if(c.Id=="jazar")RegisterRenderer(c.Attack,"flight","toxic");
            if(c.Id=="suki")RegisterRenderer(c.Ability,"flight","fish");
            if(c.Id=="bugaloo")RegisterRenderer(c.Ability,"flight","reflected-moon");
            if(c.Id=="martio"){RegisterRenderer(c.Attack,"mars","mars");RegisterRenderer(c.Attack,"moons","moons");}
            if(c.Id=="tsu") {
                RegisterRenderer(c.Attack,"flight","basic-gum");RegisterRenderer(c.Ability,"flight","sticky-gum");
                foreach(string variant in new[]{"speed","shield","damage"})RegisterRenderer(c.Upgrades[2],variant,variant+"-gum");
            }
            if(c.Id=="tokoro")RegisterRenderer(c.Upgrades[1],"flight","cone");
        }
    }
}

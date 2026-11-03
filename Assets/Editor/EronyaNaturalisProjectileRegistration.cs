#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

public static class EronyaNaturalisProjectileRegistration
{
    // Explicit integration call after import. No auto-run, no new editor and no SaveAssets.
    public static void Register(CharacterDefinition character, ProjectileArtCatalog catalog)
    {
        if (character == null) return;
        string id = character.Id;
        if (id != "cael" && id != "guarda-veterano" && id != "antidota") return;
        if (character.Attack == null || catalog == null)
            throw new InvalidOperationException("Import Naturalis Attack assets and provide the common projectile catalog.");
        string root = "Assets/resources/MonsterPouch/Characters/" + id + "/Art/VFX/";
        var flight = Enumerable.Range(0, 4).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(root + "flight-" + i + ".png")).ToArray();
        var impact = Enumerable.Range(0, 4).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(root + "impact-" + i + ".png")).ToArray();
        if (flight.Concat(impact).Any(s => s == null))
            throw new InvalidOperationException("Missing imported Naturalis flight/impact sprite: " + id);
        string english = id == "cael" ? "Leaf slash" : id == "antidota" ? "Petal mote" : "Spiral seed";
        string spanish = id == "cael" ? "Corte de hojas" : id == "antidota" ? "Mota de pétalos" : "Semilla espiral";
        MonsterPouchProjectileVisualRegistration.Register(character.Attack, "flight", english, spanish, flight,
            noteEnglish: "Current size preserves the authored catalog appearance.",
            noteSpanish: "El tamaño actual conserva la apariencia del catálogo.", rendererId: "naturalis-catalog");
        var entries = new List<ProjectileArt>(catalog.Projectiles ?? Array.Empty<ProjectileArt>());
        var style = entries.FirstOrDefault(p => p != null && p.UnitId == id);
        Undo.RecordObject(catalog, "Register Naturalis projectiles");
        if (style == null)
        {
            style = new ProjectileArt { UnitId = id, DisplaySize = 46, ReferencePixels = 256, OrientToTarget = true };
            entries.Add(style);
        }
        style.Flight = flight;
        style.Impact = impact;
        catalog.Projectiles = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        // Preserve the established 46/256 canvas ratio and existing human calibration.
        // DisplaySize, ReferencePixels, orientation, OverrideSize and SizePercent survive re-registration.
    }
}
#endif

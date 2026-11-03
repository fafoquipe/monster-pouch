#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;

public static class EronyaGeotharProjectileRegistration
{
    // Integration owner calls once after importing assets. No auto-run or new editor.
    public static void Register(CharacterDefinition character, ProjectileArtCatalog catalog)
    {
        if (character == null || character.Id != "protectora-del-mausoleo") return;
        if (character.Attack == null || catalog == null)
            throw new InvalidOperationException("Import Protectora Attack and provide the common projectile catalog.");
        const string root="Assets/resources/MonsterPouch/Eronya/Geothar/protectora-del-mausoleo/vfx/frame-";
        var all=Enumerable.Range(0,8).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(root+i.ToString("00")+".png")).ToArray();
        if(all.Any(s=>s==null))throw new InvalidOperationException("Import all Protectora flight and impact sprites first.");
        var flight=all.Take(4).ToArray();
        MonsterPouchProjectileVisualRegistration.Register(character.Attack,"flight","Stone rune","Runa pétrea",flight,rendererId: "catalog");
        var entries=new List<ProjectileArt>(catalog.Projectiles??Array.Empty<ProjectileArt>());
        var style=entries.FirstOrDefault(x=>x!=null&&x.UnitId==character.Id);
        Undo.RecordObject(catalog,"Register Geothar projectile");
        if(style==null)
        {
            style=new ProjectileArt{UnitId=character.Id,DisplaySize=46,OrientToTarget=true};
            entries.Add(style);
        }
        style.Flight=flight;style.Impact=all.Skip(4).ToArray();
        style.ReferencePixels=flight.Max(sprite=>
        {
            var bounds=MonsterPouchProjectileVisualRegistration.Measure(sprite);
            return Mathf.Max(bounds.width*sprite.rect.width,bounds.height*sprite.rect.height);
        });
        catalog.Projectiles=entries.ToArray();EditorUtility.SetDirty(catalog);
        // Existing OverrideSize/SizePercent and other characters remain unchanged.
    }
}
#endif

using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

public static class MonsterPouchOctober8Setup
{
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
        var original=AssetDatabase.LoadAssetAtPath<UnitArtCatalog>("Assets/Resources/MonsterPouch/UnitArt.asset").Get("aky");
        const string sheet="Assets/art/units/aky-battle/aky-combat.png";
        var frames=MonsterPouchSpriteAnimationImporter.ImportGrid(sheet,100,10,8,"aky-battle",true);
        float ppu=frames[0,0].rect.height/original.Idle(UnitFacing.South).bounds.size.y;
        frames=MonsterPouchSpriteAnimationImporter.ImportGrid(sheet,ppu,10,8,"aky-battle",true);
        var battle=JsonUtility.FromJson<UnitArt>(JsonUtility.ToJson(original));
        int[] rows={4,3,2,1,0,7,6,5};
        battle.DirectionalAnimations=new UnitDirectionalAnimation[8];battle.WalkRigs=new UnitSpriteRig[8];
        battle.IdleDirections=new Sprite[8];battle.AttackDirections=new Sprite[8];
        for(int d=0;d<8;d++)
        {
            int r=rows[d];battle.IdleDirections[d]=frames[r,0];battle.AttackDirections[d]=frames[r,5];
            battle.DirectionalAnimations[d]=new UnitDirectionalAnimation
            {
                Idle=new[]{frames[r,0],frames[r,1]},Move=new[]{frames[r,2],frames[r,3]},
                Attack=new[]{frames[r,0],frames[r,4],frames[r,5],frames[r,4],frames[r,0]},AttackContactFrame=2,
                Special=new[]{frames[r,6],frames[r,7],frames[r,8],frames[r,7]},
                Death=new[]{frames[r,6],frames[r,9]},AttackLungeDistance=.07f,AttackImpactSquash=.04f
            };
        }
        battle.SourceNotes="Combat-only connected wings. Original lobby catalog and animations preserved.";
        const string catalogPath="Assets/Resources/MonsterPouch/AkyBattleArt.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(catalogPath);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<UnitArtCatalog>();AssetDatabase.CreateAsset(catalog,catalogPath);}
        catalog.Units=new[]{battle};EditorUtility.SetDirty(catalog);
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        var hook=config.Get("popow").Tricks.Single(t=>t.EffectId=="popow-sweep"||t.Id=="popow-sweep");
        hook.Trigger=AbilityTrigger.EveryAttacks;hook.EveryAttacks=3;
        var parameters=(hook.Parameters??Array.Empty<AbilityParameter>()).Where(p=>p.Key!="every"&&p.Key!="duration").ToList();
        parameters.Add(new AbilityParameter{Key="every",Value=3});parameters.Add(new AbilityParameter{Key="duration",Value=2});
        hook.Parameters=parameters.ToArray();EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();MonsterPouchBrandingImport.Apply();
        return "Aky combat-only art, editable Popow hook (3 attacks / 2 seconds), launcher safe insets applied.";
    }
}

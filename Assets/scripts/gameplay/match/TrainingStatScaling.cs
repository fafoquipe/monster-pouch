using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Detached practice-only values; IDs, mechanics and shop costs remain intact.</summary>
    public static class TrainingStatScaling
    {
        public static UnitDefinition Weaken(UnitDefinition source,float multiplier)
        {
            if(source==null||float.IsNaN(multiplier)||float.IsInfinity(multiplier)||multiplier<=0||multiplier>1)
                throw new ArgumentException("Invalid training stat multiplier.");
            var unit=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(source));
            float inverse=1f/multiplier;
            unit.MaxHealth=Lower(unit.MaxHealth,multiplier,1);unit.Damage=Lower(unit.Damage,multiplier);
            unit.AttackRange=Lower(unit.AttackRange,multiplier,1);unit.IQSpeed=Lower(unit.IQSpeed,multiplier,1);
            unit.AttackInterval=Slower(unit.AttackInterval,inverse);unit.MoveInterval=Slower(unit.MoveInterval,inverse);
            unit.AttackWindup=Slower(unit.AttackWindup,inverse);
            // Resolve inherited timing on this clone before scaling; the source
            // retains its sentinels and the effective action is also weakened.
            var tail=BasicAttackTiming.DefaultTail(unit.Id);
            unit.AttackHold=unit.AttackHold<0?BasicAttackTiming.Resolve(unit.AttackHold,tail.x)*inverse:Slower(unit.AttackHold,inverse);
            unit.AttackRecovery=unit.AttackRecovery<0?BasicAttackTiming.Resolve(unit.AttackRecovery,tail.y)*inverse:Slower(unit.AttackRecovery,inverse);
            unit.ProjectileSpeed*=multiplier;
            unit.EnergyMax*=inverse;unit.EnergyPerAttack*=multiplier;unit.EnergyOnDamage*=multiplier;unit.EnergyPerSecond*=multiplier;
            unit.RevivesPerCombat=Lower(unit.RevivesPerCombat,multiplier);unit.ReviveHealthFraction*=multiplier;unit.ReviveDelay=Slower(unit.ReviveDelay,inverse);
            unit.LethalEveryHits=Higher(unit.LethalEveryHits,inverse);unit.SummonInterval=Slower(unit.SummonInterval,inverse);
            unit.MaxLivingSummons=Lower(unit.MaxLivingSummons,multiplier);unit.SummonHealth=Lower(unit.SummonHealth,multiplier,1);
            unit.SummonDamage=Lower(unit.SummonDamage,multiplier);unit.SummonAttackInterval=Slower(unit.SummonAttackInterval,inverse);unit.SummonMoveInterval=Slower(unit.SummonMoveInterval,inverse);
            ScaleEffect(unit.BaseAbility,multiplier,inverse);
            foreach(var effect in unit.Tricks??Array.Empty<TrickDefinition>())ScaleEffect(effect,multiplier,inverse);
            ScaleEffect(unit.MonsterUpgrade,multiplier,inverse);
            return unit;
        }

        static int Lower(int value,float multiplier,int minimum=0)=>value<=0?value:Mathf.Max(minimum,Mathf.CeilToInt(value*multiplier));
        static int Higher(int value,float inverse)=>value<=0?value:Mathf.CeilToInt(value*inverse);
        static float Slower(float value,float inverse)=>value<=0?value:value*inverse;
        static void ScaleEffect(TrickDefinition effect,float multiplier,float inverse)
        {
            if(effect==null)return;
            // Resolve existing fallbacks before scaling; omitted parameters must
            // not silently retain full strength in the training simulation.
            var resolved=AbilityBalance.Resolve(effect);
            if(resolved.Length>0)effect.Parameters=resolved;
            effect.EveryAttacks=Higher(effect.EveryAttacks,inverse);
            effect.HealthBonus=Lower(effect.HealthBonus,multiplier);effect.DamageBonus=Lower(effect.DamageBonus,multiplier);
            effect.RangeBonus=Lower(effect.RangeBonus,multiplier);
            effect.HealOnHit=Lower(effect.HealOnHit,multiplier);effect.BonusDamage=Lower(effect.BonusDamage,multiplier);
            effect.BonusEveryHits=Higher(effect.BonusEveryHits,inverse);
            // 1 means no extra modifier, not an additional attack interval.
            if(effect.AttackIntervalMultiplier!=1)effect.AttackIntervalMultiplier=Slower(effect.AttackIntervalMultiplier,inverse);
            foreach(var parameter in effect.Parameters??Array.Empty<AbilityParameter>())
            {
                if(parameter==null||parameter.Value<=0)continue;
                switch(parameter.Key)
                {
                    case "animationDuration": case "blockActions": case "blockEnergy": break;
                    case "every":parameter.Value=Mathf.Ceil(parameter.Value*inverse);break;
                    case "castDuration":case "holdDuration":case "recoveryDuration":case "travelDuration":
                        parameter.Value*=inverse;break;
                    case "radius":case "range":case "targets":case "count":case "damage":case "damageOverride":case "heal":
                        parameter.Value=Mathf.Ceil(parameter.Value*multiplier);break;
                    default:parameter.Value*=multiplier;break;
                }
            }
        }
    }
}

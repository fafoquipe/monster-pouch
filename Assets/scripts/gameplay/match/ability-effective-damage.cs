using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public static partial class AbilityBalance
    {
        // Negative means a basic-attack formula. Tokoro's automatic mode instead
        // uses the fixed canonical default from the SAME parameter registry.
        public static float ResolvedFixedDamage(TrickDefinition effect)
        {
            if(effect==null)return -1;
            string id=effect.EffectId??effect.Id??"";
            if(id=="gochan-seal-burst")return Mathf.Max(0,Mathf.RoundToInt(Parameter(effect,"damage")));
            return ResolveDamage(id,Parameter(effect,"damageOverride",-1),-1);
        }
        public static int ResolveDamage(string effectId,float overrideValue,int automatic)
        {
            float value=overrideValue;
            if(value<0 && effectId=="tokoro-headbutt")
                value=Parameter(new TrickDefinition {EffectId=effectId},"damageOverride",-1);
            return value<0?automatic:Mathf.Max(0,Mathf.RoundToInt(value));
        }
    }
}

using System;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        // Query existing absorbing shields and Pokrak's active protective bubbles.
        // Bubbles are shield-like reduction, not HP layers or arbitrary damage reduction.
        // Fractional legacy/fish and Eronya layers qualify before rounded HUD refresh.
        private bool EronyaHasTemporaryShield(ActorClock target) => target?.Unit!=null &&
            (target.Unit.HasLegacyShield || EronyaState(target).Shields.Total>0m ||
                PokrakBubbleReduction(target.Unit)>0f);
        // Legacy-only matches never enter this route. Existing legacy burn instances
        // and fish-shield layers retain their own lifetimes and source attribution.
        private bool EronyaRouteLegacyImpact(PendingImpact impact,ActorClock defender)
        {
            if(!eronyaCombat)return false;
            bool reflected=impact.Effect=="bugaloo-reflected-moon";
            bool lethal=impact.Effect=="tauris-super"&&!Has(impact.Source,"tauris-hunger");
            float raw=lethal?(float)(EronyaHealth(defender)+EronyaState(defender).Shields.Total+defender.Unit.LegacyShield):
                impact.Effect=="tauris-super"?(float)Math.Ceiling(EronyaHealth(defender)*(decimal)V(impact.Source,"tauris-hunger","healthPercent",50)/100m):impact.PreciseDamage.HasValue?(float)impact.PreciseDamage.Value:impact.Damage;
            // Existing packets use general damage; reflection remains an origin safeguard.
            var hit=EronyaDealDamage(impact.Source,defender,raw,impact.Effect,reflected?EronyaDamageOrigin.Reflection:impact.Super?EronyaDamageOrigin.Ability:EronyaDamageOrigin.Basic,
                0,!reflected,lethal);
            if(!hit.ValidContact)return true;
            if(!reflected)QueueReflectedMoon(defender,impact.Source,(int)raw);
            ApplyHitEffects(impact,defender);
            if(!EronyaOwns(impact.Source)&&(Id(impact.Source)=="suki"||Id(impact.Source)=="pokrak")&&!impact.Super&&!impact.Secondary&&hit.Landed)
                impact.Source.Unit.AddEnergy(impact.Source.Unit.BaseStats.EnergyPerAttack);
            if(impact.Critical&&hit.Landed){CriticalImpacted?.Invoke(impact.Source.Unit,defender.Unit);AbilityUsed?.Invoke(impact.Source.Unit,"atong-critical");}
            if(lethal)LethalImpacted?.Invoke(impact.Source.Unit,defender.Unit);
            return true;
        }
        private bool EronyaRouteDirectDamage(ActorClock source,ActorClock defender,int amount)
        {
            if(!eronyaCombat||source==null||defender==null)return false;
            EronyaDealDamage(source,defender,amount,"legacy-direct",EronyaDamageOrigin.Periodic);
            QueueReflectedMoon(defender,source,amount);return true;
        }
        private decimal EronyaAbsorbLegacyShield(BattleUnit target,decimal damage) => target.AbsorbPreciseLegacyShield(damage,(source,version,absorbed)=>
        {
            if(source!=null&&source!=target&&source.IsAlive&&source.isActiveAndEnabled&&source.Side==target.Side&&
                source.CombatResetVersion==version&&source.BaseStats.HasEffect("suki-shield-energy"))
                source.AddEnergy((float)absorbed*source.BaseStats.AbilityValue("suki-shield-energy","energyPerShieldDamage",1));
        });
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public static partial class AbilityBalance
    {
        static string Number(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        static string Signed(float value)=>value.ToString("+0.##;-0.##;+0",CultureInfo.InvariantCulture);
        static float Parameter(TrickDefinition effect,string key,float fallback=0)
            =>Resolve(effect).FirstOrDefault(p=>p.Key==key)?.Value??fallback;

        // Presentation only: the stored combat values stay unchanged. Upgrade deltas
        // are measured against the owner's edited base ability, not roster constants.
        public static bool IsRelativeParameter(string effect,string key)
        {
            switch(effect+":"+key)
            {
                case "popow-dash-stun:duration": case "blotan-fast-income:interval":
                case "sepora-multishot:targets": case "sepora-multishot:superTargets":
                case "flo-flower-heal:heal": case "flo-flower-invulnerability:health":
                case "jazar-intense-radiation:reduction": case "kayon-double-summon:count":
                case "stein-triple:targets": case "trimol-brutal-boulder:duration": return true;
                default:return false;
            }
        }
        static float ParameterBaseline(string effect,string key,UnitDefinition owner)
        {
            if(!IsRelativeParameter(effect,key))return 0;
            if(effect=="sepora-multishot"&&key=="targets")return 1;
            string baseId=effect=="popow-dash-stun"?"popow-dash":effect=="blotan-fast-income"?"blotan-income":
                effect=="sepora-multishot"?"sepora-storm":effect.StartsWith("flo-")?"flo-flower":
                effect=="jazar-intense-radiation"?"jazar-radiation":effect=="kayon-double-summon"?"kayon-summon":
                effect=="stein-triple"?"stein-stun":"trimol-boulder";
            var baseAbility=owner?.BaseAbility;
            if(baseAbility==null||baseAbility.EffectId!=baseId)baseAbility=new TrickDefinition{EffectId=baseId};
            return Parameter(baseAbility,key=="superTargets"?"targets":key);
        }
        public static bool IsFactorParameter(string effect,string key)
            =>key=="reflectPercent"||(key=="damagePercent"&&(effect=="atong-critical"||effect=="atong-critical-damage"));
        public static float ParameterEditorValue(string effect,string key,float stored,UnitDefinition owner)
            =>IsFactorParameter(effect,key)?stored/100:MartioBalanceValues.Normalize(effect,key,stored)-ParameterBaseline(effect,key,owner);
        public static float ParameterStoredValue(string effect,string key,float edited,UnitDefinition owner)
        {
            float stored=IsFactorParameter(effect,key)?edited*100:edited+ParameterBaseline(effect,key,owner);
            return MartioBalanceValues.Normalize(effect,key,Mathf.Max(key=="damageOverride"||key=="castDuration"?-1:0,stored));
        }

        public static string DamageFormula(TrickDefinition effect,bool spanish=true)
        {
            float fixedDamage=ResolvedFixedDamage(effect);
            if(fixedDamage>=0)return Mathf.RoundToInt(fixedDamage)+(spanish?" de daño fijo":" fixed damage");
            float multiplier=Parameter(effect,"multiplier",1);
            if(effect.EffectId=="atong-critical"||effect.EffectId=="atong-critical-damage")
                multiplier=Parameter(effect,"damagePercent",150)/100;
            return "×"+Number(multiplier)+(spanish?" del daño de ataque":" attack damage");
        }

        // Presentation only. References name the actual replaced base, passive or
        // compatible Trick; independent effects keep absolute quantities. No asset
        // writes, ownership changes, or second set of balance defaults is involved.
        static readonly Regex DescriptionDeltaToken = new Regex(
            @"\{([^{}:]+):(delta|deltaPercent):([^{}:]+)(?::([^{}:]+))?\}");

        public static string DescriptionDifference(float original, float updated, bool percentage = false)
        {
            double delta = Math.Round(((double)updated - original) * (percentage ? 100 : 1), 2);
            return delta == 0 ? "0" : delta.ToString("+0.##;−0.##;0", CultureInfo.InvariantCulture);
        }

        static TrickDefinition DescriptionReference(UnitDefinition owner, string id)
        {
            if ((owner?.BaseAbility?.EffectId ?? owner?.BaseAbility?.Id) == id) return owner.BaseAbility;
            if ((owner?.PassiveAbility?.EffectId ?? owner?.PassiveAbility?.Id) == id) return owner.PassiveAbility;
            return owner?.Tricks?.FirstOrDefault(t => t != null && (t.EffectId ?? t.Id) == id)
                ?? new TrickDefinition { Id = id, EffectId = id };
        }

        // Same units/limits as the faction adapters, EronyaMechanics and shield ledger.
        // A target-dependent shield is an authored HP grant; only Golem's own shield
        // has a known recipient here. Final damage still depends on the actual target.
        public static float DescriptionValue(TrickDefinition effect, string key, float value, UnitDefinition owner)
        {
            string id = effect?.EffectId ?? effect?.Id ?? "";
            if (!EronyaAbilityParameters.Knows(id)) return value;
            if (key == "reduction" && id.StartsWith("alveyr-", StringComparison.Ordinal))
                return Mathf.Clamp(value, 0, (float)EronyaMechanics.ReductionCap);
            if (key == "damageReductionPercent" || key == "reduction")
                return Mathf.Clamp(value, 0, (float)EronyaMechanics.ReductionCap * 100);
            // Live attacks use nonstacking-effects.AttackSpeed (1 + strongest
            // bonus), not the retired EronyaMechanics.AttackSpeed cap. Fractions
            // stay fractions; seconds are never interpreted as speed percentages.
            if (key == "antiHealPercent" || key == "healingReduction") return Mathf.Clamp(value, 0, 100);
            if (key == "duration" || key.EndsWith("Duration", StringComparison.Ordinal))
                return (float)Math.Round(value * 10d, MidpointRounding.AwayFromZero) / 10;
            if (key == "shield" && (id == "golem-tectonico-guardia-de-piedra" || id == "golem-tectonico-escudo-reforzado") && owner != null)
                return Mathf.Min(value, owner.MaxHealth * (float)EronyaMechanics.ShieldCap);
            return value;
        }

        static string FormatDescriptionDeltas(TrickDefinition effect, UnitDefinition owner, string template)
            => DescriptionDeltaToken.Replace(template, token =>
            {
                string key = token.Groups[1].Value, reference = token.Groups[3].Value;
                float updated = DescriptionValue(effect, key, Parameter(effect, key), owner);
                float original;
                if (!float.TryParse(reference, NumberStyles.Float, CultureInfo.InvariantCulture, out original))
                {
                    var baseline = DescriptionReference(owner, reference);
                    string baselineKey = token.Groups[4].Success ? token.Groups[4].Value : key;
                    original = DescriptionValue(baseline, baselineKey, Parameter(baseline, baselineKey), owner);
                }
                return DescriptionDifference(original, updated, token.Groups[2].Value == "deltaPercent");
            });

        public static string FormatDescription(TrickDefinition effect,UnitDefinition owner,string template,bool spanish=true)
        {
            string id=effect.EffectId??effect.Id??"";
            string text=FormatDescriptionDeltas(effect,owner,template??"");
            if (owner != null)
                text=text.Replace("{energyMax}",Number(Mathf.Max(0,owner.EnergyMax)))
                    .Replace("{energyPerAttack}",Number(Mathf.Max(0,owner.EnergyPerAttack)))
                    .Replace("{energyPerSecond}",Number(Mathf.Max(0,owner.EnergyPerSecond)))
                    .Replace("{energyOnDamage}",Number(Mathf.Max(0,owner.EnergyOnDamage)));
            if (owner?.Ruleset == CombatRuleset.Eronya)
                text=text.Replace("{manaCost}",Number(Mathf.Max(0,owner.EnergyMax)))
                    .Replace("{baseArmor}","0")
                    .Replace("{basicDamage}",Number(owner.Damage))
                    .Replace("{basicRange}",Number(owner.AttackRange))
                    .Replace("{basicInterval}",Number(owner.AttackInterval));
            text=text.Replace("{abilityDamage}",DamageFormula(effect,spanish))
                .Replace("{unit}",!string.IsNullOrWhiteSpace(owner?.DisplayName)?owner.DisplayName:
                    spanish?"el personaje":"the character");
            foreach(var p in Resolve(effect))
            {
                float displayed=DescriptionValue(effect,p.Key,p.Value,owner);
                text=text.Replace("{"+p.Key+"}",Number(displayed))
                    .Replace("{"+p.Key+":percent}",Number(displayed*100));
                text=text.Replace("{"+p.Key+"Delta}",Signed(ParameterEditorValue(id,p.Key,p.Value,owner)));
                text=text.Replace("{"+p.Key+"Factor}",Number(p.Value/100));
            }
            if(id=="kazuru-ring-throw")
                text=text.Replace("{range}",Number(effect.RangeBonus)).Replace("{damage}",Number(effect.DamageBonus));
            return text;
        }

        public static string ParameterLabel(string effect,string key)
        {
            if(IsFactorParameter(effect,key))return key=="reflectPercent"?"Daño reflejado (× recibido)":"Daño crítico (× ataque)";
            if(IsRelativeParameter(effect,key))
            {
                switch(key)
                {
                    case "duration":return "Aturdimiento adicional (s)";
                    case "interval":return "Cambio entre monedas (s; negativo = antes)";
                    case "targets":return "Objetivos adicionales";
                    case "superTargets":return "Objetivos adicionales durante el súper";
                    case "heal":return "Curación adicional de la flor (+)";
                    case "health":return "Cambio de vida de la flor (+/−)";
                    case "reduction":return "Reducción adicional (puntos porcentuales)";
                    case "count":return "Invocaciones adicionales";
                }
            }
            switch(key)
            {
                case "secondRingPercent":return "Daño del segundo aro (% del primero)";
                case "burstInterval":return "Separación entre aros (s)";
                case "healthPerKill":return "Vida máxima y curación por eliminación (+)";
                case "damage":return effect=="kazuru-rising-rings"||effect=="popow-dash-charge"||effect=="sepora-kill-power"||effect=="bugui-kill-power"||effect=="kayon-dummy-legacy"||effect=="tsu-chocolate"?"Daño adicional (+)":"Daño fijo";
                case "damageOverride":return "Daño de habilidad";
                case "multiplier":return "Daño (× ataque)";
                case "bounceDamageMultiplier":return "Daño del rebote (× impacto)";
                case "damagePercent":return "Daño adicional (%)";
                case "bonusPercent":return "Bonificación adicional (%)";
                case "speedPercent":return "Velocidad adicional (%)";
                case "weaknessPercent":return "Daño recibido adicional (%)";
                case "extraDuration":return "Duración adicional (s)";
                case "every":return "Cada cuántos ataques";
                case "duration":return "Duración (s)";
                case "health":return effect=="kayon-dummy-health"?"Vida adicional de los Dummys (+)":"Vida";
                case "heal":return "Curación";
                case "healthPercent":return "Vida (%)";
                case "healPercent":return "Curación (%)";
                case "reduction":return "Reducción (%)";
                case "energy":return "Energía";
                case "coins":return "Monedas";
                case "interval":return "Intervalo (s)";
                case "stacks":return "Máximo de acumulaciones";
                case "count":return "Invocaciones";
                case "targets":return "Objetivos";
                case "radius":return "Radio (casillas)";
                case "radiusX":return "Alcance horizontal (casillas por lado)";
                case "radiusY":return "Alcance vertical (casillas por lado)";
                case "width":return "Anchura (casillas)";
                case "extension":return "Casillas extra en el centro de cada lado";
                case "height":return "Altura (casillas)";
                case "projectileSpeed":return effect=="tsu-colored-gum"?"Velocidad del proyectil (casillas/s; 0 = mitad de la velocidad base)":"Velocidad de proyectil";
                case "distance":return "Distancia (casillas)";
                case "bounceRange":return "Alcance del rebote (casillas)";
                case "threshold":return "Umbral de vida (%)";
                case "shield":return "Vida del escudo";
                case "shieldBonus":return "Escudo adicional (+)";
                case "energyPerShieldDamage":return "Energía por daño al escudo aliado (+)";
                case "shieldPercent":return "Escudo: reducción de daño (%)";
                case "tauntDuration":return "Provocación (s)";
                case "travelDuration":return "Recorrido (s)";
                default:return key;
            }
        }
    }
}

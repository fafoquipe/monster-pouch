using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // All action clocks come from the match balance, never from a renderer or its facing.
    public static class AbilityTiming
    {
        // Existing authored returns shared with presentation; the simulation rounds
        // them up to its fixed tick without reading renderer state.
        public const float BackflipLandingDuration = .16f;
        public const float BlotanIncomeFallbackDuration = .72f;
        public static string Effect(string kind)
        {
            switch(kind)
            {
                case "bugaloo-super": return "bugaloo-reflect";
                case "popow-super": return "popow-dash";
                case "sepora-super": return "sepora-storm";
                case "tauris-super": return "tauris-execute";
                case "suki-super": return "suki-fish-shield";
                case "pokrak-super": return "pokrak-bubbles";
                case "kazuru-super": return "kazuru-rising-rings";
                case "rosie-super": return "rosie-healing-rose";
                case "flo-super": return "flo-flower";
                case "kayon-super": return "kayon-summon";
                case "stein-super": return "stein-stun";
                case "tokoro-super": return "tokoro-headbutt";
                case "gochan-super": return "gochan-seal-burst";
                case "hymay-super": return "hymay-pull";
                default: return kind ?? "";
            }
        }
        // These abilities add a hit effect to the basic attack, without a second action clock.
        public static bool UsesBasicAttackAnimation(string kind)=>kind=="tsu-root"||kind=="jazar-radiation";
        public static int Ticks(float seconds) => Mathf.Max(0,Mathf.CeilToInt(seconds/CombatSimulation.TickDuration-.00001f));
        public static float Cast(UnitStats stats,string kind) => Ticks(stats.AbilityValue(Effect(kind),"castDuration",0)<0?stats.AttackWindup:stats.AbilityValue(Effect(kind),"castDuration",0))*CombatSimulation.TickDuration;
        public static float Hold(UnitStats stats,string kind) => stats.AbilityValue(Effect(kind),"holdDuration",0);
        public static float Recovery(UnitStats stats,string kind) => stats.AbilityValue(Effect(kind),"recoveryDuration",0);
        public static bool HasPhases(UnitStats stats,string kind) => stats.AbilityValue(Effect(kind),"castDuration",-2)>=-1;
        public static float Duration(UnitStats stats,string kind,float fallback=.65f)
        {
            if(kind=="blotan-income")fallback=BlotanIncomeFallbackDuration;
            float duration=HasPhases(stats,kind)
                ? Cast(stats,kind)+Hold(stats,kind)+Recovery(stats,kind)
                : stats.AbilityValue(Effect(kind),"animationDuration",0)>0 ? stats.AbilityValue(Effect(kind),"animationDuration",0) : fallback;
            return kind=="blotan-income"&&duration<=0?BlotanIncomeFallbackDuration:duration;
        }
        public static int Damage(UnitStats stats,string effect,int automatic)
        {
            float value=stats.AbilityValue(Effect(effect),"damageOverride",-1);
            return AbilityBalance.ResolveDamage(Effect(effect),value,automatic);
        }
    }
}

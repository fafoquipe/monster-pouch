using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public static class BasicAttackTiming
    {
        // Contact and return match the authored poses; charge remains the designer's windup.
        public static Vector2 DefaultTail(string id)
        {
            switch(id)
            {
                case "alveyr":return new Vector2(.12f,.20f);
                case "anuik":return new Vector2(.10f,.16f);
                case "bugaloo":return new Vector2(.16f,.28f);
                case "popow":return new Vector2(.08f,.16f);
                case "sepora":return new Vector2(.06f,.16f);
                case "tauris":return new Vector2(.16f,.24f);
                case "atori":return new Vector2(.10f,.24f);
                case "blotan":return new Vector2(.08f,.18f);
                case "bugui":return new Vector2(.06f,.12f);
                case "flo":return new Vector2(.08f,.16f);
                case "gochan":return new Vector2(.08f,.18f);
                case "jazar":return new Vector2(.06f,.14f);
                case "kayon":return new Vector2(.12f,.24f);
                case "stein":return new Vector2(.10f,.22f);
                case "trimol":return new Vector2(.14f,.26f);
                case "tsu":return new Vector2(.08f,.16f);
                case "atong":return new Vector2(.18f,.38f);
                case "tokoro":return new Vector2(.18f,.30f);
                case "aky":return new Vector2(.12f,.24f);
                case "hymay":return new Vector2(.08f,.22f);
                default:return Vector2.zero;
            }
        }
        public static float Resolve(float value,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)||value<0?fallback:value;
        // Shared action phases now apply to both rosters, including training timeouts.
        public static float TrainingCycleSeconds(UnitStats stats) =>
            Mathf.Max(stats.AttackInterval,stats.AttackWindup+stats.AttackHold+stats.AttackRecovery);
        public static Vector2 Tail(BattleUnit actor)
        {
            if(actor==null)return new Vector2(.08f,.16f);
            var stats=actor.BaseStats;
            if(actor.ActionAbilityEffect!=null)
            {
                float hold=AbilityTiming.Hold(stats,actor.ActionAbilityEffect),back=AbilityTiming.Recovery(stats,actor.ActionAbilityEffect);
                if(hold>0||back>0)return new Vector2(hold,back);
            }
            return new Vector2(stats.AttackHold,stats.AttackRecovery);
        }
        public static int TailTicks(BattleUnit actor)
        {
            var tail=Tail(actor);
            return Mathf.Max(1,AbilityTiming.Ticks(tail.x+tail.y));
        }
    }
}


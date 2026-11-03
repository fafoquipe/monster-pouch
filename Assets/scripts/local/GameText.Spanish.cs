using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public static partial class GameText
    {
        public static bool Spanish { get; private set; } = PlayerPrefs.GetString("monster-pouch.language","en")=="es";
        public static void SetLanguage(bool spanish)
        { Spanish=spanish;PlayerPrefs.SetString("monster-pouch.language",spanish?"es":"en");PlayerPrefs.Save(); }

        static readonly Dictionary<string,string> ExtraSpanish = new Dictionary<string,string>
        {
            ["ROUND DRAW"]="RONDA ANULADA",["DEFEAT"]="DERROTA",["WINNER"]="GANADOR",
            ["YOU"]="TÚ",["OPPONENT"]="RIVAL",["BASE ABILITY"]="HABILIDAD BASE",
            ["UPGRADE ACTIVE"]="MEJORA ACTIVA",["Upgrade purchased"]="Mejora adquirida",
            ["SELECTED"]="ELEGIDA",["SUPER"]="SÚPER",["PASSIVE"]="PASIVA",["ATTACK"]="ATAQUE",
            ["TAUNT"]="PROVOCADO",["HIDDEN"]="OCULTO",["ROOTED"]="INMOVILIZACIÓN",["IMMOBILIZATION"]="INMOVILIZACIÓN",["BOULDER"]="ROCA",
            ["STUN"]="ATURDIMIENTO",["SHD"]="ESCUDO",["CRITICAL!"]="¡CRÍTICO!",
            ["A copy in the Brief is required"]="Necesitas una copia en el Brief",
            ["Not enough Moonkens."]="No tienes suficientes Moonkens."
        };
        static string ResolveSpanish(string text)
        {
            if(ExtraSpanish.TryGetValue(text,out var extra))return extra;
            if(ReverseExact.TryGetValue(text,out var translated))return translated;
            if(text.Contains("\n"))return string.Join("\n",text.Split('\n').Select(ResolveSpanish));
            if(text.StartsWith("Round "))return text.Replace("Round ","Ronda ");
            if(text.StartsWith("ROUND ")&&text.EndsWith(" COMPLETE"))return text.Replace("ROUND ","RONDA ").Replace(" COMPLETE"," COMPLETADA");
            if(text.StartsWith("Volume: "))return text.Replace("Volume: ","Volumen: ");
            if(text.StartsWith("Cost: "))return text.Replace("Cost: ","Coste: ");
            if(text.StartsWith("SELL · +"))return text.Replace("SELL","VENDER");
            if(text.StartsWith("UPGRADE · "))return text.Replace("UPGRADE","MEJORAR");
            if(text.StartsWith("ABILITY · "))return text.Replace("ABILITY","HABILIDAD");
            if(text.StartsWith("YOUR WHELPS"))return text.Replace("YOUR WHELPS","TUS WHELPS").Replace("SELECTED","ELEGIDOS");
            if(text.StartsWith("HEALTH ")||text.StartsWith("ATTACKS "))
                return text.Replace("HEALTH","VIDA").Replace("DAMAGE","DAÑO").Replace("ATTACKS","ATAQUES").Replace("RANGE","ALCANCE");
            return text;
        }
        static Dictionary<string,string> reverseExact;
        static Dictionary<string,string> ReverseExact
        {
            get
            {
                if(reverseExact!=null)return reverseExact;
                reverseExact=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var pair in Exact)if(!reverseExact.ContainsKey(pair.Value))reverseExact.Add(pair.Value,pair.Key);
                return reverseExact;
            }
        }

        static readonly Dictionary<string,string> SpanishNames = new Dictionary<string,string>
        {
            ["martio-orbits"]="Sistema planetario",["martio-explosion"]="Onda expansiva",["martio-piercing"]="Perforación",["martio-moons"]="Abanico planetario",
            ["anuik-revive"]="Siempre de pie",["anuik-second-revive"]="Una vez más",["anuik-revive-impact"]="Regreso explosivo",["anuik-bounce"]="Corazones rebote",
            ["bugaloo-reflect"]="Rebote lunar",["bugaloo-protection"]="Eclipse protector",["bugaloo-revenge"]="Luna vengativa",["bugaloo-healing"]="Luz sanadora",
            ["popow-dash"]="Embestida",["popow-dash-stun"]="Gran impacto",["popow-sweep"]="Gancho amplio",["popow-dash-charge"]="Carga de impacto",
            ["sepora-storm"]="Tormenta",["sepora-stealth"]="Velo de tormenta",["sepora-multishot"]="Multidescarga",["sepora-kill-power"]="Poder creciente",
            ["tauris-execute"]="Mordida letal",["tauris-mark"]="Presa marcada",["tauris-sweep"]="Mandíbula amplia",["tauris-hunger"]="Hambre insaciable",
            ["atori-hunt"]="Caza al débil",["atori-hard-head"]="Cabeza dura",["atori-hunt-speed"]="Caza sin pausa",["atori-kill-heal"]="Festín de caza",
            ["blotan-income"]="Forja Moonkens",["blotan-fast-income"]="Forja veloz",["blotan-health"]="Truco de fuga",["blotan-steal"]="Botín del ganador",
            ["bugui-rhythm"]="Ritmo de batalla",["bugui-unlimited-range"]="Sin fronteras",["bugui-unlimited-stacks"]="Ritmo infinito",["bugui-kill-power"]="Hambre de poder",
            ["flo-flower"]="Flor sanadora",["flo-flower-heal"]="Plena floración",["flo-full-energy"]="Lista para brotar",["flo-flower-invulnerability"]="Brote protegido",
            ["gochan-seal-burst"]="Sello explosivo",["gochan-anti-heal"]="Sello marchito",["gochan-burn"]="Marca ardiente",["gochan-expanded-seal"]="Sello ampliado",
            ["jazar-radiation"]="Contaminación",["jazar-cross"]="Onda radiactiva",["jazar-intense-radiation"]="Radiación intensa",["jazar-front-row"]="Zona tóxica",
            ["kayon-summon"]="Llamada Dummy",["kayon-double-summon"]="Llamada doble",["kayon-dummy-legacy"]="Legado Dummy",["kayon-dummy-health"]="Dummy reforzado",
            ["stein-stun"]="Tormenta eléctrica",["stein-long-stun"]="Aturdimiento prolongado",["stein-energy-gift"]="Energía compartida",["stein-triple"]="Cuarta descarga",
            ["trimol-boulder"]="Roca salvaje",["trimol-opening-boulder"]="Roca inicial",["trimol-infinite-boulder"]="Roca imparable",["trimol-brutal-boulder"]="Roca brutal",
            ["tsu-root"]="Chicle pegajoso",["tsu-death-trap"]="Chicle duradero",["tsu-chocolate"]="Chocolate explosivo",["tsu-colored-gum"]="Chicle de colores",
            ["atong-critical"]="Golpe calculado",["atong-critical-damage"]="Crítico devastador",["atong-always-critical"]="Golpe perfecto",["atong-control-immunity"]="Inquebrantable",
            ["tokoro-headbutt"]="Cabezazo dormilón",["tokoro-stun-heal"]="Siesta reparadora",["tokoro-ice-cream"]="Helado sorpresa",["tokoro-sleep"]="Hora de dormir",
            ["aky-energy-drain"]="Drenaje de energía",["aky-opening-flight"]="Vuelo rasante",["aky-drain-heal"]="Absorción vital",["aky-sweep"]="Golpe amplio",
            ["hymay-pull"]="Tirón de látigo",["hymay-focus"]="Objetivo marcado",["hymay-weakness"]="Presa vulnerable",["hymay-backstep"]="Tirón acrobático"
        };
        static string SpanishAbilityName(TrickDefinition ability)
        {return SpanishNames.TryGetValue(ability.EffectId??ability.Id??"",out var name)?name:ability.Name;}

        static string SpanishAbilityDescription(TrickDefinition ability,UnitDefinition owner)
            =>AbilityBalance.Description(ability,owner);
    }
}

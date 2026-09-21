using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // Shared by combat, the inspector and the player-facing ability cards.
    public static class AbilityBalance
    {
        const string Rows = @"anuik-revive|Revive una vez con el 50% de vida.|
anuik-second-revive|Revive otra vez con {healthPercent}% de vida.|healthPercent=25
anuik-revive-impact|Al revivir, causa {damage} de daño y empuja a los enemigos cercanos.|damage=10
anuik-revive-jump|Al revivir, salta {distance} casillas hacia delante.|distance=4
bugaloo-reflect|Provoca a enemigos cercanos y refleja su daño durante {duration} s.|duration=3,radius=2
bugaloo-protection|Recibe un {reduction}% menos de daño durante su súper.|reduction=50
bugaloo-revenge|Refleja el {reflectPercent}% del daño recibido durante su súper.|reflectPercent=150
bugaloo-healing|Recibe un {bonusPercent}% más de curación.|bonusPercent=50
popow-dash|Embiste al enemigo más lejano. Aturde {duration} s a quienes atraviesa.|duration=1
popow-dash-stun|La embestida aturde durante {duration} s.|duration=2
popow-sweep|Golpea 3 casillas y aturde durante {duration} s.|duration=0.5
popow-dash-charge|El siguiente golpe gana +{damage} de daño por enemigo embestido.|damage=3
sepora-storm|Durante {duration} s, ataca a {targets} enemigos un {speedPercent}% más rápido. No carga energía.|duration=5,targets=3,speedPercent=50
sepora-stealth|Al bajar del {threshold}% de vida, ella y sus aliados cercanos se ocultan {duration} s.|threshold=50,duration=2
sepora-multishot|Ataca a {targets} enemigos. Durante el súper, a {superTargets}.|targets=2,superTargets=5
sepora-kill-power|Cada baja otorga +{damage} de daño para el resto de la partida.|damage=1
tauris-execute|Elimina al objetivo de un mordisco.|
tauris-mark|Al inicio, marca a enemigos de su fila y gana {energy} de energía por cada uno.|energy=25
tauris-sweep|El mordisco alcanza 3 casillas frente a Tauris.|
tauris-hunger|El súper quita {healthPercent}% de vida actual. Cada baja cura toda su vida y da +{damagePercent}% de daño.|healthPercent=50,damagePercent=50
atori-hunt|Busca al enemigo con menos vida.|
atori-hard-head|Recibe {reduction}% menos de daño durante los primeros {duration} s.|reduction=20,duration=5
atori-hunt-speed|Cada baja aumenta {speedPercent}% su velocidad.|speedPercent=50
atori-kill-heal|Recupera {heal} de vida por cada baja.|heal=20
blotan-income|Consigue {coins} munken cada {interval} s.|coins=1,interval=20
blotan-fast-income|Consigue munkens cada {interval} s.|interval=10
blotan-health|Aumenta su vida máxima.|
blotan-steal|Si sobrevive, roba {coins} munkens al rival.|coins=2
bugui-rhythm|Cada ataque da +{speedPercent}% de velocidad. Máximo {stacks} cargas.|speedPercent=5,stacks=10
bugui-unlimited-range|Sus ataques alcanzan todo el tablero.|
bugui-unlimited-stacks|Cada ataque da +{speedPercent}% de velocidad, sin límite.|speedPercent=2.5
bugui-kill-power|Cada baja otorga +{damage} de daño esta ronda.|damage=1
flo-flower|Invoca una flor con {health} de vida. Al caer, cura {heal} a los aliados cercanos.|health=20,heal=20
flo-flower-heal|La flor cura {heal} de vida al caer.|heal=40
flo-full-energy|Empieza con la energía al máximo.|
flo-flower-invulnerability|La flor tiene {health} de vida y es invulnerable durante {duration} s.|health=1,duration=1
gochan-seal-burst|Inflige {damage} de daño en un área de 3 × 3.|damage=20
gochan-anti-heal|Sus ataques reducen la curación {reduction}% durante {duration} s.|reduction=60,duration=4
gochan-burn|El súper quema: {damage} de daño cada segundo durante {duration} s. Se acumula.|damage=2,duration=4
gochan-expanded-seal|Sus ataques básicos dañan un área de 3 × 3.|
jazar-radiation|Reduce {reduction}% la velocidad de ataque del objetivo.|reduction=25
jazar-cross|Sus ataques dañan en cruz alrededor del objetivo.|
jazar-intense-radiation|La radiación reduce {reduction}% la velocidad de ataque.|reduction=50
jazar-front-row|Al inicio, contamina la primera fila enemiga durante {duration} s.|duration=1
kayon-summon|Invoca {count} Dummy para luchar a su lado.|count=1
kayon-double-summon|Invoca {count} Dummys con cada súper.|count=2
kayon-dummy-legacy|Al caer un Dummy, los aliados cercanos ganan +{damage} de daño.|damage=1
kayon-dummy-health|Los Dummys tienen +{health} de vida.|health=4
stein-stun|Lanza {targets} descargas. Cada impacto aturde {duration} s.|targets=2,duration=0.1
stein-long-stun|Cada impacto aturde durante {duration} s.|duration=0.2
stein-energy-gift|Al inicio, da {energy} de energía a los aliados de ambos lados.|energy=25
stein-triple|Dispara a {targets} objetivos a la vez.|targets=3
trimol-boulder|Cada {every} ataques, lanza una roca: daño ×{multiplier} y {duration} s de aturdimiento en su recorrido.|every=4,multiplier=2,duration=1
trimol-opening-boulder|Lanza una roca al empezar la ronda.|
trimol-infinite-boulder|La roca recorre todo el tablero.|
trimol-brutal-boulder|La roca inflige daño ×{multiplier} y aturde {duration} s.|multiplier=3,duration=2
tsu-root|Cada {every} ataques, inmoviliza al objetivo durante {duration} s.|every=4,duration=2
tsu-death-trap|Al caer, deja una trampa que inmoviliza al primer enemigo que la pisa.|
tsu-chocolate|Mientras dura la inmovilización, causa daño creciente: +{damage} por segundo.|damage=2
tsu-colored-gum|Alterna regalos a un aliado: +{bonusPercent}% velocidad, escudo o daño.|bonusPercent=20,shieldPercent=10
atong-critical|Cada {every} golpes, inflige {damagePercent}% de daño.|every=2,damagePercent=150
atong-critical-damage|Los golpes críticos infligen {damagePercent}% de daño.|damagePercent=200
atong-always-critical|Todos sus golpes son críticos.|
atong-control-immunity|Inmune al control durante los primeros {duration} s.|duration=5
tokoro-headbutt|Cabezazo de daño ×{multiplier}. Ambos quedan aturdidos {duration} s.|multiplier=2,duration=3
tokoro-stun-heal|Mientras está aturdido, recupera {heal} de vida cada segundo.|heal=8
tokoro-ice-cream|Al aturdirse, deja helados: curan {heal} o hacen recibir +{weaknessPercent}% de daño durante {duration} s.|heal=10,weaknessPercent=20,duration=3
tokoro-sleep|Los últimos {duration} s duerme: deja de atacar y cura {heal} por segundo.|duration=10,heal=15
aky-energy-drain|Cada golpe roba {energy} de energía al objetivo.|energy=20
aky-opening-flight|Al inicio, vuela al fondo de su columna en {duration} s. No puede ser atacado durante el vuelo.|duration=1.5
aky-drain-heal|Se cura el {healPercent}% de la energía que roba.|healPercent=50
aky-sweep|Sus golpes alcanzan 3 casillas.|
hymay-pull|Atrae al objetivo a la casilla de enfrente.|
hymay-focus|Los aliados concentran sus ataques en el enemigo atraído.|
hymay-weakness|El enemigo atraído recibe {bonusPercent}% más de daño.|bonusPercent=25
hymay-backstep|Tras atraer al enemigo, retrocede {distance} casilla.|distance=1";
        static readonly Dictionary<string,string[]> Entries = Rows.Split('\n').Select(line=>line.Trim().Split('|')).ToDictionary(row=>row[0],row=>row,StringComparer.Ordinal);
        public static AbilityParameter[] Resolve(TrickDefinition effect)
        {
            if(effect==null||!Entries.TryGetValue(effect.EffectId??effect.Id??"",out var entry)||entry.Length<3||entry[2].Length==0)return Array.Empty<AbilityParameter>();
            return entry[2].Split(',').Select(part=>{
                var pair=part.Split('=');float value=float.Parse(pair[1],CultureInfo.InvariantCulture);
                if(pair[0]=="every" && effect.EveryAttacks>0)value=effect.EveryAttacks;
                var edited=effect.Parameters?.FirstOrDefault(p=>p!=null&&p.Key==pair[0]);
                if(edited!=null&&!float.IsNaN(edited.Value)&&!float.IsInfinity(edited.Value))value=Mathf.Max(0,edited.Value);
                if(effect.EffectId=="aky-opening-flight"&&pair[0]=="duration")
                    value=Mathf.Max(1,Mathf.CeilToInt(value/CombatSimulation.TickDuration-.00001f))*CombatSimulation.TickDuration;
                return new AbilityParameter{Key=pair[0],Value=value};
            }).ToArray();
        }
        public static string Description(TrickDefinition effect, UnitDefinition owner = null)
        {
            if(effect==null)return "";
            string id=effect.EffectId??effect.Id??"";
            // Unit-level mechanics use the same balance fields as combat, never a saved sentence.
            if(id=="anuik-revive" && owner!=null)
            {
                int revives=Mathf.Max(0,owner.RevivesPerCombat);
                if(revives==0)return "No revive.";
                string times=revives==1?"una vez":revives.ToString(CultureInfo.InvariantCulture)+" veces";
                string health=(Mathf.Clamp(owner.ReviveHealthFraction,.01f,1f)*100f).ToString("0.##",CultureInfo.InvariantCulture);
                return "Revive "+times+" con el "+health+"% de vida.";
            }
            string text=Entries.TryGetValue(id,out var entry)?entry[1]:effect.Description??"";
            foreach(var parameter in Resolve(effect))text=text.Replace("{"+parameter.Key+"}",parameter.Value.ToString("0.##",CultureInfo.InvariantCulture));
            if(id=="blotan-health")text="+"+Mathf.Max(0,effect.HealthBonus)+" de vida máxima.";
            return text;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // Shared by combat, the inspector and the player-facing ability cards.
    public static partial class AbilityBalance
    {
        const string Rows = @"martio-orbits|Marte y dos lunas recorren hasta {travelRange} casillas. Las lunas causan {firstMoonDamage} y {secondMoonDamage} de daño.|firstMoonDamage=3,secondMoonDamage=3,travelRange=7
martio-explosion|Cada impacto provoca una onda 3×3: Marte causa {marsDamage} y las lunas {firstMoonDamage} y {secondMoonDamage} de daño a los rivales adyacentes. El centro recibe solo el impacto.|marsDamage=6,firstMoonDamage=3,secondMoonDamage=3
martio-piercing|Atraviesan a los rivales hasta terminar su recorrido. Un impacto directo por rival y proyectil; las explosiones vecinas pueden volver a dañarlos.|
martio-moons|Añade dos grupos de Marte y lunas a ±{fanAngleDegrees}°. Recorrido lateral: {lateralTravelRange} casillas. Conservan el daño base y las mejoras de explosión y perforación.|lateralTravelRange=3,fanAngleDegrees=30
kazuru-rising-rings|Otorga +{damage} de daño durante {duration} s a los aliados en un área de {width}×{width}, incluido {unit}. Se suma antes de los críticos.|damage=15,duration=5,width=3,castDuration=0.3,holdDuration=0.1,recoveryDuration=0.2
kazuru-ring-throw|+{range} de alcance. Cada ataque lanza 2 aros en ráfaga: el segundo causa {secondRingPercent}% del daño del primero. Después retoma su cadencia normal.|secondRingPercent=70,burstInterval=0.2
kazuru-last-halo|Una vez por combate, sobrevive a un golpe mortal con 1 de vida y bloquea todo el daño durante {duration} s.|duration=2
kazuru-renewal|Cada súper recupera {heal} de vida por segundo durante {duration} s.|heal=10,duration=3
pokrak-bubbles|Burbujas en un área de {width}×{width}, incluido {unit}: reducen el {reduction}% del daño recibido durante {duration} s.|width=3,reduction=20,duration=2,castDuration=0.3,holdDuration=0.1,recoveryDuration=0.25
pokrak-long-bubbles|Las burbujas duran {duration} s.|duration=3
pokrak-wide-bubbles|Añade {extension} casilla en el centro de cada lado del área.|extension=1
pokrak-strong-bubbles|Las burbujas reducen el {reduction}% del daño recibido.|reduction=25
suki-fish-shield|Lanza pescados a los {targets} aliados más cercanos. Cada uno y {unit} reciben {shield} de escudo durante {duration} s. Absorbe daño antes que la vida.|shield=15,duration=5,targets=3,travelDuration=0.7,castDuration=0.3,holdDuration=0.1,recoveryDuration=0.25
suki-shield-energy|+{energyPerShieldDamage} de energía por punto de daño absorbido por los escudos dados a aliados. Su propio escudo no activa este bonus.|energyPerShieldDamage=1
suki-opening-shield|Lanza su habilidad gratis al iniciar la ronda.|castDuration=0,holdDuration=0.35,recoveryDuration=0.25
suki-extra-shield|+{shieldBonus} de escudo a su habilidad, también para {unit}.|shieldBonus=10
rosie-healing-rose|Rosa de {width} casillas de ancho: cura el {healPercent}% de vida máxima, incluida {unit}, y sigue recta hasta salir del tablero. Apunta al aliado más cercano desde la última fila; al más lejano desde las demás.|healPercent=25,width=3,projectileSpeed=3,castDuration=0.3,holdDuration=0,recoveryDuration=0.2
rosie-haste|La rosa otorga +{speedPercent}% de velocidad de ataque durante {duration} s, también a {unit}.|speedPercent=30,duration=3
rosie-shield|La rosa otorga un escudo del {shieldHealthPercent}% de vida máxima, también a {unit}.|shieldHealthPercent=10
rosie-control-immunity|Elimina y bloquea stun, ralentización y demás controles durante {duration} s, también en {unit}.|duration=3
anuik-revive|Revive una vez con el 50% de vida.|
anuik-second-revive|+1 resurrección con {healthPercent}% de vida.|healthPercent=25
anuik-revive-impact|Al revivir: {damage} de daño adicional en área, empuje y {stunDuration} s de aturdimiento.|damage=10,stunDuration=3
anuik-bounce|+1 rebote hacia otro enemigo a {bounceRange} casillas o menos. Daño del rebote: ×{bounceDamageMultiplier} del impacto.|bounceDamageMultiplier=0.5,bounceRange=3
bugaloo-reflect|Provoca {tauntDuration} s a enemigos cercanos. Durante {duration} s devuelve el daño en lunas que golpean al llegar.|duration=3,radius=2,tauntDuration=3,castDuration=0,holdDuration=0,recoveryDuration=0.18
bugaloo-protection|−{reduction}% de daño recibido durante su súper.|reduction=50
bugaloo-revenge|Refleja ×{reflectPercentFactor} del daño recibido durante su súper.|reflectPercent=150
bugaloo-healing|+{bonusPercent}% de curación recibida.|bonusPercent=50
popow-dash|Atraviesa al enemigo más lejano y termina detrás. Aturde {duration} s a quienes cruza.|duration=1,travelDuration=0.38,width=1,castDuration=0,holdDuration=0,recoveryDuration=0
popow-dash-stun|{durationDelta} s de aturdimiento en la embestida.|duration=2
popow-sweep|Cada {every} ataques lanza un gancho que golpea +2 casillas laterales y aturde {duration} s.|every=3,duration=2
popow-dash-charge|El siguiente golpe gana +{damage} de daño por enemigo embestido.|damage=3
sepora-storm|Durante {duration} s: {abilityDamage} por rayo, {targets} objetivos y +{speedPercent}% de velocidad de ataque. No carga energía.|duration=5,targets=3,speedPercent=50,castDuration=0.6,holdDuration=0.1,recoveryDuration=0.2,damageOverride=-1
sepora-stealth|Al bajar del {threshold}% de vida, ella y sus aliados cercanos se ocultan {duration} s.|threshold=50,duration=2,animationDuration=0
sepora-multishot|Objetivos adicionales: {targetsDelta} en ataques básicos y {superTargetsDelta} durante el súper.|targets=2,superTargets=5
sepora-kill-power|Cada baja otorga +{damage} de daño permanente. Máximo {maxPerRound} incrementos nuevos por ronda.|damage=1,maxPerRound=3
tauris-execute|Elimina al objetivo de un mordisco.|castDuration=0.65,holdDuration=0,recoveryDuration=0.18
tauris-mark|Al empezar el combate, marca enemigos hasta {range} casillas delante, en su columna. +{energy} de energía por marcado. No pueden recolocarse desde la siguiente preparación.|energy=25,range=5
tauris-sweep|+2 casillas de alcance lateral para el mordisco.|
tauris-hunger|El súper quita {healthPercent}% de vida actual. Cada baja suma {healthPerKill} de vida máxima, cura esa cantidad y da +{damagePercent}% de daño. La vida ganada se acumula entre rondas de la partida.|healthPercent=50,damagePercent=50,healthPerKill=20
atori-hunt|Busca al enemigo con menos vida.|
atori-hard-head|−{reduction}% de daño recibido durante los primeros {duration} s.|reduction=20,duration=5
atori-hunt-speed|+{speedPercent}% de velocidad de ataque y movimiento por cada baja.|speedPercent=50
atori-kill-heal|+{heal} de vida por cada baja.|heal=20
blotan-income|+{coins} Moonkens cada {interval} s.|coins=1,interval=20,animationDuration=0
blotan-fast-income|{intervalDelta} s entre entregas de Moonkens.|interval=10
blotan-health|Al generar Moonkens, se vuelve invisible para las tropas durante {duration} s y se teletransporta a una casilla libre aleatoria hasta {radiusX} casillas en horizontal y {radiusY} en vertical.|duration=0.5,radiusX=2,radiusY=2
blotan-steal|Si sobrevive, roba {coins} Moonkens al rival.|coins=2
bugui-rhythm|Cada ataque da +{speedPercent}% de velocidad de ataque. Máximo {stacks} cargas.|speedPercent=5,stacks=10
bugui-unlimited-range|Sus ataques alcanzan todo el tablero.|
bugui-unlimited-stacks|Cada ataque da +{speedPercent}% de velocidad de ataque, sin límite.|speedPercent=5
bugui-kill-power|Cada baja otorga +{damage} de daño esta ronda.|damage=1
flo-flower|Planta una flor tras {castDuration} s. Tiene {health} de vida y otorga +{heal} de vida a aliados cercanos al caer.|health=20,heal=20,castDuration=0.45,holdDuration=0,recoveryDuration=0
flo-flower-heal|{healDelta} de curación adicional de la flor al caer.|heal=40
flo-full-energy|Empieza con la energía al máximo.|
flo-flower-invulnerability|{healthDelta} de vida para la flor. Invulnerable durante {duration} s.|health=1,duration=1
gochan-seal-burst|Causa {abilityDamage} en un área de 3 × 3 casillas.|damage=20,castDuration=0.4,holdDuration=0.1,recoveryDuration=0.2
gochan-anti-heal|Sus ataques aplican −{reduction}% de curación recibida durante {duration} s.|reduction=60,duration=4
gochan-burn|El súper añade una quemadura: +{damage} de daño por segundo durante {duration} s. Se acumula.|damage=2,duration=4
gochan-expanded-seal|+4 casillas diagonales al área del ataque básico: completa un área de 3 × 3.|
jazar-radiation|Sus ataques aplican −{reduction}% de velocidad de ataque al objetivo.|reduction=25
jazar-cross|Añade daño en las 4 casillas adyacentes al objetivo, formando una cruz.|
jazar-intense-radiation|{reductionDelta} puntos porcentuales de reducción de velocidad de ataque con Radiación.|reduction=50
jazar-front-row|Al inicio, contamina la primera fila enemiga durante {duration} s; los afectados quedan ralentizados hasta el final de la ronda.|duration=3
kayon-summon|Invoca {count} Dummy para luchar a su lado.|count=1,castDuration=0.35,holdDuration=0,recoveryDuration=0.2
kayon-double-summon|Invocaciones adicionales de Dummy por súper: {countDelta}.|count=2
kayon-dummy-legacy|Al caer un Dummy, los aliados cercanos ganan +{damage} de daño.|damage=1
kayon-dummy-health|Los Dummys tienen +{health} de vida.|health=4
stein-stun|Lanza {targets} rayos a enemigos distintos. Cada rayo causa {abilityDamage} y aturde {duration} s.|targets=3,duration=2,castDuration=0.65,holdDuration=0.1,recoveryDuration=0.2,damageOverride=-1
stein-long-stun|+{extraDuration} s de aturdimiento para los rayos del súper.|extraDuration=1.5,duration=3.5
stein-energy-gift|Al inicio: +{energy} de energía a {unit} y a los aliados justo a su izquierda y derecha.|energy=25
stein-triple|Objetivos adicionales para los rayos del súper: {targetsDelta}.|targets=4
trimol-boulder|Cada {every} ataques, lanza una roca: {abilityDamage} y {duration} s de aturdimiento a enemigos en su recorrido.|every=4,multiplier=2,duration=1,castDuration=0.25,holdDuration=0,recoveryDuration=0.15,damageOverride=-1
trimol-opening-boulder|Lanza una roca al empezar la ronda.|castDuration=0,holdDuration=0,recoveryDuration=0.15
trimol-infinite-boulder|La roca recorre todo el tablero.|
trimol-brutal-boulder|Daño de la roca: {abilityDamage}. {durationDelta} s de aturdimiento.|multiplier=3,duration=2,damageOverride=-1
tsu-root|Cada {every} ataques, inmoviliza al objetivo {duration} s y causa {abilityDamage}.|every=4,duration=2,damageOverride=-1
tsu-death-trap|+{extraDuration} s de inmovilización para el Chicle Pegajoso.|extraDuration=3
tsu-chocolate|Mientras dura la inmovilización, causa daño creciente: +{damage} de daño por segundo.|damage=2
tsu-colored-gum|Alterna regalos a un aliado: +{bonusPercent}% de velocidad de ataque y movimiento, su escudo reduce {shieldPercent}% el daño recibido o +{bonusPercent}% de daño.|bonusPercent=50,shieldPercent=30,projectileSpeed=0
atong-critical|Cada {every} golpes, hace un crítico: {abilityDamage}.|every=2,damagePercent=150,castDuration=-1,holdDuration=0,recoveryDuration=0,damageOverride=-1
atong-critical-damage|Daño de los golpes críticos: {abilityDamage}.|damagePercent=200,damageOverride=-1
atong-always-critical|Todos sus golpes son críticos.|
atong-control-immunity|Inmune al control durante los primeros {duration} s.|duration=5
tokoro-headbutt|Cabezazo: {abilityDamage}. Ambos quedan aturdidos {duration} s.|damageOverride=10,duration=3,castDuration=0.5,holdDuration=1,recoveryDuration=0.2
tokoro-stun-heal|Mientras está aturdido: +{heal} de vida cada segundo.|heal=8
tokoro-ice-cream|Cada nuevo aturdimiento recibido lanza una tanda de helados, incluso si ya estaba aturdido: +{heal} de vida a aliados o +{weaknessPercent}% de daño recibido por enemigos durante {duration} s.|heal=10,weaknessPercent=20,duration=3,travelDuration=0.4
tokoro-sleep|Descansa los primeros {duration} s: no ataca ni obtiene energía y recupera +{heal} de vida por segundo.|duration=3,heal=60,blockActions=1,blockEnergy=1,animationDuration=0
aky-energy-drain|Cada golpe roba {energy} de energía al objetivo.|energy=20
aky-opening-flight|Al inicio, vuela al fondo de su columna en {duration} s. No puede ser atacado durante el vuelo.|duration=1.5,castDuration=0,holdDuration=0,recoveryDuration=0
aky-drain-heal|Se cura el {healPercent}% de la energía que roba.|healPercent=50
aky-sweep|+2 casillas de alcance lateral en sus golpes.|
hymay-pull|Atrae al objetivo en {pullDuration} s y causa {abilityDamage}. Aturde durante el tirón y +{postArrivalStun} s al llegar.|pullDuration=0.45,postArrivalStun=1,castDuration=0.3,holdDuration=0,recoveryDuration=0.15,damageOverride=-1
hymay-focus|Al atraer a un enemigo, los aliados en un área de {width} × {height} casillas junto a {unit} concentran sus ataques en él.|width=2,height=2
hymay-weakness|+{bonusPercent}% de daño recibido por el enemigo atraído.|bonusPercent=25
hymay-backstep|Tras atraer al enemigo, salta {distance} casilla hacia atrás. Invulnerable durante {travelDuration} s.|distance=1,travelDuration=0.7";
        static readonly Dictionary<string,string[]> Entries = Rows.Split('\n').Select(line=>line.Trim().Split('|')).ToDictionary(row=>row[0],row=>row,StringComparer.Ordinal);
        public static bool IsRetiredArmorEffect(string effectId) =>
            effectId=="padding"||effectId=="reinforced";
        public static bool IsRetiredArmorParameter(string effectId,string key) =>
            IsRetiredArmorEffect(effectId)||key=="armor"||key=="armorReduction"||key=="armorPenetration"||
            (effectId=="la-minera-fractura"&&key=="duration");
        public static AbilityParameter[] Resolve(TrickDefinition effect)
        {
            if (EronyaAbilityParameters.Knows(effect?.EffectId ?? effect?.Id)) return EronyaAbilityParameters.Resolve(effect);
            if(effect==null||!Entries.TryGetValue(effect.EffectId??effect.Id??"",out var entry)||entry.Length<3||entry[2].Length==0)return Array.Empty<AbilityParameter>();
            return entry[2].Split(',').Select(part=>{
                var pair=part.Split('=');float value=float.Parse(pair[1],CultureInfo.InvariantCulture);
                if(pair[0]=="every" && effect.EveryAttacks>0)value=effect.EveryAttacks;
                var edited=effect.Parameters?.FirstOrDefault(p=>p!=null&&p.Key==pair[0]);
                // Old saved abilities shared one duration; preserve their custom timing.
                if(pair[0]=="tauntDuration" && effect.EffectId=="bugaloo-reflect" && edited==null)
                {
                    var duration=effect.Parameters?.FirstOrDefault(p=>p!=null&&p.Key=="duration");
                    if(duration!=null&&!float.IsNaN(duration.Value)&&!float.IsInfinity(duration.Value))value=Mathf.Max(0,duration.Value);
                }
                if(edited!=null&&!float.IsNaN(edited.Value)&&!float.IsInfinity(edited.Value))value=Mathf.Max(pair[0]=="damageOverride"||pair[0]=="castDuration"?-1:0,edited.Value);
                if((effect.EffectId=="blotan-health"&&pair[0]=="duration") ||
                    (effect.EffectId=="aky-opening-flight"&&pair[0]=="duration") ||
                    (effect.EffectId=="popow-dash"&&pair[0]=="travelDuration") ||
                    (effect.EffectId=="hymay-pull"&&pair[0]=="pullDuration") ||
                    (effect.EffectId=="hymay-backstep"&&pair[0]=="travelDuration"))
                    value=Mathf.Max(1,Mathf.CeilToInt(value/CombatSimulation.TickDuration-.00001f))*CombatSimulation.TickDuration;
                if(effect.EffectId=="hymay-focus" && (pair[0]=="width" || pair[0]=="height"))
                    value=Mathf.Clamp(Mathf.RoundToInt(value),1,pair[0]=="width"?BoardManager.Width:BoardManager.Height);
                if(effect.EffectId=="blotan-health"&&(pair[0]=="radiusX"||pair[0]=="radiusY"))
                    value=Mathf.Clamp(Mathf.RoundToInt(value),0,pair[0]=="radiusX"?BoardManager.Width-1:BoardManager.Height-1);
                if(effect.EffectId=="tauris-mark"&&pair[0]=="range")
                    value=TaurisMarkGeometry.NormalizeRange(value);
                return new AbilityParameter{Key=pair[0],Value=MartioBalanceValues.Normalize(effect.EffectId??effect.Id,pair[0],value)};
            }).ToArray();
        }
        public static bool IsReadOnlyParameter(string effectId,string key) => EronyaAbilityParameters.IsReadOnlyParameter(effectId,key);
        public static float Value(TrickDefinition effect, string key, float fallback = 0f)
        {
            var parameter = Resolve(effect).FirstOrDefault(p => p.Key == key);
            return parameter == null ? fallback : parameter.Value;
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
            return FormatDescription(effect,owner,text);
        }
    }
}


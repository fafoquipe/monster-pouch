using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Player-facing summaries only. Numeric values come from the same resolved
    // abilities as combat; this class never changes assets or balance parameters.
    public static class AbilitySummaryDescriptions
    {
        const string Rows=@"martio-orbits|Marte y dos lunas.\nRecorrido: {travelRange} casillas.\nDaño de las lunas: {firstMoonDamage} y {secondMoonDamage}.|Mars and two moons.\nTravel: {travelRange} tiles.\nMoon damage: {firstMoonDamage} and {secondMoonDamage}.
martio-explosion|Cada impacto: onda 3×3.\nDaño adyacente: Marte {marsDamage}; lunas {firstMoonDamage} y {secondMoonDamage}.\nEl centro solo recibe el impacto.|Each impact: 3×3 wave.\nAdjacent damage: Mars {marsDamage}; moons {firstMoonDamage} and {secondMoonDamage}.\nThe center takes only the direct hit.
martio-piercing|Los proyectiles atraviesan rivales hasta terminar el recorrido.\nUn impacto directo por rival y proyectil. Las ondas vecinas pueden volver a dañarlo.|Projectiles pass through enemies until their path ends.\nOne direct hit per enemy per projectile. Nearby waves can hit again.
martio-moons|Añade dos grupos de Marte y lunas a ±{fanAngleDegrees}°.\nRecorrido lateral: {lateralTravelRange} casillas.\nConservan el daño base y las mejoras de explosión y perforación.|Adds two Mars-and-moons groups at ±{fanAngleDegrees}°.\nSide travel: {lateralTravelRange} tiles.\nThey retain base damage and the explosion and piercing upgrades.
kazuru-rising-rings|Aliados y {unit}, área {areaWidth}×{areaWidth}.\nDaño +{damage}.\nDuración: {duration} s.|Allies and {unit}, {areaWidth}×{areaWidth} area.\nDamage +{damage}.\nDuration: {duration} s.
kazuru-ring-throw|Alcance +{range} casillas.\nLanza 2 aros por ataque.\nSegundo aro: {secondRingPercent} % de daño.|Range +{range} tiles.\nThrows 2 rings per attack.\nSecond ring: {secondRingPercent} % damage.
kazuru-last-halo|Sobrevive con 1 de vida.\nINVULNERABILIDAD: {duration} s.\nUna vez por combate.|Survives with 1 health.\nINVULNERABILITY: {duration} s.\nOnce per battle.
kazuru-renewal|Al usar el súper:\nVida +{heal}/s.\nDuración: {duration} s.|After each Super:\nHealth +{heal}/s.\nDuration: {duration} s.
pokrak-bubbles|Protege a {unit} y aliados en {areaWidth}×{areaWidth}.\nDaño recibido: −{reduction} %.\nDuración: {duration} s.|Protects {unit} and allies in {areaWidth}×{areaWidth}.\nDamage taken: −{reduction} %.\nDuration: {duration} s.
pokrak-long-bubbles|Burbujas:\nDuración: {duration} s.|Bubbles:\nDuration: {duration} s.
pokrak-wide-bubbles|Área de burbujas:\n+{extension} casilla en el centro de cada lado.|Bubble area:\n+{extension} tile at the center of each side.
pokrak-strong-bubbles|Burbujas:\nDaño recibido: −{reduction} %.|Bubbles:\nDamage taken: −{reduction} %.
suki-fish-shield|{unit} y {targets} aliados cercanos:\nEscudo: {shield}.\nDuración: {duration} s.|{unit} and {targets} nearby allies:\nShield: {shield}.\nDuration: {duration} s.
suki-shield-energy|Escudos de sus aliados:\nEnergía +{energyPerShieldDamage} por daño absorbido.\nSu propio escudo no cuenta.|Allied shields:\nEnergy +{energyPerShieldDamage} per damage absorbed.\nHer own shield does not count.
suki-opening-shield|Usa su habilidad al comenzar la ronda.\nCoste de energía: 0.|Uses her ability at round start.\nEnergy cost: 0.
suki-extra-shield|Escudos de {unit} y sus aliados:\nEscudo +{shieldBonus}.|Shields for {unit} and allies:\nShield +{shieldBonus}.
rosie-healing-rose|Cura a {unit} y aliados que alcanza.\nVida +{healPercent} % de la máxima.\nAncho: {width} casillas.|Heals {unit} and allies reached.\nHealth +{healPercent} % of maximum.\nWidth: {width} tiles.
rosie-haste|Aliados que alcanza la rosa:\nVelocidad de ataque +{speedPercent} %.\nDuración: {duration} s.|Allies reached by the rose:\nAttack speed +{speedPercent} %.\nDuration: {duration} s.
rosie-shield|Aliados que alcanza la rosa:\nEscudo: {shieldHealthPercent} % de vida máxima.|Allies reached by the rose:\nShield: {shieldHealthPercent} % of maximum health.
rosie-control-immunity|La rosa elimina el control.\nInmunidad al control: {duration} s.|The rose removes control effects.\nControl immunity: {duration} s.
anuik-second-revive|Resurrecciones: +1.\nVida al revivir: {healthPercent} %.|Revivals: +1.\nRevival health: {healthPercent} %.
anuik-revive-impact|Al revivir, empuja a enemigos cercanos.\nDaño: {damage}.\nAturdimiento: {stunDuration} s.|On revival, pushes nearby enemies.\nDamage: {damage}.\nStun: {stunDuration} s.
anuik-bounce|Rebotes: +1.\nDaño del rebote: {bouncePercent} %.\nAlcance: {bounceRange} casillas.|Bounces: +1.\nBounce damage: {bouncePercent} %.\nRange: {bounceRange} tiles.
bugaloo-reflect|Provoca a enemigos cercanos para que lo ataquen: {tauntDuration} s.\nDevuelve el daño recibido: {duration} s.|Taunts nearby enemies into attacking him: {tauntDuration} s.\nReflects damage taken: {duration} s.
bugaloo-protection|Durante el súper:\nDaño recibido: −{reduction} %.|During Super:\nDamage taken: −{reduction} %.
bugaloo-revenge|Durante el súper:\nDaño reflejado: {reflectPercent} % del recibido.|During Super:\nReflected damage: {reflectPercent} % of damage taken.
bugaloo-healing|Cura recibida: +{bonusPercent} %.|Healing received: +{bonusPercent} %.
popow-dash|Embestida: atraviesa al rival más lejano y aturde {duration} s.|Dash: crosses the farthest enemy and stuns for {duration} s.
popow-dash-stun|Embestida:\nAturdimiento: {duration} s.|Dash:\nStun: {duration} s.
popow-sweep|Cada {every} ataques:\nGolpea 2 casillas laterales más.\nAturdimiento: {duration} s.|Every {every} attacks:\nHits 2 extra side tiles.\nStun: {duration} s.
popow-dash-charge|Siguiente golpe tras embestir:\nDaño +{damage} puntos por enemigo alcanzado.|Next hit after dashing:\nDamage +{damage} points per enemy hit.
sepora-storm|Súper: {duration} s, {targets} objetivos.\nDaño por rayo: {hitDamage}.\nVelocidad de ataque +{speedPercent} %.\nNo obtiene energía.|Super: {duration} s, {targets} targets.\nDamage per bolt: {hitDamage}.\nAttack speed +{speedPercent} %.\nNo energy gained.
sepora-stealth|Con menos del {threshold} % de vida:\nInvisibilidad para ella y aliados cercanos: {duration} s.|Below {threshold} % health:\nInvisibility for herself and nearby allies: {duration} s.
sepora-multishot|Objetivos totales:\nAtaque básico: {targets}.\nSúper: {superTargets}.|Total targets:\nBasic attack: {targets}.\nSuper: {superTargets}.
sepora-kill-power|Daño +{damage} punto por baja.\nMáximo: {maxPerRound} aumentos por ronda.\nSe conserva entre rondas.|Damage +{damage} point per elimination.\nMaximum: {maxPerRound} gains per round.\nCarries over between rounds.
tauris-execute|Su mordisco elimina al objetivo.|His bite eliminates the target.
tauris-mark|Marca enemigos hasta {range} casillas delante al iniciar.\nEnergía +{energy} por marcado.\nNo pueden recolocarse en la siguiente preparación.|Marks enemies up to {range} tiles ahead at the start.\nEnergy +{energy} per marked enemy.\nThey cannot be moved in the next preparation.
tauris-sweep|Mordisco:\nGolpea 2 casillas laterales más.|Bite:\nHits 2 extra side tiles.
tauris-hunger|Por baja, {unit}: +{healthPerKill} vida máxima y cura; +{damagePercent} % daño esta ronda.\nSúper: quita {healthPercent} % de vida actual.\nLa vida máxima ganada dura toda la partida.|Per elimination, {unit}: +{healthPerKill} max health and healing; +{damagePercent} % damage this round.\nSuper: removes {healthPercent} % current health.\nGained max health lasts the whole match.
atori-hunt|Cacería: ataca al rival con menos vida.|Hunt: attacks the enemy with the least health.
atori-hard-head|Al comenzar la ronda:\nDaño recibido: −{reduction} %.\nDuración: {duration} s.|At round start:\nDamage taken: −{reduction} %.\nDuration: {duration} s.
atori-hunt-speed|Por baja:\nVelocidad de ataque y movimiento: +{speedPercent} %.|Per elimination:\nAttack and movement speed: +{speedPercent} %.
atori-kill-heal|Por baja:\nVida +{heal}.|Per elimination:\nHealth +{heal}.
blotan-income|Moonkens +{coins}.\nCada {interval} s.|Moonkens +{coins}.\nEvery {interval} s.
blotan-fast-income|Generación de Moonkens:\nIntervalo: {interval} s.|Moonken generation:\nInterval: {interval} s.
blotan-health|Al generar Moonkens:\nInvisibilidad: {duration} s.\nTeletransporte a una casilla libre aleatoria: hasta {radiusX} horizontal y {radiusY} vertical.|When generating Moonkens:\nInvisibility: {duration} s.\nTeleports to a random empty tile: up to {radiusX} horizontally and {radiusY} vertically.
blotan-steal|Si sobrevive la ronda:\nRoba {coins} Moonkens al rival.|If he survives the round:\nSteals {coins} Moonkens from the opponent.
bugui-rhythm|Por ataque:\nVelocidad de ataque +{speedPercent} %.\nMáximo: {stacks} acumulaciones.|Per attack:\nAttack speed +{speedPercent} %.\nMaximum: {stacks} stacks.
bugui-unlimited-range|Alcance: sus ataques llegan a todo el tablero.|Range: attacks reach the entire board.
bugui-unlimited-stacks|Por ataque:\nVelocidad de ataque +{speedPercent} %.\nSin límite de acumulaciones.|Per attack:\nAttack speed +{speedPercent} %.\nUnlimited stacks.
bugui-kill-power|Por baja:\nDaño +{damage} punto esta ronda.|Per elimination:\nDamage +{damage} point this round.
flo-flower|Invoca una flor.\nVida: {health}.\nAl caer, cura aliados cercanos: +{heal}.|Summons a flower.\nHealth: {health}.\nOn defeat, heals nearby allies: +{heal}.
flo-flower-heal|Al caer la flor:\nCura: {heal}.|When the flower falls:\nHealing: {heal}.
flo-full-energy|Energía inicial: 100 %.|Starting energy: 100 %.
flo-flower-invulnerability|La flor tiene {health} de vida.\nINVULNERABILIDAD: {duration} s.|The flower has {health} health.\nINVULNERABILITY: {duration} s.
gochan-seal-burst|Área: 3×3 casillas.\nDaño: {hitDamage}.|Area: 3×3 tiles.\nDamage: {hitDamage}.
gochan-anti-heal|Sus ataques reducen la cura rival.\nCura recibida: −{reduction} %.\nDuración: {duration} s.|Attacks reduce enemy healing.\nHealing received: −{reduction} %.\nDuration: {duration} s.
gochan-burn|El súper aplica quemadura.\nDaño +{damage}/s.\nDuración: {duration} s. Se acumula.|Super applies burn.\nDamage +{damage}/s.\nDuration: {duration} s. Stacks.
gochan-expanded-seal|Ataque básico:\nÁrea: 3×3 casillas.|Basic attack:\nArea: 3×3 tiles.
jazar-radiation|Al golpear al enemigo:\nVelocidad de ataque: −{reduction} %.|On hitting an enemy:\nAttack speed: −{reduction} %.
jazar-cross|Ataque en cruz: golpea las 4 casillas junto al objetivo.|Cross attack: hits the 4 tiles next to the target.
jazar-intense-radiation|Radiación, velocidad de ataque rival:\nReducción: {reduction} %.|Radiation, enemy attack speed:\nReduction: {reduction} %.
jazar-front-row|Al inicio, contamina la primera fila rival: {duration} s.\nLos afectados quedan ralentizados toda la ronda.|At the start, contaminates the enemy front row: {duration} s.\nAffected enemies stay slowed for the round.
kayon-summon|{unit} invoca {count} Dummy para combatir.|{unit} summons {count} Dummy to fight.
kayon-double-summon|Dummys por súper: {count}.|Dummies per Super: {count}.
kayon-dummy-legacy|Al caer un Dummy, aliados cercanos:\nDaño +{damage}.|When a Dummy falls, nearby allies:\nDamage +{damage}.
kayon-dummy-health|Dummys:\nVida +{health}.|Dummies:\nHealth +{health}.
stein-stun|Objetivos: {targets}.\nDaño por rayo: {hitDamage}.\nAturdimiento: {duration} s.|Targets: {targets}.\nDamage per bolt: {hitDamage}.\nStun: {duration} s.
stein-long-stun|Rayos del súper:\nAturdimiento: +{extraDuration} s.|Super bolts:\nStun: +{extraDuration} s.
stein-energy-gift|Al inicio, {unit} y los aliados a sus lados:\nEnergía +{energy}.|At the start, {unit} and allies beside him:\nEnergy +{energy}.
stein-triple|Objetivos del súper: {targets}.|Super targets: {targets}.
trimol-boulder|Roca cada {every} ataques.\nDaño: {hitDamage}.\nAturdimiento: {duration} s.|Boulder every {every} attacks.\nDamage: {hitDamage}.\nStun: {duration} s.
trimol-opening-boulder|Roca inicial: lanza una roca al empezar la ronda.|Opening boulder: throws a boulder at round start.
trimol-infinite-boulder|Alcance de la roca: todo el tablero.|Boulder range: the entire board.
trimol-brutal-boulder|Daño de la roca: {hitDamage}.\nAturdimiento: {duration} s.|Boulder damage: {hitDamage}.\nStun: {duration} s.
tsu-root|Cada {every} ataques:\nDaño: {hitDamage}.\nInmovilización: {duration} s.|Every {every} attacks:\nDamage: {hitDamage}.\nImmobilization: {duration} s.
tsu-death-trap|Chicle Pegajoso:\nInmovilización: +{extraDuration} s.|Sticky Gum:\nImmobilization duration: +{extraDuration} s.
tsu-chocolate|Durante la inmovilización:\nDaño creciente: +{damage} puntos cada segundo.|While immobilized:\nIncreasing damage: +{damage} points each second.
tsu-colored-gum|Alterna regalos a un aliado:\nVelocidad: +{bonusPercent} %.\nDaño recibido: −{shieldPercent} %.\nDaño +{bonusPercent} %.|Alternates gifts to one ally:\nSpeed: +{bonusPercent} %.\nDamage taken: −{shieldPercent} %.\nDamage +{bonusPercent} %.
atong-critical|Crítico cada {every} golpes.\nDaño crítico: {hitDamage}.|Critical hit every {every} hits.\nCritical damage: {hitDamage}.
atong-critical-damage|Daño crítico: {hitDamage}.|Critical damage: {hitDamage}.
atong-always-critical|Críticos: todos sus ataques son críticos.|Criticals: every attack is a critical hit.
atong-control-immunity|Al comenzar la ronda:\nInmunidad al control: {duration} s.|At round start:\nControl immunity: {duration} s.
tokoro-headbutt|Daño: {hitDamage}.\nAturde a ambos: {duration} s.|Damage: {hitDamage}.\nStuns both fighters: {duration} s.
tokoro-stun-heal|Mientras está aturdido:\nVida +{heal}/s.|While stunned:\nHealth +{heal}/s.
tokoro-ice-cream|Al recibir aturdimiento:\nAliados: +{heal} de vida.\nRivales: +{weaknessPercent} % de daño recibido durante {duration} s.|When stunned:\nAllies: +{heal} health.\nEnemies: +{weaknessPercent} % damage taken for {duration} s.
tokoro-sleep|Descansa al inicio: {duration} s.\nVida +{heal}/s.\nNo ataca ni obtiene energía.|Rests at the start: {duration} s.\nHealth +{heal}/s.\nCannot attack or gain energy.
aky-energy-drain|Por golpe:\nRoba {energy} de energía al objetivo.|Per hit:\nSteals {energy} energy from the target.
aky-opening-flight|Al inicio, vuela al fondo de su columna.\nDuración: {duration} s. No puede ser atacado durante el vuelo.|At the start, flies to the back of its column.\nDuration: {duration} s. Cannot be targeted during flight.
aky-drain-heal|Vida +{healPercent} % de la energía robada.|Health +{healPercent} % of energy stolen.
aky-sweep|Sus ataques golpean 2 casillas laterales más.|Attacks hit 2 extra side tiles.
hymay-pull|Atrae al objetivo.\nDaño: {hitDamage}.\nAturde durante el tirón y {postArrivalStun} s después.|Pulls the target.\nDamage: {hitDamage}.\nStuns during the pull and {postArrivalStun} s afterward.
hymay-focus|Aliados junto a {unit}, área {width}×{height}:\nAtacan al enemigo atraído.|Allies near {unit}, {width}×{height} area:\nAttack the pulled enemy.
hymay-weakness|Enemigo atraído:\nDaño recibido: +{bonusPercent} %.|Pulled enemy:\nDamage taken: +{bonusPercent} %.
hymay-backstep|Tras atraer, retrocede {distance} casilla.\nInvulnerabilidad durante el salto: {travelDuration} s.|After pulling, jumps back {distance} tile.\nInvulnerability during the jump: {travelDuration} s.";
        static readonly Dictionary<string,string[]> Summaries=Rows.Split('\n').Select(row=>row.Trim().Split('|'))
            .ToDictionary(row=>row[0],row=>row,StringComparer.Ordinal);

        public static bool HasSummary(string effectId)=>effectId=="anuik-revive" || effectId!=null&&(Summaries.ContainsKey(effectId)||AbilityUiCatalog.HasDescription(effectId));

        public static string Describe(TrickDefinition ability,UnitDefinition owner,bool spanish)
        {
            if(ability==null)return string.Empty;
            if(AbilityBalance.IsRetiredArmorEffect(ability.EffectId??ability.Id))return spanish?"Efecto retirado.":"Retired effect.";
            if(AbilityUiCatalog.TryDescription(ability,owner,spanish,out var authored))return authored;
            string id=ability.EffectId??ability.Id??string.Empty;
            if(id=="anuik-revive")
            {
                int count=owner==null?1:Mathf.Max(0,owner.RevivesPerCombat);
                if(count==0)return spanish?"No revive.":"Does not revive.";
                float health=owner==null?50:Mathf.Clamp(owner.ReviveHealthFraction,.01f,1)*100;
                return (spanish?"Resurrecciones: ":"Revivals: ")+count+".\n"+
                    (spanish?"Vida al revivir: ":"Revival health: ")+Number(health)+" %.";
            }
            if(!Summaries.TryGetValue(id,out var row))return AbilityBalance.Description(ability,owner);
            string template=row[spanish?1:2].Replace("\\n","\n");
            template=template.Replace("{hitDamage}",HitDamage(ability,spanish))
                .Replace("{bouncePercent}",Number(Value(ability,"bounceDamageMultiplier",.5f)*100));
            if(id=="kazuru-rising-rings"||id=="pokrak-bubbles")
            {
                var cells=CombatSimulation.PokrakBubbleCells(Vector2Int.zero,Mathf.RoundToInt(Value(ability,"width",3)));
                template=template.Replace("{areaWidth}",(cells.Max(c=>c.x)-cells.Min(c=>c.x)+1).ToString(CultureInfo.InvariantCulture));
            }
            return AbilityBalance.FormatDescription(ability,owner,template,spanish);
        }

        static float Value(TrickDefinition ability,string key,float fallback)=>AbilityBalance.Value(ability,key,fallback);
        static string Number(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        static string HitDamage(TrickDefinition ability,bool spanish)
        {
            float damage=AbilityBalance.ResolvedFixedDamage(ability);
            if(damage>=0)return Mathf.RoundToInt(damage).ToString(CultureInfo.InvariantCulture);
            float percent=ability.EffectId=="atong-critical"||ability.EffectId=="atong-critical-damage"
                ?Value(ability,"damagePercent",100):Value(ability,"multiplier",1)*100;
            return Number(percent)+(spanish?" % del ataque":" % of attack");
        }
    }
}

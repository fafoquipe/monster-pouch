using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Local
{
    /// <summary>
    /// Player-facing Spanish and English copy. Gameplay keeps stable IDs
    /// without scattering translated strings through combat and economy code.
    /// </summary>
    public static partial class GameText
    {
        public static string Ready => Spanish ? "LISTO" : "READY";
        public static string Battle => Spanish ? "BATALLA" : "BATTLE";
        public static string Results => Spanish ? "RESULTADO" : "RESULTS";
        public static string TimeoutTitle => Spanish ? "¡TIEMPOOO!" : "TIMEEE!!";
        public static string Tiebreaker => Spanish ? "DESEMPATE" : "TIEBREAKER";
        public static string Preparation => Spanish ? "PREPARACIÓN" : "PREPARATION";
        public static string Figures => Spanish ? "FIGURAS" : "FIGURES";
        public static string Guide => Spanish ? "GUÍA" : "GUIDE";
        public static string Sound => Spanish ? "SONIDO" : "SOUND";
        public static string Taunt => Spanish ? "PROVOCADO" : "TAUNT";
        public static string Hidden => Spanish ? "OCULTO" : "HIDDEN";
        public static string Rooted => Spanish ? "INMOVILIZACIÓN" : "IMMOBILIZATION";
        public static string Boulder => Spanish ? "ROCA" : "BOULDER";
        public static string Super => Spanish ? "SÚPER" : "SUPER";
        public static string Stunned => Spanish ? "ATURDIMIENTO" : "STUN";
        public static string Shield => Spanish ? "ESCUDO" : "SHD";
        public static string Settings => Spanish ? "AJUSTES" : "SETTINGS";
        public static string Close => Spanish ? "CERRAR" : "CLOSE";
        public static string Back => Spanish ? "VOLVER" : "BACK";

        static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MAPA"] = "MAP",
            ["MAZO"] = "BRIEF",
            ["ESTILOS"] = "STYLES",
            ["TIENDA"] = "SHOP",
            ["DESAFÍOS"] = "CHALLENGES",
            ["PARQUE DE BOLAS"] = "BALL PIT",
            ["CAMPO REAL"] = "ROYAL COURT",
            ["Practica a tu ritmo"] = "Practice at your own pace",
            ["SALA"] = "ROOM",
            ["Juega con un amigo"] = "Play with a friend",
            ["PRÓXIMAMENTE"] = "COMING SOON",
            ["Nuevas sorpresas para tu colección"] = "New surprises for your collection",
            ["Torneos y retos entre amigos"] = "Tournaments and challenges with friends",
            ["SALA DE AMIGOS"] = "FRIENDS ROOM",
            ["CREAR SALA"] = "CREATE ROOM",
            ["Comparte tu código"] = "Share your code",
            ["UNIRSE A SALA"] = "JOIN ROOM",
            ["Entra con un código"] = "Enter a room code",
            ["UNIRSE A UNA SALA"] = "JOIN A ROOM",
            ["INTRODUCE EL CÓDIGO"] = "ENTER ROOM CODE",
            ["PEGAR"] = "PASTE",
            ["COPIAR"] = "COPY",
            ["UNIRSE"] = "JOIN",
            ["CÓDIGO DE LA SALA"] = "ROOM CODE",
            ["TÚ"] = "YOU",
            ["RIVAL"] = "OPPONENT",
            ["Jugador"] = "Player",
            ["ESPERANDO"] = "WAITING",
            ["ABANDONAR"] = "LEAVE",
            ["INICIAR"] = "START",
            ["Conectando…"] = "Connecting…",
            ["Sala creada. Comparte el código con tu amigo."] = "Room created. Share the code with your friend.",
            ["Entrando a la sala…"] = "Joining the room…",
            ["Tu rival está aquí. Pulsa Iniciar cuando estén preparados."] = "Your opponent is here. Press Start when you are ready.",
            ["Esperando a que el anfitrión inicie la partida."] = "Waiting for the host to start the match.",
            ["Conectado. Prepara tu equipo."] = "Connected. Prepare your team.",
            ["Crea una sala o escribe el código de tu amigo."] = "Create a room or enter your friend's code.",
            ["Elige una mejora"] = "Choose an upgrade",
            ["Toca MEJORAR para comprar"] = "Tap UPGRADE to buy",
            ["MEJORAR"] = "UPGRADE",
            ["Desempate por tiempo: vida del Monster y 50 % de Whelps vivos"] = "Time limit: Monster HP + 50% of living Whelp HP",
            ["Ronda anulada: empate de vida y prioridad del tablero"] = "Round void: tied health and board priority",
            ["Muerte simultánea: ventaja de casilla, cuadrante o territorio"] = "Simultaneous death: cell, quadrant or territory advantage",
            ["Ronda anulada: muerte simultánea sin ventaja"] = "Round void: simultaneous death with no advantage",
            ["Ronda anulada: eliminación simultánea sin ventaja única"] = "Round void: simultaneous elimination with no unique advantage",
            ["PREPARACIÓN"] = "PREPARATION",
            ["COMBATE"] = "BATTLE",
            ["RESULTADO"] = "RESULTS",
            ["LISTO"] = "READY",
            ["FIGURAS"] = "FIGURES",
            
            ["GUÍA"] = "GUIDE",
            ["SONIDO"] = "SOUND",
            ["AJUSTES"] = "SETTINGS",
            ["CERRAR"] = "CLOSE",
            ["VOLVER"] = "BACK",
            ["MENÚ"] = "MENU",
            ["PAUSA"] = "PAUSED",
            ["CONTINUAR"] = "CONTINUE",
            ["SILENCIO"] = "MUTE",
            ["Cómo jugar"] = "How to play",
            ["¡VICTORIA!"] = "VICTORY!",
            ["FIN DE PARTIDA"] = "MATCH OVER",
            ["RONDA COMPLETADA"] = "ROUND COMPLETE",
            ["REVANCHA"] = "REMATCH",
            ["SIGUIENTE RONDA"] = "NEXT ROUND",
            ["ELIGE TU MONSTER"] = "CHOOSE YOUR MONSTER",
            ["ELEGIDO"] = "SELECTED",
            ["ELEGIR"] = "SELECT",
            ["EN EQUIPO"] = "IN TEAM",
            ["AÑADIR"] = "ADD",
            ["JUGAR CONTRA BOT"] = "BATTLE THE BOT",
            ["EDITAR BRIEF"] = "EDIT BRIEF",
            ["BATALLA"] = "BATTLE!",
            ["SELECCIONAR MONSTER"] = "SELECT MONSTER",
            ["SELECCIONAR WHELP"] = "SELECT WHELP",
            ["COLECCIÓN"] = "COLLECTION",
            ["ELIGE UNA FIGURA PARA TU BRIEF"] = "CHOOSE A FIGURE FOR YOUR BRIEF",
            ["QUITAR DEL BRIEF"] = "REMOVE FROM BRIEF",
            ["NOMBRE DEL JUGADOR"] = "PLAYER NAME",
            ["GUARDAR"] = "SAVE",
            ["VOLVER A FIGURAS"] = "BACK TO FIGURES",
            ["Invocación"] = "Summon",
            ["MEJORA"] = "UPGRADE",
            ["SÚPER"] = "SUPER",
            ["ATAQUE"] = "ATTACK",
            ["PASIVA"] = "PASSIVE",
            ["Explora sus habilidades"] = "Explore this figure's abilities",
            ["Habilidades del rival"] = "Opponent abilities",
            ["Este héroe ya eligió su mejora"] = "This hero already chose an upgrade",
            ["MEJORA ACTIVA"] = "UPGRADE ACTIVE",
            ["Disponible durante preparación"] = "Available during preparation",
            ["1 mejora por héroe · Doble toque para comprar"] = "1 upgrade per hero · Double-tap to buy",
            ["Doble toque para comprar una mejora"] = "Double-tap to buy an upgrade",
            ["Necesitas una copia en el Brief"] = "You need a copy in the Brief",
            ["Mejora adquirida"] = "Upgrade purchased",
            ["INVOCADO POR KAYON\n\nEste Dummy lucha durante esta ronda y desaparece al terminar."] = "SUMMONED BY KAYON\n\nThis Dummy fights for this round, then disappears.",
            ["TU PRIMERA PARTIDA"] = "YOUR FIRST MATCH",
            ["El Brief se abre con tres Whelps disponibles. Arrastra uno al campo para comprarlo y colocarlo. También puedes tocarlo y luego tocar su casilla.\n\nUna copia de un Whelp propio compra su siguiente Trick. Mantén una figurita para inspeccionarla.\n\nGuarda hasta tres Whelps en el banco. Al guardar uno del campo recuperas su inversión y conserva sus Tricks. Reservar un Whelp nuevo desde el Brief es gratis. Toca el banco para ver el coste de despliegue; arrástralo al campo para pagarlo. Los tipos guardados no aparecen en la rotación del Brief. Para vender una tropa, arrástrala al Brief grande. Las del campo devuelven su inversión; las del banco no devuelven monedas adicionales.\n\nEl reroll cierra la caja y revela nuevas copias. Pulsa LISTO o espera 40 segundos para combatir. Si cae el Monster, los Whelps siguen luchando hasta que un equipo quede sin unidades en pie. Gana tres rondas."] = "The Brief opens with three available Whelps. Drag one onto the board to buy and place it, or tap it and then tap a tile.\n\nA copy of one of your Whelps buys its next Trick. Hold a figure to inspect it.\n\nStore up to three Whelps in the bank. Storing a field Whelp refunds its investment and keeps its Tricks. Reserving a new Whelp from the Brief is free. Tap the bank to see each deployment cost; drag a Whelp onto the board to pay it. Banked types stay out of the Brief rotation. To sell a troop, drag it into the large Brief. Field troops refund their investment; banked troops give no additional refund.\n\nReroll closes the case and reveals new copies. Press READY or wait 40 seconds to fight. Losing your Monster does not end the round: your Whelps keep fighting until a team has no units left. Win three rounds.",
            ["ENTENDIDO"] = "GOT IT",
            ["VOLVER AL MENÚ"] = "BACK TO MENU",
            ["Tus Whelps elegidos aparecerán en el Brief."] = "Your selected Whelps will appear in the Brief.",
            ["Toca para añadir o retirar del equipo"] = "Tap to add or remove from your team",
            ["Copia adquirida. Selecciona el Whelp del Brief y toca una casilla."] = "Copy purchased. Select the Whelp in the Brief, then tap a tile.",
            ["Copia adquirida; las mejoras conservan la posición de tu Whelp."] = "Copy purchased. Upgrades keep your Whelp in place.",
            ["Nuevas ofertas"] = "New offers",
            ["Mejora activa en el próximo combate; posición fija desde la siguiente ronda."] = "Upgrade activates next battle; position locks next round.",
            ["Habilidad activada para el próximo combate."] = "Ability activates next battle.",
            ["Trick elegido para la próxima copia."] = "Trick selected for the next copy.",
            ["Mejora comprada."] = "Upgrade purchased.",
            ["Whelp en el bench"] = "Whelp stored on the bench",
            ["Whelp en el Brief"] = "Whelp stored in the Brief",
            ["Venta completada."] = "Sold.",
            ["La casilla no está disponible."] = "That tile is unavailable.",
            ["La casilla está ocupada."] = "That tile is occupied.",
            ["No se puede ocupar esa casilla."] = "That tile cannot be occupied.",
            ["Elige una casilla de tu campo o el banco."] = "Choose a tile on your side or the bench.",
            ["Elige una casilla iluminada, el bench o el Brief."] = "Choose a highlighted tile, the bench, or the Brief.",
            ["Espera a que se abra el Brief."] = "Wait for the Brief to open.",
            ["No se cargó la configuración de la partida."] = "The match configuration could not be loaded.",
            ["Elige un Monster válido."] = "Choose a valid Monster.",
            ["Esa oferta ya no está disponible."] = "That offer is no longer available.",
            ["Ese Trick no está pendiente."] = "That Trick is not pending.",
            ["Arrastra la oferta al campo o al bench."] = "Drag the offer onto the board or the bench.",
            ["Este Whelp ya tiene sus tres Tricks."] = "This Whelp already has all three Tricks.",
            ["No tienes suficientes Moonkens."] = "Not enough Moonkens.",
            ["No tienes suficientes Moon Tokens."] = "Not enough Moonkens.",
            ["No tienes suficientes Moon-Ken."] = "Not enough Moonkens.",
            ["La Pouch ya cambió."] = "The Pouch has already changed.",
            ["No quedan rerolls."] = "No rerolls left.",
            ["Libera un hueco del Brief para recibir ofertas."] = "Free a Brief slot to receive offers.",
            ["Ya adquiriste todas las copias."] = "You already own every available copy.",
            ["No hay Whelps elegibles para esta rotación."] = "No eligible Whelps remain in this rotation.",
            ["Selecciona un Whelp propio."] = "Select one of your Whelps.",
            ["Necesitas una copia de este Whelp en la Pouch."] = "You need a copy of this Whelp in the Pouch.",
            ["Mejora ya adquirida o no disponible."] = "Upgrade already purchased or unavailable.",
            ["Tu héroe ya eligió su única mejora para esta partida."] = "This hero already chose its one upgrade for this match.",
            ["Reanuda la partida para comprar Tricks."] = "Resume the match to buy Tricks.",
            ["Las compras no están disponibles en este entrenamiento."] = "Purchases are unavailable in this training lesson.",
            ["La preparación está bloqueada."] = "Preparation is locked.",
            ["Solo puedes vender tus Whelps."] = "You can only sell your Whelps.",
            ["Primero desbloquea este Whelp."] = "Unlock this Whelp first.",
            ["Destino inválido."] = "Invalid destination.",
            ["Tu Monster permanece en el campo."] = "Your Monster must stay on the board.",
            ["El Brief no tiene un hueco libre."] = "The Brief has no free slot.",
            ["Elige una casilla de despliegue válida."] = "Choose a valid deployment tile.",
            ["El banco está lleno (3/3)."] = "The bank is full (3/3).",
            ["Esta acción solo está disponible durante la preparación."] = "This action is only available during preparation.",
            ["La mejora del Monster fija su posición desde esta ronda."] = "The Monster upgrade locks its position from this round onward.",
            ["El campo admite cinco Whelps distintos."] = "The board supports five different Whelps.",
            ["Cada Whelp necesita tres Tricks."] = "Every Whelp needs three Tricks.",
            ["Falta un Trick del Whelp."] = "A Whelp Trick is missing.",
            ["La máscara de despliegue del Monster está vacía."] = "The Monster deployment mask is empty.",
            ["El catálogo está vacío."] = "The collection is empty.",
            ["El catálogo contiene una definición vacía o un ID repetido."] = "The collection contains an empty definition or duplicate ID.",
            ["Selecciona únicamente Whelps disponibles en la colección."] = "Select only Whelps available in the collection.",
            ["Elige al menos un Whelp para tu equipo."] = "Choose at least one Whelp for your team.",
            ["Sin oferta"] = "No offer",
            ["No disponible"] = "Unavailable",
            ["Tricks completos"] = "All Tricks unlocked",
            ["El equipo rival perdió todas sus unidades"] = "The opposing team lost all its units",
            ["Tu equipo perdió todas sus unidades"] = "Your team lost all its units",
            ["Sin progreso durante 10 segundos"] = "No progress for 10 seconds",
            ["Empate"] = "Draw",
            ["¡Ronda ganada!"] = "Round won!",
            ["Ronda del rival"] = "Opponent won the round",
            ["Panzazo"] = "Belly Bash",
            ["Panza de acero"] = "Steel Belly",
            ["Derechazo"] = "Right Hook",
            ["Puños veloces"] = "Quick Fists",
            ["Acolchado"] = "Padding",
            ["Resistente"] = "Sturdy",
            ["Refuerzo"] = "Reinforced",
            ["Remiendo"] = "Patch Up",
            ["Chispa"] = "Spark",
            ["Brillo"] = "Bright Spark",
            ["Chispa larga"] = "Long Spark",
            ["Invocado"] = "Summoned",
            ["¡CRÍTICO!"] = "CRITICAL!"
        };

        static readonly Dictionary<string, string> AbilityNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["martio-orbits"]="Planetary System", ["martio-explosion"]="Shock Wave", ["martio-piercing"]="Piercing", ["martio-moons"]="Planetary Fan",
            ["anuik-revive"]="Back on His Feet", ["anuik-second-revive"]="One More Time", ["anuik-revive-impact"]="Return Impact", ["anuik-bounce"]="Ricochet Hearts",
            ["bugaloo-reflect"]="Moon Bounce", ["bugaloo-protection"]="Protective Eclipse", ["bugaloo-revenge"]="Vengeful Moon", ["bugaloo-healing"]="Restoring Light",
            ["popow-dash"]="Unstoppable Rush", ["popow-dash-stun"]="Crushing Impact", ["popow-sweep"]="Sweeping Hook", ["popow-dash-charge"]="Impact Charge",
            ["sepora-storm"]="Lightning Storm", ["sepora-stealth"]="Storm Veil", ["sepora-multishot"]="Multi Discharge", ["sepora-kill-power"]="Gathered Power",
            ["tauris-execute"]="Lethal Bite", ["tauris-mark"]="Marked Prey", ["tauris-sweep"]="Sweeping Jaw", ["tauris-hunger"]="Insatiable Hunger",
            ["atori-hunt"]="Hunt the Weak", ["atori-hard-head"]="Hard Head", ["atori-hunt-speed"]="Relentless Hunt", ["atori-kill-heal"]="Hunter's Feast",
            ["blotan-income"]="Moonkens Forge", ["blotan-fast-income"]="Fast Forge", ["blotan-health"]="Escape Trick", ["blotan-steal"]="Winner's Share",
            ["bugui-rhythm"]="Battle Rhythm", ["bugui-unlimited-range"]="No Boundaries", ["bugui-unlimited-stacks"]="Endless Rhythm", ["bugui-kill-power"]="Power Hunger",
            ["suki-fish-shield"]="Protective Catch", ["suki-shield-energy"]="Energizing Catch", ["suki-opening-shield"]="First Catch", ["suki-extra-shield"]="Bountiful Catch",
            ["kazuru-rising-rings"]="Solar Circle", ["kazuru-ring-throw"]="Flying Rings", ["kazuru-last-halo"]="Last Halo", ["kazuru-renewal"]="Solar Renewal",
            ["pokrak-bubbles"]="Protective Bubbles", ["pokrak-long-bubbles"]="Lasting Bubbles", ["pokrak-wide-bubbles"]="Protective Tide", ["pokrak-strong-bubbles"]="Reinforced Bubbles",
            ["rosie-healing-rose"]="Rose of Life", ["rosie-haste"]="Swift Petals", ["rosie-shield"]="Thorn Embrace", ["rosie-control-immunity"]="Free Petals",
            ["flo-flower"]="Healing Bloom", ["flo-flower-heal"]="Full Bloom", ["flo-full-energy"]="Ready to Bloom", ["flo-flower-invulnerability"]="Protected Sprout",
            ["gochan-seal-burst"]="Seal Burst", ["gochan-anti-heal"]="Withering Seal", ["gochan-burn"]="Burning Mark", ["gochan-expanded-seal"]="Expanded Seal",
            ["jazar-radiation"]="Contamination", ["jazar-cross"]="Radioactive Wave", ["jazar-intense-radiation"]="Intense Radiation", ["jazar-front-row"]="Contaminated Zone",
            ["kayon-summon"]="Dummy Call", ["kayon-double-summon"]="Double Call", ["kayon-dummy-legacy"]="Dummy Legacy", ["kayon-dummy-health"]="Reinforced Dummy",
            ["stein-stun"]="Lightning Storm", ["stein-long-stun"]="Longer Stun", ["stein-energy-gift"]="Energy Transfer", ["stein-triple"]="Fourth Bolt",
            ["trimol-boulder"]="Wild Boulder", ["trimol-opening-boulder"]="Opening Throw", ["trimol-infinite-boulder"]="Unstoppable Rock", ["trimol-brutal-boulder"]="Brutal Boulder",
            ["tsu-root"]="Sticky Gum", ["tsu-death-trap"]="Long-Lasting Gum", ["tsu-chocolate"]="Explosive Chocolates", ["tsu-colored-gum"]="Colorful Gum",
            ["atong-critical"]="Calculated Strike", ["atong-critical-damage"]="Devastating Critical", ["atong-always-critical"]="Perfect Strike", ["atong-control-immunity"]="Unbreakable Will",
            ["tokoro-headbutt"]="Sleepy Headbutt", ["tokoro-stun-heal"]="Restful Nap", ["tokoro-ice-cream"]="Pop-up Ice Cream", ["tokoro-sleep"]="Bedtime",
            ["aky-energy-drain"]="Energy Drain", ["aky-opening-flight"]="Low Flight", ["aky-drain-heal"]="Vital Absorption", ["aky-sweep"]="Wide Strike",
            ["hymay-pull"]="Whip Pull", ["hymay-focus"]="Marked Target", ["hymay-weakness"]="Vulnerable Prey", ["hymay-backstep"]="Acrobatic Pull"
        };

        static readonly Dictionary<string, string> AbilityDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rosie-healing-rose"]="A rose {width} tiles wide heals {healPercent}% maximum health, including {unit}, and flies straight off the board. Aims at the nearest ally from the back row; farthest from other rows.",
            ["rosie-haste"]="The rose grants +{speedPercent}% attack speed for {duration}s, including {unit}.",
            ["suki-fish-shield"]="Throws fish to the {targets} nearest allies. Each ally and {unit} gain {shield} shield for {duration}s. Absorbs damage before health.",
            ["suki-shield-energy"]="+{energyPerShieldDamage} energy per point of damage absorbed by shields given to allies. Her own shield does not trigger this bonus.",
            ["suki-opening-shield"]="Casts her ability for free at the start of the round.",
            ["suki-extra-shield"]="+{shieldBonus} shield to her ability, including {unit}.",
            ["kazuru-rising-rings"]="Grants +{damage} damage for {duration}s to allies in a {width}×{width} area, including {unit}. Added before critical multipliers.",
            ["kazuru-ring-throw"]="+{range} range. Each attack throws 2 rings in a burst; the second deals {secondRingPercent}% of the first ring's damage, then the normal attack cadence resumes.",
            ["kazuru-last-halo"]="Once per battle, survives a lethal hit with 1 health and blocks all damage for {duration}s.",
            ["kazuru-renewal"]="Each Super restores {heal} health per second for {duration}s.",
            ["pokrak-bubbles"]="Bubbles in a {width}×{width} area, including {unit}: reduce damage taken by {reduction}% for {duration}s.",
            ["pokrak-long-bubbles"]="Bubbles last {duration}s.",
            ["pokrak-wide-bubbles"]="Adds {extension} tile at the center of each side of the area.",
            ["pokrak-strong-bubbles"]="Bubbles reduce damage taken by {reduction}%.",
            ["rosie-shield"]="The rose grants a shield worth {shieldHealthPercent}% maximum health, including {unit}.",
            ["rosie-control-immunity"]="Removes and blocks stuns, slows and all other control effects for {duration}s, including {unit}.",
            ["anuik-second-revive"]="+1 revival with {healthPercent}% health.",
            ["anuik-revive-impact"]="On revival: {damage} additional area damage, knockback and a {stunDuration}s stun.",
            ["anuik-bounce"]="+1 bounce to another enemy within {bounceRange} tiles. Bounce damage: ×{bounceDamageMultiplier} of the hit.",
            ["bugaloo-reflect"]="Taunts nearby enemies for {tauntDuration}s. Reflects damage for {duration}s.",
            ["bugaloo-protection"]="−{reduction}% damage taken during his Super.",
            ["bugaloo-revenge"]="Reflects ×{reflectPercentFactor} incoming damage during his Super.",
            ["bugaloo-healing"]="+{bonusPercent}% healing received.",
            ["popow-dash"]="Rushes behind the farthest enemy. Stuns enemies in his path for {duration}s.",
            ["popow-dash-stun"]="{durationDelta}s stun duration on the rush.",
            ["popow-sweep"]="Every {every} attacks, hooks +2 side tiles and stuns for {duration}s.",
            ["popow-dash-charge"]="The next hit gains +{damage} damage per enemy rushed through.",
            ["sepora-storm"]="For {duration}s: {abilityDamage} per bolt, {targets} targets and +{speedPercent}% attack speed. Gains no energy.",
            ["sepora-stealth"]="Below {threshold}% health, she and nearby allies vanish for {duration}s.",
            ["sepora-multishot"]="Additional targets: {targetsDelta} for basic attacks and {superTargetsDelta} during her Super.",
            ["sepora-kill-power"]="Each takedown grants +{damage} damage for the rest of the match.",
            ["tauris-execute"]="Eliminates the target with one bite.",
            ["tauris-mark"]="At the start, marks enemies in his row. +{energy} energy per marked enemy.",
            ["tauris-sweep"]="+2 side tiles hit by the bite.",
            ["tauris-hunger"]="The Super removes {healthPercent}% current health. Each takedown adds {healthPerKill} maximum health, heals that amount, and grants +{damagePercent}% damage. Earned health stacks across rounds of this match.",
            ["atori-hunt"]="Targets the enemy with the lowest current health.",
            ["atori-hard-head"]="−{reduction}% damage taken for the first {duration}s.",
            ["atori-hunt-speed"]="+{speedPercent}% attack and movement speed per takedown.",
            ["atori-kill-heal"]="+{heal} health per takedown.",
            ["blotan-income"]="+{coins} Moonkens every {interval}s.",
            ["blotan-fast-income"]="{intervalDelta}s between Moonkens payouts.",
            ["blotan-health"]="On each Moonkens payout, becomes invisible to troops for {duration}s and teleports to a free tile within {radiusX} columns and {radiusY} rows.",
            ["blotan-steal"]="If he survives, steals {coins} Moonkens from the opponent.",
            ["bugui-rhythm"]="Each attack grants +{speedPercent}% attack speed, up to {stacks} stacks.",
            ["bugui-unlimited-range"]="Attacks can reach the entire board.",
            ["bugui-unlimited-stacks"]="Each attack grants +{speedPercent}% attack speed with no stack limit.",
            ["bugui-kill-power"]="Each takedown grants +{damage} damage this round.",
            ["flo-flower"]="Plants a flower after {castDuration}s. It has {health} health and grants +{heal} health to nearby allies on defeat.",
            ["flo-flower-heal"]="{healDelta} additional flower healing on defeat.",
            ["flo-full-energy"]="Starts with full energy.",
            ["flo-flower-invulnerability"]="{healthDelta} flower health. Invulnerable for {duration}s.",
            ["gochan-seal-burst"]="Deals {abilityDamage} in a 3 × 3 area.",
            ["gochan-anti-heal"]="Attacks apply −{reduction}% healing received for {duration}s.",
            ["gochan-burn"]="The Super adds a burn: +{damage} damage per second for {duration}s. Stacks.",
            ["gochan-expanded-seal"]="+4 diagonal tiles to the basic attack area, completing a 3 × 3 area.",
            ["jazar-radiation"]="Attacks apply −{reduction}% attack speed to the target.",
            ["jazar-cross"]="Adds damage to the 4 adjacent tiles around the target, forming a cross.",
            ["jazar-intense-radiation"]="{reductionDelta} percentage points of attack speed reduction from Radiation.",
            ["jazar-front-row"]="At the start, contaminates the enemy front row for {duration}s. Affected enemies stay slowed for the rest of the round.",
            ["kayon-summon"]="Summons {count} Dummy to fight alongside {unit}.",
            ["kayon-double-summon"]="Additional Dummy summons per Super: {countDelta}.",
            ["kayon-dummy-legacy"]="When a Dummy falls, nearby allies gain +{damage} damage.",
            ["kayon-dummy-health"]="Dummies gain +{health} health.",
            ["stein-stun"]="Strikes {targets} different enemies with lightning. Each bolt deals {abilityDamage} and stuns for {duration}s.",
            ["stein-long-stun"]="+{extraDuration}s stun duration on Super bolts.",
            ["stein-energy-gift"]="At the start: +{energy} energy to {unit} and the allies immediately to his left and right.",
            ["stein-triple"]="Additional Super bolt targets: {targetsDelta}.",
            ["trimol-boulder"]="Every {every} attacks, rolls a boulder: {abilityDamage} and a {duration}s stun to enemies along its path.",
            ["trimol-opening-boulder"]="Rolls a boulder at the start of the round.",
            ["trimol-infinite-boulder"]="The boulder crosses the entire board.",
            ["trimol-brutal-boulder"]="Boulder damage: {abilityDamage}. {durationDelta}s stun duration.",
            ["tsu-root"]="Every {every} attacks, a hit dealing {abilityDamage} roots the target for {duration}s.",
            ["tsu-death-trap"]="+{extraDuration}s root duration for Sticky Gum.",
            ["tsu-chocolate"]="While rooted, the enemy takes increasing damage: +{damage} each second.",
            ["tsu-colored-gum"]="Cycles gifts to an ally: +{bonusPercent}% attack and movement speed, a shield that reduces damage taken by {shieldPercent}%, or +{bonusPercent}% damage.",
            ["atong-critical"]="Every {every} hits: a critical hit for {abilityDamage}.",
            ["atong-critical-damage"]="Critical hit damage: {abilityDamage}.",
            ["atong-always-critical"]="Every hit is critical.",
            ["atong-control-immunity"]="Immune to control for the first {duration}s.",
            ["tokoro-headbutt"]="Headbutt for {abilityDamage}. Stuns both fighters for {duration}s.",
            ["tokoro-stun-heal"]="While stunned: +{heal} health each second.",
            ["tokoro-ice-cream"]="While stunned, drops cones: +{heal} health to allies or +{weaknessPercent}% damage taken by enemies for {duration}s.",
            ["tokoro-sleep"]="Rests for the first {duration}s: cannot attack or gain energy and restores +{heal} health each second.",
            ["aky-energy-drain"]="Each hit steals {energy} energy from the target.",
            ["aky-opening-flight"]="At the start, flies to the end of the column in {duration}s and cannot be targeted.",
            ["aky-drain-heal"]="Heals for {healPercent}% of the energy stolen.",
            ["aky-sweep"]="+2 side tiles hit by attacks.",
            ["hymay-pull"]="Pulls the target in {pullDuration}s and deals {abilityDamage}. Stuns during the pull and for +{postArrivalStun}s after arrival.",
            ["hymay-focus"]="Allies focus their attacks on the pulled enemy.",
            ["hymay-weakness"]="+{bonusPercent}% damage taken by the pulled enemy.",
            ["hymay-backstep"]="After the pull, backflips {distance} tile in {travelDuration}s. Invulnerable while airborne."
        };

        public const string CurrencyName="Moonkens";
        static readonly Regex CurrencyAliases=new Regex(@"\b(?:moon[\s-]*tokens?|moon[\s-]*kens?|munkens?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static string NormalizeCurrencyName(string text)
        {
            if(string.IsNullOrEmpty(text))return text??string.Empty;
            return CurrencyAliases.Replace(text,match=>match.Value==match.Value.ToUpperInvariant()?CurrencyName.ToUpperInvariant():CurrencyName);
        }
        static readonly Regex SpanishBriefTerms=new Regex(@"\b(?:(del|al|el|un)\s+)?brief\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static string NormalizeBriefName(string text)
        {
            if(!Spanish||string.IsNullOrEmpty(text))return text;
            return SpanishBriefTerms.Replace(text,match=>{
                string article=match.Groups[1].Value.ToLowerInvariant();
                string replacement=article=="del"?"de la Maleta":article=="al"?"a la Maleta":
                    article=="el"?"la Maleta":article=="un"?"una Maleta":"Maleta";
                if(match.Value==match.Value.ToUpperInvariant())return replacement.ToUpperInvariant();
                if(char.IsUpper(match.Value[0]))return char.ToUpperInvariant(replacement[0])+replacement.Substring(1);
                return article.Length>0?replacement:"maleta";
            });
        }
        public static string Resolve(string text)=>NormalizeCurrencyName(NormalizeBriefName(ResolveLocalized(text)));
        static string ResolveLocalized(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if(text=="BRIEF"||text=="DECK"||text=="MAZO"||text=="MALETA")return Spanish?"MALETA":"BRIEF";
            if(text=="BATTLE"||text=="BATTLE!"||text=="BATALLA")return Battle;
            if (Spanish) return ResolveSpanish(text);
            const string markedSuffix=" marcó esta unidad: su posición está fija.";
            if(text.EndsWith(markedSuffix,StringComparison.Ordinal))
                return text.Substring(0,text.Length-markedSuffix.Length)+" marked this unit, so its position is locked.";
            if (Exact.TryGetValue(text, out string translated)) return translated;
            if (text.IndexOf('\n') >= 0)
                return string.Join("\n", text.Split('\n').Select(Resolve));
            if (text.StartsWith("TUS WHELPS  ·  ", StringComparison.Ordinal))
                return "YOUR WHELPS  ·  " + text.Substring(15).Replace(" ELEGIDOS", " SELECTED");
            if (text.StartsWith("Volumen: ", StringComparison.Ordinal)) return "Volume: " + text.Substring(9);
            if (text.StartsWith("Coste: ", StringComparison.Ordinal)) return "Cost: " + text.Substring(7).Replace("Moon Tokens", "Moonkens");
            if (text.StartsWith("VENDER · +", StringComparison.Ordinal)) return "SELL · +" + text.Substring(10).Replace(" MT", " MOONKENS");
            if (text.EndsWith(" colocado", StringComparison.Ordinal)) return text.Substring(0, text.Length - 9) + " placed";
            if (text.StartsWith("El equipo admite hasta ", StringComparison.Ordinal)) return text.Replace("El equipo admite hasta ", "Your team supports up to ").Replace(" tipos de Whelp.", " Whelp types.");
            if (text.StartsWith("VIDA  ", StringComparison.Ordinal))
                return text.Replace("VIDA", "HEALTH").Replace("DAÑO", "DAMAGE").Replace("ATAQUES", "ATTACKS").Replace("ALCANCE", "RANGE");
            return text;
        }

        public static string AbilityName(TrickDefinition ability)=>AbilityName(ability,Spanish);
        public static string AbilityName(TrickDefinition ability,bool spanish)=>NormalizeCurrencyName(LocalizedAbilityName(ability,spanish));
        static string LocalizedAbilityName(TrickDefinition ability,bool spanish)
        {
            if (ability == null) return spanish ? "Invocación" : "Summon";
            var authored=AbilityUiCatalog.Title(ability.EffectId??ability.Id,spanish);
            if(!string.IsNullOrWhiteSpace(authored))return authored;
            if (spanish) return SpanishAbilityName(ability);
            string id = ability.EffectId ?? ability.Id ?? string.Empty;
            return AbilityNames.TryGetValue(id, out string name) ? name : Resolve(ability.Name);
        }

        public static string AbilityDescription(TrickDefinition ability,UnitDefinition owner)=>AbilityDescription(ability,owner,Spanish);
        public static string AbilityDescription(TrickDefinition ability,UnitDefinition owner,bool spanish)=>NormalizeCurrencyName(AbilitySummaryDescriptions.Describe(ability,owner,spanish));
        static string LocalizedAbilityDescription(TrickDefinition ability, UnitDefinition owner,bool spanish)
        {
            if (ability == null) return string.Empty;
            if (spanish) return SpanishAbilityDescription(ability,owner);
            string id = ability.EffectId ?? ability.Id ?? string.Empty;
            if (id == "anuik-revive" && owner != null)
            {
                int revives = Math.Max(0, owner.RevivesPerCombat);
                if (revives == 0) return "Does not revive.";
                string count = revives == 1 ? "once" : revives.ToString(CultureInfo.InvariantCulture) + " times";
                string health = (Math.Min(1f, Math.Max(.01f, owner.ReviveHealthFraction)) * 100f).ToString("0.##", CultureInfo.InvariantCulture);
                return "Revives " + count + " with " + health + "% health.";
            }
            if (!AbilityDescriptions.TryGetValue(id, out string description)) return Resolve(ability.Description);
            return AbilityBalance.FormatDescription(ability,owner,description,false);
        }
    }
}

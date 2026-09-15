# Revisión de reglas de los PDF

Fuentes: Monsters.pdf (5 páginas) y whelps.pdf (14 páginas). Se inspeccionaron visualmente las 19 páginas. Catálogo: 5 Monsters y 14 Whelps. Dummy es una invocación de Kayon, no un Whelp seleccionable documentado.

## Reglas sin valores definidos

- PDFs define no global energy capacity/generation/cost/damage received gain. Energy tuning must be explicitly provisional.
- Except values embedded in abilities, PDFs omit maxHP, basicdamage, attackinterval, movementinterval, acquisitionrange, IQ, costs and upgrade unlock rules.
- PDFs do not specify whether Monster upgrades stack or choose1; Tsu explicitly allows Whelp upgrades1+3 together.
- Adjacency usually unspecified4-way/8-way; 3x3 and cross differ explicitly. Preserve separate area functions.
- Status durations often omitted. Do not silently present inferred defaults as PDF facts.
- Bugaloo taunt, Hymay focus, Sepora invisibility and Anuik resurrection create legitimate target-lock exceptions.
- Keep summons alive after ownerdeath and whole-teamround completion from prioruserrequirements.

## Anuik - Monster (Monsters.pdf, pág. 1)


- **Siempre de Pie** (`anuik-revive`, first_defeat): La primera vez que Anuik es derrotado, cae de pie y revive con 50% de su vida máxima.

- **Una Vez Más** (`anuik-second-revive`, second_defeat): Después de utilizar Siempre de Pie, Anuik puede revivir una segunda vez al ser derrotado, esta vez con 25% de su vida máxima.

- **Impacto de Retorno** (`anuik-revive-impact`, revive): Cuando Anuik revive mediante Siempre de Pie, inflige daño a todos los enemigos en las casillas adyacentes y los empuja.

- **Salto de Regreso** (`anuik-revive-jump`, revive): Cuando Anuik revive mediante Siempre de Pie, en lugar de reaparecer donde fue derrotado, aparece 4 casillas hacia adelante.

Pendientes de interpretación:
- El PDF dice cae de pie; petición previa del usuario especifica animación de cabeza.
- No indica daño del impacto, distancia de empuje, retraso de resurrección, destino ocupado/fuera del tablero ni si mejoras 2/3 también se activan en segunda resurrección.
- Ataque a distancia de corazones procede de petición previa, no se repite en PDF.

## Bugaloo - Monster (Monsters.pdf, pág. 2)


- **Rebote Lunar** (`bugaloo-reflect`, energy): Durante 3 segundos, Bugaloo provoca a los enemigos cercanos para que lo ataquen y refleja el daño recibido hacia ellos.

- **Eclipse Protector** (`bugaloo-protection`, while_super): Mientras Rebote Lunar está activo, Bugaloo recibe solo el 50% del daño entrante. El daño reflejado se calcula con el daño original y no se reduce.

- **Luna Vengativa** (`bugaloo-revenge`, while_super): Mientras Rebote Lunar está activo, Bugaloo refleja 150% del daño recibido.

- **Luz Reparadora** (`bugaloo-healing`, receive_heal): Las curaciones que recibe Bugaloo son 50% más efectivas.

Pendientes de interpretación:
- No especifica radio de cercanía, interacción entre reflejos o ganancia/coste de energía.
- La provocación es una excepción explícita del documento a fijar objetivo.

## Popow - Monster (Monsters.pdf, pág. 3)

Ataque: melee. Golpea directamente con sus puños. {}

- **Embestida Imparable** (`popow-dash`, energy): Popow se lanza hacia el enemigo más alejado de su posición. Durante el recorrido, todos los enemigos que atraviesa quedan aturdidos durante 1 segundo.

- **Impacto Demoledor** (`popow-dash-stun`, super_hit): Los enemigos alcanzados durante Embestida Imparable quedan aturdidos durante 2 segundos en lugar de 1.

- **Gancho Barrido** (`popow-sweep`, basic_hit): Los puñetazos de Popow golpean 3 casillas a la vez: la casilla del objetivo y las 2 casillas adyacentes a esta dentro del área 3×3 que rodea a Popow. Los enemigos alcanzados quedan aturdidos durante 0,5 segundos. Por ejemplo, si Popow golpea una de las esquinas del área 3×3 que lo rodea, también golpea las dos casillas centrales que tocan esa esquina.

- **Carga de Impacto** (`popow-dash-charge`, super_hit): Por cada enemigo que Popow impacta durante Embestida Imparable, acumula +3 de daño. Al finalizar la embestida, todo el daño acumulado se añade a su siguiente ataque básico. Después de realizar ese ataque, la bonificación desaparece.

Pendientes de interpretación:
- No especifica daño propio de embestida, movimiento/resolución de casilla ocupada ni energía.

## Sepora - Monster (Monsters.pdf, pág. 4)

Ataque: ranged. Dispara 1 rayo contra su objetivo. {"projectile_count": 1}

- **Tormenta Eléctrica** (`sepora-storm`, energy): Sepora dispara 3 rayos y aumenta su velocidad de ataque durante 5 segundos.

- **Velo de Tormenta** (`sepora-stealth`, health_below_threshold): Cuando la vida de Sepora baja del 50%, Sepora y sus aliados adyacentes se vuelven invisibles.

- **Descarga Múltiple** (`sepora-multishot`, attack): Sepora pasa a atacar 2 objetivos simultáneamente con sus ataques básicos. Al usar Tormenta Eléctrica, pasa a atacar 5 objetivos.

- **Poder Acumulado** (`sepora-kill-power`, kill): Por cada enemigo que Sepora derrota, obtiene +1 de daño permanente entre rondas durante esa partida.

Pendientes de interpretación:
- No especifica multiplicador de velocidad, duración/fin de invisibilidad, frecuencia del umbral, reparto de rayos cuando hay pocos objetivos ni energía.

## Tauris - Monster (Monsters.pdf, pág. 5)

Ataque: melee. Ataca cuerpo a cuerpo mordiendo. {}

- **Mordisco Letal** (`tauris-execute`, energy): Tauris ejecuta un mordisco letal que elimina instantáneamente al enemigo objetivo.

- **Presa Marcada** (`tauris-mark`, round_start): Al inicio de la ronda, Tauris obtiene energía por cada enemigo situado en su misma fila. Además, esos enemigos quedan marcados por Tauris y el rival no podrá cambiar su posición en las siguientes rondas.

- **Mandíbula Barrida** (`tauris-sweep`, super_hit): Mordisco Letal pasa a afectar 3 casillas: la casilla del objetivo y las 2 casillas adyacentes a ella dentro del área 3×3 que rodea a Tauris, siguiendo la misma geometría que el gancho de Popow. Los enemigos alcanzados por esa zona reciben el efecto del Super.

- **Hambre Insaciable** (`tauris-hunger`, super_and_kill): Mordisco Letal deja de ejecutar instantáneamente al objetivo y, en su lugar, le quita 50% de su vida. A cambio, cada vez que Tauris elimina a un enemigo, recupera el 100% de su vida y su ataque principal hace un 50% más de daño.

Pendientes de interpretación:
- No indica energía por marcado, si fila significa horizontal (interpretación literal), vida actual o máxima para 50%, ni si +50% de ataque acumula por baja.

## Atori - Whelp (whelps.pdf, pág. 1)


- **Caza al Débil** (`atori-hunt`, target_acquisition): Atori persigue y ataca prioritariamente al personaje enemigo con menor vida actual.

- **Cabeza dura** (`atori-hard-head`, round_start): Durante los primeros 5 segundos del combate, Atori reduce en 20% el daño recibido.

- **Impulso de Caza** (`atori-hunt-speed`, kill): Cada vez que Atori elimina a un enemigo, obtiene un 50% de velocidad adicional.

- **Instinto Voraz** (`atori-kill-heal`, kill): Cada vez que Atori elimina a un enemigo, recupera 20 puntos de vida.

Pendientes de interpretación:
- No define si velocidad incluye ataque y movimiento, acumulación y duración. Se conserva fijación de objetivo previa: prioridad se evalúa al adquirir, no cambia objetivo vivo.

## Blotan - Whelp (whelps.pdf, pág. 2)


- **Buscatesoros** (`blotan-income`, combat_interval): Blotan encuentra 1 Moon-Ken cada 20 segundos mientras permanece en combate.

- **Fortuna Acelerada** (`blotan-fast-income`, combat_interval): Blotan pasa a encontrar 1 Moon-Ken cada 10 segundos.

- **Reserva Dorada** (`blotan-health`, stats): Blotan aumenta su vida máxima en 20 puntos.

- **Botín de Guerra** (`blotan-steal`, round_end_alive): Si Blotan permanece con vida al terminar la ronda, roba 2 Moon-Ken al rival.

Pendientes de interpretación:
- No especifica robo cuando el rival tiene menos de 2; limitar al saldo real. Ataque básico no especificado.

## Bugui - Whelp (whelps.pdf, pág. 3)

Ataque: ranged.  {}

- **Ritmo Creciente** (`bugui-rhythm`, attack): Cada ataque aumenta la velocidad de ataque de Bugui, hasta un máximo de 10 acumulaciones.

- **Sin Fronteras** (`bugui-unlimited-range`, stats): El alcance de los ataques de Bugui pasa a ser ilimitado.

- **Aceleración Infinita** (`bugui-unlimited-stacks`, attack): La bonificación de velocidad de ataque obtenida por cada acumulación es menor, pero Ritmo Creciente ya no tiene límite de acumulaciones.

- **Hambre de Poder** (`bugui-kill-power`, kill): Cada vez que Bugui elimina a un enemigo, obtiene +1 de daño permanentemente durante esa ronda, acumulable.

Pendientes de interpretación:
- No especifica aumento de velocidad por acumulación normal/mejorada. Pantalla characters dice Bugmy, PDF autoritativo dice Bugui.

## Flo - Whelp (whelps.pdf, pág. 4)


- **Flor de Auxilio** (`flo-flower`, energy): Flo genera una maceta con una flor en la casilla frente a él. La flor tiene 20 de vida y, al ser derrotada, cura 20 de vida a los aliados a su alrededor.

- **Floración Intensa** (`flo-flower-heal`, summon_death): La flor pasa a curar 40 de vida al ser derrotada.

- **Brote Preparado** (`flo-full-energy`, round_start): La barra de energía de Flo comienza completamente cargada al inicio de la ronda.

- **Flor Eterna** (`flo-flower-invulnerability`, summon): La flor tiene solo 1 punto de vida, pero es invulnerable durante 1 segundo después de aparecer. Durante ese tiempo no puede ser derrotada.

Pendientes de interpretación:
- No se especifican radio de curación, máximo de flores, ataque/movimiento de la flor ni alternativa para casilla frontal ocupada. Comentario editorial sobre bomba programada no se incluye como regla: después de 1 s aún necesita golpe.

## Gochan - Whelp (whelps.pdf, pág. 5)

Ataque: ranged. Lanza el símbolo de su frente; daña en cruz alrededor del objetivo. {"area": "cross"}

- **Explosión del Sello** (`gochan-seal-burst`, energy): Al activar su energía, el impacto genera un área de 3×3 alrededor del enemigo alcanzado e inflige 20 de daño a todos los enemigos dentro de esa zona.

- **Sello Marchitante** (`gochan-anti-heal`, hit): Los ataques de Gochan reducen en 60% la curación recibida por los enemigos afectados.

- **Marca Ardiente** (`gochan-burn`, super_hit): La habilidad de energía deja una marca sobre los enemigos afectados. Cada marca inflige 2 de daño por segundo durante 4 segundos y puede acumularse.

- **Sello Expandido** (`gochan-expanded-seal`, basic_hit): El ataque básico de Gochan deja de golpear en forma de cruz y pasa a afectar un área completa de 3×3 alrededor del objetivo.

Pendientes de interpretación:
- No se especifica duración anticuración ni energía.

## Jazar - Whelp (whelps.pdf, pág. 6)

Ataque: ranged. Lanza materia radiactiva a distancia. {}

- **Contaminación** (`jazar-radiation`, hit): Los enemigos alcanzados por los ataques de Jazar quedan irradiados, haciendo que su velocidad de ataque se reduzca en 25%.

- **Onda Radiactiva** (`jazar-cross`, basic_hit): Los ataques de Jazar pasan a afectar un área en forma de cruz alrededor del objetivo alcanzado.

- **Radiación Intensa** (`jazar-intense-radiation`, hit): La reducción de velocidad de ataque provocada por Contaminación aumenta de 25% a 50%.

- **Zona Contaminada** (`jazar-front-row`, round_start): Al inicio de la ronda, Jazar cubre con materia radiactiva las casillas de la primera fila enemiga ubicada junto al centro del tablero. La materia permanece durante 1 segundo y aplica Contaminación a los enemigos que se encuentren sobre esas casillas.

Pendientes de interpretación:
- No especifica duración de irradiación.

## Kayon - Whelp (whelps.pdf, pág. 7)

Ataque: ranged. Lanza monedas de oro a distancia. {}

- **Refuerzo Dummy** (`kayon-summon`, energy): Kayon invoca 1 Dummy para ayudarlo durante el combate.

- **Doble Refuerzo** (`kayon-double-summon`, super): Al usar Refuerzo Dummy, Kayon invoca 2 Dummys en lugar de 1.

- **Legado del Dummy** (`kayon-dummy-legacy`, summon_death): Cuando un Dummy es derrotado, los aliados a su alrededor obtienen +1 de daño.

- **Dummy Reforzado** (`kayon-dummy-health`, summon_stats): Los Dummys invocados por Kayon tienen +4 de vida máxima.

Pendientes de interpretación:
- No especifica stats Dummy, energía, cantidad máxima simultánea, radio legado ni duración/acumulación del bono. Rango4 y stats reducidos se conservan de petición anterior.

## Stein - Whelp (whelps.pdf, pág. 8)

Ataque: ranged. Lanza 2 bolas de energía a 2 enemigos distintos dentro de alcance. Si solo hay uno ambas impactan al mismo. {"projectile_count": 2, "max_distinct_targets": 2, "duplicate_target_if_alone": true}

- **Microdescarga** (`stein-stun`, each_impact): Cada impacto de Stein aturde al enemigo durante 0,1 segundos.

- **Sobrecarga** (`stein-long-stun`, each_impact): La duración del aturdimiento de Microdescarga aumenta de 0,1 a 0,2 segundos por impacto.

- **Transferencia de Energía** (`stein-energy-gift`, round_start): Al inicio de la ronda, Stein otorga energía a los aliados situados inmediatamente a su derecha y a su izquierda.

- **Triple Descarga** (`stein-triple`, attack): Stein pasa de poder atacar 2 objetivos a 3 objetivos simultáneamente.

Pendientes de interpretación:
- No indica cantidad de energía transferida; tercer proyectil y reparto cuando solo hay1/2 enemigos deben explicitarse como interpretación.

## Trimol - Whelp (whelps.pdf, pág. 9)

Ataque: ranged.  {}

- **Pedrada Salvaje** (`trimol-boulder`, every_attacks): Cada 4 ataques, Trimol lanza una roca gigante en línea recta. La roca atraviesa a los enemigos que encuentra en su recorrido y los aturde durante 1 segundo.

- **Lanzamiento Inicial** (`trimol-opening-boulder`, round_start): Al comenzar la ronda, Trimol lanza inmediatamente una roca gigante en línea recta, aunque no tenga ningún enemigo frente a él.

- **Roca Imparable** (`trimol-infinite-boulder`, boulder): Las rocas lanzadas por Trimol pasan a tener recorrido infinito, continuando en línea recta hasta salir del tablero.

- **Pedrada Brutal** (`trimol-brutal-boulder`, boulder): Las rocas de Trimol infligen más daño y el aturdimiento aumenta de 1 a 2 segundos.

Pendientes de interpretación:
- No especifica daño básico/roca/roca mejorada, recorrido base ni dirección cuando objetivo fuera de misma columna.

## Tsu - Whelp (whelps.pdf, pág. 10, 11)

Ataque: ranged. Lanza paletitas de fresa desde sus bolsillos. {}

- **Chicle Pegajoso** (`tsu-root`, every_attacks): Cada 4 ataques, Tsu arranca una bola de chicle de su propio cabello y la lanza contra su objetivo. El enemigo alcanzado queda inmovilizado durante 2 segundos: puede continuar atacando normalmente, pero no puede cambiar de casilla.

- **Cabello Pegajoso** (`tsu-death-trap`, death): Cuando Tsu es derrotado, deja caer su cabello de chicle sobre la casilla donde murió. La casilla queda convertida en una trampa permanente. El primer enemigo que entre en ella queda inmovilizado en esa casilla y no puede abandonarla. La trampa permanece activa mientras ese enemigo siga vivo y desaparece cuando es derrotado.

- **Chocolates Explosivos** (`tsu-chocolate`, sticky_gum_root): Los enemigos inmovilizados por el Chicle Pegajoso reciben chocolates explosivos. Mientras permanezcan inmovilizados, los chocolates explotan cada segundo e infligen daño progresivo.

- **Chicles de Colores** (`tsu-colored-gum`, super): Cada vez que Tsu activa Chicle Pegajoso, además lanza un chicle especial al aliado vivo más cercano. Si no queda ningún aliado vivo, Tsu se lo aplica a sí mismo. Los chicles siguen este ciclo: 1. Primer chicle — Rosa veloz: +20% de velocidad de ataque y movimiento. 2. Segundo chicle — Chicle protector: otorga un escudo equivalente al 10%. 3. Tercer chicle — Chicle potente: aumenta el daño en 20%. Después del tercer efecto, el ciclo puede volver a comenzar desde el primero.

Pendientes de interpretación:
- El título dice energía pero activación explícita cada4ataques; respetar frecuencia.
- No especifica escala de daño progresivo, base del10%escudo, duración/acumulación buffs ni si chocolates se aplican a trampa de muerte. Sí dice que habilidades1y3 se pueden combinar.

## Atong - Whelp (whelps.pdf, pág. 11)

Ataque: melee. Golpes lentos, poco frecuentes y letales. {"tempo": "slow"}

- **Golpe Calculado** (`atong-critical`, every_attacks): Cada segundo ataque de Atong es un golpe crítico e inflige 50% más de daño.

- **Crítico Devastador** (`atong-critical-damage`, critical): El daño adicional de sus golpes críticos aumenta de 50% a 100%.

- **Golpe Perfecto** (`atong-always-critical`, attack): Todos los ataques de Atong pasan a ser golpes críticos.

- **Voluntad Inquebrantable** (`atong-control-immunity`, round_start): Durante los primeros 5 segundos de la ronda, Atong obtiene Inamovible: los efectos de control no tienen efecto sobre él.

Pendientes de interpretación:
- No especifica ritmo numérico ni si crítico cuenta ataques iniciados o acertados.

## Tokoro - Whelp (whelps.pdf, pág. 12)

Ataque: melee.  {}

- **Cabezazo Somnoliento** (`tokoro-headbutt`, energy): Tokoro lanza un poderoso cabezazo contra su objetivo. Después del impacto, Tokoro y el enemigo quedan aturdidos durante 3 segundos.

- **Siesta Reparadora** (`tokoro-stun-heal`, while_stunned): Mientras Tokoro esté aturdido, recupera 8 de vida por segundo, comenzando desde el segundo 0 del aturdimiento. Así, un aturdimiento de 3 segundos puede activar la curación desde el instante en que comienza.

- **Heladería Improvisada** (`tokoro-ice-cream`, while_stunned): Mientras Tokoro esté aturdido, genera conitos de helado en las casillas a su alrededor. ● Si un aliado entra en una casilla con un cono, lo consume y recupera 10 de vida. ● Si un enemigo lo consume, recibe Debilidad, haciendo que reciba 20% más de daño por 3 segundos. ● Solo puede existir 1 cono por casilla. ● Si se intenta generar otro cono en una casilla que ya contiene uno, el nuevo reemplaza al anterior, evitando acumulaciones en la misma casilla.

- **Hora de Dormir** (`tokoro-sleep`, last_seconds): Durante los últimos 10 segundos de la ronda, Tokoro se queda dormido. Mientras duerme: ● Recupera 15 de vida por segundo. ● Deja de atacar por completo.

Pendientes de interpretación:
- No especifica daño del cabezazo ni energía, frecuencia/número de conos o si sueño impide movimiento. Curación segundo0 debe serinmediata; no confundir con comenzar alsegundo1.

## Aky - Whelp (whelps.pdf, pág. 13)

Ataque: melee.  {}

- **Drenaje de Energía** (`aky-energy-drain`, basic_hit): Los ataques de Aky drenan una cierta cantidad de energía del enemigo alcanzado.

- **Vuelo Rasante** (`aky-opening-flight`, round_start): Al iniciar la ronda, Aky vuela en línea recta hasta el final del mapa.

- **Absorción Vital** (`aky-drain-heal`, drain): Cada vez que Aky drena energía, recupera vida equivalente al 50% de la energía drenada.

- **Golpe Amplio** (`aky-sweep`, basic_hit): El ataque básico de Aky pasa a afectar 3 casillas, con la misma geometría del gancho de Popow: la casilla objetivo y las 2 casillas adyacentes dentro del área 3×3 que lo rodea.

Pendientes de interpretación:
- No especifica energía drenada, ruta aterrizajeocupado ni si vuelo inflige daño.

## Hymay - Whelp (whelps.pdf, pág. 14)

Ataque: ranged. Ataca con su látigo hasta 4 casillas de distancia. La cabecera declara alcance de 3 casillas. {"range_header": 3, "range_body": 4}

- **Tirón de Látigo** (`hymay-pull`, energy): Hymay atrapa con su látigo al enemigo que está atacando y lo arrastra hasta la casilla situada delante de Hymay.

- **Objetivo Marcado** (`hymay-focus`, pull): Cuando Hymay atrae a un enemigo con Tirón de Látigo, provoca que sus aliados concentren sus ataques sobre ese enemigo.

- **Presa Vulnerable** (`hymay-weakness`, pull): El enemigo atraído por Tirón de Látigo recibe Debilidad, haciendo que reciba 25% más de daño.

- **Tirón Acrobático** (`hymay-backstep`, after_pull): Inmediatamente después de atraer a un enemigo, Hymay realiza una maniobra acrobática y retrocede 1 casilla, recuperando distancia respecto al enemigo.

Pendientes de interpretación:
- CONTRADICCIÓN numérica: cabecera3casillas, texto4. Preferencia provisional sugerida4 por regla deataque detallada, documentar.
- No especifica duraciónDebilidad, resistencia/desplazamiento inválido. Forzarataquesaliados esexcepciónexplícita delPDF ala fijaciónprevia.

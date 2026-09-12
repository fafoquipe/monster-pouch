# Nuevas habilidades de combate

Estas reglas implementan las peticiones actuales del usuario para Anuik, Tauris, Kayon y Stein. Los valores de balance que no especificó el usuario son provisionales, configurables y propios de Monster Pouch; no se atribuyen a la colección física de Gogo’s.

## Valores iniciales

| Personaje | Tipo | Vida | Daño | Alcance | Ataque cada | Preparación | Movimiento cada | IQ | Compra |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Anuik | Monster | 90 | 7 | 3 | 1,3 s | 0,3 s | 0,6 s | 4 | Selección inicial |
| Tauris | Monster | 100 | 6 | 1 | 1,4 s | 0,3 s | 0,6 s | 4 | Selección inicial |
| Kayon | Whelp | 28 | 2 | 4 | 1,5 s | 0,3 s | 0,6 s | 3 | 3 monedas |
| Stein | Whelp | 26 | 6 | 4 | 1,2 s | 0,3 s | 0,6 s | 5 | 3 monedas |
| Dummy invocado | Whelp temporal | 14 | 2 | 1 | 1,4 s | 0,2 s | 0,7 s | 2 | No se compra |

Las fábricas `ExpandedRoster.CreateAnuik()`, `CreateTauris()`, `CreateKayon()` y `CreateStein()` entregan definiciones nuevas para agregarlas al catálogo. No reemplazan `MatchConfig.DefaultUnits()`. `UnitStats.FromDefinition` copia los valores efectivos antes del combate y mantiene separados los datos compartidos del estado de una partida.

## Anuik

Anuik ataca a distancia con corazones. «Segundo corazón» permite una resurrección por combate: al recibir daño letal conserva su casilla y espera 0,8 segundos. Durante ese intervalo tiene cero de vida, `IsReviving = true`, no actúa, no es un objetivo válido y no puede curarse mediante impactos. Después recupera el 50 % de su vida máxima efectiva, redondeando hacia arriba y con un mínimo de un punto. La mejora de Monster aumenta 25 de vida y 2 de daño, de modo que también aumenta la vida recuperada al resucitar.

La simulación emite `Reviving(actor, duration)` para iniciar la animación de vuelta, y `Revived(actor)` cuando realmente retorna. Sólo emite `Died(actor)` cuando la muerte es definitiva. `IsAlive` permanece falso durante la espera; el resultado de la ronda usa `IsReviving` como una posibilidad de supervivencia. Si el adversario ya no tiene unidades, la simulación termina de ejecutar la resurrección antes de mostrar la victoria. La segunda muerte de Anuik ya no permite regresar. Al comenzar otro combate, la disponibilidad de la habilidad se restablece.

Los proyectiles que Anuik ya lanzó siguen su curso. Los proyectiles enemigos que llegan mientras está reviviendo se descartan y no cuentan como impactos acertados. La animación no decide el instante de resurrección: el reloj lógico lo determina.

Por petición explícita del usuario, la resurrección libera inmediatamente a todos los atacantes que tenían fijado a Anuik, incluso si están esperando su próximo ataque. Pueden elegir otro rival y conservarlo cuando Anuik regrese. Si no existe otro enemigo vivo, esperan; tras el regreso, Anuik vuelve a ser elegible. Anuik también adquiere de nuevo su propio objetivo después de resucitar.

## Objetivos persistentes y despliegue

Cada ActorClock conserva el BattleUnit elegido desde el primer paso de persecución hasta su derrota o invalidez. Solo la adquisición inicial aplica NearestReachable o LowestHealth. La ruta y el alcance se recalculan contra ese mismo rival; enemigos más próximos, cambios de vida, invocaciones y bloqueos temporales no sustituyen la referencia. Los impactos en vuelo conservan su destinatario original. Begin/Stop/Finish y ResetForCombat limpian o invalidan el objetivo; se invalida también si desaparece, queda inactivo, cambia de equipo o pierde su ocupación. La resurrección de Anuik es la excepción deliberada descrita arriba.

El tablero sigue siendo 6 columnas × 10 filas. Cada bando despliega en cinco filas completas: Blue Y5–9 y Red Y0–4, 30 casillas por lado, sin filas neutrales. Las máscaras de MatchConfig son la única fuente para compra, colocación, resaltado y formación del bot. MonsterPouchDeploymentSetup.Setup migra solo esas dos máscaras en el asset existente.

TargetLockTests incluye 14 casos de adquisición, persecución en ambos bandos y alcances, bloqueo de ruta, política de vida baja, Dummy recién invocado, muerte definitiva, reinicio y resurrección. DeploymentMaskTests añade 13 casos; una prueba de PlayMode arrastra por las 30 casillas, comprueba resaltado y que ninguna interfaz las tape, y rechaza la mitad enemiga sin consumir copias ni monedas.

## Tauris

Tauris ataca cuerpo a cuerpo con alcance de una casilla. Se acerca al rival antes de morder; la mordida no tiene vuelo y su efecto aparece al contacto. La elección provisional para la frecuencia de «Mordisco final» es **cada cuarto impacto acertado**. Ese impacto aplica la vida restante del objetivo como daño y omite la armadura. Funciona contra Whelps y Monsters; un Anuik que todavía conserve su resurrección puede regresar después de recibirlo. Los ataques que no encuentran un objetivo vivo no avanzan el contador.

Todos los impactos que llegan en un mismo tick leen la vida al inicio de esa resolución. Por eso dos golpes letales simultáneos pueden eliminar ambos equipos y producir empate; no se concede victoria por el orden de la lista, el ID, el IQ o el cuadrante. La mejora de Tauris multiplica su intervalo de ataque por 0,8 y aumenta el daño ordinario en 1; conserva la frecuencia de cuatro impactos.

`LethalImpacted(actor, target)` informa el resultado autoritativo a la vista. `IsNextHitLethal(actor)` permite anticipar visualmente la carga según los impactos ya acertados; es una consulta del contador, no una promesa de que un proyectil lanzado vaya a acertar. El evento `Attacked` conserva sus tres argumentos anteriores.

## Kayon y los Dummys temporales

Kayon lanza monedas de oro a distancia, con alcance de cuatro casillas y estadísticas bajas. La moneda tiene 0,3 s de preparación y vuelo según la distancia, con animación de giro y destello dorado al impactar. La primera invocación ocurre a los seis segundos de combate; después hay seis segundos de separación desde cada invocación efectiva. Se permiten como máximo dos Dummys vivos por Kayon. Busca una casilla ortogonal vecina libre, primero hacia el enemigo, después izquierda, derecha y atrás. No ocupa una casilla que ya tenga unidad o reserva. Si no hay espacio o el cupo está lleno, espera hasta poder crear el siguiente; no acumula una cola de invocaciones.

El Dummy tiene menos vida y daño que el Dummy comprado, no hereda los Tricks de Kayon ni los del Dummy normal, no tiene su armadura y no invoca otras unidades. Se crea después de resolver los daños del tick: Kayon debe seguir vivo para invocar. Las criaturas ya creadas sobreviven a la muerte de Kayon y cuentan como integrantes del equipo hasta su propia eliminación.

La simulación crea un `WhelpUnit` con `IsCombatSummon = true`, identificador único por invocación y definición visual `dummy`. Emite `Summoned(actor, definition)` después de colocarlo en el tablero. `CombatOnlyUnits` expone estas unidades para presentación y diagnóstico. **No crea un `OwnedUnit`, no escribe en `PouchState`, no consume copias ni añade oro.** Al detener la simulación, libera sus casillas y destruye los objetos temporales. No quedan disponibles en la siguiente preparación.

Los Tricks de Kayon mejoran a Kayon: «Cuidador» añade 12 de vida por 2 monedas; «Empuje» añade 2 de daño por 2; «Ritmo» multiplica el intervalo de sus ataques por 0,8 por 3. No modifican la cadencia ni las estadísticas de sus invocaciones.

## Stein

Stein dispara rayos a distancia. Como habilidad provisional, «Descarga» añade cuatro de daño cada tercer impacto. Sus Tricks son «Voltaje» (+2 de daño, coste 2), «Conductor» (+1 de alcance, coste 2) y «Frecuencia» (intervalo de ataque ×0,8, coste 3). El aspecto del rayo pertenece a la presentación; el daño mantiene el mismo procesamiento simultáneo de los demás proyectiles.

## Reglas conservadas y verificación

El paso lógico continúa siendo de 0,1 segundos. Se conservan el movimiento por `BoardMovementResolver`, las prioridades existentes de reservas, la eliminación de todos los integrantes del equipo como condición de derrota y los límites de 40 segundos de combate y 10 segundos sin progreso. Las resurrecciones pendientes y las invocaciones posibles se consideran trabajo pendiente al evaluar el estancamiento; el límite total de 40 segundos sigue vigente.

`expanded-combat-tests.cs` cubre: duración y límite de resurrección, regreso de ambos equipos, impactos en vuelo, intangibilidad mientras revive, muerte letal contra ambas categorías y armadura, golpes letales simultáneos, resurrección después de un mordisco letal, cadencia y cupo de invocaciones, casillas ocupadas, supervivencia sin Kayon, limpieza de objetos temporales, aislamiento de la economía, fábricas de definiciones, reinicio de habilidad entre combates y equivalencia de eventos con pasos variables y orden inicial invertido. También verifica que un mordisco letal pendiente y los impactos que cargan la habilidad eviten un empate prematuro incluso si el daño ordinario configurable es cero. Dos casos comprueban que Kayon salte una vecina bloqueada para usar otra libre y que no prolongue un combate estancado cuando todas sus vecinas están bloqueadas. Si el límite total de 40 segundos interrumpe una resurrección, se cancela el retorno, se libera la casilla y se emite una única muerte definitiva; otro caso verifica este contrato. Su ejecución y la validación de la interfaz se realizan desde la sesión central de Unity; este documento no sustituye los resultados de esa ejecución.

La regresión de monedas comprueba que Kayon dispara desde cuatro casillas sin moverse, aplica dos de daño tras el tiempo de vuelo y conserva la primera invocación a los 6 segundos, la siguiente a los 12 y el límite de dos vivos. PlayMode comprueba los sprites reales de vuelo e impacto dorado.

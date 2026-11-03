# Copas: duración y ritmo de la prueba local

Fecha: 22 de octubre de 2026. Este informe distingue configuración del juego, cálculos analíticos y escenarios hipotéticos. No contiene mediciones de jugadores ni predice cuánto tardará una persona concreta.

## Política de prueba aprobada

- Inicio y suelo: 0 copas; una derrota que cruza el suelo queda en 0.
- Classic: 0–149 copas antes del BO5, victoria +30 y derrota −5.
- Royal: desde 150 copas antes del BO5, victoria +30 y derrota −30.
- Solo estas dos arenas y todos contra todos por ahora. No se añaden franjas de matchmaking.

El modelo decide la penalidad por las copas previas al encuentro. Bajar de 150 devuelve la penalidad de Classic para el encuentro siguiente. Esto **no decide** si el tablero Royal deja de estar disponible, si se cambia una selección o si se retira un desbloqueo; esas reglas de acceso no se infieren del cálculo. Tampoco convierte el ensayo local en copas comerciales o persistencia de servidor.

## Tiempos verificados en las fuentes

La configuración cargada por el juego es `Assets/resources/MonsterPouch/MatchConfig.asset`, no los valores predeterminados de la clase. El controlador da 40 segundos de preparación y hasta 40 de combate. La ventana inicial de habilidades de 1,5 segundos está incluida en el reloj del combate.

| Concepto | Segundos |
| --- | ---: |
| Transición tras eliminación | 0,9 + 0,4 + 0,7 + 1 + 0,5 = **3,5** |
| Transición con comparación de HP | 1,2 + 3,2 + 0,4 + 0,7 + 1 + 0,5 = **7** |
| Espera hasta botones finales tras eliminación | **3** |
| Espera hasta botones finales con HP | **6,5** |

El anuncio de tiempo agotado toma 1,2 segundos del valor inicial de `RoundPresentationSettings.timeoutBannerDuration`; ese campo no tiene una sobrescritura explícita en el asset. Las demás cifras de presentación constan en el asset. La comparación de HP solo se muestra cuando hay timeout y aportes vivos en ambos equipos.

Para `n` rondas sin empates, con transición uniforme `r` y espera final `f`:

`T = suma(preparación_i + combate_i) + (n − 1) × r + f`

| Resultado | Ambos relojes al máximo, eliminación sin secuencia HP | Ambos relojes al máximo, HP en todas las rondas |
| --- | ---: | ---: |
| 3–0 | 250 s = 4 min 10 s | 260,5 s = 4 min 20,5 s |
| 3–2 | 417 s = 6 min 57 s | 434,5 s = 7 min 14,5 s |

Son escenarios al agotar los relojes, **no duraciones medias**. El botón LISTO acorta la preparación; contra bot su espera es de 1,2 segundos. Con LISTO inmediato y llevando matemáticamente el tiempo de combate hacia cero, la fórmula da 13,6 segundos para tres rondas y 23 para cinco. Ese límite teórico no demuestra que una partida válida pueda terminar así. El promedio real necesita telemetría o sesiones de juego observadas.

El confeti final no bloquea hasta terminar: los botones se habilitan antes. El cambio circular de mapa dura 1,46 segundos y corre en paralelo al reloj; no se suma como pausa. No se incluyen carga, búsqueda de rival, latencia, pausas ni tiempo voluntario en la pantalla final. Tampoco hay máximo absoluto: las rondas anuladas no suman victoria y pueden extender un encuentro más allá de cinco rondas.

Referencias de código: `MatchController.cs:176,400,459–497`; `combat-simulation.cs:15,104`; `documented-combat.cs:141–210`; `MatchConfig.asset:16–29`; `RoundPresentationSettings.cs:17,34–38`; `local-round-result.cs:202–210`; `online-match.cs:32–38`; `MapIrisTransition.cs:10–11`. Todas pertenecen al proyecto Unity; ninguna fuente se modificó para este informe.

## Modelo de primera llegada

Se supone una probabilidad constante e independiente `p` de ganar cada **BO5**, no cada ronda. Un empate o abandono no se modela como derrota. Para una meta `H`, `E(s)` es el número esperado de BO5 hasta alcanzar o superar `H` desde `s` copas:

`E(s) = 0`, si `s ≥ H`.

`E(s) = 1 + p × E(s + 30) + (1 − p) × E(max(0, s − L(s)))`, en otro caso.

En la prueba aprobada, `L(s) = 5` si `s < 150`, y `L(s) = 30` desde 150. Resolver el sistema lineal incorpora derrotas en el suelo, sobrepasar la meta y volver a la penalidad inferior. Dividir la meta por la ganancia neta media es solo una aproximación y no sustituye esta primera llegada.

### Llegar a Royal: 150 copas desde cero

| Victorias por BO5 | BO5 esperados | Minutos con todas las partidas de 3 rondas al máximo con HP | Minutos con todas de 5 rondas al máximo con HP |
| --- | ---: | ---: | ---: |
| 40 % | 16,97 | 74 | 123 |
| 50 % | 12,52 | 54 | 91 |
| 60 % | 9,96 | 43 | 72 |
| 70 % | 8,26 | 36 | 60 |
| 100 % | 5 | 22 | 36 |

Estas columnas comparan dos duraciones supuestas por BO5. No son percentiles, garantías, mínimo real ni máximo absoluto del tiempo de subida. No se deduce el número de rondas a partir del porcentaje de victorias de encuentros.

### Después de 150

En Royal, la deriva media local de +30/−30 es `60p − 30`: −6, 0, +6, +12 y +30 copas por BO5 para 40 %, 50 %, 60 %, 70 % y 100 %, respectivamente. Tener deriva cero no impide alcanzar una meta mediante fluctuaciones. La expectativa de llegada depende del suelo y de la regla que vuelve a aplicarse al descender.

Como comparación analítica adicional, alcanzar **300 desde cero con las dos etapas vigentes** da:

| Victorias | BO5 esperados | Minutos si cada BO5 agota relojes, 3–5 rondas con HP |
| --- | ---: | ---: |
| 40 % | 156,79 | 681–1135 |
| 50 % | 54,48 | 237–395 |
| 60 % | 29,50 | 128–214 |
| 70 % | 20,05 | 87–145 |
| 100 % | 10 | 43–72 |

300 es una meta de análisis, **no otra arena ni un desbloqueo aprobado**.

### Estudio histórico de umbrales alternativos

Antes de aprobar 150 se compararon dos posibles finales de la primera arena. En aquella comparación, la penalidad −5 seguía vigente hasta la meta elegida:

| Victorias | Primera arena hipotética hasta 150 | Primera arena hipotética hasta 300 |
| --- | ---: | ---: |
| 40 % | 16,974895 BO5 | 33,609843 BO5 |
| 50 % | 12,524603 BO5 | 24,465095 BO5 |
| 60 % | 9,958035 BO5 | 19,253442 BO5 |
| 70 % | 8,259515 BO5 | 15,917526 BO5 |
| 100 % | 5 BO5 | 10 BO5 |

La columna de 300 se conserva como estudio histórico. **No describe la prueba aprobada**, cuya penalidad cambia a −30 desde 150.

## Reproducir y comprobar

Herramienta independiente del workspace: `tools/ladder-pacing.mjs`, Node sin paquetes externos. Solo escribe JSON a la salida estándar; no abre Unity ni lee/escribe perfiles.

```powershell
node tools/ladder-pacing.mjs
node tools/ladder-pacing.mjs --targets 150 --win-rates 0.4,0.5,0.6,0.7,1
# Solo para reproducir el estudio histórico de una primera arena hasta 300:
node tools/ladder-pacing.mjs --targets 300 --royal-start 300
# Escenario de duración distinto, no cambio del juego:
node tools/ladder-pacing.mjs --targets 150 --prep-seconds 20 --combat-seconds 20
```

Resultado predeterminado conservado en `final-foundations-docs-staging/ladder-pacing-results.json`. La herramienta permite parametrizar meta, inicio, suelo, frontera, cantidades, probabilidad y tiempos; sus argumentos no cambian ninguna configuración del juego.

La solución usa eliminación gaussiana con pivoteo parcial y comprueba el residuo de las ecuaciones. Valida números finitos, enteros de copas, pasos positivos, probabilidades entre 0 y 1, tiempos no negativos y orden de rondas. Limita el cálculo a 400 estados, 100 combinaciones, copas hasta 1.000.000, pasos hasta 10.000 y duraciones individuales hasta 3600 segundos. Rechaza argumentos desconocidos, repetidos o incompletos. Probabilidad cero se informa sin expectativa finita; un sistema numéricamente inestable se marca como tal, sin publicar un tiempo falso.

Verificación independiente: 27 comprobaciones aprobadas, incluidas las cinco expectativas de 150 obtenidas por un cálculo independiente, el caso histórico de 300, la diferencia con la política de dos etapas, probabilidad cero, meta ya alcanzada, el límite válido de 400 estados y 16 entradas inválidas. También se comprobaron la sintaxis Node y el máximo de copas con una malla admitida: 1.000.000 de copas, pasos de 10.000 y victoria segura requieren 100 BO5. Registro: `final-foundations-docs-staging/ladder-pacing-verification.json`. No se ejecutó Unity para esta herramienta matemática.

La exactitud del modelo no convierte sus supuestos sobre conducta, ritmo o victorias en datos observados. Las cifras son para evaluar esta prueba local antes de decidir un balance final.

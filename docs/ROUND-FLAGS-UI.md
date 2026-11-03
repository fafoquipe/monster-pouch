# Rondas y banderitas

La partida termina cuando alguien gana **3 rondas**. Los empates no otorgan banderas; se juega otra ronda para desempatar. El combate y las estadísticas de los personajes se conservan.

## Ajustes en Unity

Selecciona `Assets/resources/MonsterPouch/MatchConfig.asset`. Abre **Rondas · banderitas y transición** en el Inspector. No hace falta conectar componentes ni referencias manualmente.

| Campo | Valor inicial | Función |
| --- | ---: | --- |
| `maxRoundsToWin` | 3 | Victorias necesarias; ambas filas de banderas se adaptan. |
| `delayBeforeBetweenRoundsUI` | 0,20 s | Pausa inicial con el combate detenido. |
| `betweenRoundsPanelAnimationDuration` | 0,22 s | Entrada de las tarjetas. |
| `flagStampAnimationDuration` | 0,42 s | Aproximación, impacto, rebote y asentamiento de la bandera. |
| `delayAfterFlagStamp` | 0,65 s | Tiempo para leer el resultado. |
| `nextRoundStartDelay` | 0,25 s | Desvanecimiento antes de preparar la próxima ronda. |
| `enemyPanelPosition` | (18, 16) | Panel superior izquierdo. |
| `playerPanelPosition` | (338, 704) | Panel inferior derecho, debajo del tablero. |
| `roundLabelPositionOffset` | (0, 0) | Desplazamiento respecto de (374, 13). |
| `timerPositionOffset` | (0, 0) | Desplazamiento respecto de (399, 44). |
| `spacingBetweenFlags` | 7 | Separación entre banderas; se limita al espacio disponible. |

Las coordenadas usan un lienzo lógico **540 × 960**, desde la esquina superior izquierda: X hacia la derecha, Y hacia abajo. El escalado y área segura existentes adaptan el HUD al formato vertical. El panel local aparece durante combate/resultados; en preparación ese lugar se reserva al contador de munkens y al reroll del brief.

La transición completa dura **1,74 segundos** por defecto. Pausar detiene su reloj y su animación. La puntuación se registra inmediatamente al resolver el combate. La nueva bandera se vuelve dorada al impactar el sello (42 % de la animación), simultáneamente en la tarjeta y el HUD. Al llegar a tres victorias, aparecen Revancha y Menú y el resultado permanece abierto.

## Estructura

- `match-controller.cs`: `EndRound` registra al ganador y la puntuación; `Advance` cuenta el tiempo de resultados y comienza automáticamente la siguiente preparación. Sigue funcionando aunque no exista UI. `LastRoundWinner` y `RoundResultElapsed` son la fuente de verdad de la representación.
- `RoundPresentationSettings.cs`: configuración serializable de reglas, tiempos y posiciones, expuesta por `MatchConfig.RoundUI`.
- `local-round-hud.cs`: nombres, banderas persistentes, etiqueta de ronda y reloj independientes.
- `RoundFlagIcon.cs`: icono de bandera en píxeles, dibujado como una malla UI; no necesita texturas importadas.
- `local-round-result.cs`: oscurecimiento, tarjetas roja/azul, retratos y sello con destellos. Calcula cada pose desde el reloj de la partida; no controla el avance ni usa una coroutine que pueda bloquearlo.
- `LocalGameUI.cs`: construye las nuevas piezas y congela las poses del combate al mostrar el resultado.
- `MonsterPouchBalanceInspector.cs`: expone los ajustes sin cambiar las estadísticas de personajes.

La UI existente se construye en ejecución. Se sustituyó el antiguo marcador de texto y el panel con botón «Siguiente ronda». **No se cambiaron escenas ni prefabs** y se reutilizan los retratos actuales. Los paneles nuevos son `Enemy round HUD`, `Player round HUD`, `Round label`, `Clock`, `Round transition`, `Enemy round card` y `Local round card`.

## Verificación

Pruebas de lógica: `round-presentation-flow-tests.cs` y `match-flow-tests.cs`. Prueba de la escena/UI y botones existentes: `local-game-play-tests.cs`. Cubren puntuación, pausa, transición sin UI, empates, tres victorias, conservación de balance, sello sincronizado y siguiente preparación.

La herramienta de Editor `MonsterPouchRoundUIPreview.Preview("all")` captura HUD, resultado local/rival, sello, siguiente preparación y tres banderas finales. `Restore("")` vuelve al menú y restaura la actualización normal. Estas herramientas no guardan escenas, preferencias ni cambios de balance.

Verificación del 26 de septiembre de 2026: **495 pruebas EditMode y 23 PlayMode aprobadas**, incluyendo envío de la malla de las banderas al CanvasRenderer y pruebas de ratón/táctil emulado. Se revisaron capturas reales del HUD, la estampa, ambos ganadores y la tercera victoria. Las pruebas táctiles son emuladas; la entrega es para Windows.

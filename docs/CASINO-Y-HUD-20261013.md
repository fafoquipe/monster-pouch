# Casino y avisos de partida

## Grand Casa Hotel

La corrección posterior del usuario pide un casino inspirado en Balatro también para esta ambientación: tapete verde profundo, burdeos, latón, cartas y fichas, con píxeles más marcados. Se mantienen el carro (shuffler-cart), el reloj/torre (clown-tower), el tablero y su marco originales.

- `Assets/resources/MonsterPouch/Styles/lounge-scenery.asset`: 8 piezas independientes, posiciones de apoyo fijas respecto al tablero.
- `Assets/art/ambientations/casino/casino-table-path.png`: se reutiliza el tapete verde del diseño original, con su tono intacto y un oscurecimiento neutro del 10 %. Una sola capa repetible cubre el área visible, sin estirar la textura ni superponer una franja central.
- `Assets/art/ambientations/modular/casino-accessories-props.png`: bandeja, lámpara, cartas y portamazo, con recortes de Unity en el archivo `.parts.json`.
- Las fichas laterales reutilizan los recursos existentes. Se mantienen los puntos de apoyo de los ocho accesorios conservados; todos tienen rotación 0, con la orientación original de su sprite.
- Se han retirado las líneas de apuestas, los pequeños rombos y las dos franjas verticales exteriores. El marco original del tablero se conserva.
- Las sombras reutilizan el alfa y la silueta del objeto: se proyectan hacia abajo y se oscurecen con un shader específico. Incluyen carro, reloj y dados. El shader recorta cualquier fragmento que entraría al marco/tablero. El casino ya no usa óvalos de contacto. Las fichas respetan el margen transparente del PNG para que la sombra arranque en el apoyo visible. La sombra original del marco se conserva.
- Los dispositivos anchos muestran más tapete libre. Se eliminaron las estaciones repetidas y las fichas dispersas; las piezas conservan su tamaño y punto de apoyo.
- No se modifican las ilustraciones de personajes.

El atlas de accesorios se generó con la herramienta integrada de imágenes. El tapete generado se ha sustituido por el original a petición del usuario; sus píxeles se conservan intactos. Se conserva el conjunto de prompts en `docs/art-prompts/casino-20261013.json`. No se han modificado sus píxeles por scripts; el atlas se recorta dentro de Unity.

## Daño

`Assets/resources/MonsterPouch/DamageNumbers.asset` permite configurar:

| Campo | Valor inicial |
| --- | --- |
| MinimumSize | 22 |
| MaximumSize | 44 |
| DamageAtMinimum | 1 |
| DamageAtMaximum | 100 |
| CriticalEmphasis | 1.12 |

El tamaño aumenta logarítmicamente para que se note la diferencia entre golpes pequeños. Se limita también durante la animación de aparición y en los críticos: un crítico no puede superar el máximo. La cantidad escrita sigue siendo el daño real registrado por la simulación.

## Cuenta atrás

`PhaseCountdownHud` consulta `Match.Remaining`: últimos 10 segundos de preparación y 5 de combate. Usa un número central grande con aparición, rebote suave y salida en cada segundo. No introduce un segundo reloj, no modifica fases y no captura eventos táctiles. En combate baja adicionalmente su opacidad si un luchador coincide con el centro. Se oculta en menús, resultados, pausa y modales.

## Identidad y listo online

El HUD y los resultados toman el nombre recibido por `OnlineRoom`; el paquete de inicio también incluye el nombre del anfitrión. Se muestra como texto literal, con límite de longitud y ajuste al espacio. El check junto al nombre refleja `Match.OpponentReady`, se dibuja con geometría nativa y aparece/desaparece animado. El protocolo de listo y sus revisiones mantienen la autoridad que ya tenían.

## Verificación

Las pruebas de esta revisión están en `Assets/tests/play-mode/casino-hud-play-tests.cs`. Cubren límites de daño, umbrales del reloj, nombre recibido, check, ausencia de bloqueo táctil y separación entre accesorios y tablero. Pasaron las 15 pruebas de casino/HUD, las 30 de integración y las 761 de EditMode. Los informes están enlazados en `VALIDACION-CASINO-HUD-20261014.md`; las capturas de Game View no sustituyen una prueba física ni una conexión Relay entre dispositivos.


## Ajuste del 15 de octubre

Se redujo la decoración de 29 a 8 accesorios, sin giros adicionales. Carro, reloj, dados, tablero, tapete y sombras de silueta se conservan. Las pruebas de distribución se ajustaron al nuevo diseño; los resultados de la sección anterior corresponden a ejecuciones anteriores a este ajuste. No se genera otro APK, por petición expresa del usuario.

La revisión visual posterior está documentada en `VALIDACION-CASINO-HUD-20261014.md`: capturas actualizadas de móvil y pantalla ancha, sin generar otro APK. La comprobación automática de este ajuste quedó interrumpida.

# Ampliación de personajes — 12 de septiembre de 2026

Versión jugable: `Builds/Windows-Roster/Monster Pouch.exe`, dentro del proyecto original `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch`. Conserva todos los archivos de esa carpeta junto al ejecutable. El juego fue abierto y revisado en Windows; quedó en el menú con Anuik seleccionado y Kayon y Stein incluidos en el equipo, junto a los tres Whelps anteriores.

## Comportamiento entregado

- Anuik lanza corazones; una vez por combate se apoya sobre la cabeza durante 0,8 segundos y resucita con el 50 % de la vida máxima efectiva. La ronda espera ese regreso. Se comprobó visualmente la postura y el retorno con 45/90 de vida.
- Tauris lanza mordiscos; cada cuarto impacto acertado elimina a un Monster o Whelp sin reducción por armadura. La habilidad de Anuik puede devolverlo después de esa muerte. El impacto mortal tiene un efecto mayor.
- Kayon tiene 28 de vida y 2 de daño. Invoca cada seis segundos un Dummy temporal de 14 de vida y 2 de daño, con un máximo de dos vivos. Las casillas bloqueadas, ocupadas o reservadas se descartan. Sus invocaciones siguen luchando si Kayon muere, no consumen copias, no se venden y se limpian al cambiar de ronda o partida.
- Stein dispara rayos y añade cuatro de daño cada tercer impacto. Bugui conserva sus estadísticas y recibe animaciones de carga, lanzamiento, recuperación, movimiento, reposo y caída.
- El menú ofrece cuatro Monsters y cinco Whelps. Permanecen las figuritas centradas en los tres compartimentos del Brief, su dibujo cerrado, el banco separado y la venta mediante arrastre.

Los límites y frecuencias no especificados por el usuario son valores iniciales configurables. [Reglas, estadísticas y APIs](expanded-roster-combat.md).

## Arte integrado

335 poses nuevas de personajes: Anuik 60 + 25 de resurrección, Tauris 70, Kayon 60, Stein 60 y Bugui 60. Cada personaje tiene cinco vistas dibujadas y tres reflejadas para completar ocho direcciones. La resurrección de Anuik utiliza dibujos de cabeza, con escala calibrada entre hojas. Además hay 32 cuadros de efectos: cuatro de vuelo y cuatro de impacto para corazones, mordiscos, rayos y magia de Bugui.

Se usó **imagegen integrado**, con fuentes originales preservadas, derivados RGBA y filtrado Point. [Rutas de assets, referencias y prompts exactos](arte-nuevo-resumen.md).

## Validación

| Comprobación | Resultado | Evidencia |
| --- | --- | --- |
| EditMode | 267 aprobadas, 0 fallos | `Logs/local-validation/edit-mode.xml`, final 2026-09-12 08:54:07 UTC |
| PlayMode | 10 aprobadas, 0 fallos | `Logs/local-validation/play-mode.xml`, final 2026-09-12 08:57:02 UTC |
| Compilación Windows | Succeeded, 0 errores, 280.649.091 bytes | `Logs/local-validation/command-84c25376e37f426f939015fb73347157.txt` |
| Resurrección en escena real | Cabeza apoyada y retorno 45/90 | `Logs/local-validation/screenshots/anuik-headstand.png` y salida de Snapshot |
| Ejecutable en Windows | Menú completo; clics de selección de Anuik, Kayon y Stein | `Logs/local-validation/screenshots/roster-menu-native.png` |

Las pruebas PlayMode usan dispositivos sintéticos de Input System a través del EventSystem real, con la vista del juego enfocada. La prueba de presentación de Kayon usa rivales sin daño para observar una invocación completa; los tests del simulador verifican daño, muerte y supervivencia. No se afirma una prueba sobre una pantalla táctil física ni un balance competitivo final.

Las correcciones de la revisión cubren invocación junto a obstáculos, limpieza de resurrecciones al llegar al límite de 40 segundos, aislamiento económico de los Dummys y determinismo con pasos temporales distintos. Unity 6000.3.9f1 permaneció abierto; no se usó otra instancia ni batchmode, y no hubo commits ni publicaciones.

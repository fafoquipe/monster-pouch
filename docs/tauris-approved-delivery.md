# Tauris: diseño del usuario, pies compactos y cuerpo a cuerpo

Revisión del 12 de septiembre de 2026. La imagen final del usuario conserva la cabeza azul grisácea de dos lóbulos suaves, los ojos blancos inclinados y la sonrisa dentada. La última aclaración visual usa dos pies redonditos pegados al torso, sin piernas alargadas, y reduce el tamaño un 25 % (`WorldWidth = 0.75`).

## Cambio jugable

Tauris tiene alcance de **una casilla**. Se acerca al rival antes de morder y no crea un proyectil en vuelo. El efecto de mordida permanece sobre el objetivo al contacto, con efecto mayor para el golpe letal. Conserva 100 de vida, 6 de daño, ataque cada 1,4 s, anticipación de 0,3 s, movimiento cada 0,6 s, formación media, mejora y cuarto impacto acertado letal. La resurrección de Anuik sigue pudiendo devolverlo tras ese golpe.

La fábrica y el asset MatchConfig se actualizan de forma coherente. `MonsterPouchTaurisMeleeSetup.Setup()` modifica exclusivamente AttackRange y la descripción de su habilidad mediante API del Editor. No fue necesario cambiar el sistema de proyectiles: el camino existente distingue los ataques cuerpo a cuerpo por alcance.

## Arte

Retrato y **75 poses** con pies compactos: reposo, desplazamiento, mordida cercana y caída. Cinco vistas dibujadas y tres reflejadas completan ocho direcciones. La hoja v3 actualiza los apoyos inferiores. Se conservan las fuentes y se crean derivados RGBA con filtrado Point y pivote en los pies.

El arte aprobado de Anuik permanece en el catálogo. El tamaño y los apoyos inferiores son cambios visuales: no alteran las casillas ni el combate. La auditoría final confirma 15×5 bandas, 75 poses completas y margen mínimo de 7 píxeles, sin cortes. El sprite frontal visible mide 82×107 píxeles; con escala0,75 ocupa 0,90×1,1744 unidades de mundo, aproximadamente la altura de una casilla y el70,45% de su ancho.

[Archivos, referencias y prompts exactos de imagegen integrado](tauris-user-design.md). [Medidas de importación](tauris-user-import-report.md).

## Verificación

- **269 EditMode aprobadas, 0 fallos**, final 2026-09-12 09:33:30 UTC. Incluyen persecución desde ambos lados, ataque sólo adyacente, vuelo cero y cuarto mordisco letal contra armadura.
- **11 PlayMode aprobadas, 0 fallos**, repetidas con el arte compacto final, final 2026-09-12 09:51:54 UTC. La prueba nueva selecciona a Tauris en la interfaz, comprueba acercamiento, ausencia de `projectile-tauris` y presencia del efecto local de contacto.
- Inspección de la escena: `range=1; distance=1; projectile=False; pose=tauris-approved-r04-f07`. Captura final `Logs/local-validation/screenshots/tauris-compact-melee.png`.

Las pruebas de interfaz usan Input System y EventSystem reales con dispositivos sintéticos; no se afirma prueba física táctil ni balance competitivo final. Unity permaneció abierto y se trabajó con su API del Editor, sin otra instancia, batchmode, commits ni publicación.

## Ejecutable final

`Builds/Windows-Tauris/Monster Pouch.exe`, dentro de `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch`. Conserva toda la carpeta junto al ejecutable.

Compilación final: **Succeeded, 0 errores, 293.228.515 bytes**. Evidencia: `Logs/local-validation/command-28754e36fde648b1a5fb88a915ab516b.txt`. Esta compilación reemplaza la primera versión intermedia de dos piernas largas.

El juego se abrió en Windows y se observó a Tauris compacto en preparación, con los pies cortos y el tamaño reducido. Quedó abierto para jugar. Captura: `Logs/local-validation/screenshots/tauris-compact-native.png`.

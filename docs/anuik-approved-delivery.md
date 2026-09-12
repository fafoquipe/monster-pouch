# Anuik de la referencia del usuario — 12 de septiembre de 2026

El Anuik amarillo con camiseta azul y corazón blanco reemplaza el diseño anterior en el menú y en todas sus animaciones. La imagen proporcionada por el usuario es la referencia principal y se conserva intacta dentro del proyecto.

## Entrega visual

- Retrato transparente con orejas grandes, ojos cerrados, cara sin boca y borde blanco.
- 60 poses de reposo, movimiento, lanzamiento y caída, en cinco vistas dibujadas y tres reflejadas.
- 25 poses de resurrección, con postura de cabeza y escala calibrada respecto a las demás animaciones.
- Filtrado Point, fondos transparentes y pivotes al suelo para mantener el anclaje al tablero.
- El corazón del pecho permanece en el dibujo; los proyectiles se animan desde el sistema de efectos existente.

La habilidad conserva el retorno una vez por combate con el 50 % de la vida. Esta revisión modifica el arte de Anuik y sus referencias en UnitArt; no modifica las estadísticas, el combate, el Brief ni los otros personajes. Tauris conserva su diseño anterior hasta recibir la nueva referencia anunciada por el usuario.

## Fuentes y reproducción

[Referencia, rutas de assets y prompts exactos](anuik-user-design.md). [Prompt de la resurrección](anuik-approved-revival-art.md). [Medidas de importación](anuik-user-import-report.md).

La importación exclusiva se ejecuta mediante `MonsterPouchAnuikUserArtSetup.Setup()` desde el Editor existente. La fuente principal v2 corrige las partículas que unían dos columnas de v1. El análisis de la cuadrícula confirma 12 columnas y 5 filas, 60 celdas no vacías, ningún corte sobre píxeles visibles y margen mínimo de 9 píxeles. Las fuentes se conservan separadas de sus derivados RGBA.

## Verificación

Las 10 pruebas PlayMode existentes pasaron de nuevo después de la importación, con cero fallos (final: 2026-09-12 09:19:11 UTC, `Logs/local-validation/play-mode.xml`). La revisión visual en Unity confirma el retrato aprobado, la postura de cabeza y el retorno a 45/90 de vida. Capturas en `Logs/local-validation/screenshots/anuik-approved-headstand.png` y `anuik-approved-recovered.png`.

La validación anterior de las reglas fue de 267 pruebas EditMode aprobadas; esta revisión no cambió esas reglas. [Entrega anterior de personajes y habilidades](expanded-roster-delivery.md).

## Versión jugable

`Builds/Windows-Characters/Monster Pouch.exe`, dentro del proyecto original `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch`. Conserva todos los archivos de la carpeta junto al ejecutable.

Compilación Windows correcta: **Succeeded, 0 errores, 286.936.995 bytes**, en `Logs/local-validation/command-395be80f5e104077a88c48a673740a7d.txt`. El ejecutable se abrió en Windows y muestra el Anuik aprobado, seleccionado junto a los cinco Whelps. Captura: `Logs/local-validation/screenshots/anuik-approved-menu-native.png`.

Unity 6000.3.9f1 permaneció abierto. No se utilizó otra instancia ni batchmode, y no hubo commits ni publicaciones.

# Monster Pouch

Prototipo local de combate automático en pixel art, hecho con **Unity 6000.3.9f1**. Prepara un Monster y sus Whelps contra un bot y consigue tres victorias de ronda.

## Abrir desde un clon

```sh
git clone git@github.com:fafoquipe/monster-pouch.git
cd monster-pouch
```

En Unity Hub, añade esta carpeta y ábrela con la versión indicada en `ProjectSettings/ProjectVersion.txt`. Abre `Assets/Scenes/main-scene.unity` y pulsa Play. Los catálogos, sprites, fuentes y metadatos necesarios ya están incluidos; no hace falta ejecutar los importadores de arte para jugar.

Los recursos binarios se versionan directamente en Git. Este historial no utiliza objetos de Git LFS; `.gitattributes` marca estos archivos como binarios sin filtros LFS.

## Estado actual

- Brief de tres espacios con apertura/cierre, compra por arrastre, venta al devolver al Brief y banco independiente de un espacio.
- Despliegue de cinco filas por seis columnas por bando.
- Objetivos persistentes hasta derrotar al rival; la resurrección de Anuik libera a sus atacantes.
- Bugaloo, Popow, Anuik y Tauris; Whelps Dummy, Bugui, Atori, Kayon y Stein.
- Diseños del usuario para Anuik, Tauris, Stein y Kayon, con animaciones de reposo, movimiento, ataque y caída.
- Kayon lanza monedas de oro con alcance4 e invoca hasta dos Dummys temporales. Stein dispara rayos; Anuik revive una vez con la mitad de su vida.

La [guía de juego](docs/JUGAR.md) describe los controles y el balance. [Cambios y validación](docs/gameplay-user-designs-delivery.md), [reglas](docs/expanded-roster-combat.md) y [arte, referencias y prompts](docs/arte-nuevo-resumen.md).

## Compilar y comprobar

Para Windows, abre **File > Build Profiles**, selecciona Windows, incluye `Assets/Scenes/main-scene.unity` y compila en una subcarpeta de `Builds`. Conserva todos los archivos generados junto al ejecutable. Las distribuciones, cachés y registros no se incluyen en Git.

Las pruebas están en `Assets/tests/edit-mode` y `Assets/tests/play-mode`, disponibles desde el Test Runner de Unity. Última validación del juego: **297 EditMode y 13 PlayMode aprobadas**, con compilación Windows sin errores.

Las rutas absolutas y capturas citadas en informes históricos corresponden a la máquina de desarrollo; los archivos del proyecto se encuentran por sus rutas relativas dentro de este repositorio.

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

- Catálogo documentado: 5 Monsters y 14 Whelps, energía y tres mejoras por figura.
- Inicio, colección y tres briefs locales guardados, con un Monster y hasta siete Whelps cada uno.
- Brief de tres ofertas, apertura/cierre, arrastre para comprar/vender y banco independiente.
- Cinco filas por seis columnas de despliegue por bando. La ronda termina al eliminar al equipo, con excepciones de resurrección y las habilidades de los PDF.
- Diseños originales del usuario; animaciones existentes conservadas y movimiento procedimental para personajes nuevos.
- Distribución de partida actualizada y referencias visuales del maletín y los iconos reparadas.

Consulta [la entrega de septiembre](docs/september-delivery.md), [las habilidades de los PDF](docs/pdf-combat-reference.md) y [el balance provisional](docs/documented-combat-tuning.md). Los informes anteriores describen versiones históricas.

## Compilar y comprobar

La versión actual se genera en `Builds/Windows-September/Monster Pouch.exe` desde **Monster Pouch > Build > Windows September**. Para compilar manualmente, abre **File > Build Profiles**, selecciona Windows, incluye `Assets/Scenes/main-scene.unity` y compila en una subcarpeta de `Builds`. Conserva todos los archivos generados junto al ejecutable. Las distribuciones, cachés y registros no se incluyen en Git.

Las pruebas están en `Assets/tests/edit-mode` y `Assets/tests/play-mode`, disponibles desde el Test Runner de Unity. Última validación del juego: **350 EditMode y 15 PlayMode aprobadas**, con compilación Windows sin errores.

Las rutas absolutas y capturas citadas en informes históricos corresponden a la máquina de desarrollo; los archivos del proyecto se encuentran por sus rutas relativas dentro de este repositorio.

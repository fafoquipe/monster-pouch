# Monster Pouch — actualización del 14 de septiembre

## Contenido

- Catálogo de los PDF: 5 Monsters y 14 Whelps, ataques, energía, habilidad base y tres mejoras por personaje. Las cifras que los PDF no especifican quedan como balance provisional editable; ver `pdf-combat-reference.md` y `documented-combat-tuning.md`.
- Inicio con el Monster elegido, colección y tres briefs independientes guardados localmente. Cada brief contiene un Monster y hasta siete Whelps; se conservan cinco filas de despliegue por lado.
- Diseños suministrados en `pictures/gogos-minis`. Los personajes existentes conservan sus animaciones de combate; los nuevos usan respiración, desplazamiento, ataque y caída procedimentales sobre sus diseños. No se generaron personajes adicionales.
- Maletín más abajo, recursos y banco lateral, sin instrucciones fijas debajo del tablero. Sprites del brief, banco y monedas reconectados con Unity.

## Recuperación

El código versionado coincidía con `1160411`. Se conservó la carpeta recuperada y la APK anterior. El editor estaba mostrando el prefab del hotel sin la escena jugable. Se abrió `Assets/Scenes/main-scene.unity`, se corrigió la asociación de `LocalGameUI.cs` conservando su GUID y se reimportaron las referencias visuales.

Solo se genera Windows por indicación del usuario. Los comandos reproducibles están en `MonsterPouchSeptemberSetup`: abrir escena, aplicar contenido, reparar referencias y compilar Windows. No ejecutar los antiguos importadores de balance sobre el catálogo documentado.

## Arte de inicio

Herramienta integrada de generación de imágenes, usada una sola vez para extraer un fondo limpio de `principal.png`. Archivo final: `Assets/resources/MonsterPouch/UI/home-background.png`. La estantería usa el `decks.png` original con controles y personajes interactivos superpuestos.

Prompt: Edit the supplied portrait pixel-art main-menu reference into a clean reusable background plate. Preserve the 9:16 framing, blue sky, pixel clouds, floating grassy islands, left waterfall, right blue-roof castle, torches, stairs and central blue/silver pedestal. Remove all UI overlays, currencies, buttons, navigation, text, nameplate, stars and the Bugaloo character. Reconstruct the scenery behind them. Leave empty air above the pedestal for a runtime animated Monster. Same colorful pixel-art style, no characters, text or UI in the output.

La partida continúa siendo local contra bot. Las pestañas del menú ofrecen funciones disponibles; no simulan compras, cuentas ni servicios en línea.

Validación del 14 de septiembre: 350 EditMode y 15 PlayMode aprobadas. Resultados completos en Logs/local-validation/edit-mode.xml y play-mode.xml.

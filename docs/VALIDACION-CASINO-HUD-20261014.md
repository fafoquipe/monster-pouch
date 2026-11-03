# Validación de casino, sombras y avisos

## Estado del 15 de octubre

El usuario ha cancelado la generación de otro APK. La distribución del casino se ha simplificado a ocho accesorios sin inclinación, y se han retirado líneas y franjas exteriores. Los informes de abajo son históricos. Se revisaron las capturas actuales a 1920 × 1200 y 1080 × 2340: `Logs/local-validation/screenshots/casino-sparse-1920x1200-20261015.png` y `casino-sparse-1080x2340-20261015.png`. Muestran ocho accesorios, orientación original y tapete sin líneas, conservando carro, reloj y sombras.

La nueva prueba automática de distribución no produjo un resultado válido: hubo cambios de modo de juego durante su ejecución. No se contabiliza como aprobada ni se reutiliza el resultado anterior de `focused-play.xml`, que pertenece a otros tests. El editor informó `compilationFailed=False` después del cambio.

El primer APK sí terminó el 14 de octubre: `Builds/Android/2026-10-14-casino-hud/build-report.json` registra Succeeded y restauración de ajustes/plataforma. Los seis atlas posteriores ya se aplicaron al proyecto; no se crea un paquete nuevo con esos cambios.

## Resultados ejecutados en Unity

| Grupo | Aprobadas | Fallos | Informe |
| --- | ---: | ---: | --- |
| Casino, límites de daño, contador, nombre y check | 15 | 0 | Logs/local-validation/casino-hud-final.xml |
| Tres mapas, muebles, temas, viewport y lógica online | 30 | 0 | Logs/local-validation/maps-ui-network-final.xml |
| EditMode completo: lógica y persistencia | 761 | 0 | Logs/local-validation/edit-mode-casino-final.xml |
| Total de estas ejecuciones | 806 | 0 | |

Las ejecuciones finales no omitieron pruebas. La primera ejecución detectó que abrir el menú online dejaba visible el contador hasta la siguiente actualización. Se corrigió: la asignación de modal oculta el contador inmediatamente, y la prueba lo verifica antes de esperar un fotograma.

Un cambio reciente del mueble del Brief trasladó su paleta al material de `LobbyFurnitureDefinition`. La prueba de temas se actualizó para verificar el sprite, tinte y saturación configurados de cada mueble. No se sustituyó ese cambio de presentación por el mecanismo anterior.

Durante una repetición Unity dejó de responder. Se guardó la escena y la copia de recuperación en `Backups/editor-recovery-20261014-000040`, se reinició el editor y se ejecutaron nuevamente los grupos de validación. La ejecución interrumpida no cuenta como aprobada.

## Revisión visual

Capturas reales de Game View a 1080 × 2340, 1600 × 2560 y 1920 × 1200:

- `Logs/local-validation/screenshots/casino-table-1920x1200.png`: tapete verde original, accesorios asimétricos y sombras de silueta fuera del marco.
- `Logs/local-validation/screenshots/casino-table-1080x2340.png`: encuadre móvil conservando tablero, carro y reloj.
- `Logs/local-validation/screenshots/casino-opponent-ready.png`: nombre recibido y check animado, con un rival simulado para reproducir el estado.
- `Logs/local-validation/screenshots/countdown-preparation-10.png`, `countdown-preparation-3.png`, `countdown-combat-5.png`: estados del contador central.
- `Logs/local-validation/screenshots/final-restored-<resolución>-<classic|lounge|royal>.png`: los tres mapas en los tres tamaños.

Se revisaron directamente las vistas de casino en móvil y ancha, parque y Campo Real en ancha, y los avisos de partida. Los tests complementan estas capturas comprobando anclajes, texturas, exclusión del marco, selección de temas y bloqueo táctil.

## Archivos y texturas

`Logs/local-validation/casino-revision-files.json` registra rutas, tamaños y SHA-256 de 31 archivos centrales de esta revisión. Los prompts de accesorios generados se conservan en `docs/art-prompts/casino-20261013.json`.

La auditoría `docs/texture-audit-20261013.csv` se actualizó a 136 texturas de UI/entornos: ninguna con mipmaps o Read/Write. 16 usan override ETC2_RGB4 y 12 ETC2_RGBA8; las otras conservan compresión automática.

Se detectó además un ajuste de atlas V2 ignorado por la API antigua: el parque conservaba filtrado bilineal y empaquetado con rotación. El configurador ahora aplica Point, sin rotación/tight packing y ETC2_RGBA8 en el importador. Se añade `SceneryAtlasImportTests` para verificar estos ajustes después de reimportar. API de referencia: [SpriteAtlasImporter de Unity](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/u2d/spriteatlasimporter).

### Atlas compilado para Android

Se leyó directamente el archivo serializado producido por Unity para Android, sin decodificar ni modificar la imagen. El atlas del parque contiene una textura de 2048 × 2048, formato 47 (ETC2_RGBA8), filtro 0 (Point), un solo nivel y Read/Write desactivado. El SHA-256 del archivo es `a32d9ff283f38e5e7063d4b038633df27dc177ca52b009f2462892f31cfec9f2`.

Evidencia: `Logs/local-validation/apk-inspection/android-park-atlas.json`. El lector reproducible está en `BuildTools/inspect_android_package.py`, usa UnityPy 1.25.4 y conserva sus dependencias locales fuera de Assets. La comparación con la entrada del APK se hará cuando el empaquetado termine.

### Diferencia entre configuración y contenido real

La inspección del primer build mostró piezas nuevas como `brief-lounge-cabinet-v2` (1774 × 887) y `furniture-props` (1254 × 1254) en RGBA32, aunque su override Android solicitaba ETC2. Los fondos cuadrados se serializaron en RGB24. Por tanto, el CSV de importadores describe la configuración solicitada y no demuestra por sí solo que se haya aplicado compresión GPU.

`MonsterPouchSceneryPacking.Apply()` prepara seis atlases V2 para muebles y decoración, con padding, filtro Point y sin rotación ni tight packing. Reutiliza las referencias de los sprites originales: no reescala imágenes ni modifica posiciones o recortes. El empaquetado y su nueva verificación se ejecutarán al terminar el build activo; esa primera compilación todavía no incluye estos seis atlases. Los PNG de referencia se registran en `Logs/local-validation/apk-inspection/scenery-source-hashes-before-packing.json`.

Los fondos de Resources y suelos se conservan en esta revisión; no se presupone compresión efectiva de ellos. El inventario provisional de datos serializados creció de 912 473 030 a 970 859 888 bytes antes de este segundo empaquetado. Estas cifras son datos sin la compresión ZIP del APK, no tamaño de descarga ni RAM simultánea.

## Android

Solicitud guardada en `BuildTools/AndroidCasinoHud20261014.json`. Compilación universal ARMv7 + ARM64, IL2CPP, sin Development, con identificador y firma actuales conservados. El resultado se registrará en `Builds/Android/2026-10-14-casino-hud/build-report.json`.

Referencia comparable anterior: 356 721 667 bytes, mismo motor y arquitecturas. La nueva compilación está en curso; todavía no se informa una reducción de tamaño.

## Límites de esta comprobación

ADB no detectó dispositivos conectados. No se ha medido FPS, RAM o entrada táctil en un Android físico, ni establecido una partida Relay entre dos dispositivos. Las pruebas del protocolo local y los rivales simulados no sustituyen esas comprobaciones.

El estado del encargo amplio y sus pendientes está en `REDISENO-INTEGRAL-20261013.md`. No se incluyen rediseños nuevos de personajes: los aportará el usuario.




# Limpieza de recursos y trabajo por fotograma — 3 de octubre de 2026

Aplicada desde la conversación lateral, sin enviar comandos al editor Unity.

## Recursos retirados

- Retirados de `Assets/resources/MonsterPouch/UI`: `LobbyArt` y `LobbyFrames`, las dos versiones anteriores de animaciones, con sus metadatos.
- Retiradas las clases sin consumidores `LobbyArtwork` y `LobbyFrameLayout`, con sus metadatos.
- Total: 66 archivos, 29 PNG, **51,53 MiB de archivos fuente**. Esta cifra no es una medición del tamaño final del APK.
- La búsqueda de GUIDs en los archivos restantes de Assets no encontró dependencias de los recursos retirados. Las referencias a sus rutas solo pertenecían al código retirado y a documentación histórica.
- Conservados los recursos activos `LobbyGestures`: 19 personajes, sus atlas e idles originales. No se modificaron sus imágenes ni los archivos de trabajo de las nuevas animaciones.

## Trabajo innecesario eliminado

- `DocumentedBattleEffects`: reutiliza las colecciones de retirada y de efectos del suelo; evita copiar diccionarios con `ToArray` en cada actualización.
- El texto del escudo se actualiza al cambiar el estado, sin crear la misma cadena cada fotograma.
- Elimina referencias a actores destruidos de los registros de disparos y limpia el registro de rocas al cambiar de simulación.
- Los rayos conservan sus dos capas y no crean un array por segmento.
- `UnitVitalsUI`: solicita una reconstrucción del dibujo únicamente cuando cambia la vida, la energía, la estela de daño, las estrellas, el equipo o las mejoras. La geometría de las barras permanece igual; los cambios de tamaño del RectTransform siguen gestionados por Unity UI.

## Comprobaciones y recuperación

Compilación independiente de `MonsterPouch.Local` con el compilador y referencias de Unity 6000.3.9f1: correcta. Se comprobaron hashes antes de editar para evitar sobrescribir trabajo simultáneo. No se ha ejecutado Play Mode ni medido FPS/GC en un dispositivo Android desde esta conversación.

Respaldo de los archivos retirados y las dos fuentes anteriores:
`C:/Users/HP VICTUS/.codex/visualizations/2026/10/03/01a1022c-0613-7721-8e05-4d991de0c764/backup`

Los manifiestos `cleanup-plan.json` y `vitals-plan.json`, en la misma carpeta, conservan rutas y hashes. El respaldo está fuera de Assets y no se incluye en el juego.

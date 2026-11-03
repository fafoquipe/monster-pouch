# Pruebas históricas retiradas de la batería activa

El creador indicó el 20 de octubre de 2026 que los comportamientos antiguos ya no deben utilizarse (MP-DEC-029).

Los cuatro fixtures PlayMode exigían reproducir gestos de menú descartados, catálogos de 19/20 personajes o al menos 24 fotogramas por gesto. Tres activaban expresamente `LobbyAnimationDirector.PlaybackEnabled`; el cuarto cargaba directamente sus atlas. La presentación vigente es estática y el catálogo contiene 22 personajes.

También se retiró `lobby-juggling-stones-tests.cs` de EditMode (40 casos de reproducción de gestos antiguos). De `lobby-gesture-timing-tests.cs` se conserva aquí una copia anterior: se retiró solo el caso que exigía temporización de esos gestos. Su prueba de movimiento actual del tablero sigue activa como `CurrentTileHopProfileTests`. En total son 10 casos PlayMode y 41 EditMode exclusivos de la función descartada; no se retiraron pruebas de combate aplicables para ocultar fallos.

Se conservan las fuentes y sus metadatos como `.txt`, fuera de `Assets`, junto a sus hashes en `manifest.json`. Unity no los compila ni ejecuta. No se movieron sprites, personajes, escenas ni prefabs.

La cobertura vigente está en:

- `CharacterPreviewPlayTests`: todos los personajes, retratos inmóviles tras tiempo/toques, navegación, Detalle/Usar y mazos conservados.
- `LobbyIdleArtworkPlayTests`: recursos actuales de Home, Brief y nuevos Whelps; cambio de contexto sin cargar gestos antiguos.
- `LobbyFrameGraphic.Configure`: la configuración predeterminada carga únicamente el retrato estático.

Retirar estos tests no significa que los demás fallos de la batería general estén resueltos. Los casos de combate, interacción y layout que siguen siendo aplicables permanecen activos.

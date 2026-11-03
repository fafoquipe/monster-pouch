# Áreas, provocación y bordes de texturas

## Efectos durante el combate

- Gochan lanza un sello dorado con la espiral de su sombrero. Al impactar, aparecen fuegos sobre la cruz afectada; su súper y la mejora de área completa muestran las nueve casillas correspondientes, recortadas por el borde del tablero.
- Jazzar lanza una gota verde viscosa: cuerpo con brillo húmedo, cola que se estira y salpicadura al impactar. Mantiene el daño, la contaminación y la velocidad de proyectil configurados. Su nombre visible se corrige a Jazzar, conservando los identificadores internos para mantener las partidas guardadas.
- Popow conserva su velocidad configurable y muestra una estela curva de gancho, borde naranja, núcleo claro, llamas y la ruta de su embestida.
- La mejora Impacto de Retorno de Anuik muestra una explosión rosa, corazones y las casillas afectadas cuando revive. No agrega daño ni explosiones a Anuik sin esa mejora.
- Los ataques de área de Jazzar, Aky y Tauris también marcan las casillas que realmente afectan.
- El suelo usa el material sin iluminación del juego para conservar los colores en este tablero. Todos los efectos se pausan con la partida y se limpian al salir.

## Taunt de Bugaloo

La provocación toma una sola instantánea al activar el súper. Afecta a enemigos elegibles que están dentro del radio en ese instante; entrar después no provoca a una unidad. El pulso rojo dura un segundo y no constituye daño periódico. Los afectados muestran un símbolo rojo de enfado y un tinte rojo hasta que termina su estado.

En el inspector de `MatchConfig`, Bugaloo → Habilidad propia:

- `radius`: alcance de la provocación.
- `tauntDuration`: segundos de provocación de los enemigos alcanzados.
- `duration`: segundos de reflexión del súper; es independiente de la provocación.

Los archivos anteriores sin `tauntDuration` heredan su `duration` personalizado. El proyecto conserva inicialmente los cinco segundos configurados; editar el nuevo campo fija una duración independiente. Cero desactiva el estado de provocación. Muerte, resurrección, reinicio o pérdida válida del provocador limpian el estado y su indicador.

## Texturas

La auditoría recorre las texturas referenciadas por la escena, los catálogos de personajes/proyectiles y los recursos de UI. `MonsterPouchPixelImportGuard` corrige los residuos de croma en la caché de importación de derivados identificados por su fuente. Conserva los PNG originales, GUID, pivotes y recortes; no elimina indiscriminadamente los colores violetas de personajes y efectos. También corrige filtrado, mipmaps, compresión y límites de resolución incorrectos encontrados en esa auditoría.

`MonsterPouchPixelImportGuardChecks.RunChecks()` verifica preservación de colores y cobertura. `MonsterPouchPixelImportGuard.Apply()` comprueba hashes de PNG y genera `Library/MonsterPouch/pixel-import-audit.md` con lectura real de la textura importada. `MonsterPouchTextureEdgeContactSheet.Capture()` genera una comparación ampliada sobre fondos gris y verde.

## Validación

El registro final de pruebas y compilación se encuentra en `Logs/local-validation`. Las capturas `area-gochan-*`, `area-popow-*`, `area-anuik-*` y `area-taunt-*` provienen de partidas temporales de comprobación que no guardan cambios de estadísticas.

Validado el 26-09-2026: 507 pruebas EditMode y 26 PlayMode aprobadas; 12 comprobaciones del importador aprobadas; 94.455 píxeles de caché corregidos con 32 PNG intactos. Compilación Windows: Succeeded, 0 errores, 0 advertencias. Capturas de Gochan, Jazzar, Popow, Anuik y taunt revisadas visualmente.

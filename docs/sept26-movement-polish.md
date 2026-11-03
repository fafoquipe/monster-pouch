# Correcciones de Tokoro y presentación — 26 septiembre 2026

- Los helados se consumen al aparecer bajo una unidad válida: curación aliada o debilidad enemiga. Los que aparecen en casillas vacías esperan un ocupante. No se acumulan ni se consumen dos veces.
- El dibujo procedural del helado ahora tiene una bola de fresa y un cono triangular con trama de barquillo. El chupete de Tsu conserva su diseño propio.
- Bugaloo usa pasos mirando al rumbo correcto. La corrección del espejo se aplica exclusivamente al caminar, sin cambiar idle, ataques o Súper.
- Las caminatas incluyen transferencia leve de peso, balanceo y compresión, y mantienen el ciclo entre casillas. `UnitArt.WalkWeight` controla su intensidad.
- Las sombras son óvalos oscuros sencillos sobre el tablero, anclados al suelo y ocultos al morir. Incluyen Monsters, Whelps e invocaciones. Antes compartían orden de dibujo con el tablero y podían quedar ocultas.
- Aumentos de `WorldWidth`: Flo .78→.94; Tokoro .90→1.02; Blotan .85→.95; Aky .82→.92; Atori .88→.95; Atong .72→.77; Tauris .75→.82. No se redujo ningún personaje.
- El brief usa `Assets/resources/MonsterPouch/UI/deck-background-no-whelp-lights.png`. Los siete focos de Whelps desaparecen; el foco del Monster permanece. El fondo original está conservado.

## Arte del fondo

Modo: herramienta integrada `image_gen`, edición del fondo existente. Resultado importado como sprite sin compresión, filtrado Point, sin mipmaps.

Prompt utilizado:

> Precise-object-edit for existing pixel art Unity game background. Edit target is the attached tall blue and gold display cabinet image. Remove ONLY the seven yellow spotlight cones and the seven small glowing ceiling light fixtures inside the bottom two shelves (four on middle shelf, three on bottom shelf). Replace those light beams with continuous warm orange cabinet back walls matching the surrounding unlit wall pixels. Preserve EVERY other feature and location: top Monster podium and its large overhead yellow spotlight MUST stay untouched; all 7 Whelp pedestals at exactly same positions, cabinet edges, shelves, posters, potted plant, boxes, Monster horn emblem, lettering, navigation bottom buttons and sky. Keep original portrait proportions and framing, no crop, no new objects, no blur. Crisp unchanged pixel-art style. This is a minimal retouch, not a redesign.

## Verificación

Pruebas de Tokoro: ocupantes aliados/enemigos de ambos equipos, debilidad efectiva, entrada posterior, consumo único y generación repetida.

Pruebas de movimiento: orientación, espejo exclusivo de caminar, continuidad entre casillas y sombras de Monsters, Whelps e invocaciones. Se conserva la comprobación de precisión al terminar un paso.

Los resultados actualizados se guardan en `Logs/local-validation/edit-mode.xml`, `play-mode.xml` y `directional-build.txt`. Las capturas de revisión están en `Logs/local-validation/screenshots/sept26-*.png`.

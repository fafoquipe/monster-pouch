# Anuik: diseño elegido por el usuario

El usuario rechazó el diseño anterior y proporcionó la imagen amarilla con camiseta azul. Su imagen prevalece sobre las referencias del álbum usadas antes. Se conserva en `Assets/art/units/generated/anuik-approved/anuik-user-reference.png`.

Rasgos: cabeza dorada amplia, orejas grandes dirigidas hacia arriba, ojos cerrados negros, cara sin boca ni nariz, camiseta azul con corazón blanco, manos amarillas y borde blanco en píxeles.

La revisión modifica únicamente el arte de Anuik. Sus corazones, resurrección una vez por combate, retorno al 50 % y tiempos permanecen en el modelo existente. Tauris conserva su arte actual hasta recibir la imagen que el usuario anunció.

## Archivos y generación

Herramienta utilizada: **imagegen integrado**. Las fuentes generadas y la referencia quedan intactas en el proyecto.

- Retrato: `anuik-approved/anuik-portrait-source-v1.png` y derivado `anuik-portrait-rgba-v1.png`.
- Hoja de 60 poses: `anuik-approved/anuik-animation-source-v2.png` y derivado `anuik-animation-rgba-v2.png`. Se conserva la fuente v1, reemplazada por v2 para separar los efectos de los personajes.
- Resurrección de 25 poses: `anuik-approved/anuik-revive-source-v1.png` y derivado `anuik-revive-rgba-v1.png`. [Prompt de resurrección](anuik-approved-revival-art.md).
- Todas las rutas anteriores están bajo `Assets/art/units/generated/`.
- Importación: `MonsterPouchAnuikUserArtSetup.Setup()`, exclusiva de esta entrada de UnitArt. Filtrado Point, transparencia real, anclaje al suelo y calibración de escala entre hojas.

## Prompt de las animaciones

Use case: identity-preserve / game sprite animation. Input image is the user's APPROVED Anuik design and the ONLY identity reference. Create a precise game-ready sprite sheet of this EXACT character, 12 equal columns by 5 equal rows, exactly 60 isolated poses. Pixel-art fidelity is essential: very chunky square pixel clusters like the reference (approximately 96 native pixels across the ear span), NO smooth vector outlines, no watercolor, no realistic rendering. Preserve the GOLDEN YELLOW head and huge angular ears pointing outward/upward, the exact wide flattened round-square head shape, happy CLOSED BLACK CHEVRON eyes, NO MOUTH and NO NOSE, BLUE short-sleeved shirt and WHITE HEART on chest, yellow hands tucked beside shirt, extremely squat body, white pixel outline and gold/blue pixel shading. Do not reinterpret as orange, peach or gray; no new face. Uniform pure magenta #FF00FF backdrop separated from sprites; alternatively truly transparent alpha. No black background, no labels, no grids. All 60 sprites same body size and each entire ear span safely within 65% of its cell width, generous equal empty gutters at least 24px, never clipped. Rows top to bottom are front/South, front-right/SouthEast, right/East, back-right/NorthEast, back/North. The blue shirt is plain on its back; heart only appears on the front. Columns 1-2 idle breathing poses; 3-6 four genuinely different walking steps with subtle yellow foot glimpses and body bob while retaining tiny squat proportions; 7-9 ranged HEART CAST anticipation, hands push forward at release, recover (do not make these death poses); 10-12 death crouch, side topple, lying down. Each row remains in its specified facing during attacks. First frame MUST look like the attached image scaled down faithfully, not a new design. Keep head/shirt/ear proportions consistent across all actions. Transparent or magenta background only, no floor shadows, no floating detached decorations outside the poses.

## Corrección de la hoja principal

Referencia: la hoja v1. Se eliminan efectos separados del cuerpo porque los proyectiles ya se dibujan en el juego.

Precise editing of this existing 12-column by 5-row Anuik animation sprite sheet. KEEP EVERY CHARACTER PIXEL, POSE, PROPORTION, COLOR, CLOTHES, WHITE OUTLINE, POSITION, CELL SPACING AND CANVAS SIZE EXACTLY UNCHANGED. Only remove the floating white/pink HEART PROJECTILES and detached sparkles outside the characters near the casting columns (especially around the eighth column); replace those detached VFX with the exact uniform magenta #FF00FF background. Retain the WHITE HEART PRINT ON EVERY BLUE SHIRT, the yellow hands and the character itself. The game renders projectile effects separately, so the attack poses should show the character's hands only, with NO heart being held and no detached particles. Do not move, recompose, redraw or resize any character. Do not add columns or rows. The purpose is clean completely empty vertical magenta gutters between every one of the 12 columns. All 60 original poses remain. Magenta RGB background, no transparency checkerboard.

## Prompt del retrato

Use case: background-extraction. Edit the supplied image ONLY to remove the black background and return a truly transparent PNG alpha background. Keep Anuik EXACTLY as drawn, including every visible golden-yellow pixel, the original enormous upward ears, closed black eyes, NO mouth, blue shirt, white heart, small hands, white pixel outer border, chunky pixel grid, proportions, pose, highlights and texture. This is an exact cutout task, NOT a redesign or new drawing. Do not add feet, mouth, shadows, smoothness or new outlines. Preserve the original full image resolution and placement, with transparent pixels replacing only the black outside the character. The dark eyes must remain opaque.

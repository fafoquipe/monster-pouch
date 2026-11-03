# Anuik: texturas y contorno negro

Fecha: 2026-10-08

Se renovaron el retrato, las 60 poses de combate y las 25 poses de resurrección. Se conservan la cabeza amarilla, la camiseta azul y el corazón blanco. La fotografía azul se usó únicamente como referencia de forma. El tamaño en partida aumenta aproximadamente un 19,4 %, de manera uniforme; se desactivaron las deformaciones de escala de este personaje.

La animación nueva de mostrar el corazón fue rechazada por el usuario y NO se incorporó. Se conserva el gesto anterior del lobby.

## Archivos de arte incorporados

- Assets/art/units/generated/anuik-black-outline/anuik-portrait-v3.png
- Assets/art/units/generated/anuik-black-outline/anuik-combat-v3.png
- Assets/art/units/generated/anuik-black-outline/anuik-revive-v3.png

Los PNG generados se conservan sin procesar sus píxeles. Dos materiales específicos de Anuik descartan el residuo de transparencia inferior a 0,5 y dibujan un contorno negro interior de un texel. El material de UI conserva recorte y stencil. Se aplica también al anticipo del rival y al retrato; los otros personajes mantienen sus materiales.

## Validación

- Dos pruebas Play Mode aprobadas; cero fallos.
- Revisión del renderizado de 86 dibujos distintos: cero píxeles exteriores blancos o de color; todas las poses resultan visibles.
- Escala uniforme verificada en ocho direcciones, ataque y resurrección.
- El catálogo de arte solo cambió en la entrada de Anuik; los datos de balance se conservaron.
- No se generó una compilación Android.

Resultados: Logs/local-validation/anuik-contour-play.xml. Capturas nativas: Logs/anuik-contour-validation/anuik-borde-fondo-claro.png, anuik-borde-fondo-oscuro.png, anuik-en-partida.png y anuik-resurreccion.png.

## Prompts utilizados

### Retrato

Use case: precise-object-edit. Asset: polished ANUIK character idle portrait, transparent PNG for the pixel-art game Monster Pouch. Image 1 (existing yellow game portrait) is the EDIT TARGET and authoritative COLOR reference. Image 2 (photo of blue plastic toy) is ONLY a SHAPE/PROPORTION reference, never a color reference. Keep the character GOLDEN YELLOW head, ears, hands and tiny feet, BLUE shirt, WHITE heart on chest, BLACK closed smiling arc eyes, NO mouth. Do not turn the head blue or change the existing palette. Refine the silhouette to the toy mold: broad slightly bowl-shaped head, wide thick swept-up ears continuous with the upper sides, compact short flared torso, short natural arms. Preserve recognizable Anuik, no extra anatomy, no oversized forehead, no tall rabbit ears. Improve texture and shading into clean polished pixel-art clustered highlights, subtle smooth toy volume with coherent 3-4 shades, beautiful readable at small size, no noisy speckles, no blurry gradients. Replace ALL white exterior halo with a continuous BLACK contour 2-3 logical pixels thick, including ears, shirt, hands, feet. White is allowed ONLY inside the chest heart and tiny internal specular details, never exterior. Centered front-facing whole-body neutral idle, fixed grounded feet, no stretching, no detached parts, no floor shadow. Transparent background, no matte, no fake checkerboard, no magenta, no text. Large full body occupying 82% canvas width, sufficient transparent margins.

### Combate

Use case: precise-object-edit. Asset: ANUIK combat animation SPRITE ATLAS, exact 12 columns by 5 rows (60 sprites), wide canvas approximately 2400x1000. Image 1 is the EDIT TARGET and authoritative frame layout/action/direction reference. Image 2 is the approved new character model: exact GOLDEN YELLOW head/ears/hands/feet, BLUE shirt with WHITE heart, black happy closed eyes, no mouth, swept-up broad ears, same compact undistorted toy proportions, and BLACK outline. Repaint EVERY sprite of image1 to match image2's improved clean pixel art, smoother intentional color clusters and continuous BLACK exterior contour. Remove ALL white outline and ALL magenta background. Real transparent alpha behind all sprites. Maintain EXACTLY all 12 columns and all 5 rows, clearly separated generous transparent gutters, no labels. Row1 front; row2 front-right 3/4; row3 profile facing right; row4 back-right 3/4; row5 straight back. Within EACH row: cols1-2 tiny idle breath, cols3-6 four short walking steps with same stable head, cols7-9 attack prepare hands together / release from chest / return, cols10-12 death leaning then falling on side then lying down. Keep each view facing exactly as source, keep every pose in corresponding cell, keep consistent character dimensions across frames except intentional rotation in death poses. Keep body widths, head shape, ear lengths, shirt, heart consistent, NO squash/stretch deformation, no detached limbs. Front head ear span ~75% cell width, always safely inside own cell. Distinct crisp BLACK 2-3 logical pixel outline on every silhouette including death poses. White remains only inside chest heart or small internal highlights. Preserve yellow/blue palette, no blue skin. Do not add a white sticker edge, floor shadows, glow, particles, frames, text or extra characters.

### Resurrección

Use case: precise-object-edit. Image1 is edit target: ANUIK revive sprite atlas, EXACTLY 5 columns x 5 rows, 25 poses. Image2 is new definitive character appearance. Repaint image1 matching image2's GOLDEN YELLOW head/ears/limbs, BLUE shirt and WHITE chest heart, closed BLACK arc eyes, no mouth, wide upward ears, compact unchanged proportions, crisp clean pixel art and continuous BLACK outline. Keep EXACT frame count, action order, row direction and pose placements. In every row: column1 lying on side; column2 inverted headstand; column3 inverted headstand legs moving gently; column4 rolling onto feet; column5 upright neutral idle. Row1 front, row2 front-right, row3 right profile, row4 back-right, row5 back. Keep SAME character size throughout, rotation rather than stretching, no reshaped heads, no additional limbs. Improve soft toy-volume shading with a few deliberate pixel clusters, preserve yellow/blue palette exactly. True transparent background with generous empty gutters. Eliminate magenta and white border entirely. All exterior silhouettes must have solid crisp BLACK contours, no colored speckles/fringing, no pale halo, no ground shadow. White only inside the heart/internal shine. No text, no props, no grid lines, no extra frames.


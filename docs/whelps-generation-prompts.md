# Prompts de generación de Kayon, Stein y Bugui

Herramienta utilizada: `image_gen.imagegen` integrada, 12 de septiembre de 2026. No se ejecutó un generador por CLI ni se pintaron píxeles mediante scripts. Las fuentes generadas se conservan; Unity crea un derivado RGBA mediante el importador compartido. El primer resultado de Kayon tenía trece columnas; se corrigió con una edición de imagen y solo el resultado corregido se usa en el proyecto.

## Stein

Referencia local inspeccionada: `atori-reference/100_5340.JPG`, fotografía del álbum enlazada en [las referencias del arte](whelps-expanded-art.md).

```text
Create one production-ready PIXEL ART game sprite animation atlas of STEIN, Gogo's MegaTrip character number 03, based closely on the attached album photograph. The reference character is the cream/beige scientist at the CENTER-RIGHT BELOW ATORI, in the panel labelled STEIN 03; the large comic at left also shows his appearance. Ignore ALL other characters, page graphics, text and panel backgrounds. His identity: small stocky cream/beige body and large rounded tall head, swept white/silver single center forelock and long drooping white hair tufts on both sides of head, black little e=mc²-like facial markings above his huge wide toothy grin, short claw-like hands, two chunky bare feet. Keep this recognizable whimsical vinyl toy silhouette, no human clothes, no lab coat, no added weapons, no cat ears. White/silver hair, warm beige skin, dark purple outlines, tiny cool blue highlight accents.
EXACT LAYOUT: one wide canvas, 12 columns and 5 rows, exactly SIXTY separated complete sprites. All cells equal size; 1536 x 640 pixels preferred, 128 x 128 each. No additional frames, title, labels, gridlines, scenery or watermark. Entire background uniformly opaque pure bright magenta RGB 255 0 255; no shadows or floor; no magenta on the character. Every sprite stays completely inside its cell with at least 10 pixels clear margin and a shared foot ground line within its row. Consistent body scale all 60 frames; never clip hair, hands, feet or motion.
ROWS top to bottom: row1 SOUTH facing camera, row2 SOUTHEAST three-quarter front turned right, row3 EAST true right profile, row4 NORTHEAST three-quarter back turned right, row5 NORTH full back. Each row has all 12 frames facing its stated direction. Back views show hair back and no front facial markings. Do not rotate the whole canvas or reuse front views in rear rows.
COLUMNS left to right in EACH row:
1 idle rest with playful grin, 2 idle gentle breathing head bob;
3 walk contact left foot, 4 walk down/compression, 5 walk contact right foot, 6 walk up/passing. Clearly changing feet and arms;
7 attack WINDUP crouches and draws both hands toward chest gathering tiny cyan/yellow sparks;
8 attack CONTACT strongly thrusts both claw hands forward in the direction of that row, straight body extension, a small yellow/cyan electrical burst attached to fingers only;
9 attack FOLLOWTHROUGH hands recoil upward, hair sprung up, body settles. Energetic scientist casting lightning, no full projectile and no beam crossing the cell;
10 death startled hit reaction, 11 death falling sideways, 12 death fully collapsed small body on ground, peaceful cartoon defeat, no gore.
Crisp deliberate square pixels like a charming handheld console tactics game, clean dark 1-2 pixel contour, approximately 5 shade tones per material, no blur, no antialiasing, no painterly or 3D rendering. Small readable expressive poses, consistent character model and palette. This atlas will be mechanically sliced into exactly 12x5 cells, so all 60 complete sprites and empty magenta gutters are mandatory.
```

## Kayon, generación inicial

Referencias inspeccionadas: `whelps-reference/kayon-figure.JPG` y `whelps-reference/100_5354.JPG`.

```text
Create a production-ready PIXEL ART game animation sprite atlas of KAYON, Gogo's MegaTrip character number 76, faithfully based on the two attached reference images. First image is an unpainted plastic Kayon figure showing the sculpt; second image is the official album photographed, where Kayon is ONLY the bottom-right panel labelled KAYON 76. Ignore all other characters and book graphics. Use the sculpt from the first and the dark indigo/navy plus cyan eyes colorway from that album panel.
Character identity: very short, squat, cute round alien/toy with a large smooth rounded egg-shaped head wider than its tiny body, a flat rounded crown with NO ears, no hair, no horns. Two small narrowed teardrop/almond cyan eyes slant inward above a small wavy moustache-like mouth. Very small rounded side nubs/hands at cheek height, tiny torso, short wide stubby feet, a curved little hook arm extending to its side. It is a smooth solid toy, not a hooded person, not a ninja, not a robot. Dark navy body with cobalt shaded edges and pale blue highlights so it remains legible on a dark board, cyan eyes. Preserve rounded silhouette and face; no clothing, sword, hat, gems added to body.
EXACT LAYOUT: one wide canvas, exactly 12 columns and 5 rows, SIXTY separate complete sprites. All equal cells, 1536 x 640 pixels preferred, 128 x128 per cell. No labels, title, border, grid lines, text, logos, shadows or scenery. Entire background solid opaque pure magenta RGB 255 0 255. No magenta pixels anywhere inside character. At least 10 pixels of clear magenta margin around each sprite. Consistent sprite body scale, foot ground line, and camera angle. Do not clip any pose.
ROW directions top to bottom: SOUTH full front facing viewer; SOUTHEAST three-quarter front facing right; EAST pure right profile; NORTHEAST three-quarter back facing right; NORTH full back. Each row's faces and ALL action poses stay oriented correctly, even attack followthrough and defeat. Rear views show a smooth round head back, no facial markings.
COLUMN action sequence within EACH row:
1 idle rest, 2 idle gentle breathing with slight squash.
3 move left foot contact, 4 move compressed mid-step, 5 move right foot contact, 6 move lifted passing pose. Clearly distinct short stomping movement.
7 attack windup: compact body crouches back, little hooked hand pulled inward with a tiny cyan energy fleck;
8 attack contact: vigorous body bump/hand thrust in the facing direction, stretched posture and a very small cyan-white impact glint attached to the leading hand;
9 attack followthrough: recoils to upright with hands spread. Keep projectiles absent because game spawns them separately.
10 death hit reaction wide-eyed, 11 death tipping onto side, 12 death lying flat sideways, peaceful toy defeat, no gore.
Art style: charming console tactics pixel art, true crisp small square pixels, dark 1-2 pixel contour, deliberate small palette and clustered pixel shading. Visible clean pixels, no blurry edges, no antialiasing, no painterly or 3D rendering. The unit should remain readable at a game display height of 48 pixels. Exactly 60 complete poses, 12x5, empty magenta gutters required for mechanical slicing.
```

## Kayon, corrección final de columnas

Entrada: `exec-551ed97b-2f0c-4914-8493-e4d9437e153f.png`, primer resultado preservado en la carpeta de imágenes generadas de Codex.

```text
Edit this existing Kayon pixel-art sprite atlas with only ONE structural correction: it has THIRTEEN columns and FIVE rows. Remove the entire TENTH column (counted from the left), the unnecessary repeated idle pose immediately after the attack followthrough and immediately before the startled wide-eyed defeat reaction. Shift the final THREE defeat frames left. The result must have exactly TWELVE equal columns and FIVE rows = SIXTY complete sprites. Preserve all other pixels, character appearance, colors, row directions, crisp pixel style, and the solid RGB255,0,255 magenta background. Keep existing columns1-9, omit oldcolumn10, keep oldcolumns11-13 as newcolumns10-12. No new poses, text, labels or gridlines. Trim canvas width to fit the resulting12columns with consistent equal cell spacing and empty magenta gutters. Ensure final defeat poses are still fully inside last column.
```

## Bugui

Referencias originales del proyecto: `Assets/art/units/local/bugui/bugui-south.png`, `bugui-east.png`, `bugui-north.png` y `bugui-north-east.png`. Se inspeccionaron antes de solicitar la hoja.

```text
Create a production-ready pixel-art sprite animation sheet of BUGUI, matching the ORIGINAL attached existing sprites exactly in character identity and style. References show south/front, east/right, north/back and northeast/back of the SAME character; follow their anatomy and markings. Bugui is a petite friendly pink catlike toy: two triangular pink ears, round large pink head, two solid vertical oval black eyes, little black curved brows, broad thin black smile, compact pink body with white oval belly/chest, two small pink mitten hands held at chest, two stubby pink feet and tiny round tail on back. No nose or whiskers, no clothing, no crown, no weapon. Bubblegum/light pink body, darker pink contour/shadows, white belly. Keep the original eyes, smile, proportions and simple low-resolution pixel look. Improve animation expressiveness without redesign.
Output ONE strict animation atlas: exactly TWELVE columns x FIVE rows, sixty sprites total, wide 12:5 aspect preferred 1536x640. No labels, text, separators, grid, watermark, scenery, floor or shadow. Flat opaque solid RGB255,0,255 magenta background and generous empty gutters. Avoid pure magenta inside Bugui; use rose pink #f6a7ce and #d65b9a instead. Every complete pose fits its equal cell with 10px margin; consistent scale, centered feet baseline, never clips. Pixel art with crisp square pixels, flat clusters and original simple outlines, no blur or antialiasing.
Rows from top to bottom: S fully front, SE front-three-quarter right, E full right profile, NE back-three-quarter right, N full back. Every action stays in its row-facing direction, including casting/recovery. Rear-facing poses show no eyes or white belly.
The columns must be exactly these TWELVE frames, no extra return-to-idle column:
A/B (columns1-2) idle: original happy posture then soft blink/breathing squash;
C/D/E/F (columns3-6) walking: left-foot contact, compressed passing, right-foot contact, upward passing; visible alternating steps, arm swing, ear bounce;
G (column7) strong ATTACK ANTICIPATION: lowers torso deeply, ears tilt back, brings both hands to chest around a SMALL violet-blue glowing energy orb. Determined eyebrows with original oval eyes, body angled back.
H (column8) strong ATTACK RELEASE/CONTACT: extends both arms decisively toward the row's facing direction, leans forward, feet braced, small white-blue flare between hands, mouth open with exertion. Orb/flare attached to hands only, no detached projectile.
I (column9) ATTACK FOLLOWTHROUGH: arms lifted and spread after release, body recoiling back, ears trailing then settling. Clearly different from idle.
J/K/L (columns10-12) defeat: hit/stagger, tipping sideways, lying peacefully on ground as a small fallen toy with eyes closed. No gore.
All sixty poses must be separate and readable at about48px display height. Preserve Bugui's white belly only on front-visible surfaces and small pink round tail on back. Make the attack sequence readable with three genuinely different silhouettes and a much stronger windup/followthrough than the original static sprite. 
```

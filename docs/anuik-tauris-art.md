# Anuik y Tauris: diseño, fuentes y animaciones

Se agregan dos Monsters solicitados para Monster Pouch: **Anuik** y **Tauris**. El arte usa sus figuras de MegaTrip como referencia visual; las poses, reglas de combate, proyectiles y resurrección son trabajo nuevo del videojuego. Las imágenes originales del usuario, el catálogo existente y su material conservan sus archivos y GUID.

## Referencias comprobadas

La fuente visual principal es [Mundo Gogos Crazy Bones: Serie MegaTrip](https://mundogogoscrazybones.blogspot.com/2012/07/serie-megatrip-sao-80-novos-bonecos.html), una página de coleccionista con fotografías del álbum y de figuras reales. No es una página oficial del fabricante. Se inspeccionaron las fotos completas sin modificarlas:

- `100_5340.JPG`: el álbum identifica a Anuik como **02** y muestra también un dibujo grande de él. Tiene cabeza ancha naranja melocotón, grandes orejas laterales, ojos cerrados felices y camiseta gris con corazón. Es la referencia directa de identidad y paleta para el sprite.
- `100_5346.JPG`: la ficha **36 TAURIS**, situada abajo a la derecha, permite verificar su silueta de cabeza lobulada y la colocación de ojos, boca y brazo.
- `cjzg4gq88wg61.jpg`: fotografía de las 80 figuras. Tauris es la figura amarilla de la cuarta fila, sexta desde la izquierda. Se usa para entender el volumen de su cabeza con forma de hueso, torso corto y brazo lateral. El sprite nuevo emplea una interpretación azul grisácea; no se atribuye ese color a la figura amarilla fotografiada.

La identificación y rasgos se contrastaron con [Anuik en Crazy Bones Pedia](https://crazybonespedia.fandom.com/wiki/Anuik) y [Tauris en Crazy Bones Pedia](https://crazybonespedia.fandom.com/wiki/Tauris). La página de Tauris registra variantes azul grisáceo y su temática de tiburón. Las vistas traseras, los movimientos, las mordidas proyectadas y la resurrección no se presentan como animaciones originales de Gogo's.

Las fotografías de consulta se guardaron, sin editar, en `C:\Users\HP VICTUS\OneDrive\Documentos\ChatGPT\monsters-pouch\monster-art-work\references\`. No se incluyen como sprites del juego. El estilo se comparó con el Popow sur ya aportado por el usuario; no se transfieren su estrella, cabeza redonda ni guantes a los personajes nuevos.

## Archivos definitivos

| Uso | Fuente preservada | Derivado técnico para Unity |
| --- | --- | --- |
| Anuik general | `Assets/art/units/generated/anuik/anuik-animation-source-v2.png` | `Assets/art/units/generated/anuik/anuik-animation-rgba-v2.png` |
| Anuik resurrección | `Assets/art/units/generated/anuik/anuik-revive-source-v1.png` | `Assets/art/units/generated/anuik/anuik-revive-rgba-v1.png` |
| Tauris general | `Assets/art/units/generated/tauris/tauris-animation-source-v1.png` | `Assets/art/units/generated/tauris/tauris-animation-rgba-v1.png` |

Se usó exclusivamente la herramienta integrada **image_gen.imagegen**, no la ruta CLI/API. Las tres fuentes seleccionadas son PNG **RGB con un fondo magenta deliberado**; no se afirma que el generador haya entregado alfa. El método compartido `PrepareChromaSource` crea una textura RGBA32 separada y hace transparente el color de exportación. No modifica las fuentes. No se usó Python para editar imágenes; su empleo se limitó a lecturas y medidas.

La primera hoja de Anuik `anuik-animation-source-v1.png` se conserva como fuente anterior. Las orejas dejaban huecos demasiado estrechos y la proyección global sólo encontraba dos bandas de columnas, por lo que el recorte uniforme podía cortarlas. Una llamada adicional a image_gen redujo cada figura dentro de su celda y produjo la variante v2, con doce bandas limpias. El tamaño dentro del juego se determina por la referencia visible y PPU, por lo que las figuras no se vuelven pequeñas por tener más margen en el PNG.

## Secuencias y direcciones

Todas las hojas tienen filas **S, SE, E, NE, N**, de arriba abajo. El catálogo usa el orden horario **N, NE, E, SE, S, SW, W, NW**. Las últimas tres vistas reflejan sus equivalentes derechos mediante `FlipX`; son cinco vistas fuente y tres espejos, no ocho vistas dibujadas independientemente. En Tauris el gesto lateral se adapta con ese espejo.

| Personaje/hoja | Tamaño | Columnas | Secuencias, índices desde cero |
| --- | --- | --- | --- |
| Anuik general v2 | 1774 × 887 | 12 | Idle 0,1; Move 2,3,4,5; Attack 6,7,8; Death 9,10,11 |
| Anuik resurrección | 1254 × 1254 | 5 | Revive 0,1,2,2,2,3,4 |
| Tauris general | 1774 × 887 | 14 | Idle 0,1; Move 2,3,4,5; Attack 6,7,8,9; Death 10,11,12,13 |

Tauris salió con catorce columnas en lugar de las doce solicitadas. Las dos poses adicionales se integran en recuperación y derrota; las setenta figuras tienen una función. El contacto o lanzamiento usa el índice 1 de cada secuencia Attack. Las imágenes no contienen proyectiles sueltos: los corazones y mordidas a distancia se animan en la capa de combate.

La resurrección de Anuik contiene cinco dibujos por vista: encogido/apoyándose, parado sobre la cabeza, equilibrio sobre la cabeza, regreso a cuclillas y de pie. En las columnas 1 y 2 la **cabeza toca el suelo y los pies están encima del cuerpo**. También se invierten correctamente los ojos y el corazón del pecho. No se obtiene la pose girando rígidamente el sprite de reposo. Repetir la segunda pose de equilibrio en la secuencia sostiene el momento invertido durante la resurrección.

La hoja de resurrección tiene figuras mayores en píxeles que la hoja general. El setup mide la anchura visible de las orejas del fotograma final de pie y calibra `revivePPU = 100 × reviveStandingWidth / mainIdleWidth`. Importa de nuevo esa hoja con el PPU resultante, conservando los IDs, para mantener el tamaño visual de Anuik. El factor visual global 1.2× del juego permanece vigente.

## Importación y contrato

Ejecutar explícitamente `MonsterPouchMonstersArtSetup.Setup()`, definido en `Assets/Editor/MonsterPouchMonstersArtSetup.cs`, después de crear los assets locales base. El método:

1. Conserva las fuentes RGB y crea los tres derivados con alfa mediante el importador compartido.
2. Detecta los huecos reales de las hojas y crea subassets con Point filtering, sin mipmaps ni compresión con pérdida.
3. Calibra el PPU de Revive por la misma figura de pie y conserva los anclajes de contacto con el suelo.
4. Agrega o sustituye únicamente las entradas `anuik` y `tauris` de `UnitArt`.
5. Obtiene sus definiciones de `MonsterPouch.Gameplay.Match.ExpandedRoster.CreateAnuik()` y `CreateTauris()`, sin duplicar las reglas en el importador.
6. Escribe `docs/monsters-art-import-report.md` con el alfa real de los derivados, PPU, nombres de sprites, rectángulos y pivotes de las 155 figuras.

Los demás personajes, la escena, tablero, UI y material no son modificados por este setup. No se escriben manualmente archivos .meta ni YAML.

El contrato nuevo que usa Anuik es `UnitDirectionalAnimation.Revive : Sprite[]`. El presentador compartido recibe `BeginRevive(duration)` y `Revive()`; su integración y la temporización del simulador corresponden al cambio de combate del proyecto. El setup sólo asigna las imágenes.

## Auditoría de las fuentes y alcance de validación

Una lectura de los píxeles con el mismo criterio de clave magenta del importador confirma:

| Fuente | Bandas X/Y | Píxeles que se harán transparentes | Píxeles que permanecerán opacos |
| --- | --- | --- | --- |
| Anuik general v2 | 12 / 5 | 1,327,968 | 245,570 |
| Anuik Revive | 5 / 5 | 1,205,001 | 367,515 |
| Tauris general | 14 / 5 | 1,130,946 | 442,592 |

Estos números son la auditoría técnica de la fuente RGB y su predicción de clave, no una afirmación de que el archivo fuente ya tenga alfa. La comprobación RGBA real posterior se registra en el informe generado al ejecutar Setup. Se inspeccionaron visualmente las fuentes y el apoyo real sobre la cabeza en Anuik. Compilación, pruebas EditMode/PlayMode, revisión dentro de la escena y build se coordinan centralmente; este documento no afirma resultados que todavía no constan en esa validación.

La importación central se ejecutó después de escribir el setup. Una lectura independiente de los tres derivados PNG confirmó **RGBA real**, los mismos conteos de alfa 0/255 de la tabla y **cero píxeles de alfa parcial**. El informe de Unity contiene los 155 sprites y un PPU de resurrección de **198,980** frente al PPU 100 de la hoja principal. Las tres copias de fuente coinciden byte por byte (SHA-256) con sus salidas originales de image_gen. Esta verificación posterior confirma importación y escala, sin sustituir las pruebas centrales de combate o la inspección del ejecutable.

## Prompts exactos y procedencia

Todas las salidas originales están en `C:\Users\HP VICTUS\.codex\generated_images\01a09256-d6d2-7dc1-a3ff-c6c0faa4c6f6\`.

### Anuik initial atlas (source-v1, retained as rejected layout)

Salida: `exec-ea767a3d-54de-4b42-bb4b-009c4d97f95c.png`.

```text
Use case: stylized-concept. Asset type: production pixel-art animation atlas for a top-down battle game.
Reference image1 is a photograph of the ORIGINAL MegaTrip album. Identity reference is ONLY ANUIK, labeled 02 on the right and shown enlarged smiling on the left: peach-orange broad flattened head with enormous lateral ears, two closed happy curved eyes, no nose or teeth, tiny gray shirt with a heart at its center, little orange hands/feet. Do not copy Atori, Stein, the other characters, typography or page artwork. Reference image2 supplies ONLY the existing game's crisp pixel-art technique; do not borrow its star, gloves, teeth or color.
Make a new polished sprite sheet for Anuik retaining exactly this broad-eared, cheerful, heart-shirt identity. His head is a rounded trapezoid with a nearly flat top and rounded chin, NOT a round mouse head. His ears are wide tapered lateral lobes attached along the upper head, with rounded tips, not circular mouse ears or upright rabbit ears. Peach/apricot orange head/ears, gray short shirt, clear orange-red heart on the chest, minimal stubby feet. Tiny friendly mouth optional, closed crescent eyes are the facial signature.
Style: true hand-placed 16-bit pixel art around a 48×48 character grid then enlarged uniformly, 1–2 pixel dark contour, limited stepped color clusters, no smooth gradients, no blur, no antialias, no glossy 3D rendering. Consistent character size and head/ear shape.
EXACTLY TWELVE columns and FIVE rows, 60 complete figures total, on a wide atlas preferably 2400x1200. No grid lines or text. Every figure entirely inside its own equal cell with at least 18% empty side gutter. Feet at consistent cell baseline. No figure overlaps another.
Rows from TOP to BOTTOM face SOUTH(toward viewer), SOUTH-EAST(lower right), EAST(right profile), NORTH-EAST(upper right), NORTH(rear). Back views must show the back of the ears, plain gray shirt back with NO heart or eyes. Draw believable overlapping ears/limbs for each viewpoint.
Columns from LEFT to RIGHT:
1 idle relaxed arms;
2 idle breathing, ears and shoulders slightly raised;
3 walk left step forward;
4 walk passing with one lifted foot;
5 walk right step forward;
6 walk other passing pose;
7 ranged heart-cast anticipation: hands draw inward to chest over the shirt heart;
8 heart-cast release: hands open forward, body leans, ears perk; no separate projectile drawn;
9 hands and ears settle back to idle;
10 defeated stagger, knees bend, eyes tighten;
11 seated slump, ears droop;
12 fallen on side, head and body intact, no injuries.
These are distinct drawn limb/body poses, not rigid copies translated or rotated. No unattached effects or hearts outside the figure: the game renders projectiles separately.
Background is deliberately one uniform OPAQUE PURE MAGENTA #FF00FF chroma-key matte, including all limb gaps. This is NOT a transparency request. Never draw checkerboard, scenery, ground, shadows, particles, labels, numbers, frames, logos or watermark. Preserve character colors and solid black outline against the magenta.
```

### Anuik spacing correction (selected source-v2)

Salida: `exec-0c691698-9f01-44af-850e-5deeab3ed483.png`.

```text
Use case: precise-object-edit. Technical sprite-atlas layout correction only. Preserve the exact Anuik character design, colors, pixel-art technique, all sixty poses, the five row directions, and twelve columns of this atlas. Fix only the insufficient gutters: reduce each complete character pose to 70% of its current width AND height while keeping proportions, and center it within its original cell. Keep every character whole, both ears included. There must now be at least 30 pixels of SOLID MAGENTA between neighboring character silhouettes in every row and column. Outer margins must also be clear. Keep the same wide canvas and exact 12-column by 5-row layout. All standing figures use a common baseline within each cell; fallen figures sit at that baseline. Use nearest-neighbor-style crisp stepped pixel edges, no blur. Do not add poses, remove poses, repaint the identity, add effects or change front/back directions. Background stays entirely uniform opaque pure magenta #FF00FF, including all empty limb gaps. No checkerboard, no grid lines, numbers, labels or text. This is a production atlas; the ears must never touch or cross the neighboring cell's empty gutter.
```

### Anuik headstand revival (selected)

Salida: `exec-48dccd77-3747-401f-bcb8-d1790dad90ee.png`.

```text
Use case: stylized-concept. Production pixel-art revival animation sheet, same character as reference.
Reference1 is the newly approved Anuik game sprite sheet. Keep his exact peach-orange wide head, huge tapered side ears, happy closed eyes, charcoal-gray short shirt with orange-red heart on front, stubby orange hands and feet, dark pixel contour and simple stepped shading. This is a new five-frame HEADSTAND REVIVAL sequence, not a recolor or rigid rotation of an idle pose.
EXACTLY FIVE COLUMNS and FIVE ROWS, 25 separate complete figures, no additional figures. Square canvas. Every cell same dimensions, generous at least 20% clear margin all around each figure, figure fully contained. Same head size across frames, common ground-contact baseline, no overlap between cells.
Top-to-bottom row directions: SOUTH (front), SOUTH-EAST, EAST(profile facing right), NORTH-EAST, NORTH(back). Rear views have no facial features or chest heart visible. Maintain ears and shirt correctly for each angle.
Left-to-right timeline:
column1: lying character curls knees and tucks chin, plants both small hands near the crown, preparing to rise;
column2: obvious full upside-down HEADSTAND, crown of the head actually touching ground, ears spread along the ground, shirt/body ABOVE head, two feet sticking UP; arms flex for balance. His face and chest-heart orientation are truly upside down.
column3: a second sustained upside-down headstand balance pose, head still down, feet above, one knee bends a little, ears flex; clearly a different drawing but the same grounded headstand.
column4: recovering from headstand by rolling forward into a low crouch, head coming upright and feet landing;
column5: standing upright recovered, happy closed eyes, arms open and ears perked, matches the idle reference.
It is essential that columns2 and3 show HEAD BELOW BODY and FEET AT THE TOP, visibly inverted; do not draw ordinary upright poses. Draw limbs and ear bending authentically instead of mechanically rotating the entire sprite. No halos, particles, shadows, colored auras, hearts floating outside character or VFX.
All empty pixels use a deliberate uniform solid opaque pure magenta #FF00FF chroma-key matte. NO checkerboard. Crisp uniformly enlarged hand-pixelled edges, limited color clusters, no antialiasing blur, no glossy 3D, no gradients. No labels, numbers, text, borders or watermark.
```

### Tauris atlas (selected)

Salida: `exec-e8a1136b-ed4d-4c79-92fe-99caabf84fb4.png`.

```text
Use case: stylized-concept. Asset type: production pixel-art animation atlas for a top-down battle game.
Identity references: image1 is the MegaTrip album page containing TAURIS, number36, in the bottom-right blue panel. Use ONLY this character's silhouette and face placement, not the neighboring Tau/Juruk/Tsui characters or comic. Image2 is the actual 80-figure MegaTrip collection photo: Tauris is the YELLOW figure in the fourth row, sixth from the left (roughly center of the photograph), with a knucklebone-shaped head and arm gesturing to the side. Image3 supplies ONLY the established game's pixel-art technique, not its red color, round head, star or boxing gloves.
Create Tauris as a blue-gray/teal toy monster matching this knucklebone head, TWO oval eyes, broad toothy jaw, short stout torso and legs, and a short arm held slightly to his right. The head has two large rounded upper lobes with a shallow notch across the top and a pinched contour at the jaw; it is not a bull and has NO horns, ears, tail, fin, hair, clothing, weapon, emblem, star or boxing gloves. Keep him compact, head much larger than torso, stout planted feet. Blue-gray upper planes, deeper slate-blue shadows, pale eyes, opaque ivory triangular teeth and dark mouth. The alternate color is a game palette interpretation of the photographed mold.
Style: crisp hand-placed 16-bit pixel art, about a48x48 original sprite enlarged uniformly, hard stepped outlines with1–2pixel dark contour, limited flat color clusters, no antialiasing, no gradient, no smooth glossy3D. Match image3's pixel density and clarity. Every frame keeps the same head size and character identity.
Exactly TWELVE columns × FIVE rows =60 complete separate figures, wide atlas preferably2400x1200, generous empty gutters at least18% of cell width. No grid lines. Same ground baseline in every cell. Each pose wholly within its own cell.
Rows TOP toBOTTOM face SOUTH(front), SOUTH-EAST(lower right), EAST(right profile), NORTH-EAST(upper right), NORTH(back). Rear views show only back of head/body, never eyes/teeth on the back. Overlap far limbs correctly.
Columns LEFT toRIGHT:
1 idle grin/arms relaxed;
2 idle breathe, slight head/shoulder rise;
3 walk left foot forward with opposite arm;
4 walk passing/knee lift;
5 walk right foot forward with opposite arm;
6 opposite passing step;
7 ranged BITE anticipation, knees bend, mouth opens wide, head pulls back;
8 bite release, head thrusts toward facing direction, jaws snap with big readable ivory teeth, arms counterbalance; no projectile drawn;
9 jaw/body return toward neutral;
10 defeated backward flinch, mouth slack;
11 knees give way, seated slump;
12 fallen sideways, head/body intact, defeated but no wounds.
Limbs and jaw must be distinctly redrawn between action poses; do not copy and rotate a rigid standing figure. No detached bite effects, particles or shadows; projectiles are rendered separately by the game.
Backdrop is a deliberate single uniform opaque PURE MAGENTA #FF00FF chroma-key matte, including all gaps between limbs. This is NOT a transparency request. Never draw checkerboard, gradients, scenery, floor, cast shadows, numbers, labels, text, frames, logos or watermark. All character interiors and outlines stay opaque.
```


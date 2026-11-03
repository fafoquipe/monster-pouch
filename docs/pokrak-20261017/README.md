# Pokrak — 17/10/2026

Pokrak es un Whelp cuerpo a cuerpo: ataca con pinzas y concede burbujas temporales de reducción de daño. Diseño solicitado por el usuario; el arte sigue los sprites e iconos pixel art existentes.

Actualización posterior: el usuario eligió una nueva hoja de combate con más detalle. Se usa desde `ArtArchive/pokrak/combat-reference-20261017.png`, empaquetada a resolución nativa, manteniendo el idle de menú. Consultar [la actualización de preparación](../preparation-idles-20261017/README.md); los prompts siguientes documentan el arte anterior y los iconos que siguen vigentes.

## Balance inicial

- Vida: 55; daño: 3; alcance: 1 casilla; ataque cada 1,5 s. Valores provisionales editables.
- Energía: 25; +1 al conectar un ataque básico; +2 por golpe recibido, incluidos golpes absorbidos por un escudo de vida. Sin regeneración pasiva.
- Burbujas Protectoras: área de 3×3 centrada en Pokrak, incluido él. Reduce el daño recibido un 20% durante 2 s. No añade vida ni escudo de vida.
- Mejora 1 — Burbujas Duraderas: duración final de 3 s. Coste inicial: 3.
- Mejora 2 — Marea Protectora: añade una casilla al centro de cada lado; 13 casillas en total. Coste inicial: 3.
- Mejora 3 — Burbujas Reforzadas: reducción final del 25%. Coste inicial: 4.
- Coste del personaje: 3. Preparación del súper: 0,3 s; pausa: 0,1 s; recuperación: 0,25 s.

```
  ■
 ■■■
■■P■■
 ■■■
  ■
```

Se seleccionan aliados vivos al soltar la habilidad. La burbuja les acompaña aunque se muevan; no incorpora aliados que entren después. La reducción se aplica antes de absorber escudos de vida y también al daño reflejado; las ejecuciones mantienen la regla existente. Varias burbujas de Pokrak no suman porcentajes: se usa la más fuerte que siga vigente. Cada lanzador renueva solamente su propia duración. La muerte del lanzador no elimina una burbuja ya concedida. Reiniciar al receptor, terminar el combate o agotar el tiempo elimina la protección.

Las burbujas visibles son círculos transparentes de píxeles con reflejos suaves; los iconos de habilidad mantienen el lenguaje visual de los otros personajes. Hay 25 poses, cinco vistas originales y ocho direcciones con reflejo lateral, ataque, movimiento, habilidad y derrota.

## Edición y archivos

- `Assets/resources/MonsterPouch/Characters/pokrak/Character.asset`: ficha, estadísticas, arte y referencias de habilidades.
- `Ability.asset` y `Upgrade1.asset` a `Upgrade3.asset` en esa misma carpeta: porcentajes, duración, área y costes.
- `Assets/resources/MonsterPouch/Pokrak/pokrak-combat.png`: atlas importado al tamaño de píxel del juego.
- `Assets/resources/MonsterPouch/Pokrak/pokrak-abilities.png`: cuatro iconos de 64×64.
- `ArtArchive/pokrak/`: originales de ImageGen conservados.
- `MonsterPouchPokrakSetup.Apply`: instalación e importación reproducible; respeta cambios de balance al volver a importar.

## Arte

Generado con la herramienta integrada ImageGen. Referencias: imagen de Pokrak del usuario, `Suki/suki-combat.png`, `UI/LobbyIdles/atong.png` y `UI/AbilityArt/abilities-04.png`. Se descartaron las versiones iniciales demasiado ilustradas tras la petición de usar pixel art como el resto del juego. El importador aplica el mismo muestreo de píxeles, bordes transparentes y filtro Point que el pipeline de Suki; no añade dibujos ni modifica el diseño.

### Prompt final de personajes

GAME ASSET, STRICT LOW RESOLUTION PIXEL ART. Match the first two reference images' actual game sprites, NOT a digital illustration. Make Pokrak (pirate crab from third ref) a TRUE 16-bit game sprite with LARGE visible SQUARE pixels. Logical resolution EACH CHARACTER ONLY 48x48 pixels, maximum 16 solid colors shared by all sprites, 1-pixel dark outline at logical scale, 2 or 3 flat stepped shades per material. Every logical pixel is a perfectly uniform square enlarged with NEAREST NEIGHBOR. NO antialiasing, NO smooth curves, NO tiny noisy pixels, NO gradients, NO painterly/glossy surfaces, NO complex details. Simplify crab emblem to a few cream pixels. Pirate tricorn, burgundy body, orange pincer edges, cream belly, tentacle moustache. Normal balanced warm colors matching reference game sprites. Full transparent sprite atlas exactly FIVE COLUMNS x FIVE ROWS, equal cells, each figure fully contained with generous clear cell margins, no labels. Columns back view, back-right view, right profile, front-right view, front view. Rows standing idle, walking with leg lifted, claw attack extended, super with both claws raised (no particles), defeated collapsed eyes closed. 25 distinct small chunky pixel sprites. Consistent logical grid and scale, no shadow. Small game sprites, not miniature paintings. Output should look like the Suki reference's crisp low-resolution atlas and the tiny yellow Atong reference.

### Prompt final de iconos

Create FOUR ability icons for Pokrak in EXACT pixelart style of attached first game icon atlas. This is a low-resolution bitmap game asset, NO painted illustration. EACH ICON IS ONLY 64x64 LOGICAL PIXELS, enlarged as perfect uniform square nearest-neighbor blocks. 16-bit pixel art, dark 1-logical-pixel outlines, tiny simple characters, flat pixel shade clusters, limited 20 colors. No smooth curves, antialiasing, gradients, detailed gloss, noisy texture. Square output of exactly 2x2 equal icons. Same balanced contrast and saturation as existing game reference, no neon orange or giant blinding flares, no drab washed-out filter. TOPLEFT: small burgundy orange pirate crab Pokrak inside a round transparent pale amber BUBBLE, tiny claw impact outside bubble. TOPRIGHT: simple pixel hourglass inside protective BUBBLE. BOTTOMLEFT: 3x3 tile square plus one extra tile at midpoint ofeachside (13 tiles), central bubble. BOTTOMRIGHT: bubble with reinforced double rim stops two small red pixel strikes. Backgrounds understated navy, plum, teal, burgundy, with few simple accent pixels like reference. Bubble interiors transparent-looking, not filled orange. No shield silhouettes, no health hearts, no text. Enforce LARGE visible square pixels throughout, icons for same game as first reference, not high-res art with a pixel filter. Character design from second reference, vastly simplified to match first.

## Verificación

Resultados de Unity en `Logs/local-validation/pokrak/`. Las pruebas cubren área y orientación de ambos equipos, duración, porcentajes, energía por impacto, coexistencia con escudos de vida, múltiples lanzadores, reinicio, bordes del tablero, ficha, sprites, burbujas, pausa y limpieza visual.

## Validación final

La revisión de combate y colección pasó 2/2 pruebas Play Mode tras corregir el centrado de las burbujas sobre la silueta, el glifo pixel art del HUD y el recorte del atlas de habilidades. Las 19 pruebas de Pokrak y las regresiones relacionadas pasaron; la única expectativa antigua del catálogo se actualizó a 22 personajes y su repetición pasó. Se repitieron también los cuatro casos de área con un enemigo situado dentro de la zona: todos pasaron, sin otorgarle protección. Resultados y capturas en `Logs/local-validation/pokrak/`.


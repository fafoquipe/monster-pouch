# Arte y animación — octubre de 2026

Modo de generación: herramienta integrada **image_gen**. Los PNG finales se copiaron sin alterar sus píxeles ni su canal alfa. Los archivos `.parts.json` contienen únicamente rectángulos UV medidos en los originales para el motor de Unity.

## Assets finales

- `Assets/resources/MonsterPouch/UI/TableSkins/lounge-deck.png`: skin opcional Salón de cartas. El fondo clásico se conserva en su archivo original.
- `Assets/resources/MonsterPouch/UI/TableSkins/table-decor.png`: lámpara y accesorios de cartas independientes para el escenario.
- `Assets/resources/MonsterPouch/UI/LobbyRigs/props.png`: objetos que sostienen los personajes, independientes del cuerpo.
- `Assets/resources/MonsterPouch/UI/LobbyRigs/aky-wings.png`: alas desplegadas de Aky.
- `Assets/resources/MonsterPouch/UI/LobbyRigs/rigs.json`: particiones articuladas sobre los idles originales de `pictures/gogos-minis`; no es arte generado.

Los sprites de los personajes en reposo mantienen los originales. Las nuevas animaciones transforman piezas de esos mismos sprites con curvas continuas, en lugar de intercambiar dibujos de proporciones diferentes. Los atlas anteriores se conservan como fuente de trabajo y el nuevo renderer no los carga.

## Conjunto de prompts

### Salón de cartas

Referencia de edición: `Assets/resources/MonsterPouch/UI/deck-background-no-whelp-lights.png`.

Edit this exact mobile game lobby background. Keep its front-facing 9:16 portrait composition and exact layout of 8 EMPTY pedestals: one large featured podium, four middle podiums and three lower podiums. Create a cozy premium card-game lounge with teal felt, walnut and brushed brass, amber lamps, small card decks and leather pouches at the margins, burgundy drapes. Preserve pale ivory oval podium tops and their center locations so the current character UI still fits. Replace the horn crest with a pouch and two cards, with a blank name plaque. Remove all text and navigation; keep the bottom 16 percent deep teal for UI. No figures, poker chips, roulette, slots, dollar signs, coins, neon, flashing lights or clutter. Portrait 9:16.

Salida: `exec-4b0dd06f-4cc7-429a-982d-9f585901de3a.png`.

### Alas de Aky

Referencia de identidad: idle original de Aky de `pictures/gogos-minis`.

Create ONE transparent production parts atlas with exactly two fully unfurled bat wings matching original Aky: orange folded membranes, cobalt blue bones and navy outlines. No character body, face, hands, arms, legs or ground shadow. Left wing extends left with its root at the inner RIGHT edge; right wing mirrors it. Three scalloped lobes, root near the upper quarter, wing width about 1.2 times its height. Leave transparent margins around each separate part. 1536x1024, genuine alpha, no magic or neon glow.

Edición final sobre el primer atlas:

Edit this exact TWO wing sprite atlas. Change ONLY the alpha edges: completely remove the blue outer bloom, hazy aura, blur, shadows and any color outside the sharply outlined wings. Wing pixels and shapes must remain identical: same orange membranes, cobalt-blue bones, navy blocky contours, mirrored arrangement and root locations. Real clean transparent background all the way to the navy outer pixel contour, with fully opaque solid wing interiors and only a crisp pixel edge. Do not add any part, no body, no background, no glow, no feathering. Preserve original canvas size and sprite placement.

Salida final: `exec-63df789c-cbfe-4f77-900c-cebf2c01e469.png`. Se comprobó alfa cero fuera de las alas.

### Objetos de los gestos

Referencia de estilo: idle original de Anuik, únicamente como guía de estilo.

Create a transparent pixel-art parts ATLAS with exactly 8 separate props in a strict 4 columns by 2 rows layout. Top row: vanilla ice cream cone; ivory white handkerchief with a loose corner and pale blue shading; tiny wooden training dummy with round head, crossbar arms and pedestal; oval golden star badge, not money or a chip. Bottom row: rounded slate stone; angular slate stone; red paper talisman with a single spiral; thin short brown toothpick. Front three-quarter held objects, 22 percent padding, no hands, characters, hearts, decorations or ground shadows. Chunky pixels and navy outlines matching the reference, genuine alpha, 2:1 canvas.

Salida: `exec-47c49fad-5461-4c1b-a035-bfb9de76532b.png`.

### Decoración de mesa

Referencia de estilo: `Assets/art/ambientations/casino/card-shuffler-cart.png`.

Create ONE transparent ATLAS of four separate decorative objects for the existing pixel card arena, matching the reference cart's warm walnut, brass, chunky pixels and navy outlines, seen from three-quarter overhead. Strict 2 by 2 grid with 18 percent padding, one connected cluster per cell. Top left: brass reading lamp with cream shade, unlit, without baked glow. Top right: two closed teal and burgundy leather card-deck boxes. Bottom left: small fern in a teal pot. Bottom right: walnut card case and a small fan of playing cards. No characters, money, chips, dice, slots, roulette, text or shadow blobs. Square canvas, genuine alpha. Existing scene dice are retained separately.

Salida: `exec-648d5b7c-9a95-4938-83e9-a31022b32170.png`.

## Selección

En Ajustes → Escenario se puede alternar entre Casino clásico y Salón de cartas. La preferencia `monster-pouch.map-skin` se guarda localmente y no modifica el Brief ni los personajes elegidos. La distribución lógica del tablero y las reglas de combate son iguales para ambas skins.

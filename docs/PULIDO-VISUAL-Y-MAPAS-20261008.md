# Pulido visual y mapas online

## Interfaz

- Inicio: cabecera y Brief alineados a los mismos márgenes; los dos botones principales tienen el mismo ancho.
- Colección: columnas del mismo ancho, separación regular, Monsters y Whelps ordenados alfabéticamente, y acciones Detalle/Usar ajustadas al ancho de la carta.
- Ficha de habilidades: dos columnas iguales, lista y descripción alineadas arriba y abajo, botón de mejora centrado y separado del contenido. Se conservan los textos relativos del balance.
- Costes: número sobre la misma Moonken utilizada en partida, en cada mejora y en el botón de compra. Ya no se añade «MK».
- Estilos, ajustes, sonido, ayuda y sala online: márgenes, tamaños y espacios más uniformes. Los fondos de las ventanas cubren también las pantallas altas.

## Mapas online

Al conectar se envía el mapa elegido junto con el equipo. El anfitrión corresponde al jugador 1: su mapa se usa en rondas impares. El mapa del invitado se usa en rondas pares. Ambos equipos siguen viendo el mismo escenario y conservan la orientación de su tablero.

Las elecciones se fijan al conectar y se incluyen en la comprobación de sincronización. Los identificadores desconocidos se rechazan. Volver al menú restaura el mapa personal sin sobrescribirlo con el del rival. La versión de protocolo es `mp-2026-10-08-v5-gum-attack`; los dos dispositivos necesitan la misma versión del juego. Las 17 pruebas locales de sincronización online pasaron; no se ha realizado una partida por Internet entre dos dispositivos físicos en esta revisión.

## Campo Real y Anuik

- Campo Real usa las dimensiones exteriores del marco original de Grand Casa Hotel, con su propia madera, incrustaciones doradas y esquinas. No cambian las casillas ni las posiciones táctiles.
- Nuevo entorno: `Assets/resources/MonsterPouch/UI/TableSkins/royal-arena-pixel.png`. Se conserva `royal-arena-giant-props.png` como original.
- Anuik conserva sus PNG, recortes, pivotes y animaciones. Sus materiales ya no pintan una silueta negra adicional sobre los colores originales. Se mantiene el recorte de residuos casi transparentes.
- Las texturas de Anuik y el fondo nuevo se importan con Point, sin mipmaps ni compresión, también para Android.

## Parque de juegos

- El mapa predeterminado ahora es un parque de bolas inflable de pixel art. El tablero está dentro de los tubos de colores, con casillas de espuma y entorno de juguetería.
- Se mantienen la geometría, las 60 casillas, los puntos táctiles y la colocación de tropas. El fondo cubre las pantallas altas.
- Los tres mapas conservan sus diseños separados y pueden alternarse sin superponerse. Las 11 pruebas finales de mapas, geometría e interfaz pasaron.
- Se incluye la sombra del banco guardada por el chat lateral; el usuario confirmó que ese chat terminó antes de iniciar Android.
- Recursos: `Assets/resources/MonsterPouch/UI/TableSkins/toy-inflatable-room.png` y `toy-inflatable-frame.png`. Point, sin mipmaps ni compresión; PNG originales preservados. El marco utiliza ocho recortes de sprite nativos para ajustarse a los bordes existentes.

## TSU e inspector simplificado

- El selector muestra «Parque de juegos» para el mapa predeterminado; conserva su identificador guardado y online.
- Solo las habilidades con animación propia muestran su sección de tiempos. Las pasivas y mejoras que modifican otra habilidad ya no repiten esa sección.
- Chicle pegajoso de TSU y Radiación de Jazzar usan el ataque normal. No inician otra animación del personaje. Los valores antiguos de tiempos guardados de estas habilidades se ignoran, sin borrar los valores de balance del usuario.
- TSU lanza una bola redonda de chicle rosa, con sombreado pixel art y sin palo. El ataque básico y el chicle especial conservan el mismo ritmo. La inmovilización se aplica al impacto; se conservan las miniexplosiones de la mejora.
- Se actualizan únicamente el nombre y la descripción del ataque básico guardado de TSU, además del código. Daños, costes, intervalos y mejoras personalizados se conservan.
- Los nuevos APK y sus carpetas usan únicamente fecha y hora. La compilación cancelada por el usuario no es una entrega. Las builds antiguas de Android se retirarán una vez verificada la nueva.

## Generación del parque

Herramienta: `image_gen` integrada. Dos imágenes nuevas: entorno opaco y marco con transparencia.

Prompt del entorno:

> Use case: stylized-concept. Production game background asset for Monster Pouch, portrait 9:16, coarse authentic 16-bit pixel art drawn on a consistent approximately 270x480 square pixel grid, enlarged nearest-neighbor. Toy-store indoor inflatable ball park. SATISFYING LEFT-RIGHT SYMMETRY. Bright azure, cyan, butter yellow, coral red, warm lavender. At top 18%: a small inflatable castle entrance arch with two equal blue/yellow bouncy turrets, toy shelves behind, simple pennants, padded rainbow tunnel; modest scale so the map fits a phone screen. Down the VERY NARROW left and right edges: colorful shiny plastic balls and padded toy playroom props, outside a huge clear central playing area. CENTER 80% width from y=20% to y=80% is EMPTY muted turquoise foam flooring, completely unobstructed: actual game board and inflatable rails will be composited by game code. Bottom 20%: simple foam floor, a few balls only near outermost corners, open space for game's briefcases. Balanced overhead/three-quarter perspective for a mobile tactics game. Rounded inflated rubber highlights made of chunky stepped pixels and 3-tone flat shading, charming simple toy proportions. No board squares, no checkered pattern, no game UI, no text, no labels, no numbers, no characters, no logos, no gradients or realistic lighting. Full bleed opaque background.

Prompt del marco:

> Use case: stylized-concept. A SINGLE isolated game sprite on genuine transparent alpha: rectangular inflatable ball-pit perimeter frame, taller than wide, outer aspect ratio about 4:5. Top-down almost orthographic view with a small sense of volume. Authentic chunky 16-bit pixel art, square consistent pixels, bold stepped edges, limited palette with 3 shades per rubber color, no blur or smooth gradients. A continuous closed ring of inflated rounded vinyl tubes: left and right long rails vivid blue and cyan, top and bottom rails warm golden yellow, four equally sized rounded coral-red corner pads. A small cluster of 3-4 bright plastic toy balls tucked into each corner only, perfectly symmetric spacing, no balls extending far into the center. Thick but not oversized rails: approximately 8% of outer width, leaving an EMPTY TRANSPARENT rectangular opening occupying 80% of width and 82% of height. Ring is centered, fits fully inside the image with 3% transparent margin all around, straight parallel rails with softly rounded corners, no perspective taper. Pixel highlights and subtle seams make it unmistakably inflatable. Intended to enclose the real 6x10 board of a mobile tactics game inside a toy-store play park. Output ONLY this ring sprite with transparent center AND transparent exterior; no floor, checkerboard backdrop, board squares, shadows outside the ring, furniture, text, icons, logo, people, UI, extra objects or additional sprites.

## Generación del entorno

Herramienta: `image_gen` integrada, edición de imagen existente con fondo opaco. Referencia: `royal-arena-giant-props.png`.

Prompt final:

> Use case: style-transfer. Edit target: the attached royal arena background for a portrait pixel-art game. Redraw the environment as AUTHENTIC COARSE PIXEL ART, consistent square pixel clusters, crisp stepped silhouettes, flat limited palette shading with 3-4 tones per material, no smooth gradient, no painterly texture or photorealistic rendered details. Preserve the royal identity: symmetric red velvet and gold throne at top center, two equal ivory columns, matching small green plants and matching gold candle holders on either side, warm horizontal oak floor, thin burgundy-and-gold carpet strips down the two side edges. Keep the same portrait 9:16 composition; all throne and side props fit in the TOP 20 percent of the image, with a little breathing room above a completely visible throne crest. The center and bottom 80 percent remain unobstructed wooden floor for the game board and UI to be placed in Unity. Mirror the architectural spacing left/right for a satisfying balanced view. Simplify ornaments to bold readable pixel shapes. This is a finished game background, as if drawn natively on a 270x480 pixel grid and enlarged with nearest neighbor, not a highly detailed illustration with a pixel filter. Do not include chess squares, board, characters, coins, interface, writing, text, labels, logos or watermarks. Keep the supplied image's colors and royal furniture identity, but fix the overly smooth giant throne style. Opaque background.

## Entrega Android

- APK: `Builds/Android/2026-10-08_02-39-41/2026-10-08_02-39-44.apk`.
- Resultado: Succeeded; ARM64 / IL2CPP; compilación sin modo Development.
- Tamaño: 316,679,759 bytes. SHA-256: `35b01ffeac4384cf70dac376e4015572b55a5215a82942efe2d146f626c2b6e3`.
- Firma Android e integridad ZIP verificadas; permisos y arquitectura comprobados. Ajustes y plataforma del editor restaurados.
- Validación: 17 pruebas locales online; 29 casos de tiempos e inspector, con corrección y repetición del caso de TSU; 11 pruebas de mapas/interfaz; una prueba visual de TSU (pausa, forma del proyectil y limpieza al terminar).
- Comparación del balance guardado: solo cambian BasicAttackName y BasicAttackDescription de TSU. Los demás campos son idénticos.
- Capturas: `Logs/local-validation/screenshots/default-inflatable-park.png` y `tsu-bola-chicle.png`.
- No se ha instalado ni probado esta APK en un teléfono físico en esta revisión.

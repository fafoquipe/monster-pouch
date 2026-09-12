# Prompts del Brief cerrado

Modo: herramienta integrada `image_gen`. Imagen de referencia original: `Assets/art/utilities/briefcase.png`.

Las dos primeras salidas incluyeron un damero dibujado en RGB, por lo que no se usaron como sprites transparentes. La tercera genera fondo croma uniforme para la importación técnica de Unity. El dibujo fuente se conserva sin modificar en `Assets/art/ui/brief/brief-closed-source.png`.

## Dibujo cerrado

Use case: precise-object-edit. Asset type: production pixel-art UI sprite for a Unity game. Image 1 is the edit target and strict style/design reference: an open brown wooden briefcase with golden corner reinforcements and one centered golden clasp. Create THIS EXACT SAME BRIEFCASE FULLY CLOSED, a physically believable shut suitcase viewed straight-on from the same slightly elevated camera, horizontally symmetrical. The top lid is a solid broad wooden exterior with the same gold corner hardware, a thin horizontal seam separates top and base, a single centered latch fastens it. No velvet interior, no compartments visible, no duplicated clasps, no upside-down interior or stretched open lid. Match original rich brown wood, yellow gold, chunky stepped pixel outlines and handcrafted pixel shading. Full single object centered on a genuinely transparent RGBA background with generous transparent margin. Closed silhouette width about 1000 pixels and height 450 pixels on a 1280x1280 canvas, width/height about2.2, no handle sticking out above, no characters, no text, no checkerboard, no contact shadow/background. Keep every part of case inside canvas. This will substitute only the fully closed pose while the original remains the open pose.

## Solicitud de transparencia

Use case: background-extraction. Edit ONLY the background of the supplied closed briefcase sprite. Preserve every wooden and gold pixel of the briefcase exactly. The gray and white checkerboard currently visible around it is a PAINTED BACKGROUND, not real transparency: remove it completely. Output an actual RGBA PNG with alpha=0 for every pixel outside the briefcase silhouette and alpha=255 inside the case. DO NOT draw a checkerboard, gray, white, black, or any color in the removed background. No new shading, no new details, no resizing the object, no labels. This is a transparent production sprite to composite over a green Unity game board. Transparent means invisible pixels, not a visible transparency-pattern illustration.

## Fondo croma para importar

Use case: precise-object-edit. Preserve the supplied closed brown-and-gold pixel-art suitcase exactly, including its silhouette, proportions, single front latch and wood/gold pixels. Change ONLY all of the gray-and-white checkerboard surrounding the suitcase into one perfectly flat solid MAGENTA background, exact RGB (255,0,255), #FF00FF. Make ALL pixels outside the suitcase this exact one flat magenta color with no checkers, gradient, texture, shadow, reflection, noise or glow. Keep the suitcase crisp, fully inside the canvas, centered and the same size. The magenta is a technical chroma key for a Unity sprite importer. Do not add magenta to the suitcase, do not redraw it, do not add a handle, text, grid or any new objects.


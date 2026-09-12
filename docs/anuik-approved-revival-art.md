# Resurrección del Anuik amarillo aprobado

Esta hoja sustituye la interpretación anterior naranja/gris sólo como nueva fuente para la resurrección. El usuario corrigió expresamente la identidad y aportó la referencia exacta:

`C:/Users/HPVICT~1/AppData/Local/Temp/codex-clipboard-790a26f9-71a8-41d1-a43f-a2a9c87893a6.png`

Se abrió con `view_image` antes de generar. Es la única referencia visual de esta llamada; no se usaron los diseños anteriores rechazados ni fotografías de otras variantes. La nueva hoja mantiene cabeza ancha dorada/amarilla, enormes orejas angulares que apuntan hacia arriba, ojos negros cerrados en chevrón, ausencia de boca y nariz, cuerpo corto con camiseta azul, corazón blanco y contorno blanco de píxeles. En reposo no se añade un cuerpo humano, pantalones o zapatos; los pequeños pies amarillos aparecen sólo cuando la acción invertida los requiere.

## Procedencia y archivos

- Herramienta: `image_gen.imagegen` integrada en Codex. No CLI/API externo.
- Salida original: `C:\Users\HP VICTUS\.codex\generated_images\01a09256-d6d2-7dc1-a3ff-c6c0faa4c6f6\exec-b34962cb-10c6-4112-aab7-2fbe1c3e5f93.png`.
- Copia de proyecto: `Assets/art/units/generated/anuik-approved/anuik-revive-source-v1.png`.
- Formato de la fuente: PNG RGB, **1254 × 1254**, fondo magenta deliberado.
- La fuente de imagen y la referencia del usuario se conservan sin editar. No se usó Python para modificar imágenes. La lectura de píxeles se hizo sólo para auditoría.
- La creación del derivado RGBA y su importación quedan a cargo del setup central de Anuik; este trabajo no cambia scripts, catálogo, escena ni archivos .meta/YAML.

## Secuencia y lectura

La hoja contiene exactamente **cinco columnas por cinco filas**, 25 dibujos.

| Columna, desde cero | Dibujo |
| --- | --- |
| 0 | Colapso y cuerpo encogido, preparando el apoyo |
| 1 | Parada completa sobre la cabeza; cuerpo azul arriba y pies amarillos hacia arriba |
| 2 | Equilibrio sobre la cabeza con variación de las piernas |
| 3 | Regreso a cuclillas con cabeza y orejas de nuevo levantadas |
| 4 | Figura recuperada de pie, con la misma identidad de la referencia |

Filas de arriba abajo: **S, SE, E, NE, N**. Las tres vistas opuestas pueden utilizar los espejos explícitos ya previstos por el catálogo. Las vistas posteriores no muestran cara ni corazón en la espalda.

Las columnas 1 y 2 son dibujos de la postura invertida: la cabeza ocupa la parte inferior, la camiseta queda por encima y los pies apuntan hacia arriba. El corazón y los ojos frontales se orientan con la inversión. No se pretende simular esta acción con una rotación rígida del sprite de reposo.

Se recomienda conservar una pausa visible usando una secuencia como `0,1,2,2,2,3,4` en `UnitDirectionalAnimation.Revive`. El tiempo exacto y los eventos pertenecen al presentador/simulador central.

## Escala e importación

El sprite de pie en `[fila0,columna4]` sirve como referencia de escala para compararlo con el Idle sur de la hoja principal nueva. Medir ambas anchuras visibles y usar:

`revivePPU = mainPPU × reviveStandingWidth / mainIdleWidth`

Esto evita aumentar el tamaño de Anuik cuando cambia a la hoja de resurrección. Todos los fotogramas de esta hoja deben conservar el mismo PPU relativo; las diferentes posturas no se ajustan una por una.

Los pivotes deben seguir el contacto inferior: pies/cuerpo en las poses normales y cabeza/orejas durante la parada invertida. El recorte técnico debe conservar íntegros el borde blanco y el corazón blanco, sin tratarlos como fondo.

## Auditoría de fuente

Inspección visual completada: amarillo/dorado, azul, corazón y borde blanco presentes; ninguna boca, nariz, prenda inferior ni dibujo anterior naranja/gris incorporado. Las dos posturas invertidas muestran apoyo real de la cabeza.

La lectura de píxeles emuló el criterio de la clave magenta del importador compartido, sin escribir una imagen modificada:

- Proyección horizontal: exactamente **5 bandas**.
- Proyección vertical: exactamente **5 bandas**.
- Píxeles previstos transparentes tras la clave: **1,114,233**.
- Píxeles previstos opacos: **458,283**.
- Píxeles opacos casi blancos (R/G/B mayores de220): **45,862**, conservados por la selección de clave.
- Todas las poses quedan separadas por huecos suficientes para recortar mediante las bandas reales.

La fuente es RGB; estos conteos predicen la conversión técnica y no afirman que ya tenga alfa. La comprobación RGBA real, importación, revisión del tamaño dentro del juego y pruebas posteriores se coordinan desde la tarea principal.

## Prompt exacto

```text
Use case: identity-preserve. Asset: production pixel-art REVIVAL atlas for the EXACT yellow Anuik in the attached user reference.
The reference is authoritative. Preserve this specific design, not any earlier orange/gray interpretation: large GOLDEN-YELLOW broad angular head with slightly flattened top and squared rounded lower corners; enormous upward-pointed yellow ears with angular stepped edges; two solid BLACK happy CHEVRON closed eyes; absolutely NO MOUTH, NO NOSE, NO eyebrows, no pupils. Very short bright BLUE shirt/body with one solid WHITE pixel HEART centered on the FRONT. Tiny yellow arms at the sides. No trousers, shoes, belt, gloves or accessories. The standing reference has no visible legs; maintain the squat shape. The overall width across ears is much greater than the total height. Preserve the reference's bright WHITE OUTER PIXEL OUTLINE, gold interior shadow clusters, blue highlights and white heart. Do not substitute a black outer outline.
Draw exactly FIVE COLUMNS and FIVE ROWS, 25 whole figures, on a square canvas. This is a NEW five-frame headstand revival animation with correctly flexing limbs and ears, not a rigid rotation of the reference. All figures same head/ear size. Leave a generous clear gutter of at least 20% of cell size on every side; ears and white outlines never touch another cell. Shared ground-contact baseline within cells. No grid lines.
Rows from top to bottom: SOUTH(front toward viewer), SOUTH-EAST(lower right), EAST(right profile), NORTH-EAST(upper right), NORTH(rear). Rear views show plain blue back with no white heart and no eyes visible. A side view may show the edge of the heart if correct for that viewpoint.
Columns from left to right:
1 collapsed and curled on the ground, yellow head tilted sideways, blue torso tucked behind it, preparing to push up;
2 FULL HEADSTAND: crown of yellow head touches the ground, enormous ears spread along the ground, blue torso ABOVE the head, two tiny stubby yellow feet pointing UP from the blue hem; front eyes and white heart are truly upside down;
3 HOLD HEADSTAND: crown remains on ground, one stubby yellow leg bent and the other up as he balances, ears flex slightly, blue torso still above yellow head. Same headstand with genuinely redrawn pose;
4 return to low crouch: head comes upright, feet tuck under the blue hem, ears lift, tiny hands help regain balance;
5 fully recovered STANDING reference pose: yellow head/pointed ears, closed black chevron eyes, NO mouth/nose, short blue shirt with white heart, no visible legs under the hem.
Columns2 and3 must unmistakably show HEAD AT BOTTOM, BODY ABOVE HEAD, FEET AT TOP. Tiny yellow feet appear only where the headstand action requires them; do not invent a humanoid lower body or visible trousers.
Style must be AUTHENTIC CHUNKY PIXEL ART: each pose designed on approximately a64x48 logical pixel grid and enlarged with exact hard square pixel clusters. White outline is one logical pixel thick, no blur, no antialiasing, no smooth painterly curves, no gradients, no glossy 3D rendering. Use restrained flat gold/yellow/blue/white/black clusters matched to the reference. Preserve the angular ear silhouette.
All background and empty limb gaps are one perfectly uniform opaque saturated MAGENTA #FF00FF chroma-key matte. This is deliberate export matte, NOT a transparency request. Never draw checkerboard, black background, scenery, floor, cast shadows, particles, extra hearts, labels, numbers, text, captions, frames, logos or watermark. Keep the opaque white outline and heart intact.
```


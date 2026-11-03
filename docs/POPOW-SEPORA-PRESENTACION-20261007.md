# Popow y Sepora — presentación, 7 de octubre de 2026

Popow: retrato de reposo retocado con la herramienta integrada image_gen, preservando el PNG original y las animaciones. La nueva imagen RGBA se copia sin transformaciones a `Assets/resources/MonsterPouch/UI/LobbyFrames/popow-idle-polished.png`. El manifiesto configura su recorte, límites y apoyo de los pies. La figura de Popow en el pedestal del inicio tiene un factor adicional de 0.90; el tamaño de combate no cambia.

Sepora: `WorldWidth` pasa de 1 a 1.2 en `UnitArt.asset`, por lo que sus vistas durante la partida aumentan un 20 %. `DisplayScale` sigue en 1.1 para conservar el tamaño de los menús. No se modifican estadísticas ni casillas.

## Prompt final de la imagen

Use case: lighting-weather / precise-object-edit. Asset type: transparent home-screen character sprite for a pixel-art mobile game. Input image 1 is the EDIT TARGET, Popow. Improve ONLY the lighting, shadow quality and cutout cleanliness of the supplied character. Preserve its exact silhouette, front-facing pose, proportions, enormous round red head, white five-point star, distinctive asymmetric clenched-tooth grin, black boxing gloves, little red torso and short red feet. Retain the original pixel-art visual language with crisp pixel clusters and a restrained palette. Replace the harsh near-black patches on the red head and torso with controlled warm dark-red shading and soft, readable highlights; keep gloves charcoal with subdued highlights. Remove the isolated stray gold/brown pixels outside the body. No scene, no floor, no baked ground shadow, no halo, no text or new features. Actual transparent background. Full character centered with clear padding all around, feet fully visible, one static pose. Keep the broad grin and star exactly recognizable. This is a faithful cleanup of this existing sprite, not a redesign or conversion to 3D.

Entrada: `Assets/art/units/user-sept14/popow/popow-portrait-rgba.png`.

## Comprobación

Revisión visual en Unity del inicio y de Sepora en preparación, con capturas antes/después en `Logs/local-validation/screenshots/popow-*.png` y `sepora-*.png`. Sepora mantiene el apoyo en pantalla `(493.84, 214.58)` y sus semiejes visuales pasan de `(0.67, 0.56)` a `(0.81, 0.67)` unidades.

Las 11 pruebas existentes de `LobbyFramePlaybackPlayTests`, `CharacterPreviewPlayTests` y `LobbyRosterAnimationPlayTests` quedan aprobadas: 10 en la ejecución inicial y la restante en una repetición después de actualizar su referencia al retrato nuevo. El PNG conserva sus bytes y transparencia originales de generación. Proyecto compilado sin errores. No se ha reconstruido el APK con esta revisión visual.

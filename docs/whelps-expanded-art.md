# Kayon, Stein y la animación ampliada de Bugui

Arte preparado el 12 de septiembre de 2026 con la herramienta integrada `image_gen.imagegen`. Las hojas son interpretaciones en pixel art para Monster Pouch; las poses de caminar, atacar y caer se diseñaron para este juego. No se presentan como animaciones extraídas de la colección.

## Referencias inspeccionadas

- **Kayon #76:** se comprobó su panel en la fotografía `100_5354.JPG` del [álbum MegaTrip publicado en Mundo Gogo's](https://mundogogoscrazybones.blogspot.com/2012/07/serie-megatrip-sao-80-novos-bonecos.html). La variante del álbum muestra un cuerpo oscuro con ojos cian. Para leer mejor la forma se inspeccionó también la [fotografía de la figura sin pintar, AK000025.JPG](https://blogger.googleusercontent.com/img/b/R29vZ2xl/AVvXsEjMy25bYw7-5BbW9Eq_-LcaBX_eBbWmDyoY9-PLwqcFexxezXvpchBMv7WzrmLy9JR7GnIsMotSRV21rNoGJWC9tBFl05C3woAsPPJTEllcvavx-rFTXJo7p2KMRGOu3HX8CfFsWcd9rTj1/s1600/AK000025.JPG), enlazada junto a “76- Kayón” en [la colección de Lucas](https://gogosdolucas.blogspot.com/). Se conservaron la cabeza redonda, los ojos inclinados, la boca ondulada, las extremidades cortas y el brazo curvo; el contraste azul se amplió para que se lea en el tablero.
- **Stein #03:** se inspeccionó el panel STEIN de la fotografía `100_5340.JPG` del [mismo álbum MegaTrip](https://mundogogoscrazybones.blogspot.com/2012/07/serie-megatrip-sao-80-novos-bonecos.html), junto al dibujo de Stein en la historieta de `100_5354.JPG`. La hoja conserva el cuerpo crema, el mechón central y el cabello blanco a los lados, la fórmula de la cara y la amplia sonrisa dentada. La pose de descarga eléctrica es una interpretación del ataque solicitado.
- **Bugui:** se inspeccionaron las vistas originales del propio proyecto en `Assets/art/units/local/bugui/`: sur, este, norte y nordeste. Se conservaron las dos orejas triangulares, ojos negros ovalados, sonrisa fina, barriga blanca, manos pequeñas y cola redonda. La nueva hoja añade carga del orbe, extensión de brazos al soltarlo y recuperación; los originales siguen en su ubicación.

Los colores, la silueta y las marcas visibles se tomaron de esas referencias. El ritmo, la deformación de cada pose y las vistas no visibles en las fotografías son adaptaciones del juego. Las tres vistas occidentales se reflejan deliberadamente a partir de las orientales; esto también refleja los pequeños detalles de la fórmula lateral de Stein.

## Archivos e importación

| Unidad | Fuente inmutable | Derivado RGBA que crea Unity |
| --- | --- | --- |
| Kayon | `Assets/art/units/generated/kayon/kayon-animation-source-v1.png` | `Assets/art/units/generated/kayon/kayon-animation-rgba-v1.png` |
| Stein | `Assets/art/units/generated/stein/stein-animation-source-v1.png` | `Assets/art/units/generated/stein/stein-animation-rgba-v1.png` |
| Bugui | `Assets/art/units/generated/bugui/bugui-animation-source-v2.png` | `Assets/art/units/generated/bugui/bugui-animation-rgba-v2.png` |

Cada fuente mide **1942 × 809 píxeles** y contiene doce columnas por cinco filas. Las filas son sur, sureste, este, nordeste y norte. Las columnas contienen:

| Columnas, desde 1 | Acción |
| --- | --- |
| 1–2 | Reposo |
| 3–6 | Movimiento |
| 7–9 | Preparación, contacto y recuperación del ataque |
| 10–12 | Reacción, caída y derrota |

El importador detecta los canales vacíos reales entre las figuras, por lo que no exige que el tamaño del lienzo sea divisible exactamente entre doce y cinco. El contacto del ataque es el segundo fotograma de su secuencia (`AttackContactFrame = 1`); la presentación lo sincroniza con el instante de contacto definido por la simulación.

En Unity, usar **Monster Pouch → Art → Import Kayon Stein and Bugui animations**, o invocar `MonsterPouchWhelpsArtSetup.Setup()`. El setup:

1. Comprueba las tres fuentes y los catálogos antes de importarlos.
2. Conserva cada fuente magenta y crea un PNG RGBA distinto mediante `MonsterPouchSpriteAnimationImporter`.
3. Recorta sesenta sprites por unidad, calcula anclas en los pies y aplica filtrado Point, sin mipmaps ni compresión.
4. Añade o sustituye únicamente las definiciones de Kayon y Stein mediante `ExpandedRoster.CreateKayon()` y `ExpandedRoster.CreateStein()`.
5. Sustituye únicamente las entradas de arte de Kayon, Stein y Bugui. Conserva la definición de Bugui y su ancho visible configurado. Los demás personajes y sus estadísticas permanecen en el catálogo.

El ancho visible base es 0,80 unidades para Kayon y 0,86 para Stein. Para Bugui se conserva `WorldWidth` de su entrada anterior; solo si no existe se usa 0,72. Las hojas no incluyen un proyectil separado: el pequeño destello está unido a las manos y el juego presenta sus proyectiles con el catálogo de efectos. Los Dummies que invoca Kayon reutilizan el arte de Dummy.

Las definiciones y los costes son balance propio editable de Monster Pouch, proporcionado por `ExpandedRoster`; no son estadísticas del juguete.

## Comprobaciones realizadas en esta entrega de arte

Se inspeccionaron visualmente las tres hojas finales. Una lectura independiente de los PNG, sin escribir ni pintar imágenes, aplicó el mismo criterio de clave magenta que el importador para comprobar los canales y las celdas:

| Fuente | Bandas horizontales × verticales | Celdas no vacías | Píxeles visibles en la celda más pequeña |
| --- | --- | --- | --- |
| Kayon v1 | 12 × 5 | 60 de 60 | 7435 |
| Stein v1 | 12 × 5 | 60 de 60 | 6944 |
| Bugui v2 | 12 × 5 | 60 de 60 | 6217 |

Esto comprueba la disposición de las fuentes. La importación efectiva de assets, las pruebas de Unity y el ejecutable se validan en la tarea principal; este documento no presupone que esos pasos hayan terminado.

El primer resultado de Kayon tenía trece columnas. Se corrigió con una segunda solicitud a la herramienta de imágenes; el resultado corregido es la fuente del proyecto. El original se conserva en la carpeta de generación de Codex. Los [prompts completos](whelps-generation-prompts.md) registran tanto la generación como esa corrección.

## Trazabilidad de las fuentes

Los originales de la herramienta están en `C:\Users\HP VICTUS\.codex\generated_images\01a09257-d1a7-7da0-8072-0b5202d21062\`:

| Unidad | Archivo original | SHA-256 de la copia del proyecto |
| --- | --- | --- |
| Kayon corregido | `exec-67972499-31be-4714-b06d-aef9b60612f2.png` | `194462BEC2A16B4B09F5535B01F30DE811E4C61D2DC37239886BC73545D1D386` |
| Stein | `exec-f9f321ae-d093-4c3c-8f16-476f97ddae54.png` | `45FFCFFB7460030E94FED0D5FAACEBEAAB44F19B688AD51D8013F4389FDD9B0E` |
| Bugui | `exec-0f80a54d-3de1-438e-9a5d-16b40b7f0593.png` | `01289B6E0EA24CF34A3772EBAAFA5E906EDB381F3EB13448B3025A45ABAEEFC2` |

Los PNG originales se copiaron, no se movieron ni sobrescribieron.

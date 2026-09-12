# Atori de MegaTrip: referencia e integración

Atori es la figura **01 de MegaTrip**, elegida como un Whelp adicional para Monster Pouch. Este nombre también aparece en otras colecciones de Gogo’s: esta interpretación usa exclusivamente la figura naranja de MegaTrip. El nombre, la colección y los rasgos se separan del balance de juego, que es una propuesta propia y editable.

## Referencia visual comprobada

Se inspeccionó la fotografía `100_5340.JPG` del álbum, publicada en [Mundo Gogos Crazy Bones, «Serie Megatrip»](https://mundogogoscrazybones.blogspot.com/2012/07/serie-megatrip-sao-80-novos-bonecos.html). Es una fotografía de material impreso de la colección alojada por un coleccionista, no una página oficial del fabricante. La ficha «01 ATORI» y su dibujo ampliado aparecen en la página derecha. No se deduce una fecha de publicación de la fecha superpuesta por la cámara.

Los rasgos visibles usados son cabeza naranja muy grande, casi cuadrada y redondeada; cuerpo y pies diminutos; boca horizontal en la parte baja de la cabeza; dos dientes blancos separados; ojos anulares blancos situados a los lados de la cabeza, y dos pequeñas marcas en la parte alta del frente. La vista de espaldas y las poses de movimiento, ataque y caída son interpretaciones nuevas necesarias para el videojuego. No se añaden estrella, guantes, orejas, cuernos, armas ni ropa.

La identificación se contrasta con [Atori (Megatrip), Crazy Bones Pedia](https://crazybonespedia.fandom.com/wiki/Atori_%28Megatrip%29) y la [lista de MegaTrip de LastDodo](https://www.lastdodo.com/en/areas/3379713-gogo-s-series-6-edge-megatrip). No se usa el Atori homónimo de la serie original ni se identifica automáticamente «Boogie» como un personaje distinto. Las referencias consultadas sitúan Bugui y Popow en Urban Toys; se mantienen los nombres de los personajes que ya aportó el usuario.

## Archivos y acciones

- `Assets/art/units/generated/atori/atori-animation-source.png`: salida original de la herramienta de imágenes, con fondo magenta; se conserva intacta.
- `Assets/art/units/generated/atori/atori-animation-rgba.png`: derivado técnico creado por el importador de Unity, con alfa y sin tocar el archivo fuente.
- `Assets/Editor/MonsterPouchAtoriSetup.cs`: método `Setup()` aditivo e idempotente, que agrega o actualiza únicamente la entrada `atori` de los assets existentes `MatchConfig` y `UnitArt`. No cambia `DefaultUnits()`.
- `docs/atori-generation-prompts.txt`: prompts utilizados y rutas originales de generación.

La hoja tiene **1374 × 1145 píxeles**, 12 columnas y 5 filas. La generación dejó márgenes y separaciones ligeramente irregulares: dividir únicamente el ancho entre 12 cortaba algunas figuras. El importador compartido detecta los huecos transparentes entre las bandas de contenido para delimitar cada celda. La auditoría de píxeles encontró exactamente 60 componentes grandes y proyecciones de 12 bandas horizontales y 5 bandas verticales; cada pose queda aislada al usar esos huecos. No se cambia la escala ni se redibuja la fuente para corregir el recorte.

| Posición | Contenido |
| --- | --- |
| Filas 1–5 | Sur, sureste, este, noreste, norte |
| Columnas 1–2 | Reposo: 2 fotogramas |
| Columnas 3–6 | Caminar: 4 fotogramas |
| Columnas 7–9 | Cabezazo: anticipación, contacto, recuperación |
| Columnas 10–12 | Caída: retroceso, descenso, reposo en el suelo |

Las vistas suroeste, oeste y noroeste reflejan horizontalmente las vistas equivalentes del lado derecho; se declara en `FlipX`. Son 60 poses generadas, con cuatro acciones disponibles en las ocho direcciones. La animación de ataque usa el contacto del simulador. Caminar y reposo tienen cadencias propias; la caída usa la duración compartida de la presentación.

Se comparó el estilo con las vistas sur de Popow y Bugaloo aportadas por el usuario. La hoja mantiene la silueta característica de Atori y el contorno oscuro con colores escalonados. Las acciones son interpretaciones pixel art generadas; no son animaciones extraídas de otro juego.

## Balance provisional de Monster Pouch

Atori es un Whelp de cuerpo a cuerpo: 36 de vida, 6 de daño, ataque cada 1,05 segundos, preparación del ataque de 0,3 segundos, alcance 1, movimiento cada 0,4 segundos, IQ 7 y coste 3. Prefiere la zona media de la formación y un enemigo alcanzable con menos vida. «Cabezazo certero» añade 4 de daño cada tercer impacto. Los tres Tricks son «Cabeza dura» (+1 armadura, coste 2), «Impulso» (+2 daño, coste 2) y «Aguante» (+16 vida, coste 3). Ninguno de estos valores o poderes se atribuye a Gogo’s ni al álbum.

## Alcance de la verificación

La referencia impresa y ambas salidas de generación se inspeccionaron visualmente. La primera salida tenía un tablero de transparencia pintado y se descartó para importar. Una segunda llamada reemplazó ese fondo por magenta. La extracción de clave ocurre solamente en una textura derivada mediante el importador compartido de Unity. Una lectura de componentes y proyecciones de píxeles comprobó la separación de las 60 poses y detectó el problema de los recortes uniformes descrito arriba. La compilación, ejecución final de `Setup()` con el recorte corregido, comprobación de alfa, pruebas de partida y revisión dentro del ejecutable corresponden a la validación central del proyecto; esta ficha por sí sola no afirma que hayan pasado.

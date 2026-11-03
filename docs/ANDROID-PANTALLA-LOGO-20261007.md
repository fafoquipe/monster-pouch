# Android: pantalla completa, textos e identidad visual — 7 de octubre de 2026

La interfaz conserva las proporciones de las figuras y del tablero. El fondo del menú llena la pantalla física; el encabezado y la navegación aprovechan la altura de la zona segura. Una cámara de escenario completa los bordes durante las partidas con la misma escala y alineación del tablero. Se retiraron los cuatro paneles negros. Android tiene renderOutsideSafeArea y pantalla completa activados; los controles siguen dentro de Screen.safeArea.

Se quitaron nombres de personajes del pedestal, estantes y cuadrícula, los rótulos MONSTER/WHELP, el subtítulo repetido del perfil y el título repetido del brief. Se conservan nombres en las fichas informativas, selección del brief, botones, contadores útiles y estadísticas. El fondo clásico se limpió para quitar PLAYER NAME, el texto del cartel y la navegación dibujada que duplicaba los controles reales. La versión lounge ya carecía de esos textos.

## Arte suministrado
El archivo original del usuario se conserva sin cambios de píxeles en:
`Assets/resources/MonsterPouch/UI/Branding/supplied-logo-and-icon.png`.

El logo se importa mediante un rectángulo de sprite y aparece exclusivamente durante el arranque de la aplicación, en el splash nativo de Unity. Se retiró de la lobby. El sprite persistente está en `Assets/art/branding/launch-logo.asset`. La imagen de la derecha se asigna al icono Android convencional, redondo y adaptable. Los recursos de importación están en `Assets/art/branding/`. El icono adaptable conserva margen para las máscaras de los lanzadores.

## Interacción y banco
- La pantalla permanece encendida durante preparación, combate y resultados de la partida. Al volver a la lobby o pasar la aplicación a segundo plano se restaura el ajuste de suspensión que tenía el dispositivo.
- Banco de tres tipos de tropa. El maletín está cerrado, se abre al arrastrar una tropa encima y vuelve a cerrarse al soltarla o alejarla. Tocarlo muestra las tres casillas; se puede arrastrar una tropa guardada al campo, o seleccionarla y luego tocar la casilla de destino. Se conservan las mismas instancias, copias, mejoras y monedas invertidas. Sus tipos quedan excluidos de las ofertas hasta retirarlos del banco. Un cuarto tipo se rechaza antes de cobrar.
- La colección tiene pestañas MAZO y ESTILOS. Los estilos ya no están en Configuración. La tarjeta seleccionada muestra DETALLE en azul encima de USAR en verde, dentro de la cuadrícula. Los botones se mantienen visibles al seleccionar la última fila.
- Las cinco secciones principales se desplazan horizontalmente al pulsar sus pestañas o deslizar a izquierda/derecha. La barra inferior permanece fija. El desplazamiento vertical de la colección conserva su función; las fichas de detalles y configuración no cambian de sección al arrastrar.
- El protocolo de sala es `mp-2026-10-08-v3-ability-timing`; ambos dispositivos deben usar la misma versión para evitar que un cliente antiguo interprete el banco como una sola casilla.

## Compilar manualmente
Por instrucción del usuario, no se generan nuevos builds de Android sin que lo pida. El build iniciado antes de esa instrucción terminó en `Builds/Android/fullscreen-20261007-231240`; no incluye este rework posterior.

Para generar el APK actualizado desde Unity:
1. Salir de Play Mode y usar **Monster Pouch → Guardar proyecto y copia de balance**.
2. Elegir **Monster Pouch → Build → Android APK**. La variante **Android APK (desarrollo)** añade las herramientas de depuración y la marca de Development Build.
3. El resultado queda en una carpeta fechada dentro de `Builds/Android/` y, si termina correctamente, se copia a `Builds/Android/latest/Monster-Pouch.apk`. El informe `build-report.json` indica el resultado y la ruta.

No se ha generado un APK ni un ejecutable de Windows con este rework. La comprobación actual se realiza dentro del editor.

## Fondo limpio
Modo: herramienta integrada image_gen. Entrada: `Assets/resources/MonsterPouch/UI/deck-background-no-whelp-lights.png`. Salida nueva: `Assets/resources/MonsterPouch/UI/deck-background-clean.png`. Se conserva la fuente anterior.

Prompt final:
Use case: precise-object-edit. Input image 1 is the EDIT TARGET, an existing pixel-art mobile game shelf background. Remove ALL embedded text from this background: remove PLAYER NAME from the gold plaque (leave a clean blank gold plaque), remove STRONGER TOGETHER and its punctuation from the right hanging yellow poster (leave a plain matching yellow poster), and remove the entire five-button navigation artwork at the bottom, including icons and SHOP, DECK, PLAY, QUESTS, REWARDS, replacing that bottom area with a seamless simple blue base matching the shelf surround. Keep every other part unchanged. Exact same front view, full canvas aspect ratio, pixel-art style, lighting and composition. Critically preserve the exact normalized positions, widths and heights of all EIGHT empty pedestals: one large at the top, four in the middle and three at the bottom. Preserve shelves, plant, boxes, skull emblems, crest and frame. No characters, no new objects, no new words. Background stays fully opaque. This is a surgical cleanup of baked text and navigation, not a redesign.

## Verificación
La suite de lógica del 8 de octubre aprobó 691 pruebas, incluidas reglas de banco, sincronización local de pares online, tiempos de habilidad, daños y cancelaciones. La revisión visual utiliza Game View vertical 393 × 873. No hubo compilación Android después de la instrucción del usuario.

Los ajustes posteriores de distribución y habilidades están documentados en `HABILIDADES-Y-BANCO-20261008.md`.

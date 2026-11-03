# Deslizamientos consecutivos del menú

Había dos causas reproducibles: `BeginMenuSwipe` rechazaba cualquier gesto mientras existiera una animación de transición, y las zonas vacías del menú enviaban el arrastre a `BoardPointerGesture`. Durante la transición se desactivaba la recepción de toques de las páginas, por lo que un segundo toque podía quedar capturado por ese control del tablero.

El menú ahora tiene una superficie transparente persistente detrás de sus controles. Esta superficie envía los arrastres al manejador de navegación, cubre también los márgenes de la pantalla y se desactiva al entrar en combate. Un gesto nuevo termina de colocar la página elegida y empieza el siguiente arrastre sin exigir una nueva pulsación.

Las tres pruebas de regresión fallaron antes de aplicar la corrección, con estos resultados:

- Zona vacía: se esperaba el manejador del menú y se recibía el del tablero.
- Gesto durante transición de botón: la pestaña permanecía en 1 cuando debía pasar a 2.
- Gestos consecutivos: el segundo gesto dejaba la pestaña en 3 cuando debía pasar a 4.

Registro de la reproducción: [before.xml](before.xml).

Los cambios están en `local-menu-navigation.cs`, `LocalGameUI.cs` y `local-viewport-layout.cs`. Las nuevas pruebas de regresión están en `mobile-menu-bank-play-tests.cs` y cubren 24 gestos consecutivos, cambios de sentido, transiciones iniciadas con botón, retorno de un gesto corto, recepción real de raycasts y retirada de la superficie durante el combate.

Resultado después de la corrección: **10 pruebas Play Mode aprobadas, 0 fallidas**, incluidas las tres reproducciones anteriores y las comprobaciones existentes de colección, cancelación, límites de pestañas y área segura. Registro: [after.xml](after.xml).

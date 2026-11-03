# Rediseño integral — estado del trabajo

## Respaldo y alcance

`Backups/before-full-redesign-20261013-194312.zip` conserva Assets, Packages, ProjectSettings, docs, BuildTools y el estado previo de Git; su CRC fue comprobado. Hay copias adicionales de las revisiones de parque y casino. Los cambios ajenos existentes se conservan.

Se incorpora el documento adjunto y las correcciones posteriores del chat. Se mantienen tres ambientaciones y las identidades de red/guardado. Los personajes nuevos los aportará el usuario; su rediseño visual está aplazado. La última corrección sí pide para Grand Casa Hotel un casino inspirado en Balatro, más pixelado, conservando carro y reloj.

## Implementado

- Botones con caras y bordes geométricos, presión y rebote sin desplazar el área táctil. Se conservan la transición de mapas y las animaciones del Brief.
- Final del swipe corregido para evitar una segunda copia fija del fondo. Fondos a pantalla completa; interfaz respeta el área segura.
- Ambientaciones cuadradas y extensibles: las pantallas anchas muestran más entorno. Personajes conservan anclaje de pies y escala por personaje.
- Parque con 473 piezas de decoración y 26 sprites distintos: juguetes, pelotas, cuatro estanterías, castillo, banderines y nubes. Atlas V2 independiente del suelo repetible.
- Campo Real conserva marco/tablero con columnas, trono, plantas y candelabros separados.
- Grand Casa Hotel mantiene carro, reloj y marco. Tapete verde original ligeramente oscurecido, sin la doble capa anterior, 29 accesorios de casino, líneas de apuestas y sombras por silueta; detalles en CASINO-Y-HUD-20261013.md.
- Estanterías modulares del Brief: laterales, cornisa, baldas y pedestales con centros repetibles y bordes conservados.
- Luz ambiental y lámparas afectan al color de sprites por posición/distancia. Sombras de contacto y proyección para decoración. No usa normales ni un sistema de iluminación física.
- Lobby y mapa tienen selección independiente. Fondo, botones y mueble aplican la paleta del lobby; el mapa mantiene las reglas de selección y sincronización de la partida.
- Tsu: mejora con +50% de velocidad de movimiento/ataque, reducción del 30% del daño recibido o +50% de ataque. El escudo reduce 10 a 7, sin añadir una reserva basada en vida. Velocidad propia de proyectil y valor automático conservados.
- Tokoro usa únicamente el daño de habilidad configurado, sin multiplicador adicional de ataque. Etiqueta simplificada a «Daño de habilidad».
- Sepora limita sus acumulaciones por ronda y conserva el incremento permanente. Popow separa la elevación/caída visual rápida de la duración del aturdimiento.
- Barras aliadas celestes desde la perspectiva del jugador local.
- 19 configuraciones por personaje separan definición, ataque, habilidad y tres mejoras. Copias de runtime evitan modificar los assets durante combate.
- Editor central de balance con búsqueda, filtros, comparación, guardado, duplicación y secciones de habilidad/mejoras.
- Nombres visibles actualizados conservando IDs; archivo histórico de arte y tabla de migración.
- Registro de estados y política de acumulación conectada a varios efectos; indicadores priorizados y sin consultas completas cada frame.
- Progresión persistente: XP por partida/victoria, niveles, recompensas configurables e historial idempotente.
- Contadores de daño con tamaño proporcional limitado; cuenta atrás central 10/5 segundos, nombre real y check del rival online.
- Compresión Android ajustada para nuevos fondos/atlases; auditoría inicial de recursos y tamaño base.

## Validación actual

- 761 pruebas de EditMode aprobadas en la ejecución final.
- 45 pruebas de PlayMode aprobadas: 15 del casino/HUD y 30 de integración de mapas, muebles, temas, viewport y lógica online.
- Se corrigió el ocultamiento inmediato del contador al abrir un modal. Se adaptó la prueba de muebles a su material de paleta actual.
- Capturas reales de Game View en 1080×2340, 1600×2560 y 1920×1200.
- Informes finales y límites en VALIDACION-CASINO-HUD-20261014.md. Compilación Android universal en curso.

## Trabajo que sigue abierto

- Recibir e integrar los nuevos diseños de personajes.
- Completar el uso de todos los metadatos del catálogo de estados (duración/intensidad/condiciones y prefabs visuales) en todos los comportamientos heredados.
- Conectar los campos de prefabs/sonidos opcionales de ataque y habilidad donde aún son solo datos; ampliar controles del editor para los casos no expuestos.
- Mejorar la presentación de recompensas desbloqueables y de subida de nivel al terminar la partida.
- Generar APK comparable, medir el tamaño final y rendimiento en un Android físico. El APK base universal mide 356,721,667 bytes; no hay una cifra final medida.
- Comprobar Relay entre dos dispositivos y rendimiento/RAM/FPS reales. Las pruebas locales del protocolo no equivalen a esa comprobación.
- Una habilidad nueva con reglas inéditas todavía requiere código; un cuarto mapa requiere ampliar el registro/enum de red actual. El procedimiento está documentado en GUIA-BALANCE-Y-AMBIENTACIONES.md.

Este documento registra implementación y límites; no afirma que el encargo completo esté terminado.


# Salas y ajustes — 8 de octubre de 2026

## Cambios

- Batalla abre dos tarjetas verticales: Bot y Crear sala. Desde Sala de amigos se puede crear una sala o unirse con código.
- Sala de espera con código copiable, los dos jugadores, el Brief y los botones Abandonar e Iniciar. El anfitrión inicia cuando el rival se ha conectado.
- Código de seis caracteres en mayúsculas; botones Copiar/Pegar y ajuste del panel sobre el teclado móvil. Se conserva la entrada nativa del teclado.
- Navegación: Tienda, Brief, Batalla, Guía, Desafíos. Tienda y Desafíos quedan preparados sin compras ni torneos implementados. La colección se abre tocando al héroe; Sonido permanece en Ajustes.
- Los fondos del Brief y la pantalla completa comparten el mismo encuadre, evitando repetir o cortar la franja superior.
- Botones Detalle/Usar más bajos. Listo se llena de verde y muestra el check gradualmente; puede cancelarse durante la preparación. En Bot hay 1,2 segundos para cancelar antes de empezar. Al vencer el tiempo se inicia automáticamente.
- El online usa revisiones de Listo y una confirmación de ambos clientes antes de revelar las órdenes. Corregido el cierre del transporte al abandonar la sala.
- Gancho Barrido de Popow: cada 3 ataques, golpea el objetivo y las dos casillas laterales y aturde 2 segundos. Frecuencia y duración editables en sus parámetros. La descripción usa los valores actuales.
- Aky tiene un catálogo exclusivo de combate con alas unidas al cuerpo y aleteo durante el vuelo. No se modificó el catálogo original del lobby.
- El icono adaptativo de Android incluye margen para que la máscara del lanzador conserve el dibujo completo.

## Validación

- 66 pruebas EditMode aprobadas: combate, balance, preparación, temporizador, Popow y sincronización de rondas.
- 6 pruebas PlayMode aprobadas: navegación, mayúsculas y portapapeles, continuidad del fondo, zona segura y conexión entre dos clientes.
- La prueba de red usa conexiones locales reales: verifica la espera, inicio por anfitrión, cancelación concurrente de Listo y resultados idénticos al finalizar una ronda. No sustituye una prueba de Relay entre una laptop y un teléfono.
- Revisadas capturas reales de Unity a 393 × 873. El teclado físico de Android queda pendiente de comprobar en un dispositivo.
- Comparación con la copia anterior: UnitArt.asset idéntico; MatchConfig.asset solo cambia la activación, frecuencia y duración de popow-sweep. El coste y el resto del balance se conservan.
- Copia completa: Monster-Pouch-backup-20261008-064735.zip; SHA-256 ed1453a2b314013fce5d2696599a90e90cdb11b974a323bafbb3db91d601d7bd.

## Compilación

El menú Monster Pouch → Build ofrece Android APK y Windows EXE. Cada compilación usa una carpeta con fecha y hora. Windows se entrega con su carpeta de datos y dependencias; el ejecutable aislado no es suficiente. La APK y Windows deben usar esta misma versión del protocolo online (v7).

Los resultados de las builds y sus comprobaciones se guardan junto a cada salida. La eliminación de builds anteriores se realiza únicamente después de verificar ambas nuevas.

## Entrega verificada

- APK: `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch\Builds\Android\2026-10-08_06-53-22\MonsterPouch_2026-10-08_06-53-24.apk`. SHA-256: `9d65c7d560ccf1831b9722b9648a1c149541258f146f21c0cf7ea761daabac58`. Firma e integridad ZIP correctas, ARM64, sin modo de desarrollo.
- Windows: `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch\Builds\Windows\MonsterPouch_2026-10-08_06-47-57_Windows.zip`. SHA-256: `fef9e6400c926f9090ba1b65affd3f66f0c5f049bdb30bf78a6a368801e05a7a`. EXE x64 y dependencias verificados; arranque sin excepciones durante 12 s en modo sin gráficos.
- El icono adaptativo extraído de la APK conserva todos sus píxeles visibles dentro de la zona segura circular (radio visible 104,42 px; límite 132 px a 432 × 432).
- Eliminadas la APK anterior y la compilación intermedia de Windows.
- La prueba de conexión volvió a pasar tras cancelar también el temporizador de desconexión al retirar Listo.

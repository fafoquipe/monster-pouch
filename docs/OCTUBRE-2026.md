# Monster Pouch — cambios del 7 de octubre de 2026

## Partidas por Internet

En el menú principal, selecciona tu equipo y pulsa **JUGAR ONLINE**. Un jugador crea la sala y comparte el código de seis caracteres. El otro introduce ese código y pulsa **UNIRME A LA SALA**. Ambos necesitan la misma versión y el mismo balance.

Cada jugador compra y coloca su equipo en privado. El combate empieza antes si ambos pulsan **LISTO**; si alguno no lo hace, comienza al terminar los **40 segundos** de preparación. El anfitrión coordina las rondas; cada cliente comprueba que el resultado coincida. El jugador invitado ve su lado del tablero abajo.

Proyecto Unity Cloud vinculado: **Monster Pouch**, ID `c600bc6a-62a6-4cf3-8dc2-9657e7c301a4`. La conexión utiliza Authentication anónimo y Unity Relay con DTLS, mediante Multiplayer Services 2.3.3 y Netcode for GameObjects 2.13.3.

Esta primera versión es para partidas casuales de dos jugadores. No incluye reconexión, sustitución del anfitrión, matchmaking público ni servidor dedicado para partidas competitivas. Al cerrar o suspender la aplicación se abandona la sala. Se deben crear salas nuevas después de una desconexión.

## Habilidades

- **Blotan, segunda mejora: Truco de Fuga.** Al generar monedas se oculta durante un segundo y se teletransporta a una casilla libre elegida al azar. Conserva su animación de monedas y no puede moverse ni atacar durante ese segundo. Si no queda ninguna casilla libre, se oculta sin teletransportarse. Se conserva el identificador antiguo `blotan-health` para mantener las mejoras desbloqueadas; ya no añade vida.
- **TSU, chicles explosivos.** Cada aplicación de daño periódico produce pequeñas explosiones y fragmentos de chocolate, sincronizados con el daño real.

## Pantalla Android y guardado

El juego utiliza orientación vertical y ajusta conjuntamente la cámara, los controles y la interfaz al área segura del teléfono. Conserva las proporciones del tablero, con márgenes cuando el dispositivo tiene otra relación de aspecto. Respeta recortes de cámara y navegación del sistema.

Los cambios de balance hechos en los assets del editor se guardan automáticamente cada tres segundos fuera del modo de juego. El menú **Monster Pouch → Guardar proyecto y copia de balance** guarda también las escenas y genera una copia JSON con fecha en `Backups/Balance`. Los valores temporales de una partida no se copian al balance.

Antes de modificar las habilidades se guardaron los assets y las escenas, y se creó una copia del proyecto. La migración de Blotan conserva el coste de la mejora y los demás valores de balance existentes.

## Validación

- 671 pruebas de lógica en Edit Mode aprobadas.
- 13 pruebas de Play Mode aprobadas: presentación de Blotan y TSU, bordes del área segura, disposición del tablero, intercambio de casillas y vista previa del rival.
- 11 pruebas de multijugador aprobadas después de la última corrección: incluye empezar al estar ambos listos, esperar al segundo jugador hasta agotar el tiempo y poder confirmar incluso al alcanzar el límite de decisiones de preparación.
- Pruebas de simulación con dos jugadores: decisiones privadas, sincronización de dos rondas, rechazo de posiciones y rondas inválidas, y perspectiva del invitado.
- Partida real a través de Authentication y Relay: editor y cliente Windows independiente completaron dos rondas con hashes de resultado idénticos. Informes `online-host.json` y `online-guest.json`, ambos con `passed: true`. Los dos procesos estaban en el mismo PC y utilizaron Relay por Internet; aún falta probar dos teléfonos en redes diferentes.

Los informes de Unity se encuentran en `Logs/local-validation`. La comprobación en Game View no sustituye la prueba táctil en un teléfono físico.

## Instalador Android

APK de desarrollo: `Builds/Android/latest/Monster-Pouch.apk`, para ARM64 y Android 7.1 o posterior. El informe y el SHA-256 están junto al archivo.

La compilación de Unity completó el código nativo IL2CPP, pero el primer empaquetado encontró un fallo de Java al crear su conexión local. Se completó `assembleDebug` directamente sobre el proyecto Gradle generado, dando a Java y a su daemon la carpeta explícita `Library/mp-sockets` mediante `JAVA_TOOL_OPTIONS`, solo durante esa ejecución. Gradle terminó correctamente; se verificaron la firma APK v2, el CRC del archivo, ARM64 y el permiso de Internet. Se conserva el registro del intento inicial.

El constructor Android del proyecto incorpora el mismo ajuste temporal y restaura la variable al finalizar. La base técnica del ajuste está documentada en [Java Networking de Oracle](https://docs.oracle.com/en/java/javase/17/core/java-networking.html).

# Habilidades y distribución — 8 de octubre de 2026

## Ajustar una habilidad en Unity

Seleccionar el balance del juego (`Assets/resources/MonsterPouch/MatchConfig.asset`) y el personaje en su inspector. Los campos aparecen dentro de **Habilidad propia** o de la mejora correspondiente. Los parámetros nuevos tienen valores de respaldo; no se reemplazan las estadísticas ni los cambios de balance guardados por el usuario.

- **Preparación / hasta el golpe** (`castDuration`): segundos antes de ejecutar o lanzar la habilidad. 0 es instantáneo. -1 hereda la preparación del ataque básico, que es el valor inicial de los golpes periódicos de Atong y TSU. Las habilidades por energía tienen tiempo propio.
- **Mantener pose de impacto** (`holdDuration`): tiempo sosteniendo la pose después de soltar el golpe.
- **Animación de regreso** (`recoveryDuration`): tiempo para volver desde esa pose al estado normal. La siguiente acción espera a que termine la secuencia, además de respetar sus reglas de ataque, aturdimiento o movimiento.
- **Daño de habilidad** (`damageOverride`): número independiente del daño básico. -1 conserva el cálculo anterior del personaje y sus multiplicadores; 0 causa cero daño. Gochan ya tiene su campo `damage`. La ejecución de Tauris conserva su mecánica especial de vida actual. Curaciones, invocaciones, reflejo y otras habilidades sin daño directo conservan sus parámetros específicos.
- **Duración de esta animación** (`animationDuration`): para animaciones reactivas como monedas de Blotan, invisibilidad de Sepora, radiación de Jazar y sueño de Tokoro; 0 conserva el clip original.
- **Recorrido de los helados** (`travelDuration`): en Heladería Improvisada de Tokoro. 0 coloca los conos inmediatamente; el valor inicial es 0,4 s. Salen del centro de Tokoro hacia las casillas vecinas y solo se pueden recoger al llegar.

El tiempo de daño no depende del catálogo visual, del rumbo del sprite ni de que exista un componente de presentación. El combate usa pasos de 0,1 s: una preparación de 0,65 s se ejecuta en 0,7 s. La animación se ajusta a ese mismo instante. Los campos genéricos del catálogo de arte se conservan como respaldo para otras animaciones.

Tokoro usa inicialmente 0,5 s de preparación, 1 s con la pose del cabezazo y 0,2 s de regreso. Su propio aturdimiento no corta esa pose; una interrupción durante la carga sí la cancela. Sepora carga 0,6 s antes del primer rayo y la duración de su tormenta empieza al soltarlo. Stein carga 0,65 s. Vuelo Rasante de Aky mantiene preparación 0.

La ficha DETALLE muestra el daño de las habilidades ofensivas, incluyendo el valor independiente cuando se configura. En daño por porcentaje, reflejo o ejecución se conserva la descripción de esa mecánica.

## Interfaz

- Máquina de cartas y su sombra desplazadas 48 píxeles lógicos hacia la derecha; el botón de pausa conserva su lugar.
- Tokens y rerolls juntos a la derecha del brief, con sus iconos circulares, números blancos y contorno oscuro. Sin panel rectangular de fondo ni tokens debajo del brief.
- El bloque inferior baja 12 píxeles lógicos. El banco se alinea usando la altura real del sprite ajustado a su proporción; no se alinea su rectángulo transparente. Recursos y Listo acompañan ese ajuste conservando sus separaciones.
- Banco cerrado de tres tropas con su base a la misma altura que la base del brief principal, y los contadores encima; al abrirlo, sus tres tropas aparecen dentro de la franja inferior del brief. Las ofertas quedan ocultas y dejan de recibir pulsaciones hasta cerrarlo; el panel no cubre casillas del tablero.
- Botón LISTO pequeño sobre los recursos de la columna del banco, según la última referencia del usuario. Al pulsar aparece un ✓, cambia a verde y hace un pulso de 0,24 s antes de confirmar. Online mantiene el check mientras espera al rival; la lógica de ambos listos o fin del tiempo continúa vigente.

## Compatibilidad y validación

Protocolo online `mp-2026-10-08-v3-ability-timing`. Los dos dispositivos deben ejecutar esta versión: un cliente anterior no conoce los tiempos nuevos. La suite de lógica aprobó 691 pruebas, incluidas 16 nuevas sobre daño, preparación, interrupciones, recuperación, pose de Tokoro y recogida de conos.

Tras bajar y alinear el bloque inferior, las 10 pruebas de interfaz de `MobileMenuBankPlayTests` y `CardTableArenaPlayTests` aprobaron: banco, gesto de navegación, check de Listo, política de pantalla encendida y controles dentro del área segura en proporciones de móvil, tableta y horizontal. La base del banco se comprueba contra la altura visible del sprite; el área táctil también queda dentro de la pantalla.

La verificación se realiza en Unity. No se generó una nueva build Android ni de Windows. Para generar el APK cuando se desee: **Monster Pouch → Guardar proyecto y copia de balance**, después **Monster Pouch → Build → Android APK**.

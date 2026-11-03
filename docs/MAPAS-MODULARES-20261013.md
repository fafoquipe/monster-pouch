# Mapas y estanterías por piezas

## Ambientación

Parque de Juegos y Campo Real usan `Assets/resources/MonsterPouch/Styles/classic-scenery.asset` y `royal-scenery.asset`. Cada asset se enlaza desde `EnvironmentStyle.Scenery`. Grand Casa Hotel usa lounge-scenery.asset para el tapete y accesorios de casino, y conserva su carro, reloj/torre, tablero y marco originales de escena. Su revisión se describe en CASINO-Y-HUD-20261013.md.

Cada elemento tiene nombre, sprite, anclaje, posición del pie, ancho, inversión horizontal, sombra, orden de dibujo y luz opcional. Los anclajes se calculan respecto al marco del tablero. Cambiar la relación de aspecto no modifica las coordenadas ni la escala de las piezas. El editor permite modificar posiciones y tamaños durante Play Mode para previsualizar; esos cambios de Play Mode deben trasladarse al asset guardado si Unity los descarta al salir.

El suelo y la pared son capas independientes. La geometría cubre una región cuadrada mayor que la pantalla. La madera del suelo se repite a escala constante; el parque usa una textura de espuma turquesa repetible y Grand Casa un tapete verde uniforme. La doble capa anterior del tapete se desactiva. Las paredes, alfombras, rodapiés y sombras se dibujan en una malla. Las figuras decorativas son SpriteRenderers separados. El parque ahora contiene 473 piezas con 26 sprites diferentes y un atlas V2. Las sombras proyectadas se agrupan por textura; cada textura conserva su material para evitar mezclar sprites de atlases distintos. El modo Tabletop del hotel omite la pared y sitúa las marcas de apuestas fuera del tablero. No hay una ilustración completa agrandada detrás del tablero.

El marco inflable y el tablero de madera siguen usando sus recursos anteriores. La sombra inflable tiene las esquinas recortadas y se retiró el rectángulo de relleno que se veía fuera de las curvas. La cámara usa un encuadre fijo de 12.2 para dejar espacio alrededor. Las casillas, sus coordenadas lógicas y el tamaño relativo de los personajes no cambian entre dispositivos.

Los candelabros incluyen fuentes de luz de color configurables. Afectan al color de los sprites según distancia y se eliminan al cambiar de mapa. Es iluminación de paleta, sin URP ni sombras dinámicas sobre los personajes. El lobby mantiene su luz ambiental independiente.

## Estanterías

`Assets/resources/MonsterPouch/Styles/LobbyFurniture.asset` referencia cuatro piezas: balda, pedestal, cornisa y lateral. El atlas se conserva intacto; Unity crea los sprites mediante su editor de recortes. Balda, cornisa y lateral tienen bordes configurados y centro repetible; no se estira una imagen del mueble entero.

El Brief ensambla dos laterales, tres baldas, una cornisa, tres paneles de fondo y ocho pedestales. Los personajes conservan sus puntos de apoyo: protagonista a Y=291, primera fila a Y=514 y segunda fila a Y=710, en unidades del diseño 540×960. Los pedestales conservan su proporción. `PedestalFootHeight` define dónde apoyan los pies en la superficie superior. La vista inicial usa el mismo pedestal como pieza independiente.

Las tarjetas de estilos tienen un margen inferior de 20 unidades y lateral derecho de 20 para que el botón y su animación queden dentro del marco.

## Recursos y recuperación

Los atlases nuevos están en `Assets/art/ambientations/modular/`. Los PNG originales generados y sus prompts se conservan. Los dos fondos cuadrados de arena rechazados se archivaron en `ArtArchive/RejectedScenery-20261013`, fuera de Assets; no se incorporan al APK. El respaldo previo al trabajo está en `Backups/before-full-redesign-20261013-194312.zip` y su CRC fue comprobado.

Las ambientaciones del lobby continúan como fondos cuadrados independientes. Las estanterías y las figuras no están pintadas dentro de ellos. El selector mantiene separadas la ambientación local del lobby y la ambientación compartida de la partida.

## Comprobación

Las capturas se producen en el editor abierto con tamaños reales de Game View 1080×2340 y 1600×2560; también se ha revisado 1920×1200 para ver los márgenes amplios. No equivalen a una prueba en un teléfono físico. El resultado definitivo de pruebas se registra en `REDISENO-INTEGRAL-20261013.md`.

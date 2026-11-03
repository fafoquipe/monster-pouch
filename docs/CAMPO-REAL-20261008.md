# Campo Real y estilos

En BRIEF → ESTILOS hay tres opciones:

- **Predeterminado** conserva la presentación original. No se diseñó ni añadió un mapa default: por ahora sigue usando el escenario de batalla existente.
- **Grand Casa Hotel** corresponde al antiguo estilo oscuro «Salón de cartas», con su estante verde petróleo, botones y decoración de casino. Conserva las selecciones guardadas como `lounge`.
- **Campo Real** añade un salón real luminoso, casillas alternadas de roble y nogal, marco de madera con incrustaciones doradas y decoración de trono, estandartes, columnas y laureles. También cambia la pantalla principal, el estante y los botones a burdeos, crema y dorado.

La elección persiste al cerrar y abrir el juego. Es cosmética: no cambia las reglas, el tamaño de las casillas, las posiciones de las tropas, el balance ni la distribución de banco/brief/recursos. Cada dispositivo puede elegir su presentación.

## Arte

Cinco PNG nuevos en `Assets/resources/MonsterPouch/UI/TableSkins`: `royal-arena.png`, `royal-wood.png` (marco), `royal-wood-soft.png` (casillas), `royal-deck.png` y `royal-home.png`. Se generaron con image_gen y se copiaron sin modificar los PNG originales. Los prompts completos están en `Assets/art-prompts/campo-real-20261008.md`.

El tablero real se coloca sobre las mismas 60 casillas del mapeador existente. La decoración original se oculta solamente mientras Campo Real está activo y se restaura al cambiar de estilo. No se sobrescribió la escena ni el prefab de Grand Casa Hotel.

## Ideas para el futuro mapa predeterminado — sin implementar

1. **Juguetería artesanal**: mesa clara de arce, casillas arena y caramelo, fondo crema; solo un pequeño tren de madera, bloques y una caja en los extremos. Poca saturación y mucho espacio despejado.
2. **Mesa junto a una ventana**: tablero beige y café suave, luz de mañana, una maceta pequeña y un par de juguetes de madera al fondo.
3. **Taller de miniaturas**: superficie de abedul, herramientas pequeñas ordenadas fuera del tablero y estantes discretos. Colores apagados y sin efectos luminosos.

La primera opción encaja especialmente con las figuras coleccionables y permite que Campo Real y Grand Casa Hotel sigan sintiéndose especiales.

## Validación

El 8 de octubre de 2026 pasaron 3/3 pruebas de Play Mode (`RoyalStylePlayTests` y `TableSkinsPlayTests`). Verifican selección y persistencia, navegación y controles táctiles, conservación de figuras y mazos, posición y dimensiones de las 60 casillas de madera, colocación en todas las casillas del jugador, restauración de la decoración del hotel y cobertura de toda la pantalla. Se revisaron visualmente capturas del editor a 393 × 873.

Resultados: `Logs/local-validation/royal-styles-play.xml`. Capturas: `Logs/royal-style-validation/` (inicio, brief, estilos y partida de Campo Real, más brief del hotel).

No se generó ningún build de Android. La revisión visual se hizo en el editor; todavía no se probó este mapa en un teléfono físico.

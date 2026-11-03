# Estantes del Brief: revisión del diseño antiguo

## Alcance
Se recuperan las formas del mueble antiguo del Brief con piezas independientes y una paleta menos saturada. Esta revisión se limita a los estantes; no modifica mapas, combate, diseños de personajes ni el pedestal del Home.

## Recursos
- Tres atlas nuevos: Assets/art/ambientations/modular/brief-{classic,lounge,royal}-cabinet-v2.png.
- Tres configuraciones: Assets/resources/MonsterPouch/Styles/Furniture/{classic,lounge,royal}.asset.
- Siete recortes nativos de Unity por atlas: balda, pedestal, remate, columna, respaldo inferior, respaldo superior y viga.
- Los originales permanecen intactos. Las imágenes generadas mantienen su transparencia y no se editan por scripts.
- Herramienta usada: image_gen integrada, edición con las tres imágenes antiguas como referencia. Prompts, fuentes y recortes: docs/art-prompts/brief-shelves-v2-20261013.json.

## Integración
LobbyPedestalGraphic selecciona el mueble por la temática del lobby. Conserva los ocho puntos de apoyo de los personajes: centro principal (270,291), fila de cuatro a y=514 y fila de tres a y=710. El pedestal principal es más ancho, como en el mueble antiguo. La altura de cada balda se calcula desde el borde inferior de los pedestales para apoyarlos en su superficie.

La anchura de baldas y longitud de columnas usan recortes con bordes y centro repetible. El remate superior conserva su proporción. LobbyCabinetBacking conserva las proporciones de las esquinas y del emblema central al ampliar los tramos de tela de los lados. La pantalla adicional revela el fondo existente; no deforma el mueble ni mueve sus personajes.

LobbyCabinetPalette crea un material por página y lo libera al destruirla. Reduce la saturación de forma configurable (0.64 real, 0.72 hotel, 0.68 parque), sin convertir la ambientación en nocturna. Las sombras de las piezas usan su alfa. Los elementos decorativos no capturan eventos táctiles.

## Estado de revisión
Se inspeccionaron las imágenes antiguas y los tres atlas generados. No se han ejecutado pruebas ni controlado la instancia de Unity del hilo principal desde esta conversación lateral. Queda pendiente la comprobación visual de la composición integrada en Game View.

## Respaldo
Backups/brief-shelves-before-20261013-side conserva los dos scripts existentes modificados antes de esta revisión.

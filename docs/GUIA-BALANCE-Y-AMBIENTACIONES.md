# Editar balance y ambientaciones

## Personajes existentes

1. Sal de Play Mode y abre **Monster Pouch > Balance Editor**.
2. Filtra Monsters/Whelps, elige el personaje y despliega General, Ataque básico, Habilidad principal o las tres mejoras.
3. Guarda con **Guardar assets**. El cambio se aplica al iniciar una partida nueva.

Los datos están en `Assets/resources/MonsterPouch/Characters/<id>/`: `Character.asset`, `Attack.asset`, `Ability.asset` y `Upgrade1..3.asset`. `MatchConfig.asset` referencia los personajes en `Characters`; la partida crea copias independientes mediante `CreateRuntimeConfig`. Los campos temporales de vida, energía o acumulaciones no se escriben sobre esos assets.

### Tamaño del personaje en partida

En **Monster Pouch > Balance Editor**, elige el personaje y abre **Estadísticas**. Al principio aparece **Tamaño del personaje en partida**, con una barra de **0 a 100%** y una vista previa que cambia al moverla:

- **0%**: mínimo visible, 0,4 baldosas de ancho.
- **50%**: 1,2 baldosas de ancho.
- **100%**: máximo, 2 baldosas de ancho.

La vista previa muestra el personaje sobre una baldosa central marcada y sus vecinas, con la misma proporción del tablero real. Mantiene fija la escala de las baldosas al mover la barra y permite cambiar de dirección. El ancho se mide por el cuerpo de referencia en reposo; los gestos de ataque pueden extenderse más. La altura mantiene la proporción del dibujo.

Pulsa **Guardar cambios** y empieza una partida nueva. Si estabas en Play Mode, sal y vuelve a iniciarlo para cargar los tamaños guardados. Cada personaje guarda su propio valor en `Visuals.InGameSizePercent`; los personajes existentes conservan su tamaño hasta que ajustes la barra. **Tamaño original** recupera el valor del arte, siempre dentro de los límites. Se aplica a la figura, sombra, preparación y combate. Conserva el apoyo de los pies, las estadísticas y los tamaños del lobby y de las cartas. Puedes deshacer el ajuste con Ctrl+Z.

El ID interno permanece estable. Cambia `General.DisplayName` para renombrar; no cambies nombres de IDs, parámetros de habilidad ni identificadores de efectos para traducir el texto.

- Intervalo de ataque: segundos entre ataques; un valor menor ataca más rápido.
- Proyectil básico: velocidad en casillas por segundo, visible solo en ataques a distancia.
- Tsu: la velocidad del proyectil de la habilidad es independiente del básico; 0 conserva el cálculo automático existente.
- Daño de habilidad: el toggle automático guarda -1 en las habilidades que admiten fórmula de ataque. Toronko/Tokoro usa el valor propio de habilidad.
- Tsu: `shieldPercent` significa reducción del daño recibido, no puntos de escudo según vida máxima.
- Selora/Sepora: `maxPerRound` limita los incrementos nuevos en una ronda; los incrementos permanentes anteriores se conservan.

## Añadir personajes y habilidades

**Duplicar configuración como borrador** crea assets independientes con ID nuevo, fuera del catálogo activo. Antes de añadir el borrador a `MatchConfig.Characters`, revisa su ataque, tres mejoras, referencias de arte y validación. La duplicación conserva el comportamiento del personaje fuente; un comportamiento de habilidad nuevo requiere código de simulación, descripción y presentación además del asset. Un ID nuevo no adquiere automáticamente el comportamiento especial de otro personaje.

La simulación se organiza en `Assets/scripts/gameplay/match/`, con efectos identificados por `EffectId`; `AbilityBalance` conecta parámetros y descripciones. Mantén los cálculos dentro de la simulación compartida, y los efectos gráficos en presentación. Prueba el comportamiento, el consumo de energía/tiempo, sus tres mejoras y el hash de configuración para ambos clientes. Los campos opcionales de prefab/sonido aún no están conectados a todas las rutas de presentación; ver los pendientes en `REDISENO-INTEGRAL-20261013.md`.

## Cambiar arte de personajes

El usuario aportará los rediseños. El original está respaldado en `ArtArchive/LegacyCharacters/`. Para integrar nuevos sprites, conserva los IDs y actualiza las referencias de arte por personaje en `UnitArtCatalog` y su configuración visual. Respeta las direcciones, pivotes, cuadros de animación y `DisplayScale` propios; comprueba el punto de apoyo de los pies antes de sustituir los recursos. No borres un `.meta` de un asset referenciado para reemplazar su contenido.

## Las tres ambientaciones

En `Assets/resources/MonsterPouch/Styles/`:

| ID estable | Tema | Entorno modular |
| --- | --- | --- |
| classic | Parque de Juegos | classic-scenery.asset |
| lounge | Grand Casa Hotel | lounge-scenery.asset |
| royal | Campo Real | royal-scenery.asset |

Cada `<id>.asset` de `EnvironmentStyle` referencia el fondo de lobby, previsualización, entorno, colores de botones y luz ambiental. El mueble del Brief tiene piezas y paleta propias en `Styles/Furniture/<id>.asset`: `Tint` y `Saturation` se aplican mediante su material; el pedestal del Home conserva el tinte de EnvironmentStyle. **Lobby** y **Mapa** se eligen por separado en el selector del juego.

### Colocar accesorios y sus sombras

Edita `Elements` del asset de entorno. Cada elemento tiene sprite, ancla, pie (`Foot`), ancho (`Width`), giro (`Rotation`) y opacidad de sombra (`Shadow`). Las posiciones usan las unidades de diseño de 540 × 960: X hacia la derecha, Y hacia abajo. `BoardLeft/Right/Top/Bottom` anclan la pieza al marco; `Room` usa el centro de diseño. La pantalla ancha revela más piezas manteniendo el tamaño y pie de cada una.

`BottomPadding` es la fracción de margen transparente bajo el pie visible. El sprite y su sombra comparten ese apoyo. La sombra proyectada utiliza el alfa del sprite, `ShadowDirection` y `ShadowLength`; el shader excluye el marco completo. En el casino no se añaden sombras circulares de contacto.

Grand Casa conserva `casino-table-path.png` del diseño antiguo, con multiplicador neutro 0.9 y color original. `FloorTileWidth` controla su escala de repetición; no modifiques el tamaño según la pantalla. La capa antigua de escena permanece desactivada para evitar duplicar el piso. `Tabletop` evita dibujar una pared encima del tapete.

`LightStrength`, `LightRadius` y `LightColor` crean iluminación de color sobre los personajes cercanos a una lámpara. Las piezas decorativas no deben invadir `FramePortraitBounds`. Los comandos de setup de arte regeneran el layout y pueden sobrescribir cambios manuales: edita los assets para ajustes cotidianos.

### Incorporar un cuarto mapa

Actualmente los tres IDs están registrados en `CardTableTheme`, el selector y la validación/sincronización de mapas. Duplicar un asset permite diseñar una variante, pero no la añade al selector. Para un cuarto tema hay que ampliar ese registro y `TableStyle`, añadir su previsualización/minimundo y revisar las reglas de red de ambos clientes. No reutilices el ID de un mapa distinto.

## Números de daño y avisos

`Assets/resources/MonsterPouch/DamageNumbers.asset` configura tamaños mínimo/máximo y daño de referencia. La animación y los críticos respetan esos límites. `PhaseCountdownHud` muestra 10..1 al final de preparación y 5..1 al final del combate utilizando el reloj de la partida; no captura entrada y se oculta al abrir un modal.

El nombre online proviene de `OnlineRoom`; el check animado refleja el estado de listo recibido. Las capturas de validación usan un rival simulado para reproducir cada estado, no una conexión Relay real.

## Progresión y verificación

`Assets/resources/MonsterPouch/Progression.asset` define XP por partida/victoria, requisitos por nivel y recompensas con ID. `PlayerProgress` guarda el historial y evita conceder dos veces una misma partida. Los desbloqueos configurados aún requieren conectarse al contenido que deba restringirse; no se inventan recompensas nuevas.

Ejecuta EditMode para balance/persistencia y PlayMode para interfaz, combate e integración. Los resultados de esta revisión se guardan en `Logs/local-validation/`; las capturas corresponden a Game View. La medición de FPS/RAM y Relay entre dispositivos requiere hardware conectado y una sesión real.

### Empaquetado móvil de piezas

El menú **Monster Pouch > Art > Pack modular scenery for Android** agrupa los recortes existentes de las estanterías y accesorios en seis atlases V2. No regenera la decoración ni recoloca el escenario. El parque utiliza su atlas propio `park-pieces`. Después de cambiar un atlas de origen, ejecuta el empaquetado fuera de Play Mode y de cualquier build.

Los atlases usan Point, padding de 4 píxeles, sin rotación y sin tight packing. Android solicita ETC2_RGBA8 de calidad alta. Comprueba el formato que acaba en el APK: un override del importador por sí solo no demuestra que Unity haya comprimido la textura. `BuildTools/inspect_android_package.py --all-scenery-atlases` comprueba los datos compilados; los PNG originales y las medidas de los sprites permanecen independientes del atlas GPU.

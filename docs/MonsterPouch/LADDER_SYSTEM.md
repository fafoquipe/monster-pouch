# Progresión competitiva de cuenta

## Estado vigente — MP-DEC-055, 25 de octubre de 2026

**Alcance local integrado y validado en el Editor: 380 EditMode y 25 PlayMode únicos aprobados.** Esta actualización prevalece sobre las referencias históricas a modos, conexión de cuentas y acceso al descender aún pendientes. Solo puntúan **BO5 completos de salas online**, con marcador final válido y el mismo resultado confirmado por ambos clientes. Bots, tutorial, rondas aisladas y resultados sin esa confirmación no conceden ni restan copas. No se inventa una puntuación de abandono, desconexión o empate.

El saldo inicial y el suelo son **0**. Una victoria suma **30**; una derrota resta **5** si las copas previas eran menores de 150, o **30** si eran 150 o más, respetando el suelo. Parque de Bolas corresponde a 0–149; Campo Real exige **150 copas actuales**, también al volver a seleccionarlo después de descender. Si estaba seleccionado y deja de estar disponible, se vuelve a Parque de Bolas únicamente en menú, sin cambiar la geometría durante la partida. No hay ajustes por rival, rachas ni tableros adicionales.

El perfil v10 conserva copas y recibos locales. Los perfiles anteriores, que no tenían copas jugables, comienzan en 0 conservando los demás datos; no se recalculan partidas antiguas ni se importan saldos o simulaciones QA/DEV. Los contadores sin datos muestran 0 y la selección consulta el perfil real. El Hotel queda fuera del recorrido; este alcance no cambia su política comercial ni activa pagos.

La confirmación usa el protocolo `mp-2026-10-25-v16-online-trophies`, un identificador compartido de partida y un hash que incluye el marcador BO5 en orden canónico. Un resultado confirmado se guarda una sola vez; un fallo de escritura queda pendiente de reintento en esa instancia local, incluso al volver al menú. Esto no constituye autoridad remota, recuperación de cuenta ni protección completa contra clientes modificados. No se incorpora emparejamiento automático, servidor nuevo, clasificación global ni monetización.

La pausa de expansión permanece vigente salvo esta conexión de copas expresamente aprobada. El módulo 9 del tutorial sigue pausado y el 10 pendiente. El cierre de pruebas y sus límites están registrados en `QA_CHECKLIST.md`; los resultados históricos siguientes se conservan como antecedentes.

## Antecedentes conservados — 22 de octubre de 2026

Actualizado el 22 de octubre de 2026. Decisión relacionada: **MP-DEC-036**.

## Alcance aprobado y decisiones pendientes

El creador pidió un sistema competitivo de **cuenta**, inspirado en Clash Mini: el jugador gana **COPAS** al vencer y pierde copas al perder. Confirmó la denominación copas y aprobó después la **prueba de balance con umbral de 150 copas** descrita abajo. Esa aprobación permite probar estas cifras; el balance comercial definitivo sigue pendiente. No sustituye el XP existente ni convierte la maestría de personajes en puntuación competitiva.

La última simplificación fija un **recorrido FINITO de SOLO DOS arenas gratuitas actuales**: **Parque de Bolas** (`classic`, antes Parque de Juegos) como inicial y **Campo Real** (`royal`). **Grand Cas Hotel** (`lounge`) es de pago y queda **excluido del recorrido por copas**. Esta decisión no autoriza activar compras, fijar precios ni retirar accesos existentes.

Al llegar al **último rango**, el jugador puede elegir entre los tableros gratuitos que haya superado y el Hotel de pago **si lo posee**. Las copas siguen subiendo sin añadir más tableros al recorrido. No se inventa un catálogo infinito ni una rareza formal «legendaria». El creador también se refirió al Hotel como «Grand Casino Hotel»; al no pedir expresamente un cambio de nombre, se conserva el nombre actual **Grand Cas Hotel** y el ID `lounge`.

La ampliación con aproximadamente tres tableros, la propuesta de cinco rangos y la repetición provisional de tableros quedan **pospuestas**. El trabajo actual utiliza únicamente las dos arenas gratuitas existentes. No se crean nuevas identidades, arte, nombres ni rangos para completar un número anterior.

Tras solicitar el cálculo del tiempo de progresión, el creador **aprobó probar 150 copas** con esta configuración explícita:

| Parámetro de la prueba | Valor aprobado |
|---|---|
| Copas iniciales y mínimo | 0; ninguna derrota deja el saldo por debajo de 0 |
| Parque de Bolas (`classic`) | 0–149 copas; victoria +30, derrota −5 |
| Campo Real (`royal`) | Desde 150 copas, sin límite superior; victoria +30, derrota −30 |
| Rival y rachas | Todos contra todos; sin ajustes por diferencia de copas ni rachas |

La derrota usa la arena que corresponda a las copas propias **antes** de aplicar el resultado. Por ejemplo, perder desde 150 resta 30 y deja 120; el siguiente resultado ya parte del rango inicial. Esto no decide si descender retira opciones de selección de tableros. La referencia anterior de **−25 en la penúltima** queda fuera de esta prueba: no se aplica a ninguna de las dos arenas actuales. Los valores aprobados para probar no se presentan como balance comercial cerrado.

**Todos pueden jugar contra todos por ahora**, sin separación de rivales por arena o copas. Se mantienen las salas existentes, porque todavía no hay cola de emparejamiento. Se posponen matchmaking por copas y ajustes del premio según la diferencia con el rival. El ejemplo de **+32 frente a una diferencia de 60 copas** no es una fórmula activa ni autoriza configurar bandas.

Siguen pendientes el balance comercial definitivo, recompensas y modalidades puntuables. Tampoco se han definido protección de descensos más allá del mínimo 0 aprobado para la prueba, tratamiento de abandonos/desconexiones, uso del tablero de pago antes de llegar al final, efecto de descender sobre la opción de elegir tablero, temporadas ni reinicios. No se conecta equipamiento automático ni se decide si el selector final depende del máximo histórico o del rango actual.

La base propuesta al creador fue:

- Copas, ligas y camino de hitos.
- Un resultado puntuable por BO5 PvP completo, excluyendo tutorial, práctica y partidas amistosas.
- Puntuación variable según las copas del rival como alternativa a una cantidad fija por resultado.

El creador respondió **«Quiero ajustar esa base»** y después precisó las copas y los tableros. No aprobó por ello ligas, camino de hitos, recompensas o exclusiones de modalidades. La elección de modos no quedó confirmada. Inicialmente eligió **«Según las copas del rival (recomendado)»**, pero la instrucción posterior pospone ese ajuste para concentrarse en dos arenas y el cálculo temporal; la instrucción más reciente prevalece.

No hay progresión por copas conectada a las partidas ni guardados competitivos de producción. La prueba aprobada está integrada como configuración explícita y herramienta de Editor aislada; no concede copas a cuentas reales. El cambio de nombre aprobado y los roles de los tableros también están integrados. La referencia a Clash Mini tampoco define arte, premios o una copia de sus sistemas.

## Catálogo de tableros verificado y cambio de nombre

| ID estable | Enum actual | Nombre visible vigente | Decisión vigente |
|---|---|---|---|
| `classic` | `TableStyle.Default` | Parque de Bolas / BALL PIT | Gratuito e inicial; sustituye Parque de Juegos / PLAY PARK, con ID intacto |
| `royal` | `TableStyle.CampoReal` | Campo Real / ROYAL COURT | Segunda y última arena gratuita actual; prueba aprobada desde 150 copas |
| `lounge` | `TableStyle.GrandCasaHotel` | Grand Cas Hotel / GRAND CAS HOTEL | De pago y excluido del recorrido; seleccionable al final si se posee, sin activar compras |

Los tres assets reales están en `Assets/resources/MonsterPouch/Styles/{classic,lounge,royal}.asset`. La UI obtiene los nombres de `CardTableTheme.Name` en `local-table-skins.cs`; el `DisplayName` del asset es metadato independiente. Los selectores actuales de lobby y mapa muestran los tres estilos y guardan preferencias separadas en `monster-pouch.lobby-style` y `monster-pouch.map-skin`. No existe todavía propiedad comercial o bloqueo por copas para estos tableros. El renombrado no cambia esos selectores ni sus accesos.

`MatchController.ValidOnlineMap` permite exactamente `classic`, `lounge` y `royal`. El protocolo transmite esos IDs y mantiene los mapas del anfitrión e invitado; el mapa de ronda alterna entre ellos. Asociar tableros a rangos requiere integrar deliberadamente la nueva progresión con estas reglas y con la selección actual, sin cambiar ahora el comportamiento de combate.

El paquete integrado, preparado en `ball-pit-name-staging/apply-manifest.json`, cambió textos en cuatro archivos existentes:

- `Assets/scripts/local/local-table-skins.cs`: título visible PARQUE DE BOLAS / BALL PIT.
- `Assets/scripts/local/GameText.cs`: correspondencia de traducción del título actualizado.
- `Assets/resources/MonsterPouch/Styles/classic.asset`: `DisplayName: Parque de Bolas`.
- `Assets/Editor/MonsterPouchStyleSetup.cs`: nombre del estilo al crear assets faltantes, para no reintroducir el anterior.

El inglés encontrado era PLAY PARK, no BALL PIT; se actualiza para mantener el mismo significado. No cambian los IDs, valores de `TableStyle`, claves de guardado, GUIDs, referencias a sprites, colores, geometría, estadísticas, protocolo ni precios. No se crea arte.

La asociación técnica adicional reutiliza `EnvironmentStyle`: campo `ProgressionRole` con `TrophyBoardRole`, cuyos valores serializados son `Unspecified=0`, `InitialProgression=1`, `Progression=2` y `PaidCosmetic=3`. Los assets `classic`, `royal` y `lounge` declaran respectivamente 1, 2 y 3. `ToTrophyBoardDefinition()` valida ID y rol y devuelve un DTO independiente para el catálogo del motor. La herramienta de creación de estilos conserva esa metadata al generar assets faltantes. Esto describe el uso en progresión, sin conceder propiedad del Hotel ni bloquear su selección actual.

Los 10 casos EditMode de `EnvironmentProgressionCatalogueTests` contrastan assets, IDs de guardado/red, roles, exclusión del Hotel, independencia de DTOs y rechazo de metadata inválida. La verificación estática previa comprobó los siete archivos existentes del paquete contra sus originales: solo cambian los nombres y la metadata descritos; se conservan todos los demás campos y referencias. Evidencia: `ball-pit-name-staging/static-verification.json`. Los 10 casos pasaron después en Unity dentro de la tanda 91/91 documentada al final. `TrophyLadderModels.cs` y la metadata están integrados conjuntamente.

## Auditoría del proyecto existente

La auditoría inicial, anterior a esta implementación, no encontró en `Assets/scripts`, paquetes y documentación un sistema de ladder, copas, rating, rangos, leaderboard ni emparejamiento por puntuación. La tabla siguiente describe las bases existentes de partida y perfil; el núcleo y panel QA incorporados después no las conectan automáticamente a una competición online.

| Área | Estado encontrado | Implicación para la integración |
|---|---|---|
| Resultado BO5 | `MatchController.EndRound` actualiza victorias y entra en `MatchPhase.MatchResult` cuando alguien alcanza `WinsToWin` | Existe el punto donde reconocer el final; una ronda aislada no equivale al resultado del encuentro |
| XP y nivel | `PlayerProgress`, `LocalProgressStore` y `local-progression.cs` conceden XP local con recibos por `SessionId` | Mantener este progreso separado; no deducir copas del nivel ni reemplazar XP |
| Historial actual | `CompletedMatches` guarda IDs, sin conservar el resultado completo de cada encuentro | No se pueden reconstruir puntuaciones históricas de victoria/derrota a partir de esa lista |
| Identidad | `LocalGuestProfile` v5 tiene `ProfileId` local; `OnlineRoom` inicia autenticación anónima de Unity Services | No existe aquí una vinculación verificada entre identidad local y cuenta competitiva remota |
| Sesiones online | `OnlineRoom` usa Relay y Netcode para salas casuales de dos jugadores por código | No hay cola competitiva ni selección de rivales por puntuación. No se presupone su diseño |
| ID de encuentro | `MatchController.BeginRound` genera un `SessionId` local al iniciar cada encuentro; `OnlineRoom.Packet` no lo transmite | Los dos clientes no comparten un identificador autoritativo de encuentro adecuado para adjudicar puntos |
| Verificación online | Ambos clientes simulan y comparan el digest de la ronda; `LastVerifiedRound` y `LastVerifiedDigest` registran coincidencia | Esta comprobación detecta divergencias entre clientes, pero no proporciona una autoridad competitiva segura |
| Momento de notificación | `EndRound` emite `Changed` antes de `OnlineRoundFinished` y de la comparación de resultados online | No conectar el cálculo competitivo directamente al listener actual de XP; podría procesar el resultado antes de verificarlo |
| Desconexión y pausa | `OnlineRoom.Fail/Leave` cierra la conexión y vuelve al menú; la pausa de aplicación cierra la sala | No existe una adjudicación de derrota competitiva ni regla aprobada para introducirla |
| Guardado | Hay guardados locales versionados, revisiones, respaldos y operaciones idempotentes | Es reutilizable el patrón técnico, pero no convierte el dispositivo en servidor confiable ni vincula por sí mismo el progreso a una cuenta |

Archivos principales inspeccionados:

- `Assets/scripts/gameplay/match/MatchController.cs`
- `Assets/scripts/gameplay/match/online-match.cs`
- `Assets/scripts/local/OnlineRoom.cs`
- `Assets/scripts/local/local-progression.cs`
- `Assets/scripts/gameplay/progression/PlayerProgress.cs`
- `Assets/scripts/gameplay/progression/LocalProgressStore.cs`
- `Assets/scripts/gameplay/profiles/LocalGuestProfile.cs`
- `Assets/scripts/gameplay/profiles/LocalGuestProfileStore.cs`
- `Packages/manifest.json`

## Base técnica e integración competitiva pendiente

Un estado competitivo separado debe identificar la cuenta y su revisión. La política debe declarar su versión y las reglas aprobadas; no incorporar cifras predeterminadas presentadas como balance final. Un recibo de resultado debe relacionar el encuentro compartido, las cuentas participantes, el resultado verificado, la política aplicada y su evidencia de autoridad.

La aplicación del resultado debe ser idempotente: reintentar el mismo encuentro conserva lo ya aplicado; un recibo contradictorio se rechaza sin resetear puntos. La persistencia debe confirmar el cambio y su recibo conjuntamente, conservando versiones anteriores frente a errores. Las modalidades elegibles y las consecuencias de un abandono deben venir de decisiones explícitas, no deducirse del comportamiento actual del menú.

El recorrido de dos arenas y la prueba 0/150 con +30 por victoria, −5 en la inicial y −30 en la final están aprobados. No existe concesión activa de copas en partidas. La fórmula por diferencia del rival queda pospuesta. Los hitos y sus premios tampoco están implementados ni aprobados. El cliente no debe autoadjudicarse puntos de producción. La comparación entre dos clientes actuales puede servir como evidencia de sincronización, pero no equivale a validación autoritativa. No se ha elegido ni contratado un servicio nuevo.

## Estado del trabajo y comprobaciones

**Auditoría terminada y prueba de 150 copas integrada; balance comercial y modos pendientes.** Núcleo, renombrado, metadata y panel QA están integrados: 21 archivos, incluidas fuentes, pruebas, assets y sus metas. En su cierre coincidían con los manifiestos `ladder-board-integration-20261022/manifest.json` y `ladder-trial-integration-20261022/manifest.json`, sin discrepancias. El motor puro admite una banda plana explícita y pérdidas por arena previa, sin política de producción activa por defecto. Su revisión independiente comprobó las copias aisladas, firma de política, ascenso/descenso, reintentos después de partidas posteriores y rechazo de historia manipulada. Sus **74/74 casos independientes de NUnit** se conservan como evidencia previa en `ladder-engine-staging/verification/standalone-results.json`.

La prueba aislada preparada en `ladder-trial-staging` está integrada. `TrophyLadderTrialConfiguration.CreatePolicy()` devuelve una copia nueva de la configuración aprobada; no inicia sesiones ni cambia perfiles. La ventana de Editor **Tools > Monster Pouch > Ladder de prueba** carga el catálogo existente y permite simular victorias/derrotas en memoria con identificadores QA. Cerrar o recargar el editor reinicia esa sesión; no hay guardado, conexión a cuentas, suscripción a resultados de partidas, cambio del tablero equipado ni pagos. El paquete aporta seis de los 21 archivos integrados.

La factory pasó previamente **6/6 comprobaciones independientes**, incluida compilación de sus fuentes C# exactas e independencia de políticas; informe `ladder-trial-staging/verification/factory-check.json`. Los **7 casos `TrophyLadderTrialTests`** usan los assets reales mediante `Resources` y pasaron después en Unity. Cubren cinco victorias hasta 150, pérdida de 30 al salir de Campo Real y de 5 en el rango inicial, mínimo 0, reintentos tras resultados posteriores, acumulación después de la última arena y ausencia de ajustes por rival/rachas.

La tanda real pasó **91/91 EditMode**, cero fallos y omitidos, el 22 de octubre de 2026 entre las 20:25:12 y 20:25:14 UTC: 74 casos del núcleo, 10 del catálogo y 7 de prueba. Informe: `Logs/roadmap-v1.2/ladder-trial-edit-20261022.xml`, con copia en `ladder-board-integration-20261022/results/`. Esto valida la implementación aislada; no acredita una versión competitiva jugable conectada a cuentas ni una revisión visual de la ventana de Editor.

La regresión de escenas pasó **3/3 PlayMode**, cero fallos y omitidos, entre las 20:29:59 y 20:31:00 UTC del mismo día: `TableSkinsPlayTests` 2/2 y `LobbyThemeSelectionPlayTests` 1/1. Informe: `Logs/roadmap-v1.2/ladder-boards-play-20261022.xml`, con copia en el mismo directorio de resultados del workspace. Verifica selección/persistencia de mapas y apariencia independiente del lobby mediante aserciones en batchmode. No hubo inspección visual; esta tanda es anterior al ajuste posterior de áreas táctiles.

El ajuste técnico posterior del selector existente aumenta solo el área que recibe pulsaciones hasta 44 píxeles y mantiene texto de al menos 12 píxeles según la escala efectiva. Conserva los rectángulos y arte originales. Pasó **8/8 EditMode** de geometría; la primera tanda de escenas quedó en **5/6**, al detectar que la GameView de batchmode tenía 480 píxeles de alto y no los 512 mínimos exigidos. Se corrigió la preparación del entorno sin reducir esa aserción. La repetición completa cerró con **6/6 PlayMode** entre las 21:04:31 y 21:05:28 UTC: tres casos nuevos y las tres regresiones previas, sin sumarlas como nueve casos únicos. Se inspeccionaron capturas Unity de 360×640 en ES/EN, sin recortes en texto o cabeceras del selector. Esto valida el selector existente; la nueva presentación de TABLEROS tendrá evidencia propia. Historial e informes en `QA_CHECKLIST.md`.

El paquete independiente `mastery-engine-staging` permanece congelado y **sin integrar** por el cambio de prioridad. Sus 62 casos independientes de NUnit corresponden exclusivamente al motor puro de maestría; no validan ladder, cuentas remotas ni una ejecución de Unity. No se continuará ampliando ese paquete mientras el foco actual sea definir la progresión competitiva de cuenta.

## Orientación y presentación visual aprobada

El creador precisó que el móvil debe usar exclusivamente vertical normal, sin giro horizontal ni boca abajo; PC puede usar una ventana ancha. La auditoría `mobile-orientation-audit-20261022/evidence.json` confirmó que el proyecto ya configura Portrait fijo, desactiva las otras orientaciones y mantiene la ventana de escritorio redimensionable. Android lo refuerza en ejecución y en el build; iOS usa la misma configuración sin overrides hallados. No se modificaron Assets ni se realizó una prueba física para esta auditoría. La composición horizontal de la propuesta es para PC y todavía no representa una nueva interfaz nativa integrada.

La referencia de recorrido y selector preparada en el otro chat se conserva en `arena-presentation-staging-20261022`. Usa HTML/CSS con PNG y fuentes existentes, con 25 vistas en seis formatos y comprobaciones de navegador documentadas en `validation.json`. Tras revisar una vista vertical, el creador **aprobó el diseño visual de tableros** y corrigió expresamente los **números borrosos**: la implementación debe usar cifras nativas nítidas; no pidió rediseñar los botones. Luego rechazó el nombre visible «ARENAS» y fijó **TABLEROS / BOARDS**, con **Tablero actual** y **Siguiente tablero**. Los IDs, rutas técnicas y referencias históricas conservan sus nombres para mantener trazabilidad. Su `manifest.json` describe referencias y límites; el archivo `freeze.json` anterior requiere regenerarse tras la corrección de orientación y no se considera comprobación vigente de integridad.

El umbral deberá mostrarse mediante «+150» seguido del símbolo de trofeo, eliminando «Desde 150 copas». El creador **no aprobó el trofeo original**: pidió más pequeño, menos brillo y una familia **PLATA, ORO y LUNAR**, con textura cristalina tipo diamante para el lunar. Las variantes se preparan en chats separados y todavía requieren elección visual antes de importar recursos. Su reparto entre los dos tableros no está definido; no se inventan umbrales, premios o una tercera etapa. Las cifras se renderizan como texto nativo y no forman parte de una captura rasterizada del prototipo.

La referencia posterior `trophy-reference-20261022.png`, con la indicación «parecido a este, pero no igual», y la precisión explícita **«es en PIXELART»** sustituyen la dirección de las primeras versiones: copa de forma sencilla en PIXEL ART, contorno oscuro, paleta reducida y reflejos mínimos. Los tres chats recibieron ambas aclaraciones; el lunar conserva textura de cristal/diamante con brillo contenido. No se aprueba una versión vectorial o cartoon lisa. Estas instrucciones guían nuevas propuestas, sin aprobar una imagen final ni importar iconos. La futura configuración de importación deberá usar filtrado Point para conservar el pixel art; la preparación bilineal original queda archivada y no se aplica.

Las propuestas actuales de Plata, Oro y Lunar se presentaron juntas en `trophy-family-preview-20261022.png` para una aprobación conjunta que sigue pendiente; rutas de los tres PNG en `PENDING_APPROVAL.md`. El «+150» de la lámina es solo una muestra visual: no asigna trofeos a rangos, crea etapas ni resuelve la distribución pendiente. Ninguno de esos recursos está importado.

La preparación técnica original se conserva en `trophy-icon-import-staging-20261022` únicamente como historial y **no debe aplicarse**, porque su imagen no fue aprobada. Sus tres archivos son la carpeta `Progression.meta`, el PNG y su meta, separados del manifiesto de documentación. Conservan byte a byte la fuente generada de **1254×1254**, SHA256 `35C50DCC82741F707E281522690F019BE164772E1B9574F32BE18C326373F9FF`. La configuración preparada limita la textura importada a **512**, con Sprite Single, sRGB/alpha, bilineal, sin mipmaps ni compresión; no modifica los bytes del PNG fuente. No se ejecutó Unity con ese paquete ni se aprobó un recurso de reemplazo por el mero hecho de preparar metadatos.

Estos seis formatos del prototipo de navegador son distintos de los siete tamaños verticales de las pruebas del selector Unity. Los estados de ejemplo para Hotel antes de 150 o al descender sirven como guardas del prototipo, sin decidir reglas pendientes de propiedad, acceso o selección. La pantalla nativa y su enlace con la herramienta QA están integrados en un paquete de diez archivos para la prueba de copas en memoria. La primera importación de fixtures detectó dos metadatos malformados, reparados conservando ambos GUIDs. V1 pasó **9/9 EditMode** a las 21:26:38 UTC y **3/3 PlayMode** entre las 21:29:49 y 21:30:08 UTC, con seis capturas conservadas y verificadas por SHA en `board-presentation-integration-20261022/v1-evidence.json`.

La fase de UI sigue abierta: la revisión posterior detectó tamaño insuficiente en pantallas de alta densidad que los escenarios V1 no cerraban. La corrección a escala lógica 360×640 está integrada en tres archivos, con validación adicional en 1080×2400, 1440×3200 y tableta 1536×2048. Pasó **19/19 EditMode** entre las 21:47:18 y 21:47:19 UTC. La tanda PlayMode combinada terminó **3/6**: TABLEROS 1/3 por dos recortes reales de `ProgressValue`, y lobby 2/3 por una expectativa de su fixture. Se corrige la medición del ancho a la escala efectiva del Canvas antes de repetir; solo se generaron dos capturas parciales. Informes y límites en `QA_CHECKLIST.md`.

Se conservan los resultados V1 por separado. La integridad de 37 archivos registró 36 coincidencias exactas y un cambio externo revisado de descripciones Kazuru/Tauris, sin diferencias inexplicadas. Esta presentación sigue aislada en QA: no concede copas de cuenta ni define modalidades puntuables. Los archivos del prototipo permanecen fuera del manifiesto de 13 documentos y herramienta matemática.

**Cierre posterior, 21:56 UTC:** corregida la medición del texto según la escala real del Canvas, la repetición pasó **6/6 PlayMode** (TABLEROS 3/3 y lobby 3/3), con once capturas completas inspeccionadas. Se cierra el problema de alta densidad y recorte de cifras. XML, log y PNG están preservados por hash en `board-metrics-integration-20261022`; detalle en QA_CHECKLIST.md. Los trofeos siguen como propuestas pendientes y las copas siguen exclusivamente en memoria de QA.

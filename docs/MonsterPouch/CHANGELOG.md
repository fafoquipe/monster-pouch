# Cambios

## 2026-10-20 — auditoría v1.2

- Rama de trabajo y respaldo completo externo de fuentes/activos antes de modificaciones.
- Fuente original del roadmap conservada; seguimiento, decisiones posteriores, inventario, riesgos y QA añadidos en `docs/MonsterPouch`.
- 22 definiciones de personaje y 38 PNG utilitarios inventariados con rutas/dimensiones/hashes.
- Pruebas base Unity: 940/941. Primera regresión tras cambios: 955/955 EditMode.
- Corrección de alcance: banco actual gratuito, sin nuevo mecanismo económico ni compra de unidades duplicadas.

## 2026-10-20 — fase 1 y MP-DEC-023

- `battle-unit.cs`, `combat-simulation.cs`, `documented-combat.cs`, `combat-status-snapshot.cs`: ventana inicial de 1,5 s; Tokoro se recupera 60 HP/s durante los primeros 3 s, sin ataques ni energía. Temporizadores deterministas y bloqueo de energía externo.
- `AbilityBalance.cs`, `DocumentedRoster.cs`, `GameText.cs`, assets de Tokoro y `MatchConfig.asset`: valores y descripciones coherentes, conservando IDs y arte.
- `round-health-summary.cs`, `round-tiebreakers.cs`: captura única del HP actual; solo Monster/Whelps vivos; todas las invocaciones excluidas. Nuevo desempate aprobado sin consumir HP real.
- `local-round-health.cs`, `local-round-result.cs`, `RoundPresentationSettings.cs`: división, transferencia y barras normalizadas; banderas posteriores; título final y confeti BO5; duración de recuento configurable.
- `OnlineRoom.cs`: versión de protocolo v11 para impedir mezclar clientes con reglas diferentes.
- Pruebas de banco, apertura, Tokoro, vida congelada, quemaduras pendientes, exclusión de muertos/invocaciones y presentación en escena real.
- `tools/Invoke-RoadmapTests.ps1`: ejecución reproducible con versiones del proyecto e informes conservados por nombre.
- No se modificaron sprites ni se borraron personajes, escenas, prefabs o animaciones por este trabajo.
- `LobbyPedestalGraphic.cs`: Home reutiliza el pedestal y la paleta existentes del ambiente del lobby, sin cambiar el mapa de combate.
- Integración con el trabajo paralelo de estados: el stun conserva la duración más larga; Tokoro mantiene su reacción de conitos ante cada nuevo stun válido, aunque sea más corto.
- Pruebas de captura compatibles con editor oculto; registro por test para diagnosticar bloqueos. Captura explícita mediante cámara/Canvas, sin depender de Game view visible.

## 2026-10-20 — base técnica de etapa 2

- `profiles/LocalEconomyProfile.cs`, `LocalQAProfileStore.cs`, `EconomyDevelopmentConfig.cs`: esquema, identidad local QA, monedas y procedencias separadas, bienvenida provisional única, recibos persistentes, validación, atomicidad y rechazo de estados corruptos o futuros.
- `Assets/Editor/RoadmapProfileWindow.cs`: herramienta de prueba accesible desde Tools; deshabilitada al abrir, exclusiva del Editor.
- `local-profile-tests.cs`: reinicio, reintentos, conflictos, gasto sin saldo, aislamiento de orígenes, respaldo incompleto, versiones futuras y bloqueo de escritores.
- `PROFILE_STORAGE.md`: alcance y límites. No se activa ni sustituye la economía del jugador; progreso/mazos anteriores conservados.

## 2026-10-20 — MP-DEC-024, corte diagonal y llegada a la barra

- `DiagonalHealthHalf.cs`: recorte geométrico de los glifos del número en dos mitades diagonales complementarias; conserva coordenadas de textura y no requiere sprites ni materiales nuevos.
- `local-round-health.cs`: números verdes; separación de las dos piezas, aparición del valor de media vida y transferencia con aumento y reducción rápida de tamaño al llegar. Monster sin corte; reglas y totales intactos.
- Pruebas de cobertura de las dos mitades y UV; prueba en escena de color, corte, 13 → 6.5, tamaño al llegar, pausa y limpieza. Capturas esperan al final de la entrada del Canvas para evitar registrar la interfaz aún translúcida.

## 2026-10-20 — elección inicial y revisión visual

- MP-DEC-025: `StarterRoster.cs`, `LocalQAProfileStore.Starter.cs` y perfil v2 con recibo de elección inicial Bugaloo/Popow; siete Whelps aprobados. Migración aditiva v1 sin elección automática, duplicados ni modificación de mazos existentes. Botones en la herramienta QA del Editor; todavía no es onboarding de cuentas comerciales.
- MP-DEC-026: números sobre el límite superior del sprite, contorno negro más grueso, colores relativos al jugador local y transformación a `RoundHealthOrb` durante el viaje. Se conserva el pulso al entrar en la barra.
- Pruebas nuevas de migración/elección y posición/color/orbes en las dos perspectivas del tablero.

## 2026-10-20 — MP-DEC-027/028, desvanecimiento y enteros

- Cada aporte Whelp redondea hacia arriba antes de sumarse: 13 HP aporta 7 y 1 HP aporta 1. El desempate y la animación comparten esos valores; no se modifica la vida real de combate.
- Los contadores muestran enteros durante toda la transferencia. Protocolo online v12 para no mezclar clientes con reglas de redondeo diferentes.
- Las mitades cortadas se desvanecen de forma continua durante la aparición del aporte y el comienzo del vuelo. Los números cercanos a la barra enemiga ocupan el espacio libre superior con un tamaño compacto.
- Botones Detalle/Usar más altos para mantener objetivos táctiles de al menos 44 px a 360 px de ancho. La prueba de colección abre explícitamente Whelps; se conserva la pestaña inicial existente.
- Validación de Anuik ajustada al gesto corporal que ya usa desde la revisión del 15 de octubre: escala acotada durante el ataque, ancla inmóvil y proporciones originales recuperadas al terminar. Sin cambios en sus sprites ni animación.
- Bloqueo entre fixtures de PlayMode: la limpieza de UI tras CasinoHud permite procesar un frame antes de cargar otra escena. Secuencia antes bloqueada verificada con 22/22 pruebas aprobadas. La batería general todavía no está validada; consultar QA_CHECKLIST.

## 2026-10-20 — MP-DEC-029, retirar requisitos antiguos del circuito activo

- Cuatro fixtures de gestos de menú archivados fuera de Assets, con sus metadatos y hashes; no se compilan ni ejecutan. No se movieron activos del juego.
- La configuración predeterminada de `LobbyFrameGraphic` es estática. Las pruebas de navegación, idiomas, personajes y estilos mantienen desactivada la reproducción de gestos antiguos.
- Las comprobaciones vigentes preservan retratos, contextos Home/Brief, catálogo completo, iconos y descripciones actuales; los casos de combate de Popow, Suki y Pokrak esperan la ventana inicial aprobada.
- El resultado global anterior, 127/160, queda conservado como diagnóstico. Los fallos aplicables que todavía no estén corregidos permanecen activos y pendientes.
- Verificada la configuración estática con 998/998 EditMode. Revalidación específica de combate, estados, iconos y estilos: 19/19 PlayMode. Los tests usan tiempos, destinatarios y cantidades de la configuración vigente; no se cambiaron habilidades ni balance.
- Botones para aplicar estilo con 68 unidades de alto, comprobados mediante raycast y límites del área segura a tamaño móvil. El selector mantiene separados lobby y mapa, conserva mazos y figuras y persiste los tres mapas al recargar la escena.

## 2026-10-20 — MP-DEC-030, nombres visibles y IDs separados

- Auditoría de los 22 nombres actuales desde CharacterDefinition: CHARACTER_NAMES.md. Las fichas ya usan DisplayName; las pruebas dejaron de buscar nombres deducidos del ID.
- Descripciones en ambos idiomas resuelven `{unit}` con el nombre visible actual. El aviso de posición marcada consulta el nombre configurado de Tauruk.
- La herramienta QA muestra Bugrum/Pombú y los Whelps por sus nombres actuales. Conserva los mismos IDs en la elección, el inventario y los recibos.
- No se modificaron nombres aprobados, IDs, sprites, estadísticas ni partidas guardadas. Se añadieron comprobaciones de actualización del nombre sin cambiar identidad y de las fichas de todo el catálogo en ambos idiomas.
- Progreso usa los nombres del catálogo y de las recompensas configuradas en lugar de imprimir sus IDs. La prueba de integración comprueba que el JSON de desbloqueos no cambia al mostrar sus nombres.
- Regresión global: 149/150 PlayMode aprobadas. El único fallo nuevo de margen de estilos se corrige desplazando el botón 2 unidades hacia arriba, conservando el área táctil ampliada. Revalidación enfocada en curso.

## 2026-10-20 — persistencia de progreso jugable

- Cierre de interfaz y nombres: 8/8 PlayMode aprobadas. El fallo de margen ya está revalidado.
- `PlayerProgress` conserva todos los recibos; ya no elimina los anteriores a las últimas 128 partidas. No cambian XP, niveles ni recompensas configuradas.
- `LocalProgressStore` importa una sola vez el guardado actual, conserva su JSON original, escribe XP/recibo de forma atómica y mantiene respaldo. Las instancias antiguas vuelven a cargar antes de operar.
- Integrado con el resultado BO5 y la pantalla de Progreso. Los errores muestran recuperación o reintento; no se sustituyen datos corruptos por progreso vacío ni se sobrescriben versiones futuras.
- Pruebas de escenas aisladas del directorio real del jugador. Cobertura nueva de replays antiguos, importación, concurrencia, corrupción, interrupciones y versiones futuras; ejecución pendiente.
- Kazuru apareció en el catálogo durante el trabajo concurrente; se registra su nombre actual sin modificar su diseño ni sus activos.
- La regresión detectó que la nueva comparación de mejoras externas descartaba el regalo de daño de Tsumi sobre aliados con daño base cero. Se conserva ese regalo, incluidos sus efectos sobre daño innato ganado después; no se cambia el 50 % ni la regla de no acumular mejoras externas.
- Los resultados BO5 que fallan al guardar permanecen pendientes en memoria para reintentar desde el menú durante la misma sesión.

## 2026-10-20 — regresión de guardado y corrección de corrupción

- `phase2-progress-storage-final-edit.xml`: 994/994 EditMode aprobadas. `phase2-progress-storage-global-play.xml`: 156/157 PlayMode aprobadas; se conservan ambos informes sin cambiar sus resultados.
- Las fichas de los 23 personajes actuales pasaron en ambos idiomas. Pasaron también los resultados de combate y tres casos reales de Progreso: recarga sin premio duplicado, respaldo de solo lectura y reintento de un BO5 tras liberar el archivo.
- El caso restante de corrupción sin respaldo detectó que `InvalidDataException` escapaba del manejo de errores porque no es una subclase de `IOException`. Se corrige en la UI y en los almacenes de progreso y QA; las versiones futuras siguen protegidas.
- Dos nuevas regresiones cubren un JSON válido con checksum incorrecto, respaldo consultable sin operaciones nuevas y conservación del archivo dañado. Cierre enfocado: 40/40 EditMode y 10/10 PlayMode, informes `phase2-recovery-focused-edit.xml` y `phase2-recovery-focused-play.xml` en `Logs/roadmap-v1.2`. Los cuatro casos de Progreso y los cuatro de recuento/resultados pasan.
- Trabajo siguiente en curso, todavía sin validación: inventario de QA por IDs y recibos sin premios nuevos, y almacén versionado de mazos con importación no destructiva. No se declara terminado ni se habilita economía de jugador por ese trabajo.
## 2026-10-20 — inventario QA v3, mazos y referencias

- `LocalEconomyProfile.Inventory.cs` y `LocalQAProfileStore.Inventory.cs`: propiedad de mejoras por IDs y recibos idempotentes en perfiles QA. Reconoce contenido ya obtenido entre procedencias sin nuevas concesiones ni monedas; migración v1/v2 no destructiva. La ventana QA incorpora inspección y concesión explícita.
- `LocalDeckStore.cs` y `local-deck-storage.cs`: mazos existentes con importación no destructiva, revisión, checksum, respaldo, bloqueo de escritor y mensajes/reintento en la UI. Conflictos y referencias de catálogo ausentes impiden sobreescrituras que pierdan selecciones.
- `MonsterPouchCatalogueValidation.cs`: menú de validación de solo lectura que reutiliza reglas del personaje y del combate; muestra nombres actuales y detecta referencias/IDs/habilidades incompatibles sin alterar activos.
- Primera regresión integrada: 1026/1028 EditMode. Corregidos los dos diagnósticos del validador, la global posterior pasó 1053/1053 en `phase2-data-final-edit.xml`. PlayMode: 25/25 aprobadas en `phase2-decks-progress-play.xml`, incluidos mazos, colección, progreso y resultados. Los informes originales conservan sus recuentos.
- Integrados aislamiento simétrico QA/Player y base de identidad invitada con elección inicial persistente, cubiertos por la global 1053/1053. El hook silencioso posterior de identidad al inicio pasó 19/19 EditMode y 9/9 PlayMode (`phase2-guest-hook-edit.xml` y `phase2-guest-hook-play.xml`); no implementa onboarding visible, concesión de habilidades, moneda real ni restricciones nuevas de colección.
- Pendientes de respuesta del creador: habilidades/mejoras disponibles para cuentas nuevas y propiedad que debe reconocerse a jugadores del legado. No se aplicó ninguna interpretación de esas decisiones.

## 2026-10-20 — MP-DEC-031, entrada exclusiva de cuentas nuevas

- Primera implementación del selector: elección visible Bugrum/Pombú con los siete Whelps, nombres y arte actuales; perfil invitado con recibo y mazos v2 con aplicación idempotente. Sus **43/43 EditMode** (`starter-selection-edit.xml`) son evidencia histórica anterior a la corrección del flujo solicitada por el creador.
- La regla vigente es cuenta nueva → elección → tutorial. `LocalGuestProfile` v2 distingue `None`, `ChooseStarter`, `Tutorial` y `Complete`; los invitados v1 migran a `None`, incluso si no tenían recibo. Abrir un perfil existente conserva su acceso y no dispara elección ni tutorial.
- El primer mazo y su marcador se guardan atómicamente una sola vez. Revisiones previas, reintentos y conflictos conservan selecciones ya guardadas; se mantienen los tres espacios de mazo existentes. Esto no convierte un mazo en propiedad del catálogo ni concede habilidades o monedas.
- `local-first-contact-tutorial.cs` implementa únicamente Primer contacto: tocar para colocar, mantener pulsado para ver información y arrastrar a otra casilla válida. Reutiliza los gestos, tablero, nombres y arte actuales; no acepta un movimiento rechazado ni abrir una ficha por otra vía como sustituto del ejercicio.
- `StartTraining`/`IsTraining` aíslan la práctica de las partidas normales: sin rival desplegado, cuenta atrás, combate, XP ni premios; no alteran estadísticas de personajes ni balance. Salir permite reanudar el módulo incompleto desde el menú; su recibo se guarda al completar los tres ejercicios y permite reintento ante errores.
- Completar `first-contact` no marca el tutorial global como terminado. Los módulos 2–10, sus dependencias y recompensas siguen pendientes; no se habilitaron cuentas vinculadas, backend, servicios, cobros ni publicación. El cierre del flujo corregido está centralizado en `QA_CHECKLIST.md`.
- Cierre del flujo corregido: **1090/1090 EditMode** en `new-account-tutorial-edit.xml` y **19/19 PlayMode** en `new-account-tutorial-play.xml`, ambos conservados en `Logs/roadmap-v1.2`. La tanda PlayMode incluye siete casos de selección, tres de Primer contacto, dos de identidad invitada, tres de mazos y cuatro de Progreso.


## 2026-10-20 — tutorial, módulos 2–6 y MP-DEC-032

- Cinco nuevos ejercicios con tablero, gestos, compra, copias, reroll, banco y simulación reales. El siguiente módulo se abre al continuar; salir y recargar respetan el progreso guardado. Ayuda permite repasar sin duplicar recibos.
- Rival del combate sencillo: Monster inicial no elegido, con estadísticas favorables a la mitad e inversas duplicadas; solo se alteran copias de ejecución. La revisión cruzada corrigió una reducción doble del ancho de embestida.
- Objetivo didáctico inmóvil, sin daño, cuya resistencia y tiempo de práctica se derivan del ciclo real de energía. Se observa una habilidad automática sin inyectar energía; la animación puede terminar tras acreditar el ejercicio.
- Perfil invitado v3: migración conservadora desde v1/v2, orden de módulos, guardado atómico, reintento y repetición sin créditos de progreso adicionales. Entrenamiento sin recibo BO5 ni XP ni moneda persistente.
- Repaso de BO5 con resultado normal y lectura del recibo de XP exacto desde disco. Repaso de salas con formularios actuales, sin conexión automática; cuenta nueva necesita BO5 observado y XP confirmada en la misma sesión. Esa evidencia no se reconstruye desde recibos sin tipo al reiniciar y no completa módulos 9/10.
- Se preservaron los cambios concurrentes de selección de mapas al integrar. Sin cambios de arte, catálogo global, pagos ni publicación. Evidencia de pruebas en QA_CHECKLIST.md.

## 2026-10-20 — MP-DEC-033/034, tres iniciales y Brief automático

- Nueva composición inicial exacta: `atori`, `bugui`, `blotan`, con los nombres actuales del catálogo. Selector centrado y texto dinámico; se mantiene la elección Bugrum/Pombú exclusiva de cuentas nuevas.
- Perfil invitado v4 con colección inicial y recibos explícitos de desbloqueo, separado de QA. Conserva identidades, recibos de siete iniciales y acceso anterior mediante migración de compatibilidad, sin retirar personajes ni inventar propiedad.
- Brief de siete espacios: los nuevos Whelps disponibles rellenan los huecos; se exige el equipo completo según la colección disponible al iniciar partida. Selecciones válidas y octavo personaje no desplazados automáticamente. Colección consultable con selección limitada a disponibilidad confirmada para cuentas nuevas.
- Revisión de conflictos entre sesiones y de identidad no disponible para evitar sobrescrituras y acceso accidental a personajes bloqueados. Guardado con revisión, respaldo y recibos idempotentes.
- Banco del tutorial corregido para comprobar ofertas ocultas sin exigir que desaparezcan las copias pendientes del pool. Se conservan las reglas reales de compra, mejoras y banco gratuito.
- Cierre: **1140/1140 EditMode y 43/43 PlayMode**, informes `starter-three-tutorial-edit.xml` y `starter-three-tutorial-play.xml`. Capturas del selector y banco inspeccionadas. Las recompensas de tutorial y máquinas siguen sin inventarse; límites en `QA_CHECKLIST.md`.

## 2026-10-20 — MP-DEC-035, colección bloqueada y motor de máquinas

- Whelps y Monsters bloqueados muestran retrato oscuro y candado cerrado. La ficha sigue disponible; Usar requiere propiedad. Ambos indicadores desaparecen al desbloquear el personaje.
- Corregida la validación de Monsters de cuentas nuevas en edición, guardado y entrada a BOT/sala. Desbloquear otro Monster no lo selecciona automáticamente ni altera el número de Whelps. Se preservan cuentas antiguas y mazos en conflicto.
- Motor técnico separado para catorce premios, garantías de personaje/mejora, exclusión de premios propios y recibos idempotentes. Sin operaciones de cartera, inventario real, red ni política comercial predeterminada.
- Contexto preparado con copias privadas para evitar validar y firmar el mismo catálogo millones de veces en una simulación. Se mantienen las comprobaciones sobre estado mutable y la compatibilidad con las llamadas estáticas.
- La validación posterior y sus límites se registran en QA_CHECKLIST.md. Los resultados anteriores no se atribuyen a estos cambios nuevos.

- Cierre de colección: 46/46 PlayMode y capturas de ambos grupos inspeccionadas. Global posterior: 1195/1195 EditMode; 100.000 máquinas simuladas en 420.157 ms, con exactamente los mismos resultados que la ejecución anterior que excedió el tiempo. Ningún reinicio de Unity fue necesario.

## 2026-10-20 — habilidades del invitado y transacción QA aislada

- Guest v5 conserva la colección v4 y añade recibos explícitos de habilidades por IDs existentes, sin definir ni conceder mejoras iniciales. Migración solo en memoria al leer, control de propiedad, colisiones entre recibos, idempotencia y recuperación de solo lectura.
- Adaptador de propiedad externa con copia separada, rechazo de legado sin inventario definido y rechazo de recuperación para operaciones autoritativas. No altera mejoras de combate ni desbloquea nada desde UI.
- Sandbox de máquinas separado: una escritura confirma crédito QA, débito, premio sintético, estado y recibo. Configuración explícita, revisión, reintentos, respaldo y fallos de commit; no toca carteras ni inventarios del jugador.
- Revisión cruzada corrigió aceptación de encabezados omitidos, redondeo de una muestra extrema y validación de estructura antes de serializar objetos inline. 30/30 y 31/31 casos independientes previos a Unity; evidencia integrada y límites en QA_CHECKLIST.md.

## 2026-10-22 — cierre de serialización y continuidad

- El lector del invitado exige listas raíz según la versión de perfil, rechaza JSON ambiguo y normaliza nombres escapados antes de pasarlos a Unity; conserva literalmente los timestamps y no reescribe archivos al leer.
- La transacción QA copia su configuración sin pasar probabilidades por JsonUtility y firma los bits exactos de sus valores. Una configuración incompatible devuelve conflicto y conserva el archivo anterior.
- Se mantienen los dos fallos históricos detectados por Unity. Las reparaciones añaden regresiones de listas ausentes/nulas/duplicadas, recibos bajo claves escapadas y probabilidades adyacentes; resultados reales en QA_CHECKLIST.md.
- Cierre real: **280/280 EditMode y 18/18 PlayMode**, con informes separados de los históricos. Confirma guardado, migración, máquinas y arranque/colección afectados; no se repitieron pruebas de sistemas intactos.

## 2026-10-22 — MP-DEC-036, copas y prueba de 150

- Integrados núcleo configurable de copas, catálogo por IDs y roles, y política explícita de prueba: inicio/suelo 0, Parque de Bolas 0–149 con +30/−5 y Campo Real desde 150 con +30/−30. La pérdida usa la etapa previa; no se ajusta por rival ni rachas. Se conservan recibos idempotentes, máximo histórico y límites de aritmética.
- Parque de Juegos pasa a Parque de Bolas / BALL PIT, conservando `classic`, GUIDs, sprites y preferencias. Campo Real mantiene `royal`; Grand Cas Hotel mantiene `lounge` y se clasifica fuera del recorrido gratuito, sin activar compras o retirar accesos actuales.
- Panel de Editor **Tools > Monster Pouch > Ladder de prueba** para simular resultados exclusivamente en memoria. No se conecta a partidas, perfiles, saldos ni servicios; no equipa ni concede tableros. Modos puntuables, consecuencias de desconexión y balance comercial siguen pendientes.
- Integración verificada de 21 archivos. Cierre **91/91 EditMode** en Unity, con 74 casos del núcleo, 10 del catálogo y 7 del ensayo 0/150. Regresión **3/3 PlayMode** de selección/persistencia de tableros y apariencia del lobby, sin fallos. Esta evidencia es anterior al ajuste posterior de áreas táctiles y no incluye inspección visual. Informes y límites en `QA_CHECKLIST.md`; las pruebas independientes anteriores se conservan.
- Informe `LADDER_PACING.md` y herramienta Node sin dependencias para reproducir expectativas de encuentros y tiempos. Distingue la comparación histórica de umbrales de la política actual de dos etapas y separa escenarios de mediciones reales.

## 2026-10-22 — selector en pantallas pequeñas y orientación

- Ajuste técnico del selector existente: área de pulsación mínima de 44 píxeles, texto mínimo de 12 píxeles según la escala real y recuperación al volver a una pantalla mayor. Mantiene la geometría y los recursos visuales originales.
- Geometría validada con **8/8 EditMode**. La primera tanda PlayMode conserva **5/6**: su única incidencia fue una GameView de 480 píxeles de alto frente al mínimo de 512 requerido por la prueba de clics. Tras ajustar la preparación/restauración del entorno, el cierre pasó **6/6 PlayMode**, con capturas reales ES/EN inspeccionadas. Estos seis incluyen las tres regresiones previas; no son nueve casos únicos. Historial de interrupciones y resultados en `QA_CHECKLIST.md`.
- La aclaración del creador exige móvil fijo en vertical normal, sin horizontal ni boca abajo; PC puede usar ventana ancha. La auditoría confirmó esa configuración ya existente en las fuentes, sin cambios ni certificación en dispositivos físicos.
- Diseño visual del nuevo recorrido/selector aprobado después de presentar la propuesta fuera de Unity. El creador precisó que deben corregirse los números borrosos mediante cifras nativas nítidas; no pidió rediseñar botones. La nomenclatura visible será **TABLEROS / BOARDS**, con «Tablero actual» y «Siguiente tablero», tras rechazar «ARENAS». La implementación nativa sigue en curso. Las vistas horizontales corresponden a PC; esta aprobación no define acceso a Hotel ni selección tras un descenso.
- Pantalla nativa y enlace QA integrados en diez archivos. Se repararon dos metas de tests malformadas conservando sus GUIDs; el intento inicial no se declara aprobado. V1 pasó **9/9 EditMode y 3/3 PlayMode** en Unity, con seis capturas preservadas. La revisión posterior detectó tamaño insuficiente en alta densidad. La reparación a escala lógica 360×640 pasó **19/19 EditMode**, pero la tanda combinada posterior quedó en **3/6 PlayMode**: TABLEROS 1/3 por dos recortes de texto a escala 2, lobby 2/3 por su fixture. Se conservan el XML y dos capturas parciales; la fase permanece abierta mientras se corrige medición y se repite.
- El trofeo original no fue aprobado. Se solicitaron propuestas más pequeñas y con menos brillo para PLATA, ORO y LUNAR, con acabado cristalino tipo diamante para el último. Las imágenes finales y su distribución entre dos tableros siguen pendientes; el paquete técnico del icono anterior no se importa.
- Cierre posterior de la pantalla QA: medición de Lato a escala real del Canvas, con márgenes y aserciones intactos. **6/6 PlayMode** (TABLEROS 3/3 y lobby 3/3), once capturas nativas revisadas y conservadas. Los 19/19 EditMode de geometría siguen aplicando a los cálculos sin cambios. El recorte y la escala insuficiente quedan resueltos; no se activó progreso competitivo de cuentas.
- La referencia posterior del creador sustituye las primeras variantes: copa de forma sencilla en PIXEL ART, contorno oscuro, paleta reducida y reflejos mínimos, «parecido a este, pero no igual». Lunar conserva cristal/diamante con brillo contenido. Ningún icono nuevo fue importado.

## 2026-10-22 — MP-DEC-037, respuesta visual de interacción

- Anillo blanco translúcido durante la pulsación larga de una unidad. Se consume a lo largo de los **0,5 s existentes**, abre su ficha una vez y se cancela al soltar antes, arrastrar o perder el contexto. Cubre campo, rivales, eco, Maleta y banco; conserva toque, colocación y traslado, sin interceptar raycasts.
- Las habilidades aparecen a color solo si pueden comprarse en ese momento. La comprobación usa el coste concreto, saldo, oferta/token, propiedad de la instancia y bloqueos reales; no muta la selección ni gasta recursos. Las demás se ven grises y siguen permitiendo consultar la ficha y descripción. La compra mantiene su validación real y sus costes/límites.
- **Maleta** sustituye al nombre visible Brief en español, con artículos y mayúsculas coherentes. El inglés conserva **Brief**; no se cambian IDs, claves ni datos del jugador.
- Contador central con contorno más definido, naranja en 5–2 y rojo oscuro intenso en 1. Conserva tiempos, aviso de preparación/combate, pulso y desvanecimiento existentes; no modifica el reloj ni la entrada del jugador.

<!-- MP-DEC-037-VALIDATION-BEGIN -->
Validado con **52/52 EditMode** y 21 casos únicos de escena. La primera tanda Play (18/21) detectó falta de GameView para tres capturas; reparada únicamente la preparación de los fixtures, la repetición completa de los tres pasó **9/9**, manteniendo los doce casos adicionales ya aprobados. Se revisaron seis capturas reales y 27 hashes de fuentes, sin diferencias. Informes, intentos y límites en `QA_CHECKLIST.md`; la pantalla de carga solicitada después tiene un alcance separado.
<!-- MP-DEC-037-VALIDATION-END -->

El roadmap global permanece incompleto. Los trofeos PIXEL ART y su distribución siguen pendientes; no se activan copas de cuenta, cobros, servicios ni publicación.

## 2026-10-22 — MP-DEC-038, imagen completa de carga y fundido de entrada

- Sustitución del arranque con logo aislado por la imagen exacta suministrada de **941×1672**, sin editar sus píxeles. SHA256 `48907AA47FBB48803548C6A1AAAD38DA597A6D0BA7920DEA1D4418D68CB930C1`; recurso `MonsterPouch/UI/Branding/loading-screen-supplied`. Sprite completo con filtro Point, sin compresión ni mipmaps.
- Imagen contenida en el área segura de móvil vertical y PC, sin recorte ni deformación, con el color oscuro existente cubriendo los espacios libres. Desvanecimiento elegido entre las alternativas autorizadas: mínimo opaco **0,64 s** y fundido **0,44 s**, reutilizando tiempos existentes y sin cambiar la escala de tiempo.
- Arranque de la escena existente con overlay único; se completa después de preparar la vista real. Conserva lobby de cuentas existentes, elección inicial y entrada/reanudación del tutorial de cuentas nuevas. El hook no vuelve a crear una carga al reconstruir la UI.
- Bloqueo temporal de puntero y teclado, con liberación y destrucción al finalizar o cancelar. La carga pendiente sobrevive a la descarga de su escena de origen; la desactivación no deja un Canvas bloqueando la pantalla.
- Splash anterior desactivado tanto en configuración como en su herramienta de Editor. El logo y sus otros usos se conservan; no se sustituyen modales, ficha de XP ni cambios de tablero por cargas ficticias.

<!-- MP-DEC-038-VALIDATION-BEGIN -->
Validación integrada cerrada: **10/10 EditMode y 9/9 PlayMode**, con salida 0 en ambos procesos (2026-10-22, 22:40:46–22:40:47 y 22:45:23–22:46:27 UTC). Play incluye cuatro casos de carga y cinco regresiones de pulsación/estilo. Se inspeccionaron seis PNG nativos a 320×568, 1080×2400, 1536×2048, 1920×1080 y dos a 540×960 para fundido/lobby: imagen completa, proporción conservada y transición visible hacia una UI operable. La integración confirmó 11/11 hashes previstos. Evidencia en `loading-artwork-integration-20261022` y detalle en `QA_CHECKLIST.md`. MP-DEC-037 permanece intacto; no se ha probado una build instalada ni un móvil físico.
<!-- MP-DEC-038-VALIDATION-END -->

Este alcance no completa el roadmap ni activa compras o publicación. Los trofeos PIXEL ART y su distribución continúan pendientes de aprobación.


## 2026-10-22 — MP-DEC-039, mejoras al arrastrar copias y claridad de colocación/resultados

- Corrección preparada para que arrastrar una copia poseída desde la Maleta a una casilla del tablero mejore su unidad original aunque haya otro actor en el destino. Conserva posición e identidad de ambas unidades, habilidad seleccionada, coste, límite de copias y comando online de compra. El despliegue inicial y las rutas de banco permanecen sujetos a sus validaciones actuales.
- Retirada de la línea diagnóstica pequeña bajo los resultados, manteniendo los datos de diagnóstico, título, tarjetas, puntuación, ganador, comparación de vida, transición y avisos de rechazo necesarios.
- Contornos verdes existentes más intensos y gruesos para el mismo conjunto de casillas válidas. Soltar una miniatura fuera del tablero oculta su previsualización y mantiene su origen sin la frase «Elige una casilla iluminada, el bench o el Brief.». Se conservan los destinos de banco/venta y los errores concretos de colocación o saldo.
- El aviso desconocido «Listo» queda aplazado por instrucción del creador, sin cambios ni nueva consulta para continuar este alcance.

<!-- MP-DEC-039-VALIDATION-BEGIN -->
Validación enfocada cerrada con **98 casos únicos EditMode y seis únicos PlayMode**. Los intentos iniciales 97/98 y 5/6 se conservan: se repararon únicamente una prueba de pausa configurada online y el helper que restauraba el tamaño libre de GameView. Las fixtures afectadas pasaron después 10/10 Edit y 4/4 Play; los otros 88 Edit y dos Play ya aprobados permanecen válidos. No se duplican casos al contar repeticiones. Captura nativa 540×960 revisada: contornos más visibles y UI legible. XML, reparaciones, hashes y límites en QA_CHECKLIST.md; sin validación física de dispositivos. MP-DEC-037 y MP-DEC-038 permanecen intactos. El resultado acreditado es Passed del Test Runner; Unity visible permanece abierto y no se atribuye un código de salida de proceso a estas tandas. Se verificaron 13/13 hashes de archivos integrados tras las reparaciones de pruebas.
<!-- MP-DEC-039-VALIDATION-END -->

No se añaden reglas creativas, recompensas, compras reales, servicios de pago ni publicación; el roadmap global continúa abierto.


## 2026-10-22 — MP-DEC-040, accesos del lobby, bordes propios y filtro rival durante la selección

- Expendedoras encima de misiones en el lobby, conservando sus acciones.
- Resaltado solicitado: solo bordes propios iluminados, superficie propia conservada y filtro rojizo suave/translúcido en la mitad rival durante la selección de unidades o el arrastre de ofertas y unidades del campo/banco, siguiendo LocalSide incluso con la vista invertida. Es presentación; conserva casillas legales, ocupantes, compras y colocación.

<!-- MP-DEC-040-VALIDATION-BEGIN -->
Validación de MP-DEC-040 cerrada con **ocho casos únicos PlayMode aprobados**: 1/1 del orden del lobby en tres tamaños (23:34:10–23:34:32 UTC) y 7/7 del tablero y regresiones, sin fallos ni omitidas (23:46:05–23:46:29 UTC del 2026-10-22). Se inspeccionaron tres capturas 540×960: accesos sin solapamiento, solo bordes verdes propios y filtro rojizo rival, también con la vista roja invertida; superficie propia conservada y sprites encima. Coinciden 8/8 hashes finales. Informes y límites en QA_CHECKLIST.md. Unity visible sigue abierto: la evidencia es Passed del Test Runner, sin código de salida de proceso ni validación física. MP-DEC-039 conserva su cierre.
<!-- MP-DEC-040-VALIDATION-END -->

## 2026-10-22 — MP-DEC-041, vida cerca de la cabeza y prioridad del HUD frontal

- Preparados 23 puntos de cabeza individuales en los assets actuales; siguen escala y elevación del dibujo, sin tomar armas o efectos como altura de cabeza. El HUD existente conserva un margen inferior de 3 píxeles de diseño y sus medidores/insignias juntos.
- Orden de dibujo según proximidad en pantalla, dentro de la capa «Unit vitals», conservando los controles y modales por encima. Sin cambios de estadísticas, tamaño de personajes, compras o combate.

<!-- MP-DEC-041-VALIDATION-BEGIN -->
Integración y validación cerradas el 2026-10-23, fecha local: **siete casos únicos PlayMode aprobados**. El primer 5/7 detectó dos errores de preparación de pruebas; reparada únicamente la fixture, su repetición pasó 3/3, manteniendo las cuatro regresiones aprobadas. Root revisó ocho capturas 540×960 de los 23 personajes, escala al 125 %, solapamiento azul/rojo y medidores/estados: cabeza libre y HUD próximo delante. La revisión de 29 fuentes conserva la reparación de la prueba y los ajustes concurrentes de energía/tamaño de Tauruk, con su anclaje intacto, como excepciones documentadas; no se declara coincidencia total con el manifiesto inicial. Informes y límites en QA_CHECKLIST.md. Sin móvil físico ni cambios de banco incluidos en esta decisión.
<!-- MP-DEC-041-VALIDATION-END -->


## MP-DEC-042 — Banco, reservas y fusión (2026-10-23)

Se aplican las reglas aprobadas en DECISIONS.md: reserva gratuita, reembolso al guardar, cobro al retirar, venta de banco sin devolución adicional y fusión entre la unidad del campo y la reserva del mismo tipo. Se conservan límites, mejoras, regalos del tutorial e identidades del catálogo.

<!-- MP-DEC-042-VALIDATION-BEGIN -->
**MP-DEC-042 integrado y validado: 96/96 EditMode y 16/16 PlayMode aprobados.** Se revisaron 2 capturas y se verificaron los hashes de 20 archivos integrados. Evidencia consolidada en bank-economy-integration-20261023/final-validation.json y QA_CHECKLIST.md. La evidencia corresponde al Test Runner del Editor; no se atribuye un código de salida de proceso ni validación en móvil físico o red remota. MP-DEC-041 conserva su cierre independiente; este alcance no completa el roadmap global.
<!-- MP-DEC-042-VALIDATION-END -->


## 2026-10-24 — MP-DEC-043, recuperación completa y preparación única

- Registrada la aclaración explícita «Una sola preparación desde que vuelve a idle». Integrado el ajuste de recuperación para iniciar ese único ciclo tras finalizar la habilidad y su retorno, sin sumar una segunda espera de primer ataque.
- Integradas duraciones compartidas para aterrizaje de Hymal y extracción de Blotun; conservación de estadísticas, arte, intervalos y recompensas. La extracción cancela la preparación no lanzada y mantiene los proyectiles que ya salieron.
- Ejecutadas pruebas de aterrizaje, extracción, duración editable, proyectiles y determinismo; ampliación de la cobertura de súper a los personajes que faltaban.

<!-- MP-DEC-043-VALIDATION-BEGIN -->
**MP-DEC-043 integrado y validado en el Editor.** El conjunto compartido con MP-DEC-044 tiene 207/207 casos únicos EditMode y 5/5 PlayMode aprobados, más 3 casos nuevos de compatibilidad online aprobados; los reintentos no se cuentan otra vez. Se cierra el alcance funcional de recuperación y preparación única. Se verificaron 28 archivos únicos de combate y protocolo frente a sus manifiestos finales, incluidas las dos reparaciones de fixtures. Protocolo actual: `mp-2026-10-24-v14-combat-occupancy`; rechaza clientes v13 y balances distintos. Evidencia y límites en QA_CHECKLIST.md. No se atribuye revisión manual de capturas, móvil físico, build instalada o red remota. MP-DEC-042 conserva su cierre independiente.
<!-- MP-DEC-043-VALIDATION-END -->

## 2026-10-24 — MP-DEC-044, reserva separada de ocupación

- Registrada la corrección aprobada: la selección de objetivo usa la casilla realmente alcanzada; una reserva de movimiento no cuenta como ocupación adelantada.
- Integradas la confirmación del paso al llegar y la cancelación de reservas obsoletas, conservando pausa inicial, prioridades y objetivos válidos. Se combinaron los cambios compartidos con MP-DEC-043 y se preservó el trabajo concurrente sobre objetivos.

<!-- MP-DEC-044-VALIDATION-BEGIN -->
**MP-DEC-044 integrado y validado en el Editor.** Comparte los 207/207 EditMode y 5/5 PlayMode únicos de MP-DEC-043, con 3 comprobaciones adicionales de compatibilidad online. Las pruebas de ocupación, cancelación física y gancho pasan; la reparación de presentación cambió únicamente el instante de muestreo de la fixture. Los 28 archivos únicos verificados corresponden al mismo conjunto de combate y protocolo, no a otro conjunto que deba sumarse. Se cierra este alcance funcional, conservando las restricciones de validación del Editor y el cierre independiente de MP-DEC-042.
<!-- MP-DEC-044-VALIDATION-END -->


## 2026-10-24 — MP-DEC-045, distribución del casino

- Registrada la composición de la referencia `codex-clipboard-03619ae6-be01-4549-a052-61ad55e4bb8c.png`, con los mismos recursos en móvil vertical y PC.
- Dados ocultos sin borrar sus recursos; alcance de distribución e interacción, sin cambios de mecánicas ni aprobación de arte nuevo.

<!-- MP-DEC-045-VALIDATION-BEGIN -->
**MP-DEC-045 integrado y cerrado en Unity:** 22/22 casos específicos del casino aprobados y regresión compartida final 9/9 aprobada. Las dos capturas nativas de Game view, 540×960 y 1366×768, fueron inspeccionadas por root y confirman distribución, marco/fichas y arte conservados, ausencia de dados, Maleta debajo a la izquierda y accesorios periféricos en PC. Evidencia: `casino-layout-integration-20261024/results/progression-casino-play.xml`, `results/casino-viewport-regression-play-final.xml` y `native-visual-validation.json`. La regresión final resuelve tres expectativas antiguas de fixtures y un defecto real de área segura; no se modificó el diseño para acomodar las pruebas. Se cierra el alcance visual aprobado, sin atribuir validación en dispositivo físico o build instalada.
<!-- MP-DEC-045-VALIDATION-END -->

## 2026-10-24 — MP-DEC-046, Pouch diaria y tabla aprobada

- Registrados nueve resultados: 10/20/30/50 Earthkens y los mismos importes de Sunkens, con 10/15/15/7,5 % para cada moneda; 5 Saturnkens con 5 %. La tabla anterior queda histórica y sus recibos se conservan.
- Integrados apertura y premio simple sin porcentajes; Vista previa azul `daily-pouch-blue-v1.png` aprobada para integración exacta, sin redibujar; falta comprobar su presentación nativa. Los Saturnkens se guardan en la cuenta para gasto decidido por el jugador, sin consumo automático ni Saturncoin inicial/diaria.

<!-- MP-DEC-046-VALIDATION-BEGIN -->
**MP-DEC-046: alcance local del regalo aprobado e integrado, con tabla, persistencia, flujo y arte azul validados.** Los 9 DailyGiftTests y el caso del regalo en lobby habían aprobado; los recibos/crédito se validaron dentro de 109 Edit únicos de regalo/perfiles. La nueva prueba `VendingUnlocksPlayTests.ApprovedCatalogueShowsAllCurrentMachineArtAndDailyGiftUsesItsBluePouch` pasó y root revisó la captura nativa del regalo con la imagen azul exacta aprobada, cifras y botón legibles. Registro: `approved-vending-catalogue-integration-20261024/native-visual-validation.json`. La regresión compartida posterior resolvió sus tres fallos de fixture: Lobby pasó 4/4 al repetir, incluido el regalo; el total es 11 casos únicos Play aprobados según MP-DEC-048. Se cierra el alcance local del regalo con tabla, flujo, arte y regresión de Lobby validados. La bienvenida posterior de 20 Saturnkens corresponde a MP-DEC-050, todavía sin implementar.
<!-- MP-DEC-046-VALIDATION-END -->

## 2026-10-24 — MP-DEC-047, 5 Saturnkens directos por expendedora

- Registrado el coste aprobado de 5 Saturnkens sin Saturncoin intermedia y con gasto elegido por el jugador; se integra la transacción local de débito, resultado y desbloqueo.
- El creador aprobó para pruebas diez paquetes: Earthkens 10/20/30/50, Sunkens 10/20/30/50 y mixtos 10+10 y 20+20, con probabilidad provisional de personaje pendiente por apertura 1–10 de 1/1/1/1/1/2/4/8/20/100 %. Se conserva MachineEngine sin repetir premios ni volver a entregar el personaje obtenido; no se añade reinicio del ciclo. La implementación está integrada y sus 31 pruebas específicas EditMode están aprobadas; falta la revisión visual final; esta aprobación no concede una recompensa de bienvenida ni Saturncoin diaria.

- El creador aprobó integrar los 23 diseños y sus asociaciones de `output/imagegen/vending-machines-v1/index.html`; se conservan sus PNG y hashes exactos. El registro original de arte pendiente queda superado por esta aprobación. La importación ya se realizó; la validación nativa final todavía no se presenta como ejecutada.

El creador también aprobó que todos los premios pendientes tengan el mismo peso, salvo las garantías del personaje y de su primera habilidad; no se repiten premios dentro de una máquina.

<!-- MP-DEC-047-VALIDATION-BEGIN -->
**MP-DEC-047: alcance local de expendedoras cerrado, con catálogo de 23 máquinas, configuración aprobada y arte validado.** Los 31 ApprovedVendingEconomyTests, 10 GuestVendingTests y 7 AndroidPortraitTests pasaron en la tanda 48/48; las tres VendingUnlocksPlayTests aprobaron en `pouch-room-ui-integration-20261024/results/pouch-room-play-initial.xml`, incluida la nueva prueba de catálogo/arte reales. Las dos pruebas de infraestructura ya habían pasado y no se cuentan como nuevas. Root inspeccionó la captura nativa del catálogo desplazable con sus 23 asociaciones y nombres actuales. Coste directo 5, paquetes y curva aprobados, idempotencia, propiedad y ausencia de aperturas automáticas se conservan. La migración de acceso/economía de Legacy continúa pendiente y no se valida autoridad de servidor, pagos reales ni dispositivo físico. Los fallos de fixture de Lobby de la tanda compartida quedaron resueltos con 4/4 en la repetición; MP-DEC-048 registra 11 casos únicos Play aprobados.

Reparadas únicamente las metas del catálogo y de sus tests con GUID válidos mediante `approved-catalogue-meta-repair-integration-20261024/manifest.json`; los datos y scripts no cambiaron. La primera ejecución de 17 pruebas no descubrió los 31 casos ignorados y se conserva como intento incompleto. Tras la reparación, `approved-machines-edit.xml` acredita 48/48 (31 nuevos del catálogo, 10 de expendedoras y 7 de orientación); la regresión Play final 9/9 aprobó; las capturas específicas posteriores del regalo y las expendedoras ya fueron revisadas.
<!-- MP-DEC-047-VALIDATION-END -->

<!-- ONLINE-REVEAL-ATOMIC-VALIDATION-BEGIN -->
## 2026-10-24 — Corrección técnica de revelación online atómica

Se corrigió el caso en que una secuencia recibida contenía decisiones válidas seguidas de una inválida: antes podían quedar aplicadas compras, movimientos, fusiones de banco o rerolls parciales. Ahora se valida el diario completo en un estado privado y solo se confirma si todos sus comandos son válidos. Un rechazo conserva saldo, ofertas, unidades, reservas, referencias existentes y secuencia aleatoria; un éxito conserva la identidad de la instancia superviviente y solo se aplica una vez. Es una corrección técnica de las reglas vigentes, sin decisión creativa nueva.

**Integrado y validado en EditMode:** `online-reveal-atomic-integration-20261024/results/online-wallet-edit-final.xml` contiene 152/152 aprobados: OnlineRevealAtomic17, OnlineMatch25, BankEconomy19, PouchState27, GuestAbility54 y GuestVending10. Los 17 casos de atomicidad cubren cola inválida por tipo de comando, reroll rechazado, fusiones en ambas direcciones, venta, recompra e identidad al retirar del banco. Los 6 archivos de `online-reveal-atomic-integration-20261024/manifest.json` coinciden en proyecto y staging. La versión vigente de conexión es `mp-2026-10-24-v14-combat-occupancy`; no se mezclan clientes anteriores aunque el balance coincida. La regresión compartida de UI/mapas/regalo/expendedoras quedó aprobada 9/9 en `casino-layout-integration-20261024/results/casino-viewport-regression-play-final.xml`; no constituye una prueba de transporte online. No es una validación de red remota ni de servidor autoritativo.
<!-- ONLINE-REVEAL-ATOMIC-VALIDATION-END -->

## 2026-10-24 — MP-DEC-048, Sala de estuches con reglas completas

- Aprobadas cinco oportunidades diarias UTC por victorias de partidas completas de cualquier tipo, incluidos bots; acceso desde menú de tres rayas.
- Primeras tres azules; últimas dos con 75 % azul/25 % personaje. Personaje sorteado uniformemente entre no obtenidos y fijado por ID en el estuche.
- Apertura del estuche de personaje: 0,5 % del personaje concreto, en otro caso tabla exacta DailyGift. Propiedad obtenida antes de abrir o colección completa resuelve a recursos. No afecta a las probabilidades de expendedoras ni al recibo del regalo diario.
- El cambio de día descarta estuches sin abrir y renueva cinco oportunidades. Abrir no repone; se conservan premios y recibos ya confirmados.

<!-- MP-DEC-048-VALIDATION-BEGIN -->
**MP-DEC-048 integrado y cerrado en Unity: 185/185 EditMode y 11 casos únicos PlayMode aprobados, con 7 capturas nativas revisadas.** La tanda Edit incluye 26 pruebas nuevas de Sala y 159 regresiones, sin contarlas de nuevo en otros cierres. La primera tanda Play terminó 8/11; tras reparar únicamente el ciclo de vida de la fixture de Lobby, su repetición completa pasó 4/4 entre 11:29:49 y 11:30:39 UTC del 24-10-2026. Se conservan los otros 7 casos aprobados: 11 únicos, no 15. Las capturas de 320×568, 393×852 con área segura y 1280×720 confirman cinco posiciones, estuches azules/personaje, abiertos sin reposición y premios de recursos/personaje. Evidencia final: `pouch-room-ui-integration-20261024/final-validation.json` y sus XML/capturas. Se cierra este alcance local, sin validar dispositivo físico ni servidor autoritativo; la política Legacy sigue pendiente.
<!-- MP-DEC-048-VALIDATION-END -->

## MP-DEC-049 — Desbloqueo permanente por 100 Earthkens + 100 Sunkens (2026-10-24)

Aprobado cobrar ambas monedas juntas una sola vez para desbloquear permanentemente una habilidad bloqueada. Los desbloqueos gratuitos de cajas conservan su gratuidad; no es un cobro por uso ni cambia la compra temporal de mejoras de combate. Los IDs y recibos de propiedad se conservan y una habilidad ya obtenida no se cobra de nuevo.

<!-- MP-DEC-049-INITIAL-LOCK-APPROVED -->
**Aclaración posterior aprobada directamente por el creador en el chat principal:** las cuentas nuevas comienzan con **las tres mejoras bloqueadas**. Se conserva la habilidad automática base de cada personaje. El bloqueo permanente debe reflejarse en la disponibilidad real de las mejoras, además de su presentación; desbloquear una mejora por monedas o por un premio aprobado la incorpora a la propiedad de la cuenta. Los costes y reglas temporales de mejorar durante una partida se conservan. Esta decisión no convierte la habilidad base en una compra, no revoca habilidades ya obtenidas y no resuelve por sí sola los derechos de cuentas Legacy.

Esta aclaración directa amplía la decisión de precio transmitida anteriormente desde el chat lateral y prevalece en el alcance de las cuentas nuevas. No aprueba premios adicionales de módulos del tutorial ni un estuche azul de tutorial.
<!-- MP-DEC-049-COPY-LIMIT-APPROVED -->
**Aclaración directa posterior sobre copias y fusiones:** el límite total de copias de cada tipo en la partida es **1 + número de habilidades permanentes desbloqueadas de ese personaje**. Sin mejoras desbloqueadas permite 1 copia; con 1 permite 2; con 2 permite 3; con 3 permite 4. La habilidad base automática no cuenta como una de esas tres mejoras.

El límite se aplica al total del tipo, incluidas las copias de la unidad principal y de su reserva de banco; no concede copias adicionales ni propiedad por guardar, retirar o fusionar. Las compras y fusiones solo pueden incorporar mejoras desbloqueadas. La siguiente mejora por orden numérico es la **primera desbloqueada que todavía no esté presente** en la unidad; no se concede una mejora bloqueada. Esta aclaración resuelve la interacción antes pendiente y ajusta el límite fijo de cuatro de MP-DEC-042 a un máximo variable de 1–4 según propiedad.

Se conservan la inversión/reembolso y los demás límites del banco, la identidad de las unidades y la contabilidad de stock. El tutorial conserva su permiso efímero de entrenamiento aprobado, limitado a `IsTraining`, sin recibos de propiedad ni cambios en la cuenta; no se extiende a partidas BO5 normales. No se inventan regalos para completar el límite de copias ni se cobra la habilidad automática base.

<!-- MP-DEC-049-VALIDATION-BEGIN -->
**Regla aprobada; auditoría técnica previa realizada, implementación e integración pendientes.** Preparar una transacción única de cartera y propiedad, reintentos idempotentes y migración conservadora desde v7 después de validar MP-DEC-048. No se atribuyen pruebas ni activación del precio a este registro.
<!-- MP-DEC-049-VALIDATION-END -->

## MP-DEC-050 — Bienvenida única para cuentas nuevas (2026-10-24)

Aprobados directamente **20 Saturnkens una sola vez por cuenta nueva**, equivalentes al coste de cuatro aperturas de expendedora, siempre elegidas por el jugador. La concesión debe ser idempotente y conservar cuentas existentes/Legacy; no es un regalo al abrir el juego ni un premio diario. No se aprobaron otras recompensas de módulos ni un estuche azul del tutorial.

<!-- MP-DEC-050-VALIDATION-BEGIN -->
**Regla aprobada; contrato técnico en preparación, implementación y pruebas pendientes.** Esperar el cierre de Sala de estuches antes de implementar la concesión. No se atribuye saldo entregado ni validación a este registro.
<!-- MP-DEC-050-VALIDATION-END -->


## 2026-10-24 — mejoras, bienvenida e icono DEV

MP-DEC-049/050: perfil v8, cartera/recibos, fichas de colección, acceso de partida, banco y handshake v15. 341 casos únicos EditMode y 14 PlayMode aprobados; 38 de los EditMode son nuevos de economía/copia. Los dos fallos de la regresión eran fixtures que suponían cartera vacía; tras corregir la preparación, sus 54 casos completos pasaron. Cinco capturas nativas 393×852 y 1280×720 revisadas. Informes en permanent-ability-welcome-integration-20261024, permanent-ability-ui-integration-20261024 y dev-icon-integration-20261024. No se suma la primera ejecución de 215, que no incluyó los fixtures nuevos durante la recarga.

MP-DEC-052: nuevo PNG exacto en art/branding y ámbito temporal de iconos en MonsterPouchAndroidBuild; 5 pruebas aprobadas. Assets/escenas/personajes originales conservados. MP-DEC-051 aprobado y en preparación; aún no activado.


## 2026-10-24 — tutorial de colección y expendedoras

- Estuche azul único del módulo 7, apertura real y persistencia atómica; no consume los cinco espacios diarios.
- Visitas a Maleta, colección, habilidades y monedas; avance solo tras completar objetivos.
- Módulo 8 conectado a catálogo y extracción real, sin gasto automático ni nueva bienvenida.
- Revisión de una extracción previa para evitar bloquear a quien ya gastó su saldo; reanudar y repasar sin cobrar otra vez.
- Perfil v9 y pruebas de migración y fallos de guardado. 162 EditMode y 28 PlayMode únicos finalmente aprobados.
- Fixtures antiguos del tutorial cierran su UI antes de retirar sus perfiles temporales. No altera el juego ni elimina datos del usuario.
- Premio de mejora: icono de la habilidad obtenida; cobertura de extracción garantizada y revisión sin duplicar propiedad ni gasto.



## 2026-10-25 — tamaño del casino, sombras y premios visuales

Correcciones pedidas por el creador tras dejar el punto jugable:

- Grand Casino Hotel comparte la misma expansión horizontal, dimensiones y centros de las 60 casillas que Parque de Bolas y Campo Real. Se elimina la excepción que lo hacía un 9,09 % más estrecho. El cambio de estilo ya no modifica la geometría bajo los actores. Se conserva cámara, altura, distribución y recursos originales.
- Se recuperan las 11 sombras autoradas del payaso, carro y fichas, sin duplicarlas con siluetas generadas. Dados y sus sombras permanecen ocultos; los accesorios periféricos mantienen sus sombras.
- La lista de premios de expendedoras utiliza imágenes existentes. Monedas: iconos de 76 unidades UI, cantidades de 30 y nombres de 18, con espacio suficiente para dibujar las cifras, incluidos paquetes mixtos. Personajes y habilidades: etiquetas genéricas PERSONAJE / NUEVA HABILIDAD. Las habilidades no disponibles conservan la presentación gris; siguen siendo consultables.
- Por petición posterior se retiran los porcentajes visibles. Se mantienen estados OBTENIDO / BLOQUEADO, garantías, precios, pesos internos y recibos. La recompensa obtenida mantiene su icono y su identificación.
- La compilación Android normal ahora muestra confirmación con Mostrar carpeta, como DEV. Se verificó un APK normal ya existente: Builds/Android/2026-10-25_00-00-40/MonsterPouch_2026-10-25_00-02-43.apk, paquete com.monsterpouch.game, sin debuggable ni desbloqueo DEV, ARMv7/ARM64. Ese APK es anterior a estos cambios. No se ha recompilado ni publicado otro APK; su compilación anterior duró 40 min 42 s.

Validación: 15/15 EditMode Android y 38 casos únicos PlayMode finalmente aprobados. Tras ampliar monedas y retirar porcentajes se repiten los casos de expendedoras y tutorial. La comprobación de cantidades exige geometría de texto para detectar cifras truncadas, además de sus valores. Capturas nativas de premios móvil/PC y casino móvil/PC se revisan antes del cierre; evidencias en los paquetes de integración 20261025 del workspace.

El avance a nuevos módulos del roadmap sigue pausado. No se reinician perfiles ni se convierten pruebas en premios de cuentas reales.

## 2026-10-25 — MP-DEC-055, copas locales conectadas a salas online

- Perfil invitado v10 con copas actuales/máximas, política y recibos persistentes. Las cuentas anteriores incorporan copas 0 conservando los demás datos; no se importan partidas antiguas, XP ni saldos QA/DEV.
- BO5 online completo confirmado por ambos clientes: +30 por victoria, −5 con saldo previo menor de 150 o −30 desde 150, con suelo 0. Bots, tutorial, rondas y resultados incompletos no puntúan.
- Protocolo `mp-2026-10-25-v16-online-trophies`, GUID compartido y hash del marcador BO5 en orden canónico. Publicación única del resultado confirmado; UI vuelve a comprobar la sesión y evidencia antes del guardado.
- Recibos idempotentes y cola local de reintento ante fallo de escritura, conservada al volver al menú. La cola no acredita recuperación después de cerrar el proceso ni autoridad remota.
- Contadores sin datos en 0, copas reales de la cuenta y acceso a Campo Real desde 150 copas actuales. El descenso vuelve a bloquearlo y corrige una selección inválida únicamente en menú.

Alcance de archivos: perfiles/guardado (`LocalGuestProfile`, `LocalGuestProfileStore` y adaptador `GuestTrophyLadder`), conexión y marcador (`OnlineRoom`, `online-match`), presentación y reintentos (`local-trophy-results`, `local-trophy-boards`, lobby/tableros) y sus pruebas. No se modifica el arte ni se incorpora un servidor, emparejamiento, cobros o publicación. El resto de la expansión sigue pausado.

<!-- MP-DEC-055-VALIDATION-BEGIN -->
**Alcance local validado en Unity: 380 casos únicos EditMode y 25 PlayMode aprobados.** La primera tanda Edit aprobó 378/380. Las dos correcciones fueron exclusivamente de pruebas: versión de perfil codificada como 9 y comparación de actores aún privados durante preparación. La repetición completa de sus dos fixtures aprueba 46/46, conservando los otros 334 casos aprobados, sin sumar reintentos.

La primera tanda de escenas aprobó 22/22. Unity había omitido los tres casos nuevos del enlace online por un GUID de metadatos malformado; se reparó el meta y se ejecutó esa fixture completa, 3/3. No se presenta la primera tanda como cobertura del enlace online. Incluye resultado confirmado una sola vez, persistencia, ausencia de puntos por bots/entrenamiento, rechazo de resultados discordantes y reintento tras fallo de escritura.

Se revisaron cuatro capturas nativas del recorrido a 0 y 150, presentación PC y bloqueo de Campo Real tras descender a 120. Los archivos y XML se conservan en `ladder-runtime-integration-20261025`. Compilación previa de cinco ensamblados y compilación real del Editor sin errores. No se recompiló APK ni se probaron dos dispositivos mediante Relay real; la confirmación entre clientes y el guardado local no equivalen a servidor autoritativo. El resto del roadmap continúa incompleto y su expansión sigue pausada salvo este alcance.
<!-- MP-DEC-055-VALIDATION-END -->


## 2026-10-25 — Habilidades, marca frontal y cierre local anterior a naciones

- Habilidades y marca frontal de Tauruk: 186 EditMode y 12 PlayMode únicos aprobados, seis capturas revisadas. Descripciones concisas, texto negro y parámetros actuales; sin cambios de balance por redacción.
- El cierre local anterior a las naciones registra 409 EditMode aprobados (303 online/cercanía + 106 perfiles/estuches/arte) y 21 PlayMode únicos aprobados. La primera tanda Play terminó 20/21; la repetición aislada del fixture de arte pasó 2/2 y no se suma dos veces. Evidencia: `online-pouches-cleanup-checkpoint-20261025.json`. Estos resultados no validan las naciones, el nuevo ladder, el encabezado ni el salto de Kazuru.
- Estuches por victorias sin límite de cantidad: ciclo repetido de tres azules y dos sorteos 75 % azul / 25 % personaje, con 30 % de desbloqueo al abrir el estuche de personaje. Se conserva la caducidad diaria UTC de los estuches pendientes; ilimitado no significa acumulación entre días. El regalo diario conserva tabla, periodicidad y recibos.
- Limpieza de 54 PNG y 54 metadatos aplicada con copia recuperable. Regresión local previa completada; 138,27 MB es una estimación, sin nuevo APK ni medición final.

## 2026-10-25 — Cinco tableros integrados, nueva presentación y QA en curso

- Naturalis 0 → Aquarion 150 → Geothar 300 → Volkanar 450 → Campo Real 600; victoria +30 en todos los tramos y derrotas −5/−10/−15/−25/−30 según el saldo anterior a la partida. Inicio y suelo 0, sin techo de copas.
- Parque de Bolas (`classic`) y Grand Casino Hotel (`lounge`) quedan como cosméticos fuera del recorrido. El Hotel conserva su categoría de pago, pero no se activa IAP, cobro ni publicación. Aireola queda fuera del ladder por ahora; sus assets se conservan. El acceso a los tramos depende de las copas actuales y se vuelve a restringir al descender; no se cambian tableros bajo los actores durante combate.
- Protocolo vigente `mp-2026-10-25-v19-nation-ladder`; sustituye v18-nearest-duel. Solo BO5 online completo y confirmado puntúa; bots y tutorial no dan copas. Se conserva migración del historial previo sin recalcular sus ajustes, con reglas antiguas para el prefijo histórico y nuevas para resultados posteriores.
- El recorrido conserva sus 84 archivos integrados y la corrección de historial. Los 263 casos EditMode únicos de la tanda inicial están resueltos combinando sus 260 aprobados y la repetición de NationTrophyLadderTests, 22/22; no hubo una nueva corrida global 263/263. El lote compartido de repetición terminó 72/79 por seis fallos del fixture de eje de Kazuru y una expectativa histórica 20/10 de Tauruk; sus reparaciones están integradas y el retest de 27 casos de Kazuru sigue pendiente. PlayMode del 25-10-2026 a las 14:41:44 UTC: 38/44, con NationArenaScenery 2/2, NationThemeCompatibility 12/12 y LobbyRedesign 12/12 aprobados. Header quedó 11/15 y selector 1/3; las seis reparaciones de fixtures están integradas, pendientes de repetir. XML conservados en `nation-combined-integration-20261025/results`.
- Encabezado y salto de Kazuru integrados: nombre guardado en el recuadro y una sola cantidad de copas; salto de cuerpo completo con arte actual, misma velocidad, reservas y tiempos de combate. La integración final `nation-trophy-final-integration-20261025/manifest.json` añade 23 archivos: 17 de trofeos/UI, dos del helper nativo y cuatro reparaciones de fixtures. Compilación offline: cinco asambleas Editor y dos asambleas runtime AndroidRelease aprobadas; esto no es una build APK ni ejecución en Android. Repeticiones enfocadas y revisión visual final pendientes.
- Los cuatro trofeos V2 están APROBADOS e integrados: césped, agua con peces, roca y roca con lava. Sus 17 archivos de importación/UI forman parte del paquete final de 23 archivos. Las propuestas V1 no se importan. Son variantes visuales del tramo para una única cantidad de copas, no cuatro monedas o contadores. La comprobación visual final en Unity sigue pendiente.
- Martio continúa como trabajo paralelo en su propio chat: el creador ordenó «YA PONLO EN EL JUEGOOO». Se observaron cambios externos en producción y su integración, registro y pruebas están en curso. Este cierre no atribuye una integración terminada ni valida ese trabajo; tampoco afirma que siga fuera del juego. Los 20 personajes de Eronya NO están implementados ni asociados al roster por inferencia. El módulo 9 del tutorial sigue pausado y el 10 pendiente. No se completan por esto las 12 etapas del roadmap.

No se contabilizan los reintentos como pruebas nuevas ni se presenta el roadmap completo. El cierre anterior no acredita los paquetes de esta última sección.

# QA y evidencia

Pruebas base iniciadas con Unity 6000.3.9f1 en batchmode, antes de editar runtime.
Resultados/logs: `Logs/roadmap-v1.2/baseline-edit.xml` y `baseline-edit.log`.
Resultado base: **941 pruebas, 940 aprobadas, 1 fallida**. El fallo fue `RosieTests.ImmunityBlocksControl...`; la prueba del otro chat situaba un objetivo dentro del aura inicial. Su corrección pertenece al trabajo concurrente de Rosie.

Tras banco/Tokoro/apertura/BO5: **955/955 EditMode aprobadas**, informe `Logs/roadmap-v1.2/phase1-edit.xml`.

La ejecución completa PlayMode `phase1-play.log` se interrumpió: una prueba anterior usa `WaitForEndOfFrame`, que Unity no ejecuta en batchmode. No hay resultado XML completo y no se considera aprobada. Nueva ejecución de lógica y pruebas enfocadas tras MP-DEC-023 en curso.

MP-DEC-023, primera ejecución EditMode: **974/976** (`phase1-health-edit2.xml`). Todas las pruebas nuevas de HP pasaron. Dos pruebas existentes detectaron una regresión de la reacción de conitos frente a un segundo stun más corto, introducida por el cambio paralelo de estados. Se preservó esa reacción sin acumular duración de control; pendiente repetir la batería.

PlayMode enfocado: **20/21** (`phase1-health-play.xml`). Las tres pruebas de recuento/victoria/derrota y las de bases/ambientes pasaron. La prueba de proyectil de Kayon asumía ataque normal a los 0,1 s; ahora comprueba la espera inicial aprobada antes del disparo.

La ejecución `phase1-rendered-play.log` tampoco terminó: al mantener Unity oculto no se completa la espera de frame de Game view. Se adaptaron únicamente esas esperas de seis archivos de tests para batchmode. Las capturas antiguas con ScreenCapture requieren Game view visible; no se asume que se hayan generado en batch. Las nuevas capturas del roadmap usan cámara/Canvas reales en RenderTexture de 540 × 960 y escriben PNG explícitamente. Batería completa posterior: `phase1-all-play.xml`, en curso.

Verificación final de lógica de fase 1: **976/976 aprobadas**, `phase1-final-edit.xml`.
Recuento, victoria, derrota y Kayon: **4/4 PlayMode aprobadas**, `phase1-capture-play.xml`; PNG generados. La primera captura explícita se revisó y se corrigió el orden del Canvas para reproducir la prioridad Overlay sobre sprites; pendiente captura posterior.

La ejecución global `phase1-all-play` se detuvo sin XML completo, pese a corregir WaitForEndOfFrame. Se añadió registro de inicio/fin por caso (`*.log.progress.txt`) antes de reintentarlo. No se declara pasada esa batería.

Etapa 2: pruebas de perfiles/cartera más la batería completa EditMode: **986/986 aprobadas**, `phase2-profile-edit.xml`.

Capturas con el orden de Canvas corregido: **3/3 PlayMode aprobadas**, `phase1-final-captures.xml`. La comparación de HP se revisó visualmente. La captura de victoria se tomó antes de finalizar la entrada de página; se añadió una espera de 0,3 s de tiempo real en el test, manteniendo detenido el reloj de partida.

La batería `phase1-all-play-traced` se detuvo sin XML completo. Su registro por caso localiza el bloqueo en `CharacterPreviewPlayTests.DisabledPresentationKeepsLobbyBriefAndEveryPreviewIdleAfterTimeAndTaps`. También identifica dos expectativas antiguas de fórmula de Tokoro en `AbilityDescriptionPlayTests`; requieren contraste con los parámetros actuales. No se considera validada toda la batería PlayMode.

MP-DEC-024: nueva presentación de números verdes, corte diagonal y aumento/reducción al entrar en la barra. **988/988 EditMode aprobadas**, `phase1-diagonal-edit.xml`; incluye cobertura geométrica y UV de las mitades. Capturas anteriores de división textual quedan sustituidas como diseño, no como evidencia de los totales.

Primera ejecución visual nueva (`phase1-diagonal-play.xml`): victoria y derrota aprobadas. El test de HP conservaba una referencia a la etiqueta retirada al pausar; ahora consulta la etiqueta reconstruida. Las dos pruebas de descripción de Tokoro se actualizaron a la configuración existente: 10 fijo por defecto, ×1 con override -1, 17 con override explícito. No se cambió su daño ni se añadió un multiplicador de ×2. Revalidación: `phase1-diagonal-play-final`, en curso.

Revalidación terminada: **5/5 PlayMode aprobadas**, `phase1-diagonal-play-final.xml`. Capturas de corte, aparición de 6.5, aumento y reducción revisadas. Posteriormente el creador corrigió ubicación, borde, color enemigo y vuelo como orbe (MP-DEC-026); estas capturas documentan la iteración anterior y no validan todavía la nueva.

Prueba aislada de retratos (`phase1-preview-isolated.xml`): termina y señala que Home tiene un manifest de arte propio; la expectativa antigua cargaba siempre el idle de colección. Se corrigió el test para comprobar el recurso de su manifest efectivo y conservar la verificación de pose inmóvil tras tiempo/toques. No se cambió arte ni animación.

Primera regresión de perfiles v2: **980/993 EditMode**, `phase2-starter-orbs-edit.xml`. Detectó la representación de clases nulas de JsonUtility al recargar una elección pendiente y fixtures que aún usaban el catálogo antiguo reducido. Se corrigió el formato a cero/una selección, se mantienen pruebas de migración v1 real y se valida también el catálogo que carga el juego. Reejecución `phase2-starter-orbs-edit-final`, en curso.

Regresión v2 corregida: **994/994 EditMode aprobadas**, `phase2-starter-orbs-edit-final.xml`. Incluye ambas elecciones iniciales, catálogo real, reintentos, rechazo de cambio de recibo, corrupción y migración v1 con archivo/respaldo conservados.

Primera validación de números encima, contorno negro y orbes (`phase1-overhead-orbs-play.xml`): **9/11 PlayMode aprobadas**. Pasaron los cuatro casos de resultados/recuento/colores relativos y las dos descripciones. Dos fallos de colección: el test abría la pestaña inicial Monsters y esperaba Whelps, y los botones Detalle/Usar no alcanzaban 44 píxeles a 360 px de ancho. Se corrigieron la navegación explícita del test y la altura de esos botones; pendiente revalidación.

MP-DEC-027/028: cobertura de desvanecimiento en varios instantes, ausencia de decimales, redondeo por unidad (incluido HP 1), suma y ganador coherentes. **998/998 EditMode aprobadas**, `phase1-ceil-fade-edit.xml`. **9/9 PlayMode aprobadas**, `phase1-ceil-fade-play.xml`: resultados/recuento y colección completa, incluidos los dos fallos corregidos. Capturas `health-half-emerges.png` y `health-orbs-flight.png` revisadas: 13 → 7, 101 → 51, piezas que se desvanecen, números encima, borde negro, orbes por equipo y número superior fuera de la barra enemiga.

Regresión PlayMode global `phase1-all-play-ceil` interrumpida sin XML completo. El registro por caso señala tres fallos: escala de ataque de Anuik esperada por una prueba antigua, proporción de ampliación de tablero y posición del carrito esperadas por dos pruebas de casino antiguas. No se cambió arte, animación ni cámara para satisfacer esas expectativas.

La prueba de Anuik ahora conserva el gesto corporal documentado el 15 de octubre y exige amplitud limitada, ancla de combate inmóvil y regreso a escala uniforme al terminar. En la ejecución aislada `phase1-scene-load-isolation2` los dos casos de Anuik y los de CasinoHud pasan antes del bloqueo; ese registro parcial no sustituye un XML completo. El primer intento de aislamiento tuvo un error de acceso a una API interna en el propio test, corregido usando el avance real de la escena.

El bloqueo se reproduce al cargar la escena de `CharacterPreviewPlayTests` inmediatamente después de `CasinoHudPlayTests.OpponentNameReadyAnimationAndCountdownAreNonBlocking`. La misma clase de colección pasa completa en `phase1-ceil-fade-play`. Se añadió cierre/limpieza con un frame de espera al finalizar cada prueba de CasinoHud; **hipótesis de aislamiento pendiente de verificar**, no se declara resuelto. La reejecución espera que termine una instancia concurrente de Unity del otro trabajo de alcance.

Revalidación del aislamiento: **22/22 PlayMode aprobadas**, `phase1-scene-load-isolation3.xml`. La secuencia de Anuik → CasinoHud → colección completa termina al permitir que se procese la destrucción diferida de UI antes de cargar otra escena. Incluye el test de Anuik actualizado y todos los retratos. La reejecución global `phase1-all-play-ceil2` está en curso; los dos fallos de encuadre del test de tablero siguen pendientes de contraste con el layout vigente.

La batería global terminó: **127/160 aprobadas, 33 fallidas**, `phase1-all-play-ceil2.xml`. Incluye dos timeouts de 180 s que requieren repetición aislada. Pasaron los cuatro casos del recuento/resultados; no se declara validada toda la fase por ese resultado.

Tras MP-DEC-029 se retiraron cuatro fixtures exclusivos de gestos antiguos del menú, conservados en `retired-tests/2026-10-20-lobby-gestures` con hashes. Los tests activos ya no activan `PlaybackEnabled` ni exigen atlas antiguos para revisar menús. La cobertura de retratos estáticos de todos los personajes sigue activa.

Se actualizaron pruebas aplicables para el encuadre vigente, el tinte de reposo real, las descripciones configuradas, el catálogo actual y la espera inicial de combate. No se cambiaron cámara, sprites, animaciones, daño ni escudos para satisfacerlas. `LobbyFrameGraphic.Configure` carga por defecto solo el retrato estático. Revalidación terminada: **998/998 EditMode aprobadas**, `phase1-current-behavior-edit.xml`.

`phase1-current-behavior-play.xml`: **23/29 aprobadas**. Pasaron todos los casos del desempate, retratos actuales y Rosie. Los seis fallos restantes identificaron expectativas de sombras antiguas, altura de reposo de Anuik, preparación de habilidades, balance de Suki/Pokrak y prefijo de nombres de iconos.

`phase1-current-behavior-play2.xml`: **19/19 aprobadas**, incluidos esos seis casos corregidos, persistencia de los tres mapas y selección independiente del lobby. Las pruebas de habilidades consultan su configuración actual, esperan su preparación y siguen comprobando destinatarios, escudos, pausa y caducidad. No se modificó el balance. Los botones de aplicar estilo pasaron de 56 a 68 unidades de alto: superan 44 px a 360 px de ancho y siguen dentro de su tarjeta. La regresión general aún no se declara completa.

`phase1-current-navigation-play.xml`: **13/16 aprobadas**. La navegación de inicio por BATALLA → BOT, selección de mapa online, botón LISTO cancelable, traducción de la ficha rival y reconstrucción de fondos quedaron verificadas. Los dos casos que antes agotaban el tiempo terminaron con errores identificables: un test buscaba el nombre anterior del efecto de Tsumi y otro exigía «SALA» incluso con el juego en inglés. El tercer fallo esperaba «POPOW» en la ficha, aunque su nombre actual es Pombú; la selección del personaje sí era correcta. Se actualizaron estas tres expectativas, pendientes de la ejecución global.

MP-DEC-030: **966/966 EditMode aprobadas**, `phase1-current-names-edit.xml`. La diferencia respecto a 998 es explícita: se retiraron 41 casos exclusivos de gestos descartados y se añadieron 9 sobre nombres actuales y conservación del ID. Las pruebas de movimiento del tablero permanecen activas. Fuentes/metadatos archivados verificados por SHA-256: 12 archivos, cero diferencias. Regresión PlayMode completa posterior: `phase1-current-global-play`, en curso.

La regresión global actual terminó: **149/150 PlayMode aprobadas**, `phase1-current-global-play.xml`. Pasaron los 22 personajes en ambos idiomas, la selección de fichas, los cuatro casos del recuento/resultados y los 23 fallos anteriores que conservaban cobertura activa. El único fallo nuevo fue el margen inferior de los botones de estilos: la ampliación táctil dejaba 10 unidades en lugar de 12. Se desplazaron 2 unidades hacia arriba manteniendo su altura de 68; revalidación enfocada pendiente.

Se encontró además que Progreso imprimía los IDs de sus desbloqueos. Ahora resuelve el nombre actual del personaje o el DisplayName configurado de la recompensa, sin modificar los IDs almacenados. Prueba de integración incluida en `phase1-current-names-final-play`.

Revalidación de cierre: **8/8 PlayMode aprobadas**, `phase1-current-names-final-play.xml`. El margen corregido y el área táctil pasan juntos; también pasa la pantalla de Progreso con nombres actuales y los mismos IDs guardados. No se reetiqueta el informe global anterior: mantiene su resultado original 149/150.

Etapa 2, nuevo trabajo: `LocalProgressStore` y su integración con resultados BO5, recibos completos, importación de PlayerPrefs, escritura atómica y respaldo de solo lectura ante corrupción. Pruebas de persistencia, concurrencia, versiones futuras, 260 partidos y UI de error preparadas. Validación pendiente; no afirmar aún que el guardado nuevo pasó la batería.

Primera regresión de guardado: **987/993 EditMode**, `phase2-progress-storage-edit.xml`. Las **16 pruebas nuevas de progreso pasaron**. Los seis fallos pertenecen a integración concurrente: cuatro expectativas fijadas al catálogo anterior y dos casos de Tsumi cuyo aliado tiene daño base cero. La comparación de mejoras recibidas descartaba el regalo porcentual por tener aporte inmediato cero; se conservó la mejora para que también afecte al daño innato ganado después. Nueva prueba de daño efectivo añadida; no cambian porcentajes ni acumulación. Los tests de catálogo y del inspector leen ahora los personajes y nombres actuales.

Se añadió reintento de resultados pendientes en memoria después de volver al menú, cubierto por una prueba de BO5 con el archivo bloqueado temporalmente. Regresiones completas `phase2-progress-storage-final-edit` y `phase2-progress-storage-global-play` pendientes.

Regresión de lógica terminada: **994/994 EditMode aprobadas**, `phase2-progress-storage-final-edit.xml`. Incluye el catálogo actual de 23 personajes, la mejora de Tsumi sobre un receptor con daño base cero y la conservación de recibos de progreso.

La regresión global PlayMode terminó: **156/157 aprobadas**, `phase2-progress-storage-global-play.xml`. Pasaron combate, recuento/resultados, las fichas de los 23 personajes en ambos idiomas, guardado de BO5 con recarga sin premio duplicado, lectura del respaldo y reintento tras liberar el archivo bloqueado. El único fallo fue `LocalProgressionPlayTests.CorruptionWithoutBackupShowsAnErrorInsteadOfInventingEmptyProgress`: el error de JSON se relanzaba como `InvalidDataException` y escapaba de `LocalGameUI.LoadProgression` porque esa excepción no hereda de `IOException`.

Se amplió el manejo de corrupción en Progreso y en ambos almacenes, sin interceptar ni degradar versiones futuras. Se añadieron dos regresiones para un archivo JSON válido con checksum incorrecto: recuperar solo lectura, rechazar nuevas concesiones y conservar el principal dañado. El cierre enfocado pasó **40/40 EditMode** y **10/10 PlayMode** el 20 de octubre a las 14:55 y 14:56 (Bolivia), informes `Logs/roadmap-v1.2/phase2-recovery-focused-edit.xml` y `Logs/roadmap-v1.2/phase2-recovery-focused-play.xml`. Incluye ambas regresiones de checksum, los cuatro casos de Progreso, sus nombres visibles y los cuatro casos de recuento/resultados. Los informes globales conservan sus recuentos originales; **156/157 no se reescribe como 157/157**.

Casos de aceptación de la primera fase:

- Banco: guardar/retirar gratis; saldo intacto con cero monedas; rechazo atómico de banco lleno, casilla ocupada y fuera de preparación.
- Banco: copias, mejoras y Paid conservados; ofertas del tipo guardado ocultas; rerolls no lo reintroducen; venta conserva fórmula.
- Tokoro: curación 60/s durante 3 s, limitada por vida máxima; sin energía de daño/tiempo/aliados ni ataques; recuperación normal después; no dormir al final; variantes editadas y ambos lados.
- Apertura: los efectos iniciales resuelven antes de ataques normales; el reloj continúa; efecto de 3 s no bloquea al resto; orden canónico conservado.
- BO5: texto VICTORYYY/YOU LOSE solo al terminar encuentro; UI no cambia ganador ni entrega recompensas.
- HP: congelación antes de capturar; Monster vivo con HP actual completo; Whelps vivos al 50 % actual redondeado hacia arriba por unidad; muertos y cualquier invocación excluidos; sin decimales; ambas barras en paralelo; corte diagonal con desvanecimiento gradual, aparición del aporte y vuelo como orbe; pausa y siguiente ronda; banderas después del recuento.
- Regresión: pruebas existentes, EditMode y PlayMode; documentar fallos anteriores separadamente.

Pendiente manual: Android físico, dos clientes Relay reales, reconexión, nitidez y defectos visuales de todos los mapas.
No confundir PlayMode batch con una prueba de usuario o de dispositivo.

## Ampliación de datos posterior al cierre de recuperación

Integrados tres cambios independientes: inventario QA v3, mazos locales versionados y validador de referencias en Editor. La regresión global terminó **1026/1028 EditMode**, informe `phase2-inventory-decks-catalogue-edit.xml`. Los dos fallos fueron `CatalogueValidationTests.LegacyConfigIsValidatedWithoutReplacingItsExistingRules` y `MissingAbilityDefinitionAndGeneralMetadataDoNotCrashTheReport`. Unity materializa clases inline nulas como objetos vacíos al inspeccionarlas; el validador se ajustó para reconocer únicamente slots opcionales vacíos del formato anterior y conservar el diagnóstico de definición ausente. No se cambió contenido para satisfacer las pruebas.

- `LocalInventoryTests`: IDs reales, nombres que cambian sin alterar propiedad, concesión única entre procedencias, recibos en conflicto, personaje requerido, QA deshabilitado, escritores concurrentes, respaldo de solo lectura y migración v1/v2 sin alterar el original.
- `LocalDeckStoreTests`: importación exacta de los tres mazos existentes, revisión/conflicto, reintentos sin rotación innecesaria de respaldo, principal ausente/JSON/checksum inválido, legado dañado, catálogo temporalmente incompleto, versiones futuras y escritor bloqueado.
- `LocalDeckStoragePlayTests`: integración de selección, reinicio, mensajes de error y reintento; los directorios de prueba están aislados de los mazos reales.
- `CatalogueValidationTests`: nueve casos para catálogo actual, conservación exacta de datos/dirty flags, IDs repetidos con nombres visibles, referencias ausentes, habilidades ajenas/en otro espacio, entradas incompletas, parámetros inválidos y compatibilidad con la configuración anterior. La nueva regresión exige ID a un slot anterior parcialmente configurado, de modo que no se esconda detrás de la excepción para placeholders vacíos.

La regresión de escenas posterior pasó **25/25 PlayMode**, informe `phase2-decks-progress-play.xml`: tres casos de mazos, cinco de colección, cuatro September, cuatro Progreso, dos rediseño, cuatro resultados y tres perspectiva online. Esta perspectiva automatizada no equivale a dos clientes reales de Relay.

Regresión global posterior terminada: **1053/1053 EditMode aprobadas**, `Logs/roadmap-v1.2/phase2-data-final-edit.xml`. Incluye los nueve casos del validador corregido, cinco de aislamiento simétrico de monedas, 19 de identidad invitada y las regresiones de perfil/inventario/mazos. El global anterior 1026/1028 conserva su resultado.

Después de esa global se conectó la carga de identidad invitada al inicio y se amplió el aislamiento de sus directorios en tests. Cierre específico: **19/19 EditMode**, `Logs/roadmap-v1.2/phase2-guest-hook-edit.xml`, y **9/9 PlayMode**, `Logs/roadmap-v1.2/phase2-guest-hook-play.xml`. Incluye los 19 casos de `LocalGuestProfileTests` y nueve PlayMode (dos identidad invitada, tres mazos y cuatro Progreso). Ese hook solo conserva GUID/marca de legado; no configura la propiedad jugable.

El primer intento del cierre de 19 casos no produjo XML: Unity seguía en Play Mode y el runner no pudo crear su escena. Se verificó menú/ronda cero, se salió de Play y se reintentó. Es una incidencia del entorno de ejecución, no una prueba de producto aprobada ni un reemplazo de la global 1053/1053.

No usar el cierre anterior de recuperación (40/40 EditMode y 10/10 PlayMode) como evidencia de estas ampliaciones posteriores. No se desbloquea contenido del jugador ni se infieren sus habilidades iniciales o propiedad antigua mediante estas pruebas.
Nota de alcance tras el cierre: a partir de las 15:23 del 20 de octubre se observaron cambios concurrentes de Kazuru en su setup, preview, pruebas y escala del retrato en Home. El diff de local-game-menu.cs conserva íntegra la integración de mazos y añade únicamente el ajuste del retrato (además del BOM). Esos cambios posteriores de presentación no forman parte de los XML de esta tanda; se preservan para su validación por el trabajo correspondiente. Los resultados anteriores identifican la versión probada y no se reinterpretan como una ejecución posterior a esos cambios.

## MP-DEC-031: entrada de cuentas nuevas y primer contacto

El informe `Logs/roadmap-v1.2/starter-selection-edit.xml` (**43/43 EditMode**) corresponde al selector anterior a MP-DEC-031. Se conserva como evidencia histórica y no valida el flujo corregido de elección seguida de tutorial. No se usa un informe PlayMode anterior sin archivo conservado y verificado como evidencia de este selector.

Alcance de la nueva verificación:

- Invitado nuevo: elegir Bugrum o Pombú y entrar a Primer contacto, manteniendo IDs, nombres actuales y los siete Whelps. Recargar o reintentar no repite la aplicación del mazo.
- Perfil v1, con o sin recibo: migrar a v2 con estado `None` sin modificar su archivo al leerlo, ni mostrar elección/tutorial al abrir el juego. Conservar también perfiles con datos de legado, mazos personalizados y conflictos entre sesiones.
- Mazos v2: aplicación y marcador atómicos, revisión exigida, reintentos idempotentes y tres espacios conservados; ninguna concesión de propiedad, habilidades o monedas.
- Práctica real: toque para colocar, pulsación sostenida para inspeccionar y arrastre a una casilla distinta legal. Una casilla rechazada, un gesto estacionario o abrir información por otra vía no completan el objetivo correspondiente.
- Entrenamiento: sin rival en campo, reloj ni combate; Ready y compras deshabilitados, estadísticas normales intactas. No genera recibo BO5 ni XP; salir y volver a una partida normal recupera su comportamiento habitual.
- Interrupción y reanudación: el módulo incompleto reinicia sus ejercicios; un módulo confirmado no duplica el recibo. El fallo de guardado conserva datos anteriores y ofrece reintento. Completar Primer contacto no completa el tutorial global ni habilita los módulos 2–10.

Cierre de MP-DEC-031: **1090/1090 EditMode aprobadas** en `Logs/roadmap-v1.2/new-account-tutorial-edit.xml` (2026-10-20, 20:21:28–20:22:25 UTC) y **19/19 PlayMode aprobadas** en `Logs/roadmap-v1.2/new-account-tutorial-play.xml` (20:23:54–20:25:18 UTC), con cero fallos en ambos informes.

Las 19 PlayMode incluyen siete casos de selección inicial, tres de Primer contacto, dos de identidad invitada, tres de mazos y cuatro de Progreso. Este cierre verifica el flujo corregido y sus regresiones; no reetiqueta los resultados anteriores.

Ese cierre histórico cubrió solo el primer módulo. Su ampliación posterior se registra debajo. Las pruebas automatizadas no sustituyen QA manual en dispositivo ni autorización comercial.

## Continuación del tutorial y tres Whelps iniciales

La ampliación de ejercicios 2–6 pasó **1117/1117 EditMode**, `Logs/roadmap-v1.2/tutorial-modules-edit.xml` (20:54:22–20:54:57 UTC). La primera tanda de escenas terminó **36/37**, `tutorial-modules-play.xml` (20:59:42–21:02:04 UTC). Su único fallo fue el ejercicio del banco: tanto el objetivo como su prueba exigían agotar el pool, aunque la regla vigente oculta sus ofertas y conserva copias pendientes. Se corrigieron objetivo y prueba, sin cambiar el banco real. Ese informe se conserva con su resultado original.

Después se integraron MP-DEC-033/034: tres iniciales, colección invitada v4, Brief automático de hasta siete y comprobación de disponibilidad al entrar a bot/salas. La regresión global pasó **1140/1140 EditMode**, `Logs/roadmap-v1.2/starter-three-tutorial-edit.xml` (21:24:25–21:25:57 UTC), sin fallos. Incluye 15 casos nuevos de colección y ocho de disponibilidad de mazos, además de migraciones, recibos históricos y toda la lógica previa.

La tanda PlayMode de cierre pasó **43/43**, `Logs/roadmap-v1.2/starter-three-tutorial-play.xml` (21:29:13–21:31:42 UTC), sin fallos. Verifica elección de tres y nombres actuales, módulos 1–6, banco gratis, 3→7→8 desbloqueos, sustitución voluntaria, recarga, conflicto de mazos, identidad ilegible y validación local de equipo online sin usar la red. El caso bancario que falló en la tanda anterior pasó después de corregir el objetivo, conservando el pool y las reglas reales.

Se inspeccionaron `starter-selection.png` y `tutorial-tactical-bank-open.png` de esta ejecución: fila de tres centrada con nombres actuales, instrucciones legibles y banco real abierto con saldo cero. También se generaron capturas de posicionamiento y preparación. Son comprobaciones en Editor; Android físico y dos clientes Relay siguen pendientes.

## Colección bloqueada y motor técnico de máquinas

La primera ejecución de máquinas terminó **30/31 EditMode**, `Logs/roadmap-v1.2/machine-engine-simulation-edit.xml` (21:41:27–22:09:54 UTC). Pasaron los treinta casos de reglas. La simulación completó las 100.000 máquinas con 1.210.941 extracciones, 70.323 primeras mejoras garantizadas y 40.000 adquisiciones externas intermedias; falló por exceder su límite de 300.000 ms, con 1.705.781 ms medidos. Se conserva como fallo de tiempo, no como una batería aprobada. El intento previsto de reiniciar el Editor se canceló al detectar el XML recién terminado: no se cerró ni reinició Unity.

Se preparó un contexto con catálogo/política privados validados una sola vez, conservando validación por llamada en las entradas mutables. La simulación ahora cede el control cada 250 máquinas y escribe cinco avances, sin reducir las 100.000 máquinas, diez escenarios, semilla 20261020 o invariantes. Mantiene un límite explícito de quince minutos. Las treinta pruebas originales no se retiraron.

Verificación independiente previa a Unity: **47/47 casos**, con NUnit real del proyecto y un adaptador `System.Text.Json` para las comparaciones serializadas fuera del bucle. Las 100.000 máquinas produjeron exactamente los mismos recuentos en 76,8 s. El informe y hashes están en el workspace, `machine-prepared-tests-staging/verification/standalone-results.json`. Esta ejecución no se presenta como una prueba de Unity, del backend ni de economía comercial.

La ampliación integrada de colección añade ocho casos EditMode y tres PlayMode de Monsters, más comprobaciones de candado cerrado, sombra y desaparición de ambos al desbloquear Whelps/Monsters. Los resultados de Unity y la inspección de sus capturas se registran después de ejecutar esta versión; los 1140/1140 y 43/43 anteriores no validan estos cambios posteriores.

Cierre de escenas de MP-DEC-035: **46/46 PlayMode**, `Logs/roadmap-v1.2/locked-collection-tutorial-play.xml` (22:16:26–22:19:10 UTC), sin fallos. Incluye los 16 casos del selector/colección, módulos 1–6 y regresiones de identidad, mazos, progreso e intercambio. Se inspeccionaron `collection-locked-monsters.png` y `collection-locked-whelps.png`: personajes bloqueados más oscuros con candado cerrado, los tres Whelps iniciales y Monster elegido visibles sin esa capa. Las pruebas también comprueban la desaparición de ambos indicadores al desbloquear, ficha consultable, Usar rechazado, elección voluntaria del nuevo Monster, legado conservado y rechazo local de BOT/sala sin propiedad. No se realizaron conexiones Relay reales.

El runner de Unity tiene un timeout predeterminado de tres minutos, confirmado en el paquete instalado. La simulación cooperativa usa `[Timeout(960000)]` para dejar actuar su guardia explícita de quince minutos; no se elimina el límite ni se reducen casos. Esta enmienda solo afecta al test y conserva sus invariantes y semilla.

La global posterior terminó **1195/1195 EditMode**, `Logs/roadmap-v1.2/locked-collection-machine-edit.xml` (22:23:04–22:31:43 UTC), sin fallos. Incluye la comprobación de Monsters, los treinta casos originales de máquinas, dieciséis casos del contexto preparado y las 100.000 simulaciones completas. El stress tardó **420.157 ms** y conservó exactamente 1.210.941 extracciones, 70.323 primeras mejoras garantizadas, 40.000 adquisiciones intermedias y las frecuencias por tiro del informe anterior. El Editor cedió control durante la ejecución. Esto cierra el fallo de tiempo de la tanda anterior sin reinterpretar su 30/31 histórico.

## Recibos de habilidades y transacción local QA

Después de esa global se integraron Guest v5 y el agregado de transacción QA; no cambió el motor ni la simulación. Antes de Unity pasaron 30/30 casos independientes de habilidades y 31/31 de transacción con NUnit real y adaptadores JSON. El informe inicial 29/29 de transacción se conserva separado; una revisión posterior añadió validación estructural antes de JsonUtility para impedir que Unity convierta propiedad nula en un objeto predeterminado. También se corrigieron encabezados omitidos y un intervalo de probabilidad cuyo punto medio redondeaba a uno. La validación integrada se registra a continuación sin atribuir estos cambios posteriores a la global 1195/1195.

La primera tanda integrada terminó **256/258 EditMode**, `Logs/roadmap-v1.2/guest-abilities-machine-transaction-edit.xml` (22:34:56–22:35:37 UTC). `MissingAbilityListInCurrentPayloadRequiresRecoveryInsteadOfInventingEmptyOwnership` detectó que JsonUtility materializaba como vacía la lista de habilidades ausente; `ReplayAcceptsAValidResourceInTheSmallestFinalProbabilityInterval` detectó pérdida de precisión al clonar una política mediante JSON de Unity. No eran cambios de diseño ni expectativas descartadas: se prepararon correcciones del guardado y de la copia/firma de configuración. El informe conserva ambos fallos.

La regresión de escenas posterior, anterior a esas dos reparaciones, pasó **46/46 PlayMode**, `Logs/roadmap-v1.2/guest-v5-collection-tutorial-play.xml` (2026-10-20, 22:39:41–22:42:17 UTC). Comprueba colección, tutorial e integración del perfil v5; no valida la reparación estructural posterior del lector.

Las dos reparaciones conservan el balance y las concesiones existentes. El lector valida arrays raíz por versión con Newtonsoft ya instalado, rechaza claves duplicadas y entrega a Unity nombres decodificados sin reinterpretar timestamps. La configuración QA usa copia manual y firma binaria con los bits exactos de probabilidades y pesos. Antes de Unity, las regresiones independientes pasaron **50/50** y **33/33** respectivamente; esos harnesses no sustituyen el serializador real de Unity.

El 22 de octubre se retomó la ejecución. La comprobación de integridad confirmó 33 archivos únicos de cuatro manifiestos sin discrepancias ni faltantes. Las copias de escenas existentes en `Assets/_Recovery` se preservaron también en el workspace (`unity-recovery-preserved-20261022`) al resolver el aviso de recuperación del Editor. La validación real posterior de las reparaciones se registra debajo cuando termine; no se reutilizan los resultados del día 20 como si fueran una ejecución nueva.

Cierre real de ambas reparaciones: **280/280 EditMode**, `Logs/roadmap-v1.2/guest-machine-serialization-fix-edit.xml` (2026-10-22, 19:48:05–19:48:38 UTC), cero fallos. Se ejecutaron los 17 fixtures afectados de perfil, colección, onboarding, mazos, entrenamiento y máquinas; incluye los 50 casos de habilidades y 33 de transacción QA con el JsonUtility real de Unity. Ambas regresiones que fallaron en 256/258 pasan. La simulación de 100.000 no se repitió porque su motor y prueba no cambiaron; conserva su evidencia propia anterior.

Regresión de escenas tras las reparaciones: **18/18 PlayMode**, `Logs/roadmap-v1.2/guest-v5-storage-final-play.xml` (2026-10-22, 19:51:31–19:52:50 UTC), cero fallos. Incluye 16 casos de selección inicial/colección y dos de identidad invitada. Confirma el lector corregido en el inicio y la UI afectada; no representa una nueva ejecución de los 46 casos históricos del tutorial ni una prueba en dispositivo Android.

## Copas y prueba de dos arenas — 22 de octubre de 2026

Se integraron 21 archivos de núcleo, catálogo, nombre Parque de Bolas y herramienta QA, incluidos pruebas y metadatos. En ese cierre, los manifiestos `ladder-board-integration-20261022/manifest.json` y `ladder-trial-integration-20261022/manifest.json` coincidían con los archivos de destino, sin faltantes ni discrepancias. La política de prueba aprobada empieza en 0, mantiene suelo 0 y usa `classic` 0–149 con +30/−5 y `royal` desde 150 con +30/−30; sin rachas ni diferencias según rival. La herramienta de Editor opera solo en memoria y no adjudica resultados de partidas reales.

Evidencia independiente previa: **74/74** casos del núcleo y **6/6** comprobaciones de la factory. La tanda posterior en Unity pasó **91/91 EditMode**, sin fallos ni omitidos y salida 0, entre las **20:25:12 y 20:25:14 UTC**. Informe: `Logs/roadmap-v1.2/ladder-trial-edit-20261022.xml`, con copia de trabajo en `ladder-board-integration-20261022/results/`. Reparto: `TrophyLadderEngineTests` 74/74, `EnvironmentProgressionCatalogueTests` 10/10 y `TrophyLadderTrialTests` 7/7. Comprueba reglas, recibos y límites, catálogo real, exclusión del Hotel y configuración 0/150. Batchmode no constituye una inspección visual del panel ni una prueba online.

La regresión posterior pasó **3/3 PlayMode**, sin fallos ni omitidos y salida 0, entre las **20:29:59 y 20:31:00 UTC** del 22 de octubre. Informe: `Logs/roadmap-v1.2/ladder-boards-play-20261022.xml`, con copia de trabajo en `ladder-board-integration-20261022/results/`. Reparto: `TableSkinsPlayTests` 2/2 y `LobbyThemeSelectionPlayTests` 1/1. Comprueba persistencia de los mapas, navegación con retratos estáticos y cambios de fondo/paleta del lobby sin alterar el mapa de combate. Ambos XML y logs de esta integración están copiados en el proyecto y verificados por SHA. Son aserciones sobre escenas reales en batchmode; no se capturaron ni inspeccionaron imágenes. Esta tanda precede al ajuste posterior de áreas táctiles y no lo valida.

La herramienta independiente `tools/ladder-pacing.mjs` reproduce el estudio de primera llegada y los escenarios de tiempo, con 27 comprobaciones aprobadas más sintaxis y límites máximos. Sus resultados son cálculos, no medidas de jugadores ni pruebas Unity; detalle en `LADDER_PACING.md`.

## Selector existente: áreas táctiles y escala — 22 de octubre de 2026

El paquete de ocho archivos `style-selector-responsive-integration-20261022/manifest.json` añade áreas táctiles mínimas de 44 píxeles y texto mínimo de 12 píxeles según la escala efectiva del Canvas. Conserva los rectángulos visuales originales, distribuye la expansión sin solapar los ocho botones y respeta los límites del panel. Las comprobaciones cubren siete tamaños verticales con márgenes seguros; el PlayMode comprueba además texto ES/EN, raycasts y recuperación de tamaño. Son comprobaciones del selector existente, independientes del diseño visual de TABLEROS aprobado posteriormente y de su futura implementación nativa.

La primera ejecución EditMode se interrumpió durante compilación sin XML ni resultado de tests; se conserva `Logs/style-selector-responsive-edit-20261022.log` en el workspace. El reintento pasó **8/8 EditMode**, cero fallos y omitidos, salida 0, a las **20:41:20 UTC**. Informe: `style-selector-responsive-integration-20261022/results/style-selector-responsive-edit-20261022-retry1.xml`. Incluye siete escalas y la regresión de la antigua suposición de ancho completo, que ocultaba que un botón de 68 unidades se reducía a 36,27 píxeles en la pantalla menor. La evidencia independiente anterior de ocho casos permanece separada.

La primera tanda posterior terminó **5/6 PlayMode**, cero omitidos, salida 2, entre las **20:42:52 y 20:43:51 UTC**. Informe: `style-selector-responsive-integration-20261022/results/style-selector-responsive-play-20261022.xml`. Falló únicamente `CompactSelectorExpandedEdgesReceiveRealPointerRaycastsWithoutBlockingNeighbors`: la GameView arrancó a 640×480 y la guardia exigía al menos 512 píxeles de alto, antes de comprobar raycasts o capturas. Pasaron las otras dos pruebas nuevas y las tres regresiones existentes. El fallo se conserva; no equivale a seis casos aprobados.

El ajuste de la fixture usa el helper de previsualización existente para solicitar GameView 360×640, espera el tamaño real y restaura la selección anterior al terminar. No reduce el mínimo exigido ni modifica el comportamiento del selector. El siguiente intento no generó XML: se abortó por un error de compilación de un test concurrente de otro chat, `kazuru-burst-tauris-growth-tests.cs`, al tratar el retorno void de `StartMatch` como booleano. Su log se preserva; no se clasifica como resultado de tests de este paquete. La repetición se realizó después de reparar y liberar ese entorno compartido.

La revisión `style-selector-responsive-integration-20261022/pre-retry2-integrity.json` conserva la diferencia legítima posterior de `GameText.cs`: de 28 archivos revisados, 27 coincidían y uno cambió en el trabajo concurrente de Kazuru/Tauris. El diff revisado solo modifica dos descripciones de habilidades y conserva Parque de Bolas / BALL PIT. No se restableció el archivo ni se ocultó esa diferencia.

Cierre posterior: **6/6 PlayMode**, cero fallos y omitidos, salida 0, entre las **21:04:31 y 21:05:28 UTC** del 22 de octubre. Informe: `style-selector-responsive-integration-20261022/results/style-selector-responsive-play-20261022-retry2.xml`. Reparto: `StyleSelectorResponsivePlayTests` 3/3, `TableSkinsPlayTests` 2/2 y `LobbyThemeSelectionPlayTests` 1/1. Se comprobaron los siete tamaños verticales con márgenes, texto ES/EN, raycasts reales en centros y bordes de los ocho botones, y restauración al cambiar escala. Las tres regresiones del cierre anterior se repiten aquí: no se suman como nueve casos diferentes.

Se inspeccionaron las capturas reales Unity `Logs/roadmap-v1.2/style-selector-compact-es.png` y `style-selector-compact-en.png`, ambas de 360×640: texto y cabeceras del selector caben sin recortes. Los límites táctiles de 44 píxeles se comprueban con geometría y raycasts, no se deducen de la imagen. `style-selector-responsive-integration-20261022/evidence-copy.json` verifica tres XML, cinco logs —incluidos los intentos anteriores— y dos PNG congelados en `screenshots/`; los XML/logs también están en `Logs/roadmap-v1.2` del proyecto. Esta tanda no valida el futuro diseño de TABLEROS ni una ejecución en hardware móvil.

## Orientación móvil

La auditoría de fuentes y configuración `mobile-orientation-audit-20261022/evidence.json` confirmó el requisito aclarado por el creador: vertical normal fijo en móvil, sin horizontal ni boca abajo. `defaultScreenOrientation=0` se contrastó con el enum Portrait de la DLL local; las tres orientaciones alternativas están desactivadas. Android lo refuerza en ejecución y build. iOS mantiene ese mismo ajuste y pantalla completa, sin overrides hallados en fuentes. Escritorio conserva ventana redimensionable. Se verificaron seis hashes, sin cambios ni ejecución de Unity; SHA256 del informe: `92C3570B92BCE071A408162E31D3E160151098A265C1FA4C6020D337E7717B1B`. Esta auditoría no sustituye pruebas de builds ni dispositivos físicos.

## Presentación nativa de TABLEROS — V1 validada en su alcance; fase abierta

Se integraron diez archivos en `board-presentation-integration-20261022`: pantalla nativa con texto dinámico, modelo explícito de presentación, enlace con la ventana QA y pruebas. La vista usa las fuentes y miniworlds existentes, cifra «+150» y slots de icono; el trofeo generado original no está aprobado ni importado. Los escenarios de selección indefinidos siguen bloqueados en la herramienta QA, sin convertir esa guardia en regla del producto ni escribir perfiles o preferencias del jugador.

La comprobación independiente de geometría pasó nueve casos con el código real y bibliotecas administradas de Unity, fuera del Editor. No certifica importación, render ni PlayMode. La primera ejecución integrada abortó al importar dos archivos `.meta` de tests cuyo valor de `guid:` estaba en una línea separada; esas fixtures no se importaron correctamente y no hubo resultado válido que atribuir a sus pruebas. Se conserva el log del intento. La reparación `board-presentation-meta-fix-integration-20261022/manifest.json` cambia solo el formato de esas dos metas, preserva ambos GUIDs y pasó dos comprobaciones estructurales independientes. Compilar C# no se presenta como aprobación de los metadatos.

La nueva tanda real pasó **9/9 EditMode**, cero fallos y omitidos, salida 0, a las **21:26:38 UTC** del 22 de octubre. Informe: `board-presentation-integration-20261022/results/board-presentation-edit-20261022-retry1.xml`. Valida la geometría de área segura del código integrado. No se atribuye ese resultado a los iconos de trofeo, que permanecen fuera de Assets.

V1 pasó después **3/3 PlayMode**, cero fallos y omitidos, salida 0, entre las **21:29:49 y 21:30:08 UTC**. Informe: `board-presentation-integration-20261022/results/board-presentation-play-20261022.xml`; logs en `Logs/board-presentation*.log` del workspace. Comprueba 58 escenarios del recorrido —siete tamaños, ES/EN y copas 0/149/150/100000, más long.MaxValue en móvil ES/EN—, cuatro escenarios del selector y seis capturas nativas. Estas son tres pruebas que contienen esos escenarios; no se presentan como 62 pruebas NUnit distintas.

Las seis capturas de `Logs/roadmap-v1.2/board-presentation-20261022-213007` del proyecto se preservaron sin alteraciones en `board-presentation-integration-20261022/v1-screenshots/`. Incluyen recorrido ES 360×640, selector ES 320×568, recorrido EN de 100000 copas a 320×568, recorrido y selector EN a 800×1280, y ventana PC ES 1024×768. `v1-evidence.json` registra fuentes, copias, dimensiones y SHA256 de cada PNG, además de los dos XML y tres logs. SHA256 del registro: `96F20D96D0F3B997C6EC77BE198642E80FBBEAEE7D2A4691D00016FEADC60F11`.

**En V1 la fase de UI permaneció abierta.** Una revisión posterior detectó que V1 se veía demasiado pequeña en pantallas de alta densidad, pese a pasar los escenarios anteriores: usaba medidas en píxeles físicos sin escalar proporcionalmente la composición. Los resultados V1 se conservan como tales; no validan la reparación posterior descrita debajo. Ninguna de estas tandas certifica un dispositivo físico ni activa copas de cuenta.

### Corrección de densidad

Se integraron tres archivos en `board-density-integration-20261022/manifest.json`: el layout ahora usa una base lógica 360×640, conserva el área segura y deja que el texto dinámico rasterice según la escala efectiva del Canvas. Las pruebas se amplían a 1080×2400, 1440×3200 y 1536×2048, manteniendo los formatos anteriores. Se sustituyeron las expectativas incorrectas de escala fija 1 y columna limitada a 540 píxeles físicos; se conservan los checks de texto completo, navegación, selección explícita y márgenes seguros.

La evidencia independiente previa pasó **19/19** casos de geometría fuera del Editor. La tanda posterior real pasó **19/19 EditMode**, cero fallos y omitidos, salida 0, entre las **21:47:18 y 21:47:19 UTC**. Informe original: `board-presentation-integration-20261022/results/board-presentation-edit-20261022-retry2.xml`; copia preservada en `board-density-integration-20261022/results/`. El log original `Logs/board-presentation-edit-20261022-retry2.log` también se copió a `board-density-integration-20261022/logs/`. `edit-evidence-copy.json` verifica ambos hashes; SHA256 del registro: `1C4D01F7E54B5F94977107687EBBE7B98EBF5EAE855780C8B98CFCE0BBA28874`.

La siguiente tanda combinada terminó **3/6 PlayMode**, tres fallos y cero omitidos, salida 2, entre las **21:49:07 y 21:49:36 UTC**. Informe: `board-presentation-integration-20261022/results/board-presentation-play-20261022-retry1-with-lobby.xml`. El desglose es **1/3 BoardPresentationPlayTests** y **2/3 LobbyRedesignPlayTests**; no se registra como 6/6.

Dos pruebas de TABLEROS detectaron un recorte real de `ProgressValue`: `preferredWidth` era 59,5 frente a 58 unidades reservadas. La medición calculaba texto a escala 1 mientras el Canvas lo dibujaba a escala 2. Fallaron `NativePortraitAndPcWindowLayoutsKeepApprovedValuesReadableInBothLanguages` y `TrophySlotsAcceptProvidedArtAndNativeCapturesPreservePixelDimensions`. Se prepara la corrección de medición; no se reduce la aserción ni se declara completo el render de alta densidad.

La prueba de lobby `AvatarUnlockAndSelectionPersistWithoutChangingBriefOrOwnership` falló por una expectativa de guardado tomada antes del rellenado automático del Brief tras `UnlockCharacter`; su propietario corrige esa fixture por separado. El informe completo conserva el fallo concurrente, sin atribuirlo a la pantalla TABLEROS ni ocultarlo tras el resultado de otro fixture.

Solo se produjeron dos PNG parciales, `phone-es-149.png` y `phone-small-en-100000.png`, antes de fallar. Se copiaron sin cambios desde `Logs/roadmap-v1.2/board-presentation-20261022-214918` del proyecto a `board-density-integration-20261022/first-play-failure/screenshots/`. Ese registro también conserva XML y log; `first-play-failure/evidence.json` verifica los cuatro archivos por SHA256, con hash del registro `F05C9BCE452C07A2BB699AF7F61076E48DA1769A77F9A3F818BBC4DDA2D9E5A4`. No son once capturas completas.

La cobertura ampliada mantiene tres métodos de TABLEROS con 84 estados del recorrido, diez del selector y once capturas, incluyendo long.MaxValue en móvil pequeño y alta densidad ES/EN. El cierre de su repetición se registra a continuación, sin sobrescribir el intento fallido.

La revisión de integridad actual abarca **37 archivos únicos**: 36 hashes exactos y un cambio externo revisado en `GameText.cs`, limitado a las descripciones inglesas de Kazuru/Tauris, con Parque de Bolas / Ball Pit conservado. Cero diferencias inexplicadas. Evidencia: `board-density-integration-20261022/source-integrity.json` y `source-integrity-review.json`. Se preservó ese cambio; esta comprobación no afirma que todo el proyecto Unity permanezca intacto.

### Cierre de densidad y medición de TABLEROS

El parche de un archivo `board-metrics-integration-20261022/manifest.json` corrige `Measure`: genera las métricas a la escala real del Canvas y divide el ancho a unidades lógicas antes del redondeo. Se conservan tamaños de fuente, márgenes y tolerancias de prueba. La geometría no cambia respecto a los **19/19 EditMode** anteriores.

La repetición pasó **6/6 PlayMode**, cero fallos y omitidos, salida 0, entre **21:55:15 y 21:56:00 UTC** del 22 de octubre: **BoardPresentationPlayTests 3/3** y **LobbyRedesignPlayTests 3/3**. Informe `board-presentation-integration-20261022/results/board-presentation-play-20261022-retry2-with-lobby.xml`. El propietario del lobby corrigió su preparación de baseline; la selección de avatar no cambia las reglas del Brief.

Se generaron e inspeccionaron las **once capturas** completas de `Logs/roadmap-v1.2/board-presentation-20261022-215534`, preservadas sin alteración en `board-metrics-integration-20261022/screenshots/`. Móviles de 320/360/1080/1440 píxeles, tabletas de 800/1536 y ventana PC: números legibles, contención y navegación correctas; las tarjetas fuera de la vista se alcanzan mediante scroll. `evidence-copy.json` verifica quince copias: once PNG, dos XML y dos logs. Se cierra la adaptación visual de esta pantalla de QA; continúan pendientes dispositivos físicos, copas autoritativas y aprobación/distribución del arte de trofeos.

## MP-DEC-037: pulsación, habilidades, Maleta y contador — 22 de octubre de 2026

Alcance de la tanda enfocada: indicador blanco translúcido ligado al gesto existente de **0,5 s**, sin nuevo temporizador de interacción; habilidades a color únicamente cuando la compra concreta está permitida; gris con ficha y descripción consultables; nombre visible **Maleta** en español y **Brief** en inglés; contador central con contorno más definido, 5–2 naranja y 1 rojo oscuro intenso. La geometría del anillo no recibe raycasts. Se mantienen reglas de adquisición, saldo, copias, banco gratuito, tiempos de preparación/combate y animación del contador.

La regresión preparada comprende:

- `UnitHoldProgressTests` y `UnitHoldFeedbackPlayTests`: progreso y umbral, apertura única, liberación temprana, arrastre, cancelaciones por foco/pausa/desactivación y contexto; rutas de campo, rival, eco, Maleta y banco. Comprueba el orden de soltar y terminar arrastre, el traslado válido y la ausencia de acciones o gastos duplicados. Incluye captura intermedia del anillo.
- `AbilityPurchaseEligibilityTests` y `AbilityAvailabilityVisualPlayTests`: coincidencia entre consulta y transacción real por habilidad/coste, consultas sin mutación, oferta/token actual, preparación y bloqueos, copia ausente o almacenada, habilidades adquiridas y límite de Monster. Comprueba que las fichas grises sigan consultables, el cambio en vivo al variar saldo/contexto, el material de escala de grises y su restauración; capturas de compra disponible/no disponible.
- `StyleRevisionPlayTests`: Maleta/Brief en ES/EN, artículos españoles, mayúsculas, frases existentes y palabras ajenas como «Debriefing». Solo cambia el texto visible: se conservan IDs, claves y datos guardados.
- `CountdownVisualPlayTests`: transición a naranja en 5–2 y rojo en 1, contorno renderizado, aviso de combate desde 5 y ocultación al terminar. Comprueba que el componente no avance el reloj, pulse Listo ni intercepte entradas; conserva la supresión por modal. Incluye capturas del contador.

La revisión independiente de las fuentes contrastó `CanPurchaseAbility`, `CanBuyTrick` y `CanUpgradeMonster` con las transacciones vigentes. La consulta no selecciona habilidades ni consume ofertas; la transacción vuelve a comprobar la compra. El shader conserva transparencia, stencil y recorte de UI. Esa revisión y la compilación fuera del Editor no sustituyen las pruebas integradas de render y escenas.

<!-- MP-DEC-037-VALIDATION-BEGIN -->
**Cierre comprobado el 22 de octubre:** EditMode **52/52**, salida 0, 22:14:01–22:14:03 UTC, informe `interaction-feedback-integration-20261022/results/interaction-feedback-edit-20261022.xml`. Desglose: elegibilidad 14, progreso del gesto 4, gestos existentes 4, PouchState 27, banco 1 y regresión bancaria 2.

Play inicial **18/21**, salida 2, 22:16:09–22:17:13 UTC: los tres fallos corresponden exclusivamente a capturas inexistentes, conservados en `interaction-feedback-play-20261022.xml`; no se descartaron sus aserciones. `interaction-capture-setup-integration-20261022` añade preparación de GameView real a 540×960 y restauración de selección/dimensiones en los tres fixtures. La repetición de esos fixtures pasó **9/9**, salida 0, 22:24:13–22:24:57 UTC, informe `interaction-feedback-play-20261022-retry1.xml`: anillo 4, habilidades 3 y contador 2. Los doce casos no modificados (Maleta/estilos 1 y umbrales del contador 11) ya habían pasado. Hay **21 casos Play únicos**, no treinta pruebas distintas ni una tanda global nueva.

Las **seis capturas nativas** están preservadas en `interaction-feedback-integration-20261022/screenshots-retry1/`: habilidades comprables/no comprables y descripción, habilidad adquirida/restantes grises, arco durante la pulsación y contador en 6/5/1. Se inspeccionaron completas: colores y gris coherentes, descripciones legibles, arco blanco translúcido visible, naranja en 5 y rojo oscuro en 1 con contorno definido. `visual-review.json` registra la revisión y `evidence-retry1.json` conserva rutas y hashes de las seis copias y de las **27 fuentes verificadas**, sin diferencias. Una corrección previa de fixture evita destruir la malla de CanvasRenderer al leer el contorno; no cambió producción ni tolerancias. No se ejecutó una build móvil ni una prueba física de dispositivos.
<!-- MP-DEC-037-VALIDATION-END -->

El cierre de estas pruebas no certifica móviles físicos, dos clientes Relay ni el roadmap completo. La progresión competitiva autoritativa, las aprobaciones de trofeos y las demás dependencias creativas/comerciales continúan pendientes.

## MP-DEC-038: imagen suministrada de carga y desvanecimiento — 22 de octubre de 2026

La imagen de **941×1672** procede directamente del adjunto `codex-clipboard-16683ac2-fa02-44de-8199-7fec7bc581cf.png`; SHA256 `48907AA47FBB48803548C6A1AAAD38DA597A6D0BA7920DEA1D4418D68CB930C1`. Su copia PNG se conserva byte por byte. Importación prevista como Sprite Single/FullRect con filtro Point, sin compresión ni mipmaps y tamaño máximo 2048, en `MonsterPouch/UI/Branding/loading-screen-supplied`. No se añade arte, texto ni progreso ficticio.

Auditoría de inserción: `EditorBuildSettings.asset` habilita únicamente `Assets/Scenes/main-scene.unity`; `LocalGameUI.Awake` prepara los sistemas y llama `RenderPage`. La cabecera con logo en `local-progression.cs` corresponde a la ficha de XP, no a una carga, y se conserva. El splash nativo anterior se configuraba mediante `MonsterPouchLaunchBranding.Apply` y `ProjectSettings`; se desactiva su visualización sin borrar el logo. El nuevo overlay se crea al arrancar, y el hook posterior a `RenderPage` solo completa una carga existente: no crea otra en cada reconstrucción de UI.

La imagen completa se contiene en el área segura, sin recorte ni deformación, sobre el fondo oscuro existente a pantalla completa. El mínimo opaco **0,64 s** y el fundido **0,44 s** reutilizan `MapIrisTransition.HoldSeconds` y `OpenSeconds`. El tiempo es independiente de `Time.timeScale`; la vista preparada sigue siendo la que determinan los sistemas actuales de cuenta. Se bloquean puntero, navegación y las entradas directas de teclado de la UI durante la presentación. Al completar, desactivar o cancelar el overlay se libera la entrada, se limpia la instancia y se destruye su raíz. La descarga de la escena de origen no cancela una carga aún sin destino vinculado.

Paquetes preparados por separado: `loading-artwork-import-staging` conserva el PNG y su meta; `loading-artwork-staging` contiene runtime, hook, configurador y ajuste de splash; `loading-artwork-tests-staging` contiene la regresión. Local/Editor y las fixtures compilan contra las bibliotecas reales de Unity fuera del Editor; esto no se registra como prueba integrada ni como validación de importación.

La cobertura preparada comprueba:

- Archivo exacto, resolución, Sprite completo, UV 0–1 y ajustes de importación para escritorio, Android e iOS; geometría de ajuste con márgenes, notch sintético y coordenadas fraccionarias.
- Imagen realmente generada por UGUI a 320×568, 1080×2400, 1536×2048 y 1920×1080: proporción, contención y fondo completo. Capturas nativas de esos formatos, del fundido y del lobby preparado.
- Mínimo opaco, fundido progresivo con `Time.timeScale=0`, bloqueo y recuperación de raycasts/navegación, instancia única, cancelación/desactivación y limpieza. No se permite que un Canvas restante bloquee entradas después del cierre.
- Cuenta existente aislada que vuelve al lobby sin repetir selección/tutorial; cuenta nueva que conserva selección y entrada al tutorial; reanudación de la elección ya guardada. Identidad y recibos se contrastan sin usar archivos del jugador.

<!-- MP-DEC-038-VALIDATION-BEGIN -->
**MP-DEC-038 validado en Unity el 2026-10-22.**

- **EditMode: 10/10, 0 fallos, salida de proceso 0**, de 22:40:46 a 22:40:47 UTC. Fixture `LoadingArtworkImportTests`: archivo y recurso exactos, Sprite 941×1672 completo, filtro Point, importación sin compresión ni mipmaps y geometría de área segura. XML conservado en el workspace: `loading-artwork-integration-20261022/results/loading-artwork-edit-20261022.xml`.
- **PlayMode: 9/9, 0 fallos, salida de proceso 0**, de 22:45:23 a 22:46:27 UTC: `LoadingArtworkScreenPlayTests` 4/4, `UnitHoldFeedbackPlayTests` 4/4 y `StyleRevisionPlayTests` 1/1. Cubre fundido sin depender de la escala de tiempo, bloqueo/liberación de entradas, limpieza y rutas de cuentas nuevas/existentes; el resto comprueba regresiones de pulsación y estilo. XML: `loading-artwork-integration-20261022/results/loading-artwork-play-20261022.xml`.
- **Seis PNG nativos inspeccionados** en `loading-artwork-integration-20261022/screenshots`: `320x568.png`, `1080x2400.png`, `1536x2048.png`, `1920x1080.png`, `fade-reveals-existing-lobby.png` y `existing-lobby-ready-after-loading.png`; los dos últimos son de 540×960. La revisión visual confirma imagen completa sin recorte ni estiramiento, márgenes oscuros donde cambia la proporción, fundido visible y lobby preparado. Las pruebas confirman entrada operable al finalizar.
- `loading-artwork-integration-20261022/evidence.json` conserva los hashes de las seis capturas y confirma **11/11 hashes de archivos integrados iguales a los esperados**, incluido el PNG original. Los resultados pertenecen a esta revisión de las fuentes, sin atribuirles verificaciones posteriores.
- Perfiles temporales aislados y restauración de preferencias, configuración temporal y GameView cubiertos por las fixtures. **Sin prueba en móvil físico ni build instalada**: el notch se comprueba mediante geometría sintética; las capturas usan el área segura que informa el Editor. No se certifican rendimiento ni compatibilidad física de dispositivos.

El cierre de MP-DEC-037 se conserva sin cambios: sus 52/52 EditMode, 21 casos únicos PlayMode y seis capturas corresponden a su propio alcance; no se han sumado a los resultados de la carga.
<!-- MP-DEC-038-VALIDATION-END -->

Los ensayos de importación/Editor no certifican el arranque de una build instalada ni un dispositivo físico. El roadmap global, la autoridad de copas, los trofeos PIXEL ART y su distribución mantienen sus límites y aprobaciones pendientes.


## MP-DEC-039: copia arrastrada, resultados y casillas válidas — 22 de octubre de 2026

El alcance preparado corresponde a tres paquetes independientes: `brief-drag-upgrade-staging`, `match-bottom-messages-staging` y `valid-cell-feedback-staging`. La compra de una copia poseída sobre una casilla administrada del tablero se dirige a la operación `Buy` existente antes de comprobar una nueva colocación; no se modifica `PouchState` ni se cambia la habilidad elegida. La línea diagnóstica se retira solo de la presentación del resultado, conservando su dato. Las casillas legales mantienen color y selección; el contorno aumenta su opacidad de 0,65 a 1 y su ancho de 0,035 a 0,06. La caída de una miniatura fuera del tablero termina su previsualización sin rechazo genérico, mientras banco, venta y errores concretos conservan sus rutas.

Cobertura preparada para la ejecución integrada:

- `BriefCopyUpgradeTests` y los casos actualizados de `MatchFlowTests`: compra sobre la propia unidad o sobre otro actor propio/rival; identidad y posición originales, ocupante intacto, habilidad elegida y coste correcto, sin duplicación. Rechazo sin consumo de token obsoleto, saldo insuficiente, pausas/bloqueos y destinos nulos o ajenos. Límite de cuatro copias y secuencia online coherente con la compra existente.
- Regresión EditMode prevista: `BriefCopyUpgradeTests`, `MatchFlowTests`, `OnlineMatchTests`, `PlacementExchangeTests`, `PouchStateTests`, `BankStorageTests` y `RoadmapBankRegressionTests`. Se confirmarán los nombres completos y los totales contra el XML, sin deducir resultados del número de fixtures.
- `BriefCopyDragPlayTests` prepara dos casos con el gesto real sobre la Maleta: copia sobre otro actor y conservación del despliegue inicial, token obsoleto, caída fuera del tablero y banco. Usa perfiles temporales aislados y restaura preferencias y GameView.
- `MatchBottomMessagesPlayTests` prepara dos casos: eliminación/victoria final y desempate por tiempo mantienen tarjetas, puntuación, ganador, transición y comparación de vida, sin la línea diagnóstica. La explicación sigue en el modelo y el aviso separado de error sigue funcionando.
- `ValidCellFeedbackPlayTests` prepara dos casos: mismo conjunto de casillas válidas con contorno reforzado; la previsualización desaparece al soltar fuera, se conserva la unidad/celda de origen y no se produce ese rechazo ni su sonido. Comprueba banco, copias, recursos, despliegue ordinario y conservación de rechazos específicos. Prevé captura nativa `drag-valid-cells.png` bajo `Logs/roadmap-v1.2/valid-cell-feedback-<UTC>`.

<!-- MP-DEC-039-VALIDATION-BEGIN -->
**MP-DEC-039 cerrado el 2026-10-22 con 98 casos únicos EditMode y seis casos únicos PlayMode. Los reintentos se conservan y no aumentan esos totales.**

- Primer EditMode: **97/98**, de 23:04:32 a 23:04:34 UTC, en placement-feedback-integration-20261022/results/placement-feedback-edit-20261022.xml. Falló solo el caso pause de BriefCopyUpgradeTests: el fixture iniciaba una partida online y Pause la deja sin pausar por diseño. La reparación modifica únicamente la prueba para abrir partida local y comprobar IsOnline=false, Paused=true y escala de tiempo cero; conserva los otros seis rechazos y el código de producción.
- Repetición de BriefCopyUpgradeTests: **10/10, Passed**, de 23:11:36 a 23:11:37 UTC, en placement-feedback-integration-20261022/results/placement-feedback-edit-20261022-retry1.xml. Las otras 88 pruebas Edit ya habían pasado y permanecieron sin cambios: MatchFlowTests 24, OnlineMatchTests 17, PlacementExchangeTests 17, PouchStateTests 27, BankStorageTests 1 y RoadmapBankRegressionTests 2. Total validado único: **98**.
- Primer PlayMode: **5/6**, de 23:06:41 a 23:07:15 UTC, en placement-feedback-integration-20261022/results/placement-feedback-play-20261022.xml. Las comprobaciones de comportamiento pasaron; falló el TearDown de ValidCellFeedbackPlayTests.BankDropOutsideKeepsItsUnitAndCoinsWhilePurchaseErrorsAndValidDeploymentStillWork al exigir las dimensiones previas de una GameView libre que había cambiado de 222×395 a 345×614 en el Editor visible. Se conserva ese intento y se corrige exclusivamente el helper de pruebas para restaurar el preset libre, manteniendo la comprobación exacta de dimensiones para resoluciones fijas.
- Repetición completa de BriefCopyDragPlayTests y ValidCellFeedbackPlayTests: **4/4, Passed**, de 23:13:40 a 23:14:03 UTC, en placement-feedback-integration-20261022/results/placement-feedback-play-20261022-retry1.xml. MatchBottomMessagesPlayTests ya había pasado **2/2** y no depende del helper reparado. Total Play validado único: **seis**. Se conservaron también los informes de texto de ambos intentos.
- Captura final nativa placement-feedback-integration-20261022/screenshots/drag-valid-cells-retry1.png, **540×960**, SHA256 ED9BC1C3002B1B91938184DA8D9685A5D76FAA404CCA1E4CFA064A112DF818F4, inspeccionada nuevamente por root. Coincide con la captura inicial registrada en visual-review.json: los contornos verdes son visibles sobre ambos tonos del tablero y siguen legibles la miniatura, el alcance, banco y Maleta. La ausencia de rechazo, retorno al origen, conservación de recursos y mismo conjunto de casillas se acreditan con las pruebas, no se infieren de la captura.
- placement-feedback-integration-20261022/source-hashes-after-fixture-repairs.json registra **13/13 hashes de archivos integrados coincidentes**, comprobados nuevamente contra los archivos reales para este cierre. Las reparaciones posteriores a los fallos solo afectan las pruebas; no cambian las reglas de producción. **La evidencia de ejecución es Passed del Test Runner: Unity visible permanece abierto (PID 9536) y no se atribuye salida de proceso 0 a estas tandas.**

La validación corresponde al Editor. No certifica móvil físico, build instalada o conexión remota; el replay online se comprueba localmente. El aviso desconocido «Listo» sigue aplazado por petición del creador. Los cierres MP-DEC-037/038 conservan su evidencia independiente y no se acumulan a esta tanda.
<!-- MP-DEC-039-VALIDATION-END -->

El aviso «Listo» queda pospuesto expresamente: no se diagnostica ni se modifica en esta tanda. No se declara prueba de red remota, build instalada o móvil físico; el replay online local y las escenas del Editor tienen un alcance distinto. Los cierres MP-DEC-037 y MP-DEC-038 permanecen intactos.


## MP-DEC-040: orden del lobby y resaltado de las mitades del tablero — 22 de octubre de 2026

Alcance: expendedoras encima de misiones, con sus acciones actuales; solo bordes propios iluminados sin cambiar su superficie, y filtro rojizo suave/translúcido en la mitad rival durante la selección de unidades o el arrastre de ofertas y unidades del campo/banco, relativa a LocalSide. Estos efectos visuales no conceden colocaciones ni modifican ocupación, compras o banco.

La validación debe comprobar el orden geométrico de los accesos y su comportamiento; ambas mitades para jugador azul y rojo/vista invertida; aparición de los bordes propios y filtro rival durante la interacción, superficie propia sin tintado y limpieza al terminarla; conservación del conjunto de casillas legales y de los rechazos; legibilidad de tablero, unidades y previsualizaciones mediante captura nativa. Implementación integrada: se conserva el contorno verde existente (alfa 1, ancho 0,06) y solo se crea un filtro rojo rival translúcido (alfa 0,30), situado bajo personajes y contornos. LocalSide y Mapper determinan la mitad rival. El arrastre usa un contexto visual temporal sin seleccionar ni comprar; al cancelarlo recupera la selección previa si existía. Líneas y sprite se reutilizan mientras se mueve. El indicador se oculta con modal, estado preparado/bloqueado, desactivación o menú. Este detalle describe las fuentes integradas; la validación en escena figura en el cierre de esta decisión.

<!-- MP-DEC-040-VALIDATION-BEGIN -->
**MP-DEC-040 cerrado el 2026-10-22: ocho casos únicos PlayMode aprobados, sin sumar los cierres anteriores.**

- **Lobby: 1/1, Passed**, de 23:34:10 a 23:34:32 UTC. LobbyRedesignPlayTests.FooterTouchesScreenEdgeAndOriginalPedestalRemainsAtPhoneSizes comprueba expendedoras completamente encima de misiones y geometría a 320×568, 393×852 y 768×1024. XML: lobby-button-order-integration-20261022/results/lobby-button-order-play.xml. Este caso geométrico no certifica por sí solo todas las acciones de ambos accesos.
- **Tablero y regresiones: 7/7, Passed, cero fallos y cero omitidas**, de 23:46:05 a 23:46:29 UTC. BoardSideSelectionFeedbackPlayTests 3/3, BriefCopyDragPlayTests 2/2 y ValidCellFeedbackPlayTests 2/2. XML y texto conservados en board-side-selection-feedback-integration-20261022/results/board-side-feedback-play-20261022.xml y .txt. Comprueban selección/arrastre de oferta, campo y banco; contornos propios sin relleno; filtro solo rival relativo al jugador azul o rojo; caché durante movimiento; cancelación, recuperación de selección y ocultación al salir del contexto; conservación de compras, unidades, recursos y reglas de colocación.
- **Tres capturas nativas 540×960 inspeccionadas por root**, en board-side-selection-feedback-integration-20261022/screenshots: initial-lobby.png muestra expendedoras arriba y misiones debajo sin solapamiento; blue-offer-drag.png muestra solo contornos verdes propios y filtro rojizo rival; red-rotated-selection.png confirma la misma relación desde la perspectiva roja invertida. La superficie propia no tiene relleno añadido y los sprites permanecen por encima del filtro.
- **8/8 hashes finales de archivos coincidentes**, seis del tablero y dos del lobby, registrados en board-side-selection-feedback-integration-20261022/source-hashes-final.json y comprobados nuevamente contra los archivos reales para este cierre.
- Resultado acreditado por el **Test Runner en el Editor visible PID 9536**. El proceso de Unity permanece abierto; no se atribuye un código de salida de proceso a estas tandas. Las capturas y pruebas corresponden al Editor, sin móvil físico, build instalada ni conexión remota. La restauración al estado Play/lobby del usuario corresponde al cierre operativo del Editor y no se deduce del XML.

Los cierres anteriores conservan su evidencia independiente. El aviso desconocido «Listo» sigue aplazado por petición del creador.
<!-- MP-DEC-040-VALIDATION-END -->

Los cierres anteriores, incluido MP-DEC-039, conservan sus resultados independientes. El aviso desconocido «Listo» sigue pospuesto.

## MP-DEC-041 — HUD junto a la cabeza y orden de solapamiento

Comprobar los 23 personajes actuales con sus anclajes calibrados y a escala mayor; separación entre la cabeza y todo el HUD, incluida energía, escudo e insignias; arma de Jazir excluida del punto de cabeza; elevación/animación visual sin desplazar la referencia a un efecto o proyectil. Verificar que al cruzarse unidades se dibuja delante el HUD del personaje más próximo en pantalla, tanto en perspectiva azul como roja, manteniendo estados juntos, ocultación al morir/revivir y controles/modales por encima de esta capa. Inspeccionar capturas nativas del catálogo y del solapamiento.

<!-- MP-DEC-041-VALIDATION-BEGIN -->
**MP-DEC-041 cerrado con siete casos únicos PlayMode aprobados; los reintentos no se suman como casos nuevos.**

- Primer intento: **5/7**, sin omitidas, 2026-10-23 00:06:03–00:06:34 UTC. XML: health-bar-head-anchors-integration-20261022/results/vitals-initial-play.xml. Pasaron cuatro regresiones del HUD y ClosestCharacterDrawsLastRegardlessOfSpawnOrderForBothPlayerPerspectives. La galería falló porque la configuración de ejecución no conserva la lista Characters de assets; el caso de estados falló al buscar dummy en el catálogo normal. Ambos fallos eran de preparación de pruebas.
- Reparación exclusivamente de unit-vitals-anchor-play-tests.cs, registrada en health-bar-head-anchor-fixture-repair-integration-20261023/manifest.json, SHA256 final 39FD6021E9A296E20E89FEB060B7C18AB4C4EC24CEFC6A7CD4958F8B87427E62. Reintento completo de la fixture: **3/3, Passed**, sin fallos ni omitidas, 2026-10-24 03:37:10–03:37:27 UTC (23 de octubre en America/La_Paz). XML: health-bar-head-anchors-integration-20261022/results/vitals-retry1-play.xml. Con las cuatro regresiones previas se acreditan siete casos únicos.
- **Ocho PNG nativos 540×960 inspeccionados por root**, en health-bar-head-anchors-integration-20261022/screenshots: roster-01-06.png, roster-07-12.png, roster-13-18.png y roster-19-23.png cubren los 23 personajes; roster-01-06-size-125-percent.png comprueba el primer grupo ampliado; overlap-blue-perspective.png y overlap-red-perspective.png muestran el HUD cercano delante; health-energy-shield-statuses.png conserva los medidores y estados juntos. No se observan cabezas tapadas en estas capturas.
- Revisión de las **29 fuentes iniciales**, con dos diferencias explícitas frente a aquel manifiesto: la fixture reparada indicada arriba y el cambio concurrente conservado en Assets/resources/MonsterPouch/Characters/tauris/Character.asset (Tauruk): EnergyPerSecond 3→0 e InGameSizePercent 56.8→49.8. Su VitalsHeadAnchor {x:0,y:1.27} permanece intacto. El asset conservado tiene SHA256 22A78C977CB0CBCF60865A52B5768FCFCBC3F0545A2B869C640EFF76828626C3. Registro: health-bar-head-anchors-integration-20261022/concurrent-adjustments.json y copia tauris-concurrent-user-adjustments.asset; ver también source-hashes-final.json. No se presenta el conjunto como 29 coincidencias con el manifiesto inicial ni se atribuyen estos ajustes ajenos a MP-DEC-041.
- La compilación offline previa de Gameplay, Local y PlayMode terminó con salida 0 y se conserva en health-bar-head-anchors-staging/verification/compile.json; por sí sola no ejecutaba pruebas. El cierre funcional usa los XML del Test Runner anteriores, sin atribuir código de salida al Editor visible. Sin validación en móvil físico, build instalada ni red remota. El cambio económico del banco queda fuera de MP-DEC-041 y de este cierre.
<!-- MP-DEC-041-VALIDATION-END -->


## MP-DEC-042 — Banco, reservas y fusión (2026-10-23)

Se aplican las reglas aprobadas en DECISIONS.md: reserva gratuita, reembolso al guardar, cobro al retirar, venta de banco sin devolución adicional y fusión entre la unidad del campo y la reserva del mismo tipo. Se conservan límites, mejoras, regalos del tutorial e identidades del catálogo.

<!-- MP-DEC-042-VALIDATION-BEGIN -->
**MP-DEC-042 integrado y validado: 96/96 EditMode y 16/16 PlayMode aprobados.** Se revisaron 2 capturas y se verificaron los hashes de 20 archivos integrados.

- EditMode: **96/96**; PlayMode: **16/16**. Las cantidades proceden del informe final revisado por root, sin convertir la compilación offline en pruebas ejecutadas.
- Revisión visual: **2 capturas revisadas**. Verificación de fuentes: **20 archivos integrados**.
- Registro de cierre: bank-economy-integration-20261023/final-validation.json. Los informes de ejecución y capturas se conservan en bank-economy-integration-20261023.
- La evidencia corresponde al Test Runner del Editor; no se atribuye un código de salida de proceso ni validación en móvil físico o red remota. MP-DEC-041 conserva su cierre independiente; este alcance no completa el roadmap global.
<!-- MP-DEC-042-VALIDATION-END -->


## MP-DEC-043 — Fin de habilidad y una sola preparación del básico (2026-10-24)

Alcance aprobado: concluir la animación corporal de habilidad, sus desplazamientos y retorno antes de comenzar la preparación del primer básico; no sumar otra pausa de `AttackWindup`. Los relojes del modelo y la presentación comparten las duraciones existentes. No modificar estadísticas ni recompensas para satisfacer una prueba.

<!-- MP-DEC-043-VALIDATION-BEGIN -->
**MP-DEC-043 integrado y validado en el Editor.** El conjunto compartido con MP-DEC-044 tiene 207/207 casos únicos EditMode y 5/5 PlayMode aprobados, más 3 casos nuevos de compatibilidad online aprobados; los reintentos no se cuentan otra vez. Se cierra el alcance funcional de recuperación y preparación única. Se verificaron 28 archivos únicos de combate y protocolo frente a sus manifiestos finales, incluidas las dos reparaciones de fixtures. Protocolo actual: `mp-2026-10-24-v14-combat-occupancy`; rechaza clientes v13 y balances distintos. Evidencia y límites en QA_CHECKLIST.md. No se atribuye revisión manual de capturas, móvil físico, build instalada o red remota. MP-DEC-042 conserva su cierre independiente.

Evidencia compartida de combate, consolidada por `test-case/@fullname` usando el último resultado de cada caso:

- `combat-timing-occupancy-integration-20261024/results/combat-edit-initial.xml`: 207 casos, inicialmente 205 aprobados. Los dos casos de Hymal pasan en `daily-vending-integration-20261024/results/progression-combat-edit-initial.xml` (6/6 AbilityRecoveryTests); los demás casos iniciales permanecen aprobados. Resultado del alcance inicial: 207/207 únicos.
- `combat-timing-occupancy-integration-20261024/results/combat-play-initial.xml`: 3/3 AbilityRecoveryPresentationPlayTests aprobados; los dos casos de TargetOccupancyPresentationPlayTests pasan después en `casino-layout-integration-20261024/results/progression-casino-play.xml`. Resultado inicial consolidado: 5/5 únicos.
- OnlineMatchTests pasa 25/25 tanto en el XML de progresión como en `online-reveal-atomic-integration-20261024/results/online-wallet-edit-final.xml`. Son los 22 casos online ya incluidos en los 207 más 3 nuevos: acepta v14 con el balance correcto, rechaza v13 y rechaza balance/payload incorrectos. Total de combate y compatibilidad: 210 Edit únicos, sin sumar las repeticiones.
- Reparación Hymal: se distingue el básico posterior al aterrizaje de un proyectil anterior, se verifica su tick de salida y una sola preparación completa. Solo cambió `ability-recovery-tests.cs`; no cambió el runtime ni el balance para aprobar la prueba.
- Integridad: 28 rutas únicas contra `combat-timing-occupancy-integration-20261024/manifest.json`, las reparaciones `ability-recovery-fixture-repair-integration-20261024/manifest.json` y `target-occupancy-play-fixture-repair-integration-20261024/manifest.json`, y `combat-protocol-integration-20261024/manifest.json`. Para rutas repetidas prevalece la reparación posterior. Coinciden tanto proyecto como fuentes de staging.
- Evidencia del Test Runner del Editor: no se atribuye un código de salida de proceso ni prueba en dispositivo físico o red remota. No se afirma haber ejecutado otras suites citadas como previstas.
<!-- MP-DEC-043-VALIDATION-END -->

## MP-DEC-044 — Ocupación real y reservas de movimiento (2026-10-24)

Alcance aprobado: resolver el objetivo según la casilla alcanzada, conservando la reserva como exclusión de navegación y sin adelantar la posición lógica. La pausa de apertura y las prioridades del catálogo siguen vigentes.

<!-- MP-DEC-044-VALIDATION-BEGIN -->
**MP-DEC-044 integrado y validado en el Editor.** Comparte los 207/207 EditMode y 5/5 PlayMode únicos de MP-DEC-043, con 3 comprobaciones adicionales de compatibilidad online. Las pruebas de ocupación, cancelación física y gancho pasan; la reparación de presentación cambió únicamente el instante de muestreo de la fixture. Los 28 archivos únicos verificados corresponden al mismo conjunto de combate y protocolo, no a otro conjunto que deba sumarse. Se cierra este alcance funcional, conservando las restricciones de validación del Editor y el cierre independiente de MP-DEC-042.

Evidencia específica: 26/26 TargetOccupancyTests, 15/15 TargetLockTests, 17/17 TargetDeathRetargetTests y las regresiones de movimiento/combate del conjunto Edit anterior; 2/2 TargetOccupancyPresentationPlayTests en `casino-layout-integration-20261024/results/progression-casino-play.xml`.

La fixture visual muestrea ahora la mitad del salto definida por TileHopProfile, a 0,61 s con el perfil actual de Stein; antes lo hacía a 0,4 s, todavía dentro de la preparación sin desplazamiento. Se conservan las aserciones de distancia, reinicio, desactivación de unidad/objeto, cancelación de reserva, detención y gancho desde la posición renderizada. No se cambió la animación ni se debilitó la cancelación física. Manifiesto de la reparación: `target-occupancy-play-fixture-repair-integration-20261024/manifest.json`. Los XML iniciales fallidos se conservan como historial; no son nuevos casos ni un fallo pendiente. Los 28 archivos verificados y el total de 210 Edit/5 Play son compartidos con MP-DEC-043.
<!-- MP-DEC-044-VALIDATION-END -->


## MP-DEC-045 — Distribución del casino (2026-10-24)

Referencia del creador: `C:/Users/HP VICTUS/AppData/Local/Temp/codex-clipboard-03619ae6-be01-4549-a052-61ad55e4bb8c.png`. Alcance: distribución con el mismo arte, dados ocultos y adaptación móvil vertical/PC.

<!-- MP-DEC-045-VALIDATION-BEGIN -->
**MP-DEC-045 integrado y cerrado en Unity:** 22/22 casos específicos del casino aprobados y regresión compartida final 9/9 aprobada. Las dos capturas nativas de Game view, 540×960 y 1366×768, fueron inspeccionadas por root y confirman distribución, marco/fichas y arte conservados, ausencia de dados, Maleta debajo a la izquierda y accesorios periféricos en PC. Evidencia: `casino-layout-integration-20261024/results/progression-casino-play.xml`, `results/casino-viewport-regression-play-final.xml` y `native-visual-validation.json`. La regresión final resuelve tres expectativas antiguas de fixtures y un defecto real de área segura; no se modificó el diseño para acomodar las pruebas. Se cierra el alcance visual aprobado, sin atribuir validación en dispositivo físico o build instalada.

Desglose específico: CasinoReferenceLayoutPlayTests 3/3, CasinoHudPlayTests 15/15 y CardTableArenaPlayTests 4/4. Las otras cinco pruebas del XML inicial pertenecen a ocupación (2) y regalo/expendedoras (3), por lo que no se suman a los 22 casos del casino. Los 13 archivos originales del paquete se verificaron contra su manifiesto antes de las reparaciones posteriores de fixtures y área segura.

Regresión final del 24-10-2026, 11:03:47–11:04:50 UTC: ViewportLayoutPlayTests 3, InflatableMapPlayTests 1, RoyalStylePlayTests 2, VendingUnlocksPlayTests 2 y LobbyRedesignPlayTests 1; los nueve aprobaron. Los tres casos de regalo/expendedoras se repiten y no son tres casos nuevos. Las expectativas de ancho ahora respetan las dimensiones propias del casino; la pulsación usa el tiempo vigente; la corrección real de área segura conserva el rectángulo efectivo del diseño.

Capturas nativas revisadas y hashes verificados:

- `captures/casino-native-mobile-20261024.png`, 540×960, SHA-256 `0A5C8A5050617D4E1E9690BCE9C81029865078D87F43672539FBE0201F3D01F5`.
- `captures/casino-native-desktop-20261024.png`, 1366×768, SHA-256 `31BB07D3AC43BF5B18298DEE85CC2DEC5376B36357DBEC3955D3FD5F35DB8FA6`.

Rutas relativas al registro `casino-layout-integration-20261024`. Revisión archivada en `native-visual-validation.json`; captura desde Unity por el puente del editor, sin composición externa. No se acredita Android físico ni build instalada. Se restauraron el tablero y el tamaño de Game view después de capturar.
<!-- MP-DEC-045-VALIDATION-END -->

## MP-DEC-046 — Regalo diario (2026-10-24)

Aplicar únicamente la tabla exacta de nueve resultados aprobada en DECISIONS.md. La imagen azul `daily-pouch-blue-v1.png` ya está aprobada; su archivo exacto está integrado y su presentación nativa ya fue revisada; la regresión compartida de Lobby también quedó aprobada en MP-DEC-048.

<!-- MP-DEC-046-VALIDATION-BEGIN -->
**MP-DEC-046: alcance local del regalo aprobado e integrado, con tabla, persistencia, flujo y arte azul validados.** Los 9 DailyGiftTests y el caso del regalo en lobby habían aprobado; los recibos/crédito se validaron dentro de 109 Edit únicos de regalo/perfiles. La nueva prueba `VendingUnlocksPlayTests.ApprovedCatalogueShowsAllCurrentMachineArtAndDailyGiftUsesItsBluePouch` pasó y root revisó la captura nativa del regalo con la imagen azul exacta aprobada, cifras y botón legibles. Registro: `approved-vending-catalogue-integration-20261024/native-visual-validation.json`. La regresión compartida posterior resolvió sus tres fallos de fixture: Lobby pasó 4/4 al repetir, incluido el regalo; el total es 11 casos únicos Play aprobados según MP-DEC-048. Se cierra el alcance local del regalo con tabla, flujo, arte y regresión de Lobby validados. La bienvenida posterior de 20 Saturnkens corresponde a MP-DEC-050, todavía sin implementar.

- `daily-vending-integration-20261024/results/progression-combat-edit-initial.xml`: 9/9 DailyGiftTests, incluidos los 10.000 sorteos de distribución exacta, recibos históricos, cobro único y cambio de día UTC.
- `casino-layout-integration-20261024/results/progression-casino-play.xml`: LobbyRedesignPlayTests.HeaderLinksSettingsAndDailyGiftUseRealLocalState y los 2 casos VendingUnlocksPlayTests aprobados; apertura/premio simple, saldo persistido, reintento sin doble crédito y recuperación de una importación interrumpida.
- Consolidado de perfiles usando también `online-reveal-atomic-integration-20261024/results/online-wallet-edit-final.xml`: DailyGift9 + GuestAbility54 + GuestCollection15 + GuestOnboardingState11 + GuestOnboardingV3 10 + GuestVending10 = 109 Edit únicos aprobados. BankEconomy19 y PouchState27 agregan 46 regresiones distintas (155 con perfiles), no forman parte del recuento de 109. No se incluyen combate ni online en ese total.
- Los 3 fallos iniciales de GuestAbility/GuestVending se resuelven en el último XML, con las fixtures reparadas; DailyGiftTests no tuvo esos fallos. Manifiestos: `daily-vending-integration-20261024/manifest.json` y `daily-vending-edit-fixture-repair-integration-20261024/manifest.json`: 22 rutas únicas comprobadas en proyecto y staging.
- Imagen azul exacta integrada y revisada mediante captura nativa; nueva prueba de arte aprobada. La repetición compartida de Lobby aprobó 4/4. La aprobación de esa imagen es independiente de la aprobación posterior de los 23 diseños de expendedoras.
<!-- MP-DEC-046-VALIDATION-END -->

## MP-DEC-047 — Expendedora con coste directo (2026-10-24)

Coste aprobado: 5 Saturnkens por apertura solicitada por el jugador. Los diez paquetes de recursos y la probabilidad provisional de personaje pendiente por apertura 1–10 de 1/1/1/1/1/2/4/8/20/100 % están aprobados para pruebas: Earthkens 10/20/30/50, Sunkens 10/20/30/50 y mixtos 10+10 y 20+20. La configuración exacta está integrada y sus 31 pruebas específicas EditMode están aprobadas. Se conserva la ausencia de repeticiones y no se añade un reinicio del ciclo; La regresión Play final 9/9 incluye los dos casos de expendedoras y el regalo; queda pendiente su revisión nativa específica.

El creador también aprobó que todos los premios pendientes tengan el mismo peso, salvo las garantías del personaje y de su primera habilidad; no se repiten premios dentro de una máquina.

<!-- MP-DEC-047-VALIDATION-BEGIN -->
**MP-DEC-047: alcance local de expendedoras cerrado, con catálogo de 23 máquinas, configuración aprobada y arte validado.** Los 31 ApprovedVendingEconomyTests, 10 GuestVendingTests y 7 AndroidPortraitTests pasaron en la tanda 48/48; las tres VendingUnlocksPlayTests aprobaron en `pouch-room-ui-integration-20261024/results/pouch-room-play-initial.xml`, incluida la nueva prueba de catálogo/arte reales. Las dos pruebas de infraestructura ya habían pasado y no se cuentan como nuevas. Root inspeccionó la captura nativa del catálogo desplazable con sus 23 asociaciones y nombres actuales. Coste directo 5, paquetes y curva aprobados, idempotencia, propiedad y ausencia de aperturas automáticas se conservan. La migración de acceso/economía de Legacy continúa pendiente y no se valida autoridad de servidor, pagos reales ni dispositivo físico. Los fallos de fixture de Lobby de la tanda compartida quedaron resueltos con 4/4 en la repetición; MP-DEC-048 registra 11 casos únicos Play aprobados.

- `online-reveal-atomic-integration-20261024/results/online-wallet-edit-final.xml`: GuestVendingTests 10/10 y GuestAbilityTests 54/54, todos aprobados tras reparar las fixtures. No queda un fallo técnico abierto de esa ejecución.
- `casino-layout-integration-20261024/results/progression-casino-play.xml`: VendingUnlocksPlayTests.EarnedSaturnkensWaitForExplicitChoiceAndOneConfirmationUnlocksTheCurrentCharacter y VendingUnlocksPlayTests.MissingApprovedCatalogueAndInsufficientFundsNeverDrawAndGiftImportRetriesSafely aprobados. Cubren confirmación explícita, coste de 5 Saturnkens, doble clic/reintento, desbloqueo, falta de saldo/catálogo e importación de regalo interrumpida entre archivos.
- Los 109 Edit de perfiles/regalo, 3 Play compartidos y 22 hashes se desglosan en MP-DEC-046; no duplicar ese recuento. La migración de formato preserva los datos actuales sin conceder saldo ni derechos nuevos.
- Configuración provisional implementada, integrada y probada con 31 ApprovedVendingEconomyTests; los 10 GuestVendingTests anteriores validaron infraestructura y se repitieron en la tanda de 48/48. Los 23 diseños y asociaciones de la galería ya están aprobados; importación exacta, referencias por ID y catálogo operativo ya están integrados; las tres pruebas Play de expendedoras y la revisión nativa del catálogo ya aprobaron. Sigue pendiente la decisión de acceso/economía de cuentas Legacy. Sin pagos reales, publicación ni autoridad de servidor validada.

Arte aprobado: `output/imagegen/vending-machines-v1/index.html` y manifiesto SHA-256 `ED926B6D56081B7F425F652B2EBBC00B8196DAB04AD7A5372E0334CD69EA91D5`. Comprobación de integridad previa: 23 PNG de 23 coinciden con sus hashes; no se atribuye resultado de Unity. La nueva prueba de catálogo real verificó importación, filtrado Point sin mipmaps y referencias por ID; root revisó la legibilidad en captura nativa.

Integridad final del arte y catálogo: 56 rutas únicas de `vending-art-integration-20261024/manifest.json` (48) y `approved-vending-catalogue-integration-20261024/manifest.json` (8), con las sustituciones finales de `approved-catalogue-meta-repair-integration-20261024/manifest.json` para las dos metas reparadas. Los 56 hashes coinciden en proyecto y fuentes finales. Las 28 metas tienen GUID de 32 caracteres hexadecimales y cada GUID aparece una sola vez en Assets; no hay colisiones. La revisión estática de las 23 asociaciones, 69 IDs de habilidades y 96 fuentes permanece registrada en `approved-vending-independent-review-20261024.json`, ahora con la reparación y esta comprobación adicional. La primera revisión omitió validar la longitud de GUID: Unity ignoró `VendingCatalogue.asset` y `approved-vending-economy-tests.cs`; la ejecución inicial de 17 casos no incluyó los 31 casos nuevos del catálogo y no los acredita. Solo cambiaron las dos metas. La repetición válida, archivada en `approved-vending-catalogue-integration-20261024/results/approved-machines-edit.xml`, aprobó 48/48 casos entre 11:02:10 y 11:02:26 UTC: 31 nuevos de ApprovedVendingEconomyTests, 10 regresiones GuestVendingTests y 7 AndroidPortraitTests. Los 10 GuestVending ya estaban contados en el cierre de infraestructura; no se suman dos veces como casos nuevos. Estos tests se ejecutaron en el Editor y no acreditan un móvil físico. La regresión final 9/9 había repetido los dos casos de expendedoras y el regalo; la prueba de catálogo/arte y las capturas específicas posteriores completan esa revisión.

Validación nativa del 24-10-2026 archivada en `approved-vending-catalogue-integration-20261024/native-visual-validation.json`: captura `captures/approved-catalogue-23.png` SHA-256 `50CFCB62B713C70B26AE9A3A3DC0A74CD92705829FCAA3C90007400905879EF8` y `captures/approved-daily-blue-pouch.png` SHA-256 `97CCB51F2DB97B68557F1FD0851A193509492CAF4147A6A817668FFC4BF9FE88`. Ambas fueron inspeccionadas por root y verificadas por hash; se capturaron en PlayMode con perfil temporal. El recorte de filas inferiores pertenece al viewport desplazable, no a un recurso faltante. No son capturas de dispositivo físico.

Recuento sin duplicación: los 109 Edit previos incluyen los 10 GuestVending; añadir los 31 ApprovedVending y 7 AndroidPortrait produce 147 casos únicos entre esos alcances, no 157. Los 3 Play previos de regalo/expendedoras y la nueva prueba de catálogo/arte son 4 casos únicos; los 2 Vending de infraestructura se repitieron. La tanda actual completa de 11 terminó 8/11: los 3 Vending,4 LocalProgression y 1 Sala de fallos/reintentos pasaron; los otros 3 Lobby se repararon únicamente en su fixture y aprobaron en la repetición 4/4 de MP-DEC-048.
<!-- MP-DEC-047-VALIDATION-END -->

<!-- ONLINE-REVEAL-ATOMIC-VALIDATION-BEGIN -->
## 2026-10-24 — Corrección técnica de revelación online atómica

Se corrigió el caso en que una secuencia recibida contenía decisiones válidas seguidas de una inválida: antes podían quedar aplicadas compras, movimientos, fusiones de banco o rerolls parciales. Ahora se valida el diario completo en un estado privado y solo se confirma si todos sus comandos son válidos. Un rechazo conserva saldo, ofertas, unidades, reservas, referencias existentes y secuencia aleatoria; un éxito conserva la identidad de la instancia superviviente y solo se aplica una vez. Es una corrección técnica de las reglas vigentes, sin decisión creativa nueva.

**Integrado y validado en EditMode:** `online-reveal-atomic-integration-20261024/results/online-wallet-edit-final.xml` contiene 152/152 aprobados: OnlineRevealAtomic17, OnlineMatch25, BankEconomy19, PouchState27, GuestAbility54 y GuestVending10. Los 17 casos de atomicidad cubren cola inválida por tipo de comando, reroll rechazado, fusiones en ambas direcciones, venta, recompra e identidad al retirar del banco. Los 6 archivos de `online-reveal-atomic-integration-20261024/manifest.json` coinciden en proyecto y staging. La versión vigente de conexión es `mp-2026-10-24-v14-combat-occupancy`; no se mezclan clientes anteriores aunque el balance coincida. La regresión compartida de UI/mapas/regalo/expendedoras quedó aprobada 9/9 en `casino-layout-integration-20261024/results/casino-viewport-regression-play-final.xml`; no constituye una prueba de transporte online. No es una validación de red remota ni de servidor autoritativo.
<!-- ONLINE-REVEAL-ATOMIC-VALIDATION-END -->

## MP-DEC-048 — Sala de estuches diaria (2026-10-24)

Reglas aprobadas: cinco oportunidades UTC al ganar partidas completas de cualquier tipo/bots; primeras tres azules; últimas dos 75 % azul/25 % personaje; personaje pendiente elegido uniformemente y fijado por ID; 0,5 % para ese personaje o recursos de la tabla diaria exacta; colección completa/personaje obtenido antes de abrir entrega recursos; reset descarta sin abrir y abrir no repone.

<!-- MP-DEC-048-VALIDATION-BEGIN -->
**MP-DEC-048 integrado y cerrado en Unity: 185/185 EditMode y 11 casos únicos PlayMode aprobados, con 7 capturas nativas revisadas.** La tanda Edit incluye 26 pruebas nuevas de Sala y 159 regresiones, sin contarlas de nuevo en otros cierres. La primera tanda Play terminó 8/11; tras reparar únicamente el ciclo de vida de la fixture de Lobby, su repetición completa pasó 4/4 entre 11:29:49 y 11:30:39 UTC del 24-10-2026. Se conservan los otros 7 casos aprobados: 11 únicos, no 15. Las capturas de 320×568, 393×852 con área segura y 1280×720 confirman cinco posiciones, estuches azules/personaje, abiertos sin reposición y premios de recursos/personaje. Evidencia final: `pouch-room-ui-integration-20261024/final-validation.json` y sus XML/capturas. Se cierra este alcance local, sin validar dispositivo físico ni servidor autoritativo; la política Legacy sigue pendiente.

Registros: `pouch-room-core-integration-20261024/results/pouch-room-edit-initial.xml` (185/185); `pouch-room-ui-integration-20261024/results/pouch-room-play-initial.xml` (8/11); `results/pouch-room-lobby-play-retry.xml` (4/4). Reparación: `pouch-room-fixture-lifecycle-repair-integration-20261024/manifest.json`, destruye la UI temporal antes de borrar sus perfiles/restaurar overrides; no cambió reglas ni premios. Las 65 rutas integradas originales incluyen core 11, UI 53 y referencia de ensamblado 1; el fixture reparado sustituye su hash original y debe aplicarse como override final. Las 7 capturas y sus hashes figuran en `final-validation.json`; fueron revisadas por root. Desglose Edit: GuestPouchRoom26, ApprovedVending31, DailyGift9, GuestAbility54, GuestCollection15, OnboardingState11, OnboardingV3 10, GuestVending10 y LocalGuest19. Play únicos: Sala3, LobbyGift1, Vending3 y LocalProgression4. No duplicar reintentos ni las regresiones ya acreditadas.
<!-- MP-DEC-048-VALIDATION-END -->

## MP-DEC-049 — Compra de habilidad permanente (2026-10-24)

Regla aprobada: una habilidad bloqueada cuesta 100 Earthkens + 100 Sunkens, debitados juntos una sola vez. Los premios gratuitos conservan su gratuidad y las mejoras temporales de combate conservan sus costes actuales.

<!-- MP-DEC-049-INITIAL-LOCK-APPROVED -->
**Aclaración posterior aprobada directamente por el creador en el chat principal:** las cuentas nuevas comienzan con **las tres mejoras bloqueadas**. Se conserva la habilidad automática base de cada personaje. El bloqueo permanente debe reflejarse en la disponibilidad real de las mejoras, además de su presentación; desbloquear una mejora por monedas o por un premio aprobado la incorpora a la propiedad de la cuenta. Los costes y reglas temporales de mejorar durante una partida se conservan. Esta decisión no convierte la habilidad base en una compra, no revoca habilidades ya obtenidas y no resuelve por sí sola los derechos de cuentas Legacy.

Esta aclaración directa amplía la decisión de precio transmitida anteriormente desde el chat lateral y prevalece en el alcance de las cuentas nuevas. No aprueba premios adicionales de módulos del tutorial ni un estuche azul de tutorial.
<!-- MP-DEC-049-COPY-LIMIT-APPROVED -->
**Aclaración directa posterior sobre copias y fusiones:** el límite total de copias de cada tipo en la partida es **1 + número de habilidades permanentes desbloqueadas de ese personaje**. Sin mejoras desbloqueadas permite 1 copia; con 1 permite 2; con 2 permite 3; con 3 permite 4. La habilidad base automática no cuenta como una de esas tres mejoras.

El límite se aplica al total del tipo, incluidas las copias de la unidad principal y de su reserva de banco; no concede copias adicionales ni propiedad por guardar, retirar o fusionar. Las compras y fusiones solo pueden incorporar mejoras desbloqueadas. La siguiente mejora por orden numérico es la **primera desbloqueada que todavía no esté presente** en la unidad; no se concede una mejora bloqueada. Esta aclaración resuelve la interacción antes pendiente y ajusta el límite fijo de cuatro de MP-DEC-042 a un máximo variable de 1–4 según propiedad.

Se conservan la inversión/reembolso y los demás límites del banco, la identidad de las unidades y la contabilidad de stock. El tutorial conserva su permiso efímero de entrenamiento aprobado, limitado a `IsTraining`, sin recibos de propiedad ni cambios en la cuenta; no se extiende a partidas BO5 normales. No se inventan regalos para completar el límite de copias ni se cobra la habilidad automática base.

<!-- MP-DEC-049-VALIDATION-BEGIN -->
**Auditoría previa realizada; implementación y pruebas pendientes.** Comprobar:

- Saldo exacto 100/100 permite una compra; falta de cualquiera de las dos monedas no modifica saldo ni propiedad. Saturnkens y Moonkens no se debitan.
- Matriz de propiedad 0/1/2/3: límite de copias 1/2/3/4, contando principal y reserva. Compras y fusiones respetan stock y solo mejoran con la primera habilidad desbloqueada aún no presente; comprobar propiedad no consecutiva, límite alcanzado, fusión en ambas direcciones, reembolso y coste al retirar. No conceder habilidades bloqueadas ni exceder copias al reintentar.
- Descuento de ambas monedas y desbloqueo en una única escritura atómica. Fallo antes de confirmar conserva el estado anterior y permite reintentar.
- Repetir la petición tras guardar o recargar devuelve el resultado original; reutilizar su ID para otra habilidad se rechaza. Doble clic y dos intentos sobre la misma habilidad no cobran dos veces.
- Si una caja, expendedora u otra fuente aprobada la desbloqueó antes de confirmar, no se cobra y se conserva el recibo de esa fuente.
- IDs de personaje y habilidad válidos, personaje poseído, recuperación y cuentas Legacy respetan las restricciones actuales; no se cobra una habilidad base ni un nombre/índice que no sea ID desbloqueable.
- Migración desde el formato v7 preserva sala, cartera, colección, recibos y progreso de máquinas. Los valores serializados de enums deben ser estables entre Editor, desarrollo y compilación comercial.
- Propiedad nueva reconocida por las expendedoras sin repetir el premio ni romper la garantía de la primera habilidad. No se cambian coste o selección de mejoras durante el combate.
- UI presenta el coste conjunto y actualiza propiedad/saldo solo después de confirmar. En cuentas nuevas, las tres mejoras permanecen bloqueadas hasta un desbloqueo permanente válido; la habilidad base automática sigue disponible. Conservar habilidades ya poseídas y no trasladar el precio permanente a las mejoras temporales de combate. Registrar pruebas y capturas reales antes del cierre.
<!-- MP-DEC-049-VALIDATION-END -->

## MP-DEC-050 — Bienvenida única de 20 Saturnkens (2026-10-24)

Regla aprobada directamente: 20 Saturnkens por cuenta nueva una sola vez, sin gasto automático. No habilita otros premios de tutorial.

<!-- MP-DEC-050-VALIDATION-BEGIN -->
**Implementación y pruebas pendientes.** Verificar creación de cuenta nueva con un único recibo y crédito de 20; reapertura, reinicio, fallo/reintento de escritura y reanudación del tutorial sin otro crédito; cuentas existentes/Legacy sin concesión retroactiva; saldo previo conservado; cuatro aperturas a coste 5 únicamente por decisión del jugador; ausencia de activación automática, moneda intermedia o entrega diaria. Comprobar convivencia con regalo diario, Sala y compra permanente de habilidades. No se suman pruebas de otras fases como validación de esta bienvenida.
<!-- MP-DEC-050-VALIDATION-END -->


## Cierre MP-DEC-049/050 y MP-DEC-052 — 2026-10-24

341 casos únicos EditMode y 14 PlayMode aprobados; 38 de los EditMode son nuevos de economía/copia. Los dos fallos de la regresión eran fixtures que suponían cartera vacía; tras corregir la preparación, sus 54 casos completos pasaron. Cinco capturas nativas 393×852 y 1280×720 revisadas. Informes en permanent-ability-welcome-integration-20261024, permanent-ability-ui-integration-20261024 y dev-icon-integration-20261024. No se suma la primera ejecución de 215, que no incluyó los fixtures nuevos durante la recarga.

Icono DEV: 5/5 EditMode finales, incluyendo el PNG 2D correcto, tamaños/capas Android, conservación del icono normal y restauración ante un fallo sintético. Informe dev-icon-integration-20261024/final-edit-results.xml. El primer intento detectó textura cúbica; se corrigió únicamente su importación a 2D y se repitió. No se ejecutó BuildPlayer.


## Módulos 7 y 8 — validación local, 2026-10-24

- 162/162 EditMode: migraciones, secuencia de tutorial, premio único, repetición, fallo de escritura y reintento, moneda persistente, expendedoras y Sala de estuches.
- 28 casos únicos PlayMode finalmente aprobados: pantallas reales, compra confirmada, conservación de los primeros ejercicios, banco y mejoras permanentes. Los reportes iniciales y finales están en tutorial-economy-integration-20261024/results del workspace.
- Capturas nativas móvil 393×852 y PC 1280×720; revisión visual de guía, estuche, premio, monedas y expendedora. Sin capturas sintéticas.
- La prueba de integración abre el sprite de una de las 23 máquinas, comprueba sus 14 premios y realiza una única extracción real. No entrega premios mediante animaciones o callbacks sin recibo.
- Icono DEV: conserva además las cinco comprobaciones aprobadas en MP-DEC-052. No se ha creado un APK nuevo ni sustituido el icono de las builds normales.


## 2026-10-25 — tamaño del casino, sombras y premios visuales

Correcciones pedidas por el creador tras dejar el punto jugable:

- Grand Casino Hotel comparte la misma expansión horizontal, dimensiones y centros de las 60 casillas que Parque de Bolas y Campo Real. Se elimina la excepción que lo hacía un 9,09 % más estrecho. El cambio de estilo ya no modifica la geometría bajo los actores. Se conserva cámara, altura, distribución y recursos originales.
- Se recuperan las 11 sombras autoradas del payaso, carro y fichas, sin duplicarlas con siluetas generadas. Dados y sus sombras permanecen ocultos; los accesorios periféricos mantienen sus sombras.
- La lista de premios de expendedoras utiliza imágenes existentes. Monedas: iconos de 76 unidades UI, cantidades de 30 y nombres de 18, con espacio suficiente para dibujar las cifras, incluidos paquetes mixtos. Personajes y habilidades: etiquetas genéricas PERSONAJE / NUEVA HABILIDAD. Las habilidades no disponibles conservan la presentación gris; siguen siendo consultables.
- Por petición posterior se retiran los porcentajes visibles. Se mantienen estados OBTENIDO / BLOQUEADO, garantías, precios, pesos internos y recibos. La recompensa obtenida mantiene su icono y su identificación.
- La compilación Android normal ahora muestra confirmación con Mostrar carpeta, como DEV. Se verificó un APK normal ya existente: Builds/Android/2026-10-25_00-00-40/MonsterPouch_2026-10-25_00-02-43.apk, paquete com.monsterpouch.game, sin debuggable ni desbloqueo DEV, ARMv7/ARM64. Ese APK es anterior a estos cambios. No se ha recompilado ni publicado otro APK; su compilación anterior duró 40 min 42 s.

Validación: 15/15 EditMode Android y 38 casos únicos PlayMode finalmente aprobados. Tras ampliar monedas y retirar porcentajes se repiten los casos de expendedoras y tutorial. La comprobación de cantidades exige geometría de texto para detectar cifras truncadas, además de sus valores. Capturas nativas de premios móvil/PC y casino móvil/PC se revisan antes del cierre; evidencias en los paquetes de integración 20261025 del workspace.

El avance a nuevos módulos del roadmap sigue pausado. No se reinician perfiles ni se convierten pruebas en premios de cuentas reales.

## MP-DEC-055 — Copas locales de salas online, 2026-10-25

<!-- MP-DEC-055-VALIDATION-BEGIN -->
**Alcance local validado en Unity: 380 casos únicos EditMode y 25 PlayMode aprobados.** La primera tanda Edit aprobó 378/380. Las dos correcciones fueron exclusivamente de pruebas: versión de perfil codificada como 9 y comparación de actores aún privados durante preparación. La repetición completa de sus dos fixtures aprueba 46/46, conservando los otros 334 casos aprobados, sin sumar reintentos.

La primera tanda de escenas aprobó 22/22. Unity había omitido los tres casos nuevos del enlace online por un GUID de metadatos malformado; se reparó el meta y se ejecutó esa fixture completa, 3/3. No se presenta la primera tanda como cobertura del enlace online. Incluye resultado confirmado una sola vez, persistencia, ausencia de puntos por bots/entrenamiento, rechazo de resultados discordantes y reintento tras fallo de escritura.

Se revisaron cuatro capturas nativas del recorrido a 0 y 150, presentación PC y bloqueo de Campo Real tras descender a 120. Los archivos y XML se conservan en `ladder-runtime-integration-20261025`. Compilación previa de cinco ensamblados y compilación real del Editor sin errores. No se recompiló APK ni se probaron dos dispositivos mediante Relay real; la confirmación entre clientes y el guardado local no equivalen a servidor autoritativo. El resto del roadmap continúa incompleto y su expansión sigue pausada salvo este alcance.
<!-- MP-DEC-055-VALIDATION-END -->


## 2026-10-25 — Cierres locales anteriores y paquetes nuevos pendientes

### A. Habilidades y Tauruk: cierre local verificado

- Primera tanda EditMode: 175/176; repetición ampliada: 43/43. La unión por nombre completo del caso contiene **186 aprobados únicos**, sin fallo activo; no sumar 176+43.
- Primera tanda PlayMode: 6/12; repetición de las seis afectadas: 6/6. Total **12 únicos aprobados**. El ciclo de vida del fixture se corrigió para desactivar/destruir la UI antes de retirar su perfil temporal; no se ocultaron excepciones ni se cambió la economía.
- Evidencia: `ability-combined-integration-20261025/results/ability-mark-edit-initial.xml`, `ability-mark-edit-retest.xml`, `ability-mark-play-initial.xml`, `ability-mark-play-retest.xml`; resumen en `ability-mark-checkpoint-20261025.json`.
- Seis capturas nativas revisadas: fichas `flo-3-393x852.png`, `tauris-1-393x852.png`, `atori-2-393x852.png`, `aky-1-393x852.png`; actores reales `tauruk-mark-combat-393x852.png` y `tauruk-mark-preparation-393x852.png`. La marca aparece en combate y se respeta la ocultación del rival durante preparación.
- Límite: sin APK nuevo, dispositivo físico ni multijugador remoto real.


### B. DUELO, cercanía, estuches y limpieza: cierre anterior a naciones

<!-- MP-20261025-ONLINE-PLAY-BEGIN -->
El cierre local anterior a las naciones registra 409 EditMode aprobados (303 online/cercanía + 106 perfiles/estuches/arte) y 21 PlayMode únicos aprobados. La primera tanda Play terminó 20/21; la repetición aislada del fixture de arte pasó 2/2 y no se suma dos veces. Evidencia: `online-pouches-cleanup-checkpoint-20261025.json`. Estos resultados no validan las naciones, el nuevo ladder, el encabezado ni el salto de Kazuru.

Capturas nativas revisadas: DUELO 393×852 y 1280×720, estuches página 3 a 393×852, estuches 320×568, Home y Maleta de Bugaloo. El fixture de arte se aisló de idioma/cuenta del usuario; sus reintentos no incrementan el total de casos únicos. No son ensayos en dispositivo físico ni de transporte UGS.
<!-- MP-20261025-ONLINE-PLAY-END -->

Estuches por victorias sin límite de cantidad: ciclo repetido de tres azules y dos sorteos 75 % azul / 25 % personaje, con 30 % de desbloqueo al abrir el estuche de personaje. Se conserva la caducidad diaria UTC de los estuches pendientes; ilimitado no significa acumulación entre días. El regalo diario conserva tabla, periodicidad y recibos.

<!-- MP-20261025-CLEANUP-BEGIN -->
54 PNG y 54 metadatos retirados, con recovery en `apk-size-cleanup-integration-20261025/recovery` y copia ZIP de recuperación. Se completó la regresión local anterior indicada arriba. Estimación: 138.269.581 bytes (138,27 MB); no se generó un APK nuevo para medir el ahorro final. Los recursos activos y las referencias de arte aprobadas se conservan.
<!-- MP-20261025-CLEANUP-END -->

### C. Naciones, migración de copas, encabezado y salto: integrado, pendiente de cierre

<!-- MP-20261025-NATION-QA-BEGIN -->
El recorrido conserva sus 84 archivos integrados y la corrección de historial. Los 263 casos EditMode únicos de la tanda inicial están resueltos combinando sus 260 aprobados y la repetición de NationTrophyLadderTests, 22/22; no hubo una nueva corrida global 263/263. El lote compartido de repetición terminó 72/79 por seis fallos del fixture de eje de Kazuru y una expectativa histórica 20/10 de Tauruk; sus reparaciones están integradas y el retest de 27 casos de Kazuru sigue pendiente. PlayMode del 25-10-2026 a las 14:41:44 UTC: 38/44, con NationArenaScenery 2/2, NationThemeCompatibility 12/12 y LobbyRedesign 12/12 aprobados. Header quedó 11/15 y selector 1/3; las seis reparaciones de fixtures están integradas, pendientes de repetir. XML conservados en `nation-combined-integration-20261025/results`.

Encabezado y salto de Kazuru integrados: nombre guardado en el recuadro y una sola cantidad de copas; salto de cuerpo completo con arte actual, misma velocidad, reservas y tiempos de combate. La integración final `nation-trophy-final-integration-20261025/manifest.json` añade 23 archivos: 17 de trofeos/UI, dos del helper nativo y cuatro reparaciones de fixtures. Compilación offline: cinco asambleas Editor y dos asambleas runtime AndroidRelease aprobadas; esto no es una build APK ni ejecución en Android. Repeticiones enfocadas y revisión visual final pendientes.

| PlayMode anterior a las reparaciones finales | Aprobadas / total | Estado |
|---|---|---|
| NationArenaScenery | 2/2 | Aprobado |
| NationThemeCompatibility | 12/12 | Aprobado |
| LobbyRedesign | 12/12 | Aprobado |
| LobbyPlayerHeader | 11/15 | Cuatro expectativas comparaban píxeles con unidades del Canvas; fixture reparado, repetición pendiente |
| StyleSelectorResponsive | 1/3 | Dos casos dejaban viva la UI después de borrar su cuenta temporal; ciclo de vida reparado, repetición pendiente |

Evidencia Play: `nation-combined-integration-20261025/results/nation-header-play-initial.xml`, final 14:41:44 UTC. La tanda suma 38/44; no se acredita 44/44 antes del retest. La cobertura Edit de 263 casos se obtiene por unión de `nation-edit-initial.xml` y los casos pertinentes aprobados de `nation-history-kazuru-edit-initial.xml`; no se suman reintentos como casos nuevos ni se incluyen los siete fallos ajenos como resueltos.

El activo actual de Tauruk y el snapshot anterior tienen SHA256 idéntico `6EA41803B87D367D6CD09DDC1A202C20A80464D9A664F4E47E76100E94B5C8E7` y healthPerKill=10. La corrección del test preserva ese balance. Retest de Kazuru: 27 casos pendiente.

Compilación documentada en `nation-trophy-final-staging-20261025/verification/compile-Editor.json` y `compile-AndroidRelease.json`: 5 y 2 asambleas, respectivamente. Compilar no ejecuta pruebas ni produce un APK.

La revisión visual final espera a que Unity quede disponible tras el trabajo paralelo de Martio. Esto no bloquea documentación, revisión estática ni otros trabajos independientes. Root puede añadir futuros resultados a este bloque antes de aplicar los documentos.
<!-- MP-20261025-NATION-QA-END -->

Configuración a comprobar: Naturalis 0 → Aquarion 150 → Geothar 300 → Volkanar 450 → Campo Real 600; victoria +30 en todos los tramos y derrotas −5/−10/−15/−25/−30 según el saldo anterior a la partida. Inicio y suelo 0, sin techo de copas. Parque de Bolas (`classic`) y Grand Casino Hotel (`lounge`) quedan como cosméticos fuera del recorrido. El Hotel conserva su categoría de pago, pero no se activa IAP, cobro ni publicación. Aireola queda fuera del ladder por ahora; sus assets se conservan. El acceso a los tramos depende de las copas actuales y se vuelve a restringir al descender; no se cambian tableros bajo los actores durante combate. Protocolo vigente `mp-2026-10-25-v19-nation-ladder`; sustituye v18-nearest-duel. Solo BO5 online completo y confirmado puntúa; bots y tutorial no dan copas. Se conserva migración del historial previo sin recalcular sus ajustes, con reglas antiguas para el prefijo histórico y nuevas para resultados posteriores.

Arte aprobado e integrado, pendiente de revisión visual: Los cuatro trofeos V2 están APROBADOS e integrados: césped, agua con peces, roca y roca con lava. Sus 17 archivos de importación/UI forman parte del paquete final de 23 archivos. Las propuestas V1 no se importan. Son variantes visuales del tramo para una única cantidad de copas, no cuatro monedas o contadores. La comprobación visual final en Unity sigue pendiente.

### D. Límites y siguiente cierre

Martio continúa como trabajo paralelo en su propio chat: el creador ordenó «YA PONLO EN EL JUEGOOO». Se observaron cambios externos en producción y su integración, registro y pruebas están en curso. Este cierre no atribuye una integración terminada ni valida ese trabajo; tampoco afirma que siga fuera del juego. Los 20 personajes de Eronya NO están implementados ni asociados al roster por inferencia. El módulo 9 del tutorial sigue pausado y el 10 pendiente. No se completan por esto las 12 etapas del roadmap.

Faltan el retest de 27 casos de Kazuru, las repeticiones PlayMode de encabezado/selector, la revisión visual de composiciones móvil/PC y trofeos, dos clientes UGS reales, Android físico y medición de APK. La disponibilidad del Editor afecta a la revisión visual; las tareas independientes continúan. Completar los bloques de resultados con XML/capturas reales antes de aplicar esta documentación. No sumar pruebas ya contabilizadas ni atribuir el cierre previo a los paquetes nuevos.

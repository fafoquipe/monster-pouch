# Núcleo configurable de copas y recorrido de tableros

## Integración vigente — MP-DEC-055, 25 de octubre de 2026

**Integrada y validada localmente: 380 EditMode y 25 PlayMode únicos aprobados.** Esta sección sustituye las afirmaciones históricas de ausencia de integración con perfil, partidas y UI, sin atribuir a los cierres antiguos la validación del código nuevo.

- `LocalGuestProfile` v10 persiste copas actuales/máximas y recibos mediante `LocalGuestProfileStore.RecordTrophyResult`. `GuestTrophyLadder` proporciona la política local aprobada: inicio y suelo 0, victoria +30, derrota −5 antes de 150 o −30 desde 150 según el saldo previo. Las versiones anteriores incorporan un estado inicial de copas 0 conservando sus demás datos; no se importan partidas antiguas ni simulaciones QA/DEV.
- `OnlineRoom` emplea `mp-2026-10-25-v16-online-trophies`: genera un GUID compartido al iniciar la sala y lo valida al recibir el inicio y los resultados. `OnlineDigest` incluye el marcador BO5 en orden azul/rojo para que ambos clientes confirmen los mismos valores pese a sus perspectivas inversas.
- `MatchCompletedVerified` se emite una sola vez por identificador compartido tras comparar dos hashes válidos e iguales, únicamente para una partida online que no sea entrenamiento, en resultado final BO5 y con marcador 3–0/1/2 o su inverso. La UI vuelve a comprobar sesión local, marcador, ronda y hash confirmado antes de solicitar el recibo. Una ronda, un bot, una desconexión o un resultado incompleto no acredita copas.
- La transacción local conserva recibos idempotentes. La cola de reintento pertenece al perfil que obtuvo el resultado y no se descarta al volver al menú; `TrophySavePending` y `RetryTrophyResults` permiten tratar fallos de escritura. La cola reside en memoria: no se presenta como recuperación tras cerrar el proceso ni como confirmación de servidor.
- La UI muestra 0 cuando no hay datos y usa las copas del perfil. Campo Real exige 150 copas actuales. Un descenso retira ese acceso; si estaba seleccionado, `RefreshTrophyBoardAccess` aplica Parque de Bolas solo en menú, sin alterar el tablero bajo actores. La selección del Hotel no cambia por esta integración.

Persisten los límites de seguridad del cliente/host: no hay servidor autoritativo, emparejamiento, clasificación global, IAP ni nuevo servicio remoto. Abandono y empate carecen de una regla de puntuación aprobada. La pausa de expansión continúa salvo MP-DEC-055. Las pruebas nuevas, la revisión de UI y las pruebas físicas/remotas deben distinguirse en `QA_CHECKLIST.md`; el alcance local está validado y las comprobaciones físicas/remotas siguen pendientes.

## Antecedentes del núcleo — 22 de octubre de 2026

Integrado el 22 de octubre de 2026 a partir de decisiones expresas del creador: las victorias suman copas y las derrotas restan. La aclaración más reciente limita el recorrido actual a dos arenas gratuitas y pospone ajustar por diferencia de copas del rival. El núcleo permite ganancia plana y pérdida configurada por etapa; la factory independiente de QA suministra la prueba 0/150 aprobada, sin conectarla a cuentas o partidas. El recorrido es finito y, al llegar a la última etapa, las copas continúan creciendo. El soporte genérico para repetir tablero entre etapas no añade repeticiones a la prueba vigente. El Hotel es un cosmético de pago previsto y no forma parte de la progresión gratuita.

## Alcance implementado

`TrophyLadderEngine` es C# puro y no se conecta al combate, perfil, servidor, UI ni compras. Requiere una `TrophyLadderPolicy` y un `TrophyBoardCatalogue` explícitos. No contiene un catálogo productivo, cifras predeterminadas, fórmula comercial, rangos reales ni concesiones.

La política suministra copas iniciales, mínimo, ajustes y etapas. Una sola banda abierta expresa una ganancia/pérdida plana explícita, independiente de las copas del rival. Cada etapa puede indicar `DefeatLossOverride`, una pérdida positiva que sustituye la pérdida de esa banda; si está ausente, se usa la banda configurada. La etapa se resuelve con las copas **previas** del jugador, por lo que una derrota que desciende de rango conserva el descuento del rango desde el que empezó. El recibo registra esa etapa y su ajuste; un reintento posterior no los recalcula.

El motor conserva soporte opcional para varias bandas contiguas por diferencia entre las copas previas del rival y del jugador. En esa modalidad, las ganancias aumentan y las pérdidas de banda disminuyen entre franjas superiores. Esta modalidad no está conectada ni activa: cualquier configuración productiva por diferencia de rival requiere que el creador retome y apruebe esa decisión. Las pruebas del núcleo usan cifras, anchos e IDs sintéticos. El preset separado `TrophyLadderTrialConfiguration` contiene únicamente la configuración de prueba 0/150 aprobada.

Los campos `InitialTrophies` y `MinimumTrophies` son anulables para distinguir ausencia de configuración de un cero explícito. El núcleo no impone un mínimo de cero ni convierte un valor por defecto en balance aprobado. `MinimumRule.Unspecified` no inventa qué ocurre al cruzar el mínimo: devuelve `NeedsConfiguration` sin aplicación. La opción `ClampToMinimum` debe seleccionarse explícitamente; la factory QA la configura con mínimo 0 conforme a la prueba aprobada. Una derrota en el mínimo se registra aunque su variación sea cero, para impedir que un reintento posterior reste nuevamente. Una derrota nunca incrementa las copas.

`Record(state, receipt, expectedRevision)` acepta solo victoria o derrota con elegibilidad competitiva adjudicada. La autoridad aporta jugador, rival distinto, ID compartido de partida, evidencia, modo de origen y las copas previas de ambos. El motor no decide qué modos son competitivos ni valida por sí mismo una partida. Empates y abandonos no tienen política: devuelven `NeedsConfiguration`; elegibilidad no aprobada devuelve `NotEligible`. No se inventan premios ni penalizaciones para ellos.

La puntuación del jugador en el recibo debe coincidir con el estado vigente al aplicar un resultado nuevo. Las copas del rival no pueden estar por debajo del mínimo explícito de la política; se rechaza ese dato sin aplicar ni consumir el recibo. Repetir una partida ya contabilizada devuelve `AlreadyApplied` y el estado actual, incluso después de otras partidas; conserva el ajuste y las copas originales del rival. Alterar cualquier dato de esa adjudicación produce `Conflict`. La evidencia no se puede reutilizar para otra partida del mismo estado. Cada jugador puede registrar el mismo ID compartido de encuentro en su propio estado.

El historial conserva cada recibo y su ajuste. Al validar un estado se reconstruyen sus copas actuales y máximas desde las condiciones iniciales y los recibos. Se rechazan duplicados, revisiones incoherentes, resultados alterados y valores que no reproduzcan el historial. Las restas de puntuaciones usan aritmética exacta; exceder la representación `long` devuelve `ArithmeticOverflow`, sin saldo envuelto ni resultado parcial. Este límite técnico no es un techo de temporada o de progresión.

`PreviewStage` resuelve la etapa de las copas actuales y devuelve las etapas alcanzadas según el máximo histórico. Los rangos cubren desde el mínimo y la última etapa queda abierta: subir más copas no crea etapas ni tableros adicionales. Los datos del preview están copiados y no equipan, desbloquean, revocan ni compran tableros. La elección de apariencia sigue siendo una integración pendiente; la lista de etapas alcanzadas es evidencia histórica, no una concesión de propiedad.

## Catálogo de tableros

`TrophyBoardRole` tiene valores estables para los metadatos: `Unspecified=0`, `InitialProgression=1`, `Progression=2`, `PaidCosmetic=3`. El catálogo debe identificar exactamente un tablero inicial. La primera etapa lo usa y las copas iniciales resuelven a esa etapa. Las etapas siguientes pueden repetir tableros elegibles. Ninguna etapa puede usar un ID desconocido, `Unspecified` o `PaidCosmetic`.

Auditoría comunicada por el integrador del catálogo real:

| ID estable | Nombre vigente aprobado | Papel aprobado |
|---|---|---|
| `classic` | Parque de Bolas / BALL PIT | Tablero gratuito inicial |
| `royal` | Campo Real / ROYAL COURT | Tablero gratuito de progresión |
| `lounge` | Grand Cas Hotel / GRAND CAS HOTEL | Cosmético de pago previsto, excluido de etapas |

Estos IDs no aparecen como valores predeterminados del motor. Los metadatos y el adaptador del catálogo real se integraron junto al núcleo, manteniendo sus responsabilidades separadas. Identificar el Hotel como cosmético de pago no activa un cobro, crea un precio ni cambia por sí mismo el acceso del selector existente.

## Contratos de integración y pendientes

- El creador aprobó la prueba del 22 de octubre: inicio/suelo 0, Parque de Bolas 0–149 con +30/−5, Campo Real desde 150 con +30/−30; sin techo superior, rachas ni diferencias según rival. `TrophyLadderTrialConfiguration` devuelve explícitamente esa política; no la activa. `TrophyLadderTrialWindow`, en Tools > Monster Pouch > Ladder de prueba, permite simular resultados exclusivamente en memoria usando los assets reales del catálogo. No lee ni escribe copas de cuentas. El balance comercial y su conexión a partidas siguen pendientes.
- Modos que conceden copas, empates, abandonos y eventuales temporadas requieren decisiones adicionales. Un `SourceModeId` sirve de trazabilidad y no activa una regla de elegibilidad.
- La autoridad externa debe verificar identidades, finalización, elegibilidad y ambas puntuaciones previas. La firma de política detecta diferencias de configuración; ni ella ni un identificador de evidencia autentican al cliente o evitan fraude por sí solos.
- El propietario de la transacción debe validar y persistir el resultado con concurrencia e idempotencia. Si ambos jugadores se actualizan en un servidor, la consistencia entre ambos perfiles pertenece a esa capa. Este motor no realiza escrituras ni transacciones remotas.
- Los DTO no son un formato de guardado activo. El futuro adaptador deberá validar estructura, campos obligatorios, versión y recuperación antes de convertirlos; no debe confiar en deserialización de Unity para campos anulables.
- La firma binaria incluye la política y los roles del catálogo. Cambiarlos requiere una migración deliberada; no se reinician copas, máximos o recibos automáticamente.
- Está aprobada la elección entre tableros gratuitos alcanzados y el Hotel si se posee, al completar el recorrido. El acceso al Hotel antes del último rango, la elección al descender y las temporadas siguen pendientes. Este núcleo no introduce ninguna de esas reglas ni equipamiento automático.
- No se implementan clasificaciones globales, arte, rarezas comerciales, recompensas de etapa ni personajes.

## Verificación

Pasaron **74/74 casos** con las fuentes C# exactas y sus tests usando el ensamblado NUnit del proyecto en un proceso independiente, sin Unity ni adaptadores JSON. El resumen y hashes se conservan en `ladder-engine-staging/verification/standalone-results.json` como evidencia previa.

La ejecución posterior dentro de Unity pasó **91/91 EditMode**, sin fallos ni omitidos, el 22 de octubre de 2026 entre las 20:25:12 y 20:25:14 UTC. Incluye **74 del núcleo, 10 del catálogo real y 7 de la configuración de prueba**. Informe: `Logs/roadmap-v1.2/ladder-trial-edit-20261022.xml`, con copia de trabajo en `ladder-board-integration-20261022/results/`. Esta tanda valida el código integrado y los assets del catálogo; no demuestra una clasificación online activa ni una inspección visual de la ventana.

La regresión de integración de escenas pasó **3/3 PlayMode** el mismo día entre las 20:29:59 y 20:31:00 UTC, sin fallos ni omitidos: `TableSkinsPlayTests` 2/2 y `LobbyThemeSelectionPlayTests` 1/1. Informe: `Logs/roadmap-v1.2/ladder-boards-play-20261022.xml`. Conserva selección/persistencia de tableros y separación entre apariencia del lobby y mapa de combate. Fue una ejecución batchmode, sin inspección visual ni partidas competitivas reales; precede al ajuste posterior de áreas táctiles.

Cobertura: ausencia de defaults jugables, configuración inválida, banda plana, pérdidas por etapa previa, promoción/descenso/reintento, bordes de franjas opcionales, rival más alto, victoria/derrota, máximo histórico y descenso, recorrido finito, repetición de tableros, exclusión de pago, conflicto de payload/puntuación/revisión/identidad/evidencia, dos jugadores con un ID compartido, empates/abandonos sin política, mínimo pendiente o explícito, derrota de cero copas idempotente, límites de representación, historial alterado, futura versión, cambio de política, orden del catálogo y aislamiento de referencias.

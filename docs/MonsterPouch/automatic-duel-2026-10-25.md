# DUELO automático — implementación preparada el 25 de octubre de 2026

Estado actualizado: DUELO integrado y validado localmente antes de naciones. El cierre local anterior a las naciones registra 409 EditMode aprobados (303 online/cercanía + 106 perfiles/estuches/arte) y 21 PlayMode únicos aprobados. La primera tanda Play terminó 20/21; la repetición aislada del fixture de arte pasó 2/2 y no se suma dos veces. Evidencia: `online-pouches-cleanup-checkpoint-20261025.json`. Estos resultados no validan las naciones, el nuevo ladder, el encabezado ni el salto de Kazuru. El nuevo protocolo v19 y el recorrido actual tienen su cierre pendiente en QA_CHECKLIST.md. No se probaron dos clientes con UGS real ni se modificó el Dashboard.

## Comportamiento

Pulsar DUELO en el selector inicia la búsqueda. JUGAR con DUELO seleccionado también la inicia. Durante la búsqueda se muestra únicamente el Monster seleccionado, «Buscando» y CANCELAR, sobre un fondo opaco. No hay códigos, lista de salas, Whelps, rival ficticio ni botón Iniciar. Al conectar un rival válido se utiliza el inicio online existente automáticamente.

AMIGO conserva crear sala, compartir código, unirse e iniciar manualmente. Los errores del DUELO presentan Reintentar y cerrar; no abren el menú de AMIGO. Cerrar la pantalla, poner la aplicación en pausa, abandonar o destruir el componente cancela la búsqueda. Una nueva búsqueda espera a que termine la operación anterior y su limpieza; la pantalla puede mostrar «Cancelando…» durante ese cierre.

## Proyecto auditado y compatibilidad

- Proyecto leído: `C:/Users/HP VICTUS/Juegos Unity/mini-gogos/game/monster-pouch`.
- Staging: `C:/Users/HP VICTUS/OneDrive/Documentos/ChatGPT/monsters-pouch/automatic-duel-staging-20261025`.
- Paquete instalado: `com.unity.services.multiplayer` 2.3.3, caché `com.unity.services.multiplayer@3bd2e24872f4`.
- Ya contiene `Unity.Services.Lobbies`, incluyendo consultas, orden, paginación, miembros, bloqueos, heartbeat y limpieza. La asamblea local ya referencia `Unity.Services.Multiplayer`: no se cambia ningún paquete ni asmdef.
- Existe `cloudProjectId` en la configuración local. Su valor no se incluye en los artefactos.
- Protocolo vigente: `mp-2026-10-25-v19-nation-ladder`, común a marca frontal, cercanía, DUELO y nuevo recorrido. Sustituye v18. Conserva perfil v10 y migra explícitamente el historial de ladder sin recalcular ajustes antiguos.

El protocolo y el hash de configuración/reglas existentes forman la clave de compatibilidad. No se filtra por tablero: cada jugador mantiene el tablero que el flujo existente permite seleccionar.

## Descubrimiento y reserva

Se utiliza Lobby para descubrimiento y reserva; Relay y Netcode conservan su conexión y simulación existentes. No se usa una cola de Matchmaker ni se crea infraestructura externa.

Cada sala pública de DUELO tiene dos plazas. Los índices son S1=mecanismo de DUELO, S2=compatibilidad, S3=anfitrión y N1=copas del anfitrión. Para un jugador nuevo se consulta el candidato más cercano con copas menores o iguales y el más cercano con copas mayores; se comparan las diferencias absolutas. Las consultas se ordenan en el servicio, desactivan el muestreo aleatorio y siguen la continuación cuando una página no contiene candidatos utilizables. No hay franjas de copas, ampliaciones por tiempo, filtros de tablero ni sustitución por bots.

La consulta y el ingreso son operaciones distintas: los resultados pueden quedar obsoletos. La admisión al segundo cupo la decide `JoinLobbyByIdAsync` en el servicio. Una sala llena, bloqueada, eliminada o en conflicto lleva a volver a consultar. Nunca se considera la lista consultada como prueba de reserva.

Si dos búsquedas ven la cola vacía y ambas crean sala, solo el anfitrión de la sala más reciente puede migrar a una anterior (fecha del servicio, desempate por ID). Entre las anteriores disponibles elige la más cercana en copas. Primero bloquea su sala y examina la membresía devuelta: si un rival alcanzó a reservarla, conserva ese encuentro. Si sigue solo, reserva la otra y elimina la propia. Esto evita migraciones circulares y salas separadas indefinidamente. Durante ese cambio puede existir brevemente una membresía en la sala propia bloqueada y otra en la sala de destino; no hay dos salas propias abiertas.

La selección corresponde a los candidatos observados en consultas sucesivas. No promete una optimización global de todas las parejas que lleguen simultáneamente. El orden de antigüedad solo resuelve la carrera entre anfitriones que ya crearon sala; no impone una distancia máxima de copas.

El cliente reservado aporta un token aleatorio con visibilidad Member. El anfitrión bloquea la sala, abre Relay y publica el código con visibilidad Member. Netcode exige protocolo, balance y ese token para aceptar al rival de DUELO. No se muestra ni publica el código en la interfaz. AMIGO sigue usando el formato de aprobación anterior.

## Cancelación, limpieza y reintento

Los resultados tardíos de crear/unirse se registran antes de observar la cancelación, de modo que puedan eliminarse o abandonarse. Una única búsqueda permanece activa hasta finalizar sus tareas. Las continuaciones canceladas no crean un nuevo NetworkManager. Se espera al cierre de Netcode antes de reutilizar la conexión.

El anfitrión envía heartbeat cada diez segundos. Los accesos frecuentes de consulta/ingreso/lectura se separan al menos 1,1 segundos; crear se separa 3,1 segundos. Estas pausas respetan las cuotas del servicio, no condicionan la elegibilidad por copas. Las reservas o conexiones incompletas tienen un límite de cuarenta segundos; se liberan y se reintenta. La espera sin rivales no tiene un tiempo de corte ni inventa oponentes. Tres fallos consecutivos de conexión presentan el error y permiten reintentar manualmente.

Las salas se borran al completar el emparejamiento o cancelar; los clientes abandonan su membresía. La limpieza intenta tres veces, acepta «ya no existe» y refresca la versión cuando el SDK usa If-Match. Al iniciar y después de un intento interrumpido se revisan las membresías de este mecanismo para resolver respuestas ambiguas y restos de un proceso anterior. Una recuperación fallida impide crear otra sala en el próximo intento. Si el proceso se cierra sin red, la eliminación inmediata no puede garantizarse: el heartbeat se detiene y se depende de la caducidad de Lobby, además de la recuperación al volver.

## Copas

`ReadAutomaticDuelTrophies` recarga la cuenta y exige un perfil válido, sin recuperación pendiente ni resultado de copas pendiente de guardar. Lee `AccountTrophies`, que procede de `LocalGuestProfile.Trophies` (perfil v10). No usa PlayerPrefs, XP, monedas ni puntuaciones de prueba.

El parche no modifica el motor, almacenamiento, reglas ni deduplicación de copas. `CompareResults`, `MatchCompletedVerified`, el identificador de partida y `RecordVerifiedTrophyMatch` siguen premiando únicamente un BO5 online completo con resultado confirmado. Bots, tutorial, rondas sueltas, cancelaciones y desconexiones no generan un nuevo resultado válido. La actualización posterior del ladder aplica: Naturalis 0 → Aquarion 150 → Geothar 300 → Volkanar 450 → Campo Real 600; victoria +30 en todos los tramos y derrotas −5/−10/−15/−25/−30 según el saldo anterior a la partida. Inicio y suelo 0, sin techo de copas. El nuevo inicio automático llama al mismo `StartRoom` protegido contra un segundo inicio.

Se mantiene el modelo de confianza ya existente: copas locales y verificación entre pares, sin servidor autoritativo ni nuevo sistema antitrampas.

## Archivos y entrega

Se modifican tres archivos existentes: `OnlineRoom.cs`, `local-online-menu.cs` y `local-lobby-redesign.cs`. Se añaden `AutomaticDuelSearch.cs`, `UnityDuelDirectory.cs`, `OnlineRoom.Duel.cs`, `local-automatic-duel.cs` y sus `.meta`. La implementación de copas y la geometría de Tauruk quedan fuera del manifest.

`apply-manifest.json` tiene entradas `{path, original, source, hash}` para `integrate-approved-patch.ps1`. `original` corresponde al SHA256 del archivo de proyecto observado; para archivos nuevos es null. `source` es absoluta dentro del staging y `hash` fija el contenido entregado. Las copias originales están en `before/`. Si cambia otro chat uno de los tres destinos, la validación debe detener la integración: hay que fusionar, no sustituir hashes para forzarla.

Comprobaciones reproducibles desde el workspace:

```powershell
& .\automatic-duel-staging-20261025\verification\run-offline-tests.ps1
& .\automatic-duel-staging-20261025\verification\verify-invariants.ps1
& .\automatic-duel-staging-20261025\verification\compile.ps1
& .\automatic-duel-staging-20261025\verification\compile.ps1 -RuntimeOnly -Variant AndroidRelease
& .\integrate-approved-patch.ps1 -Manifest automatic-duel-staging-20261025/apply-manifest.json -Record automatic-duel-integration-20261025
```

La última línea valida sin escribir en el proyecto. Es un comando de la preparación histórica; la integración posterior sí está registrada en sus manifiestos, sin que este bloque sea evidencia de una ejecución nueva.

Pruebas offline: 21 casos del orquestador real, con cancelación antes/durante crear, reservar, publicar y conectar; reentrada; compatibilidad; selección cercana/lejana; carreras de bloqueo; recuperación de salas llenas; heartbeat; fallo de limpieza; y dos, tres y cuatro jugadores concurrentes contra un directorio simulado con reserva atómica. Compilación Roslyn con referencias reales del proyecto para Editor (incluye asambleas de pruebas existentes) y AndroidRelease. Compilar una asamblea de pruebas no equivale a ejecutarla. Los resultados quedan en `verification/offline-results.txt` y `verification/compile-*.json`.

La preparación original no ejecutó llamadas reales a Lobby/Relay, PlayMode, build IL2CPP/APK ni inspección visual en Unity. Tras integrar, la tanda compartida EditMode aprobó 303/303; el cierre local posterior compartido con perfiles/estuches/arte suma 409 EditMode y 21 PlayMode únicos, con capturas revisadas. Es anterior a naciones y no valida el protocolo v19 ni sus assets. Véase QA_CHECKLIST.md. Continúan pendientes de validación real: dos identidades Authentication distintas, entrada directa desde DUELO y desde JUGAR, cancelar durante conexión, volver a entrar, tres clientes compitiendo por una plaza, pausas/cierre de proceso, versiones incompatibles, tableros distintos, AMIGO manual y un BO5 completo con relectura de copas en ambos dispositivos.

## Requisito externo pendiente

Debe comprobarse que Lobby permite operaciones de cliente en el mismo proyecto/entorno que Authentication y Relay. La configuración local y el paquete no permiten confirmar su habilitación remota. Si UGS devuelve 403 o requiere habilitar Lobby, aceptar condiciones o modificar facturación, esa acción necesita autorización del creador. No se hizo ninguna de esas operaciones. No hace falta crear una cola de Matchmaker, desplegar Cloud Code ni cambiar el Dashboard para definir índices de esta implementación.

## Fuentes revisadas

- [API oficial de Lobby](https://docs.unity.com/en-us/oas-lobby/1.0.0): consultas ordenadas, filtros, continuación, ingreso por ID, datos de miembros y operaciones de sala.
- [Límites oficiales de solicitudes](https://docs.unity.com/en-us/mps-sdk/rate-limits): cuotas por operación usadas para el espaciado local.
- [Glosario oficial de Multiplayer](https://docs.unity.com/en-us/mps-sdk/glossary): salas inactivas, membresía y datos privados/de miembros. Una sala sin heartbeat deja de aparecer tras su vida activa; tras una hora inactiva puede ser eliminada.
- [Buscar e ingresar a una sesión](https://docs.unity.com/en-us/mps-sdk/join-session): descubrimiento público y entrada por identificador; se revisó la alternativa Sessions. Se conservó la API Lobby incluida en 2.3.3 para no sustituir la conexión Relay/NGO ya existente.
- Código instalado: `Runtime/Lobbies/SDK/ILobbyService.cs`, `QueryLobbiesOptions.cs`, `CreateLobbyOptions.cs`, `UpdateLobbyOptions.cs`, `WrappedLobbyService.cs`, `Exceptions/LobbyExceptionReason.cs`, `Models/QueryFilter.cs`, `QueryOrder.cs`, `Lobby.cs`, `Player.cs`, `DataObject.cs` y `PlayerDataObject.cs`.

Consulta de documentación realizada el 25 de octubre de 2026. Varias URLs antiguas de Lobby redirigen al portal MPS; las firmas usadas se contrastaron con el paquete 2.3.3 instalado y la compilación local.

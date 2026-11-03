# Habilidades permanentes del perfil invitado

Este contrato registra habilidades permanentes mediante recibos explícitos en el perfil invitado local. No concede habilidades al crear una cuenta, no decide cuáles son iniciales y no cambia las mejoras compradas durante una partida. Tampoco crea una cartera, entrega premios ni activa máquinas o servicios.

El perfil invitado sigue siendo un guardado local, sin autoridad comercial. Estos recibos no convierten el cliente en una fuente confiable para compras reales.

## Datos y APIs

El esquema `LocalGuestProfile` v5 añade la lista `AbilityUnlocks`. Cada `GuestAbilityUnlockReceipt` contiene:

| Campo | Uso |
| --- | --- |
| `ReceiptId` | Identificador estable de la concesión explícita; no puede repetirse en los recibos de personajes o habilidades del perfil. |
| `CharacterId` | ID persistente del personaje que posee la habilidad. |
| `AbilityId` | ID persistente exacto de una entrada `Tricks[].Id` de ese personaje. |
| `Source` | Procedencia existente `GuestUnlockSource`: `Tutorial`, `NormalPouch`, `Machine` o `Mastery`. Etiquetar una procedencia no implementa ni autoriza por sí solo un premio. |
| `TimestampUtc` | Fecha de registro de la operación. |

Se utilizan los IDs del catálogo actual. Los nombres visibles, posiciones de las habilidades y `EffectId` no sustituyen a `Tricks[].Id`.

### Consultar el perfil

- `GetOwnedAbilityIds(string characterId)` devuelve una copia de los IDs registrados para ese personaje.
- `OwnsAbility(string characterId, string abilityId)` consulta esos recibos; no infiere habilidades a partir de las mejoras de partida.

La lista vacía significa **que no hay recibos permanentes registrados**. No representa una decisión sobre las habilidades iniciales, que siguen pendientes de definición. El acceso y las reglas de combate existentes no se modifican mediante estas consultas.

### Registrar una concesión explícita

```csharp
LocalGuestProfileStore.UnlockResult UnlockAbility(
    MatchConfig catalogue,
    string receiptId,
    string characterId,
    string abilityId,
    GuestUnlockSource source);
```

El llamador debe aportar un premio previamente autorizado y su identificador estable. Ningún menú, tutorial, máquina ni inicialización invoca automáticamente esta operación.

Para aceptar la operación, el perfil debe usar `StarterInventory`, tener su elección inicial registrada y poseer el personaje. El catálogo debe identificar sin ambigüedad tanto el personaje como la habilidad. Si un ID ya registrado dejó de existir en el catálogo recibido, se bloquean nuevas mutaciones de colección; no se elimina propiedad para adaptarse a un catálogo incompleto.

| Resultado | Significado |
| --- | --- |
| `Unlocked` | Se persistió un recibo nuevo y se incrementó una vez la revisión del perfil. |
| `AlreadyApplied` | El mismo recibo ya tiene exactamente el mismo personaje, habilidad y procedencia; no se vuelve a escribir. |
| `AlreadyOwned` | La habilidad ya era propia por otro recibo; no se añade otra concesión. |
| `Conflict` | El recibo existe con otro contenido o pertenece a una concesión de personaje. |
| `CharacterLocked` | El personaje no figura como propio; la operación no lo desbloquea implícitamente. |
| `InvalidRequest` | Faltan datos, el ID no corresponde al personaje, el catálogo es ambiguo para el premio solicitado, la procedencia es inválida o el recibo está reservado. |
| `InvalidCatalogue` | El catálogo recibido no reconoce íntegramente la colección ya registrada. |
| `LegacyProfile` | No existe una colección autoritativa inferible para esta cuenta anterior. |
| `StarterRequired` | Falta la elección inicial. |
| `RecoveryRequired` | Se leyó una generación de respaldo; las mutaciones permanecen bloqueadas. |

No tener un perfil guardado produce `InvalidOperationException`. Las excepciones de acceso o escritura se propagan para permitir un reintento explícito. Una versión futura desconocida se rechaza mediante `NotSupportedException` y no se sustituye por una generación anterior.

## Migración y conservación

- **v4 → v5:** se conserva exactamente `CollectionMode`, junto con identidad, revisión, elección inicial, recibos de personajes y progreso del tutorial. Se incorpora una lista vacía de recibos de habilidades.
- **v1–v3 → v5:** se conserva la regla de compatibilidad previa que migra esas cuentas a `Legacy`; no se les atribuye el inventario de una cuenta nueva.
- Abrir o recargar un perfil antiguo migra únicamente en memoria. No reescribe el archivo ni modifica mazos.
- Una posterior mutación válida guarda el esquema actual mediante el mecanismo existente de archivo temporal, reemplazo atómico y respaldo de la generación anterior.
- Una versión anterior no puede introducir recibos de habilidades v5 durante la migración. En un archivo v5, que falte la lista obligatoria es un dato inválido, no una autorización para reconstruir propiedad vacía.

La presencia se comprueba en el JSON original, antes de confiar en las listas del objeto deserializado. Unity puede convertir un campo omitido o `null` en una lista vacía; por ello, un sentinel `null` por sí solo no demuestra que la colección estuviera guardada.

| Lista obligatoria en la raíz | Desde la versión |
| --- | --- |
| `StarterSelections` | v1 |
| `CompletedTutorialModules` | v2 |
| `CharacterUnlocks` | v4 |
| `AbilityUnlocks` | v5 |

Cada campo obligatorio debe ser un array explícito. `null`, otros tipos, claves duplicadas, nombres con otra capitalización o coincidencias dentro de strings u objetos anidados no satisfacen este contrato. A los perfiles históricos no se les exigen listas que todavía no existían en su versión. Si incluyen listas vacías de recibos posteriores, estas no conceden propiedad; introducir recibos de personajes o habilidades antes de la versión que los define se rechaza durante la migración.

El parser Newtonsoft ya presente en el proyecto valida la estructura; no se añade un paquete. Unity recibe después las mismas claves decodificadas que superaron esa validación, incluidas las escritas con escapes JSON. Los timestamps se mantienen como strings, sin conversión de fechas, para conservar exactamente los recibos. Leer no modifica los bytes originales. Los errores del parser se traducen a `InvalidDataException`, conservando el comportamiento de recuperación del store.

No se infiere propiedad de habilidades o personajes a partir del acceso legado al catálogo, la selección de un mazo, las mejoras de partida ni datos internos de QA.

## Recuperación, concurrencia y reintentos

Cada concesión adquiere el bloqueo del archivo y recarga la última generación antes de comprobar recibos y propiedad. Así, una instancia antigua del store no sobrescribe concesiones que otra instancia haya confirmado.

Si el bloqueo o el reemplazo fallan, no se publica un recibo en memoria como si estuviera confirmado. El llamador puede reintentar con el mismo `receiptId`. El registro idempotente impide repetir una concesión que sí se hubiese persistido.

Si el archivo principal está dañado y el respaldo es válido, `NeedsRecovery` queda activo. Las concesiones se bloquean para evitar repetir premios ausentes en ese respaldo. Los archivos originales se conservan. No se permite que una versión futura desconocida provoque una recuperación automática hacia datos antiguos.

## Adaptador de propiedad para máquinas

```csharp
GuestMachineOwnershipStatus GuestMachineOwnershipAdapter.TryCreate(
    LocalGuestProfile profile,
    MatchConfig catalogue,
    MachineDefinition machine,
    out MachineOwnership ownership);

GuestMachineOwnershipStatus LocalGuestProfileStore.ReadMachineOwnership(
    MatchConfig catalogue,
    MachineDefinition machine,
    out MachineOwnership ownership);
```

El adaptador puro valida el perfil y la correspondencia exacta de las tres habilidades de la máquina con el personaje del catálogo. Devuelve únicamente `CharacterOwned` y los IDs de habilidades registrados para ese personaje. Los arrays de salida son copias; modificar el resultado no modifica el perfil, el catálogo ni una lectura posterior.

Para cuentas `Legacy` devuelve `LegacyUnavailable`, sin atribuir propiedad. También rechaza perfiles inválidos, cuentas sin elección inicial, catálogos incompletos y máquinas incompatibles. En esos casos la salida es `null`.

Los consumidores que leen del disco deben utilizar `ReadMachineOwnership`: recarga bajo bloqueo y devuelve `RecoveryRequired` si los datos proceden del respaldo. Una llamada directa al adaptador puro no conoce el estado de recuperación del store que originó su objeto.

La consulta no añade recibos, no consume extracciones y no modifica el estado de la máquina. El motor puro puede utilizar el resultado para reconocer premios externos ya poseídos y calcular sus exclusiones y garantías.

## Límite transaccional

El reemplazo atómico protege **el perfil invitado individual**. No proporciona una transacción conjunta entre ese perfil, el estado de una máquina y una cartera.

Por ello, encadenar una extracción, `UnlockAbility` y un cobro no basta para garantizar una operación económica indivisible. Un futuro coordinador debe persistir conjuntamente el resultado de la máquina, el débito y la concesión antes de presentar el premio como confirmado. Esta entrega no implementa ese coordinador ni conecta los créditos de QA con el perfil del jugador.

## Validación de esta entrega

**50/50 casos aprobados en el harness independiente actualizado**, con las aserciones de la biblioteca NUnit del proyecto: los 30 anteriores y 20 regresiones adicionales de estructura y compatibilidad. Se verificaron migración, conservación, requisitos de propiedad, IDs exactos, colisiones de recibos, reintentos, acceso concurrente, fallo de reemplazo en Windows, recuperación, versiones desconocidas, catálogos incompletos, aislamiento de objetos y adaptación al cálculo de probabilidades sin consumir una extracción.

La primera ejecución en Unity detectó que una lista omitida podía convertirse en vacía y permitir una concesión. Ese fallo se conserva como evidencia; no se considera validado por el resultado anterior 30/30 del harness. El harness actualizado reproduce expresamente esa materialización y demuestra que la fuente anterior falla la regresión, mientras la reparación la supera. Los nuevos casos incluyen `null`, campos en strings u objetos anidados, duplicados, campos obligatorios por versión, migraciones v1–v4 y una clave escapada con un recibo no vacío que debe conservarse y seguir siendo idempotente.

El harness usa el código real de `DocumentedRoster` y los campos reales de `UnitDefinition`/`TrickDefinition`, pero sustituye `JsonUtility` por serialización de campos con `System.Text.Json` y utiliza adaptadores mínimos de `Resources`/`MatchConfig`. En su copia de la prueba, las rutas temporales se trasladan al workspace permitido. Estos resultados **no equivalen a ejecutar Unity, sus assets ni su `JsonUtility` real**.

La revisión independiente de la reparación final y sus pruebas no encontró un defecto concreto que impidiera integrar. La ejecución posterior en Unity aprobó **280/280 EditMode**, sin fallos, el 22 de octubre de 2026 entre las 19:48:05 y 19:48:38 UTC. Incluye los **50/50 casos de `GuestAbilityTests`** con el `JsonUtility` real, además de las regresiones de perfiles, mazos, tutorial y máquinas de la tanda enfocada. Informe: `Logs/roadmap-v1.2/guest-machine-serialization-fix-edit.xml`. Este resultado cierra el fallo de estructura detectado en la tanda histórica 256/258, cuyo XML se conserva sin alterar.

La revalidación posterior pasó **18/18 PlayMode**, sin fallos, el mismo día entre las 19:51:31 y 19:52:50 UTC. Incluye los 16 casos de selección inicial/colección y los dos de perfil invitado. Informe: `Logs/roadmap-v1.2/guest-v5-storage-final-play.xml`. Verifica esos flujos con la reparación integrada; no valida servicios, compras ni concesiones comerciales.


## Acceso jugable MP-DEC-049 — 2026-10-24

La cuenta nueva no posee ninguna de las tres mejoras. La habilidad base permanece automática. El snapshot inmutable por partida contiene IDs de habilidades poseídas; la Maleta crea 1 + mejoras desbloqueadas copias, contando principal y banco. Compras explícitas, compras automáticas, fusiones y replay rechazan mejoras bloqueadas. Las fusiones seleccionan la primera mejora desbloqueada pendiente. Los costes temporales de Moonkens y reembolsos se conservan. Tutorial efímero y bots mantienen su acceso anterior. Online intercambia ambos snapshots y los incluye en digest, protocolo v15; esto permite validación determinista, no constituye autoridad de servidor.

# Perfiles y economía: primera base técnica

La cartera y el inventario de la etapa 2 permanecen **aislados para QA**, sin economía de jugadores reales. XP, mazos e identidad invitada usan almacenes locales separados integrados en el juego; tampoco son autoridad comercial ni cuentas vinculadas.

## Implementado

- Esquema versionado con ID persistente, revisión, inventario por IDs, pasos de tutorial y copia del JSON de progreso anterior.
- Earthcoins, Suncoins y Saturncoins separadas. Moon-Ken permanece en `PouchState`; no se convierte en moneda persistente.
- Orígenes explícitos: QA, gratuito de jugador y comprado. El almacén local QA solo acepta el primero; no ofrece un método para acreditar compras ni transferir saldos a jugadores.
- `starterSaturncoins = 1` como referencia provisional ya autorizada para pruebas. No se activa automáticamente y cambiar su valor no vuelve a entregar una bienvenida ya registrada.
- Operaciones con ID, saldo anterior/posterior y fecha; reintentos idempotentes y conflictos rechazados. Un gasto sin saldo no escribe ningún registro.
- Escritura temporal con flush y reemplazo atómico; respaldo de la generación anterior, checksum para corrupción accidental y bloqueo exclusivo de escritor.
- Si el principal está corrupto, el respaldo puede consultarse pero **no se permiten nuevas operaciones**: podría faltar un débito o recibo de bienvenida. Una versión futura tampoco se sobrescribe ni degrada.
- Las claves PlayerPrefs anteriores se conservan. La copia en el perfil QA no sustituye la progresión jugable ni cambia personajes/mazos disponibles.
- Histórico del perfil QA v2: elección inicial entre Bugrum y Pombú (IDs `bugaloo`/`popow`), con Atrik, Blotun, Bivui, Floru, Gochar, Trimar y Atron. Los recibos de esa composición se conservan; las elecciones nuevas usan los tres Whelps de MP-DEC-033. Se guarda un único recibo y un mazo inicial; repetir la elección no concede duplicados y cambiar el Monster del recibo se rechaza. Esto no limita futuras modificaciones del mazo jugable. Los IDs se conservan; nombres vigentes en CHARACTER_NAMES.md.
- Los perfiles v1 se leen y migran en memoria sin inventar una elección. El archivo original se conserva hasta una operación válida y queda como respaldo al guardar la versión actual. Una lista vacía representa la elección pendiente, evitando los objetos vacíos que Unity crea al serializar clases nulas.

## Probar en Unity

Abrir `Tools > Monster Pouch > Roadmap > Perfil de pruebas`.
Habilitar economía QA, abrir/crear el perfil y pulsar la concesión de bienvenida dos veces: la segunda devuelve `AlreadyApplied` y el saldo se mantiene.
Los botones leen el nombre actual del catálogo: «Elegir Bugrum» y «Elegir Pombú». Registran la elección inicial y muestran los Whelps del recibo guardado por sus nombres actuales: tres en una elección nueva o siete en los recibos históricos. No modifican las claves de los mazos que ya usa el juego.

Los archivos se guardan en `Application.persistentDataPath/RoadmapQA/qa-profile.json` y su respaldo. La ventana es exclusiva del Editor y comienza con QA deshabilitado. Los tests usan directorios temporales separados.

## Límites explícitos

El checksum no impide trampas de quien puede modificar archivos. Esta identidad local no garantiza una bienvenida única tras reinstalación, cambio de dispositivo o vinculación de cuentas. Se necesita autoridad persistente de servidor antes de entregar moneda real o habilitar máquinas para jugadores.

No se inventaron premios de tutorial, paquetes de recursos, probabilidades comerciales ni conversión de créditos QA. No hay tienda real, pagos, anuncios ni servicios contratados.

La etapa 2 completa también necesita perfil de jugador vinculado, reconciliación y onboarding con saldo real gratuito, antes de activar etapas dependientes. Las pruebas de este prototipo no equivalen a validar esos servicios.

## Progreso jugable local: XP y recibos BO5

Se añadió `LocalProgressStore` al flujo real de `LocalGameUI`. La regresión de lógica pasó 994/994. La global PlayMode terminó 156/157 por el manejo del principal corrupto sin respaldo; el fallo está corregido y el cierre enfocado pasó 40/40 EditMode y 10/10 PlayMode, incluidos los cuatro casos de Progreso. Este almacén es independiente de la cartera QA y no concede monedas.

- Primer acceso: importa el JSON existente de `monster-pouch.progress.v1`, con sus niveles, XP, desbloqueos y recibos. Conserva la clave original sin modificarla y una copia exacta dentro del archivo nuevo.
- Destino: `Application.persistentDataPath/PlayerProgress/progress.json`, más `progress.backup.json`. El formato tiene versiones explícitas, revisión y checksum para detectar corrupción accidental.
- Un BO5 guarda su XP y su recibo juntos. Cada operación vuelve a cargar la última generación bajo un bloqueo exclusivo; una instancia antigua no puede sobrescribir una más reciente.
- Los recibos dejan de eliminarse tras 128 partidas. Esto evita repetir recompensas de los recibos conservados; no puede reconstruir IDs que versiones anteriores ya hubieran borrado.
- Una escritura completa se prepara en un temporal, se fuerza a disco y reemplaza el archivo anterior conservándolo como respaldo. Un temporal incompleto no se importa automáticamente.
- Si el principal está corrupto o desaparece pero hay respaldo, este se muestra en modo de solo lectura: podría faltarle un recibo confirmado. No se reinicia el nivel ni se repiten premios. Datos futuros tampoco se degradan.
- La pantalla de Progreso informa si no se pudo cargar/guardar y permite reintentar. El resto de la partida no borra ni sustituye los datos que fallaron. Un error de almacenamiento no permite prometer persistencia de resultados aún no confirmados.
- Los resultados que no se pudieron guardar se mantienen pendientes en memoria durante esa sesión, incluso al volver al menú. Reintentar vuelve a comprobar el recibo en disco y confirma cada resultado una sola vez; cerrar el proceso antes de resolver un fallo de almacenamiento puede perder los resultados aún no confirmados.
- Las pruebas de escena sustituyen el directorio antes de cargar el juego y usan archivos temporales; no escriben el archivo de progreso real del jugador.

Sigue siendo guardado local de desarrollo, editable por quien controla el dispositivo, sin recuperación garantizada tras reinstalar y sin autoridad comercial. No se modifica el balance de XP ni se crean recompensas nuevas.

## Evidencia de la corrección de corrupción

`InvalidDataException` no hereda de `IOException`. Se maneja explícitamente al cargar y actualizar Progreso, y al recuperar/validar los archivos de los almacenes local y QA. Así se mantiene el error visible sin crear progreso vacío, y un checksum incorrecto puede activar el respaldo de solo lectura. `NotSupportedException` continúa sin degradación: una versión futura no se sustituye por un respaldo antiguo.

Los informes globales se conservan como evidencia anterior a la corrección: `phase2-progress-storage-final-edit.xml` (994/994) y `phase2-progress-storage-global-play.xml` (156/157). Las regresiones adicionales de checksum pasaron dentro de las 40/40 pruebas enfocadas de `phase2-recovery-focused-edit.xml`; el caso de UI corregido y los otros tres casos de Progreso pasaron dentro de las 10/10 de `phase2-recovery-focused-play.xml`. Informes en `Logs/roadmap-v1.2`; detalle en QA_CHECKLIST.md.
## Inventario QA v3

El perfil v3 añade `OwnedAbilities` y `InventoryReceipts`. Cada habilidad se identifica por el par de IDs estables de personaje y mejora del catálogo actual; no se deduce del nombre visible, índice de casilla ni `EffectId`.

- `GrantInventory` requiere QA habilitado y un perfil InternalQA. Solo acepta un personaje existente o una de sus tres mejoras actuales; la habilidad principal no se trata como mejora desbloqueable.
- No permite conceder una mejora de un personaje que el perfil todavía no posee. La elección inicial anterior conserva sus ocho personajes, sin volver a concederlos ni inventar habilidades iniciales.
- El recibo contiene identificador, procedencia, personaje, habilidad opcional y fecha. Repetir la misma operación devuelve `AlreadyApplied`; reutilizar el recibo para otro contenido o procedencia devuelve `Conflict`.
- La propiedad es compartida entre procedencias: un contenido ya obtenido devuelve `AlreadyOwned`, sin otro recibo, cobro, saldo ni modificación del archivo. Las etiquetas Machine, NormalPouch, Mastery y Tutorial son referencias de pruebas; no activan esos sistemas ni sus premios.
- Los perfiles v1/v2 migran en memoria con listas nuevas vacías. Conservan identidad, saldo, elección y registros anteriores; el original no se reescribe hasta una operación válida y queda respaldado.
- La herramienta `Tools > Monster Pouch > Roadmap > Perfil de pruebas` permite inspeccionar y conceder contenido QA explícitamente, mostrando nombres actuales. No modifica la colección jugable ni entrega moneda.

Este inventario no resuelve la decisión pendiente de habilidades iniciales ni la propiedad que se reconocerá a jugadores existentes. No convierte selección de mazo en propiedad.

## Mazos locales versionados

`LocalDeckStore` conserva las tres ranuras de mazo y la selección activa que el proyecto ya utiliza. No introduce mazos adicionales ni cambia las reglas de compra, colección o colocación.

- Primer acceso: importa el JSON de mazos y conserva una copia exacta junto al Monster/Whelps elegidos anteriormente. No elimina ni reescribe las claves PlayerPrefs originales.
- Destino: `Application.persistentDataPath/PlayerDecks/decks.json`, `decks.backup.json` y bloqueo de escritor. Esquema explícito, revisión, checksum, temporal con flush y reemplazo atómico.
- Guardar exige la revisión esperada. Un cambio de otra sesión devuelve conflicto y la UI vuelve a cargar ese estado; no sobrescribe silenciosamente la elección más reciente. Repetir un guardado idéntico no cambia revisión ni rota respaldos.
- Un principal corrupto o ausente con respaldo permite lectura, pero bloquea nuevas escrituras. Una versión futura se conserva sin degradación.
- Si temporalmente falta un personaje del catálogo, los IDs originales se mantienen en disco y se impide guardar una versión que los borre. La vista jugable puede omitir la referencia no disponible hasta recuperar el catálogo.
- La UI muestra fallo, respaldo, conflicto o catálogo incompleto y permite reintentar. Un cambio que falla al guardar conserva su intención pendiente en memoria durante esa sesión y restaura la vista confirmada. Cerrar el proceso durante un fallo no garantiza conservar esa intención todavía no confirmada.
- Los tests sustituyen el directorio antes de cargar escenas para aislar los mazos reales del jugador.
- Desde el esquema de mazos v2, `ApplyStarterSelection` registra el primer mazo y su marcador de aplicación juntos en una escritura atómica. Exige el recibo invitado válido y revisión cero para una aplicación nueva; repetir el mismo recibo no sustituye el mazo ni sus modificaciones posteriores. Los otros dos espacios y los guardados importados se conservan.
- Los archivos de mazos v1 se leen sin inventar un marcador de aplicación. Perfiles sin recibo con mazos ya modificados y conflictos entre sesiones conservan los mazos existentes; la UI recarga el estado confirmado en lugar de imponer el equipo inicial o bloquear al jugador en reintentos permanentes.

Los mazos expresan selección, no desbloqueo. La entrada de invitados nuevos está descrita abajo; la conversión de disponibilidad antigua en propiedad y las habilidades iniciales siguen esperando decisiones del creador.

## Validación de referencias del catálogo

`Tools > Monster Pouch > Roadmap > Validar catálogo y referencias` agrega las comprobaciones existentes de `CharacterDefinition.Validate()` y `UnitAbilityOwnership.ForCombat()`. Detecta IDs repetidos, referencias ausentes o rotas, habilidades ajenas o colocadas en otro espacio y parámetros duplicados/no finitos. Los mensajes muestran el `DisplayName` actual junto al ID estable.

La herramienta es de solo lectura, exclusiva del Editor: no repara recursos automáticamente, no altera nombres, estadísticas, arte ni estados guardados, y conserva la vía anterior de `MatchConfig.Units`. Su prueba del catálogo real incluye los 23 personajes vigentes.

La primera regresión integrada terminó 1026/1028 EditMode; pasaron el inventario y los mazos. Los dos fallos correspondían a diagnósticos del validador sobre clases inline vacías de Unity y fueron corregidos. La global posterior pasó **1053/1053 EditMode**, `phase2-data-final-edit.xml`, incluidos los nueve casos del validador. Las **25/25 PlayMode** de `phase2-decks-progress-play.xml` también pasaron, incluidas las tres de mazos y las regresiones de colección, progreso y resultados. La evidencia anterior se conserva con sus recuentos originales.

## Separación de créditos e identidad invitada

Se implementó una comprobación simétrica de procedencia: un perfil QA rechaza créditos gratuitos/comprados y un perfil Player rechaza créditos QA, incluidos recibos cuyo saldo neto haya vuelto a cero. Cambiar únicamente el campo de entorno no convierte su contabilidad en válida. Sus cinco casos pasaron dentro de la global 1053/1053. Esta protección no habilita pagos ni implementa una cartera de producción.

La base de invitado local conserva un ID persistente y, tras elección explícita, un recibo único para Bugrum/Pombú. La composición histórica era de siete Whelps; las cuentas nuevas v4 usan los tres de MP-DEC-033, según el apartado vigente al final. Usa guardado versionado, checksum, respaldo y bloqueo de escritor. El marcador de datos anteriores es de compatibilidad: no concede propiedad del catálogo, monedas ni habilidades.

El detector de legado lee claves de juego y archivos confirmados de XP/mazos antes de que esos almacenes creen datos nuevos. Ignora perfiles QA, ajustes visuales, locks y temporales. Sus 19 casos pasaron dentro de la global 1053/1053.

El primer hook silencioso cargaba la identidad invitada y su marca de legado antes de inicializar los archivos de XP y mazos. Guarda `Application.persistentDataPath/GuestProfile/guest-profile.json` y su respaldo; un error mantiene la identidad no disponible y conserva los archivos, permitiendo iniciar el juego local existente. Esa integración histórica aún no mostraba selección inicial; pasó **19/19 EditMode** y **9/9 PlayMode**, informes `phase2-guest-hook-edit.xml` y `phase2-guest-hook-play.xml` en `Logs/roadmap-v1.2`. El flujo posterior se rige por MP-DEC-031.

## Invitado local v2 (histórico): cuenta nueva, elección y primer contacto

Solo un invitado creado sin datos de jugador anteriores comienza en `ChooseStarter`. La confirmación de Bugrum/Pombú guarda su recibo y cambia a `Tutorial`; la UI aplica el recibo al mazo de forma idempotente y abre la práctica. Se conservan los siete Whelps aprobados, los nombres de `DisplayName` y el arte actual. No se crea una cuenta vinculada ni una identidad de servidor.

Los perfiles invitados v1 se migran en memoria a v2 con estado `None`, manteniendo identidad, marca de legado y elección anterior si existía. Tener un v1 sin recibo no lo convierte en una cuenta nueva. Los jugadores con datos anteriores también usan `None`: abrir su juego no muestra selección ni tutorial. La migración no reescribe el original por el mero hecho de leerlo.

`CompletedTutorialModules` guarda únicamente módulos implementados, en orden y sin duplicados. `first-contact` es el único disponible: se registra al completar colocación por toque, inspección al mantener pulsado y arrastre a otra casilla legal. Marcarlo no establece `Complete`; los módulos 2–10 siguen pendientes y el almacén rechaza dar por completado un módulo que aún no está implementado.

Salir interrumpe la práctica y permite reanudarla desde el menú. Un módulo incompleto vuelve a empezar sus tres ejercicios al reanudarse; solo su finalización confirmada queda persistida. Un fallo al guardar permite reintentar sin inventar el recibo ni conceder premios.

`MatchController.StartTraining` usa una sesión independiente con el Monster en el Brief, enemigo fuera del campo, preparación sin reloj y combate deshabilitado. Colocar y mover usan las reglas reales del tablero. La sesión no tiene recibo BO5 y Progreso excluye explícitamente entrenamiento al capturar resultados: no concede XP, monedas, propiedad ni mejoras. Los recibos legítimos de partidas anteriores que ya estuvieran pendientes siguen pudiendo reintentarse.

El flujo corregido pasó **1090/1090 EditMode** y **19/19 PlayMode**, informes `new-account-tutorial-edit.xml` y `new-account-tutorial-play.xml` en `Logs/roadmap-v1.2`; detalle en `QA_CHECKLIST.md`. Este trabajo no completa el tutorial global, la economía del jugador ni sus recompensas; tampoco implementa vinculación de cuentas, backend o arte nuevo.


## Invitado local v3: continuación y repaso

La ampliación posterior habilita los ejercicios jugables 1–6. Los perfiles v1 siguen migrando a None; v2 valida su contrato original y conserva su progreso antes de pasar a v3 en memoria. La siguiente modificación se guarda atómicamente, con el original en el respaldo. Completados, orden, reintentos y versiones futuras mantienen las mismas protecciones. Los módulos 7–10 no se acreditan mientras falten sus requisitos.

Continuar abre el siguiente ejercicio disponible; Salir permite reanudar desde el menú. Ayuda incorpora repaso sin duplicar recibos. Las sesiones artificiales siguen sin SessionId de BO5 y sin premios de partida. El repaso de BO5 utiliza la partida normal y una lectura de LocalProgressStore que no importa ni concede progreso; solo confirma XP cuando encuentra el recibo correspondiente en disco. Los detalles y límites están en TUTORIAL_MODULES.md.

## Invitado local v4: tres iniciales y colección del Brief

MP-DEC-033/034 separan el inventario del invitado nuevo de las herramientas QA. Una cuenta creada sin datos anteriores usa `StarterInventory`: recibe el Monster elegido y exactamente `atori`, `bugui`, `blotan`, con recibo `starter:v2`. Los recibos históricos `starter:v1` solo aceptan la composición original de siete. No se reinterpretan los recibos guardados usando la lista inicial vigente.

Los perfiles v1–v3 migran en memoria a `Legacy`, manteniendo acceso y mazos anteriores; leerlos no cambia sus bytes. Esa compatibilidad no crea recibos de propiedad para todo el catálogo. La propiedad de jugadores antiguos y las mejoras iniciales siguen pendientes de una decisión del creador.

`UnlockCharacter` registra únicamente un desbloqueo explícito con ID de recibo, personaje, procedencia y fecha. Repetirlo es idempotente, reutilizar el ID para otro contenido es conflicto y poseer ya el personaje no duplica la concesión. Se valida el catálogo y se bloquean escrituras en recuperación. No entrega monedas, elige premios, ejecuta una máquina ni convierte el inventario QA en inventario del jugador.

El Brief conserva siete espacios. En cuentas con inventario inicial nuevo, completa espacios disponibles con Whelps desbloqueados, conserva selecciones válidas y exige `min(7, Whelps disponibles)` al entrar a una partida. Un octavo desbloqueo amplía las opciones sin desplazar automáticamente a los siete elegidos. Las entradas a bot y sala usan la misma comprobación local; esto no sustituye la futura autoridad de servidor.

La propiedad también se comprueba para el Monster. Una cuenta `StarterInventory` puede consultar todo el catálogo, pero solo puede elegir y usar Monsters incluidos en `GetOwnedCharacterIds()`: inicialmente, el elegido entre Bugrum y Pombú. `AvailableMonsterIdsForDeck` filtra esos IDs con el catálogo actual, del mismo modo que `AvailableWhelpIdsForDeck` filtra los Whelps. Solo un perfil explícitamente `Legacy` utiliza disponibilidad sin restricciones; un perfil nuevo sin elección o una identidad no disponible no se convierten en legado.

La asignación del mazo, su guardado y las entradas a bot/sala validan ambos grupos. Abrir una ficha o conservar un ID en un mazo no concede propiedad. Un recibo real de `UnlockCharacter` para otro Monster lo habilita como opción, conserva el Monster elegido y no cuenta como Whelp ni rellena uno de sus siete espacios. La normalización solo ajusta un Monster no poseído en mazos con inventario confirmado o durante una edición explícita; los mazos en conflicto conservan sus datos y deben resolver una selección incompatible antes de entrar en partida.

Según MP-DEC-035, los Monsters y Whelps bloqueados muestran el retrato oscurecido y un candado cerrado. Sus fichas siguen siendo consultables y el botón Usar está deshabilitado; el callback también comprueba disponibilidad. Al desbloquear el personaje, la colección actualizada elimina candado y oscurecimiento. Los nombres visibles proceden de `DisplayName`; los recibos, mazos y filtros mantienen los IDs estables.

Los conflictos conservan los mazos confirmados en disco. Una elección concurrente no autoriza personajes, ni se sobrescribe silenciosamente para aplicar el equipo inicial. La UI exige resolver una selección incompatible antes de usarla en partida. Los errores de identidad no se interpretan como acceso de legado.

El formato QA también se versiona para distinguir los recibos iniciales históricos y nuevos. Su inventario, saldo y herramientas siguen aislados del invitado. Las pruebas de esta ampliación se registran en `QA_CHECKLIST.md` después de integrar y ejecutar los cambios.

### Comprobación de Monsters y MP-DEC-035

Se preparó la ampliación de `StarterSelectionPlayTests` a 16 casos, conservando los 13 anteriores. Los tres nuevos cubren: elegir Bugrum y bloquear Pombú hasta un recibo explícito, sin autoselección ni cambio del número de Whelps; rechazar bot y validación de sala cuando un mazo en conflicto contiene un Monster no poseído, sin reescribirlo; y conservar el acceso de legado sin crear propiedad. El caso de identidad ilegible también comprueba el rechazo de Usar para Monsters. Las pruebas del desbloqueo comprueban que tanto el Whelp como el Monster pasan de retrato oscuro con candado a retrato sin ambas capas.

Estos casos usan directorios temporales distintos para invitado, mazos y XP; sus recibos explícitos no acreditan contenido a jugadores reales. Pasaron los 16 casos dentro de **46/46 PlayMode**, `locked-collection-tutorial-play.xml`. La lógica complementaria pasó dentro de **1195/1195 EditMode**, `locked-collection-machine-edit.xml`; detalle en `QA_CHECKLIST.md`. No se dan por validadas compras, servidor ni recompensas reales.

## Invitado v5: recibos de habilidades

La versión vigente es v5. Conserva íntegra la colección inicial de v4: identidad, elección, recibos de personajes, revisión y avance del tutorial. Leer v1–v4 migra solo en memoria y no escribe; v1–v3 mantienen la conversión de compatibilidad a Legacy y v4 conserva exactamente su modo de colección. Se añade una lista vacía de recibos de habilidades, que significa ausencia de recibos y no resuelve qué mejoras deben poseer inicialmente las cuentas nuevas.

Las listas obligatorias se validan como arrays explícitos en la raíz del JSON: `StarterSelections` desde v1, `CompletedTutorialModules` desde v2, `CharacterUnlocks` desde v4 y `AbilityUnlocks` desde v5. No se aceptan campos obligatorios omitidos, `null`, otros tipos, duplicados ni menciones del nombre dentro de strings u objetos anidados. A los archivos antiguos no se les exigen listas de versiones posteriores; la migración no permite introducir recibos de personajes o habilidades antes de la versión que los define. Esta comprobación es necesaria porque `JsonUtility` puede convertir listas omitidas o nulas en vacías, ocultando su ausencia al validador del objeto.

Se utiliza Newtonsoft ya referenciado por el proyecto para comprobar esa estructura y entregar a Unity las mismas claves decodificadas; no se añade ningún paquete. Las fechas permanecen strings para conservar los timestamps exactos. Los errores del parser se convierten en `InvalidDataException` y activan la recuperación existente sin reescribir archivos. Las versiones futuras siguen rechazándose sin degradación.

`UnlockAbility` exige un personaje poseído y un ID existente en sus tres `Tricks`. La confirmación incrementa una revisión y escribe el recibo usando el mismo guardado atómico; repetir una operación no duplica propiedad y las colisiones entre recibos de personaje y habilidad se rechazan. No hay llamadas automáticas desde elección, tutorial, máquinas o interfaz, ni cambios en las mejoras durante la partida. Catálogo incompleto, corrupción, recuperación y versiones futuras impiden nuevas mutaciones sin perder datos.

`ReadMachineOwnership` lee propiedad confirmada bajo bloqueo y rechaza recuperación; el adaptador puro devuelve un snapshot independiente y no convierte acceso Legacy en propiedad. No une guardados de inventario, máquina y moneda en una transacción comercial. La versión QA de esa frontera se prueba en un agregado distinto y no toca el perfil invitado. Contratos y evidencia en `GUEST_ABILITIES.md`, `QA_MACHINE_SANDBOX.md` y `QA_CHECKLIST.md`.

La reparación estructural pasó **50/50 casos en un harness independiente**, incluidos los 30 anteriores y 20 regresiones nuevas. El harness actualizado reproduce el fallo de lista omitida observado en Unity: la fuente anterior falla ese caso y la reparada lo supera. Usa NUnit real y fuentes del catálogo, pero sustituye los servicios de serialización/recursos de Unity; no equivale a su ejecución real.

Después, la tanda enfocada real de Unity pasó **280/280 EditMode**, sin fallos, el 22 de octubre de 2026 entre las 19:48:05 y 19:48:38 UTC. Incluye los **50/50 casos de habilidades del invitado** y los **33/33 de transacción QA**, con las reparaciones de estructura y precisión integradas. Informe: `Logs/roadmap-v1.2/guest-machine-serialization-fix-edit.xml`. El histórico 256/258 conserva sus dos fallos originales; esta revalidación los cierra.

La tanda posterior pasó **18/18 PlayMode**, sin fallos, el mismo día entre las 19:51:31 y 19:52:50 UTC: los 16 casos de selección inicial/colección y los dos de perfil invitado. Informe: `Logs/roadmap-v1.2/guest-v5-storage-final-play.xml`. Confirma esos flujos tras las reparaciones; no extiende el alcance a servicios, compras o premios comerciales.


## Perfil invitado v8 — MP-DEC-049/050

Compra permanente: recibo de propiedad y débitos exactos de 100 Earthkens + 100 Sunkens en un guardado atómico. Idempotencia por requestId; saldo insuficiente, propiedad adquirida por premio y escritura fallida no dejan cargos parciales. Bienvenida: 20 Saturnkens en la transacción que crea una identidad nueva, antes de elegir Monster; nunca al migrar perfiles v1–7, reabrir ni repasar tutorial. Las representaciones vacías de clases inline de JsonUtility se admiten sin aceptar datos de máquina reales. La autoridad sigue siendo local; no hay garantía entre reinstalaciones/dispositivos sin backend.


## Perfil v9: evidencia de los módulos 7 y 8

TutorialPouches y TutorialMenuVisits son colecciones obligatorias; máximo un estuche tutorial por identidad. La adquisición conserva tabla, sorteo y fecha; la apertura y su evento de moneda se guardan juntos. El identificador reservado tutorial-pouch y su fuente no pueden utilizarse en créditos genéricos. Completar el módulo 7 requiere apertura y las cuatro vistas; completar el módulo 8 requiere un recibo real de MachineDraw observado. Las lecturas y repeticiones no crean recibos.

Las versiones 1–8 migran sin regalar estuches ni acreditar módulos. Leer no reescribe; se conserva identidad, selección, inventario, saldo, bienvenida y pruebas de compras. Se rechaza evidencia futura introducida en un formato antiguo. El contrato sigue siendo local, no identidad vinculada ni autoridad de servidor.

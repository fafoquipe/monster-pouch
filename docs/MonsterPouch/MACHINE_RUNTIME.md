# Motor de máquinas: alcance local

Implementación técnica de las reglas definidas en §5.1–5.3 del roadmap. No habilita extracciones en el juego, cobros, premios reales ni una economía de producción.

## Contrato

`MachineDefinition` contiene identidad estable, versión y exactamente 14 premios: un personaje, tres mejoras con IDs distintos y diez recursos identificados por slots. El motor no asigna cantidades ni inventa el contenido de esos recursos. La asociación con un personaje real y los paquetes aprobados pertenecen al futuro catálogo.

`MachinePolicy` exige probabilidades condicionales y pesos explícitos. No existe una política comercial predeterminada. Los porcentajes provisionales 1, 1, 1, 1, 1, 2, 4, 8, 20 y 100, así como los pesos sintéticos, viven exclusivamente en las pruebas. Una política que no garantiza el personaje como máximo en la décima extracción es inválida.

`MachineDrawEngine.Preview` calcula los estados Disponible/Bloqueado/Obtenido y la distribución de cada premio elegible. `TryDraw` consume exactamente esa distribución con una muestra suministrada por el llamador; el motor no genera aleatoriedad ni decide su procedencia. Las entradas no se modifican. Un fallo no devuelve estado o recibo nuevos para confirmar.

Para consultas repetidas, un contexto preparado conserva copias privadas de la definición y política validadas, junto con sus firmas. Modificar los objetos originales después no cambia ese contexto. Las llamadas estáticas conservan validación por llamada para entradas mutables; estado, propiedad externa y solicitud se comprueban también al usar el contexto. No se reutilizan firmas basadas únicamente en referencias de objetos.

## Premios y garantías

- Los premios obtenidos se excluyen de la distribución. Las mejoras se bloquean hasta poseer el personaje.
- El personaje se obtiene a más tardar en la extracción diez; la siguiente extracción entrega una mejora pendiente. Si ya se poseen todas, se omite esa garantía.
- Un snapshot de propiedad externa reconoce personaje y mejoras de otras vías, sin consumir una extracción. Observar por primera vez el personaje activa la garantía de mejora; no vuelve a activarla en cada consulta.
- `DrawCount` cuenta extracciones confirmadas, no premios externos. Los recursos reclamados necesitan un recibo de extracción.
- Una máquina completa no produce otro resultado. Una rama de probabilidad sin candidatos o pesos utilizables devuelve `NeedsConfiguration`, sin inventar una distribución de reemplazo.

## Recibos e integración pendiente

La solicitud identifica máquina, versión, revisión esperada y operación. El resultado contiene estado nuevo y recibo. Repetir una solicitud confirmada devuelve el mismo premio, sin volver a sortear ni retroceder extracciones posteriores. Reutilizarla con revisión o política incompatibles devuelve conflicto.

Las firmas de catálogo y política evitan reinterpretar un estado usando definiciones diferentes. Se rechazan estados inconsistentes, IDs repetidos, secuencias imposibles, garantías omitidas y muestras no finitas o fuera de `[0,1)`. Esto es validación local; las firmas no son una protección contra quien controla el dispositivo.

El futuro dueño de la transacción deberá confirmar **estado de máquina, inventario y débito de 1 Saturncoin de forma atómica antes de animar el premio**. Este motor puro no realiza esa transacción, no guarda en disco y no invoca `UnlockCharacter`. Tampoco conecta a un backend ni implementa la UI de máquina. La colección invitada y QA siguen separadas.

`LocalQAMachineStore` implementa esa frontera de confirmación únicamente en un agregado aislado de prueba: créditos QA, premios opacos, estado y recibo se sustituyen juntos. Su coste, saldo y paquetes deben configurarse explícitamente; no mueve Saturncoins del jugador ni enlaza almacenes distintos. `GuestMachineOwnershipAdapter` proporciona por separado una lectura de personajes/habilidades confirmadas del invitado v5. Unir esos componentes para una economía real requiere la transacción autoritativa todavía pendiente; ver `QA_MACHINE_SANDBOX.md` y `GUEST_ABILITIES.md`.

## Pendientes para habilitar una máquina real

Se necesitan asociaciones explícitas `machineId ↔ characterId`, los diez paquetes aprobados con cantidades, pesos de recursos y mejoras y reparto después de obtener el personaje, balance comercial confirmado, saldo gratuito inicial y persistencia transaccional confiable. El arte se integra desde un manifiesto por IDs y referencias de aprobación, conservando los originales. Las muestras dibujadas en una vitrina no definen premios ni cantidades.

La simulación usa únicamente IDs sintéticos y una semilla reproducible. Su ejecución y resultados se documentan en `QA_CHECKLIST.md`; no equivalen a una prueba de compras, servidor o dispositivo.

## Arte externo en preparación

El chat de arte entregó `output/imagegen/vending-machines-v1/manifest.json` en el workspace, SHA256 `ED926B6D56081B7F425F652B2EBBC00B8196DAB04AD7A5372E0334CD69EA91D5`. La validación de solo lectura confirmó una máquina por cada uno de los 23 IDs actuales, nombres separados correctos y 72 archivos únicos presentes con hashes coincidentes. La galería contiene exactamente las 23 imágenes.

Las imágenes exportadas conservan los bytes de sus originales. No están importadas en Assets y la composición nueva sigue pendiente de revisión visual del creador. `proposedMachineId = character-<id>` es una propuesta explícita del manifiesto, todavía sin vinculación económica. Estos resultados visuales no aprueban paquetes, cantidades ni probabilidades.

Los 23 pouches también fueron entregados y auditados: `output/imagegen/pouches-v1/manifest.json`, SHA256 `01351879C6C90A99865D587DD52396C57A9DFBD9028D330B6CFBC858D8CDABA0`. Coinciden IDs, nombres y asociaciones con las máquinas. Los 50 archivos del paquete coinciden byte a byte con la copia `pictures/utilities/pouches/rework-v1`; se verificaron 170 archivos únicos contando referencias, sin faltantes, extras ni discrepancias. La galería contiene 23 imágenes y su aprobación visual sigue pendiente. No se importó ese paquete a Assets.

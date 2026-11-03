# Transacción local de máquinas: sandbox QA

Este paquete implementa una transacción de prueba aislada. No implementa economía comercial ni sustituye autoridad de servidor. No añade interfaz, compras, servicios ni extracciones para jugadores.

## Alcance

- `LocalQAMachineStore` solo escribe bajo `<directorio QA>/machine-sandbox/`, en `qa-machine-sandbox.json`, su respaldo y temporales. No abre ni modifica `LocalQAProfileStore`, carteras Guest/Player, PlayerPrefs, inventario del jugador o datos de partida.
- Se exige `EconomyDevelopmentConfig.EnableLocalQA` y Editor o compilación de desarrollo. El importe de bienvenida de esa configuración no se utiliza.
- El llamante debe proporcionar definición, política, saldo QA inicial, coste positivo, propiedad inicial y los diez paquetes de recursos. Saldo/coste sin configurar usan el sentinel inválido `-1`. No hay catálogo, probabilidades, precios, importes de recursos ni saldos comerciales predeterminados. Los IDs e importes de las pruebas son sintéticos.
- La estructura de entrada se valida antes de copiar: una propiedad inicial, lista de habilidades, objeto o array anidado ausente se rechaza. La configuración se copia manualmente, incluidos todos sus objetos y arrays; ninguna probabilidad ni peso pasa por `JsonUtility`.
- Los recursos se registran como IDs opacos con cantidades enteras. No se convierten en moneda de jugador ni conceden contenido a otro almacén. La propiedad inicial explícita y los premios posteriores pertenecen exclusivamente al agregado QA. No se añade una API de concesión externa durante la sesión.
- Cada directorio contiene una configuración inmutable de una máquina. Cambiar política, paquetes, coste o saldo inicial contra un archivo existente devuelve conflicto y conserva sus bytes; no reinicia la máquina.
- La firma de configuración usa SHA256 sobre una codificación binaria versionada con longitudes, strings delimitados y los bits IEEE-754 exactos de cada `double`. Distingue `0.9999999999999999` de `1.0` tanto en probabilidades como en pesos. Las configuraciones guardadas con la firma QA anterior quedan en conflicto y se conservan sin reescritura automática.

## Confirmación y recuperación

`TryDraw` toma un bloqueo exclusivo, relee el agregado y valida revisión e identidad. Calcula el resultado en copias. Una única sustitución de archivo confirma conjuntamente saldo QA, débito, premio, estado de máquina y recibo. No encadena las APIs `Spend` y `Grant` ni publica un premio antes del commit.

El mismo ID y revisión originales devuelven el recibo previo sin otro cargo y conservan avances posteriores. Una colisión con contenido/configuración o revisión distinta se rechaza. Fondos insuficientes, muestra inválida, elegibilidad sin configuración, máquina completa y desbordamiento no escriben. El punto opcional de inyección de fallo, exclusivo de QA, permite comprobar la frontera entre `Flush(true)` y la sustitución atómica.

El archivo incluye versión, ámbito obligatorio, ID de sandbox y checksum. La lectura reconstruye como máximo catorce transacciones para comprobar que saldos, premios, historial y estado concuerdan. Los campos obligatorios se leen sobre sentinels para impedir que un JSON incompleto herede valores seguros por defecto. Los checksums detectan corrupción; no son protección contra fraude.

Un respaldo recuperado es solo lectura: todas las operaciones de extracción quedan bloqueadas. No se vuelve a gastar un saldo anterior ni se reescribe automáticamente el primario corrupto. Un esquema futuro se conserva y se rechaza. No se ofrece reparación automática que pueda descartar recibos.

## Evidencia

El cierre independiente de la reparación de precisión compiló las fuentes exactas y ejecutó **33/33 casos** con NUnit real y adaptadores locales de JSON/ciclo de vida Unity: **4,994 segundos**. Incluye commit/reinicio, reintento después de operaciones posteriores, escritor concurrente, conflictos, fallo después de flush, respaldo solo lectura, encabezados omitidos con checksum válido, historial alterado, aislamiento de DTO y archivos ajenos, 14 extracciones completas, overflow, propiedad inicial/lista de habilidades nulas y un intervalo probabilístico extremo cuyo punto medio redondearía a `1.0`. Dos regresiones nuevas rechazan configuraciones cuyos pesos o probabilidades solo difieren entre el valor adyacente inferior a uno y `1.0`; la prueba extrema comprueba también los bits del preview.

La evidencia histórica de **29/29** permanece en `qa-machine-transaction-staging/verification/historical-29/`. El cierre independiente anterior de **31/31** se conserva en `qa-machine-precision-fix-staging/verification/historical/standalone-31-results.json`.

La tanda histórica de Unity terminó **256/258**. Uno de esos fallos fue el redondeo de la configuración mediante `JsonUtility`: el valor casi uno podía convertirse en uno, mientras el serializador del harness independiente lo conservaba. Su XML se mantiene en `Logs/roadmap-v1.2/guest-abilities-machine-transaction-edit.xml` y en la copia histórica del staging de reparación; no se presenta como cierre aprobado.

Los informes independientes y hashes de la reparación están en `qa-machine-precision-fix-staging/verification/standalone-results.json`. La reparación se validó después en Unity dentro de **280/280 EditMode**, sin fallos, el 22 de octubre de 2026 entre las 19:48:05 y 19:48:38 UTC: pasaron los **33/33 casos de `QAMachineTransactionTests`**, incluida la regresión de precisión con el serializador real. Informe: `Logs/roadmap-v1.2/guest-machine-serialization-fix-edit.xml`. El fallo histórico 256/258 se conserva y queda cerrado por esta ejecución. El motor y la simulación de 100.000 máquinas permanecieron intactos. Los DTO persistidos de estado, solicitud, recibos y cantidades no contienen `double` y conservan su serialización existente.

La regresión posterior de selección inicial/colección y perfil invitado pasó **18/18 PlayMode**, sin fallos, el mismo día entre las 19:51:31 y 19:52:50 UTC, en `Logs/roadmap-v1.2/guest-v5-storage-final-play.xml`. Son pruebas de esos flujos con las reparaciones integradas; la transacción QA se verificó en los 33 casos EditMode y no dispone de una pantalla de extracción jugable.

El entorno independiente impide `File.Move` en su carpeta temporal aislada; el harness usa `MONSTER_POUCH_QA_TEST_ROOT` para dirigir únicamente los archivos de prueba al workspace permitido. Sin esa variable, las pruebas Unity usan la carpeta temporal normal. La limpieza verifica la raíz absoluta y el prefijo de cada directorio de prueba antes de borrarlo.

# Tutorial: implementación progresiva

Las aclaraciones del creador en `DECISIONS.md` prevalecen sobre el documento original. La selección inicial pertenece al alta de una cuenta nueva; los perfiles anteriores conservan su acceso habitual.

## Flujo y persistencia

El perfil invitado conserva el orden de los diez módulos. Los perfiles v1 permanecen en `None`; los v2/v3 conservan identidad, elección, etapa y módulos completados. Leer un perfil antiguo no reescribe el archivo; la siguiente modificación confirmada guarda la versión vigente y conserva un respaldo. Una versión futura no se reemplaza con un respaldo antiguo.

MP-DEC-033 sustituye los siete iniciales por `atori`, `bugui` y `blotan` en cuentas nuevas. El selector y los ejercicios usan ese equipo, sin conceder otros cuatro personajes para rellenar espacios. El Brief conserva capacidad para siete y se completa conforme se desbloquean Whelps, según MP-DEC-034. Los recibos históricos de siete iniciales permanecen válidos.

Al terminar y guardar un ejercicio, Continuar abre el siguiente módulo implementado. Salir vuelve al menú; reanudar o recargar retoma el módulo pendiente. El avance de las prácticas incompletas se reinicia; los módulos 7/8 conservan visitas, estuche y recibos confirmados; los módulos ya completados no pierden sus recibos. Ayuda permite repasar los ejercicios ya disponibles sin duplicar progreso ni conceder premios.

## Ejercicios implementados

| Módulo | Verificación jugable |
|---|---|
| 1. Primer contacto | Colocar por toque, inspeccionar manteniendo pulsado y arrastrar a una casilla legal |
| 2. Posicionamiento | Movimiento real, zona permitida y ocupación del tablero con objetivo técnico inmóvil |
| 3. Ataques y vida | Alcance, ficha, inicio de simulación, ataques, daño y habilidad automática reales contra objetivo didáctico |
| 4. Combate sencillo | Despliegue y victoria real contra el Monster inicial no elegido; reducción aislada conforme a MP-DEC-032 |
| 5. Preparación y economía | Moon-Ken temporal, compra, reroll y mejora mediante las reglas actuales de copias |
| 6. Banco táctico | Guardar y devolver mediante gestos, conservación de mejoras, devolución de inversión pagada y coste de despliegue aprobado; el regalo didáctico conserva inversión cero |

El entrenamiento usa el tablero, los gestos, la simulación y las restricciones existentes. No crea personajes coleccionables ni modifica assets de balance o arte. Sus sesiones no tienen `SessionId` de BO5; no producen XP de partida, victorias normales, moneda permanente ni propiedad. Los premios por módulo siguen pendientes de definición, por lo que no se muestra una concesión inexistente.

El objetivo del módulo 3 reutiliza el dummy técnico. Su resistencia se deriva del ciclo de energía y daño del Monster para permitir observar una habilidad automática; ese ajuste no afecta al enemigo del módulo 4 ni a una partida normal.

## Rival sencillo aprobado

MP-DEC-032: Bugrum se enfrenta a Pombú y Pombú a Bugrum. Estadísticas favorables a la mitad; las inversas se duplican si eso debilita al rival. Incluye vida, daño, intervalos, movimiento, alcance, IQ, energía y magnitudes de habilidad. El umbral de energía se duplica y sus ganancias se reducen a la mitad; reducir el umbral facilitaría la habilidad y contradiría la aclaración.

Los enteros se redondean hacia arriba respetando los mínimos del motor; cero y los indicadores de herencia conservan su significado. IDs, clases, selección de objetivos y costes de tienda no se reinterpretan como estadísticas de combate. Las definiciones originales no se modifican.

## Colección y máquina implementadas

7. Reclamar y abrir el estuche azul único; recorrer Maleta, colección, una mejora desbloqueable y las tres monedas persistentes. El premio usa la tabla del regalo diario y es independiente de la Sala de estuches.

8. Elegir una máquina tocando su sprite, revisar los 14 premios, confirmar 5 Saturnkens y ver el premio realmente guardado. Se puede revisar una extracción anterior sin volver a pagar. La bienvenida ya entregó 20 una sola vez al crear la cuenta.

Ambos pueden cerrarse, retomarse y repasarse sin nuevas concesiones. El botón de continuar refleja objetivos guardados. Un fallo de escritura presenta reintento y no confirma un premio que no se guardó.

## Dependencias siguientes

Los módulos 7 y 8 tienen sus dependencias aprobadas e implementadas (MP-DEC-047, 049, 050 y 051). El rival del primer BO5 está aprobado por MP-DEC-053; su implementación y el módulo 10 siguen pendientes. Otros premios no aprobados no se conceden.

El repaso de BO5 y salas reutiliza los flujos actuales. No reduce la IA global, no entrega un premio nuevo, no conecta automáticamente a servicios y no acredita los módulos 9/10 saltándose los anteriores. Solo un recibo de XP guardado para la misma partida normal puede presentarse como confirmado. Las pruebas de Relay con dos clientes y las cuentas vinculadas siguen pendientes.

La evidencia de ejecución se registra en `QA_CHECKLIST.md`; implementar un ejercicio no equivale a validar toda la fase ni el producto comercial.

El acceso al repaso de salas mantiene la disponibilidad de cuentas anteriores. Para una cuenta nueva requiere terminar un BO5 normal desde el repaso y confirmar su recibo de XP. La evidencia de que fue offline pertenece a la instancia actual de la UI: recargar vuelve a exigirla porque los recibos de XP previos no almacenan ese tipo. Esta limitación no se presenta como tutorial 10 terminado ni como avance persistente del módulo 9.

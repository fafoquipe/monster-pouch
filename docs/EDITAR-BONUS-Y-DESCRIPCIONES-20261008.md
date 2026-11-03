# Editar bonus y descripciones

Selecciona `Assets/resources/MonsterPouch/MatchConfig.asset` en Unity, elige el personaje y abre **Habilidad propia** o la mejora correspondiente.

- **Daño adicional (+):** puntos que añade esa mejora, por ejemplo +2 por baja.
- **Daño (× ataque)** y **Daño crítico (× ataque):** multiplicadores del daño de ataque. 2 significa el doble; 1,5 significa un 50 % adicional.
- **Curación adicional**, **Objetivos adicionales**, **Invocaciones adicionales** y **Aturdimiento adicional:** permiten escribir directamente el incremento respecto a la habilidad base. La vista previa usa también los cambios de la habilidad base que aún estás editando.
- **Cambio entre monedas:** segundos de diferencia respecto al intervalo base; un valor negativo entrega monedas antes.
- **Reducción adicional (puntos porcentuales):** por ejemplo, +25 sobre una reducción base de 25 % da una reducción de 50 %.
- **Daño fijo de habilidad:** -1 conserva la fórmula de ataque; un valor de 0 o mayor indica daño independiente y la ficha lo identifica como daño fijo.

Las fichas se actualizan a partir del balance. No hace falta reescribir una descripción ni guardar dos cifras para un mismo efecto. Las mejoras que ya sumaban un bonus lo mantienen; las que guardaban un total muestran y permiten editar la diferencia respecto a la base. Cambiar la base actualiza esa diferencia sin modificar automáticamente el total guardado de la mejora.

Esta actualización no cambia el balance de combate existente. Las descripciones de los 19 personajes y sus 76 habilidades usan el mismo formateo en las fichas y en el inspector. Los textos en inglés usan las mismas fórmulas. Los tiempos de animación siguen siendo independientes por habilidad.

Para guardar: **Monster Pouch → Guardar proyecto y copia de balance**. No se genera un APK al guardar.

# Ajustes de combate — 27 de septiembre de 2026

## Comportamiento

- **Anuik, Impacto de Retorno:** al revivir, los enemigos alcanzados reciben el daño, empuje y 3 segundos de stun. Respeta inmunidades.
- **Anuik, Ricochet Hearts:** un solo rebote desde la víctima del primer impacto hacia otro enemigo a un máximo de 3 casillas de ella. Usa la misma distancia de casillas del combate. El corazón sale inmediatamente del primer impacto, no vuelve a Anuik ni inicia otra animación de ataque. El viaje usa la velocidad de proyectil configurada.
- **Hymay:** el objetivo permanece aturdido durante todo el arrastre y 1 segundo después de llegar. Con Tirón Acrobático, Hymay comienza su voltereta tras completar el tirón: se recoge, gira y aterriza. Es invulnerable durante el salto. No salta si no hay una casilla libre válida.
- **Popow:** su aura aplica el stun conforme alcanza a cada enemigo. Cada enemigo recibe un contacto por embestida y cuenta su propia duración desde ese contacto. No aplica el stun a toda la ruta por adelantado. Un reset/interrupción del dash detiene los contactos futuros y retira su efecto visual.
- **Taunt:** las tropas afectadas priorizan al provocador también en habilidades dirigidas y en la embestida de Popow. Una preparación dirigida a otro enemigo se reinicia contra el provocador; un súper cancelado antes de liberarse recupera la energía para intentarlo de nuevo. Los proyectiles ya liberados conservan su destino. Áreas, blancos secundarios e invocaciones conservan sus reglas; Flo termina una flor que ya estaba plantando.
- **Tauris:** la muerte del objetivo y el retarget normal ya no cortan la duración visual de su mordisco especial. Hambre no reinicia su reloj. Los siguientes básicos mantienen sus tiempos originales. Muerte, stun y reinicio real de combate siguen cancelando la pose.

## Campos editables

En el Inspector de `Assets/Resources/MonsterPouch/MatchConfig.asset`, selecciona el personaje:

| Personaje | Sección | Campo |
|---|---|---|
| Hymay | Habilidad propia | Tiempo de arrastre (s), Stun tras llegar (s) |
| Hymay | Mejora 3: Tirón Acrobático | Tiempo de voltereta (s), `distance` |
| Anuik | Mejora 2 | Duración del stun (s) |
| Anuik | Mejora 3 | `bounceRange` (3 por defecto), `bounceDamageMultiplier` |
| Popow | Habilidad propia | `travelDuration`, `width` |
| Tauris | Animación de habilidad | Duración (s) |

El combate resuelve los tiempos de movimiento en pasos de 0,1 s. Los valores nuevos se ofrecen como defaults sin restaurar las estadísticas personalizadas del proyecto.

## Comprobación

555 pruebas EditMode y 27 pruebas PlayMode aprobadas. La comprobación visual registró invulnerabilidad de Hymay, stun hasta 1 segundo después del arrastre y el aterrizaje. En el escenario lento de Popow, los contactos ocurrieron a los 0,1 / 0,5 / 0,9 s y cada stun terminó por separado. Anuik mantuvo los tres stuns desde 2,1 hasta 5,1 s.

Los informes y capturas están en `Logs/local-validation/control-motion-*.json` y `Logs/local-validation/screenshots/control-*.png`. Las escenas de comprobación utilizan copias temporales de las definiciones.

No se generó una build ni un ejecutable.

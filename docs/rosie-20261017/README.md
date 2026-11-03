# Rosie — 17 de octubre de 2026

Nueva Whelp de apoyo basada en el personaje entregado por el usuario. Diseño y habilidades definidos en esta conversación; no proceden de Monsters.pdf ni de whelps.pdf.

## Balance inicial

| Parámetro | Valor |
|---|---|
| Vida | 25 |
| Daño básico | 2 |
| Alcance básico | 6 casillas |
| Intervalo entre ataques | 1,2 s |
| Energía máxima / por ataque | 20 / 2 |
| Energía por daño o tiempo | 0 |
| Coste del personaje | 3 Moonkens |
| Curación base | 25% de la vida máxima de cada receptor |
| Anchura de la rosa | 3 casillas |
| Velocidad de la rosa | 3 casillas/s |
| Preparación / recuperación | 0,3 / 0,2 s |
| Mejora 1 | +30% de velocidad de ataque durante 3 s; coste 3 |
| Mejora 2 | Escudo del 10% de vida máxima; coste 3 |
| Mejora 3 | Inmunidad al control durante 3 s; coste 4 |

## Comportamiento

La fila se evalúa al lanzar la rosa: última fila propia (azul Y=9, rojo Y=0), aliado más cercano; cualquier fila adelantada, aliado más lejano. Se excluye a Rosie de la búsqueda del destinatario, pero recibe inmediatamente su propia curación y todas las mejoras compradas. Los empates usan distancia y luego ID estable.

La rosa va en línea recta hasta la casilla que ocupaba el destinatario al lanzarla. Cura una vez a cada aliado vivo que cruza la franja, incluso si se mueve durante el recorrido. No persigue al destinatario, no cura enemigos y no atraviesa objetivos dos veces. Sin otros aliados, Rosie se cura y lanza hacia el extremo opuesto del tablero. No requiere un enemigo como objetivo del súper.

La ondulación, el giro y el ligero cambio de escala son exclusivamente visuales: no alteran trayectoria, anchura ni colisiones. Pausar congela simulación y animación; terminar combate o salir limpia la rosa. El proyectil ya lanzado puede terminar aunque Rosie sea derrotada, como otros proyectiles del juego.

La velocidad de ataque tiene su propio multiplicador y temporizador: no cambia movimiento, refresca duración sin sumar acumulaciones y se aplica también a Rosie. El escudo absorbe daño y permanece hasta consumirse o reiniciar el combate. La tercera mejora, Pétalos Libres, utiliza la misma inmunidad al control de Atong durante 3 segundos desde que cada receptor recibe la rosa. Impide nuevos controles; no impide recibir daño ni elimina controles ya activos. También se aplica a Rosie. La curación continúa siendo del 25% con las tres mejoras. Los aliados con vida llena reciben las mejoras igualmente. Curación y escudo se redondean a los enteros del sistema de vida del juego; la curación respeta anticuración y bonificaciones existentes.

## Edición

En **Monster Pouch > Editor de balance**, seleccionar **Rosie**. Los assets individuales están en `Assets/resources/MonsterPouch/Characters/rosie/`: Character, Attack, Ability y Upgrade1–3. Los porcentajes, duración, ancho, velocidad de proyectil, tiempos, costes, energía, daño, vida y alcance se pueden editar. Las descripciones se actualizan con esos valores, expresando curación y escudo como porcentajes de vida máxima.

Integración: catálogo de personajes, selección de Brief, tienda, tres mejoras, retrato, gesto de lobby, ocho direcciones, movimiento, ataque, súper, derrota, iconos propios y proyectiles. Cinco vistas dibujadas se reflejan para las ocho direcciones. El lobby usa una secuencia compacta de cinco poses. Los textos de habilidades están en español e inglés.

Rosie respeta el ajuste global de reproducción del lobby: actualmente está desactivado para todos los personajes. El gesto está preparado y la prueba lo habilita temporalmente para validarlo; después restaura el ajuste previo.

## Arte

Se utilizó la herramienta integrada image_gen. Referencia original y prompts completos en esta carpeta; PNG finales en `Assets/resources/MonsterPouch/Rosie/`. La transparencia y los archivos generados se conservan. Unity realiza el recorte de sprites y aplica filtro Point. Los cuatro iconos finales de habilidades se basan en el atlas de los demás personajes (abilities-04): tarjetas cuadradas de 64 píxeles, contornos marcados y fondos de colores. Están en rosie-abilities.png; sustituyen los iconos preliminares del primer atlas y el primer icono de inmunidad. `MonsterPouchRosieSetup.Apply` instala los recursos sin restablecer el balance que el usuario haya editado.

## Verificación

Pruebas específicas: `RosieTests` (selección por fila en ambos bandos, curación progresiva, ancho y diagonales, mejoras propias, duración, energía y edición de balance) y `RosiePlayTests` (colección, iconos, porcentaje legible, balanceo, pausa y limpieza). Resultados y capturas: `Logs/local-validation/rosie/`.

Validación final de lógica: **123 pruebas aprobadas, 0 fallos**, incluidas las 14 de Rosie y regresiones de combate, energía, descripciones, propiedad de habilidades, catálogo, estados y selector de balance. Resultado: `edit-final.xml`. La inmunidad impide el aturdimiento, permite recibir daño y caduca según el instante en que cada aliado recibe la rosa.

Validación visual: **2 pruebas Play Mode aprobadas, 0 fallos**. Se comprobaron selección, retrato, gesto disponible, cuatro iconos de 64 píxeles, descripción breve, trayectoria recta con giro visual, pausa y limpieza. Capturas reales revisadas a 1080×1920: `rosie-collection.png` y `rosie-healing-rose.png`. Total: **125 pruebas aprobadas**.

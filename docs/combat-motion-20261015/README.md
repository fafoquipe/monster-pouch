# Revisión de combate · 15 de octubre de 2026

Los 19 personajes tienen acentos de cuerpo propios para que se distingan la preparación, el contacto o liberación, y la recuperación. Los dibujos, tamaños y secuencias de desplazamiento se conservan.

Abre `review.html` para ver capturas de las animaciones reales de Unity con sus tiempos de combate. Incluye los 19 ataques básicos y 12 habilidades activas, de frente y de lado; permite cámara lenta, pausa y filtro por personaje.

Cada tarjeta mantiene un encuadre fijo calculado sobre todas las poses del personaje, incluidos sus mayores gestos. La cámara de revisión permite inspeccionar los ataques completos; las tarjetas no son una comparación de escala entre personajes.

| Personaje | Gesto de ataque / habilidad |
| --- | --- |
| Aky | Extensión y recogida de energía hacia el pecho |
| Anuik | Corazón reunido y liberado con retroceso aéreo |
| Atong | Preparación pesada, masa elevada y golpe seco |
| Atori | Recogida y ataque rápido hacia la presa |
| Blotan | Golpe corto de cuerpo; eleva su botín |
| Bugaloo | Golpe de cuerpo; guardia estable para reflejar |
| Bugui | Lanzamiento ligero y rápido acorde al ritmo creciente |
| Flo | Golpe marcado; elevación y plantado de la flor |
| Gochan | Proyección del sello desde la frente |
| Hymay | Hombro preparado, latigazo y tirón de recogida |
| Jazar | Carga del brazo y lanzamiento pesado de materia |
| Kayon | Lanzamiento y gesto de invocación de Dummys |
| Popow | Puñetazo seco desde la guardia |
| Sepora | Retroceso del rayo instantáneo; carga del súper |
| Stein | Compresión eléctrica y retroceso expansivo |
| Tauris | Preparación de mandíbula, mordisco y retirada |
| Tokoro | Peso elevado y cabezazo pronunciado |
| Trimol | Balanceo pesado y lanzamiento de roca |
| Tsu | Preparación y lanzamiento rápido de chicle |

Las poses de liberación de los súper esperan el tiempo de lanzamiento de la simulación. Las reacciones por bajas, sueño e invisibilidad no arrancan una segunda animación ofensiva. Sepora conserva su rayo instantáneo; sus preparaciones sólo se muestran cuando la habilidad tiene preparación real.

La liberación del súper de Hymay mantiene el brazo hacia el objetivo y reserva la pose de voltereta para su retroceso acrobático. Tokoro usa una pose firme con los ojos cerrados al contactar con la cabeza, en lugar del gesto de rugido con brazos levantados.

La lógica de daño, reglas, tiempos, proyectiles y efectos externos no se modifica. Los saltos alternados, vuelo, derrota y resurrección siguen usando las secuencias existentes.

Validación: 135 pruebas aprobadas, sin fallos ni omisiones; ver `tests.xml`. Incluyen el catálogo real en ocho direcciones, el momento exacto del contacto y del súper, la duración, el ancla de casilla y las cancelaciones, además de regresiones de movimiento y habilidades.

Implementación: `CharacterCombatMotion.cs` define gestos y amplitudes; `unit-presentation.cs` aplica las poses exclusivamente a los diseños con `AuthoredBodyMotion`. La ruta anterior continúa disponible para los personajes de prueba y arte antiguo. `MonsterPouchCombatMotionReview` permite repetir pruebas y capturas desde el editor. No se ha generado un APK en esta revisión.

Control final de capturas: 1.984 fotogramas, 19 personajes, cero recortes ni capturas vacías, margen mínimo de 13 píxeles; ver capture-audit.json. Visor comprobado con 19 ataques y 12 habilidades, sin errores JavaScript.

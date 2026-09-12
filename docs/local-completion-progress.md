# Estado local de Monster Pouch

La revisión actual incluye objetivo persistente con excepción durante la resurrección de Anuik, despliegue de cinco filas por seis columnas, diseños del usuario para Stein y Kayon, y monedas de oro de Kayon con alcance4: [gameplay-user-designs-delivery.md](gameplay-user-designs-delivery.md). Ejecutable: `Builds/Windows-Coins/Monster Pouch.exe`. Validación: 297 EditMode y 13 PlayMode aprobadas. Tauris y Anuik conservan sus diseños aceptados. Los apartados siguientes son historial.

## Entrega anterior

Entrega del 11 de septiembre de 2026. Esta revisión implementa las correcciones más recientes del usuario: maletín cerrado con dibujo propio, animaciones de Dummy, un personaje de MegaTrip y elección de equipo. Las instrucciones actuales del usuario prevalecen sobre los documentos originales.

## Versión jugable

Ejecutable actualizado: `Builds/Windows-Brief/Monster Pouch.exe`, dentro de `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch`. Se creó una distribución independiente para conservar la partida abierta en `Builds/Windows`. La carpeta Windows contiene la versión anterior; no debe usarse para comprobar estas últimas incorporaciones.

Unity **6000.3.9f1**, escena `Assets/Scenes/main-scene.unity`. No se cerró el Editor, no se abrió otra instancia, no se ejecutó batchmode y no se publicaron cambios ni se hizo commit. Los assets y la escena se conectaron mediante APIs del Editor, conservando las fuentes originales.

## Cambios visibles

- El Brief muestra tres figuritas dentro de sus compartimentos, centradas y con precios pequeños de alto contraste. No hay una fila inferior de ofertas ni botones permanentes de venta o almacenamiento.
- Se mantiene la apertura/cierre de 0,32 s por transición. La pose cerrada usa un sprite nuevo de maletín cerrado, con tapa exterior de madera y una sola cerradura; se mezcla con la bisagra al terminar la transición. Ya no se presenta la tapa abierta estirada como exterior del maletín cerrado.
- Banco independiente: mini Brief de un solo espacio al costado. Campo → Brief vende y devuelve exactamente lo pagado. Banco → Brief guarda cuando existe un hueco sin oferta ni unidad. Los huecos guardados se reservan frente al reroll.
- Menú con Bugaloo/Popow y selección de Dummy, Bugui y Atori. Solo los tipos elegidos entran en la Pouch; se exige al menos uno. El conjunto elegido se guarda localmente, se valida y se conserva en la revancha. El bot recibe el otro Monster y el mismo conjunto de Whelps disponibles.
- Dummy tiene 55 poses nuevas y Atori 60, para reposo, caminar, ataque y caída. Cada uno dispone de cinco vistas dibujadas y tres reflejadas para completar ocho orientaciones. Los fotogramas de contacto respetan el instante de impacto del simulador; las muertes ya iniciadas pueden terminar después de congelar a los supervivientes al cerrar la ronda.
- Los recortes de las hojas utilizan los huecos transparentes reales, evitando cortar los extremos de las poses de caída o incluir parte del personaje vecino. Pivotes en los pies, filtrado Point y fuentes preservadas.
- Se conserva el aumento visual del 20 % en el tablero, el centrado del área segura y el arte original de Bugaloo, Popow y Bugui.

La referencia del antiguo Clash Mini se documenta en [clash-mini-brief-reference.md](clash-mini-brief-reference.md). Se observó el vídeo oficial; la duración, bisagra y pose cerrada son decisiones propias de Monster Pouch. Atori se basó en una fotografía del álbum de MegaTrip: identificación, rasgos y límites en [atori-megatrip.md](atori-megatrip.md). Archivos, prompts completos y generación con la herramienta integrada: [arte-nuevo-resumen.md](arte-nuevo-resumen.md).

## Reglas conservadas

El Monster muerto no termina por sí solo la ronda. Los Whelps desplegados vivos siguen combatiendo; la eliminación de todo un equipo decide el vencedor y la eliminación simultánea produce empate. Permanecen los límites de 40 s de preparación, 40 s de combate, 10 s sin progreso y ticks de 0,1 s. La serie termina con tres victorias, sin un límite artificial de rondas.

El tablero conserva 6 × 10 casillas, A* ortogonal, ocupación y reservas únicas. Los impactos simultáneos se resuelven antes de decidir supervivientes; los impactos futuros se cancelan al cerrar la ronda. Compra con destino atómica, tokens y versiones de ofertas impiden consumir o cobrar operaciones inválidas, repetidas o caducadas.

Cada Whelp tiene cuatro copias, tres Tricks y una sola instancia propia. Cinco Whelps distintos pueden desplegarse y uno puede quedar en el banco. La colección puede superar siete tipos; el equipo admite hasta siete, con límite editable. La Pouch inicial se construye únicamente con cuatro copias de cada tipo elegido.

Los costes, ingresos, máscaras, estadísticas y efectos permanecen editables en MatchConfig. El balance de Atori es propio y provisional: no se atribuye al álbum o fabricante. La guía incluye sus valores y controles: [JUGAR.md](JUGAR.md).

## Validación ejecutada

| Comprobación | Resultado | Evidencia |
| --- | --- | --- |
| EditMode | **245 aprobadas, 0 fallidas, 0 omitidas**, final 2026-09-12 02:50:10Z | `Logs/local-validation/edit-mode.xml`, `edit-mode.txt` |
| PlayMode | **9 aprobadas, 0 fallidas, 0 omitidas**, final 2026-09-12 02:51:06Z | `Logs/local-validation/play-mode.xml`, `play-mode.txt` |
| Build Windows x64 | **Succeeded, 0 errores, 238.520.179 bytes**, registrado 2026-09-11 22:52:07 UTC−4 | `Logs/local-validation/command-5a51e239cec942349a2ec4f335a57cb6.txt` |

Los once casos nuevos del equipo cubren deduplicación, validación, copia defensiva, catálogo ampliable, límites, filtrado del suministro/ofertas, bot y revancha. Cuatro casos nuevos verifican la reproducción temporal, el contacto y la caída de sprites animados.

PlayMode recorre el EventSystem usando dispositivos Input System de ratón y toque emulado: selector, rechazo del equipo vacío, persistencia tras recargar la escena, ofertas filtradas, compra/colocación, copias y Tricks, inspección, banco, venta, reroll, pausa, cierre con el sprite nuevo, cambio de ronda, muerte del Monster con supervivientes, final de serie y revancha. También comprueba el movimiento final y la liberación recibidos en el mismo frame y el arrastre que caduca al terminar la preparación.

Se abrió el ejecutable de Windows-Brief y se observó el menú con los tres Whelps, la preparación con ofertas dentro del Brief, compra y colocación de Dummy por dos clics, sus poses de combate y el maletín cerrado. Evidencia:
- `Logs/local-validation/screenshots/build-team-menu.png`.
- `Logs/local-validation/screenshots/build-brief-open-v2.png`.
- `Logs/local-validation/screenshots/build-brief-closed-v2.png`.

La comprobación nativa del ejecutable no equivale a una prueba física de pantalla táctil ni a una revisión manual de cada pose y dirección. La venta por arrastre queda cubierta por la suite del EventSystem; no se declara verificada mediante la herramienta nativa de arrastre externa.

## Arte y límites del prototipo

Las nuevas acciones y vistas traseras son interpretaciones generadas, no animaciones extraídas de Clash Mini ni del fabricante de Gogo’s. Los dibujos generados se conservan sin modificar como fuentes; Unity crea derivados RGBA para la importación técnica. Dummy tiene 1.136.138 píxeles con alfa 0 y 437.400 opacos en su atlas, sin alfa parcial; su auditoría registra los 55 recortes.

Bugaloo, Popow y Bugui conservan sus recursos y animación articulada existente. Dummy y Atori sí usan fotogramas propios. El ataque de Bugaloo todavía no cuenta con una pose detallada de plantas expuestas; el proyectil estrella de Bugui y el balance general siguen siendo provisionales. El juego es local contra bot.

Para reconstruir el arte, los importadores aditivos son MonsterPouchClosedBriefSetup, MonsterPouchDummyArtSetup y MonsterPouchAtoriSetup. Si se ejecuta de nuevo la configuración base MonsterPouchArtSetup, deben reaplicarse los importadores nuevos antes de validar y compilar. Nunca se deben modificar los GUID manualmente.

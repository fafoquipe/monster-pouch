# Monster Pouch — guía local

Una partida de estrategia contra un bot local: prepara tu Monster y tus Whelps, observa el combate automático y consigue tres victorias de ronda. El juego usa recursos locales y está planteado para jugar sin conexión, con una ventana vertical de referencia de **540 × 960**.

Esta guía incluye a Anuik, Tauris, Kayon y Stein, las animaciones ampliadas de Bugui y el Brief. El estado de pruebas y compilación se registra en `docs/local-completion-progress.md`.

## Abrir el juego

Ejecuta:

```text
C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch\Builds\Windows-Coins\Monster Pouch.exe
```

Conserva el ejecutable junto a `Monster Pouch_Data`, `UnityPlayer.dll`, `MonoBleedingEdge` y los demás archivos de su carpeta de distribución. Para copiar el juego a otra ubicación, copia la carpeta completa de Windows.

Para abrir el proyecto en Unity Hub, selecciona:

```text
C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch
```

La versión declarada es **Unity 6000.3.9f1**. Abre `Assets/Scenes/main-scene.unity` y pulsa Play. El menú permite elegir **Bugaloo, Popow, Anuik o Tauris** y seleccionar los Whelps del equipo: **Dummy, Bugui, Atori, Kayon y Stein**. Toca sus figuritas para añadirlas o retirarlas. Debe quedar al menos un Whelp; la selección se guarda para la próxima partida. Solo los tipos elegidos pueden aparecer en el Brief. El bot recibe un Monster distinto y el mismo conjunto de tipos disponibles. Si ya tenías guardado un equipo, activa a Kayon y Stein en el menú para incluirlos.

## Nuevos personajes

- **Anuik:** lanza corazones. Una vez por combate, al caer se pone de cabeza durante 0,8 segundos y vuelve con el 50 % de su vida máxima. Durante la resurrección deja de ser un objetivo válido y quienes lo atacaban pueden elegir otro rival. La ronda espera su regreso. Una segunda muerte es definitiva.
- **Tauris:** usa la cabeza y cara de la referencia del usuario, pies cortos redondos y una escala del tablero un 25 % menor. Ataca cuerpo a cuerpo, con alcance de una casilla y mordida al contacto. Cada cuarto impacto acertado elimina a un Monster o Whelp sin importar su armadura. Anuik puede usar su resurrección si aún la conserva.
- **Kayon:** lanza monedas de oro con alcance de cuatro casillas; tiene 28 de vida y 2 de daño. Cada 6 segundos crea un Dummy de 14 de vida y 2 de daño junto a él, hasta dos vivos. Protege a Kayon para que alcance a invocar. Si no hay espacio libre espera; las invocaciones sobreviven a Kayon, pero desaparecen al terminar la ronda y no se venden ni consumen copias.
- **Stein:** dispara rayos con animación de carga e impacto eléctrico. Cada tercer impacto añade 4 de daño.
- **Bugui:** conserva su balance y ahora tiene dibujos de reposo, marcha, lanzamiento y caída, además de su proyectil mágico animado.

Los tiempos y límites que no especificó el usuario son valores iniciales ajustables. Las reglas completas están en `docs/expanded-roster-combat.md`.

Stein y Kayon usan los diseños proporcionados por el usuario: Stein color crema con pelo blanco y fórmula en la cara; Kayon azul con sus marcas blancas y bolsa de piedras doradas. Tienen retratos propios y 75/80 poses, respectivamente, de reposo, desplazamiento, ataque y caída.

## Preparar tu equipo

La preparación dura **40 segundos**. Usa el ratón o el toque sobre los mismos controles.

1. **Espera a que se abra el Brief.** Sus tres compartimentos muestran directamente tres figuritas disponibles, con un coste pequeño y la ficha de Moon Tokens, abreviados **MT**. Los Whelps elegidos pueden aparecer repetidos porque cada figura representa una copia. La Pouch es su suministro interno; no se dibuja una bolsa adicional.
2. **Compra y coloca con un gesto.** Arrastra una oferta a una casilla legal y libre del campo o al **banco**, el mini Brief de un solo espacio situado a la derecha. La compra y la colocación ocurren juntas al soltar. Si el destino, el saldo o la oferta ya no son válidos, no se cobra ni se consume la copia. También puedes tocar una oferta nueva para seleccionarla y después tocar su casilla para comprarla y colocarla. La compra deja libre el hueco de esa oferta.
3. **Mejora con copias.** Tocar una oferta de un Whelp que ya posees compra su siguiente Trick inmediatamente. También puedes arrastrar esa copia a un destino válido: se aplica la mejora y la unidad conserva su ubicación y posición. Cada Whelp tiene cuatro copias en total: la primera desbloquea la unidad y las otras tres conceden un Trick cada una. Siempre conservas una sola unidad de ese tipo.
4. **Reorganiza tus unidades.** Arrastra un Whelp desde el campo al banco para guardarlo, o desde el banco al campo para desplegarlo, sin comprarlo de nuevo. Del banco al Brief principal vuelve a guardarse **solo si hay un compartimento libre**, sin oferta ni otra unidad. Ese hueco queda reservado y los rerolls no lo sobrescriben. Puedes volver a seleccionar o arrastrar la unidad guardada para desplegarla.
5. **Pulsa LISTO** cuando termines. El combate también comienza al agotarse los 40 segundos.

**Para vender, arrastra un Whelp desplegado desde el tablero hasta el Brief principal abierto.** Se devuelve exactamente lo pagado por su base y sus Tricks y se retira del plantel. El destino reconoce de dónde viene: **campo → Brief vende; banco → Brief guarda si hay hueco libre**. La venta no necesita un hueco de almacenamiento. No hay botones separados de VENDER o GUARDAR. El Monster no se vende ni se guarda en el banco.

Una acción rechazada conserva el estado anterior y muestra el motivo. Durante combate no se puede comprar, vender ni cambiar posiciones.

## Tricks, Pouch y mejora del Monster

Mantén pulsada una figurita durante aproximadamente medio segundo para inspeccionarla, incluida una oferta todavía no comprada. También puedes inspeccionar tus unidades en el banco o el campo. Durante preparación, cuando ya posees ese Whelp y hay otra copia entre las ofertas, puedes elegir cuál de sus Tricks pendientes recibirá la próxima compra. Elegirlo no consume la copia ni cobra por adelantado.

Si compras sin escoger, recibes el primer Trick que falte según su lista. Por ejemplo, después de elegir primero el tercero, las compras automáticas siguientes conceden el primero y el segundo. Las estrellas muestran los Tricks adquiridos: de cero a tres, sin una cuarta mejora adicional.

Cada preparación suma **cuatro rerolls gratuitos** y conserva los sobrantes. El reroll cierra la caja, cambia las ofertas y vuelve a abrirla para revelar las nuevas figuritas. Gasta un uso, devuelve al suministro las ofertas no compradas y extrae hasta tres copias nuevas, respetando los compartimentos que guardan unidades. Al comenzar otra ronda se conservan las ofertas no compradas y solo se rellenan los huecos libres. La Pouch inicial contiene cuatro copias de cada Whelp elegido en el menú; pueden aparecer tipos repetidos en sus tres ofertas.

El Brief se abre al comenzar la preparación y se cierra al empezar el combate. Espera a que termine de abrirse para usar sus ofertas. El cierre y la apertura duran aproximadamente **0,32 segundos cada uno**; son una animación propia del proyecto. La posición cerrada usa ahora un dibujo propio del maletín completamente cerrado, con tapa exterior y una sola cerradura. La referencia visual y sus límites están registrados en `docs/clash-mini-brief-reference.md`.

Para comprar la **mejora del Monster**, mantén pulsado tu Monster en el campo durante preparación. En su ficha de inspección aparece **MEJORAR · … MT**, con el coste correspondiente; no hay un botón de mejora en la bandeja principal. Sus efectos se aplican en el combate inmediatamente posterior. Puedes terminar de colocarlo durante esa preparación; su posición de despliegue queda bloqueada **desde la siguiente ronda**. Este bloqueo de recolocación no impide que participe normalmente en el combate automático.

## Combate, pausa y resultado

Cada personaje fija un rival al comenzar a perseguirlo y lo mantiene hasta derrotarlo. Un enemigo que se acerque, pierda más vida o aparezca invocado no cambia ese objetivo. Si el rival se mueve, lo persigue; si el camino se bloquea temporalmente, espera para continuar. Al reiniciarse el personaje o comenzar otra ronda puede elegir de nuevo. Anuik es la excepción: al resucitar libera a sus atacantes, y su regreso no les quita el nuevo rival que hayan fijado.

La ronda termina cuando un equipo pierde **todas sus unidades desplegadas**, al alcanzar el límite de **40 segundos** de combate o por **10 segundos sin progreso útil**. La muerte del Monster no termina por sí sola la ronda: los Whelps supervivientes siguen luchando. El resultado explica la salida correspondiente.

Un empate avanza el número de ronda y no concede puntos. La serie continúa hasta que un jugador alcanza **tres victorias**; no existe un máximo de rondas. Pulsa **SIGUIENTE RONDA** para continuar. Los Whelps conservan sus copias y Tricks y recuperan su vida para el nuevo combate.

Puedes inspeccionar unidades propias o rivales mientras combaten. La ficha muestra los valores actuales sin detener la simulación; la del rival solo revela sus mejoras adquiridas.

Usa el botón de pausa de la esquina superior derecha o **Esc**. La pausa detiene el reloj y el combate. Desde ella puedes continuar, consultar la ayuda, ajustar el volumen o volver al menú. Al terminar la serie, **REVANCHA** inicia una partida nueva con el mismo Monster y los mismos tipos de Whelps y restablece el progreso de la partida. El equipo elegido y el volumen se guardan como preferencias locales.

## Balance actual configurable

Estos son **valores iniciales provisionales**, centralizados en `Assets/Resources/MonsterPouch/MatchConfig.asset`. No representan un balance final aprobado.

Los ingresos por ronda son **6, 4, 3, 2 y después 1 MT**. El dinero restante se conserva. Por ahora, vender también devuelve al suministro las copias compradas de ese Whelp; la opción `ReturnSoldCopies` permite revisar esa decisión.

| Unidad | Vida base | Daño base | Intervalo de ataque | Alcance | Intervalo de movimiento | IQSpeed | Coste base |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Bugaloo | 110 | 10 | 1,2 s | 1 | 0,6 s | 4 | Monster inicial |
| Popow | 90 | 8 | 0,85 s | 1 | 0,4 s | 6 | Monster inicial |
| Dummy | 45 | 5 | 1,1 s | 1 | 0,6 s | 3 | 2 MT |
| Bugui | 28 | 4 | 0,9 s | 3 | 0,5 s | 5 | 3 MT |
| Atori | 36 | 6 | 1,05 s | 1 | 0,4 s | 7 | 3 MT |
| Anuik | 90 | 7 | 1,3 s | 3 | 0,6 s | 4 | Monster inicial |
| Tauris | 100 | 6 | 1,4 s | 1 | 0,6 s | 4 | Monster inicial |
| Kayon | 28 | 2 | 1,5 s | 4 | 0,6 s | 3 | 3 MT |
| Stein | 26 | 6 | 1,2 s | 4 | 0,6 s | 5 | 3 MT |

La tabla muestra los datos base antes de sumar habilidades y mejoras. **IQSpeed decide prioridades de movimiento** y es independiente de las cadencias. El combate usa ticks de 0,1 s y redondea los intervalos al siguiente tick; por ejemplo, 0,85 s produce un intervalo lógico de 0,9 s. La anticipación del ataque (`AttackWindup`) es 0,3 s para Bugaloo, Atori y los cuatro personajes nuevos; 0,2 s para Popow, Dummy y Bugui.

| Unidad | Habilidad base | Mejoras y costes |
| --- | --- | --- |
| Bugaloo | Panzazo: +3 de daño cada tercer impacto | Panza de acero: +30 de vida y +2 de daño; 4 MT |
| Popow | Derechazo: +1 de daño en cada impacto | Puños veloces: intervalo ×0,7 y +1 de daño; 4 MT |
| Dummy | Acolchado: 1 de armadura; cada golpe dañino causa al menos 1 | Resistente: +18 de vida, 2 MT. Refuerzo: +1 de armadura, 2 MT. Remiendo: cura 2 por impacto, 3 MT |
| Bugui | Chispa: +3 de daño cada tercer impacto | Brillo: +2 de daño, 2 MT. Destello: intervalo ×0,75, 3 MT. Chispa larga: +1 de alcance, 2 MT |
| Atori | Cabezazo certero: +4 de daño cada tercer impacto; busca un enemigo alcanzable con poca vida | Cabeza dura: +1 armadura, 2 MT. Impulso: +2 daño, 2 MT. Aguante: +16 vida, 3 MT |

Dummy, Bugaloo y Popow prefieren la formación delantera; Bugui, Anuik, Kayon y Stein la trasera; Atori y Tauris la media. La máscara inicial permite seis columnas por cinco filas: 30 casillas de despliegue por lado, filas lógicas 5–9 para Blue y 0–4 para Red. Las casillas del rival no admiten despliegue. Es una configuración editable; el eje Y lógico aumenta hacia abajo en la representación. La política de objetivo es el enemigo alcanzable más cercano, excepto Atori, que prioriza uno con poca vida.

Para ajustar el balance, detén Play y selecciona **MatchConfig.asset** en el panel Project. Edita `Units`, sus `BaseAbility`, `Tricks`, `MonsterUpgrade`, `TargetPolicy` y `Formation`, o los campos `RoundIncome`, `BlueDeployment` y `RedDeployment`. Inicia una partida nueva para revisar los cambios. Los cambios en el proyecto necesitan un build nuevo para aparecer en el ejecutable.

El catálogo de contenido actual incluye **Dummy, Bugui y Atori** como Whelps. La colección puede ampliarse; cada equipo admite hasta siete tipos elegidos, cinco Whelps distintos en campo y uno en el banco. `TeamWhelpLimit` permite reducir el límite del equipo.

## Arte provisional y archivos principales

Se conserva el arte original de Bugaloo, Popow y Bugui. **Dummy tiene 55 poses nuevas y Atori 60**, con reposo, caminar, ataque y caída. Cada hoja contiene cinco vistas dibujadas; las otras tres orientaciones reflejan las correspondientes vistas simétricas. Los ataques mantienen el instante de contacto del simulador y la caída termina aunque se cierre la ronda. Bugaloo, Popow y Bugui mantienen sus animaciones articuladas y las vistas aportadas por el usuario; el proyectil de Bugui sigue siendo una estrella provisional. Referencias y prompts: `docs/dummy-animation-art.md`, `docs/atori-megatrip.md` y `docs/brief-closed-prompts.md`.

En este ajuste, las figuras del tablero se muestran un **20 % más grandes**, manteniendo el anclaje de sus pies y sus casillas. Los retratos principales crecen alrededor de un **21 %** y las imágenes se ajustan con el pivote centrado. Estos cambios son visuales y no modifican las estadísticas ni la ocupación del tablero.

El catálogo visual está en `Assets/Resources/MonsterPouch/UnitArt.asset`. Las referencias, pivotes y estados están descritos en `docs/local-unit-art.md` y `docs/unit-art-import-report.md`. Mantén el filtrado Point y las referencias de los sprites al editar recursos de pixel art; no cambies sus GUID manualmente.

| Parte | Archivo o carpeta | Responsabilidad |
| --- | --- | --- |
| Configuración | `Assets/scripts/gameplay/match/MatchConfig.cs` | Tipos de unidad, balance y máscaras; el asset del mismo nombre contiene los datos editables |
| Economía y plantel | `Assets/scripts/gameplay/match/pouch-state.cs` | Copias, ofertas con identidad, compra y colocación atómicas, huecos reservados del Brief, rerolls, Tricks y reembolsos |
| Partida | `Assets/scripts/gameplay/match/match-controller.cs` | Menú, preparación, bot, combate, puntuación, pausa y reinicio |
| Combate | `Assets/scripts/gameplay/match/combat-simulation.cs` | Ticks, objetivos, ataques, impactos y final de ronda |
| Tablero | `Assets/scripts/gameplay/board/` | 60 celdas, ocupación, rutas y reservas; `BoardWorldMapper` convierte las coordenadas |
| Presentación | `Assets/scripts/gameplay/presentation/` | Catálogo de sprites y animación, separados del daño y la ocupación |
| Interfaz local | `Assets/scripts/local/local-game-ui.cs` | Brief, ofertas, gestos, fichas, barras, sonido y composición vertical |
| Pruebas | `Assets/tests/edit-mode/Tests/` y `Assets/tests/play-mode/` | Reglas e integración; su presencia no sustituye el registro de ejecución |

La vida de combate y la economía pertenecen a cada partida; los assets describen los tipos compartidos. La simulación aplica el daño y la presentación responde a sus eventos. Para diagnosticar un problema de colocación, compara la celda del actor, la ocupación del tablero y la conversión de `BoardWorldMapper`, en lugar de compensarlo con offsets en la interfaz.

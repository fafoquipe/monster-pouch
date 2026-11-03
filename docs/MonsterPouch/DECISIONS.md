# Decisiones vigentes

La fuente histórica es `ROADMAP_SOURCE_v1.2.md`, sin alterar. Sus MP-DEC-001 a MP-DEC-019 siguen vigentes salvo sustitución explícita.

| ID | Fecha | Estado | Decisión y alcance |
|---|---|---|---|
| MP-DEC-020 | 2026-10-20 | APROBADO, creador en este chat | Mantener el banco exactamente como funciona; guardar cuesta 0 Moon-Kens. Sustituye la ampliación de reembolso/redespliegue de MP-DEC-004 para este trabajo. No añadir cobro de regreso ni reembolso al guardar |
| MP-DEC-021 | 2026-10-20 | APROBADO, creador en este chat | Comprar copias sigue mejorando al personaje en campo. No introducir elección «otra unidad o mejora». Al guardar, se ocultan copias no desplegadas; conservar unidades en el mapa; rechazar un segundo guardado del mismo tipo |
| MP-DEC-022 | 2026-10-20 | APROBADO, creador en este chat | Comunicaciones de desarrollo en español; esto no cambia el idioma principal del juego |
| MP-DEC-023 | 2026-10-20 | APROBADO, creador en este chat | Al empezar el desempate se bloquea todo daño, incluidas quemaduras y efectos pendientes. El Monster vivo aporta el 100 % de su HP actual primero; después los Whelps vivos aportan uno por uno el 50 % de su HP actual. Ambos equipos se cuentan en paralelo. Los muertos y todas las invocaciones (flores, dummies, etc.) aportan cero. Esta misma suma determina el desempate y su presentación; sustituye al campeón Whelp que antes conservaba toda su vida |
| MP-DEC-024 | 2026-10-20 | APROBADO, creador en este chat | Números de vida verdes. El número del Whelp se corta visualmente por la mitad en diagonal y de ese corte sale el 50 % de su vida actual. Al entrar en la barra el número se agranda y se encoge rápidamente. Sustituye la presentación textual «HP ÷ 2»; conserva los aportes y exclusiones de MP-DEC-023 |
| MP-DEC-025 | 2026-10-20 | APROBADO, creador en este chat | El jugador nuevo elige su Monster inicial entre Bugaloo y Popow. Sustituye la referencia del roadmap a un Monster que revive al 50 % y la aclaración intermedia «será Bugaloo». Mantener Atori, Blotan, Bugui, Flo, Gochan, Trimol y Atong como los siete Whelps iniciales. Conservar los mazos existentes |
| MP-DEC-026 | 2026-10-20 | APROBADO, creador en este chat | Mostrar los números por encima del personaje, con borde más negro. Números, orbes y vida del enemigo en rojo; aliados en verde. Después del corte y de mostrar la mitad de HP, el número se convierte en orbe durante el viaje a la barra. El pulso de llegada se aplica al orbe. Colores relativos al jugador local, también cuando juega desde el lado rojo |
| MP-DEC-027 | 2026-10-20 | APROBADO, creador en este chat | Las dos piezas del número cortado se desvanecen gradualmente mientras aparece el aporte y se convierte en orbe; no desaparecen de golpe al comenzar el vuelo |
| MP-DEC-028 | 2026-10-20 | APROBADO, creador en este chat | Sin decimales: redondear hacia arriba («upper»). Cada Whelp vivo aporta ceil(HP actual / 2), antes de sumar los aportes; 13 HP → 7, 1 HP → 1. Aplicar el mismo valor al ganador y a la animación. Los contadores de las barras también muestran enteros; la interpolación gráfica puede seguir siendo continua. Monster vivo conserva el 100 % y muertos/invocaciones siguen excluidos |
| MP-DEC-029 | 2026-10-20 | APROBADO, creador en este chat | «Esas cosas antiguas no deben ser usadas ya». Las decisiones y funciones vigentes prevalecen sobre requisitos/test antiguos. No restaurar gestos descartados, cantidades antiguas de personajes/fotogramas, balance o encuadres anteriores para satisfacer una prueba. Actualizar las pruebas aplicables; retirar del circuito activo las exclusivas de funciones descartadas y conservarlas como respaldo fuera de Assets. Esto no autoriza borrar ni rediseñar arte actual |
| MP-DEC-030 | 2026-10-20 | ACLARADO, creador en este chat | Distinguir ID interno y nombre actual del personaje. Por ejemplo, `anuik` identifica a Aniku. Usar `CharacterDefinition.General.DisplayName` y su copia de ejecución para nombres visibles, explicaciones y pruebas de UI. Conservar IDs, referencias y datos guardados. No deducir el nombre visible a partir del ID ni restaurar nombres antiguos. Catálogo auditado en CHARACTER_NAMES.md |
| MP-DEC-031 | 2026-10-20 | ACLARADO, creador en este chat | El flujo es exclusivo de cuentas nuevas: elegir el Monster inicial entre Bugrum/Pombú y, después, entrar al tutorial. No mostrar la elección ni iniciar el tutorial por abrir el juego con una cuenta existente. Sustituye la primera integración que terminaba la elección en el menú. Conservar perfiles, mazos y acceso existentes; esta aclaración no concede habilidades, propiedad, monedas ni premios pendientes de definir |
| MP-DEC-032 | 2026-10-20 | APROBADO y precisado, creador en este chat | En el combate sencillo del tutorial, rival = Monster inicial no elegido. El creador aprobó la configuración y después precisó «50% de TODAS sus estadisticas» y «x2 si es a peor»: reducir a la mitad las magnitudes favorables y duplicar las inversas que lo debilitan. Sustituye la propuesta inicial que conservaba velocidades. Ajuste exclusivo de esa instancia; no modifica balance global, identidad ni arte |
| MP-DEC-033 | 2026-10-20 | APROBADO, creador en este chat | Solo tres Whelps iniciales: Atori, Bugui y Blotun, IDs `atori`, `bugui`, `blotan`. Sustituye los siete iniciales de MP-DEC-025 para cuentas nuevas. Los nombres visibles se resuelven desde el catálogo actual conforme a MP-DEC-030. Conservar personajes y mazos de cuentas anteriores; no retirar sus concesiones históricas |
| MP-DEC-034 | 2026-10-20 | APROBADO, creador en este chat | Los Whelps desbloqueados se incorporan automáticamente al Brief hasta completar siete. Con menos de siete disponibles se usan todos; con siete o más, la entrada a partida exige siete. Conservar selecciones válidas, sin duplicar personajes ni inventar desbloqueos para llenar espacios. Esta regla no aumenta el límite de cinco Whelps en campo ni cambia el banco |
| MP-DEC-035 | 2026-10-20 | APROBADO, creador en este chat | «Los personajes bloqueados aparecen con un candado cerrado y mas oscuros». Aplicar tanto a Monsters como a Whelps: retrato oscurecido y candado cerrado visible. Al desbloquearlos desaparecen el candado y el oscurecimiento. La ficha permanece consultable y Usar sigue deshabilitado mientras el personaje esté bloqueado |
| MP-DEC-036 | 2026-10-22 | COPAS, DOS ARENAS Y PRUEBA 150 APROBADOS; BALANCE COMERCIAL PENDIENTE, creador en este chat | Competitivo de cuenta con COPAS y recorrido finito de SOLO DOS arenas gratuitas actuales: Parque de Bolas (`classic`, antes Parque de Juegos) y Campo Real (`royal`). El creador aprobó probar inicio 0 y mínimo 0; `classic` 0–149 con victoria +30 y derrota −5; `royal` desde 150 sin límite superior, con victoria +30 y derrota −30. Pérdida según el rango propio previo al resultado. No aplicar −25 de la referencia futura penúltima a estas dos arenas. Todos pueden jugar contra todos, sin separar por arena/copas por ahora, sin rachas ni ajuste por diferencia del rival; se mantienen las salas existentes, sin cola nueva. Matchmaking por copas, ajuste según diferencia del rival, unos tres tableros nuevos y repeticiones quedan pospuestos; el ejemplo +32 ante diferencia de 60 no se activa. Grand Cas Hotel (`lounge`) es de pago y queda fuera del recorrido; al final se puede elegir entre gratuitos superados y Hotel si se posee, mientras las copas siguen subiendo sin nuevas arenas. No activar compras, retirar accesos, inventar arte o rareza formal «legendaria», ni renombrar Hotel por la mención «Grand Casino Hotel». La aprobación numérica es para prueba, no balance comercial definitivo. Pendientes modos puntuables, premios, abandonos, uso del Hotel antes del final, efecto de descender sobre selección y temporadas. No es maestría ni XP; no se conecta equipamiento automático ni adjudicación de puntos de producción. Ver LADDER_SYSTEM.md. Maestría sigue congelada en staging, sin integrar |

Precisiones posteriores del creador el 22 de octubre para MP-DEC-036: móvil exclusivamente en vertical normal, sin horizontal ni boca abajo; PC puede usar ventana ancha. Diseño visual del recorrido/selector aprobado a partir de la referencia `arena-presentation-staging-20261022`, con corrección de los números borrosos mediante texto nativo nítido. La aclaración no pedía rediseñar botones. El nombre visible debe ser **TABLEROS / BOARDS**, con «Tablero actual» y «Siguiente tablero»: el creador rechazó «ARENAS». El umbral se presenta con «+150» seguido del símbolo de trofeo, sin la frase «Desde 150 copas». Se autorizó crear propuestas del trofeo, pero el original no fue aprobado: se pidió más pequeño, menos brillo y una familia PLATA, ORO y LUNAR, con textura cristalina tipo diamante para el último. La referencia posterior trophy-reference-20261022.png («parecido a este, pero no igual») y la precisión «es en PIXELART» sustituyen las primeras variantes: copa de forma sencilla en PIXEL ART, contorno oscuro, paleta reducida y reflejos mínimos; Lunar conserva cristal/diamante con brillo contenido. Las apariencias finales esperan aprobación; la distribución de tres trofeos entre dos tableros no está definida y no autoriza inventar umbrales. Las menciones anteriores a arenas se conservan como historial, sin imponer ese nombre a la UI. La aprobación visual no define modos puntuables, acceso al Hotel antes del final o selección al descender, ni convierte las copas de prueba en progreso real de cuenta.

Texto histórico de la aclaración bancaria de MP-DEC-020, sustituida por MP-DEC-042: «tiene que funcionar como ahora mismo funciona, pero con la diferencia de que ahora no cuesta poner en el banco el elixir, fin».
En el código el recurso se denomina Moon-Ken; no se crea una quinta moneda llamada elixir.

Última precisión del creador sobre MP-DEC-023: «si el monster u otros personajes ya murieron, no suman sus vidas».
Se conserva el desempate existente por tablero cuando las sumas coinciden y existe un representante inequívoco. Ninguna barra visual inventa un vencedor.

Decisiones técnicas: conservar rama/cambios previos y hacer respaldo externo; mantener IDs de Tokoro y su habilidad para preservar referencias y arte; versionar el protocolo online si cambian reglas de combate.

**MP-DEC-037 — 2026-10-22, aprobado explícitamente por el creador.** Mostrar una circunferencia blanca translúcida que consume el tiempo de pulsación larga sobre una unidad y abre su ficha de habilidades al completarse, conservando el gesto y duración actuales. La aclaración posterior «solo que se vean a color las habilidades que se pueden comprar» prevalece sobre el criterio inicial de presencia en el Brief: las habilidades que no puedan comprarse en ese momento aparecen grises, pero todas conservan su consulta/descripción. No cambia costes, propiedad ni reglas de adquisición. En español, el nombre visible es **Maleta**; inglés conserva Brief y los identificadores internos permanecen intactos. El contador central conserva sus tiempos y animación, con contorno más definido, naranja en 5–2 y rojo oscuro intenso en 1. Esta decisión autoriza estos cambios visuales; no aprueba las propuestas de trofeos ni sus rangos.

La comprobación de propiedad de Monsters en cuentas nuevas es una corrección técnica de la colección desbloqueada/bloqueada ya definida en §4.2 de `ROADMAP_SOURCE_v1.2.md`, no una concesión nueva. `StarterInventory` permite seleccionar y usar solo personajes poseídos; elegir Bugrum no desbloquea Pombú ni viceversa. Un desbloqueo posterior de otro Monster lo vuelve elegible sin seleccionarlo automáticamente ni aumentar el número de Whelps. `Legacy` conserva el acceso anterior sin fabricar recibos de propiedad. MP-DEC-035 define su presentación visual; no cambia las fuentes de desbloqueo ni resuelve las habilidades iniciales pendientes.

Verificación de la implementación acotada de MP-DEC-031: 1090/1090 EditMode y 19/19 PlayMode, informes `new-account-tutorial-edit.xml` y `new-account-tutorial-play.xml` en `Logs/roadmap-v1.2`. Cubre entrada de invitados nuevos y Primer contacto; no declara implementados los módulos 2–10 ni aprueba recompensas o cuentas vinculadas/backend. Detalle en `QA_CHECKLIST.md`.

**MP-DEC-038 — 2026-10-22, aprobado explícitamente por el creador.** Usar la imagen adjunta `codex-clipboard-16683ac2-fa02-44de-8199-7fec7bc581cf.png` como pantalla de carga en lugar del logo aislado. El creador permitió una transición como la del mapa sin mostrar el mapa, o un desvanecimiento; se eligió el **desvanecimiento** dentro de esas alternativas autorizadas. Se conserva la imagen completa de **941×1672**, sin redibujar, recortar, reencodificar ni alterar sus píxeles. SHA256 del archivo original y su copia: `48907AA47FBB48803548C6A1AAAD38DA597A6D0BA7920DEA1D4418D68CB930C1`. Recurso de ejecución: `MonsterPouch/UI/Branding/loading-screen-supplied`, GUID `8856ea35e1c74035ad8846188c5a34ad`.

La adaptación técnica contiene toda la imagen dentro del área segura, conserva su proporción en móvil vertical y PC y cubre los espacios libres con el color oscuro ya existente. Se reutilizan los tiempos de la transición actual: mínimo visible opaco **0,64 s** y fundido **0,44 s**, después de que la vista de destino esté lista; no se modifica `Time.timeScale`, no se inventa una barra de progreso ni se añade arte. El arranque real tiene una sola escena habilitada. Se desactiva el splash anterior de logo, conservando su asset y los demás usos del logo, incluida la ficha de XP. El overlay bloquea la entrada durante su presentación y libera entrada, referencias y objeto al terminar o cancelarse.

Esta aprobación no cambia MP-DEC-031: las cuentas existentes conservan su lobby; las nuevas eligen Monster y continúan al tutorial, y las pendientes reanudan la ruta guardada. No se añade una carga ficticia a los modales, al cambio de tablero ni a cada reconstrucción de UI. Los IDs, propiedad, compras y demás recursos permanecen sujetos a sus reglas vigentes. El cierre de MP-DEC-037 sigue válido y los trofeos PIXEL ART y su distribución continúan pendientes de aprobación.

<!-- MP-DEC-038-VALIDATION-BEGIN -->
**Validado en Unity el 2026-10-22:** 10/10 EditMode y 9/9 PlayMode, ambos procesos con salida 0. Play incluye cuatro casos de carga, cuatro de mantenimiento de pulsación y uno de revisión de estilo. Se inspeccionaron las seis capturas nativas: imagen completa sin recorte ni estiramiento, márgenes oscuros para adaptar la proporción, fundido visible y lobby listo con entrada operable. La integración confirmó los 11 hashes de fuentes previstos. Informes y alcance detallados en `QA_CHECKLIST.md`; no se ha probado una build instalada ni un móvil físico. El cierre previo de MP-DEC-037 permanece intacto.
<!-- MP-DEC-038-VALIDATION-END -->


**MP-DEC-039 — 2026-10-22, correcciones solicitadas explícitamente por el creador.** Al arrastrar una copia de la Maleta a una casilla real del tablero, si su unidad ya está en posesión del jugador, la compra mejora esa misma unidad aunque el punto de caída esté ocupado por otro personaje propio o rival. No mejora al ocupante, no intercambia posiciones y no crea otra instancia. Se utiliza la compra existente, incluida la habilidad seleccionada y su coste, con las comprobaciones vigentes de oferta/token, saldo, fase, bloqueos y límite de cuatro copias. Se conserva el comando online de compra y su reproducción. Esta corrección no cambia el despliegue de una unidad nueva, las rutas de banco, venta o caída fuera del tablero, ni el almacenamiento gratuito y supresión de ofertas ya aprobados.

Se retira únicamente la línea diagnóstica pequeña situada debajo de los resultados. Se mantienen título, tarjetas, puntuación, ganador, transición, comparación de vida y mensajes de rechazo necesarios. La explicación diagnóstica permanece en los datos del resultado; no se elimina el sistema de avisos. El aviso desconocido «Listo» queda **aplazado por petición del creador**, sin atribuirle una causa ni bloquear este trabajo con otra pregunta.

Las casillas válidas conservan el mismo conjunto y color verde, con mayor intensidad y grosor del contorno existente. Al soltar una miniatura propia fuera del tablero, después de descartar los destinos de banco y venta, desaparece su previsualización y la unidad permanece en su origen. Se elimina de ese caso la frase «Elige una casilla iluminada, el bench o el Brief.» y su rechazo asociado; se conservan los errores específicos de una casilla inválida y de fondos insuficientes. No se autorizan nuevas casillas, movimientos, habilidades, precios ni reglas de compra.

<!-- MP-DEC-039-VALIDATION-BEGIN -->
**Validación enfocada cerrada: 98 casos únicos EditMode y seis casos únicos PlayMode verificados.** El primer Edit aprobó 97/98: la prueba de pausa usaba una partida online, que no admite pausa. Se corrigió solo ese caso para usar una pausa local real y la repetición de su fixture pasó 10/10; las otras 88 pruebas no se modificaron. El primer Play aprobó 5/6 y falló únicamente al restaurar las dimensiones de una GameView libre redimensionada por el Editor visible. Tras corregir solo el helper de pruebas, las dos fixtures afectadas pasaron 4/4; los dos casos de resultados ya aprobados no usan ese helper. Los reintentos no se suman como casos nuevos. Se inspeccionó la captura nativa de casillas reforzadas; informes y reparaciones en QA_CHECKLIST.md. Sin prueba en móvil físico ni red remota; MP-DEC-037 y MP-DEC-038 conservan sus cierres independientes. El resultado acreditado es Passed del Test Runner; Unity visible permanece abierto y no se atribuye un código de salida de proceso a estas tandas. Se verificaron 13/13 hashes de archivos integrados tras las reparaciones de pruebas.
<!-- MP-DEC-039-VALIDATION-END -->


**MP-DEC-040 — 2026-10-22, ajustes de UI aprobados explícitamente por el creador.** En el lobby, el acceso a expendedoras se coloca arriba y el de misiones debajo, conservando sus acciones. Al seleccionar una unidad o arrastrar una oferta o una unidad del campo/banco, se iluminan **solo los bordes de las casillas de la mitad propia**, conservando su superficie; la mitad rival recibe un filtro rojizo suave y translúcido. La identificación es relativa al lado real del jugador (LocalSide), también si juega en el lado rojo o la vista está invertida. Es una indicación visual: no añade casillas legales, modifica compras, cambia ocupantes ni sustituye las validaciones de colocación. Se conserva el cierre MP-DEC-039 y el aviso desconocido «Listo» sigue aplazado.

<!-- MP-DEC-040-VALIDATION-BEGIN -->
**MP-DEC-040 validado el 2026-10-22 con ocho casos únicos PlayMode aprobados.** El orden del lobby pasó 1/1 a 320×568, 393×852 y 768×1024; el tablero y sus regresiones pasaron 7/7, sin fallos ni omitidas. Se inspeccionaron tres capturas nativas 540×960: expendedoras sobre misiones sin solapamiento, solo bordes verdes propios y filtro rojizo rival, también desde el lado rojo con la vista invertida. La superficie propia se conserva y los sprites quedan por encima del filtro. Se verificaron 8/8 hashes finales de los archivos integrados. Informes, horarios y alcance en QA_CHECKLIST.md. La evidencia es Passed del Test Runner en Unity visible; no se atribuye código de salida del proceso ni prueba en móvil físico. MP-DEC-039 permanece intacto.
<!-- MP-DEC-040-VALIDATION-END -->

**MP-DEC-041 — 2026-10-22, ajuste solicitado explícitamente por el creador.** Acercar el HUD de vida a la cabeza de cada personaje, ligeramente por encima y sin taparla. El punto se calibra por personaje y sigue su tamaño visual y elevación. Si se superponen HUDs, el personaje más próximo al jugador en pantalla se dibuja delante. Se conserva el HUD existente: esta petición no autoriza redimensionar personajes, cambiar estadísticas, habilidades, mecánicas o la animación del desempate.

<!-- MP-DEC-041-VALIDATION-BEGIN -->
**MP-DEC-041 integrado y validado el 2026-10-23, fecha local: siete casos únicos PlayMode aprobados.** El primer intento aprobó 5/7: cuatro regresiones del HUD y el orden por profundidad; los otros dos fallaron por preparación de las pruebas (catálogo Characters vacío en la configuración de ejecución y búsqueda de dummy como personaje normal). Se reparó únicamente la fixture y sus tres casos pasaron 3/3; el caso de profundidad repetido no se cuenta dos veces. Root inspeccionó ocho capturas nativas 540×960: los 23 personajes, aumento al 125 %, solapamiento desde ambas perspectivas y vida/energía/escudo/estados. Las barras quedan justo encima de las cabezas y el HUD más cercano se dibuja delante. Se verificaron las fuentes con la reparación de prueba y la excepción concurrente de Tauruk documentadas en QA_CHECKLIST.md; se conserva su anclaje y los ajustes ajenos. La evidencia es Passed del Test Runner, sin atribuir salida de proceso ni prueba en móvil físico. Los cierres anteriores permanecen intactos.
<!-- MP-DEC-041-VALIDATION-END -->

## MP-DEC-042 — 2026-10-23, banco: reembolsos, reservas y fusión

Aprobación explícita del creador en este chat; sustituye MP-DEC-020 en estas reglas económicas y de coexistencia:

- Campo a banco: devuelve la inversión realmente pagada y conserva las mejoras. Retirarlo vuelve a cobrar esa inversión; vender la unidad guardada devuelve 0, porque ya fue reembolsada.
- Oferta nueva de la Maleta a banco: reserva gratuita, con pago del precio base al desplegar. Se conserva el límite de tres espacios de banco.
- Oferta de un tipo ya presente en campo a banco: la unidad del campo permanece y se guarda otra copia como reserva. Mientras exista la reserva, dejan de aparecer más ofertas de ese tipo. Se mantiene el total de cuatro copias por tipo.
- Campo a banco con reserva del mismo tipo: devuelve la inversión de la unidad del campo, retira esa unidad y la reserva hereda todas sus mejoras más la siguiente pendiente en orden numérico (primera, segunda, tercera). Retirar el resultado cuesta precio base más el coste de todas sus mejoras; venderlo en banco devuelve 0.
- Banco a campo con el mismo tipo ya desplegado: se fusionan y se paga la mejora; permanece una sola unidad del tipo en campo. No se habilitan dos unidades independientes del mismo tipo en campo ni se cambia el límite de cinco Whelps.
- Los movimientos rechazados no deben cobrar, devolver, consumir copias ni cambiar ubicaciones. Repetir guardar, retirar o vender no debe duplicar devoluciones. Tutorial y reproducción online deben utilizar las mismas reglas; no se cambian los regalos o saldos iniciales del tutorial.

<!-- MP-DEC-042-VALIDATION-BEGIN -->
**MP-DEC-042 integrado y validado: 96/96 EditMode y 16/16 PlayMode aprobados.** Se revisaron 2 capturas y se verificaron los hashes de 20 archivos integrados. Registro de cierre: bank-economy-integration-20261023/final-validation.json. La evidencia corresponde al Test Runner del Editor; no se atribuye un código de salida de proceso ni validación en móvil físico o red remota. MP-DEC-041 conserva su cierre independiente; este alcance no completa el roadmap global.
<!-- MP-DEC-042-VALIDATION-END -->


## MP-DEC-043 — 2026-10-24, finalizar la habilidad antes de preparar el siguiente básico

Aprobación explícita del creador: el personaje debe terminar su habilidad/súper y su regreso a reposo antes de iniciar el siguiente ataque básico. Ante la consulta sobre acumular dos esperas, confirmó **«Una sola preparación desde que vuelve a idle»**. La regla aplica a todos los personajes con una acción corporal de habilidad; no añade una pausa de la misma duración antes de la preparación del golpe.

El valor vigente `CharacterAttack.Windup` / `UnitStats.AttackWindup` es la **Preparación del primer golpe**: cubre la preparación animada hasta contacto o lanzamiento. Empieza una vez terminado el retorno de la habilidad, con el mismo valor del personaje, y no se cobra dos veces. Se incluyen las fases ya configuradas de preparación, pose y regreso; los desplazamientos de habilidad y, en Hymal (`hymay`), su aterrizaje existente de 0,16 s. La extracción corporal de Blotun (`blotan-income`) también termina antes de otro básico; conserva su duración existente de 0,72 s cuando usa el valor alternativo, su intervalo y sus recompensas.

La simulación determina los límites temporales con su reloj fijo, sin consultar el estado del renderer. Se conserva el redondeo a ticks y la reproducción online. Una preparación de básico interrumpida por la extracción no puede resolver un golpe nuevo dentro de esa animación; los proyectiles ya lanzados siguen su recorrido. Las señales de efectos pasivos sin una nueva acción corporal —por ejemplo, radiación, sigilo o una recompensa de una baja— no crean una segunda habilidad ni una espera adicional. No se cambian estadísticas, daño, precios, arte, velocidades o duraciones de animación para simular esta regla.

<!-- MP-DEC-043-VALIDATION-BEGIN -->
**MP-DEC-043 integrado y validado en el Editor.** El conjunto compartido con MP-DEC-044 tiene 207/207 casos únicos EditMode y 5/5 PlayMode aprobados, más 3 casos nuevos de compatibilidad online aprobados; los reintentos no se cuentan otra vez. Se cierra el alcance funcional de recuperación y preparación única. Se verificaron 28 archivos únicos de combate y protocolo frente a sus manifiestos finales, incluidas las dos reparaciones de fixtures. Protocolo actual: `mp-2026-10-24-v14-combat-occupancy`; rechaza clientes v13 y balances distintos. Evidencia y límites en QA_CHECKLIST.md. No se atribuye revisión manual de capturas, móvil físico, build instalada o red remota. MP-DEC-042 conserva su cierre independiente.
<!-- MP-DEC-043-VALIDATION-END -->

## MP-DEC-044 — 2026-10-24, objetivos según ocupación real

Corrección de selección de objetivo aprobada por el creador: **una casilla reservada para caminar no equivale a una casilla ocupada**. Los ataques y prioridades deben considerar la casilla realmente alcanzada por la unidad. Durante un paso ordinario, la unidad conserva la ocupación del origen y reserva el destino; la ocupación se confirma al llegar con el reloj del combate. La reserva continúa impidiendo que otra unidad reclame esa ruta, sin adelantar la posición del objetivo.

Se conservan la pausa inicial completa, los alcances, las prioridades de cada personaje, los bloqueos de objetivo válidos y la identidad de un proyectil ya lanzado. Llegar a una reserva no debe robar un objetivo que sigue siendo válido. Los pasos cancelados por muerte, reinicio, desactivación o recolocación no pueden confirmar después un destino obsoleto. Los movimientos especiales, como gancho, salto y teletransporte, conservan su lógica propia y deben cancelar cualquier reserva ordinaria que haya quedado atrás. No se redefinen habilidades ni se elige un rival nuevo por orden de aparición de los actores.

<!-- MP-DEC-044-VALIDATION-BEGIN -->
**MP-DEC-044 integrado y validado en el Editor.** Comparte los 207/207 EditMode y 5/5 PlayMode únicos de MP-DEC-043, con 3 comprobaciones adicionales de compatibilidad online. Las pruebas de ocupación, cancelación física y gancho pasan; la reparación de presentación cambió únicamente el instante de muestreo de la fixture. Los 28 archivos únicos verificados corresponden al mismo conjunto de combate y protocolo, no a otro conjunto que deba sumarse. Se cierra este alcance funcional, conservando las restricciones de validación del Editor y el cierre independiente de MP-DEC-042.
<!-- MP-DEC-044-VALIDATION-END -->


## MP-DEC-045 — 2026-10-24, distribución del casino según la referencia

El creador aprueba ajustar la distribución de Grand Cas Hotel a la imagen `C:/Users/HP VICTUS/AppData/Local/Temp/codex-clipboard-03619ae6-be01-4549-a052-61ad55e4bb8c.png`, conservando su arte existente. La referencia define la composición, no un rediseño de ilustraciones, personajes o mecánicas. Los dados quedan ocultos; sus objetos y recursos se conservan. Se mantiene el mismo arte en móvil vertical y PC, adaptando la distribución y las zonas de interacción a la pantalla. No se habilita el giro del móvil.

La maleta, ofertas, banco y tablero deben seguir siendo operables tras recolocar los elementos; el ajuste visual no cambia reglas, costes, accesos ni propiedad del Hotel. No constituye autorización de compra ni publicación.

<!-- MP-DEC-045-VALIDATION-BEGIN -->
**MP-DEC-045 integrado y cerrado en Unity:** 22/22 casos específicos del casino aprobados y regresión compartida final 9/9 aprobada. Las dos capturas nativas de Game view, 540×960 y 1366×768, fueron inspeccionadas por root y confirman distribución, marco/fichas y arte conservados, ausencia de dados, Maleta debajo a la izquierda y accesorios periféricos en PC. Evidencia: `casino-layout-integration-20261024/results/progression-casino-play.xml`, `results/casino-viewport-regression-play-final.xml` y `native-visual-validation.json`. La regresión final resuelve tres expectativas antiguas de fixtures y un defecto real de área segura; no se modificó el diseño para acomodar las pruebas. Se cierra el alcance visual aprobado, sin atribuir validación en dispositivo físico o build instalada.
<!-- MP-DEC-045-VALIDATION-END -->

## MP-DEC-046 — 2026-10-24, regalo diario y premio simple

El creador aprueba una Pouch genérica azul para presentar el regalo diario. Al abrirla debe aparecer una pantalla simple con el premio obtenido, sin mostrar la tabla de probabilidades. El creador aprobó integrar exactamente `daily-pouch-preview-20261024/daily-pouch-blue-v1.png` (SHA-256 `EDC7361537D4315A8E40F215390456B6AD4487341FCA23B5C403E27114DCEB7C`), la Pouch azul sencilla en pixel art mostrada como vista previa. Se conserva esa imagen sin redibujarla. La imagen exacta ya fue integrada y la regresión final del regalo en el lobby volvió a aprobar; su revisión nativa específica sigue pendiente; los 23 diseños de expendedoras tienen una aprobación independiente registrada en MP-DEC-047.

Tabla exacta aprobada para futuros regalos; cada fila es un resultado excluyente de una apertura:

| Moneda | Cantidad | Probabilidad |
| --- | ---: | ---: |
| Earthkens | 10 | 10 % |
| Earthkens | 20 | 15 % |
| Earthkens | 30 | 15 % |
| Earthkens | 50 | 7,5 % |
| Sunkens | 10 | 10 % |
| Sunkens | 20 | 15 % |
| Sunkens | 30 | 15 % |
| Sunkens | 50 | 7,5 % |
| Saturnkens | 5 | 5 % |

Las nueve filas suman 100 %: 47,5 % de Earthkens, 47,5 % de Sunkens y 5 % de Saturnkens. Esta aprobación sustituye la configuración anterior del regalo —incluido el 1 % histórico de Saturnkens— solo para próximas aperturas. No cambia ni vuelve a sortear recibos ya guardados, ni habilita otro cobro del mismo día. Se conserva el contrato existente de un regalo por día UTC y cuenta local.

Los Saturnkens obtenidos se guardan en la cuenta. El jugador decide cuándo gastarlos; abrir el regalo no activa una expendedora ni gasta automáticamente el premio. Se conserva el crédito idempotente entre recibo diario y saldo gastable. No se concede una Saturncoin intermedia de bienvenida ni una Saturncoin diaria. La bienvenida posterior de 20 Saturnkens tiene aprobación independiente en MP-DEC-050.

<!-- MP-DEC-046-VALIDATION-BEGIN -->
**MP-DEC-046: alcance local del regalo aprobado e integrado, con tabla, persistencia, flujo y arte azul validados.** Los 9 DailyGiftTests y el caso del regalo en lobby habían aprobado; los recibos/crédito se validaron dentro de 109 Edit únicos de regalo/perfiles. La nueva prueba `VendingUnlocksPlayTests.ApprovedCatalogueShowsAllCurrentMachineArtAndDailyGiftUsesItsBluePouch` pasó y root revisó la captura nativa del regalo con la imagen azul exacta aprobada, cifras y botón legibles. Registro: `approved-vending-catalogue-integration-20261024/native-visual-validation.json`. La regresión compartida posterior resolvió sus tres fallos de fixture: Lobby pasó 4/4 al repetir, incluido el regalo; el total es 11 casos únicos Play aprobados según MP-DEC-048. Se cierra el alcance local del regalo con tabla, flujo, arte y regresión de Lobby validados. La bienvenida posterior de 20 Saturnkens corresponde a MP-DEC-050, todavía sin implementar.
<!-- MP-DEC-046-VALIDATION-END -->

## MP-DEC-047 — 2026-10-24, expendedoras por 5 Saturnkens directos

El creador aprueba que una apertura de expendedora cueste **5 Saturnkens directamente**, sustituyendo la Saturncoin intermedia en este flujo. No hay compra o conversión previa de Saturncoin. El gasto requiere la decisión del jugador y saldo suficiente; se debita junto con el resultado confirmado y sus desbloqueos, sin permitir doble cargo al reintentar.

Los identificadores internos históricos pueden conservarse para no romper guardados; no deben presentar al jugador una moneda intermedia ni implicar una conversión nueva. Esta aclaración sustituye el precio y la ruta anteriores para este flujo, sin activar compras reales, créditos QA o servicios pagos. El coste de expendedora no autoriza una Saturncoin diaria; la bienvenida posterior de 20 Saturnkens se rige por MP-DEC-050.

<!-- MP-DEC-047-TEST-CONFIG-APPROVED -->
**Aclaración posterior del creador, aprobada para pruebas:** diez paquetes de recursos: Earthkens de 10, 20, 30 y 50; Sunkens de 10, 20, 30 y 50; un paquete mixto de 10 Earthkens + 10 Sunkens y otro de 20 Earthkens + 20 Sunkens. Se aprueba para probar el balance la probabilidad del personaje pendiente por apertura 1 a 10: `1 %, 1 %, 1 %, 1 %, 1 %, 2 %, 4 %, 8 %, 20 %, 100 %`. Se conserva la regla de MachineEngine de no repetir premios: una vez obtenido el personaje no vuelve a salir. No se introduce un reinicio adicional del ciclo. Esta aprobación sustituye el estado pendiente anterior de esos importes y valores; las pruebas previas de infraestructura no acreditan todavía la batería específica de esta configuración, ahora integrada. No implica balance comercial definitivo, equivalencias monetarias ni aprobación de la migración de cuentas Legacy; el arte aprobado se registra por separado a continuación. El coste continúa en 5 Saturnkens directos y no hay apertura automática.

<!-- MP-DEC-047-ART-APPROVED -->
**Aprobación posterior explícita del creador: integrar los 23 diseños y sus asociaciones** de la galería `output/imagegen/vending-machines-v1/index.html`. El manifiesto `output/imagegen/vending-machines-v1/manifest.json` (SHA-256 `ED926B6D56081B7F425F652B2EBBC00B8196DAB04AD7A5372E0334CD69EA91D5`) fija cada PNG y su hash, nombre visible, ID de personaje e ID de expendedora. Se verificaron sus 23 imágenes sin discrepancias. Esta aprobación sustituye el estado de revisión pendiente del manifiesto artístico original; la importación posterior quedó registrada, pero todavía no acredita render ni pruebas finales en Unity. Conservar los PNG y sus transparencias, sin redibujarlos ni inferir identidades desde nombres visibles. Las monedas dibujadas son ilustrativas y no alteran la tabla económica aprobada.

| Personaje visible | ID estable | ID de expendedora | Imagen |
| --- | --- | --- | --- |
| Akryn | `aky` | `character-aky` | `aky.png` |
| Aniku | `anuik` | `character-anuik` | `anuik.png` |
| Atron | `atong` | `character-atong` | `atong.png` |
| Atrik | `atori` | `character-atori` | `atori.png` |
| Blotun | `blotan` | `character-blotan` | `blotan.png` |
| Bugrum | `bugaloo` | `character-bugaloo` | `bugaloo.png` |
| Bivui | `bugui` | `character-bugui` | `bugui.png` |
| Floru | `flo` | `character-flo` | `flo.png` |
| Gochar | `gochan` | `character-gochan` | `gochan.png` |
| Hymal | `hymay` | `character-hymay` | `hymay.png` |
| Jazir | `jazar` | `character-jazar` | `jazar.png` |
| Kaydo | `kayon` | `character-kayon` | `kayon.png` |
| Kazuru | `kazuru` | `character-kazuru` | `kazuru.png` |
| Pokrak | `pokrak` | `character-pokrak` | `pokrak.png` |
| Pombú | `popow` | `character-popow` | `popow.png` |
| Rosie | `rosie` | `character-rosie` | `rosie.png` |
| Selora | `sepora` | `character-sepora` | `sepora.png` |
| Stenko | `stein` | `character-stein` | `stein.png` |
| Suki | `suki` | `character-suki` | `suki.png` |
| Tauruk | `tauris` | `character-tauris` | `tauris.png` |
| Toronko | `tokoro` | `character-tokoro` | `tokoro.png` |
| Trimar | `trimol` | `character-trimol` | `trimol.png` |
| Tsumi | `tsu` | `character-tsu` | `tsu.png` |

El creador también aprobó que todos los premios pendientes tengan el mismo peso, salvo las garantías del personaje y de su primera habilidad; no se repiten premios dentro de una máquina.

<!-- MP-DEC-047-VALIDATION-BEGIN -->
**MP-DEC-047: alcance local de expendedoras cerrado, con catálogo de 23 máquinas, configuración aprobada y arte validado.** Los 31 ApprovedVendingEconomyTests, 10 GuestVendingTests y 7 AndroidPortraitTests pasaron en la tanda 48/48; las tres VendingUnlocksPlayTests aprobaron en `pouch-room-ui-integration-20261024/results/pouch-room-play-initial.xml`, incluida la nueva prueba de catálogo/arte reales. Las dos pruebas de infraestructura ya habían pasado y no se cuentan como nuevas. Root inspeccionó la captura nativa del catálogo desplazable con sus 23 asociaciones y nombres actuales. Coste directo 5, paquetes y curva aprobados, idempotencia, propiedad y ausencia de aperturas automáticas se conservan. La migración de acceso/economía de Legacy continúa pendiente y no se valida autoridad de servidor, pagos reales ni dispositivo físico. Los fallos de fixture de Lobby de la tanda compartida quedaron resueltos con 4/4 en la repetición; MP-DEC-048 registra 11 casos únicos Play aprobados.
<!-- MP-DEC-047-VALIDATION-END -->

## MP-DEC-048 — 2026-10-24, Sala de estuches diaria

El creador aprueba la Sala de estuches, accesible desde el menú de tres rayas, con **cinco oportunidades por día UTC**. Estas reglas sustituyen las preguntas pendientes de la primera petición:

- Cada victoria de una **partida completa**, de cualquier tipo e incluyendo partidas contra bots, obtiene la siguiente oportunidad disponible. Una ronda aislada no cuenta como partida completa.
- Las oportunidades 1, 2 y 3 entregan estuches azules de recursos. Las oportunidades 4 y 5 sortean **75 % estuche azul de recursos / 25 % estuche de personaje**.
- El personaje del estuche se elige entre los personajes todavía no obtenidos, con la misma probabilidad para cada candidato. Se guarda su **ID estable** al obtener el estuche y no se vuelve a sortear al abrirlo.
- Al abrir el estuche de personaje existe un **0,5 %** de obtener ese personaje concreto. El **99,5 %** restante entrega recursos según la tabla exacta del regalo diario de MP-DEC-046. El estuche azul usa esa misma tabla.
- Si ese personaje se obtuvo por otra vía antes de abrir su estuche, la apertura entrega recursos. Si la colección ya está completa, se entregan recursos en lugar de intentar seleccionar otro personaje.
- Al cambiar el día UTC se renuevan las cinco oportunidades y **se descartan los estuches sin abrir** del día anterior. Abrir un estuche **no repone** una oportunidad consumida.

La tabla de recursos compartida tiene nueve resultados: Earthkens 10/20/30/50 con 10/15/15/7,5 %; Sunkens 10/20/30/50 con 10/15/15/7,5 %; y 5 Saturnkens con 5 %. Es el resultado alternativo de la sala, no una apertura adicional del regalo diario. No consume ni duplica su recibo diario. El 0,5 % no cambia las probabilidades ni las garantías de las expendedoras.

Cada victoria y apertura debe aplicarse una sola vez, conservando recibos y saldo ante reintentos o reinicios. El cambio diario descarta estuches pendientes, pero no revoca premios ya abiertos ni vuelve a abonar su contenido. No se inventan fuentes adicionales de moneda, precios de apertura o concesiones de bienvenida.

<!-- MP-DEC-048-VALIDATION-BEGIN -->
**MP-DEC-048 integrado y cerrado en Unity: 185/185 EditMode y 11 casos únicos PlayMode aprobados, con 7 capturas nativas revisadas.** La tanda Edit incluye 26 pruebas nuevas de Sala y 159 regresiones, sin contarlas de nuevo en otros cierres. La primera tanda Play terminó 8/11; tras reparar únicamente el ciclo de vida de la fixture de Lobby, su repetición completa pasó 4/4 entre 11:29:49 y 11:30:39 UTC del 24-10-2026. Se conservan los otros 7 casos aprobados: 11 únicos, no 15. Las capturas de 320×568, 393×852 con área segura y 1280×720 confirman cinco posiciones, estuches azules/personaje, abiertos sin reposición y premios de recursos/personaje. Evidencia final: `pouch-room-ui-integration-20261024/final-validation.json` y sus XML/capturas. Se cierra este alcance local, sin validar dispositivo físico ni servidor autoritativo; la política Legacy sigue pendiente.
<!-- MP-DEC-048-VALIDATION-END -->

## MP-DEC-049 — 2026-10-24, desbloqueo permanente de una habilidad por monedas

Decisión del creador transmitida expresamente desde el chat lateral al coordinador: **desbloquear permanentemente una habilidad bloqueada cuesta 100 Earthkens y 100 Sunkens**. Ambas cantidades se descuentan juntas, una sola vez, al confirmar ese desbloqueo. Aclaración textual: «o sea si la habilidad esta bloqueada y la quieres desbloquear».

El precio corresponde a una habilidad concreta y a su propiedad permanente en la cuenta. No es un precio por uso ni sustituye el coste temporal de mejorarla dentro de una partida. Si una habilidad se obtiene como premio de una caja u otra fuente gratuita aprobada, se conserva ese desbloqueo sin cobrar estas monedas. No se añade una habilidad nueva a tablas de premios que no la contemplen ni se alteran las garantías actuales.

Una habilidad ya obtenida no se vuelve a cobrar. La operación debe comprobar el saldo de ambas monedas, la identidad estable de personaje/habilidad y la propiedad actual antes de confirmar. Reintentos, doble clic y errores de escritura no deben producir débitos parciales ni duplicados. La política de migración de cuentas Legacy continúa pendiente; no se reinterpretan mejoras de combate como propiedad permanente.

<!-- MP-DEC-049-INITIAL-LOCK-APPROVED -->
**Aclaración posterior aprobada directamente por el creador en el chat principal:** las cuentas nuevas comienzan con **las tres mejoras bloqueadas**. Se conserva la habilidad automática base de cada personaje. El bloqueo permanente debe reflejarse en la disponibilidad real de las mejoras, además de su presentación; desbloquear una mejora por monedas o por un premio aprobado la incorpora a la propiedad de la cuenta. Los costes y reglas temporales de mejorar durante una partida se conservan. Esta decisión no convierte la habilidad base en una compra, no revoca habilidades ya obtenidas y no resuelve por sí sola los derechos de cuentas Legacy.

Esta aclaración directa amplía la decisión de precio transmitida anteriormente desde el chat lateral y prevalece en el alcance de las cuentas nuevas. No aprueba premios adicionales de módulos del tutorial ni un estuche azul de tutorial.
<!-- MP-DEC-049-COPY-LIMIT-APPROVED -->
**Aclaración directa posterior sobre copias y fusiones:** el límite total de copias de cada tipo en la partida es **1 + número de habilidades permanentes desbloqueadas de ese personaje**. Sin mejoras desbloqueadas permite 1 copia; con 1 permite 2; con 2 permite 3; con 3 permite 4. La habilidad base automática no cuenta como una de esas tres mejoras.

El límite se aplica al total del tipo, incluidas las copias de la unidad principal y de su reserva de banco; no concede copias adicionales ni propiedad por guardar, retirar o fusionar. Las compras y fusiones solo pueden incorporar mejoras desbloqueadas. La siguiente mejora por orden numérico es la **primera desbloqueada que todavía no esté presente** en la unidad; no se concede una mejora bloqueada. Esta aclaración resuelve la interacción antes pendiente y ajusta el límite fijo de cuatro de MP-DEC-042 a un máximo variable de 1–4 según propiedad.

Se conservan la inversión/reembolso y los demás límites del banco, la identidad de las unidades y la contabilidad de stock. El tutorial conserva su permiso efímero de entrenamiento aprobado, limitado a `IsTraining`, sin recibos de propiedad ni cambios en la cuenta; no se extiende a partidas BO5 normales. No se inventan regalos para completar el límite de copias ni se cobra la habilidad automática base.

<!-- MP-DEC-049-VALIDATION-BEGIN -->
Implementación local cerrada: 341 casos únicos EditMode y 14 PlayMode aprobados; 38 de los EditMode son nuevos de economía/copia. Los dos fallos de la regresión eran fixtures que suponían cartera vacía; tras corregir la preparación, sus 54 casos completos pasaron. Cinco capturas nativas 393×852 y 1280×720 revisadas. Informes en permanent-ability-welcome-integration-20261024, permanent-ability-ui-integration-20261024 y dev-icon-integration-20261024. No se suma la primera ejecución de 215, que no incluyó los fixtures nuevos durante la recarga.
<!-- MP-DEC-049-VALIDATION-END -->

## MP-DEC-050 — 2026-10-24, bienvenida de 20 Saturnkens para cuentas nuevas

El creador aprueba directamente una bienvenida de **20 Saturnkens, una sola vez por cuenta nueva**. Al coste vigente de 5 Saturnkens, el saldo permite cuatro aperturas de expendedora; no garantiza cuatro personajes ni activa las aperturas automáticamente. El jugador decide cuándo gastarlo.

La concesión debe quedar guardada con un recibo único de bienvenida, sin repetirla por abrir el juego, reintentar, recargar o reanudar el tutorial. Las cuentas existentes y Legacy no se convierten en cuentas nuevas ni reciben un saldo retroactivo por esta decisión. Se preservan los saldos y recibos previos.

Esta aprobación sustituye la ausencia de bienvenida anterior únicamente por esta concesión de 20 Saturnkens. No aprueba una Saturncoin intermedia, una entrega diaria de bienvenida, otras recompensas de módulos ni un estuche azul del tutorial. El regalo diario, la Sala de estuches y los precios de expendedora conservan sus reglas independientes.

<!-- MP-DEC-050-VALIDATION-BEGIN -->
Implementación local cerrada: 341 casos únicos EditMode y 14 PlayMode aprobados; 38 de los EditMode son nuevos de economía/copia. Los dos fallos de la regresión eran fixtures que suponían cartera vacía; tras corregir la preparación, sus 54 casos completos pasaron. Cinco capturas nativas 393×852 y 1280×720 revisadas. Informes en permanent-ability-welcome-integration-20261024, permanent-ability-ui-integration-20261024 y dev-icon-integration-20261024. No se suma la primera ejecución de 215, que no incluyó los fixtures nuevos durante la recarga.
<!-- MP-DEC-050-VALIDATION-END -->

## MP-DEC-051 — 2026-10-24, estuche azul único del tutorial

El creador aprueba expresamente un único estuche azul en el módulo 7 con la tabla del regalo diario. Se reclama una sola vez por cuenta y es independiente de los 20 Saturnkens de bienvenida y de los cinco estuches diarios por victorias. Apertura, premio real, reintento y persistencia deben quedar ligados al mismo recibo. Esta decisión no concede otros premios de módulos ni un personaje garantizado. Implementación de módulos 7/8 en preparación; todavía no se presenta como validada.

## MP-DEC-052 — 2026-10-24, icono de la DEV build

El creador proporciona pictures/utilities/imagenes promocionales/icon.png y pide usar esa imagen exacta en la DEV build. Se integra sin rediseñarla en Assets/art/branding/dev-build-icon.png; SHA256 AFB87697B3A5A03A98AFFA259569FA4D1C2A30F6E192829151D43D0CEBEED65B, idéntico al original. La receta Android DEV aplica temporalmente el icono a los tamaños estándar, redondos y adaptativos; para estos últimos se usa la imagen original como fondo y una capa transparente técnica. Al terminar o fallar restaura todas las referencias anteriores. No modifica el icono de builds normales. Cinco pruebas EditMode aprobadas; no se generó un APK ni se afirma inspección en un launcher físico.

### MP-DEC-051 — Cierre de los módulos 7 y 8, 2026-10-24

Implementados y validados localmente. El módulo 7 reclama y abre un único estuche azul con la tabla aprobada del regalo diario; premio fijado al reclamar, moneda entregada al abrir. Es independiente de bienvenida y Sala de estuches. Exige visitar Maleta, colección, una mejora desbloqueable y monedas. El módulo 8 usa las 23 máquinas reales, sus 14 premios y confirmación de 5 Saturnkens; solo avanza al revisar un recibo confirmado. Una extracción anterior puede revisarse sin cobrar otra: gastar previamente la bienvenida no bloquea el tutorial. Repetir, recargar y reintentar no duplica concesiones.

Evidencia: 162/162 EditMode y 28 casos únicos PlayMode finalmente aprobados; capturas nativas revisadas en móvil y PC. Los reportes con fallos iniciales se conservan junto a las correcciones. No se ha generado un APK ni probado dispositivos físicos por estos cambios.

### MP-DEC-053 — Rival del primer BO5, aprobado 2026-10-24

El creador aprobó probar el Monster inicial no elegido y los tres Whelps iniciales (IDs estables atori, bugui y blotan), sin mejoras, con estadísticas normales y las compras y colocaciones de la IA existente. Solo corresponde al primer BO5 del módulo 9; resultados y premios normales. No autoriza cambiar el balance general, inventar premios ni definir una progresión posterior de dificultad. Implementación del módulo 9 pausada por petición del creador para conservar este punto jugable. Solo hay borradores parciales en el workspace, no integrados en Unity.
### MP-DEC-054 — Icono de la mejora obtenida, 2026-10-24

El creador pidió que al conseguir una mejora aparezca su icono. El premio confirmado de la expendedora muestra el icono propio de esa habilidad, con su nombre; los personajes conservan su figura y los recursos sus monedas. Se reutiliza el arte existente de habilidades. El mismo render se aplica al módulo 8 y a revisar una extracción anterior, sin repetir cobros ni concesiones.

## MP-DEC-055 — Copas de salas online y acceso a Campo Real, 2026-10-25

**Aprobado por el creador; implementación local integrada, validación pendiente.** Se autoriza conectar el sistema de copas a **partidas BO5 completas de salas online**. Bots, tutorial, rondas aisladas y resultados no confirmados por ambos clientes no puntúan. No se aprueba una fórmula para abandono, desconexión o empate.

Se conservan las cifras de prueba aprobadas: **inicio y suelo 0; victoria +30; derrota −5 si las copas previas son menores de 150 o −30 desde 150**. El descuento se decide con el saldo previo y respeta el suelo 0. Campo Real solo puede seleccionarse con **150 copas actuales o más**. Al descender se bloquea otra vez; si estaba seleccionado, se vuelve a Parque de Bolas al estar en menú, sin cambiar el mapa durante la partida. No se añaden rachas, ajustes por rival ni tableros. El Hotel permanece fuera del recorrido y esta decisión no cambia su acceso comercial ni activa compras.

La cuenta conserva sus copas y recibos en el perfil v10. Las cuentas anteriores comienzan este sistema en 0 porque no tenían copas jugables persistentes; se conservan identidad, monedas, propiedad, máquinas, Sala, tutorial y demás progreso. No se traducen resultados históricos, XP ni simulaciones QA/DEV en copas. Los contadores sin datos muestran 0.

El protocolo v16 vincula cada resultado al GUID compartido de la partida y confirma un hash que incluye el marcador BO5. Un resultado válido se registra una sola vez; los fallos de escritura permiten reintentar sin duplicación. La confirmación entre pares y la persistencia local no sustituyen un servidor autoritativo ni resuelven la política de propiedad Legacy.

Esta aprobación habilita únicamente el alcance de copas descrito. Se mantiene la pausa de expansión, incluido el módulo 9 del tutorial; el 10 sigue pendiente. No autoriza emparejamiento, clasificación remota, servicios nuevos, monetización ni publicación. Las 23 máquinas locales y los ocho módulos del tutorial conservan sus cierres anteriores; el roadmap global sigue incompleto.

<!-- MP-DEC-055-VALIDATION-BEGIN -->
**Alcance local validado en Unity: 380 casos únicos EditMode y 25 PlayMode aprobados.** La primera tanda Edit aprobó 378/380. Las dos correcciones fueron exclusivamente de pruebas: versión de perfil codificada como 9 y comparación de actores aún privados durante preparación. La repetición completa de sus dos fixtures aprueba 46/46, conservando los otros 334 casos aprobados, sin sumar reintentos.

La primera tanda de escenas aprobó 22/22. Unity había omitido los tres casos nuevos del enlace online por un GUID de metadatos malformado; se reparó el meta y se ejecutó esa fixture completa, 3/3. No se presenta la primera tanda como cobertura del enlace online. Incluye resultado confirmado una sola vez, persistencia, ausencia de puntos por bots/entrenamiento, rechazo de resultados discordantes y reintento tras fallo de escritura.

Se revisaron cuatro capturas nativas del recorrido a 0 y 150, presentación PC y bloqueo de Campo Real tras descender a 120. Los archivos y XML se conservan en `ladder-runtime-integration-20261025`. Compilación previa de cinco ensamblados y compilación real del Editor sin errores. No se recompiló APK ni se probaron dos dispositivos mediante Relay real; la confirmación entre clientes y el guardado local no equivalen a servidor autoritativo. El resto del roadmap continúa incompleto y su expansión sigue pausada salvo este alcance.
<!-- MP-DEC-055-VALIDATION-END -->


## MP-DEC-056 — 2026-10-25, fichas de habilidades concisas y legibles

El creador pide resumir las habilidades de todos los personajes. Los cambios de estadísticas se expresan con signo y cantidad (Vida: +20 puntos, Daño: +25 %); los cambios de funcionamiento llevan una frase breve con título cuando ayuda, por ejemplo Vuelo. Se omiten velocidades de vuelo/proyectiles y detalles técnicos. Las letras de las fichas deben ser negras y legibles. La flor con invulnerabilidad muestra su vida absoluta (por defecto 1), nunca la diferencia negativa respecto a la flor base, y nombra INVULNERABILIDAD. Los números siguen las estadísticas editadas; esta decisión no cambia el balance de los demás personajes ni renombra sus IDs o nombres actuales.

## MP-DEC-057 — 2026-10-25, marca frontal de Taurik / Tauruk

El creador llama Taurik al personaje cuyo ID estable es tauris y cuyo nombre actual en el asset es Tauruk; no se cambia su identidad guardada. Su primera mejora marca a los rivales en su misma columna, en línea recta hacia delante, con alcance inicial editable de cinco casillas, sin incluir su casilla de origen. Se activa al empezar el combate sobre las posiciones desplegadas. Aclaración expresa del creador: las piezas marcadas no pueden recolocarse en la siguiente preparación. La locomoción durante el combate se conserva. Se mantiene el resto de la mecánica existente, incluida la energía por marcado y la persistencia de la marca durante la partida. La marca tiene un indicador visible que reutiliza el icono actual de la habilidad y respeta la ocultación de rivales.

El cambio de geometría exige que los dos clientes usen el mismo comportamiento. Se preparó v17-forward-mark y la integración posterior de cercanía/Duelo lo sustituye por `mp-2026-10-25-v18-nearest-duel`, conservando perfil y recibos de copas v10. MP-DEC-056/057 quedan validados localmente con 186 EditMode y 12 PlayMode únicos aprobados y seis capturas nativas revisadas; evidencia en `ability-mark-checkpoint-20261025.json`. No se acredita APK, móvil físico ni dos clientes remotos.

## MP-DEC-058 — 2026-10-25, naciones de Eronya como referencia visual

El creador aporta Eronya_Diseno_del_juego.docx.pdf y cinco láminas de personajes. Declara expresamente que el documento NO ES OFICIAL y conserva el modo de juego actual, sin añadir daño mágico ni adoptar las otras reglas del PDF. Pide un tablero con lobby, miniatura de transición y entorno ampliable por nación, y adaptar el arte a Monster Pouch. Zephalontropo utiliza una ballesta, corrección directa que prevalece sobre el arma dibujada en la lámina.

Se aprueban propuestas visuales para Naturalis (bosque ancestral), Aquarion (puerto e islas con santuario lunar), Geothar (fortaleza de basalto y canteras), Aireola (monasterio entre nubes, talleres e instrumentos) y Volkanar (fortaleza de forja junto al cráter). Cada composición debe conservar el tablero actual de 6 por 10 casillas, el móvil vertical y el encuadre ampliado en PC. La aprobación de estos ambientes permite generar propuestas; los diseños finales se presentan antes de integrarlos.

Llegar al tablero de una nación permite obtener sus personajes mediante expendedoras y estuches; no los concede directamente. El orden y los umbrales se resuelven en la actualización de recorrido del 25 de octubre al final de este documento; el tratamiento de personajes actuales sin correspondencia documentada sigue pendiente. La auditoría encuentra 20 nombres del PDF y 23 personajes actuales, sin equivalencia de nombre o ID confirmada: no se sustituyen personajes, estadísticas, habilidades ni propiedad de cuentas por similitud visual. Estos trabajos son una ampliación expresamente solicitada respecto al punto jugable anterior.

## MP-DEC-059 — 2026-10-25, DUELO automático por copas

El creador pide que DUELO cree/busque sala automáticamente, sin mostrar códigos ni el menú de salas; la búsqueda muestra el Monster del jugador centrado y «Buscando». Debe escoger el rival compatible disponible más cercano en copas. AMIGO conserva las salas manuales. La implementación se prepara en un chat separado autorizado, sin activar servicios pagados ni publicar. El descubrimiento y sus límites reales deben verificarse; no se simula un rival humano con un bot ni se afirma que el servicio esté disponible antes de probarlo.

### Estado actualizado de implementación de MP-DEC-058/059

Naturalis v4 y las direcciones aprobadas de Aquarion, Geothar y Volkanar se integraron con miniaturas, lobbies ampliados y piezas de combate de 60 casillas. El recorrido conserva sus 84 archivos integrados y la corrección de historial. Los 263 casos EditMode únicos de la tanda inicial están resueltos combinando sus 260 aprobados y la repetición de NationTrophyLadderTests, 22/22; no hubo una nueva corrida global 263/263. El lote compartido de repetición terminó 72/79 por seis fallos del fixture de eje de Kazuru y una expectativa histórica 20/10 de Tauruk; sus reparaciones están integradas y el retest de 27 casos de Kazuru sigue pendiente. PlayMode del 25-10-2026 a las 14:41:44 UTC: 38/44, con NationArenaScenery 2/2, NationThemeCompatibility 12/12 y LobbyRedesign 12/12 aprobados. Header quedó 11/15 y selector 1/3; las seis reparaciones de fixtures están integradas, pendientes de repetir. XML conservados en `nation-combined-integration-20261025/results`.

Duelo y cercanía: El cierre local anterior a las naciones registra 409 EditMode aprobados (303 online/cercanía + 106 perfiles/estuches/arte) y 21 PlayMode únicos aprobados. La primera tanda Play terminó 20/21; la repetición aislada del fixture de arte pasó 2/2 y no se suma dos veces. Evidencia: `online-pouches-cleanup-checkpoint-20261025.json`. Estos resultados no validan las naciones, el nuevo ladder, el encabezado ni el salto de Kazuru. Protocolo vigente `mp-2026-10-25-v19-nation-ladder`; sustituye v18-nearest-duel. Solo BO5 online completo y confirmado puntúa; bots y tutorial no dan copas. Se conserva migración del historial previo sin recalcular sus ajustes, con reglas antiguas para el prefijo histórico y nuevas para resultados posteriores. Dos clientes UGS reales siguen sin probarse. No se habilitó facturación ni se modificó el Dashboard.

### Martio — aclaraciones directas del creador confirmadas el 25 de octubre

Martio está aprobado como Whelp. La casilla central de la explosión recibe únicamente el impacto; la explosión afecta a las ocho casillas adyacentes. Cada proyectil puede dañar directamente a un mismo enemigo solo una vez durante su vuelo; las explosiones originadas por impactos vecinos pueden dañarlo repetidamente. Estas aclaraciones resolvieron la consulta de solapamiento; las decisiones y cambios adicionales se llevan en el chat propio de Martio. Actualización posterior: el creador ordenó integrarlo en su propio chat; se observaron cambios en producción y allí continúan registro y pruebas. No se certifica ese trabajo desde este cierre.

## MP-DEC-060 — 2026-10-25, estuches por victorias sin límite diario

El creador pide estuches por victorias infinitas. Se repite un ciclo de cinco: tres estuches azules seguidos de dos sorteos con 75 % azul / 25 % personaje. Cuando corresponde un estuche de personaje, la probabilidad de desbloquearlo pasa a **30 %**, sustituyendo el 0,5 % de MP-DEC-048. La actualización elimina el límite de cinco oportunidades diarias de victorias. Las reglas no modificadas expresamente —victoria de partida completa, personaje asociado aún no obtenido y recursos alternativos aprobados— conservan su comportamiento anterior.

El creador canceló el cambio propuesto al regalo diario: su tabla, periodicidad y recibos se mantienen. El 30 % corresponde al estuche de personaje de victorias, no a las máquinas ni al regalo diario.

Estado: seis archivos integrados y cierre local acreditado por `online-pouches-cleanup-checkpoint-20261025.json`. Estuches por victorias sin límite de cantidad: ciclo repetido de tres azules y dos sorteos 75 % azul / 25 % personaje, con 30 % de desbloqueo al abrir el estuche de personaje. Se conserva la caducidad diaria UTC de los estuches pendientes; ilimitado no significa acumulación entre días. El regalo diario conserva tabla, periodicidad y recibos. No se deduce una nueva fuente de moneda ni se cambia el coste de las máquinas.


## Actualización aprobada del 25 de octubre — recorrido de naciones y presentación

Naturalis 0 → Aquarion 150 → Geothar 300 → Volkanar 450 → Campo Real 600; victoria +30 en todos los tramos y derrotas −5/−10/−15/−25/−30 según el saldo anterior a la partida. Inicio y suelo 0, sin techo de copas.

Parque de Bolas (`classic`) y Grand Casino Hotel (`lounge`) quedan como cosméticos fuera del recorrido. El Hotel conserva su categoría de pago, pero no se activa IAP, cobro ni publicación. Aireola queda fuera del ladder por ahora; sus assets se conservan. El acceso a los tramos depende de las copas actuales y se vuelve a restringir al descender; no se cambian tableros bajo los actores durante combate.

Protocolo vigente `mp-2026-10-25-v19-nation-ladder`; sustituye v18-nearest-duel. Solo BO5 online completo y confirmado puntúa; bots y tutorial no dan copas. Se conserva migración del historial previo sin recalcular sus ajustes, con reglas antiguas para el prefijo histórico y nuevas para resultados posteriores.

Encabezado y salto de Kazuru integrados: nombre guardado en el recuadro y una sola cantidad de copas; salto de cuerpo completo con arte actual, misma velocidad, reservas y tiempos de combate. La integración final `nation-trophy-final-integration-20261025/manifest.json` añade 23 archivos: 17 de trofeos/UI, dos del helper nativo y cuatro reparaciones de fixtures. Compilación offline: cinco asambleas Editor y dos asambleas runtime AndroidRelease aprobadas; esto no es una build APK ni ejecución en Android. Repeticiones enfocadas y revisión visual final pendientes.

Los cuatro trofeos V2 están APROBADOS e integrados: césped, agua con peces, roca y roca con lava. Sus 17 archivos de importación/UI forman parte del paquete final de 23 archivos. Las propuestas V1 no se importan. Son variantes visuales del tramo para una única cantidad de copas, no cuatro monedas o contadores. La comprobación visual final en Unity sigue pendiente.

Martio continúa como trabajo paralelo en su propio chat: el creador ordenó «YA PONLO EN EL JUEGOOO». Se observaron cambios externos en producción y su integración, registro y pruebas están en curso. Este cierre no atribuye una integración terminada ni valida ese trabajo; tampoco afirma que siga fuera del juego. Los 20 personajes de Eronya NO están implementados ni asociados al roster por inferencia. El módulo 9 del tutorial sigue pausado y el 10 pendiente. No se completan por esto las 12 etapas del roadmap.

La dirección visual y el recorrido están aprobados; su integración no sustituye las pruebas pendientes. No se concede automáticamente un personaje por alcanzar un tablero, ni se activan asociaciones de facción o compras no definidas.

# MONSTER POUCH — ROADMAP MAESTRO VIVO + PROMPT DE IMPLEMENTACIÓN PARA CODEX

**Versión de trabajo:** 1.2 — 20 de octubre de 2026  
**Proyecto:** Monster Pouch — Unity 2D, Android, orientación vertical 9:16  
**Responsable de las decisiones:** el creador de Monster Pouch  
**Naturaleza:** hoja de ruta evolutiva; NO contrato de funciones inmutables ni autorización general para inventar contenido.
**Actualización v1.2:** tutorial práctico con recompensas y Saturncoins gratuitas de bienvenida; rediseño progresivo, menos recargado y fiel a los personajes de **todas** las máquinas expendedoras, con revisión creativa del creador.

> **MENSAJE PRINCIPAL PARA CODEX:** Actúa como un equipo senior que mantiene y expande un videojuego REAL, ya existente. Implementa lo que esté definido y autorizado; propone cuando falte una decisión; pregunta al creador antes de inventar o alterar contenido creativo o mecánicas sustantivas. Conserva lo ya construido. Este roadmap puede cambiar en cualquier momento; las decisiones más recientes y explícitas del creador prevalecen. No confundas una IDEA FUTURA o un EJEMPLO con permiso de implementarla.

---

# 0. CÓMO USAR ESTE ROADMAP (OBLIGATORIO)

## 0.1. Autoridad creativa y autonomía técnica

El creador de Monster Pouch es el **Game Director** y tiene la última palabra sobre personajes, nombres, habilidades, animaciones, balance central, ambientaciones, precios definitivos, economía y monetización. Codex es responsable de ingeniería, implementación y QA, no de tomar el control creativo.

**Codex PUEDE hacer sin preguntar cada detalle:** auditar; organizar código; corregir bugs inequívocos; extraer componentes reutilizables; preparar configuraciones; agregar pruebas; optimizar sin alterar el aspecto o funcionamiento aprobado; implementar requisitos definidos en este documento; documentar; crear migraciones seguras y reversibles; arreglar riesgos evidentes de seguridad. Siempre debe explicar los cambios importantes.

**Codex DEBE preguntar y obtener aprobación ANTES de:**

- Crear un personaje, Monster, Whelp, Pouch, mascota o unidad nueva; decidir sus nombres, siluetas, colores, sprites, animaciones, lore, habilidades, estadísticas, IA o rol.
- Crear una evolución específica o una habilidad activable de héroe específica, incluso para un personaje existente. La existencia futura del framework NO autoriza inventar su contenido.
- Incorporar skins, cosméticos, emotes, iconos, ambientaciones, recompensas nuevas, rarezas, recursos nuevos o tipos de tienda no definidos.
- Cambiar estadísticas, fórmulas de daño, reglas del tablero, probabilidades finales, curvas económicas o precios cuando estén sin aprobar.
- Sustituir un diseño o arte por otro, eliminar recursos anteriores, editar sprites sin pauta visual aprobada o publicar assets generados automáticamente.
- Introducir monetización real, publicidad dirigida a usuarios reales, compras, suscripciones o servicios pagos.
- Cambiar un sistema de redes, guardado o combate operativo por otro incompatible; eliminar datos o hacer migraciones destructivas.
- Resolver arbitrariamente una contradicción con impacto visible al jugador.

**No significa detener el desarrollo completo:** ante una decisión creativa pendiente, deja esa parte identificada como «REQUiere DECISIÓN DEL CREADOR», avanza con sistemas independientes autorizados y presenta opciones breves sin imponer ninguna. NO fabriques una solución creativa de ejemplo y la introduzcas al juego a escondidas. Solo son admisibles dobles de prueba puramente técnicos, aislados de builds jugables y que no se confundan con contenido aprobado.

## 0.2. Estados formales del roadmap

Cada elemento tendrá uno de estos estados **independientes**:

- **APROBADO — IMPLEMENTAR:** instrucción explícita, suficiente para programar el comportamiento especificado.
- **APROBADO — PENDIENTE:** decisión tomada pero requiere recurso, dependencia o fase previa.
- **PROPUESTA / BALANCE PROVISIONAL:** referencia cuantitativa para discutir o pruebas aisladas; NO definitiva.
- **FUTURO — DISEÑO POR APROBAR:** intención conceptual; NO desarrollar contenido jugable específico todavía.
- **EN DESARROLLO / EN PRUEBAS / VALIDADO:** estados operativos que solo se asignan con evidencia real.
- **CANCELADO / SUSTITUIDO:** conservar historial y razón; no reintroducir versiones antiguas.

Una especificación en un documento adjunto no demuestra que ya esté programada. Un elemento «aprobado» no equivale a «implementado». Un ejemplo no equivale a «aprobado».

## 0.3. Jerarquía para resolver conflictos

1. Instrucción explícita más reciente del creador, especialmente una corrección.
2. Decisiones vigentes expresas de este roadmap maestro.
3. Mecánicas actuales **verificadas** en proyecto, si no contradicen los puntos anteriores.
4. Documentación anterior y prompts previos, como referencia histórica.
5. Propuestas de Codex, solo después de pedir aprobación.

Si una diferencia no se puede resolver mediante esta jerarquía, registra «DECISIÓN PENDIENTE» y no hagas cambios irreversibles. **Ejemplos de contradicciones ya resueltas:** Moon-Ken NO es moneda persistente, sino del combate; Pouch Dorada NO es una categoría de personajes dorados; se permiten unidades del mismo tipo simultáneamente en tablero y banco, y el bloqueo del brief no invalida copias existentes.

## 0.4. Versionado vivo y control de cambios

Mantén una carpeta de documentación fuera de `Assets` si es posible, por ejemplo `Docs/MonsterPouch/`:

- `ROADMAP.md`: fases, estado, dependencias, prioridad y próximos trabajos.
- `DECISIONS.md`: ID, fecha, decisión, estado, alcance y qué sustituye.
- `PENDING_APPROVAL.md`: preguntas puntuales al creador; no mezclar con bugs técnicos.
- `CHANGELOG.md`: archivos, escenas, prefabs y sistemas modificados.
- `CURRENT_SYSTEMS.md`: hallazgos verificados sobre el proyecto real.
- `BALANCE_CONFIG.md`: valores definidos, provisionales y pendientes.
- `QA_CHECKLIST.md`: casos automatizados y pruebas manuales.
- `RELEASE_READINESS.md`: bloqueos para una beta / versión pública.
- `TUTORIAL_REWARDS.md`: pasos del tutorial, recompensas, concesiones únicas y saldo inicial de Saturncoins.
- `MACHINE_ART_REWORK.md`: inventario y ficha de revisión de **cada** máquina (personaje, sprite original, propuesta visual, aprobación, variante final, estado de integración).

Identificadores sugeridos para decisiones: `MP-DEC-001`, `MP-DEC-002`...; evitar reescribir el historial. Antes de aplicar una nueva regla, verifica qué scripts, datos guardados, tests y pantallas afecta. Permite agregar, modificar, posponer y eliminar ideas del roadmap mediante aprobación del creador y migraciones seguras.

## 0.5. Forma de entregar cada fase

Antes de programar, examina el repositorio y presenta un informe **corto y concreto**: encontrado / reutilizable / por desarrollar / conflictos / decisiones que realmente requieren respuesta. Luego ejecuta el trabajo autorizado. Tras cada fase entrega: (1) qué implementaste y archivos; (2) qué comprobaste de verdad y resultados; (3) qué no pudiste comprobar; (4) cómo probarlo en Unity; (5) qué necesita configuración o aprobación; (6) siguiente trabajo. **No afirmes haber compilado, jugado, probado dispositivos o conectado servicios sin haberlo hecho.**

---

# 1. IDENTIDAD DEL JUEGO Y MECÁNICAS EXISTENTES

## 1.1. Producto que se debe preservar

- Juego de estrategia/combate automático 2D con personajes coleccionables, presentación **pixel art**, controles móviles verticales 9:16 y textos del juego preferentemente en **inglés** (preparar localización).
- Partidas locales contra bot y partidas entre jugadores usando la infraestructura ya existente **Unity Lobby + Relay + Netcode for GameObjects**.
- Combate de ronda con fases de preparación y batalla, formato actual **BO5** (gana quien alcanza tres victorias), preparación en torno a **40 segundos** y combate en torno a **40 segundos**, salvo lo que indique la configuración efectiva del proyecto.
- Tablero **6 × 10**, ocupación exclusiva por casilla y reglas ya definidas de movimiento, ataques, alcance, energía, IA, desempates de objetivos y posiciones; no reescribirlas como pretexto para agregar progresión. Conservar movimiento ortogonal y ataques diagonales donde correspondan según las reglas individuales verificadas.
- Mazo inicial estructural de **1 Monster + 7 Whelps**; su selección para colección es distinta de la capacidad de despliegue y banco dentro de una partida. **No adivines la capacidad del banco, número simultáneo permitido en tablero ni reglas de copias**: inspéctalas y respétalas.
- Bolsa/brief, tres ofertas, compras que no reponen automáticamente, rerolls y copias por Whelp según las reglas vigentes. No introducir ventas, compraventas o reapariciones adicionales no autorizadas.
- Mantener **ataque básico**, habilidades base, Super, tres mejoras, estados, animaciones, estrellas/indicadores de mejoras y lógica particular de cada personaje.
- Habilidades de Monsters: las tres opciones son **independientes y NO acumulables entre sí**, salvo excepciones expresamente aprobadas; no combinarlas involuntariamente. Las modificaciones acumulativas específicas de un personaje deben seguir sus reglas propias.
- En la interfaz, respetar decisiones previas: sin botón de pausa innecesario en la vista de combate, sin título del juego o número de ronda donde se pidió retirarlos, recursos visibles y legibles, reroll debajo del indicador de Moon-Kens, banco sin título «BANCO», botones y tamaños aprobados, estética limpia de pixel art.

## 1.2. Personajes y nombres actuales

Algunos nombres cambiaron al rediseñar. Audita IDs reales y referencias de arte antes de tocar archivos; usa IDs estables y alias para migrar nombres sin duplicar unidades.

**Plantel de referencia a verificar:** Monster original, Bugaloo, Popow, Sepora, Tauris, Atori, Blotan, Bugui, Flor/Flo, Gochan, Jazar, Kayon, Trimol, Tsu/Suki (confirmar ID y alias), Atong, Tokoro/Toronko, Anuik/Aniku, Aky, Hymay, Rayos y otros registrados efectivamente en proyecto. No asumas que todas estas grafías corresponden a unidades separadas ni que todas están terminadas.

Un documento menciona aproximadamente **17 diseños de Pouches, 17 máquinas y cuatro sprites de monedas**. Es una estimación del paquete artístico, NO una orden de inventar los sprites faltantes. Detecta qué assets están efectivamente disponibles y su correspondencia con los personajes.

**Arte y derechos:** conservar los diseños históricos como respaldo para el desarrollo, pero no reutilizar automáticamente diseños cercanos a Gogos Crazy Bones como arte comercial. Rediseños originales con siluetas propias, cambios de color, nombres y animaciones solo tras aprobación del creador. Estilo orientativo: criaturas compactas, jugueteras, expresivas, pixeladas, limpias, no excesivamente brillantes y no convertidas en monstruos tipo Pokémon.

---

# 2. DECISIÓN RECIENTE PRIORITARIA — BANCO TÁCTICO (APROBADO)

## 2.1. Regla fundamental: copias en campo Y en banco

**Un mismo tipo de personaje PUEDE existir simultáneamente en el tablero y en el banco.** No interpretes «personaje en banco» como veto a conservar o usar las otras copias de ese personaje en el tablero. Cada copia física/unidad se administra por instancia, no solo por `characterId`.

**SE MANTIENE la regla actual del brief/tienda durante la preparación:** cuando hay una unidad de un tipo en el banco, ya **NO deben aparecer NUEVAS copias de ese tipo** en las ofertas del brief/rotación mientras dure la condición que ya aplica el juego. Este filtro afecta **ofertas nuevas**, NO elimina copias desplegadas, NO quita unidades del tablero, NO borra copias previamente adquiridas y NO impide conservar personajes del mismo tipo simultáneamente en campo y banco. Respeta la mecánica actual de filtrado sin rehacerla.

No introducir por iniciativa propia restricciones globales de unicidad por tipo, ni bloquear el despliegue de las copias que ya existen. Tampoco crear nuevas reglas para los límites de espacios del banco: conservar los actuales hasta que el creador los cambie.

## 2.2. Reembolso al llevar al banco (nuevo)

Colocar desde el tablero una **instancia** de personaje en el banco:

1. Es **gratuito** como movimiento de banco; NO descuenta Moon-Kens por guardarla.
2. Devuelve al jugador los Moon-Kens que corresponden a la inversión recuperable de **esa instancia**, «como si se hubiera vendido», **sin destruirla**.
3. Conserva la unidad, sus mejoras, su identidad, nivel/estrellas, habilidades, estadísticas aplicables y datos necesarios para redesplegarla de manera consistente.
4. Asigna la devolución **una sola vez por movimiento válido**; prohibido explotar arrastres repetidos, dobles clics, reconexiones o animaciones.
5. Registra por instancia (en datos de partida, no moneda persistente) costo base **efectivamente pagado**, costo de mejoras/copia **efectivamente pagado**, reembolso aplicado, estado de banco/campo y costo de retorno. No suponer que todas las copias costaron lo mismo.

## 2.3. Costo de regresar al campo (nuevo)

- Regresar esa **misma instancia** desde el banco al tablero requiere **volver a pagar su precio total real ya pagado por ella**, incluyendo las mejoras aplicables. **No comprar una copia nueva**; se reactiva la que estaba guardada.
- Si la instancia se recibió **gratuitamente** y no se pagó ninguna mejora, su retorno cuesta **0 Moon-Kens**.
- Si el personaje base fue gratuito, pero se pagaron mejoras, su retorno cuesta el valor **efectivamente pagado por esas mejoras**.
- Si fue comprado y mejorado, el retorno cuesta su base pagada y el gasto real en mejoras que corresponda; no calcular solo por la apariencia de estrellas.
- Si no hay Moon-Kens suficientes, no se despliega; la unidad permanece segura en banco y se informa costo/saldo. No permitir saldo negativo.
- Al regresar, comprobar casilla válida, ocupación, etapa permitida y sincronización multijugador existente; fallos no deben debitar saldo o duplicar unidades.
- Evitar que una misma instancia esté contabilizada a la vez en **dos casillas**; sí pueden existir simultáneamente **instancias diferentes** del mismo tipo en banco y tablero.

**Precisión económica que debe auditarse:** el creador pidió «recibir lo mismo que si vendiera» y volver a pagar «el total que ya pagó». Inspecciona la fórmula de venta vigente. Si **el valor de venta actual es distinto del costo total pagado**, informa esa diferencia antes de programar la fórmula final: es una decisión de diseño con impacto económico. No inventes descuentos, penalizaciones ni ganancias. Como caso de prueba ilustrativo, si compra e invierte 2 + 4 Moon-Kens y la venta actual reintegra los 6, entrar al banco devuelve 6 y salir del banco cuesta 6.

## 2.4. Integración y escenarios obligatorios

- Mantener el número y funcionamiento de espacios de banco actualmente implementados, sin crear más slots.
- No alterar el ciclo de ofertas/cantidad de copias máximas actual salvo el filtro **ya existente** de banco. No bloquear unidades previamente compradas.
- UI de banco: mostrar precio de redespliegue y, cuando corresponda, el monto recuperado, con números legibles. No rediseñar toda la interfaz sin necesidad.
- El registro de reembolsos/costos es **solo economía Moon-Ken de partida**, no Earthcoins, Suncoins ni Saturncoins, y no se guarda como recurso entre partidas.
- Considerar etapas de compra, drag-and-drop, tap para colocar, selección, sustitución, vender permanentemente, upgrades y rerolls para que el nuevo banco no duplique o pierda Moon-Kens.
- Multijugador: la autoridad efectiva valida cada movimiento y refleja el mismo saldo/instancias a ambos clientes; documentar límites del host actual.

**Pruebas de aceptación mínimas:**

1. Hay dos Atori en el tablero; se mueve **solo uno** al banco; el otro **sigue desplegado**.
2. Hay un Atori en banco y otro en campo: nuevas ofertas de Atori **no aparecen** en el brief, pero los Atori que ya estaban en tablero permanecen.
3. Se recuperan los Moon-Kens de la instancia movida una sola vez; no se destruyen mejoras.
4. Devolver una unidad gratuita sin mejoras cuesta 0; si esa unidad recibió mejoras pagadas, cuesta solamente lo efectivamente invertido.
5. Devolver una unidad comprada/mejorada tiene el costo total previsto; sin saldo suficiente permanece en banco sin pérdida.
6. Intentos de desplegar sobre casillas ocupadas o fuera de preparación no mueven la unidad ni generan cobros.
7. Repetir movimientos no crea Moon-Kens infinitos ni unidades duplicadas; comprobación con escenarios de redes y reconexión.
8. Si hay dos instancias del mismo personaje con costos distintos, cada una conserva sus propios costos y mejoras.

**ESTADO:** APROBADO — IMPLEMENTAR tras auditar lógica actual de tienda/venta y cerrar exclusivamente cualquier diferencia encontrada en la fórmula de reembolso.

---

# 3. ECONOMÍA: CUATRO MONEDAS SIN MEZCLAR

## 3.1. Moon-Ken / Moonkens — SOLO combate (APROBADO)

- Recurso temporal por partida/ronda para comprar unidades, copias, mejorar y hacer rerolls/Tricks según su sistema vigente.
- Incluye el nuevo reembolso y redespliegue del **banco táctico**.
- NO se compra con dinero real ni se transfiere de una partida a otra. No incluir un saldo Moon-Ken permanente como recurso del perfil.
- Conservar su sprite pixel art lavanda/lila pálido/azul plateado con cráteres lunares; NUNCA sustituir por oro ni por una media luna dorada.
- Si los scripts llaman `Moonken` a otra variable, migrar semánticamente y no mezclar ámbitos. Preservar las reglas ya existentes de inicio de ronda y obtención.

## 3.2. Earthcoins — moneda común PERMANENTE (APROBADO)

- Obtenible por partidas completas, misiones, progresión, eventos y Pouches normales cuando corresponda.
- Mantener cantidades moderadas (referencia: decenas/cientos, no miles o decenas de miles sin necesidad).
- Preparar gasto futuro y posibles máquinas especiales sin inventar objetos/recompensas no aprobados.
- En el ZIP pueden existir sprites con el nombre `Earthken`: verificar y mapear el identificador visual al nombre mostrado aprobado; no crear moneda extra accidentalmente.

## 3.3. Suncoins — progresión secundaria PERMANENTE (APROBADO)

- Menos abundante que Earthcoins. Fuentes previstas: desafíos, eventos, maestrías y recompensas especiales/Pouches normales.
- Mantener su uso futuro configurable; no inventar ahora costos o mecánicas obligatorias para gastarla.
- Comprobar si el sprite se llama `Sunken` y asociarlo sin duplicaciones.

## 3.4. Saturncoins — moneda premium PERMANENTE (APROBADO)

- **Costo de cada extracción de máquina individual: 1 Saturncoin**.
- Futuro comercial de referencia **10 Saturncoins por ~USD 2,99**, **NO precio aprobado para activar una tienda** ni autorización para cobrar; comprobar regiones, tasas y requisitos de plataforma en su momento.
- **NUEVA DECISIÓN (v1.2): todo jugador nuevo debe recibir Saturncoins gratuitas de bienvenida para poder probar realmente al menos una extracción en una máquina individual al comenzar su experiencia.** Este premio es parte de la economía inicial y no debe depender de comprar monedas ni esperar varias partidas.
- Parámetro central configurable `starterSaturncoins` (entero ≥ 1 para la experiencia inicial aprobada de costo 1/extracción). **La cantidad definitiva no está decidida:** no imponer 3, 5 o 10 monedas sin aprobación. Registrar el valor como pendiente; durante pruebas puede usarse provisionalmente el **1 Saturncoin** ya especificado en el prototipo anterior, sin presentarlo como balance final. La cantidad puede revisarse tras la beta.
- Mantener como **propuesta provisional de pruebas**, NO recompensa definitiva de producción, **1 Saturncoin adicional cada 2 encuentros BO5 completos**, sujeta a controles antifarming y aprobación del balance.
- La entrega de bienvenida debe ser **una sola vez por identidad real de jugador**, incluso si se reinicia tutorial, se reinstala el juego, cambia de dispositivo o se intenta reconectar. No apilar accidentalmente una "Saturncoin de prueba" antigua, otra "de bienvenida" y una tercera "del tutorial" por el mismo concepto.
- Distinguir saldos de prueba internos, Saturncoins gratuitas de jugador real y Saturncoins compradas en el futuro. Los regalos gratuitos persistentes son válidos en la progresión normal y en las máquinas habilitadas para ello, mientras que los saldos internos de QA jamás se convierten automáticamente en saldo comercial ni se trasladan a producción.
- Reservar/mostrar el saldo y el premio de bienvenida en el onboarding de forma clara. Si se define una recompensa adicional de Saturncoins por culminar un módulo tutorial, **debe sumarse solo cuando ese premio haya sido aprobado y efectivamente completado**; no confundirla con la concesión inicial.
- **Separar créditos de prueba, gratuitos de producción y comprados**; jamás migrar automáticamente saldo de pruebas a dinero real. Preparar políticas de reconciliación y migración para lanzamiento.
- Usar el asset aprobado (posiblemente denominado `Saturnken`); verificar nombres reales, no añadir monedas nuevas.

## 3.5. Controles y parámetros

- Costos, recompensas, topes, misiones, XP, precios de extracción y frecuencias parametrizados en configuraciones centrales. Remote Config solo para valores que deban poder modificarse sin APK y con restricciones de seguridad.
- No permitir gasto duplicado, saldo negativo por errores, operaciones repetidas o que el cliente se autoasigne saldo permanente.
- No presentar monedas de prueba como comprables o canjeables por dinero real.
- Separar contabilidad **de combate temporal** (Moon-Ken) de **perfil/economía persistente** (Earthcoins, Suncoins, Saturncoins).
- Antes de modificar compra, recompensa o venta, detectar si está enlazada a una lógica ya existente y conservarla.

---

# 4. COLECCIÓN, MAZO INICIAL Y INVENTARIO

## 4.1. Mazo de nuevos jugadores (APROBADO, verificar contenidos disponibles)

Inicio con **1 Monster + 7 Whelps**:

- Monster base: personaje original cuya mecánica base es revivir al **50 % de vida**.
- Whelps: **Atori, Blotan, Bugui, Flor/Flo, Gochan, Trimol y Atong**.

Una cuenta comienza con **un mazo**, con estructura para varios mazos en una actualización futura. Desbloqueos permiten sustituir miembros **fuera de partidas**; no alterar mazos a mitad de una partida activa. La estructura de colección NO implica que todos los 8 estén simultáneamente sobre el tablero.

Reutilizar los personajes reales, sus IDs, sprites y habilidades existentes. No crear sustitutos ni rediseños del mazo inicial por iniciativa propia.

## 4.2. Inventario de cuenta y colección

- Estado por ID estable de personaje (desbloqueado/bloqueado), habilidad 1/2/3, maestría, recompensas reclamadas, mazo(s), evoluciones futuras y habilidades activas futuras.
- Una misma recompensa obtenida por máquina, Pouch normal o maestría se reconoce en todos los sistemas y **NO se otorga/consume por segunda vez**.
- Mantener visibles elementos bloqueados y progresos de manera no engañosa; explicar método de obtención.
- No añadir bonificaciones puramente estadísticas por «rareza», personajes premium exclusivos o ventajas competitivas exclusivas de pago.
- El inventario persistente de **desbloqueos** es distinto de las **instancias/copias desplegadas en partida** y del banco táctico.

---

# 5. MÁQUINAS EXPENDEDORAS INDIVIDUALES / POUCH DORADA

## 5.1. Regla del contenido (APROBADO)

Cada personaje con máquina propia tiene una máquina temática de su Pouch. **Revisar y rediseñar visualmente TODAS las máquinas existentes** según el apartado **5.6**: utilizar sus sprites originales como referencia y conservar sus respaldos. La máquina rosa pendiente se orientará al concepto de **margarita**, sujeto a aprobación del creador. No añadir máquinas/personajes que no existan ni publicar arte final sin revisión.

**Pouch Dorada** es el sistema de estas máquinas de recompensas limitadas, **NO** una rareza de personaje ni «personajes dorados».

Cada máquina dispone por ahora de **14 premios ÚNICOS**:

| Categoría | Unidades |
|---|---:|
| Desbloqueo de personaje | 1 |
| Habilidad/mejora 1 | 1 |
| Habilidad/mejora 2 | 1 |
| Habilidad/mejora 3 | 1 |
| Paquetes de recursos diferentes | 10 |
| **Total** | **14** |

No insertar cosméticos, skins, recursos inventados ni una recompensa 15 en el catálogo de producción. Los **10 paquetes** deben configurarse con IDs/recompensas previamente aprobadas; si faltan contenidos concretos, preparar los 10 slots como pendientes de configuración sin asignarles recursos ficticios que puedan otorgarse.

## 5.2. Extracciones, bloqueos y garantías (APROBADO)

- **1 Saturncoin por extracción**.
- Cada recompensa solo se obtiene **una vez** por máquina; premios reclamados salen de la selección y aparecen marcados como obtenidos.
- El personaje puede salir antes, pero se obtiene **no más tarde de la extracción número 10**, siempre considerando premios ya reclamados y progreso real.
- Las tres habilidades NO pueden salir antes de obtener el personaje; se habilitan al desbloquearlo.
- La **primera extracción posterior** al desbloqueo del personaje entrega obligatoriamente **una habilidad aún pendiente**, si hay alguna (también si personaje vino de Pouch normal o regalo).
- Si ciertas habilidades ya se obtuvieron en maestría, NO volver a otorgarlas ni ofrecerlas. Si no queda ninguna habilidad por obtener, omitir esta garantía de forma coherente.
- Al completar 14 recompensas, la máquina queda completa y **no permite gastar Saturncoins**.
- Si el personaje o habilidades se obtuvieron fuera de su máquina, reconocer premios como obtenidos, sin cargo, y ajustar elegibilidad/contador para evitar premios imposibles.
- Resultado de extracción decidido y confirmado por lógica confiable **antes** de la animación; cierre, desconexión o reintento no puede cambiar premio ni duplicarlo.
- Probabilidades condicionadas por estado, catálogo elegible, garantías y otras recompensas ya obtenidas. Mostrar probabilidades reales de cada resultado y garantía correspondiente.

## 5.3. Distribución provisional de aparición del personaje

Los valores siguientes se propusieron para ensayos de balance y **NO deben convertirse en probabilidades comerciales definitivas sin confirmación**:

| Número de extracción (si falta el personaje) | Probabilidad condicional propuesta |
|---|---:|
| 1 | 1 % |
| 2 | 1 % |
| 3 | 1 % |
| 4 | 1 % |
| 5 | 1 % |
| 6 | 2 % |
| 7 | 4 % |
| 8 | 8 % |
| 9 | 20 % |
| 10 | 100 % |

Son **probabilidades condicionales**, no probabilidades totales de desbloqueo ni diez pesos para mezclar sin normalizar. Simular al menos **100.000 máquinas completas** con premios externos y garantías para validar no duplicados, ocurrencia antes/en tiro diez y correcta primera habilidad. Si catálogo o estado de premios hacen inaplicable una probabilidad, registrar la necesidad de una regla sin inventarla silenciosamente.

## 5.4. Interfaz de máquinas (APROBADO)

- En sección **Shop/Tienda**, **tocar directamente el sprite de la máquina** abre su menú: NO agregar botón intermedio «Ver máquina».
- UI en 9:16, pixel art, navegación táctil: nombre, personaje, 3 habilidades, 10 recursos, estados **Disponible / Bloqueado / Obtenido**, restantes, costo 1, saldo, progreso a garantía, probabilidades reales y animación de premio.
- Las habilidades bloqueadas aparecen en gris y sin posibilidad de activarse. Mostrar ayuda/descripción sin convertirla en botón de compra.
- Confirmación comprensible antes de gastar Saturncoins; deshabilitar taps simultáneos y errores de doble gasto.
- Referencias visuales por `machineId` y `characterId` estables, nunca por similitud de nombres o índice de sprite.
- Diseñar preparada una **migración versionada** para posibles cosméticos futuros, sin agregar por ahora el slot 15.

## 5.5. Estructura configurable

Sugerencia de datos (adaptar a estructuras existentes): `machineId`, `characterId`, `sprite`, `animation`, `abilityIds`, `resourceRewardIds`, `cost`, `guaranteeDraw`, `firstAbilityGuarantee`, `rewardWeights`, `unlockRequirements`, `displayOrder`, `theme`, `catalogVersion`. Preferir ScriptableObjects como catálogo/presentación; **nunca** confiar únicamente en información del APK para una economía premium real.

## 5.6. Rediseño de TODAS las máquinas expendedoras (APROBADO: DIRECCIÓN; ARTE FINAL SUJETO A REVISIÓN)

**Carpeta local señalada por el creador:**

```text
C:\Users\HP VICTUS\Juegos Unity\mini-gogos\pictures\utilities
```

- **Codex debe revisar primero esa ruta dentro del equipo del creador, si tiene acceso a ese sistema de archivos.** Inventariar sprites, archivos fuente, resoluciones, tamaños, animaciones y asociaciones de cada máquina. Si la ruta no es accesible, informar qué archivos faltan y continuar con arquitectura/QA independiente; no afirmar haber visto imágenes ni inventar su aspecto.
- El creador ha aprobado la **necesidad de rediseñar todas las máquinas**, no una licencia para cambiar arbitrariamente sus identidades. Objetivo: que sean **menos cargadas**, con siluetas claras, menos adornos/texturas innecesarias, colores legibles y pixel art nítido, reconocibles en pantallas móviles 9:16, y que reflejen **mucho mejor el personaje específico** al que pertenecen.
- Cada propuesta debe tomar como referencias el **sprite actual del personaje**, su silueta, paleta, personalidad y temática. Evitar el mismo molde genérico con solo recoloraciones, y no añadir detalles de otros personajes. **No rediseñar el personaje por modificar su máquina.** Mantener identidad visual coherente entre el catálogo completo y con el lobby; no abusar de saturación, destellos ni elementos decorativos.
- **Flujo obligatorio por máquina:** (1) mapear `machineId ↔ characterId ↔ sprite actual`; (2) guardar original sin sobrescribir; (3) describir qué se simplificará, qué rasgo del personaje será la referencia y qué elementos se conservarán; (4) mostrar al creador una propuesta visual, boceto o especificación concreta **según las herramientas de arte realmente disponibles**; (5) solicitar y registrar aprobación creativa individual o de un lote explícitamente aprobado; (6) producir/importar sprites finales solo tras ese visto bueno; (7) comprobar nitidez, escalado, UI, legibilidad, animaciones y no regresión.
- La **máquina rosa** tendrá concepto de margarita conforme a la dirección indicada, pero la composición final se validará antes de sustituir el original. No asumir que ya existe un archivo aprobado para ella.
- La revisión afecta al **aspecto visual**, no a la economía: **conservar 14 premios, las probabilidades, el costo de 1 Saturncoin, la asociación por IDs, la interacción pulsando el sprite y el progreso guardado**. Un cambio visual nunca reinicia una máquina ni un desbloqueo.
- Mantener originales en copia/branch o carpeta de respaldo no importada a producción cuando proceda; registrar nombres de archivos antes/después, variantes, aprobación y estado en `MACHINE_ART_REWORK.md`. No crear contenido sin autorización ni usar imágenes sin derechos suficientes para publicación.
- Codex puede iniciar **ya** la auditoría de máquinas, los informes y las propuestas de simplificación; no debe bloquear los sistemas de progresión mientras se aprueba el arte. No marcar la tarea "completada" hasta haber reemplazado y verificado todas las máquinas realmente existentes y aprobado sus diseños.

---

# 6. POUCHES NORMALES Y PROGRESIÓN GRATUITA

## 6.1. Principio de accesibilidad (APROBADO)

TODOS los personajes y sus tres habilidades deben poder desbloquearse jugando gratis. Las máquinas con Saturncoins son una vía alternativa, no un bloqueo de pago. Las recompensas obtenidas en cualquier lugar se sincronizan con máquinas, mazos e inventarios.

## 6.2. Pouches normales (PROPUESTA DE BALANCE PARA PROBAR)

- Probabilidad base sugerida: **2 %** de personaje nuevo antes de garantías. El resto entre recursos/recompensas no premium aprobados, sin duplicar personajes.
- Garantías de prueba: primer personaje dentro de las **primeras 3** Pouches; segundo personaje dentro de las **6 siguientes**; siguientes personajes dentro de **12** Pouches desde el último desbloqueo. Contadores reiniciados correctamente según hitos.
- Obtención inicial sugerida: **1 Pouch durante el tutorial**, **1 Pouch cada 2 partidas BO5 completas**, y adicionales mediante misiones y eventos aprobados.
- Cuando ya no existan personajes elegibles, las garantías deben resolverse con reglas aprobadas; NO crear personajes ni recompensas nuevas. Proponer comportamiento al creador.
- Un personaje obtenido en Pouch normal queda reclamado en su máquina correspondiente y su primera extracción debe considerar la garantía de habilidad restante.

**NO tratar estos números como balance definitivo.** Deben ser configurables y analizarse con sesiones de prueba y datos reales.

## 6.3. Maestría de personajes (APROBADO como sistema; umbrales PROVISIONALES)

- Cada personaje tiene progreso de maestría ligado a participar en partidas, con protección contra abandonos, duplicación de resultados y farming artificial.
- Tres habilidades desbloqueables gratis. Umbrales propuestos de prueba: habilidad 1 tras **2** partidas usando personaje, habilidad 2 tras **6** partidas acumuladas, habilidad 3 tras **14** partidas acumuladas. No asumir que las cantidades son finales.
- Desbloqueo de una habilidad por maestría actualiza inmediatamente la máquina, el inventario y la UI de habilidades, sin premios duplicados.
- Preparar extensibilidad para cosméticos futuros sin programar ni inventar cosméticos ahora.

## 6.4. Nivel de cuenta, XP y misiones (APROBADO como arquitectura)

- XP por partidas terminadas, victorias, tutoriales, misiones e hitos; XP y nivel persistentes.
- Configurar curva XP por nivel, nivel máximo si se adopta, recompensas por subida y fuentes, **sin fijar números arbitrarios**.
- Misiones diarias/semanales, logros, hitos, eventos, recompensas de nivel y participación como arquitectura reutilizable; interfaz y contenido concretos solo cuando sean aprobados y necesarios.
- Cada misión: ID estable, condición, progreso verificable, premio configurable, reclamación una sola vez, restablecimiento por **tiempo del servidor** en producción.
- No premiar abandonar ni dar experiencia desproporcionada por perder. Separar partidas BO5 completas de rondas; garantizar idempotencia en reconexiones.
- Priorizar tutorial progresivo y recompensas agradables sin agotar la colección demasiado rápido; comprobar balance con jugadores reales.

---
# 7. CUENTAS, GUARDADO, BACKEND Y ECONOMÍA SEGURA

## 7.1. Estrategia compatible con el estado real

Este roadmap contiene dos necesidades válidas: **probar el juego gratis ahora** y **tener progreso seguro en nube antes de comercializarlo**. No se contradicen si se ejecutan por etapas:

**Modo de desarrollo / beta interna:** reutilizar autenticación/cloud existentes si funcionan. Si aún no hay backend persistente, permitir **perfil invitado y guardado local VERSIONADO** como solución provisional de pruebas, sin simular que es seguro ni sincronizado entre dispositivos. No usar el perfil local como autoridad para saldo de Saturncoins comerciales.

**Modo de producción / beta comercial:** integrar o completar Unity Authentication (vinculación de cuenta y recuperación cuando esté soportada), persistencia remota con Cloud Save u otra solución verificada, lógica económica sensible de servidor (Cloud Code u otro backend capaz de garantizar consistencia) y controles de concurrencia. El progreso debe recuperarse al ingresar en otro dispositivo con una cuenta vinculada. Nunca prometer recuperación de una cuenta anónima sin vincular tras perder sus credenciales.

**Criterio de bloqueo:** NO activar pagos, recompensas premium de anuncios comerciales ni economía de dinero real hasta disponer de verificación autoritativa e idempotente. Si Cloud Save + Cloud Code **no aportan** por sí solos transacciones atómicas suficientes, probar e incorporar una alternativa de backend adecuada; no declarar segura una sincronización simple.

## 7.2. Modelo lógico de datos (NO obligar a duplicar estructuras)

Establecer IDs estables y versionado/migración:

| Entidad lógica | Contenido principal |
|---|---|
| `PlayerProfile` | `playerId`, nombre visible, creación, último acceso, nivel, XP, estado cuenta |
| `PlayerWallet` | **Earthcoins, Suncoins, Saturncoins** de prueba/gratuitos/comprados según reglas, versión, modificación; **no Moon-Ken persistentes** |
| `PlayerInventory` | personajes, habilidades, paquetes reclamados, cosméticos reservados sin inventario falso |
| `PlayerProgress` | BO5 jugados/ganados/perdidos, bots/PVP, tutoriales, misiones, hitos, XP |
| `PlayerDecks` | mazo(s), 1 Monster + 7 Whelps, configuración vigente, IDs estables |
| `CharacterMastery` | progreso y recompensas por ID de personaje |
| `MachineProgress` | ID máquina, premios obtenidos, tiros, garantías, personaje/habilidades, versión |
| `NormalPouchProgress` | contador por garantía, hitos, recompensas y fuentes |
| `EconomicTransaction` | ID transacción, usuario, moneda, monto, saldo antes/después, fuente, estado, timestamp, `idempotencyKey` |
| `PlayerSettings` | idioma, audio, gráficos, accesibilidad, controles |
| `FutureFeatureStates` | evoluciones y habilidades activas, **solo cuando existan y sean aprobadas** |

**Separar datos de partida y datos de cuenta**: las instancias, costos de compra de unidades, mejoras, Moon-Kens, banco y ocupaciones son **estado de partida**, no inventario de moneda permanente.

## 7.3. Reglas de integridad y migración

1. No perder partidas ni progreso al cerrar normalmente; definir manejo explícito de interrupciones y caídas.
2. No duplicar monedas, personajes, recompensas o recibos por doble clic, retries, replay o reconexión.
3. Operaciones económicas críticas con autoridad confiable, control de concurrencia, atomicidad e idempotencia.
4. No usar PlayerPrefs como **fuente autoritativa comercial** de monedas, compras ni inventarios; los perfiles invitados pueden tener guardado local claramente no seguro y sin valor comercial.
5. Cuando haya conflicto entre dispositivos, resolver sin sobreescribir silenciosamente progreso más reciente.
6. Operaciones UI con estados: pendiente, completada, fallida, recuperación; comprobación desde backend al volver a conectar.
7. Claves privadas, credenciales, secretos IAP y API siempre fuera de APK y repositorio.
8. Datos guardados con esquema versionado, backups/migraciones y pruebas con partidas viejas. No inventar IDs nuevos si existe un identificador persistente.
9. Instrumentar y probar solicitudes de eliminación de cuenta/datos, retención mínima y recuperación de cuenta vinculada.
10. Guardar una huella confiable de recompensas ya otorgadas y fuentes para conciliarlas sin duplicación.

## 7.4. Servicios de Unity por inspeccionar

Se sabe que existen **Lobby + Relay + Netcode for GameObjects**. Examinar si están configurados **Authentication, Cloud Save, Cloud Code, Analytics y Remote Config** antes de instalarlos o modificarlos. Verificar documentación vigente y paquetes compatibles con la versión real de Unity; no inventar APIs ni depender de servicios descontinuados. Nada de contratar automáticamente planes pagos.

---

# 8. PERFIL DE DESARROLLADOR, HERRAMIENTAS Y BALANCE

## 8.1. Herramientas de prueba (APROBADO)

Implementar un modo DEV **exclusivo** de editor, desarrollo autorizado o cuentas verificadas desde servidor cuando exista backend. Nunca activable por texto en nombre de jugador ni modificando un campo local en build pública.

Capacidades de QA, solo en entornos seguros: desbloquear todos los personajes/habilidades **existentes**, ver evoluciones/activos **cuando estén aprobados e implementados**, cargar mazos de prueba, recargar monedas ficticias, restablecer máquinas de prueba, simular sorteos, inspeccionar probabilidades, modificar temporalmente estadísticas, reproducir combates, registrar errores, resetear perfil sandbox. Esto no autoriza a crear personajes que no existen.

Mantener totalmente separados dev/test y producción. No introducir puertas traseras en APK pública, RPCs sin autorización ni privilegios de servidor autoasignables.

## 8.2. Editor de estadísticas y configuraciones

- Parámetros numéricos reales y unidades documentadas: vida, daño, velocidad, intervalos, rango en casillas, duración, coste, velocidad de proyectil, etc.
- Mantener la interpretación actual de **IQ** cuando aplique: un valor **menor puede significar un comportamiento más inteligente**, p. ej. 0,5 más inteligente que 0,9 en el algoritmo definido. No transformarlo en escalas arbitrarias 1–10 sin autorización.
- Valores de base, ataques, habilidades 1–3, Super, IA y demás configurables de manera modular, idealmente usando sistemas/ScriptableObjects ya existentes sin duplicar fuentes de verdad.
- Permitir balancear sin alterar el arte; las variaciones de prueba no se propagan automáticamente a producción.

## 8.3. QA automatizado y manual

Pruebas unitarias, de integración y de escenarios para cálculo de moneda, colecciones, extracciones, red, reconexión, progreso, guardado, sprites, UI, escenas y rendimiento. Marcar qué se ejecutó y qué quedó pendiente de prueba manual real en Editor/dispositivo. No afirmar cobertura total sin haberla medido.

---

# 9. CAMBIOS INMEDIATOS DE COMBATE, RESULTADO Y ANIMACIONES

**Estas instrucciones son CAMBIOS APROBADOS y se deben implementar sin reescribir el motor de combate.** Mantener separada la lógica de resultado y la presentación visual. No cambiar arbitrariamente daño, IA, vida o economía para facilitar efectos.

## 9.1. Tokoro / Toronko: tercera habilidad (APROBADO)

Reemplazar la habilidad tercera anterior por:

- Desde el comienzo de la ronda, **se cura 60 puntos de vida por segundo** durante los **primeros 3 segundos**.
- Durante estos 3 segundos **NO ataca** y **NO obtiene/carga energía**.
- Terminado el plazo, regresa al comportamiento normal según estados actuales.
- Mostrar animación o efecto de curación **coherente con assets disponibles**; para una animación artística nueva que requiera dibujar o redefinir apariencia, presentar al creador el planteamiento visual antes de producirla.
- Duración, tasa de curación y bloqueo de acciones editables; respetar límites máximos de vida y sincronización.
- No romper su Super, mejoras 1 y 2, estados de control ni otras fuentes de curación.
- Como esta curación dura **3 segundos**, debe continuar después de la ventana global inicial de habilidades de 1,5 s mientras el resto del combate procede.

## 9.2. Ventana inicial de habilidades (APROBADO)

Al comienzo de **cada ronda**, antes del intercambio normal de ataques, las habilidades marcadas para ejecutarse al inicio de ronda se **disparan primero** dentro de una ventana inicial por defecto de **1,5 segundos**.

- Preparar sistema de eventos/temporizadores configurable **por habilidad**, no forzar que todas duren 1,5 s.
- Incluir habilidades rápidas, por ejemplo primeras/segundas mejoras de Suki (verificar nombre/ID real de personaje).
- Resolver orden determinista si hay activaciones simultáneas, ocupación/teletransportes, stun, muertes, invocaciones o choque de reglas; ante decisiones estratégicas no resueltas, solicitar aprobación en lugar de inventar prioridades.
- No cobrar ataques normales ni generar energía de ataque durante la porción inicial si el diseño aprobado los retrasa; utilizar el ritmo exacto del combate actual como referencia.
- Al terminar la fase inicial, el combate continúa; efectos de duración larga siguen activos por sus tiempos naturales. Evitar pausas y bloqueos del resto de tropas por una habilidad de 3 s.
- En multijugador las activaciones y tiempos dependen de una única simulación/autoridad de partida y se reflejan en clientes.

## 9.3. Secuencia de comparación de HP al terminar la ronda (APROBADO)

Crear una **presentación intrigante, progresiva y con buen ritmo** basada en HP de los supervivientes:

1. Finaliza el combate y se congela/establece el **resultado de ronda** conforme a las reglas reales existentes.
2. Aparecen, junto a cada superviviente, sus **HP restantes** en cifras legibles.
3. En toda unidad **NO Monster**, representar visualmente la división de sus HP entre 2: un «tick» o golpe visual, reducción de tamaño del número y transición a la cifra computada para presentación. Los Monsters usan sus HP restantes completos. **No alterar HP reales** por esta representación.
4. Los números/resultados «viajan» o se transfieren visualmente a una **barra verde y una roja**, una por equipo.
5. Ambas barras se llenan **gradualmente y casi simultáneamente**, con interpolación continua y brillo/pulso pequeño en cada aporte de vida. **No** avanzar la barra solamente por saltos de ticks o añadir tramos bruscos.
6. Al concluir, la barra del equipo con el total más alto llega visualmente a **100 %**, y la otra muestra `suTotal / totalMáximo` de longitud; no modificar el valor real para forzar el porcentaje.
7. **Solo entonces** aparecen las banderitas/estampas del vencedor y la transición de ronda.

Usar colores verde y rojo según lados ya configurados, sin asumir que «verde» es local en todos los clientes. Para empate, ausencia de supervivientes o casos especiales, preservar regla existente y pedir decisión si no hay criterio. No inventar un ganador porque una barra visual quede llena. Las dos barras son una **visualización normalizada** de los totales, no dos nuevas barras de salud real durante el combate.

**QA:** números supervivientes correctos; Whelp a la mitad solo visual; Monsters sin mitad; suma real consistente; barras fluidas y casi simultáneas; diferencias de total representadas; empate; interrupciones/red; bandera solo tras animación; no alterar datos posteriores del combate.

## 9.4. Pantalla final de partida BO5 (APROBADO)

- Cuando el jugador **gana el encuentro**, presentar **`VICTORYYY`** con confeti y una animación celebratoria acorde al pixel art.
- Cuando **pierde**, presentar **`YOU LOSE`** y un fondo suavemente rojizo, sin saturación agresiva.
- No presentar pantalla final en rondas intermedias. No reemplazar la lógica BO5 ni entregar dos veces premios/XP porque la animación se reprodujo de nuevo.
- Compatible con bot y multijugador, Android 9:16 y finalizaciones anómalas.
- Ajustar tamaño, duración y transiciones por configuración; propuestas visuales nuevas que impliquen arte distinto requieren visto bueno antes de reemplazar assets.

---

# 10. AMBIENTACIONES, UI Y CORRECCIONES VISUALES

## 10.1. Base visual según mapa (APROBADO)

Las bases/pedestales sobre los que se muestran los héroes/personajes deben **cambiar con la ambientación activa**. Hoy se reutiliza el del campo real: corregir. Reutilizar sprites/temas existentes si los hay; cuando una ambientación carezca de base adecuada, documentar arte faltante y **pedir aprobación para diseñarla**. No generar un set completo por iniciativa propia.

## 10.2. Artefactos gráficos (APROBADO: CORREGIR)

Auditar escenas, tilemaps, sprites, slicing, filtrado, bordes, pivotes, atlas, camera/pixel perfect y escalas buscando líneas/artefactos feos.

**Incidencias reportadas:** en el brief/estantería del mapa **Parque de Juegos** hay una zona que parece separada del resto; múltiples personajes presentan **líneas verticales antiestéticas**. Corregir causas reales después de verificarlas, sin redibujar identidades ni aplicar soluciones que borren detalles. Revisar todas las ambientaciones, registrar defectos con capturas comparativas antes/después y corregir de manera incremental.

## 10.3. Parque de Juegos (APROBADO: DIRECCIÓN DE REDISEÑO)

- Reducir la saturación de colores para aproximar el mapa a la paleta más suave del **lobby**.
- **Quitar tubos** alrededor del tablero.
- Sustituir el perímetro por un **borde de inspiración arcoíris** que combine con el estilo de juguete/pixel art sin resultar estridente.
- Conservar dimensiones, posiciones, colliders, interacciones, pathfinding, límites, zonas de UI y rendimiento.
- El diseño artístico específico de ese nuevo borde y cualquier ilustración de reemplazo deben presentarse al creador para aprobación antes de fijarse como arte final. La dirección de diseño está aprobada; el resultado visual concreto no está preaprobado.

## 10.4. Coherencia visual y controles

- Pixel art más definido, con bordes limpios, mínima sobreiluminación/brillo excesivo y texto legible.
- Evitar deformaciones al cambiar resolución y en dispositivos 9:16 distintos. Configurar filtrado y pixel-perfect conforme a los sprites reales.
- Mantener mochila/brief/estantería y banco con sus estilos aprobados; no sustituir todas las interfaces cuando solo hace falta eliminar líneas o ajustar una pieza.
- Panel informativo al mantener presionado: vida, daño, velocidad, habilidades y costos, descripciones; opciones no comprables aparecen grises/no seleccionables. Tap para colocar y arrastre según mecánica actual.
- Interfaz principal de preparación: recursos Moon-Ken y rerolls grandes/apilados, banco sin rótulo encima, botón de listo conservado; no reintroducir pausa/título/número de ronda donde fueron retirados.
- Proponer accesibilidad para brillo/transiciones sin alterar el contenido visual aprobado.

---

# 11. PROGRESIÓN DEL JUGADOR Y TUTORIAL INTEGRADOS EN EL GAME LOOP

## 11.1. Flujo jugable coherente (APROBADO)

**Primer ingreso → creación/carga de perfil → concesión ÚNICA de Saturncoins gratuitas de bienvenida → tutorial interactivo contra objetivos de entrenamiento y bots débiles → recompensas por módulos → demostración guiada de una máquina expendedora usando el saldo gratuito → mazo inicial y colección → partida BO5 normal con bot → progreso/XP/recompensas configuradas → personalización del mazo → multijugador cuando esté listo.**

- El usuario **debe poder conocer y probar las máquinas desde el inicio**, sin pasar por pagos. El onboarding mostrará el saldo gratuito y un flujo claro para usar la máquina. La recompensa de bienvenida se acredita una vez y se conserva si el jugador sale a mitad del tutorial.
- Los módulos didácticos son distintos de los encuentros BO5 competitivos. **No disparar por error recompensas de victoria, XP BO5, Pouches por partidas, misiones de "terminar encuentros" ni estadísticas de victorias competitivas** al completar una sesión artificial de entrenamiento.
- Cada concesión se realiza tras validar el evento correspondiente y con registro persistente; los textos/valores deben reflejar montos realmente acreditados, no premios simulados.

## 11.2. Tutorial interactivo con práctica real (APROBADO)

No limitarse a textos. Enseñar dentro del tablero con **acciones verificables** que el jugador realice, preferentemente con indicaciones visuales sencillas y pixel art legible, usando el control real del juego:

| Módulo | Acción jugable que debe aprenderse | Oponente o contexto |
|---|---|---|
| 1. Primer contacto | Identificar casillas, tocar para colocar una unidad, mantener pulsado para ver información y **arrastrar** una unidad a otra casilla válida | Tablero seguro, sin enemigo agresivo |
| 2. Posicionamiento | Arrastrar, cambiar de lugar, respetar ocupación única y distinguir zona propia/enemiga | **Dummies inmóviles** y sin daño para practicar |
| 3. Ataques y vida | Iniciar combate, observar alcance, daño, barra de vida y habilidades automáticas | **Dummies** que sirven de objetivos de entrenamiento |
| 4. Combate sencillo | Colocar unidades y ganar un enfrentamiento asequible, con margen para equivocarse | **Personajes enemigos existentes configurados como débiles** para tutorial; sin inventar nuevos coleccionables |
| 5. Preparación y economía | Utilizar Moon-Ken temporal, comprar, rerollear cuando proceda, entender copias/mejoras y bolsa/brief | Fase de preparación guiada sin presión |
| 6. Banco táctico | Arrastrar una copia al banco, observar reembolso; devolverla pagando lo recuperado; entender que otras copias del mismo tipo pueden quedar en tablero y nuevas ofertas se bloquean | Escenario preparado con copia gratuita y/o copia mejorada existente |
| 7. Progreso y colección | Reclamar el premio real del tutorial, examinar mazo, Pouch normal, personaje/skill desbloqueable y monedas persistentes | Menús reales conectados al inventario |
| 8. Primera máquina | Abrir una máquina **tocando su sprite**, ver 14 recompensas, estado de bloqueos y garantía, gastar **1 Saturncoin gratuita ya recibida** y observar premio real confirmado | Máquina existente y disponible, sin compra real |
| 9. Primer BO5 normal | Jugar contra una IA inicial que respete reglas reales de partida, conocer resultado y XP | Bots de dificultad baja y ascenso gradual conforme al aprendizaje |
| 10. Multijugador | Explicar crear/entrar sala por código/enlace cuando servicios y conexión funcionen | Solo tras haber comprendido el modo contra bots |

- **Dummies** = objetivos **exclusivamente didácticos**, no Monsters/Whelps coleccionables, ni nuevos premios. Reutilizar un dummy técnico existente si lo hay. Si no existe un arte adecuado, crear la lógica funcional aislada y **pedir aprobación del diseño visual** antes de introducir un sprite nuevo en el juego final.
- **Enemigos débiles** = personajes existentes con comportamiento/estadísticas especiales para la instancia de tutorial, configurables y aislados. **No reducir estadísticas globales** del personaje ni alterar partidas competitivas por facilitar el tutorial. Evitar derrotas frustrantes en los primeros ejercicios sin fingir combate normal.
- Integrar indicaciones adaptativas: resaltar casilla o unidad permitida, indicar gesto táctil, repetir explicación si falla y desbloquear el siguiente paso solo cuando se ejecute la acción. Mantener controles de toque, arrastre y pulsación larga coherentes con el juego actual; no forzar gestos incompatibles.
- Preservar inglés como idioma principal y localización futura. Permitir cerrar y **retomar** tutorial guardando progreso sin rehacer concesiones económicas; permitir repasar pasos completados sin volver a cobrar su recompensa. Las condiciones para omitir módulos y cómo se desbloquean se ajustarán sin saltarse la entrega única.
- No modificar sin permiso las reglas reales de tablero, IA, ataque, economía o habilidades; usar escenas/estados de entrenamiento aislados conectados a las mecánicas reales donde sea posible.

## 11.3. Recompensas por tutorial y controles de entrega (APROBADO; MONTOS PENDIENTES)

- **Cada módulo recompensable del tutorial debe entregar un premio visible** al completar su objetivo. El jugador tiene que entender qué aprendió y qué ganó. Los premios pueden ser Earthcoins, XP, Pouches normales o Saturncoins gratuitas **si el creador aprueba esa asignación concreta**. **Nunca entregar Moon-Kens como moneda permanente**, ya que Moon-Ken solo existe en partidas.
- Definir `tutorialStepId`, `completionCondition`, `rewardDefinition`, `claimStatus`, `claimedAt`, `rewardTransactionId` e `inventoryVersion` o equivalentes adaptados al proyecto. La recompensa de cada paso se concede **una sola vez**; si se repite el tutorial, solo puede revisitarse sin farmear monedas o Pouches.
- **No inventar cantidades de cada premio, personajes extra, habilidades ni paquetes cosméticos.** Codex puede construir la arquitectura de recompensas y presentar al creador una **tabla concreta de premios por módulo como PROPUESTA** para aprobación antes de activar su entrega. La suma de beneficios debe ser razonable para no vaciar el sistema de progresión desde el primer día.
- Conservar la propuesta anterior de **1 Pouch normal durante el tutorial** como asignación inicial en revisión; no entregar varias por accidente. La **Saturncoin gratuita de bienvenida** es una concesión separada y única, no se duplica con el tutorial.
- Acreditar permanentemente con método autoritativo adecuado para el entorno (servidor seguro cuando exista). Las transacciones tienen ID e idempotencia, toleran desconexión, no permiten reclamar dos veces y se recuperan tras reinicio; no colocar la recompensa premium bajo control exclusivo de UI/cliente en producción.
- Presentar en UI: tarjeta de recompensa ganada, saldo actualizado y estado `pendiente / confirmada / error reintentable`; solo felicitar como acreditado tras persistencia verificada. Mantener reclamación accesible si falla la conectividad.

## 11.4. Saturncoins iniciales y primera extracción (APROBADO; NÚMERO FINAL PENDIENTE)

- Toda cuenta nueva recibe `starterSaturncoins` **gratuitas** exactamente una vez. El sistema debe hacer posible **al menos una extracción real** desde la primera experiencia (costo actual = 1), siempre que haya una máquina elegible y funcional.
- **No forzar un gasto automático ni decidir por el jugador qué personaje obtener:** mostrar una introducción breve, conducirlo a la sección Tienda, permitir que elija qué máquina abrir tocando el sprite y que confirme su primera extracción. Si el tutorial sugiere una máquina concreta, es guía, no imposición de elección.
- En desarrollo, el valor provisional **1 Saturncoin inicial** del roadmap anterior sigue siendo válido para pruebas, pero el **saldo de bienvenida final** y los **premios adicionales por tutorial** están pendientes de tu aprobación, no se inventan. Si el creador decide más monedas para probar varias máquinas, actualizar el parámetro sin reprogramar sistemas.
- Guardar explícitamente la concesión de bienvenida con transacción única por jugador, sin duplicaciones por cambio de dispositivo, reinicio, reintento ni cuentas vinculadas; no reutilizar créditos internos de QA como saldo real.
- La animación de extracción debe reflejar el premio real y persistido, no una simulación aislada para aparentar que la máquina funciona. La extracción mantiene sus 14 premios únicos, condiciones de garantía, habilidad bloqueada y probabilidad visible.

## 11.5. Balance y objetivos no definitivos

Hipótesis para beta, no promesas: primer personaje extra cerca de primeras **5 partidas**, **2–3 encuentros BO5 por sesión** como referencia de pruebas, sensación de progreso temprano, variedad de mazos y decisiones sin pay-to-win. Medir antes de aumentar tiempo de desbloqueo. No adaptar el azar secretamente según gasto individual o frustración.

---

# 12. MULTIJUGADOR, HOST, SEGURIDAD Y COSTOS

## 12.1. Mantener el stack actual

Mantener **Unity Lobby + Unity Relay + Netcode for GameObjects** mientras cumplan necesidades. No cambiar a servidores dedicados sin justificación por concurrencia, seguridad o costo. Verificar modelo host/cliente real, autoridad de eventos, validaciones, estados, cierre de host, reconexiones viables y consistencia de los nuevos sistemas (banco, ventana de habilidades, fin de ronda, mazos, recompensas).

## 12.2. Riesgos y mitigaciones realistas

- El host puede ser manipulable: **no afirmar antitrampas total** cuando resultados provengan de un host cliente.
- Para economía permanente, no aceptar ciegamente «gané 10 partidas» enviados por el cliente. Definir qué validaciones reales son posibles hoy y bloquear premios comerciales vulnerables hasta resolver autoridad.
- Prevenir reenvío de resultados, edición local de inventario, saldo, probabilidades, compras falsas, extracción de premios arbitrarios, horas locales manipuladas y rewards por sesiones artificiales.
- Construir una matriz de riesgos: impacto, explotabilidad, mitigación actual y qué necesitaría infraestructura autoritativa futura.
- Mantener juego contra bots viable si no hay comunidad conectada. Diferenciar recompensas de bot/PVP cuando corresponda y proteger su verificación.

## 12.3. Rendimiento y control de gastos

Medir por separado usuarios registrados, MAU, DAU, **CCU**, sesiones, creación de lobbies, transferencias/banda Relay, Cloud Save, Cloud Code, Analytics, egress, tráfico por partida y costos. Proyectar escenarios de pocos jugadores y crecimiento.

Crear procedimientos para calcular **costo por partida, costo por usuario activo, uso mensual y alertas**. Documentar rutas verificadas en la consola Unity para consultar uso y presupuestos; no inventar nombres de menús sin verificarlos. No contratar servicios ni aceptar pagos automáticamente. Optimizar reconexiones y tráfico sin alterar sincronización.

---

# 13. ANALÍTICA, MÉTRICAS Y BETA

## 13.1. Catálogo de eventos (APROBADO)

Medir, con consentimiento y privacidad según corresponda: creación/vinculación de perfil, tutorial iniciado/terminado, primera partida, BO5 iniciados/terminados, rondas, victorias y derrotas, abandono, duración sesión, uso de personajes y habilidades, mazos, compras y rerolls de combate (sin confundir con ingresos comerciales), movimientos de banco, recompensas de misión, XP y maestría, Pouches abiertas, máquinas vistas/extraídas, garantías, desbloqueos, errores, lag/disconnect y conversiones futuras.

Agregar eventos monetarios de anuncio/IAP **solo** si existen funciones reales; no registrar cobros ficticios. Catálogo con evento, versión, finalidad, propiedades permitidas y protección de información personal.

## 13.2. Métricas de producto y negocio

DAU, MAU, retención D1/D7/D30, partidas por usuario, promedio de sesión, progreso gratuito por hora, éxito del tutorial, % de jugadores que desbloquean nuevo personaje, uso/combinaciones, balance Monster/Whelp, apertura de tienda y drop-off, conversión futura a pago, ARPU, ARPPU, ARPDAU, eCPM, ingresos netos y costos.

Separar claramente **datos observados, estimaciones, proyecciones y supuestos**. No fingir datos ni ingresos.

## 13.3. Beta

Preparar beta cerrada inicial de **aprox. 20–50 probadores** cuando haya estabilidad básica. Incluir identificación de versión, reporte de error/sugerencia, trazas técnicas sin secretos, verificación de retención y pruebas en Android real. Solicitar autorización antes de invitar/publicar usuarios.

---
# 14. EVOLUCIONES — VISIÓN FUTURA, NO CONTENIDO INVENTADO

**ESTADO:** FUTURO — FRAMEWORK PLANIFICADO; cada evolución concreta REQUIERE APROBACIÓN.

## 14.1. Visión

Evoluciones inspiradas conceptualmente en la idea de trasformación estratégica de juegos tipo Clash Royale, pero **originales y adaptadas** a Monster Pouch. No serán simplemente +vida/+daño/+velocidad; deben transformar decisiones de juego, patrones de movimiento, ataques, invocaciones o gestión del tablero.

## 14.2. Acceso y economía

Requisitos conceptuales para evolución: personaje desbloqueado + sus tres mejoras desbloqueadas + maestría máxima que se apruebe. El progreso gratuito debe permitir obtenerla. No vender ventajas exclusivas.

Se propuso costo de **5–6 unidades de energía/elixir** para activación y máximo **1 evolución equipada por mazo** durante primer prototipo. **NO hay aprobación final de fuente de energía, costo ni funcionamiento específico.** Primero auditar los recursos reales y preguntar al creador: no inventar un «elixir» nuevo ni confundirlo con Moon-Ken o la energía del Super.

## 14.3. Preparación técnica permitida

Planificar estructura versionada de `evolutionId`, `characterId`, requisitos, estado/equipamiento, parámetros, costo configurable, condiciones, duración, cooldown, animación/efectos, sincronización Netcode, reglas de IA y tests. **No crear ni programar una evolución piloto de Popow, Bugaloo, Monster ni ninguna otra unidad sin que el creador decida exactamente cuál y cómo funciona.** Se puede construir un marco genérico aislado únicamente si ha sido priorizado y autorizado como tarea.

## 14.4. Criterios de aprobación por evolución

Para cada evolución, obtener especificación explícita del creador de: personaje, fantasía/mecánica, costo y recurso, instante de activación, duración, limitaciones, contrajuego, parámetros, sprites/VFX, IA y multijugador. Si falta algo sustantivo, preguntar antes de integrarla en partidas reales.

---

# 15. HÉROES Y HABILIDADES ACTIVAS — VISIÓN FUTURA

**ESTADO:** FUTURO — MECÁNICA CONCEPTUAL; habilidades individuales POR DEFINIR CON EL CREADOR.

## 15.1. Objetivo

Introducir decisiones tácticas puntuales durante combates automáticos, sin transformarlos en juegos de reflejos. Algunos personajes elegibles podrán activar manualmente una habilidad en el momento escogido; pantalla táctil con icono, feedback de disponibilidad, objetivo si procede, costo, cooldown, animación y estado de bloqueo.

## 15.2. Límites propuestos para primer experimento

Como hipótesis: **una unidad con habilidad activa por equipo y una habilidad manual**, dentro de combates de ~40 s, con costo y recarga configurables. **No es autorización para imponer esa regla en el modo normal** ni para convertir a todos los Monsters en héroes.

Se mencionaron **Popow**, **Bugaloo** y **Monster base** como ideas de exploración; NO son diseños concretos aprobados. No les implementes automáticamente saltos manuales, escudos, resurrecciones diferentes ni ningún nuevo Super sin autorización.

## 15.3. Reglas técnicas preliminares

Validar que el actor existe y está vivo; que puede actuar; que tiene recurso suficiente; que cooldown/objetivo/tiempo/estado son válidos; y que el resultado se replica correctamente por la autoridad efectiva del combate. Preparar reglas para bots, bloqueo de spam y no gastar recursos al fallar la activación.

No reemplazar la habilidad base o tres mejoras actuales por una habilidad activa sin una decisión explícita; son sistemas potencialmente **adicionales**.

---

# 16. EXPANSIÓN DE PERSONAJES Y ARTE ORIGINAL

**ESTADO:** FUTURO — SOLO MEDIANTE APROBACIÓN INDIVIDUAL.

## 16.1. Política de creación

El proyecto tendrá más personajes a futuro, pero **NINGÚN personaje nuevo será diseñado, nombrado, dibujado, animado o incorporado por Codex sin que el creador lo solicite y apruebe**. La recomendación antigua «un Whelp nuevo y luego un Monster nuevo cada 4–8 semanas» es una **posibilidad editorial**, no un compromiso, cronograma ni autorización.

Al recibir autorización concreta, trabajar en este orden:

1. Nombre y tipo (Monster/Whelp) — validación del creador.
2. Rol, idea estratégica y diferenciador — validación del creador.
3. Diseño original, paleta, silueta y pixel art — validación visual del creador.
4. Ataque básico, Super/pasiva y opciones de habilidades 1–3 con interacciones — validación del creador.
5. Estadísticas numéricas con unidades y parámetros — validación del creador.
6. Sprite sheets, animaciones y VFX — aprobación de assets finales antes de integrar.
7. IA, alcance, energía, casos de bordes, sincronización, balance y pruebas.
8. Integración con colección, mazo, Pouches normales, maestría y **máquina correspondiente SOLO SI sus recompensas y arte están aprobados**.
9. Habilidad activa/evolución opcional solo en una fase y aprobación independiente.

**Cada personaje puede tener una máquina en el futuro; no hagas nacer automáticamente 14 premios con paquetes inventados al dar de alta un personaje.** Dejar registros de contenido faltante sin inventar recompensas. No modificar o eliminar diseños antiguos sin aprobación, y no presentar referencias inspiradas en Gogos como assets comerciales originales sin verificar diferenciación.

## 16.2. Plantilla de aprobación de contenido (usar solo cuando haga falta)

Para un personaje nuevo, mostrar al creador una ficha breve: `ID / nombre propuesto / tipo / concepto / diseño / ataque / habilidad base / mejora 1 / 2 / 3 / stats / coste(s) / animaciones / interacciones / máquina / pendientes`. **No rellenar por su cuenta los campos de diseño que el creador no proporcionó**; marcar «por definir» y preguntar de manera concreta.

---

# 17. TEMPORADAS, MÁQUINAS ESPECIALES, COSMÉTICOS Y EVENTOS

**ESTADO:** FUTURO; bases extensibles, contenido por aprobar.

## 17.1. Máquinas especiales

Existen referencias gráficas para máquinas independientes de las individuales de personajes:

- **Máquina de Earthcoins:** posibles emotes, marcos, iconos, fondos, efectos o decoraciones. Se sugirió **~100 Earthcoins** por extracción, **NO precio aprobado**, no asignar premios ni implementar economía funcional ahora.
- **Máquina de Saturncoins:** posibles skins, animaciones de victoria, efectos de Super, emotes animados, fondos o personalizaciones **cosméticas**. No otorgar mejoras competitivas exclusivas.

Preparar puntos de integración y asociar assets reales, si existen. **No desarrollar catálogo, cajas, drops, rarezas, sprites o sistemas cosméticos jugables sin aprobación.** No crear nuevas pantallas visibles inútiles para los jugadores.

## 17.2. Temporadas y eventos

Temporadas de **aprox. 8 semanas** y pase gratuito/premium son propuestas, no compromisos. Preparar una estructura para rotaciones, misiones y recompensas **sin imponer fechas** ni vender pases reales. Si hay pase premium futuro, priorizar cosméticos y accesibilidad gratuita de personajes/mejoras.

Eventos especiales podrán cambiar reglas **en modos separados** (equipos fijos, rotaciones, desafíos, etc.) cuando el creador apruebe contenido; no modificar el combate competitivo normal para construir un evento.

---

# 18. MONETIZACIÓN — PREPARADA, NO ACTIVADA

## 18.1. Compras integradas (FUTURO, sujeto a autorización)

Preparar separación modular de catálogo de productos, solicitud, recibos/tokens, validación de servidor, entrega, historial, conciliación, reembolsos, revocaciones y recuperación de compras. Investigar integración Unity IAP / Google Play Billing **vigente** antes de programar. IDs por transacción, idempotencia y pruebas con compras sandbox.

**No habilitar pagos reales, no definir precios finales ni contratar servicios sin autorización.** No almacenar datos de tarjetas en Monster Pouch. No acreditar Saturncoins compradas hasta confirmar legítimamente la compra. Exponer probabilidades de recompensas aleatorias cuando exija la plataforma, de forma veraz y comprensible.

La referencia histórica de **10 Saturncoins ~ USD 2,99** es solo un escenario a revisar (precios regionales, impuestos, tasas y balance). No crear conversiones ni mostrar precios como oficiales.

## 18.2. Anuncios recompensados (FUTURO, sujeto a autorización)

Arquitectura compatible con AdMob para Unity u otra solución aprobada; **solo voluntarios/recompensados** como prioridad. No interrumpir combate, habilidades o resultados. Posibles recompensas de Earthcoins u otros recursos permanentes aprobados; **NO regalar Moon-Ken persistentes**, puesto que Moon-Ken es recurso temporal de combate. No crear recompensas pagadas con ventajas imposibles de ganar jugando gratis.

Configurar tope por día, cooldown, disponibilidad y elegibilidad, solo cuando se aprueben cifras. Verificación servidor de recompensa donde sea viable, protección contra duplicados, SDK en modo prueba durante desarrollo. Activación independiente de IAP, revisión de privacidad y protección de menores.

## 18.3. Regla comercial

Primero estabilidad y diversión → perfiles/guardado seguros → progresión gratuita confiable → métricas de beta → validación económica → revisión legal/plataforma → aprobación explícita del creador → monetización real. **No optimizar la economía para frustrar deliberadamente al jugador.**

---

# 19. OPTIMIZACIÓN, PUBLICACIÓN Y PRIVACIDAD

## 19.1. Android y recursos

Auditar peso actual de APK y preparar Android App Bundle cuando se publique; compresión y atlas de sprites, evitar arte innecesariamente pesado, imágenes pixel-perfect, carga de recursos, GC, asignaciones frame, pooling, animaciones, memoria, tiempos de carga, rendimiento de dispositivos modestos, 9:16 y conectividad inestable. No degradar la nitidez pixel art por comprimir y no borrar respaldo de recursos antiguos.

## 19.2. Preparación Google Play

Checklist con estado y enlaces oficiales **verificados al ejecutarse**, sin inventar requisitos actuales: cuenta dev, identidad, iconos, capturas, descripción, política de privacidad, clasificación de contenido, seguridad de datos, permisos, anuncios, IAP, pruebas, AAB, firma, API/SDK objetivo, normas si el arte parece atractivo a menores, probabilidades aleatorias y políticas de eliminación de datos.

Codex puede preparar archivos, informes y borradores; NO registrar cuentas, aceptar contratos, pagar tasas, publicar builds ni configurar cobros automáticamente. Señalar lo que dependa de acciones humanas.

## 19.3. Privacidad y datos

Minimizar datos personales; no capturar identificadores innecesarios; retención y eliminación cuando correspondan; revisar consentimientos según edad/territorio. No instalar sistemas de analítica/ads con recolección comercial sin la configuración de privacidad legal pertinente.

---

# 20. MARKETING, PRESUPUESTO Y NEGOCIO

Estos entregables son **documentación comercial**, no comandos para publicar, pagar o contratar automáticamente.

## 20.1. Estrategia orgánica de lanzamiento

Priorizar TikTok, Reels, Shorts, Discord, comunidades indie y microcreadores; material propio autorizado y gameplay real. Organizar carpeta fuera del árbol que Unity importa automáticamente para: clips verticales, capturas, gameplay, logos, mensajes, guiones, calendario, microcreadores y testeo.

Preparar **20 ideas para vídeos cortos**, guiones de **5 primeros vídeos**, calendario editorial de **30 días**, plan de comunidad, invitación a testers, acercamiento a microcreadores y métricas por publicación. Los guiones deben basarse en funciones y assets efectivamente existentes, sin anunciar contenido inexistente como disponible.

## 20.2. Modelo financiero editable

Preparar una hoja de presupuesto con fórmulas (preferiblemente XLSX, si la herramienta está disponible) y escenarios **conservador / intermedio / optimista**, separando supuestos de datos reales.

- **Ingresos potenciales:** anuncios (impresiones/eCPM), ventas de Saturncoins (conversión/ticket), futuros productos digitales autorizados.
- **Costos potenciales:** Unity Lobby/Relay/servicios, almacenamiento/backend, dominio, herramientas, publicidad, arte/música, comisiones Google, bancos, impuestos estimados y registro formal si corresponde.
- **Resultados:** ingresos brutos, comisiones, ingresos netos, costos fijos/variables, margen, punto de equilibrio, costo por jugador, necesidad de reserva, capital de reinversión.
- **Variables editables:** DAU/MAU/CCU, retención, anuncios por DAU, eCPM, pagadores %, ARPPU, tarifas, publicidad pagada, costo por adquisición, servicios cloud, impuestos supuestos.

No inventar ingresos «garantizados» ni camuflar proyecciones como ingresos percibidos. Propuesta de política: obligaciones e impuestos → reserva de emergencia → presupuesto infraestructura → reinversión configurable → retiros personales solo con estabilidad. Separar caja personal y proyecto.

## 20.3. Aspectos legales y fiscales en Bolivia

Guía con **trámite, institución, motivo, momento recomendado, costo oficial verificado, documentos, enlace oficial y estado**. Investigar cuando se trabaje esta fase: SIN/NIT, SEPREC y empresa unipersonal si corresponde, SENAPI/marca, titularidad de código/arte/música, ingresos extranjeros, medios de cobro Google Play, bancos/comisiones, impuestos sobre ventas digitales, privacidad y menores, regulación de sorteos aleatorios y publicidad.

Diferenciar requisitos confirmados con fuente oficial y asuntos que requieren contador/abogado. No ejecutar registros, declaraciones ni pagos. El roadmap NO constituye asesoramiento legal final ni prueba de que ya se haya hecho un trámite.

---

# 21. ORDEN REAL DE EJECUCIÓN — ROADMAP CON DEPENDENCIAS

**Este orden sustituye secuencias contradictorias de prompts previos.** No se promete terminar «todo en una sola pasada». Se trabaja en ciclos seguros, pero sin detener lo independiente por un detalle creativo pendiente. Los estados se actualizan tras auditar el repositorio.

| Etapa | Prioridad | Qué se realiza | Puerta de salida |
|---|---|---|---|
| **0. Auditoría y respaldo** | Inmediata | Versiones Unity, código, escenas, máquinas/arte en `pictures/utilities` si hay acceso, servicios, comportamiento vigente, backups/rama segura y mapa de riesgos | Inventario y build/base verificada o limitaciones explicadas |
| **1. Estabilidad + cambios aprobados de partida** | Alta | **Banco táctico** y convivencia campo/banco; ventana inicial; Tokoro; ronda HP; VICTORYYY/YOU LOSE; fixes gráficos seguros | Pruebas de combate, economía Moon-Ken y regresión; diferencias de venta consultadas |
| **2. Núcleo de datos / monedas permanentes** | Alta | Separar cuatro monedas; modelos de inventario, IDs, guardado versionado; **starterSaturncoins y concesión única**; compatibilidad y migración; reutilizar backend si existe | Saldo inicial sin duplicados, sin pérdidas; esquemas probados |
| **3. Progresión inicial** | Alta | Mazo inicial, colección, XP y maestría, Pouches normales configurables y misiones básicas autorizadas | Desbloqueos consistentes y guardados; balance provisional visible |
| **4. Máquinas individuales** | Alta | Asociación sprites existentes, 14 premios, toque sobre sprite, 1 Saturncoin, probabilidades, garantías, UI y pruebas de 100k simulaciones; **auditar y proponer rediseño de TODAS las máquinas** | Catálogos y sorteos correctos; sin doble cobro; inventario visual y propuestas de arte registradas |
| **5. Seguridad y online preparados** | Alta antes de dinero real | Authority/anticheat realista, cuentas y cloud si no estaban, idempotencia de economía, reconexión, costos Relay | Análisis de ataques y operaciones sensibles verificadas |
| **6. Primer usuario y calidad visual** | Alta para beta | **Tutorial práctico** (arrastrar/tocar, dummies, enemigos débiles, banco), **recompensas por pasos y visita guiada a máquina usando Saturncoins gratuitas**; bases por escenario, Parque de Juegos; **integrar rediseños de máquinas que el creador haya aprobado** | Flujo nuevo jugador real, recompensas únicas, primera extracción real, QA visual móvil y aprobación de arte |
| **7. DEV / analítica / beta cerrada** | Alta | Herramientas de prueba protegidas, catálogo de eventos, balance, 20–50 testers autorizados, métricas de retención | Estabilidad y datos reales para decisiones |
| **8. Preparación comercial** | Media | AdMob en pruebas, IAP sandbox, privacy/legal, Google Play checklist, finanzas, marketing | No se activa cobro ni publicación sin autorización |
| **9. Evoluciones** | Futura / bloqueada por diseño | Definir mecánica y unidad con el creador; luego framework y prototipo autorizado | Ficha creativa y reglas aprobadas + QA |
| **10. Héroes manuales** | Futura / bloqueada por diseño | Decisiones de activación, personaje y habilidad aprobados; luego prototipo y Netcode | Ficha creativa y recursos aprobados + QA |
| **11. Expansión y live ops** | Futura | Personajes uno a uno, cosméticos, máquinas especiales, eventos, temporadas | Aprobación individual y base estable |

**Flexibilidad:** la Etapa 1 puede subdividirse o adelantar correcciones de UX; las fases de infraestructura deben anticiparse si una característica depende de nube segura. Las fases futuras **no se ejecutan automáticamente** al concluir la anterior, sino cuando estén priorizadas y sus decisiones creativas aprobadas. El orden de las etapas es una **priorización por dependencias**, no fechas ni obligaciones de lanzar contenido.

## 21.1. Política de preguntas

**No bombardees al creador con preguntas obvias de implementación.** Pregunta cuando la respuesta cambia la identidad del juego, una regla de balance/arte sensible o una decisión irreversible. En esos casos:

- Explica en 1–3 líneas **qué decisión falta, por qué, y cuáles son 2–3 opciones** cuando ayuden.
- Da una **recomendación claramente marcada como propuesta**, no como hecho aprobado.
- Continúa en paralelo con tareas independientes ya autorizadas cuando sea seguro.
- No realices cambios creativos antes de recibir la respuesta.
- Nunca uses «tengo libertad de diseño» como permiso implícito para inventar personajes, premios, habilidades o skins.

---

# 22. CASOS DE PRUEBA DE ACEPTACIÓN TRANSVERSALES

## 22.1. Banco y economía de combate

- Un Atori banco + otro Atori campo coexisten sin error; filtrado de futuras ofertas mantiene su regla actual.
- El retorno desde banco usa precio por instancia, conserva mejoras y no duplica un mismo GameObject/estado lógico.
- Moon-Kens se acreditan y debitan correctamente sin pérdidas ni explotación.
- Diferentes costos de copias y upgrades se preservan; gratuitos no pagan artificialmente; casilla inválida no gasta.

## 22.2. Inicio, curación y combate

- Todas las habilidades marcadas `OnRoundStart` se disparan antes de combate según tiempos configurados.
- Toronko 60 HP/s × 3 s; no ataca ni carga energía en ese intervalo; se recupera correctamente después.
- Si hay invocaciones, control o revivir, se resuelven respetando reglas vigentes; iguales resultados host/client cuando corresponda.

## 22.3. Presentación de final

- HP supervivientes, mitad solo Whelps y otros no Monster, números animados, barras fluidas casi simultáneas, normalización a total mayor, bandera al final.
- Ganar encuentro produce VICTORYYY/confeti; perder produce YOU LOSE/fondo rojizo; no reentrega premios por volver a mostrar escena.
- El resultado real nunca depende de una animación, barra o cliente secundario.

## 22.4. Contenido y escenarios

- En tutorial, el usuario puede **tocar para colocar**, **mantener para inspeccionar** y **arrastrar** la unidad siguiendo casillas válidas; los dummies funcionan solo en entrenamiento y los bots débiles usan personajes existentes sin cambiar balance competitivo.
- En el paso "primera máquina", el jugador elige una máquina existente, puede tocar su sprite, ve su Saturncoin gratuita, confirma extracción y recibe **un premio real persistido** conforme a garantías/14 slots.
- Inventariar **todas** las máquinas originales de `pictures/utilities`; cada rediseño que llegue a producción es más limpio, se identifica con su personaje, tiene aprobación del creador y conserva versión anterior. Verificar **todos** los `machineId` y `characterId` tras cambios de sprites; ningún progreso económico se altera por arte.

- Base visual se corresponde con ambientación, ningún pedestal del campo real reutilizado incorrectamente donde exista su versión aprobada.
- No hay líneas verticales antiestéticas ni piezas desconectadas en estantería del Parque de Juegos; borde no tapa unidades y mantiene tablero/colliders.
- No aparecieron personajes, skins, poderes ni sprites nuevos no autorizados.

## 22.5. Economía persistente

- Crear cuenta nueva otorga **Saturncoins gratuitas iniciales** exactamente una vez, suficientes para al menos una extracción al costo aprobado de 1. Entrar de nuevo, cerrar/reabrir, reinstalar o vincular la misma cuenta **no vuelve a regalar** saldo de bienvenida.
- Iniciar tutorial y completarlo entrega **solo** recompensas de pasos aprobadas; completar de nuevo un paso, omitirlo, simular reconexiones y repetir la animación no crea nuevas transacciones de premio.
- Un paso didáctico no se contabiliza como una victoria BO5 normal, ni activa misiones/recompensas de partida completa por error. Saldo gratis de bienvenida, saldo de QA y compra futura no se mezclan.

- Moon-Ken desaparece al acabar una partida según reglas actuales, no se convierte en Earthcoins/Suncoins/Saturncoins.
- Save/load persistente, inventario sin duplicados, XP y misiones sin repetición, compras/extracciones idempotentes.
- Máquinas: 14 premios distintos, 10° tiro personaje como máximo, habilidades solo tras personaje, habilidad inmediata posterior si pendiente, costo 1 Saturncoin, sin gastar al completar.
- Pouches normales y maestrías no duplican habilidades ya obtenidas en máquinas y viceversa.
- Pruebas con cuenta invitada claramente distinguida de backend seguro de producción.

## 22.6. Seguridad / monetización / redes

- Perfiles normales jamás obtienen DEV; SDK de anuncios e IAP en prueba y apagado por defecto; no se habilitan cobros.
- La red no permite decidir premios por el cliente sin verificación; se documentan vulnerabilidades restantes por arquitectura host.
- Diferentes resoluciones 9:16 y dispositivos Android, cambios de orientación/resolución y conectividad variable no rompen menús ni estado.

---

# 23. DECISIONES CERRADAS, PROPUESTAS Y PENDIENTES — REGISTRO INICIAL

## 23.1. Decisiones ya explícitas y vigentes

| ID | Estado | Decisión |
|---|---|---|
| MP-DEC-001 | APROBADO | Codex autónomo técnicamente; creador aprueba contenido y cambios creativos |
| MP-DEC-002 | APROBADO | Roadmap flexible, modular y versionado; no crear nada por «completar la lista» |
| MP-DEC-003 | APROBADO | Moon-Ken solo combate; Earthcoins/Suncoins/Saturncoins permanentes |
| MP-DEC-004 | APROBADO | Banco devuelve valor recuperable; volver a desplegar cuesta lo efectivamente pagado; mejoras conservadas |
| MP-DEC-005 | APROBADO | Misma especie permitida simultáneamente en **tablero y banco**; banco bloquea **nuevas ofertas del brief**, no copias existentes |
| MP-DEC-006 | APROBADO | Cada máquina individual: 14 premios únicos (1 personaje, 3 habilidades, 10 recursos), no skins |
| MP-DEC-007 | APROBADO | Extracción individual cuesta 1 Saturncoin; personaje máximo en tiro 10; primera extracción posterior garantiza habilidad pendiente |
| MP-DEC-008 | APROBADO | Progresión gratuita para personajes y habilidades; maestría y Pouches normales como canales |
| MP-DEC-009 | APROBADO | Tokoro/Toronko tercera habilidad: 60 HP/s, primeros 3 s, sin ataques ni energía |
| MP-DEC-010 | APROBADO | Ventana habilidades inicio ~1,5 s configurable por habilidad |
| MP-DEC-011 | APROBADO | Final ronda: totales HP, mitad visual no Monsters, barras verde/roja fluidas y banderas después |
| MP-DEC-012 | APROBADO | Final encuentro: VICTORYYY/confeti o YOU LOSE/fondo rojizo |
| MP-DEC-013 | APROBADO | Base por ambientación, eliminar artefactos, rediseñar Parque de Juegos menos saturado, sin tubos, borde arcoíris |
| MP-DEC-014 | APROBADO | Conservar Lobby + Relay + NGO, personajes y arte existentes; no rehacer motor ni cobrar sin autorización |
| MP-DEC-015 | APROBADO | Mazo inicial: Monster base + Atori, Blotan, Bugui, Flor, Gochan, Trimol y Atong |
| MP-DEC-016 | APROBADO | Tutorial **interactivo** con toques, arrastre, dummies y combates graduales contra personajes existentes debilitados solo en entrenamiento |
| MP-DEC-017 | APROBADO | Tutoriales dan recompensas únicas; importes y tabla exacta por módulo se someten a aprobación del creador |
| MP-DEC-018 | APROBADO | Toda cuenta nueva recibe Saturncoins gratuitas de bienvenida para probar máquinas; **cantidad final configurable y pendiente** |
| MP-DEC-019 | APROBADO | Rediseñar **todas** las máquinas para ser menos recargadas y más fieles a cada personaje, usando `pictures/utilities`; diseños finales bajo aprobación creativa |

## 23.2. Propuestas para configurar y probar, NO definitivas

| Área | Referencia provisional | Qué falta |
|---|---|---|
| Máquina individual | Chances por tiro: 1,1,1,1,1,2,4,8,20,100 % | Aprobar distribución final tras simulaciones |
| Pouch normal | 2 % personaje, garantías 3/6/12 | Revisar frecuencia y premios con beta |
| Pouch normal por partida | 1 por 2 BO5; 1 tutorial | Aprobar balance final y protección antifarming |
| Maestría | 2/6/14 partidas acumuladas | Aprobar conteo y dificultad final |
| Saturncoins de bienvenida / pruebas | **≥1 inicial gratuito obligatorio** para primera extracción; **1 actual de prueba** y 1 por 2 BO5 como referencias históricas, no valores finales | Definir monto de bienvenida final, relación con recompensas de tutorial y distribución posterior |
| Paquete comercial | 10 Saturncoins por ~USD 2,99 | Precio/regiones/tasas/no activar aún |
| Evoluciones | 1 equipada, hipotético costo 5–6 de recurso por definir | Mecánica, recurso y evolución concreta |
| Héroes activos | 1 por equipo, activación manual | Personaje, habilidad, costo y balance |
| Temporadas | Duración ~8 semanas | Decidir si/cuándo se incorpora |
| Máquina Earthcoins | 100 por extracción | Arte, recompensas, economía y aprobación |
| Metas beta | Primer personaje extra ~5 partidas; 2–3 BO5/sesión | Validar con analítica real |

## 23.3. Puntos que Codex NO debe resolver inventando

- **Valor de venta vs inversión total**: si el vendedor actual devuelve menos/más de lo invertido, comparar y **consultar la fórmula de banco** antes de programarla.
- **Catálogo preciso de los 10 paquetes** de recursos por máquina: usar las definiciones aprobadas existentes; si faltan, solicitar composición.
- **Nombres y mapeos de assets** Earthken/Earthcoin, Sunken/Suncoin, Saturnken/Saturncoin, Flo/Flor, Tsu/Suki, Tokoro/Toronko, Anuik/Aniku: confirmar IDs del proyecto y migrar sin duplicar.
- **Arte final** de borde arcoíris, bases faltantes, margarita rosa y cualquier rediseño.
- **Distribución final** de probabilidades, maestría, economía de recompensas y XP que solo tienen valores experimentales.
- **Cantidad exacta de Saturncoins de bienvenida y recompensas de cada módulo tutorial:** el beneficio inicial y la existencia de premios de tutorial están aprobados; presentar propuesta de cifras y asignaciones sin activar montos no autorizados. Usar 1 como valor de QA provisional ya existente.
- **Diseño visual de CADA máquina:** el rediseño integral está aprobado como objetivo, pero bocetos/composición, nuevos sprites, colores específicos, siluetas e integración final requieren una aprobación explícita (individual o lote identificado) del creador. Si `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\pictures\utilities` no es accesible, no afirmar haber inspeccionado assets.
- **Evoluciones, héroes, nuevos personajes, cosméticos y temporadas**: sin contenido jugable hasta aprobación específica.
- **Dilema de recompensa cuando un catálogo se agotó** o una garantía no puede cumplirse por premios ya obtenidos: evitar gasto y consultar reglas de resolución si no existen.
- **Empate o vacío en animación de HP**: mantener resultado actual y solicitar criterio solo si hay divergencia.
- **Autoridad de recompensas permanentes en partidas hosteadas**: no declarar seguridad total hasta tener validación adecuada.

---

# 24. INSTRUCCIÓN OPERATIVA FINAL PARA CODEX

**Empieza por la ETAPA 0.** Estudia el proyecto real y prepara un respaldo/rama. Revisa los cambios existentes y las capacidades disponibles. Presenta solo las **decisiones verdaderamente bloqueantes** y comienza después la implementación de los requisitos aprobados por orden de dependencias. Evita detener todo por una sola decisión creativa: ejecuta lo independiente, pero NO rellenes un vacío de diseño inventando.

Ten siempre presentes estas instrucciones de máxima prioridad:

1. **Es un ROADMAP VIVO.** Se pueden cambiar o cancelar ideas, números, fechas y fases; registra qué versiones quedan sustituidas.
2. **Los personajes no son tuyos para crearlos.** El creador decide todo lo creativo; también evoluciones y habilidades nuevas. No inventes recompensas o arte sin permiso.
3. **El banco NO prohíbe que la misma especie esté a la vez en campo y banco.** Bloquea nuevas ofertas de esa especie según la regla de brief ya existente; las copias ya desplegadas permanecen.
4. **Guardar una instancia en banco recupera Moon-Kens y conserva personaje/mejoras; retornarla paga su inversión real.** No confundir identidad por especie con instancia ni mezclar Moon-Ken con dinero de cuenta.
5. **No rompas lo que funciona.** Conserva personajes, animaciones, Netcode, herramientas y UI aprobada.
6. **No declares éxito no comprobado.** Compila, prueba y documenta evidencia, o informa qué no pudo validarse.
7. **No actives nada comercial real sin aprobación.** Compras, anuncios, planes pagos, publicación y obligaciones legales requieren acciones y autorizaciones explícitas.
8. **La primera experiencia SÍ incluye recompensas:** enseñar a arrastrar y colocar, practicar contra dummies y bots débiles y luego permitir gastar Saturncoins gratuitas de bienvenida en una máquina de verdad. No fingir premios ni cobrarlos con dinero real.
9. **TODAS las máquinas deben revisarse y rediseñarse visualmente** para ser más limpias y asociadas a cada personaje; preservar originales y solicitar tu aprobación antes de integrar cada arte final.
10. **La calidad y jugabilidad mandan.** Monetizar de forma sostenible, sin engañar, sin «pay-to-win» ni bloquear acceso gratuito a personajes/habilidades.

**OBJETIVO:** convertir Monster Pouch en un juego indie comercial sostenible, flexible, seguro y divertido, donde cada actualización respete la visión del creador y el progreso técnico anterior; no simplemente cumplir una lista de features ni producir contenido que el creador no pidió.

---

# REGISTRO DE ACTUALIZACIÓN — V1.2 (20 DE OCTUBRE DE 2026)

**Se añade sin eliminar decisiones anteriores:**

1. Recompensas verificadas por completar módulos de tutorial, registradas una sola vez y con valores definitivos sujetos a aprobación.
2. Saturncoins gratuitas al crear una cuenta para abrir al menos una máquina al principio; `starterSaturncoins` configurable; conservar **1 inicial** como referencia de prueba hasta decidir el valor final, sin doble entrega.
3. Tutorial de práctica: tocar, pulsación larga, arrastrar, posicionar, dummies, enemigos débiles existentes, banco táctico, preparación y primera extracción real.
4. Rediseño integral de **todas** las máquinas del directorio local señalado, más simples y asociadas visualmente a cada personaje, con originales conservados y aprobación de cada propuesta final.
5. QA, fase de onboarding, registro de decisiones y requisitos de persistencia ampliados coherentemente.

No se han aprobado automáticamente personajes, skins, poderes nuevos, montos finales de recompensa ni nuevos productos comerciales.

---

# ANEXO A. REFERENCIAS DE COMBATE YA CONVERSADAS — NO REINVENTAR

Estas notas ayudan a Codex a reconocer sistemas y personajes que **ya se diseñaron en conversaciones anteriores**. **No son instrucciones para reemplazar a ciegas el código actual**: contrastar con assets, IDs y especificaciones más recientes. Toda discrepancia que afecte una mecánica debe registrarse y, si es creativa, consultarse. **No utilizar este anexo para crear contenido faltante sin aprobación.**

## A.1. Reglas de tienda, colocación e IA

- Tablero 6 × 10; movimientos por casillas válidas, sin dos instancias en la misma casilla; alcance, dirección de ataques, pathfinding y reglas de selección de objetivos ya existentes.
- Referencias conocidas de desempates: valoración de celda, cuadrantes A/B/C/D, ventaja de lado (BlueSide orientado vertical / RedSide diagonal) y, sin desempate resoluble, cancelar el tick. **No reconstruir esta prioridad por descripción breve si ya está implementada.**
- Reglas previas de ofertas: tres opciones a la vez, cuatro copias por Whelp cuando aplique, comprar NO repone automáticamente el slot y reroll actualiza las ofertas según la lógica vigente.
- **Nueva corrección obligatoria:** una instancia de personaje en banco debe impedir **nuevas ofertas de ese tipo** en el brief bajo la lógica actual, PERO pueden existir otras instancias del mismo tipo en campo. No filtrar personajes ya desplegados por `characterId`.
- Interacción previa: pulsar para colocar, mantener pulsado para consultar datos/descripciones, arrastrar para mover; evitar introducir gestos que rompan el banco táctico.
- En caso de empates, casillas ocupadas, desplazamientos involuntarios y teletransportes, priorizar reglas preexistentes/últimas correcciones específicas de cada unidad.

## A.2. Registro de habilidades conocidas (referencias, verificar versiones)

| Personaje | Referencia de mecánicas previamente definidas |
|---|---|
| **Monster base** | Revive con 50 % de vida; alternativas de mejora conocidas: segunda resurrección al 25 %, daño/empuje adyacente al revivir, o reaparecer cuatro casillas hacia adelante. **No acumular opciones de mejora distintas**. |
| **Atori** | Melee; prioriza al enemigo con menos vida. Mejoras: escudo del 20 % durante 5 s; +50 % velocidad al eliminar; curación de 20 por eliminación. |
| **Blotan** | Melee; pasiva de generación Moon-Ken cada 20 s. Mejoras referidas: generar cada 10 s; +20 HP; robar 2 Moon-Ken si sigue vivo al final. |
| **Bugaloo** | Monster melee; Super con reflejo de energía 3 s y provocación/taunt. Opciones: recibe 50 % de daño; refleja 150 %; recibe +50 % de curación. |
| **Bugui** | Distancia; acumula velocidad de ataque hasta 10 stacks. Mejoras: alcance infinito; stacks ilimitados con menor efecto individual; +1 daño por kill. |
| **Flor/Flo** | Cuerpo a cuerpo; Super con maceta de 20 HP que cura 20 al morir. Mejoras: cura 40; comienza con energía llena; invulnerabilidad durante 1 s. |
| **Gochan** | Distancia; ataque en cruz y energía con ataque 3 × 3 de 20 daño. Mejoras: −60 % curación enemiga; marca 2 daño/s durante 4 s; ataque 3 × 3. |
| **Jazar** | Distancia; radiación con 25 % ralentización. Mejoras: ataque en cruz; slow 50 %; efecto de suelo en primera fila rival durante 1 s. |
| **Kayon** | Distancia; invoca un dummy con energía. Mejoras: 2 dummies; explosión adyacente +1 daño; +4 vida para dummies. |
| **Popow** | Monster melee; salto al enemigo más lejano, stun 1 s. Mejoras: stun 2 s; gancho/área 3 × 3 y stun 0,5 s; +3 daño en siguiente ataque por cada objetivo impactado. |
| **Sepora** | Monster; ataque básico de rayo a 1 objetivo, Super de 3 rayos y aumento de velocidad de ataque por 5 s. Mejoras: invisibilidad por debajo del 50 % HP para ella/adyacentes; rayos básicos a 2 y Super a 5 objetivos; +1 daño por eliminación, **hasta 3 acumulaciones por ronda** según última corrección. |
| **Rayos** | Distancia; dos bolas a dos rivales con stun 0,1 s. Mejoras: stun 0,2 s; energía inicial para atacar lateralmente; lanzar hacia 3 objetivos. |
| **Tauris** | Monster; mordida con Super letal. Mejoras conocidas: energía por rivales de su fila y bloqueo de movimiento; área en gancho; Super de 50 % de vida con curación completa al matar. |
| **Trimol** | Distancia; cada 4 ataques lanza piedra que atraviesa y aturde 1 s. Mejoras: roca al iniciar; recorrido infinito; mayor daño y stun 2 s. |
| **Tsu / Suki (verificar alias)** | Distancia; ataque de chupetes, cada 4 ataques crea chicle que inmoviliza 2 s. Mejoras referidas: cabello inmoviliza casilla; chocolates explosivos; chicles de colores que conceden buffs. Las referencias actuales a habilidades rápidas de **Suki** necesitan confirmación de ID; no inventar una unidad adicional. |
| **Atong** | Melee lento; cada segundo golpe es crítico +50 %. Mejoras: crítico 100 %; siempre crítico; inamovible durante 5 s al inicio. |
| **Tokoro / Toronko** | Melee; Super de cabezazo que aturde a ambos 3 s. Mejora 1: curación de 8 HP/s durante stun. Mejora 2: conitos que curan 10 a aliados o debilitan a rivales un 20 %. **Mejora 3 ANTIGUA (últimos 10 s se cura y no ataca) QUEDA SUSTITUIDA por 60 HP/s durante primeros 3 s, sin atacar ni generar energía**. |
| **Aky** | Melee; drena energía. Mejoras: vuela al final del mapa al iniciar; se cura el 50 % de energía drenada; ataque en área tipo gancho de Popow. |
| **Hymay** | Alcance de 3 casillas; latiguea y Super atrae objetivo. Mejoras: provoca a aliados a atacar al atraído; aplica 25 % debilidad; atrae frente a sí y retrocede 1 casilla. Movimiento/retroceso sujeto a regla específica de ocupación. |
| **Anuik / Aniku** | Distancia; rebote de ataque con 50 % del daño según decisión conocida. Las tres mejoras detalladas deben leerse de la documentación/código vigente; **NO inventarlas**. |

**Regla específica conocida de desplazamiento Hymay:** tras la maniobra de retroceso, intentar posición objetivo; si está ocupada, posiciones laterales junto a ella; si no, reducir distancia en 1 y reintentar. Hay un caso extremo con todas las casillas bloqueadas cuya frase histórica fue ambigua: **no inventar el comportamiento final**, consultar si código y documentación actual no lo resuelven.

## A.3. Definición técnica de una unidad: datos numéricos

Configurar por personaje mediante el modelo **ya existente o su extensión**: ID, tipo, HP, daño, tiempo de ataque, velocidad de movimiento, rango (casillas), ataque/proyectil (velocidad/dirección/daño), Super/energía, habilidad base, mejoras 1–3, condiciones, duraciones, cooldowns, descripciones, costo, IA/IQ, sprites/animaciones y sincronización. **No asignar estadísticas faltantes por iniciativa propia**: «por definir» y consulta al creador.

## A.4. Qué información del proyecto prevalece

Si el código o la versión reciente de una ficha aprobada contradicen la tabla histórica anterior, NO reemplazar el juego por ella automáticamente. La finalidad del anexo es evitar pérdidas de contexto, identificar regresiones y permitir preguntas específicas al creador. El caso de **Tokoro/Toronko tercera habilidad** y la **coexistencia banco/tablero por tipo** son correcciones recientes expresas y, por tanto, **sí prevalecen**.

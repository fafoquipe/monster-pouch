# Auditoría inicial — 20 de octubre de 2026

Proyecto real: `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch`.
El workspace de la conversación contiene respaldos y scripts auxiliares, no es el proyecto Unity.

## Base y conservación

- Unity declarado: **6000.3.9f1**, instalado localmente. URP 17.3.0, Test Framework 1.6.0, NGO 2.13.3, Multiplayer Services 2.3.3.
- Git inicial: rama `main`, commit `f79ab5f`; **88 modificados, 2 eliminados y 678 entradas no registradas** preexistentes. No se revierte ninguno.
- Rama de trabajo: `codex/roadmap-v1-2-20261020`.
- Respaldo completo de Assets, Packages, ProjectSettings, docs, herramientas y archivos raíz en `C:\Users\HP VICTUS\OneDrive\Documentos\ChatGPT\monsters-pouch\roadmap-backup-20261020\project-source-before.tar`; estado Git y diff binario junto a él. No contiene Library ni builds derivados.
- Escena habilitada para build: `Assets/Scenes/main-scene.unity`. Las escenas de recuperación y otros assets se conservan.
- Inventario reproducible: `INVENTORY.json`; 158 scripts de runtime, 75 archivos de tests EditMode, 43 de PlayMode. Estas cantidades no equivalen a cobertura ni a pruebas aprobadas.

## Sistemas existentes y reutilizables

| Área | Evidencia en código | Estado inicial |
|---|---|---|
| Tablero | `board/`, 6 × 10, ocupación, reservas, movimiento, prioridades y máscaras | Implementado; conservar reglas |
| Rondas | `MatchController`, `CombatSimulation`, reloj de 100 ms, BO5 y límite 40 s | Implementado |
| Banco/Brief | `pouch-state.cs`, `local-bank.cs` | 3 banco, 5 Whelps campo, 3 ofertas, 4 copias/tipo; compras repetidas son mejoras |
| Moneda de combate | `PouchState.Coins`, `OwnedUnit.Paid`, generación Blotan | Temporal; venta reintegra Paid completo |
| Movimiento al banco | `TrySetLocation` | Ya gratuito; sin reembolso, regreso gratuito. Se mantiene tras aclaración del creador |
| Filtro banco | `SuppressBenchOffers`, `IsBenchBlocked` | Devuelve ofertas no compradas al pool y oculta ese tipo mientras esté guardado |
| Identidad | `CharacterDefinition.stableId`, `OwnedUnit` por tipo | Los IDs de personajes son estables; no hay adquisición de múltiples instancias del mismo tipo |
| Personajes | 22 `Character.asset` inventariados | 5 Monsters, 17 Whelps; conservar también Suki, Rosie y Pokrak |
| Habilidades | Configuración por personaje, base + tres mejoras; Monster elige una | Implementado; no reemplazar por la tabla histórica |
| Tokoro/Toronko | ID `tokoro`, tercera `tokoro-sleep` | Todavía duerme al final; asset efectivo 5 s/25 HP, código por defecto 10 s/15 HP. Roadmap sustituye ambos por primeros 3 s/60 HP por segundo |
| Inicio de combate | `BeginDocumentedCombat` | Efectos iniciales existentes, orden canónico por ID; falta ventana general de 1,5 s |
| Final de ronda | `RoundPresentationSettings`, `local-round-result.cs` | Tarjetas y banderas; falta comparación animada pedida |
| Resultado BO5 | `local-round-result.cs` | Textos VICTORY!/DEFEAT; falta especificación nueva |
| Online | `OnlineRoom`, `online-match.cs` | Authentication anónima + Relay/NGO, validación/replay de comandos, checksums y barreras del host. No servidor económico confiable |
| Lobby | Paquete Multiplayer Services disponible | No se encontró uso de API Lobby en `OnlineRoom`; no afirmar que un lobby persistente está desplegado |
| Desconexión | `Disconnected -> Fail` | Termina sesión; no recuperación completa del estado de partida |
| Mazos | `DeckLibrary` | Tres mazos locales existentes; no borrar dos por el roadmap de un mazo inicial |
| Progresión | `PlayerProgress`, `local-progression` | XP/nivel local en PlayerPrefs; historial deduplicado limitado a 128 partidos |
| Monedas permanentes | Búsqueda de modelos/catálogos/runtime | No cartera de Earthcoins/Suncoins/Saturncoins ni concesión de bienvenida |
| Máquinas/Pouches/maestría | Búsqueda de modelos/catálogos/runtime | No implementación económica o catálogos de premios |
| Tutorial | Ayuda textual `LocalGameUI` | No tutorial práctico verificable ni premios persistentes |
| Guardado remoto | Paquetes y código | No Cloud Save/Cloud Code ni cuenta vinculada/recuperación |
| Monetización | Paquetes y runtime | Sin IAP/ads comerciales; mantener sin activar |
| Presentación | Sistemas de escenarios, animación y UI existentes | Reutilizables; defectos visuales reportados requieren reproducción/capturas |
| Arte máquinas | Carpeta original accesible | 17 máquinas, 17 Pouches, 4 monedas; sin asociación aprobada machineId↔characterId |

## Alias y contradicciones

Los nombres visibles actuales difieren del anexo: Atori→Atrik, Blotan→Blotun, Bugui→Bivui, Flo→Floru, etc. `INVENTORY.json` conserva ambos datos. **No se renombran**.
`tsu`/Tsumi y `suki`/Suki son **dos assets separados**, con habilidades diferentes; no fusionarlos como alias.
`anuik`/Aniku revive al 50 %. La duda inicial sobre el «Monster original» quedó resuelta después de la auditoría por MP-DEC-025: elegir Bugrum o Pombú (IDs `bugaloo`/`popow`); no asignar Aniku por esa referencia histórica. MP-DEC-030 exige usar los nombres actuales; lista completa verificada en CHARACTER_NAMES.md.
La escala IQ real es entera 1–10; el roadmap menciona otra interpretación. Se conserva el algoritmo actual.

El desempate inicial entregaba la mitad de la vida de aliados a un campeón; si no quedaba Monster, un Whelp campeón conservaba su vida completa. Las aclaraciones posteriores **MP-DEC-023/028** resuelven esa divergencia: el cálculo y la animación suman Monster vivo al 100 % y Whelps vivos al 50 % de HP actual redondeado hacia arriba por unidad, sin muertos ni invocaciones. Consultar `CHANGELOG.md` para los cambios posteriores a esta auditoría.

## Alcance de la auditoría

Auditoría estructural de código, configuración, dependencias, activos y riesgos. Las pruebas reales se registran en QA_CHECKLIST. Inventariar imágenes no implica haber revisado visualmente cada sprite. Ninguna partida con dos dispositivos, compra, servicio remoto o publicación se ha ejecutado en esta auditoría.

## Auditoría adicional de importación y bases

`GRAPHICS_IMPORT_AUDIT.json` registra 395 PNG: ninguno con mipmaps, 114 con filtrado distinto de Point o no declarado y 75 con compresión declarada. Estos valores señalan candidatos a revisar, **no prueban por sí solos un defecto**; algunos assets son ilustraciones, no pixel art. No se cambiaron masivamente importadores ni imágenes.

Existen pedestales configurados para `classic`, `royal` y `lounge`. El Brief ya los usaba; Home reutilizaba el pedestal general. Se conectó Home con el asset/paleta de su ambiente seleccionado, conservando la selección independiente del mapa de combate.

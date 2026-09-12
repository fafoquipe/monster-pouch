# Objetivos, despliegue y diseños del usuario

Revisión del 12 de septiembre de 2026. Las correcciones actuales del usuario prevalecen sobre el brief original y las entregas anteriores.

## Cambios incluidos

- Cada personaje conserva el objetivo desde el primer paso de persecución hasta derrotarlo, incluso si aparece otro enemigo más cercano, con menos vida o recién invocado. Recalcula el camino hacia el mismo rival; un bloqueo temporal no desvía su ataque. Un reinicio permite adquirir otro objetivo.
- Anuik es la excepción solicitada: al morir para resucitar libera a sus atacantes. Pueden fijar otro rival durante los 0,8 segundos de espera y conservarlo cuando regrese. Si es el único enemigo, esperan su regreso. Su propia elección de objetivo también se reinicia al volver.
- Despliegue de **cinco filas × seis columnas por bando**, 30 casillas. Blue Y5–9, Red Y0–4, tablero total 6×10. Compra, arrastre, resaltado y formaciones del bot usan las mismas máscaras.
- Stein usa el diseño crema con pelo blanco, fórmula facial, gran sonrisa y corazón del usuario; Kayon usa el diseño azul con marcas blancas, ojos centrales y bolsa dorada. Retratos nuevos, 75 poses de Stein y 80 de Kayon. Pivotes en los pies y tamaños anteriores conservados.
- Kayon lanza **monedas de oro con alcance 4**. Tiene cuatro cuadros de giro y cuatro de impacto. Conserva 28 de vida, 2 de daño, ataque cada 1,5 s, invocaciones cada 6 s y máximo dos Dummys temporales vivos.
- Los diseños aceptados de Anuik y Tauris, las reglas de venta y banco, el Brief y las condiciones de final de ronda se mantienen.

## Verificación

| Comprobación | Resultado | Registro |
| --- | --- | --- |
| EditMode | 297 aprobadas, 0 fallidas, 0 omitidas; final 2026-09-12 10:46:52Z | Logs/local-validation/edit-mode.xml |
| PlayMode | 13 aprobadas, 0 fallidas, 0 omitidas; final 2026-09-12 10:48:32Z | Logs/local-validation/play-mode.xml |

Los nuevos casos comprueban persecución en ambos bandos y alcances, enemigo más próximo, política de vida baja, invocación, rutas bloqueadas, muerte definitiva, reinicio y las dos situaciones de resurrección de Anuik. El cambio de máscaras tiene cobertura de compra, movimiento y bot; PlayMode arrastra por las 30 casillas, comprueba que ninguna interfaz las tape y rechaza la mitad enemiga sin cobrar. Kayon se prueba a cuatro casillas sin acercarse, con daño y vuelo retardados, invocaciones y sus imágenes reales de moneda e impacto en la interfaz.

## Archivos y reconstrucción

- Ejecutable final: Builds/Windows-Coins/Monster Pouch.exe. Conservar toda la carpeta junto al ejecutable.
- Diseño y prompts exactos, herramienta image_gen integrada: [user-whelps-designs.md](user-whelps-designs.md), [kayon-coins-prompt.md](kayon-coins-prompt.md).
- Fuentes y referencias: Assets/art/units/generated/stein-approved y kayon-approved; efectos: Assets/art/effects/kayon. Las fuentes se conservan y Unity crea derivados con alfa; la moneda ya tenía alfa real y se conservó.
- Recortes, pivotes y escalas: [user-whelps-import-report.md](user-whelps-import-report.md).
- Reglas actualizadas: [expanded-roster-combat.md](expanded-roster-combat.md); controles: [JUGAR.md](JUGAR.md).

Los importadores aditivos son MonsterPouchDeploymentSetup.Setup, MonsterPouchUserWhelpsArtSetup.Setup y MonsterPouchKayonCoinsSetup.Setup. Se ejecutaron sobre el Editor existente mediante la API de Unity; no se escribieron GUID ni YAML a mano. No se hizo commit ni publicación. Tres orientaciones opuestas de cada personaje se reflejan intencionalmente; las animaciones son dibujos generados a partir de las referencias del usuario.

## Compilación y apertura final

Windows x64: Succeeded, 0 errores, 312.139.619 bytes. Registro: Logs/local-validation/command-22aa662ff33140e0a982aaf9a336beeb.txt. Ejecutable creado y abierto: Builds/Windows-Coins/Monster Pouch.exe.

Se observó el menú del ejecutable final con los retratos de Stein y Kayon, sin fondos opacos y centrados en sus tarjetas. Captura: Logs/local-validation/screenshots/coins-menu-native.png. La colocación en las 30 casillas y el vuelo/impacto de monedas se verificaron en PlayMode sobre la escena real con EventSystem y presentación activos; no se afirma una revisión manual de cada fotograma del ejecutable.

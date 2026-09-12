# Arte de Monster Pouch

Estos recursos se crearon con la herramienta integrada `image_gen`, usando el arte aportado por el usuario como referencia. Las fuentes generadas se conservan intactas. Las salidas RGB con fondo croma se importan en Unity como derivados RGBA; los fondos de damero dibujados de los primeros intentos no se utilizan en el juego.

| Recurso | Archivo usado en el juego | Fuente preservada | Prompts y referencia |
| --- | --- | --- | --- |
| Maletín cerrado | `Assets/art/ui/brief/brief-closed.png` | `Assets/art/ui/brief/brief-closed-source.png` | [Prompts completos](brief-closed-prompts.md); referencia: `Assets/art/utilities/briefcase.png` |
| Dummy: 55 poses | `Assets/art/units/local/dummy/dummy-animation-v1.png` | `Assets/art/units/local/dummy/dummy-animation-source-v1.png` | [Prompts y decisiones](dummy-animation-art.md), [auditoría del importador](dummy-animation-import-report.md) |
| Atori: 60 poses | `Assets/art/units/generated/atori/atori-animation-rgba.png` | `Assets/art/units/generated/atori/atori-animation-source.png` | [Referencia MegaTrip](atori-megatrip.md), [prompts completos](atori-generation-prompts.txt) |
| Anuik aprobado: retrato, 60 poses + 25 de resurrección | `Assets/art/units/generated/anuik-approved/anuik-animation-rgba-v2.png`, `anuik-revive-rgba-v1.png` y `anuik-portrait-rgba-v1.png` | Fuentes hermanas y `anuik-user-reference.png` | [Imagen del usuario y prompts](anuik-user-design.md), [importación](anuik-user-import-report.md) |
| Tauris compacto: retrato y 75 poses | `Assets/art/units/generated/tauris-approved/tauris-animation-rgba-v3.png` y `tauris-portrait-rgba-v2.png` | Fuentes hermanas y referencia compacta del usuario | [Referencias y prompts finales](tauris-user-design.md), [importación](tauris-user-import-report.md) |
| Kayon del usuario: retrato y 80 poses | `Assets/art/units/generated/kayon-approved/kayon-animation-rgba-v1.png` y `kayon-portrait-rgba-v1.png` | Fuentes hermanas y `kayon-user-reference.png` | [Referencias y prompts](user-whelps-designs.md), [importación](user-whelps-import-report.md) |
| Stein del usuario: retrato y 75 poses | `Assets/art/units/generated/stein-approved/stein-animation-rgba-v1.png` y `stein-portrait-rgba-v1.png` | Fuentes hermanas y `stein-user-reference.png` | [Referencias y prompts](user-whelps-designs.md), [importación](user-whelps-import-report.md) |
| Bugui: 60 poses nuevas | `Assets/art/units/generated/bugui/bugui-animation-rgba-v2.png` | `bugui-animation-source-v2.png` | [Referencias](whelps-expanded-art.md), [prompts](whelps-generation-prompts.md) |
| Corazones, mordiscos, rayos y magia: 32 cuadros | `Assets/art/effects/roster/projectiles-rgba-v1.png` | `projectiles-source-v1.png` | [Prompt y uso](projectile-generation.md) |

La ampliación y corrección del 12 de septiembre incluyen 375 poses de personajes y 40 cuadros de efectos; Tauris usa su mordida como efecto de contacto cuerpo a cuerpo. Anuik usa dibujos auténticos apoyado sobre la cabeza durante la resurrección; la escala de esa hoja se calibra con su postura de pie. Los cuatro proyectiles tienen vuelo e impacto separados; el mordisco fatal tiene un impacto mayor. Las fuentes y derivadas permanecen en el proyecto, no dependen de las carpetas internas de generación.

Dummy y Atori tienen dibujos de reposo, caminar, anticipación/contacto/recuperación del ataque y caída. Hay cinco vistas dibujadas por personaje, con tres orientaciones reflejadas para completar ocho direcciones. Los sprites nuevos se recortan por los espacios transparentes reales de las hojas, con pivote en los pies. Las vistas traseras y las acciones de Atori son una interpretación nueva del personaje del álbum, no material extraído de otro videojuego.

El Brief conserva la animación de bisagra y la aparición de las figuras, pero al terminar el cierre muestra un maletín cerrado dibujado expresamente: tapa exterior de madera, herrajes dorados y una sola cerradura. La imagen abierta original se conserva.

La extracción croma únicamente crea transparencia en un archivo derivado. El importador conserva colores opacos, configura filtrado Point, evita compresión con pérdida y mantiene los identificadores de los sprites al repetir la importación. `MonsterPouchClosedBriefSetup.Setup`, `MonsterPouchDummyArtSetup.Setup` y `MonsterPouchAtoriSetup.Setup` conectan los recursos a la escena y los catálogos a través de las APIs de Unity.

Los originales de Bugaloo, Popow y Bugui se mantienen. Bugaloo y Popow conservan sus vistas aportadas y articulación; Bugui utiliza ahora sus nuevas secuencias dibujadas. Los efectos y estadísticas son balance propio provisional de Monster Pouch, descrito en la guía.

Las monedas de Kayon añaden cuatro cuadros de giro y cuatro de impacto dorado: Assets/art/effects/kayon/coins-rgba-v1.png, fuente hermana preservada. Prompt exacto en [kayon-coins-prompt.md](kayon-coins-prompt.md). MonsterPouchUserWhelpsArtSetup.Setup instala los diseños del usuario y MonsterPouchKayonCoinsSetup.Setup conecta el proyectil sin sustituir los otros efectos.

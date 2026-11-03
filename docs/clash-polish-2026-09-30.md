# Monster Pouch: movimiento, habilidades y lobby

Revisión del 30 de septiembre de 2026. Aplicada al proyecto Unity `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch`.

## Cambios en el juego

- El desplazamiento normal entre casillas usa anticipación, salto con arco y aterrizaje. Mantiene la pose direccional, con la sombra en el suelo. Los vuelos, tirones y desplazamientos especiales conservan sus animaciones propias.
- Los retratos de la cuadrícula de selección y del equipo permanecen estáticos. Solo la figura grande del lobby o de la ficha reproduce su gesto.
- Hay 76 ilustraciones independientes: habilidad base y tres mejoras para cada uno de los 19 personajes. Sus recortes e identificadores están en `AbilityArtwork.cs`.
- Las barras muestran vida segmentada, daño reciente y energía separada; la energía completa cambia a dorado. Las estrellas se sitúan debajo.
- La ficha durante una partida ocupa 508 × 382 unidades de un lienzo 540 × 960. Sus descripciones usan texto de 20, permiten desplazamiento y requieren pulsar MEJORAR para comprar. Se retiraron los rótulos PREPARACIÓN/COMBATE.
- Lato Bold para el cuerpo y Lilita One para títulos, con sus licencias OFL incluidas. Español predeterminado e inglés seleccionable en Ajustes. Las descripciones de Stein reflejan el nuevo súper en ambos idiomas.
- En la primera ronda se muestra el Monster rival en su posición predeterminada, inmóvil. Desde la segunda, los hologramas muestran el despliegue confirmado de la ronda anterior. Las compras y movimientos actuales del bot se revelan al iniciar el combate.

El burbujeo de Jazzar, las transiciones y los desempates del GDD v4 siguen descritos en [la revisión anterior](rediseno-2026-09-30.md#desempates-del-gdd-v4). El charco conserva sus tres segundos.

## Stein

El ataque básico alcanza a un solo enemigo. Al completar energía, lanza tres rayos contra enemigos distintos al alcance y los aturde durante dos segundos. El primer objetivo respeta la selección del combate; los restantes son distintos. Si hay menos objetivos disponibles, alcanza a los que existan.

1. Aturdimiento prolongado: +1,5 segundos, para un total de 3,5.
2. Cuarta descarga: el súper alcanza a cuatro objetivos.
3. Energía compartida: conserva la mejora existente de 25 de energía inicial para aliados adyacentes horizontales.

La configuración guardada conserva 25 de vida, 4 de daño y costes de mejoras 3/4/2. Para el nuevo súper se configuró un máximo de 100 de energía y 25 por ataque básico, sin regeneración pasiva ni energía por daño recibido. Estos dos valores son una decisión de implementación ajustable; el pedido no fijaba su cadencia.

## Gestos del lobby

| Personaje | Gesto |
| --- | --- |
| Anuik | Saca el corazón blanco del pecho, lo contempla y lo devuelve. |
| Bugaloo | Se rasca la barriga. |
| Popow | Practica golpes de boxeo. |
| Sepora | Juega con una chispa. |
| Tauris | Se limpia los dientes con un palillo. |
| Atori | Se lleva la mano a la frente y observa. |
| Blotan | Exhibe una moneda. |
| Bugui | Baila con sus propios brazos. |
| Flo | Sostiene y acerca una flor con sus dos brazos largos. Sin ojos ni boca añadidos. |
| Gochan | Toca su sombrero y muestra un talismán; conserva el sombrero rojo, dientes y cuerpo verde de referencia. |
| Jazzar | Infla y toca una burbuja tóxica. |
| Kayon | Saca un pequeño Dummy y lo devuelve a la bolsa. Mantiene ambas orejas junto a la cabeza y dos brazos cortos en el cuerpo. |
| Stein | Juega con electricidad estática en su pelo. |
| Trimol | Levanta una piedra. |
| Tsu | Infla un globo de chicle; expresión neutra, sin sonrisa. |
| Atong | Pule su superficie. |
| Tokoro | Saca un helado y lo lame. |
| Aky | Estira sus alas. |
| Hymay | Practica con su látigo. |

Cada gesto alterna reposo, acción y reposo. Los recortes tienen límites por fila y compensación de la posición de los pies para evitar fragmentos de filas vecinas y cambios bruscos de altura.

## Arte y procedencia

Se utilizó la herramienta integrada **image_gen**. Los originales del proyecto y las referencias del usuario guiaron la identidad de los personajes. Los archivos finales se copiaron al proyecto:

- `Assets/resources/MonsterPouch/UI/AbilityArt/abilities-01.png` a `abilities-05.png`.
- `Assets/resources/MonsterPouch/UI/LobbyArt/lobby-anuik.png`, `lobby-bugaloo.png`, `lobby-tokoro.png`, `lobby-flo.png`, `lobby-gochan.png`, `lobby-kayon.png` y `lobby-group-1.png` a `lobby-group-4.png`.
- Prompts y correspondencias: [habilidades](art-prompts/abilities-2026-09-30.json) y [gestos](art-prompts/lobby-2026-09-30.json).

Las filas de Flo/Gochan/Kayon presentes en los atlas compartidos no se consumen: el juego usa sus hojas individuales corregidas. Los PNG conservan el canal alfa generado; Unity recorta los fotogramas sin modificar esas imágenes.

La composición de las cartas se estudió mediante las referencias del usuario y material de [Clash Mini](https://www.youtube.com/watch?v=lyn3HBSEI-8), incluidos los conceptos de [Electro Wizard](https://clashmini.fandom.com/wiki/Electro_Wizard) y [Miner](https://clashmini.fandom.com/wiki/Miner). Las ilustraciones integradas son nuevas y pertenecen al catálogo de Monster Pouch.

## Validación

- Edit Mode: 567 pruebas aprobadas, sin fallos. Incluyen ataque básico de Stein a un objetivo, súper a tres/cuatro objetivos, duraciones del aturdimiento, salto sin ciclos de caminar y reglas del GDD.
- Play Mode: 33 pruebas aprobadas, sin fallos. Incluyen retratos de cuadrícula estáticos, formación inicial del rival inmutable, geometría visible de las barras, 76 ilustraciones diferentes y los 19 gestos en ambos idiomas.
- Revisión visual en Unity de las fichas de los 19 personajes, colección estática, formación predeterminada del rival, ficha compacta y barras. El informe de los textos no detectó rótulos recortados.
- Captura de combate real: tres rayos de Stein visibles y un salto entre casillas, en `clash-stein-super.png` y `clash-board-hop.png`. El recuento está en `clash-combat-preview.txt`.

Capturas del editor: `Logs/local-validation/screenshots/clash-*.png`. El informe de dimensiones de textos está en `Logs/local-validation/clash-preview.txt`. Las copias de seguridad y el registro de archivos aplicados se conservaron en la carpeta de trabajo de la revisión.

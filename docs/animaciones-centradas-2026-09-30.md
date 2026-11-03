# Personajes centrados, sombras y gestos espaciados

Revisión del lobby, Brief con pedestales y fichas grandes de los 19 personajes.

## Presentación

- Cada personaje tiene una nueva secuencia de 24 poses dibujadas. Los PNG se generaron con la herramienta integrada `image_gen`; las instrucciones y rutas originales están en `art-prompts/lobby-drawn-2026-09-30.json`.
- `LobbyFrameGraphic` muestra una pose completa por vez con el material normal de Unity UI. Se retiraron el shader y los campos de interpolación óptica que deformaban las figuras.
- Los puntos de apoyo se midieron en cada pose. Se usa la misma escala durante todo el gesto y el centro entre los pies permanece en el centro del pedestal, aunque una mano o un accesorio sobresalga.
- `LobbyContactShadow` dibuja una elipse suave bajo cada figura. La sombra permanece sobre la superficie del pedestal durante el gesto.
- Se corrigió la altura del héroe en el Brief y la posición de los pedestales inferiores.
- Se mantienen las orejas de Kayon, los dos brazos y el sombrero de Gochan, Flo sin ojos y Tsu sin sonrisa.

Los originales generados se copiaron sin modificar. `Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json` contiene las regiones, anclas y SHA-256 de los 19 atlas. El análisis de píxeles se utilizó únicamente para medir coordenadas; no se generaron imágenes con scripts.

## Ritmo

- Pose quieta entre gestos. Cada gesto dura aproximadamente 2,1–2,5 segundos, con unas 10–12 poses por segundo.
- Al entrar, el héroe espera entre 6 y 12 segundos y los demás entre 3 y 10.
- Después de actuar, el héroe espera entre 12 y 24 segundos y los demás entre 14 y 29. Se vuelve a elegir el tiempo al azar cada vez.
- Máximo dos personajes simultáneos, incluyendo al héroe. Los inicios se separan entre 1,8 y 3,8 segundos y el director elige al azar entre los disponibles.
- Abrir otra pantalla o cubrir el Brief con una ficha detiene sus gestos. La cuadrícula de selección permanece estática.
- La fuente aleatoria de presentación es independiente de la simulación del combate.

## Verificación

Se capturó la pantalla real del Brief durante 120 fotogramas en `Logs/local-validation/lobby-fluid-sequence/`. El informe `lobby-fluid-preview.txt` confirma ocho figuras con secuencias dibujadas. Las fichas se capturaron en `Logs/local-validation/screenshots/clash-gesture-*.png`; `clash-preview.txt` no detectó textos recortados.

Las pruebas de Play Mode verifican los 19 personajes, los descansos, el máximo compartido de dos gestos durante cuatro minutos simulados, las poses intermedias, los puntos de apoyo y sombras, la vuelta al reposo y la liberación de lugares al navegar. Resultado: `Logs/local-validation/play-mode.txt` y XML asociado. Revisión visual realizada en el editor de Unity para Windows.

# Referencia visual del Brief: Clash Mini

Esta nota registra la observación visual directa realizada durante la revisión del proyecto, usando el [vídeo oficial de Clash Mini](https://www.youtube.com/watch?v=8bjwf63B6rE). Separa lo visible en la referencia de las decisiones aplicadas al Brief original de Monster Pouch.

## Lo observado

| Instante | Evidencia visible | Aplicación en Monster Pouch |
| --- | --- | --- |
| [2:49](https://www.youtube.com/watch?v=8bjwf63B6rE&t=169s) | Tres figuritas colocadas en los tres compartimentos de la caja, con un coste pequeño y una ficha de moneda. | Las tres ofertas se presentan directamente en los huecos del Brief, con figurita y coste, en lugar de una fila inferior de tarjetas. |
| [2:54](https://www.youtube.com/watch?v=8bjwf63B6rE&t=174s) | Aparece «CLASH» y la caja todavía está visible. | Referencia del paso de preparación a combate; este fotograma no demuestra por sí solo una tapa totalmente cerrada. |
| [2:59](https://www.youtube.com/watch?v=8bjwf63B6rE&t=179s) | El combate ocupa el encuadre más cercano y la caja queda fuera de vista con el acercamiento. | Referencia del énfasis visual en el campo durante el combate. La desaparición del encuadre no permite deducir el ángulo exacto de la tapa. |

## Interpretación de esta implementación

Por la corrección solicitada por el usuario, Monster Pouch abre su Brief al comenzar la preparación, lo cierra para cambiar y revelar ofertas durante el reroll y lo cierra al entrar en combate. La animación usa dos recortes del arte original de la caja, una tapa giratoria y ocultación progresiva de las figuritas. **La duración de 0,32 segundos por cierre o apertura, el giro y la pose final son decisiones propias; no son mediciones ni una extracción exacta de la animación del vídeo.**

La referencia permite sostener la composición de tres figuritas con coste dentro de los compartimentos. No se observó con claridad suficiente el ángulo completo de la tapa para afirmar una reproducción exacta de su cierre. La caja visible durante «CLASH» y su salida posterior por el acercamiento se mantienen como observaciones separadas. [Transición visible desde 2:54](https://www.youtube.com/watch?v=8bjwf63B6rE&t=174s).

Los controles económicos de Monster Pouch son decisiones del proyecto: arrastrar una oferta al campo o al banco compra y coloca de forma atómica; tocar una copia propia compra un Trick sin mover su unidad; campo → Brief vende, y banco → Brief guarda solo si hay un hueco libre. Estas reglas no se atribuyen al vídeo.

Implementación: `Assets/scripts/local/local-game-ui.cs`, especialmente `RenderBrief`, `UpdateBriefMotion`, `ApplyBriefPose` y `CycleBrief`. Estado de pruebas y build: `docs/local-completion-progress.md`.

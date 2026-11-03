# Validación en Unity — 15 de octubre de 2026

## Arte e importación

- 19 idles distintos, transparentes y con filtro Point, conectados al lobby y a las tarjetas.
- Stein y Blotan: 8 direcciones y secuencias completas de idle, movimiento, salto, ataque, habilidad y derrota.
- 76 ilustraciones de habilidades distintas, exportadas en celdas nativas de 64×64 y verificadas con filtro Point. Se corrigió el importador que antes forzaba Bilinear.
- Shader de escenarios validado sin errores; alfombra del hotel con ajuste localizado a rojo vino moderado.
- Informes: `Logs/local-validation/lobby-polish-20261015/ability-import.txt` y `import.txt`.

## Pruebas focalizadas

- **74 aprobadas, 0 fallidas** en combate, reflejo de Bugaloo, calidad de sprites, saltos y tiempos de animación (`Logs/local-validation/focused-edit.xml`).
- Energía de Stein verificada con 25 y 40 puntos: la recibe una vez él mismo y sus aliados inmediatos de izquierda y derecha; no la reciben enemigos, otras filas ni aliados más lejanos. Sin la mejora no se concede energía.
- **11 aprobadas** en entrega de Moonkens, venta, brief y selección de escenarios (`Logs/local-validation/lunar-final-play.xml`). Se comprueba el ascenso visible, vuelo, pago único al llegar, pausa, cambio de ronda y cancelación al salir.
- Una comprobación adicional, `RedesignPlayTests.CompactSheetHasExplicitPurchaseAndAllAbilitiesHaveDedicatedArt`, falla antes de revisar el arte: exige una tarjeta de altura menor que 400, mientras la interfaz actual mide 476. Se conserva ese resultado; no se ha cambiado el tamaño de la tarjeta ni relajado la prueba. Las 76 referencias de arte se validaron por separado mediante `MonsterPouchLobbyPolish.ImportAbilityArtwork`.

## Revisión visual

Capturas reales del juego en `Logs/local-validation/lobby-polish-20261015/`: home y brief de los tres escenarios, las 19 tarjetas y la secuencia lunar de combate. Las capturas restauran el brief y la preferencia de lobby del usuario.

Los recursos se generaron con ImageGen integrado. `final-prompts.json` y `all-ability-prompts.json` contienen los prompts; `all-19-idles.png` y `all-76-abilities.png` muestran las exportaciones finales.

# Idles de pantallas — 17/10/2026

Se incorporaron poses de reposo propias de Suki, Rosie y Pokrak para Brief, colección y las miniaturas del equipo. Mantienen la escala de píxel de los otros Whelps: 64 píxeles de altura visible, margen transparente y filtro Point.

Los cinco Monsters (Anuik, Bugaloo, Popow, Sepora y Tauris) tienen ilustraciones con píxeles más finos exclusivamente en la pantalla principal con el botón BATALLA. Las cinco referencias nuevas del usuario se adaptaron a las identidades actuales: Anuik amarillo con corazón y alas blancas; Bugaloo azul y sonriente con luna; Popow boxeador rojo compacto; Sepora magenta con ojos amarillos; Tauris azul claro con brazos cruzados.

El Brief y las fichas de los Monsters conservan sus sprites anteriores. El arte de combate permanece separado. La pausa global de animaciones de lobby sigue vigente: este cambio añade dibujos de reposo sin reactivar animaciones.

## Archivos guardados

- [Whelps: sprites nativos](../../Assets/resources/MonsterPouch/UI/NewWhelpIdles/): `suki.png`, `rosie.png`, `pokrak.png`.
- [Monsters: arte exclusivo de BATALLA](../../Assets/resources/MonsterPouch/UI/BattleHomeIdles/): `anuik.png`, `bugaloo.png`, `popow.png`, `sepora.png`, `tauris.png` y `clips.json`.
- [Originales y referencias](../../ArtArchive/IdleArtwork-20261017/): fuentes de los tres Whelps y las cinco referencias proporcionadas por el usuario.
- [Prompts finales](prompts.json): generación con la herramienta integrada **ImageGen**, sin CLI.
- `Assets/Editor/MonsterPouchIdleArtwork.cs`: importación reproducible con `Apply`. Los instaladores de los tres Whelps preservan su idle de interfaz al volver a importar el combate.

`LobbyArtworkContext.BattleHome` se pasa explícitamente desde `RenderHomeRoom`. El gráfico usa un catálogo independiente, sin modificar la caché ni las referencias del Brief. Los idles detallados no activan secuencias antiguas de menor resolución.

## Revisión

Las capturas y resultados se guardan en `Logs/local-validation/idle-artwork-20261017/`. Se comprueban las cinco selecciones, la navegación BATALLA → Brief, la separación de sus texturas y los tres nuevos Whelps en Brief y fichas.

Resultado: **2/2 pruebas Play Mode aprobadas**, con las cinco selecciones y los tres Whelps recorridos. Revisión visual de las cinco pantallas principales y el Brief con los tres idles nuevos. Auditoría del manifiesto: se conservaron los 22 personajes y todas las secuencias existentes; únicamente cambiaron los cuatro campos de idle de los tres nuevos Whelps. El catálogo exclusivo de BATALLA contiene exactamente los cinco Monsters.

- [Ejemplo en BATALLA](../../Logs/local-validation/idle-artwork-20261017/home-bugaloo.png)
- [Los tres nuevos Whelps en Brief](../../Logs/local-validation/idle-artwork-20261017/new-whelps-brief.png)
- [Brief de Tauris con su arte anterior](../../Logs/local-validation/idle-artwork-20261017/brief-tauris.png)
- [Resultado de las pruebas](../../Logs/local-validation/idle-artwork-20261017/play.xml)
- [Origen y destino de cada imagen](outputs.json)


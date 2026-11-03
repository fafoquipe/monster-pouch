# Perspectiva y menú de partida — 8 de octubre de 2026

- Los números de daño muestran únicamente impactos del equipo local contra el rival. Se usa `Match.LocalSide` / `RivalSide`, incluyendo al invitado que controla Red.
- Las barras de vida son verdes para las tropas propias y rojas para el rival. El rojo usa RGB (1, 0.10, 0.12) y un brillo menos blanquecino.
- El botón de pausa tiene un icono dibujado con geometría, independiente de las fuentes, y queda sobre la capa de resultados para seguir recibiendo toques.
- El menú permite continuar, consultar ayuda, ajustar sonido y volver al menú principal. En online la simulación sigue funcionando; contra el bot se pausa. Ayuda y sonido regresan al menú de partida, que permanece abierto entre rondas.
- Salir usa el cierre existente de la sala y su transporte de red.

## Validación

Unity compiló sin errores. Seis pruebas PlayMode aprobadas:

- `OnlinePerspectivePlayTests.BothPerspectivesShowOwnDamageAndGreenAllies`: impactos reales de la simulación y barras renderizadas como anfitrión e invitado.
- `OnlinePerspectivePlayTests.OnlineOptionsStayReachableThroughCombatResultsAndExit`: raycasts de pantalla, ayuda/sonido, cambio de ronda y salida para ambos lados.
- `OnlinePerspectivePlayTests.BotMatchStillPausesAndResumes`.
- `October8NetworkPlayTests.LobbyWaitsForHostAndCancellingReadyDoesNotBreakRoundHandshake`: incluye salida con dos transportes Netcode locales y retorno de ambos jugadores al menú.
- Las dos pruebas de `ViewportLayoutPlayTests`.

Captura de la vista móvil de Unity: `Logs/local-validation/screenshots/online-pause-fixed.png` (393 × 873).

No se generaron nuevas builds. La APK y el EXE anteriores no contienen estas correcciones. Las pruebas de conexión se hicieron localmente entre dos clientes; queda la comprobación en dispositivos físicos.

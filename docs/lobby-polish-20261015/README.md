# Lobby, sprites and lunar effects

Final raster artwork was edited with the built-in `image_gen` tool, then exported with nearest-neighbor sampling and its original transparency. Final prompts are in `final-prompts.json`.

- `Assets/resources/MonsterPouch/UI/LobbyIdles/`: nineteen native pixel sprites used by the lobby and character cards. Tauris and Tokoro use the brighter blue palette.
- `Assets/resources/MonsterPouch/SteinScientist/stein-scientist.png`: five authored views, mirrored for eight game directions, with blue eyes, grounded movement, attack, special and defeat.
- `Assets/resources/MonsterPouch/BlotanMoonken/blotan-moonken.png`: Moonkens in hands, pouch and hat. No permanent star glint or glow in the idle/base frames. Income raises a separate Moonken above the head.
- `all-19-idles.png`: contact sheet of the final exported poses.
- `Assets/resources/MonsterPouch/UI/AbilityArt/abilities-06.png`: four native 64px pixel-art ability illustrations matching Stein's scientist design and blue eyes.
- `Assets/resources/MonsterPouch/UI/AbilityArt/abilities-01.png` through `abilities-06.png`: all 76 abilities now use native 64px pixel art. `all-76-abilities.png` shows the complete collection; `all-ability-prompts.json` records the final image prompts.

Stein's Energy Transfer upgrade now grants the configured energy amount to himself and his immediate left and right allies at round start. It still requires the upgrade, does not reach other rows or enemies, and uses the same editable amount for every recipient. Spanish and English descriptions include Stein himself.

Blotan's match-owned delivery clock drives both the visual and payment: rise and shine above the head, flight to the Moonken HUD, then one credit of the configured income amount. Pausing freezes delivery; round transitions preserve in-flight income; leaving the match cancels it. The HUD balance updates during combat.

Bugaloo launches a crescent moon for each reflected hit. Travel lasts 0.5–0.9 seconds depending on distance. Damage resolves at arrival on the simulation clock, uses the unreduced incoming amount and existing revenge multiplier, and never recursively reflects another reflected moon. Ending combat clears pending hits.

The Play Park lobby has stronger saturation and brightness. Campo Real flags use crimson cloth and gold embroidery. The Grand Cas Hotel brief has a casino-style header labeled Grand Cas HOTEL. Its lobby carpet uses a muted wine-red color correction, preserving the original weave, pattern and lighting.

Unity import and visual checks: `MonsterPouchLobbyPolish.Apply`, `Validate`, `Capture`. Reports and captures are written under `Logs/local-validation/lobby-polish-20261015/`. Backups are under `Backups/lobby-polish-*` and `Backups/lobby-polish-code-*`.

# Combat impact polish — 26 September 2026

## Changes

- Popow selects the farthest enemy, crosses its cell and lands beyond it. At a board boundary he uses a deterministic free side cell. Blocked and reserved cells are excluded. The visual lane follows the exact two-segment hit area; flames follow the dash. Punches use anticipation, a fast release and a short contact hold.
- Existing Popow stun durations and upgrade damage bonuses remain configurable. New `popow-dash.width` and `travelDuration` control area and travel time.
- Bugui's unlimited rhythm now defaults to +5% attack speed per attack, without a stack cap. Migration updates the former 2.5% default and preserves other custom values.
- Atong pulls his head 0.48 world units behind the planted body, holds the pose, then snaps it 0.36 units forward at impact. Side views swing from 55 degrees backward to 86 degrees into the slam. A strip of the original neck pixels keeps the head connected. Critical-hit feedback no longer replaces this animation. Contact-triggered ground cracks, dust and shockwave are enlarged and last 0.65 seconds.
- Flo reveals and lifts a flower before planting it. `flo-flower.castDuration` controls the delay (0.45 seconds rounds to 0.5 on the combat tick). Reset, death and stun interrupt preparation. The destination is checked again at planting time. Flo's world width increased from 0.94 to 1.20.
- Hymay uses a curved segmented whip with extension and recoil, tied to the impact event. His body anticipates and snaps with the whip.

## Reference

Wave Master / Tritón's farthest-enemy dash and path stun were checked against the [Clash Mini character reference](https://www.noff.gg/clash-mini/unit/wave-master). Popow uses those targeting/path principles with original flame effects and the project's existing balance values. [Ocellus's official artwork credits](https://ocellusart.artstation.com/projects/VJbLgP) were also reviewed; the embedded video could not be retrieved, so frame-by-frame animation copying is not claimed.

## Validation tools

`MonsterPouchImpactPolishMigration.Apply` updates retired stock defaults without resetting custom balance. `MonsterPouchDirectionalSetup.ApplyImpactPolish` applies clip tuning and Flo's size. `MonsterPouchImpactPreview.Preview` accepts `popow`, `punch`, `atong`, `flo`, or `hymay`; it creates runtime-only QA encounters and captures frames using each configured attack timing. `Restore` returns to the menu. No character sheet was regenerated.

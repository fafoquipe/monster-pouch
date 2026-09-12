# Unit presentation and source references

The existing four prototype images and their `.meta` files are retained. The final match presentation uses a separate local catalog configured through **Monster Pouch → Setup local unit art** (`MonsterPouchArtSetup.Setup()`). It is not an automatic scene initializer. Running it again preserves the sprite IDs by name and updates the same catalog; it never recreates the original prototype GUIDs.

The catalog is `Assets/Resources/MonsterPouch/UnitArt.asset`. `UnitArtCatalog.Get(id)` resolves the original `bugaloo`, `popow`, `dummy`, and `bugui` entries and any separately installed profiles such as `atori`. The new authored Dummy atlas is documented in [dummy-animation-art.md](dummy-animation-art.md); the source audit below also records the original fallback art. `WorldWidth` is the visible width of the south view in board world units; `ReferencePixelWidth` keeps perspective widths coherent rather than forcing every direction to the same width. The runtime uses the material `Assets/Resources/MonsterPouch/UnitSpriteMaterial.mat` so its unlit shader is included in the local build.

The runtime presentation now applies the constant `VisualScaleMultiplier = 1.2f` to the catalog-based character art and its complete body/feet rig: a **20% visual size increase** around the same foot anchor. The horizontal ground-shadow width uses the same multiplier. Actor positions, cell mapping, routes, statistics, economy, attack timing, animation state, and the original sprite assets are unchanged. The catalog reference dimensions remain the editable baseline.

## Reference choice and alpha audit

The reused sources are under the repository's historical parent folder, `mini-gogos/pictures/gogos-minis/`. The selected separate, named 256 × 256 directions preserve Popow's face/star/black gloves, Bugaloo's blue body and beach belly, and Bugui's eyebrows and opaque white belly. They are more suitable for directional presentation than the single front prototype image. This selection is based on the files' content, not their modification date or an assumption that a filename proves approval.

| Character | Reused source | Existing authored content |
| --- | --- | --- |
| Bugaloo | `bugaloo/bugaloo-animations/bugaloo-idle/bugaloo-{direction}.png` | Eight separate idle views, 256 × 256 |
| Popow | `popow/popow-animations/popow-idle/{direction}.png` | Eight separate idle views, 256 × 256 |
| Bugui | `bugui/bugui-animations/bugui-idle/bugui-{direction}.png` | Eight separate idle views, 256 × 256 |
| Dummy | `Assets/art/units/prototypes/dummy-whelp-prototype.png` | One white/gray silhouette, 1254 × 1254; no directional sheet found |
| Bugaloo attack | `bugaloo/bugaloo-atack.png` | Eight attack poses in one 1448 × 1086 directional atlas |
| Popow attack | `popow/popow-atack.png` | Eight attack poses in one 1448 × 1086 directional atlas |

A read-only image audit confirmed that the 24 named direction files contain true RGBA transparency with only alpha 0/255. Popow's opaque whites and black glove pixels remain intact. Both attack atlases are indexed PNGs with actual transparent palette entries; their green RGB background has alpha zero. Bugaloo's visible attack pixels are predominantly alpha 254, and Popow also contains many alpha-254 pixels. These are nearly opaque source pixels, not a painted checkerboard. The bytes are copied unchanged; there is no global color removal, conversion, image regeneration, or destructive compression.

The first setup copies only those 24 direction PNGs, two attack atlases, and the existing Dummy silhouette to `Assets/art/units/local/`. Once imported, this directory makes the project portable; the historical parent folder is no longer needed to play or build it.

## Directions, frames, and pivots

The arrays use the explicit clockwise order **N, NE, E, SE, S, SW, W, NW**. `UnitPresentation.Face(dx, dy)` accepts logical board deltas: positive X is east and positive Y is south. It preserves the previous facing for a zero vector. Attacks face the target in world coordinates. A move never iterates through the eight views as temporal frames.

The attack atlas is interpreted in image rows from top to bottom:

| Row | Column 0 | Column 1 | Column 2 | Column 3 |
| --- | --- | --- | --- | --- |
| Top | S | N | E | W |
| Bottom | SW | SE | NW | NE |

Each cell is 362 × 543. Unity's bottom-left texture origin is accounted for during slicing. No attack pose is horizontally flipped; Popow uses each supplied view so its authored attacking hand is preserved.

All slicing uses Unity's `ISpriteEditorDataProvider` and `ISpriteNameFileIdDataProvider`. Originals and original subassets are untouched. The new sprites use Point filtering, no mipmaps, no lossy compression, a full rectangular mesh, and no generated physics shape. Idle views use 150 pixels per unit; Popow attack uses 260, Bugaloo attack 336, and the larger Dummy source 580, keeping their visible sizes comparable.

The full idle rectangle preserves the source canvas and margins. The foot pivot is the horizontal center of the opaque footprint's lower 14% and one pixel above the bottom of the opaque bounds; only alpha greater than 128 participates in this measurement so near-invisible prototype border noise cannot move the pivot. Atlas pivots are measured separately within each cell. A successful setup writes **`docs/unit-art-import-report.md`** with the actual imported path, sprite name, direction, state, pixel rectangle, pixel pivot, and PPU for every catalog entry.

Move uses three Editor-created subasset regions (body, left foot, right foot) from the same unmodified PNG. Their common origin is the full pose's foot pivot, with explicit per-region offsets. A one-pixel overlap hides tiny articulation seams. The feet advance alternately, with a small upper-body counter-motion. Ground shadows are separate procedural discs; the supplied directional references do not contain a large ground shadow.

## Runtime states and provisional work

Presentation is independent of board occupancy and damage. State priority is **Death → Attack → Move → Idle**. `Time.deltaTime` drives only display so setting the match time scale to zero pauses it together with the simulation.

- **Idle:** subtle breathing around the fixed foot pivot.
- **Move:** alternating articulated feet, a small body counter-motion and a short vertical step; the root interpolates between the supplied cell anchors.
- **Popow Attack:** anticipation, supplied directional right-hand contact pose, local reach, and recovery. No reflection is used.
- **Bugaloo Attack:** anticipation, raised-arm directional pose, visual elevation, belly-first landing squash, and recovery. The separate shadow stays on the ground and the visual root never changes the logical cell.
- **Dummy:** the separately installed authored atlas supplies real Idle/Move/Attack/Death drawings, with contact aligned to the simulation delay. Five source views and three intentional symmetric mirrors replace the old projected silhouette.
- **Bugui Attack:** a local body/feet rig anticipation, reach or ranged release, and recovery pending an authored temporal sheet.
- **Death:** unambiguous collapse followed by opacity fading and shadow cleanup. There is no red damage tint or disruptive Hit state. The match controller remains responsible for death, reservations, and final object disposal.

The original Bugaloo/Popow/Bugui profiles use **provisional articulated animations** around supplied poses. The Bugaloo attack views do not include a detailed soles-exposed airborne pose. The original Dummy fallback had no rear or side artwork; the user subsequently requested a complete animated design, now installed separately through `MonsterPouchDummyArtSetup.Setup()`. That generated atlas preserves the white faceless identity and supplies five actual source views with three symmetric mirrors. It must not be reported as eight independently drawn views. The built-in image generation prompts, source copies, RGB-to-RGBA technical import and exact frame mapping are recorded in [dummy-animation-art.md](dummy-animation-art.md).

Runtime integration: call `Configure(unit, mapper, art)` on a unit's scale-one root, then `Move(from, to, duration)`, `Attack(targetWorldPosition, isProjectile)`, or `Die()` from the relevant simulation events. `VisualRoot`, `Renderer`, `Facing`, and animation status properties are exposed for UI inspection and verification. Bars and Tricks stars belong to the match UI. `Die()` is idempotent, and animations never apply damage themselves.

The synchronized overload is `Attack(Vector3 targetWorldPosition, bool isProjectile, float impactDelay)`. Pass `CombatSimulation.GetImpactDelay(actor, target)`. A melee strike reaches its contact pose exactly at that delay; a ranged pose releases at `CombatSimulation.GetAttackWindup(actor)` and recovers independently of the star's flight. The projectile visual must wait for that windup and then travel for `CombatSimulation.GetProjectileTravelTime(actor, target)`. All helpers use the simulation's 0.1 second quantization, including editable windups such as 0.17 seconds. Recovery fits the configured attack interval instead of stretching a fixed .58 second animation across faster upgrades.

At round end, `Freeze()` (or `SetFrozen(true)`) cancels surviving actors' attack/move presentation, settles them on their current logical cell, and stops their display clock. Already-started Death animations are allowed to finish; `SetFrozen(false)` does not resume a stale attack. The old Dummy fallback retains its directional body/feet lean only when the optional authored atlas has not been installed. Authored clips use their own drawings without that projected width or additional rig distortion.

`UnitPresentationTimingTests` checks contact and recovery at upgraded cadence, release versus flight time, Bugaloo's logical-root stability, and Death completion after the round freeze. `UnitAuthoredAnimationTests` checks the separate drawing changes, contact-frame alignment, explicit mirrors, fixed foot origin and authored death playback. These are automated timeline/invariant checks; they do not replace the pending rendered inspection of every direction and pose.

## Scene measurements from the audit

The existing scene mapper uses `table-surface` at world (-0.98, 1.3350009), cell size (1.3482901, -1.1645357), and offset (-3.3851199, 5.1307142). Cell (0,0) is (-4.3651199, 6.4657151); cell (5,9) is (2.3763306, -4.0151062). This confirms the visual Y inversion. The whole logical grid spans approximately X [-5.0393, 3.0505], Y [-4.5974, 7.0480], including half-cell margins. Use `BoardWorldMapper` for runtime mapping rather than duplicating these offsets.

The existing Brief lives under the gameplay/briefs hierarchy, independently of `grand-cas-hotel`; retain that relationship. The three existing Brief slot transforms have no interactive UI. The old `SafeArea` component applies once in `Awake` and requires a Canvas; responsive UI must also handle window/safe-area changes. Diagnostic cell markers are already serialized with `showMarkers = 0`.

This document describes inspected sources and the implementation. The generated import report, Unity compilation, actual PlayMode/build verification, and screenshots provide separate evidence; source inspection alone is not proof those checks passed.

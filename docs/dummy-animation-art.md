# Dummy authored animation art

The user requested complete original animations for the white Dummy in the game's pixel-art style. The original white prototype remains unchanged at `Assets/art/units/prototypes/dummy-whelp-prototype.png`, together with its existing GUID and the older local reference. Dummy keeps its large round white head, short body/limbs and faceless identity.

## Final assets and provenance

- Tool: built-in `image_gen.imagegen` via Codex, not the CLI/API fallback. No Python bitmap editing was used.
- Selected generated source: `C:\Users\HP VICTUS\.codex\generated_images\01a09256-d6d2-7dc1-a3ff-c6c0faa4c6f6\exec-c39a79bd-7a68-4f99-8948-8d01ce172bba.png`.
- Project source copy: `Assets/art/units/local/dummy/dummy-animation-source-v1.png` — 1774 × 887, RGB, magenta export matte. Source bytes are preserved.
- Runtime derivative: `Assets/art/units/local/dummy/dummy-animation-v1.png` — real RGBA, created by Unity's explicit Editor setup.
- Catalog: existing `Assets/Resources/MonsterPouch/UnitArt.asset`; only the Dummy profile is retargeted by `MonsterPouchDummyArtSetup.Setup()`.
- Import setup: `Assets/Editor/MonsterPouchDummyArtSetup.cs`.
- Shared technical importer: `Assets/Editor/MonsterPouchSpriteAnimationImporter.cs`.
- Runtime: `Assets/scripts/gameplay/presentation/unit-presentation.cs`, optional clips in `UnitArtCatalog.cs`.

The first atlas and the attempted background extraction both exported an RGB checkerboard; neither is integrated. Their original outputs remain under the generation directory as `exec-56fcaabf-316e-4d5e-ac6e-efe413078a5a.png` and `exec-1fb589bf-4590-42ae-921b-e5abb8f3f675.png`. The third tool call requested a deliberate solid magenta matte, which permits a technical, deterministic alpha conversion without redrawing the character.

An actual read-only audit of the imported runtime derivative confirmed RGBA, 1774 × 887, **1,136,138 fully transparent pixels and 437,400 fully opaque pixels**. There are no partially transparent pixels. The generator did not produce this alpha directly; the Unity Editor conversion did.

## Pose layout and playback

The generated atlas contains **eleven columns and five rows**, not the ten columns requested in the first prompt. The extra relaxed pose is deliberately used as the start of Death, so every column has a role. Rows are ordered from the top: **S, SE, E, NE, N**. For the symmetrical, faceless Dummy only, SW/W/NW mirror the corresponding SE/E/NE row. These are five source views, not eight independently drawn views.

| State | Source columns, zero based | Playback |
| --- | --- | --- |
| Idle | 0, 1 | Two poses, 2.5 frames/second |
| Move | 2, 3, 4, 3 | Two opposite steps with a passing pose between them, 10 frames/second |
| Attack | 5, 6, 7 | Anticipation, contact, recovery; contact frame index 1 |
| Death | 8, 9, 10 | Standing release, knees buckle, lying on side |

The limbs, torso posture and fallen silhouette are drawn in the frames. The new profile disables the old Dummy width projection and cut-up body/foot rig. It does not rotate a standing sprite to simulate the new fallen pose. Other characters retain their existing fallback presentation unless they install their own optional clips.

The common data contract is `UnitArt.DirectionalAnimations[8]` in order **N, NE, E, SE, S, SW, W, NW**. Each `UnitDirectionalAnimation` contains `Sprite[] Idle, Move, Attack, Death`, `IdleFramesPerSecond`, `MoveFramesPerSecond`, `AttackContactFrame` and `FlipX`. Empty clips retain the legacy rig behavior.

Attack frames respect the existing simulation delay. Pre-contact drawings occupy the windup; the contact drawing begins at the same instant as melee impact or ranged release; recovery fits the configured attack interval. Death remains .62 seconds and finishes even after round-end Freeze. Actor position, cell occupancy, combat data and all damage remain outside the animation system.

The existing 1.2× visual multiplier still applies around the fixed foot anchor. The new atlas uses 128 PPU and its actual front-view reference width for scale. The separate shadow remains centered on the logical footprint.

## Technical import

Call `MonsterPouchDummyArtSetup.Setup()` after `MonsterPouchArtSetup.Setup()` has created the base catalog. Repeating either setup preserves the already installed Dummy profile and additional catalog entries, as well as the catalog/material GUIDs.

`PrepareChromaSource(source, derivative, new Color32(255, 0, 255, 255), 48)` sets the selected export matte's alpha to zero on a separate RGBA32 texture; it retains the opaque white character, black outline, original source file and source RGB bytes. Matte/fringe matching uses chroma differences, not brightness, so the white head is not removed.

`ImportGrid(path, ppu, columns, rows, prefix)` returns `Sprite[row,column]`. It measures projected alpha bands to locate the real transparent gutters, because image generators do not reliably produce equal outer margins. The midpoint of each empty gutter becomes the boundary when the band count matches. The initial equal-grid measurement would have clipped 3–5 pixels from the left of the final fallen poses; gutter slicing fixes that without changing the bitmap. Low-coverage specks are ignored only while finding bands, not erased from the image.

Each sprite rectangle uses visible alpha bounds with a two-pixel margin. The pivot uses the midpoint of the lower 14% of the opaque footprint and one pixel above its bottom. A UI portrait reuses the tightly trimmed front idle subasset. No hand-written YAML or .meta files are used. Unity's Sprite Editor data provider preserves existing subasset IDs by name. Point filtering, uncompressed RGBA, no mipmaps, full rectangular meshes and no generated physics outlines are configured explicitly.

The setup writes `docs/dummy-animation-import-report.md` with actual imported rectangles, pivots, PPU, row/facing mappings and a fresh alpha audit. This document records design and source inspection; actual Unity compilation, tests and rendered checks are reported separately by the main task.

## Validation

`UnitAuthoredAnimationTests` covers drawing changes during idle/walk, unchanged visual foot origin, explicit mirrors, contact-frame timing and recovery, drawn crouch/fallen states, death completion after Freeze and settled idle during a round pause. Existing Popow/Bugaloo timing tests continue to cover the legacy fallback. These tests are timeline/invariant checks, not a substitute for in-game visual review.

## Exact prompts

The first call referenced the original Dummy prototype for identity and `Assets/art/units/local/popow/popow-south.png` only for the established pixel-art technique. The second and third calls each referenced the first generated atlas.

### Initial authored pose generation

```text
Use case: stylized-concept.
Asset type: production 2D game sprite atlas on actual transparent alpha, designed pixel by pixel.
Input image 1 is the character identity reference: Dummy, a completely faceless white toy with an oversized round ball head, tiny neck, short stocky torso, short mitten arms, and two stubby legs. Preserve this identity. Input image 2 is ONLY the pixel-art technique/outline/shading reference; do not borrow its red color, star, face, teeth, or boxing gloves.
Create a polished complete animation atlas for that WHITE FACELESS Dummy. Same compact 16-bit pixel art as the red character: dark 1–2 pixel contour, restrained white plus light/mid cool-gray clusters for volume, solid opaque character, crisp stair-stepped pixel edges, no antialiasing, no smooth 3D render. Think each figure originally hand-drawn on a 48x56 pixel grid then uniformly enlarged.
Atlas layout is EXACTLY TEN equal-width columns and FIVE equal-height rows on a wide 2:1 canvas, preferably 2560x1280. Fifty separate figures total. No grid drawn. Each cell contains exactly one complete pose, with broad transparent gutters. All standing figures have the SAME head size and body proportions, feet anchored near the same cell baseline, image centered with room for arms. The ten temporal poses from LEFT to RIGHT are:
1 idle relaxed arms half lowered;
2 idle inhale / small shoulder rise;
3 walk left leg forward / opposite arm swing;
4 walk passing pose, knee visibly lifted;
5 walk right leg forward / opposite arm swing;
6 attack windup, knees bend, torso turns, one plain mitten hand pulled back;
7 melee impact, body leans and hand thrusts forward in a clear punch, both feet planted;
8 attack recoil back toward neutral;
9 defeated crouch, knees buckle, head droops, arms drop;
10 defeated lying on side on the ground, round head and body intact, no injury.
These must be DISTINCT drawn poses with limbs flexing naturally, not copies translated or rotated as a rigid whole.
View directions by row, TOP to BOTTOM:
row 1 facing SOUTH, toward viewer;
row 2 facing SOUTH-EAST, toward lower right;
row 3 facing EAST, right-facing profile;
row 4 facing NORTH-EAST, toward upper right;
row 5 facing NORTH, rear view away from viewer.
Character has NO FACE in EVERY view: no eyes, no mouth, no nose, no ears, no hair, no marks, no clothes, no accessories. Rear views still show head/torso/arms spatially correctly through contour and restrained gray shading.
Use the same slightly elevated top-down battle-board camera for all cells; feet and limb overlap must reflect each angle. The far arm/leg may be shaded cool gray. Death poses occupy their own cell near the same ground baseline. No motion streaks, no impact effects, no shadows below feet, no scenery.
Background must be truly transparent alpha=0, not white and not black. Never draw a checkerboard, gray squares, or transparency preview. White head/body must remain fully opaque. No captions, numbers, labels, text, logos, frames, or watermark. Keep every figure whole inside its cell, no overlap between cells.
```

### Rejected transparency correction

```text
Use case: background-extraction. Edit this sprite atlas only to replace the entire gray checkerboard backdrop with true transparent alpha. This is a production PNG asset for Unity, not a preview. Preserve every white faceless character, black contour, gray shading, pose, placement, scale, and all five rows unchanged. The white heads and bodies must remain opaque. Remove ALL checkerboard pixels, including between arms and torso, between feet, inside curved arm gaps, and between figures. The output PNG must have actual RGBA transparent empty pixels; do not draw new squares, a white background, black background, floor, shadow, grid, text, or labels. Do not redraw or rearrange the sprites. Keep the same wide canvas.
```

### Selected chroma-key export

```text
Use case: precise-object-edit. Production sprite atlas cleanup. Keep every white faceless character and the complete layout of this image exactly as-is. Keep their crisp pixel silhouettes and black outlines. Replace ONLY the entire gray checkerboard background with one uniform opaque pure saturated magenta color, exact RGB #FF00FF. Magenta is an intentional chroma-key export matte. Every empty area, including narrow gaps between feet and arms, must be the exact same flat #FF00FF. NO checkerboard whatsoever. NO shadows, texture, noise, gradients, grid, labels, or new elements in the magenta. This is not a transparency request: output one perfectly solid magenta backdrop behind all 55 character poses. Preserve all eleven columns and five rows of characters. White character interiors must remain white and fully opaque. Do not put magenta inside a character. Sharp pixel art only, no antialias blur.
```


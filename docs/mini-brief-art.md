# Mini Brief: asset and generation record

The bench uses one separate open case with exactly one empty burgundy compartment. It was generated with the built-in `image_gen.imagegen` tool, using the first usable generation. No CLI/API fallback, bitmap editing, background removal, or re-encoding was used.

- Project asset: `Assets/art/ui/bench/mini-brief.png`.
- Absolute asset path: `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch\Assets\art\ui\bench\mini-brief.png`.
- Original generated file: `C:\Users\HP VICTUS\.codex\generated_images\01a09256-d6d2-7dc1-a3ff-c6c0faa4c6f6\exec-e88eb4d4-d98d-4a95-8862-fe15f69708ab.png`.
- The existing `Assets/art/utilities/briefcase.png` was inspected as the style reference and remains unchanged. The generated source was also retained.
- Source and project copy have identical SHA-256: `AC5040CE68E334C2A904D1F26E014A2D9AD0D84F36603EF39E4971801154A033`.

## Read-only image audit and UI alignment

The output is a 1374 × 1145 PNG in **RGBA** mode. Its alpha range is 0–255; 508,375 of 1,573,230 pixels have alpha zero, and all four corners are transparent. Most visible pixels have alpha 253, so the body is nearly opaque. Visual inspection confirmed one compartment and no painted checkerboard. The visible bounds at alpha greater than 128 are `(54, 99, 1320, 1063)`, expressed as left, top, right, bottom pixels.

Coordinates below are normalized to the full image, with origin at the **top left**:

| Placement | Value |
| --- | --- |
| Safe interior rectangle: x, y, width, height | `(0.24, 0.39, 0.52, 0.25)` |
| Interior center | `(0.50, 0.515)` |
| Suggested unit foot position on the compartment floor | `(0.50, 0.603)` |

If the sprite is cropped to the visible bounds above, the corresponding safe rectangle is approximately `(0.218, 0.361, 0.564, 0.297)` and the foot position is `(0.50, 0.613)`. These are visual placement guides; Unity import and UI integration are separate from the generation audit.

## Exact final prompt

```text
Create ONE standalone 2D game sprite: a miniature open wooden carrying case with EXACTLY ONE centered empty burgundy velvet compartment. This is the bench slot beside the existing Monster Pouch Brief. Use a stout, squat, near-square silhouette, approximately 150:125 width-to-height, viewed from the front in a shallow top-down 3/4 perspective, left-right symmetric, no isometric sideways rotation. There is one narrow open lid across the back/top, a dark walnut/chestnut wood body with horizontal plank shading, chunky warm gold/brass corner protectors, small gold hinges at the back, and a centered front gold clasp. The SINGLE burgundy velvet basin is deep and unobstructed, with a dark maroon back wall and rich crimson floor, occupying the middle 55% of the sprite. NO dividers, NO extra slots, NO tiny secondary wells. Match a classic richly colored pixel-art wooden briefcase: darkest brown outlines, medium burnt-umber wood, warm amber shadows, yellow-gold highlights, dark wine-red velvet. TRUE PIXEL ART with a consistent approximately 96 by 80 logical pixel grid uniformly enlarged: crisp chunky square pixels, hard stepped contour edges, carefully clustered limited palette, no antialiasing, no smooth gradients, no blur, no glossy 3D rendering. Center the prop and fill about 90% of the canvas width with modest clear margin around every edge. Aim for a 1152x960 output canvas. Background: actual transparent alpha outside the case if supported; absolutely NEVER draw a checkerboard/transparency grid. If actual alpha cannot be produced, use a perfectly flat solid dark forest-green #092f22 surround instead, with no texture or squares. No cast shadow outside the case. No words, labels, numbers, watermark, characters, coins, tools, hands, interface, or other objects. Deliver a single finished sprite, not a sprite sheet.
```

The prompt's requested canvas and nominal pixel grid are generation instructions, not measured output properties. The actual dimensions and alpha findings are reported above.

# Runtime texture edge audit

Audit performed against `C:/Users/HP VICTUS/Juegos Unity/mini-gogos/game/monster-pouch`. The auditor inspected project files without editing them; root subsequently integrated the staged importer and checks. Final integrated Unity results are recorded at the end. Source and runtime PNG bytes remain unchanged.

## Scope and interpretation

80 PNGs were decoded: transitive YAML dependencies of `Assets/Scenes/main-scene.unity`, `Assets/Resources/MonsterPouch/UnitArt.asset`, and `ProjectileArt.asset`, plus the dynamically loaded home/deck background and five MenuItems. Referenced prototype/fallback art is included; unused source images are excluded from the runtime counts. Sources were decoded separately only to establish provenance. Full paths and all results are in `texture-edge-runtime-paths.json` and `texture-edge-runtime-results.json`; `Run-TextureEdgeAudit.ps1` reproduces the read-only scan.

Coordinates in this report use the PNG top-left origin. `Exterior` means a visible pixel adjacent in the eight-neighbour grid to alpha-zero background connected to the image perimeter. The colour candidate rule is `min(R,B) - G > 24` and `abs(R-B) <= 90`. These are **candidates**, not instructions to erase all purple.

The mixture check searches a four-pixel neighbourhood for an opaque, non-magenta interior reference (one pixel away from transparency), then fits `candidate = (1-t) * reference + t * (255,0,255)`, with `0.03 <= t <= 0.98`. `Fit` means RGB RMS residual <=12; `Strict` means <=6. The independent scan does not apply the importer's additional protection for purple regions. Consequently its candidate counts are deliberately larger than the safe changes.

## Confirmed colour-key derivatives

All eleven sources below are completely opaque and contain more than 700,000 magenta-key pixels. Existing editor setup code explicitly maps each source to its runtime derivative. The runtime PNGs already have binary alpha but retain some opaque colour-key mixtures around their silhouettes.

| Runtime PNG | Exterior | Fit | Strict |
|---|---:|---:|---:|
| `Assets/art/ui/brief/brief-closed.png` | 1282 | 1240 | 213 |
| `Assets/art/units/generated/anuik-approved/anuik-animation-rgba-v2.png` | 5396 | 3220 | 1472 |
| `Assets/art/units/generated/anuik-approved/anuik-revive-rgba-v1.png` | 1169 | 1048 | 693 |
| `Assets/art/units/generated/atori/atori-animation-rgba.png` | 8300 | 5272 | 1390 |
| `Assets/art/units/generated/bugui/bugui-animation-rgba-v2.png` | 29833 | 1754 | 713 |
| `Assets/art/units/generated/kayon-approved/kayon-animation-rgba-v1.png` | 31727 | 29452 | 23055 |
| `Assets/art/units/generated/kayon-approved/kayon-portrait-rgba-v1.png` | 3239 | 3156 | 2711 |
| `Assets/art/units/generated/stein-approved/stein-animation-rgba-v1.png` | 12564 | 8467 | 3566 |
| `Assets/art/units/generated/stein-approved/stein-portrait-rgba-v1.png` | 1 | 1 | 0 |
| `Assets/art/units/generated/tauris-approved/tauris-animation-rgba-v3.png` | 3809 | 899 | 263 |
| `Assets/art/units/local/dummy/dummy-animation-v1.png` | 4723 | 4466 | 1501 |

The source is in the same directory, using these exact filenames:

| Derivative basename | Source basename |
|---|---|
| `brief-closed.png` | `brief-closed-source.png` |
| `anuik-animation-rgba-v2.png` | `anuik-animation-source-v2.png` |
| `anuik-revive-rgba-v1.png` | `anuik-revive-source-v1.png` |
| `atori-animation-rgba.png` | `atori-animation-source.png` |
| `bugui-animation-rgba-v2.png` | `bugui-animation-source-v2.png` |
| `kayon-animation-rgba-v1.png` | `kayon-animation-source-v1.png` |
| `kayon-portrait-rgba-v1.png` | `kayon-portrait-source-v1.png` |
| `stein-animation-rgba-v1.png` | `stein-animation-source-v1.png` |
| `stein-portrait-rgba-v1.png` | `stein-portrait-source-v1.png` |
| `tauris-animation-rgba-v3.png` | `tauris-animation-source-v3.png` |
| `dummy-animation-v1.png` | `dummy-animation-source-v1.png` |

Representative edge evidence, all from visible pixels:

| Asset shorthand | Pixel / RGB | Interior reference RGB | Key proportion / RMS |
|---|---|---|---|
| Brief | (1081,406) / (194,6,181) | (9,7,1) | 0.73 / 5.02 |
| Anuik animation | (1605,34) / (243,49,213) | (251,182,86) | 0.74 / 6.50 |
| Anuik revive | (673,74) / (240,92,227) | (241,227,209) | 0.58 / 7.50 |
| Atori | (385,111) / (224,15,206) | (104,22,5) | 0.80 / 6.16 |
| Bugui | (682,27) / (189,12,175) | (29,0,16) | 0.69 / 8.02 |
| Kayon animation | (1298,18) / (155,4,203) | (3,11,126) | 0.60 / 0.47 |
| Kayon portrait | (525,144) / (149,26,205) | (20,45,156) | 0.54 / 4.16 |
| Stein animation | (1073,13) / (151,12,123) | (23,18,23) | 0.49 / 11.55 |
| Stein portrait | (1128,576) / (239,50,252) | (252,247,252) | 0.80 / 8.99 |
| Tauris | (326,26) / (79,51,147) | (8,65,114) | 0.27 / 4.35 |
| Dummy | (551,27) / (87,4,71) | (0,0,0) | 0.31 / 6.93 |

The brief has 1,074,413 alpha-zero pixels, no partial-alpha pixels and 498,103 opaque pixels. Its visible matte residue is already in the PNG: compression or sampling cannot explain it. `MonsterPouchSpriteAnimationImporter.PrepareChromaSource` currently retains key mixtures when `abs(R-B)>12`; the first brief example differs by 13. The main scene binds GUID `820ad6957f9355e4bb855a10bf8c6da4`, sprite fileID `64799808`, to `LocalGameUI.ClosedBriefArt`, and its Image uses white tint and the normal UI material.

## Purple which must be preserved

| Runtime asset | Exterior candidates / fit | Reason to exclude or protect |
|---|---:|---|
| `Assets/art/effects/roster/projectiles-rgba-v1.png` | 1796 / 0 | Original-alpha source and derivative are byte-identical; intentionally violet projectile art. Excluded from cleanup. |
| `Assets/art/effects/kayon/coins-rgba-v1.png` | 4 / 0 | Original alpha; source and derivative identical. Excluded. |
| `Assets/art/units/generated/anuik-approved/anuik-portrait-rgba-v1.png` | 36 / 5 | Original alpha; source and derivative identical. A few fit results alone do not prove colour-key provenance. Excluded. |
| `Assets/art/units/generated/bugui/bugui-animation-rgba-v2.png` | 29833 / 1754 | Mixed case: genuine key residue plus authored rose/purple body and violet-blue orb. Cleanup is restricted to exterior mixtures without deeper purple support. |
| `Assets/art/units/directional-v1/sepora-atlas.png` | 8636 / 234 | Original-alpha magenta bat character and purple sparks; no magenta-key source processing. Excluded. |
| `Assets/art/units/directional-v1/tsu-atlas.png` | 6743 / 445 | Purple character, original alpha. Excluded. |
| `Assets/art/units/directional-v1/trimol-atlas.png` | 1890 / 313 | Original-alpha artwork, no key provenance. Excluded. |
| `Assets/art/units/directional-v1/bugui-special.png` | 508 / 18 | Authored violet artwork, no key provenance. Excluded. |
| `Assets/art/units/user-sept14/tsu/tsu-portrait-rgba.png` | 2312 / 106 | Authored purple portrait, no key provenance. Excluded. |

Bugui's intent is explicit in `docs/whelps-generation-prompts.md`: rose body palette `#f6a7ce/#d65b9a` and a violet-blue energy orb in attack windup column 7. Original directional sprite intent is documented in `Assets/art/units/directional-v1/prompts.json`. Smaller purple counts in other original-alpha UI/portraits are likewise excluded from key cleanup rather than being treated as automatic defects.

## Import settings

All eleven colour-key derivatives already use Point filtering, no mipmaps, no NPOT resize, uncompressed default import and a sufficiently large size limit. Their Standalone entries show `overridden: 0`; any compression value inside that inactive entry is not the effective setting.

The only effective exceptions among the 80 runtime PNGs were:

| Asset(s) | Current exception | Target |
|---|---|---|
| `Assets/art/units/prototypes/bugaloo-hero-prototype.png`, `bugui-whelp-prototype.png`, `dummy-whelp-prototype.png`, `popow-hero-prototype.png` | Bilinear; default compression enabled | Point, uncompressed |
| `Assets/resources/MonsterPouch/UI/home-background.png` | Default compression value 2 | Uncompressed |
| `Assets/resources/MonsterPouch/UI/MenuItems/batalla.png`, `brief.png`, `figuras.png`, `guia.png`, `sonido.png` | Source 1254×1254, size limit 512 | Size limit 2048, preserve pixels |

All inspected platform overrides were disabled. The importer compares paths case-insensitively because the existing `resources` directory is lowercase.

## Initial corrective importer and validation

`Assets/Editor/MonsterPouchPixelImportGuard.cs` limits colour cleanup to the eleven explicit source/derivative pairs and verifies on each import that the matching source is completely opaque, has at least 5% key pixels, and matches dimensions. It adjusts only imported texture memory. It does not re-encode a PNG or modify sprite rectangles, pivots, names or GUIDs.

The normal pass touches only the one-pixel exterior band. It fits a nearby non-magenta interior colour, rejects residuals above 12 RGB RMS, restores that foreground RGB and derives alpha coverage from `(1-t)`. It protects candidates near a deeper purple region (at least three purple pixels at exterior distance >=3). The verified brown/gold brief, which has no intentional violet, permits a three-pixel matte band and removes unmatched magenta there. Enclosed transparent details do not seed cleanup.

`Apply()` imports the eleven derivatives and any still-misconfigured settings-only asset. It checks hashes for source and runtime PNGs before/after, audits actual imported textures using GPU readback even when `isReadable` is false, and writes its result to `Library/MonsterPouch/pixel-import-audit.md`. The separate `Audit()` performs no asset mutations. Broad remaining-purple counts are reported for inspection, not treated as failures on Bugui.

`MonsterPouchPixelImportGuardChecks.RunChecks()` supplies five focused synthetic checks for external matte correction/coverage, preservation of identical interior colour, an authored purple region touching the silhouette, enclosed transparent details, and an already-clean silhouette including invisible RGB.

## Pure algorithm verification

`Preview-PixelImportGuard.ps1` extracts the actual `CleanFringe` implementation and executes it with small C# equivalents of Color32/Vector3/Mathf against the unchanged PNGs. All five synthetic checks passed. The preview changes 58,504 pixels across eleven derivatives and leaves **zero exterior magenta candidates in the brief**. It writes only JSON statistics, never an image.

| Derivative shorthand | Changed | Purple protected | Exterior candidates remaining |
|---|---:|---:|---:|
| Brief | 1445 | 0 | 0 |
| Anuik animation | 3259 | 0 | 2153 |
| Anuik revive | 1050 | 0 | 120 |
| Atori | 5295 | 0 | 3007 |
| Bugui | 463 | 26342 | 29501 |
| Kayon animation | 30087 | 257 | 2496 |
| Kayon portrait | 3441 | 0 | 86 |
| Stein animation | 8072 | 986 | 4649 |
| Stein portrait | 1 | 0 | 0 |
| Tauris | 926 | 1 | 2883 |
| Dummy | 4465 | 3 | 258 |

The remaining counts deliberately include authored purple and ambiguous mixtures rejected by the conservative fit. They must not be globally erased. Full preview data is in `texture-edge-cleanup-preview.json`. Imported-cache readback and visual inspection are still needed to distinguish actual remaining halo from preserved art.

Compilation, execution with Unity's own types, and imported-cache visual verification must be performed after integration. The pure preview does not establish the final rendered result.

## Refinement after actual imported contact-sheet inspection

The initial pass was subsequently imported in Unity: all five initial checks passed, all settings were correct, 32 source/runtime PNG hashes were unchanged, and actual GPU readback matched the pure preview exactly. The six-sprite contact sheet nevertheless demonstrated genuine narrow magenta residue on Anuik, Atori and Tauris, plus smaller specks on mixed artwork. It also confirmed that Stein's lightning, Kayon's currency glyph and Bugui's body/orb contain authored purple.

The refined importer therefore uses explicit palette policies. The brief, both Anuik sheets, Atori, Tauris, Dummy and the Kayon/Stein portraits have no authored violet silhouette: only their exterior four-pixel matte band is eligible. Stein and Kayon retain coherent detached glyphs; Stein additionally preserves purple adjacent to cyan lightning. Bugui permits a purple foreground in the colour-mixture fit, preserves colours supported by nearby interior paint, and removes tiny disconnected matte specks next to a substantial body. Interior artwork and enclosed transparency remain ineligible. The importer version is intentionally unchanged; `Apply()` explicitly reimports the eleven allowed derivatives.

The updated pure preview changes 94,455 pixels and gives:

| Derivative shorthand | Changed | Protected | Exterior purple remaining |
|---|---:|---:|---:|
| Brief | 1445 | 0 | 0 |
| Anuik animation | 8056 | 0 | 0 |
| Anuik revive | 2586 | 0 | 0 |
| Atori | 10569 | 0 | 0 |
| Bugui | 8545 | 7230 | 27971 |
| Kayon animation | 34424 | 925 | 876 |
| Kayon portrait | 3544 | 0 | 0 |
| Stein animation | 11161 | 4338 | 2371 |
| Stein portrait | 394 | 0 | 0 |
| Tauris | 6564 | 0 | 0 |
| Dummy | 7167 | 0 | 0 |

The remaining purple in the three mixed assets is not a failure count: it includes the preserved illustrated body, orb, glyph and lightning. Tests were expanded to cover removal of an isolated nearby speck, preservation of a detached thin glyph and cyan lightning, recovery of a key mixture over a purple foreground, and preservation of blue/brown palettes. The final integrated check below supersedes the earlier pending-validation statements.

## Final integrated Unity verification

The refined importer was compiled and applied in the real Unity project. `Library/MonsterPouch/pixel-import-audit.md` confirms **11 imported assets, 94,455 changed imported pixels and 32 unchanged PNG hashes**. All eleven source-provenance checks and all targeted import-setting checks are true. Actual imported GPU readback matches every row of the refined preview table above, including zero exterior magenta candidates in all eight no-violet-palette textures. **All 12 checks passed with Unity's own types**; the final two specifically distinguish Kayon's blue body from its detached purple glyph and exercise Stein's actual lightning policy.

The refreshed `Library/MonsterPouch/texture-edge-contact-sheet.png` was visually inspected over both gray and board-green backgrounds. The helper selected the highest remaining candidate count among imported sprite slices for each of six assets:

| Asset | Selected imported sprite | Visual result |
|---|---|---|
| Anuik | `anuik-approved-r04-f00` | Clean silhouette; no visible magenta matte. Existing white outline retained. |
| Atori | `atori-r04-f00` | Clean black/orange contour; prior isolated magenta edge pixels absent. |
| Tauris | `tauris-approved-r04-f11` | Head and body contour clean; prior pink lines along the head absent. |
| Stein | `stein-approved-r02-f07` | Cream body contour clean; cyan/violet lightning remains intact. |
| Kayon | `kayon-approved-r03-f14` | Blue body contour clean; detached pink currency glyph and coin effects retained. |
| Bugui | `bugui-v2-r00-f06` | Pink body and violet orb retained; no obvious external matte specks in the inspected enlarged frame. |

No further import-code change is indicated by this final visual check. The claim is removal of the identified unwanted colour-key residue while retaining authored colour, **not zero purple pixels in mixed artwork**. The counts 27,971 (Bugui), 876 (Kayon) and 2,371 (Stein) include legitimate illustrated colour. Small internal transparent holes visible in Bugui were already present in the previous capture/source derivative and are outside this import-edge correction; this change does not reconstruct those source pixels. The contact sheet is a worst-candidate-frame sample backed by full-texture numerical scans, not a visual inspection of every animation frame.

Only the staged audit documentation was changed during this final review; the guard, checker, real project files and image bytes were left untouched.

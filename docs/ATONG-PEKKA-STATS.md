# Atong: P.E.K.K.A base stats

Requested on 2026-09-26. Sources: https://www.noff.gg/clash-mini/unit/pekka and https://clashmini.fandom.com/wiki/P.E.K.K.A (Clash Mini, final documented balance).

Atong's base health is 35, basic damage is 12, attack interval is 1 / 0.3 seconds (about 3.33 seconds), melee range is one tile, and deployment costs 4 Munkens. The combat simulation rounds action times to its 0.1-second tick.

This copies base statistics, not P.E.K.K.A's star progression or abilities. Atong retains his own abilities and the existing configured windup, movement and IQ values, which are not specified by the referenced stat table. The saved catalog and fallback definition both carry the new base numbers.

Before this request, the saved catalog already had 100 health, 40 damage, 3-second attack interval and windup, 2.5-second movement, IQ 3, and cost 3. These match commit f79ab5f (2026-09-20); the recent hammer animation and target-reaction changes did not edit these values.

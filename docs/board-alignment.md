# Board alignment to the existing artwork

Measured from green-path-np.png (1086 x 1448). A least-squares regular grid fits the six drawn columns and ten drawn rows. This changes only the mapper, preserving sprite foot pivots, animations, board values, occupancy, Brief, and artwork.

Previous cell size: (1.3482900, -1.1645360); offset: (-3.3851200, 5.1307140).
Current cell size: (1.2774140, -1.1745080); offset: (-3.2151270, 5.1606710).
Maximum center error before: 0,190529 world units; after: 0,029898 world units.
Cell (0,0): (-4.195127, 6.495672, 0.000000); cell (5,9): (2.191942, -4.074896, 0.000000).

The source's hand-drawn boundaries vary slightly: remaining residual is approximately one screen pixel in the 540 x 960 composition. Unit roots and their ground footprint use these cell centers; their bodies extend above them. Rendering validation should inspect preparation with no result dialog and compare the four corners and central cells. This report records measurements, not a completed screenshot check.

Repeat in the Editor with Monster Pouch → Align board cell centers to existing art, or invoke MonsterPouchBoardAlignment.Calibrate(). It is safe to rerun and leaves texture GUIDs and subassets intact.

# Target death and IQ reaction

When the current enemy dies or starts resurrection, its attackers cancel their attack animation and unreleased impacts. They acquire a living replacement and wait for their IQ reaction delay before attacking or moving. The next attack starts with its full windup. Existing movement reservations and the normal targeting rules remain in effect.

Reaction time is `(11 - clamp(IQ, 1, 10)) × 0.1 seconds`: IQ 1 = 1 second, IQ 5 = 0.6 seconds, IQ 10 or higher = 0.1 seconds. IQ continues to decide priority when units contest a tile. Movement and attack speeds remain separate values.

Already launched projectiles continue independently. Flo's flower preparation, committed Popow dashes and airborne movement are not interrupted by the death of an unrelated former target. A replacement already in range stays selected if Anuik resurrects during the reaction delay.

Bugui keeps its accumulated rhythm throughout the round. When a living replacement is already in range, its next projectile releases at the later of its original scheduled release and the IQ reaction deadline. It does not pay an extra first-attack windup after that deadline. A canceled preparation resumes the same attack cycle, without awarding another stack; subsequent attacks keep their normal accelerated cadence. Projectiles still travel normally. If movement is needed, or an explicit reset/stun interrupts the action, the normal full attack preparation applies.

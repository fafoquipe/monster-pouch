# Validación del 8 de octubre de 2026

Unity 6000.3.9f1. Se conserva Android/APK como destino.

## Comprobaciones de esta entrega

- Blotan: ocho pruebas de habilidades (incluidos límites editables, casillas bloqueadas y duración exacta), aprobadas.
- Stein: cinco pruebas de rayos, duración y causa eléctrica, aprobadas.
- Combate documentado: 41 pruebas, aprobadas, incluida la causa de elevación al atravesar objetivos con Popow.
- Movimientos de control: 19 pruebas de simulación y cinco de presentación, aprobadas.
- PlayMode: tres pruebas de viewport aprobadas con el arte original centrado y recortado, sin panoramas añadidos.
- PlayMode: tres pruebas de efectos de estado y una del holograma de Blotan aprobadas.

- Integración de Kayon y presentación de control: 16 pruebas aprobadas después de conservar la animación y el retrato de la invocación.

## Fallos pendientes de la suite general

No se considera aprobada la suite completa. Resultado EditMode: 746 aprobadas y 9 fallidas. Una prueba PlayMode de Tsu también falla. No se ha determinado si estos fallos anteceden a los cambios.

- MonsterPouch.Gameplay.Tests.EditMode.BasicAttackPhaseTests.PresentationHoldsContactThenAdvancesReturnFramesAndAllowsZeroPhases: Expected: same as < (UnityEngine.Sprite)>   But was:  <aky-battle-r00-f04 (UnityEngine.Sprite)>
- MonsterPouch.Gameplay.Tests.EditMode.BugalooSuperTests.AtongEmitsSpecialOnlyWhenTheCriticalHitLands: Expected: 1   But was:  0
- MonsterPouch.Gameplay.Tests.EditMode.BugalooSuperTests.ConfiguredDurationExpiresAtZeroThenPassiveChargingAndNextSuperResume: Expected: True   But was:  False
- MonsterPouch.Gameplay.Tests.EditMode.TauntAbilityTargetingTests.AlreadyReleasedProjectileKeepsItsCapturedTarget: Expected: True   But was:  False
- MonsterPouch.Gameplay.Tests.EditMode.TauntAbilityTargetingTests.PreparingSuperRetriesTaunterWithoutSpendingAnotherEnergyBar(False): Expected: True   But was:  False
- MonsterPouch.Gameplay.Tests.EditMode.TauntAbilityTargetingTests.PreparingSuperRetriesTaunterWithoutSpendingAnotherEnergyBar(True): Expected: True   But was:  False
- MonsterPouch.Gameplay.Tests.EditMode.TauntAbilityTargetingTests.TauntDuringAnOlderBasicDoesNotCancelOrDelayFloAlreadyPlanting: Expected: True   But was:  False
- MonsterPouch.Gameplay.Tests.EditMode.TaurisAbilityDurationTests.LethalBiteCompletesItsConfiguredCastThenResumesBasicAttacks(1.4f): Expected: less than 1000   But was:  1000
- MonsterPouch.Gameplay.Tests.EditMode.TaurisAbilityDurationTests.LethalBiteCompletesItsConfiguredCastThenResumesBasicAttacks(5.0f): Expected: less than 1000   But was:  1000
- October7PresentationPlayTests.ChocolatePeriodicHitCreatesMiniExplosionsAndCleansThemUp: The real periodic damage event must produce the mini explosions.   Expected: not null   But was:  null

Los informes XML y las capturas se guardan en Logs/portrait-controls y Logs/portrait-review dentro del proyecto.


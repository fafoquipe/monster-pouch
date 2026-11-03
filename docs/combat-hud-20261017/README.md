# Indicadores de combate — 17 de octubre de 2026

Diseño basado en las tres referencias del usuario: medallones circulares sobre la vida, estrellas a la izquierda, daño sobre rojo y velocidad de ataque sobre azul. La energía es dorada en ambos bandos; la vida conserva azul aliado y rojo rival según la perspectiva local.

## Lectura visual

- Espada roja: daño aumentado, sin cantidades ni porcentajes.
- Espada con estelas azules: velocidad de ataque aumentada (incluye ritmo de Bugui, tormenta de Sepora y bonificación temporal de Rosie).
- Fantasma azul: invisibilidad. La barra de Blotan sigue visible durante la desaparición.
- Rombo amarillo con brillo suave: inmunidad al control de la tercera mejora de Atong. No implica inmunidad al daño.
- Bola completamente blanca sobre círculo gris: invulnerabilidad al daño, incluida la flor de Flo. El aro claro se consume alrededor del fondo gris.
- Escudos, estrella de aturdimiento, rayo, botas pegadas, luna, llama, provocación, curación tachada y escudo roto completan los estados.

Los efectos temporales consumen un aro exterior. Los permanentes conservan el aro completo. Se mantienen los efectos existentes sobre el cuerpo y el suelo. Se eliminaron las letras de estado y el texto flotante redundante del escudo. Ningún efecto se descarta por superar los seis iconos: se distribuyen de tres en tres sobre la barra.

Los símbolos usan un atlas transparente de 20 ilustraciones generado con ImageGen integrado, en `Assets/resources/MonsterPouch/UI/CombatIcons/combat-status-atlas.png`. El encargo exacto está en [imagegen-prompt.txt](imagegen-prompt.txt) y la revisión de colores en [imagegen-recolor-prompt.txt](imagegen-recolor-prompt.txt). La estrella de aturdimiento y el rayo son amarillos; la luna es amarilla con estrella azul y la vulnerabilidad usa un escudo lila. Los fondos opacos, aros y cifras se dibujan en Unity para actualizarse durante el combate. La invulnerabilidad conserva un centro blanco plano, tal como pidió el usuario. [Leyenda completa de los 20 símbolos](leyenda.md).

## Pasivas

`CollectPassiveProgress` consulta el mismo contador y los mismos parámetros de balance usados por el combate. No cambia el balance ni añade una nueva habilidad de invisibilidad.

Trimol y Tsu: símbolo inicial → 1 → 2 → 3 iluminado → símbolo inicial, con el balance actual de cuatro ataques. La cifra blanca aparece centrada dentro de un círculo azul oscuro con borde blanco completo, siguiendo las referencias. El brillo anuncia que el siguiente ataque activa la habilidad. El contador nunca muestra cero. Atong sigue su cadencia de críticos; con Golpe Perfecto está preparado desde el inicio. Popow muestra el contador de Gancho Barrido junto con su barra de energía solo si posee esa mejora. Los parámetros editados en el inspector también se reflejan en los contadores.

La presentación no suma ataques, no modifica energía y no controla duraciones. Los brillos y aros se actualizan con el reloj de simulación, por lo que se congelan al pausar. Los indicadores desaparecen al terminar la ronda, morir o abandonar la partida. Las filas y objetos se reutilizan.

## Revisión

`Monster Pouch > Review > Combat HUD` genera `combat-hud-preview.png` usando los componentes reales de la interfaz en una escena aislada. La muestra amplía las barras para facilitar la revisión; no cambia las escenas ni los recursos de balance.

Pruebas de simulación: `CombatHudSnapshotTests`. Pruebas de interfaz y partida: `CombatHudPlayTests`. Resultados finales en `validation.md`.

Copias de los cinco archivos existentes antes del cambio: `Backups/combat-hud-20261017-191324/`.

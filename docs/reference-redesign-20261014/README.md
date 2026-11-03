# Rediseño de personajes — 14 de octubre de 2026

Estado: 16 referencias identificadas y 1504 poses fuente preparadas. La compilación estática se verifica en el registro de esta entrega; las pruebas dentro de Unity y la asignación de recursos se ejecutan mediante una importación única al quedar libre el editor. Consultar `Logs/local-validation/reference-redesign-status.txt` para el estado real. No interpretar la vista previa como prueba de integración en una APK.

Faltan referencias identificadas de Stein, Tokoro y Bugui. Conservan sus recursos anteriores.

| ID | Referencia |
|---|---|
| aky | Caballero azul con alas, número 1 |
| anuik | Rosada grande con corazones en la frente y el pecho, confirmada por el usuario |
| atong | Robot dorado, número 2, confirmado |
| atori | Criatura naranja con cuernos, número 3 |
| blotan | Comerciante verde con monedas, confirmado |
| bugaloo | Mago azul con luna, número 13 |
| flo | Criatura turquesa con maceta y margarita |
| gochan | Criatura verde con sombrero rojo en espiral |
| hymay | Héroe naranja con capa morada |
| jazar | Armadura amarilla con depósitos verdes |
| kayon | Mago de madera con martillo, confirmado |
| popow | Boxeador rojo con cabeza de fuego |
| sepora | Criatura magenta de ojos amarillos |
| tauris | Criatura gris azulada, musculosa y sonriente, confirmada |
| trimol | Gólem morado de roca, confirmado |
| tsu | Cabello magenta, ropa oscura y corazón |

Arte generado con la herramienta integrada imagegen. El conjunto de prompts está en `prompts.json`. Los PNG fuente se conservan sin cambios en `Assets/art/ReferenceRedesign/Sources`. Unity hace únicamente la separación técnica de siluetas y su empaquetado RGBA en celdas acolchadas; no redibuja ni reescala sus píxeles.

Las hojas contienen reposo, movimiento, ataque, habilidad, impacto y derrota, con recuperación inversa para los personajes que ya tenían resurrección. Hay ocho direcciones de presentación; la espalda izquierda usa la vista reflejada de espalda derecha. La selección de cuadros se documenta en `Assets/resources/MonsterPouch/ReferenceRedesign/manifest.json`.

Los terrestres alternan pie de impulso por desplazamiento real entre casillas. Anuik y Aky usan vuelo visual: flotar no modifica la inmunidad ni el estado de vuelo del combate. La anchura de cada personaje conserva WorldWidth y DisplayScale del proyecto; ReferencePixelWidth se recalcula según la silueta nueva para evitar encogerlos. El giro de Hymay utiliza la figura completa y Atong deja de usar el antiguo recorte de mandíbula.

Se actualizan personajes, catálogo, retratos y gestos del lobby. No se editan proyectiles, explosiones, efectos ni reglas de combate. Hay copias de los archivos anteriores en `Backups/reference-motion-*` y, al aplicar las referencias, en `Backups/reference-redesign-*`.

## Revisión y ejecución

- `review.html`: vista ampliada de los 16 diseños con selector de acción y dirección. Es una revisión del arte; no simula toda la lógica del juego.
- `review-idle.png`: panel de reposo.
- `Monster Pouch > Art > Apply supplied reference redesign`: vuelve a aplicar las referencias explícitamente en el editor.
- `MonsterPouchReferenceRedesign.RunValidation`: ejecuta las pruebas enfocadas de saltos, vuelo, animación y tiempos de presentación.
- `Logs/local-validation/reference-redesign.json`: asignaciones y escala tras la importación.
- `Logs/local-validation/reference-redesign-tests.xml`: resultados reales de las pruebas, cuando terminen.

La APK que ya se estaba compilando cuando comenzó esta integración no incluye estos cambios. Hará falta una compilación posterior para llevarlos a Android.

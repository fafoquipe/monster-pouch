# Personajes y animaciones — 15 de octubre de 2026

Los 19 personajes están integrados en el catálogo, las fichas y el lobby. Se conservaron las reglas, los tiempos de combate y los efectos externos de proyectiles y habilidades.

## Referencias finales

| Personaje | Diseño |
|---|---|
| Aky | Caballero azul con alas |
| Anuik | Hada amarilla con corazón, número 15 |
| Atong | Robot dorado grande, número 2 |
| Atori | Criatura naranja con cuernos |
| Blotan | Comerciante verde con monedas |
| Bugaloo | Mago azul con luna |
| Bugui | Criatura rosada con alas y corazones |
| Flo | Criatura turquesa con maceta y margarita |
| Gochan | Criatura verde con sombrero rojo en espiral |
| Hymay | Héroe naranja con capa morada |
| Jazar | Armadura amarilla con depósitos verdes |
| Kayon | Criatura de madera con martillo |
| Popow | Boxeador rojo con cabeza de fuego |
| Sepora | Criatura magenta de ojos amarillos |
| Stein | Espíritu eléctrico amarillo de la última referencia número 11 |
| Tauris | Criatura gris azulada musculosa y sonriente |
| Tokoro | Yeti azul de pelaje blanco de la referencia individual |
| Trimol | Gólem morado |
| Tsu | Cabello magenta, ropa oscura y corazón |

Stein tiene una preparación más marcada, mayor extensión del ataque, una habilidad más enérgica y una derrota horizontal con la antena caída. Sus parámetros visuales acentúan el impulso y la expansión del cuerpo sin cambiar el daño, los tiempos ni los proyectiles.

Los personajes terrestres alternan las secuencias de impulso izquierdo y derecho en los desplazamientos entre casillas. Los voladores conservan su vuelo visual. Atong pasa de aproximadamente 1,13 a 2,15 unidades de ancho visible (+91 %); es el de mayor ancho de referencia. También aumentaron los tamaños de la mayoría de los demás personajes.

Se revisaron las ocho orientaciones. Se sustituyeron las secuencias laterales que incluían giros involuntarios de espalda en Aky, Atori, Bugaloo y Sepora. Tokoro utiliza su rugido de tres cuartos durante la habilidad lateral.

## Arte y conservación

Arte generado mediante la herramienta integrada imagegen. Los PNG originales de cada diseño se conservan en `Assets/art/ReferenceRedesign/Sources`. Unity separa las siluetas conectadas y las empaqueta sin escalar ni repintar sus píxeles en `Assets/resources/MonsterPouch/ReferenceRedesign/*-clean-v3.png`.

Registros de generación: `prompts.json` (diseños iniciales; la criatura rosada quedó asignada a Bugui), `prompts-20261015.json` (Anuik, Tokoro y el personaje alternativo archivado) y `stein-final-prompt.json` (referencia final y refuerzo de las animaciones de Stein). El registro `stein-cloud-prompt.json` corresponde a una propuesta rechazada; esa nube no se utiliza en el juego.

El personaje amarillo que el usuario pidió conservar está guardado, sin asignación a Stein, en `ArtArchive/UnassignedYellowFairy-20261015/animation-sheet.png`. Los datos anteriores se conservan en `Backups/reference-correction-20261015-130529` y `Backups/reference-redesign-*`.

## Revisión

- Auditoría de los 19 personajes y las 1.684 poses fuente: cero componentes desprendidos, píxeles de alfa tenue, recortes en los márgenes o secuencias vacías.
- Revisión visual de todas las hojas en ocho direcciones y comprobación del tamaño real en el tablero.
- Pruebas de saltos, vuelo, sincronización, asignación de sprites, tamaño de Atong y derrota de Stein: consultar el resultado fechado de `reference-redesign-tests.xml`.
- Dos pruebas de Anuik dentro de partida pasaron: ataque y resurrección con el arte nuevo, proporciones y colores originales preservados por el material.
- El visor `review.html` carga los 19 personajes y permite elegir acción, dirección, fondo, velocidad y avance de una pose. Usa los sprites y pivotes importados; no simula toda la lógica del combate.

La auditoría de píxeles comprueba defectos concretos del atlas; no constituye una garantía absoluta sobre cualquier detalle artístico.

Las capturas, el informe de píxeles y las pruebas se guardan en `Logs/local-validation/reference-quality-20261015`. No se ha generado una APK nueva con esta revisión.

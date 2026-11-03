# Preparación y aspecto de Pokrak — 17/10/2026

Suki, Rosie y Pokrak tenían un único sprite en cada secuencia de reposo. El reproductor lo trataba como una animación completa y regresaba antes de aplicar la respiración. Ahora los idles de un solo dibujo reciben una respiración leve, con variación vertical máxima del 1,2 % y compensación horizontal menor, anclada a los pies. Los idles con varias poses conservan su reproducción anterior. El reloj respeta la pausa y la congelación al terminar la ronda. La vista previa del editor también muestra esta respiración.

Pokrak utiliza la nueva hoja proporcionada por el usuario, conservada en [combat-reference-20261017.png](../../ArtArchive/pokrak/combat-reference-20261017.png). El importador empaqueta sus 25 dibujos a su resolución original en [pokrak-combat.png](../../Assets/resources/MonsterPouch/Pokrak/pokrak-combat.png), con cinco orientaciones y reflejos para completar ocho. Los destellos separados de la silueta se excluyen del sprite corporal; las burbujas de la habilidad siguen siendo efectos independientes. La hoja anterior permanece archivada. No se generó arte nuevo para este ajuste.

Se conservan el tamaño del personaje en casillas, sus estadísticas y habilidades, los iconos y el idle específico de las pantallas de menú. La regla de Monsters detallados solo en BATALLA sigue igual.

El Brief de preparación encuadra ahora los tres retratos por su silueta visible. Suki y Pokrak usaban la celda completa de animación, con margen transparente; ese margen hacía que la figura se viera pequeña dentro de la oferta. El importador añade un recorte de retrato independiente, conservando un pequeño borde, los píxeles originales y las celdas/pivotes de combate. Rosie recibe el mismo criterio de encuadre. Los instaladores conservan este recorte al volver a importar el arte. No se agrandan las casillas, los precios ni el tamaño en el tablero.

## Verificación

Resultados y capturas en `Logs/local-validation/preparation-idles-20261017/`.

- 10/10 pruebas Edit Mode aprobadas: tres Whelps en ocho orientaciones, respiración limitada, ancla inmóvil, estadísticas y dibujos intactos, congelación/reanudación, regresión del idle de varias poses y secuencias de combate.
- La integración Play Mode recorre preparación real, pausa y burbujas de Pokrak.


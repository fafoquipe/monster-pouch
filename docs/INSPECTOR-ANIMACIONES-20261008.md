# Tiempos visuales por habilidad

En el inspector de `MatchConfig`, el selector **Personaje** se divide en **Monsters** y **Whelps**, con nombres en orden alfabético dentro de cada grupo. El orden del catálogo guardado se conserva.

Cada habilidad y mejora tiene un apartado **Animación visual de esta habilidad**:

- **Preparación hasta el golpe:** modifica la preparación visual y el momento de soltar el golpe. 0 elimina la preparación; -1 hereda la del ataque básico.
- **Mantener pose de impacto:** tiempo durante el que mantiene la pose.
- **Regreso al estado normal:** tiempo de vuelta al reposo.
- **Duración visual total:** muestra el tiempo resultante. La preparación y los recorridos se sincronizan con los pasos de combate de 0,1 segundos.
- **Recorridos:** Popow muestra la duración de la embestida; Aky, el vuelo; Hymay, el arrastre o la voltereta; los helados de Tokoro, el viaje hasta la casilla. Se distinguen de la duración del aturdimiento, inmovilización o invulnerabilidad.
- **Duración visual:** en las animaciones reactivas que tienen ese parámetro, un valor positivo configura esa habilidad; 0 conserva el clip de respaldo. El inspector muestra la duración efectiva de la vista frontal.

La resurrección se ajusta en la habilidad de Anuik. Las mejoras que comparten una animación muestran a qué habilidad corresponde y sus tiempos; no presentan un control independiente que el juego ignoraría. Las pasivas sin una secuencia propia también lo indican.

El campo global **Animación genérica de respaldo** deja de ocupar una sección aparte en este inspector. Su valor guardado en el catálogo de arte se conserva. Ese valor no sustituye los tiempos propios de las habilidades.

No se cambiaron las estadísticas ni los tiempos guardados del balance. No se generó una build Android.

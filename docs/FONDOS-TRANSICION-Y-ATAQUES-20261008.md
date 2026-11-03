# Fondos, minimundos y fases de ataque · 8 de octubre de 2026

## Resultado
- Parque de juegos reemplaza el antiguo paisaje de árboles en el inicio.
- Grand Casa Hotel tiene su propio inicio, con corazones, diamantes, tréboles y picas.
- Los tres minimundos comparten lienzo cuadrado, vista frontal elevada y tamaño de presentación (260 × 260 en el lienzo lógico).
- La transición cierra en círculo durante 0,38 s, muestra SOLAMENTE el minimundo del destino sobre negro durante 0,64 s y abre durante 0,44 s. No muestra ni transforma el minimundo del mapa anterior.
- Se aplica al elegir estilos y cuando el mapa online cambia entre rondas. El reloj de partida sigue siendo autoritativo; la transición usa tiempo real y bloquea la interacción mientras está visible.
- El centro transparente del marco inflable ahora se conserva: las bolas y los bordes dibujan sobre las casillas sin cambiar sus posiciones ni sus zonas táctiles.
- Las futuras builds se llaman MonsterPouch_aaaa-MM-dd_HH-mm-ss.apk. No se ha generado otra APK durante estos ajustes.

## Ataques
Cada personaje tiene carga, pose de golpe y regreso editables en MatchConfig, en «Animación del ataque normal». Se puede usar 0 para eliminar golpe o regreso. Los tiempos de habilidades independientes permanecen en sus respectivas habilidades.
Se conservaron las cargas personalizadas existentes. La comparación de las copias de balance confirmó que solamente se añadieron los 38 valores de golpe y regreso de los 19 personajes; vida, daño, energía, costes y parámetros anteriores no cambiaron.
El siguiente golpe espera la duración completa si las tres fases exceden el intervalo. Esto afecta especialmente las cargas largas de Aky y Atong. La versión online se actualizó a v6 para impedir mezclar clientes con reglas de tiempos distintas.

## Arte integrado
Creado y editado con la herramienta imagegen incorporada. No se modificaron las imágenes con scripts.
- Assets/resources/MonsterPouch/UI/home-background.png — Parque de juegos; conserva el GUID del fondo anterior.
- Assets/resources/MonsterPouch/UI/TableSkins/lounge-home.png — hotel con las cuatro figuras.
- Assets/resources/MonsterPouch/UI/MiniWorlds/classic.png — parque ajustado a las proporciones aprobadas.
- Assets/resources/MonsterPouch/UI/MiniWorlds/lounge.png — hotel aprobado.
- Assets/resources/MonsterPouch/UI/MiniWorlds/royal.png — Campo Real aprobado.

Prompts: [fondos y corrección de proporciones](FONDOS-Y-MINIMUNDOS-PROMPTS-20261008.md), [cuatro figuras del hotel](HOTEL-CUATRO-FIGURAS-PROMPT.md), [minimundos originales](MINIMUNDOS-PROMPTS-20261008.md).
La versión corregida de classic reemplaza la primera versión diagonal.

## Verificación
- 39 pruebas aprobadas de fases, habilidades y sincronización online local.
- 5 pruebas Play Mode aprobadas de transición, capas del parque y bola de chicle de TSU.
- Las pruebas de estilos existentes comprobaron el cambio de fondos, la persistencia al recargar y la conservación de posiciones.
- Revisión adicional de las animaciones existentes: se corrigió la compatibilidad del golpe largo de Atong en actores de prueba sin catálogo.
- Capturas nativas de Unity: Logs/world-polish/home-classic.png, home-lounge.png, home-royal.png e iris-classic.png, iris-lounge.png, iris-royal.png.
- No se ha validado esta revisión en dos dispositivos físicos por Internet.


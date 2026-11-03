# Editar animaciones de habilidad

En Unity, abre `Assets/resources/MonsterPouch/MatchConfig.asset`, elige el personaje y busca **Animación de habilidad > Duración visual (s)**. El valor se guarda en su entrada de `UnitArt` y se aplica a las ocho direcciones. Un valor de `0` utiliza el tiempo original de cada clip, indicado como **Duración actual**. Se admite deshacer con Ctrl+Z.

Blotan tiene ahora 1,4 segundos para mostrar las dos poses de Buscatesoros. El destello y el aviso de Munkens usan ese mismo tiempo. Los valores de combate, el intervalo de ingresos y la cantidad de monedas siguen siendo independientes.

Los movimientos que deben coincidir con la simulación conservan controles específicos: Popow usa `travelDuration`, Flo usa `castDuration`, Aky usa **Tiempo de vuelo** y Anuik usa **Revive Delay**. El inspector los señala. En las supers sostenidas, la duración visual controla cada ciclo; la duración del efecto continúa definida por la habilidad.

Muerte, aturdimiento, reinicios y fin de ronda siguen cancelando la animación cuando corresponde. Alargando la pose no se retrasa el daño de un ataque ya iniciado.

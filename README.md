# Monsters of Eronya

Juego de combate automático en pixel art para Android, desarrollado con **Unity 6000.3.9f1**. Prepara tu equipo, compra figuras y Tricks con Moonkens y consigue tres victorias de ronda.

## Abrir el proyecto

```sh
git clone git@github.com:fafoquipe/monster-pouch.git
cd monster-pouch
```

Añade esta carpeta en Unity Hub usando la versión de `ProjectSettings/ProjectVersion.txt`. Abre `Assets/Scenes/main-scene.unity` y pulsa Play. El repositorio contiene los recursos, catálogos y metadatos necesarios; los importadores históricos de arte no son un paso de instalación.

El repositorio conserva temporalmente su URL anterior. Algunos identificadores internos, rutas de recursos y menús de herramientas también conservan `MonsterPouch` para mantener la compatibilidad con los guardados.

## Contenido y organización

El catálogo contiene 44 personajes: 20 de Eronya, 6 Monsters y 18 Whelps. La selección distingue Héroes y Piezas. Las cinco facciones de Eronya son Naturalis, Aquarion, Geothar, Volkanar y Aireola.

Los recursos de personajes están agrupados en `Assets/resources/MonsterPouch/Characters/`:

- `Eronya/<facción>/<id>`
- `Monsters/<id>`
- `Whelps/<id>`

Los recursos compartidos se agrupan en `UI`, `Effects`, `Materials`, `Shaders` y `Config`. Los archivos `.meta` forman parte del proyecto y deben acompañar a sus recursos. Las imágenes se versionan directamente en Git, sin Git LFS.

## Compilar y probar

El destino principal es Android en orientación vertical. Usa **Monster Pouch → Build → Android APK** en Unity. La variante **Android Developer Build (todo desbloqueado)** se destina a pruebas y mantiene un identificador Android separado. Consulta [BuildTools/ANDROID.md](BuildTools/ANDROID.md) para conocer los requisitos y las opciones del script de compilación.

Las pruebas EditMode y PlayMode están en `Assets/tests/` y se ejecutan desde el Test Runner de Unity. Los informes de `docs/` describen entregas concretas; no todos representan el estado actual ni una validación completa del catálogo.

## Espacio en disco

`Assets`, `Packages` y `ProjectSettings` contienen el proyecto reproducible. `Library`, `Temp`, `obj`, `Logs` y los resultados de `Builds` son datos locales y no se publican. Unity genera sus cachés al importar y compilar, por lo que el tamaño de la carpeta de trabajo no equivale al tamaño del APK.

## Multijugador y seguridad

El modo online actual usa sesiones casuales entre clientes y comprobaciones de sincronización. No equivale a un servidor autoritativo: la propiedad de contenido y el progreso local no constituyen una economía protegida contra clientes modificados. Un lanzamiento competitivo o con compras reales necesita validación y almacenamiento en un backend de confianza.

Los resultados online deben verificarse antes de entregar recompensas. Las búsquedas automáticas de las versiones de desarrollo y QA usan grupos separados de la versión normal. No incluyas credenciales, claves de firma, archivos `.env` ni datos de cuentas en los commits; configura las credenciales fuera del repositorio.

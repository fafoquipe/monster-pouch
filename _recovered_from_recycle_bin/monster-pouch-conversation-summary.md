# Monster Pouch — resumen compacto para nueva conversación

## Nombre oficial del proyecto

- Nombre visible: **Monster Pouch**
- Kebab-case para carpetas, archivos, prefabs y GameObjects: **monster-pouch**
- C# / namespaces / clases: **MonsterPouch**
- Bundle ID: **com.monsterpouch.game**

Nombres incorrectos que no deben usarse:
- Mini Gogos, Gogos, Gogo
- Monter Pouch, MonterPouch, monter-pouch
- Monster Pounch, MonsterPounch, monster-pounch

Estado final del renombrado:
- `productName`: Monster Pouch
- Bundle IDs Android/iPhone/Standalone: `com.monsterpouch.game`
- Namespaces:
  - `MonsterPouch.Core`
  - `MonsterPouch.Managers`
  - `MonsterPouch.Mobile`
- Solution final: `monster-pouch.sln`
- Carpeta raíz final: `monster-pouch`
- Reportado: 0 referencias incorrectas.
- Escenas, prefabs, `.meta`, GUIDs y assets visuales intactos.

---

## Rutas importantes

Proyecto Unity:
- `C:\Users\HP VICTUS\Juegos Unity\mini-gogos\game\monster-pouch`

Escena principal:
- `Assets/Scenes/main-scene.unity`

Prefab de ambientación:
- `Assets/prefabs/ambientations/grand-cas-hotel.prefab`

Carpeta de ambientación casino:
- `Assets/art/ambientations/casino/`

Chips individuales:
- `Assets/art/ambientations/casino/chips/`

Cartas:
- `Assets/art/cards/`

Backups de escenas:
- `Assets/Scenes/backups/`

---

## Estado de la escena principal

Escena importante:
- `main-scene`

En `EditorBuildSettings` están:
- `Assets/Scenes/main-scene.unity`
- `Assets/Scenes/SampleScene.unity`

Había backups mezclados en `Assets/Scenes`, incluyendo:
- `main-scene.unity.bak_cleanup`
- `main-scene.unity.bak_hierarchy2`
- `main-scene.unity.bak_reorg`
- `main-scene_bak_shadows.unity`

Uno podía verse como otro `main-scene` porque terminaba en `.unity`, pero no estaba en Build Settings.

Recomendación:
- En `Assets/Scenes` dejar solo `main-scene.unity`, `SampleScene.unity` y `backups/`.
- Mantener los backups hasta tener backup externo.

---

## Ambientación terminada

Ambientación actual:
- `grand-cas-hotel`

Jerarquía:

```text
scene-root
└── ambientations-root
    └── grand-cas-hotel
        ├── background-root
        │   └── casino-table-path
        ├── board-root
        │   ├── table-surface
        │   └── table-frame
        └── decor-root
            ├── structures-root
            │   ├── clown-tower
            │   └── shuffler-cart
            ├── dices-root
            │   ├── die-white
            │   ├── die-red
            │   ├── die-red-small-1
            │   └── die-red-small-2
            ├── chips-root
            │   ├── chip-stack
            │   ├── chip-blue-stack-small
            │   ├── chip-green-stack-small
            │   ├── chip-red-stack-tall
            │   ├── chip-white-stack-tall
            │   ├── chip-blue-cluster
            │   ├── chip-mixed-pile-2
            │   ├── chip-red-cluster
            │   └── chip-green-stack-tall
            └── cards-root
```

`cards-root`:
- Existe.
- Está vacío por ahora.
- Las cartas se agregarán más adelante.

La ambientación visual quedó aprobada por el usuario. No seguir tocándola salvo necesidad clara.

---

## Prefab

Prefab creado:
- `Assets/prefabs/ambientations/grand-cas-hotel.prefab`

Se creó con Unity batch mode / `PrefabUtility`.
Importante:
- No se reemplazó el objeto de escena por instancia prefab.
- `main-scene.unity` no fue modificada visualmente.
- Conserva la jerarquía interna de `grand-cas-hotel`.

---

## Sombras

Hubo un intento automático fallido:
- OpenCode intentó crear `*-shadow` editando YAML.
- Falló porque agregó `m_Children` duplicado y GameObjects con `Transform` pero sin `SpriteRenderer` en `m_Component`.
- Se revirtió limpiamente:
  - se eliminaron objetos `*-shadow` automáticos
  - se eliminaron IDs `900380–900415`
  - la jerarquía volvió a quedar limpia

Sombras finales:
- El usuario las creó manualmente en Unity duplicando objetos.
- Quedaron bien visualmente.
- No usar OpenCode para crear sombras por YAML.

Método seguro para nuevas sombras:
1. Duplicar objeto en Hierarchy.
2. Renombrar `objeto-shadow`.
3. Hacerlo hijo del objeto original.
4. SpriteRenderer negro con alpha bajo.
5. `Order in Layer` menor que el original.
6. Desplazar un poco abajo/derecha.
7. Ajustar visualmente.

---

## Reglas generales del proyecto

Mantener kebab-case para:
- archivos
- carpetas
- prefabs
- GameObjects cuando aplique

Mantener C# style para:
- namespaces
- clases
- identificadores

No hacer:
- No editar `.meta` manualmente.
- No tocar GUIDs.
- No tocar ProjectSettings sin diagnóstico.
- No editar escenas YAML a mano para cambios complejos.
- No borrar assets originales.
- No reemplazar assets originales sin backup.
- No usar PNGs compuestos cuando se piden piezas separadas.
- No dejar objetos decorativos sueltos fuera de `grand-cas-hotel`.
- No tocar `chip-stack` si el usuario dice que está bien.
- No usar otra vez los PNG grandes de chips como decoración nueva, salvo `chip-stack` principal si ya funciona.

---

## Cosas corregidas durante el proceso

1. Hubo problemas de casing entre `Assets/scenes` y `Assets/Scenes`.
   - Se terminó usando `Assets/Scenes/main-scene.unity`.

2. Se corrigió el tablero:
   - `casino-table-border.png` debía ser RGBA con centro transparente.
   - El centro del marco debía dejar ver la base verde/table-surface.
   - No tocar `.meta` ni GUIDs.

3. La escena originalmente usaba una imagen compuesta.
   - Se cambió a sprites separados.

4. Chips:
   - Algunos sprites salían negros o se veían mal.
   - Se abandonaron los PNGs problemáticos.
   - Finalmente se usaron chips individuales válidos y se ajustaron manualmente.

5. Errores `Broken PPtr` / `Transform child can't be loaded`:
   - Causa común: `m_Children` apuntando a GameObject IDs en lugar de Transform IDs.
   - Regla:
     - `m_Children` debe contener Transform fileIDs.
     - Si A lista B en `m_Children`, B debe tener `m_Father: A`.
     - Si B tiene `m_Father: A`, A debe listar B en `m_Children`.

6. Se reorganizó la escena con:
   - `ambientations-root`
   - `grand-cas-hotel`
   - `structures-root`
   - `dices-root`
   - `chips-root`
   - `cards-root`

7. Se creó el prefab:
   - `grand-cas-hotel.prefab`

8. Se renombró el proyecto correctamente a:
   - Monster Pouch / monster-pouch / MonsterPouch

---

## Forma segura de pedir cambios a OpenCode

Siempre usar fases:
1. Diagnóstico
2. Plan
3. Aprobación
4. Ejecución mínima
5. Verificación

Prompt base:

```text
NO MODIFIQUES NADA.
Solo lee y reporta.

No tocar:
- escenas
- prefabs
- .meta
- GUIDs
- ProjectSettings
- assets visuales
- PNGs
- posiciones
- escalas
- rotaciones
- sprites
- sorting
- jerarquía

Reporta:
- ruta
- tipo
- valor actual
- problema
- cambio propuesto
- riesgo

No apliques cambios todavía.
```

Para cambios de escena:
- Evitar YAML manual.
- Preferir Unity Editor, `AssetDatabase`, `PrefabUtility` o cambios manuales.
- Si se edita YAML:
  - backup antes
  - cambiar solo líneas exactas
  - verificar `m_Children` / `m_Father`
  - verificar ausencia de `Broken PPtr`

---

## Próximas fases posibles

### Opción A — Sistema de ambientaciones
Crear lógica para cargar/activar ambientaciones desde prefabs:
- `AmbientationsManager`
- `ambientations-root`
- prefab `grand-cas-hotel`
- futuras ambientaciones

### Opción B — Gameplay base
Crear:
- `GameManager`
- `BoardManager`
- `CardManager`
- `InputManager`
- `UIManager`

### Opción C — Cartas decorativas
Agregar cartas a:
- `cards-root`

Reglas:
- No tocar `grand-cas-hotel` salvo `cards-root`.
- Usar sprites individuales.
- No usar imágenes compuestas.
- Mantener objetos separados.

---

## Estado recomendado actual

El proyecto está estable.

Siguiente paso recomendado:
1. Abrir Unity.
2. Confirmar que compila sin errores.
3. Confirmar que `main-scene` se ve bien.
4. Guardar.
5. Hacer backup externo completo del proyecto.
6. Pasar a gameplay base o sistema de ambientaciones.

No seguir tocando la ambientación visual `grand-cas-hotel` salvo para cartas futuras.

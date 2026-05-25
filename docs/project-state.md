# Monster Pouch — Project State

## Official names

- Visible name: **Monster Pouch**
- Folder/repo name: **monster-pouch**
- C# style: **MonsterPouch**
- Bundle ID: **com.monsterpouch.game**

## Current scripts (namespace map)

| File | Namespace |
|---|---|
| `Assets/scripts/core/game-bootstrap.cs` | `MonsterPouch.Core` |
| `Assets/scripts/managers/game-manager.cs` | `MonsterPouch.Managers` |
| `Assets/scripts/mobile/safe-area.cs` | `MonsterPouch.Mobile` |

## Current stable scene

- Main scene: `Assets/Scenes/main-scene.unity` (enabled in build)
- Secondary scene: `Assets/Scenes/SampleScene.unity` (disabled in build)

### Scene backups

All pre-stabilisation scene backups are stored in:

`Assets/Scenes/backups/`

## Stable ambientation

Current approved ambientation:

**grand-cas-hotel**

Prefab asset: `Assets/prefabs/ambientations/grand-cas-hotel.prefab`

### Hierarchy

```text
grand-cas-hotel
├── background-root
│   └── casino-table-path
├── board-root
│   ├── table-surface
│   ├── table-frame
│   └── board-shadow
│       └── table-frame-shadow
└── decor-root
    ├── structures-root
    │   ├── clown-tower
    │   ├── shuffler-cart
    │   └── structure-shadows
    │       ├── clown-tower-shadow
    │       └── shuffler-cart-shadow
    ├── dices-root
    │   ├── die-white
    │   ├── die-red
    │   └── dices-shadows
    │       ├── die-white-shadow
    │       └── die-red-shadow
    ├── chips-root
    │   ├── chip-stack
    │   ├── chip-blue-stack-small
    │   ├── chip-green-stack-small
    │   ├── chip-red-stack-tall
    │   ├── chip-white-stack-tall
    │   ├── chip-blue-cluster
    │   ├── chip-mixed-pile-2
    │   ├── chip-green-stack-tall
    │   ├── chip-red-cluster
    │   └── chip-shadows
    │       ├── chip-red-cluster-shadow
    │       ├── chip-green-stack-tall-shadow
    │       ├── chip-mixed-pile-2-shadow
    │       ├── chip-blue-cluster-shadow
    │       ├── chip-white-stack-tall-shadow
    │       ├── chip-blue-stack-small-shadow
    │       ├── chip-green-stack-small-shadow
    │       ├── chip-stack-shadow
    │       └── chip-red-stack-tall-shadow
    └── cards-root
```

**Total GameObjects in prefab: 42**

## Build settings

- **Enabled build scene:** `Assets/Scenes/main-scene.unity`
- **Disabled build scene:** `Assets/Scenes/SampleScene.unity`
- **EditorBuildSettings.asset** — verified intact.

## Notes

- The `board-shadow` node contains `table-frame-shadow` as a single child.
- `cards-root` exists in the prefab hierarchy with no children yet.
- Shadow objects are children of dedicated shadow root groups (`structure-shadows`, `dices-shadows`, `chip-shadows`) rather than being siblings of their light counterparts.

## Previous names (cleaned)

All references to the following have been fully removed from ProjectSettings, .cs files, and file names:

- Mini Gogos / MiniGogos / mini-gogos / minigogos / Gogos / Gogo
- Monter Pouch / MonterPouch / monter-pouch / monterpouch

## Git

- Remote: `https://github.com/fafoquipe/monster-pouch.git`
- Default branch: `main`
- `.gitignore`: Covers Library, Temp, Obj, Build, Builds, Logs, UserSettings, MemoryCaptures, .vs, .idea, .DS_Store, Thumbs.db, *.csproj, *.sln, *.user, *.pidb, *.mdb, *.pdb, and others.

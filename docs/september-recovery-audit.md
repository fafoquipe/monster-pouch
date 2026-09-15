# Recovery audit — 14 September 2026

## Findings

- The actual Unity repository is clean at `1160411` (`main` and `origin/main`); only `_recovered_from_recycle_bin/` is untracked. The versioned scene, scripts, importers, resources and settings currently match the last delivered baseline. Do not reset or overwrite this repository to repair an assumed diff.
- An Android build was attempted six times today. `Logs/build-android.log` and `build-android2.log` report a missing SDK; `build-android3.log` reports CS0104 (`Debug` ambiguous) in a temporary `Assets/Editor/BuildScript.cs`; attempts four and five report a missing NDK. That BuildScript is now absent. The sixth log finishes successfully with exit code zero.
- `Builds/Android/Monster Pouch.apk` exists, 82,713,059 bytes, modified 14 September 12:32. A successful build is not evidence that input/layout or installation works on a phone. The APK must be rebuilt from the revised game and checked separately.
- The sixth build used Unity 6000.3.9f1 but a JDK from 6000.2.10f1, with a local, unversioned build script. The installed 6000.3.9f1 AndroidPlayer now contains SDK, NDK and OpenJDK directories. A reproducible Editor build entry point should use the current editor's supported toolchain.
- Only the old `Builds/Windows` executable dated 11 September remains. The previously validated `Windows-Coins` distribution is absent. Versioned source and assets remain intact, so it can be rebuilt; do not confuse the old executable with current source.
- `_recovered_from_recycle_bin/` contains original art, archives and extracted character animations, including `monsters.zip` dated today. It is not a replacement Unity project and contains no active source modifications. Preserve it and compare its art with the supplied current source folder.
- Build settings still enable only `Assets/Scenes/main-scene.unity`; `SampleScene.unity` remains disabled. Manifest and editor version are baseline Unity 6000.3.9f1.
- Three Unity processes started together around17:21; two recent AssetImportWorker logs strongly suggest one editor plus two import workers. No additional editor was launched for this audit. Process command lines and the global Editor.log were denied by the read-only sandbox; current compilation/runtime status must be checked through the existing editor bridge by the root agent.

## Baseline limitations relevant to requested redesign

- Only four Monsters and five Whelps exist in the current delivered gameplay, so full PDF roster/energy mechanics require implementation, not restoration.
- The main UI already applies `Screen.safeArea` in `local-game-ui.cs`, but the older standalone `mobile/safe-area.cs` handles it only in Awake and uses a required Canvas reference. The active scene/UI setup must be verified to avoid applying both safe-area systems.
- Existing target persistence, Anuik's resurrection exception, full five-row deployment, whole-team elimination and temporary summon isolation must be preserved by new mechanics.

Read-only audit; no project assets, settings, caches, builds or files were changed.

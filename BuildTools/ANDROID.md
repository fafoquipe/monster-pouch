# Compilar Monster Pouch para Android

## Botón Android Developer Build con todo el catálogo desbloqueado

En Unity: **Monster Pouch → Build → Android Developer Build (todo desbloqueado)**.
Este botón deja a elección del usuario cuándo generar el APK; no compila al importar el proyecto.
Guarda las escenas y espera a que terminen Play Mode, las pruebas y la compilación de scripts.

- Crea una Developer Build IL2CPP con firma de depuración, nombre de aplicación `Monster Pouch DEV`
  (el nombre actual del proyecto más `DEV`) e identificador Android terminado en `.devbuild`.
  Puede convivir con la app normal: Android separa sus archivos, preferencias y progreso.
- Guarda cada resultado en una carpeta nueva `Builds/ANDROID DEVBUILD/yyyy-MM-dd_HH-mm-ss_<id único>/`.
  El APK final se llama `MonsterPouch_DEV_yyyy-MM-dd_HH-mm-ss.apk`, con la hora real al terminar de guardarse,
  en **America/La_Paz**, independientemente de la zona horaria del ordenador. No reemplaza APK anteriores ni `latest`.
- Al terminar, muestra la ruta completa en la consola y en un diálogo. `build-report.json` registra también
  la ruta, la zona horaria, la variante y el hash SHA-256.
- Desbloquea todos los personajes y mejoras del catálogo actual en el perfil local de esta variante.
  Omite el bloqueo inicial del tutorial sin inventar lecciones completadas. Los estilos ya disponibles
  siguen accesibles. No añade funciones anunciadas como «próximamente».
- Mantiene costes, Moonkens y validaciones de compra dentro de la partida. No activa pagos ni servicios comerciales.
- Los desbloqueos requieren simultáneamente Android, `DEVELOPMENT_BUILD` y la marca exclusiva
  `MONSTER_POUCH_ANDROID_DEVBUILD`. El botón pasa esta marca solo mediante
  [BuildPlayerOptions.extraScriptingDefines](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/buildplayeroptions),
  sin guardarla en los ajustes del proyecto. No se activan en el editor ni con los botones Android APK,
  Android APK (desarrollo) o Windows. Los ajustes temporales se restauran al finalizar, también si falla el build.


Desde la carpeta del proyecto, ejecuta en PowerShell:

```powershell
.\BuildTools\Build-Android.ps1
```

Si la política local impide ejecutar scripts descargados, puedes permitir únicamente esta ejecución:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\BuildTools\Build-Android.ps1
```

También puedes usar **Monster Pouch → Build → Android APK (desarrollo)** dentro de Unity. Sal de Play Mode y guarda las escenas antes de compilar. El APK usa la escena guardada `Assets/Scenes/main-scene.unity`.

## Qué hace

- Usa la versión de Unity indicada por `ProjectSettings/ProjectVersion.txt`.
- Si el proyecto está abierto, solicita el build al editor existente y espera el cambio de plataforma y la recompilación. Si está cerrado, inicia Unity en segundo plano. Nunca abre dos editores del mismo proyecto.
- Genera un **APK de desarrollo IL2CPP**, orientación vertical, identificador `com.monsterpouch.game` y firma de depuración de Android. Incluye ARM64 y ARMv7 cuando ambas bibliotecas están instaladas.
- Conserva cada resultado en `Builds/Android/AAAAMMDD-HHMMSS-id/`. Solo un build terminado correctamente actualiza `Builds/Android/latest/Monster-Pouch.apk`.
- Guarda `build-report.json`, `build.log`, SHA-256 y el entorno detectado. La ejecución en segundo plano también genera `unity.log`; el script añade `launcher-report.json` tras comprobar el archivo y su contenido.
- Restaura los ajustes de aplicación que modifica. En el editor abierto, también vuelve a la plataforma que estaba activa. La sesión batch se inicia directamente en Android.

La firma es para instalar y probar localmente. No se usan contraseñas ni certificados de publicación; no se sube nada a Google Play. Se conserva el número de versión configurado en el proyecto. El APK requiere la versión mínima de Android del proyecto (actualmente API 25 / Android 7.1).

## Opciones

```powershell
# Revisar herramientas sin iniciar Unity ni compilar:
.\BuildTools\Build-Android.ps1 -CheckOnly

# APK más pequeño para dispositivos ARM64:
.\BuildTools\Build-Android.ps1 -Architectures ARM64

# Exigir ARM64 + ARMv7, sin sustitución silenciosa:
.\BuildTools\Build-Android.ps1 -Architectures Universal

# Editor instalado en otra ubicación:
.\BuildTools\Build-Android.ps1 -UnityPath 'D:\Unity\6000.3.9f1\Editor\Unity.exe'
```

También acepta `-ProjectPath`, `-Mode Auto|Editor|Batch`, `-ApplicationIdentifier` y `-TimeoutMinutes` (45 por defecto). `UNITY_EDITOR_PATH` permite indicar la instalación de Unity sin repetir el argumento.

El primer build puede tardar por la importación para Android, IL2CPP y las dependencias de Gradle. Gradle puede necesitar conexión a Internet. Un tiempo de espera agotado no mata Unity ni borra archivos: revisa la carpeta del resultado y la consola antes de intentar otro build.

## Herramientas y diagnóstico

En Unity Hub, la versión del proyecto debe tener **Android Build Support**, **Android SDK & NDK Tools** y **OpenJDK**. Este comando comprueba los módulos incluidos con Unity y no instala herramientas. Dentro de Unity se validan además las rutas efectivas de **Preferences → External Tools**.

En este equipo se detectaron Unity **6000.3.9f1**, OpenJDK **17.0.9**, NDK **27.2.12479018 (r27c)**, plataformas SDK **34, 35 y 36**, build-tools **36.0.0** y bibliotecas IL2CPP ARM64/ARMv7. La presencia de herramientas no sustituye una compilación completada ni la prueba en un teléfono.

Si el editor rechaza la solicitud, revisa errores C#, Play Mode, escenas sin guardar o un comando pendiente. Si falla durante Gradle/IL2CPP, consulta `build.log` y `unity.log` en la carpeta fechada. El APK anterior de `latest` se conserva cuando falla el build.

Para instalarlo, copia `Monster-Pouch.apk` al teléfono y abre el archivo, permitiendo instalaciones desde esa aplicación. También puedes usar `adb install -r` con un dispositivo autorizado; el script de compilación no instala automáticamente en dispositivos. Si ya existe una versión firmada con otra clave, Android rechazará su reemplazo: conserva sus datos antes de decidir si desinstalarla.

Referencias oficiales: [entorno Android de Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/android-sdksetup.html), [argumentos de línea de comandos](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html), [BuildPlayerOptions](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildPlayerOptions.html) y [firma de depuración mediante useCustomKeystore](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerSettings.Android-useCustomKeystore.html).

## Inspeccionar el paquete y sus atlases

La auditoría de esta revisión usa Python 3.12 y UnityPy 1.25.4. Sus dependencias locales van en Library, fuera de Assets y del código distribuido:

```powershell
python -m pip install --target Library/MonsterPouchApkInspection UnityPy==1.25.4
python BuildTools/inspect_android_package.py --apk 'ruta/al/resultado.apk' --all-scenery-atlases --output Logs/local-validation/apk-inspection/final-package.json
```

Ejecuta el lector después del build correspondiente, conservando `Library/Bee/Android/Prj/IL2CPP/Gradle`. Comprueba CRC del ZIP, arquitecturas, SHA-256 y el formato/filtrado efectivo de los siete atlases; compara sus bytes con los preparados por Unity. `--baseline 'ruta/al/anterior.apk'` añade la comparación de tamaño del archivo completo. El lector no decodifica ni modifica imágenes y no instala el APK en ningún dispositivo.

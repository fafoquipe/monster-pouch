# Respaldo: referencias de depuración de Renderer2D

En `Assets/Settings/Renderer2D.asset`, el bloque serializado
`probeVolumeResources` conserva seis GUID que no se localizaron en `Assets` ni
en la caché de paquetes instalada:

| Campo | GUID |
| --- | --- |
| `probeVolumeDebugShader` | `e5c6678ed2aaa91408dd3df699057aae` |
| `probeVolumeFragmentationDebugShader` | `03cfc4915c15d504a9ed85ecc404e607` |
| `probeVolumeOffsetDebugShader` | `53a11f4ebaebf4049b3638ef78dc9664` |
| `probeVolumeSamplingDebugShader` | `8f96cd657dc40064aa21efcc7e50a2e7` |
| `probeSamplingDebugMesh` | `57d7c4c16e2765b47a4d2069b311bffe` |
| `probeSamplingDebugTexture` | `24ec0e140fb444a44ab96ee80844e18e` |

URP 17.3.0 declara ese contenedor y sus campos como obsoletos en
`Runtime/Deprecated.cs`; los recursos de depuración se trasladaron a
`ProbeVolumeDebugResources`. La búsqueda del código de URP instalado solo
encontró el campo antiguo en esa declaración. No se sustituyeron referencias.

Unity 6000.3.9f1 abrió la escena principal en modo sin gráficos y cargó los
44 personajes durante las pruebas de archivo. Esto no es una validación
visual ni una compilación Android: ambas quedan pendientes. Cinco de siete
pruebas seleccionadas pasaron; las otras dos comparan hashes congelados de
personajes que han cambiado desde el plan del 2 de noviembre. Los valores
actuales se preservan por indicación del propietario.

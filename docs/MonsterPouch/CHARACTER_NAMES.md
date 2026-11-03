# Nombres vigentes de personajes

Auditoría del 20 de octubre de 2026, después de MP-DEC-030. Fuente: el campo General.DisplayName de cada CharacterDefinition actual, no el nombre del archivo ni el ID. La incorporación concurrente de Kazuru amplió el catálogo de 22 a 23 personajes durante el trabajo posterior.

Los IDs estables se conservan en referencias, efectos, mazos, guardado y protocolo. Los nombres visibles se leen del catálogo. Esta tabla documenta los nombres ya existentes; no propone renombrar personajes.

| ID interno | Nombre visible actual | Tipo |
|---|---|---|
| anuik | Aniku | Monster |
| bugaloo | Bugrum | Monster |
| popow | Pombú | Monster |
| sepora | Selora | Monster |
| tauris | Tauruk | Monster |
| kazuru | Kazuru | Monster |
| aky | Akryn | Whelp |
| atori | Atrik | Whelp |
| atong | Atron | Whelp |
| bugui | Bivui | Whelp |
| blotan | Blotun | Whelp |
| flo | Floru | Whelp |
| gochan | Gochar | Whelp |
| hymay | Hymal | Whelp |
| jazar | Jazir | Whelp |
| kayon | Kaydo | Whelp |
| pokrak | Pokrak | Whelp |
| rosie | Rosie | Whelp |
| stein | Stenko | Whelp |
| suki | Suki | Whelp |
| tokoro | Toronko | Whelp |
| trimol | Trimar | Whelp |
| tsu | Tsumi | Whelp |

Ubicación de cada fuente: Assets/Resources/MonsterPouch/Characters/<ID>/Character.asset. MatchConfig.CreateRuntimeConfig usa esas definiciones actuales. DocumentedRoster y los archivos históricos conservan referencias de desarrollo y no son la autoridad para nombrar la UI.

La elección inicial aprobada conserva los IDs bugaloo/popow: sus nombres visibles actuales son Bugrum/Pombú. Los siete Whelps conservan los IDs atori/blotan/bugui/flo/gochan/trimol/atong: Atrik, Blotun, Bivui, Floru, Gochar, Trimar y Atron.

Correcciones: pruebas de título comparan DisplayName; descripciones que mencionan al propietario usan su nombre actual; el aviso de posición marcada usa el nombre configurado de Tauruk; la herramienta QA muestra los nombres actuales y conserva IDs en su almacenamiento. La regresión comprueba los dos idiomas y que editar un nombre visible no renombre su identidad.

Validación: 966/966 EditMode aprobadas en phase1-current-names-edit.xml, incluidas nueve comprobaciones de nombre/identidad. Las fichas de los 22 personajes anteriores a Kazuru pasaron en ambos idiomas en phase1-current-global-play.xml. La batería global tuvo 149/150 aprobadas; el margen de estilos y Progreso quedaron verificados después con 8/8 en phase1-current-names-final-play.xml. La ejecución posterior phase2-progress-storage-final-edit.xml pasó 994/994 con el catálogo de 23 personajes. En phase2-progress-storage-global-play.xml pasaron las fichas de los 23, incluido Kazuru, en ambos idiomas; la global tuvo 156/157 por un fallo independiente en el manejo de corrupción de Progreso. Consultar QA_CHECKLIST.md.

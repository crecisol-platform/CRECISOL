# CRECISOL — Registro propio de referencias externas de Skills

Estado: CATALOGO DE REFERENCIAS CONDICIONALES (no instaladas, no ejecutadas). Fecha: 2026-11-08.
Autoridad: el Documento Maestro Único de CRECISOL y .agents/skills/crecisol-project-governance/SKILL.md.
Origen del inventario inicial: referencias consultadas en modo lectura del proyecto SOLQARYN; este archivo se escribió para CRECISOL, sin copiar Skills externas ni instrucciones operativas de otro proyecto.

## Regla estricta
CRECISOL tiene UNA Skill local: crecisol-project-governance. Las doce entradas de abajo son **referencias externas opcionales**, nunca Skills locales ni dependencias de ejecución. Para cada microtarea seleccionar solo las pertinentes; consultar directamente el repositorio del creador en el SHA fijado y la ruta exacta antes de afirmar uso. Verificar licencia, estructura y comportamiento; si la fuente fijada no está disponible, omitirla y registrar NO_VERIFICABLE. No instalar, copiar ni ejecutar scripts externos de manera automática. Ninguna referencia concede permisos, cambia el stack o reduce la seguridad.

## Inventario de orígenes y pins de referencia (requieren verificación directa al usarse)

| ID | Recurso y uso en CRECISOL | Fuente original | Pin | Ruta |
| --- | --- | --- | --- | --- |
| EXT-01 | Especificación Agent Skills; validar estructura | agentskills/agentskills | 69ef37e9424c0a7ea9dd2293b559e43ec8176379 | docs/specification.mdx |
| EXT-02 | Skill Creator integrado; crear y validar skill local | OpenAI / ChatGPT integrado | Integrado | Skill Creator disponible en sesión |
| EXT-03 | Impeccable; dashboards, CRUD, paneles, modales, accesibilidad | pbakaus/impeccable | 2149fcce39a90bb409df5f16515f316a76dc6199 | .agents/skills/impeccable/SKILL.md |
| EXT-04 | Motion Skills (Emil Kowalski); animaciones discretas y revisión | emilkowalski/skills | d23d7f88a2e21c9e4b1418c7abe420f5c1052ba7 | skills/animate/SKILL.md; skills/review-animations/SKILL.md; skills/improve-animations/SKILL.md; skills/find-animation-opportunities/SKILL.md |
| EXT-05 | Taste Skill; landing, sitios públicos, CMS visual | Leonxlnx/taste-skill | ccbc15639c97057cbfcf32ecebc38ef716e4bb37 | skills/taste-skill/SKILL.md |
| EXT-06 | Humanizer; UX copy claro sin alterar precisión | blader/humanizer | 9862685f575c65a8247f90369951df1b3416e3d6 | SKILL.md |
| EXT-07 | Napkin; lecciones y runbooks no normativos | blader/napkin | 27fa60a895de4383b26a539136bc983155cb979c | SKILL.md |
| EXT-08 | Token Optimizer; lectura progresiva de documentos extensos | alexgreensh/token-optimizer | 37a9546b9fecba2c4e9a02ef4e90855d449bf08f | skills/token-optimizer/SKILL.md |
| EXT-09 | Caveman; resúmenes efímeros NO críticos | JuliusBrussee/caveman | 15581d14007fd01fb3f132016741962f34936ca2 | skills/caveman/SKILL.md; skills/caveman-compress/SKILL.md |
| EXT-10 | Angular Developer; Angular, Signals, formularios, tests | angular/angular | 6808966993bf2b2d455d9b6245e960a59a01b9a9 | skills/dev-skills/angular-developer/SKILL.md |
| EXT-11 | Security Threat Model; riesgos de tenancy/JWT/IDOR | openai/skills | 49f948faa9258a0c61caceaf225e179651397431 | skills/.curated/security-threat-model/SKILL.md |
| EXT-12 | .NET Performance; perf cuando haya perfilado real | dotnet/skills | 9386ed7c1148a7bef970aa0502371c982effa01c | plugins/dotnet-diag/skills/analyzing-dotnet-performance/SKILL.md |

## Enrutamiento simple
- Siempre: CRECISOL governance. No consultar otras referencias por defecto.
- Trabajo de Skills: EXT-01 y Skill Creator.
- Pantallas administrativas, listas, modales: EXT-03 y, si afecta Angular, EXT-10.
- Animación: EXT-04 SOLO cuando exista motion en el scope.
- Web pública, revista visual, constructor: EXT-05; más EXT-03/EXT-10 si aplican.
- Amenazas multitenant, permisos o autenticación: EXT-11 cuando se modele la amenaza; su uso no sustituye pruebas negativas.
- Rendimiento .NET: EXT-12 SOLO con mediciones, nunca optimización hipotética.
- Redacción: EXT-06; runbook: EXT-07; contexto: EXT-08; resúmenes efímeros: EXT-09.
- Registrar en el PR qué referencia original se leyó, pin SHA y finalidad; si ninguna aplica, documentar "sin referencias externas".
- No reutilizar credenciales, branches, scripts, CI ni autoridades de SOLQARYN. CRECISOL no depende de su repositorio para trabajar.

## Estado de activación
Este documento **registra fuentes**, no prueba que se hayan leído las 12 en su origen. Al comenzar cada microtarea: verificar directamente solo las fuentes seleccionadas y su pin; no atribuir ejecución de Skill externa sin evidencia. Las actualizaciones de SHA requieren revisión de cambios y decisión documentada.

# CRECISOL — C01-06 · Acta de cierre técnico de microfase

**Fecha:** 9 de noviembre de 2026, America/Tegucigalpa.
**Resultado:** C01-06 CERRADA TÉCNICAMENTE (identidad gráfica oficial).
**Proyecto exclusivo:** solqaryn-platform/CRECISOL. **Avance:** C01 6/8 microfases; 0/37 capítulos certificados. Siguiente etapa C01-07. C02 no habilitado.

## Autoridad, requisito y aprobación

Autoridad: Documento Maestro Único CRECISOL, edición institucional vigente, Word SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e y Git blob 214bf9c3d9763be23a77b7e0c464359584edde46. Matriz C01: P14, P25, T02-R01, T03-R01, P07 y controles transversales P04/P06 según alcance.

El propietario comparó las capturas móvil/escritorio y confirmó inequívocamente: «ESE ES EL LOGO CLARO QUE SI» y «EL LOGO QUE UTILIZAS SI ES MI LOGO»; solicitó que el símbolo aparezca también en la zona superior, no solo el texto de plataforma. La aprobación comprende el PNG extraído byte a byte de word/media/image1.png, **1298×859**, **1 054 246 bytes**, SHA-256 **9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f**. No se aprobó otro diseño ni recoloración.

## Implementación y evidencias reales

PR [#24](https://github.com/solqaryn-platform/CRECISOL/pull/24) fusionado exclusivamente a dev en SHA **ac66a8aa39354d5126c3102be99e746452d455ce**. Último HEAD de PR validado: **e1deed5ec24a7bfdfe5a415554d597c34326ed9a**.

El script de Python stdlib genera dos archivos **independientes en cada build/frontend start**, sin dependencias de terceros, a partir de Word íntegro con SHA fijado. Logo completo: bytes originales sin variación. Emblema para header y favicon: recorte exacto de píxeles RGB del símbolo, x390..948/y16..553, 558×537, hash **ca789b73ee888cff86cf7849c27482546ce248f806676c5b9d34131e5302fd39**, sin cambiar forma, escala relativa ni color. Los archivos se verifican también en dist/crecisol-web/browser/assets. Se versiona la fuente canónica y el generador, no una copia adicional opaca; el asset independiente aprobado existe físicamente en el build generado.

En Angular 22 el header incluye símbolo oficial azul y nombre de marca publicado accesible, en escritorio y móvil; imagen de hero conserva logotipo completo, con object-fit: contain. Rutas same-origin saneadas, favicon local, comportamiento ante recursos ausentes, sin acceso administrativo, sin selección de tenant, sin exposición de datos privados ni dependencia de SOLQARYN.

## Compilación, casos positivos y negativos

- Último HEAD PR: [C01-06 #42573961789](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42573961789) SUCCESS, 2/2 jobs, y [foundation #42573961921](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42573961921) SUCCESS; todos sobre e1deed5ec24a7bfdfe5a415554d597c34326ed9a.
- Post-merge dev: [C01-06 #42574147302](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42574147302) SUCCESS 2/2, y [foundation #42574147272](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42574147272) SUCCESS; ambos sobre ac66a8aa39354d5126c3102be99e746452d455ce.
- Angular 22 / Node 24: typecheck y build PASS, bundle inicial JS 233.46 kB raw / 66.64 kB transferencia estimada; integridad byte-exacta de assets PASS.
- Chromium Playwright **28/28 PASS**: 11 regresiones C01-05, 8 de candidato/previsualización histórica, 9 de integración con asset aprobado. Header y hero (desktop 1440, tablet 768, móvil 360 y compacto 320), ratio sin deformar, sin overflow, navegación, alt, favicon, metadatos, fallback HTTP 404 y rechazo de URL remota/traversal/script. Axe automático con tags WCAG 2.2 AA sin violaciones detectadas. Capturas visuales de pantalla completa revisadas en desktop y mobile.
- Doce negativas adversariales del extractor **PASS**: alteración de Word, relación externa, traversal, HTML disfrazado, referencia ambigua, CRC/truncamiento/chunks críticos o compresión maliciosa; fuente SHA verificada.

## Tres auditorías definitivas — alcance de C01-06

**A — PASS, funcional/trazabilidad:** matriz, archivo exacto aceptado por el propietario, fuente autorizada, API pública tipada, header corregido, evidencia de PR y CI sobre SHAs.

**B — PASS, seguridad/aislamiento:** hashes obligatorios, rechazo de assets no autorizados, URL same-origin, pruebas negativas reales, fallback sin estado privado, sin secretos ni escrituras tenant, integración dentro de límites de identidad pública; sin reclamar que se auditaron los módulos privados futuros.

**C — PASS, arquitectura/rendimiento/accesibilidad/UI:** Angular 22, Node 24, diseño responsive real 320/360/768/1440, no deformación de marca, build y 28 Chromium, axe automatizado, inspección visual de capturas, reversibilidad; sin atribuir conformidad de lector de pantalla manual no ejecutado.

**Hallazgos:** no se detectaron P0/P1 en el alcance de C01-06. Avisos Node DEP0040/DEP0169 y actions checkout/upload-artifact con runtime Node20 forzado a Node24; no causaron fallos y requieren revisión futura controlada de dependencias, sin desactivar pruebas.

## Integración y límites de certificación

Se verificó que PR #24 no modificó main ni SOLQARYN, ni configuró VPS, DNS, producción, pagos, secretos o servicios externos. Rollback: revert del merge SHA ac66a8aa39354d5126c3102be99e746452d455ce y del acta documental si correspondiera, recuperando ilustración anterior en header. No hay despliegue productivo.

**Microfase C01-06 CERRADA TÉCNICAMENTE** tras implementación, aceptación visual, auditorías A/B/C y CI postmerge efectivas. **Capítulo C01 NO CERTIFICADO; 0/37 capítulos certificados.** C01-07 habilitada para pruebas de regresión integral, host manipulado, identidad plataforma/tenant, XSS, caché y aislamiento; C01-08 conserva auditoría integral y aceptación expresa antes de C02.

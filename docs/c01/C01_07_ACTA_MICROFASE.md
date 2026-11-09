# CRECISOL — C01-07 · Acta de cierre técnico: regresión, seguridad y aislamiento de identidad

**Fecha de cierre técnico:** 9 de noviembre de 2026, America/Tegucigalpa.  
**Estado:** **C01-07 CERRADA TÉCNICAMENTE**, 7/8 microfases operativas C01. **C01 capítulo NO CERTIFICADO, 0/37 capítulos.** Próxima microfase habilitada únicamente C01-08, no C02.  
**Repositorio exclusivo:** solqaryn-platform/CRECISOL. Sin modificaciones en main, SOLQARYN, producción, VPS, DNS, Cloudflare/R2, compras, cobros ni secretos.

## A. Autoridad y alcance

Fuente: Word canónico `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Verificación efectiva de integridad/estructura a través de `qa/c01_verify_source.py` ejecutado en CI, sin atribuir una lectura de su contenido binario a la sesión de chat. Matriz C01 P04, P06, P07, P14, P25, P28 y T03-R02/R04; contrato [C01_07_REGRESION_Y_SEGURIDAD.md](C01_07_REGRESION_Y_SEGURIDAD.md). Entrada válida C01-06 cerrada PR #24 y #25 con CI post-merge.

Entregable: regresiones automáticas sin nuevos endpoints ni permisos. MySQL 8.4 aislado por socket Unix en Docker `--network none`, datos sintéticos PLATFORM publicada/borrador y TENANT privada, lector SELECT-only; API ASP.NET Core 10 real; Angular 22/Node24 y Chromium conectados al servidor por proxy de loopback sin interceptaciones ni respuestas simuladas para los siete casos E2E. Los veinte casos históricos de UI se reejecutan aparte, con fixtures explícitos; no se mezclan con la integración real. Frontend de producto, backend y esquema MySQL permanecen sin cambios.

## B. Commits, PR y CI del último HEAD

- [PR técnico #26](https://github.com/solqaryn-platform/CRECISOL/pull/26), creado en `feature/c01-07-end-to-end-security` desde dev `f18b06387c5473e43415031f454207c0704dc316`. Último HEAD de implementación **`5477d07aa8911b0e2a776abf27f0c94248b022ba`**.
- [CI final #42576150576](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576150576): **SUCCESS**; [foundation #42576150786](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576150786): **SUCCESS**, mismo HEAD.
- Merge seguro a dev **`745278d5ad3838ad5d63e0a15a88f6d9bafaeb9d`**, mediante `expected_head_sha`, sin force, base dev inalterada al revisar.
- [CI post-merge C01-07 #42576652049](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576652049): **SUCCESS**; [foundation post-merge #42576652052](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576652052): **SUCCESS**, ambas sobre el SHA integrado.

## C. Evidencias ejecutadas y negativas

- Node 24.15.0, `npm ci` desde lock sin lifecycle scripts, `npm audit --omit=dev --audit-level=high`, Angular 22 typecheck y build de producción con presupuestos: **PASS**.
- .NET 10 build API Release: **PASS, 0 errores y 0 advertencias**. Domain **51/51** aserciones y Application **19/19** aserciones positivas/negativas: **PASS**.
- **20/20 Chromium Playwright de regresión C01-05/06** y **7/7 Chromium E2E contra MySQL/API reales**, incluida UI móvil 320 px y análisis axe automatizado WCAG 2.2 AA: **PASS**. Estos análisis automáticos no certifican lectores de pantalla humanos ni WCAG global.
- **37 solicitudes HTTP reales**: 17 baseline + 1 sin publicación + 1 con XSS almacenado + 17 tras restaurar publicación + 1 sin DSN: **PASS**. Host malicioso/forwarded, `X-Tenant-Id`, Authorization falsa, query, métodos de escritura, CORS y rutas admin/tenant/preview inexistentes: rechazo verificado. Borrador y tenant publicados en DB jamás sustituyeron marca PLATFORM; cambio de revisión 2→3→2 sin identidad obsoleta. Lector SQL restringido a SELECT; UPDATE denegado.
- **Auditoría A FINAL PASS:** fuente y trazabilidad contractual, evidencia de ejecución real y alcance C01. **B FINAL PASS:** aislamiento de publicación, denegación de datos tenant, SQL read-only, XSS, host manipulado, CORS, ausencia de DSN, sin secretos. **C FINAL PASS:** Angular/Node/.NET/MySQL, builds reales, regresión 20+7 navegador, responsive y WCAG automático, reversión y rendimiento limitado a presupuestos de build.

## D. Historial de fallos y correcciones

- [#42575542202](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42575542202): falsa detección de `page.route` en comentario de un test; CI FAIL tras integración real aprobada. Corregida lectura de sentencias, sin omitir casos ni bajar política.
- [#42575656621](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42575656621): error de sintaxis del script auditor, corregido y añadido `py_compile` temprano.
- [#42575684544](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42575684544): fallo intermitente de lectura del cuerpo por CDP pese a HTTP 200; mantenido el evento de navegador, validación del DTO con petición HTTP real adicional de Playwright sin mocks. Nuevas ejecuciones PASS.
- No se silenció ninguna prueba ni se alteraron evidencias históricas. Advertencias deprecación Node de algunas GitHub Actions antiguas subsisten como mantenimiento, sin fallo de ejecución.

## E. Límites, revisión y reversión

No se implementaron autorización de empleados, pertenencia o recursos privados, BOLA/IDOR sobre endpoints privados inexistentes, sucursales, MySQL dedicado, CSP del proxy de producción, VPS/R2, mediciones con usuarios reales ni pruebas manuales con lector de pantalla. Estos son contratos/gates de capítulos posteriores: **no afirmar que están certificados**. C01-07 prueba **denegación de rutas no implementadas** y aislamiento del endpoint PLATFORM, no un sistema tenant operativo. Sin hallazgos P0/P1 sin resolver **dentro del alcance probado**; no prometer cero defectos globales.

Diff revisado de PR #26: siete archivos nuevos de QA, workflow, Playwright/config y documentación; sin cambios de negocio/API/DDL/secrets, sin material de SOLQARYN. **Rollback:** revertir PR #26 con nuevo PR (y el acta documental cuando corresponda), sin datos productivos que restaurar. GitHub Actions usa tokens de lectura y runner efímero.

## F. Puerta de continuidad

**C01-07 CERRADA TÉCNICAMENTE tras código+pruebas+3 auditorías finales+merge+CI posterior.** C01: **7/8 microfases**; **0/37 capítulos certificados**. Siguiente permitida **C01-08**, auditorías **integrales** A/B/C de TODO el capítulo, regresiones y acta. La aceptación explícita del propietario es obligatoria **antes de certificar C01 o programar C02**. Este cierre técnico delegado no la sustituye.

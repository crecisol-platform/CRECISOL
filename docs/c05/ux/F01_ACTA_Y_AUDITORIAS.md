# CRECISOL · C05 UX · Acta de cierre técnico F01: inventario y línea base

**Estado del alcance F01: CERTIFICADO TÉCNICAMENTE para INVENTARIO + LÍNEA BASE VISUAL SINTÉTICA.**  
**Fecha de evidencia:** 2026-11-19 (UTC). **F02: BLOQUEADA hasta nueva instrucción y aceptación de la fase siguiente.**  
**No se certifica** diseño premium, nota 9/10, permisos productivos, operación con identidades reales, ni servicios externos.

## 1. Identidad, fuente y cambios permitidos

- Repositorio vigente: `crecisol-platform/CRECISOL`, ID `1450414592` (owner anterior `solqaryn-platform` solo histórico).
- Fuente congelada de C05 antes del rediseño: `f61a79d242288c686a88a7c3dd711f2be9a48c18`, rama `feature/c05-totp-sha1-integrated-qa-20261118` (PR #172).
- Propuesta de F01: rama `feature/c05-ux-f01-inventario-20261119`, [PR #174](https://github.com/crecisol-platform/CRECISOL/pull/174), inicialmente DRAFT, base de PR #172.
- Gate técnico verificado de código/pruebas F01: **SHA `8b45d8663c1e74d94e9ae8760d6379f278b0ff67`**, [run #44638380802](https://github.com/crecisol-platform/CRECISOL/actions/runs/44638380802), **conclusión SUCCESS**.
- El único cambio posterior al SHA auditado es esta acta documental; si se requiere una certificación del nuevo HEAD, verificar su workflow posterior. Ninguna aplicación cambia con esta acta.
- `dev`, `main`, PR #173 (rediseño posterior), `frontend/src/`, `backend/`, `database/`, producción, VPS, SOLQARYN y C06 **sin modificaciones F01**. No mezclar autorizaciones ni documentos maestros.

## 2. Entregables y trazabilidad

| Entregable | Evidencia / valoración |
| --- | --- |
| Inventario Angular/estados | [F01_MATRIZ_INTERFACES_BASE.csv](F01_MATRIZ_INTERFACES_BASE.csv): **65/65** superficies, 17 vistas, 18 estados, 3 complementos y 27 modales, IDs inmutables |
| Formularios | **47** formularios HTML declarados en cuenta/operadores, cotejados con fuentes |
| Contratos API | [F01_CONTRATOS_API_BASE.csv](F01_CONTRATOS_API_BASE.csv): **78** contratos/rutas, 43 operaciones C05 cuenta + 31 plataforma + 4 adicionales |
| Perfiles/estados | `/cuenta`, 12 valores `vista`, 3 estados de sesión en cada realm, 5 pestañas de operadores, roles y acciones pendientes de permiso real |
| Mensajes y transiciones | [F01_MENSAJES_Y_FLUJOS.md](F01_MENSAJES_Y_FLUJOS.md), mapeo a Angular y a backend |
| Método de capturas | [F01_CAPTURAS_Y_GATE.md](F01_CAPTURAS_Y_GATE.md), script `frontend/tests/c05-f01-visual-baseline.spec.ts`, workflow y audit `qa/c05_ux_f01_evidence_audits.py` |
| Hallazgos | [F01_EVIDENCIA_VISUAL_Y_HALLAZGOS_2026_11_19.md](F01_EVIDENCIA_VISUAL_Y_HALLAZGOS_2026_11_19.md), preservando defectos como entrada a F02+ |

La nota inicial es **provisional 4,02/10** en **65/65** entradas, rango **3,0–5,0**, **51 confianza media** y **14 confianza baja**. No se presenta como medición científica, nota visual aprobada ni 9/10. El punto de partida original de 31 muestras aportadas por el propietario promediaba 4,29/10; las 34 adicionales son estimaciones identificadas como tales.

## 3. Auditoría A — Inventario, funcionamiento representado y contratos

**PASS para F01 documental.** El run #44638380802 ejecutó `qa/c05_ux_f01_inventory_audits.py A`: recuento 65/27/47/78, existencia de componentes, coincidencia de contrato backend y diff restringido. Angular/TypeScript compiló con Node 24.15.0.

La prueba visual ejecutó **24/24 casos Playwright Chromium PASS**; los casos producen capturas, no transacciones contra una base de datos real.

## 4. Auditoría B — Privacidad y separación de ámbitos

**PASS para F01 documental y captura ficticia.** El run ejecutó `qa/c05_ux_f01_inventory_audits.py B`, verificando que los fixtures son ficticios, que el catálogo separa las rutas de cuenta de las de plataforma y que se documentan límites de host, token, cookie/CSRF y permisos por comprobar. El QR TOTP/llave, códigos de respaldo y datos de ejemplo son **conocidos, falsos y no reutilizables como credenciales reales**.

**Regresión real histórica del mismo SHA de C05**, observada por lectura de Actions, no repetida por F01:
- [run 44601678730](https://github.com/crecisol-platform/CRECISOL/actions/runs/44601678730) – Angular→API→MySQL y regresiones: SUCCESS.
- [run 44601679085](https://github.com/crecisol-platform/CRECISOL/actions/runs/44601679085) – auditorías A/B/C/D UI/seguridad: SUCCESS.
- [run 44601678999](https://github.com/crecisol-platform/CRECISOL/actions/runs/44601678999) – MFA recuperación HTTP/MySQL: SUCCESS.

**Alcance no verificado por capturas sintéticas:** 401/403 reales, 30 minutos efectivamente vencidos en backend, aislamiento multitenant/IDOR, accesibilidad en lectores de pantalla físicos, WebAuthn físico y recuperación con demora de 24 horas. No se declara que esas pruebas se ejecutaron de nuevo ni que F01 certifica producción.

## 5. Auditoría C — Responsive, evidencia visual e integridad

**PASS para la línea base sintética reproducible** en [run 44638380802](https://github.com/crecisol-platform/CRECISOL/actions/runs/44638380802):

- Artefacto GitHub Actions **#14584253101**, `c05-f01-synthetic-visual-baseline-8b45d8663c1e74d94e9ae8760d6379f278b0ff67`; descargado e inspeccionado directamente.
- **629 entradas ZIP:** **314 PNG**, **314 JSON individuales** y **1 JSON de síntesis**. Todos los PNG poseen JSON; IDs únicos, sin imágenes faltantes.
- Conteo por viewport: **320×720: 85**, **390×844: 76**, **768×1024: 76**, **1440×900: 77**.
- **27 tomas iniciales distintas de los modales originales por viewport**, total **108**. En cada viewport existen además cinco capturas con un modal abierto en estados adicionales. 22 tomas adicionales documentan scroll de diálogos largos.
- Auditor `qa/c05_ux_f01_evidence_audits.py`: `PASS_evidence_integrity_only` con `runnerSha=8b45d866...`, `sourceSha=f61a79d...`, todas `synthetic=true`, **0** mediciones de overflow horizontal superior a 2 px.
- Segunda inspección independiente del ZIP: **0 modales fuera del viewport**, **0 casos de overflow horizontal interno mayor de 2 px**, **0 finales de modal fuera de vista tras desplazamiento al final**. **27** tomas iniciales documentan pie de modal fuera de la primera vista, observación de UX no corregida aquí.
- Hash SHA-256 de la copia ZIP inspeccionada: `24045c3b306050f2c6b842e174ce000b8fb44a722b5c504367497d02c1fbad13`.
- La etapa de integridad corrigió un error histórico inicial del auditor al leer `.last-run.json` y una colisión de nombres que podía sobrescribir capturas. La ejecución final PASS usa nombres con sufijo SHA-256 y no falsifica los runs anteriores que fallaron.

## 6. Hallazgos visuales aceptados como línea base, NO como calidad premium

Las interfaces todavía presentan espacios desproporcionados, filas de botones homogéneas, títulos repetidos, terminología técnica, mensajes genéricos, marca que se quiebra en 320 px, posibles superposiciones de skip-link y formularios cuya parte inferior requiere scroll. Esto **no bloquea certificar que el inventario y sus capturas son fieles**, pero **sí impide calificar el producto 9/10** o presentar el diseño C05 como premium.

La calidad, accesibilidad física y aceptación visual del diseño mejorado corresponden a las fases posteriores. Las notas de confianza baja se conservan explícitamente como aproximaciones, no como observaciones de backend.

## 7. Resolución de F01

**GO: F01 certificada técnicamente en su alcance documental, de inventario trazable y línea base visual sintética**, respaldada por auditorías A/B/C de F01, 24 Playwright PASS, Angular PASS, ZIP inspeccionado y regresiones históricas de la misma fuente.

**NO-GO: F02 permanece bloqueada**. Esta acta no certifica una puntuación 9/10, no reabre C06 ni declara pruebas reales nuevas de privilegios/tenants/sesiones. No fusionar automáticamente PR #174 ni PR #173; conservar el PR F01 como evidencia de revisión independiente. La certificación de alcance F01 no sustituye la aceptación del diseño mejorado por el propietario.

---

## 8. Adenda de conciliación F01 ↔ F06 — 22-11-2026

**Alcance:** complemento documental a la certificación técnica histórica de inventario y línea base sintética F01. No certifica nota comercial, producción, nuevos permisos ni fusión. La matriz original sigue inalterada y representa el estado previo al rediseño.

**Fuentes comprobadas:**
- F01: PR #174, fuente congelada f61a79d242288c686a88a7c3dd711f2be9a48c18, matriz Git blob a1ff218baf489d69104145048f94e7ff9ee48f73.
- F06: PR #179, código SHA d539b75d13cde64c8ea3279d33ab35b6349c1adf sin fusionar a dev.
- [Inventario de 65 × 5 #45032597168](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597168): SUCCESS; [artefacto individual #14758472442](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597168/artifacts/14758472442) con imágenes PNG/JSON sintéticas identificables por inventoryIds.
- [E2E y frontend #45032597178](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597178): dos trabajos SUCCESS, 344 Playwright y 340 aserciones Angular → .NET 10 → MySQL 8.4, 18 migraciones con reversión; válido para ese SHA, no automáticamente para commits posteriores.

**Comparación:** coinciden los 65 ID, títulos, tipos y componentes Angular (17 vistas, 18 estados, 3 complementos, 27 modales). Existe una discrepancia histórica de ruta:
- **C05-F01-057:** la ruta antigua /cuenta?vista=perfil [empty] NO demuestra historial vacío. El código F06 exige /cuenta?vista=historial [empty], escena account-historial-empty-* y encabezado «Sin actividad disponible».
- **C05-F01-014:** exige condiciones realmente cargadas en la escena account-terms-current-loaded-* con encabezado «Condiciones qa-f01»; no acepta la portada genérica de login. La ruta catalogada no cambia, pero sí la prueba semántica.

No se reescriben capturas históricas ni se atribuyen falsamente a estas dos escenas. Las 65 celdas internas de evidencia de F06 todavía citan como antecedente el SHA 6384c545...; la matriz cruzada referencia de modo explícito el run contemporáneo #45032597168 y artefacto #14758472442.

**Fuente cruzada:** [Matriz reconciliada de 65 filas](F01_MATRIZ_RECONCILIADA_F06_2026_11_22.csv). Incluye rutas original/vigente, fuente Angular, contratos, riesgos, identificador de captura, dos CI, puntuación exploratoria antigua y separación estricta de nota final/dictamen.

**Auditor automatizado:** qa/c05_ux_f01_reconciliation_audits.py. Lee desde Git el SHA F06 exacto, valida cuatro blobs inmutables, las 65 correspondencias, el único cambio de ruta 057, las protecciones de escenas 014/057 y que los 65 dictámenes comerciales finales sean PENDIENTE. La CI F01 debe ejecutarlo en el HEAD nuevo antes de afirmar aprobado este complemento.

**Tres auditorías de este cambio:**
- A, trazabilidad: 65 IDs, 4 clases de superficie, correspondencia de rutas, Angular, API y SHA.
- B, seguridad y privacidad: límites de permisos y datos originales preservados; no se conceden autorizaciones por una captura ni se ejecutan ataques reales nuevos contra tenants en este paso documental.
- C, UX y QA: evidencia de inventario sintético individualizada, dos guardas semánticos, distinción entre notas exploratorias y nota final; no equivalencia con lector de pantalla físico ni inspección integral.

**Dictamen:** F01 históricamente certificada en alcance de inventario y baseline; conciliación de ramas incorporada a revisión y su prueba nueva sujeta a CI del HEAD final. Aceptación visual comercial **0/65**: todos los campos finales vacíos y los 65 dictámenes PENDIENTE. Media exploratoria anterior de 9.18/10 no habilita compra. PR #174 y #179 DRAFT; F07/C06 bloqueados, sin cambiar dev/main, frontend de producto, backend, base de datos, Word/PDF, producción ni SOLQARYN.

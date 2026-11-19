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

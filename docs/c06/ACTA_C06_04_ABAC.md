# CRECISOL — C06-04: acta institucional ABAC, propiedad e IDOR

**Fecha de elaboración:** 24-11-2026, America/Tegucigalpa. **ESTADO INSTITUCIONAL VIGENTE: C06-04 CERTIFICADA ÚNICAMENTE EN SU ALCANCE TÉCNICO, 9.5/10.** La condición inicial de esta acta se cumplió: código C06-04 PRE/POST A/B/C/E2E 4/4 SUCCESS ([#45522703023](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703023), [#45524471431](https://github.com/crecisol-platform/CRECISOL/actions/runs/45524471431)); C05 POST 2/2 SUCCESS; acta PR #202 PRE/POST A/B/C 3/3 SUCCESS ([#45528830322](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528830322), [#45528982856](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528982856)), más regresión C04-12 transversal SUCCESS [#45528982862](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528982862). Se conserva abajo la cronología del estado condicionado, sin atribuir permisos futuros ni operaciones productivas.

## 1. Autoridad y límites

Repositorio CRECISOL ID original 1450414592, ubicación autorizada crecisol-platform/CRECISOL, redirigida desde solqaryn-platform/CRECISOL. Documento Maestro Único Word SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c; AGENTS.md, Skill de gobernanza CRECISOL, docs/MASTER_REFERENCE.md y registro institucional vigente. C06-01 inventario 80 operaciones HTTP + 12 puertos internos + 28 acciones futuras no concedibles. C06-02 matriz 10 roles y 108 acciones; roles preparados NO conceden permisos. C06-03 identidad, sesión, tenant y membresía verificados en transacción MySQL, certificado en alcance mediante PR #199/#200.

**Entregable real de C06-04:** proteger la operación HTTP productiva existente POST /api/v1/identity/private/read, acción canónica **identity.private.read**, mediante ABAC central en backend: actor autenticado, rol de membresía, acción fijada por servidor, tenant, sucursal, propiedad de expediente, grant RESOURCE_READ explícito y, para empleados no propietarios con acceso de sucursal, asignación exacta de caso. La acción futura cases.read continúa NO registrada y NO concedible. No se afirma un motor ABAC operativo sobre rutas todavía inexistentes.

## 2. Implementación y separación arquitectónica

**PR técnico #201:** https://github.com/crecisol-platform/CRECISOL/pull/201, HEAD PREMERGE a27f1ec5e66355abf83d2b16f058f9b4ee8b405e. Fusionado exclusivamente en dev SHA **31b8f58fcdb5f6f134a0b663213976f285371653**. main permanece b794181148d2c5e36c1c88127f19a8a9082acff5.

- Domain: backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs, política pura y centralizada; todo requisito no satisfecho devuelve DENY. Roles PLATFORM, SuperAdmin, tenant-custom no reciben acceso automático.
- Infrastructure: backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs; consulta parametrizada dentro de transacción C05 bloqueando recurso, grant y asignación con FOR SHARE, verificando revisión y tiempo UTC de MySQL. No lee rol, actor, tenant ni acción del navegador.
- Integración mínima: MySqlOperationalIdentityFlow.Authorized.cs usa el contexto C06-03 antes de invocar ABAC y devolver una referencia privada. No crea rutas nuevas ni modifica frontend.
- SQL: database/migrations/020_c06_case_abac.sql, tabla aditiva c06_case_resource_assignment; UNIQUE, tres FK compuestas por tenant, vigencia máxima 30 días, revocación irreversible y denegación de borrado/reparenting. Cero seed o concesión por migración. database/operations/c06_04_local_runtime_role.sql concede **SELECT ONLY** al rol de laboratorio. Reversión 020_c06_case_abac.down.sql únicamente en MySQL desechable.
- QA: 64 aserciones .NET, 61 mutaciones estáticas de guardas detectadas, 22 ataques SQL reales rechazados, 351 comprobaciones E2E Angular→API→MySQL, E2E histórico C05 sin flag y auditorías A/B/C independientes. No hay entorno productivo, VPS, datos reales, pagos o escritura en SOLQARYN.

### Política precisa
1. Miembro propietario del expediente: requiere además grant individual de recurso vigente, exacto, no revocado.
2. OWNER/ADMIN de empresa: lectura solo si su rol real está vigente y tiene grant explícito de sucursal; no hereda acceso PLATFORM.
3. Empleado, encargado o lector de sucursal: un grant amplio de sucursal nunca basta para un expediente de compañero; requiere asignación **por tenant+actor+branch+resource+identity.private.read** única, activa, temporal y no revocada.
4. Recurso de otro tenant, sucursal ajena, propietario distinto sin asignación, grant repetido, vencido o revocado, rol desconocido, acción distinta, SELECTOR falsificado o SQL sin permiso: DENY genérico, sin enumeración.

**Importante:** la tabla verifica asignaciones ya existentes, pero **no existe API que las emita en el producto**. Cualquier futuro gestor de asignaciones deberá comprobar actor emisor, techo de delegación, MFA step-up, segregación de tareas y bitácora ANTES de activarse; un FK del aprobador no sustituye autorización. Reasignaciones posteriores a revocación exigen modelo/versionado explícito de la fase que implemente emisión. No se pretende desplegarlo en esta etapa.

## 3. Trazabilidad y pruebas reales

| Puerta | Evidencia y resultado |
| --- | --- |
| C06-04 PREMERGE A/B/C/D — SHA a27f1ec... | [GitHub Actions #45522703023](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703023): **4/4 SUCCESS**, incluidas 351 aserciones E2E |
| Contratos Domain .NET 10 | **64 aserciones PASS** (IDOR mismo tenant/branch, acción, dueño, rol, TTL y revocación) |
| Mutaciones estáticas QA | **61 guardas adversariales detectadas**; no confundir con ataques HTTP |
| MySQL 8.4 real | **22 negativas rechazadas**, 3 FK tenant-scoped, UNIQUE/estado, rol SELECT-only y rollback |
| E2E C06-04 | **9 escenarios HTTP nuevos PASS**: denegación mismo negocio/sucursal sin grant o sin asignación, conflictos de grant DENY, acceso al caso exacto ALLOW, caso hermano/otra sucursal/otro tenant DENY, revocación DENY, grant propietario preservado |
| Reversión E2E | **PASS**: retirar primero 020, verificar preservación de expedientes y membresías C05, ejecutar 18 rollback C05 y comprobar esquema vacío |
| C05 E2E histórico PREMERGE | [#45522703090](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703090): **2/2 SUCCESS**; regresiones funcionales y Chromium |
| Regresiones de capítulos | C03-10 [#45522703156](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703156) SUCCESS; C04-12 [#45522703044](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703044) SUCCESS; C06-03 [#45522703010](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703010) A/B/C SUCCESS; C05 SHA1 [#45522703015](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703015) A/B/C SUCCESS |

**Evidencia histórica NO ocultada:** los primeros workflows fallaron por dos aserciones erradas del arnés (el rol tenant-custom estaba excluido correctamente y una mutación solo retiraba una referencia). Al corregirlos A/B/C aprobó. El primer E2E registró 348 aserciones correctas pero FAIL por desmontar C05 antes de su dependiente C06 (MySQL 3730). Se corrigió el orden y el E2E definitivo terminó **351 PASS** con rollback completo. La acción inicialmente elegida cases.read era futura; se corrigió a identity.private.read, la acción C06-01 de la ruta real, antes del PASS definitivo. Todos los fallos previos continúan trazables, sin renombrarse exitosos.

## 4. Tres auditorías institucionales de esta acta

**A — Funcionalidad y trazabilidad:** identidad de repositorio, hash Word maestro, 80/12/28 + 10/108, acción existente, diff de 14 archivos, hashes PRE/POST del código, evidencia A/B/C/D y no concesión futura.

**B — Seguridad/privacidad:** pruebas reales y negativas, SQL con aislamiento tenant/sucursal/actor, paso exacto de ABAC antes de leer referencia privada, rollback, ausencia de permisos PLATFORM/SuperAdmin implícitos, no filtrar GUID/token/PII.

**C — Arquitectura, compatibilidad y UX:** Domain puro, Infrastructure SQL transaccional, .NET10 Release -warnaserror, Angular22 types/build, Chromium, regresiones C05, sin migración sobre cuentas reales. No se modificaron interfaces: **calificación visual N/A**, pero exigencia >=9.5/10 para seguridad, funcionalidad, arquitectura, rendimiento razonado y documentación. No se afirma benchmark en producción.

## 5. Juicio comercial, gates finales y reversión

**Dictamen final y criterio como comprador técnico: 9.5/10 — ACEPTADA Y CERTIFICADA SOLO EN SU ALCANCE.** Se verificó la puerta antes condicionada: [PR #201 POSTMERGE A/B/C/E2E #45524471431](https://github.com/crecisol-platform/CRECISOL/actions/runs/45524471431) **4/4 SUCCESS** en SHA 31b8f58fcdb5f6f134a0b663213976f285371653; [C05 POSTMERGE #45524470905](https://github.com/crecisol-platform/CRECISOL/actions/runs/45524470905) **2/2 SUCCESS**; [acta #202 PRE A/B/C #45528830322](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528830322) y [POST A/B/C #45528982856](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528982856) **3/3 SUCCESS** cada una sobre SHA final de acta 43bc672f005e88b12f777bc4520a4687539be705. [C04-12 transversal #45528982862](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528982862) **SUCCESS**. No se identificaron P0/P1 atribuibles al cambio en las pruebas y auditorías ejecutadas. Este juicio no es una promesa de seguridad absoluta ni certificación de producción.

Al superar todas las puertas: C06-01/02/03/04 **4/11 microfases certificadas en sus alcances** y 5/37 capítulos completos; C06 global NO CERTIFICADO; C06-05 solo preparación independiente, C07 bloqueado. Antes del cierre documental se registraban 3/11; **puertas documentales satisfechas, estado vigente 4/11**. El motor no concede permisos de las 28 acciones futuras y no valida operaciones HTTP no implementadas.

**Rollback:** revertir PR técnico #201 y esta acta con análisis de dependencias; en MySQL descartable retirar 020 antes de cualquier reversión C05 y verificar conservación de datos. Jamás ejecutar rollback en datos de propietario o producción en esta etapa.


## 6. Informe final detallado para el propietario — cierre C06-04

### 6.1 Objetivo y dictamen verificable
**Requisito:** negar accesos IDOR cuando una persona solicita expedientes que no le pertenecen, incluso si otra persona trabaja en la misma empresa y sucursal. **CUMPLIDO dentro del ámbito implementado:** la ruta existente `POST /api/v1/identity/private/read` utiliza una decisión ABAC del backend basada en identidad C06-03, tenant, membresía, rol comprobado, acción `identity.private.read`, sucursal, propietario, grant específico y, para empleados no propietarios, asignación individual vigente. Un UUID de la petición solo es selector, nunca fuente de autorización. **No es una cobertura certificada de las 80 rutas ni de expedientes futuros que aún no tienen API.**

### 6.2 Inventario exacto de lo entregado
El [PR técnico #201](https://github.com/crecisol-platform/CRECISOL/pull/201) modificó **14 archivos autorizados**, agrupados de esta forma:

| Capa / archivos | Resultado |
| --- | --- |
| Domain: `backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs` | Política central pura, default deny, actor/acción/tenant/branch/owner/assignment |
| Infrastructure: `backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs`, `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs` | Lectura transaccional con `FOR SHARE`, resolución desde filas SQL y control antes de exponer referencia privada |
| Database: `database/migrations/020_c06_case_abac.sql`, `020_c06_case_abac.down.sql`, `database/operations/c06_04_local_runtime_role.sql` | Tabla aditiva de asignaciones por recurso, 3 FK compuestas, TTL finito, revocación irreversible, SELECT-only runtime y reversión |
| Pruebas: proyecto `backend/tests/Crecisol.C06Abac.ContractsTests/`, `database/tests/c06_04_abac_mysql.sh` | 64 aserciones .NET y 22 ataques SQL MySQL 8.4 rechazados |
| QA/E2E: `qa/c05_operational_flow_e2e.mjs`, `qa/c06_03_context_audits.py`, `qa/c06_04_abac_audits.py`, `.github/workflows/c06-04-abac-ownership.yml` | E2E real, 61 controles de seguridad por mutación, auditorías A/B/C y regresión heredada |
| Especificación: `docs/c06/C06_04_ESPECIFICACION.md` | Contrato, amenazas, condiciones, límites y reversión |
| Acta posterior PR #202 | Cinco archivos exclusivamente documentales/QA, sin nuevo código productivo |

**Frontend: cero pantallas modificadas. API: cero rutas nuevas.** No se ejecutó migración sobre cuentas reales ni se habilitó gestión de asignaciones, VPS o cobros.

### 6.3 Matriz funcional de aceptación — mismo negocio, otros ámbitos
| Caso probado | Resultado exigido y observado |
| --- | --- |
| Usuario propietario + grant exacto vigente para su recurso | **ALLOW** |
| Empleado A, recurso de empleado B, mismo tenant y sucursal, sin grant | **DENY** |
| Empleado A con permiso amplio de sucursal, sin asignación exacta para el expediente B | **DENY** |
| Empleado A con grant y asignación exacta, activa, no revocada para expediente B | **ALLOW** únicamente para B |
| Empleado A intenta otro expediente del mismo compañero/sucursal | **DENY** |
| Tenant o sucursal ajenos, ID o host falsificados | **DENY** |
| Grants duplicados o ambiguos y acción no coincidente | **DENY** |
| Asignación revocada, vencida, de otro actor o recurso | **DENY** |
| Rol PLATFORM/SuperAdmin o personalizado sin autoridad | **DENY** |

La ruta responde genéricamente al rechazo, sin transformar el selector del navegador en permiso ni devolver la referencia del expediente denegado.

### 6.4 Evidencia real A/B/C y regresiones — por SHA
| Prueba / puerta | Enlace y resultado |
| --- | --- |
| Técnica PREMERGE | [#45522703023](https://github.com/crecisol-platform/CRECISOL/actions/runs/45522703023) — A/B/C/E2E **4/4 SUCCESS**, 351 aserciones E2E, 9 escenarios nuevos, 64 .NET, 22 SQL |
| Técnica POSTMERGE en SHA `31b8f58fcdb5f6f134a0b663213976f285371653` | [#45524471431](https://github.com/crecisol-platform/CRECISOL/actions/runs/45524471431) — **4/4 SUCCESS** |
| C05 operación POSTMERGE | [#45524470905](https://github.com/crecisol-platform/CRECISOL/actions/runs/45524470905) — **2/2 SUCCESS** |
| Acta institucional PREMERGE | [#45528830322](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528830322) — A/B/C **3/3 SUCCESS** |
| Acta institucional POSTMERGE SHA `43bc672f005e88b12f777bc4520a4687539be705` | [#45528982856](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528982856) — **3/3 SUCCESS** |
| Regresión integral C04-12 del SHA documental | [#45528982862](https://github.com/crecisol-platform/CRECISOL/actions/runs/45528982862) — **SUCCESS** |
| Controles previos C06-03 del SHA técnico | [#45524471121](https://github.com/crecisol-platform/CRECISOL/actions/runs/45524471121) — A/B/C **3/3 SUCCESS** |

**Auditoría A (trazabilidad y funcionalidad):** acción existente identificada `identity.private.read`, Documento Maestro y catálogos conservados, diff autorizado y evidencias por SHA.

**Auditoría B (seguridad/privacidad):** IDOR en mismo tenant/sucursal, otro tenant, otros roles/actores, recursos y grants ajenos, expiración, revocación, SQL tenant-scope y ausencia de concesiones nuevas, con pruebas negativas reales.

**Auditoría C (arquitectura y regresiones):** Domain separado de Infrastructure, `.NET 10 -warnaserror`, Angular 22 types/build, Chromium, C05/C06-03 y rollback limpio. **No se midió throughput o latencia en producción.**

### 6.5 Errores detectados y correcciones sin ocultar intentos fallidos
1. Primera auditoría exigía por equivocación el texto `tenant-custom` en la lista positiva; corregida para verificar expresamente su exclusión.
2. Primera mutación retiraba solo una ocurrencia del control; corregida para detectar su ausencia total.
3. La política usó inicialmente la acción futura `cases.read`; sustituida por la canónica y ya existente `identity.private.read` sin activar el catálogo futuro.
4. Primer E2E verificó 348 aserciones pero finalizó en **FAILURE MySQL 3730** al desmontar C05 antes de la tabla dependiente C06. Se corrigió el orden: revertir primero C06-04, verificar preservación de recursos/membresías y después ejecutar los 18 rollbacks C05. **E2E definitivo: 351 PASS**. Ningún FAIL histórico se reetiqueta como PASS.

### 6.6 Evaluación como comprador / calidad estricta
**Calificación técnica del alcance: 9.5/10 — SÍ aceptaría/compraría esta entrega limitada de C06-04.** Fundamentación: control ABAC verificable en backend, pruebas negativas intraempresa y cross-tenant reales, aislamiento SQL, revocación, C05 intacto y evidencia PRE/POST por SHA. **No es una puntuación estadística ni una nota de toda CRECISOL.** Calidad visual: **N/A**, porque ninguna interfaz fue rediseñada; no declarar 9.5/10 visual. No se identificaron defectos P0/P1 en el cambio auditado.

**Límites/riesgos residuales expresos:** la tabla de asignaciones tiene infraestructura de consulta, pero no un proceso productivo para emitirlas, renovarlas o auditarlas como delegaciones; habrá que diseñar emisión autorizada, aprobación, MFA step-up, techo de delegación y bitácora en su futura fase, sin concederlas prematuramente. `cases.read` y las otras acciones futuras no están habilitadas. Ninguna afirmación sobre todos los casos de uso, autorización de recursos futuros, rendimiento productivo o despliegue.

### 6.7 Reversión, ramas y continuación
- `dev` integra [PR #201](https://github.com/crecisol-platform/CRECISOL/pull/201) y [PR #202](https://github.com/crecisol-platform/CRECISOL/pull/202) (SHA de acta aprobado `43bc672f005e88b12f777bc4520a4687539be705`). `main` permanece sin cambios respecto al SHA verificado `b794181148d2c5e36c1c88127f19a8a9082acff5`.
- Rollback evaluado solamente sobre MySQL 8.4 descartable: quitar tabla C06 dependiente primero, comprobar preservación C05; cualquier reversión en datos del propietario exige evaluación/backup separado. Revertir PR técnicos/documentales de forma controlada, no eliminar datos a ciegas.
- **C06-01/02/03/04 = 4/11 CERTIFICADAS cada una en su alcance; 5/37 capítulos completos; C06 global NO CERTIFICADO.** La siguiente C06-05 (step-up para operaciones sensibles) solo queda habilitada para iniciar su propia preparación, sin asumir permisos por estar documentada.

### 6.8 Contrato obligatorio de cierre futuro
Toda microfase restante C06 se entregará con este nivel de detalle: objetivo real, alcance/cobertura negativa, archivos exactos, controles de tenant y PII, resultados positivos/negativos, auditorías A/B/C y E2E/CI con SHA y URLs, incidentes y correcciones, nota técnica y visual separadas, criterio explícito de compra, riesgos residuales, rollback, estado de `dev`/`main`, certificación precisa y siguiente gate. **No sustituir este reporte con un simple “CERTIFICADO”.**

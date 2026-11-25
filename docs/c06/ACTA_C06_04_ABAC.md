# CRECISOL — C06-04: acta institucional ABAC, propiedad e IDOR

**Fecha:** 24-11-2026, America/Tegucigalpa. **Estado de esta acta:** CANDIDATA / CIERRE CONDICIONADO. Se vuelve efectiva únicamente si el POSTMERGE técnico y sus propias auditorías A/B/C PREMERGE y POSTMERGE terminan completed/success sobre sus SHAs reales. No declarar certificación por haber escrito este documento.

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

**Dictamen técnico preliminar: 9.5/10 con evidencia PREMERGE.** Solo podría formalizarse la aceptación final si el workflow POSTMERGE del PR #201 finaliza 4/4 SUCCESS en SHA 31b8f58fcdb5f6f134a0b663213976f285371653, no existen P0/P1 atribuibles al alcance, el E2E C05 POSTMERGE resulta SUCCESS y esta acta obtiene sus propias auditorías A/B/C PRE/POST sobre SHAs finales. **Hasta entonces C06-04 NO CERTIFICADA.**

Al superar todas las puertas: C06-01/02/03/04 **4/11 microfases certificadas en sus alcances** y 5/37 capítulos completos; C06 global NO CERTIFICADO; C06-05 solo preparación independiente, C07 bloqueado. Antes del cierre documental vigente: 3/11. El motor no concede permisos de las 28 acciones futuras y no valida operaciones HTTP no implementadas.

**Rollback:** revertir PR técnico #201 y esta acta con análisis de dependencias; en MySQL descartable retirar 020 antes de cualquier reversión C05 y verificar conservación de datos. Jamás ejecutar rollback en datos de propietario o producción en esta etapa.

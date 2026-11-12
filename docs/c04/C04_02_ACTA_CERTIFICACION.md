# CRECISOL — C04-02 · Acta institucional de implementación SHARED (eficacia condicionada)

**Fecha:** 12 de noviembre de 2026, Honduras. **Repositorio original:** `crecisol-platform/CRECISOL` (ID GitHub `1450414592`). **Rama receptora:** `dev`. **Capítulo:** C04, microfase técnica **C04-02 (2/12)**. **Estado del presente documento al abrir su PR:** PROPUESTA DE CERTIFICACIÓN CONDICIONADA: no equivale a certificación hasta superar la propia CI documental PREMERGE y POSTMERGE, integrarse en `dev` y registrar los IDs de la evidencia en el PR.

## 1. Autoridad y puerta de entrada

El **Documento Maestro Único** autorizado sigue siendo `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` (PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`). C04: 72 párrafos originales, texto SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`. Las ocho puertas normativas `MAE-C04-01..08` NO equivalen a doce microfases técnicas C04.

C03: certificado 10/10, tres de 37 capítulos; C04-01: certificado en el alcance limitado de contratos internos (1/12); PR #85: C04-02 **preparación** validada con fixture sintético MySQL aislado, no migración. Ninguna evidencia precedente fue reinterpretada como implementación.

## 2. Objeto, alcance estricto y fusión técnica

El [PR técnico #86](https://github.com/crecisol-platform/CRECISOL/pull/86) se fusionó **solo a `dev`**, HEAD PREMERGE `e3b450a80c783c226f11aaca2f62f57f3df7c3c6`, SHA de integración `1377e83aac652195b8e2daf29968689e4015edf6`. Su diff de **12 archivos** se limita a:

- `database/c04_02_shared_schema.sql`: 4 tablas SHARED InnoDB `tenant/branch/resource/event`, PK/FK compuestas, índices de ámbito, `RESTRICT`, CHECK y **5 triggers** de pertenencia inmutable y eventos append-only. No se ejecuta como migración productiva y no crea tenants reales.
- `backend/src/Crecisol.Application/Tenancy/ReadSharedTenantResource.cs`: contrato tipado de ámbito validado por un futuro backend, autorización **antes de leer** y rechazo de respuestas mal asignadas; no recibe tenant/branch del cliente.
- `backend/src/Crecisol.Infrastructure/Tenancy/MySqlSharedTenantResourceReader.cs`: lectura SQL con `WHERE tenant_id AND branch_id AND resource_id` vinculados como parámetros `BINARY(16)`. `MySqlRfc4122Guid.cs`: byte order RFC 4122 sin mixed endian.
- `backend/tests/Crecisol.SharedTenancy.ContractsTests/`, `database/tests/c04_02_application_schema_smoke.sh`, `database/tests/c04_02_backend_reader_smoke.sh`, `qa/c04_02_implementation_audits.py`, workflow dedicado y matriz documental: pruebas reales y auditorías A/B/C.
- `qa/c04_01_contracts_audits.py`: actualización restringida del control histórico C04-01, que ahora acepta únicamente el DDL C04-02 si existen su workflow, test y documentación, pero sigue rechazando rutas privadas o esquemas no autorizados.

**No se cambiaron** `frontend/`, `backend/src/Crecisol.Api/Program.cs`, contratos C01/C02/C03 originales, base productiva, maestro Word/PDF, `main`, SOLQARYN, VPS, DNS, Cloudflare, cobros ni configuraciones productivas. No hay nuevas rutas, middleware privado ni registros DI del lector.

## 3. Auditorías y resultados técnicos reales — PREMERGE

Todas las ejecuciones siguientes corresponden a HEAD `e3b450a80c783c226f11aaca2f62f57f3df7c3c6`. **Cinco workflows aplicables, cinco SUCCESS**:

| Control real | Run PREMERGE | Resultado |
| --- | --- | --- |
| C04-02 implementación A/B/C, .NET 10 y MySQL 8.4 | [43264211958](https://github.com/crecisol-platform/CRECISOL/actions/runs/43264211958) | SUCCESS |
| C04-02 regresión de preparación relacional MySQL | [43264211855](https://github.com/crecisol-platform/CRECISOL/actions/runs/43264211855) | SUCCESS |
| C04-01 contratos Domain/Application y negativas | [43264211733](https://github.com/crecisol-platform/CRECISOL/actions/runs/43264211733) | SUCCESS |
| C03-10 regresión integral Angular/.NET/MySQL/Chromium | [43264211719](https://github.com/crecisol-platform/CRECISOL/actions/runs/43264211719) | SUCCESS |
| Foundation | [43264212003](https://github.com/crecisol-platform/CRECISOL/actions/runs/43264212003) | SUCCESS |

**Auditoría A — trazabilidad:** SHA de Word/PDF, 72 párrafos del C04, requisitos de claves compuestas y límites multiempresa, diff de alcance y estructura de puertos.  
**Auditoría B — seguridad:** **76 mutaciones adversariales rechazadas** en el control de implementación; **12 pruebas SQL positivas / 16 negativas**, incluido reparenting de empresa A a B con *destino existente y FK válida*, cambio indebido de sucursal, actualización/borrado de eventos, valores nulos/duplicados; cuenta `SELECT` que rechaza UPDATE/DELETE/DROP/enumeración del catálogo. Sin RLS implícito.  
**Auditoría C — arquitectura:** compilaciones reales `Release -warnaserror` de .NET 10, **38 aserciones de contratos sin DB y 43 con MySQL 8.4 real**, codec GUID, scope y fallos; evidencias A/B/.NET/MySQL del **mismo SHA/run**. Regressiones C04-01 y C03-10 separadas.

El lector se probó con dos tenants sintéticos en BD descartable y puerto **solo loopback**. La autenticación real de un actor se simuló deliberadamente con un port falso; **no** se certifica acceso HTTP privado o BOLA/IDOR productivo.

## 4. CI posterior del PR #86 (conclusión sujeta a verificación final)

**Commit integrado:** `1377e83aac652195b8e2daf29968689e4015edf6`. Siete workflows *aplicables* programados para este SHA (no confundir con checks condicionales `skipped`):

| Control | Run POSTMERGE |
| --- | --- |
| C04-02 implementación A/B/C .NET/MySQL | [43269467430](https://github.com/crecisol-platform/CRECISOL/actions/runs/43269467430) |
| C04-02 preparación MySQL/FK | [43269467450](https://github.com/crecisol-platform/CRECISOL/actions/runs/43269467450) |
| C04-01 regresión de contratos | [43269467262](https://github.com/crecisol-platform/CRECISOL/actions/runs/43269467262) |
| C03-03 Application | [43269467288](https://github.com/crecisol-platform/CRECISOL/actions/runs/43269467288) |
| C03-04 Infrastructure | [43269467221](https://github.com/crecisol-platform/CRECISOL/actions/runs/43269467221) |
| C03-10 regresión integral | [43269467251](https://github.com/crecisol-platform/CRECISOL/actions/runs/43269467251) |
| Foundation | [43269467303](https://github.com/crecisol-platform/CRECISOL/actions/runs/43269467303) |

**Puerta:** cada uno debe alcanzar `completed/success` para la **misma versión fusionada**. Un fallo, cola, cancelación o skipped de control obligatorio BLOQUEA el acta, sin excepción.

## 5. Desviaciones históricas y riesgos fuera de este alcance

Fallos anteriores de configuración de workflow YAML, guard C04-01, validador de cadena literal y escenarios relacionales insuficientes se corrigieron por **commits posteriores** y nuevas ejecuciones exitosas. El histórico conserva resultados fallidos originales; no se borran, cancelan artificialmente ni se presentan como PASS.

**C04-02 NO implementa:** autorización productiva por identidad/membresía/rol/acción, asociación de propietario real, HTTP/BOLA/IDOR, DB dedicada, selector de DSN, aprovisionamiento, migración de tablas productivas, rutas de medios, caches, colas, backups o rendimientos p95 de un SaaS en producción. Estos controles corresponden a C04-03..12 y no se consideran cubiertos. La autorización es **P0 obligatorio antes de activar cualquier ruta privada**; como no existe ruta privada ni DI de lectura, no se libera exposición insegura en este corte.

**Reversibilidad:** revertir exclusivamente el squash SHA `1377e83aac652195b8e2daf29968689e4015edf6` en `dev`, reevaluar tests; ningún esquema activo o dato productivo requiere rollback porque no se aplicó en producción.

## 6. Dictamen institucional condicionado

La petición explícita del propietario de continuar hasta certificar autoriza proceder **solo si** se verifican todos los gates de seguridad y ejecución. La presente acta entra en vigor únicamente tras registrar: (a) **7/7 SUCCESS** del SHA POSTMERGE técnico; (b) revisión 0 P0/P1 *en el alcance interno implementado*; (c) fusión de su **propio PR documental** a `dev` con CI PREMERGE/POSTMERGE `SUCCESS` y SHA/run IDs en comentario permanente.

Una vez cumplido, el dictamen exacto será: **C04-02 CERTIFICADA EN SU ALCANCE DE MODELO RELACIONAL SHARED Y LECTURA INTERNA NO EXPUESTA (2/12)**, **C04-03 HABILITADA SOLO PARA PREPARACIÓN**, **C04 como capítulo NO CERTIFICADO**, total **3/37 capítulos**. No certificar identidad real, administración tenant, DB dedicada ni acceso privado end-to-end.

Mientras falte una condición: **C04-02 PENDIENTE; C04-03 BLOQUEADA**. El estado histórico de preparación del PR #85 permanece intacto y no sustituye esta acta.
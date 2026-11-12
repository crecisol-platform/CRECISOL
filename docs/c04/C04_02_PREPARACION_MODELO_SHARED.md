# CRECISOL — C04-02 · Preparación del modelo relacional SHARED tenant-aware

**Fecha:** 12 de noviembre de 2026 (Honduras). **Repositorio:** `crecisol-platform/CRECISOL` (ID 1450414592). **Base:** `dev` SHA `9cd099cc38549f6c6dc321c0e200b36dc9308075`. **Estado:** PREPARACIÓN DE MODELO Y PRUEBA SINTÉTICA, **NO IMPLEMENTADA NI CERTIFICADA** como persistencia productiva.

**Puerta anterior:** C04-01 certificada **solo como contratos internos** por [PR #83](https://github.com/crecisol-platform/CRECISOL/pull/83) y [acta del PR #84](https://github.com/crecisol-platform/CRECISOL/pull/84#issuecomment-6827626750): 7/7 CI POSTMERGE SUCCESS del SHA exacto. Total **3/37 capítulos** certificados (C01/C02/C03); C04 completo NO certificado. **Autorización actual:** avanzar con la siguiente fase, C04-02, empezando únicamente por su preparación de seguridad y diseño. No se habilita C04-03.

## 1. Documento Maestro Único y alcance

La fuente normativa es `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. El inventario independiente `qa/c04_01_normative_inventory.py` verifica el texto canónico de C04: 72 párrafos (1735–1806), SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`.

**Requisito preciso de C04-02:** `Modelo relacional shared tenant-aware`; prueba positiva `FK e índices compuestos probados en MySQL 8.4`; negativa `Rechazar referencias cruzadas A a B`, según `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`. El Word exige **FK compuestas con tenant**, separación tenant y sucursal y que `tenant_id` aportado por navegador NUNCA autorice accesos. Las ocho puertas `MAE-C04` del Word (extracción/diseño/código/pruebas/A/B/C/acta) no equivalen a las doce microfases de implementación.

La preparación materializa **solamente un fixture aislado en `database/tests/`**. No introduce migraciones, entidades reales, datasets tenant activos, endpoints, ORM/adapters, DNS, red, conexiones dedicadas ni provisión. La compatibilidad con .NET 10/MySQL 8.4 y ADR-002 todavía deben comprobarse sobre el adapter real cuando esté autorizado.

## 2. Diseño lógico shared (propuesta, no DDL de producto)

| Relación propuesta | Clave primaria / claves foráneas | Invariante |
| --- | --- | --- |
| `Tenant` | `PRIMARY KEY (tenant_id)` | TenantId opaco BINARY(16) no nulo/no cero, ciclo de vida validado; no es autorización |
| `Branch` | `PRIMARY KEY (tenant_id, branch_id)`, `FK (tenant_id) → Tenant` | Sucursal solo existe dentro de su tenant |
| `TenantResource` | `PRIMARY KEY (tenant_id, branch_id, resource_id)`, `FK (tenant_id, branch_id) → Branch` | Una referencia a sucursal de B no puede insertarse en A |
| `ResourceEvent` | `PRIMARY KEY (tenant_id, branch_id, resource_id, event_id)`, `FK (tenant_id, branch_id, resource_id) → TenantResource` | Un evento no puede apuntar a un recurso de otro tenant/sucursal |
| Referencia externa por recurso | `UNIQUE (tenant_id, branch_id, external_key)` | Se permiten nombres iguales en A y B; no colisiones dentro del ámbito |
| Consultas privadas futuras | `WHERE tenant_id = @verifiedTenantId AND branch_id = @authorizedBranchId` | **Además** identidad, pertenencia, rol/acción y propiedad del recurso verificados por backend |

**BINARY(16):** el adapter futuro fijará una única convención UUID-byte-order y la probará con vectores. No se asume equivalencia de GUID entre C# y MySQL sin contrato. Las tablas de soporte C01/C03 no se migran ni se convierten aquí. Las referencias históricas scope_kind/platform que ya existen exigen una adaptación gobernada, no un UPDATE masivo.

**DELETE/UPDATE:** todas las FK del fixture son `RESTRICT`. No habilitar `CASCADE` que borre historial/adjuntos; retención, soft delete, auditoría y `C04-08` decidirán la transición correcta. `UNIQUE` y FK son garantías de integridad **no de autorización**.

## 3. Amenazas y pruebas obligatorias

| ID | Prueba de ataque / condición | Resultado exigido |
| --- | --- | --- |
| SH-01 | Insertar sucursal de tenant inexistente | MySQL rechaza FK |
| SH-02 | Insertar recurso B con una sucursal solo de A | MySQL rechaza FK compuesta |
| SH-03 | Insertar recurso A con una sucursal solo de B | MySQL rechaza FK compuesta |
| SH-04 | Insertar evento B a recurso solo de A | MySQL rechaza FK con tenant + sucursal + recurso |
| SH-05 | Reasignar por UPDATE el recurso A al tenant B | MySQL rechaza FK |
| SH-06 | Eliminar sucursal con recursos | MySQL rechaza por FK `ON DELETE RESTRICT` |
| SH-07 | Duplicar identificador externo dentro de A y sucursal | MySQL rechaza `UNIQUE` |
| SH-08 | Reutilizar legítimamente identificador externo en A/B | MySQL acepta aislamiento lógico |
| SH-09 | Reutilizar IDs de sucursal/recurso/nota en A/B | MySQL acepta por PK compuesta |
| SH-10 | Insertar tenant cero, estado inválido o `tenant_id NULL` | MySQL rechaza `CHECK` o `NOT NULL` |
| SH-11 | SQL SELECT sin filtro de tenant | Evidenciar que devuelve **ambos**, por tanto el backend DEBE autorizar y filtrar |
| SH-12 | Reader de fixture intenta UPDATE, DELETE, DROP o enumerar tenants | MySQL rechaza por grants mínimos |
| SH-13 | Fallo de SQL, socket o arranque | **NO** contar como negativo aprobado: fallar la CI |
| SH-14 | Duplicar GUID con distinto tenant | No correlacionar registros entre A y B |
| SH-15 | Mutar una FK, PK o la opción `--network none` en el fixture/test | Auditoría B rechaza modificación |
| SH-16 | A/B/C con SHA y run ID diferentes | Auditoría C falla: evidencia no reutilizable |

**Límite no negociable:** el fixture solo demuestra **integridad referencial**; un usuario SQL con SELECT no filtrado podría leer A y B. Por eso **no certifica aislamiento lógico de consultas reales, RLS, autorización de una sesión, BOLA/IDOR HTTP, cachés, eventos, medios, respaldos ni base dedicada**. Estos riesgos siguen abiertos y deben probarse antes de exponer cualquier acceso privado. El producto no tendrá una identidad confiable hasta completar C04-05/06 y capítulos de autorización.

## 4. Material y pruebas exclusivamente de laboratorio

- `database/tests/fixtures/c04_02_shared_integrity_fixture.sql`: cuatro tablas **`c04_02_fixture_*`** sintéticas, sin tablas de aplicación ni migraciones; InnoDB + `CHECK` + PK/FK por tenant/sucursal.
- `database/tests/c04_02_shared_integrity_smoke.sh`: MySQL **8.4 fijado por digest**, `docker --network none`, contenedor efímero `--rm`, sin puertos, espera por marca de finalización del bootstrap y dos probes SQL. A/B comparten deliberadamente branch/item ID; negativas aceptan solo códigos reales de integridad, no errores de socket o sintaxis.
- `qa/c04_02_preparation_audits.py`: A = fuente y trazabilidad; B = mutaciones adversariales de claves/red/privilegios; C = resultado real MySQL y evidencia `GITHUB_SHA/GITHUB_RUN_ID` de la misma ejecución.
- `.github/workflows/c04-02-preparation.yml`: CI de solo lectura GitHub, SQL ejecutado en base aislada, regresión documental C04-01 y C03; sin proveedores, secrets ni despliegue.

## 5. Riesgos/decisiones pendientes antes del producto

**P0 de diseño todavía abierto:** consulta no filtrada o suplantación de `tenant_id` en backend, cross-tenant en cachés/export, DSN desde Host, derechos del usuario interno. FK no soluciona el acceso. **P1:** colisiones de UUID/índices, bytes de GUID inconsistentes, cambios de esquema sin rollback, bloqueo de escrituras en migración y coste de índices.

**ADR-002:** provider EF Core/MySQL 8.4, transacciones de migración y formato GUID necesitan prueba; **no instalar ni seleccionar** paquete sin comprobar compatibilidad real. **ADR-004:** dedicated router queda para C04-03/09, no se cambia aquí.

**Plan de reversión:** si alguna prueba A/B/C falla, NO fusionar; tras la integración preparatoria, revertir solo el commit de pruebas/documentación por SHA. Nunca alterar C04-01, maestros Word/PDF, `main`, SOLQARYN, VPS, Cloudflare, DNS ni producción.

## 6. Puerta de cierre de PREPARACIÓN (no de implementación)

1. Validar SHA de Word/PDF y requisitos C04 exactos; inventariar impacto de las FK y el alcance limitado.
2. Probar **en MySQL 8.4 real** relaciones, índices compuestos, casos positivos y negativas A/B; no aceptar pruebas de sólo texto.
3. Ejecutar **tres auditorías A/B/C** y regresiones C04-01/C03 aplicables en CI PREMERGE del HEAD final.
4. Integrar a `dev` solo si todo lo obligatorio está en `SUCCESS`; repetir POSTMERGE en SHA exacto y publicar acta verificable.
5. Dictamen posible al finalizar: **C04-02 PREPARACIÓN VALIDADA, IMPLEMENTACIÓN DE MODELO SHARED PENDIENTE, C04-02 NO CERTIFICADA**. No habilitar C04-03. El siguiente acto, cuando esté expresamente habilitado, será diseñar migración real/adapter con autorización backend, no transferir SQL de fixture a producción.

**Calidad:** cero P0/P1 en la **preparación**; los riesgos del producto futuro permanecen visibles y bloquean una certificación funcional prematura. Los errores históricos se documentan y reparan, nunca se hacen pasar por SUCCESS.

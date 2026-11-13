# CRECISOL — C04-09 · Preparación de migraciones SHARED y DEDICATED

**Fecha:** 2026-11-12 (Honduras). **Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID GitHub `1450414592`). **Base verificada:** `dev` SHA `136c8697d662ed6f8b4213f95370312cc6654a4e`. **Rama:** `feature/c04-09-preparacion-migraciones-20261112`. **Estado:** PREPARACIÓN DOCUMENTAL ÚNICAMENTE. NO IMPLEMENTADA / NO PROBADA / NO CERTIFICADA.

## 1. Autoridad y precondición

- Fuente funcional: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 vigente registrado en `docs/MASTER_REFERENCE.md`: `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; capítulo C04. La fuente de planificación técnica `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md` define **C04-09: Migraciones shared y dedicated**; prueba positiva «Ensayos de compatibilidad y rollback por ámbito»; prueba negativa «No migrar tenant equivocado». La planificación no sustituye el Word.
- Precedente verificado: certificación institucional **C04-08 exclusivamente interna/sintética** en [comentario del PR #98](https://github.com/crecisol-platform/CRECISOL/pull/98#issuecomment-6838736796), 20/20 CI aplicables; SHA final `136c8697d662ed6f8b4213f95370312cc6654a4e`. Solo autoriza preparar C04-09. C04-10 BLOQUEADA.
- Activos previamente existentes: `database/c04_02_shared_schema.sql` (modelo shared y FK), `database/tests/c04_02_application_schema_smoke.sh` y `database/tests/c04_02_backend_reader_smoke.sh` (tests); `backend/src/Crecisol.Domain/Tenancy/TenantStoragePlacement.cs` (enum Shared/Dedicated, slot opaco y revisión); `backend/src/Crecisol.Application/Tenancy/ResolveCurrentTenantStorage.cs` (identidad autorizada y catálogo backend; sin DSN proporcionado por cliente). Los ensayos sintéticos C04-07/08 son **independientes** y no son migraciones productivas.

## 2. Delimitación y estrategia de migración segura

**Objetivo de C04-09:** definir y más adelante ensayar migraciones versionadas separadamente para (a) esquema SHARED de plataforma con claves tenant-aware y (b) esquema DEDICATED vinculado a un único slot/tenant autorizado, sin que un cliente elija conexión ni destino. Las migraciones pertenecen a `database/` y a un futuro ejecutor administrativo del backend; el frontend nunca recibe credenciales ni decide ámbito.

1. **Inventario y compatibilidad primero:** comparar tablas, columnas, índices, FK compuestas, triggers, tipos MySQL 8.4, codificación de GUID RFC4122, restricciones por tenant y procedimientos existentes; fijar versión y checksum de cada migración. No ejecutar `ALTER` sobre datos existentes en esta preparación.
2. **Selección segura del ámbito:** el ejecutor futuro recibe únicamente un identificador opaco de una decisión aprobada; obtiene de catálogo interno de confianza el `TenantId`, `TenantPlacementMode`, `DedicatedStorageSlotId`, versión y target. Nunca acepta host, cabecera, cookie, `tenant_id` cliente, DSN, ruta ni URL como selector de base de datos.
3. **SHARED:** probar restricciones compuestas `(tenant_id, resource_id)` y filtros obligatorios en lecturas/escrituras; migraciones globales que preserven aislamiento por tenant y no cambien propiedad o borrado lógico.
4. **DEDICATED:** exigir binding inmutable slot→tenant y revisión de placement vigente comprobada *dentro del gate transaccional* antes de operar; base/slot distintos no deben compartir datos ni bitácoras. No inferir que disponer de un slot GUID autoriza conexión.
5. **Ledger de migraciones:** versión, checksum, estado, scope, `TenantId` opcional según ámbito, slot confiable opcional, epoch/revisión y resultado auditados. Aplicación repetida con igual checksum es replay/no-op; misma versión con checksum distinto es conflicto cerrado. No registrar DSN, secretos ni datos personales en logs.
6. **Atomicidad realista:** DDL de MySQL puede efectuar commits implícitos; **no afirmar rollback transaccional de DDL**. Ensayar tanto migración compatible reversible mediante script compensatorio probado como restauración desde backup desechable verificado, con control de estados `prepared / applying / verified / failed`. Fail-closed ante aplicación parcial, corte de energía simulado, lock timeout y deadlock.
7. **Bloqueo y carreras:** locking por ámbito/versión con lease y fencing token, idempotencia por checksum y versionado; medir duración de lock y tiempos de migración. Dos ejecutores concurrentes para mismo target deben producir una aplicación exacta y cero resultados ambiguos.
8. **Cortes y compatibilidad:** antes/después en versiones compatibles de esquema/lectores .NET, planes `expand → backfill → verify → contract` cuando aplique; sin drops destructivos antes de verificación, snapshot y ventana de reversión, y sin migración cruzada SHARED↔DEDICATED bajo la misma operación de esquema.
9. **No producción:** ensayos solo en instancias MySQL8.4 efímeras `--network none`, esquemas sintéticos y datos ficticios. Prohibidos VPS, DNS/Cloudflare, hosts reales, backups de clientes, pagos y cambios productivos.

## 3. Matriz de ensayos que deberá ejecutarse antes de certificar

| Código | Caso | Resultado obligatorio |
| --- | --- | --- |
| M01 | SHARED: migración compatible de esquema tenant-aware | Índices, FK y restricciones se preservan; datos ficticios de A/B permanecen aislados |
| M02 | DEDICATED: slot autorizado de tenant A | Solo base sintética de A migra; slot/tenant/epoch vinculados al ledger |
| M03 | Rollback lógico o restauración ensayada por ámbito | Versión/restauración y checksums correctos sin afectar otros ámbitos |
| M04 | Replay de versión+checksum idénticos | No-op idempotente, una sola aplicación registrada |
| M05 | Dos aplicadores concurrentes sobre mismo ámbito | Solo uno aplica; el otro espera/replay o falla cerrado |
| M06 | Compatibilidad de código/esquema antes y después | Contratos .NET 10 y queries MySQL 8.4 funcionan sin pérdida |
| N01 | Tenant A intenta migrar B o un slot ajeno | DENY, cero DDL/filas/ledger en B |
| N02 | Scope/slot/tenant proveniente de URL/header/claims/DSN | DENY antes de abrir conexión |
| N03 | Revisión de placement o epoch obsoleta | DENY, cero aplicación |
| N04 | Version igual con checksum distinto | CONFLICT sin alterar esquema |
| N05 | DDL parcial o caída simulada | Estado `failed`, no certificado ni reintento ciego; recuperación demostrada |
| N06 | Intento de downgrade destructivo sin respaldo verificado | DENY |
| N07 | Error de FK tenant-aware, índice o aislamiento compartido | Migración rechazada, sin lectura cross-tenant |
| N08 | Dos tenants apuntan a mismo slot DEDICATED | DENY por unicidad de ownership; no consolidar datos |
| N09 | Restauración sobre destino equivocado | DENY y prueba de no contaminación |
| N10 | Cuenta ejecutora intenta migración sin autorización administrativa | DENY por mínimo privilegio |

Las pruebas de N01/N02/N03/N08/N09 son bloqueantes P0. Los casos M03/N05 no pueden declararse cubiertos con un `ROLLBACK` SQL alrededor de DDL, ya que MySQL efectúa commits implícitos. La evidencia deberá demostrar recuperación real con snapshots o scripts compensatorios, nunca prometer una propiedad que el motor no ofrece.

## 4. Impacto, riesgos y reversión

| Área | Situación en preparación |
| --- | --- |
| `frontend/` | Sin cambios; ningún selector de tenant/DSN público |
| `backend/` | Solo lectura de contratos existentes; el ejecutor seguro aún NO existe |
| `database/` | Solo inventario propuesto y futura batería desechable; cero DDL ejecutado |
| `qa/` y CI | Especificar auditorías A/B/C y regresiones; no fingir ejecución |
| `main`, Word/PDF maestro, SOLQARYN, proveedores | Inalterados |

**Riesgos P0:** migrar tenant equivocado, ruta de almacenamiento controlada por usuario, mezcla shared/dedicated, mutación cruzada, restauración sobre destino ajeno. **Riesgos P1:** colisión de versiones, divergencia de checksum, DDL parcialmente confirmado, locks no liberados, falsa reversibilidad, regresión de lectores.

**Auditoría A — funcional/normativa:** cotejar íntegramente capítulo C04 del Word, mapa C04-09, migraciones existentes y evidencia de compatibilidad.

**Auditoría B — seguridad:** falsificación de selector, aislamiento tenant/slot/actor, permisos, concurrencia, checksums, caída parcial y pruebas negativas reales.

**Auditoría C — arquitectura/rendimiento:** Domain/Application/Infrastructure/Database separados, gestión de DDL y versiones, locks medidos, recuperación efectiva, regresiones .NET/MySQL, ausencia de secretos y plan de reversión verificable.

## 5. Puertas obligatorias de implementación y cierre

1. Verificar que la preparación documental y su PR no cambian código, `database/`, workflows, `main` ni otras microfases.
2. Antes del PR técnico independiente: cotejar norma Word y huellas, revisar esquema `database/c04_02_shared_schema.sql`, contratos C04-03 y la identidad de repo/rama/SHA. Elaborar decisión de scope y checksum sin rutas de conexión reales.
3. Implementar solo pruebas de migración en MySQL8.4 efímero y contratos de ejecutor futuro según permisos; prohibidas migraciones productivas, adaptadores de conexión reales y acceso a servidores.
4. Compilar y ejecutar .NET10 Release `-warnaserror`; ejecutar casos M01–M06/N01–N10 realmente, incluidos dos tenants y dos slots, DDL parcial y rollback/restauración. Registrar conteos y logs reales.
5. Completar auditorías A/B/C sin P0/P1 ni fallos obligatorios, CI PREMERGE SHA exacto, merge controlado solo a `dev`, CI POSTMERGE, acta documental separada y dictamen con SHAs/run IDs.
6. Ningún `skipped` o `queued` cuenta como PASS. Histórico de fallos se conserva; correcciones requieren ejecuciones nuevas exitosas.

**ESTADO:** C04-09 PREPARADA, NO IMPLEMENTADA, NO CERTIFICADA. C04-10 BLOQUEADA. Capítulo 04 todavía NO CERTIFICADO; avance previo válido 8/12, capítulos completos 3/37. Rollback de esta preparación: revertir exclusivamente el commit/documento de C04-09; no existe cambio de datos ni base productiva.

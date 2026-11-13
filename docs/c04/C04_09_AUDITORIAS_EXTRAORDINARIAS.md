# CRECISOL — C04-09 · Seis auditorías técnicas (A/B/C + D/E/F) y expediente de corrección

**Fecha:** 12-11-2026. **Repositorio:** `crecisol-platform/CRECISOL` ID `1450414592`; `dev` base `136c8697d662ed6f8b4213f95370312cc6654a4e`. **PR documental:** #100 (DRAFT, SIN FUSIONAR). **PR técnico:** #101 (DRAFT, SIN FUSIONAR). **Estado:** revisión y corrección en marcha. **NO CERTIFICADA, C04-10 BLOQUEADA.**

## 1. Auditoría de alcance y gobierno

Fuente canónica única: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` según `docs/MASTER_REFERENCE.md`, capítulo 04 y `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`. Puerta C04-08 acreditada por comentario institucional [PR #98](https://github.com/crecisol-platform/CRECISOL/pull/98#issuecomment-6838736796) en SHA `136c8697d662ed6f8b4213f95370312cc6654a4e`, con alcance sintético. C04-09 exige compatibilidad y reversión por ámbito; prohíbe migrar el tenant equivocado. El presente expediente no es acta de certificación ni actualización del Word.

## 2. Hallazgos y medidas correctivas — evidencia final NO PRESUPUESTA

| ID | Severidad si estuviera expuesto | Hallazgo identificado | Corrección introducida en PR técnico / evidencia exigible | Estado de certificación |
| --- | --- | --- | --- | --- |
| D-01 | P0 | Un `ALTER TABLE` SHARED afecta a TODOS los tenants, aunque la petición incluya un tenant único; diseño original ambiguo | `TrustedTenantMigrationPlan` se restringe a DEDICATED; nuevo `TrustedSharedSchemaMigrationPlan` sin `TenantId` ni slot y coordinador separado, aprobación global y CAS de revisión de esquema | Pendiente de pruebas finales |
| D-02 | P0 | El fixture anterior solo probaba FK a tenant inexistente, no ataque a **tenant B existente** sin colisión PK | Nueva matriz SQL con dos tenants válidos y reparent A→B que debe ser rechazado por trigger, además de aprobación de B que intenta migrar DEDICATED A | Pendiente de pruebas finales |
| D-03 | P0 | Ownership de slot DEDICATED no protegido en ensayo antiguo | Tabla de targets con `UNIQUE(slot_id)`, trigger de identidad inmutable, verificación exacta de scope/tenant/slot/revisión/epoch y cuenta de ejecución sin escritura directa | Pendiente de pruebas finales |
| E-01 | P1 | No se verificó carrera de ejecutores ni fencing transaccional | Ledger en fixture, una operación reservada por destino, 12 approvals competidores, bloqueo por scope, contador de fencing, CAS y rechazo de tokens obsoletos | Pendiente de pruebas finales |
| E-02 | P1 | Compensación de columna no equivale a restauración ante DDL parcialmente confirmado | Simulación de `START TRANSACTION; ALTER TABLE; ROLLBACK` que debe conservar la columna, estado `failed`, restauración desde `mysqldump` real desechable, hash antes/después y estado `recovered` | Pendiente de pruebas finales |
| E-03 | P0 | Restauración equivocada puede escribir datos de B sobre base A | Helper desechable que consulta target/fence en catálogo protegido y rechaza destino incorrecto **antes** de DROP/import. Esto NO sustituye un ejecutor productivo | Pendiente de pruebas finales |
| E-04 | P1 | Orden de bloqueos inverso entre reserva, finalización y recuperación puede aumentar deadlocks | Estándar de locking `approval → target → gate → ledger` en cuatro procedimientos, comprobación independiente en auditoría E | Pendiente de pruebas finales |
| E-05 | P1 | Un recibo histórico VERIFICADO podría ocultar cambio físico posterior en esquema | Revalidar `information_schema.columns` antes de devolver REPLAY; prueba real que elimina temporalmente `migration_tag` y exige DENY hasta restaurar metadatos | Pendiente de pruebas finales |
| F-01 | P1 | La primera batería solo tenía 34 aserciones .NET y 18 comprobaciones SQL; insuficiente para toda la matriz | Nuevas pruebas de tipado y confusión de ámbito, banco MySQL adicional, regresión de fases anteriores y auditorías independientes D/E/F | Pendiente de pruebas finales |
| F-02 | P1 | Los flags de CI verdes podrían interpretarse erróneamente como autorización real de migración | Puerta de doble ensayo MySQL + .NET + seis auditorías del MISMO SHA/run y acta condicionada; se excluyen skipped y se preservan fallos anteriores | Pendiente de pruebas finales |

Los hallazgos P0/P1 se refieren a riesgos de un hipotético acoplamiento productivo; **no existe conexión productiva ni ruta privada habilitada por C04-09**. La evidencia futura debe demostrar que los controles sintéticos niegan cada caso; los controles de producción reales siguen NO IMPLEMENTADOS y no se fingirán cubiertos.

## 3. Seis auditorías obligatorias

- **A — Funcional / normativa:** huella vigente del Word, matriz C04-09 y separación del ámbito SHARED/DEDICATED.
- **B — Seguridad base:** validación de planes, huellas, recibos, revalidación y cuatro mutaciones estáticas de guardas.
- **C — Arquitectura:** Domain/Application por puertos, sin Infrastructure/DI/HTTP/DSN, MySQL8.4 aislado y cero migraciones reales.
- **D — Aislamiento multitenant reforzado:** Shared global ≠ Dedicated por tenant, ownership de slot único, spoof de A/B válidos y reparenting real a tenant existente, permisos mínimos y cinco mutaciones independientes de SQL.
- **E — Concurrencia y recuperación:** 12 contendientes reales, fencing, checksum, replay exacto, aprobación revocada/caducada, DDL implícitamente confirmado, falla parcial, `mysqldump` y restauración verificada.
- **F — Integridad / regresión:** .NET 10 Release `-warnaserror`, regresiones C04 previas, dos suites MySQL8.4 reales, CI con pruebas obligatorias no ignoradas, mismo SHA/run y reversibilidad.

## 4. Condiciones para fusión y certificación

1. PR #101: ejecutar en **SHA HEAD final** seis auditorías A/B/C/D/E/F, todas las pruebas .NET 10 y las **dos** suites MySQL, con las negativas M01–M06/N01–N10 trazables. C03-10 integral y restantes workflows aplicables deben estar `completed/success`; no cuentan `skipped`.
2. Corregir todo fallo encontrado mediante nuevos commits y repetir CI; jamás alterar un run histórico. Revisar diff y que solo se agregan contratos/no-conectados/fixtures/QA/CI de C04-09.
3. Solo al completar lo anterior fusionar PR #101 a `dev`, verificar todas sus ejecuciones POSTMERGE y confirmar SHA exacto.
4. Finalizar PR #100 (especificación y este expediente + acta condicionada posterior), CI documental PREMERGE, fusionar solo a `dev` y validar POSTMERGE documental, comentario institucional con run IDs y límites.
5. **No declarar migraciones productivas ni rollback genérico de DDL.** El ejecutor administrativo real, auth/MFA, slot catalog protegido persistente y backups de clientes siguen fuera de esta microfase sintética. Si son requeridos para un uso real, bloquear ese uso hasta futuras fases y auditorías; no certificar lo inexistente.
6. Mantener `main`, producción, Word/PDF, Cloudflare, VPS, DNS, cobros y el repo `solqaryn/Solqaryn` intactos.

**Dictamen provisional:** C04-09 **NO CERTIFICADA**; PR #100/#101 sin fusionar. Solo si la evidencia final comprueba las seis auditorías y resuelve todos los P0/P1 del alcance no conectado se podrá proponer la certificación **restringida a contratos internos y ensayos sintéticos** (9/12); el capítulo 04 completo permanecerá NO CERTIFICADO y C04-10 se habilitaría únicamente para preparación.

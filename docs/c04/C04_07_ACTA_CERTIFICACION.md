# CRECISOL — C04-07 · Acta institucional de aprovisionamiento idempotente sintético (eficacia CONDICIONADA)

**Fecha:** 12-11-2026, Honduras. **Repositorio autorizado:** `crecisol-platform/CRECISOL`, GitHub ID `1450414592`. **Rama receptora:** `dev`. **Microfase:** C04-07 (7/12 del capítulo C04). **Estado de esta acta:** propuesta SIN eficacia hasta verificar TODAS las puertas de CI y dictamen institucional permanente. **Mientras falte una puerta:** C04-07 PENDIENTE; C04-08 BLOQUEADA.

## 1. Autoridad, secuencia y alcance aprobado

Fuente única: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` y PDF de lectura SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Capítulo normativo C04: 72 párrafos, SHA-256 texto `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`. Ocho puertas MAE-C04 no equivalen a doce microfases técnicas.

**Entrada:** C04-06 certificada en [acta y comentario final #95](https://github.com/crecisol-platform/CRECISOL/pull/95#issuecomment-6835009442), `dev` SHA `f7bd63cda19cd60dd55a9e2afaceeb3a8b702a7a`, CI final 4/4 SUCCESS. El plan original `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md` define **C04-07: Aprovisionamiento idempotente**; prueba positiva: «Creación sintética y recuperación ante reintento»; negativa: «No duplicar tenants por carreras». Los 37 capítulos son secuenciales: C01/C02/C03 completos **3/37**; capítulo C04 **NO CERTIFICADO**.

## 2. Implementación realmente fusionada — SÓLO interna y sintética

[PR técnico #96](https://github.com/crecisol-platform/CRECISOL/pull/96) HEAD PREMERGE `8008abefeef3bc95a2c26abf11ed4aeb99ff014e`, squash fusionado **exclusivamente** a `dev` SHA **`d045a3c23a74728d9329401d08901352ba8ca0b6`**. Diff revisado **9 archivos nuevos C04-07**, sin modificación a `Program.cs`, DI, `Infrastructure/`, tablas productivas/migraciones, frontend, Word/PDF maestro, `main`, DNS, Cloudflare, pagos o SOLQARYN.

- `backend/src/Crecisol.Domain/Tenancy/TenantProvisioningIdempotency.cs`: operation GUID opaca (NO permiso), `TenantProvisioningBusinessKey` interna canónica (NO slug/host), plan confiable con `TenantId`, patrocinador, modalidad de dirección tipada PathSlug/Subdomain/CustomDomain, estado Approved/Suspended/Revoked, revisión y vigencia UTC; fingerprint SHA-256 de intención inmutable (NO HMAC, no autenticación); recibo interno solo `TenantLifecycleState.Draft`, nunca Active, y resultado tipado NO AUTORIZANTE.
- `backend/src/Crecisol.Application/Tenancy/ProvisionApprovedTenantDraft.cs`: puerto de plan aprobado backend `ITrustedTenantProvisioningPlanReader` y puerto de futura escritura `IAtomicTenantProvisioningStore`. El caso de uso acepta solo operationId como **selector** y cancelación; comprueba aprobación temporal, invoca commit atómico por puerto y revalida plan y recibo después. Ninguna API, adaptador/product DB ni credenciales reales integrados. El futuro adaptador deberá comprobar permisos/estado real en la misma transacción de escritura; una comprobación doble antes/después NO evita revocación concurrente posterior.
- `backend/tests/Crecisol.TenantProvisioning.ContractsTests`: 132 pruebas .NET10 reales de lógica con mocks/fake store expresamente declarado, incluyendo 48 solicitudes simultáneas de misma operación (1 CREATED/47 REPLAY), 24 distintas sobre mismo negocio (1 CREATED/23 DENY), estado revocado/suspendido, expiración, fallos antes/después de commit, reintentos tras pérdida de respuesta, ID/actor/tenant/mode/fingerprint falsificados y cancelaciones. Fake NO representa DB productiva.
- `database/tests/c04_07_idempotent_fixture.sql` + `database/tests/c04_07_idempotency_smoke.sh`: **fixture DESECHABLE de test**, no migración: MySQL 8.4 InnoDB con dos tablas independientes, claves únicas de operación/tenant/business key global, FK restrictiva, triggers de inmutabilidad, procedimiento transaccional sintético para crear `Draft` o replay exacto. Docker `--network none`, usuario sintético `EXECUTE` sin lectura/escritura directa, ninguna conexión a producción. Carreras **reales de 12 callers mismo op (1 CREATED/11 REPLAY)** y **12 ops distintas mismo business key (1 CREATED/11 conflicto)**, rollback total antes de commit y replay tras respuesta perdida, 24 comprobaciones positivas y 15 denegaciones. Lectura `FOR SHARE` de filas inmutables y reintentos acotados de errores **1213/1205** en el runner de ensayos, NUNCA para conflicto semántico 1644. En el run final exitoso el mecanismo de retry transitorio no necesitó activarse; **no afirmar que el retry 1213 se ejecutó en ese run**.
- `qa/c04_07_idempotency_audits.py`, `.github/workflows/c04-07-idempotent-provisioning.yml`, `docs/c04/C04_07_ESPECIFICACION.md`: A/B/C, mutaciones, evidencias de .NET/MySQL ligadas al mismo SHA+run, CI read-only y delimitación de superficie.

## 3. Evidencia de tres auditorías, pruebas y CI PREMERGE

**HEAD técnico** `8008abefeef3bc95a2c26abf11ed4aeb99ff014e`: **5/5 workflows aplicables COMPLETED/SUCCESS**. Otros **3 checks `skipped`** por condiciones no se contabilizaron como PASS.

| Gate PREMERGE técnico | Ejecución de GitHub | Conclusión |
| --- | --- | --- |
| C04-07 A/B/C + .NET10 + MySQL8.4 sintético | [43305619530](https://github.com/crecisol-platform/CRECISOL/actions/runs/43305619530) | SUCCESS |
| C04-01 contratos catálogo | [43305619310](https://github.com/crecisol-platform/CRECISOL/actions/runs/43305619310) | SUCCESS |
| C04-02 SHARED MySQL8.4 | [43305619252](https://github.com/crecisol-platform/CRECISOL/actions/runs/43305619252) | SUCCESS |
| C03-10 integral Angular22/Node24/.NET10/Chromium/MySQL8.4 | [43305619253](https://github.com/crecisol-platform/CRECISOL/actions/runs/43305619253) | SUCCESS |
| Foundation | [43305619237](https://github.com/crecisol-platform/CRECISOL/actions/runs/43305619237) | SUCCESS |

**Auditoría A — integridad normativa y función:** Word/PDF sin cambios y capítulo C04 de 72 párrafos; fase original C04-07 y sus dos resultados, catálogo Domain/Application limpio, sin routes/DSN/product.  
**Auditoría B — seguridad y concurrencia:** **159 mutaciones independientes de guardas rechazadas**, 132 aserciones .NET10, MySQL8.4 real con 12+12 colisiones, 24 positivos + 15 negativos, FK/UNIQUE/inmutabilidad, recuperación ante fallo y `REPLAY` solo para misma intención, claves distintas que intentan duplicar un negocio denegadas.  
**Auditoría C — arquitectura/evidencia:** `.NET10 Release -warnaserror` real, A/B + .NET y MySQL referidos al mismo SHA/run, sin DI/API o migración de producto, regresiones C04-01..06 y API C03-05 .NET y C03-10 integral + fixture MySQL C04-02 externos. Todo probado en runners GitHub; no se ejecutó un aprovisionamiento real.

**Historial de fallos conservado y corregido por nuevos commits**, sin reetiquetar como éxito:
- [43303633982](https://github.com/crecisol-platform/CRECISOL/actions/runs/43303633982): error de compilación CS0411 del fake de catálogo; corregida firma del test.
- [43303824751](https://github.com/crecisol-platform/CRECISOL/actions/runs/43303824751): expresión SQL REGEXP incompleta; corregida y añadida guarda explícita.
- [43304253913](https://github.com/crecisol-platform/CRECISOL/actions/runs/43304253913): fragmento SQL duplicado tras `DELIMITER ;`; eliminado y añadido guard de único cierre/único procedimiento.
- [43304736187](https://github.com/crecisol-platform/CRECISOL/actions/runs/43304736187): primer deadlock MySQL 1213 bajo carreras.
- [43305099856](https://github.com/crecisol-platform/CRECISOL/actions/runs/43305099856): 1213 diagnosticado como conflicto de bloqueos del replay; corrección con `FOR SHARE` en registro inmutable y retry 1213/1205 limitado para SQL de prueba.
- [43305619530](https://github.com/crecisol-platform/CRECISOL/actions/runs/43305619530) probó todo de nuevo: **SUCCESS**; 0 fallos vigentes en HEAD final técnico.

## 4. POSTMERGE técnico — NO presupone resultados

El commit integrado técnico SHA `d045a3c23a74728d9329401d08901352ba8ca0b6` inició estos 7 workflows; **hasta comprobar individualmente su conclusión, esta acta NO tiene eficacia**:

| Gate POSTMERGE técnico | Run lanzado |
| --- | --- |
| C04-07 A/B/C, .NET10 y MySQL8.4 | [43306526888](https://github.com/crecisol-platform/CRECISOL/actions/runs/43306526888) |
| C04-01 | [43306527308](https://github.com/crecisol-platform/CRECISOL/actions/runs/43306527308) |
| C04-02 SHARED MySQL8.4 | [43306526977](https://github.com/crecisol-platform/CRECISOL/actions/runs/43306526977) |
| C03-02 Domain | [43306527224](https://github.com/crecisol-platform/CRECISOL/actions/runs/43306527224) |
| C03-03 Application | [43306526821](https://github.com/crecisol-platform/CRECISOL/actions/runs/43306526821) |
| C03-10 integral | [43306527139](https://github.com/crecisol-platform/CRECISOL/actions/runs/43306527139) |
| Foundation | [43306526916](https://github.com/crecisol-platform/CRECISOL/actions/runs/43306526916) |

Sólo `completed/success` cuenta como PASS. No `skipped`, `in_progress`, `queued`, `cancelled` o `failure`. Los resultados y SHA reales se publicarán en dictamen final.

## 5. Riesgos futuros, no producción y reversión

**No hay producto aprovisionado ni acceso privado habilitado por este PR.** Se omitieron deliberadamente y quedan pendientes como bloqueos de integración/producción: autenticación criptográfica y aprobación backoffice (MFA/4 ojos/motivo), validación transaccional real de aprobación/revocación, plan/comercio/contrato/aceptación/pagos, reservas de slug/host/DNS, catálogo C04-01 persistente, shared/dedicated routing, permisos de tenant/branch/recurso, migraciones productivas, outbox/sagas/colas para efectos externos, suspensión/reactivación con datos retenidos, cuotas/bitácora, operación de recovery real en cluster, BOLA/IDOR de endpoints privados y aislamiento de caché/medios. El SHA-256 de intención no es MAC/credencial. Los fallos 1213/1205 se prueban sólo como eventos de InnoDB observados y se aborda la política de retry; el run final no recorrió necesariamente la rama de reintento.

La superficie implementada es **sintética y sin rutas productivas**: en ella, los tests/QA del último SHA no reportan hallazgos P0/P1 pendientes. **Esa conclusión no aplica al SaaS productivo** ni autoriza desplegar. Reversión: revertir exclusivamente squash técnico `d045a3c23a74728d9329401d08901352ba8ca0b6`, documentar incidencia y repetir CI; no hay tablas ni datos de producción alterados. `main`, Word/PDF, SOLQARYN, VPS, Cloudflare/DNS, contratos y cobros intactos.

## 6. Condiciones exactas para eficacia institucional

Esta acta se considera EFICAZ **solo cuando**: (1) los 7 workflows POSTMERGE técnicos del SHA `d045...` finalicen `completed/success`; (2) el PR **documental separado** con esta acta pase **toda CI PREMERGE** sobre su HEAD final; (3) se fusione exclusivamente a `dev` con SHA esperado; (4) **todos** los workflows documentales POSTMERGE sobre el nuevo SHA final de dev finalicen `completed/success`; (5) comentario permanente registre los cuatro conjuntos de evidencia, runs, SHAs y riesgos excluidos.

**Antes de esas puertas:** `C04-07 NO CERTIFICADA` y `C04-08 BLOQUEADA`. **Solo después:** `C04-07 CERTIFICADA EN SU ALCANCE EXCLUSIVO DE MODELO INTERNO Y ENSAYO SINTÉTICO IDEMPOTENTE` (7/12); `C04-08 HABILITADA SOLO PARA PREPARACIÓN`, capítulo C04 aún NO CERTIFICADO; capítulos institucionales completos 3/37. No es firma criptográfica ni certificación de autenticación, aprovisionamiento o infraestructura productiva.

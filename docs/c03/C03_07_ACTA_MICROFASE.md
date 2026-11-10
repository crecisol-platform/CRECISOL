# CRECISOL — C03-07 | Acta técnica, auditorías A/B/C y autorización de certificación

**Estado al redactar:** C03-07 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (7/10), **NO CERTIFICADA** hasta finalizar el PR documental y su CI independiente. C03-01..C03-06 previamente CERTIFICADAS (6/10); C03 NO CERTIFICADO; C04 BLOQUEADO; C03-08 NO INICIADA. Solo **2/37 capítulos completos** (C01 y C02).

## Identidad, autoridad y autorización

- Exclusivamente repositorio privado `solqaryn-platform/CRECISOL`, ramas `feature/c03-07-outbox-workers-contract` y `dev`. PR técnico [#63](https://github.com/solqaryn-platform/CRECISOL/pull/63). Sin escritura en `main` ni `solqaryn/Solqaryn`.
- Documento Maestro Único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 **`e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`**, blob `214bf9c3d9763be23a77b7e0c464359584edde46`. PDF de lectura intacto; matriz original C03, **T01-R06 Workers**, frontera del capítulo 04 y `MAE-C03-01..08` sin confundir 8 pasos con 10 microfases técnicas.
- **Permiso humano explícito PREVIO A LAS PRUEBAS**, 10 de noviembre de 2026: **«OK, ya adelante, continúa, por favor, hasta certificarla.»** Aplica solo a C03-07, sin extenderse a C03 entero, C04, producción ni pagos. No se interpreta como aprobación posterior falsificada.

## Código integrado y alcance limitado

- `backend/src/Crecisol.Domain/Events/PlatformOutboxEvent.cs`: sobre tipado inmutable para un único evento **PLATFORM** `platform.foundation-check.v1`, payload fijo no configurable, UTC, versión y scope explícitos. No se pueden construir eventos tenant desde el contrato.
- `backend/src/Crecisol.Application/Events/PlatformOutboxWorker.cs`: contrato de puerto/lease, worker local **solo opt-in**, lotes de 1–32, cancelación, rechazo de esquemas desconocidos y sin efectos de negocio.
- `backend/src/Crecisol.Infrastructure/Events/MySqlPlatformOutboxRelay.cs`: inserción en **la misma transacción** de un agregado de prueba, `FOR UPDATE SKIP LOCKED`, lease dueño/generación/expiración, recuperación, retries 3 máximos, dead-letter y recibo idempotente transaccional. El recibo interno es el único efecto del procesamiento; **NO implica exactly-once para proveedores externos**.
- `database/c03_07_outbox_schema.sql`: esquema MySQL8.4 **aditivo solo aplicado en CI desechable**; claves/FK compuestas por scope y evento, validación de estados/leases.
- `database/tests/c03_07_outbox_mysql_smoke.sh`, `backend/tests/Crecisol.Outbox.ContractsTests/`, `qa/c03_07_outbox_audits.py`, `.github/workflows/c03-07-outbox.yml`: pruebas independientes de integración/scope, 16 mutaciones hostiles y regresiones. API de producción sin endpoints nuevos y sin registrar worker; frontend intacto.
- **No se prueban ni certifican BOLA/IDOR/MFA privados**, autorización de recursos/tenant en backend, outbox privado, routing shared/dedicated, efectos comerciales, solicitudes externas, envío de correo, notificaciones, datos privados, cobros, despliegues, VPS, DNS ni producción.

## PREMERGE PR técnico #63 (SHA exacto)

**HEAD `f7efc6d2b62db916bb429303887bcae3d6b0a4fd`**, confirmado GitHub Actions `event=pull_request,status=completed,conclusion=success`:
- [C03-07 A/B/C real + .NET/MySQL8.4 #42815359099](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815359099) — SUCCESS.
- [Foundation #42815359163](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815359163) — SUCCESS.
- Otros workflows `skipped` condicionales no se contabilizan como `SUCCESS`. Intentos PREMERGE anteriores fallidos se conservaron en el historial y fueron corregidos (trazabilidad de acta y mutación de generación); no son evidencia exitosa.

## POSTMERGE técnico PR #63 (SHA exacto)

Squash a `dev`, commit **`a256885afa5ee2f639bd4ed6f7d8595b709e6409`**; verificación directa `event=push,status=completed,conclusion=success,head_sha=a256885afa5ee2f639bd4ed6f7d8595b709e6409` para **8/8 workflows POSTMERGE SUCCESS**:

| Suite | Run | Resultado |
| --- | --- | --- |
| C03-07 workers/outbox A/B/C .NET+MySQL | [42815698115](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698115) | SUCCESS |
| C03-01 integral y regresiones | [42815698155](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698155) | SUCCESS |
| C03-02 Domain | [42815698446](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698446) | SUCCESS |
| C03-03 Application | [42815698259](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698259) | SUCCESS |
| C03-04 Infrastructure/MySQL | [42815698005](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698005) | SUCCESS |
| C03-05 API/DTO | [42815698160](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698160) | SUCCESS |
| Windows/local static A/B | [42815697923](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815697923) | SUCCESS |
| Foundation | [42815698031](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698031) | SUCCESS |

## Pruebas y triple auditoría verificables

**Auditoría A — trazabilidad:** Word SHA y T01-R06, límites Domain → Application → Infrastructure/API y precedencia 6/10, sin alterar Word/PDF. Resultado PASS.

**Auditoría B — seguridad:** **16/16 mutaciones adversariales** rechazadas en código/SQL/QA; se probaron no cambiar a tenant, versión desconocida (dead-letter), no aceptar ACK de otro worker/generación, exclusión de cola de tenant, FK compuesta contra cruce de scope y usuario SQL sin DROP/DELETE. No se presume autorización privada completa.

**Auditoría C — runtime/regresión:** .NET10 Release warnings-as-errors sin error, **26 aserciones** Domain/Application de worker/puerto (25 previas + 1 scope explícito), **40 aserciones de MySQL8.4** real sintético: atomicidad de agregado+outbox, rollback total, 8 claims concurrentes sin doble reserva, recuperación de lease vencido, fencing, replay, backoff, tres reintentos máximos, dead-letter, recibo único por scope/consumidor. Regresiones C01/C02/C03-01..06 y HTTP público pasan en los jobs correspondientes; CI histórica de Angular/Chromium permanece atribuida a los gates previos, no a una nueva implementación de frontend.

**Diff revisado:** 10 archivos nuevos dentro de `backend/`, `database/`, `docs/c03/`, `qa/` y `.github/workflows/`; ningún secreto, deploy, ruta privada, archivo de `main` o SOLQARYN.

## Decisión limitada y rollback

**C03-07 CERRADA TÉCNICAMENTE (7/10)** con aprobación expresa anterior a los resultados para completar la certificación. **NO CERTIFICADA hasta pasar CI PREMERGE y POSTMERGE de este PR documental y el PR de actualización del estado canónico.** C03-08 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO; C01/C02 siguen 2/37 capítulos.

**Rollback:** revert del squash técnico `a256885afa5ee2f639bd4ed6f7d8595b709e6409` mediante nuevo PR sobre `dev`; borrar solo tablas efímeras de CI, sin migraciones ni servicios productivos por revertir. Ningún despliegue realizado.

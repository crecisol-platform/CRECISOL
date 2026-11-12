# CRECISOL — C04-08 · Acta institucional de suspensión, reactivación y baja lógica (CONDICIONADA)

**Fecha:** 12-11-2026. **Repositorio único:** `crecisol-platform/CRECISOL`, ID `1450414592`; integración `dev`. **Microfase:** C04-08 (8/12 únicamente AL COMPLETAR las puertas). **Alcance:** Domain/Application internos NO CONECTADOS y fixture MySQL 8.4 totalmente desechable. **Estado del acta al escribirla:** SIN EFICACIA mientras falten los gates técnicos/documentales enumerados abajo.

## 1. Fuente normativa y precondición

- Autoridad funcional: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` y `docs/MASTER_REFERENCE.md`; 37 capítulos, capítulo C04, matriz técnica C04-08: resultado «Transiciones seguras y auditables» y negativo «Suspensión no deja acceso residual». La auditoría A del run técnico verificó hash original.
- C04-07 certificada EXCLUSIVAMENTE en alcance interno sintético por PR #96/#97, comentario institucional #97 (20/20 workflows), SHA final en `dev` `1d4d37a1850f5dcb338729582d6f2a38e273a742`. C04-08 se habilitó solamente para preparación; su código requirió nuevas pruebas.
- `Deactivated` es el estado ya existente en `TenantLifecycleState` para baja lógica, NO `Deleted`. `Draft`, `PendingVerification` y `Deactivated` nunca pueden reactivarse en este caso de uso.

## 2. Cambios técnicos comprobados

PR técnico [#99](https://github.com/crecisol-platform/CRECISOL/pull/99), HEAD PREMERGE `e747db66b4b0001b717c4cc74a589cb5ce319add`, integrado únicamente a `dev` mediante squash `683837de5b90278a4166d25e9887a6119f17e869` (validado por la respuesta de GitHub). OCHO archivos añadidos, sin modificación a codebase antiguo, API, DI, Infrastructure, migraciones productivas, frontend, main, Word/PDF o SOLQARYN.

- `backend/src/Crecisol.Domain/Tenancy/TenantLifecycleTransitions.cs`: `Suspend`, `Reactivate`, `Deactivate`, estados de origen tipados, denegación por defecto; intent fingerprint inmutable SHA-256 atado a operación, tenant, actor, revisión, epoch, motivo y hora; nunca credencial/MAC.
- `backend/src/Crecisol.Application/Tenancy/ChangeTenantLifecycle.cs`: lector de decisión administrativa FUTURA y puerto de escritura atómica FUTURA; revalida recibo de tenant/op/fingerprint/revisión/epoch/estado y decisión. NO autoriza por operationId, ni implementa transacción real productiva.
- `backend/tests/Crecisol.TenantLifecycle.ContractsTests`: 57 aserciones .NET10 efectivamente observadas en CI, positivas y negativas del modelo y del coordinador con doubles de pruebas.
- `database/tests/c04_08_lifecycle_fixture.sql` y `c04_08_lifecycle_smoke.sh`: MySQL8.4/InnoDB REAL en Docker aislado `--network none`, CUATRO tablas desechables y cuatro triggers de auditoría/recibo inmutables; CAS, decisión revocada/expirada, rollback, recuperación, estado Deactivated que retiene datos, dos operaciones concurrentes con revisión obsoleta, 12 llamadas simultáneas de MISMA operación (1 APPLIED/11 REPLAY). No es una migración ni adaptador del producto.
- `qa/c04_08_lifecycle_audits.py` y `.github/workflows/c04-08-lifecycle.yml`: fuente A, límites B con cuatro mutaciones estáticas rechazadas, separación C, Release `-warnaserror`, regresiones .NET y MySQL con huellas de misma ejecución/SHA.

## 3. CI técnica PREMERGE — verificada en SHA exacto

HEAD `e747db66b4b0001b717c4cc74a589cb5ce319add`: cinco workflows aplicables completaron `SUCCESS`. Tres workflows condicionales `skipped` se excluyeron del cómputo; NO equivalen a PASS.

| Workflow | Run | Estado |
| --- | --- | --- |
| C04-08 A/B/C + .NET10 + MySQL8.4 | [43325615428](https://github.com/crecisol-platform/CRECISOL/actions/runs/43325615428) | SUCCESS |
| C04-02 SHARED MySQL8.4 | [43325615487](https://github.com/crecisol-platform/CRECISOL/actions/runs/43325615487) | SUCCESS |
| C04-01 catálogo contratos | [43325615279](https://github.com/crecisol-platform/CRECISOL/actions/runs/43325615279) | SUCCESS |
| C03-10 integral | [43325615336](https://github.com/crecisol-platform/CRECISOL/actions/runs/43325615336) | SUCCESS |
| Foundation | [43325615283](https://github.com/crecisol-platform/CRECISOL/actions/runs/43325615283) | SUCCESS |

El log real del job `131557198074` del run C04-08 consigna **57 aserciones .NET10**, **22 comprobaciones MySQL positivas y 10 negativas**, doce callers concurrentes (1 APPLY/11 REPLAY), dos solicitudes diferentes sobre misma revisión (1 éxito/1 conflicto), revocación/expiración de decisión denegadas, rollback íntegro, borrado lógico sin eliminar la fila ni su payload, auditoría append-only. Auditoría A Word/estados; B guardas y mutaciones; C arquitectura y pruebas del mismo SHA/run.

## 4. POSTMERGE técnico — requiere comprobación final

Sobre commit de integración `683837de5b90278a4166d25e9887a6119f17e869`, GitHub lanzó 7 workflows obligatorios:

- C04-01: [43326549106](https://github.com/crecisol-platform/CRECISOL/actions/runs/43326549106)
- C03-02 Domain: [43326548978](https://github.com/crecisol-platform/CRECISOL/actions/runs/43326548978)
- C03-03 Application: [43326549159](https://github.com/crecisol-platform/CRECISOL/actions/runs/43326549159)
- C04-02 MySQL: [43326549061](https://github.com/crecisol-platform/CRECISOL/actions/runs/43326549061)
- C04-08 A/B/C .NET/MySQL: [43326549074](https://github.com/crecisol-platform/CRECISOL/actions/runs/43326549074)
- C03-10 integral: [43326549094](https://github.com/crecisol-platform/CRECISOL/actions/runs/43326549094)
- Foundation: [43326549047](https://github.com/crecisol-platform/CRECISOL/actions/runs/43326549047)

**Esta tabla identifica los runs, NO los considera aprobados por anticipado.** Confirmar individualmente status `completed`, conclusion `success` y mismo head SHA en GitHub antes de certificar.

## 5. Limitaciones P0/P1 y riesgos excluidos

C04-08 NO TIENE autenticación backoffice real, MFA/cuatro ojos, autorización productiva, implementación MySQL de catálogo real, outbox/worker integrado, invalidación real de JWT/cookies/sesiones, caché, medios, exportaciones, CDN, contratos SaaS, cobros, dominios, API privada, DNS, dedicated router ni despliegue. Una suspensión en el fixture no equivale a revocar sesiones reales; accesos residuales solo están bloqueados en la máquina de estados interna y en el fixture. No declarar cobertura BOLA/IDOR de HTTP ni garantías de retención legal/jurídica. Antes de integrar producto real, exigir adaptador transaccional que compruebe aprobación/auth actual, invalidación de epoch y evidencia de denegación de todos los puntos de acceso.

El ensayo no certifica operación ante partición de red/cluster, workers ya ejecutándose ni revocación de tokens emitidos. La CI no sustituye revisión de seguridad de esas superficies FUTURAS. No hay P0/P1 reportado en el alcance estrictamente sintético de las pruebas ejecutadas; los controles de producto se mantienen bloqueados, no descartados.

## 6. Cinco puertas para eficacia institucional

1. PREMERGE técnico del SHA final: 5/5 aplicables SUCCESS, sin P0/P1 en alcance sintético — **VERIFICADO**.
2. POSTMERGE técnico: los siete runs indicados sobre `683837de...` deben terminar `completed/success` — **CONDICIÓN ABIERTA AL REDACTAR**.
3. Este PR documental con especificación, acta y registro de estado debe aprobar TODA su CI PREMERGE en HEAD final — **PENDIENTE**.
4. Fusionar exclusivamente a `dev` sin conflictos, y verificar TODA CI POSTMERGE documental en SHA final — **PENDIENTE**.
5. Publicar comentario permanente en PR documental con cuatro conjuntos de run IDs/SHAs, auditorías A/B/C, límites y cero fallos obligatorios vigentes — **PENDIENTE**.

**Hasta cumplir las cinco puertas: C04-08 NO CERTIFICADA, C04-09 BLOQUEADA.** Después: C04-08 CERTIFICADA **SOLO EN CONTRATOS Y FIXTURE SINTÉTICO NO CONECTADO**, 8/12 microfases, C04 capítulo completo NO CERTIFICADO, 3/37 capítulos completos. Reversión técnica: revertir únicamente squash `683837de5b90278a4166d25e9887a6119f17e869`; no se migró dato productivo. No tocar `main`, Word/PDF, producción, proveedores, pagos ni SOLQARYN.

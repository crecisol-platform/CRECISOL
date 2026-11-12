# CRECISOL — C04-07 · Aprovisionamiento idempotente, sintético y con recuperación

**Estado:** C04-07 NO CERTIFICADA. **C04-08 BLOQUEADA**. 12-11-2026, Honduras.  
**Repositorio autorizado:** `crecisol-platform/CRECISOL`, ID GitHub `1450414592`. **Base `dev` verificada:** `f7bd63cda19cd60dd55a9e2afaceeb3a8b702a7a`; rama `feature/c04-07-aprovisionamiento-idempotente-20261112`.

## 1. Autoridad y requisito trazable

**Documento Maestro Único inalterado:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF de consulta SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. El capítulo C04 son **72 párrafos**, SHA-256 texto `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`. Las 8 puertas MAE-C04 no son las 12 microfases del capítulo.

[Certificación institucional C04-06 #95](https://github.com/crecisol-platform/CRECISOL/pull/95#issuecomment-6835009442) y 4/4 CI documental POSTMERGE **SUCCESS** en `f7bd63cda19cd60dd55a9e2afaceeb3a8b702a7a`: habilita C04-07 únicamente para preparación, sin APIs privadas ni permisos productivos. C01–C03: 3/37 capítulos certificados; C04 completo no certificado.

**Plan original `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`:**

| Código | Entregable | Prueba positiva | Negativa bloqueante |
| --- | --- | --- | --- |
| C04-07 | Aprovisionamiento idempotente | Creación sintética y recuperación ante reintento | No duplicar tenants por carreras |

**Ampliación COM-001, COM-009, COM-010:** se mantienen las tres modalidades de dirección tipadas `PathSlug`, `Subdomain`, `CustomDomain`; este ensayo **no** implementa reserva, publicación, DNS, cobros, contratos, plazos, aceptación, suspensión/reactivación o cambios de planes. Precio, modalidad comercial, paquete, duración y acciones globales son administrables en el futuro; **no hardcodearlos en C04-07**. Nombre interno `business_key` no es slug ni host.

## 2. Alcance EXACTO sin infraestructura productiva

**Domain `backend/src/Crecisol.Domain/Tenancy/TenantProvisioningIdempotency.cs`:** `TenantProvisioningOperationId` GUID opaco no vacío; clave de negocio interna global `TenantProvisioningBusinessKey` canónica ASCII minúscula, 3–64, sin dominio; snapshot confiable `TrustedTenantProvisioningPlanSnapshot` con `OperationId`, `TenantId` preasignado por futuro backend, actor patrocinador, modalidad de dirección, estado Approved/Suspended/Revoked, revisión positiva, vencimiento UTC y `IntentFingerprint` SHA-256 determinista de todos los campos inmutables (incluyendo versión de política), NO firma de autenticación; recibo de store de Draft inmutable y resultado opaco NO AUTORIZANTE. El recibo solo admite `TenantLifecycleState.Draft`, nunca Active.

**Application `backend/src/Crecisol.Application/Tenancy/ProvisionApprovedTenantDraft.cs`:** caso de uso que recibe **solo** `TenantProvisioningOperationId` como selector y `CancellationToken`, sin tenantId, actor, slug, host, plan, DSN, dinero o claims del navegador. Busca plan APROBADO y vigente desde puerto backend `ITrustedTenantProvisioningPlanReader`; invoca UN puerto `IAtomicTenantProvisioningStore` con plan inmutable; rechaza recibo nulo, actor/tenant/op/clave/modalidad/huella distinta; reconsulta aprobación y UTC para detectar cambios durante operación. Operación GUID **NO ES CREDENCIAL NI AUTORIZACIÓN**. Sin adaptador productivo, endpoints, DI o permisos privados.

**Contrato crítico del futuro adaptador:** misma transacción protegida con aislamiento InnoDB y unicidad física de (a) operation_id, (b) tenant_id, (c) business_key global; verificar identidad/aprobación vigente dentro de la transacción; crear fila de operación y tenant **Draft** conjuntamente; cualquier duplicado de mismo op con distinto fingerprint, actor, tenant, business key, modalidad o revisión = CONFLICT/DENY; una repetición de plan idéntico = REPLAY del mismo receipt **solo si ambas filas están completas y coinciden**; distintas operaciones para el mismo nombre/tenant = CONFLICT; cancelación/errores nunca autorizan un segundo tenant ni un fallback de "insert if missing" fuera de transacción. No usar `INSERT IGNORE`, `ON DUPLICATE KEY UPDATE` mutante, SELECT-then-INSERT sin lock ni advisory lock como única defensa. No exponer connection string/DSN.

**MySQL real solamente como ensayo controlado:** `database/tests/c04_07_idempotent_fixture.sql` son dos tablas sintéticas más un procedimiento de prueba, con `UNIQUE`, FK restrictiva, 4 triggers contra cambios/borrados, transacción y validación completa de replay, sin inserción fuera de transacción, sin duplicar empresas. `database/tests/c04_07_idempotency_smoke.sh` arranca MySQL 8.4 fijado por digest en **Docker `--network none`**, usuario efímero con **solo EXECUTE** sobre procedimiento, sin privilegios de SELECT/INSERT directo, pruebas reales de **12 solicitudes concurrentes de igual operación**, **12 solicitudes concurrentes con distinta operación y mismo nombre**, rollback antes de commit, retry después de crash, respuesta perdida después de commit, conflictos de actor/tenant/nombre/modalidad/fingerprint, controles de duplicidad e inmutabilidad; todo en esquema recién creado y eliminado. **No ejecuta migración ni opera datos de clientes.** Los flags `@c04_07_ci_*` y el procedimiento son **solo simuladores de fallos locales**, nunca instalables en entorno real.

**Tests .NET10:** suite `backend/tests/Crecisol.TenantProvisioning.ContractsTests` usa **dobles explícitos**, un fake store con exclusión mutua para comprobar 48 reintentos simultáneos de una operación y 24 solicitudes simultáneas de diferentes operaciones sobre mismo negocio, huella exacta, expiración/revocación, reversión antes de commit, reintentos tras respuesta perdida, cancelaciones y firma de métodos sin parámetros de tenant/host/DSN. El fake NO es una implementación persistente o distribuida ni se conectará al API.

## 3. Modelo de amenazas y límites de certificación

**P0 incluido en alcance sintético:** doble alta por dos solicitudes iguales, doble alta por distintas solicitudes para misma identidad interna, GUID/tenant/actor ajenos, replay con cambios de modalidad, operación sin aprobación, aprobación caducada/revocada, recibo de tenant incorrecto, commit parcial, exclusión de usuario sin permiso para tablas, fallo tras commit y replay incorrecto. Cada caso debe terminar sin crear otro tenant o filtrar datos. La huella detecta cambios pero **no** autentica ni valida una firma; solo plan protegido de backend podrá hacerlo en futura integración.

**P0 NO implementados y NO certificables con estas pruebas:** creación productiva real, autenticación del aprobador, MFA/4 ojos y justificación de altas, identidad/roles, planes comerciales/contratos/pagos, host/slug/DNS reservados y verificados, escritura en catálogo C04-01 y sincronización con SHARED/DEDICATED, enrutamiento de base, bitácoras/outbox/sagas para efectos externos, reactivación/suspensión, consistencia transaccional de otras tablas, cuotas y retención, pruebas HTTP cross-tenant, colas/caché/medios, despliegue o VPS. La lectura de aprobación dos veces no garantiza serializabilidad después del commit. Por diseño no hay ningún servicio real ni superficie que exponga datos de tenant. **No declarar capacidad productiva**.

## 4. Tres auditorías obligatorias y CI secuencial

- **A — trazabilidad y funcionalidad:** verificar Word/PDF maestros y 72 párrafos, fila C04-07, dependencia de C04-06, Domain/Application separados, prueba real de idempotencia y límites correctos.
- **B — seguridad:** amenazas de repetición, actor/tenant/clave/name/mode diferentes, carrera de **una misma operación y de distintas operaciones**, huellas incompatibles, errores/revocación, SQL injection indirecta, mutaciones de cada guard, permisos mínimos SQL, no privates HTTP o datos productivos.
- **C — arquitectura, rendimiento y evidencia:** compilación real `.NET10 Release -warnaserror`, pruebas Domain/Application reales, MySQL 8.4 real en Docker sin red, todos A/B/.NET/MySQL referidos al **mismo SHA/run**; regresiones C04-01..06, C03-10 Angular22/Node24/Chromium/MySQL y Foundation por workflows aplicables, reproducibilidad y rollback. Pruebas documentales históricas `skipped` no cuentan como PASS.

**Gates:** PR técnico PREMERGE último HEAD completo `success`; diff/alcance autorizados revisados; merge solo a `dev` con SHA esperado; **todos los POSTMERGE** técnicos `success`; acta documental **separada**, con PREMERGE + POSTMERGE `success` sobre SHA definitivo y comentario institucional con run IDs. Cualquier fallo exige corrección y ejecución real nueva; preservar historial. P0/P1, `skipped` obligatorio, falta de prueba MySQL/CI u otra inconsistencia = NO_GO. No crear acta válida por anticipado.

**Solo después de todas las puertas:** C04-07 **CERTIFICADA SOLO EN MODELO/ENSAYO INTERNO IDPOTENTE** (7/12), C04-08 HABILITADA **SOLO PARA PREPARACIÓN**; capítulo C04 NO CERTIFICADO, capítulos completos 3/37. Hasta entonces C04-07 NO CERTIFICADA y C04-08 BLOQUEADA. No tocar `main`, Word/PDF, producción, DNS/Cloudflare, VPS, cobros ni SOLQARYN.

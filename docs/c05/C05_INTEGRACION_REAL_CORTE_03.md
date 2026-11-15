# C05 — Evidencia de integración operativa, corte 03

**Fecha:** 2026-11-15 (Honduras)  
**Repositorio:** crecisol-platform/CRECISOL  
**Rama:** `feature/c05-identity-integration-gate-20261114`  
**Dictamen:** **C05 NO CERTIFICADA; C06 BLOQUEADO.** Este corte añade persistencia y consumo transaccional de una prueba de buzón a la ruta de registro neutral. No completa la identidad operativa.

## Cambios

- La solicitud neutral ahora persiste, en una única transacción, la intención, el digest de prueba de posesión y un payload de prueba aleatoria cifrado para entrega posterior. El cifrado del outbox usa una llave independiente (`CRECISOL_C05_PROOF_OUTBOX_ENCRYPTION_KEY`) y AAD ligado al tenant, proof ID, digest y versión.
- Se añadió la migración `002_c05_registration_proof_outbox.sql` con outbox cifrado, vencimiento, intentos limitados y procedimiento transaccional de consumo único. La API expone `POST /api/v1/identity/registration-proofs/confirm` únicamente cuando `CRECISOL_C05_REGISTRATION_ENABLED=true`; el tenant se resuelve desde el Host verificado, no desde el cuerpo.
- Una confirmación válida consume la prueba una sola vez e invalida la entrega pendiente. Confirmaciones inválidas no enumeran buzones, pruebas ni tenants. La prueba de buzón no crea cuenta, aprobación, membresía o sesión.
- Se extendieron las pruebas HTTP/MySQL y de migración para cubrir la nueva tabla, permisos mínimos, persistencia conjunta y rollback 002→001.
- El rol de API solo ejecuta procedimientos `SQL SECURITY DEFINER` para staging y consumo; no puede leer ni insertar directamente el outbox. El procedimiento de staging valida dentro de la misma transacción que la intención pendiente existe y que su tenant y digest de buzón coinciden.

## Evidencia ejecutada en este corte

- `dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release -warnaserror`: **PASS**, 0 advertencias, 0 errores.
- `database/tests/c05_operational_identity_migration_smoke.sh` con MySQL 8.4 descartable: **PASS**, migraciones 001/002, constraints existentes y rollback 002/001.
- `database/tests/c05_operational_registration_http_mysql.sh` con API .NET 10 y MySQL 8.4: **PASS** para solicitudes A/B, tenant no verificado, Host reenviado, cuota y concurrencia; además prueba decrypt controlado del outbox, digest incorrecto, intento cross-tenant, confirmación válida, replay y denegación de SELECT/INSERT directos al usuario de API.
- El arnés de prueba local recupera el payload cifrado desde el MySQL descartable (no desde HTTP), lo descifra solo en memoria para simular la posesión del buzón y ejecuta confirmación incorrecta, intento desde tenant B, confirmación correcta y replay desde tenant A. Se verifica respuesta neutral, contador de fallo, aislamiento, consumo de una sola vez y revocación del outbox pendiente. Esto prueba el ledger/API con un sink sintético; **no entrega correo real ni implementa el worker de entrega**.

## Auditoría A/B/C y estado

- **A — Funcional/trazabilidad:** PASS parcial para registrar intención y prueba ligada al mismo tenant y consumirla una vez mediante el endpoint HTTP; pendiente entrega y continuación a cuenta pendiente.
- **B — Seguridad:** PASS parcial para aislamiento por Host validado, respuesta neutral, digest erróneo, replay, cruce de tenant, límite transaccional y persistencia cifrada; pendiente bloqueo al quinto intento, expiración y límites/temporalidad anti-enumeración.
- **C — Arquitectura/QA/UX:** PASS parcial para compilación API y migración reversible; pendiente canal de entrega del correo, interfaz Angular de confirmación, recorrido Angular→API→MySQL y pruebas de accesibilidad.

Siguen sin implementación operativa aprobación administrativa auditada, credenciales/login, MFA, sesiones, recuperación, cambios sensibles, recursos privados con autorización y cierre integral de Angular. Los fixtures de pruebas existentes no sustituyen estos adaptadores. COM-T08/T09/T10 y proveedores externos siguen **NO EJECUTADOS**.

**Decisión:** mantener PR #151 en DRAFT; C05 no certificada y C06 bloqueado. No fusionar, desplegar, enviar correos reales ni escribir en `dev`, `main`, SOLQARYN o producción en este corte.

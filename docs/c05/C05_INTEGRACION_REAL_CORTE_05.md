# C05 — Integración operativa, corte 05

**Fecha:** 2026-11-15 (Honduras)  
**Repositorio:** `crecisol-platform/CRECISOL`  
**Rama:** `feature/c05-identity-integration-gate-20261114`  
**Commit con código probado:** `024b02c31ccb2d9dcf4030a56b42702e6aac49f6`.  
**Dictamen:** **C05 operativa NO CERTIFICADA; C06 BLOQUEADO.** Este corte implementa y prueba la transición de prueba de buzón a cuenta y membresía pendientes. No certifica el capítulo completo.

## Implementado en este corte

- Migración `003_c05_pending_account_completion.sql` añade la cuota atómica de finalización y procedimientos que validan el buzón una sola vez, guardan el verificador Argon2id, crean una cuenta en estado pendiente y membresía `PENDING`, consumen la prueba, revocan su outbox y registran auditoría. No activa membresías ni crea sesiones.
- API `POST /api/v1/identity/registration-completions` acepta únicamente JSON acotado con identificador, código, buzón y contraseña. Resuelve el tenant por Host verificado, exige tenant activo, comprueba el peer de socket y reserva cuota antes de calcular Argon2id. Todas las respuestas públicas conservan el recibo neutral `202`.
- Angular ahora envía ese contrato y presenta explícitamente la cuenta como pendiente de aprobación. Las claves HMAC se copian a un adaptador persistente antes de limpiar las copias de composición; la prueba real encontró y validó esta condición de ciclo de vida.
- El modo de finalización se habilita solo para pruebas locales: DSN loopback a `crecisol_c05`, hosts `.example.test` y proveedor de contraseñas de prueba. Cuando este modo está activo, la ruta anterior que consumía el buzón sin crear cuenta no se compone.
- Reversión: deshabilitar `CRECISOL_C05_LOCAL_ACCOUNT_COMPLETION_ENABLED` y aplicar `003_c05_pending_account_completion.down.sql`. El API no envía correo real; el código se recupera únicamente del outbox sintético por el arnés aislado.

## Evidencia ejecutada

- `.NET SDK 10.0.404`: `dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release -warnaserror` — **PASS, 0 advertencias, 0 errores**.
- Node `24.15.0`; `npm ci --include=optional` — **250 paquetes, 0 vulnerabilidades**.
- `npm run check:types` — **PASS**.
- `npm run build` (Angular `22.2.4`) — **PASS**, bundle de producción generado.
- `database/tests/c05_operational_identity_migration_smoke.sh` — **PASS** en MySQL `8.4`, migraciones hacia delante, restricciones, auditoría append-only y rollback.
- `database/tests/c05_operational_registration_http_mysql.sh` — **PASS** con MySQL `8.4` descartable, API .NET y Chromium. Verificó hosts y aislamiento entre tenants, outbox cifrado, código erróneo, cruce de tenant, replay, lockout, expiración, privilegio mínimo, cuotas concurrentes; después recorrió Angular → proxy same-origin → API → MySQL y verificó cuenta y membresía pendientes, prueba consumida, ausencia de sesión y bloqueo de la ruta antigua en modo de finalización.
- `npm run test:e2e -- --config=playwright.c05-operational.config.ts` sin variables del arnés — **2 PASS, 1 SKIP**. La prueba saltada requiere una prueba efímera del outbox local; el mismo recorrido pasó **1/1** dentro del arnés MySQL/API.

## Auditorías A/B/C del alcance probado

- **A — Trazabilidad: PASS parcial.** La transición está conectada entre pantalla, endpoint, adaptador MySQL y procedimiento de migración. Estado y auditoría se verificaron en la base descartable.
- **B — Seguridad: PASS parcial.** La prueba real verificó host/tenant, código de un solo uso, denegación cruzada, replay/lockout/expiración, límite previo al hash, privilegio SQL mínimo, respuesta neutral y ausencia de membresía activa/sesión. El código solo se obtiene desde el outbox sintético por el arnés local.
- **C — Arquitectura/ejecución: PASS parcial.** Los builds .NET/Angular, tipos y Chromium se ejecutaron con sus runtimes; la integración HTTP persistió estado en MySQL 8.4. El módulo creado en este corte está restringido a test local.

## Pendiente para completar C05

No se conectaron todavía al API de producto: despacho de correo/outbox, aprobación administrativa autenticada y con MFA, directorio de login Argon2id con rate limit, inscripción/verificación MFA, emisión/rotación/revocación de sesiones, recuperación/cambios sensibles y recursos privados con autorización tenant/sucursal y pruebas BOLA/IDOR. Las implementaciones `MySqlDisposable*` siguen siendo fixtures de prueba, no sustitutos de adaptadores de producto. No se certifican SMTP, KMS, runtime de producción, acceso privado ni C05 completo.

**Estado:** esta integración está implementada y probada en su alcance local; **C05 NO CERTIFICADA; C06 BLOQUEADO**. PR #151 sigue en borrador y sin fusionar. No se modificaron `dev`, `main`, producción ni SOLQARYN.

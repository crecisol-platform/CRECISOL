# C05 — Compatibilidad Windows del arnés local, corte 07

- **Fecha:** 2026-11-15 (Honduras)
- **Repositorio:** `crecisol-platform/CRECISOL`
- **Rama:** `feature/c05-identity-integration-gate-20261114`
- **PR:** #151, Draft, sin fusionar.
- **Base vigente de `dev`:** `8b9e1d5919dda192990c34fb096b235cb94bfec8`.
- **Alcance:** corregir fallos del arnés al ejecutarlo en Windows/Git Bash; no añade rutas ni transiciones de identidad al producto.

## Cambios

- Se normaliza el `CR` final al leer los mensajes `READY` y `MAIL` del proceso SMTP .NET. Windows escribe `CRLF`; Bash conservaba el `CR` e impedía que el arnés aceptara el puerto o el código sintético recibido por SMTP.
- La comprobación concurrente de cuota considera los cubos de minuto realmente persistidos. El ensayo tarda más de un minuto en este equipo y podía cruzar el límite UTC; ahora verifica que cada cubo respeta el máximo de ocho, que al menos uno alcanza el límite y que los intentos admitidos coinciden con las intenciones persistidas.

## Evidencia ejecutada en este equipo

- Archivo oficial `node-v24.15.0-win-x64.zip`: SHA-256 verificado contra `SHASUMS256.txt`; Node `v24.15.0`, npm `11.12.1`.
- `frontend/npm ci` — PASS, 250 paquetes instalados; auditoría npm sin vulnerabilidades reportadas.
- `frontend/npm run check:types` — PASS.
- `frontend/npm run build` — PASS, Angular 22.
- `frontend/npm run test:e2e -- --config=playwright.c05-operational.config.ts` — **2 PASS, 1 SKIP**; el omitido requiere credenciales sintéticas efímeras inyectadas por el arnés integrado.
- `dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release -warnaserror` — PASS, 0 advertencias y 0 errores.
- `database/tests/c05_operational_identity_migration_smoke.sh` — PASS, MySQL 8.4: migraciones, restricciones, auditoría append-only y rollback.
- `database/tests/c05_operational_registration_http_mysql.sh` — PASS, MySQL 8.4 + API .NET 10 + Chromium: recorrido Angular/proxy/API/MySQL/SMTP loopback, aislamiento de tenant, Host falso, proof de un solo uso, lockout/expiración, cuenta y membresía pendientes, sin sesión, permisos SQL mínimos y cuota concurrente.

## Dictamen

Este corte solo corrige y vuelve reproducible la prueba integrada del registro pendiente en Windows. **C05 continúa NO CERTIFICADA; C06 BLOQUEADO.** Siguen sin existir en el producto las rutas operativas de login, MFA, sesiones, recuperación, aprobación y autorización privada; las pruebas actuales de esos contratos no prueban un flujo de producto conectado. No se habilita producción, correo externo, `dev`, `main` ni SOLQARYN.


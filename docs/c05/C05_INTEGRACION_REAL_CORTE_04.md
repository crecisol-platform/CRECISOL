# C05 — Informe de integración real, corte 04

**Fecha:** 2026-11-15 (Honduras)  
**Repositorio:** `crecisol-platform/CRECISOL`  
**Rama:** `feature/c05-identity-integration-gate-20261114`  
**SHA de código probado:** `96ea214cbd2100f6b52d8fff7232a0d1cdf75592`  
**SHA que registra este informe y corrige el runner:** `647c162b2028a23f57d9771116c3e31a7af9b888`  
**Dictamen:** **C05 operativa NO CERTIFICADA; C06 BLOQUEADO.** La evidencia valida únicamente el tramo de registro neutral y consumo de prueba de buzón en el entorno descartable.

## Trabajo y evidencia real

- El API compiló con .NET SDK `10.0.404`, Release y advertencias como errores: **0 advertencias, 0 errores**.
- Con Node `24.15.0`, `npm ci` instaló 250 paquetes; npm informó **0 vulnerabilidades**.
- `npm run check:types`: PASS.
- `npm run build`: PASS; Angular `22.2.4`, bundle de producción generado.
- `database/tests/c05_operational_registration_http_mysql.sh`: **PASS** en MySQL `8.4` descartable. Incluyó Kestrel/.NET, cuotas y concurrencia, hosts verificados/no verificados, aislamiento A/B, outbox cifrado, confirmación errónea, cruce de tenant, confirmación válida, replay, quinto intento, expiración y privilegios SQL mínimos.
- Dentro del mismo script, Chromium completó el recorrido real **Angular → proxy same-origin → API .NET → MySQL 8.4**, consumiendo una prueba creada en esa base: **1/1 PASS**. El código se obtuvo en memoria por el sink local de prueba; no hubo correo real ni código devuelto por el API.
- `npm run test:e2e -- --config=playwright.c05-operational.config.ts`: **2 PASS, 1 SKIP** sin variables del arnés. Se corrigió la condición para que la prueba que requiere el código efímero de MySQL solo se salte cuando no se ejecuta junto con el arnés; el script de integración sí la ejecutó y aprobó.

## Auditorías A/B/C

- **A — Requisitos y trazabilidad: PASS parcial.** La persistencia de intención y la confirmación de buzón recorren Angular, API y MySQL reales. La confirmación no crea identidad operativa ni acceso.
- **B — Seguridad: PASS parcial.** Evidencia real para neutralidad, Host/tenant, replays, intentos, expiración, aislamiento y permisos de base en el tramo probado. No es evidencia de autorización privada de usuario.
- **C — Arquitectura y ejecución: PASS parcial.** Builds y E2E reales ejecutados con sus toolchains; el E2E dependiente de MySQL queda reproducible mediante el script aislado. No se validó correo externo, sesión autenticada ni runtime productivo.

## Bloqueos para C05 completa

El frontend presenta formularios de login, MFA y lectura privada que llaman a rutas que **no están compuestas en `Crecisol.Api/Program.cs`**. No existe todavía una entrega de prueba de buzón operativa: la API persiste el outbox cifrado, pero no se implementó su worker/canal, y el código solo se inyecta desde el sink controlado de pruebas. El producto tampoco implementa el recorrido persistente de creación de cuenta pendiente tras verificar correo, aprobación administrativa con MFA, login Argon2id/rate limit conectado al API, inscripción/verificación MFA, emisión/rotación/revocación de sesión, recuperación y cambios sensibles conectados al API Angular. Los adaptadores `MySqlDisposable*` permanecen restringidos a fixtures descartables y no sustituyen adaptadores de producto.

No se afirman pruebas de SMTP, KMS, sesión autenticada, autorización real de casos, BOLA/IDOR en recursos de producto ni acceso privado Angular→API. Las rutas privadas no deben habilitarse antes de completar su composición y pruebas con persistencia operativa.

## Decisión

**C05 NO CERTIFICADA. C06 BLOQUEADO.** PR #151 continúa DRAFT y sin fusionar; no se tocó `dev`, `main`, producción ni SOLQARYN. El corte 04 deja reproducible y probado el tramo descrito, no cierra el objetivo integral de identidad.

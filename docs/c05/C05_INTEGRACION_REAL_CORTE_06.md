# C05 — Integración operativa, corte 06

- **Fecha:** 2026-11-15 (Honduras)
- **Repositorio:** `crecisol-platform/CRECISOL`
- **Rama:** `feature/c05-identity-integration-gate-20261114`
- **PR:** #151, Draft, sin fusionar.
- **Documento Maestro Único:** SHA-256 Word `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`.
**Dictamen:** **C05 NO CERTIFICADA; C06 BLOQUEADO.** Este corte completa y prueba el despacho local aislado de la prueba de buzón dentro del flujo Angular→API→MySQL. No incorpora entrega de correo real ni autentica usuarios.

## Cambios del corte

- El modo local de finalización registra un worker hospedado por el API. El adaptador de infraestructura no depende del host ASP.NET; un envoltorio de API ejecuta el proceso de cola.
- La migración `004_c05_local_proof_delivery.sql` añade reclamo transaccional con `FOR UPDATE SKIP LOCKED`, propietario de lease, reintento limitado y recuperación de lease vencido. Al llegar al límite o expirar el código, la prueba se revoca. Un trigger mantiene coherente el propietario con el estado; el rollback revierte procedimientos, trigger y columna.
- El worker solo abre el DSN MySQL loopback `crecisol_c05`, utiliza los procedimientos SQL autorizados, descifra en memoria únicamente el registro reclamado, comprueba el digest y envía solo a `@example.test` por SMTP hardcoded a loopback. El código y los datos personales no se escriben en logs. No es un proveedor SMTP de producto.
- El arnés de HTTP ya no extrae desde MySQL el código del recorrido Angular. Un receptor .NET separado lo obtiene mediante una conexión SMTP real de loopback; el test coteja el digest contra el ledger, completa la cuenta pendiente y verifica que no hay sesión.

## Pruebas ejecutadas en el entorno local

- `dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release -warnaserror` — **PASS**, 0 advertencias, 0 errores.
- `database/tests/c05_operational_identity_migration_smoke.sh` — **PASS**, MySQL 8.4: migraciones 001→004, finalización pendiente, fallo/reintento, éxito de entrega, recuperación de sender detenido, revocación tras tres leases expirados, auditoría append-only y rollback 004→001.
- `database/tests/c05_operational_registration_http_mysql.sh` — **PASS**, API .NET 10 + MySQL 8.4 desechable + Angular/Chromium. El recorrido cubrió Host/tenants, denegaciones neutrales, digest por SMTP independiente, DB least-privilege, prueba de un uso/lockout y cuenta/membresía PENDING sin sesión.
- `cd frontend && npm run check:types` — **PASS**.
- `cd frontend && npm run build` — **PASS**, Angular 22, bundle de producción generado.
- `python3 qa/c05_11_integral_audits.py A` y `B` — **PASS**: huellas maestras y trazabilidad verificadas; 128 mutaciones negativas rechazadas. Son auditorías estáticas y no sustituyen integración operacional completa.
- `cd frontend && npm run test:e2e -- --config=playwright.c05-operational.config.ts` — **2 PASS, 1 SKIP**. El caso omitido requiere las variables de proof efímero y su mismo recorrido pasó **1/1** en el arnés API/MySQL/SMTP.

## Auditoría A/B/C de este incremento

- **A — PASS para el incremento.** Los cambios se limitan al despacho loopback, sus procedimientos/rollback, el arnés y esta evidencia. Word/PDF, `dev`, `main`, producción y SOLQARYN no cambian.
- **B — PASS para el incremento.** MySQL controla leases y reintentos; la cuenta se crea solo tras proof válido y queda pendiente. El worker no permite destinos externos, el código no se devuelve al navegador antes de su envío por SMTP, se mantiene antienumeración y no se emite una sesión.
- **C — PASS parcial.** Se ejecutaron compilación .NET, construcción Angular, MySQL 8.4 y el recorrido de Chromium con SMTP separado. No se ha integrado ni probado autenticación de producto, aprobación administrativa/MFA ni permisos privados reales.

## Pendiente para certificar C05

La integración funcional de registro/entrega es exclusivamente local y sintética. Continúan pendientes en la API y Angular de producto: login contextual con Argon2id y rate-limit integrado; aprobación administrativa con reautenticación/MFA y bitácora; enrolamiento y verificación MFA; emisión/rotación/revocación de sesiones; recuperación y cambios sensibles; autorización privada por tenant, sucursal, actor y recurso con BOLA/IDOR; y pruebas reales de esos flujos unidos. Los contratos y adaptadores `MySqlDisposable*` no reemplazan adaptadores operacionales ni la seguridad de runtime.

Por tanto, el incremento es **IMPLEMENTADO Y PROBADO EN SU ALCANCE LOCAL**, pero no se eleva a certificación de C05-02 ni del capítulo. C05 permanece **NO CERTIFICADA; C06 BLOQUEADO**. No habilita SMTP real, KMS, despliegues, datos de clientes, dominios, VPS, cobros o escritura en SOLQARYN.

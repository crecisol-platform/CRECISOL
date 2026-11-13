# CRECISOL — C05-05 · Sesiones y revocación, contrato técnico y ensayo HTTP/MySQL

**Fecha:** 2026-11-13. **Repositorio:** `crecisol-platform/CRECISOL` (ID 1450414592). **Base:** `dev` `358df52d785cb4074790ce7841c5c8d0d4873908`. **Rama:** `feature/c05-05-session-revocation-20261113`. **Estado:** DESARROLLO Y PRUEBAS; NO CERTIFICADA hasta evidencias, acta propia y auditorías A/B/C; **C05-06 BLOQUEADA**. No modificar main, SOLQARYN, Documento Maestro, VPS, DNS, Cloudflare ni producción.

## 1. Requisito, fuentes y dependencia

Documento Maestro Único: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` y `docs/MASTER_REFERENCE.md` (Word SHA256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`). Capítulo 05 leído en [run #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). Plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`: **C05-05 Sesiones y revocación**; positivo: **«Rotación y logout verificables en API»**, negativo: **«Token revocado no vuelve a funcionar»**. Precedente C05-04 [certificado solamente en contexto pre-sesión y datos sintéticos](https://github.com/crecisol-platform/CRECISOL/pull/134#issuecomment-6863804285) con `dev` `358df52d785cb4074790ce7841c5c8d0d4873908`. No se infiere identidad real, MFA, contraseñas persistidas, navegador, sesión de plataforma ni permisos productivos de sus contratos.

## 2. Arquitectura de este incremento

| Capa / archivo | Implementación comprobable | Límite deliberado |
| --- | --- | --- |
| `Application/Identity/SessionLifecycleContracts.cs` | Puerto tipado de huella, sesión y ledger; `ScopedSessionLifecycle` exige `ContextualLoginPreparation.Ready` de C05-04, token opaco y alcance `VerifiedTenantHostHint` backend para verificar/rotar/logout; no ID de tenant recibido directamente | No crea rol, acción, permiso, sesión autenticada para API productiva, JWT, cookie ni DSN |
| `CredentialSecurity/OpaqueSessionTokenProtector.cs` | 32 bytes de CSPRNG por token; codificación estricta `cs5_` + 43 caracteres base64url canónicos; clave HMAC-SHA256 independiente de 32 a 64 bytes exclusivamente del backend; `ToString` redactado; borrado de bytes y clave descartada en `Dispose` | Clave solo aleatoria de la prueba; proveedor productivo de secretos/rotación y HTTPS pendiente |
| `Infrastructure/Identity/MySqlDisposableSessionLedger.cs` | MySqlConnector@2.6.2 existente, sin alterar csproj histórico; DB local `c05_05_disposable` exclusiva, DSN del servidor, sin SELECT/UPDATE directos; cuatro procedimientos CAS con control de estado y alcance | Nunca se registra en API/DI productiva, no enruta DSN SHARED/DEDICATED real |
| `database/tests/fixtures/c05_05_disposable_sessions.sql` | Dos tablas InnoDB sintéticas: sujetos con revisiones/estado, sesiones con **únicamente huella HMAC**, generation, UTC, revocación. Issue SQL comprueba atomía de revisiones account, credential, membership, catalog, host y pertenencia; validate comprueba vigencia, coincidencia tenant/actor, estado actualizado y revoca al detectar suspensión; rotate actualiza una sola huella con lock y generación; logout revoca | Fixture temporal, no migración de credenciales ni sesiones productivas; cambios de identidad reales deben transaccionar con el mismo directorio |
| `backend/tests/Crecisol.SessionRevocation.MySqlTests` + `database/tests/c05_05_session_http_mysql.sh` | MySQL 8.4 real en contenedor efímero, puerto 127.0.0.1, dos identidades de tenant simuladas y C05-04 + Argon2id reales; servidor ASP.NET 10 HTTP aislado en loopback con rutas `/__c05_05_test__/...`; llamadas HttpClient reales y 16 rotaciones concurrentes | Servidor **SOLO de prueba**, no se añade ninguna ruta privada a `backend/src/Crecisol.Api/Program.cs` |

**Tokens:** aleatorios opacos 256-bit; nunca llevan tenant, usuario, email, roles o claims. Solo se almacena huella HMAC-SHA256 generada con secreto de backend. Un token emitido bajo tenant A no verifica ni rota bajo B incluso si el actor tiene dos vínculos. Rotación atómica sustituye la huella en una transacción: el token anterior se vuelve inservible. Logout deja `revoked_utc` permanente y una nueva comprobación no puede reactivarlo. La caducidad se decide por reloj UTC de MySQL, 15 min de inactividad desde emisión/rotación con límite absoluto de 8 horas y tope de validez de pertenencia. Revocaciones por cambios de credential/subject revision no se deshacen restaurando un estado activo. `generation` controla rotación.

**API de ensayo:** `POST /__c05_05_test__/login`, `GET /__c05_05_test__/validate`, `POST /__c05_05_test__/rotate`, `POST /__c05_05_test__/logout`. El servidor solo enlaza `127.0.0.1`, admite exclusivamente hosts sintéticos verificados `alpha.example.test` y `beta.example.test`, bloquea `Forwarded`, `X-Forwarded-Host`, `X-Tenant-Id`, ignora cualquier selector (el payload de login exige solo email y password), responde sin caché/cookies, devuelve genéricamente `401` a tokens inválidos y `204` neutral a logout. El transporte HTTP en loopback **NO** es aceptable para usuarios finales; la activación productiva exigiría TLS, cookies seguras o bearer aislado y defensas CSRF/CORS según esquema.

## 3. Casos de aceptación, negativas y riesgos

| Reto | Evidencia positiva | Evidencia negativa |
| --- | --- | --- |
| Emisión | Argon2id real C05-04, host A/B y ledger MySQL CAS comprueba revisiones | No emitir ante contraseña errónea, host ajeno, email desconocido, tenant_id extra, cuenta/credencial revocada |
| Tokens | 256-bit CSPRNG, HMAC con clave aleatoria, servidor sin secrets en Git | Token inválido, truncado, padding, clave diferente y falta de huella en MySQL deniegan |
| Validación | HTTP `204` con token actual de tenant correcto | Token de A bajo host B, antiguo, vencido, revocado o modificado da `401` |
| Rotación | HTTP devuelve token nuevo, `generation` cambia y huella previa desaparece | Reutilizar viejo token falla; **16 rotaciones concurrentes, exactamente 1 gana** |
| Logout | HTTP neutral `204` y MySQL persiste `revoked_utc` | Logout cruzado no toca otra empresa; sesiones revocadas jamás resucitan |
| Revocación de credencial | Invalida una sesión en la siguiente validación backend y marca revocación permanente | Recuperar estado activo no reactiva token; caducidad forzada no habilita refresh |
| Privilegios DB | Cuenta solo `EXECUTE` procedimientos, FK compuesta y `SELECT` privilegiado separado del adapter | SELECT directo de huellas desde cuenta ejecutora es rechazado |
| Arquitectura | C03/Angular/Chromium/infraestructura previa sin cambios | Cero API productiva privada, ninguna regla de autorización/rol de empresa por token |

**P0/P1 pendiente de integración productiva (NO-GO fuera de fixtures):** directorio real de identidad persistente con pruebas de cierre de carrera con emisión, ticket de login de un solo uso con ventana de frescura/step-up, clave HMAC protegida y procedimiento de rotación, HTTPS real y decisión de cookie segura/CSRF/samesite, sesión entre dispositivos y cierre global, replay de antiguos tokens con respuesta de incidente y revocación de familia, rate limit distribuido, MFA, recovery y auditoría privada sin tokens. Los ensayos de fixtures no demuestran esos controles. No se certifica emisión de sesión para usuarios reales ni autorización de acciones tenant/sucursal/recurso.

**Impacto:** nuevos contratos internos y fixture desechable; no migración productiva, no UI, ni cambio de main. **Rollback:** revertir exclusivamente el squash técnico/doc de C05-05 en `dev` tras valorar dependencias, sin afectar C05-01..04 certificados.

## 4. Puertas innegociables

**Auditoría A:** source Word/PDF, capítulo, requisito/positivo/negativo, C05-04 previa, HTTP verificable. **B:** tokens/huellas/HMAC, aislamiento A/B, revocación/rotación/expiración, carreras 16-way, SQL privilegio mínimo y 0 secretos en logs. **C:** arquitectura limpia, alcance de rutas de API productiva intacto, C03-04 MySqlConnector pin intacto, .NET10, MySQL8.4, Foundation y regresiones C05-01/02/03/04, SHAs/runs PREMERGE y POSTMERGE, acta institucional separada.

**C05-05 NO CERTIFICADA, C05-06 BLOQUEADA** hasta contar con evidencia real y dictamen permanente de la microfase. En el mejor caso, certificación **acotada** al núcleo de sesiones y al servidor HTTP sintético de prueba; nunca sesión productiva o capítulo C05 completo.

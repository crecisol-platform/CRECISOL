# CRECISOL — C05-06 · Recuperación segura de contraseña (entorno aislado)

**Fecha:** 2026-11-13. **Repositorio exclusivo:** `crecisol-platform/CRECISOL`, ID 1450414592, base `dev` `f34a751ee10dddc47c8f75fb49291a4741d5cca5`, rama `feature/c05-06-password-recovery-20261113`. **C05-06 EN DESARROLLO, NO CERTIFICADA; C05-07 BLOQUEADA.** Sin producción, VPS, DNS, Cloudflare, cuentas reales, `main`, SOLQARYN ni modificaciones del Documento Maestro.

## 1. Fuente canónica y alcance

Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, hash Word SHA256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF lectura hash `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`, estado `docs/MASTER_REFERENCE.md`, capítulo 05 extraído en [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). El plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md` define **C05-06 Recuperación de contraseña**, requisito positivo **«Tokens de un solo uso con expiración y revocación»**, negativo **«No reutilizar enlaces»**. La [certificación C05-05](https://github.com/crecisol-platform/CRECISOL/pull/136#issuecomment-6864907945) habilita preparar C05-06 pero **no afirma** identidad, correo ni sesiones productivas.

## 2. Arquitectura, autorización y datos

| Capa / archivos | Contrato implementado | Límite obligatorio |
| --- | --- | --- |
| `backend/src/Crecisol.Application/Identity/PasswordRecoveryContracts.cs` | `RecoverPasswordOnce.RequestAsync` recibe email canónico y un `VerifiedTenantHostHint` **producido por backend**. Solicitud HTTP neutral independientemente de existencia; ni token ni actor ni email del directorio se retornan. `RedeemAsync` requiere token y nueva contraseña de política; el resultado no crea una sesión. | Nunca acepta tenant_id, roles, pertenencia, DSN ni autorización desde navegador. No registra DI/rutas productivas. |
| `backend/src/Crecisol.CredentialSecurity/PasswordRecoveryCrypto.cs` | Tokens opacos CSPRNG de **256 bits**, `cr6_` + base64url canónico; índice **HMAC-SHA256** con clave propia; huella de mailbox por HMAC y contexto separado con otra clave independiente. Claves 32–64 bytes, limpieza de buffers y `Dispose`, redacción de `ToString`. `Argon2idRecoveryPasswordEncoder` reutiliza C05-03 con chequeo de contraseñas comprometidas estricto. | En CI las claves son aleatorias y efímeras. Su gestor seguro, rotación y entrega productiva todavía no existen. |
| `backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePasswordRecoveryLedger.cs` | Adaptador MySqlConnector 2.6.2 de solo procedimientos `EXECUTE`, DB `c05_05_disposable` únicamente en 127.0.0.1. Devuelve `DENIED` sin revelar existencia. Los digest y el PHC no se imprimen. | No enrutamiento multitenant productivo, cuenta/SMTP real, ni API productiva. |
| `database/tests/fixtures/c05_06_disposable_recovery.sql` | Tres tablas efímeras (credenciales PHC Argon2id, mailboxes verificables como HMAC, desafíos como HMAC); FK compuesta `tenant_id,actor_id`, unique digest; 2 procedimientos CAS; 3 triggers de incremento obligatorio de revisiones de buzón, sujeto y credencial. Emisión vigente **10 minutos**; nueva emisión revoca enlaces anteriores del actor global; consume solo en tenant/host probado, estado pendiente, mailbox y revisiones actuales. | Es un fixture adicionado a `c05_05_disposable_sessions.sql`, **NO** migración de producción ni persistencia real. |
| `Crecisol.PasswordRecovery.MySqlTests` + script `c05_06_recovery_http_mysql.sh` | API **ASP.NET 10 real SOLO EN LOOPBACK** `/__c05_06_test__/request` y `/__c05_06_test__/redeem`, cliente HTTP real, MySQL 8.4 en contenedor temporal y buzón de entrega **en memoria**. Test de dos tenants A/B, tercer actor y credencial de operador de plataforma, 16 consumos en carrera. | La entrega real de email, verificación de propiedad del buzón, cifrado de outbox y conexión de usuarios verdaderos NO están implementadas. |

**Atomicidad:** `c05_06_consume_once` usa transacción `START TRANSACTION` y bloqueo `FOR UPDATE`: comprueba token pendiente/no vencido, tenant exacto, buzón registrado todavía verificado, revisiones de cuenta/credencial/membresía/host/tenant, actor activo y realm `TENANT` (no `PLATFORM`). Solo entonces sustituye el PHC Argon2id y aumenta revisión global; revoca todas las sesiones del actor en A y B, aumenta `subject_revision` para todos sus tenants, revoca todos sus desafíos pendientes y marca el token consumido con UTC. Si una parte falla, la transacción se revierte. Un segundo consumo, otro host o un enlace antiguo no pueden restaurar credenciales ni sesiones.

**Anti-enumeración:** `POST /__c05_06_test__/request` acepta solo `email` en JSON de tamaño limitado, host sintético verificado y responde siempre `202` sin payload/token/Set-Cookie para cuentas conocidas/desconocidas/deshabilitadas de formato válido. Correo de prueba solo sale al sink interno si el servidor confirma que está registrado/verificado; nunca se revela al navegador. `POST /__c05_06_test__/redeem` recibe `token` en cuerpo POST, **no en URL ni query**, más `newPassword`; devuelve 204 para consumo exitoso y 401 genérico para token inválido/expirado/reutilizado o política de password denegada. Todas las respuestas tienen `Cache-Control: no-store` y `Referrer-Policy: no-referrer`. Cookies y sesiones no son emitidas. Esta neutralidad de código/cuerpo **no prueba** equivalencia de latencia productiva ni rate limiting, pendientes de C05-10.

## 3. Matriz de seguridad de ejecución real

| Reto | Negativas obligatorias |
| --- | --- |
| Buzón no verificable | Email desconocido, mailbox revocado, operador `PLATFORM`, host ajeno, tenant/header falsificado: `202` neutral sin token o `400` para host inválido; no entregar enlaces. |
| Secreto de recuperación | Token 256-bit, HMAC de token, HMAC distinto de email, no token/email claro en SQL, clave incorrecta, truncado, padding y formato alterado rechazados. |
| Un solo uso | Expiración 10 min del reloj de MySQL, consumo una vez, enlaces previos revocados al emitir otro, `16` clientes reales simultáneos compitiendo por un desafío: **exactamente 1 ganador, 15 denegados**. |
| Revocaciones y races | Revocar buzón, reactivarlo, suspender/restaurar membresía y renovar revisiones invalida permanentemente enlaces viejos. No confundir A y B aunque actor sea global. |
| Contraseña real | Hash Argon2id reusa C05-03 y criba comprometidas: débil/comprometida/servicio de criba caído = denegación. Cambio PHC se verifica: anterior deja de funcionar, nueva sí. |
| Sesiones | Al reset válido, sesiones anteriores del actor en **A y B** revocadas en la misma transacción; sesión de otro actor sigue vigente, y **no** se crea nueva sesión. |
| Privilegios DB | Runner con `EXECUTE` en procedimientos acotados, SELECT de PHC/tabla privada denegado; FK tenant+actor y ausencia de tokens y mails en claro. |
| Arquitectura | C05-01..05 regresión real .NET10 y C03 integral Angular22/Chromium/MySQL8.4; ninguna modificación a `Crecisol.Api/Program.cs`. |

## 4. P0/P1 residuales y no-go de producción

**NO-GO para login/recuperación productiva** hasta integrar un directorio real con buzón verificado independiente de invitación, entrega SMTP real mediante outbox **durable, cifrado y transaccional** (la prueba solo tiene sink de memoria), protección/rotación de claves HMAC con KMS/secret-manager, autenticación humana/consentimiento/recuperación de cuenta separada, neutralidad temporal y rate limit distribuido, política contra contraseñas comprometidas real disponible, trazas y notificación de restablecimiento sin secretos, TLS y defensas CSRF/CORS según esquema de credencial, política de MFA/step-up/passkeys de C05-08, control anti-replay y de incidentes, pruebas ofensivas con almacenamiento shared/dedicated y autorización real por recurso. No confundir SQL protegido y datos sintéticos con un backend listo para operar. **Resetear una contraseña NO reinicia MFA, NO concede sesión y NO desbloquea consola SuperAdmin**.

**No se toca `main`, SOLQARYN, Word/PDF, API productiva, frontend ni migraciones operativas.** Rollback: revertir solo los commits/PR propios de C05-06 en `dev` tras revisar dependencias; no alterar certificaciones C05-01..05.

## 5. Criterios de cierre

**Auditoría A (funcional):** fuente Word, alcance C05-06 y C05-05 anterior, pruebas de email neutral, expiración/reutilización, validación PHC Argon2id, revocación sesiones. **Auditoría B (seguridad):** token 256-bit/HMAC, scopes host A/B, cuentas PLATFORM, SQL locks/CAS/privilegios, vencimiento/estado/revisión y adversarios; 0 P0/P1 abierto *en alcance de fixture*. **Auditoría C (arquitectura):** diff autorizado, .NET10 y MySQL 8.4 reales, C03/Angular/Chromium, documentación, reversión, SHA de CI, permisos mínimos.

**C05-06 NO CERTIFICADA; C05-07 BLOQUEADA** hasta PREMERGE y POSTMERGE técnicos completos, acta separada, auditorías A/B/C y evidencia real final. Solo será posible certificar el núcleo **aislado y sintético**; no recuperación de usuarios reales ni capítulo C05 completo.

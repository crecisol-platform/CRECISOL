# CRECISOL — C05-06 · Acta institucional CONDICIONADA de recuperación de contraseña

**Fecha:** 13 de noviembre de 2026. **Repositorio autorizado exclusivo:** `crecisol-platform/CRECISOL` (ID GitHub 1450414592). **Rama de ratificación:** `feature/c05-06-recovery-certification-20261113`, creada desde `dev` `2620d6fb274bd0fd6d8d6af24b1f9744fd08664b`. **ESTADO AL REDACTAR: NO EFICAZ; C05-06 NO CERTIFICADA, C05-07 BLOQUEADA.** Solo ejecución de código/pruebas, sin VPS, DNS, Cloudflare, correo real, cuentas productivas, cambios en main/SOLQARYN, Word/PDF o cobros.

## 1. Autoridad y requisito

Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, `docs/MASTER_REFERENCE.md`, SHA-256 del Word `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` y del PDF de lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; capítulo 05 extraído en [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). Plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`: **C05-06 Recuperación de contraseña**, positivo **«Tokens de un solo uso con expiración y revocación»**, negativo **«No reutilizar enlaces»**. Precedente: [C05-05 certificada exclusivamente en sesiones HTTP y MySQL sintéticos](https://github.com/crecisol-platform/CRECISOL/pull/136#issuecomment-6864907945), `dev` `f34a751ee10dddc47c8f75fb49291a4741d5cca5`. No atribuir credenciales reales, SMTP, MFA ni sesiones productivas a precedentes.

## 2. Implementación técnica PR #137 (diez archivos, NO productiva)

[PR técnico #137](https://github.com/crecisol-platform/CRECISOL/pull/137), HEAD final `0884837bb8ba75d8f73e47847075c2a8e485d912`, squash exclusivo `dev` `2620d6fb274bd0fd6d8d6af24b1f9744fd08664b`, diff exactamente diez archivos:

- **Application/Identity**: `PasswordRecoveryContracts.cs` incluye solicitud neutral con `CanonicalLoginHandle` y `VerifiedTenantHostHint` del backend, token de un solo uso y `RecoverPasswordOnce`. No elige empresa, sucursal ni actor desde navegador; no concede roles ni sesión. Respuesta pública uniforme a emails bien formados registrados/desconocidos/deshabilitados.
- **CredentialSecurity**: `PasswordRecoveryCrypto.cs` crea secreto aleatorio **256-bit CSPRNG**, índice HMAC-SHA256, una clave independiente para el digest de mailbox y otra para el token, ambas suministradas por backend y efímeras en las pruebas. `Argon2idRecoveryPasswordEncoder` reutiliza Argon2id real C05-03 y cribado estricto de contraseñas comprometidas, sin aceptar caídas de cribado.
- **Infrastructure**: `MySqlDisposablePasswordRecoveryLedger.cs` reutiliza `MySqlConnector@2.6.2` histórico; solo DB local `c05_05_disposable`, usuario ejecutor `EXECUTE` protegido. No SELECT/UPDATE directos, DSN de cliente ni rutas productivas.
- **Database**: `database/tests/fixtures/c05_06_disposable_recovery.sql` añade tres tablas InnoDB efímeras, dos procedimientos protegidos y tres triggers de incremento obligatorio de revisiones. La emisión comprueba buzón verificado, cuenta/membresía/host/tenant activos y realm `TENANT`, dura **10 minutos UTC** y revoca enlaces pendientes anteriores del actor global. El consumo usa `FOR UPDATE` y una sola transacción: rechaza token viejo/expirado/revocado, buzón rebindeado, credencial/catálogo/host/membresía modificados; reemplaza PHC, aumenta revisión, revoca sesiones previas de **todos los tenants del mismo actor**, revoca desafíos pendientes y marca el único token `CONSUMED`. No modifica ni resetea MFA.
- **Ensayo HTTP real**: `Crecisol.PasswordRecovery.MySqlTests` y `database/tests/c05_06_recovery_http_mysql.sh` levantan ASP.NET 10 **únicamente en 127.0.0.1** y MySQL 8.4 desechable. `POST /__c05_06_test__/request` devuelve 202 neutral, sin token/cookie; `POST /__c05_06_test__/redeem` recibe token solo en cuerpo POST y devuelve 204 o 401 genérico. El correo está representado **EXCLUSIVAMENTE por sink en memoria**, no hay SMTP. Se prueban dos tenants A/B del mismo actor, otro actor independiente, operador de plataforma excluido, enlace de uso único, expiración, revocaciones/reverificaciones, política Argon2id y carrera de 16 solicitudes.
- **Calidad y fuente**: `qa/c05_06_recovery_audits.py`, `.github/workflows/c05-06-recovery.yml`, `docs/c05/C05_06_ESPECIFICACION.md`. La API productiva `backend/src/Crecisol.Api/Program.cs` conserva solo `/api/v1/public/branding/platform`; **ningún endpoint real de recuperación, sesión o login**.

## 3. Evidencia positiva/negativa real y auditorías A/B/C

**PREMERGE técnico en HEAD final — 3/3 SUCCESS:**

| Gate | Ejecución | Evidencia |
| --- | --- | --- |
| C05-06 A/B/C + .NET 10 + HTTP/MySQL 8.4 | [#43510570679](https://github.com/crecisol-platform/CRECISOL/actions/runs/43510570679) | completed/success: **Auditoría A**, **174 mutaciones estáticas de seguridad rechazadas (B)**, **91 aserciones funcionales HTTP/MySQL8.4 reales**, **Auditoría C** del SHA/run exacto, regresiones reales C05-01 **100**, C05-02 **102**, C05-03 **68**, C05-04 **95**, C05-05 **52** |
| Foundation | [#43510570816](https://github.com/crecisol-platform/CRECISOL/actions/runs/43510570816) | completed/success |
| C03 integral .NET 10 / Angular 22 / Chromium / MySQL 8.4 | [#43510570762](https://github.com/crecisol-platform/CRECISOL/actions/runs/43510570762) | completed/success |

La prueba HTTP/MySQL incluye 16 consumos concurrentes del mismo enlace (**exactamente 1 éxito y 15 rechazados**), correo conocido/desconocido indistinguible por código/cuerpo 202, link revocado al reemitir, link expirado, reutilización imposible, HMAC de mailbox y token separados, PHC Argon2id realmente sustituido, contraseña antigua rechazada, caída de cribado fail-closed, bloqueo de operador PLATFORM y de host/tenant alterado, invalidación ante verificación/reverificación del correo y pertenencia, revocación real de sesiones A+B del actor y sesión del tercero sin afectar. SQL confirma 5 tablas, 6 procedimientos, 3 triggers y 2 FK tenant/actor con ningún token/email/password claro en challenges/mailboxes.

**POSTMERGE técnico PR #137, SHA `2620d6fb274bd0fd6d8d6af24b1f9744fd08664b`: inició y ESTÁ PENDIENTE de conclusión AL REDACTAR:**
1. [C05-06 #43511437305](https://github.com/crecisol-platform/CRECISOL/actions/runs/43511437305)
2. [Foundation #43511437348](https://github.com/crecisol-platform/CRECISOL/actions/runs/43511437348)
3. [C03-03 Application #43511437334](https://github.com/crecisol-platform/CRECISOL/actions/runs/43511437334)
4. [C03-04 Infrastructure #43511437395](https://github.com/crecisol-platform/CRECISOL/actions/runs/43511437395)
5. [C03-10 integral #43511437332](https://github.com/crecisol-platform/CRECISOL/actions/runs/43511437332)

Los errores CS8602 (nulabilidad) y CS4007 (`ReadOnlySpan<char>` atravesando un `await`) del primer intento [#43510234402](https://github.com/crecisol-platform/CRECISOL/actions/runs/43510234402) siguen como fallos históricos reales; se corrigió el test, se volvió a ejecutar toda la batería y **no** se degradó `-warnaserror`. No se confunden workflows `skipped` con `SUCCESS`.

## 4. Matriz adversaria y límites P0/P1

| Control | Prueba ofensiva real | Límite productivo pendiente |
| --- | --- | --- |
| Identidad y anti-enumeración | 202 sin token en respuesta para email conocido/desconocido, usuario PLATFORM sin enlace, host falso rechazado | Mailbox real verificado, latencia indistinguible, rate limiting por IP/actor/tenant, antifraude y auditoría |
| Token single-use, 10 min y HMAC | Caducado, formato inválido, token de A en B, reutilización, nuevo enlace revoca anterior, 16 CAS con un ganador | Secret manager/KMS, seguridad del enlace por canal, distribución SMTP y retención |
| Buzón, membresía, credencial | Revocación y rearme de mailbox/membresía causan revisión irreversible, actor/realm y estado examinados por SQL | Directorio real seguro, emisión/outbox persistentes atómicos, reconexión transaccional con stores reales |
| Argon2id y sesiones | PHC cambia en DB, anterior contraseña deja de verificar, pantalla de comprometidas caída niega; sesiones globales A/B revocadas, tercero intacto | Sesiones productivas multi-dispositivo, rotación de claves, recovery MFA/passkeys, step-up y branch/resource policy |
| Arquitectura y pruebas | MySQL 8.4 real local, .NET10, C05 anteriores, A/B/C, CI SHA, API productiva intacta | HTTPS/cookies/CSRF reales, SIEM, outbox cifrado, UI/UX accesible |

**NO-GO total para producción**: sin email confiable ni entrega real, sin usuario real autenticado, sin proveedor productivo de contraseña comprometida, sin sesiones reales ni MFA implementada, sin rate limit distribuido; no hay rutas de recovery en la API productiva ni configuración de secretos. Afirmar que esta acta habilita la recuperación de cuentas de clientes sería incorrecto. Estos controles deben ejecutarse en microfases propias antes de desplegar. No aprobar acceso privado con solo un token sintético.

## 5. Condiciones de vigencia de esta acta

**Auditoría A:** fuente canónica/precedente, exigencia positiva/negativa C05-06 y pruebas reales. **B:** control HMAC, PHC, single-use, expiración, 16-way CAS, huellas, aislamiento tenant y revocación all-sessions; seguridad frente a un enlace reaparecido. **C:** diff EXACTO del acta/QA/workflow y validación SHA/run, arquitectura Application-CredentialSecurity-Infrastructure, compilación .NET10, MySQL8.4 real, C05-01..05, regresión C03, no API productiva y rollback.

**SEIS puertas obligatorias:** (1) 5/5 POSTMERGE técnico completed/success en SHA `2620d6fb...`; (2) PR de acta con **exactamente tres archivos** y CI A/B/C + .NET10/HTTP/MySQL 8.4 real y C05-01..05; (3) PREMERGE documental 2/2 completed/success en HEAD final; (4) squash del acta **solo a dev** con HEAD fijo; (5) POSTMERGE documental 2/2 completed/success en SHA final; (6) comentario institucional permanente con SHA, runs, límites y rollback. **Hasta entonces: C05-06 NO CERTIFICADA; C05-07 BLOQUEADA**.

**DICTAMEN SOLO CUANDO TODAS LAS PUERTAS APRUEBEN:** **C05-06 CERTIFICADA EXCLUSIVAMENTE EN CONTRATOS Y PROTOTIPO HTTP/MySQL 8.4 AISLADO Y SINTÉTICO (6/11 C05)**. C05-07 habilitada SOLO para preparación/desarrollo/QA de su alcance; **capítulo C05 completo NO CERTIFICADO; C06 BLOQUEADO**. Sin usuarios, correo, token o sesión reales.

**Rollback:** revertir exclusivamente el squash del acta, y tras evaluación de dependencias el squash técnico `2620d6fb274bd0fd6d8d6af24b1f9744fd08664b` en `dev`, preservando historial de fallos y certificaciones C05-01..05; no cambiar Word/PDF, `main`, SOLQARYN, cloud ni VPS.

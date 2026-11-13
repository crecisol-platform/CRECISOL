# CRECISOL — C05-05 · Acta institucional CONDICIONADA de sesiones y revocación

**Fecha:** 2026-11-13. **Repositorio único autorizado:** `crecisol-platform/CRECISOL`, ID `1450414592`. **Rama:** `feature/c05-05-session-certification-20261113`, creada desde `dev` `c30bafa20add32b6042283e0c9f853d177d603d3`. **ESTADO AL REDACTAR: PROPUESTA NO EFICAZ; C05-05 NO CERTIFICADA Y C05-06 BLOQUEADA.** No main, SOLQARYN, producción, Word/PDF, VPS, DNS, Cloudflare ni cobros.

## 1. Fuente canónica y alcance de microfase

Documento Maestro Único CRECISOL (`docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, registro `docs/MASTER_REFERENCE.md`), SHA256 Word `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF de lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; capítulo 05 extraído en [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). El plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md` fija **C05-05 Sesiones y revocación**, positivo **«Rotación y logout verificables en API»**, negativo **«Token revocado no vuelve a funcionar»**. Solo después del precedente [C05-04 certificado en contratos sintéticos](https://github.com/crecisol-platform/CRECISOL/pull/134#issuecomment-6863804285), `dev` `358df52d785cb4074790ce7841c5c8d0d4873908`. No se traspasan autorizaciones reales de C05-04 a este código.

## 2. Implementación técnica PR #135

[PR técnico #135](https://github.com/crecisol-platform/CRECISOL/pull/135), HEAD final `3b912a08b019a80c0cffbdfd1c1cf36aa0918d5f`, squash exclusivo a `dev` `c30bafa20add32b6042283e0c9f853d177d603d3`, diff exacto de diez archivos:

- `Application/Identity/SessionLifecycleContracts.cs`: tipos `SessionTokenDigest`, `IssuedSessionToken`, puertos confiables y `ScopedSessionLifecycle` de issue/validate/rotate/logout. Exige `ContextualLoginPreparation.Ready` C05-04 para emisión, recibe `VerifiedTenantHostHint` de backend para operaciones y no acepta `tenant_id`, roles, claims ni DSN del navegador.
- `Crecisol.CredentialSecurity/OpaqueSessionTokenProtector.cs`: token opaco aleatorio de **256 bits CSPRNG**, formato base64url canónico, índice únicamente **HMAC-SHA256** bajo clave de 32–64 bytes del servidor; borra buffers y clave en `Dispose`. No secreta información en `ToString`; no guarda clave ni token en DB.
- `Crecisol.Infrastructure/Identity/MySqlDisposableSessionLedger.cs`: reutiliza `MySqlConnector@2.6.2` histórico sin cambiar el proyecto, DB `c05_05_disposable` exclusivamente localhost, cuatro rutinas SQL protegidas con `EXECUTE` y sin `SELECT` directo desde la cuenta de sesión. No hay routing productivo de tenants ni credenciales.
- `database/tests/fixtures/c05_05_disposable_sessions.sql`: dos tablas temporales InnoDB, FK compuesta tenant/actor, único índice digest; `c05_05_issue_once` exige revisiones actuales del vínculo/credencial/catálogo/host dentro de una transacción; `c05_05_is_current` exige tenant exacto y revoca al cambiar estado/revisión; `c05_05_rotate_once` reemplaza token digest y aumenta generación bajo lock `FOR UPDATE`; `c05_05_logout_once` marca revocación irreversible. Vencimiento de token a los 15 minutos **desde emisión o rotación, no prolongado por simples consultas**, máximo absoluto 8 horas o duración restante de membresía. Reloj UTC de la base.
- `Crecisol.SessionRevocation.MySqlTests`, su CSProj, `database/tests/c05_05_session_http_mysql.sh`, QA y CI: cliente HTTP real a servidor ASP.NET 10 en **127.0.0.1 únicamente**, cuatro rutas de `/__c05_05_test__/...` *solo dentro del ejecutable de prueba*, contraseñas de ensayo Argon2id C05-04, dos tenants A/B, MySQL 8.4 real desechable y 16 rotaciones concurrentes.

**Separación obligatoria:** `backend/src/Crecisol.Api/Program.cs` NO cambia y sigue exponiendo solo `/api/v1/public/branding/platform`, por lo que ningún login/validación/rotación/logout está disponible en la API productiva. La prueba HTTP en loopback no es prueba de TLS real ni de credenciales de clientes. Ninguna cookie/JWT, permiso de sucursal/recurso ni acceso SuperAdmin nace de la sesión de ensayo.

## 3. Auditorías técnicas A/B/C y evidencias

**PREMERGE del PR #135, HEAD final, 3/3 CI aplicables SUCCESS:**

| Evidencia | GitHub Actions | Dictamen |
| --- | --- | --- |
| Auditorías A/B/C, .NET10 HTTP/MySQL8.4 y regresiones | [#43505622170](https://github.com/crecisol-platform/CRECISOL/actions/runs/43505622170) | SUCCESS; 165 mutaciones adversarias **estáticas** rechazadas; 52 aserciones **reales HTTP/MySQL**; C05-01 100, C05-02 102, C05-03 68 y C05-04 95 aserciones de regresión |
| Foundation | [#43505622204](https://github.com/crecisol-platform/CRECISOL/actions/runs/43505622204) | SUCCESS |
| C03 integral .NET10/Angular22/Chromium/MySQL8.4 | [#43505622324](https://github.com/crecisol-platform/CRECISOL/actions/runs/43505622324) | SUCCESS |

**POSTMERGE técnico sobre SHA `c30bafa20add32b6042283e0c9f853d177d603d3` — iniciadas, NO VERIFICADAS AL REDACTAR:**

- [C05-05 #43506355739](https://github.com/crecisol-platform/CRECISOL/actions/runs/43506355739)
- [Foundation #43506355760](https://github.com/crecisol-platform/CRECISOL/actions/runs/43506355760)
- [C03-03 Application #43506355742](https://github.com/crecisol-platform/CRECISOL/actions/runs/43506355742)
- [C03-04 Infrastructure #43506355741](https://github.com/crecisol-platform/CRECISOL/actions/runs/43506355741)
- [C03-10 integral #43506355907](https://github.com/crecisol-platform/CRECISOL/actions/runs/43506355907)

Ningún workflow `skipped`, pendiente o fallido se considera `SUCCESS`. El historial de intentos CS1729 (constructor interno inaccesible entre assemblies) y CS8602 (nulabilidad de prueba tratada como error) permanece verificable y fue corregido mediante commits posteriores, sin desactivar `TreatWarningsAsErrors`.

## 4. Matriz de controles positivos, negativos y límites

| Control | Evidencia positiva o negativa real exigida |
| --- | --- |
| Emisión con identidad C05-04 | Password Argon2id real de fixture, Host A/B verificado, revalidación atómica de revisions account/credential/link/host/tenant en MySQL; contraseña errónea y usuario desconocido denegados |
| Aislamiento dos tenants | Actor con A+B no puede validar ni rotar A usando host B; logout cruzado no afecta sesión A; host/headers ajenos rechazados |
| Token no reversible | 256-bit CSPRNG y HMAC-SHA256 por clave de backend; parseo estricto; base de datos almacena solo digest BINARY(32) |
| Revocación real | HTTP logout 204 neutro, MySQL `revoked_utc` persistente; token revocado/expirado no valida ni rota, aun tras reactivar credencial |
| Rotación y carrera | Un nuevo token sustituye al anterior en una transacción, generación incrementada; 16 clientes reales simultáneos: **exactamente un ganador**, 15 rechazados; token previo no resucita |
| Privilegio mínimo | DB runner `EXECUTE` de cuatro procedimientos, SELECT directo falla; schema 2 tablas FK compuesta tenant/actor; sin plaintext/cookie/password/email |
| Compatibilidad y calidad | A/B/C + .NET10/Angular22/Chromium/MySQL8.4 + regresión C05-01..04 y C03; revisión exacta SHA/run/diff |

**NO-GO productivo (no cubierto por esta microfase aislada):** directorio real con pruebas de emisión atómica bajo credenciales persistentes, freshness/ticket C05-04 de un solo uso, gestión/rotación de claves HMAC protegidas, HTTPS y decisión cookie SameSite/CSRF o bearer, MFA/step-up, rate limit distribuido, cierre global entre dispositivos, detección y revocación familiar ante replay, trazas de seguridad sin PII/tokens, recuperación de contraseñas y control backend por recurso. Ausentes de **la API activa**, no afirmados como certificados. El prototipo SQL no es migración de producción. Ningún endpoint de login/sesión real, JWT, cookie ni grant se habilita aquí.

## 5. Ratificación documental independiente y puerta institucional

**A:** concordancia de Documento Maestro, C05-04 precedente y C05-05 alcance, log real de HTTP/MySQL 8.4 y negativos. **B:** host/tenant A-B, expiración, CAS transaccional y revocación, privilegios, criptografía, mutaciones adversarias, 0 tokens/secretos en repositorio. **C:** diff de acta limitado a tres archivos, arquitectura, tests reales, compilaciones y SHAs/runs exactos.

**Esta acta será EFICAZ SOLO SI:** (1) POSTMERGE técnico #135 5/5 SUCCESS; (2) PR documental separado con acta + QA + workflow propios y suites .NET10/MySQL8.4 reales A/B/C; (3) PREMERGE 2/2 SUCCESS en HEAD definitivo; (4) fusión exclusiva a `dev` por HEAD fijado; (5) POSTMERGE 2/2 SUCCESS en SHA de fusión; (6) comentario institucional permanente posterior. Sin esos seis requisitos, C05-05 **NO CERTIFICADA, C05-06 BLOQUEADA**.

**Solo después de cumplir las puertas:** **C05-05 CERTIFICADA EXCLUSIVAMENTE EN NÚCLEO DE SESIONES Y API HTTP DE ENSAYO LOCAL CON MySQL 8.4 Y DATOS SINTÉTICOS (5/11 C05)**, C05-06 habilitada **solo para preparar/desarrollar/ensayar su propia recuperación de contraseña**, capítulo 05 no certificado, C06 bloqueado. **NO AUTORIZA sesiones de usuarios reales ni despliegue.**

**Rollback:** revertir únicamente el squash del acta y el técnico `c30bafa20add32b6042283e0c9f853d177d603d3` en `dev`, con evaluación de dependencias. No tocar `main`, SOLQARYN, Word/PDF, VPS, DNS, Cloudflare, producción ni cobros.

# CRECISOL — C05-08 · MFA y passkeys por riesgo (alcance de desarrollo aislado)

**Fecha de inicio:** 14-11-2026. **Estado:** EN DESARROLLO, NO CERTIFICADA; C05-09 BLOQUEADA. **Repositorio único autorizado:** `crecisol-platform/CRECISOL` (ID 1450414592), base `dev` `e648d7512897e2c6aaf5c42a75969f63e315c6a6`, rama `feature/c05-08-mfa-stepup-passkeys-20261114`. Ninguna modificación a `main`, SOLQARYN, Word/PDF maestro, VPS, DNS, Cloudflare, cobros ni datos productivos.

## 1. Fuente y puerta de entrada

Autoridad: Documento Maestro Único CRECISOL, `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, su registro `docs/MASTER_REFERENCE.md`, y `docs/PLAN_37_CAPITULOS.md` (C05 Identidad, autenticación y recuperación segura). Plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`: **C05-08 | MFA y passkeys por riesgo | Inscripción/recuperación y step-up testeados | No bypass por restablecer factor.**

Precedente [C05-07 certificada exclusivamente en entorno aislado y sintético](https://github.com/crecisol-platform/CRECISOL/pull/140#issuecomment-6882141034), `dev` `e648d7512897e2c6aaf5c42a75969f63e315c6a6`. No atribuir a C05-07 MFA, autenticación privada, envío SMTP, cuentas o sesiones reales.

## 2. Diseño de seguridad acordado para este incremento

**Principio rector:** inscripción, comprobación y recuperación de factores NO conceden acceso privado, rol, membresía, sesión, JWT ni autorización de recurso. Una prueba de factor produce exclusivamente evidencia interna ligada a actor, tenant verificado por backend, operación de riesgo, desafío y revisiones vigentes. Su consumo atómico es de un uso. La contraseña de C05-04 sola NUNCA satisface step-up cuando hay MFA exigida; contraseña o enlace C05-06 tampoco pueden eliminar/desactivar MFA.

### TOTP

- TOTP conforme a RFC 6238 con paso de 30 s, 6 dígitos HMAC-SHA256 y tolerancia máxima un paso hacia atrás. El servidor verifica con reloj confiable y almacén de semilla **separado**, nunca una semilla TOTP en tabla de factor, log, respuesta pública ni commit. El siguiente uso exige contador temporal estrictamente superior al último aceptado; un mismo código no se consume dos veces, ni con otro challenge.
- Inscripción condicionada a reautenticación de contraseña C05-04 **fresca dentro del caso de uso**, host/tenant verificable, cuenta y membresía activas, correo previamente verificado, no realm PLATFORM salvo política específica de consola. Factores arrancan PENDING y solo pasan a ACTIVE tras una comprobación real del TOTP y commit CAS.
- Recuperación con códigos de respaldo de alta entropía, digest con clave distinta y **uso único**, más contraseña fresca; exclusivamente permite crear un factor de reemplazo **PENDING**. Nunca concede sesión, elimina el factor vigente, emite step-up ni desactiva MFA. La sustitución efectiva exige comprobar criptográficamente el factor nuevo, y revoca todas las sesiones anteriores del actor en todas las pertenencias. Sin código válido, el caso permanece bloqueado a verificación fuera de banda futura.

### Passkeys/WebAuthn

- Para el núcleo verificable de pruebas: credencial **ES256** creada en un dispositivo de ensayo, registro de clave pública **solo desde una fuente de inscripción confiable**, credential ID y RP ID del servidor; desafío CSPRNG de 256 bits de un uso. Verificar una aserción firma ECDSA sobre `authenticatorData || SHA256(clientDataJSON)`, `type='webauthn.get'`, challenge exacto, origin HTTPS exacto, RP ID hash SHA256, bits UP+UV, y contador `signCount` estrictamente creciente; rechazar `crossOrigin=true`, challenge/host cambiado, contador 0/downgrade y firma alterada.
- La verificación de **atestation de inscripción**, metadata del autenticador, COSE/CBOR de cualquier algoritmo, sincronización/backup eligibility y usuario real son **NO-GO** de producción. Las pruebas no se llamarán inscripción de passkey WebAuthn real; se probará una credencial pública sintética explícitamente confiada desde el fixture. La integración posterior requiere stack FIDO2/WebAuthn auditado completo.

### Riesgo y control de bypass

- Política por acción: `TenantSensitiveChange` requiere MFA y step-up vinculado al tenant, actor y operación. `PlatformPrivateAccess` permanece DENEGADO en este prototipo aunque se presente un TOTP/passkey de tenant: futuras capacidades de SuperAdmin requieren realm PLATFORM, MFA, motivo, período y auditoría separados.
- Toda solicitud que intente cambiar factor/recovery invalida step-up previo. Un factor PENDING/REVOKED/LOST no completa step-up. El código de respaldo recupera **solo inscripción pendiente** y nunca evita la prueba del factor de reemplazo. La recuperación C05-06 de contraseña invalida sesiones y no modifica el estado MFA. Contadores, desafío, replay y vencimiento se verifican atómicamente en backend.
- Aislamiento: actor/tenant/host/link/revisiones de credencial/sujeto validados en servidor; nunca usar tenantId del navegador para elegir base, actor o factor. Factor de A no autoriza B, ni cuenta PLATFORM puede entrar a datos TENANT. Auditoría sin PII ni secreto.

## 3. Arquitectura y artefactos previstos

`backend/src/Crecisol.Application/Identity/`: puertos de autenticador, política de riesgo, reautenticación, gestor de desafíos y recuperación.
`backend/src/Crecisol.CredentialSecurity/`: TOTP HMAC-SHA256, WebAuthn assertion ES256 estricta, token/backup-code HMAC separado.
`backend/src/Crecisol.Infrastructure/Identity/`: adaptador solo a MySQL 8.4 desechable; ejecuta procedimientos con cuenta `EXECUTE`.
`database/tests/fixtures/`: tablas de factor, desafío y recuperación/counters con FKs tenant/actor, transacciones `FOR UPDATE`, revocación de sesión y log append-only.
`backend/tests/` y `database/tests/`: pruebas de integración .NET 10/HTTP loopback/MySQL 8.4 de ambos factores, recuperación y ataques. `qa/`: auditorías A/B/C con detección de mutaciones hostiles y SHA.
`.github/workflows/`: solo lectura, sin despliegue y sin secretos productivos.

**NO** se conectará `Crecisol.Api/Program.cs` a login, MFA o sesión de usuario; seguirá con branding público exclusivamente. No frontend productivo ni correo real.

## 4. Matriz de pruebas obligatorias antes de certificar

| Función positiva | Ataques negativos requeridos |
| --- | --- |
| TOTP de 6 dígitos con 30 s y semilla aleatoria, resultado contrastado con vector RFC 6238 SHA256 | Wrong code, código con espacios/no canónico, replay mismo tick, ventana futura fuera de política, desvío de reloj |
| Inscripción PENDING → ACTIVE solo tras prueba real y contraseña C05-04 actual | Sin contraseña, replay credencial, actor/host/tenant distinto, cuenta/membresía suspendida, seed expuesta |
| Aserción WebAuthn ES256 de credencial sintética de test | Firma falsificada, origin incorrecto, RP ID hash falso, falta UP/UV, `crossOrigin=true`, counter 0/reducido, challenge reemitido o expirado |
| Step-up por riesgo ligado a operación y revisiones | MFA pendiente/revocado, token de A en B, usuario PLATFORM sin canal excepcional, reutilización de challenge, contraseña sin MFA, recuperación C05-06 como bypass |
| Recuperación con código de respaldo y Argon2id para reemplazo pendiente | Código repetido/caducado/falso, intento de deshabilitar factor antes de verificar reemplazo, sesiones previas aún utilizables |
| Regresión C05-01..07 y C03 | No contar tests no ejecutados o workflows skipped como PASS; preservar historial de fallos |

## 5. Reglas de cierre y riesgos

A: fuente oficial/precedente/riesgo; B: MFA, passkeys, criptografía, aislamiento A/B, replay, fraude y bypass; C: arquitectura, SHA/diff, performance, documentación, QA, dependencias y regresiones. Si un gate falla, corregir y volver a ejecutar íntegro. Certificación de C05-08, si procede, **solo para comportamiento de test** y con 0 riesgos P0/P1 abiertos en el alcance.

**Riesgos productivos excluidos/bloqueantes:** identidad y sesión reales, fuentes de confianza de credenciales públicas, attestation/cose/WebAuthn completo, KMS/secrets y rotación, resiliencia/reconciliación de semilla, flujo de recuperación de cuenta sin el factor, dispositivos perdidos, antifraude/ratelimit, timeouts/límites de intentos, auditoría WORM/SIEM, consentimiento, accesibilidad, TLS/WebAuthn navegador real y MFA para acceso global de plataforma. NO-GO para producción.

**Estado al inicio:** C05-08 NO CERTIFICADA, C05-09 BLOQUEADA, C05 7/11 microfases certificadas en alcance aislado. **Rollback:** revertir solo commits de la rama/PR de C05-08, sin tocar históricos C05-01..07, `dev` fuera de PR, `main`, SOLQARYN o producción.

## 6. Fuentes técnicas para diseño (no Skill externa)

- [W3C WebAuthn Level 3](https://www.w3.org/TR/2026/REC-webauthn-3-20260825/): comprobación de challenge, origin, RP ID, flags, clientDataJSON y firma.
- [RFC 6238](https://www.rfc-editor.org/rfc/rfc6238.html): tiempo de 30 s, algoritmo TOTP HMAC y límites de desviación.


## 7. Incremento transaccional real de ENSAYO (14-11-2026; certificación pendiente)

Este incremento complementa, **sin sustituir**, el subgate criptográfico previo:
- Application: DisposableMfaRiskUseCases exige en cada inscripción, recuperación y emisión de step-up una **nueva** reautenticación C05-04 Argon2id dentro del caso de uso; el host verificado y el tenant deben coincidir. La prueba de factor se hace con TOTP SHA256 o aserción ES256 de dispositivo sintético confiado. Un booleano, contraseña, código de respaldo o enlace C05-06 no pueden llamar a la ruta de commit del caso de uso como autoridad independiente.
- CredentialSecurity: desafío opaco ma8_ de 256 bits y HMAC-SHA256 independiente del de código de respaldo cm8_, sesión C05-05, correo C05-07 y reset C05-06; formatos canónicos y borrado de buffers.
- Infrastructure: MySqlDisposableMfaLedger, solo 127.0.0.1, BD c05_05_disposable y cuenta EXECUTE sin DML/SELECT; **no registra servicios en Crecisol.Api**.
- MySQL8.4: database/tests/fixtures/c05_08_disposable_mfa.sql define cuatro tablas InnoDB (factores, desafíos, respaldos HMAC, bitácora) con FK (tenant_id,actor_id), verificaciones de enlace y revisiones de actor/credencial/membresía/catálogo/host/buzón, FOR UPDATE, contadores monotónicos, vencimientos en UTC, único consumo, reemplazo PENDING y revocación durable global de sesiones de actor tras prueba del nuevo factor. La bitácora tiene veto UPDATE/DELETE incluso en cuenta privilegiada de fixture.
- QA: Crecisol.MfaRisk.MySqlTests expone rutas **exclusivamente dentro del binario de pruebas** con HTTP ligado a loopback, host verificado del lado servidor y cuerpo JSON cerrado. Prueba TOTP activo, WebAuthn ES256 con UP+UV y RP exactos, reautenticación incorrecta, host/cabeceras falsificadas, cross-tenant, challenge/contador reutilizado, recuperación con respaldo y contraseña, pending sin derecho a step-up, reemplazo y revocación, 16 peticiones HTTP simultáneas y negativas de expiración, buzón off/on, membresía suspendida/restaurada y revisión de contraseña. database/tests/c05_08_mfa_http_mysql.sh inicia MySQL8.4 por digest pinned, desechable, con credenciales aleatorias no registradas.
- Auditorías A/B/C independientes: qa/c05_08_mfa_integration_audits.py y .github/workflows/c05-08-mfa-integration.yml; compilación estricta .NET10, ensayos HTTP/MySQL8.4 reales y regresiones C05-01..07 con identidad SHA/run. Las auditorías estáticas **no** suplen pruebas de ejecución.

**Límite del ámbito certificable:** NO se registran passkeys reales en un navegador, ni se verifican attestation/COSE/metadata FIDO2; la clave pública del dispositivo ES256 se precarga de forma sintética en memoria de prueba. La semilla TOTP existe en custodio volátil del ejecutable de prueba, **no** hay KMS ni cifrado duradero de seed. No existe autenticación de sesión productiva, transporte HTTPS público, outbox segura, enrolamiento real de usuarios, factor recovery sin respaldo, rate limiting distribuido, almacenamiento confiable de clave pública ni integración SuperAdmin. Las respuestas de ensayo no son privilegios, tokens de inicio de sesión, permisos, sesión, ni prueba de un usuario real. Todo lo anterior es **NO-GO de producción**. Los factores y el estado MFA quedan aislados en la base de pruebas sin migraciones productivas.

**Estado formal hasta verificar CI PREMERGE A/B/C y las regresiones del último SHA, revisión del diff, evidencias POSTMERGE y acta independiente:** **C05-08 NO CERTIFICADA; C05-09 BLOQUEADA; capítulo C05 7/11 en su alcance sintético**. No fusionar ni invocar certificación por una CI previa de subgate criptográfico. Preservar fallos preliminares (CS8321 corregido mediante nuevo commit) y trazabilidad por SHA. Rollback: cerrar sin fusionar el PR #141 o revertir solamente sus commits tras comprobación de dependencias; no cambiar certificaciones C05-01..07, main, Word/PDF, SOLQARYN ni producción.

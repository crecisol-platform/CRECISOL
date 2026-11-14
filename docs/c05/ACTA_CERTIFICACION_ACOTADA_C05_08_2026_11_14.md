# CRECISOL — C05-08 · Acta institucional CONDICIONADA de MFA y passkeys por riesgo

**Fecha de redacción:** 2026-11-14. **Repositorio exclusivo:** `crecisol-platform/CRECISOL`, ID 1450414592. **Rama documental:** `feature/c05-08-mfa-institutional-20261114`, desde `dev` `49e99411efa0b6639516eb703b1b057c87a7492e`. **ESTADO AL REDACTAR: ACTA NO EFICAZ; C05-08 NO CERTIFICADA; C05-09 BLOQUEADA; capítulo C05 7/11 en alcance sintético.** Los estados iniciales se conservan como evidencia histórica; el dictamen posterior solo se hace eficaz después de todas las puertas y comentario institucional verificable.

## 1. Fuente canónica, precedente y criterio de aceptación

Fuente superior: **Documento Maestro Único** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md` y plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`. Huella SHA256 registrada Word `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Fuente original del capítulo 05 verificada en CI `43364292640`. Requisito exacto: **«C05-08 | MFA y passkeys por riesgo | Inscripción/recuperación y step-up testeados | No bypass por restablecer factor»**.

Precedente: **C05-07** certificada exclusivamente para correo verificado y cambio sensible de pruebas, en el [dictamen institucional del PR #140](https://github.com/crecisol-platform/CRECISOL/pull/140#issuecomment-6882141034), `dev` `e648d7512897e2c6aaf5c42a75969f63e315c6a6`. No autoriza correos SMTP ni identidades productivas. Esta microfase no abre C05-09 sin el presente cierre.

## 2. Implementación técnica exacta y frontera de seguridad

[PR técnico #141](https://github.com/crecisol-platform/CRECISOL/pull/141), HEAD final `ca8faf2c85e07435e085e25e7474efd0d50387f4`, diff **EXACTAMENTE 17 archivos**, squash **solo en dev** `49e99411efa0b6639516eb703b1b057c87a7492e`; `main` intacto.

- `backend/src/Crecisol.CredentialSecurity/`: TOTP RFC 6238 HMAC-SHA256, paso 30 segundos, semilla aleatoria 256 bits, verificación constante con ventana hacia atrás, contador confirmado monotónico; desafío de 256 bits `ma8_` y HMAC-SHA256 con clave separada; código respaldo `cm8_` con HMAC separado, de un solo uso. Aserción ES256 de **credencial WebAuthn de fixture previamente confiada**, con firma verificable, RP ID, HTTPS origin, challenge, UP+UV, `crossOrigin` y `signCount`; se rechazan firmas/credenciales/contadores incorrectos. No hay FIDO2 registration ni attestation real.
- `backend/src/Crecisol.Application/Identity/DisposableMfaRiskUseCases.cs`: inscripción, reemplazo y expedición de step-up requieren **Argon2id C05-04 fresco dentro del propio caso de uso** y host resuelto en backend. Ninguna contraseña, restablecimiento C05-06, código respaldo ni reset de factor autoriza por sí solo una acción privada. Toda comprobación de factor precede al commit de evidencia contra SQL; no se emiten roles/sesiones/JWT.
- `backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableMfaLedger.cs`: MySqlConnector@2.6.2, conexión restringida a `127.0.0.1` y esquema desechable `c05_05_disposable`; usuario test tiene **solo EXECUTE** para procedimientos, sin SELECT/UPDATE/DELETE de factores, códigos, mailbox o auditoría. No se inyecta ni se registra en la API productiva.
- `database/tests/fixtures/c05_08_disposable_mfa.sql`: cuatro tablas InnoDB de factores, desafíos, backups HMAC y auditoría solo de IDs/acciones/fecha; FK compuesta por tenant/actor; cinco procedimientos `SQL SECURITY DEFINER`, `START TRANSACTION`, `FOR UPDATE` y CAS de un solo ganador; revalidación de cuenta, credencial, pertenencia, tenant, host, buzón C05-07 y revisiones actuales. La recuperación consume un código respaldo solo con contraseña fresca y deja reemplazo **PENDING** sin jubilar el factor anterior. Solo la prueba real del nuevo factor cambia a **ACTIVE**, revoca factor antiguo, desafíos y **todas las sesiones anteriores del mismo actor** en A/B sin revocar las de otro actor. Dos triggers impiden borrar o modificar auditoría.
- `backend/tests/Crecisol.MfaRisk.ContractsTests`, `backend/tests/Crecisol.MfaRisk.MySqlTests`, `database/tests/c05_08_mfa_http_mysql.sh`: .NET 10 y HTTP **solamente 127.0.0.1** con MySQL 8.4 contenedorizado por digest fijado; certificados/datos sintéticos, token y semilla fuera de bases; rutas `/__c05_08_test__/*` exclusivas del binario de pruebas. `backend/src/Crecisol.Api/Program.cs` mantiene **SOLO** `/api/v1/public/branding/platform`. No hay rutas productivas MFA.
- `qa/c05_08_mfa_crypto_audits.py`, `qa/c05_08_mfa_integration_audits.py`, dos workflows de subgate y esta especificación: A/B/C independientes por SHA y run real, validación de diff y prueba de mutaciones. Sin secretos, permisos GitHub de escritura ni despliegue.

## 3. PREMERGE técnico A/B/C — evidencia verificada y fallos anteriores

Las cuatro ejecuciones siguientes terminaron `completed/success` sobre el HEAD exacto del PR técnico `ca8faf2c85e07435e085e25e7474efd0d50387f4`:

| Puerta | GitHub Actions | Evidencia real de su ejecución |
| --- | --- | --- |
| Criptografía A/B/C | [43637795358](https://github.com/crecisol-platform/CRECISOL/actions/runs/43637795358) | 53 aserciones .NET10 genuinas RFC6238/WebAuthn ES256 de fixture, **112 mutaciones estáticas rechazadas**, regressiones C05-01..07; C exacta de SHA/run. |
| HTTP/.NET10/MySQL8.4 A/B/C | [43637795464](https://github.com/crecisol-platform/CRECISOL/actions/runs/43637795464) | **149 comprobaciones HTTP/MySQL** reales, 94 mutaciones estáticas rechazadas; alta PENDING/ACTIVE, challenge, step-up, replay, 16 solicitudes concurrentes (exactamente 1/16 aceptada), backup gastado/vencido, sustitución con factor nuevo probado, revocación global de sesiones del actor, host/tenant A/B, realm PLATFORM denegado, contraseña incorrecta, caducidad, mailbox off/on, pertenencia suspendida/reactivada, auditoría append-only; regresiones C05-01..07. |
| Foundation | [43637795474](https://github.com/crecisol-platform/CRECISOL/actions/runs/43637795474) | completed/success, repositorio/fuente/tipado. |
| C03 integral | [43637795403](https://github.com/crecisol-platform/CRECISOL/actions/runs/43637795403) | completed/success, .NET10, Angular22, Chromium, MySQL8.4 y regresión histórica. |

**Fallos históricos preservados:** una CI anterior de desarrollo detectó `CS8321` (función local no utilizada) y fue corregido en commit `a4e0ded1fd02b846e59358c0f2af5b51628cf7f0` sin desactivar `warnings-as-errors`. Los runs anteriores fallidos **NO** se cuentan como aprobados; la evidencia válida pertenece al HEAD final y sus 4/4 verdes. El checkout sintético de PR en los logs puede tener SHA de merge temporal diferente del HEAD: se verificó igualdad de padres base/head y diff exacto.

## 4. POSTMERGE técnico sobre `dev`

Las seis ejecuciones de verificación técnica fueron iniciadas por squash `49e99411efa0b6639516eb703b1b057c87a7492e`:

- Criptografía C05-08 [43638254515](https://github.com/crecisol-platform/CRECISOL/actions/runs/43638254515).
- Integración MFA C05-08 [43638254480](https://github.com/crecisol-platform/CRECISOL/actions/runs/43638254480).
- Foundation [43638254422](https://github.com/crecisol-platform/CRECISOL/actions/runs/43638254422).
- C03-03 Application [43638254469](https://github.com/crecisol-platform/CRECISOL/actions/runs/43638254469).
- C03-04 Infrastructure [43638254452](https://github.com/crecisol-platform/CRECISOL/actions/runs/43638254452).
- C03-10 integral [43638254419](https://github.com/crecisol-platform/CRECISOL/actions/runs/43638254419).

**NO VERIFICADAS TODAS AL REDACTAR.** Esta lista NO presume que un workflow en progreso, en cola, fallido o skipped equivale a success. Son condición necesaria del dictamen posterior y se verifican contra el SHA de dev arriba.

## 5. Matriz de riesgo, privacidad y negativas

| Punto | Positivo verificado por HTTP y/o criptografía | Negativa que debe permanecer DENEGADA |
| --- | --- | --- |
| Inscripción TOTP | Reautenticación Argon2id fresca, buzón previamente verificado, tenant/host válido; activación posterior a comprobación TOTP y CAS | Sin contraseña, factor PENDING/REVOKED, credencial ajena, replay/código caducado, cuenta o pertenencia suspendida |
| Aserción ES256 de test | Challenge 256 bits, origen HTTPS y RP exactos, UP+UV, firma P-256 real, incremento de signCount | Otro RP, crossOrigin, firma alterada, ID de credencial distinto, contador viejo/cero, usuario PLATFORM |
| Step-up | Desafío asociado a acción sensible/tenant/actor/revisiones; 16 peticiones => exactamente un ganador y 15 denegaciones | Password-only, identidad distinta, falsa cabecera tenant/host, vencimiento, replay tras cambios de buzón/host/credencial/membresía |
| Recuperación sin bypass | Backup de 256 bits y contraseña actual permiten solo reemplazo PENDING; nuevo factor se prueba antes de reemplazar y revocar sesiones anteriores del actor | Backup repetido, caducado, reset de contraseña C05-06 usado como MFA, acceso privado por backup, sustitución sin prueba, revocación de actor ajeno |
| Evidencia/arquitectura | Bitácora append-only sin PII, EXECUTE-only, test HTTP en loopback, API pública sin auth/MFA nuevos | UPDATE/DELETE de auditoría, SELECT directo de backups, credenciales/tokens/semillas en repo, rutas productivas, despliegue |

El ensayo **no** comprueba enrolamiento WebAuthn de navegador, attestation/certificados FIDO2, COSE/CBOR, pasarelas IdP, passkeys sincronizadas, custodio KMS para semillas TOTP, anti-fraude distribuido, recuperación real sin backup, administración SuperAdmin, sesiones de clientes reales ni autenticación productiva. Son **NO-GO para producción** y no se transforman en funcionalidad por una certificación sintética.

## 6. Puertas de eficacia institucional y reversibilidad

Auditoría **A**: documento Word canónico, plan y previa C05-07, alcance aislado y evidencia exacta; **B**: recuperación sin bypass, replay/riesgo, tenant A/B, host/realm, PENDING, 16-way CAS, privacidad, actores terceros; **C**: arquitectura, diff/SHAs, compilación .NET10 real, HTTP/MySQL8.4, seguridad de CI, regresiones C05-01..07 y sin P0/P1 vigentes **dentro del alcance exclusivamente sintético**.

**ACTA SERÁ EFICAZ SOLO SI**:
1. POSTMERGE técnico **6/6 completed/success**, sin skipped ni fallos en SHA `49e99411efa0b6639516eb703b1b057c87a7492e`.
2. PR documental **SEPARADO**, diff **EXACTAMENTE tres archivos**: la presente acta, `qa/c05_08_closure_gate.py` y `.github/workflows/c05-08-certification.yml`; A/B/C institucionales y pruebas .NET10/HTTP/MySQL8.4 y regresiones C05-01..07 en el mismo SHA/run.
3. PREMERGE documental **2/2 completed/success** sobre HEAD documental inmutable, con base dev exacta y sin modificaciones adicionales.
4. Fusionar **solo a dev** con verificación del SHA HEAD; nunca `main`.
5. POSTMERGE documental **2/2 completed/success**, con A/B/C y prueba real en el SHA final de dev, sin skipped.
6. Publicar dictamen definitivo permanente en el PR documental con IDs, SHAs, resultados, límites, rollback y aceptación de la microfase delegada.

**HASTA ENTONCES:** C05-08 **NO CERTIFICADA**, C05-09 **BLOQUEADA**, C05 **7/11**. Cuando todas se cumplan, **C05-08 CERTIFICADA EXCLUSIVAMENTE EN NÚCLEO MFA/STEP-UP Y RECUPERACIÓN PROBADOS CON TOTP Y ES256 DE FIXTURE, HTTP .NET10 Y MYSQL8.4 DESECHABLE; C05 8/11**; C05-09 habilitada solo para desarrollo de su alcance; **C05 COMO CAPÍTULO NO CERTIFICADO; C06 BLOQUEADO**.

**Rollback controlado:** revertir el squash institucional final y, tras analizar dependencias, el squash técnico `49e99411efa0b6639516eb703b1b057c87a7492e` exclusivamente en dev; preservar histórico C05-01..07 y los fallos anteriores. Sin tocar `main`, Word/PDF canónicos, `solqaryn/Solqaryn`, Cloudflare, DNS, VPS, datos productivos, correo real o cobros.

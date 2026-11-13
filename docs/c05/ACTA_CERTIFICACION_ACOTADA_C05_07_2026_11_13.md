# CRECISOL — C05-07 · Acta institucional CONDICIONADA, correo verificado y cambios sensibles

**Fecha:** 2026-11-13. **Repositorio autorizado exclusivamente:** `crecisol-platform/CRECISOL`, ID GitHub `1450414592`. **Rama:** `feature/c05-07-mailbox-certification-20261113`, creada desde `dev` `426c3e44cb0f1bd25ef09341dad68fbe0123e985`. **ESTADO AL REDACTAR: ACTA NO EFICAZ; C05-07 NO CERTIFICADA; C05-08 BLOQUEADA.** Ningún cambio a `main`, SOLQARYN, producción, Word/PDF canónicos, VPS, DNS, Cloudflare o cobros.

## 1. Fuente canónica, precedentes y alcance

La autoridad es el Documento Maestro Único CRECISOL, `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, registrado en `docs/MASTER_REFERENCE.md`, SHA256 Word `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF de lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; capítulo 05 extraído y verificado en [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). Plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`: **C05-07 Verificación de correo y cambios sensibles**; positivo **«Reautenticación y confirmaciones auditable»**; negativo **«No cambio de correo sin comprobación»**. Precedente [C05-06 ratificada en ámbito aislado](https://github.com/crecisol-platform/CRECISOL/pull/138#issuecomment-6865602159), SHA `4a97dbd29b3acc93e9c20ae09e0e26945af98ea0`. Ninguna certificación precedente autoriza SMTP, cuentas reales ni sesiones productivas.

## 2. Implementación y control arquitectónico del PR técnico #139

[PR técnico #139](https://github.com/crecisol-platform/CRECISOL/pull/139), HEAD final `09bf9acd5181635a22d776ef4d675f29378d6bd6`, squash EXCLUSIVAMENTE `dev` `426c3e44cb0f1bd25ef09341dad68fbe0123e985`, diff exactamente diez archivos:

- `backend/src/Crecisol.Application/Identity/SensitiveMailboxVerification.cs`: contratos internos `MailboxApprovalDigest`, `IssuedMailboxApproval`, `ITrustedSensitiveMailboxLedger`, `ITrustedMailboxApprovalDelivery`, `ITrustedImmediatePasswordReauthentication` y caso de uso `VerifyAndChangeSensitiveMailbox`. La solicitud neutra de verificación solo prueba buzones YA registrados. El cambio exige **Argon2id C05-04 FRESCO dentro del propio caso de uso**, mediante `freshPassword.VerifyFreshAsync`, sin aceptar evidencia cacheada por un controlador ni un veredicto del navegador; debe coincidir tenant y revisión de host backend. No crea sesión, credencial, rol, autorización ni factor MFA.
- `backend/src/Crecisol.CredentialSecurity/MailboxApprovalTokenProtector.cs`: token `cv7_` opaco CSPRNG de **256 bits**, índice **HMAC-SHA256** con clave del servidor distinta de las de sesión C05-05 y reset C05-06. Canonicalidad base64url, buffers y clave borrados en `Dispose`, `ToString` redactado; mailbox registrado solo por digest HMAC C05-06.
- `backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableSensitiveMailboxLedger.cs`: MySqlConnector@2.6.2, DSN fijo **solo** `127.0.0.1` y `c05_05_disposable`, usuario con `EXECUTE` de procedimientos y sin SELECT a buzones/bitácora; no tokens ni base de datos desde el cliente.
- `database/tests/fixtures/c05_07_disposable_mailbox.sql`: tres tablas InnoDB efímeras de verificaciones, cambios y auditoría append-only sin mail, token, PHC ni HMAC del correo; dos triggers impiden UPDATE/DELETE en bitácora, cuatro procedimientos `SQL SECURITY DEFINER`. Verificación 10 minutos UTC, nueva emisión revoca anterior, compara tenant, actor, vínculo, buzón, revisiones y estado vigente. Cambio 5 minutos: primero reautenticación C05-04, luego **confirmación independiente desde buzón ANTERIOR y desde buzón NUEVO**; ambas deben coexistir válidas antes de aplicar. `c05_07_confirm_change` primero busca token por índices separados sin bloqueo y después bloquea **una única fila por PK** con `FOR UPDATE`, revalida token, estado, tenant, cuenta/credencial/membresía/host/catálogo y revisión del mailbox. En la MISMA transacción actualiza solo mailbox de tenant A, revoca las sesiones anteriores del actor global de A/B, incrementa revisiones, anula desafíos anteriores y audita evento, sin afectar al actor ajeno. 16 peticiones HTTP simultáneas: un único ganador. Un token antiguo no revive.
- `backend/tests/Crecisol.MailboxApproval.MySqlTests/Program.cs`, su CSProj y `database/tests/c05_07_mailbox_http_mysql.sh`: .NET 10 ASP.NET HTTP **127.0.0.1 únicamente**, buzones simulados por sink **en memoria**, MySQL 8.4 en contenedor desechable. Rutas `/__c05_07_test__/verify/request`, `verify/confirm`, `change/request`, `change/confirm` existen **SOLO en el ejecutable de tests**, NO en el producto. Verifican neutralidad HTTP 202 de conocidos/desconocidos, Argon2id, reemisión, caducidad, dos aprobaciones, auditoría append-only, cross-tenant, spoof de host, correo ocupado, suspensión/reactivación, 16-way CAS, sesiones revocadas y tercero intacto.
- `docs/c05/C05_07_ESPECIFICACION.md`, `qa/c05_07_mailbox_audits.py` y `.github/workflows/c05-07-mailbox.yml`: especificación y matriz de seguridad, pruebas .NET10/MySQL8.4 reales y auditorías A/B/C con pruebas de procedencia SHA/run. La API `backend/src/Crecisol.Api/Program.cs` conserva SOLO `/api/v1/public/branding/platform`.

**Separación estricta:** este incremento es un PROTOTIPO de núcleo aislado. No existe integración de correo real, delivery duradero/transaccional, identidad productiva, MFA o panel de usuario en servicio; los tokens de prueba NO son credenciales productivas.

## 3. Auditorías PREMERGE técnicas A/B/C sobre HEAD final — 3/3 SUCCESS

| Puerta | GitHub Actions | Evidencia verificable |
| --- | --- | --- |
| C05-07 A/B/C, .NET10, ASP.NET HTTP real, MySQL8.4 | [#43516381771](https://github.com/crecisol-platform/CRECISOL/actions/runs/43516381771) | completed/success. Auditoría A fuente/alcance, auditoría B **229 mutaciones de guardias ESTÁTICAS rechazadas**, **79 aserciones HTTP/MySQL reales**, auditoría C exacta SHA checkout sintético `caadb2bbb76590369a0335fb183c6f1266aae5c4`, run `43516381771`. Regresiones C05-01 **100**, C05-02 **102**, C05-03 **68**, C05-04 **95**, C05-05 **52 HTTP/MySQL**, C05-06 **91 HTTP/MySQL** en el mismo run. |
| Foundation | [#43516381498](https://github.com/crecisol-platform/CRECISOL/actions/runs/43516381498) | completed/success |
| C03 integral (NET10, Angular22, Chromium, MySQL8.4) | [#43516381502](https://github.com/crecisol-platform/CRECISOL/actions/runs/43516381502) | completed/success |

**Fallos reales históricos conservados y corregidos SIN relajar garantías:** PR #139 primer estado anterior [#43515012567](https://github.com/crecisol-platform/CRECISOL/actions/runs/43515012567) aprobó 77 aserciones de diseño anterior, pero no certifica refuerzo posterior. El intento [#43515462193](https://github.com/crecisol-platform/CRECISOL/actions/runs/43515462193) detectó `CS0165` por inicialización prematura del fixture de reautenticación; corregido con orden seguro sin desactivar warnings-as-errors. La ejecución [#43515961005](https://github.com/crecisol-platform/CRECISOL/actions/runs/43515961005) detectó falla de 16 confirmaciones concurrentes por bloqueo de dos índices `OR... FOR UPDATE`; se eliminó y reemplazó por lectura indexada seguida de bloqueo UNA fila PK y revalidación actual. Solo el **HEAD final** de #43516381771 con 79 aserciones y regresiones es evidencia técnica aprobada. Ninguna ejecución `skipped` o fallida se cuenta como verde.

## 4. POSTMERGE técnico en SHA `426c3e44cb0f1bd25ef09341dad68fbe0123e985`

**INICIADAS, NO VERIFICADAS AL REDACTAR; NO PRESUMIR ÉXITO.** Verificar todas en el SHA exacto antes de declarar cierre:
- [C05-07 #43517108400](https://github.com/crecisol-platform/CRECISOL/actions/runs/43517108400)
- [Foundation #43517108681](https://github.com/crecisol-platform/CRECISOL/actions/runs/43517108681)
- [C03-03 Application #43517108660](https://github.com/crecisol-platform/CRECISOL/actions/runs/43517108660)
- [C03-04 Infrastructure #43517108416](https://github.com/crecisol-platform/CRECISOL/actions/runs/43517108416)
- [C03-10 integral #43517108534](https://github.com/crecisol-platform/CRECISOL/actions/runs/43517108534)

## 5. Matriz de resultados positivos, negativos, privacidad y riesgos

| Control | Positivo y negativo requerido |
| --- | --- |
| Solicitud de verificación de un buzón pre-registrado | 202 vacío indistinguible de desconocido/ya verificado/PLATFORM; entrega al sink solo si buzón previo no verificado y activo; nueva emisión revoca token anterior, 10 minutos UTC; token expirado y A/B cruzado denegados |
| Cambio sensible de correo | Reautenticación Argon2id **fresca en el caso de uso** con host real backend; contraseña incorrecta, cuenta inexistente, spoof de header, tenant cruzado, correo ya ocupado, igual al antiguo => no inicia cambio |
| Dos pruebas independientes | Destino antiguo y nuevo deben confirmar dentro de 5 minutos; nuevo solo no cambia; dos replays no reviven token ni MFA, 16 HTTP simultáneos => exactamente un ganador y quince rechazos |
| Revalidación DB transaccional | Bloqueo `FOR UPDATE` por PK, validación actual de actor/link, tenant, account/credential/membership/host/mail revisions; off/on de mailbox y membresía invalida enlaces; commit atómico |
| Sesiones y auditoría | A/B del mismo actor revocadas sin modificar buzón B; tercero no afectado; tabla audit solo ids/tipo/timestamp sin PII/token; triggers anti-UPDATE/DELETE |
| Regresiones | C05-01..06 reales, C03/Angular/Chromium/MySQL8.4, A/B/C y source SHA; CI read-only, cero producción |

**RIESGOS P0/P1 BLOQUEANTES PARA PRODUCCIÓN, EXCLUIDOS del prototipo:** directorio global de identidad/alias con unicidad transaccional real multi-tenant; verificación de propiedad de email real; SMTP y outbox cifrado, transaccional y duradero; MFA/step-up vigente; soporte a cuentas privilegiadas y recuperación en caso de pérdida de correo; control antifraude/rate limit distribuido, anti-enumeración de latencias, HTTPS/CSRF/CORS según transporte, secret manager KMS y rotación, bitácora WORM/SIEM, trazas y notificaciones reales, clientes actuales y pruebas manuales de accesibilidad; no datos customer/productivos. Ningún cambio de correo de usuarios reales está permitido. El control de doble aprobación no autoriza acceso privado, ni sesión, ni restablece MFA.

## 6. Puertas de efectividad y rollback

Auditoría institucional **A**: trazabilidad de Documento Maestro, previo C05-06, pruebas de buzon/Argon2id/doble aprobación; **B**: host/tenant A-B, replay, expiration, revisiones, 16-way CAS, revocación, audit sin PII; **C**: arquitectura, permisos, diez archivos técnicos/tres documentos, SHA/run, .NET10 y MySQL8.4 reales y regresiones sin fallos vigentes.

**ACTA SERÁ EFICAZ SOLO SI**:
1. Los 5/5 POSTMERGE técnicos #139 son `completed/success` en SHA `426c3e44cb0f1bd25ef09341dad68fbe0123e985`.
2. Un PR documental SEPARADO contiene EXACTAMENTE tres archivos: esta acta, `qa/c05_07_closure_gate.py` y `.github/workflows/c05-07-certification.yml`; su auditoría A/B/C y tests **reales HTTP/MySQL8.4 y regresiones** ejecutan en mismo SHA/run.
3. PREMERGE documental **2/2 completed/success** sobre HEAD definitivo y base `dev` exacta.
4. Fusionar solo en `dev` por HEAD fijado, jamás en `main`.
5. POSTMERGE documental **2/2 completed/success** en SHA final fusionado, con C que comprueba procedencia.
6. Publicar dictamen institucional permanente con run IDs, SHAs, alcance acotado, fallos históricos corregidos y rollback.

**Hasta cumplir las seis puertas: C05-07 NO CERTIFICADA; C05-08 BLOQUEADA.** Solo entonces: **C05-07 CERTIFICADA EXCLUSIVAMENTE EN NÚCLEO DE VERIFICACIÓN Y CAMBIOS SENSIBLES PROBADOS CON HTTP/.NET10 Y MYSQL8.4 EN ENTORNO AISLADO Y DATOS SINTÉTICOS (7/11 C05)**; C05-08 habilitada SOLO para preparación, desarrollo y pruebas; **C05 completo NO CERTIFICADO y C06 BLOQUEADO**. No autorización para correo/cambios de cuentas reales.

**Rollback controlado:** revertir exclusivamente el squash documental final y, tras evaluación de dependencias, el squash técnico `426c3e44cb0f1bd25ef09341dad68fbe0123e985` en `dev`, preservando historial de fallos, evidencias y certificaciones C05-01..06. No tocar `main`, SOLQARYN, Word/PDF, Cloudflare, DNS, VPS, producción ni cobros.

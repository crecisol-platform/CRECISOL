# CRECISOL — C05-09 · Acta institucional CONDICIONADA: perfiles y fotos seguras

**Redacción:** 2026-11-14. **Repositorio:** `crecisol-platform/CRECISOL`, ID GitHub `1450414592`. **Rama documental:** `feature/c05-09-private-profiles-institutional-20261114`, desde `dev` `558c54cc9b1aee20a2fac6eb0c637b4f5700a52e`. **AL REDACTAR: ACTA NO EFICAZ; C05-09 NO CERTIFICADA; C05-10 BLOQUEADA; C05 8/11**. Las condiciones y leyendas iniciales se preservan históricamente: un dictamen posterior con pruebas completas determinará su eficacia.

## 1. Documento Maestro, procedencia y alcance

Autoridad: **Documento Maestro Único CRECISOL**, `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`, gobernanza `.agents/skills/crecisol-project-governance/SKILL.md` y **ADR-015 Cloudflare R2**, `docs/ADR-015_CLOUDFLARE_ALMACENAMIENTO_MULTIMEDIA.md`. Word SHA256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF lectura SHA256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Capítulo 05 leído y verificado previamente en runner real `43364292640`. Requisito: **«C05-09 | Perfiles y fotos seguras | Datos personales y medios aislados | No lectura cruzada de foto/PII»**. El [dictamen institucional C05-08](https://github.com/crecisol-platform/CRECISOL/pull/142#issuecomment-6883945140) certificó sólo el núcleo MFA sintético (8/11), dev anterior `ba9ad6a706cfb1d75e183a83fc023935cdbc89a8`. No habilitó auth productiva ni uploads.

**Decisión ADR-015:** los futuros archivos reales se alojarán en Cloudflare y **MySQL solo conservará una referencia estable y metadatos mínimos**; ninguna URL presignada, contenido binario o token de Cloudflare. La configuración/permiso por tipo y tenant será administrable cuando se implementen C21/C32, no ahora.

## 2. Código técnico y límites exactos

[PR técnico #143](https://github.com/crecisol-platform/CRECISOL/pull/143), HEAD de rama `aeb368e9c72992b90cf26386ec7821c934daf7c7`, diff **EXACTAMENTE 11 archivos**, squash exclusivamente en `dev` `558c54cc9b1aee20a2fac6eb0c637b4f5700a52e`. `main` no se modifica.

- **Domain:** `backend/src/Crecisol.Domain/Identity/PrivateTenantProfileModels.cs` valida nombres de presentación Unicode NFC y excluye control/bidi/XSS; nombre y vista privada redactados en `ToString`. `Owner/Employee/Consumer` son perfiles descriptivos, no roles ni autorizaciones.
- **Application:** `PrivateTenantProfileUseCases` reautentica con **Argon2id C05-04 fresco dentro de cada caso de uso**; el host procede de verificación del backend, actor+tenant+link y revisiones se vuelven a validar bajo SQL. Solo permite `SELF` en tenant actual: un usuario nunca selecciona otro actor, tenant, branch, bucket, nombre de objeto o privilegio. No se emite sesión, JWT, rol ni permiso privado.
- **Infrastructure:** `SyntheticPrivateProfileCipher` cifra solo nombre de visualización personal mediante **AES-256-GCM**, nonce aleatorio de 12 bytes por actualización y AAD binaria **tenant+actor**, con clave aleatoria runtime de 256 bits nunca persistida en Git ni DB y memoria borrada. `MySqlDisposablePrivateProfileLedger` usa `MySqlConnector@2.6.2`, esquema `c05_05_disposable` y host `127.0.0.1` únicamente; identidad de prueba con `EXECUTE-only` en tres procedimientos, sin SELECT/DML directo sobre PII, fotos o auditoría.
- **MySQL 8.4:** `database/tests/fixtures/c05_09_disposable_private_profiles.sql` declara **tres tablas InnoDB**: `c05_09_private_profiles` con ciphertext opaco, revisión y `photo_id`; `c05_09_media_manifest` con referencia `profiles/<tenant>/<actor>/<media>.webp`, MIME `image/webp`, tamaño hasta 2 MiB, SHA256 de contenido simulado, `VERIFIED/CLEAN/PRIVATE`; `c05_09_profile_audit` solo con IDs/acción/tiempo. FK compuesta `(tenant_id,actor_id,photo_id)`; SELECT/UPDATE protegidos en `SQL SECURITY DEFINER` y `FOR UPDATE`; bloqueo optimista CAS de versión y erasure de ciphertext+foto, revisiones monotónicas; triggers `UPDATE/DELETE` prohíben alterar la bitácora. **NO BLOB de foto, NO nombre PII plaintext, NO URL pública/presignada**. La media ya subida/verificada existe solo en un manifiesto **sintético precargado por fixture**; no hay upload real ni detector de MIME de bytes.
- **Prueba real:** `backend/tests/Crecisol.PrivateProfiles.MySqlTests` y `database/tests/c05_09_private_profiles_http_mysql.sh` ejecutan ASP.NET Core .NET10 en **HTTP `127.0.0.1` exclusivamente**, contenedor MySQL8.4 desechable por digest fijado y credenciales aleatorias. Tres rutas `/__c05_09_test__/*` existen exclusivamente en el binario de pruebas, no en `backend/src/Crecisol.Api/Program.cs`, que conserva solo `/api/v1/public/branding/platform`; sin endpoints productivos para perfiles, fotos, subida ni descargas.
- **QA:** `qa/c05_09_profile_audits.py` y `.github/workflows/c05-09-private-profiles.yml`, documentación `docs/c05/C05_09_ESPECIFICACION.md`, auditorías A/B/C separadas y asociadas al SHA exacto, con regresiones C05-01..08 y C03.

## 3. PREMERGE técnico: A/B/C, seguridad y regresiones sobre HEAD final

| Puerta | Ejecución verificada | Evidencia |
| --- | --- | --- |
| C05-09 perfiles y fotos privadas | [#43640233079](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640233079) | `completed/success`. Auditorías A/B/C, **135 mutaciones estáticas de controles de seguridad rechazadas**, compilación .NET10 con warnings-as-errors, **221 comprobaciones genuinas HTTP/MySQL8.4** con 16 solicitudes concurrentes (1 ganador/15 denegados), cifrado AES-GCM, aislamiento tenant A/B y actor distinto, media PENDING/REVOKED/URL traversal/SVG/5MiB denegada, privacía/borrado/auditoría, regresiones C05-01..08 y C por SHA/run. |
| Foundation | [#43640233071](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640233071) | `completed/success`. |
| C03 integral (.NET10/Angular22/Chromium/MySQL8.4) | [#43640233067](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640233067) | `completed/success`, incluyendo los controles históricos y auditoría C. |

Logs de #43640233079 verificados: prueba real `PASS CRECISOL C05-09 REAL HTTP/.NET10/MySQL8.4: 221 checks`, auditoría B `135 static PII/photo tenancy security guard removals rejected`, C checkout sintético `SHA=6a6f6ca74b909766573f6742cc224b69357db176 run=43640233079` ligado a la base/head autorizados. Las regresiones incluyen C05-03 Argon2id 68, C05-04 login 95, C05-08 criptografía 53, C05-08 MFA HTTP/MySQL 149, C05-07 mailbox HTTP/MySQL 79, C05-06 recovery HTTP/MySQL 91 y C05-05 sesiones HTTP/MySQL 52, además de **C05-01 y C05-02**. Todos son contadores de pruebas, NO datos de clientes.

**Historial de fallos anteriores conservado y CORREGIDO sin relajar seguridad:** primer cifrador ubicado en `CredentialSecurity/` activó indebidamente la CI histórica C05-08, cuyo auditor C de diff/rama denegó la ejecución [#43640129673](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640129673) por no pertenecer a C05-08. Tras mover cifrado PII a `Infrastructure/Identity/`, un auditor intermedio todavía apuntó a la ruta anterior y falló [#43640206786](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640206786); se corrigió en HEAD `aeb368e9...` y se reejecutó A/B/C completo. Los fallos anteriores NO se cuentan como `success`; **cero fallos vigentes en las 3 CI obligatorias del HEAD final**.

## 4. POSTMERGE técnico — a verificar antes de eficacia

Seis CI de `dev` iniciadas sobre squash `558c54cc9b1aee20a2fac6eb0c637b4f5700a52e`:
- C05-09 perfiles privados [#43640697333](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640697333).
- Foundation [#43640697309](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640697309).
- C03-02 Domain [#43640697359](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640697359).
- C03-03 Application [#43640697323](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640697323).
- C03-04 Infrastructure/MySQL [#43640697311](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640697311).
- C03-10 integral [#43640697347](https://github.com/crecisol-platform/CRECISOL/actions/runs/43640697347).

**NO VERIFICADAS TODAS AL REDACTAR:** los nombres/IDs iniciados no equivalen a completadas. Esta acta exige 6/6 `completed/success` en SHA final y no cuenta workflows skipped/fallidos/queued/in_progress.

## 5. Matriz de A/B/C y riesgo residual

| Requisito | Evidencia positiva | Negativa probada / prohibición |
| --- | --- | --- |
| Perfil privado por actor/tenant | Mismo actor tiene perfil independiente en A y B; tercero crea su propio perfil A | No lectura cruzada de foto/PII, ni con ID de media conocido, usuario/tenant/ruta falsa, header X-Tenant-Id o X-Forwarded-Host |
| Nombre personal y cifrado | AES-256-GCM con AAD tenant+actor y nonce fresco; lectura solo propia; control de longitud/Unicode NFC | No HTML/SVG/XSS/bidi en texto, no plaintext PII en tabla ni bitácora, decryption cruzada o ciphertext alterado falla |
| Asociación de foto | Solo `media_id` de manifiesto VERIFIED, CLEAN, PRIVATE y ruta exacta objeto simulada, HTTP sin URL real | No foto otro actor/tenant, PENDING/REVOKED, SVG, tamaño >2MiB, traversal, exposición de ruta R2/URL firmada o descarga |
| CAS y erasure | 16 HTTP competidores => exactamente 1 modifica perfil; borrado de ciphertext+media ID con revision incremental | No replay de versión ni borrar perfil ajeno; read después de borrado no devuelve PII ni foto |
| Revisiones/membresía/correo | MySQL revalida credential/account/membership/tenant/host/mailbox vigente y realm TENANT | No acceso con membresía suspendida/restaurada con evidencia vieja, buzón no verificado, host cambiado, realm PLATFORM |
| Privacidad, QA y arquitectura | 3 tablas InnoDB, FK compuestas, usuario EXECUTE-only, audit append-only; A/B/C y HTTP .NET10/MySQL8.4 reales | No SELECT/UPDATE directo desde runner, UPDATE/DELETE de audit, tokens/PII/secretos en logs, conexión DB remota, deploy |

**NO-GO productivo y límites P0/P1 fuera del ensayo:** no existe backend real de usuario/sesión/rol, SuperAdmin con MFA/justificación/período, perfil Angular, fotos de usuarios reales, uploader firmado, bucket Cloudflare R2, verificación bytes MIME/antimalware, transformación/variants/CDN, descarga privada, KMS/rotación AES, borrado físico y retención legal, notificaciones, protección de menores, accesibilidad interfaz y controles anti-enumeración de producción. La certificación **solo** corresponde a contratos internos y datos sintéticos de la prueba. Estas carencias no se declaran implementadas ni autorizadas.

## 6. Puertas de eficacia institucional y rollback

Auditoría **A**: trazabilidad del Word canónico, C05-08 y ADR-015; **B**: PII/cipher, tenant y actor A/B, upload/media falso, carrera, suspensión/revocación, no exposición y auditoría; **C**: arquitectura Domain/Application/Infrastructure, MySQL8.4 real, rendimiento de CAS, API productiva intacta, diff/SHA, regresiones C05-01..08, C03 integral y documentación consistente.

**ACTA SERÁ EFICAZ SOLO SI**:
1. POSTMERGE técnico del PR #143 **6/6 completed/success** en `dev` `558c54cc9b1aee20a2fac6eb0c637b4f5700a52e`.
2. PR documental **SEPARADO** con diff **EXACTAMENTE tres archivos**: esta acta, `qa/c05_09_closure_gate.py`, `.github/workflows/c05-09-certification.yml`. Sus auditorías A/B/C y pruebas .NET10/HTTP/MySQL8.4/contra IDOR y C05-01..08 deben ejecutarse verdaderamente.
3. PREMERGE documental **2/2 completed/success** sobre HEAD de la rama y base dev exactos.
4. Fusionar documental SOLO a `dev` por SHA HEAD esperado; no `main`.
5. POSTMERGE documental **2/2 completed/success** en nuevo SHA de dev, auditoría C confirma mismo SHA/run y diff de tres archivos.
6. Publicar dictamen institucional definitivo en GitHub con SHA, todas las CI, anomalías históricas, alcance limitado, riesgos y rollback.

**HASTA ENTONCES:** C05-09 **NO CERTIFICADA**, C05 **8/11** y C05-10 **BLOQUEADA**. Tras las seis puertas, **C05-09 CERTIFICADA ÚNICAMENTE EN EL NÚCLEO TEST DE PERFILES PRIVADOS CIFRADOS Y REFERENCIAS DE FOTO VERIFICADA CON HTTP .NET10/MYSQL8.4 DESECHABLE Y DATOS SINTÉTICOS (9/11 C05)**; C05-10 habilitada solo para desarrollo. C05 completo no certificado; C06 bloqueado. Nunca inferir implementación o autorización Cloudflare.

**Rollback controlado:** revertir exclusivamente squash documental C05-09 y, tras revisar dependencias, squash técnico `558c54cc9b1aee20a2fac6eb0c637b4f5700a52e` en `dev`; preservar certificados C05-01..08 y fallos históricos. `main`, Word/PDF, `solqaryn/Solqaryn`, Cloudflare, VPS, DNS, correos, datos reales y cobros intactos.

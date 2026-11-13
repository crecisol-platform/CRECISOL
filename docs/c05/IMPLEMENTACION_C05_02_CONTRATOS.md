# CRECISOL — C05-02 · Contratos internos de registro e invitaciones de un solo uso

**Estado:** EN IMPLEMENTACIÓN, NO CERTIFICADA. **Única microfase activa:** C05-02, precedida por preparación auditada [PR #115](https://github.com/crecisol-platform/CRECISOL/pull/115#issuecomment-6845030034) en `dev` `8468a448b3164944fb75fb17270e1d3dc2186c79`; C05-01 [dictamen acotado PR #114](https://github.com/crecisol-platform/CRECISOL/pull/114#issuecomment-6844940005), 12/12 CI técnicas/documentales en SUCCESS. C05-03..11 y C06 BLOQUEADAS. Capítulo C05 NO CERTIFICADO.

**Autoridad:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; fuente C05 [CI #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640) SHA-256 `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`. Plan de 11 microfases y matriz [C05-02 preparación](C05_02_ESPECIFICACION.md), invariantes B01–B18, amenazas T01–T20. El Word conserva ocho pasos de proceso normativo, no equivalentes a once microfases técnicas.

## 1. Superficie implementada y límites de confianza

| Capa | Archivo | Capacidad y límite |
| --- | --- | --- |
| Domain | `backend/src/Crecisol.Domain/Identity/InvitationSecurityContracts.cs` | `InvitationId`, `RecipientChannelBindingId`, `InvitationTokenDigest`, `EphemeralInvitationCode`, `InvitationSnapshot`, versiones de términos, evidencia opaca de canal, `NeutralRegistrationReceipt`. No contiene correo, contraseña, sesión ni grant |
| Application | `backend/src/Crecisol.Application/Identity/InvitationUseCases.cs` | `IssueInternalInvitation` correlaciona C04/C05-01 con política de invitación y directorio de destinatarios; `ClaimInternalInvitation` valida host+tenant backend, canal y consentimiento, expiración y uso único vía interfaz de CAS |
| Tests | `backend/tests/Crecisol.InvitationSecurity.ContractsTests` | .NET 10: CSPRNG, 256 bits, domain separation, redacción, revocación, expiración, 2 empresas, 16 reclamaciones concurrentes en fake CAS, consentimiento y abuso |
| QA | `qa/c05_02_invitations_audits.py` | Auditorías A/B/C con huellas maestras, negativos, mutaciones de barreras, SHA exacto y regresión real |
| CI | `.github/workflows/c05-02-invitations.yml` | .NET10 Release warnaserror, regresiones C03/C04/C05, Angular22, Chromium y MySQL8.4 desechable |

**Estado real de integración:** los nuevos casos de uso no se registran en API, middleware ni DI. `ITrustedInvitationLedger`, `ITrustedCurrentRecipientChannel`, `ITrustedInvitationConsentLedger`, `ITrustedInvitationIssuerPolicy` y directorio destinatario son **puertos con adaptadores sintéticos solamente**. No existe persistencia real de invitación, entrega de correo, registro de usuario, sesión JWT/cookie, verificador de contraseña, confirmación de canal real ni transacción SQL del token. Los ensayos MySQL existentes protegen regresiones históricas de tenant, **NO prueba una transacción de invitación MySQL en esta fase**.

## 2. Invariantes del núcleo y riesgos

- **B01 no enumeración:** `NeutralRegistrationReceipt` devuelve siempre `REQUEST_ACCEPTED_IF_ELIGIBLE` y no expone correo/existencia; el endpoint, el envío asíncrono y los tiempos externos **NO EXISTEN**, por lo que **no se certifica no enumeración HTTP/temporal**.
- **B02/B03/B04/B05:** la invitación usa 32 bytes de CSPRNG, SHA-256 con etiqueta de propósito `CRECISOL/C05-02/invitation/v1/`, sólo digest como metadata persistible, token volátil transferible una vez y borrado al desechar; expiración UTC exclusiva, estado Pending/Claimed/Revoked. `InvitationTokenDigest.Matches` compara en tiempo constante. No es hashing de contraseñas de C05-03 ni sesión.
- **B06/B07/B08/B10/B16:** sólo el backend obtiene actor+tenant desde los contratos sintéticos C04/C05-01; acción de invitación tiene **política independiente**; receptor externo pasa por directorio trusted; perfiles Owner/otros desconocidos se rechazan; un host de B no consume invitación A; cuenta/tenant revocados niegan emisión; plataforma global no recibe grants.
- **B09/B12:** lector de canal **verificado por adaptador futuro** y consentimiento versionado por tenant/recipient; el token no acredita correo, identidad real ni aceptación. Tras reclamo, solo se devuelve `InternalInvitationClaimReceipt` sin creación de usuario, membresía ni login.
- **B11:** la interfaz `TryClaimOnceAtomicallyAsync` exige transacción única real con estado pending, digest, versión, tenant activo, destinatario, TTL y unicidad; **hoy la prueba demuestra un fake CAS sincronizado, NO una transacción real MySQL**. Fallos de backend se propagan, no hay fallback permisivo.
- **B13/B14/B15/B17/B18:** prohibidos JWT, sesión, preguntas de seguridad, PII en logs, privilegio global, salto a C05-03 o certificación productiva; `ToString` redactado de secretos y referencias.
- **No garantizado todavía:** políticas reales de rate limit, no enumeración temporal, verificación de email, entrega segura del token, protección ante side-channels de un endpoint, atomicidad de transacciones MySQL, retries/idempotencia de entrega, transacciones compartidas/dedicadas, auditoría de invitante/consentimiento en base, revocación de grant persistido y UX/accesibilidad de registro. Son P0/P1 de **integración**, NO se deben marcar PASS.

## 3. Estrategia de pruebas y tres auditorías obligatorias

- **A — norma y funcional:** fuentes Word/PDF/C05 por SHA, precondición C05-01, separación estricta de perfiles, uso único y expiración, pruebas .NET reales de emisión/consumo y escenarios negativos T01..T20 según superficie disponible.
- **B — seguridad ofensiva:** actor/tenant A/B, host no verificado, token erróneo, 16 consumidores en paralelo con un ganador, caducidad/revocación, política emisor, consentimiento, repetición y cambio de estado, mutaciones de guardas fail-closed; comprobar que no hay nuevos endpoints privados, secretos ni persistencia incorrecta.
- **C — arquitectura y calidad:** contratos Domain/Application sin dependencias API/SQL, `.NET10 Release -warnaserror`, regresión C05-01+C03+C04, Angular22/Chromium/9 suites MySQL8.4, integridad de SHA/run, diff y rollback.
- **NO GO:** fallo en pruebas o auditoría, P0/P1 en contratos internos, token o PII expuestos en logs, consentimiento/tenant opcional, nonce inseguro, token reutilizable, concurrencia doble ganador, fuente Word no verificable. Lo no ejecutado en HTTP/SQL **no es PASS**.
- **Gate:** PR técnico PREMERGE y POSTMERGE en `dev` sobre SHAs exactos, auditorías A/B/C y acta institucional condicionada independiente. Solo podrá ratificarse **el núcleo interno con dobles**, no la inscripción y envío reales. Para certificar **C05-02 funcional end-to-end** faltan integración backend/persistencia, negativos reales y nuevas auditorías. **C05-03 sigue BLOQUEADA** mientras la microfase no cierre de modo suficiente.

**Reversión:** revertir solamente los commits de C05-02 en `dev` tras revisar dependencias; mantener `main`, Word/PDF, SOLQARYN, DNS/VPS, Cloudflare, pagos y producción intactos.

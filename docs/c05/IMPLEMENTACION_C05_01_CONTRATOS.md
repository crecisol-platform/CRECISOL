# CRECISOL — C05-01 · Implementación de contratos internos de cuentas y credenciales

**Estado inicial:** EN IMPLEMENTACIÓN, NO CERTIFICADA. **Puerta anterior:** preparación auditada PR #112, dev 04c8ad23231a50829e5fbf9025f8b7d4789595b2, 4/4 CI PRE/POSTMERGE, dictamen https://github.com/crecisol-platform/CRECISOL/pull/112#issuecomment-6843853811. **Repositorio exclusivo:** crecisol-platform/CRECISOL, ID 1450414592. C05-02 permanece BLOQUEADA.

## 1. Fuente exacta y autoridad normativa

Documento Maestro Único Word docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c y su PDF de lectura SHA-256 0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77. Capítulo 05 extraído desde Word, run 43364292640: fingerprint textual bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa; 93 líneas aplanadas / 9 009 caracteres / tramo 1807:1900; el Word conserva 16 párrafos sustantivos y una tabla original de 13 filas. No editar la fuente ni confundir ocho puertas de ejecución normativas con once microfases técnicas propuestas.

C05-01 del plan funcional exige **modelo de cuentas y credenciales**, identidad separada de la membresía tenant y prohibición de cuentas con permisos compartidos entre negocios. Una misma persona puede disponer de vínculos **explícitos e independientes** con empresas A y B, pero ningún permiso, estado de sesión, contexto o perfil de A se hereda en B.

## 2. Cambios técnicos estrictos

| Capa | Nuevo archivo y propósito |
| --- | --- |
| Domain | backend/src/Crecisol.Domain/Identity/IdentityAccountModels.cs: IDs opacos, IdentityAccountSnapshot, AccountCredentialMetadata, AccountTenantLinkSnapshot, PlatformOperatorDirectoryEntry y AccountTenantBindingEvidence no autorizante. Estados/revisiones/UTC verificados. |
| Application | backend/src/Crecisol.Application/Identity/ResolveCurrentAccountTenantLink.cs: usa Resolución C04-05 de sesión/host/membresía del backend, lectores exactos actor+tenant, segunda lectura y revalidación; nunca selecciona tenant desde navegador. |
| Tests | backend/tests/Crecisol.AccountIdentity.ContractsTests/: ejecutable .NET10 Release con pruebas positivas y ofensivas A/B, revocación, vencimiento, errores y carreras. |
| QA | qa/c05_01_identity_audits.py y workflow c05-01-account-model.yml: auditorías A/B/C, mutaciones de guardas y regresiones reales sobre SHA exacto. |

**C05-01 no implementa** autenticación por contraseña, WebAuthn, TOTP, MFA, login, registro/invitación, recuperación, revocación de sesiones reales, API privada ni persistencia SQL. Estos pertenecen a C05-02 a C05-11. TrustedActorSessionSnapshot y TenantIdentityContextEvidence de C04 son **puertos sintéticos**, no prueba criptográfica productiva. Ninguna nueva dependencia de paquetes, controlador, tabla, migración o conexión de cliente se introduce.

Identificadores opacos no son bearer tokens. CredentialMetadata solo incluye ID, actor, tipo, estado, revisión y fecha UTC, **sin string/byte[] de contraseña, hash, recovery token, TOTP seed o secreto**. El almacenamiento de verificadores resistentes se hará en C05-03, no ahora.

AccountTenantProfileKind (Owner/Employee/Consumer) describe el perfil **sin otorgar roles ni permisos**. PlatformOperatorDirectoryEntry pertenece a un realm separado, no contiene TenantId ni grant, y no permite acceso privado de SuperAdmin sin futura MFA, reautenticación, causa, vigencia y auditoría.

## 3. Riesgos y pruebas bloqueantes

- **P0 aislamiento:** la identidad de A no habilita a B. El vínculo exige actor y tenant idénticos al contexto C04-05, revocable, único, vigente y con revisión exactamente igual a la de C04; duplicado, vacío, nulo, falso o de otro tenant => rechazo.
- **P0 identidad:** cuenta no activa, actor forjado, cuenta de B o membresía de B => rechazo. Host de B no es permiso para actor de A; cliente no elige host, tenant, actor ni DSN en el nuevo caso de uso.
- **P0 carreras:** doble lectura de cuenta/membresía y revalidación de contexto C04; cambio de revisión, perfil, link ID, estado de sesión o host intermedio => rechazo. Esto no es bloqueo transaccional ni revocación garantizada después de devolver evidencia.
- **P0 verificador:** credencial o perfil no valida hash, firma, autorización ni sesión. No hay ruta productiva; ningún secreto, token o dato personal queda en ToString/DTO.
- **P0 cuenta global:** operador de plataforma separado de perfiles de empresas, sin acceso privado implícito.
- **P1 tiempo:** revisión positiva, UTC, caducidad exclusiva, reloj retrocediendo, cancelación y fallas de puertos denegadas o propagadas sin fallback.
- **Regresiones C04/C03:** probar suites .NET10 existentes, incluidas C04-05/06/11, API pública y contratos. Angular22, Chromium y MySQL8.4 desechables se repetirán como barreras de regresión de la integración, sin cambio de frontend/database.
- **Riesgos de integración posterior:** HTTP/claims reales, CSRF, branch/resource atómico, sesión, token de reset, passkeys, MFA, políticas anti-enumeración, medios y proveedor de contraseñas siguen NO IMPLEMENTADOS y NO PROBADOS.

**Relación con T01–T24 de preparación:** T01/T02/T04/T05/T06 (solo modelo de realm), T08 (estado), T17 (metadata sin secretos), T22 (ambigüedad), T24 (gates) tienen pruebas en contratos internos sintéticos. T03/T07/T09–T16/T18–T21/T23 **NO EJECUTADAS en runtime productivo**. No marcar esas negativas como PASS.

## 4. Auditorías A/B/C y condiciones de cierre

**A funcional/normativa:** Word/PDF y SHA C05, comprobación de capítulos C04/C05, trazabilidad, modelo de identidad y pruebas positivas.
**B seguridad:** mutaciones ofensivas de Domain/Application, tests ejecutados de actor/tenant/perfiles/cambio de revision/expiración, sin API privada/DSN o secretos.
**C arquitectura/calidad:** .NET10 Release -warnaserror, suite C05-01 y regresiones C04/C03, TypeScript/Angular 22, Chromium, nueve MySQL8.4 desechables; evidencia de ejecución con SHA/run actual.

NO GO si falla un control obligatorio o existe P0/P1 en la superficie **de contratos internos**. Solo tras PREMERGE, diff exacto, fusión a dev, POSTMERGE, acta documental y comentario permanente podrá declararse C05-01 CERTIFICADA **EXCLUSIVAMENTE EN CONTRATOS INTERNOS Y FIXTURES SINTÉTICOS**, nunca autenticación real. C05-02 habilitada como máximo para preparación.

**Reversión:** revertir solo cambios de este PR sobre dev tras análisis de dependencias; mantener Documento Maestro, main, frontend, database, SOLQARYN, Cloudflare/VPS y pagos sin cambios.

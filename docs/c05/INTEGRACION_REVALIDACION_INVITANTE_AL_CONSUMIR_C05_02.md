# CRECISOL — C05-02: revalidación atómica del invitante al consumir

**Fecha:** 2026-11-13. **Estado:** INTEGRACIÓN TÉCNICA EN PRUEBAS. **C05-02 NO CERTIFICADA funcionalmente. C05-03 BLOQUEADA.**

**Autoridad:** Documento Maestro Único CRECISOL, Word SHA-256 histórico ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c, PDF 0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77, extracción C05 bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa registrados en docs/MASTER_REFERENCE.md. No se modifican canónicos.

**Repositorio:** crecisol-platform/CRECISOL ID 1450414592. **Base dev:** 4eca74b7c675043dc87f5c195f233503ae815518 (PR #124). **Rama:** feature/c05-02-issuer-recheck-on-claim-20261113. main intacta.

## Hallazgo P0 y corrección de ensayo

PR #124 validó autorización de invitante al emitir, pero la tabla de invitaciones histórica guardaba solo issuer_id, recipient, perfil, hash y términos. No persistía recurso, sucursal, acción ni revisiones que justificaron la emisión. El procedimiento antiguo c05_02_claim_once solo revalidaba issuer.enabled y perfil y receptor, no permisos de recurso. Un permiso posterior de otra sucursal podría entonces parecer suficiente para un vínculo viejo.

El nuevo camino, EXCLUSIVAMENTE en un fixture descartable, separa las capacidades:

1. c05_02_invite_scope_bindings registra **por invitación y tenant exactos** issuer, link, sucursal, recurso, acción identity.invitation.issue, alcance OWNER_ONLY/BRANCH_WIDE y revisiones de cuenta, sesión, miembro, host, catálogo, sucursal, recurso y grant. FKs compuestas por tenant. Sin correos ni tokens planos.
2. c05_02_issue_bound_once valida emisor y autorización actual, y registra **invitación + binding en UNA transacción InnoDB**; ante fallo se revierte todo. La identidad de worker solo tiene permisos EXECUTE.
3. c05_02_claim_bound_once exige binding específico y vuelve a verificar estado de tenant, issuer, cuenta, sesión y vínculo sintéticos, host, catálogo, sucursal, recurso, propietario, grant y acción, revisiones, tiempos, perfil, política, canal y consentimiento. Un solo UPDATE/CAS cambia PENDING a CLAIMED y revisión 1 a 2; cero filas = DENIED. Nunca crea cuentas, permisos, contraseñas ni sesiones.
4. ClaimBoundInternalInvitation combina host directo verificado y canal autenticado por los futuros adaptadores backend; no acepta tenant, actor ni acción del navegador. MySqlDisposableBoundInvitationLedger solo admite MySQL 8.4 descartable por loopback, hace parámetros binarios y elimina copias efímeras de hashes.
5. Pruebas nuevas: dos tenants, token cruzado o erróneo, invitación heredada sin binding, revocación tras emitir, scope de otra sucursal, owner/branch-wide, términos, canal, consentimientos, 16 reclamos concurrentes de los cuales solo uno puede ganar, rechazos SQL directos, sin roles/membresías/sesiones nuevas.

## Limites que impiden certificación

**NO IMPLEMENTA identidad ni autenticación criptográfica real.** Las filas c05_02_auth_sessions/c05_02_auth_links son METADATOS SINTÉTICOS: no hay JWT, MFA, cookie, contraseña, login, membresía real ni router shared/dedicated. Un GUID, correo o token por sí solos NO prueban identidad. Tampoco se ha probado entrega a correo humano externo ni anticaptura de datos o abuso en producción. El puerto de canal de receptor requiere un futuro verificador auténtico.

El legado c05_02_claim_once se conserva para regresión histórica únicamente, nunca como entrada privada productiva. La atomicidad del fixture no sustituye la revisión del sistema real de sesiones y autorización al integrarlo.

**Bloqueos P0/P1 vigentes:** autenticación y revocación real de emisor y receptor; proveedor/inbox externo autorizado; antienumeración temporal E2E/abuso distribuido; onboarding y transacción de membresía bajo shared/dedicated; consentimiento legal versionado y retención PII; clave/rotación, recovery outbox, carreras entre correo y tenant suspendido. **C05-02 NO CERTIFICADA funcionalmente, C05-03 BLOQUEADA.**

## Auditorías A/B/C y rollback

**A:** trazabilidad Word/PDF/C05, contratos previos, binding de recurso del momento de emisión. **B:** 16 consumidores y adversarios reales MySQL8.4, revocaciones, tokens, tenants, acción, rama, resource/owner, PII y mínimo privilegio; mutaciones estáticas complementarias. **C:** diff estricto, SHA/run de misma evidencia real, .NET10 warnaserror, C03/Foundation, sin API/DI productivos.

**Estado al redactar:** nuevas pruebas aún NO EJECUTADAS. Registrar únicamente logs verificados en PR; no falsificar certificación funcional de C05-02.

**Impacto y reversión:** frontend 0, Application/Infrastructure solo pruebas de backend, database solo fixture descartable y runner; ningún cambio a API, migración productiva, Word/PDF, main, SOLQARYN, VPS, DNS, Cloudflare ni cobros. Rollback: revertir el squash único de esta integración tras revisión de dependencias, conservar errores y actas históricas.

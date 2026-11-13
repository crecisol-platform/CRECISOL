# CRECISOL — C05-02: autorización de invitante y emisión atómica por recurso

**Fecha:** 2026-11-13. **Repositorio:** crecisol-platform/CRECISOL, ID 1450414592. **Base dev:** fc20c1af344ea88dfe2eac261822671159b9e158, posterior al PR #123. **Rama:** feature/c05-02-atomic-issuer-scope-20261113. **Estado:** INTEGRACIÓN TÉCNICA DE ENSAYO, **C05-02 NO CERTIFICADA funcionalmente**; C05-03 y siguientes siguen bloqueadas.

## Procedencia y riesgo corregido parcialmente

- Autoridad: Documento Maestro Único, Word SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c, PDF SHA-256 0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77, extracción C05 bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa. Huellas del registro docs/MASTER_REFERENCE.md, no recalculadas aquí. Word/PDF intactos.
- C05_02_ESPECIFICACION.md: B06/B07/B08/B10/B11/B16 y T06/T07/T08/T12/T13/T20. PR #119 ingesta neutral, #120 desafío de correo, #122 outbox AEAD y #123 recepción SMTP sintética, todos con límites de alcance.
- El procedimiento histórico c05_02_issue_once en fixture comprueba tenant activo, emisor habilitado y perfil, **pero no** la sesión, cuenta, membresía, acción, sucursal, recurso o propietario persistidos. No es seguro conectarlo directamente a la API productiva. El nuevo puerto se mantiene aislado sin modificar pruebas históricas.

## Implementación estrictamente de ensayo

1. IssueScopedInternalInvitation en Application combina ResolveCurrentAccountTenantLink, ResolveCurrentBranchResourceScope, destinatario elegible y política confiable. La acción es fija y DEL SERVIDOR: identity.invitation.issue. La sucursal se deriva de la propiedad del recurso almacenado, nunca del navegador. Se cotejan actor, tenant, revisiones de sesión, membresía, host y catálogo y se actualizan las lecturas tras cambios asíncronos.
2. ITrustedScopedInvitationLedger exige evidencia de identidad y de alcance, no un simple issuer_id cliente.
3. MySqlDisposableScopedInvitationLedger solo admite DSN de loopback y esquema c05_02_disposable, desactiva carga local SQL y borra el digest del proceso después de cada llamada. Usa parámetros SQL binarios y revisiones exactas.
4. c05_02_issuer_scope_atomic_fixture.sql agrega ocho tablas de METADATOS SINTÉTICOS (cuenta, sesión, vínculo, host, sucursal, recurso, grant, política) con claves compuestas tenant + scope. c05_02_issue_scoped_once con SQL SECURITY DEFINER y SELECT FOR UPDATE verifica todas las filas, estados, revisiones, expiraciones y política de tiempo DENTRO de la misma transacción que inserta la invitación pendiente. Duplicado o cambio => DENIED. Usuario de worker con solo EXECUTE.
5. Pruebas .NET10/MySQL8.4 con dos tenants, 16 emisores simultáneos, cambios de recurso, owner-only/branch-wide, revocación, TTL de sesión, cambio de términos, cuenta, host, grant, scope, permisos SQL denegados y ausencia de membresías.

## Límite fundamental

**Esto NO IMPLEMENTA autenticación criptográfica real del invitante.** La sesión y cuenta del fixture son metadata sintética que permiten probar su REVALIDACIÓN, no representan un login, un JWT validado, contraseña, MFA, cookie, identidad shared/dedicated ni fuente productiva de autorización. Un UUID o un snapshot por sí solos jamás prueban identidad. El flujo productivo espera una fuente real de autenticación con revocación, consentimiento y permisos por tenant/sucursal/recurso. Además siguen pendientes email a inbox externo autorizado, antienumeración temporal y protección antiabuso. No se crea ninguna membresía real, sesión ni credencial al emitir una invitación.

**Por ello C05-02 NO CERTIFICADA funcionalmente; C05-03 BLOQUEADA.**

## A/B/C, evidencia y reversión

- A: Word/PDF/C05, contratos previos y seguridad de tenant/acción/resource; sin endpoints HTTP productivos.
- B: cross-tenant, stale revisions, cuenta y sesión expiradas/revocadas, grant, sucursal, propietario, política, términos, SQL ACL, ataques concurrentes, PII y no roles implícitos; ejecutar aserciones .NET/MySQL reales además de mutaciones estáticas.
- C: SHA/run exactos y diff, compilación .NET10 Release warnings-as-errors, MySQL8.4, regresiones C03/C05 anteriores, sin main ni SOLQARYN.
- Estado al redactar: pruebas de esta integración PENDIENTES de ejecutar; registrar los resultados comprobados en PR después. No atribuir certificación funcional por pasar CI sintética.
- Impacto: frontend 0; Application + Infrastructure + test .NET, fixture y runner bajo database/tests, QA, docs y workflow; sin cambios en API, DI productiva, migraciones, Word/PDF, servicios externos, producción, VPS, DNS, Cloudflare o cobros.
- Rollback: revertir exclusivamente el squash de esta integración en dev después de inspeccionar dependencias. No modificar pruebas o fallos históricos.

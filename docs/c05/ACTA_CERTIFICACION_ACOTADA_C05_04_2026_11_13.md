# CRECISOL — C05-04 · Acta institucional CONDICIONADA de login contextual

**Fecha:** 2026-11-13. **Repositorio exclusivo:** `crecisol-platform/CRECISOL`, GitHub ID `1450414592`. **Rama de ratificación:** `feature/c05-04-contextual-login-certification-20261113`, base `dev` `be0a0fcee656ee849430a87f85ce6842afaa0261` (PR #133). **HASTA APROBAR TODAS LAS PUERTAS: PROPUESTA NO EFICAZ; C05-04 NO CERTIFICADA Y C05-05 BLOQUEADA.** Sin cambios en main, SOLQARYN, Word/PDF, VPS, DNS, Cloudflare, datos reales, producción ni cobros.

## 1. Autoridad y precedente obligatorio

Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` y registro `docs/MASTER_REFERENCE.md`, Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; capítulo 05 extraído en [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). Plan técnico `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`: **C05-04 Login contextual** y evidencia **«Vinculación de pertenencia sin selector de empresa»**, con negativa **«No confundir sesión de tenant A y B»**. **C05-05 Sesiones y revocación** no puede ser adelantada.

C05-03 certificada únicamente en hash/política Argon2id y pruebas sintéticas por [dictamen #132](https://github.com/crecisol-platform/CRECISOL/pull/132#issuecomment-6863110105), dev `70d68817269536b82e9e86bde0e7f448a5a48b6e`. No se presumen cuentas de personas verificadas, credenciales productivas ni infraestructura de sesión existentes.

## 2. Código técnico que se ratifica (PR #133)

[PR técnico #133](https://github.com/crecisol-platform/CRECISOL/pull/133), HEAD final `568ab1a43bf1473284623ed89fb06abb9221ab65`, squash DEV `be0a0fcee656ee849430a87f85ce6842afaa0261`. Diff revisado EXACTAMENTE **7 archivos**: `Application/Identity/PrepareContextualPasswordLogin.cs`, `backend/src/Crecisol.CredentialSecurity/Argon2idLoginCandidateVerifier.cs`, ejecutable/CSProj `Crecisol.ContextualLogin.ContractsTests`, `docs/c05/C05_04_ESPECIFICACION.md`, QA y workflow.

- Identificador global canónico email ASCII de prueba, sin tenant, slug, host, branch, perfil ni ID de empresa en la firma pública del caso de uso; se requiere directorio confiable de registros 0/1/N y credencial exacta `PasswordVerifier` activa, de actor y realm tenant, no plataforma privilegiada. No hay login administrativo.
- Verificación **real Argon2id C05-03** mediante adaptador independiente, PHC almacenado no exportable por interfaz pública de Application; usuario ausente consulta PHC señuelo backend y devuelve Denied genérico. El ensayo **no mide** equivalencia temporal del login E2E ni configura limitador distribuido.
- Entrada empresarial proviene de backend con `ResolveVerifiedTenantHost` de C04-04, exige **membresía exacta** dentro del tenant verificado y catálogo activo; tener vínculos A+B no concede B bajo A. Entrada central con **una** membresía activa se resuelve sin selector; 0 = Denied, 2+ = Ambiguous SIN revelar listas, **sin autorizar** a nadie.
- Dos lecturas verificables de cuenta/credencial, todos los enlaces, host y catálogo, revisiones y UTC del backend; cambiar un registro, suspender cuenta o tenant, revocar credencial, vincular mal actor/tenant o duplicar membresía bloquea la evidencia. **No hay CAS transaccional de sesión todavía**, C05-05 debe revalidar antes de emitirla.
- `PreparedContextualLoginEvidence` es **no autorizante, no transferible**; no JWT, cookies, token, claim firmado, sesión, acción, permiso, branch, recurso, panel, selector DSN ni URI. La API productiva conserva exclusivamente la ruta de branding. `Crecisol.Infrastructure` mantiene `MySqlConnector@2.6.2`; no hay nuevas dependencias ni migraciones.

## 3. Matriz de aceptación y no-gos

| Prueba positiva real .NET10 en fixtures de confianza | Negativa ofensiva incluida |
| --- | --- |
| Contraseña Argon2id verificada ligada a una cuenta y credencial activas | Contraseña incorrecta, sufijos/prefijos, cuenta duplicada/ausente/suspendida, PHC alterado, credencial revocada |
| Mismo actor en tenants A/B, host A y host B verificados separados | Enlace de B bajo A, host suspendido/no verificado, rebind de dominio, catálogo equivocado |
| Entrada central con un enlace activo y revocado secundario | Múltiples activos = ambiguo sin selector ni lista; sin vínculos = denegado |
| Períodos UTC y revisiones consistentes en todos los lectores | Sesión inexistente, reloj retrocede, membresía expira, tenant se suspende, link/credencial cambian durante la operación |
| Contrato de salida no-portador y privacidad de verificador | Ningún emisor JWT/cookie/sesión, PHC no exportable desde API pública y cero grants de Owner/SuperAdmin |

**Limitaciones P0/P1 para producción, fuera del código inerte actual:** adaptadores reales de directorio/membresías/catalogo host, cuentas activadas y verificación de buzón real, almacenamiento de PHC, control de abuso distribuido/antienumeración medible, rate limit por IP/actor/tenant, MFA/step-up, rotación/revocación de sesiones y consentimiento/grants. Ninguna prueba sintética acredita esos controles productivos. Una futura C05-05 no puede convertir la evidencia interna en una sesión sin pruebas transaccionales de autorización.

## 4. Evidencias verificadas antes del acta

**PREMERGE del PR #133, HEAD final, tres gates técnicos aplicables:**
1. [C05-04 A/B/C, .NET10 real #43498076795](https://github.com/crecisol-platform/CRECISOL/actions/runs/43498076795) — SUCCESS: **A Word/alcance; B 112 mutaciones adversarias rechazadas; 95 aserciones reales** de Argon2id/contexto, C SHA/run; regresiones C05-01 100, C05-02 102 y C05-03 68, todas sin fallos.
2. [Foundation #43498076759](https://github.com/crecisol-platform/CRECISOL/actions/runs/43498076759) — SUCCESS.
3. [C03 integral #43498076955](https://github.com/crecisol-platform/CRECISOL/actions/runs/43498076955) — SUCCESS, .NET 10, Angular 22, Chromium, MySQL 8.4 y regresiones históricas.

**POSTMERGE técnico PR #133 sobre `be0a0fcee656ee849430a87f85ce6842afaa0261`:** [C05-04 #43498783259](https://github.com/crecisol-platform/CRECISOL/actions/runs/43498783259), [Foundation #43498783230](https://github.com/crecisol-platform/CRECISOL/actions/runs/43498783230), [C03 integral #43498783272](https://github.com/crecisol-platform/CRECISOL/actions/runs/43498783272), [C03-03 Application #43498783092](https://github.com/crecisol-platform/CRECISOL/actions/runs/43498783092): **iniciadas y PENDIENTES DE CONCLUSIÓN al redactar**. No se inventan resultados ni cuentan workflows skipped como SUCCESS.

Las revisiones anteriores del PR #133 y sus CI se conservan; la auditoría de la versión final está ligada al commit y al RUN de las ejecuciones citadas. Ninguna prueba de ensayo se representa como login HTTP real.

## 5. Auditorías institucionales A/B/C y condiciones de eficacia

**Auditoría A:** fuente canónica Word/PDF, C05-03 y secuencia, especificación C05-04 contra código y pruebas, evidencia técnica precisa de 3/3 PREMERGE y 4/4 POSTMERGE. **B:** prueba de identidad+hash real, 2 tenants, no selector, duplicación, revocación y carrera, PHC no público, mutaciones negativas; sin datos reales. **C:** paquete de tres archivos exactos (acta, QA, workflow), .NET10 real en SHA del PR documental y de su fusión, regresión C05-01/02/03, arquitectura Application/CredentialSecurity inactiva y sin API, Foundation y C03 integral técnica verde.

**Requisitos obligatorios para declarar C05-04 CERTIFICADA EN ALCANCE ACOTADO:** (1) terminar 4/4 CI POSTMERGE técnico #133 SUCCESS en el SHA `be0a0fce...`; (2) crear la CI específica del acta con A/B/C y ejecución real .NET10/Argon2id + regresiones; (3) PREMERGE 2/2 SUCCESS en HEAD final del acta; (4) fusión por SHA fijo exclusivamente a dev; (5) POSTMERGE 2/2 SUCCESS sobre el SHA documental definitivo; (6) comentario institucional permanente con SHA, runs y límites. P0/P1 en contratos o fallos no corregidos = **NO-GO**.

**Solo al cumplir todas las puertas**: C05-04 **CERTIFICADA EXCLUSIVAMENTE EN CONTRATOS DE LOGIN CONTEXTUAL PRE-SESIÓN Y PRUEBAS LOCALES SINTÉTICAS EN .NET10 (4/11 del C05)**. C05-05 habilitada **SOLO para preparación, desarrollo y pruebas de sus propias sesiones/revocación**; C05 completo NO CERTIFICADO y C06 BLOQUEADO. **No existe login de usuarios reales ni sesión real** por esta acta.

**Rollback:** revertir solamente el squash futuro de acta y, previa revisión de dependencias, el squash técnico `be0a0fcee656ee849430a87f85ce6842afaa0261` en dev; sin afectar Word/PDF, main, SOLQARYN, infraestructura, DNS, Cloudflare, bases o cobros.

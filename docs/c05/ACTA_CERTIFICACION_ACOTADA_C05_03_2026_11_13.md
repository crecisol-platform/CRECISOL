# CRECISOL — C05-03 · Acta institucional CONDICIONADA de hash y política de credenciales

**Fecha:** 2026-11-13. **Repositorio exclusivo:** `crecisol-platform/CRECISOL` (ID `1450414592`). **Rama:** `feature/c05-03-argon2id-certification-20261113` desde `dev` `06186413fd8974566640d66996762da9804fe017`. **ESTADO DE ESTA ACTA HASTA CUMPLIR SUS PUERTAS: NO EFICAZ — C05-03 NO CERTIFICADA; C05-04 BLOQUEADA**. Sin main, Word/PDF, SOLQARYN, servicios externos, VPS, Cloudflare, producción ni cobros.

## 1. Autoridad, requisito y puerta de entrada

Autoridad primaria: Documento Maestro Único de CRECISOL, `docs/MASTER_REFERENCE.md` y `docs/PLAN_37_CAPITULOS.md`. Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Fuente del capítulo 05 extraída [run #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640), huella textual `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`. Ninguna huella ha sido alterada por C05-03.

El plan técnico (11 microfases) fija para **C05-03 «Hash y política de credenciales»** la prueba positiva **«Hash resistente, secretos fuera de repositorio»** y la negativa **«No almacenamiento de claves en claro»**; `docs/c05/C05_03_ESPECIFICACION.md` detalla diseño y riesgos. **C05-02 certificada solo en contratos y ensayos aislados** mediante [dictamen PR #130](https://github.com/crecisol-platform/CRECISOL/pull/130#issuecomment-6862363822), dev `98cf4315110dcd9cfb9fe7e9eb920ecf19f0253f`. No se confunden contratos con registro de usuarios reales.

## 2. Implementación técnica verificada y alcance estricto

[PR técnico #131](https://github.com/crecisol-platform/CRECISOL/pull/131), HEAD final `68a3955547474ddf933eeb90c17f1eb5f99ce1c1`, squash `06186413fd8974566640d66996762da9804fe017`, integrado exclusivamente a `dev`:

- `Application/Identity/PasswordHashingPolicy.cs`: política server-side, versión/revisión, mínimo 15 runas UTF-8, máximo configurable 64–256 sin bajar pisos, m=19–64 MiB, t=2–4, p=1; puerto obligatorio `ITrustedCompromisedPasswordScreen` con veredictos Allowed/Compromised/Unavailable. Unavailable/unknown/throw rechaza en emisión.
- `Crecisol.CredentialSecurity`: proyecto backend ADAPTADOR aislado de `Application`; NuGet original `Konscious.Security.Cryptography.Argon2@1.3.1`. Argon2id v=19 con sal CSPRNG 16 bytes, digest 32 bytes, PHC canónico, comparación constante y parser costo/longitud acotado. Rehash requerido si la política se endurece, buffers temporales limpiados, `PasswordVerifierEnvelope.ToString()` redactado.
- `backend/tests/Crecisol.PasswordHash.ContractsTests`: **68 aserciones reales** con .NET10: candidato correcto/incorrecto, Unicode/controles/surrogates, longitud, PHC manipulado, costo excesivo/downgrade, indisponibilidad del cribado, aleatoriedad/carreras y upgrade de parámetros. Solo candidatos sintéticos generados aleatoriamente; ninguna cuenta real.
- `Crecisol.Infrastructure` se preservó EXACTAMENTE con `MySqlConnector@2.6.2` (requisito histórico C03-04); `Crecisol.Api` no referencia ni registra el adaptador y sigue con su única ruta pública de branding. No hay tablas, migración MySQL de credenciales, sesión, login, JWT, cookies, alta de usuarios ni permisos tenant. El componente autónomo se integrará por composición segura y nuevas pruebas en la secuencia apropiada.

**Limitación crítica visible:** NO hay proveedor real de cribado de contraseñas comprometidas, directorio de cuentas productivo, persistencia de PHC real, rotación de secretos de cuenta ni API de alta. El fake de pruebas **no certifica** cobertura contra contraseñas filtradas conocidas en producción. No usar el adaptador como prueba de identidad ni conceder tenant, branch, Owner/SuperAdmin, membresía o sesión por un hash que coincide.

## 3. Auditorías técnicas A/B/C y evidencias

**PREMERGE verificada del PR #131 sobre HEAD final, todos los workflows aplicables completados/success:**

| Verificación | ID real | Resultado |
| --- | --- | --- |
| C05-03 A/B/C y .NET10 Argon2id | [43492886139](https://github.com/crecisol-platform/CRECISOL/actions/runs/43492886139) | SUCCESS: A fuente, B 99 mutaciones negativas, 68 aserciones Argon2id, 100 C05-01, 102 C05-02, C SHA/run |
| Foundation | [43492886038](https://github.com/crecisol-platform/CRECISOL/actions/runs/43492886038) | SUCCESS |
| Regresión C03 integral | [43492885956](https://github.com/crecisol-platform/CRECISOL/actions/runs/43492885956) | SUCCESS, .NET10/Angular22/Chromium/MySQL8.4 |

**POSTMERGE técnico:** inició sobre `06186413fd8974566640d66996762da9804fe017` con IDs `43493725591` (C05-03), `43493725693` (Foundation), `43493725558` (C03 integral), `43493725569` (Application), **PENDIENTE DE VERIFICACIÓN AL REDACTAR**. Ninguna ejecución `skipped` se presume `success`.

**Fallos históricos conservados:** la primera CI falló por incompatibilidad de sobrecarga UTF-8 .NET (CS1503); se corrigió la codificación de `ReadOnlySpan<char>` con `GetByteCount`/buffer cero. La regresión C03 posterior detectó que un NuGet añadido al proyecto Infrastructure infringía su pin exclusivo MySqlConnector; sin debilitar las reglas históricas, se encapsuló Argon2id en módulo `Crecisol.CredentialSecurity`. Ambos fallos permanecen auditables; los SHA y las CI correctivas son posteriores y no cambian el historial.

## 4. Puerta institucional propia y decisión condicionada

**Auditoría A:** Documento Maestro Word/PDF, C05-02 precedente, mapeo C05-03 y trazabilidad código/pruebas. **B:** controles de hash no reversible, aleatoriedad, parsing anti-DoS, bloqueo fail-closed de credenciales comprometidas, 99 mutaciones de controles y ejecución real, no grants por tenant. **C:** revisión del diff técnico, arquitectura sin cambio a APIs productivas, NuGet revisado/versión fija, C03/compatibilidad y regresión, SHA y RUN ID verificados de la propia acta.

**Para que este documento sea EFICAZ** se deben verificar: (1) POSTMERGE técnico #131 íntegramente SUCCESS en `06186413...`; (2) acta y gate propios con A/B/C, .NET10/compilación y regresiones reales en PR documental; (3) PREMERGE de esa acta SUCCESS, diff exacto solo docs/QA/workflow y fusión exclusiva a `dev` por HEAD SHA fijo; (4) POSTMERGE de la acta SUCCESS en SHA de fusión; (5) comentario institucional permanente con SHAs, evidencias y riesgos. **Antes de estas cinco puertas: C05-03 NO CERTIFICADA, C05-04 BLOQUEADA.**

**Únicamente después de todas:** **C05-03 CERTIFICADA EXCLUSIVAMENTE EN CONTRATOS DE POLÍTICA, HASHING ARGON2ID Y PRUEBAS LOCALES .NET10 (3/11 microfases C05)**; C05-04 habilitada **SOLO para iniciar su preparación y desarrollo propio** de login contextual con identidad, sesiones y permisos reales que correspondan. **C05 COMPLETO NO CERTIFICADO; C06 BLOQUEADO**. Esta fórmula no certifica credenciales de usuario productivo ni seguridad end-to-end de login.

## 5. Riesgos pendientes, reversibilidad

P0/P1 de una futura activación (sin bypass): fuente de contraseñas comprometidas real y siempre disponible o fail-closed, directorio protegido y auditoría de credenciales, integración del password verifier con identidad de cuenta y estado/revocación real, rate limit en login, cookie/sesión segura, MFA/step-up global, gestión de longitud y migraciones de credenciales, segregación por tenant/branch/recurso y rotación/recovery. Todos **NO IMPLEMENTADOS/NO PROBADOS** en esta microfase; su ausencia impide producto/producción, no equivale a vulnerabilidad explotable en el adaptador inerte.

**Rollback:** revertir exclusivamente el squash documental de esta acta en `dev` si fuese necesario, o el técnico `06186413fd8974566640d66996762da9804fe017` previa evaluación de dependencias. Jamás tocar `main`, SOLQARYN, Documento Maestro, VPS/DNS, Cloudflare, producción ni cobros.

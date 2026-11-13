# CRECISOL — C05-02 · Acta CONDICIONADA de preparación de registro e invitaciones

**Fecha:** 12-11-2026, Honduras. **Estado:** BORRADOR DE PREPARACIÓN, **NO EFICAZ** hasta que se verifiquen las auditorías propias A/B/C, CI PREMERGE y POSTMERGE y comentario institucional. **C05-02 NO IMPLEMENTADA / NO CERTIFICADA; C05-03..11 BLOQUEADAS; C05 NO CERTIFICADO; C06 BLOQUEADO.**

## 1. Procedencia y dependencia autorizada

Repositorio `crecisol-platform/CRECISOL` ID `1450414592`, base `dev` `39b0a322e6b8f0f8eb55db8668a959c16521f132`, rama `feature/c05-02-preparacion-registro-invitaciones-20261112`. PR de preparación por abrir; esta acta no se presume fusionada.

**C05-01 cert. de alcance limitado:** [dictamen PR #114, comentario #6844940005](https://github.com/crecisol-platform/CRECISOL/pull/114#issuecomment-6844940005). 12/12 CI aplicables técnicas y documentales `completed/success`, finales sobre `d4a62cad50c66923dec2c871d844b3b578085896` y `39b0a322e6b8f0f8eb55db8668a959c16521f132`. Certificación **solo en contratos internos y fixtures sintéticos**, NO existe todavía autenticación productiva.

El Documento Maestro Único Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` y PDF SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77` mantienen autoridad. Texto capítulo C05 SHA-256 `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`, 93 líneas aplanadas y rango `1807:1900`, extracción [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). Word: ocho puertas normativas; plan funcional: once microfases C05-01..C05-11.

## 2. Alcance de preparación y modelo de riesgos

Solo se incorporan `docs/c05/C05_02_ESPECIFICACION.md`, esta acta condicionada, `qa/c05_02_preparation_audits.py` y `.github/workflows/c05-02-preparation.yml`. El objetivo es identificar estados, invariantes B01–B18, amenazas T01–T20, criterios de aceptación y frontera entre registro/invitación y credenciales/sesiones.

Los controles de futura implementación deberán proteger token de invitación **CSPRNG con hash, TTL y uso único atómico**, destinatario exacto, emisor autorizado, pertenencia tenant inequívoca, no enumeración de email, revalidación de revisiones, consentimiento/condiciones vigentes versionadas, neutralidad de respuestas, revocación y denegación entre tenants. El correo verificado, token o perfil no otorgan permiso. SuperAdmin no se adquiere por invitación empresarial.

**Prohibido declarar pruebas NO EJECUTADAS como PASS:** sin implementación HTTP, envío email, verificador de credenciales, inscripción real, MySQL de identidad, transacción de tokens, concurrencia real de invitaciones, rate limiting, cookies, JWT, MFA/WebAuthn, ni pruebas de registro de usuario productivo. Los ensayos históricos de C04 MySQL no son ensayos reales del registro C05-02.

## 3. Tres auditorías preparatorias por verificar

| Auditoría | Exigencia | Estado antes de CI final |
| --- | --- | --- |
| A — norma/traza | SHA Word/PDF/texto C05, fila C05-02 del plan, prueba de cierre de C05-01, cobertura exacta de invariantes y amenazas | **PENDIENTE** |
| B — seguridad | 18 mutaciones documentales B01–B18 rechazadas, 20 amenazas T01–T20, API solo pública, ausencia de contraseñas/tokens y autorización impropia | **PENDIENTE** |
| C — arquitectura y evidencia | GitHub SHA/run real, prueba de A y B del mismo proceso, padres exactos de merge PR, diff solo de preparación, permisos read-only | **PENDIENTE** |

Para declarar **PREPARACIÓN C05-02 AUDITADA** exigir: (1) CI PREMERGE completa sobre HEAD final, sin fallos vigentes (2) diff exacto solo docs/QA/workflow (3) squash solo a `dev` con HEAD verificado (4) CI POSTMERGE SUCCESS sobre SHA final y (5) comentario permanente con SHAs/runs y limitaciones. No equivale a C05-02 implementada ni a C05-03 habilitada.

## 4. Riesgos y reversibilidad

Los riesgos **P0** de enumeración, token replay, consumo concurrente doble, escalada de perfil, invitación de tenant ajeno, registro en cuenta suspendida y aceptación de términos ajenos son **controles diseñados y NO PROBADOS EN RUNTIME**. En implementación deberán enfrentarse con pruebas efectivas de API/servidor, MySQL8.4, sesiones, concurrencia, rate limit y cross-tenant cuando existan; ningún P0/P1 vigente en su superficie podrá ser certificado. Separar siempre Domain, Application, Infrastructure y API; no revelar email/PII/secretos en auditorías.

Reversión exclusiva del PR preparatorio en `dev`, con inspección previa de dependencias. Ninguna modificación de `main`, Word/PDF, `frontend/`, `backend/`, `database/`, SOLQARYN, producción, DNS, VPS, Cloudflare ni cobros.

**Dictamen de este archivo al crearlo:** **NO EFICAZ, CI/PR/fusión/POSTMERGE/COMENTARIO PENDIENTES. C05-02 NO IMPLEMENTADA Y NO CERTIFICADA.**

# CRECISOL — C06-03: acta institucional de identidad, pertenencias y contexto autorizado

**Fecha:** 24-11-2026. **Estado:** CANDIDATA DE CIERRE INSTITUCIONAL; NO CERTIFICADA MIENTRAS E2E C05 POSTMERGE Y GATES DE ESTA ACTA NO FINALICEN. **Alcance:** servidor C04/C05 local e integración; ninguna política RBAC/ABAC nueva.

## 1. Fuentes y dependencia

Repositorio CRECISOL id 1450414592, ruta vigente crecisol-platform/CRECISOL, redirigido desde solqaryn-platform/CRECISOL. Documento Maestro Único Word, SHA-256 aprobado ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c, comprobado por CI. Reglas de AGENTS.md, Skill propia, docs/MASTER_REFERENCE.md, inventarios de C06-01 (80 HTTP, 12 contratos internos, 28 normativas futuras sin grant) y matriz de C06-02 (10 perfiles, 108 acciones, propuestas no autorizantes). Se preservan todos los controles anteriores.

**Entregable:** resolver realmente en backend identidad, tenant y membresía vigente, sin confiar en IDs, cabeceras o claims del navegador. El mero contexto de identidad NUNCA autoriza lectura/escritura/exportación privada; política ABAC de recurso será C06-04.

## 2. Código y cambios exactos

PR técnico [#199](https://github.com/crecisol-platform/CRECISOL/pull/199), base dev e5fe2830ab8284238389d7a8d5cc6d4add1a0b99; HEAD premerge 3acef17c86faf71e182d2157e1e3449beb0c81dd; fusionado solo en dev SHA **ea7e341010734665c1f3975047aef12d0aaebfb2**. main permanece b794181148d2c5e36c1c88127f19a8a9082acff5.

Nueve archivos de alcance, sin migraciones ni cambios a frontend:
- backend/src/Crecisol.Domain/Tenancy/C06OperationalMembershipContext.cs: evidencia inmutable y verificación de cuenta, actor, tenant, MFA, membresía, revisiones, inactividad/expiración y UTC. AuthorizesPrivateResource=false; ToString redacted.
- backend/src/Crecisol.Infrastructure/Tenancy/C06OperationalContextProjection.cs: proyección exclusiva desde las filas verificadas y bloqueadas C04/C05 en la misma transacción; GUID RFC4122, reloj de MySQL. Sin elegir actor o tenant desde el navegador.
- backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs: host y tenant del catálogo activo, host_rev y catalog_rev válidos, address_mode concordante, bloqueo FOR SHARE.
- backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs: invoca la nueva comprobación antes de la renovación de sesión y de cualquier operación autenticada de C05.
- backend/tests/Crecisol.C06Membership.ContractsTests/Program.cs y .csproj: 53 aserciones positivas/negativas.
- qa/c06_03_context_audits.py y .github/workflows/c06-03-trusted-tenant-context.yml: auditorías A/B/C con diff por SHA y 23 mutaciones adversariales.
- docs/c06/C06_03_ESPECIFICACION.md: trazabilidad y condiciones de aceptación.

**Invariantes:** host verificado -> tenant del servidor = tenant de sesión y membresía. Token HMAC -> session.account_id = account.account_id = membership.account_id = factor.account_id. La membership_id y factor_id coinciden con sesión. Cuenta verificada y activa, membresía activa y no expirada, MFA vigente, revisiones idénticas y sesión no revocada. Inactividad administrable C05 (valor vigente 30 minutos) comprobada con reloj del servidor y bloqueos transaccionales. Sin selector de empresa, JWT nuevo, seed de roles, grants, nueva API, VPS, datos reales, cobros o SOLQARYN.

## 3. Evidencias reales por SHA

| Prueba | Resultado verificado |
| --- | --- |
| [PREMERGE C06-03 A/B/C #45506430723](https://github.com/crecisol-platform/CRECISOL/actions/runs/45506430723) | 3/3 SUCCESS |
| Contratos C06-03 .NET 10 | 53 comprobaciones, SUCCESS |
| Mutaciones de protecciones de identidad | 23 controles ofensivos rechazados |
| Regresión C04-05 de identidad | 110 comprobaciones, SUCCESS |
| [PREMERGE C05 operacional #45506430398](https://github.com/crecisol-platform/CRECISOL/actions/runs/45506430398) | 2/2 jobs SUCCESS; 340 aserciones Angular → API → MySQL 8.4 y 73 pruebas Chromium aprobadas |
| [PREMERGE TOTP SHA1 C05 #45506430402](https://github.com/crecisol-platform/CRECISOL/actions/runs/45506430402) | A/B/C 3/3 SUCCESS, con DB aislada |
| [C06-01 #45506430510](https://github.com/crecisol-platform/CRECISOL/actions/runs/45506430510) | A/B/C 3/3 SUCCESS |
| [Regresión integral C03-10 #45506430727](https://github.com/crecisol-platform/CRECISOL/actions/runs/45506430727) | SUCCESS |
| [Regresión integral C04-12 #45506430438](https://github.com/crecisol-platform/CRECISOL/actions/runs/45506430438) | SUCCESS |
| [POSTMERGE C06-03 A/B/C #45508442081](https://github.com/crecisol-platform/CRECISOL/actions/runs/45508442081) | 3/3 SUCCESS en dev SHA ea7e341... |
| [POSTMERGE C06-01 #45508442085](https://github.com/crecisol-platform/CRECISOL/actions/runs/45508442085) | A/B/C 3/3 SUCCESS en dev SHA ea7e341... |
| [POSTMERGE C05 operacional #45508442240](https://github.com/crecisol-platform/CRECISOL/actions/runs/45508442240) | EN EJECUCIÓN al redactar; NO atribuir PASS anticipado |

## 4. Tres auditorías y seguridad/privacidad

**A — Funcionalidad y fuente:** comprobar Word maestro aprobado, inventarios C06-01/02, host+catálogo y nueve rutas del diff, evidencias reales y ausencia de poderes nuevos.

**B — Seguridad:** falsificación de actor/host/tenant, sesión o factor ajeno, cambio y revocación de membresía, estado de cuenta, token expirado y control administrativo de 30 minutos; 53 contratos, 23 mutaciones, 340 integraciones C05 reales. No exponer GUID, correo, MFA, token, PII ni conexión MySQL.

**C — Arquitectura, rendimiento y experiencia:** invariante en Domain, proyección Infrastructure integrada en transacción C05, .NET 10 Release -warnaserror sin errores ni warnings, Angular 22 types/build y Chromium. No cambia ninguna interfaz. Advertencias previas de CSS/CommonJS qrcode/dijkstrajs no bloqueantes permanecen en el historial; no afirmar 9.5 VISUAL en una fase sin pantallas nuevas. No se midió throughput productivo: no extrapolar latencias del entorno de prueba.

## 5. Calificación, no-go y reversión

**Nota técnica objetivo >= 9.5/10, DICTAMEN AÚN CONDICIONADO.** Para comprar/certificar el entregable deben pasar: E2E operativo C05 POSTMERGE, ausencia de defectos P0/P1, auditorías A/B/C de este PR documental PRE/POST por SHA y acta trazable. El hecho de pasar pruebas PREMERGE no certifica su instalación productiva; C06 en conjunto sigue NO CERTIFICADO.

Tras completar los gates: C06-01/02/03 = **3/11 microfases certificadas solo en sus alcances**, 5/37 capítulos completos; C06-04 habilitada **exclusivamente para preparación de su propia puerta**; C07 bloqueado. Antes de esos resultados oficiales: **2/11** y C06-03 NO CERTIFICADA.

**Rollback:** revertir PR #199 y esta acta previa revisión de dependencias; C06-03 no creó migraciones, datos ni grants. Ninguna escritura en main, producción, servidores ni SOLQARYN.

# CRECISOL — C05-11 · Acta institucional CONDICIONADA de auditoría integral del capítulo 05

**Fecha de redacción:** 2026-11-14. **Repositorio único:** `crecisol-platform/CRECISOL` (GitHub ID `1450414592`). **Base documental:** `dev` `81a31107077fa439e2a7a3ecb41964cfca5ea081`. **Rama:** `feature/c05-11-institutional-20261114`. `main` `b794181148d2c5e36c1c88127f19a8a9082acff5` permanece fuera de alcance.

**ESTADO AL REDACTAR: ACTA CONDICIONADA, NO EFICAZ. C05-11 NO CERTIFICADA; CAPÍTULO C05 NO CERTIFICADO; C06 BLOQUEADO.** Una CI verde anterior NO hace eficaz esta acta: faltan el cierre técnico POSTMERGE final y las puertas de ratificación documental PREMERGE/POSTMERGE sobre sus SHAs y un dictamen permanente. El historial de estados condicionados no se reescribe para simular cumplimiento.

## 1. Fuentes canónicas y aceptación exacta

Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` Git blob `fe7aaab60b603f1844e18666e2f99c0ea97e86e9` y SHA256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF de lectura Git blob `93c8412b2450e85a882b622e7a012477c61c2359` y SHA256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. El auditor integral técnico #148 confirmó estas huellas en los bytes reales y estructura DOCX; el gate institucional volverá a comprobar las fuentes y el registro `docs/MASTER_REFERENCE.md`. Gobernanza: `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`, `docs/SEGURIDAD_MULTITENANT.md`, `AGENTS.md` y `.agents/skills/crecisol-project-governance/SKILL.md`.

**Criterio exacto:** «C05-11 | Auditorías integrales y acta C05 | A/B/C de autentificación, abuso y aceptación | No habilitar C06 sin identidades confiables». La dependencia histórica C04 está cerrada en su núcleo de contratos/fixtures; eso no convierte los endpoints privados de C05 en un sistema de producción. Las pruebas comerciales COM-T08/COM-T09/COM-T10 siguen **NO EJECUTADAS**.

## 2. Trazabilidad exhaustiva de las diez microfases previas

Las diez PR institucionales siguientes están **fusionadas en `dev`** y sus actas permanecen en GitHub. Esta matriz se refiere solo al alcance original ratificado de cada una:

| Microfase | Acta / SHA de fusión, ámbito | Regresión/negativa crítica |
| --- | --- | --- |
| C05-01 | [#114](https://github.com/crecisol-platform/CRECISOL/pull/114), `39b0a322e6b8f0f8eb55db8668a959c16521f132`, contratos de identidad | Cuentas separadas de tenant links; sin concesión de rol |
| C05-02 | [#130](https://github.com/crecisol-platform/CRECISOL/pull/130), `98cf4315110dcd9cfb9fe7e9eb920ecf19f0253f`, contratos/registro neutral | Anti-enumeración de correo, destinatario/issuer firmados, replay/invitación/privacidad |
| C05-03 | [#132](https://github.com/crecisol-platform/CRECISOL/pull/132), `70d68817269536b82e9e86bde0e7f448a5a48b6e`, Argon2id | No persistir contraseñas en claro, dummy PHC real |
| C05-04 | [#134](https://github.com/crecisol-platform/CRECISOL/pull/134), `358df52d785cb4074790ce7841c5c8d0d4873908`, login contextual | Host y membership, no selector o sesión A/B mezclada |
| C05-05 | [#136](https://github.com/crecisol-platform/CRECISOL/pull/136), `f34a751ee10dddc47c8f75fb49291a4741d5cca5`, sesiones | Revocación, replay y concurrencia de refresh |
| C05-06 | [#138](https://github.com/crecisol-platform/CRECISOL/pull/138), `4a97dbd29b3acc93e9c20ae09e0e26945af98ea0`, recuperación | Token de un uso, caducidad, revocación |
| C05-07 | [#140](https://github.com/crecisol-platform/CRECISOL/pull/140), `e648d7512897e2c6aaf5c42a75969f63e315c6a6`, buzón y cambios | Reautenticación, confirmación y no alias ajeno |
| C05-08 | [#142](https://github.com/crecisol-platform/CRECISOL/pull/142), `ba9ad6a706cfb1d75e183a83fc023935cdbc89a8`, MFA/step-up | TOTP/WebAuthn ES256 y recuperación sin bypass |
| C05-09 | [#144](https://github.com/crecisol-platform/CRECISOL/pull/144), `6b7e0d39785bcad00cc059954eaef510f10d84fa`, PII/foto privada | AES256-GCM por actor/tenant, IDOR de referencia media A/B |
| C05-10 | [#147](https://github.com/crecisol-platform/CRECISOL/pull/147#issuecomment-6885019825), `52efec64f43245771af59459f8db3b38685c1703`, rate limit/bitácoras | 6x16, 5 admitidos/11 limitados/0 indisponibles, HMAC sin tokens/contraseñas |

**C05-02 — advertencia institucional:** [PR antiguo #121](https://github.com/crecisol-platform/CRECISOL/pull/121) de documentación sobre entrega verificable y privacidad permanece `open/draft`, no fusionado. NO es evidencia de envío real de correo, no se toma como certificación productiva y no modifica la vigencia acotada del PR #130. Queda registrada la necesidad de integrar entrega y propiedad de buzón con sistemas reales antes de C06 y producción.

**C05-10 — fallo histórico conservado:** [POSTMERGE original #145, run #43643788104](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788104) terminó `failure`: 16 intentos simultáneos entregaron **5 ADMITTED / 4 LIMITED / 7 UNAVAILABLE**. La remediación #146 corrigió el reloj de MySQL `UTC_TIMESTAMP(6)` contra esperas en los locks usando `SYSDATE(6)` posterior a cada lock dentro de fixture, y la ratificación #147 aprobó 10/10 puertas sobre SHAs posteriores. No borrar ni reetiquetar ese fallo. **No autorizar SYSDATE(6) en MySQL productivo con replicación por sentencias** sin estudio nuevo.

## 3. Auditoría técnica integral #148 — alcance y evidencia

[PR técnico #148](https://github.com/crecisol-platform/CRECISOL/pull/148), HEAD `7e560b019d4a15abc73594ed0ce116cc7ddf16c4`, base `52efec64f43245771af59459f8db3b38685c1703`, diff **EXACTAMENTE cuatro archivos**: `docs/c05/C05_11_ESPECIFICACION.md`, `docs/c05/C05_11_MATRIZ_RIESGOS.md`, `qa/c05_11_integral_audits.py`, `.github/workflows/c05-11-integral.yml`; squash exclusivo a `dev` `81a31107077fa439e2a7a3ecb41964cfca5ea081`. No altera runtime productivo ni datos reales.

**PREMERGE técnico 2/2 SUCCESS sobre HEAD:**
- [Auditoría integral C05-11 #43648572848](https://github.com/crecisol-platform/CRECISOL/actions/runs/43648572848): A Word/PDF hashes, 11 filas/10 actas y pruebas; B **128 mutaciones negativas** rechazadas; compilación de todos los proyectos backend .NET10 Release `-warnaserror`; reales contratos identidad/Argon2id/host/branch y ofensiva C04, Angular22/Node24, Chromium público/privacidad/accesibilidad, **17 runners MySQL8.4 reales** de C05-02 y C05-05..10 + ataque SQL multitenant C04-11; C con prueba SAME-SHA/SAME-RUN y diff exacto 4. Evidencia concreta: invitaciones 16 clientes competidores → un ganador, registro HTTP/MySQL neutral 42 aserciones, MFA 149, perfiles 221 y rate-limit 211 verificaciones HTTP/MySQL, incluida carrera de seis rondas 5/11/0.
- [Foundation #43648572867](https://github.com/crecisol-platform/CRECISOL/actions/runs/43648572867): `completed/success` sobre mismo HEAD.

**POSTMERGE técnico requerido, PENDIENTE AL REDACTAR:** [Integral #43649707384](https://github.com/crecisol-platform/CRECISOL/actions/runs/43649707384) y [Foundation #43649707336](https://github.com/crecisol-platform/CRECISOL/actions/runs/43649707336) en squash `81a31107077fa439e2a7a3ecb41964cfca5ea081`. Exigir **2/2 completed/success**, sin considerar `queued`/`in_progress`/`skipped` como PASS. Si falla uno, NO CERTIFICAR; corregir y reejecutar desde nuevo SHA.

## 4. Tres auditorías transversales sobre 20 barreras P0/P1

**A — funcional/precedencia:** validar Word/PDF originales y SHA reales, orden 11 microfases, diez actas y PRs fusionados; claridad de alcance y dependencias COM-T08/09/10 NO EJECUTADAS; registros históricos fijos sin editar; ningún avance no autorizado a C06.

**B — seguridad, abuso y privacidad:** [matriz R05-01..20](C05_11_MATRIZ_RIESGOS.md), solo evidencias reales de .NET y MySQL/A/B. Ensayo de forged tenant/Host/claim, cambios de realm y membresía, Argon2id real, invitación firmada, registro neutral, session CAS revocation, recuperación one-shot, reautenticación de correo, MFA risk/TOTP/WebAuthn/recovery, PII AES256-GCM y foto privada, limiter HMAC por fuente+buzón, spoof de XFF, 6 ráfagas 5/11/0, trazas append-only sin IP/correo/contraseña/token en claro y permisos MySQL mínimos. **128 mutaciones estáticas** no sustituyen 17 runners MySQL8.4 y casos reales C04-11. Si existe P0/P1 en el núcleo sintético reejecutado, NO-GO capítulo.

**C — arquitectura/compatibilidad/UX:** Domain/Application/Infrastructure/API por capas; UI Angular22/Node24 pública, Chromium pruebas móviles/accesibilidad sin prometer UI de identidad real; backend ASP.NET10 y MySQL8.4 desechable por digest, sin producción; SHA/branch/diff, rollback, persistencia/seguridad de hosts, evidencia same-run, regresiones C03/C04. El producto `backend/src/Crecisol.Api/Program.cs` continúa con **únicamente /api/v1/public/branding/platform**. No exponer rutas de login, sesiones, buzón, MFA, perfiles privados o rate limiter productivos mediante esta ratificación.

**Matriz de resultado exigido:** R05-01..R05-17 y R05-20 deben quedar comprobadas en el **ámbito sintético**, no como seguridad productiva. R05-18 y R05-19 permanecen **NO-GO PRODUCTIVO** por falta de identidad real y pruebas comerciales; su restricción se respeta negando C06. La matriz original conserva `PENDIENTE` como estado de *preparación* y no se reescribe para simular hallazgos cerrados; los resultados concretos se formalizan aquí y en el comentario final solo tras CI.

## 5. Limitaciones absolutas y no habilitación de C06

**NO-GO PRODUCTIVO:** los diez módulos C05 se han probado como contratos, adaptadores/fixtures y rutas **solo en ejecutables HTTP de ensayo 127.0.0.1**; NO existe un flujo real unificado en la API de usuarios que conecte registro → propiedad del email → identidad y credencial persistida → login autorizado → MFA y recuperación → sesiones revocables → rate-limit → bitácora duradera → roles y permisos de SuperAdmin y tenant. No se ha ensayado ataque distribuido real, TLS/reverse proxy, KMS/rotación multiserver, passkeys navegador FIDO2 productivas, plataforma de correo real, WAF, persistencia de PII real, Cloudflare R2/antimalware, retención física, respaldos/restore de clientes ni panel Angular privado o accesibilidad manual.

**El criterio del Maestro «No habilitar C06 sin identidades confiables» significa que C06 permanece BLOQUEADO** mientras no exista esa integración end-to-end probada; una conclusión técnica de **11/11 microfases sintéticas auditadas** NO autoriza código funcional privado del capítulo 06, producción, dominios, VPS, Cloudflare, pagos ni datos reales. Tampoco permite afirmar `C05 productivo certificado` o `5/37 capítulos productivos completos`. La documentación preparatoria futura requiere decisión propia y no sustituye este NO-GO.

## 6. Ratificación final condicionada

1. Técnica PREMERGE de #148: **2/2 SUCCESS** HEAD `7e560b019d4a15abc73594ed0ce116cc7ddf16c4` (cumplida).
2. Técnica POSTMERGE de #148: **2/2 SUCCESS** squash `81a31107077fa439e2a7a3ecb41964cfca5ea081`, confirmar y registrar; NO verificada al redactar.
3. PR documental SEPARADO, diff **exactamente 3 archivos**: esta acta `docs/c05/C05_11_CERTIFICACION_INSTITUCIONAL.md`, gate `qa/c05_11_closure_gate.py` y workflow `.github/workflows/c05-11-certification.yml`. Auditorías institucionales A/B/C y pruebas positivas/negativas reales de C05, más SHA/run y diff.
4. Documental PREMERGE **2/2 completed/success** sobre HEAD de PR institucional con base técnica final verificada, sin P0/P1 sintéticos vigentes.
5. Fusionar solo `dev` por squash con HEAD exacto, comprobar documental POSTMERGE **2/2 completed/success** en nuevo SHA y `main` sin modificaciones.
6. Publicar un **dictamen institucional permanente** en PR documental con SHA exacto, evidencia A/B/C, limitaciones, PR #121 open/draft, remediación histórica #145→#146, rollback y estado conservador de C06. Sin las seis, **esta acta no es eficaz**.

**Solo después de todas las puertas:** C05-11 **CERTIFICADA COMO AUDITORÍA TÉCNICA DEL NÚCLEO SINTÉTICO**; C05 **11/11 microfases verificadas en esos alcances** y capítulo de contratos/fixtures auditado; C05 como sistema productivo **NO CERTIFICADO**, **C06 BLOQUEADO** hasta integración y prueba de identidades confiables. Nunca extrapolar aceptación interna a clientes reales.

**Rollback:** revertir en orden squash de acta institucional y squash técnico `81a31107077fa439e2a7a3ecb41964cfca5ea081` solo en `dev`, previo análisis de dependencias, conservando C05-01..10, fallos históricos y Word/PDF; `main`, `solqaryn/Solqaryn`, VPS, Cloudflare, correo real, pagos y producción intocados.

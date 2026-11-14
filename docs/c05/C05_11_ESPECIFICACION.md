# CRECISOL — C05-11 · Especificación de auditorías integrales y acta del capítulo 05

**Inicio:** 14-11-2026. **Estado:** C05-11 EN DESARROLLO / NO CERTIFICADA. **Capítulo C05 NO CERTIFICADO** (diez microfases anteriores aprobadas solamente en sus ámbitos sintéticos); **C06 BLOQUEADO**. **Repositorio exclusivo:** `crecisol-platform/CRECISOL` (GitHub ID `1450414592`). Rama `feature/c05-11-integral-security-20261114` desde `dev` `52efec64f43245771af59459f8db3b38685c1703`; `main` `b794181148d2c5e36c1c88127f19a8a9082acff5` sin cambios. Se prohíbe escribir en `solqaryn/Solqaryn`.

## 1. Autoridad y aceptación vinculante

**Documento Maestro Único** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`: SHA256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, Git blob `fe7aaab60b603f1844e18666e2f99c0ea97e86e9`. PDF derivado: SHA256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`, Git blob `93c8412b2450e85a882b622e7a012477c61c2359`. Comparar bytes reales de Word/PDF en CI, no inferir hashes por citas históricas. Referencias: `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`, `docs/SEGURIDAD_MULTITENANT.md`, `AGENTS.md` y `.agents/skills/crecisol-project-governance/SKILL.md`.

**Criterio exacto de C05-11:** «Auditorías integrales y acta C05 | A/B/C de autentificación, abuso y aceptación | No habilitar C06 sin identidades confiables». Las 11 filas del capítulo deben permanecer íntegras en orden 01..11. No confundir 8 pasos del Word sobre método de ejecución con 11 microfases funcionales. El plan derivado NO sustituye el Word.

## 2. Alcance de las diez microfases precedentes (sin reinterpretar sus certificaciones)

| Microfase | Evidencia técnica a reejecutar | Negativa P0 |
| --- | --- | --- |
| C05-01 | Tipos separados de cuenta/credencial, realm, tenant link; suite AccountIdentity 100 casos | Identidad compartida concede pertenencia entre A/B |
| C05-02 | Registro neutral, invitaciones firmadas, correo ligado, privacidad/retención, MySQL8.4 desechable | Enumeración correo, enlace repetido, beneficiario ajeno, grants arbitrarios |
| C05-03 | Argon2id PHC, revisión/fuente, dummy, borrado de buffers .NET10 | Contraseña/clave persistida en claro o validación débil |
| C05-04 | Login contextual sin selector, host verificado, alias ambiguo y pertenencia fresca | Host/claim/tenant A concede B o crea sesión |
| C05-05 | Sesión sintética HTTP/MySQL, rotación/revocación/CAS | Refresh revocado o robado reutilizable |
| C05-06 | Recuperación sintética HTTP/MySQL, token de un uso y expiración | Reutilización del enlace o bypass de estado |
| C05-07 | Correo y cambios sensibles, confirmación doble, reautenticación sintética | Cambio no confirmado o cuenta ajena |
| C05-08 | TOTP/WebAuthn ES256 y step-up en fixtures, MFA replay, CAS | Recovery/restablecimiento elimina factor de seguridad |
| C05-09 | PII AES256-GCM por tenant/actor y referencia foto privada con MySQL8.4 | Cruce IDOR PII/foto A/B |
| C05-10 | Limiter HMAC-SHA256, fuente+usuario, MySQL CAS, trazas append-only | Brute force concurrente, XFF falso o bitácora con contraseña/token |

**Evidencia del precedente inmediato:** C05-10 [PR institucional #147, dictamen final](https://github.com/crecisol-platform/CRECISOL/pull/147#issuecomment-6885019825) 10/10 CI de remediación/institucional completadas en sus SHAs; `dev` final `52efec64f43245771af59459f8db3b38685c1703`. **Preservar el fallo histórico** de C05-10 POSTMERGE original #145 [#43643788104](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788104): 16 peticiones 5/4/7 (7 HTTP 503); remediado en PR #146 con clock UTC después de bloqueos InnoDB, 6 rondas 5/11/0, 211 comprobaciones reales y 174 guardas adversarias rechazadas. Prohibido reetiquetar una CI histórica fallida como PASS.

## 3. Puertas de auditoría integral

**A — funcional/procedencia:** comparar SHA256 de DOCX/PDF y su XML, inventario de diez actas, diez suites de QA y sus workflows, las once filas del plan, evidencias de GitHub por SHA y contexto C04 certificado. C05-02 exige varios adaptadores; no usar solo una prueba de invitación como evidencia de registro completo. Validar vocabulario comercial pendiente COM-T08/09/10 y roles de SuperAdmin contra realm; las pruebas comerciales NO IMPLEMENTADAS no se declaran aprobadas.

**B — seguridad/privacidad/abuso:** reejecutar negativas históricas A/B de diez microfases y casos de MySQL reales; comprobar ámbito de tenant en host/membresía/sesiones, tokens de recuperación/verificación, MFA y fotos, rotación/replay, antienumeración, no log de secretos, permisos SQL de mínimo privilegio, retención lógica, revocación y HMAC por actor/tenant, 6 rondas de 16 HTTP => 5 admitidos, 11 limitados, 0 indisponibles; revisiones CAS y fail-closed. Matriz `C05_11_MATRIZ_RIESGOS.md` con severidad P0/P1 y todos los controles marcados como PENDIENTE antes de ejecución. Ninguna mutación textual sustituye un ensayo de integración real. Denegar autoelevar membresía por headers/Host/tenant_id.

**C — arquitectura/rendimiento/UX:** Release .NET10 `-warnaserror` de API, contratos C05-01..10 y proyectos históricos C04 relevantes; suites contractuales .NET10; MySQL8.4 local en contenedores por digest con borrado al salir; Angular22/Node24/Chromium públicos, accesibilidad y privacidad de rutas; capturar SHA/run con prueba del mismo workflow, check de diff exacto, regresiones C03/C04 y exposición de backend (solo branding público). NO declarar integración real de login, MFA, recuperación, backoffice ni trazas en la API mientras estén ausentes.

## 4. Riesgos conocidos y NO-GO productivo

- **Bloqueo para identidad de clientes real:** no están conectados en un mismo flujo operativo protegido los directorios, invitación, almacenamiento de credencial, Argon2id, MFA, recuperación, emisión de sesiones, rate limit, privilegios y administración desde Angular. El entorno de CI usa identidades ficticias, Host de catálogo simulado, MySQL descartable y endpoint HTTP solo loopback. No autenticar usuarios reales, no emitir JWT/cookies de clientes ni levantar APIs privadas sobre esta prueba.
- **Regulación e infraestructura pendientes:** transporte HTTPS/reverse proxy de confianza, credenciales/llaves en KMS con rotación y distribución, correo transaccional externo real, passkeys navegador FIDO2, WAF/rate-limit distribuido, auditoría WORM y retención/erasure físico, Cloudflare R2 y escaneo MIME, vault backups/restores/tenant dedicated, anti-CSRF/antifraude y revisión GDPR/ley local; ningún fixture sustituye la aprobación productiva.
- **Aceptación comercial no ensayada:** COM-T08/T09/T10, contratos/consentimiento del titular y autorización SuperAdmin con step-up y motivo son dependencias de activación; registrar explícitamente `NO EJECUTADAS` en lugar de `PASS`.
- **Tiempo SQL y replicación:** `SYSDATE(6)` del fixture C05-10 no es una política apta para MySQL productivo con replicación por sentencias; requerir diseño nuevo y pruebas en la topología real.

**Interpretación de cierre:** una aprobación eventual de C05-11 puede certificar solamente la *coherencia de contratos y pruebas sintéticas del capítulo*, preservando los NO-GO productivos. **C06 no puede activarse en funcionalidades que requieran identidad confiable de usuarios reales mientras esas dependencias estén ausentes.** Si no se demuestra que la secuencia de C05 en el alcance declarado protege datos sintéticos sin P0/P1, **no certificar siquiera ese alcance**. La posibilidad de iniciar preparación documental de C06 no equivale a autorización de desarrollo funcional privado, despliegue o producción.

## 5. Secuencia obligatoria de cierre, sin anticipar éxito

1. Crear PR técnico con especificación, matriz, auditor A/B/C y CI de capítulo **solamente en feature**, contra `dev` exacto `52efec64...`.
2. CI PREMERGE real: A Word/precedente, B mutaciones y regresiones ofensivas; .NET10 Release, Angular22/Chromium, MySQL8.4 con todos los casos, C diff SHA y prueba same-run. Requiere `completed/success` para TODOS los workflows obligatorios del HEAD; fallos P0/P1 se corrigen y se vuelven a ejecutar.
3. Fusionar PR técnico a `dev` mediante squash con `expected_head_sha`, validar CI POSTMERGE, SHA y cambios precisos.
4. Crear PR de ratificación institucional con acta y gate/CI independientes, repetir pruebas A/B/C y regresiones, PREMERGE/POSTMERGE y diff exactos. Conservación de historia fallida.
5. Solo entonces publicar un **dictamen permanente** con lista SHA/run IDs, alcance, no-GO, rollback y aceptación; no afirmar certificación antes de completar cada gate.

**Rollback:** revertir documental y luego técnico, exclusivamente en `dev` con dependencias revisadas; conservar C05-01..10, sus actas, Word/PDF y hallazgos históricos. Nunca escribir en `main`, SOLQARYN, VPS, DNS, Cloudflare, servicios de correo, cuentas de clientes ni cobros. **C05-11 NO CERTIFICADA; capítulo C05 NO CERTIFICADO y C06 BLOQUEADO** hasta el dictamen institucional final.

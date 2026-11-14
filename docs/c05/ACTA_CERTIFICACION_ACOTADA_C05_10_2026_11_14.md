# CRECISOL — C05-10 · Acta institucional CONDICIONADA: rate limit y trazas sin secretos

**Fecha de redacción:** 2026-11-14. **Repositorio único autorizado:** `crecisol-platform/CRECISOL` (GitHub ID 1450414592). **Base documental:** `dev` `8a387d2ebc17a166fb5ed0ae20e116cfbd008276`, después de la remediación #146. **Rama exclusiva:** `feature/c05-10-rate-limit-institutional-final-20261114`.

**ESTADO AL REDACTAR: ACTA NO EFICAZ. C05-10 NO CERTIFICADA; C05-11 BLOQUEADA; C05 9/11 microfases certificadas en su alcance.** El dictamen posterior fijará el resultado tras las CI institucionales PREMERGE/POSTMERGE en los SHAs exactos. Este documento nunca convierte pruebas pendientes en pruebas aprobadas.

## 1. Autoridad, dependencia y contrato

Autoridades verificables: **Documento Maestro Único de CRECISOL**, `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` (SHA256 registrado `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`), PDF de consulta (SHA256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`), `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`, `.agents/skills/crecisol-project-governance/SKILL.md`, `AGENTS.md`, y `docs/c05/C05_10_ESPECIFICACION.md` en su revisión final.

**Requisito exacto C05-10:** «Rate limit y trazas de acceso | Pruebas contra brute-force y filtrado de logs | No registrar tokens o contraseñas». **Negativa P0:** no permitir fuerza bruta sin presupuesto y jamás persistir o emitir contraseñas, tokens, cookies, cabeceras, IP o email en claro en trazas privadas.

**Precedente C05-09:** [PR #144 y dictamen definitivo](https://github.com/crecisol-platform/CRECISOL/pull/144#issuecomment-6884289441), 13/13 CI obligatorias SUCCESS, `dev` `6b7e0d39785bcad00cc059954eaef510f10d84fa` (9/11). Certificación exclusivamente sintética de perfil privado cifrado/foto referencial; no implica cuentas/archivos reales.

## 2. Linaje técnico exacto y errores históricos

**PR original #145**, HEAD `9fc11e1c779c465975856a40ccb7a84bf7653fe0`, base C05-09 `6b7e0d39785bcad00cc059954eaef510f10d84fa`, diff exactamente **11 archivos** (Domain, Application, Infrastructure, SQL, .NET10 HTTP, script de pruebas, QA y especificación). Squash a `dev` `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd`.

**PREMERGE #145 3/3 SUCCESS** en ese HEAD: [C05-10 A/B/C y HTTP MySQL #43643373571](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643373571), [Foundation #43643373529](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643373529), [C03 integral #43643373711](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643373711). 120 comprobaciones genuinas HTTP/.NET10/MySQL8.4 y 163 mutaciones estáticas rechazadas. Primera carrera: 16 HTTP => 5 admitidos/11 limitados/0 no disponibles.

**POSTMERGE ORIGINAL #145: FALLO REAL HISTÓRICO NO OCULTADO.** En squash `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd`, [C05-10 #43643788104](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788104) finalizó `failure`: carrera de 16 peticiones => **5 admitidos, 4 limitados, 7 no disponibles (503)**. Cinco CI de acompañamiento terminaron success: [Foundation #43643788149](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788149), [C03 Domain #43643788138](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788138), [C03 Application #43643788151](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788151), [C03 Infrastructure #43643788122](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788122), [C03 integral #43643788137](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788137). **No afirmar 6/6 ni reutilizar el PREMERGE para certificar un POSTMERGE fallido.**

**Causa remediada:** `UTC_TIMESTAMP(6)` en MySQL se anclaba al comienzo del `CALL` aun cuando una transacción esperaba bloqueos SOURCE/SUBJECT InnoDB; tras adquirir un lock, una fila recién iniciada podía tener `window_started_utc>v_now` y ser rechazada erróneamente. [Documentación MySQL 8.4](https://dev.mysql.com/doc/refman/8.4/en/date-and-time-functions.html) distingue `NOW/UTC_TIMESTAMP` de `SYSDATE`.

**PR correctivo #146**, HEAD `574150fba78d61fb168d71fa87e9933ed4e160ab`, base squash #145 `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd`, diff **exactamente cuatro archivos**: `backend/tests/Crecisol.RateLimitAccess.MySqlTests/Program.cs`, `database/tests/fixtures/c05_10_disposable_rate_limits.sql`, `docs/c05/C05_10_ESPECIFICACION.md`, `qa/c05_10_rate_limit_audits.py`. Fusión a `dev` con nuevo squash `8a387d2ebc17a166fb5ed0ae20e116cfbd008276`.

Se establece la sesión **UTC** y se renueva `v_now=SYSDATE(6)` **después de cada SELECT ... FOR UPDATE** SOURCE y SUBJECT. La auditoría de SHA distingue el diff del primer PR del de remediación. Diagnóstico SQL solo agrega categorías numéricas de errores; no expone SQL completo, emails, contraseñas, IP, HMAC, secretos, cabeceras ni DSN. **SYSDATE no es segura para replicación basada en sentencias sin precauciones** y se permite exclusivamente en el fixture MySQL 8.4 desechable, nunca como migración o política productiva sin revisión nueva.

**PREMERGE #146: 3/3 SUCCESS**, SHA `574150fba78d61fb168d71fa87e9933ed4e160ab`:
- [C05-10 #43644316467](https://github.com/crecisol-platform/CRECISOL/actions/runs/43644316467): **211 comprobaciones HTTP/.NET10/MySQL8.4**, 174 mutaciones negativas de guardas rechazadas, A/B/C, regresiones C05-01..09. Carrera original y cinco adicionales: **6 rondas × 16 = 96 peticiones, cada ronda 5/11/0**, con política y tenant correctos. No hay fuga de PII ni sesiones JWT.
- [Foundation #43644316394](https://github.com/crecisol-platform/CRECISOL/actions/runs/43644316394): `completed/success`.
- [C03 integral #43644316391](https://github.com/crecisol-platform/CRECISOL/actions/runs/43644316391): `completed/success` .NET10, Angular22/Node24/Chromium, MySQL8.4, integridad documental.

**POSTMERGE #146 — todavía condicionado al redactar:** sobre squash `8a387d2ebc17a166fb5ed0ae20e116cfbd008276`, deben finalizar 3/3 `completed/success`: [C05-10 #43645885180](https://github.com/crecisol-platform/CRECISOL/actions/runs/43645885180), [Foundation #43645885132](https://github.com/crecisol-platform/CRECISOL/actions/runs/43645885132), [C03 integral #43645885139](https://github.com/crecisol-platform/CRECISOL/actions/runs/43645885139). Si falla una sola, detener la ratificación, remediar y repetir desde nuevo SHA; no aprobar por resultados de ramas antiguas.

## 3. Auditoría A — requisitos y procedencia

Confirma contrato Word Maestro C05-10/plan, dependencia C05-09 certificada y su dictamen, ausencia de archivos históricos alterados, versiones .NET10/MySQL8.4/Angular22 conforme al capítulo y alcance exacto de 11+4 archivos. No habilita C05-11 hasta ratificación final. Sin configuración de producción, Cloudflare, pagos, VPS, API de login productiva o SOLQARYN.

## 4. Auditoría B — abuso y privacidad, negación por defecto

Criterios realmente ensayados: en `ThrottledContextualPasswordAttempt`, la reserva MySQL antecede a Argon2id auténtico C05-04, incluso para email inexistente/dummy; dos buckets HMAC-SHA256, fuente de red del socket backend no de `X-Forwarded-For`/`X-Tenant-Id`, tenant+propósito+SOURCE/SUBJECT con key de 256 bits aleatoria, IPv6 /64; política versionada y revisiones del host; SQL `FOR SHARE` de política y `FOR UPDATE` sobre ambos buckets; aserción A/B de tenants cruzados; sin enumeración 202/429 ni emisión de sesión. SQL ausente, host falso, fuente indefinida, política desactivada o revisión obsoleta => 503 genérico **antes de Argon2id**. Usuario MySQL EXECUTE-only no lee ni edita auditoría. Traces/outcomes solo HMAC+estado+tenant+UTC+revisión, inmutables por triggers. Criterios negativos: no contraseñas, tokens, cookie, dirección IP, email, cabecera, PHC ni URL en columnas de bitácora; 16 finalizaciones concurrentes => solo una válida; bloqueos no generan logs infinitos.

La carrera de 6 rondas de 16 solicitudes evita la falsa aprobación por un PREMERGE afortunado. Se mantienen los límites **5 admitidos/11 bloqueados/0 indisponibles**, sin silenciar errores. No se declara seguridad absoluta frente a botnets o distribución masiva de IP.

## 5. Auditoría C — arquitectura y regresiones

Domain valida el modelo y digest tipado; Application gobierna admisión previa a Argon2id sin grants; Infrastructure contiene adaptadores HMAC y MySQL. Toda BD/esquema es `database/tests/fixtures/c05_10_disposable_rate_limits.sql`, ejecutada exclusivamente en MySQL8.4 local efímero; la API productiva `backend/src/Crecisol.Api/Program.cs` continúa únicamente con el endpoint **branding público**. El harness HTTP sólo escucha `127.0.0.1`. Se exige compilación estricta, pruebas A/B, 211 comprobaciones reales, 174 mutaciones estáticas negativas, regresiones reales C05-01..09 y C03 integral, controles de diff, SHA, README y rollback; no hay métricas de carga productiva que permitan declarar una escala superior a 16 simultáneas.

## 6. Ratificación documental A/B/C — requisitos antes del dictamen

La ratificación institucional final usa un PR **separado**, creado desde squash remediado `8a387d2ebc17a166fb5ed0ae20e116cfbd008276`, con diff **exactamente tres archivos**:
1. `docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_10_2026_11_14.md` (esta acta).
2. `qa/c05_10_closure_gate.py` (guarda documental/mutaciones/A-B-C y lineage SHA/run).
3. `.github/workflows/c05-10-certification.yml` (compila y ejecuta nuevamente HTTP/MySQL y regresiones).

Se exigen: **2/2 CI PREMERGE institucionales `completed/success`** sobre HEAD correcto, fusión squash exclusivamente en `dev`, **2/2 CI POSTMERGE institucionales `completed/success`** sobre su SHA exacto; no usar un `skipped` como `success`. C05-10 solo puede declararse certificada **en alcance de datos sintéticos** si se verifica además el POSTMERGE técnico final #146 3/3, auditorías A/B/C, diffs exactos, que `main` sigue intacta y **un dictamen institucional definitivo permanente en el PR de ratificación**. La prueba de CI nunca sustituye una auditoría humana de integración o acceso productivo. Los fallos intermedios quedan como historia; ningún error P0/P1 queda permitido en el SHA final de ensayo.

## 7. NO-GO productivo, límites y rollback

**No existe integración de este limiter en los flujos productivos de login, sesiones, recuperación/MFA, ni UI administrativa real; tampoco KMS/rotación de HMAC para múltiples nodos, WAF/DDoS, proxy verificado, retención legal/WORM, análisis de tráfico real, bucket Cloudflare o audit collector productivo.** El backend público actual no expone autenticación privada. Las políticas RECOVERY/MFA del fixture no demuestran protección operativa de esos flujos. La técnica SYSDATE de fixtures no autoriza migración productiva sin diseño de replicación.

Sin despliegues, VPS, DNS, Cloudflare, cuentas reales, cobros ni configuración de producción. No tocar `main`, archivos Word/PDF, `solqaryn/Solqaryn` o otros capítulos. **C05 completo permanece abierto y C06 bloqueado** hasta su auditoría integral C05-11 y aceptación.

Rollback seguro: revertir únicamente squash documental de ratificación y, tras verificar dependencias, squash correctivo `8a387d2ebc17a166fb5ed0ae20e116cfbd008276` y técnico `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd` en `dev`, preservando C05-01..09 y evidencias de fallos históricos.

**DICTAMEN CONDICIONADO:** si TODO lo anterior aprueba, C05-10 **CERTIFICADA SOLO EN SU ALCANCE SINTÉTICO DE ANTIFUERZA BRUTA Y TRAZAS SIN SECRETOS**, capítulo C05 **10/11** en sus alcances, C05-11 habilitada **solo para desarrollo**, C05 completo **no certificado**, C06 **bloqueado**. De lo contrario, C05-10 sigue **pendiente** y C05-11 sigue **bloqueada**.

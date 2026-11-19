# CRECISOL · C05 — Acta técnica de cierre PREMERGE del corte TOTP-SHA1

**Fecha:** 19 de noviembre de 2026, Honduras.  
**Dictamen:** **APROBADO TÉCNICAMENTE EN CÓDIGO, CI Y QA LOCAL AISLADA.** **NO CERTIFICADO EN EL VOLUMEN PERSISTENTE DEL PROPIETARIO. NO FUSIONAR TODAVÍA.**  
**Proyecto:** exclusivamente `crecisol-platform/CRECISOL` (repositorio original ID GitHub `1450414592`).  
**Integración propuesta:** [PR #172](https://github.com/crecisol-platform/CRECISOL/pull/172), rama `feature/c05-totp-sha1-integrated-qa-20261118` → `dev`. PR borrador, sin fusionar al redactar.

## 1. Alcance y trazabilidad

Se examinaron la Skill de gobierno del repositorio, `AGENTS.md`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md`, la especificación `docs/c05/C05_TOTP_SHA1_EXCLUSIVE_CUTOVER.md`, la supervisión persistente y las pruebas de C05. El Documento Maestro Único DOCX/PDF permanece inalterado; `docs/ESTADO_VIGENTE_CAPITULOS.md` conserva **5/37 capítulos certificados solo en los alcances históricos**, con C06 bloqueado.

**Último commit funcional ensayado:** `02922ca1af726345fc7da9697ebd24ad28b2ad97`.  
**Base `dev` para la comprobación:** `b996798c1c1adf8cf08e9798f03d6570de061c0b`.  
**Commit temporal de merge utilizado por Actions:** `82a9806cfc4798b774e143321a9623089513b3d8`; su registro indica literalmente `Merge 02922ca1... into b996798c...`. No representa fusión a `dev` ni `main`.

El diff del PR contiene **39 rutas**, limitadas a C05: `backend/`, `database/`, `frontend/`, `qa/`, workflows de QA y `docs/c05/`. No se modifica código de C01–C04, `main`, SOLQARYN, producción, VPS, Cloudflare, cobros ni configuración remota. Los tres commits de remediación en frontend son `afd3ed9`, `7de09ce` y `02922ca`, con **nueve nuevas pruebas negativas Playwright** sobre el estado previo.

## 2. Evidencia concluyente por SHA

Los trece workflows asociados al último commit funcional ofrecen **9 aplicables completados en SUCCESS, 4 omitidos condicionalmente y 0 fallidos**. Los cuatro `skipped` no se contabilizan como ejecutados ni equivalen a aprobación:

| Control CI | Run | Resultado |
|---|---|---|
| A/B/C SHA1-only, MySQL8.4, migración 017 y no-downgrade | [44466757888](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757888) | SUCCESS |
| Restitución SQL/huellas y negativa ante adulteración | [44466757941](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757941) | SUCCESS |
| A/B/C/D modales, Chromium, TypeScript y accesibilidad | [44466757832](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757832) | SUCCESS |
| Angular → API .NET 10 → MySQL 8.4, trabajos `operational` y `regressions` | [44466757862](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757862) | SUCCESS / SUCCESS |
| Contratos C05-08 de TOTP, WebAuthn ES256 y negativas de 16 competidores | [44466757829](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757829) | SUCCESS |
| MFA real sintético HTTP/MySQL, step-up y recuperación | [44466757866](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757866) | SUCCESS |
| Registro neutral real HTTP/MySQL | [44466757836](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757836) | SUCCESS |
| Regresión integral arquitectónica C03-10 | [44466757813](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757813) | SUCCESS |
| Fundación y fuente | [44466757827](https://github.com/crecisol-platform/CRECISOL/actions/runs/44466757827) | SUCCESS |

De los **logs reales**: integración operacional `PASS C05 operational integrated assertions=306`; `regressions` aprobó los contratos históricos C01–C05, los fixtures MySQL C04/C05 y **73 pruebas Chromium públicas**. La suite visual C05 completó **19 pruebas Chromium PASS**. La migración 017 fue probada sobre MySQL descartable con retiro irreversible de credenciales antiguas, bloqueo de reejecución, y reaprovisionamiento de **3 operadores sintéticos / 30 respaldos de un solo uso**, con replay rechazado. La prueba persistente aislada verificó 17 migraciones, restauración y las huellas de llaves, y rechazó modificaciones de archivos SQL/configuración.

## 3. Cinco auditorías del mantenimiento C05

| Auditoría independiente de alcance | Verificación material | Dictamen técnico |
|---|---|---|
| **A — requisitos y funcionalidad** | RFC6238 TOTP HMAC-SHA1 obligatorio, 20 bytes, 6 dígitos, 30 segundos, solo paso actual/anterior, sin compatibilidad dual TOTP-SHA256; fuente/contratos | **PASS en QA** |
| **B — seguridad y aislamiento** | Login contextual, desafío, sesión, CSRF, secretos efímeros, replay/CSRF/tenant A-B, permisos API/SQL, recuperación excepcional, 9 negativas nuevas de carreras asíncronas | **PASS en QA** |
| **C — arquitectura, persistencia y criptografía** | .NET10, Node24/Angular22, verificador TOTP SHA1, WebAuthn independiente, migración 017 descartable, operación offline y restauración SQL | **PASS en QA aislada** |
| **D — UX y accesibilidad** | Modales de cuenta y operadores, QR local, foco, adaptabilidad, axe, CSS accesible y pruebas Chromium reales | **PASS automatizado; NO significa aprobación estética humana** |
| **E — integración y regresiones** | 306 comprobaciones operativas, regresiones Angular/Chromium y .NET/MySQL de C01–C05, backups y negativas | **PASS en QA local descartable** |

Esto son **cinco puertas técnicas con evidencia propia del mantenimiento C05**, no una afirmación de que todas las 30 auditorías extraordinarias solicitadas (cinco por cada capítulo y cinco generales) se hayan ejecutado nuevamente. Para esa matriz ampliada se conserva el [registro previo del PR](https://github.com/crecisol-platform/CRECISOL/pull/172#issuecomment-7000864259). C01–C04 disponen además del antecedente real del [PR #161](https://github.com/crecisol-platform/CRECISOL/pull/161): cinco auditorías transversales más puerta conjunta en PREMERGE [43939352147](https://github.com/crecisol-platform/CRECISOL/actions/runs/43939352147) y POSTMERGE [43941492510](https://github.com/crecisol-platform/CRECISOL/actions/runs/43941492510), todas SUCCESS. No retroatribuir esos trabajos a una certificación nueva de 30.

## 4. Riesgos y condiciones fuera de QA

- **BKP-01 · Claves externas persistentes: BLOQUEANTE para corte real.** `backup` y `verify-backup` prueban integridad/restauración SQL aislada y huellas de archivos disponibles, **pero el manifiesto no crea una copia independiente de las llaves reales**. Se necesita copia cifrada externa de `runtime.env`, `mysql-root.password`, `access.md`, `mailbox.password` y material privado de cuentas de tenant; restauración conjunta SQL+llaves en otro entorno C05, sin publicar secretos.
- **MFA-02 · Interoperabilidad física: NO EJECUTADA.** La validez algorítmica TOTP y QR sintéticos no prueba aún enrolamiento, confirmación ni recuperación en Google/Microsoft Authenticator en teléfonos reales del propietario.
- **UX-03 · Juicio visual/tecnologías de asistencia: NO EJECUTADO HUMANAMENTE.** Chromium/axe no sustituyen lectura visual de capturas, lector de pantalla físico ni aprobación estética. No afirmar calidad comercial final por mera CI.
- **GOV-04 · Protección de ramas: observación P2.** Las lecturas de GitHub anteriores indican `protected:false` en `dev` y `main`; no se modificaron repositorio ni controles. La revisión por PR no sustituye un ruleset activado.
- **SCOPE-05 · Exclusiones:** C06, producción, servicios externos, pruebas de SMTP/KMS reales, pagos, datos de cliente, despliegue, pruebas de carga/pentest independiente. Las actas históricas C01–C05 siguen vigentes **solo según su alcance original**.

## 5. Decisión de gobierno

1. **CERTIFICACIÓN TÉCNICA PREMERGE:** favorable en código/QA aislada del commit funcional `02922ca1...` y su merge temporal registrado. Cero jobs aplicables fallidos; ninguna regresión observada en las suites efectivamente ejecutadas.
2. **C05 cambio MFA persistente:** **NO CERTIFICADO NI AUTORIZADO** hasta que existan respaldos recuperables de SQL+llaves, pruebas de acceso/revocación/recuperación con el equipo del propietario, y revisión visual humana cuando se pretenda calificar la interfaz final.
3. **PR #172:** mantener como borrador, sin fusionar con `dev`, sin tocar `main`, sin aplicar la migración irreversible `017` al volumen existente. Ninguna aprobación de este documento sustituye las condiciones anteriores.
4. **C01–C05:** se conservan las certificaciones históricas de desarrollo aislado en su alcance, sin proclamar 30 auditorías nuevas completadas. **C06 BLOQUEADO**.
5. **Reversión:** los commits de la rama pueden revertirse sin DDL en `dev`; la migración 017 **no devuelve semillas destruidas**. No probar reversión histórica sobre volumen persistente.

Acta preparada con resultados observados directamente de GitHub, sin simular ejecución manual en teléfonos, PC del propietario, VPS o producción.

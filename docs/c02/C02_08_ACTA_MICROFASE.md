# CRECISOL — C02-08 · Acta de cierre técnico de recorridos y contratos de experiencia

**ESTADO: C02-08 CERRADA TÉCNICAMENTE (8/9 microfases C02) exclusivamente cuando esta acta esté fusionada en `dev` y sus CI POSTMERGE se comprueben sobre el SHA de esa fusión.** La implementación y sus pruebas de PR #44 ya concluyeron correctamente; este documento registra el alcance real. **C02 NO CERTIFICADO; C01 sigue siendo el único capítulo certificado (1/37), C03 bloqueado**. No se declara cumplimiento WCAG manual ni autorización multitenant privada en módulos inexistentes. **C02-09** auditoría integral del capítulo queda siguiente habilitable después del cierre técnico comprobado; no iniciada.

## 1. Autoridad, secuencia y decisión

- Repositorio único autorizado: `solqaryn-platform/CRECISOL`, rama `dev`; PR técnico `feature/c02-08-journeys-contracts`, PR documental `feature/c02-08-acta-cierre`. El propietario autorizó expresamente continuar la siguiente microfase de C02, no certificar el capítulo completo ni comenzar C03.
- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`: Git blob SHA-1 `214bf9c3d9763be23a77b7e0c464359584edde46`; SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, autoridad registrada en [MASTER_REFERENCE](../MASTER_REFERENCE.md). Verificación byte-real por `qa/c02_01_source.py` en GitHub CI, no se reemplaza el Word. Su PDF de consulta conserva el blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`.
- Puerta previa: [C02-07 acta](C02_07_ACTA_MICROFASE.md), PR #43, `dev` `10c14166a333cc0509c06eedaaad420b409d86c9` con **10/10 CI POSTMERGE SUCCESS** incluida [C02-07 #42661826784](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42661826784). C02-01..07 cerradas técnicamente antes de iniciar C02-08.
- La secuencia normativa del Word `MAE-C02-01..08` define puertas de procedimiento y **no equivale** a los nueve microentregables técnicos `C02-01..09`. C02-08 satisface un alcance de recorridos y contratos, no el proceso integral de certificación del capítulo 02.

## 2. Entregables e impacto controlado

**PR técnico [#44](https://github.com/solqaryn-platform/CRECISOL/pull/44), merge `dev` exacto `be37e00232e90617181d40f25cbe6b1e1db922e9`.** Último HEAD técnico de feature `e0c25c1cc01292e4a8c47d9ea0036a6e9b9ea031`.

- [Especificación C02-08](C02_08_ESPECIFICACION.md) y [contrato JSON](C02_08_RECORRIDOS_CONTRATOS.json): **J01–J06/E01–E06, N01–N18**, 18 negativos ligados a necesidad y ocho transversales **NEG-X01–NEG-X08**. Cuatro rutas públicas allowlisted y **12 rutas privadas futuras que se mantienen 404**, no permisos. Cinco estados universales, solo carga/error/offline públicos activos; vacío/confirmación en harness sintético no productivo.
- `frontend/tests/c02-08.journeys-public.spec.ts`: **11 Playwright Chromium** sobre Angular real: visitante público → experiencias → demostraciones informativas → alta informativa, teclado, móvil, carga, HTTP error/reintento fresco, offline, query tenant/role falsificado, payload privado fail-closed, 12 rutas privadas 404, accesibilidad automatizada axe.
- `frontend/tests/c02-08.journeys-patterns.spec.ts` y `frontend/playwright.journeys-patterns.config.ts`: **5 Playwright Chromium** sobre componentes standalone Angular reales de formulario/tabla en build de `pattern-harness`, aislado en puerto 4201. Borrador, confirmación nativa, Escape/cancelación, scope sin datos obsoletos, filtro/ordenación, XSS/schemas inválidos y axe automatizado.
- `qa/c02_08_audits.py`: auditorías A/B/C reproducibles, **18 mutaciones contractuales negativas rechazadas (6 funcionales + 8 seguridad + 4 arquitectura)**. `.github/workflows/c02-08-journey-contracts.yml`: Word original, regresiones A/B/C previas, Node 24.15, Angular 22 compilación producción y harness, npm audit, **58 pruebas Chromium**, .NET 10 Release, revisión del bundle y ausencia de harness productivo.
- Documentación de progreso: `docs/C02_PREPARACION.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C02_MICROFASES_PROPUESTAS.md` mantienen **7/9 mientras el trabajo técnico no tenga acta postmerge aprobada**.
- **Impacto arquitectónico:** únicamente `docs/`, `frontend/tests/`, runner CI, `qa/` y `.github/workflows/`; **0 cambios en `frontend/src/`, `backend/`, `database/`**, DTO, rutas privadas, migraciones, cookies, permisos, endpoints de negocio, sectores, dominio ni deploy. Producción, `main` y `solqaryn/Solqaryn` intactos.

## 3. Evidencias reales comprobadas (se distinguen los dos merges)

**PREMERGE código PR #44, último HEAD `e0c25c1cc01292e4a8c47d9ea0036a6e9b9ea031`:**
- [C02-08 #42665166423](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665166423) **SUCCESS**: logs con 19/19 + 8/8 + 10/10 + 5/5 regresiones previas y 11/11 + 5/5 nuevos = **58/58 Chromium**; A/B/C PASS, 18 mutaciones, npm audit `found 0 vulnerabilities`, compilación Angular22 (producción y harness), .NET10 Release `Build succeeded`, `0 Error(s)`. Se verificó que el harness no se integra en bundle público.
- [Foundation #42665166494](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665166494) **SUCCESS**. Los otros workflows de PR con `skipped` **no** son éxitos ejecutados.

**POSTMERGE del código PR #44:** API de GitHub Actions consultada para `head_sha=be37e00232e90617181d40f25cbe6b1e1db922e9` con `event=push`, `status=completed`, `conclusion=success`; **11/11 workflows SUCCESS**:

| Workflows | ID postmerge verificado |
| --- | --- |
| **C02-08 recorridos** | [#42665864164](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864164) |
| C02-07 accesibilidad | [#42665864050](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864050) |
| C02-06 formulario y tabla | [#42665864191](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864191) |
| C02-05 estados | [#42665864108](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864108) |
| C02-04 navegación privada de diseño | [#42665864056](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864056) |
| C02-03 navegación pública | [#42665864314](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864314) |
| C02-02 necesidades | [#42665864092](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864092) |
| C02-01 fuente canónica | [#42665864174](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864174) |
| C01-07 integración stack real | [#42665864347](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864347) |
| C01-08 gate integral | [#42665864147](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864147) |
| Foundation | [#42665864125](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42665864125) |

**CI de este PR documental:** aún **no** se ha ejecutado al momento de crear esta acta; debe verificarse en su propio último HEAD y, después de fusionar a `dev`, nuevamente mediante sus CI POSTMERGE con SHA exacto. Los 11 éxitos anteriores acreditan el **código** y no se atribuyen falsamente al acta.

## 4. Auditorías A/B/C, seguridades y límites

| Auditoría | Alcance realmente verificado | Resultado |
| --- | --- | --- |
| **A — trazabilidad** | Word por hash, seis recorridos, 18 necesidades y criterios originales, 26 casos negativos documentados, seis mutaciones de fuente rechazadas | **PASS del alcance C02-08** |
| **B — seguridad/tenancy de interfaces** | Cuatro rutas públicas permitidas, 12 privadas 404, API de marca sin identidad/tenant, errores sin datos privados, no activar roles/logins/cobros, ocho mutaciones P0 rechazadas | **PASS del contrato y superficies públicas, NO de backend privado futuro** |
| **C — arquitectura/rendimiento/UX** | 58 Chromium reales, entrada de producción Angular separada del harness, responsive 320px, teclado/confirmación/safe HTML, axe automatizado, .NET10 Release, cuatro mutaciones rechazadas | **PASS del alcance técnico C02-08** |

**P0/P1 conocidos del alcance limitado:** ninguno abierto tras el HEAD final y los 11 workflows postmerge exitosos, sin inferir seguridad global del SaaS. Limitaciones explícitas: no existe backend privado para validar BOLA/IDOR, tenants, recursos, MFA, CSRF, claims, consentimientos, DB aislada, archivos/colas/backups o pagos; el 404 frontend **nunca sustituye autorización backend**. Pruebas públicas usan una API de marca simulada por Playwright contra Angular real; eso no valida un endpoint de negocio privado. Tampoco se ejecutaron lectores de pantalla reales (NVDA/VoiceOver/TalkBack/Jaws), móviles físicos, Lighthouse ni auditoría WCAG manual completa. No proclamar **WCAG certificado**.

**Parametrización integral obligatoria pero futura:** los textos, logo, tema, pie, cabecera, formularios/tablas, locales y variantes deben administrarse por `platform/tenant/branch/user`, con versiones, previsualización, aprobación, auditoría y rollback; este cierre **no** implementa el panel. En todo caso permisos, tenancy, propiedad, PII, DSN y rutas de datos siguen siendo invariantes backend, nunca código ejecutable HTML/JS/CSS/SQL desde configuraciones.

## 5. Decisión, gates y rollback

1. Revisar que el PR de acta solo altere `docs/c02/C02_08_ACTA_MICROFASE.md`, `docs/C02_PREPARACION.md`, `docs/PLAN_37_CAPITULOS.md` y la planificación provisional C02. Word/PDF y código intactos; revisar diff, enlaces, secretos, permisos, riesgo y reversión.
2. Verificar CI real de **último HEAD del PR documental**, fusionar a `dev` con `expected_head_sha`, revisar la totalidad de CI POSTMERGE del **nuevo SHA**; sin estas puertas no afirmar cierre final.
3. Tras cumplirlo: **C02-08 CERRADA TÉCNICAMENTE (8/9)**, **C02-09** auditoría integral y acta de capítulo siguiente habilitable pero **no iniciada**; C02 **NO CERTIFICADO**, C03 bloqueado, solo C01 certificado (1/37).
4. **Revert/rollback:** revert por PR del código PR #44 y del PR de acta en `dev`; sin despliegues, datos privados, migraciones, producción, VPS, cobros, `main` ni modificaciones a SOLQARYN.

**Este es un cierre técnico de microfase, no una certificación de capítulo ni una aceptación humana anticipada de C02.**

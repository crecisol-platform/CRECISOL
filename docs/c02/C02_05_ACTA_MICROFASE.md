# CRECISOL — C02-05 · Acta técnica de estados universales de interacción

**ESTADO: C02-05 CERRADA TÉCNICAMENTE (5/9 microfases C02), condicionada a la CI e integración posterior de esta acta.** No es certificación del capítulo 02 ni del software privado futuro. **Capítulo C02 NO CERTIFICADO; solamente C01 CERTIFICADO (1/37), C03 BLOQUEADO.** C02-06 (formularios/tablas UX) será la siguiente microfase habilitable, **NO iniciada**.

## 1. Autoridad y dependencias anteriores

- Repositorio autorizado **exclusivamente** `solqaryn-platform/CRECISOL` en `dev` mediante ramas `feature/c02-05-*`; sin escritura a SOLQARYN, `main`, VPS, DNS, producción ni servicios de cobro.
- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Capítulo 02 verificado por el lector real del Word: 11 párrafos, cinco personas más tabla y seis experiencias, N01–N18 de C02-02. Las ocho puertas normativas `MAE-C02-01..08` NO sustituyen las nueve microfases operativas `C02-01..09`.
- Dependencias [C02-01](C02_01_ACTA_MICROFASE.md), [C02-02](C02_02_ACTA_MICROFASE.md), [C02-03](C02_03_ACTA_MICROFASE.md) y [C02-04](C02_04_ACTA_MICROFASE.md) cerradas técnicamente (4/9 antes de esta entrega). Puerta anterior: PR #36/#37; `dev` SHA `a57dcb2ae7fb533f10beae8afb59656b301318d3` y **siete CI POSTMERGE SUCCESS**, [C02-04 #42631972825](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42631972825), [C01-07 #42631972685](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42631972685), [C01-08 #42631973073](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42631973073).
- Instrucción del propietario: «ADELANTE CONTINUA POR FAVOR» y después «CONTINUA POR FAVOR», dentro de CRECISOL y bajo las mismas reglas y secuencia. Autoriza esta microfase y su cierre técnico, **no** la aceptación/certificación final de C02.

## 2. Entregables, impacto y alcance exacto

**PR técnico [#38](https://github.com/solqaryn-platform/CRECISOL/pull/38)** ya fusionado a `dev` en commit **`24e3b3c73133a35bdb336269517f123765734e86`**.

- [Especificación de los cinco estados](C02_05_ESPECIFICACION.md) y [contrato JSON trazado](C02_05_ESTADOS_INTERACCION.json): `ST01 loading`, `ST02 empty`, `ST03 error`, `ST04 offline`, `ST05 confirmation`; seis experiencias E01–E06, necesidades N01–N18 y **12 casos adversariales NEG01–NEG12**. Se conservan contexto, seguridad, transición, disparadores autorizados y parametrización administrativa **futura**.
- `frontend/src/app/ui/interaction-state.model.ts` + `interaction-state.component.{ts,html,css}`: componente standalone OnPush con `input.required`, `output` de intención de reintento, interpolación segura, foco, 320px, `prefers-reduced-motion`, avisos al lector de pantalla. El componente **NO** contiene servicio, HTTP, decisión de acceso, escritura, selector de tenant ni autenticación.
- `frontend/src/app/pages/public-information.component.{ts,html}`: integra realmente el componente únicamente en la consulta pública publicada de C01; **loading/error/offline** activos, `role=status/alert`, botón de reintento y comprobación de estado de red como **pista visual sin autoridad**, con limpieza de los escuchadores de eventos. Conserva el servicio existing `PublicBrandService`, la API same-origin fija, el parseo fail-closed, metas y favicon descartados al fallar; no usa datos/cache anteriores.
- Los estados **empty y confirmation se definen**, pero NO se hacen operativos ni se simula una API privada. Empty solo tras consulta vacía y autorizada real, no HTTP 403. Confirmation únicamente después de ACK backend versionado/comprobante permitido real; **ninguna transacción o cobro se realizó**.
- `qa/c02_05_audits.py`: auditorías independientes **A/B/C** con **18 mutaciones negativas rechazadas** (5 de fuente/estado, 9 de seguridad, 4 de arquitectura/parametrización), además de regresiones de los gates C02-01..04.
- `frontend/tests/c02-05.interaction-states.spec.ts`: siete Chromium nuevos (loading/ready, error/retry, offline/online, inyección de tenant, metadatos limpios, single-flight, móvil/axe, 404), sumados a ocho de C02-03 y cuatro de C02-04 = **19/19 Playwright**.
- Workflow `.github/workflows/c02-05-interaction-states.yml` read-only: master Word, Python/JSON, A/B/C, dependencias, Node 24/Angular22 compilación, navegador Chromium real, .NET10 Release y evidencia de fallos.

**Por capa:** frontend añade exclusivamente presentación pública con diseño reusable; **ningún cambio en `frontend/src/app/app.routes.ts` ni rutas privadas**. Backend ASP.NET10 **sin cambios de endpoints, DTOs, auth o permisos**. Database MySQL **sin migraciones ni cambios de tenant**. Documentación/QA/workflows dentro de alcance. Sin dependencias nuevas, credenciales, datos reales, producción o SOLQARYN.

## 3. Evidencia real premerge y postmerge del código

**CI del último HEAD técnico `bdc831e0db67df3919ec69adb5c8edb574498b8e`, evento PR:**

- [C02-05 #42639291813](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639291813): **SUCCESS** (auditorías A/B/C PASS, **19/19 Chromium PASS en 42.1 s**, siete nuevos estados + regresiones, build/typecheck/assets Angular 22, compilación ASP.NET10 Release, auditoría dependencias, histórico C02).
- [Foundation #42639291574](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639291574): **SUCCESS** (alcance, secretos, integridad).
- **No se ocultan dos fallos intermedios:** [#42638996147](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42638996147) por resultados negativos demasiado abreviados, resuelto explicitando 403≠vacío y no-confirmación-sin-ACK; [#42639144290](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639144290) por validador A que aceptaba un disparador falseado, corregido exigiendo precondiciones exactas por estado. Las mutaciones adversariales se mantuvieron y la CI final quedó verde.

**Seis GitHub Actions POSTMERGE del commit técnico `24e3b3c73133a35bdb336269517f123765734e86`:** se verificaron desde GitHub con `head_sha` exacto, `event=push`, `status=completed` y `conclusion=success`.

| Workflow | Evidencia y resultado |
| --- | --- |
| [C02-05 #42639836939](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639836939) | **SUCCESS** — 19/19 Chromium, 5 estados, A/B/C, Angular22/.NET10 |
| [C02-04 #42639836403](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639836403) | **SUCCESS** — diseño de navegación privada, sus 12 rutas todavía 404, QA |
| [C02-03 #42639836651](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639836651) | **SUCCESS** — rutas públicas, seguridad y WCAG |
| [C02-02 #42639836641](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639836641) | **SUCCESS** — necesidades y auditorías anteriores |
| [C02-01 #42639836721](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639836721) | **SUCCESS** — personas, Word, auditoría de fuente |
| [Foundation #42639836413](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42639836413) | **SUCCESS** — scope e integridad |

**Nota:** los workflows integrales C01-07/C01-08 se ejecutaron y aprobaron en la puerta precedente C02-04; en el código técnico C02-05 los paths no activaron esos dos workflows. Su regresión se comprobará también al integrar el presente PR de acta/plan si GitHub los dispara; **no afirmar un paso no ejecutado**.

## 4. Tres auditorías, límites y riesgos

| Auditoría | Evidencia real | Resultado |
| --- | --- | --- |
| **A — funcional y trazabilidad** | Master Word SHA, 6 experiencias, N01–N18, 5 estados ST01–ST05, 9 transiciones, 5 mutaciones negativas de fuente/ID/precondición; CI de pruebas positivas | **PASS** |
| **B — seguridad** | Único GET público validado, limpieza en error/timeout/tenant JSON inyectado, offline no autoriza, 9 mutaciones de cache/privilegios/403/confirmación; Chromium 404 sin rutas privadas | **PASS de los límites públicos implementados** |
| **C — arquitectura y UX** | Angular22, design pattern standalone, interfaz de reintento, foco/teclado, 320px y WCAG 2.2 AA automatizado, .NET10 compilado, cuatro mutaciones de configuración/DB no adelantadas | **PASS del alcance C02-05** |

**Pruebas NO ejecutadas ni certificadas:** autorización privada en API, MFA real, BOLA/IDOR cross-tenant sobre tablas/archivos/colas privados, revocación de empleados, confirmar escrituras/ventas/cobros reales, parametrización administrativa en runtime, rendimiento bajo carga de operaciones privadas y compatibilidad MySQL private tenant. Estos controles se ejecutarán en las microfases y capítulos que desarrollen tales superficies (C02-06..09 y C04–C31). `navigator.onLine` no valida permisos ni estado real de backend.

**P0/P1 en alcance de C02-05:** ninguna alerta de CI o auditoría final pendiente. Esto NO equivale a ausencia de vulnerabilidades en módulos futuros. No se afirma certificación del capítulo 02 ni apertura de C03.

## 5. Cierre documental, siguiente puerta y rollback

- **Decisión técnica:** C02-05 **CERRADA TÉCNICAMENTE (5/9)** tras PR #38 e integración/CI del presente PR de acta. Este cierre certifica únicamente **la evidencia técnica de la microfase**, no el capítulo C02.
- Se preserva la secuencia: **C02-06 formularios y tablas UX** quedará **habilitable**, NO iniciado aquí. C02 capítulo **NO CERTIFICADO**; total capítulos certificados **1/37**; C03 bloqueado.
- **Reversión:** revert del PR técnico #38 y del PR de esta acta en `dev`, conservando la API C01 y la navegación C02-03/04. Sin rollback DB porque no existe migración; `main` y producción intactos.
- El PR de acta debe ejecutar regresiones de todos los gates C02-01..05, CI del HEAD final, merge con SHA esperado, CI POSTMERGE, revisión diff/secretos y comentario final. Hasta completarlo, no dar por cerrada **formalmente** la quinta microfase.

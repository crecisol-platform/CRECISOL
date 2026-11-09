# CRECISOL — C02-03 · Acta de cierre técnico de arquitectura pública

**ESTADO: C02-03 CERRADA TÉCNICAMENTE, 3/9 microfases del capítulo 02.** El código técnico quedó fusionado en `dev` y su **CI POSTMERGE** fue verificada contra el SHA exacto. Esta acta sigue su propia puerta documental: PR de cierre, CI del HEAD, merge a `dev`, CI posterior y comentario de trazabilidad. **C02 capítulo NO CERTIFICADO; solo C01 certificado (1/37). C03 BLOQUEADO.**

## 1. Autoridad, dependencia y consentimiento de avance

- Único repositorio afectado: `solqaryn-platform/CRECISOL`; `dev` para integración, `feature/c02-03-*` para cambio. Sin escrituras en `main`, `solqaryn/Solqaryn`, VPS, DNS, cobros o recursos productivos.
- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Fuente C02 verificada con extractor real: 11 párrafos originales y seis filas de persona/contexto; P01/P03/P04/P05/P06/P11, T01-R01/R06; precedencia sin selector de empresa en login y separación de seis experiencias.
- C01 certificado mediante PR #29 y auditorías integrales. C02-01 y C02-02 cerradas técnicamente en PR #31/#33. Puerta anterior confirmada: SHA `8628f9bd9ff15b56e53c8964a98d1e51a936222b` y cinco workflows postmerge SUCCESS.
- Autorización de usuario del 9 noviembre de 2026: «Ok, listo, adelante, continúa con la siguiente microfase». Autoriza la ejecución y cierre técnico de C02-03 según gates. **No es aprobación integral ni certificación de C02.**

## 2. Entrega de aplicación, rutas y fronteras

**PR técnico [#34](https://github.com/solqaryn-platform/CRECISOL/pull/34)**, fusionado a `dev` en commit `6ccd98960f36dce3407392fd26406366b4cd0650`.

- **Cuatro rutas públicas explícitas:** `/` (identidad publicada C01), `/experiencias`, `/demostraciones`, `/alta-empresas` (estas últimas tres informativas y cargadas lazy). El índice describe seis experiencias, pero **no crea** consola global, panel de negocio, puesto empleado, portal privado, ingreso o contratación operativa.
- **Solo marca pública de plataforma:** nombre, color, título, favicon y copyright se muestran tras leer y validar `GET /api/v1/public/branding/platform`. Frente a API fallida/JSON con propiedades privadas, los datos se ocultan, se limpian metas/favicons gestionados y se ofrece recuperación sin estado de tenant guardado.
- **Rutas denegadas:** `/admin`, `/superadmin`, `/panel`, `/cliente`, `/empleado`, `/login`, rutas de tenant y cualquier ruta no admitida conservan `NotFoundComponent`. Un parámetro URL/slug/host/claim no autoriza datos privados; no se añadió selector de empresa.
- **UI/UX:** foco visible, skip link probado por teclado, `aria-current`, headings, mobile 320px sin desbordamiento, preferencia de movimiento reducido y comprobación automatizada WCAG 2.2 AA en nuevos recorridos. Sin ejecutar HTML/JS suministrado por cliente y sin enlazar operadores privados.
- Especificación [C02-03](C02_03_ESPECIFICACION.md), sitemap JSON [C02-03](C02_03_SITEMAP.json), tres auditorías Python `qa/c02_03_audits.py`, ocho Playwright `frontend/tests/c02-03.public-information.spec.ts`, workflow read-only `.github/workflows/c02-03-public-ia.yml`.

## 3. Evidencia real premerge y postmerge del código

**CI último HEAD técnico `2e78aa7a37c5d2e6d8a5ea3f1c2fe2fbe9bc7e4c`:**

- [C02-03 #42623979586](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42623979586): **SUCCESS** (A/B/C PASS, **8/8 Chromium**, Angular22 production build/typecheck/assets, .NET10 Release, dependencia auditada).
- [Foundation #42623979591](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42623979591): **SUCCESS**.

**POSTMERGE exacto del SHA `6ccd98960f36dce3407392fd26406366b4cd0650`, evento `push`, todos `completed/success`:**

| Ejecución | Evidencia real |
| --- | --- |
| [C02-03 #42624454291](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42624454291) | A/B/C, 8/8 Playwright Chromium, build Angular22, SDK .NET10: **SUCCESS** |
| [C02-02 #42624454313](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42624454313) | Regresión de necesidades/contratos: **SUCCESS** |
| [C02-01 #42624454480](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42624454480) | Trazabilidad, personas y seguridad documental: **SUCCESS** |
| [C01-06 #42624454179](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42624454179) | Logo aprobado, browser, accesibilidad, negativos: **SUCCESS** |
| [C01-08 #42624454085](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42624454085) | Regresión integral C01 Angular/.NET/MySQL 8.4/Chromium/A-B-C: **SUCCESS** |
| [Foundation #42624454342](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42624454342) | Scope/secrets/documento maestro: **SUCCESS** |

**Historial de fallos no oculto:** se corrigió el parser del path vacío raíz en auditoría A [#42623532274](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42623532274), se explicitó P04 en matriz [#42623604652](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42623604652) y se esperó correctamente navegación de skip link en prueba Chromium [#42623711069](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42623711069). **La última CI completa pasó sin deshabilitar controles.** Todos los errores y sus correcciones constan en PR #34.

## 4. Triple auditoría y límites de seguridad

| Auditoría | Alcance real | Resultado |
| --- | --- | --- |
| **A — funcional/trazabilidad** | Word SHA, estructura C02 y C02-02, cuatro rutas/3 lazy + wildcard, fuente, sitemap; 4 mutaciones negativas de ruta faltante/privada/referencia inventada/ID duplicado | PASS |
| **B — seguridad** | Solo endpoint de publicación de plataforma, fail-closed, no ruta/credencial privada, no selector ni datos demo reales; 5 mutaciones negativas de endpoint, alta/cobro falso, selector, demo y ámbito diferido | PASS del límite público implementado |
| **C — arquitectura/rendimiento/UX** | Angular lazy + build y presupuestos, .NET compilación, estilo responsive y metadata noindex, 320px, teclado, WCAG 2.2 AA automatizado, 2 mutaciones de SEO/tenant | PASS del alcance C02-03 |

**Pruebas futuras NO EJECUTADAS aquí:** autorización de rutas privadas, JWT/MFA/passkeys, BOLA/IDOR sobre paneles todavía inexistentes, dominios tenant reales y wildcard host, bases dedicated/shared privadas, altas/contratos reales, cobros y pruebas operativas de carga del producto. Todas quedan obligatorias cuando existan las superficies en C02-04/C04–C31; las verificaciones del sitemap no sustituyen seguridad backend. Sin afirmar ausencia absoluta de vulnerabilidades.

## 5. Impacto, reversión y siguiente microfase

- `frontend/`: tres rutas lazy informativas y componente responsive; enlace en footer de inicio y ocho Playwright con mocks controlados. No introducen autoridad privada, formularios de alta ni datos tenant.
- `backend/`: sin modificación de API/DTO/autorización. El snapshot de publicación de C01 se mantiene; su integración real con MySQL fue revisada por la regresión C01-08.
- `database/`: ninguna migración, datos ni cambio de ubicación shared/dedicated.
- `docs/`, `qa/`, workflows: especificación, sitemap, A/B/C y esta acta; sin dependencias externas nuevas, secretos, producción o SOLQARYN.
- **Rollback:** revert PR #34 y su PR de acta en `dev` bajo revisión. El cambio no requiere rollback de datos; `main` no se tocó.
- **Decisión:** C02-03 **CERRADA TÉCNICAMENTE (3/9)** con código real de lectura pública, 8/8 navegadores, A/B/C y CI postmerge. Este cierre documental se hará efectivo al comprobar también su PR de acta. **C02-04** arquitectura de navegación privada **solo queda habilitable para su siguiente ciclo**, sin conceder permisos ni implementarse aquí. **C02 capítulo NO CERTIFICADO (1/37 total); C03 bloqueado.**

# CRECISOL · C02-08 — Recorridos y contratos de experiencia con pruebas reales

**ESTADO: EN DESARROLLO.** C02-01..07 cerradas técnicamente (**7/9**). C02-08 NO cerrada, C02-09 no iniciada, capítulo **C02 NO CERTIFICADO (1/37: solo C01)**, C03 bloqueado. Las 8 puertas normativas `MAE-C02-01..08` del Word no son los 9 entregables `C02-01..09`. Esta especificación NO autoriza abrir rutas privadas ni entornos comerciales.

## 1. Entrada y autoridad de la microfase

- Repositorio autorizado exclusivamente `solqaryn-platform/CRECISOL`. Base técnica `dev` `10c14166a333cc0509c06eedaaad420b409d86c9` tras PR #43 (acta C02-07). Ver [acta C02-07](C02_07_ACTA_MICROFASE.md); sus **10 GitHub Actions POSTMERGE SUCCESS** sobre el SHA de cierre incluyen [C02-07 #42661826784](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42661826784), [C01-07 #42661826763](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42661826763), [C01-08 #42661826836](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42661826836), [C02-06 #42661826751](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42661826751).
- Maestro único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Ver [referencia](../MASTER_REFERENCE.md) y extracción byte-real en `qa/c02_01_source.py`.
- Fuentes vinculantes ya implementadas por contrato: [necesidades](C02_02_MAPA_NECESIDADES.json), [rutas públicas](C02_03_SITEMAP.json), [navegación privada de diseño](C02_04_NAVEGACION_PRIVADA.json), [estados](C02_05_ESTADOS_INTERACCION.json), [patrones aislados](C02_06_PATRONES_FORM_TABLA.json) y [base móvil](C02_07_MOBILE_ACCESIBILIDAD.json).
- Nuevo contrato íntegro [C02_08_RECORRIDOS_CONTRATOS.json](C02_08_RECORRIDOS_CONTRATOS.json): seis recorridos J01–J06, seis experiencias E01–E06, 18 necesidades N01–N18, 12 rutas privadas propuestas que **se rechazan**, 18 negativas por necesidad y ocho negativas transversales. No importa ni reescribe el maestro.

## 2. Matriz de recorridos y estados autorizados

| Recorrido | Persona/objetivo | Evidencia ejecutable ahora | Prohibición que prevalece |
| --- | --- | --- | --- |
| J01 · E01 visitante | Inicio `/` → explorar `/experiencias` → `/demostraciones` | Angular real, navegación, secciones, estado público y accesibilidad | Sin demo real, cuentas, PII ni permisos |
| J02 · E02 prospecto | `/alta-empresas` informativa y vuelta al inicio | Página informativa, no formularios comerciales, CTA legible | Sin alta, aceptación contractual efectiva, tenant asignado ni cobros |
| J03 · E03 SuperAdmin | `/consola/*` propuesto | 404 para rutas proyectadas, no GET pública en privada | Sin acceso PII; excepciones futuras MFA, motivo, vencimiento, auditoría |
| J04 · E04 propietario | `/negocio/*` propuesto | 404, sin acceso horizontal, navegación no autoriza | Sin edición real, asignación de negocio ni publicaciones de tenant |
| J05 · E05 empleado | `/trabajo/*` propuesto | 404, ninguna tarea recuperada del cliente | Sin sucursal/asignación backend verificada |
| J06 · E06 consumidor | `/mi-cuenta/*` propuesto | 404, no historial privado | Sin acceso a expedientes, adjuntos o pagos |

- Estados universales registrados: `loading`, `empty`, `error`, `offline`, `confirmation`; en la web pública solo se observan `loading/error/offline` cuando corresponda; `empty/confirmation` se prueban como patrones **sintéticos**. Ningún fallo vuelve a mostrar marca obsoleta, metadatos privados ni éxito falso.
- Navegación pública allowlisted exacta: `/`, `/experiencias`, `/demostraciones`, `/alta-empresas`; todo lo demás es `**` 404. Los títulos, tarjetas informativas, enlaces, tamaños móviles y CTA se comprueban contra Angular real, no contra un documento estático.
- Marca, copyright y tema vienen solo de `GET /api/v1/public/branding/platform` versionada, saneada, `same-origin`, sin `tenant_id`, role/branch headers, cookies de autorización ni datos de negocios. Los tests usan **una respuesta sintética interceptada**, no tenant/DB reales.

## 3. Entregables técnicos exactos y pruebas previstas (no asumidas)

**Sin modificar runtime productivo:** `frontend/src/main.ts`, `frontend/src/app/app.routes.ts`, `backend/`, `database/`, rutas/DTO de identidad ni servicios de marca.

- `frontend/tests/c02-08.journeys-public.spec.ts`: **11 pruebas Chromium** ejecutadas sobre Angular 22 público real; navegación de extremo a extremo, elementos informativos, accesibilidad móvil, estados `loading/error/offline`, reintento fresco, query/host/rol falsificado, payload privado, 12 rutas futuras 404 y ausencia de solicitudes privadas.
- `frontend/tests/c02-08.journeys-patterns.spec.ts`: **5 pruebas Chromium** de recorridos de formulario, tabla, descarte/scope, inyección XSS y teclado en componentes standalone Angular reales cargados mediante harness **separado de producción**.
- `frontend/playwright.journeys-patterns.config.ts`: entorno aislado puerto 4201, archivo de test exclusivo y salida diagnóstica propia.
- `qa/c02_08_audits.py`: **A** procedencia/trazabilidad/escenarios; **B** seguridad de contrato, aislamiento de navegador y mutaciones; **C** arquitectura/compatibilidad/UI/ejecución real vinculada al CI. Contract mutations obligatorias, no mera presencia de negativas escritas.
- `.github/workflows/c02-08-journey-contracts.yml`: Word original, A/B/C, regresiones C02-01..07, `npm ci`, `npm audit`, Angular22 tipos/build producción y harness aislado, backend .NET10 Release y **42 regresiones Chromium anteriores + 11 públicas + 5 harness = 58 pruebas esperadas**. Esta cifra solo se declara `PASS` tras salida CI real.

## 4. Controles de seguridad y parametrización integral

- Contrato de navegación no equivale a una sesión. Backend futuro, capítulos C04–C07 y posteriores, deberá resolver host/tenant, identidad, membresía, acción, sucursal, recurso, revocación y auditoría de forma **server-side**, negar BOLA/IDOR y cualquier acceso horizontal. SuperAdmin requiere acceso excepcional temporal con MFA, motivo y traza, nunca permiso global implícito a PII.
- Ninguna futura pantalla activa registrada, pagos, demo interactiva comercial, checkout, archivo privado, historial o guard Angular de autorización. Un query, selector de empresa en login, slug, JWT falso o claim del navegador no conceden derechos.
- Parametrización universal futura: logo, textos, encabezado, pie, tokens UI, formularios, tablas y variantes por `platform/tenant/branch/user`, con borrador, validación, vista previa, versión, aprobación, publicación, auditoría y rollback desde panel administrativo. **Este panel NO está implementado**; permisos backend, PII, resolución tenant/DB y controles de seguridad no son configurables. HTML/JS/CSS/SQL arbitrarios prohibidos.
- Negativas del contrato: 18 necesidades N01–18 + ocho amenazas transversales NEG-X01..X08. QA reproducible muta datos del propio contrato para confirmar rechazo real. Un script estático **no** equivale a prueba cross-tenant backend, esta frontera se prueba al existir la implementación.
- Alcance limitado: no certificar auditoría manual WCAG 2.2 AA, lectores NVDA/VoiceOver/TalkBack, dispositivos físicos, API privada BOLA/IDOR/MFA, perf Lighthouse o MySQL multiempresa. Cualquier P0/P1 del alcance o test requerido fallido es `NO_GO`.

## 5. Gates de entrega y rollback

1. Crear PR técnico desde `feature/c02-08-journeys-contracts` hacia `dev`; comprobar archivos, diff acotado, secretos, Word SHA, 58 Chromium y A/B/C sobre **último HEAD**. No contar workflows `skipped` como `success`.
2. Fusionar solo si CI relevante `SUCCESS` y el HEAD no se movió; revisar **todas las ejecuciones POSTMERGE** del SHA del merge. La prueba real de producción sigue limitada a la app pública + harness Angular.
3. En PR documental **separado**, añadir `docs/c02/C02_08_ACTA_MICROFASE.md`, actualizar avance **7/9 → 8/9**, fusionar y verificar CI POSTMERGE. Hasta entonces C02-08 **EN DESARROLLO**.
4. **C02-09** auditorías integrales del capítulo es la siguiente microfase habilitable **solo después del cierre documentado**. C02 **NO CERTIFICADO**; C03 bloqueado hasta triple auditoría integral y aceptación expresa del propietario.
5. Rollback: revert del PR técnico y, si existiera, del PR acta en `dev`; sin datos de producción, migraciones, cambios en `main` ni recursos de SOLQARYN.

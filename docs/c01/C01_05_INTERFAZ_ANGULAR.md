# C01-05 — Interfaz pública Angular 22, accesibilidad y lectura de identidad

**Estado:** IMPLEMENTADA EN RAMA, PENDIENTE DE VALIDACIÓN Y CIERRE. **C01 NO CERTIFICADO (0/37)**; C01-01/02/03/04 cerradas antes de iniciar. Fuente de verdad: Documento Maestro Único en docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx, SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e, docs/MASTER_REFERENCE.md, matriz C01-01 y contrato de lectura C01-04.

## Qué abarca y trazabilidad

| Requisito del Word | Criterio aplicado en C01-05 |
| --- | --- |
| P01, P14, T02-R01, T02-R02, T02-R03 | Identidad pública de CRECISOL, lema/descriptor en cabecera, portada y pie; sin contenido de tenant ni demo de reemplazo. |
| P04, P06, T01-R04, T02-R07, T03-R02 | Solo misma ruta pública de plataforma y DTO cerrado: ni selector tenant ni acceso a rutas privadas, sin headers/claims manipulados para resolver permisos. |
| P07, T01-R03 | El navegador valida JSON desconocido, texto contra markup/bidi/control, HEX RGB, activos locales PNG/WebP y tokens limitados; no ejecutar HTML, JS, CSS, fuentes remotas o SVG. |
| P15, P31 | Composición editorial nueva, sin promesas de negocio o cifras no implementadas. No se incorpora código de prototipos históricos. |
| P24, P25, T03-R01 | Marca y metadatos SEO publicados: título, descripción, theme-color y favicon local opcional. Ningún canonical, hostname público, origen comercial o copyright no aprobado se inventa. |

## Arquitectura y flujo

- frontend/package.json: Angular **22.2.4**, TypeScript **6.0.3**, Node **24.15.0** mínimo; todas las dependencias directas están fijadas. Angular Router carga las dos páginas standalone de forma lazy; HttpClient utiliza fetch.
- PublicBrandService: signal de estado loading / ready / unavailable y GET fijo same-origin a /api/v1/public/branding/platform, sin parámetros, token, tenant ID, localStorage, sesión, cookies añadidas o datos externos de respaldo; timeout 6.5 segundos. La lectura no almacena contenido en caché desde el servicio.
- parsePublishedPlatformBrand: validación por esquema exacto (campos desconocidos, incluyendo eventuales ScopeId/tenantId, causan rechazo), texto sin caracteres de control/bidi/markup, activos locales de /assets/, color hexadecimal y tokens permitidos. Esta es defensa adicional de UI: la autorización real y selección de publicación corresponden exclusivamente al backend C01-04.
- PlatformHomeComponent: Signals/computed, cabecera, hero, descripción, áreas derivadas del descriptor, identidad pública, pie y estados accesibles; nunca interpola HTML, acepta CSS/JS arbitrario ni cambia tokens de ejecución. La paleta dinámica solo colorea motivos decorativos, no texto de contraste variable. Meta y Title proceden del snapshot publicado y se limpian al fallar. El favicon se crea solo con una ruta local validada.
- CSS propio sin CDN, frameworks gráficos, iconografía de terceros, tipografías remotas ni diseño del prototipo verde. Responsive 320/360/375/700/950/1440; objetivos táctiles mínimos 44px, foco visible, navegación teclado, skip link, jerarquía semántica, anuncios de estado y reduced motion.
- src/index.html: noindex,nofollow intencional durante etapa preproductiva. SEO SPA dinámico no equivale a SSR ni a indexación garantizada. El noindex no puede retirarse hasta puerta de SEO/legal/host real en capítulos posteriores.
- La ruta desconocida carga componente 404 propio, no monta panel de negocio ni acceso admin.

## Identidad visual y límite obligatorio C01-06

El Word contiene la imagen azul aprobada, pero **no hay aún un archivo gráfico individual aprobado** para el sitio. C01-05 muestra solamente el nombre publicado como wordmark textual y motivos abstractos decorativos; **no instala, redibuja, reemplaza ni inventa isotipo oficial**. El marcador de integración official-logo-c01-06 reserva el paso posterior. No se carga la ruta logoUrl en una imagen hasta verificar el activo en C01-06. La ruta favicon puede apuntar al archivo esperado en DB; su existencia física e integridad gráfica son parte de C01-06, no se declaran aprobadas aquí.

## Requisitos de seguridad negativos

- Respuestas HTTP 404/503, JSON no válido, objeto tenant con ScopeId, texto XSS, CSS inválido, favicon externo, token de fuente desconocido, pérdida de publicación y fallo de API: estado de no disponibilidad, sin marca previa ni demo.
- Sin innerHTML, bypassSecurityTrust, eval, scripts de terceros, autenticación privada, selector de empresa o cache persistente en localStorage.
- Retirada/caída de marca implica limpieza de Title/Meta/favicons gestionados; no deja una identidad tenant o de otra revisión visible.
- Las imágenes, host, pertenencia y permisos de tenants no se implementan en el frontend y no se consideran autorizados; el navegador nunca debe conceder privilegios.
- Política CSP de despliegue, SSR, CDN, registrador de dominio, robots producción, UI administrativa y activos aprobados quedan sujetos a capítulos posteriores. NO GO para exponer datos privados.

## Pruebas y triple auditoría requeridas

1. Angular real 22.2.4/Node 24.15/TypeScript6: npm ci, npm run check:types y npm run build con presupuestos inicial y de estilos.
2. Chromium/Playwright: mock same-origin del endpoint público (no base de datos real ni servidor remoto), lectura/SEO, 360px sin scroll horizontal, teclado/skip, WCAG 2.2 AA automatizado con axe, XSS, favicon/color, estado 404/503/reintento, tenant y rutas inexistentes, movimiento reducido. La regresión API C01-04 permanece acreditada en su propia CI; el test UI evalúa el contrato AISLADAMENTE, no acredita conexión Angular + DB end-to-end.
3. Auditoría A: Documento Maestro + trazabilidad y aceptación.
4. Auditoría B: cliente fail-closed, XSS, datos privados, caché, medios, URL, secretos.
5. Auditoría C: Angular22/Node24, Signals, lazy routing, rendimiento, WCAG, responsive, optimización y reversión.
6. PR independiente, diff, logs y acta; NO declarar PASS antes de evidencia CI del SHA final; fusionar solo si no hay P0/P1 dentro del alcance.

## Impacto y Reversión

- frontend/: nueva app Angular standalone, rutas lazy, estilos, Playwright y pruebas.
- backend/ y database/: sin cambios. GET C01-04 consumido pero no reescrito; no conexión a Hostinger.
- .github/workflows, qa/, docs/: nuevas comprobaciones y seguimiento C01-05.

**Reversión:** revertir el PR individual C01-05. No existe despliegue, datos persistentes ni servicios productivos que revertir. **C01-06 será la siguiente microfase solo tras cierre documentado; C01 sigue 0/37 capítulos certificados.**

## Evidencia de las pruebas reales

[CI Angular 22 / Chromium #42558658894](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558658894) PASS: Angular22.2.4/Node24.15/TS6.0.3, npm ci desde lock real, initial 233.23 kB raw, 11/11 browser Playwright incl. axe WCAG 2.2 AA 2 viewports, auditorías A/B/C SUCCESS y npm audit producción 0 vulnerabilidades reportadas. [Fundación #42558658881](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558658881) PASS. [Acta detallada](C01_05_ACTA_MICROFASE.md). Queda pendiente verificación de CI del último HEAD y merge individual, sin adelantar C01-06.

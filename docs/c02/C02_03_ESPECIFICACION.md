# CRECISOL · C02-03 — Arquitectura de información pública

**ESTADO: IMPLEMENTACIÓN PÚBLICA ACOTADA EN DESARROLLO; C02-03 NO CERRADA hasta A/B/C, CI, merge y acta.** C02-01 y C02-02 están cerradas técnicamente (2/9). **C02 NO CERTIFICADO; 1/37 capítulos certificados, C03 bloqueado.**

## Autoridad, dependencia y trazabilidad

- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, capítulo 02. Se valida en CI con `qa/c02_01_source.py`: 11 párrafos originales P01..P11 y seis filas de personas/centro demo T01-R01..R06; precedencia del 7 de noviembre: **sin selector de empresa en el login habitual**.
- Dependencia previa [acta C02-02](C02_02_ACTA_MICROFASE.md), PR #33 fusionado a `dev` en `8628f9bd9ff15b56e53c8964a98d1e51a936222b` y cinco suites postmerge SUCCESS, incluida [#42620627974](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42620627974).
- `C02-03` es la **tercera microfase operativa** de nueve; NO debe confundirse con la puerta de proceso `MAE-C02-03 Codificar` del maestro. La especificación desarrolla estructura **pública** sin adelantar C02-04 navegación privada ni módulos empresariales C04–C31.
- Requisitos trazados: **P01** seis experiencias comunes; **P03/P04/P05** acceso por ruta, validación de dominio por servidor y contenido público publicado sin PII; **P06** transición futura a autorización backend; **P11** teclado, WCAG 2.2 AA, responsive y lectura comprensible; T01-R01 visita pública, T01-R06 demo sintética. [Mapa C02-02](C02_02_MAPA_NECESIDADES.json): N01–N06. Se preservan E03–E06 como experiencias **no operativas**.

## Alcance exacto: sitemap allowlist y rutas efectivas

El contrato derivado [C02_03_SITEMAP.json](C02_03_SITEMAP.json) contiene **4 rutas públicas explícitas**, 3 páginas nuevas lazy y 6 ámbitos diferidos. Únicamente se usa identidad de **plataforma** proveniente de `GET /api/v1/public/branding/platform`, mismo origen, snapshot publicado y validación estricta de C01. `routerLink` solo recibe rutas literales controladas por código; la URL no selecciona empresa, rol, dataset o DSN.

| ID | Ruta pública | Vista efectiva | Referencia/experiencia | Estado |
| --- | --- | --- | --- | --- |
| S01 | `/` | Landing actual de identidad de plataforma | P01, E01, N01/N02 | **Disponible**; mantiene contrato C01 y contenido versionado |
| S02 | `/experiencias` | Índice de las seis experiencias con límites claros | P01/P11/T01-R01/R06, E01, N01/N03 | **Informativa**, sin habilitar shells privados |
| S03 | `/demostraciones` | Descripción del centro demo futuro | T01-R06, E01, N03 | **Informativa**, SIN demo interactiva, identidad o datos reales |
| S04 | `/alta-empresas` | Requisitos previstos de onboarding | P06/T01-R01, E02, N04–N06 | **Informativa**, SIN formulario, cobros ni alta real |

**Fuera de la allowlist:** toda ruta desconocida resuelve únicamente `NotFoundComponent`. No hay `/admin`, `/superadmin`, `/panel`, `/empleado`, `/cliente`, `/login`, perfiles tenant ni `/negocio/:slug` operativos. Cualquier acceso a host, ID, query, hash o header manipulado jamás concede identidad o autorización. Cuando C04/C20 implementen sitios por dominio de empresa, el **backend** validará host y publicación; no se suplanta eso con una ruta Angular.

## Árbol público y accesibilidad

```text
Plataforma (solo información pública)
├── /                      Identidad institucional publicada (C01)
├── /experiencias          Seis experiencias descritas, estatus real
├── /demostraciones        Centro demo: intención únicamente, datos sintéticos futuros
└── /alta-empresas         Información de alta: registro/cobro pendientes

Dominios de empresas verificados                   NO implementados (C04/C20)
Consola global SuperAdmin                          NO implementada (C02-04/C05/C06/C08)
Panel de empresa y puesto de empleado              NO implementados (C02-04/C05/C06)
Portal privado consumidor                          NO implementado (C02-04/C05/C06/C17)
```

Cada nueva página tiene un `h1`, navegación pública `aria-current`, enlace «Saltar al contenido principal», títulos `h2`, foco visible, metas `noindex, nofollow`, y diseño responsivo desde 320px. El tema decorativo usa colores publicados de plataforma; **el texto conserva contrastes accesibles con independencia de tokens de marca**. El texto comercial personalizable no se duplica ni sustituye al snapshot versionado. El estado «En preparación» es informativo y explícito, no una promesa de módulo activo. Se respetan lectores de pantalla, `prefers-reduced-motion`, ausencia de animaciones forzadas y ningún formulario habilitado.

Al fallar la API, al recibir JSON inyectado con campos privados, un dominio falso o un snapshot inválido, la nueva vista **falla cerrada**: se ocultan marca/contenidos, se limpian título/metas/favicon gestionados, se muestra recuperación sin reutilizar un tenant o un caché local. Las nuevas páginas nunca consultan API privada ni guardan identidad en `localStorage` o `sessionStorage`. `robots: noindex,nofollow` se mantiene hasta una futura decisión de publicación pública; no se configura SEO productivo ni DNS.

## Matriz de separación y dependencia de las seis experiencias

| Experiencia | ¿Ruta operativa C02-03? | Gate de implementación pendiente |
| --- | --- | --- |
| E01 SaaS público | Sí: cuatro rutas de plataforma; solo S01 muestra identidad operativa | Editorial y contratación real en C08/C20 |
| E02 alta/contratación | No: S04 solo informa | Verificación, consentimiento, alta y cargos C05/C08/C23/C31 |
| E03 SuperAdmin | No | MFA, privilegio mínimo, acceso excepcional con motivo/plazo/auditoría C05/C06/C08 |
| E04 panel negocio | No | Tenant, sucursal, acción y propiedad en backend C04/C05/C06/C09 |
| E05 empleado | No | Membresía/asignación/revocación backend C05/C06 |
| E06 portal consumidor | No | Host verificado, catálogo publicado y datos privados propios C04/C05/C06/C17/C20 |

Los componentes compartidos y las rutas lazy evitan seis aplicaciones/microservicios innecesarios. **La existencia de un link, card, ruta o guard Angular nunca representa permiso backend.**

## Pruebas y auditorías exigidas

**A — funcional/trazabilidad:** comprobar Word íntegro por SHA, existencia de C02-02/18 necesidades, allowlist S01–S04, rutas Angular y seis experiencias, todos los enlaces públicos existentes, rutas deep-link y navegación entre ellas. Negativas: borrar/duplicar ruta, fuente falsificada o crear ruta tenant no permitida → FAIL.

**B — seguridad:** rechazar rutas privadas y variantes de host/query que intentan obtener datos tenant; pruebas Playwright con JSON publicado inyectado y fallos HTTP; confirmar ausencia de campos privados y caché/credenciales, destino API único y no formularios reales. **La prueba de rutas no equivale a BOLA/IDOR de endpoints privados inexistentes.** Rechazar HTML arbitrario, datos demo reales y permisos inferidos del navegador.

**C — arquitectura/UX:** build Angular 22 y ASP.NET Core 10 Release; verificación de assets, versiones y bundle budgets; Playwright Chromium con teclado, 320px, revisión automatizada WCAG 2.2 AA y movimiento reducido; CSS acotado, rutas lazy. API/DB no cambian, y no existe benchmark de producción.

**Impacto por capa:** `frontend/`: tres rutas lazy informativas, una plantilla y estilo reutilizables, un enlace de acceso desde la landing y pruebas E2E. `backend/`: **ningún cambio de endpoint ni de autorización**. `database/`: **ninguna migración ni datos**. `qa/` y `.github/workflows/`: validaciones ejecutables, evidencia y tres auditorías. `docs/`: especificación y sitemap trazado. No se incorporan dependencias nuevas, secrets, recursos productivos ni funciones fuera de C02.

**Rollback:** revert del PR de C02-03 y del PR de acta posterior en `dev`, restaurando exactamente la landing C01 y la ruta catch-all previa; no existe cambio DB o producción. Solo declarar **C02-03 CERRADA TÉCNICAMENTE (3/9)** tras CI último HEAD, revisión de diff/secretos, merge con SHA esperado, CI postmerge y acta. **C02 capítulo NO CERTIFICADO; C03 bloqueado.**

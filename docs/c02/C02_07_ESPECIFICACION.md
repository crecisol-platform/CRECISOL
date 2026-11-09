# CRECISOL · C02-07 — Diseño móvil y accesibilidad basal

**ESTADO: C02-07 EN DESARROLLO; 6/9 microfases C02 cerradas previamente.** Este documento define la microfase técnica, **no certifica WCAG ni el capítulo 02**. Únicamente C01 certificado (1/37), C02 NO CERTIFICADO, C03 BLOQUEADO. No se inician C02-08 ni módulos privados.

## 1. Autoridad y cadena secuencial comprobable

- Fuente: **Documento Maestro Único**, `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Capítulo 02 P01–P11, T01-R01..R06, cinco perfiles y seis experiencias E01–E06 con N01–N18. El extractor real `qa/c02_01_source.py` verifica los bytes y el capítulo. Ocho puertas normativas `MAE-C02-01..08` **no** equivalen a las nueve microfases operativas `C02-01..09`.
- Documentos anteriores: [necesidades C02-02](C02_02_MAPA_NECESIDADES.json), [rutas C02-03](C02_03_SITEMAP.json), [diseño de navegación privada C02-04](C02_04_NAVEGACION_PRIVADA.json), [estados C02-05](C02_05_ESTADOS_INTERACCION.json), [patrones C02-06](C02_06_PATRONES_FORM_TABLA.json) y [acta C02-06](C02_06_ACTA_MICROFASE.md).
- Puerta anterior **C02-06 cerrada técnicamente 6/9** por PR #40/#41; último `dev` `d54147596984275d98cf7a313a9fdeb8e8b06e22`; **nueve GitHub Actions POSTMERGE SUCCESS**, incluida [C02-06 #42651314408](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42651314408), [C01-07 #42651314496](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42651314496) y [C01-08 #42651314339](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42651314339). Acta/plan conservan C02 no certificado y C03 bloqueado.
- El alcance de C02-07 procede de [contrato móvil y accesibilidad](C02_07_MOBILE_ACCESIBILIDAD.json): **UI01–UI06**, E01–E06/N01–N18, **16 casos adversariales NEG01–NEG16**.

## 2. Objetivo, superficies y cambios precisos

Establecer una **base medible de reflujo y accesibilidad** sobre interfaces **ya autorizadas para aparecer** en C02-03..06: landing pública, tres páginas públicas informativas, estado de error y página 404, y el laboratorio sintético de formulario/tabla. No construir una nueva navegación, un checkout, un login o la administración real de tenants.

| Superficie | Requisito verificable | Riesgo que debe seguir bloqueado |
| --- | --- | --- |
| **UI01 Inicio público** | Encabezado, tres enlaces y CTA conservados a 320, 360, 768 y 1280 CSS px; tamaño táctil de 44 × 44, foco y salto al `main`; contenidos publicados extensos pueden partir línea | No convertir la marca publicada en una regla de permisos ni hardcodear la marca de un cliente |
| **UI02 Tres páginas informativas** | Cuatro enlaces visibles en todo ancho; tarjeta/sección refluye; texto al 200 %, semántica de navegación y única cabecera H1; click/Tab visibles | No introducir formularios de alta ni pantallas privadas |
| **UI03 Loading/Error/Offline** | Foco teclado en reintento, narración `role=status/alert`, contenido de error sin metadatos o publicación antigua; movimiento reducido | El aviso de desconexión no autentica ni autoriza reutilización de caché |
| **UI04 Wildcard not-found** | 404 utilizable en 320px, link 44px y sin solicitud de branding al entrar a ruta privada | Ninguna ruta mobile-only, selector de empresa o guard frontend concede acceso |
| **UI05 Formulario sintético** | Texto al 200 %, teclado, validación local, modal nativo de descartar, `Escape`/cancelar, superficies >=44px | No emitir POST ni confirmar guardado/registro/venta/PII |
| **UI06 Tabla sintética** | Filtro/orden/columnas por teclado y `aria-sort`; scroll horizontal **solo dentro** del contenedor de tabla, accesible con `tabindex=0`; contenido escapado | No exponer datos privados o autorizarlos desde navegador |

**Archivos propuestos:** `frontend/src/styles.css`, `frontend/src/app/pages/platform-home.component.{html,css}`, `frontend/src/app/pages/public-information.component.{html,css}`, `frontend/src/app/pages/not-found.component.ts` (destino de foco y reflujo). El único cambio al test harness C02-06 es CSS local para el diálogo nativo y sus botones de al menos 44px; la **configuración de producción** Angular, `src/main.ts`, `app.routes.ts`, servicios de branding, `backend/` y `database/` **no se modifican**.

**Parametrización integral:** los colores/logos/copyright/textos reales siguen saliendo del modelo de marca pública **versionado** y validado, no del harness ni del CSS estructural. El contrato fija que futuras etiquetas, temas, tokens de contraste, textos de footer/nav, diseño y variantes se gestionarán con configuración publicada administrable desde el panel, ámbito `platform/tenant/branch/user`, previsualización, auditoría y rollback. **Esto NO implementa el panel administrativo**. Nada en UI puede hacer editables permisos backend, aislamiento, tenancy, propiedad de datos, selección de DB ni pruebas de confirmación; prohibido ejecutar HTML/JS/CSS/regex/SQL arbitrarios desde la configuración.

## 3. Accesibilidad basal y pruebas reales

El objetivo automatizado es la matriz de visualización en Chromium con **320/360/768/1280 CSS px**, ampliación real de **texto CSS al 200 %**, tamaño mínimo **44 × 44 CSS px** de objetivos interactivos, foco visible por teclado, enlace de salto con destino enfocable, semántica de `main/nav/h1`, contraste forzado del navegador, `prefers-reduced-motion`, cajas con texto publicado de longitud máxima y sin scroll **de documento**. Única excepción: scroll horizontal de la región de tabla, explícitamente enfocada, con tabla semántica. **No** resolver desbordamiento con `overflow-x:hidden`, porque ocultaría información esencial.

- `frontend/tests/c02-07.mobile-accessibility.spec.ts`: **10 pruebas Playwright Chromium** contra app pública Angular real, con respuesta sintética del único GET same-origin `/api/v1/public/branding/platform`. Comprueban todas las rutas visibles, escalado, skip/foco, 404 privado, error sin datos obsoletos, reduced-motion, forced colors, tamaño táctil y auditoría automatizada axe con tags `wcag2a/wcag2aa/wcag21aa/wcag22aa`.
- `frontend/tests/c02-07.patterns-mobile.spec.ts`: **5 pruebas reales Chromium** contra componentes standalone Angular22 dentro del `pattern-harness` de C02-06, **nunca** dentro del build público. Ensayan flujos 320–1280 px, 200 % de texto, diálogo native cancel/Escape y reintento, interacción de tabla y axe sobre diálogo/modal.
- Runner aislado `frontend/playwright.patterns-a11y.config.ts`, puerto temporal 4201, sin backend ni ruta de producción. La suite pública usa el `playwright.config.ts` existente. Se repiten **27/27** pruebas de C02-03..06 (**19 públicos + 8 harness**); esperado tras nuevos 10+5: **42 pruebas Chromium**. Esta cifra es una **expectativa**, no una ejecución hasta que GitHub Actions termine.
- Auditar con **A funcional/trazabilidad**, **B seguridad fail-closed, datos/tenants y XSS**, **C arquitectura/UX/rendimiento basal**. Negativas deterministas sobre el JSON, además de la realidad del DOM. Los casos NEG01–NEG16 incluyen selector de tenant falsificado, datos antiguos, XSS, overflow oculto, objetivo táctil de menos de 44px, foco perdido, modal omitido y harness escapado a producción.

**Límites de evidencia:** axe automatizado NO equivale a auditoría WCAG manual completa ni prueba con VoiceOver/TalkBack/NVDA/Jaws, dispositivos físicos, teclado físico real, sistemas de aumentación/lectores de pantalla, diversidad de locales o carga de datos privados. No afirmar **“WCAG certificado”** ni cumplimiento global. La compatibilidad de Angular22, .NET10 y navegadores se prueba ejecutando CI y registrando salidas, no por suposiciones.

## 4. Seguridad, capas y riesgos

- **Frontend:** CSS de diseño y destinos de foco; inputs de publicación continúan controlados por `PublicBrandService`; Angular interpola texto y la API same-origin sigue sin query tenant ni credenciales cliente. Fallback en error limpia metadatos y datos anteriores.
- **Backend ASP.NET Core 10:** **sin cambios**. Su futura autorización real debe seguir resolviendo identidad, host/tenant, pertenencia, rol/acción, sucursal, asignación, propiedad de recurso, revocación y excepciones SuperAdmin con MFA/motivo/vencimiento/auditoría. Las pruebas UI aquí **no certifican** BOLA/IDOR/MFA sobre módulos inexistentes.
- **Database MySQL 8.4:** **sin cambios**, sin migraciones, datos reales o selectores DSN. El patrón de tabla sin servidor no prueba separación cross-tenant.
- **Riesgos P0/P1:** si se abre una ruta privada, se oculta acción imprescindible a 320/200 % o se reutilizan datos de otro ámbito, **NO_GO**. Si falla CI o una negativa, corregir y repetir desde el último HEAD. Rechazar secretos/versiones productivas.
- **Rendimiento basal:** ausencia de deps nuevas, sin JS de navegación extra ni datos persistentes, reflujo CSS y pruebas browser; no afirmar métricas Lighthouse ni cargas de backend no medidas.

## 5. Puertas, secuencia y reversibilidad

1. PR técnico rama `feature/c02-07-*`; auditorías A/B/C y mutaciones negativas; build Angular22 producción + laboratorio, análisis npm, .NET10 Release; **42 pruebas Chromium esperadas**, revisión diff/secretos, merge solo a `dev` con SHA esperado.
2. **Verificar CI postmerge del SHA del código técnico**, incluidas todas las regresiones que GitHub active; no contar jobs `skipped` como `SUCCESS`.
3. Crear **PR documental de acta separada**, incorporar SHA y IDs reales de CI, endurecer gates históricos C02-01..07, actualizar el avance **6/9 → 7/9** solo al fusionar acta y comprobar CI postmerge.
4. C02-08 — recorridos y contratos de experiencia — queda **siguiente habilitable, no iniciada**; C02 **NO CERTIFICADO**, C03 bloqueado. Ninguna aceptación humana de certificación C02 se asume.
5. **Rollback:** revert PR técnico y PR acta en `dev`; sin rollback de base de datos, producción ni assets externos. `main` y SOLQARYN intactos.

# CRECISOL · C05 UX F02 — Acta de certificación técnica delimitada

**Estado:** CERTIFICACIÓN TÉCNICA DEL SISTEMA DE DISEÑO INSTITUCIONAL (alcance F02).  
**La nota final premium de las interfaces de C05 sigue PENDIENTE de la última fase.**  
**Fecha de comprobación:** 19 de noviembre de 2026 (20 de noviembre UTC).  
**PR de F02:** [#175](https://github.com/crecisol-platform/CRECISOL/pull/175), DRAFT, NO FUSIONADO.

## Autoridad y SHA de referencia

- Repositorio autorizado `crecisol-platform/CRECISOL` (ID 1450414592), rama `feature/c05-ux-f02-sistema-institucional-20261119`.
- Base congelada C05 `f61a79d242288c686a88a7c3dd711f2be9a48c18` del PR #172.
- **SHA F02 ejecutado y aprobado**: `c6388c20b6eef698f73bd1e59b6387a3b8707651`, [run #44643408763](https://github.com/crecisol-platform/CRECISOL/actions/runs/44643408763) — **SUCCESS**.
- **Artefacto**: GitHub Actions #14586543720, `c05-f02-institutional-design-system-c6388c20b6eef698f73bd1e59b6387a3b8707651`, descargado e inspeccionado; ZIP con cinco PNG reales del laboratorio (320, 390, 768, 1024 y 1440 px), sin archivos faltantes. Archivo local de revisión: `CRECISOL-C05-F02-SISTEMA-INSTITUCIONAL.zip`.
- La presente acta es una actualización documental **posterior al SHA de prueba**; requiere constancia en la conversación del PR y, si se exige HEAD final con CI de la propia acta, nueva verificación del SHA tras este commit.

## Entregables implementados

1. **Tokens semánticos únicos** bajo `frontend/src/app/ui/institutional-system/institutional-tokens.css`: color/estado, tipografía, escala, bordes/radios, elevación, foco, iconos y movimiento con forced-colors/reduced-motion.
2. **Nueve componentes Angular 22 standalone, OnPush, Signals:** navegación (`cs-ui-navigation`), tarjeta (`cs-ui-card`), campo (`cs-ui-field`), formulario (`cs-ui-form`), tabla (`cs-ui-table`), aviso (`cs-ui-notice`), estado (`cs-ui-state`), acción (`cs-ui-action`) e icono fijo (`cs-ui-icon`).
3. **Directiva de tema** `csInstitutionalTheme`, tipada a `PublishedPlatformBrand` del contrato C01, con revalidación hex-only de colores decorativos, dos estilos iconográficos permitidos y tres familias tipográficas autorizadas. El color configurado no sustituye los tokens de texto, CTA, foco ni seguridad.
4. `frontend/angular.json` incorpora solamente la hoja de tokens al build de aplicación, junto a una configuración de *harness* Angular estrictamente aislado; no se crean rutas públicas ni nuevas dependencias npm.
5. Evidencia de pruebas en `frontend/tests/c05-f02-design-system.spec.ts`, sus scripts QA/workflow y [documento técnico](F02_SISTEMA_DISENO_INSTITUCIONAL.md).

**Alcance protegido:** diff de 43 archivos, todos dentro de F02; ninguna modificación a `frontend/src/app/pages`, `app.routes.ts`, servicios de identidad, `backend/`, `database/`, C01/C02, PR #173, `dev`, `main` ni SOLQARYN. Sin migraciones, despliegue, cobro, datos reales o producción.

## Auditoría A — Contratos y regresiones funcionales (PASS en el alcance F02)

- `qa/c05_ux_f02_design_system_audits.py A`: PASS; 9 componentes y directiva, contratos, CSS, barrel único, configuración de harness, scope de cambios.
- `npm run check:types` y build Angular 22 producción: PASS. Initial bundle reportado por CI: **261,27 kB** (estimado transferido **72,98 kB**); estilos globales **4,83 kB**.
- Build Angular del laboratorio independiente: PASS.
- Pruebas del laboratorio Playwright: **11/11 PASS**, incluyendo navegación, formulario, tabla, acciones, estados, branding.
- Regresión C05 de modales institucionales: **2/2 PASS** a 390 px y 1366 px.

## Auditoría B — Privacidad, seguridad y controles negativos (PASS para primitivas sintéticas)

- Auditor B: PASS; contraste estático de pares de texto/color >=4,5:1, semántica de color fija en acciones, sin CSS remoto/URL arbitraria en sistema, sin persistencia, `innerHTML` ni llamadas de API desde primitivas.
- Playwright: PASS; payload de marca inválido no cambia el fondo del botón principal ni provoca petición remota; HTML ficticio con etiquetas se representa como texto inerte; el cambio de `scopeKey` oculta filas previas hasta un nuevo ámbito válido; controles deshabilitados u ocupados no activables.
- **Límite:** el componente de tabla no realiza autorización; todo filtrado tenant, MFA, CSRF y validación pertenece al backend y al host. No se ha ejecutado ninguna prueba de IDOR, MySQL ni expiración real como parte de F02.

## Auditoría C — Arquitectura, accesibilidad y calidad visual (PASS en el laboratorio)

- Auditor C: PASS; tokens y componentes coherentes, pruebas responsive en 320/390/768/1024/1440, 200% de texto, forced-colors, reduced-motion y foco visible.
- Playwright + axe WCAG 2.2 AA: **11/11 casos PASS**; capturas **5/5** de los cinco anchos inspeccionadas.
- Inspección visual de las cinco imágenes: composición ordenada, contenidos visibles, sin sobreposición grave observada y scroll horizontal contenido en la tabla (sin forzar el documento).
- Los iconos son SVG internos de geometría fija; no hay proveedor externo ni biblioteca visual adicional.
- **Advertencias observadas en CI:** dependencias existentes `qrcode`/`dijkstrajs` no ESM; y errores de proxy `ECONNREFUSED` al backend en la regresión UI aislada. No impidieron compilar ni aprobar las pruebas de presentación. **No** se interpretan como prueba de integración real con API/MySQL.

## Resolución y límites

**F02 certificada técnicamente como biblioteca fundacional y laboratorio visual probado, NO como rediseño completo de las 65 interfaces C05.** La calificación estética final >=9/10 por pantalla y el cierre de todo el plan solo se realizarán en la última fase. La conversión de las páginas y modales existentes a estos componentes corresponde a fases posteriores, con pruebas sobre contextos reales y aceptación humana independiente.

**NO AUTOMERGE:** el PR #175 permanece DRAFT y sin fusionarse; PR #173 se conserva sin cambios. F03 NO iniciada. Para revertir, descartar rama F02/PR y sus archivos agregados, revirtiendo únicamente `frontend/angular.json`; no hay datos ni migraciones que restaurar.

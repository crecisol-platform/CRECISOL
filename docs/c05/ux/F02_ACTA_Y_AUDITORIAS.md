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

---

## Adenda F02, 22-11-2026 — Paridad real de las 65 superficies

La certificación técnica histórica de nueve primitivas y su laboratorio sigue válida **exclusivamente para ese alcance**. Para comprobar la adopción en el código posterior F06 se añadió [diagnóstico de 65 superficies](F02_65_DIAGNOSTICO_ADOPCION_2026_11_22.md), [matriz individual](F02_65_PARIDAD_VISUAL_2026_11_22.csv) y `qa/c05_ux_f02_65_parity_audits.py`.

**SHA de producto auditado:** F06 PR #179 `d539b75d13cde64c8ea3279d33ab35b6349c1adf`; F01 fuente validada `01efbce91586597006254f721e5736adf991e2a4`, sin traer datos de SOLQARYN. La nueva auditoría A/B/C estática de adopción exige pruebas sobre SHA y blob exactos y decisiones fail-closed. [CI de inventario F06 #45032597168](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597168) y [CI técnica #45032597178](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597178) registraron SUCCESS para el SHA de origen; el código F02 no afirma haberlos vuelto a ejecutar.

**Hallazgo:** de las nueve primitivas de F02, en las 11 plantillas F06 del inventario se encuentran directamente navegación, tarjeta y estado. Los 65 recorridos heredan el tema institucional y tokens globales, pero en 34/65 referencias el archivo fuente no invoca directamente una de esas nueve primitivas. En seis hojas de producción hay 377 apariciones de colores HEX frente a 93 referencias a tokens. Es **deuda de unificación candidata a revisar**, no 377 fallos demostrados. Los modales y el feedback usan componentes especializados de F05/F06, cuya calidad no se deduce de los tests del laboratorio F02.

**Auditorías delimitadas:**
- A: trazabilidad exacta 65 F01–F06, fuente HTML, tipos, rutas y SHA.
- B: no cambiar permisos por el cliente, preservación de los controles semánticos de evidencia 014/057 y rechazo de notas artificiales; **sin nueva prueba backend/MySQL**.
- C: uso de tokens/componentes, deuda CSS cuantificada y limitaciones explícitas; **no inspección editorial completa ni aprobación 9.5**.

**Decisión actual:** biblioteca F02 certificada históricamente en su propio alcance; **paridad visual integral 65/65 NO CERTIFICADA y aceptación comercial 0/65**. Es obligatorio verificar la CI del último HEAD de PR #175 antes de cerrar el nuevo auditor. Ninguna edición de `dev`, `main`, F03–F06, backend, database, C06/F07, producción o SOLQARYN. El PR continúa DRAFT y sin fusión.

# CRECISOL · C05 UX F01 — Capturas, calificación inicial y puerta de aceptación

## Identificación inmutable de la línea base

- Candidato a fotografiar: `f61a79d242288c686a88a7c3dd711f2be9a48c18` (PR #172, TOTP-SHA1 integrado); rama F01 nacida de este SHA.
- **NO usar** PR #173 para las capturas *previas a la mejora*. Contiene ya cambios UX y sesgaría la comparación.
- Toda captura real debe contener archivo PNG, `sourceSha`, ruta y estado, resolución viewport, marca `synthetic:true/false`, timestamp UTC, resultado de a11y, ausencia de datos reales, y vínculo al run/artifact.
- Los PNG existentes en mensajes de usuario son referencias visuales sin metadata de compilación; no sustituyen capturas sobre SHA verificado.

## Presets obligatorios

| Perfil | Viewport Playwright | Escenarios |
| --- | --- | --- |
| Móvil estrecho | 320×720 | tabs, modales, scroll, formularios largos, mensajes |
| Móvil | 390×844 | pantallas C05, vacíos, alta, MFA, error |
| Tablet | 768×1024 | reflujo, menú, modales y overlays |
| Escritorio | 1440×900 | jerarquía, densidad, navegación, paneles |
| Complementario manual | 1024×768, zoom 200% | puntos de ruptura y reflow de texto |

Un resultado de 320 px que oculta acciones o corta el pie de modal es NO-GO. Las capturas `fullPage` permiten ver altura total, pero también hay que comprobar un viewport real sin recortes y el scroll interno del diálogo.

## Ejecución reproducible desde repo C05 (solo QA local)

```bash
cd frontend
npm ci --ignore-scripts --no-audit --no-fund
npm run check:types
npm run build
npx playwright install chromium
npx playwright test tests/c05-f01-visual-baseline.spec.ts --reporter=line
```

El proyecto Playwright inicia `npm run start` en `127.0.0.1:4200`. Ejecutar en Node 24.15+ y sobre la **rama F01**. El workflow `c05-ux-f01-baseline.yml` replica el flujo de captura sin desplegar ni crear servicios. Los PNG y metadatos `*.json` quedan en `frontend/test-results`, conservados como artefacto con nombre `c05-f01-synthetic-visual-baseline-<SHA>`.

La prueba introduce solamente datos falsos en señales de Angular y mocks de endpoints. Esto evalúa composición/render responsive, **no prueba permisos ni el contrato de sesión real**, y se debe reflejar en la calificación. Si `ng.getComponent` no está disponible, el test debe fallar y no fingir capturas autenticadas. No abrir conexiones a MySQL real ni fotografiar QR/códigos de producción.

## Registro de evidencias para cada fila de la matriz

Cada `C05-F01-NNN` deberá terminar con:
1. Fuente Angular precisa y rama/SHA exactos.
2. API del botón o evento, método y payload de entrada exigido, autorización a contrastar.
3. Variantes aplicables: normal, loading, error, success, empty, expired, prohibido, enfoque, cancelado, sin rol, idempotente (N/A fundamentado si no procede).
4. Capturas con `device`, `viewport`, `URL`, `state`, `screenshot path`, `synthetic`, `run_id`, `SHA`.
5. Calificación visual inicial objetiva, brecha observada y recomendación de futura F02; **no mejorar CSS o componentes durante F01**.
6. Prueba negativa de fuga de PII/tenant/realm en los estados de error y cierre, no por inspección estética solamente.

## Escala reproducible de calidad visual (0–10)

Cinco dimensiones, puntuadas de 0 a 2 cada una:
- **Jerarquía:** 0 = confusa; 1 = legible pero plana; 2 = título, propósito, CTA y prioridades inmediatamente claros.
- **Composición/densidad:** 0 = saturada o demasiado vacía; 1 = estructura básica; 2 = equilibrio profesional y progresivo.
- **Consistencia institucional:** 0 = controles nativos incoherentes; 1 = mezcla aceptable; 2 = tokens y componentes uniformes.
- **Estado y lenguaje:** 0 = técnico/ambiguo; 1 = entendible; 2 = mensajes contextuales, recuperables y no engañosos.
- **Responsive/accesibilidad visual:** 0 = overflow/corte/foco perdido; 1 = limitaciones menores; 2 = reflow, controles/foco/contraste en 320→1440.

**NOTA PROVISIONAL** = puntaje heurístico a partir de capturas suministradas. **NOTA VERIFICADA** = revaluación en PNG con SHA, test runtime y revisión manual. Cuando no hay evidencia suficiente, dejar celda vacía. No calificar por intuición lo que no aparece en la imagen.

Umbral objetivo de fases de mejora posteriores: cada fila visual esencial ≥9/10, ninguna P0/P1, sin promedios compensatorios. La F01 se puede certificar con notas bajas si la **línea base está íntegra y verificada**; no autoriza iniciar F02 sin acta.

## Matriz de dispositivos × estados

`F01_MATRIZ_INTERFACES_BASE.csv` es la lista maestra de 65 filas. Para todas las vistas base, los cuatro viewports son obligatorios. Para los 27 modales, capturar abiertos + recién cerrados y restablecimiento del foco; si el modal requiere datos, usar un fixture ficticio. Para MFA y recuperación, dividir en solicitud, respuesta y reintento. Capturar `empty` con respuestas vacías reales o mock documentado. El test automatizado cubre las superficies alcanzables mediante sus entradas sintéticas; **los faltantes se completan manualmente, nunca se dan por cubiertos automáticamente**.

## Cierre y controles de certificación F01

**A — Inventario y contratos:** todas las 65 filas reconciliadas con código, 47 formularios y 27 modales; 78 rutas API mapeadas contra fuente; no huecos sin N/A razonado.

**B — Privacidad y autorizaciones:** ninguna captura con secretos reales; separación cuenta/plataforma/tenant; negativo de permiso/expiración en backend, no solo mock; máscaras y errores neutros.

**C — Rendimiento, arquitectura y QA visual:** Node24 + Angular22 + Chromium ejecutados sobre SHA de F01, accesibilidad y scroll/teclado en cuatro anchos, evidencia PNG + metadatos + hashes, calificación visual inicial para cada estado representado, trazabilidad al workflow.

**Criterio fail-closed:** si falta cualquiera de A/B/C o una captura obligatoria está sin ejecutar, el acta permanece `PENDIENTE`, F02 bloqueada. No se altera el Documento Maestro Único para reflejar un NO-GO temporal.

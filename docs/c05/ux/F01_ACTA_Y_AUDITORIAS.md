# CRECISOL C05 · UX Fase 01 — Acta de auditoría y certificación

**ACTA: ABIERTA / SIN CERTIFICACIÓN F01**  
Fecha de apertura: 2026-11-19. Tipo: línea base documental y de captura, **no rediseño**.

## Alcance inmutable

- Proyecto y repositorio autorizado verificado: `crecisol-platform/CRECISOL`, ID 1450414592, migración del antiguo owner.
- Rama de trabajo: `feature/c05-ux-f01-inventario-20261119`.
- Fuente exacta de la línea base: `f61a79d242288c686a88a7c3dd711f2be9a48c18` (PR #172).
- PR #173 queda fuera de esta fase porque ya contiene cambios de F02 y siguientes; `dev`, `main`, `backend/`, `database/` sin mutación en F01.
- Autoridad documental: `AGENTS.md`, Skill canónica GitHub, `docs/MASTER_REFERENCE.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md`. C06 no habilitado.

## A — Auditoría funcional / exhaustividad de inventario

**Inspección documental inicial efectuada:** `app.routes.ts` (ruta Angular `/cuenta`, 12 valores de vista), `identity-portal.component.{ts,html}`, componentes de identidad de empresa y operadores, 27 instancias de modal, 47 formularios, diccionario backend de 74 operaciones y 4 rutas adicionales.  
**Entregables:** `F01_MATRIZ_INTERFACES_BASE.csv` (65 líneas de superficies), `F01_CONTRATOS_API_BASE.csv` (78 rutas), `F01_INVENTARIO_Y_LINEA_BASE.md`.  
**Pendiente:** ejecución en código del auditor `qa/c05_ux_f01_inventory_audits.py A` contra HEAD de F01; cotejo del inventario con capturas y flujos reales, y resolución de entradas con autorización no verificable por lectura estática.

Veredicto A: **PARCIAL — inspección y trazabilidad estática, sin certificación de runtime**.

## B — Auditoría privacidad, permisos y controles negativos

**Inspección inicial efectuada:** la matriz separa estrictamente `identity/` y `platform-identity/`; clasifica redirecciones, recuperación, aprobaciones, pruebas MFA, sesiones, host verificado y datos de prueba. `c05-f01-visual-baseline.spec.ts` usa identidades ficticias y evita QR, códigos de respaldo o contraseñas reales.  
**Pendiente:** ejecutar el auditor B y pruebas negativas reales: tenant cruzado, operador de negocio contra consola de plataforma, expiración server-side 30 min, sesión revocada, falta de permiso, token caducado, QR eliminado, conteo agregado sin fuga de PII. Ningún fixture visual sintético acredita esas propiedades.

Veredicto B: **PARCIAL — inventario de riesgos y precauciones, sin negativos completos**.

## C — Auditoría frontend/responsive/accesibilidad y evidencia

**Preparado:** 4 tamaños 320×720, 390×844, 768×1024, 1440×900, entorno sintético, capturas `fullPage` con JSON de contexto, prueba de apertura/cierre de modales, fixture de empty/loaded/error/success/expiración simulada, y workflow que compila Angular 22/TS con Node 24, instala Chromium y sube artefactos vinculados a SHA.
**Pendiente:** run real SUCCESS sobre HEAD final, revisión de los PNG, normalización de nombres, captura de estados no alcanzados por los mocks, comprobación de teclado/reflow/AA/zoom y nota inicial verificable para cada superficie aplicable. La simulación de expiración no sustituye el reloj de backend.

Veredicto C: **PENDIENTE DE EJECUCIÓN Y EVIDENCIA**.

## Evidencias que NO se pueden reutilizar incorrectamente

- El run previo `44601679085` de PR #172 terminó SUCCESS en A/B/C/D+gate, pero **no contiene artefactos PNG** según consulta realizada el 19-11-2026. No certifica visualmente la F01.
- Las capturas compartidas en esta conversación ilustran deficiencias y permiten notas heurísticas para 31 filas, pero **no verifican un SHA**.
- El runner actual de autoría solo dispone de Node 22, Chromium, sin .NET 10 ni repositorio descargable; no atribuirle compilación Angular, pruebas E2E o MySQL.
- Cualquier resultado de GitHub Actions en ejecución se registra con URL, SHA, run ID, conclusión y enlaces a capturas **después de verificarlo**, no antes.

## Puerta de aceptación F01 (NO-GO actual)

- [ ] A: auditor de inventario ejecutado + 100 % de filas y rutas reconciliadas.
- [ ] B: privacidad y roles documentados **y** negativos aplicables ejecutados o N/A sustentado.
- [ ] C: Angular/Chromium y 4 resoluciones ejecutadas sobre SHA; artefactos inspeccionados.
- [ ] Cada fila: nota inicial verificada o N/A razonado, vínculo a pantalla/estado/API, sin secretos.
- [ ] Tres auditorías A/B/C sin fallos vigentes ni huecos P0/P1.
- [ ] Acta de aceptación F01 con SHA, evidencias, responsable de revisión y fecha verificable.

**Resolución:** `NO CERTIFICADA`. F02/F03 y la nota objetivo 9/10 continúan BLOQUEADAS. No realizar cambios de UI en F01; no fusionar sin cierre probatorio.

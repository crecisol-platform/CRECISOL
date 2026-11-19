# CRECISOL C05 · UX Fase 01 — Acta de auditoría y certificación

**ACTA: VALIDACIÓN EN CURSO / SIN CERTIFICACIÓN FINAL F01**  
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
**Evidencia ya ejecutada:** auditor fuente `qa/c05_ux_f01_inventory_audits.py A` y verificación de rama/diff, PASS en run **44619062318**. El catálogo se actualizó a **65 notas iniciales provisionales**, rango 3,0–5,0, media 4,02/10; aún hace falta registrar la evidencia ampliada y clasificar las limitaciones de mocks.

Veredicto A: **PASS DOCUMENTAL Y DE FUENTE** en run 44619062318; condicionada a no alterar el inventario sin repetir su guard. No equivale a la certificación UX completa.

## B — Auditoría privacidad, permisos y controles negativos

**Inspección inicial efectuada:** la matriz separa estrictamente `identity/` y `platform-identity/`; clasifica redirecciones, recuperación, aprobaciones, pruebas MFA, sesiones, host verificado y datos de prueba. `c05-f01-visual-baseline.spec.ts` usa identidades ficticias y evita QR, códigos de respaldo o contraseñas reales.  
**Evidencia:** auditor B de F01 PASS en run **44619062318**. Como regresión histórica de la **misma base f61a79d...**, los runs **44601678730** (Angular→API→MySQL), **44601679085** (A/B/C/D modal UX) y **44601678999** (MFA/MySQL) acabaron SUCCESS. **Límite:** los nuevos mocks visuales no vuelven a probar esas barreras de backend, ni autorizarán datos productivos.

Veredicto B: **PASS DOCUMENTAL/SCOPE** para la línea base, con regresiones reales históricas del código fuente identificadas; no es una certificación nueva de seguridad productiva.

## C — Auditoría frontend/responsive/accesibilidad y evidencia

**Preparado:** 4 tamaños 320×720, 390×844, 768×1024, 1440×900, entorno sintético, capturas `fullPage` con JSON de contexto, prueba de apertura/cierre de modales, fixture de empty/loaded/error/success/expiración simulada, y workflow que compila Angular 22/TS con Node 24, instala Chromium y sube artefactos vinculados a SHA.
**Evidencia inicial:** run **44619062318** SUCCESS; artefacto **14575742705** inspeccionado con **212 PNG + 212 JSON**, 53 estados por viewport, 27 aperturas de diálogo por viewport y 0 desbordamientos horizontales informados. **Pendiente:** nueva ejecución del validador de integridad de capturas ampliadas, revisión de estados y geometría de modales en SHA final; pruebas físicas/zoom/teclado de flujos reales no atribuidas a este run.

Veredicto C: **PARCIAL — captura básica ejecutada, ampliación no certificada hasta run final**.

## Evidencias que NO se pueden reutilizar incorrectamente

- El run previo `44601679085` de PR #172 terminó SUCCESS en A/B/C/D+gate, pero **no contiene artefactos PNG** según consulta realizada el 19-11-2026. No certifica visualmente la F01.
- Las capturas del propietario sustentan inicialmente 31 notas sin SHA; el CSV de 65 notas actualizadas distingue 51 puntuaciones de confianza media y 14 baja, y las capturas del runner sí conservan un SHA de fuente f61a79d...
- El runner actual de autoría solo dispone de Node 22, Chromium, sin .NET 10 ni repositorio descargable; no atribuirle compilación Angular, pruebas E2E o MySQL.
- Cualquier resultado de GitHub Actions en ejecución se registra con URL, SHA, run ID, conclusión y enlaces a capturas **después de verificarlo**, no antes.

## Puerta de aceptación F01 (NO-GO actual)

- [x] A: auditor de fuente ejecutado en run 44619062318, 65 superficies y 78 contratos inventariados; repetir si cambia el CSV.
- [x] B: privacidad/roles documentados; auditor B PASS sobre F01, con regresiones históricas reales de f61 identificadas. N/A de pruebas físicas/producción explícito.
- [ ] C: capturas básicas 212 y Angular/Chromium PASS en 44619062318; exige integridad ampliada y run SUCCESS en SHA final.
- [x] Cada fila: 65 notas iniciales *provisionales*, 51 confianza media/14 baja con enlace de familia/API; ningún dato real. No reemplaza puntuación UX futura verificable 9/10.
- [ ] Tres auditorías A/B/C sin fallos vigentes ni huecos P0/P1.
- [ ] Acta de aceptación F01 con SHA, evidencias, responsable de revisión y fecha verificable.

**Resolución:** `NO CERTIFICADA`. F02/F03 y la nota objetivo 9/10 continúan BLOQUEADAS. No realizar cambios de UI en F01; no fusionar sin cierre probatorio.

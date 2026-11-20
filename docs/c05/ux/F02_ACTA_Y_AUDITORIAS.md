# CRECISOL · C05 UX F02 — Acta técnica (abierta)

**Estado actual:** IMPLEMENTACIÓN VERSIONADA, PRUEBAS Y CERTIFICACIÓN PENDIENTES DE EVIDENCIA.  
**Fase:** 02 de plan de interfaces; F01 certificada técnicamente en su alcance de inventario y capturas sintéticas (PR #174, SHA 4eacdd79...).  
**Objetivo:** sistema de diseño común, sin calificación estética final del producto.

## Identidad y límites

Repo canónico crecisol-platform/CRECISOL, rama feature/c05-ux-f02-sistema-institucional-20261119 basada en f61a79d242288c686a88a7c3dd711f2be9a48c18 (PR #172 DRAFT). No afectaciones a SOLQARYN, dev/main, backend, database ni servicios de producción. Fases de rediseño de pantallas y PR #173 NO se incluyen.

## Auditoría A — funcional/contratos

- [ ] Angular 22 producción y harness sintético compilan.
- [ ] 9 componentes + directiva publicados mediante un barrel único.
- [ ] tokens y bindings de parametrización preservan contrato PublicBrandService.
- [ ] navegación, formularios, tablas, alertas y acciones renderizados y probados.
- [ ] regresiones de modales C05 y patrones C02/C01 aplicables comprobadas.
- [ ] diffs solo en F02; no duplicar autorizaciones backend.

## Auditoría B — seguridad/privacidad

- [ ] branding HEX validado, sin CSS/URLs dinámicas arbitrarias.
- [ ] filas de scope antiguo no se muestran; HTML malicioso se escapa.
- [ ] sin API calls, almacenamiento persistente, secretos ni datos reales desde componentes.
- [ ] contraste normal >=4.5:1 y botones de seguridad sin paleta de tenant.
- [ ] sin rutas públicas de laboratorio; autorizaciones C05 intactas y en backend.

## Auditoría C — arquitectura, rendimiento y UX

- [ ] screenshots reales Chromium en 320, 390, 768, 1024, 1440.
- [ ] WCAG AA axe sin fallas graves, teclado y foco visibles.
- [ ] escala 200 %, forced-colors, prefers-reduced-motion.
- [ ] 44×44 controles, reflow sin scroll horizontal del documento.
- [ ] build budget de aplicación Angular 22 y bundle sin dependencias nuevas.
- [ ] reporte con SHA, run ID, capturas y evidencia real, sin afirmar 9/10.

## No-go y pendientes

**NO CERTIFICAR** si fallan pruebas obligatorias, si hay desbordes, fugas por cambio de ámbito, XSS, pérdida de foco, dependencia externa, P0/P1, fallas de bundle o regresión C01/C02/C05. El banco de pruebas usa fixtures y no acredita permisos backend reales, integración TOTP física ni seguridad productiva.

El acta no certifica la nota final premium. Queda por completar ejecución y resultados del SHA definitivo, tres auditorías reales A/B/C y verificación visual de los artefactos. PR F02 como DRAFT sin fusión y sin abrir F03.

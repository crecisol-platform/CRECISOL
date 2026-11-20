# CRECISOL C05 UX F03 — Acta A/B/C de navegación y arquitectura de información

**Estado inicial:** ABIERTA — **F03 NO CERTIFICADA todavía.**  
**Repositorio y rama:** `crecisol-platform/CRECISOL`, `feature/c05-ux-f03-arquitectura-navegacion-20261119` (nacida del SHA F02 `0361934286282ddb1b6ba94797125859d37464d7`).  
**Alcance:** cuenta, acceso invitado, etapa MFA y consola de operadores; rutas y permisos backend intactos. F04 bloqueada.

## Auditoría A — Funcionalidad, enlaces y regresiones
- [ ] `qa/c05_ux_f03_navigation_audits.py A` ejecutado con éxito en SHA final.
- [ ] Angular 22 producción y laboratorio F02 compilan.
- [ ] Flujos guest, challenge, cuenta y operador conservan sus componentes y handlers.
- [ ] `RouterLink` conserva `?vista` e historial sin inventar permisos.
- [ ] Cambio de pestaña de Seguridad conserva `loadSessionPolicy()`.
- [ ] 11 pruebas F02 y regresión original de modales C05 ejecutadas y aprobadas.

## Auditoría B — Privacidad y seguridad de ámbitos
- [ ] `qa/c05_ux_f03_navigation_audits.py B` ejecutado.
- [ ] Un invitado no obtiene pestañas privadas por URL; rutas no permitidas usan fallback de presentación.
- [ ] La consola de operadores exige estado autenticado de plataforma (sin sustituir autorización backend).
- [ ] Ninguna API, MySQL, CSRF, MFA, sesión, roles o credenciales alterada.
- [ ] Sin capturas con datos reales, semillas MFA, tokens de recuperación, URLs privadas ni cuentas del propietario.

## Auditoría C — Arquitectura/UX/accesibilidad
- [ ] `qa/c05_ux_f03_navigation_audits.py C` ejecutado.
- [ ] Capturas Chromium 320, 390, 768, 1024, 1440; sin overflow del documento.
- [ ] Foco, `aria-current`, breadcrumbs, navegación por teclado, 200% de texto.
- [ ] Reflow, `forced-colors`, `prefers-reduced-motion`, axe WCAG 2.2 AA.
- [ ] Regresión C01/C02 y F02, UI C05 sin P0/P1 demostrable en alcance.
- [ ] ZIP de evidencias inspeccionado, SHA exacto, run ID y resultados sin atribuciones falsas.
- [ ] Diff comparado con F02: solo páginas C05 de navegación, pruebas, documentos F03 y workflow.

**Criterio de cierre:** no afirmar «certificada» hasta que todo el gate A/B/C y las pruebas obligatorias pasen sobre el último SHA. La puntuación final ≥9/10 por interfaz queda para la última fase del programa, no esta acta. No fusión de PR #172, #173, #174 o #175 por esta actividad; producción, dev y main intactos.

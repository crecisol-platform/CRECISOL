# CRECISOL · C05 UX F04 — Consola institucional de operadores

**Fase activa:** F04, iniciada el 19-11-2026 (America/Tegucigalpa). **Estado inicial: EN DESARROLLO / NO CERTIFICADA.**

## Autoridad y dependencias
Repositorios verificables: `crecisol-platform/CRECISOL` (ID 1450414592; transferencia histórica del nombre anterior); `AGENTS.md`, Skill local `.agents/skills/crecisol-project-governance/SKILL.md`, referencia del Documento Maestro Único `docs/MASTER_REFERENCE.md` y estado vigente en `dev`. F04 parte exclusivamente del **HEAD F03 `b503c97a0cae82b3c4c60853e084286c79073943`**, certificado técnicamente con [CI #44672431435](https://github.com/crecisol-platform/CRECISOL/actions/runs/44672431435). F03 (PR #176), F02 (PR #175), F01 (PR #174) y autenticación SHA1 (PR #172) aún no están fusionados a `dev`; F04 será PR dependiente en borrador y no se fusionará antes de esas dependencias.

## Entregable funcional: cinco experiencias diferenciadas

| Superficie | Alcance autorizado por contrato existente | Estados, filtros, decisiones y límite |
| --- | --- | --- |
| Casos | `platform/case`, `platform/cases/find`, `platform/review`, `platform/release-share` | Consulta **por referencia de caso**; estado bloqueado, finalizado, en revisión y periodo de seguridad; filtro local del caso consultado; decisión deliberada (sin preselección), evidencia, motivo, MFA, confirmación adicional. **No existe contrato para bandeja global de casos** y F04 no inventa uno. |
| Sesiones | `platform/sessions`, `platform/sessions/rotate`, `platform/sessions/revoke`, `platform/sessions/revoke-all` | Tarjetas con familia de dispositivo, fechas locales y referencia corta; filtro por reciente/próxima caducidad; confirmación de sesión individual y renovación; MFA para cierre completo. No presentar UUID completo ni sesiones de negocio. |
| Perfil | `platform/profile/read`, `platform/profile/save`, `platform/profile/photo`, `platform/profile/photo-upload` | Datos legibles, imagen segura, idioma/zona horaria, formulario precargado y estados sin datos; el perfil **no** asigna roles ni permisos. |
| Seguridad | `platform/password/change`, `platform/mailbox/request/complete`, `platform/factor/replace/confirm`, `platform/session-policy/update` | Acciones sensibles separadas; motivos, MFA fresco y versión esperada para política de inactividad de 5–120 min, valor inicial 30. No tratar estas operaciones como permisos implícitos de UI. |
| Monitor | `platform/abuse-summary` | Contadores agregados de las últimas 24 h; categorías allowlist (desconocidas como “Otra señal de protección”), filtro por banda visual de prioridad; no exponer IP, correo, UUID, usuario o tenant. Priorización local **no** concede permiso ni cambia acciones del backend. |

## Arquitectura y seguridad (gate P0)
- `PlatformIdentityApiService` habla exclusivamente con `/api/v1/platform-identity/`, no comparte `IdentityApiService`, el token/cookie, CSRF, la pertenencia a un tenant, perfil o recursos privados de negocio.
- El backend existente mantiene denegación por defecto, permisos propios del operador, MFA de aprobación, doble control humano, periodo de seguridad de 24 h, protección contra IDOR y auditoría independiente. F04 **no cambia** los contratos backend ni migraciones MySQL.
- Cada lectura invalidará respuestas anteriores tras logout, revocación o rechazo de autorización; un `401` tardío no debe cerrar una nueva sesión.
- Las referencias de sesiones y casos se abrevian **solo en presentación**. Los UUID completos siguen siendo capacidades identificadoras exclusivamente en comandos al backend. Las etiquetas de eventos desconocidos nunca se muestran crudas.
- Una consulta de Casos no permite enumeración, búsqueda libre de empresas ni autorización por URL. La consulta `cases/find` existente exige prueba, motivo, ámbito explícito y MFA; cualquier ampliación futura requiere contrato de servidor y nuevas pruebas independientes.
- **Sin** cambios a `dev`, `main`, SOLQARYN, producción, Cloudflare, VPS, DNS, servicios de terceros o cobros.

## Calidad y certificación exigible
Rúbrica por cada vista (total 10): composición 2,0; jerarquía 1,5; flujos y prevención de error 2,0; estados, redacción y privacidad 1,5; responsive, teclado y accesibilidad 2,0; consistencia C01/C02/F02 1,0. **Mínimo individual ≥9,0/10** para Casos, Sesiones, Perfil, Seguridad y Monitor, no una media agregada. 320/390/768/1024/1440, estados de datos, vacío, error y diálogo cuando corresponda. La valoración humana/editorial no puede suplirse por un test de Chromium.

**Auditoría A:** contratos y navegación, pruebas reales Angular 22/Node 24, compatibilidad F03/F02, regresiones de cuenta y backend. **Auditoría B:** acceso cruzado tenant↔operador, MFA, motivación, decisiones explícitas, referencias no crudas, respuestas tardías/denegadas, cuotas/CSRF, concurrencia, logs. **Auditoría C:** jerarquía y consistencia visual, WCAG axe 2.2 AA, 200 %, cinco tamaños, reflow, teclas, modales, 25 PNG/JSON con hash SHA-256 sobre HEAD exacto. Complementar con [integración E2E real Angular→API .NET10→MySQL8.4](https://github.com/crecisol-platform/CRECISOL/actions); fixture sintético no demuestra aislamiento servidor.

**Regla de cierre:** no declarar F04 CERTIFICADA antes de A/B/C completas, Playwright F04, regresiones F03/F02/C05, backend E2E reales, revisión visual individual y acta con CI del SHA final. Guardar fallos previos como historial, corregir errores P0/P1 antes de aprobación. Rollback por revert controlado de la rama F04; no hay migraciones de datos nuevas.

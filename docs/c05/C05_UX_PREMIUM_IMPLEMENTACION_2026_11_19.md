# CRECISOL · Programa de calidad visual C05 (19-11-2026)

## Alcance, autoridad y estado real

- **Repositorio:** `crecisol-platform/CRECISOL` (ID 1450414592), rama de trabajo `feature/c05-premium-ux-20261119`.
- **Base técnica inmutable al iniciar:** `f61a79d242288c686a88a7c3dd711f2be9a48c18`, HEAD de PR #172, que sigue en DRAFT. Esta rama es **dependiente** de PR #172; no debe fusionarse a `dev` por separado ni aprovecharse para ejecutar migraciones persistentes.
- **Autoridad:** Documento Maestro Único según `docs/MASTER_REFERENCE.md`; gobierno canónico en `.agents/skills/crecisol-project-governance/SKILL.md`; estado vigente en `docs/ESTADO_VIGENTE_CAPITULOS.md`.
- **C01–C05:** se conservan sus certificaciones históricas limitadas; el trabajo presente reabre solo la calidad visual de C05. C06 permanece bloqueado.
- **Permitido:** Angular 22, pruebas Chromium/Playwright, documentación. No hay cambios en `backend/`, `database/`, `main`, Word/PDF maestro, VPS, producción, pagos ni SOLQARYN.
- **Referencias externas de solo lectura:** Impeccable v4.3.1, `pbakaus/impeccable@2149fcce39a90bb409df5f16515f316a76dc6199`, ruta `.agents/skills/impeccable/SKILL.md` y `reference/craft-floor.md`; Angular Developer v1.0, `angular/angular@6808966993bf2b2d455d9b6245e960a59a01b9a9`, ruta `skills/dev-skills/angular-developer/SKILL.md`. Sus scripts no se copiaron, instalaron ni ejecutaron; son criterios de lectura, no autoridad sobre CRECISOL.

## Inventario visual operativo — primera línea base

| Superficie | Archivos Angular principales | Hallazgo en capturas de 18–19 nov | Meta |
| --- | --- | --- | --- |
| Cuenta, recuperación, MFA y navegación | `identity-portal.component.*`, `identity-guest.component.html`, `identity-mfa.component.html`, `identity-recovery.component.html` | Contenedores estrechos, mensajes genéricos y secciones poco diferenciadas | ≥9/10 por pantalla |
| Perfil de empresa e historial | `identity-workspace.component.html`, `identity-flow-views.component.ts` | UUID, códigos y eventos crudos; historial excesivamente largo | ≥9/10 |
| Sesiones y dispositivos de negocio | `identity-workspace.component.html`, `identity-api.service.ts` | Fechas técnicas y revocación desde identificadores | ≥9/10 |
| Llaves WebAuthn | `identity-workspace.component.html` | Claves mostradas como identificadores; revocación escribiendo ID | ≥9/10 |
| Seguridad, condiciones, aprobaciones | `identity-workspace.component.html`, `identity-portal.component.css` | Formulario uniforme, botones navegador y decisiones predeterminadas | ≥9/10 |
| Operador de plataforma: Casos | `platform-identity-panel.component.html`, `platform-operator-cases.component.html` | Estado del expediente técnico y decisión preseleccionada | ≥9/10 |
| Operador: Sesiones, Perfil, Seguridad | `platform-operator-*.component.html` | Presentación textual cruda y jerarquía débil | ≥9/10 |
| Operador: Monitor de abuso | `platform-operator-monitor.component.html` | Lista escasa sin tratamiento de indicadores | ≥9/10 |
| Modales compartidos y confirmaciones | `ui/institutional-modal/*` | Scroll del diálogo completo, duplicación de títulos y avisos | ≥9/10 |
| MFA TOTP-SHA1, QR y política de inactividad | PR #172, `identity-mfa.component.html`, `platform-operator-security.component.html` | Debe integrarse sin revelar semillas ni cambiar periodos de 30 s | ≥9/10 |

La lista es un **inventario inicial de las superficies revisadas en código y capturas**. No se declara exhaustiva frente a rutas y estados de usuario aún no probados en navegador. La puntuación inicial es cualitativa; no constituye medición de la versión vigente de PR #172.

## Primera entrega implementada en rama dependiente

1. Presentación de actividad, fechas, sesiones y llaves sin imprimir IDs completos ni códigos internos en la lectura principal; el backend sigue teniendo las referencias exactas.
2. Tarjetas diferenciadas de perfil, sesiones, historial, llaves e indicadores agregados; mensajes de estado vacío que no inventan datos.
3. Selección de llave registrada para revocación; el servidor continúa validando propiedad y MFA.
4. Decisiones administrativas y de identidad sin aprobación preseleccionada.
5. Modal institucional con encabezado y pie fijos, región central desplazable, foco inicial en título, restitución de foco y bloqueo/restitución segura del scroll del documento.
6. Mensajes de éxito contextuales por operación, en lugar de una frase única para cambios de credenciales.
7. Navegación de cuenta móvil con pestañas desplazables y condiciones con controles institucionales.
8. Pruebas Playwright nuevas para reflow del modal, foco, accesibilidad, datos legibles y selección de llaves.

## Criterio de aceptación sin notas artificiales

Cada superficie y cada estado crítico se evalúa individualmente sobre 10 puntos:
identidad y composición 20%; tipografía y jerarquía 15%; interacción 20%; mensajería/estados 15%; responsive/accesibilidad 20%; consistencia 10%.

**NO-GO** si cualquier pantalla esencial obtiene <9.0/10, si existe P0/P1, si hay fallo de teclado, foco, WCAG AA, desbordamiento, mensajes engañosos, fugas de secreto, retroceso de MFA, autorización/tenant, o regresión de una operación existente. Una media general ≥9.0 **no sustituye** las notas individuales.

Matriz de revisión por ancho: 320, 375, 390, 768, 1024 y 1440 px; estados de carga, vacío, error, éxito y operación irreversible; pruebas de teclado, zoom, reduced-motion, forced-colors y lector de pantalla. La puntuación estética requiere capturas del navegador real con aceptación humana; no se certifica por leer CSS ni por CI verde únicamente.

## Puertas de integración / bloqueos

- **Auditoría A:** contratos, casos de uso y estados UI intactos.
- **Auditoría B:** MFA, privacidad temporal, limpieza de QR/códigos, revocación, IDOR/BOLA, roles de plataforma y multitenancy.
- **Auditoría C:** Angular 22 / CSS / Chromium, accesibilidad, rendimiento, mobile, consistencia, P0/P1 y rollback.
- **D/E complementarias:** evaluación visual humana y Angular → API .NET 10 → MySQL 8.4 para el SHA final.
- **Dependencia de PR #172:** no fusionar rediseño, no ejecutar corte C05-017/018 persistente ni actualizar datos del propietario mientras la combinación de MFA, backup SQL+llaves, pruebas físicas y aceptación de interfaz continúe pendiente.

### Evidencia disponible y no ejecutada

- Lectura real del código y referencias en GitHub: realizada.
- Cambios de Angular y prueba Playwright: versionados en la rama de trabajo.
- Comprobación sintáctica básica de balance de etiquetas HTML en los componentes afectados: realizada por inspección de archivos, **no equivale a compilación Angular**.
- `npm run build`, `tsc`, Playwright/Chromium, axe, .NET 10, MySQL 8.4, pruebas de MFA físico, capturas actuales y valoración 9/10: **NO EJECUTADAS todavía en este cambio**. No atribuir a este SHA ejecuciones anteriores de PR #172.
- Entorno local de la sesión de trabajo: Node 22 disponible; Node 24 y `.NET` no disponibles; acceso directo GitHub desde terminal no disponible. No afirmar pruebas ejecutadas.

**Estado:** implementación parcial trazable, SIN CERTIFICACIÓN UX 9/10 y sin fusión. Revisar CI propia del SHA final, capturas y auditorías antes de la aceptación.

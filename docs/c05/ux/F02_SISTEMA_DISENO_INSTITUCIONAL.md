# CRECISOL · C05 UX F02 — Sistema institucional premium (contrato técnico)

**Fase:** 02, después del cierre limitado de inventario/línea base F01, PR #174.  
**Objetivo posterior, no medido en F02:** ≥9/10 para cada pantalla de C05 al terminar la última fase del plan visual.  
**Repositorio autorizado:** crecisol-platform/CRECISOL (ID 1450414592).  
**Rama de F02:** feature/c05-ux-f02-sistema-institucional-20261119.  
**Base:** f61a79d242288c686a88a7c3dd711f2be9a48c18, PR #172 (MFA TOTP SHA1 y sesiones configurables).  
**Dependencia futura:** PR #173 contiene trabajo visual anticipado. No fusionarlo de manera independiente; su adopción de los componentes de F02 debe programarse y auditarse en las fases siguientes. F02 no altera esa rama.

## 1. Autoridad y límites

Documento Maestro Único de CRECISOL (docs/MASTER_REFERENCE.md), estado de C05 en docs/ESTADO_VIGENTE_CAPITULOS.md y skill GitHub .agents/skills/crecisol-project-governance/SKILL.md. C01/C02 aportan identidad editorial y patrones de experiencia, preservados; C05 agrega componentes reutilizables bajo un único prefijo. La identidad publicada proviene del servicio same-origin PublicBrandService y del contrato validado parsePublishedPlatformBrand. No se autorizan bibliotecas de componentes ajenas, fuentes remotas, iframes, rutas públicas de pruebas, almacenamiento del navegador, scripts de cliente ni personalización que determine permisos.

**Scope lock:** exclusivamente archivos de la capa F02 frontend/src/app/ui/institutional-system, su declaración de estilos en frontend/angular.json, harness aislado, Playwright, auditor de fuentes, workflow y actas F02. **Cero modificaciones** a frontend/src/app/pages, app.routes.ts, servicios de identidad, frontend C01/C02, backend, database, DTO, APIs, secretos, main, dev, SOLQARYN, VPS, cobros y producción.

## 2. Sistema de tokens (contrato central)

Archivo: frontend/src/app/ui/institutional-system/institutional-tokens.css, cargado primero desde frontend/angular.json sin imports remotos. Definidos por roles semánticos, no por detalles de un componente:

| Dominio | Ejemplos de tokens | Criterio |
| --- | --- | --- |
| Identidad / color | --cs-brand-primary, --cs-brand-accent | Solo decoración; nunca cambiar color de texto o CTA con colores de tenant |
| Superficies | --cs-color-canvas, --cs-color-surface, --cs-color-surface-soft | Jerarquía visual y densidad equilibrada |
| Texto | --cs-color-text, --cs-color-muted, --cs-color-text-strong | Legibilidad y contraste AA |
| Acción | --cs-color-action, --cs-color-action-hover, --cs-color-action-text | Botón de acción protegido, contraste fijo |
| Estados | --cs-color-success, --cs-color-warning, --cs-color-danger, --cs-color-info y fondos | Mensajes contextuales con roles ARIA |
| Tipografía | --cs-font-system, --cs-font-humanist, --cs-font-editorial, --cs-font-body, --cs-text-* | Solo tipografías locales/sistema, selección enumerada |
| Espaciado | --cs-space-1 a --cs-space-12 | Escala de 4 px equivalente a .25 rem en base 16 px |
| Geometría | --cs-radius-*, --cs-border-width, --cs-hit-target | Superficies suaves, objetivo táctil ≥44 CSS px |
| Elevación | --cs-shadow-card, --cs-shadow-raised, --cs-shadow-overlay | Diferenciar capas sin sombras invasivas |
| Movimiento | --cs-motion-fast, --cs-motion-standard, --cs-motion-ease | Microinteracciones; 0 ms si se pide movimiento reducido |
| Accesibilidad | --cs-color-focus, forced-colors | Foco visible, colores de sistema en alto contraste |

### Parametrización institucional segura

Directiva InstitutionalThemeDirective: atributo csInstitutionalTheme. Recibe exclusivamente PublishedPlatformBrand validado, nunca un objeto CSS arbitrario. Revalida que primaryColor y accentColor sean HEX estrictos #RRGGBB y descarta cualquier sintaxis adicional; elige solo tipografía system/humanist/editorial e icono outline/solid, enumerados. Esta parametrización se limita al elemento anfitrión data-cs-theme=institutional; no cambia variables globales y no presenta identidad de otro tenant por defecto.

La paleta del backend nunca sustituye --cs-color-action, --cs-color-danger, --cs-color-text ni --cs-color-focus, por lo que un color institucional demasiado claro u oscuro no hace ilegibles las acciones sensibles. El servidor debe seguir validando identidad, permisos y ámbito: la UI no actúa como una barrera de autorización.

### Iconografía

InstitutionalIconComponent: 13 iconos locales de SVG fijo para seguridad, acceso, estados y navegación. No recibe URL, etiqueta HTML, SVG sin procesar ni permite ejecutar contenido externo. Sus trazos se dimensionan con tokens; son ornamentales (aria-hidden=true), por lo que el botón/etiqueta conserva su nombre accesible.

## 3. Componentes Angular reutilizables

Todas las piezas usan Angular 22 standalone, Signals mediante input/output/computed y ChangeDetectionStrategy.OnPush, templates y CSS propios.

| Elemento | Selector | API y comportamiento |
| --- | --- | --- |
| Navegación | cs-ui-navigation | items, activeId, ariaLabel, selected; cambio de vista es intención y no concede acceso |
| Tarjetas | cs-ui-card | heading, headingId, headingLevel, eyebrow, description, tone, slot cs-card-actions, proyección libre |
| Campos/formularios | cs-ui-field | controlId, label, required, hint, error; label nativo y descripción/error enlazables |
| Tablas | cs-ui-table | caption, columns, rows, scopeKey, rowsScopeKey; solo lectura, escape de texto y fail-closed al cambiar de contexto |
| Avisos | cs-ui-notice | kind info/success/warning/error; roles status/alert, icono local, copia desde host |
| Estados | cs-ui-state | empty/loading/error/success, heading, message, actionLabel, action; movimiento reducido |
| Acciones | cs-ui-action | type button/submit/reset, variant primary/secondary/quiet/danger, disabled, busy, label, icon, activated |
| Iconos | cs-ui-icon | allowlist de 13 glyphs, sin recurso externo |

**Datos:** la aplicación debe pasar filas ya autorizadas y listas para su presentación; el componente de tabla no consulta, clasifica permisos, exporta, almacena ni identifica tenants. El par scopeKey/rowsScopeKey protege exclusivamente contra la visualización accidental de filas antiguas durante un cambio de ámbito; no sustituye IDOR/BOLA del backend.

**Formularios:** cada input proyectado requiere id idéntico a controlId, clase cs-ui-control, y aria-describedby dirigido a controlId-hint y controlId-error si existen. Cada formulario permanece bajo responsabilidad de la vista y de la validación backend. El componente no guarda ni envía datos.

**Acciones destructivas:** se expresan con variant danger y el host debe exigir una confirmación independiente, MFA, autorización y bitácora cuando corresponda. Ninguna primitiva decide si una acción está permitida; disabled se limita al estado visual.

## 4. Relación con C01 y C02, sin duplicar su comportamiento

Se conserva íntegramente el sistema C01 de identidad pública, publicación de marca y recursos same-origin; no se mezclan marcas de empresa y global. Se mantienen los patrones C02 de borradores y tablas sintéticas, incluidos sus contratos safeKey/validColumns. La tabla institucional reutiliza esa validación estática pero elimina los textos de "ejemplo" codificados en el patrón C02 para permitir contenido empresarial autorizado en fases posteriores.

Las pantallas C05 y sus 27 modales quedan como estaban mientras se establecen los componentes. La migración incremental de formularios, lista de sesiones, historial, navegación, avisos y modales corresponde a las fases posteriores de rediseño. La base F02 no carga nuevas bibliotecas, no cambia endpoint ni contrato de TOTP-SHA1, ni modifica la caducidad actual administrable de sesiones a 30 minutos de inactividad.

## 5. Banco de pruebas visual sintético (sin ruta productiva)

- Entrypoint de pruebas: frontend/tests/institutional-harness/main.ts.
- Compilación separada: frontend/tsconfig.institutional.json y build/serve institutional-harness en frontend/angular.json.
- Puerto de desarrollo aislado: 127.0.0.1:4202.
- Capturas y auditorías: frontend/tests/c05-f02-design-system.spec.ts, ejecutado solo con frontend/playwright.institutional.config.ts.
- Widths 320, 390, 768, 1024, 1440; 200 % de texto, forzados, prefers-reduced-motion, axe WCAG 2.2 AA.
- Negativos: color de marca con sintaxis de URL, HTML malicioso presentado como texto en tabla, registros de ámbito anterior, botones busy/disabled, sin peticiones a API, sin network externo.
- Contraste estático: qa/c05_ux_f02_design_system_audits.py B comprueba >=4.5:1 en pares semánticos de texto normal.

### Criterio A/B/C de F02

A — contratos, estructura, cobertura de primitivas, tokens y no regresión de C01/C02/C05.  
B — XSS, marcas publicadas, separación de ámbito, permisos no atribuibles al frontend, no credenciales ni peticiones.  
C — compilación Angular, render en 5 widths, axe, keyboard/focus, zoom, forced-colors/reduced-motion, coste de bundle y diferencia de estilos.

F02 alcanza **IMPLEMENTADA** con código versionado; **PROBADA** con CI sobre SHA final y revisión de capturas; **CERTIFICADA** solo cuando A/B/C pasan, no hay P0/P1 y se registra acta precisa. La nota global final ≥9/10 solo corresponde al término del programa visual, no a F02.

## 6. Integración y reversión

El PR F02 se abre como borrador dependiente del candidato de C05 (PR #172), no como fusión a dev. Mantener PR #174 de F01 como antecedente documental y PR #173 de las otras mejoras sin cambios hasta planificar su dependencia. Rollback de F02: descartar su rama/PR, revertir la entrada de tokens en frontend/angular.json y eliminar los archivos nuevos de UI, QA y docs; sin migraciones, datos o side effects en cuentas. No se despliega ni se habilita un endpoint.

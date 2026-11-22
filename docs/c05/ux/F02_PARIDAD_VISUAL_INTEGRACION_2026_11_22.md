# CRECISOL C05 — F02 · Corrección de paridad visual en la integración de 65 superficies

**Fecha:** 22-11-2026. **Alcance actual:** F02, sistema institucional y adopción de estilos en pantallas, formularios, modales, mensajes y consola. **No es certificación comercial de 9.5/10.**

## 1. Autoridad, rama y dependencia

- Repositorio único autorizado: `crecisol-platform/CRECISOL`. Documento Maestro Único, `AGENTS.md`, `docs/MASTER_REFERENCE.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md` y skill de gobernanza vigente consultados en `dev`. C05 continúa siendo capítulo técnico activo de los ajustes UX; C06 y F07 siguen bloqueados.
- F01 inventario PR #174: `01efbce91586597006254f721e5736adf991e2a4`, conciliación 65/65.
- F02 biblioteca institucional PR #175: `f9dd2350788bd19611687c17ec4d85bf0b949181`, CI de primitivas y matriz aprobada en #45038671231, pero sin aceptación comercial.
- F06 producto probado PR #179: **`d539b75d13cde64c8ea3279d33ab35b6349c1adf`**; [inventario 65×5 #45032597168](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597168) y [E2E #45032597178](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597178), históricamente SUCCESS en el SHA original.
- **Nueva rama de integración exclusiva:** `feature/c05-ux-f02-paridad-visual-integracion-20261122`, creada desde el SHA F06 exacto, para probar correcciones F02 contra el producto completo sin editar retroactivamente #175 ni #179 ni mezclar capítulos. Su PR será DRAFT contra la rama F06, sin fusión a `dev`/`main`.

## 2. Hallazgo confirmado y cambios realizados

La auditoría anterior demostró que F02 tenía 9 primitivas y tokens, pero solo 3 primitivas visibles directamente en las 11 plantillas fuente de las 65 superficies. Se observaron **377 literales HEX** y **93 referencias de tokens** en seis CSS (no 377 fallos). El objetivo aquí es uniformar roles semánticos sin forzar reemplazos de controles con permisos/estados propios.

| Área | Corrección concreta | Riesgo que se conserva |
| --- | --- | --- |
| Mensajería `operation-feedback.component.css` | `info`, `success`, `error` y `pending` heredan color, borde y fondo de tokens F02; contexto, mensajes de paso siguiente, iconos y movimiento mantienen diferenciación semántica | No inferir éxito desde CSS, conservar aria-role de status/alert, no desvanecer contraste |
| Modales `institutional-modal.component.css` | Superficie, sombra, borde, foco, cancelación, feedback, advertencias, pasos y destructivas referenciados a tokens F02 | El diálogo sigue siendo nativo y exclusivo; scroll interior, footer visible, foco y MFA sin cambios |
| Operadores `platform-operator-f04.component.css` | Tarjetas, lecturas, indicadores, etiquetas, advertencias, enlaces y objetivos táctiles alineados con colores y medidas de F02 | Las 5 áreas operativas conservan comandos, filtros, permisos, sesiones y controles intactos |
| Formularios `identity-flow.component.css` | Campos, etiquetas, botones, foco, resumen y campos con `aria-invalid=true` usan tokens institucionales; se añade estado de error claramente visible y forced-colors | Los nombres, `required`, autocompletado, MFA y reglas del backend no cambian |
| Pruebas Chromium | En las **30 escenas existentes de feedback/formulario/modal a 5 anchos** se verifica el color CSS calculado contra los tokens reales F02, además de geometría y axe; se preserva el inventario independiente 65×5 | No convertir evidencia sintética en dictamen humano, ni en autorización backend |

**Colores de respaldo:** valores HEX aún presentes como fallback de `var(--cs-*, #...)` no son colores alternativos en el runtime con tokens cargados. No se ha aplicado búsqueda y sustitución indiscriminada sobre todos los literales de CSS, para evitar alterar estados especiales de MFA, QR, acciones sensibles o páginas C01/C02.

## 3. Auditorías y criterios

- **A — Funcionalidad/alcance:** se exige un diff acotado entre F06 SHA y el nuevo HEAD, 65 IDs preservados, 11 plantillas fuente y pruebas de código reales; no se aceptan cambios en TS de negocios, API, base de datos ni servicios.
- **B — Seguridad y privacidad:** CSS sin imports externos, recursos dinámicos ni ejecución; funciones y permisos de backend no se tocan; sesiones por inactividad 30 minutos, recuperación, WebAuthn, MFA y X-Tenant permanecen como estaban. El test verifica errores, acciones protegidas, scroll y rol semántico; ningún campo se elimina.
- **C — Arquitectura, accesibilidad y UX:** tokens coherentes en formularios, consola, modales y feedback; `aria-invalid`, `focus-visible`, `forced-colors`, `prefers-reduced-motion`, 320/390/768/1024/1440 y comparación real de estilos calculados mediante Playwright. Ejecutar regresiones F02/F03/F04/F05/F06 e inventario 65×5 en Chromium. Ejecutar integración Angular→.NET 10→MySQL 8.4 de forma independiente tras las UI.

El auditor `qa/c05_ux_f02_visual_integration_audits.py` ejecuta A/B/C de **fuente**; los resultados Chromium y MySQL deben constar en su run real. El workflow de rama es `.github/workflows/c05-ux-f02-paridad-visual.yml`. Si falla un gate, no aprobar ni fusionar.

## 4. Dictamen comercial y cierre

**Estado actual: NO-GO comercial.** La nota final ≥9.5/10 se exige en **cada una de las 65 interfaces**, no por promedio. Después de corregir el código se requiere: capturas PNG individuales de estados, scroll completo, escritorio/tableta/móvil, uso real de teclado/lector cuando sea aplicable, revisión comercial individual y acta de aceptación. Las notas exploratorias de un SHA anterior (8.7–9.4) no son nota definitiva del nuevo HEAD. La matriz F01 y F02 de correspondencia no debe marcar ninguna nota final sin evidencia.

**Prohibiciones:** no fusionar PRs de la cadena #172→#174→#175→#176→#177→#178→#179 por este cambio, ni ejecutar despliegues, configurar VPS/DNS, activar cobros, modificar SOLQARYN, `main`, `dev`, el Maestro, backend ni database.

**Rollback:** cerrar el PR de integración o revertir solo los archivos CSS, test, auditor y workflow de esta rama; no hay migraciones ni datos persistentes que revertir.

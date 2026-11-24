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

---

## 5. Defecto real descubierto por Chromium y corrección adicional

En la primera ejecución del workflow de la rama (GitHub Actions **#45041325116**, SHA `60b9758...`) los cinco escenarios **field-error** fallaron, mientras la auditoría de fuente y compilación Angular habían pasado. Chromium midió el borde inválido como `rgb(179,84,66)` (color heredado de C05 F06 `#b35442`) y esperaba el token F02 de peligro `rgb(139,39,46)` (`#8b272e`). El fallo fue reproducible a **320, 390, 768, 1024 y 1440 píxeles** y se conserva como evidencia histórica; **no se marcó PASS**.

**Causa:** `frontend/src/styles.css` tenía una regla global estrictamente C05, `form[csFieldFeedback] [aria-invalid="true"]`, que establecía `border-color: #b35442 !important`, prevaleciendo sobre la regla aislada del componente. No era un problema del inspector: era una inconsistencia visual real.

**Solución de causa raíz:** únicamente en el bloque C05 `form[csFieldFeedback]` del CSS compartido, se sustituyen **cinco valores** de color/borde/radio por `--cs-color-danger`, `--cs-color-danger-bg`, `--cs-color-danger-border` y `--cs-radius-sm`. El `!important` existente se conserva para no alterar las prioridades funcionales del formulario; ahora su color es el institucional. **No se cambian reglas de C01/C02 ni HTML, validaciones, permisos o datos.** El auditor A impone exactamente cinco líneas nuevas con tokens dentro del archivo global y no permite otras alteraciones silenciosas.

La siguiente CI debe ejecutar nuevamente los cinco escenarios y las 65 superficies; esta adenda no presume su resultado ni otorga calificaciones humanas de 9.5/10.

---

## 6. Segunda ronda editorial — remediación de F01-005, F01-041 y F01-048

**Hallazgo fuente:** tras [CI técnica previa #45041715189](https://github.com/crecisol-platform/CRECISOL/actions/runs/45041715189), 2/2 SUCCESS en SHA `af083c4d7fdc782f0db6fb6f5e0cdd5060a7c980` (374 Playwright, 340 E2E, 65×5 y 447 capturas), la inspección directa del PNG a 320 píxeles detectó dificultad editorial en:

| ID | Evidencia anterior | Cambios actuales, sin modificar seguridad |
| --- | --- | --- |
| F01-005 invitación | La introducción y dos pasos obligatorios daban un formulario alto; no era aceptable ocultar consentimiento o MFA | Mensaje más concreto sobre token/autenticador, ritmo de etapas ajustado, nombre de ruta preservado, checkbox de aceptación y carga de condiciones intactos |
| F01-041 revisión independiente | El cuerpo del modal a 320px tenía ~518px de desplazamiento; advertencia extensa y cinco campos separados por demasiada separación. 2 checks y MFA son obligatorios | Advertencia concisa mantiene registro permanente y segundo operador; campos agrupados como pares etiqueta/control, cinco requeridos; diseño a 1 columna en 320 y 2 en 768; dos declaraciones obligatorias visibles, sin bypass de revisión |
| F01-048 correo operador | Ambas operaciones obligatorias aparecían en una hoja alta, difíciles de distinguir | Rail visual de dos etapas en el mismo modal, espaciados equilibrados, ambos formularios y códigos MFA conservados, sin colapsar el segundo o perder el campo token |

**Pruebas adicionales:** tres Playwright explícitas del comprador en `frontend/tests/c05-f06-context-feedback.spec.ts` verifican las 3 superficies, 320/768px, dos confirmaciones obligatorias, los 5 campos requeridos, patrón de invitación y los cuatro campos del cambio de correo. El auditor A/B/C agrega a whitelist únicamente las dos plantillas HTML (`identity-guest` y `platform-operator-cases`) y bloquea borrado de campos críticos.

**Declaración de alcance:** lo anterior son cambios presentacionales y de microcopy. Las dos acciones de correo y la revisión de identidad siguen siendo operaciones separadas que requieren autorización real en backend; el cliente no decide permisos. No se ha retirado ningún consentimiento, MFA, espera de recuperación, evidencia SHA-256 ni campo de auditoría.

**Dictamen de esta ronda:** nuevas pruebas y artefacto deben terminar en el **SHA posterior a estos cambios** para evaluar los resultados. No extrapolar el SUCCESS del SHA anterior al nuevo. La aprobación editorial individual 65/65 ≥9.5 continúa pendiente y no se reemplaza por estilo calculado, pruebas sintéticas o esta nota. No fusionar el PR mientras falten gates y aceptación final.

---

## 7. Tercera ronda: dictamen de comprador actualizado y cuatro remediaciones prioritarias

**Base editorial y huellas:** sobre el SHA técnico final de la segunda ronda `7a355be2a8c25c760c3716c8ab6d4097a12b856c` se inspeccionaron los **65** primeros pliegues móviles de 320 px, los **65** equivalentes de escritorio 1440 px y, para los flujos sensibles, capturas desplazadas y tableta. El ZIP original `14763843993` contiene 65/65 IDs para cada uno de cinco anchos, 444 PNG/JSON, sin desbordamientos reportados. No se finge inspección humana completa con lector de pantalla ni datos reales.

**Resultado editorial IA inicial de esa versión (NO acta institucional):** 12/65 valoraciones *visuales preliminares* ≥9.5, 53/65 por debajo, rango 8.9–9.5 y mediana 9.3. Las 65 notas y riesgos específicos quedaron registrados en el [comentario del PR #180](https://github.com/crecisol-platform/CRECISOL/pull/180#issuecomment-7084811307). Este resultado no autoriza 65 aprobaciones ni reinterpreta las notas históricas 8.7–9.4 del SHA 98caa....

### Correcciones sobre la versión posterior a la evaluación de compra

| F01 | Hallazgo | Corrección sin debilitamiento de contratos |
| --- | --- | --- |
| **006** Nueva relación con cuenta existente | Cinco verificaciones + condiciones hacen larga la página móvil | Mensaje inicial directo, ritmo de etapas e identificación segura; se preservan cuenta/contraseña/MFA/ID de confirmación/código de correo/consentimiento |
| **033** Revisión de membresía | Contexto, decisión sensible y MFA sobre una única columna incluso en tableta | Campos organizados, advertencia abreviada semánticamente, una columna en 320 y dos a partir de 700; se mantienen selección no predefinida, motivo, MFA y consentimiento explícito |
| **036** Aprobar otro navegador | Huella, solicitud, MFA y advertencia desplazaban la acción en móvil | Advertencia más concisa y comparables agrupados; token largo ocupa ancho completo en tableta; huella, MFA y confirmación independiente obligatorios |
| **040** Localizar referencia protegida | Explicación y cuatro campos sensibles demasiado dispersos | Párrafo de privacidad breve, grupos legibles y distribución 1→2 columnas; servidor conserva autorización, ámbito, motivo y MFA |

**QA adicional:** cuatro nuevas pruebas Playwright con fixtures sintéticos de solo lectura para preservar controles `required` y flujo a 320/768. El auditor de fuente A/B/C falla cerrado si se tocan rutas o ficheros no enumerados; incluye `identity-workspace.component.html/css` además de los 11 archivos ya auditados, sin cambio en backend, database o contraseñas. El workflow de la rama ejecuta regresiones de F02–F06 y Angular→.NET 10→MySQL 8.4 en base desechable. **Los resultados del SHA anterior no se extrapolan a los commits posteriores.**

**Gate de certificación integral:** seguir **NO-GO** hasta verificar la CI del SHA nuevo, regenerar evidencias individuales, revisar nuevamente las 65 interfaces y demostrar un mínimo de ≥9.5/10 por interfaz, con aprobación editorial real. No fusionar PR #175/#179/#180 ni activar F03/F07/C06 por esta adenda.


---

## 8. Ronda de homologación integral de patrones — aplicación sin modificar controles de identidad

**Base**: dictamen editorial orientativo de 65 vistas sobre SHA \`423aada6e420c3698dabd247ba76fe59d9428fd4\` (12 candidatas ≥9.5, 53 con mejoras pendientes). La nota subjetiva no sustituye un ensayo de usabilidad.

**Correcciones transversales de causa común**: (1) modal seguro con área útil mejor aprovechada a 320–480px, manteniendo encabezado, advertencia completa, foco, cuerpo desplazable y pie fijo; (2) invitación, relación y confirmación con etapas delimitadas mediante el mismo azul y fondo institucional, sin eliminar términos, contraseña ni MFA; (3) alternativa passkey claramente secundaria respecto de login convencional; (4) lanzadores de tareas de cuenta y de operador de tamaño táctil seguro, alineados y sin filas vacías excesivas. Se conservaron el contraste forzado y la reducción de movimiento.

**Pruebas adicionales**: cinco Playwright (320/390/768/1024/1440) de geometría de diálogo con la CTA protegida, botón Cerrar, ausencia de clip horizontal y cabecera separada del pie. Se mantienen inventario 65×5, 3 auditorías A/B/C, todas las regresiones y Angular→.NET10→MySQL8.4 real en entorno desechable.

**Certificación**: un resultado técnico verde en el nuevo SHA permite certificar solo la remediación en su alcance. La valoración final por cada una de las 65 experiencias y el umbral comercial ≥9.5 no se deben conceder automáticamente por CSS, CI o por solicitud de cierre. Se exige nueva evidencia en el SHA resultante, revisión visual, y acta explícita sin P0/P1.

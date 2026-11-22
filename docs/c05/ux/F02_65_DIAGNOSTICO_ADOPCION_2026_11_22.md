# CRECISOL · F02 — Diagnóstico de adopción visual en las 65 superficies (22-11-2026)

**Dictamen:** biblioteca F02 técnicamente certificada históricamente, **uniformidad de las 65 superficies NO CERTIFICADA**. El criterio comercial vigente es **≥9.5/10 en cada una de 65**, no el ≥9.0 histórico que figura en la especificación original. No confundir Angular PASS con experiencia de comprador aprobada.

## Identidad de los documentos

- Proyecto y rama de trabajo: `crecisol-platform/CRECISOL`, PR #175, `feature/c05-ux-f02-sistema-institucional-20261119`, base de la familia F01/F02: `f61a79d242288c686a88a7c3dd711f2be9a48c18`.
- F01 histórico reconciliado: PR #174, `01efbce91586597006254f721e5736adf991e2a4`; su matriz original se conserva por blob `a1ff218baf489d69104145048f94e7ff9ee48f73`.
- Producto F06 bajo revisión: PR #179, SHA **`d539b75d13cde64c8ea3279d33ab35b6349c1adf`**, sin fusionar. Catálogo `F06_65_SUPERFICIES_VALORACION.csv` en blob `1529df2257b75a9508f1d2276080bea31c9be7b3`.
- Evidencia actual del producto sobre ese SHA: [inventario #45032597168](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597168) SUCCESS (65 IDs × cinco anchos, artefacto [#14758472442](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597168/artifacts/14758472442)); [frontend y E2E #45032597178](https://github.com/crecisol-platform/CRECISOL/actions/runs/45032597178) SUCCESS (344 Playwright y 340 comprobaciones Angular → .NET10 → MySQL8.4). No extrapolar a un HEAD posterior.

## Auditoría transversal: estado comprobado

La [matriz individual de 65 filas](F02_65_PARIDAD_VISUAL_2026_11_22.csv) conserva ID, tipo, nombre, ruta, fichero Angular y su blob SHA, componentes F02 encontrados **en la plantilla** (no se supone ejecución en cada variante), presencia de modal F05, feedback F06, CSS relevante, ocurrencias de colores y tokens, hallazgo editorial exploratorio y campos de valoración final **vacíos**.

| Comprobación | Resultado de fuente, no certificado comercial |
| --- | --- |
| Superficies reconciliadas con F01/F06 | **65/65**; 17 vistas, 18 estados, 3 complementos y 27 modales |
| Plantillas Angular distintas del catálogo | **11** |
| Herencia de tokens y tema | Tokens de F02 cargados en `angular.json`; raíz de /cuenta aplica `[csInstitutionalTheme]="publishedTheme()"` |
| Uso directo visible de primitivas F02 | Solo `cs-ui-navigation`, `cs-ui-card` y `cs-ui-state` en las 11 plantillas |
| Familias de primitivas sin uso directo visible | `cs-ui-field`, `cs-ui-form`, `cs-ui-table`, `cs-ui-notice`, `cs-ui-action`, `cs-ui-icon`; **no** implica que sus funciones no existan en componentes F05/F06 |
| IDs cuyo archivo fuente contiene algún `cs-ui-*` | **31/65**. Es un análisis de **plantilla**, no confirma que cada estado dinámico despliegue esa primitiva |
| IDs cuyo archivo fuente no contiene `cs-ui-*` | **34/65**; usan controles nativos y/o componentes posteriores, no necesariamente sin identidad visual |
| CSS transversales revisados | **6 archivos**: `src/styles.css`, portal, flujo, operadores, modal y feedback |
| Colores HEX escritos directamente en estos seis archivos | **377 ocurrencias**, no 377 fallos ni 377 valores únicos |
| Referencias `var(--cs-*)` en los seis CSS | **93** |
| Notas comerciales definitivas emitidas | **0/65**; se conservan 65 decisiones `PENDIENTE` |

### Distribución de la deuda CSS por archivo de producción F06

| Archivo | HEX directos | Referencias a tokens F02 |
| --- | ---: | ---: |
| `frontend/src/styles.css` | 85 | 12 |
| `frontend/src/app/pages/identity-portal.component.css` | 94 | 65 |
| `frontend/src/app/pages/identity-flow.component.css` | 91 | 16 |
| `frontend/src/app/pages/platform-operator-f04.component.css` | 44 | **0** |
| `frontend/src/app/ui/institutional-modal/institutional-modal.component.css` | 45 | **0** |
| `frontend/src/app/ui/feedback/operation-feedback.component.css` | 18 | **0** |
| **TOTAL** | **377** | **93** |

Los literales pueden ser válidos (ej. ilustraciones, bordes y estados particulares); no deben convertirse a ciegas: primero se compara el contraste, el foco, la semántica de peligro, el modo de alto contraste, los cinco tamaños y la representación de la operación. Una coincidencia nominal de colores no prueba paridad visual entre vistas, y una diferencia justificada no supone defecto.

## Riesgos priorizados de F02 para las fases de integración

1. **Operadores:** su hoja contiene 44 colores directos y ninguna referencia `--cs-*`. Verificar jerarquía, navegación, estados protegidos y coherencia con tarjetas del ámbito empresarial sin introducir permisos nuevos.
2. **Modales y formularios:** 45 colores directos en hoja de modal, cero tokens F02 directos en ella. Comprobar prioridad de CTA, peligro, campos, foco, scroll de pie y reflujo en los 27 modales inventariados (la plantilla actual puede contener instancias adicionales, sin reinterpretar el inventario).
3. **Mensajería y estados:** la hoja F06 de feedback tiene 18 colores directos y ninguna referencia directa a `--cs-*`; contrastar `info/success/warning/error/pending` sin emitir éxito antes del recibo del servidor.
4. **Vistas públicas y cuenta:** el flujo comparte plantilla CSS pero su adopción de primitivas F02 no es homogénea. Revisar especialmente las nueve superficies prioritarias `003/005/006/033/036/039/040/041/048` sin reducir condiciones, MFA, pruebas de correo, ni controles de recuperación.
5. **Prueba por estado real:** conservar las barreras semánticas `014` (condiciones cargadas) y `057` (historial vacío real) que evitan acreditar una pantalla sustituta.

**No se modifica el código visual de la rama F06 en esta microfase F02.** La mejora de interfaces se debe validar sobre el SHA final del producto y mediante sus propios PR/CI, no atribuirla a este inventario. Cualquier migración de colores o componentes exige comprobar compatibilidad y pruebas de regresión de C05: navegación, formularios, MFA, foco, privacidad, tenant/realm y estados de error.

## Pruebas y criterios de cierre

Se incorpora el auditor de solo lectura `qa/c05_ux_f02_65_parity_audits.py` a la [CI F02](../../../.github/workflows/c05-ux-f02-design-system.yml). El programa carga *por SHA y Git blob* ambas ramas, exige 65 filas, reproduce el listado de primitivas visibles, obtiene cada recuento CSS y **rechaza notas finales fabricadas**. Ejecuta tres auditorías A (trazabilidad), B (alcance de seguridad/privacidad y negativas de falso positivo) y C (consistencia de matriz y deuda CSS), además de los tests originales de F02.

Las pruebas F02 documentales y de primitivas no comprueban nuevas autorizaciones backend, SQL de dos tenants, MFA de dispositivo real, accesibilidad con lector físico, comportamiento completo de 65 estados, estética objetiva ni aprobación del propietario. El CI F06 anterior sí probó escenarios técnicos en su alcance, pero no compra 9.5.

**Decisión:** GO solo para inventario de adopción y contrato de consistencia de la F02; **NO-GO para afirmar 65/65 experiencias uniformes o premium** hasta intervención visual, QA final y valoración individual. Mantener #175 DRAFT, no fusionar #172/#174–#179 por esta matriz; no abrir F07/C06, no modificar `dev`, `main`, backend, base de datos, producción, Maestro Único ni SOLQARYN.

**Rollback:** revertir exclusivamente los archivos F02 de matriz, auditoría y workflow/acta. No hay migraciones ni efectos en identidades, API o datos persistentes.

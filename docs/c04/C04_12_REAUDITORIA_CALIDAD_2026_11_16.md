# C04 — Reauditoría correctiva de seguridad y calidad tras C03

**16-11-2026. Repositorio:** `crecisol-platform/CRECISOL`. **Base comprobada de `dev`:** `e70e3e68da66ea309b5c625261b7d8f431305c51`. **Rama:** `feature/c04-retro-quality-20261116`. **Estado:** propuesta de corrección, **no certificada hasta pruebas y auditorías A/B/C del SHA exacto**.

## Orden y referencia de calidad

La petición actual establece el orden obligatorio **C03 → C04 → C05**. El corte anterior C03 de PR #158 fue integrado en `dev`; los **8/8 workflows POSTMERGE completed/success** se verificaron sobre `e70e3e68da66ea309b5c625261b7d8f431305c51` y constan en su comentario institucional. Este PR interviene solo en C04. C05 permanece fuera del diff.

C04 abarca **catálogo de tenants, dominios verificados, shared/dedicated, contexto de identidad, sucursal/recurso, provisión, ciclo de vida, migraciones, colas/medios/cache/export y pruebas ofensivas**. Su certificación histórica (12/12) se limita a **arquitectura/contratos y fixtures MySQL desechables**; no acredita endpoints privados productivos, selección de DSN real, jobs, Redis, Cloudflare, copias de clientes ni despliegue.

## Hallazgos y correcciones

**C04-RQ-01 (P2, validación de datos y UX):** `TenantCatalogSnapshot.RequireCatalogName` rechaza `Control`, `Format`, `Surrogate`, marcadores HTML y diagonales, pero no rechazaba explícitamente los caracteres Unicode de categoría `LineSeparator` / `ParagraphSeparator` (p.ej. U+2028/U+2029). Estos valores pueden fragmentar etiquetas, tablas, exportaciones o trazabilidad administrativa sin pasar por un salto de línea tradicional. Se amplía la validación a ambas categorías, conservando Unicode legible y NFC.

**C04-RQ-02 (P2, protección contra regresiones):** los tests del catálogo C04-01 no incluían estos dos casos negativos. Se agregan ambos valores al mismo conjunto de pruebas para nombre visible e interno; una excepción no documentada debe fallar el gate. Auditoría C04-12 B constata que fuente y pruebas siguen presentes, pero **solo la ejecución .NET real confirma la denegación**.

**C04-RQ-03 (P2, continuidad entre capítulos):** el workflow integral C04-12 contenía sus regresiones previas, pero no ejecutaba los nuevos controles retrospectivos C03 sobre estado institucional, grafo real y estilo/accesibilidad de C01/C02. Se invocan `qa/c03_10_current_state_audits.py A/B/C` dentro de las puertas C04; el auditor C04 verifica que no se eliminen. Esto protege la referencia visual sin inventar pantallas C04.

## Impacto, límites y rollback

- `frontend/`: **sin cambios**. No existen nuevas interfaces comerciales C04 a rediseñar; se mantiene el contrato visual público de C01/C02 (foco, reduced-motion, paleta, saltos y rutas lazy) mediante regresión.
- `backend/`: se restringen exclusivamente nombres propios del catálogo. No se cambian permisos, configuración de tenants, catálogos privados, estado de sesión, autorización ni transacciones.
- `database/`: **sin cambios** de SQL, migraciones, datos o índices. El código C04 actual ejecuta nueve fixtures reales MySQL descartables y sus negativas dentro del workflow.
- `qa/` y `.github/workflows/`: A/B/C obligatorias, 20 contratos .NET y regresiones Angular22/Node24, Chromium y MySQL8.4 según workflow existente. La evidencia es por SHA/run; no considerar ningún `skipped` como `PASS`.
- No se modifica Documento Maestro Word/PDF, `main`, C03 histórico, C05, C06, SOLQARYN, producción ni proveedores externos.
- **Rollback:** revertir commits de este PR; ninguna reversión SQL corresponde. Antes de revertir validación de nombres, revisar si se admitieran valores con U+2028/U+2029 en datos de ensayo. No hay datos productivos afectados.

## Tres auditorías exigidas

| Puerta | Cobertura | Estado al redactar |
| --- | --- | --- |
| **A · funcional/trazabilidad** | Documento Maestro vigente, 12/12 microfases C04, matriz P0/P1, C03 integrado y referencias C01/C02 | No ejecutada en el SHA final |
| **B · seguridad/aislamiento** | Hosts, grants, DB shared/dedicated, BOLA/IDOR, provisión, lifecycle, migración, cache/job/media/export, pruebas negativas de separadores y los 9 SQL aislados | No ejecutada en el SHA final |
| **C · arquitectura/rendimiento/UX** | .NET10, Angular22, Chromium, contratos C03, diseño/accessibility de C01/C02 y pruebas MySQL/portabilidad vinculadas a mismo SHA/run | No ejecutada en el SHA final |

**NO-GO** ante fallo P0/P1 actual, alteración no autorizada de ámbito, error .NET, SQL, Chromium, pruebas necesarias pendientes, evidencias incongruentes o cambios que activen producción. Este corte no declara que C04 sea SaaS multitenant productivo ni que las pantallas de C05 hayan pasado inspección visual humana. Solo después del cierre correctivo de C04 podrá comenzar el trabajo de C05.
